using Core.Events;
using MotionControl.Exceptions;
using MotionControl.Interfaces;
using Prism.Events;
using System.Threading;
using System.Threading.Tasks;

namespace StationTasks.Tasks
{
    /// <summary>
    /// DispensingTask partial class — 整机初始化动作（HomeAsync 重写）。
    /// 初始化逻辑不写在工站主任务中，独立放在 Init.cs 文件。
    /// 回零命令使用控制卡内已配置的回零参数（ExecuteHomeAxisAsync），无需设置回零参数。
    /// 工站间协调使用 SignalToStation / WaitForSignalAsync 信号交互。
    /// </summary>
    public partial class DispensingTask
    {
        /// <summary> Z轴回零+待机位运动速度（mm/s，工业安全速度） </summary>
        private const double InitZAxisVelocity = 20.0;
        /// <summary> XY轴回零后回到待机位运动速度（mm/s） </summary>
        private const double InitXYAxisVelocity = 30.0;
        /// <summary> 工站间信号等待超时（ms），允许慢速回零 </summary>
        private const int SignalWaitTimeoutMs = 120000;

        /// <summary>
        /// 点胶工站整机初始化（重写 HomeAsync）。
        /// 时序：
        /// 0. 逐轴上使能（每轴间隔1秒）
        /// 1. 回零 Dz₂/Dz₃ → 回到待机位
        /// 2. Dy 轴回零 → 回到待机位
        /// 3. Dz₁ 轴回零 → 回到待机位
        /// 4. 通知组装/上下料：点胶Z轴完成
        /// 5. 等待组装Z轴完成
        /// 6. 回零 Dx → 回到待机位
        /// 7. 通知组装：点胶回零完成
        /// </summary>
        public override async Task HomeAsync()
        {
            // 设置取消令牌，支持初始化过程中的停止/急停
            _cts = new CancellationTokenSource();
            State = TaskState.Homing;
            Logger.Info(string.Format(_localizationService.GetResourceOrDefault("DT_Log_InitStart", "[{0}] 开始点胶系统初始化..."), TaskName));
            PublishTaskStatusChanged(L("Init_Initializing"), State);
            PublishInitProgress(0, L("Init_Dispenser_Start"));

            try
            {
                // 预加载位置数据
                await RunStep("预加载位置数据", PreloadPositionsAsync);
                await RefreshPositionsCacheAsync();

                // 重置工站间协调信号（防止上次初始化残留）
                ResetInitSignals();

                // ===== 阶段0：逐轴上使能（回零前，每轴间隔1秒） =====
                Logger.Info(string.Format(_localizationService.GetResourceOrDefault("DT_Log_Phase0AxisEnable", "[{0}] 回零前逐轴上使能..."), TaskName));
                PublishTaskStatusChanged(L("Init_EnableAxes"), State);
                PublishInitProgress(1, L("Init_EnableAxes"));
                await EnableAxesSequentiallyAsync(new (int, string)[]
                {
                    (AxisDz2, "Dz₂"), (AxisDz3, "Dz₃"), (AxisDy, "Dy"), (AxisDy2, "Dy2"), (AxisDz1, "Dz₁"), (AxisDx, "Dx")
                }, axisName =>
                {
                    PublishTaskStatusChanged(L("Init_EnableAxis", axisName), State);
                    PublishInitProgress(2, L("Init_EnableAxis", axisName));
                }).ConfigureAwait(false);

                // ===== 阶段1：点胶Z轴（Dz₂, Dz₃）回零 → 待机位 =====
                // Dz₁ 延后至 Dy 回零后执行，避免机械干涉
                Logger.Info(string.Format(_localizationService.GetResourceOrDefault("DT_Log_Phase1ZHoming", "[{0}] 阶段1：点胶Z轴（Dz₂/Dz₃）回零..."), TaskName));
                PublishTaskStatusChanged(L("Init_Dispenser_ZHoming"), State);
                PublishInitProgress(5, L("Init_Dispenser_ZHoming"));

                int[] zAxes = { AxisDz2, AxisDz3 };
                string[] zAxisNames = { "Dz₂", "Dz₃" };
                int zIndex = 0;

                foreach (var (axisId, axisName) in zAxes.Zip(zAxisNames, (id, name) => (id, name)))
                {
                    CurrentToken.ThrowIfCancellationRequested();
                    if (axisId < 0) continue;

                    Logger.Info(string.Format(_localizationService.GetResourceOrDefault("DT_Log_AxisHoming", "[{0}] {1} 轴回零中..."), TaskName, axisName));
                    PublishTaskStatusChanged(L("Init_HomeAxis", axisName), State);
                    PublishInitProgress(5 + zIndex * 5, L("Init_HomeAxis", axisName));
                    await ExecuteHomeAxisAsync(axisId);
                    zIndex++;
                }

                // Dz₂/Dz₃ 回到待机位
                PublishTaskStatusChanged(L("Init_Dispenser_ZStandby"), State);
                int zStandbyIdx = 0;
                foreach (var (axisId, axisName) in zAxes.Zip(zAxisNames, (id, name) => (id, name)))
                {
                    CurrentToken.ThrowIfCancellationRequested();
                    if (axisId < 0) continue;

                    PublishInitProgress(15 + zStandbyIdx * 5, L("Init_StandbyPosition", axisName));
                    await ExecuteMoveAsync(axisId, "StandbyPosition", InitZAxisVelocity);
                    zStandbyIdx++;
                }

                // ===== 阶段2：Dy 轴回零 → 待机位 =====
                Logger.Info(string.Format(_localizationService.GetResourceOrDefault("DT_Log_Phase2DyHoming", "[{0}] 阶段2：Dy 轴回零..."), TaskName));
                PublishTaskStatusChanged(L("Init_HomeAxis", "Dy"), State);
                PublishInitProgress(25, L("Init_HomeAxis", "Dy"));
                if (AxisDy >= 0)
                {
                    Logger.Info(string.Format(_localizationService.GetResourceOrDefault("DT_Log_AxisHoming", "[{0}] {1} 轴回零中..."), TaskName, "Dy"));
                    await ExecuteHomeAxisAsync(AxisDy);
                    PublishInitProgress(30, L("Init_StandbyPosition", "Dy"));
                    await ExecuteMoveAsync(AxisDy, "StandbyPosition", InitXYAxisVelocity);
                }

                // ===== 阶段3：Dz₁ 轴回零 → 待机位 =====
                Logger.Info(string.Format(_localizationService.GetResourceOrDefault("DT_Log_Phase3Dz1Homing", "[{0}] 阶段3：Dz₁ 轴回零..."), TaskName));
                PublishTaskStatusChanged(L("Init_HomeAxis", "Dz₁"), State);
                PublishInitProgress(35, L("Init_HomeAxis", "Dz₁"));
                if (AxisDz1 >= 0)
                {
                    Logger.Info(string.Format(_localizationService.GetResourceOrDefault("DT_Log_AxisHoming", "[{0}] {1} 轴回零中..."), TaskName, "Dz₁"));
                    await ExecuteHomeAxisAsync(AxisDz1);
                    PublishInitProgress(40, L("Init_StandbyPosition", "Dz₁"));
                    await ExecuteMoveAsync(AxisDz1, "StandbyPosition", InitZAxisVelocity);
                }

                // ===== 阶段4：通知组装/上下料：点胶Z轴回零完成 =====
                SignalToStation("AssemblyStation", "DispensingZComplete", true);
                SignalToStation("LoadingStation", "DispensingZComplete", true);
                Logger.Info(string.Format(_localizationService.GetResourceOrDefault("DT_Log_NotifyZComplete", "[{0}] 已通知组装/上下料：点胶Z轴回零完成。"), TaskName));

                // ===== 阶段5：等待组装Z轴完成（所有Z轴归零前提） =====
                PublishTaskStatusChanged(L("Init_Dispenser_WaitAssemblyZ"), State);
                PublishInitProgress(45, L("Init_Dispenser_WaitAssemblyZ"));
                await WaitForSignalAsync("DispensingStation", "AssemblyZComplete", true, SignalWaitTimeoutMs);
                Logger.Info(string.Format(_localizationService.GetResourceOrDefault("DT_Log_AssemblyZDone", "[{0}] 组装Z轴已完成，开始 Dx 轴回零。"), TaskName));

                // ===== 阶段6：Dx 轴回零 → 待机位 =====
                Logger.Info(string.Format(_localizationService.GetResourceOrDefault("DT_Log_Phase6DxHoming", "[{0}] 阶段6：Dx 轴回零..."), TaskName));
                PublishTaskStatusChanged(L("Init_Dispenser_XYHoming"), State);
                PublishInitProgress(65, L("Init_HomeAxis", "Dx"));
                if (AxisDx >= 0)
                {
                    Logger.Info(string.Format(_localizationService.GetResourceOrDefault("DT_Log_AxisHoming", "[{0}] {1} 轴回零中..."), TaskName, "Dx"));
                    await ExecuteHomeAxisAsync(AxisDx);
                    PublishInitProgress(85, L("Init_StandbyPosition", "Dx"));
                    await ExecuteMoveAsync(AxisDx, "StandbyPosition", InitXYAxisVelocity);
                }

                // ===== 阶段7：通知组装：点胶回零完成 =====
                SignalToStation("AssemblyStation", "DispensingComplete", true);
                Logger.Info(string.Format(_localizationService.GetResourceOrDefault("DT_Log_NotifyDispensingComplete", "[{0}] 已通知组装：点胶回零完成。"), TaskName));

                State = TaskState.Idle;
                Logger.Info(string.Format(_localizationService.GetResourceOrDefault("DT_Log_InitComplete", "[{0}] 点胶系统初始化完成，进入待机。"), TaskName));
                PublishTaskStatusChanged(L("Init_Idle"), State);
                PublishInitProgress(100, L("Init_Dispenser_Complete"), true);
            }
            catch (System.OperationCanceledException)
            {
                State = TaskState.Error;
                Logger.Warn(string.Format(_localizationService.GetResourceOrDefault("DT_Log_InitCanceled", "[{0}] 点胶系统初始化被取消。"), TaskName));
                PublishTaskStatusChanged(L("Init_Canceled"), State);
                PublishInitProgress(0, L("Init_Canceled"), true, true);
                throw;
            }
            catch (RecoverableException ex)
            {
                State = TaskState.Error;
                Logger.Error(string.Format(_localizationService.GetResourceOrDefault("DT_Log_InitFailedSignalTimeout", "[{0}] 点胶系统初始化失败（等待信号超时）: {1}"), TaskName, ex.Message));
                PublishTaskStatusChanged(L("Init_Failed"), State);
                PublishInitProgress(0, L("Init_Failed"), true, true);
                throw;
            }
            catch (System.Exception ex)
            {
                State = TaskState.Error;
                Logger.Error(string.Format(_localizationService.GetResourceOrDefault("DT_Log_InitFailed", "[{0}] 点胶系统初始化失败: {1}"), TaskName, ex.Message));
                PublishTaskStatusChanged(L("Init_Failed"), State);
                PublishInitProgress(0, L("Init_Failed"), true, true);
                throw;
            }
        }

        /// <summary>
        /// 重置工站间协调信号（防止上次初始化残留导致跳过等待）
        /// </summary>
        private void ResetInitSignals()
        {
            SignalToStation("AssemblyStation", "DispensingZComplete", false);
            SignalToStation("LoadingStation", "DispensingZComplete", false);
            SignalToStation("AssemblyStation", "DispensingComplete", false);
            SignalToStation("DispensingStation", "AssemblyZComplete", false);
            SignalToStation("LoadingStation", "AssemblyZComplete", false);
        }

        /// <summary>
        /// 获取本地化字符串（支持格式化参数）
        /// </summary>
        private string L(string key, params object[] args)
        {
            return _localizationService?.GetResource(key, args) ?? key;
        }

        /// <summary>
        /// 发布初始化进度事件（供 TaskMonitorView 显示回零进度）
        /// </summary>
        private void PublishInitProgress(double progress, string message, bool isCompleted = false, bool hasError = false)
        {
            Ea.GetEvent<StationInitProgressEvent>().Publish(new StationInitProgressPayload
            {
                StationId = StationIdentifierValue,
                Progress = progress,
                Message = message,
                IsCompleted = isCompleted,
                HasError = hasError
            });
        }
    }
}
