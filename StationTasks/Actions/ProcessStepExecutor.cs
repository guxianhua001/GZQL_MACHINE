using AlarmModule.Interfaces;
using AlarmModule.Models;
using Core.Abstraction;
using Core.Models;
using Core.Services;
using Core.Utilities;
using MotionControl.Events;
using MotionControl.Exceptions;
using MotionControl.Interfaces;
using MotionControl.Models;
using Prism.Events;
using Recipe.Interfaces;
using StationTasks.Models;
using StationTasks.Tasks;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StationTasks.Actions
{
    /// <summary>
    /// 工艺步骤序列执行器：按顺序执行 ProcessStep 列表，
    /// 支持 GOTO 移动、CHECK 条件跳转、单步/暂停/急停保护
    /// 跨工站执行时，向所有涉及的目标工站发布 Running/Completed 状态事件
    /// </summary>
    public class ProcessStepExecutor
    {
        private readonly StationTaskBase _task;
        private readonly ILoggerService _logger;
        private readonly Dictionary<StepType, IProcessStepAction> _actionMap;
        private readonly IEventAggregator _ea;
        private readonly IStationRegistry _stationRegistry;
        private readonly IAlarmService _alarmService;
        private readonly IFormulaEvaluator _formulaEvaluator;
        private readonly IRecipePoolService _recipePoolService;
        private readonly IRunTaskExecutor _runTaskExecutor;

        /// <summary>
        /// 调用栈：用于 RUNTASK 步骤的循环引用检测。
        /// 每层任务调用压入任务名，执行完成后弹出。由外部调用方设置。
        /// </summary>
        public Stack<string> CallStack { get; set; } = new Stack<string>();

        /// <summary> 步骤输出参数累积字典，供 SCRIPT 步骤读取前序步骤输出 </summary>
        private Dictionary<string, string> _stepOutputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary> 步骤标签→步骤对象映射，用于事件回调时快速查找 </summary>
        private Dictionary<string, ProcessStep> _stepLookup;

        /// <summary>
        /// 单步模式门控：每步执行后调用，等待用户确认“下一步”后才继续。
        /// 若为 null 表示不启用单步模式。
        /// </summary>
        public Func<CancellationToken, Task> StepGate { get; set; }

        public ProcessStepExecutor(
            StationTaskBase task,
            ILoggerService logger,
            IEnumerable<IProcessStepAction> actions,
            IAlarmService alarmService,
            IFormulaEvaluator formulaEvaluator,
            IRecipePoolService recipePoolService,
            IRunTaskExecutor runTaskExecutor = null)
        {
            _task = task;
            _logger = logger;
            _alarmService = alarmService;
            _formulaEvaluator = formulaEvaluator;
            _recipePoolService = recipePoolService;
            _runTaskExecutor = runTaskExecutor;

            _ea = task.Ea;
            _stationRegistry = task.StationRegistry;

            _actionMap = new Dictionary<StepType, IProcessStepAction>();
            foreach (var action in actions)
            {
                _actionMap[action.SupportedStepType] = action;
            }

            // 订阅步骤报警事件：RunStep 触发 RecoverableException 时立即发布，此处立即标记红色行背景
            _ea.GetEvent<MotionControl.Events.StepFaultedEvent>().Subscribe(OnStepFaulted);
            _ea.GetEvent<MotionControl.Events.StepErrorEvent>().Subscribe(OnStepError);
        }

        /// <summary> RunStep 触发报警时的回调，仅当步骤启用了报警配置时标记红色 </summary>
        private void OnStepFaulted(string stepName)
        {
            if (_stepLookup == null) return;
            if (_stepLookup.TryGetValue(stepName, out var step))
            {
                if (step.AlarmConfig?.IsEnabled == true)
                {
                    _logger.Info($"[ProcessStepExecutor] 事件触发: 步骤 [{step.Seq}] {step.Step} 立即设置 HasActiveAlarm = true");
                    step.HasActiveAlarm = true;
                }
            }
        }

        /// <summary> 步骤错误事件回调，仅当步骤启用了报警配置时设置 ErrorMessage </summary>
        private void OnStepError(MotionControl.Events.StepErrorPayload payload)
        {
            if (_stepLookup == null) return;
            if (_stepLookup.TryGetValue(payload.StepName, out var step))
            {
                if (step.AlarmConfig?.IsEnabled == true)
                {
                    _logger.Info($"[ProcessStepExecutor] 步骤错误事件: [{step.Seq}] {step.Step} ErrorCode={payload.ErrorCode}");
                    step.ErrorMessage = $"[{payload.ErrorCode}] {payload.ErrorMessage}";
                }
            }
        }

        /// <summary>
        /// 执行工艺步骤序列，支持条件跳转和步骤追踪
        /// 跨工站执行时，向所有涉及的目标工站发布 Running/Idle 状态事件
        /// </summary>
        public async Task ExecuteAsync(ObservableCollection<ProcessStep> steps, CancellationToken token)
        {
            if (steps == null || steps.Count == 0)
            {
                _logger.Warn("工艺步骤列表为空，跳过执行");
                return;
            }

            // 每轮执行前重置故障步骤标记，避免上一轮残留影响本轮判断
            _task.LastFaultStepName = null;

            // 每轮执行前重置步骤输出参数
            _stepOutputs.Clear();

            // 收集所有涉及的目标工站（排除主工站自身）
            var targetStations = CollectTargetStations(steps);

            // 向所有目标工站发布 Running 状态
            foreach (var station in targetStations)
            {
                PublishStateToStation(station, "Running", TaskState.Running);
            }

            // 清除所有步骤的 IsCurrent 标记
            foreach (var s in steps)
                s.IsCurrent = false;

            // 构建步骤标签→步骤对象映射，供 StepFaultedEvent 回调使用
            _stepLookup = new Dictionary<string, ProcessStep>();
            foreach (var step in steps)
            {
                var label = FormatStepLabel(step);
                if (!string.IsNullOrEmpty(label))
                    _stepLookup[label] = step;
            }

            try
            {
                int currentIndex = 0;

                while (currentIndex >= 0 && currentIndex < steps.Count)
                {
                    token.ThrowIfCancellationRequested();

                    var step = steps[currentIndex];

                    // 跳过禁用步骤（不执行、不标记、直接进入下一步）
                    if (!step.IsEnabled)
                    {
                        _logger.Info($"[ProcessStepExecutor] 跳过禁用步骤: [{step.Seq}] {step.Step}");
                        currentIndex++;
                        continue;
                    }

                    // 标记当前步骤
                    step.IsCurrent = true;
                    _logger.Info($"=== 执行步骤 [{step.Seq}] {step.Step} ({step.CompFeature} → {step.SiteFeature}) ===");

                    try
                    {
                        var sw = Stopwatch.StartNew();
                        int nextIndex = await ExecuteSingleStepAsync(step, steps, currentIndex, token);
                        sw.Stop();
                        step.LastElapsedMs = sw.ElapsedMilliseconds;
                        step.IsCurrent = false;
                    
                        // 单步模式：步骤执行完成后高亮下一步并等待用户确认
                        if (StepGate != null && nextIndex >= 0 && nextIndex < steps.Count)
                        {
                            // 提前高亮下一步，避免等待期间无高亮行
                            steps[nextIndex].IsCurrent = true;
                            _logger.Info($"[ProcessStepExecutor] 单步模式等待下一步确认 (下一步骤 [{steps[nextIndex].Seq}])");
                            await StepGate(token);
                            // 用户确认后，下一轮循环会重新设置 IsCurrent，无需在此清除
                            _logger.Info("[ProcessStepExecutor] 单步模式收到下一步确认");
                        }
                    
                        currentIndex = nextIndex;
                    }
                    catch (OperationCanceledException)
                    {
                        step.IsCurrent = false;
                        _logger.Info($"[ProcessStepExecutor] 步骤 [{step.Seq}] {step.Step} 捕获 OperationCanceledException, LastFaultStepName: {_task.LastFaultStepName}, IsEnabled: {step.AlarmConfig?.IsEnabled}");
                        if (_task.LastFaultStepName != null && step.AlarmConfig?.IsEnabled == true)
                        {
                            _logger.Info($"[ProcessStepExecutor] 步骤 [{step.Seq}] {step.Step} 设置 HasActiveAlarm = true (OperationCanceledException 路径)");
                            step.HasActiveAlarm = true;
                        }
                        throw;
                    }
                    catch (Exception ex)
                    {
                        step.IsCurrent = false;
                        _logger.Info($"[ProcessStepExecutor] 步骤 [{step.Seq}] {step.Step} 捕获 Exception: {ex.Message}, IsEnabled: {step.AlarmConfig?.IsEnabled}");
                        if (step.AlarmConfig?.IsEnabled == true)
                        {
                            _logger.Info($"[ProcessStepExecutor] 步骤 [{step.Seq}] {step.Step} 设置 HasActiveAlarm = true (Exception 路径)");
                            step.HasActiveAlarm = true;
                        }
                        _logger.Error($"[ProcessSequence] 步骤 [{step.Seq}] {step.Step} 执行异常: {ex.Message}");
                        currentIndex = -1;
                    }
                }

                _logger.Info("=== 工艺步骤序列执行完成 ===");
            }
            finally
            {
                // 清理步骤映射，防止事件回调引用过期对象
                _stepLookup = null;

                // 停止/取消/异常时重置所有步骤高亮，回到第一步
                foreach (var s in steps)
                    s.IsCurrent = false;
                if (steps.Count > 0)
                    steps[0].IsCurrent = true;

                // 向所有目标工站发布 Idle 状态（序列结束）
                foreach (var station in targetStations)
                {
                    PublishStateToStation(station, "Completed", TaskState.Idle);
                }
            }
        }

        /// <summary>
        /// 单独执行指定步骤（用于步骤编辑器中的调试运行），享受暂停/急停/可恢复异常保护
        /// </summary>
        public async Task ExecuteSingleStepAsync(ProcessStep step, CancellationToken token)
        {
            if (step == null) return;

            var action = _actionMap.TryGetValue(step.Step, out var a) ? a : null;
            if (action == null)
            {
                if (step.Step == StepType.RUNTASK)
                    _logger.Warn($"步骤 [{step.Seq}] RUNTASK 类型不支持单独执行，请在任务序列中运行");
                else
                    _logger.Warn($"步骤 [{step.Seq}] 类型 {step.Step} 没有注册的 Action，无法单独执行");
                return;
            }

            if (action is ScriptStepAction scriptAction)
                scriptAction.StepOutputs = _stepOutputs;

            string stepLabel = FormatStepLabel(step);
            _logger.Info($"=== 单独执行步骤 [{step.Seq}] {step.Step} ===");

            bool publishStatus = step.Step != StepType.GOTO;
            var sw = Stopwatch.StartNew();
            await _task.ExecuteStepSafeAsync(stepLabel, async () =>
            {
                await action.ExecuteAsync(step, _task, token);
            }, publishStatus, step.AlarmConfig);
            sw.Stop();
            step.LastElapsedMs = sw.ElapsedMilliseconds;

            _logger.Info($"=== 单独执行步骤 [{step.Seq}] {step.Step} 完成 ===");
        }

        /// <summary>
        /// 收集步骤中涉及的所有目标工站（排除主工站自身）
        /// </summary>
        private List<StationTaskBase> CollectTargetStations(ObservableCollection<ProcessStep> steps)
        {
            var stations = new List<StationTaskBase>();
            var seenIds = new HashSet<string>();
            seenIds.Add(_task.StationId);

            foreach (var step in steps)
            {
                if (step.SubMoves == null) continue;
                foreach (var move in step.SubMoves)
                {
                    if (string.IsNullOrEmpty(move.StationId)) continue;
                    if (seenIds.Contains(move.StationId)) continue;

                    var provider = _stationRegistry.GetStation(move.StationId);
                    if (provider is StationTaskBase task)
                    {
                        stations.Add(task);
                        seenIds.Add(move.StationId);
                    }
                }
            }

            return stations;
        }

        /// <summary>
        /// 向指定工站发布状态变更事件，通知 TaskMonitorView 刷新显示
        /// </summary>
        private void PublishStateToStation(StationTaskBase station, string stepName, TaskState state)
        {
            _ea.GetEvent<TaskStatusChangedEvent>().Publish(new TaskStatusPayload
            {
                TaskId = station.TaskId,
                TaskName = station.TaskName,
                State = state,
                CurrentStepName = stepName
            });
        }

        /// <summary>
        /// 格式化步骤标签，HOME 步骤显示 "→ Home"，GOTO 步骤追加位置名称
        /// </summary>
        private string FormatStepLabel(ProcessStep step)
        {
            string label = $"[{step.Seq}] {step.Step}";

            if (string.Equals(step.SiteFeature, "HOME", StringComparison.OrdinalIgnoreCase))
            {
                label += " → Home";
            }
            else if (step.Step == StepType.GOTO && step.SubMoves?.Count > 0)
            {
                var posNames = step.SubMoves
                    .Where(sm => !string.IsNullOrEmpty(sm.PositionName))
                    .Select(sm => sm.PositionName)
                    .Distinct()
                    .Take(3);
                var posText = string.Join(", ", posNames);
                if (!string.IsNullOrEmpty(posText))
                    label += $" → {posText}";
            }

            return label;
        }

        /// <summary>
        /// 执行单个步骤，返回下一步的索引
        /// </summary>
        private async Task<int> ExecuteSingleStepAsync(
            ProcessStep step,
            ObservableCollection<ProcessStep> steps,
            int currentIndex,
            CancellationToken token)
        {
            string stepLabel = FormatStepLabel(step);

            switch (step.Step)
            {
                case StepType.GOTO:
                case StepType.VISION:
                case StepType.SCAN:
                case StepType.DASHBOARD:
                case StepType.SEEK:
                case StepType.WAIT:
                case StepType.SCRIPT:
                case StepType.PICK:
                case StepType.CURE:
                case StepType.DISPENSE:
                case StepType.RELEASE:
                    _logger.Info($"[ProcessStepExecutor] 开始执行步骤 [{step.Seq}] {step.Step}, stepLabel: {stepLabel}");
                    await ExecuteWithRunStepAsync(stepLabel, step, token);
                    _logger.Info($"[ProcessStepExecutor] 步骤 [{step.Seq}] {step.Step} 完成, LastFaultStepName: {_task.LastFaultStepName}, IsEnabled: {step.AlarmConfig?.IsEnabled}");
                    if (_task.LastFaultStepName == stepLabel && step.AlarmConfig?.IsEnabled == true)
                    {
                        _logger.Info($"[ProcessStepExecutor] 步骤 [{step.Seq}] {step.Step} 设置 HasActiveAlarm = true (LastFaultStepName 匹配路径)");
                        step.HasActiveAlarm = true;
                    }
                    return currentIndex + 1;

                case StepType.RUNTASK:
                {
                    // RUNTASK 步骤：调用被动任务，通过 CallStack 进行循环引用检测
                    _logger.Info($"[ProcessStepExecutor] 开始执行 RUNTASK 步骤 [{step.Seq}], 目标任务: {step.RunTaskDetail?.TargetTaskName}");
                    if (_runTaskExecutor == null)
                    {
                        _logger.Warn($"[ProcessStepExecutor] RUNTASK 步骤 [{step.Seq}] 未注入 IRunTaskExecutor，跳过");
                        return currentIndex + 1;
                    }
                    if (step.RunTaskDetail == null || string.IsNullOrEmpty(step.RunTaskDetail.TargetTaskName))
                    {
                        _logger.Warn($"[ProcessStepExecutor] RUNTASK 步骤 [{step.Seq}] 未配置目标任务，跳过");
                        return currentIndex + 1;
                    }
                    // 通过 ExecuteStepSafeAsync 包装，享受暂停/急停/可恢复异常保护
                    await _task.ExecuteStepSafeAsync(stepLabel, async () =>
                    {
                        await _runTaskExecutor.ExecutePassiveTaskAsync(
                            step.RunTaskDetail.TargetTaskName,
                            _task,
                            CallStack,
                            token);
                    }, true, step.AlarmConfig);
                    _logger.Info($"[ProcessStepExecutor] RUNTASK 步骤 [{step.Seq}] 完成");
                    return currentIndex + 1;
                }

                case StepType.BRANCH:
                    _logger.Info($"[ProcessStepExecutor] 开始执行 BRANCH 步骤 [{step.Seq}]");
                    if (step.BranchConfig?.IsEnabled == true)
                    {
                        return await ExecuteBranchLogicAsync(step, steps, currentIndex, token);
                    }
                    _logger.Warn($"[Branch] 步骤 [{step.Seq}] BranchConfig 未启用，继续下一步");
                    return currentIndex + 1;

                case StepType.IF:
                    _logger.Info($"[ProcessStepExecutor] 开始执行 IF 步骤 [{step.Seq}]");
                    await ExecuteIfStepAsync(step, token);
                    _logger.Info($"[ProcessStepExecutor] IF 步骤 [{step.Seq}] 完成");
                    return currentIndex + 1;

                default:
                    _logger.Warn($"步骤类型 {step.Step} 尚未实现执行器，跳过步骤 [{step.Seq}]");
                    return currentIndex + 1;
            }
        }

        /// <summary>
        /// 通过 RunStep 包装执行步骤动作，享受暂停/急停/单步/可恢复异常保护
        /// 将步骤的 AlarmConfig 传递到 RunStep，实现步骤级自定义报警
        /// </summary>
        private async Task ExecuteWithRunStepAsync(string stepLabel, ProcessStep step, CancellationToken token)
        {
            if (_actionMap.TryGetValue(step.Step, out var action))
            {
                // SCRIPT 步骤注入步骤输出参数
                if (action is ScriptStepAction scriptAction)
                {
                    scriptAction.StepOutputs = _stepOutputs;
                }

                bool publishStatus = step.Step != StepType.GOTO;
                await _task.ExecuteStepSafeAsync(stepLabel, async () =>
                {
                    await action.ExecuteAsync(step, _task, token);
                }, publishStatus, step.AlarmConfig);

                // 步骤执行完成后，收集输出参数到累积字典，并写入目标全局变量
                if (step.BranchConfig?.OutputParameters != null)
                {
                    foreach (var output in step.BranchConfig.OutputParameters)
                    {
                        if (!string.IsNullOrEmpty(output.Name))
                        {
                            _stepOutputs[output.Name] = output.Value ?? "false";
                        }
                    }

                    await WriteOutputParamsToGlobalVariablesAsync(step.BranchConfig.OutputParameters);
                }

                // DASHBOARD 步骤：结果根据确认结果判定（OK=true, NG=false）
                if (step.Step == StepType.DASHBOARD && step.DashboardDetail != null)
                {
                    // 步骤整体结果 = 确认结果（OK=true, NG=false）
                    string stepResultKey = $"步骤{step.Seq}_{step.Step}结果";
                    if (step.DashboardDetail.ConfirmResult.HasValue)
                    {
                        _stepOutputs[stepResultKey] = step.DashboardDetail.ConfirmResult.Value ? "true" : "false";
                    }
                    else
                    {
                        _stepOutputs[stepResultKey] = "true";
                    }

                    // 写入确认结果（OK=true, NG=false），供下游 BRANCH 步骤引用
                    if (step.DashboardDetail.ConfirmResult.HasValue)
                    {
                        string confirmKey = $"步骤{step.Seq}_DASHBOARD确认结果";
                        _stepOutputs[confirmKey] = step.DashboardDetail.ConfirmResult.Value ? "true" : "false";
                    }

                    foreach (var field in step.DashboardDetail.Fields)
                    {
                        if (!string.IsNullOrEmpty(field.DisplayName) && field.ConditionResult.HasValue)
                        {
                            string fieldKey = $"步骤{step.Seq}_DASHBOARD_{field.DisplayName}";
                            _stepOutputs[fieldKey] = field.ConditionResult.Value ? "true" : "false";
                        }
                    }
                }
                else
                {
                    // 非 DASHBOARD 步骤：步骤成功完成 = true
                    string stepResultKey = $"步骤{step.Seq}_{step.Step}结果";
                    _stepOutputs[stepResultKey] = "true";
                }
            }
            else
            {
                _logger.Warn($"步骤 [{step.Seq}] 类型 {step.Step} 没有注册的 Action，跳过");
            }
        }

        /// <summary>
        /// 执行 CHECK 步骤：评估检查条件，支持重试计数和超限动作（Alarm/Stop/Continue）
        /// FAIL 时根据 OnFailAction 决定跳转，重试超过 MaxRetries 时根据 OnMaxExceeded 决定行为
        /// </summary>
        private async Task<int> ExecuteCheckStepAsync(
            ProcessStep step,
            ObservableCollection<ProcessStep> steps,
            int currentIndex,
            string stepLabel,
            CancellationToken token)
        {
            int maxRetries = step.CheckDetail?.MaxRetries ?? 0;
            int retryCount = 0;
            bool checkPassed = false;

            do
            {
                checkPassed = true;

                // 评估检查项
                if (step.CheckDetail?.CheckItems != null && step.CheckDetail.CheckItems.Count > 0)
                {
                    checkPassed = EvaluateCheckItems(step.CheckDetail.CheckItems);
                }

                await _task.ExecuteStepSafeAsync(stepLabel, async () =>
                {
                    _logger.Info($"CHECK 步骤 [{step.Seq}] 第{retryCount + 1}次检查结果: {(checkPassed ? "PASS" : "FAIL")}");
                    await Task.CompletedTask;
                }, true, step.AlarmConfig);

                if (checkPassed)
                    break;

                // FAIL 处理
                retryCount++;

                // 重试次数超过上限，执行 OnMaxExceeded 动作
                if (maxRetries > 0 && retryCount >= maxRetries)
                {
                    _logger.Warn($"CHECK 步骤 [{step.Seq}] 重试{retryCount}次仍未通过，执行 OnMaxExceeded={step.CheckDetail?.OnMaxExceeded}");
                    return await HandleMaxExceededAsync(step, steps, currentIndex, retryCount);
                }

                // OnFailAction 不是 Retry 时，直接按动作处理
                if (step.CheckDetail?.OnFailAction != OnFailAction.Retry)
                    break;

                // 等待一小段时间后重试
                await Task.Delay(200, token);

            } while (retryCount < maxRetries || maxRetries <= 0);

            // 写入 CHECK 步骤结果到累积字典
            _stepOutputs[$"步骤{step.Seq}_CHECK结果"] = checkPassed ? "true" : "false";
            _stepOutputs[$"步骤{step.Seq}_CheckResult"] = checkPassed ? "true" : "false";

            // 根据 PASS/FAIL 决定下一步
            if (checkPassed)
            {
                return ResolveNextIndex(
                    step.CheckDetail?.OnPassAction ?? OnPassAction.Continue,
                    step.CheckDetail?.OnPassJumpStepSeq ?? 0,
                    steps,
                    currentIndex);
            }
            else
            {
                return ResolveNextIndex(
                    step.CheckDetail?.OnFailAction ?? OnFailAction.Stop,
                    step.CheckDetail?.OnFailJumpStepSeq ?? 0,
                    steps,
                    currentIndex);
            }
        }

        /// <summary>
        /// 处理 CHECK 步骤重试超限后的动作：Alarm触发报警 / Stop终止序列 / Continue继续下一步
        /// </summary>
        private async Task<int> HandleMaxExceededAsync(
            ProcessStep step,
            ObservableCollection<ProcessStep> steps,
            int currentIndex,
            int retryCount)
        {
            var onMaxExceeded = step.CheckDetail?.OnMaxExceeded ?? OnMaxExceededAction.Stop;

            switch (onMaxExceeded)
            {
                case OnMaxExceededAction.Alarm:
                    // 触发报警记录，然后暂停等待处理
                    await _alarmService.TriggerAlarmAsync(
                        "CHECK_MAX_RETRIES",
                        AlarmLevel.General,
                        $"CHECK步骤 [{step.Seq}] 重试{retryCount}次仍未通过",
                        source: $"{_task.TaskName}.Step{step.Seq}",
                        type: AlarmType.ParameterOutOfLimit);
                    // 标记该步骤有活跃报警
                    step.HasActiveAlarm = true;
                    _logger.Warn($"CHECK步骤 [{step.Seq}] 已触发报警 CHECK_MAX_RETRIES");
                    // 报警后终止序列
                    return -1;

                case OnMaxExceededAction.Stop:
                    _logger.Warn($"CHECK步骤 [{step.Seq}] 重试超限，终止执行");
                    return -1;

                case OnMaxExceededAction.Continue:
                    _logger.Warn($"CHECK步骤 [{step.Seq}] 重试超限，继续执行下一步");
                    return currentIndex + 1;

                default:
                    return -1;
            }
        }

        /// <summary>
        /// 评估检查项列表，所有已启用的检查项都通过才返回 true
        /// </summary>
        private bool EvaluateCheckItems(List<CheckItem> items)
        {
            foreach (var item in items)
            {
                if (!item.IsChecked) continue;

                bool passed = item.Value >= item.LowerLimit && item.Value <= item.UpperLimit;
                item.Status = passed;

                if (!passed)
                {
                    _logger.Warn($"检查项 [{item.DataLink}] 不通过: 值={item.Value}, 范围=[{item.LowerLimit}, {item.UpperLimit}]");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 根据动作类型解析下一步索引
        /// </summary>
        private int ResolveNextIndex<TAction>(
            TAction action,
            int jumpStepSeq,
            ObservableCollection<ProcessStep> steps,
            int currentIndex) where TAction : struct, Enum
        {
            // 处理 OnPassAction
            if (action is OnPassAction passAction)
            {
                return passAction switch
                {
                    OnPassAction.Continue => currentIndex + 1,
                    OnPassAction.SkipTo => FindStepIndexBySeq(jumpStepSeq, steps),
                    _ => currentIndex + 1
                };
            }

            // 处理 OnFailAction
            if (action is OnFailAction failAction)
            {
                return failAction switch
                {
                    OnFailAction.Retry => currentIndex,
                    OnFailAction.Stop => -1,
                    OnFailAction.SkipTo => FindStepIndexBySeq(jumpStepSeq, steps),
                    _ => currentIndex + 1
                };
            }

            return currentIndex + 1;
        }

        /// <summary>
        /// 根据 Seq 编号查找步骤索引
        /// </summary>
        private int FindStepIndexBySeq(int seq, ObservableCollection<ProcessStep> steps)
        {
            if (seq <= 0) return 0;

            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i].Seq == seq)
                {
                    _logger.Info($"跳转到步骤 Seq={seq} (索引={i})");
                    return i;
                }
            }

            _logger.Warn($"未找到 Seq={seq} 的步骤，继续顺序执行");
            return 0;
        }

        #region 条件分支逻辑

        /// <summary>
        /// 执行条件分支逻辑：评估步骤的BranchConfig，返回下一个要执行的步骤索引
        /// 支持基于全局变量或输出参数的条件表达式判断，实现灵活的流程控制
        /// </summary>
        private async Task<int> ExecuteBranchLogicAsync(
            ProcessStep step,
            ObservableCollection<ProcessStep> steps,
            int currentIndex,
            CancellationToken token)
        {
            var branchConfig = step.BranchConfig;
            if (branchConfig == null || !branchConfig.IsEnabled)
            {
                return currentIndex + 1; // 未启用分支，正常执行下一步
            }

            _logger.Info($"[Branch] 步骤 [{step.Seq}] 开始评估条件分支, BranchConfig.IsEnabled={branchConfig.IsEnabled}, 条件数={branchConfig.Conditions?.Count ?? 0}");

            // 1. 收集当前上下文中的变量值（全局变量 + 输出参数）
            var variables = await CollectContextVariablesAsync(step, branchConfig);

            // 2. 按顺序评估每个条件规则（第一个匹配即生效）
            int condIdx = 0;
            foreach (var condition in branchConfig.Conditions)
            {
                condIdx++;
                if (string.IsNullOrWhiteSpace(condition.ConditionExpression))
                {
                    _logger.Warn($"[Branch] 条件[{condIdx}] ConditionExpression为空，跳过 (Desc={condition.Description}, TargetSeq={condition.TargetStepSeq})");
                    continue;
                }

                try
                {
                    bool conditionResult = EvaluateCondition(condition.ConditionExpression, variables);
                    _logger.Info($"[Branch] 条件 '{condition.ConditionExpression}' = {conditionResult}");

                    if (conditionResult)
                    {
                        _logger.Info($"[Branch] ✓ 条件匹配! 跳转到步骤 [{condition.TargetStepSeq}] ({condition.Description})");
                        return await ResolveStepIndexAsync(condition.TargetStepSeq, steps, currentIndex);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn($"[Branch] 条件表达式评估失败: '{condition.ConditionExpression}' - {ex.Message}");
                    continue; // 该条件评估失败，继续尝试下一个条件
                }
            }

            // 3. 所有条件都不满足，执行默认动作
            _logger.Info($"[Branch] 无条件匹配，执行默认动作: {branchConfig.DefaultAction}");
            return await HandleDefaultActionAsync(branchConfig, steps, currentIndex);
        }

        /// <summary>
        /// 收集条件评估所需的上下文变量（全局变量 + 步骤输出参数）
        /// </summary>
        private async Task WriteOutputParamsToGlobalVariablesAsync(List<BranchOutputParameter> outputParameters)
        {
            try
            {
                var poolId = _recipePoolService?.CurrentPoolId;
                if (string.IsNullOrEmpty(poolId)) return;

                var globalVars = await _recipePoolService!.LoadGlobalVariablesAsync(poolId);
                bool changed = false;

                foreach (var output in outputParameters)
                {
                    if (string.IsNullOrEmpty(output.TargetGlobalVariable) || string.IsNullOrEmpty(output.Name))
                        continue;

                    var targetVar = globalVars.FirstOrDefault(v => v.Name == output.TargetGlobalVariable);
                    if (targetVar == null)
                    {
                        _logger.Warn($"[Branch] 全局变量 '{output.TargetGlobalVariable}' 不存在，跳过写入");
                        continue;
                    }

                    if (targetVar.Type != output.OutputType)
                    {
                        _logger.Warn($"[Branch] 类型不匹配: 输出参数 '{output.Name}' 类型={output.OutputType}, 全局变量 '{targetVar.Name}' 类型={targetVar.Type}，跳过写入");
                        continue;
                    }

                    string valueToWrite = output.Value ?? "false";
                    targetVar.Value = valueToWrite;
                    _logger.Info($"[Branch] 输出参数写入全局变量: {output.Name}={valueToWrite} → {targetVar.Name}");
                    changed = true;
                }

                if (changed)
                {
                    await _recipePoolService.SaveGlobalVariablesAsync(poolId, globalVars);
                    _logger.Info("[Branch] 全局变量已保存");

                    // 通知所有订阅者全局变量已更新
                    _ea.GetEvent<Recipe.Events.GlobalVariablesChangedEvent>().Publish(poolId);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"[Branch] 写入全局变量失败: {ex.Message}");
            }
        }

        /// 将输出参数以 @Output: 前缀加入变量池，供条件表达式引用
        /// 同时将前序步骤累积的输出参数（_stepOutputs）以 @Output: 前缀加入
        /// </summary>
        private async Task<Dictionary<string, string>> CollectContextVariablesAsync(
            ProcessStep step,
            BranchConfig branchConfig)
        {
            var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // 加载全局变量（@GV: 前缀）
            try
            {
                var poolId = _recipePoolService?.CurrentPoolId;
                if (!string.IsNullOrEmpty(poolId))
                {
                    var globalVars = await _recipePoolService!.LoadGlobalVariablesAsync(poolId);
                    foreach (var gv in globalVars)
                    {
                        if (!string.IsNullOrEmpty(gv.Name))
                            variables[$"@GV:{gv.Name}"] = gv.Value ?? "0";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"[Branch] 加载全局变量失败: {ex.Message}");
            }

            // 将前序步骤累积的输出参数加入变量池（@Output: 前缀）
            foreach (var kv in _stepOutputs)
            {
                string key = kv.Key.StartsWith("@Output:", StringComparison.OrdinalIgnoreCase)
                    ? kv.Key
                    : $"@Output:{kv.Key}";
                variables[key] = kv.Value;
                _logger.Info($"[Branch] 变量池 += {key} = {kv.Value}");
            }

            // 将当前步骤配置的输出参数加入变量池（@Output: 前缀，可覆盖同名累积值）
            if (branchConfig.OutputParameters != null)
            {
                foreach (var output in branchConfig.OutputParameters)
                {
                    if (!string.IsNullOrEmpty(output.Name))
                    {
                        variables[$"@Output:{output.Name}"] = output.Value ?? "false";
                    }
                }
            }

            _logger.Info($"[Branch] 变量池共 {variables.Count} 项: {string.Join(", ", variables.Keys)}");
            return variables;
        }

        /// <summary>
        /// 评估条件表达式，返回bool结果
        /// 使用FormulaEvaluator计算表达式值，非0值为true，0值为false
        /// 支持的表达式示例:
        ///   "@GV:检测结果 == true"
        ///   "@GV:H2 > 10.5"
        ///   "@Output:PassFlag == true && @GV:Count > 0"
        ///   "@GV:H2 - @GV:Slot > 0.27"
        /// </summary>
        private bool EvaluateCondition(string expression, Dictionary<string, string> variables)
        {
            try
            {
                // 使用FormulaEvaluator计算表达式值
                double result = _formulaEvaluator.Evaluate(expression, variables);

                // 对于布尔表达式，true=1.0, false=0.0
                return Math.Abs(result) > 0.0001;
            }
            catch (Exception ex)
            {
                _logger.Error($"[Branch] 表达式评估异常: '{expression}' - {ex.Message}");
                return false; // 表达式异常时返回false，不触发跳转
            }
        }

        /// <summary>
        /// 处理所有条件都不满足时的默认动作
        /// Continue: 继续下一步
        /// Stop: 终止序列执行（返回-1）
        /// SkipTo: 跳转到指定步骤（若目标不存在则报警并终止）
        /// </summary>
        private async Task<int> HandleDefaultActionAsync(BranchConfig config, ObservableCollection<ProcessStep> steps, int currentIndex)
        {
            switch (config.DefaultAction)
            {
                case DefaultBranchAction.Stop:
                    _logger.Warn("[Branch] 默认动作: 终止序列执行");
                    return -1; // -1 表示终止

                case DefaultBranchAction.SkipTo:
                    if (config.DefaultTargetStepSeq > 0)
                    {
                        _logger.Info($"[Branch] 默认动作: 跳转到步骤 [{config.DefaultTargetStepSeq}]");
                        return await ResolveStepIndexAsync(config.DefaultTargetStepSeq, steps, currentIndex);
                    }
                    // 安全机制：SkipTo但目标无效时，触发报警并终止（防止撞机风险）
                    string errorMsg = $"[Branch] ⚠️ 安全警告: 条件分支配置的跳转目标步骤 Seq={config.DefaultTargetStepSeq} 不存在！为避免设备碰撞风险，已自动终止序列。请操作员检查配置后重试。";
                    _logger.Error(errorMsg);
                    await _alarmService.TriggerAlarmAsync(
                        "BRANCH_INVALID_TARGET",
                        AlarmLevel.Emergency,
                        errorMsg,
                        source: $"{_task.TaskName}.BranchConfig",
                        type: AlarmType.ParameterOutOfLimit);
                    return -1; // 终止序列，等待操作员处理

                case DefaultBranchAction.Continue:
                default:
                    _logger.Info("[Branch] 默认动作: Continue → 继续下一步");
                    return currentIndex + 1;
            }
        }

        /// <summary>
        /// 解析目标步骤索引：根据Seq号查找对应的数组索引
        /// 安全机制：如果目标Seq为0或未找到，触发报警并终止（防止撞机风险）
        /// </summary>
        private async Task<int> ResolveStepIndexAsync(int targetSeq, ObservableCollection<ProcessStep> steps, int currentIndex)
        {
            if (targetSeq <= 0)
            {
                string errorMsg = $"[Branch] ⚠️ 安全警告: 条件分支配置的跳转目标步骤 Seq={targetSeq} 无效！为避免设备碰撞风险，已自动终止序列。请操作员检查配置后重试。";
                _logger.Error(errorMsg);
                await _alarmService.TriggerAlarmAsync(
                    "BRANCH_INVALID_TARGET_SEQ",
                    AlarmLevel.Emergency,
                    errorMsg,
                    source: $"{_task.TaskName}.BranchConfig",
                    type: AlarmType.ParameterOutOfLimit);
                return -1; // 终止序列
            }

            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i].Seq == targetSeq)
                {
                    _logger.Info($"[Branch] 成功解析跳转目标: Seq={targetSeq} → Index={i}");
                    return i;
                }
            }

            // 目标步骤不存在时的安全处理
            string notFoundMsg = $"[Branch] ⚠️ 安全警告: 跳转目标步骤 Seq={targetSeq} 在当前序列中不存在！可能已被删除或序号错误。为避免设备碰撞风险，已自动终止序列。";
            _logger.Error(notFoundMsg);
            await _alarmService.TriggerAlarmAsync(
                "BRANCH_TARGET_NOT_FOUND",
                AlarmLevel.Emergency,
                notFoundMsg,
                source: $"{_task.TaskName}.BranchConfig",
                type: AlarmType.ParameterOutOfLimit);
            return -1; // 终止序列，等待操作员处理
        }

        #endregion

        #region IF 条件块执行逻辑

        /// <summary>
        /// 执行 IF 步骤：评估条件表达式，递归执行 Then 或 Else 分支的子步骤集合。
        /// 支持多层嵌套（IF 子步骤中可再包含 IF 步骤）。
        /// 表达式为空或求值失败时按 false 处理（执行 Else 分支）。
        /// </summary>
        private async Task ExecuteIfStepAsync(ProcessStep step, CancellationToken token)
        {
            // 确保 IF 步骤已初始化 IfDetail 和 IfBranches
            EnsureIfStepInitialized(step);

            var ifDetail = step.IfDetail;
            if (ifDetail == null)
            {
                _logger.Warn($"[IF] 步骤 [{step.Seq}] IfDetail 为 null，跳过执行");
                return;
            }

            // 收集上下文变量（全局变量 + 步骤输出参数）
            var variables = await CollectIfContextVariablesAsync();

            // 评估条件表达式
            bool conditionResult = false;
            if (!string.IsNullOrWhiteSpace(ifDetail.ConditionExpression))
            {
                try
                {
                    conditionResult = EvaluateCondition(ifDetail.ConditionExpression, variables);
                    _logger.Info($"[IF] 步骤 [{step.Seq}] 条件 '{ifDetail.ConditionExpression}' = {conditionResult}");
                }
                catch (Exception ex)
                {
                    _logger.Warn($"[IF] 步骤 [{step.Seq}] 条件评估异常: {ex.Message}，按 false 处理");
                    conditionResult = false;
                }
            }
            else
            {
                _logger.Warn($"[IF] 步骤 [{step.Seq}] 条件表达式为空，按 false 处理");
            }

            // 选择执行的分支（IfBranches[0]=Then, IfBranches[1]=Else）
            var branch = conditionResult
                ? step.IfBranches.FirstOrDefault(b => string.Equals(b.Header, "Then", StringComparison.OrdinalIgnoreCase))
                : step.IfBranches.FirstOrDefault(b => string.Equals(b.Header, "Else", StringComparison.OrdinalIgnoreCase));

            if (branch == null)
            {
                _logger.Warn($"[IF] 步骤 [{step.Seq}] 未找到 {(conditionResult ? "Then" : "Else")} 分支，跳过执行");
                return;
            }

            _logger.Info($"[IF] 步骤 [{step.Seq}] 执行 {branch.Header} 分支，子步骤数={branch.Steps?.Count ?? 0}");

            // 递归执行分支内的子步骤集合
            if (branch.Steps != null && branch.Steps.Count > 0)
            {
                await ExecuteStepListAsync(branch.Steps, token);
            }
        }

        /// <summary>
        /// 递归执行子步骤集合（用于 IF 分支内的子步骤执行）。
        /// 支持嵌套 IF 步骤：当遇到 IF 类型时递归调用 ExecuteIfStepAsync。
        /// 仅顺序执行，不支持 BRANCH 跳转出块外（符合 IF 块语义）。
        /// </summary>
        private async Task ExecuteStepListAsync(ObservableCollection<ProcessStep> steps, CancellationToken token)
        {
            if (steps == null || steps.Count == 0) return;

            foreach (var step in steps)
            {
                token.ThrowIfCancellationRequested();

                // 跳过禁用步骤
                if (!step.IsEnabled)
                {
                    _logger.Info($"[IF-Sub] 跳过禁用步骤: [{step.Seq}] {step.Step}");
                    continue;
                }

                step.IsCurrent = true;
                _logger.Info($"[IF-Sub] === 执行子步骤 [{step.Seq}] {step.Step} ===");

                try
                {
                    var sw = Stopwatch.StartNew();
                    string stepLabel = FormatStepLabel(step);

                    if (step.Step == StepType.IF)
                    {
                        // 递归执行嵌套 IF 步骤
                        await ExecuteIfStepAsync(step, token);
                    }
                    else if (step.Step == StepType.BRANCH)
                    {
                        // IF 块内的 BRANCH 步骤：仅评估条件不跳转（块内顺序执行语义）
                        _logger.Info($"[IF-Sub] BRANCH 步骤 [{step.Seq}] 在 IF 块内仅评估条件，不执行块外跳转");
                        if (step.BranchConfig?.IsEnabled == true)
                        {
                            var variables = await CollectIfContextVariablesAsync();
                            if (!string.IsNullOrWhiteSpace(step.BranchConfig.Conditions.FirstOrDefault()?.ConditionExpression))
                            {
                                bool result = EvaluateCondition(step.BranchConfig.Conditions.First().ConditionExpression, variables);
                                _logger.Info($"[IF-Sub] BRANCH 步骤 [{step.Seq}] 条件评估结果: {result}（块内不跳转）");
                            }
                        }
                    }
                    else
                    {
                        // 普通步骤：通过 ExecuteWithRunStepAsync 执行，享受暂停/急停/报警保护
                        await ExecuteWithRunStepAsync(stepLabel, step, token);
                    }

                    sw.Stop();
                    step.LastElapsedMs = sw.ElapsedMilliseconds;
                }
                catch (OperationCanceledException)
                {
                    step.IsCurrent = false;
                    throw;
                }
                catch (Exception ex)
                {
                    step.IsCurrent = false;
                    _logger.Error($"[IF-Sub] 子步骤 [{step.Seq}] {step.Step} 执行异常: {ex.Message}");
                    if (step.AlarmConfig?.IsEnabled == true)
                    {
                        step.HasActiveAlarm = true;
                    }
                    throw; // 异常向上传播，终止整个 IF 块执行
                }
                finally
                {
                    step.IsCurrent = false;
                }
            }
        }

        /// <summary>
        /// 确保 IF 步骤的 IfDetail 和 IfBranches 已初始化。
        /// 反序列化旧数据或新建步骤时可能为 null，此处保证结构完整。
        /// </summary>
        private void EnsureIfStepInitialized(ProcessStep step)
        {
            if (step.IfDetail == null)
            {
                step.IfDetail = new IfDetail
                {
                    ConditionExpression = "",
                    Description = ""
                };
            }

            if (step.IfBranches == null || step.IfBranches.Count < 2)
            {
                var existingThen = step.IfBranches?.FirstOrDefault(b =>
                    string.Equals(b.Header, "Then", StringComparison.OrdinalIgnoreCase));
                var existingElse = step.IfBranches?.FirstOrDefault(b =>
                    string.Equals(b.Header, "Else", StringComparison.OrdinalIgnoreCase));

                step.IfBranches = new ObservableCollection<IfBranchGroup>
                {
                    existingThen ?? new IfBranchGroup { Header = "Then", Steps = new ObservableCollection<ProcessStep>() },
                    existingElse ?? new IfBranchGroup { Header = "Else", Steps = new ObservableCollection<ProcessStep>() }
                };
            }
        }

        /// <summary>
        /// 收集 IF 条件评估所需的上下文变量（全局变量 + 步骤输出参数）。
        /// 复用 ProcessStepExecutor 内的 _stepOutputs 累积字典。
        /// </summary>
        private async Task<Dictionary<string, string>> CollectIfContextVariablesAsync()
        {
            var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // 加载全局变量（@GV: 前缀）
            try
            {
                var poolId = _recipePoolService?.CurrentPoolId;
                if (!string.IsNullOrEmpty(poolId))
                {
                    var globalVars = await _recipePoolService!.LoadGlobalVariablesAsync(poolId);
                    foreach (var gv in globalVars)
                    {
                        if (!string.IsNullOrEmpty(gv.Name))
                            variables[$"@GV:{gv.Name}"] = gv.Value ?? "0";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"[IF] 加载全局变量失败: {ex.Message}");
            }

            // 将前序步骤累积的输出参数加入变量池（@Output: 前缀）
            foreach (var kv in _stepOutputs)
            {
                string key = kv.Key.StartsWith("@Output:", StringComparison.OrdinalIgnoreCase)
                    ? kv.Key
                    : $"@Output:{kv.Key}";
                variables[key] = kv.Value;
            }

            return variables;
        }

        #endregion
    }
}
