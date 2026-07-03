
using AlarmModule.Interfaces;
using AlarmModule.Models;
using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using global::StationTasks.Services;
using MotionControl.Events;
using MotionControl.Exceptions;
using MotionControl.Interfaces;
using MotionControl.Models;
using MotionControl.Services;
using Prism.Events;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StationTasks.Tasks
{
    /// <summary>
    /// 基类，工艺步骤与信号交互
    /// </summary>
    public abstract class StationTaskBase : TaskBase, IStationMotionOperations
    {
        protected readonly IMotionService _motion;
        private readonly IPositionProvider _positionProvider;
        protected readonly IStationInteractionService _interaction;
        private readonly ISystemStateService _systemState;
        private readonly IStationRegistry _stationRegistry;
        private readonly ISpeedOverrideService _speedOverride;
        private readonly string _stationId;
        /// <summary> 本地化服务，供基类与子类日志多语言使用 </summary>
        private readonly ILocalizationService _localization;
        /// <summary> 暴露本地化服务，供 ProcessStepExecutor 等持有 StationTaskBase 引用的组件复用 </summary>
        public ILocalizationService Localization => _localization;
        /// <summary> 当前正在运动的轴ID集合（包含跨工站轴），暂停/停止时需要停止这些轴 </summary>
        private readonly HashSet<int> _activeMotionAxes = new HashSet<int>();
        /// <summary> 当前是否在手动操作模式中（ExecuteManualProcess 设置） </summary>
        private bool _isManualOperation;
        /// <summary>
        /// 工站标识，用于位置加载和信号交互
        /// </summary>
        public string StationId => _stationId;

        /// <summary>
        /// 工站标识值，用于匹配 hwcfg.xml 中 TaskConfig.Type
        /// 子类必须返回与 hwcfg.xml 中 type 属性一致的字符串
        /// </summary>
        public abstract string StationIdentifierValue { get; }

        /// <summary>
        /// 缓存从硬件配置动态发现的轴ID数组
        /// </summary>
        private int[] _discoveredAxes;

        /// <summary>
        /// 从硬件配置中发现属于当前工站的所有轴ID
        /// 基于 hwcfg.xml 中 AxisConfig.TaskId 和 TaskConfig.Type 的映射关系
        /// </summary>
        private int[] DiscoverAxes()
        {
            if (_discoveredAxes != null)
                return _discoveredAxes;

            var axisConfigs = Motion.GetAxisConfigurations();
            var taskConfigs = Motion.GetTaskConfigurations();

            // 通过 TaskConfig.Type 匹配当前工站的 StationIdentifierValue，找到 TaskId
            int? myTaskId = null;
            foreach (var tc in taskConfigs)
            {
                if (tc.Type == StationIdentifierValue)
                {
                    myTaskId = tc.TaskId;
                    break;
                }
            }

            if (myTaskId == null)
            {
                Logger.Warn(string.Format(_localization.GetResourceOrDefault("STB_Log_StationTypeNotFound", "[{0}] 未在硬件配置中找到工站类型 '{1}'"), TaskName, StationIdentifierValue));
                _discoveredAxes = Array.Empty<int>();
                return _discoveredAxes;
            }

            _discoveredAxes = axisConfigs
                .Where(a => a.TaskId == myTaskId.Value)
                .OrderBy(a => a.LogicalId)
                .Select(a => a.LogicalId)
                .ToArray();

            Logger.Info(string.Format(_localization.GetResourceOrDefault("STB_Log_AxesDiscovered", "[{0}] 从硬件配置发现 {1} 个轴: {2}"), TaskName, _discoveredAxes.Length, string.Join(", ", _discoveredAxes.Select(id => $"{GetAxisNameById(id)}({id})"))));
            return _discoveredAxes;
        }

        /// <summary>
        /// 获取当前工站配置的所有轴ID + 当前正在运动的跨工站轴
        /// 暂停/停止时需要停止所有这些轴以确保安全
        /// </summary>
        protected override int[] GetAllAxes()
        {
            var configured = DiscoverAxes();
            if (_activeMotionAxes.Count == 0)
                return configured;
            // 合并配置轴和当前运动轴（包括跨工站轴）
            var result = new HashSet<int>(configured);
            result.UnionWith(_activeMotionAxes);
            return result.ToArray();
        }

        /// <summary>
        /// 根据轴名称解析逻辑轴ID，从当前工站的轴配置中查找
        /// </summary>
        protected int ResolveAxisId(string axisName)
        {
            foreach (var axisId in GetAllAxes())
            {
                var state = Motion.GetAxisState(axisId);
                if (state != null && state.Name == axisName)
                    return axisId;
            }
            Logger.Warn(string.Format(_localization.GetResourceOrDefault("STB_Log_AxisConfigNotFound", "[{0}] 未找到轴 '{1}' 的配置"), TaskName, axisName));
            return -1;
        }
        /// <summary> 回零前逐轴上使能间隔（ms），避免多轴同时上使能冲击电流 </summary>
        protected const int InitAxisEnableDelayMs = 3000;

        /// <summary> 日志服务（公开给扩展方法和外部 Action 使用） </summary>
        public ILoggerService TaskLogger => Logger;
        /// <summary> 事件聚合器（公开给 ProcessStepExecutor 等外部类发布跨工站状态事件） </summary>
        public new IEventAggregator Ea => base.Ea;
        /// <summary> 工站注册表（公开给 ProcessStepExecutor 等外部类查找目标工站） </summary>
        public IStationRegistry StationRegistry => _stationRegistry;
        // 单步模式：每执行一步后暂停，等待 StepNext 信号才继续
        private TaskCompletionSource<bool> _stepTcs = new TaskCompletionSource<bool>();
        private volatile bool _singleStepMode;
        // 记录当前正在运行的步骤名
        public string CurrentStepName { get; private set; } = "Idle";

        /// <summary> 最近一次触发报警的步骤名（供步骤编辑器标记红色行背景），任务停止时自动清空 </summary>
        public string LastFaultStepName { get; set; }

        protected StationTaskBase(
                IMotionService motion,
                IPositionProvider positionProvider,
                IStationInteractionService interaction,
                IEventAggregator ea,
                ILoggerService logger,
                IAlarmService alarmService,
                ISystemStateService systemState,
                IStationRegistry stationRegistry,
                ISpeedOverrideService speedOverride,
                int taskId,
                string taskName,
                string stationId,
                ILocalizationService localization)
                : base(motion, ea, logger, alarmService, taskId, taskName)
        {
            _motion = motion;
            _positionProvider = positionProvider;
            _interaction = interaction;
            _systemState = systemState;
            _stationRegistry = stationRegistry;
            _speedOverride = speedOverride;
            _stationId = stationId;
            _localization = localization;
        }

        /// <summary>
        /// 任务循环入口：子类重写以实现具体工艺流程
        /// 默认实现直接返回，不执行任何操作
        /// </summary>
        protected override Task ExecuteCycleAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 执行自定义序列，供外部调用者（如 ProcessSequenceService）使用
        /// 复用 RunAsync 的状态管理和异常处理逻辑，但不进入循环
        /// </summary>
        public async Task RunCustomSequenceAsync(Func<CancellationToken, Task> sequence, CancellationToken token)
        {
            // 防止重复启动：任务已在运行时拒绝新序列
            if (State == TaskState.Running)
                throw new InvalidOperationException($"任务 [{TaskName}] 正在运行中，无法启动自定义序列");

            // 重置暂停信号：StopAsync 会取消 _pauseCts，若此处不重建，
            // PauseAwareToken 将因 _pauseCts 已取消而立即处于取消状态，
            // 导致 Task.Run(body, token) 跳过执行体直接返回取消 Task
            ResetMotionPause();

            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            State = TaskState.Running;
            PublishTaskStatusChanged("Running", State);
            Logger.Info(string.Format(_localization.GetResourceOrDefault("STB_Log_CustomSequenceStarted", "[{0}] 自定义序列启动"), TaskName));

            try
            {
                await sequence(_cts.Token);
                State = TaskState.Idle;
                PublishTaskStatusChanged("Completed", State);
                Logger.Info(string.Format(_localization.GetResourceOrDefault("STB_Log_CustomSequenceCompleted", "[{0}] 自定义序列完成"), TaskName));
            }
            catch (OperationCanceledException)
            {
                State = TaskState.Stopped;
                PublishTaskStatusChanged("Stopped", State);
                Logger.Info(string.Format(_localization.GetResourceOrDefault("STB_Log_CustomSequenceCancelled", "[{0}] 自定义序列已取消"), TaskName));
            }
            catch (StepFailureException sfe)
            {
                // 致命步骤故障：急停本任务并通知全局急停
                Logger.Error(string.Format(_localization.GetResourceOrDefault("STB_Log_FatalStepCrash", "致命故障，任务 [{0}] 在 [{1}] 步骤崩溃。内部异常: {2}"), TaskName, sfe.StepName, sfe.InnerException?.Message));
                State = TaskState.Error;
                await EmergencyStopAsync();
                Ea.GetEvent<EmergencyStopAllEvent>().Publish();
            }
            catch (Exception ex)
            {
                // 未知严重错误：急停本任务并通知全局急停
                Logger.Error(string.Format(_localization.GetResourceOrDefault("STB_Log_CustomSequenceError", "[{0}] 自定义序列执行错误: {1}"), TaskName, ex.Message));
                State = TaskState.Error;
                await EmergencyStopAsync();
                Ea.GetEvent<EmergencyStopAllEvent>().Publish();
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
            }
        }

        // ---------- 位置加载 ----------
        protected async Task PreloadPositionsAsync()
        {
            await _positionProvider.PreloadAsync();
        }

        /// <summary>强制从配方文件刷新位置缓存后再读取（运动前确保最新位置参数）</summary>
        protected async Task RefreshPositionsCacheAsync()
        {
            await _positionProvider.RefreshCacheAsync();
        }

        protected async Task<Dictionary<string, double>> LoadPositionsAsync()
        {
            return await _positionProvider.GetPositionsAsync(_stationId);
        }

        /// <summary>
        /// 根据位置名和轴名获取指定位置值；未找到时抛 PositionNotFoundException 中止运动（防撞机）。
        /// 异常会被 RunStep 的 catch(Exception) 致命分支捕获，触发 STEP_FATAL_ERROR + Serious 报警并中止流程。
        /// </summary>
        protected async Task<double> GetPositionAsync(string positionName, string axisName)
        {
            var (found, value) = await TryGetPositionAsync(positionName, axisName);
            if (!found)
                throw new PositionNotFoundException(positionName, axisName, _stationId);
            return value;
        }

        /// <summary>
        /// 尝试获取位置值（可选轴场景）：未找到返回 false 且 value=0，不抛异常。
        /// 用于工站内某些轴位置可选的场景（如 DispensingTask 的 Dz₂ 轴）。
        /// </summary>
        protected async Task<(bool found, double value)> TryGetPositionAsync(string positionName, string axisName)
        {
            var all = await LoadPositionsAsync();
            if (Core.Utilities.PositionLookupHelper.TryGetPositionValue(all, positionName, axisName, out var v))
                return (true, v);
            return (false, 0);
        }

        /// <summary>
        /// 公开的位置值查询方法，供 GotoStepAction 在格式化步骤标签时获取目标坐标
        /// </summary>
        public async Task<double> GetPositionValueAsync(string positionName, string axisName)
        {
            return await GetPositionAsync(positionName, axisName);
        }

        /// <summary>
        /// 公开的回零执行方法，供 GotoStepAction 在 HOME 步骤时调用
        /// 通过 RunStep 包装，享受暂停/急停/单步/可恢复异常保护
        /// </summary>
        public async Task ExecuteHomeAsync(int axisId, int mode = 1, double minVel = 5, double maxVel = 20)
        {
            // 注册当前运动轴，确保暂停/停止时能停止该轴
            _activeMotionAxes.Add(axisId);
            try
            {
                await RunStep($"Home Axis {axisId}", async () =>
                {
                    try
                    {
                        // 使用合并的暂停+停止令牌
                        await Motion.HomeAsync(axisId, mode, minVel, maxVel, PauseAwareToken);
                    }
                    catch (OperationCanceledException) when (_isPaused && !CurrentToken.IsCancellationRequested)
                    {
                        // 暂停导致回零中断，转换为 MotionPausedException
                        double actualPos = _motion.GetAxisState(axisId)?.ActualPosition ?? 0;
                        throw new MotionPausedException(axisId, 0, actualPos);
                    }
                }, publishStatus: false);
            }
            finally
            {
                // 回零结束，移除当前运动轴
                _activeMotionAxes.Remove(axisId);
            }
        }

        /// <summary>
        /// 使用控制卡已配置的回零参数执行回零（不覆盖 HomeMode/速度）。
        /// 与 ExecuteHomeAsync 相同的暂停/急停/_activeMotionAxes 安全保护。
        /// 当 SubMove.HomeMode == 0 时使用此方法。
        /// </summary>
        public async Task ExecuteHomeAxisAsync(int axisId)
        {
            _activeMotionAxes.Add(axisId);
            try
            {
                await RunStep($"Home Axis {axisId} (card config)", async () =>
                {
                    try
                    {
                        await Motion.HomeAxisAsync(axisId, PauseAwareToken);
                    }
                    catch (OperationCanceledException) when (_isPaused && !CurrentToken.IsCancellationRequested)
                    {
                        double actualPos = _motion.GetAxisState(axisId)?.ActualPosition ?? 0;
                        throw new MotionPausedException(axisId, 0, actualPos);
                    }
                }, publishStatus: false);
            }
            finally
            {
                _activeMotionAxes.Remove(axisId);
            }
        }

        /// <summary>
        /// 回零前逐轴上使能：每轴上使能后延时 InitAxisEnableDelayMs 再使能下一轴。
        /// axisId小于0的项跳过；onAxisEnableStarted 可用于更新 UI 进度。
        /// </summary>
        protected async Task EnableAxesSequentiallyAsync(
            IEnumerable<(int axisId, string axisName)> axes,
            Action<string> onAxisEnableStarted = null)
        {
            var validAxes = axes.Where(a => a.axisId >= 0).ToList();
            for (int i = 0; i < validAxes.Count; i++)
            {
                var (axisId, axisName) = validAxes[i];
                CurrentToken.ThrowIfCancellationRequested();

                onAxisEnableStarted?.Invoke(axisName);
                Logger.Info(string.Format(
                    _localization.GetResourceOrDefault("STB_Log_InitAxisEnabling", "[{0}] {1} 轴上使能..."),
                    TaskName, axisName));

                await Task.Run(() => _motion.EnableAxis(axisId), CurrentToken).ConfigureAwait(false);

                // 非最后一轴：延时后再上使能下一轴
                if (i < validAxes.Count - 1)
                    await Task.Delay(InitAxisEnableDelayMs, CurrentToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 查询指定轴是否已完成回零
        /// </summary>
        /// <param name="axisId">逻辑轴ID</param>
        /// <returns>true=已回零，false=未回零或异常</returns>
        public async Task<bool> IsAxisHomedAsync(int axisId)
        {
            try
            {
                int result = await Motion.CheckHomeDoneAsync(axisId);
                return result == 1;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ---------- 单步控制 ----------
        public void EnableSingleStep() => _singleStepMode = true;
        public void DisableSingleStep()
        {
            _singleStepMode = false;
            _stepTcs.TrySetResult(true);
        }
        public void StepNext()
        {
            if (_singleStepMode)
            {
                _stepTcs.TrySetResult(true);
            }
        }

        /// <summary> 在每一步操作后调用，若处于单步模式则阻塞直到 StepNext </summary>
        protected async Task WaitForStepAsync(CancellationToken token)
        {
            if (!_singleStepMode) return;

            _stepTcs = new TaskCompletionSource<bool>();
            Logger.Info(string.Format(_localization.GetResourceOrDefault("STB_Log_WaitingNextStep", "[{0}] waiting for next step..."), TaskName));

            try
            {
                var tcsTask = _stepTcs.Task;
                if (await Task.WhenAny(tcsTask, Task.Delay(Timeout.Infinite, token)) == tcsTask)
                {
                    return;
                }
            }
            catch (OperationCanceledException) { }
        }

        // ---------- 步骤执行器 ----------

        /// <summary>
        /// 根据步骤名称自动生成可读的报警代码
        /// stepName 格式为 "[1] GOTO → Home"，提取步骤类型生成如 "GOTO_FAULT"
        /// </summary>
        private static string GenerateAlarmCode(string stepName)
        {
            if (string.IsNullOrEmpty(stepName))
                return "STEP_FAULT";

            // stepName 格式: "[1] GOTO → Home" 或 "[3] SEEK"
            // 提取 ] 后面的步骤类型关键字
            int bracketEnd = stepName.IndexOf(']');
            if (bracketEnd < 0 || bracketEnd + 1 >= stepName.Length)
                return "STEP_FAULT";

            string afterBracket = stepName.Substring(bracketEnd + 1).Trim();
            // 取第一个空格前的部分作为步骤类型
            int spaceIdx = afterBracket.IndexOf(' ');
            string stepType = spaceIdx > 0 ? afterBracket.Substring(0, spaceIdx) : afterBracket;

            return $"{stepType}_FAULT";
        }

        /// <summary>
        /// 步骤执行包装器，提供暂停/急停/单步/可恢复异常保护
        /// alarmConfig：步骤级报警配置，启用时使用自定义报警代码和等级；为null时不触发报警
        /// </summary>
        protected internal async Task RunStep(string stepName, Func<Task> action, bool publishStatus = true, StepAlarmConfig alarmConfig = null)
        {
            CurrentStepName = stepName;
            var token = CurrentToken;
            if (publishStatus)
                PublishTaskStatusChanged(stepName, State);
            await CheckPauseAsync(token);
            if (_singleStepMode)
            {
                Logger.Info(string.Format(_localization.GetResourceOrDefault("STB_Log_SingleStepWait", "[{0}] 单步等待: {1}"), TaskName, stepName));
                _stepTcs = new TaskCompletionSource<bool>();
                await WhenAny(_stepTcs.Task, Task.Delay(Timeout.Infinite, token));
            }
            while (true) 
            {
                try
                {
                    Logger.Info(string.Format(_localization.GetResourceOrDefault("STB_Log_ExecutingStep", "[{0}] 执行步骤: {1}"), TaskName, stepName));
                    var sw = Stopwatch.StartNew();
                    await action();
                    sw.Stop();
                    Logger.Info(string.Format(_localization.GetResourceOrDefault("STB_Log_StepCompleted", "[{0}] 完成步骤: {1} (耗时: {2}ms)"), TaskName, stepName, sw.ElapsedMilliseconds));
                    LastFaultStepName = null;
                    break;
                }
                catch (OperationCanceledException) when (_isPaused && !CurrentToken.IsCancellationRequested)
                {
                    // 步骤 Action 直接调用 IMotionService 且使用 MotionCancellationToken 时，
                    // 暂停导致 WaitForDone 抛出 OCE（非 MotionPausedException），与暂停中断同等处理
                    await WaitForResumeAfterPauseAsync(stepName, token);
                    continue;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (RecoverableException rex)
                {
                    // MotionPausedException 是暂停导致的运动中断，不需要弹窗和报警
                    if (rex is MotionPausedException)
                    {
                        await WaitForResumeAfterPauseAsync(stepName, token);
                        continue;
                    }

                    // 其他 RecoverableException
                    Logger.Warn(string.Format(_localization.GetResourceOrDefault("STB_Log_RecoverableFault", "步骤 [{0}] 发生可恢复故障。原因: {1} | 建议: {2}"), stepName, rex.Message, rex.SuggestedAction));

                    LastFaultStepName = stepName;
                    Logger.Info(string.Format(_localization.GetResourceOrDefault("STB_Log_LastFaultStepSet", "[StationTaskBase] LastFaultStepName 设置为: {0}, alarmConfig.IsEnabled: {1}"), stepName, alarmConfig?.IsEnabled));

                    if (alarmConfig?.IsEnabled == true)
                    {
                        Ea.GetEvent<MotionControl.Events.StepFaultedEvent>().Publish(stepName);
                        Ea.GetEvent<MotionControl.Events.StepErrorEvent>().Publish(new MotionControl.Events.StepErrorPayload
                        {
                            StepName = stepName,
                            ErrorMessage = $"{rex.Message} | 建议: {rex.SuggestedAction}",
                            ErrorCode = !string.IsNullOrEmpty(alarmConfig.AlarmCode)
                                ? alarmConfig.AlarmCode : GenerateAlarmCode(stepName)
                        });

                        var alarmCode = string.IsNullOrEmpty(alarmConfig.AlarmCode)
                            ? GenerateAlarmCode(stepName)
                            : alarmConfig.AlarmCode;
                        var alarmLevel = (AlarmLevel)(alarmConfig.AlarmLevel > 0 ? alarmConfig.AlarmLevel : 3);

                        _ = AlarmService.TriggerAlarmAsync(
                            alarmCode,
                            alarmLevel,
                            $"步骤 [{stepName}] 异常: {rex.Message} | 建议: {rex.SuggestedAction}",
                            source: $"{TaskName}.{stepName}",
                            type: AlarmType.ProcessError);
                    }

                    // 手动操作：不弹窗（避免 RecoverableFaultDialog），只记录日志
                    // 异常向上传播到 ExecuteManualProcess → ViewModel catch → ShowHintMessage(CustomDialog)
                    if (_isManualOperation)
                    {
                        Logger.Info(string.Format(_localization.GetResourceOrDefault("STB_Log_ManualOpFaultPropagated", "步骤 [{0}] 手动操作故障，异常向上传播至 ViewModel 处理"), stepName));
                        throw; // 让 ExecuteManualProcess 的 catch(Exception) 处理
                    }

                    // 自动运行：保持原有弹窗+暂停恢复逻辑
                    PublishRecoverableFault(stepName, rex, isManualOperation: false);
                    _systemState.RequestPause();
                    await PauseAsync();
                    PublishTaskStatusChanged(stepName, State);
                    try
                    {
                        await CheckPauseAsync(token);
                    }
                    catch (OperationCanceledException)
                    {
                        Logger.Info(string.Format(_localization.GetResourceOrDefault("STB_Log_OperatorStopTask", "步骤 [{0}] 操作员选择停止任务"), stepName));
                        throw;
                    }
                    if (State != TaskState.Running)
                    {
                        Logger.Info(string.Format(_localization.GetResourceOrDefault("STB_Log_TaskNotResumedCancelStep", "步骤 [{0}] 任务未恢复运行，取消当前步骤"), stepName));
                        throw new OperationCanceledException(token);
                    }
                    PublishTaskStatusChanged(stepName, State);
                    Logger.Info(string.Format(_localization.GetResourceOrDefault("STB_Log_ResumedRetryStep", "步骤 [{0}] 已恢复运行，将重新执行当前步骤..."), stepName));
                }
                catch (Exception ex)
                {
                    Logger.Error(string.Format(_localization.GetResourceOrDefault("STB_Log_StepFatalError", "[{0}] 步骤 [{1}] 致命异常: {2}"), TaskName, stepName, ex.Message));

                    // 致命异常也触发报警和步骤故障标记，确保操作员能看到具体错误
                    LastFaultStepName = stepName;
                    Ea.GetEvent<MotionControl.Events.StepFaultedEvent>().Publish(stepName);
                    Ea.GetEvent<MotionControl.Events.StepErrorEvent>().Publish(new MotionControl.Events.StepErrorPayload
                    {
                        StepName = stepName,
                        ErrorMessage = ex.InnerException?.Message ?? ex.Message,
                        ErrorCode = "STEP_FATAL_ERROR"
                    });

                    _ = AlarmService.TriggerAlarmAsync(
                        "STEP_FATAL_ERROR",
                        AlarmLevel.Serious,
                        $"步骤 [{stepName}] 致命异常: {ex.InnerException?.Message ?? ex.Message}",
                        source: $"{TaskName}.{stepName}",
                        type: AlarmType.ProcessError);

                    throw new StepFailureException(stepName, ex);
                }
            }
            if (_singleStepMode)
            {
                _stepTcs = new TaskCompletionSource<bool>();
            }
        }

        /// <summary>
        /// 暂停中断后等待操作员恢复；恢复后由 RunStep 重试当前步骤。
        /// </summary>
        private async Task WaitForResumeAfterPauseAsync(string stepName, CancellationToken token)
        {
            Logger.Info(string.Format(_localization.GetResourceOrDefault("STB_Log_StepInterruptedByPause", "步骤 [{0}] 因暂停中断，等待恢复后重试"), stepName));
            LastFaultStepName = stepName;
            _systemState.RequestPause();
            // 已在 CancelMotionPause 中暂停，但为确保状态一致再次确认
            if (State != TaskState.Paused)
                await PauseAsync();
            PublishTaskStatusChanged(stepName, State);
            try
            {
                await CheckPauseAsync(token);
            }
            catch (OperationCanceledException)
            {
                Logger.Info(string.Format(_localization.GetResourceOrDefault("STB_Log_OperatorStopTask", "步骤 [{0}] 操作员选择停止任务"), stepName));
                throw;
            }
            if (State != TaskState.Running)
            {
                Logger.Info(string.Format(_localization.GetResourceOrDefault("STB_Log_TaskNotResumedCancelStep", "步骤 [{0}] 任务未恢复运行，取消当前步骤"), stepName));
                throw new OperationCanceledException(token);
            }
            PublishTaskStatusChanged(stepName, State);
            Logger.Info(string.Format(_localization.GetResourceOrDefault("STB_Log_ResumedRetryStep", "步骤 [{0}] 已恢复运行，将重新执行当前步骤..."), stepName));
        }

        /// <summary>
        /// 辅助方法：等待 Tasks中的任意一个完成
        /// </summary>
        private async Task WhenAny(Task t1, Task t2)
        {
            try
            {
                await Task.WhenAny(t1, t2);
            }
            catch (OperationCanceledException) { }
        }
        // ---------- IO 快捷操作 ----------
        public bool ReadDI(int logicalId) => _motion.ReadDi(logicalId);
        public void WriteDO(int logicalId, bool value) => _motion.WriteDo(logicalId, value);

        /// <summary>
        /// 从 hwconfig 解析 DO 端口逻辑 ID（按端口名称查找输出配置）
        /// </summary>
        /// <param name="portName">hwconfig 中定义的 DO 端口名称</param>
        /// <returns>逻辑 ID，未找到返回 -1</returns>
        protected int GetDoLogicalId(string portName)
        {
            var outputs = Motion.GetOutputConfigurations();
            var config = outputs.FirstOrDefault(o => o.Name == portName);
            if (config == null)
                Logger.Warn(string.Format(_localization.GetResourceOrDefault("STB_Log_DOPortNotFound", "[{0}] 未找到 DO 端口配置 '{1}'"), TaskName, portName));
            return config?.LogicalId ?? -1;
        }

        /// <summary>
        /// 从 hwconfig 解析 DI 端口逻辑 ID（按端口名称查找输入配置）
        /// </summary>
        /// <param name="portName">hwconfig 中定义的 DI 端口名称</param>
        /// <returns>逻辑 ID，未找到返回 -1</returns>
        protected int GetDiLogicalId(string portName)
        {
            var inputs = Motion.GetInputConfigurations();
            var config = inputs.FirstOrDefault(o => o.Name == portName);
            if (config == null)
                Logger.Warn(string.Format(_localization.GetResourceOrDefault("STB_Log_DIPortNotFound", "[{0}] 未找到 DI 端口配置 '{1}'"), TaskName, portName));
            return config?.LogicalId ?? -1;
        }

        // ---------- 信号交互 ----------
        /// <summary>
        /// 向指定工位发送信号
        /// </summary>
        protected void SignalToStation(string stationName, string signalName, bool value)
        {
            _interaction.SetSignal($"{stationName}.{signalName}", value);
        }
        /// <summary>
        /// 等待其他工位信号到达，超时则返回 false
        /// </summary>
        protected bool WaitForSignal(string stationName, string signalName, bool expectedValue, int timeoutMs = -1)
        {
            return _interaction.WaitForSignal($"{stationName}.{signalName}", expectedValue, timeoutMs);
        }
        /// <summary>
        /// 异步等待信号到达，超时则抛出可恢复异常
        /// </summary>
        protected async Task WaitForSignalAsync(string stationName, string signalName, bool expectedValue, int timeoutMs = 5000)
        {
            var signalFullName = $"{stationName}.{signalName}";
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (_interaction.GetSignal(signalFullName) != expectedValue)
            {
                if (stopwatch.ElapsedMilliseconds > timeoutMs)
                {
                    throw new RecoverableException(
                        message: $"等待信号 [{signalFullName}] 超时 ({timeoutMs}ms)，当前值为 {!expectedValue}",
                        suggestedAction: $"请检查工位 [{stationName}] 的 [{signalName}] 传感器是否被遮挡或损坏，复位后点击恢复运行。"
                    );
                }
                await Task.Delay(50, CurrentToken);
            }
        }

        // ==========  高频动作糖衣语法 ==========
        /// <summary> 延迟等待(自动携带取消令牌) </summary>
        protected async Task WaitTime(int ms) => await Task.Delay(ms, CurrentToken);
        /// <summary> 快速移动到配方中的指定点位 (单轴) </summary>
        protected async Task MoveToAsync(int axisId, string positionName, double velocity)
        {
            string axisName = GetAxisNameById(axisId);
            var pos = await GetPositionAsync(positionName, axisName);
            var actualVelocity = velocity * (_speedOverride.SpeedPercent / 100.0);
            await _motion.MoveAbsAsync(axisId, pos, actualVelocity, CurrentToken);
        }
        /// <summary>
        /// 触发本地 DO 信号 (气缸/夹爪)，可选择是否检测 DI 反馈
        /// </summary>
        public async Task TriggerCylinderAsync(int doId, bool value, int diId = -1, int timeoutMs = 3000, int blindDelayMs = 300)
        {
            WriteDO(doId, value);
            if (diId >= 0)
            {
                await WaitForDiAsync(diId, value, timeoutMs);
            }
            else
            {
                await WaitTime(blindDelayMs);
            }
        }
        /// <summary>
        /// 等待本地 DI 信号到达指定状态
        /// </summary>
        protected async Task WaitForDiAsync(int logicalId, bool expectedValue, int timeoutMs = 3000)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (ReadDI(logicalId) != expectedValue)
            {
                if (stopwatch.ElapsedMilliseconds > timeoutMs)
                {
                    throw new RecoverableException(
                        message: $"等待本地 DI[{logicalId}] 变为 {expectedValue} 超时 ({timeoutMs}ms)",
                        suggestedAction: $"请检查 DI[{logicalId}] 传感器是否异常或气缸动作是否卡滞，复位后重试。"
                    );
                }
                await Task.Delay(20, CurrentToken);
            }
        }
        /// <summary>
        /// 根据逻辑轴号获取配置中的轴名称 (例如 9 -> "Y", 10 -> "U")
        /// </summary>
        public virtual string GetAxisNameById(int axisId)
        {
            var axisState = Motion.GetAxisState(axisId);
            if (axisState != null && !string.IsNullOrEmpty(axisState.Name))
            {
                return axisState.Name;
            }
            Logger.Warn(string.Format(_localization.GetResourceOrDefault("STB_Log_AxisNameNotFound", "未找到轴 {0} 的名称配置，将使用 ID 作为配方 Key。"), axisId));
            return axisId.ToString();
        }
        // ========== 手动操作流程机制 ==========
        private readonly SemaphoreSlim _manualLock = new(1, 1);
        /// <summary>
        /// 执行一段手动流程（供UI或外部调用），复用 RunStep 的安全保护
        /// </summary>
        public async Task ExecuteManualProcess(string processName, Func<Task> action)
        {
            if (State == TaskState.Running) return;
            await _manualLock.WaitAsync();
            try
            {
                _isManualOperation = true;
                State = TaskState.Running;
                PublishTaskStatusChanged($"[手动]{processName}", State);
                await RunStep($"[手动]{processName}", action);
                State = TaskState.Idle;
                PublishTaskStatusChanged("Completed", State);
            }
            catch (OperationCanceledException)
            {
                State = TaskState.Stopped;
                PublishTaskStatusChanged("Stopped", State);
            }
            catch (Exception)
            {
                State = TaskState.Error;
                PublishTaskStatusChanged("Error", State);
                throw;
            }
            finally
            {
                _isManualOperation = false;
                _manualLock.Release();
            }
        }

        // ========== 带偏移量的移动方法 ==========
        /// <summary>
        /// 带偏移量的移动方法（公开给 GotoStepAction 调用）
        /// 解析配方位置名 -> 获取位置值 -> 叠加偏移 -> 执行绝对运动
        /// </summary>
        public async Task ExecuteMoveAsync(int axisId, string positionName, double velocity, double offset = 0)
        {
            string axisName = GetAxisNameById(axisId);
            var pos = await GetPositionAsync(positionName, axisName);
            pos += offset;
            var actualVelocity = velocity * (_speedOverride.SpeedPercent / 100.0);

            // 注册当前运动轴（包含跨工站轴），确保暂停/停止时能停止该轴
            _activeMotionAxes.Add(axisId);
            try
            {
                // 使用合并的暂停+停止令牌，暂停时 _pauseCts 取消可立即中断 WaitForDone
                await _motion.MoveAbsAsync(axisId, pos, actualVelocity, PauseAwareToken);
            }
            catch (OperationCanceledException) when (_isPaused && !CurrentToken.IsCancellationRequested)
            {
                // 暂停导致的取消（_pauseCts取消），而非停止导致的取消（_cts取消）
                // 转换为 MotionPausedException，被 RunStep 的 RecoverableException 分支捕获
                double actualPos = _motion.GetAxisState(axisId)?.ActualPosition ?? 0;
                throw new MotionPausedException(axisId, pos, actualPos);
            }
            finally
            {
                // 运动结束（无论成功/暂停/停止），移除当前运动轴
                _activeMotionAxes.Remove(axisId);
            }
        }

        /// <summary> 根据轴名称查找逻辑轴ID，未找到返回 -1 </summary>
        public int FindAxisIdByName(string axisName)
        {
            if (string.IsNullOrEmpty(axisName)) return -1;

            foreach (var axisId in GetAllAxes())
            {
                var state = Motion.GetAxisState(axisId);
                if (state != null && state.Name == axisName)
                {
                    return axisId;
                }
            }

            Logger.Warn(string.Format(_localization.GetResourceOrDefault("STB_Log_AxisConfigByNameNotFound", "未找到名称为 '{0}' 的轴配置"), axisName));
            return -1;
        }

        /// <summary>
        /// 公开的步骤执行包装器，供 ProcessStepExecutor 等外部类调用
        /// 内部委托给 RunStep，享受暂停/急停/单步/可恢复异常保护
        /// alarmConfig：步骤级报警配置，启用时使用自定义报警代码和等级
        /// </summary>
        public async Task ExecuteStepSafeAsync(string stepName, Func<Task> action, bool publishStatus = true, StepAlarmConfig alarmConfig = null)
        {
            await RunStep(stepName, action, publishStatus, alarmConfig);
        }

        /// <summary>
        /// 公开步骤状态发布方法，供 GotoStepAction 在跨工站执行时通知目标工站的监控栏
        /// </summary>
        /// <param name="stepName">步骤名称</param>
        /// <param name="overrideState">覆盖状态：跨工站执行时传入源工站的运行状态，避免用目标工站自身的空闲状态覆盖</param>
        public void PublishStepStatus(string stepName, TaskState? overrideState = null)
        {
            PublishTaskStatusChanged(stepName, overrideState ?? State);
        }

        /// <summary>
        /// 标记当前步骤已完成，供 GotoStepAction 在 SubMove 执行完毕后通知目标工站监控栏
        /// </summary>
        /// <param name="overrideState">覆盖状态：跨工站执行时传入源工站的运行状态</param>
        public void CompleteStepStatus(TaskState? overrideState = null)
        {
            Ea.GetEvent<TaskStatusChangedEvent>().Publish(new TaskStatusPayload
            {
                TaskId = TaskId,
                TaskName = TaskName,
                State = overrideState ?? State,
                CurrentStepName = "",
                IsStepCompleted = true
            });
        }
    }
}
