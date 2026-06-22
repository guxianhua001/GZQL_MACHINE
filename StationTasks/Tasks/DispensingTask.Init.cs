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
        /// 1. 回零 Dz₁/Dz₂/Dz₃ → 回到待机位
        /// 2. 通知组装/上下料：点胶Z轴完成
        /// 3. 等待组装Z轴完成
        /// 4. 回零 Dx/Dy → 回到待机位
        /// 5. 通知组装：点胶回零完成
        /// </summary>
        public override async Task HomeAsync()
        {
            // 设置取消令牌，支持初始化过程中的停止/急停
            _cts = new CancellationTokenSource();
            State = TaskState.Homing;
            Logger.Info($"[{TaskName}] 开始点胶系统初始化...");
            PublishTaskStatusChanged(L("Init_Initializing"), State);
            PublishInitProgress(0, L("Init_Dispenser_Start"));

            try
            {
                // 预加载位置数据
                await RunStep("预加载位置数据", PreloadPositionsAsync);
                await RefreshPositionsCacheAsync();

                // 重置工站间协调信号（防止上次初始化残留）
                ResetInitSignals();

                // ===== 阶段1：点胶Z轴（Dz₁, Dz₂, Dz₃）回零 → 待机位 =====
                Logger.Info($"[{TaskName}] 阶段1：点胶Z轴回零...");
                PublishTaskStatusChanged(L("Init_Dispenser_ZHoming"), State);
                PublishInitProgress(10, L("Init_Dispenser_ZHoming"));

                int[] zAxes = { AxisDz1, AxisDz2, AxisDz3 };
                string[] zAxisNames = { "Dz₁", "Dz₂", "Dz₃" };
                int zIndex = 0;

                foreach (var (axisId, axisName) in zAxes.Zip(zAxisNames, (id, name) => (id, name)))
                {
                    CurrentToken.ThrowIfCancellationRequested();
                    if (axisId < 0) continue;

                    Logger.Info($"[{TaskName}] {axisName} 轴回零中...");
                    PublishTaskStatusChanged(L("Init_HomeAxis", axisName), State);
                    PublishInitProgress(10 + zIndex * 10, L("Init_HomeAxis", axisName));
                    await ExecuteHomeAxisAsync(axisId);
                    zIndex++;
                }

                // Z轴回到待机位
                PublishTaskStatusChanged(L("Init_Dispenser_ZStandby"), State);
                foreach (var (axisId, axisName) in zAxes.Zip(zAxisNames, (id, name) => (id, name)))
                {
                    CurrentToken.ThrowIfCancellationRequested();
                    if (axisId < 0) continue;

                    PublishInitProgress(40 + zIndex * 10, L("Init_StandbyPosition", axisName));
                    await ExecuteMoveAsync(axisId, "StandbyPosition", InitZAxisVelocity);
                }

                // 通知组装/上下料：点胶Z轴回零完成
                SignalToStation("AssemblyStation", "DispensingZComplete", true);
                SignalToStation("LoadingStation", "DispensingZComplete", true);
                Logger.Info($"[{TaskName}] 已通知组装/上下料：点胶Z轴回零完成。");

                // ===== 等待组装Z轴完成（所有Z轴归零前提） =====
                PublishTaskStatusChanged(L("Init_Dispenser_WaitAssemblyZ"), State);
                PublishInitProgress(60, L("Init_Dispenser_WaitAssemblyZ"));
                await WaitForSignalAsync("DispensingStation", "AssemblyZComplete", true, SignalWaitTimeoutMs);
                Logger.Info($"[{TaskName}] 组装Z轴已完成，开始点胶XY轴回零。");

                // ===== 阶段2：点胶XY轴（Dx, Dy）回零 → 待机位 =====
                PublishTaskStatusChanged(L("Init_Dispenser_XYHoming"), State);
                PublishInitProgress(65, L("Init_Dispenser_XYHoming"));

                int[] mainAxes = { AxisDx, AxisDy };
                string[] mainAxisNames = { "Dx", "Dy" };
                int mIndex = 0;

                foreach (var (axisId, axisName) in mainAxes.Zip(mainAxisNames, (id, name) => (id, name)))
                {
                    CurrentToken.ThrowIfCancellationRequested();
                    if (axisId < 0) continue;

                    Logger.Info($"[{TaskName}] {axisName} 轴回零中...");
                    PublishTaskStatusChanged(L("Init_HomeAxis", axisName), State);
                    PublishInitProgress(65 + mIndex * 10, L("Init_HomeAxis", axisName));
                    await ExecuteHomeAxisAsync(axisId);
                    mIndex++;
                }

                // XY轴回到待机位
                PublishTaskStatusChanged(L("Init_Dispenser_XYStandby"), State);
                foreach (var (axisId, axisName) in mainAxes.Zip(mainAxisNames, (id, name) => (id, name)))
                {
                    CurrentToken.ThrowIfCancellationRequested();
                    if (axisId < 0) continue;

                    PublishInitProgress(85 + mIndex * 5, L("Init_StandbyPosition", axisName));
                    await ExecuteMoveAsync(axisId, "StandbyPosition", InitXYAxisVelocity);
                }

                // 通知组装：点胶回零完成
                SignalToStation("AssemblyStation", "DispensingComplete", true);
                Logger.Info($"[{TaskName}] 已通知组装：点胶回零完成。");

                State = TaskState.Idle;
                Logger.Info($"[{TaskName}] 点胶系统初始化完成，进入待机。");
                PublishTaskStatusChanged(L("Init_Idle"), State);
                PublishInitProgress(100, L("Init_Dispenser_Complete"), true);
            }
            catch (System.OperationCanceledException)
            {
                State = TaskState.Error;
                Logger.Warn($"[{TaskName}] 点胶系统初始化被取消。");
                PublishTaskStatusChanged(L("Init_Canceled"), State);
                PublishInitProgress(0, L("Init_Canceled"), true, true);
                throw;
            }
            catch (RecoverableException ex)
            {
                State = TaskState.Error;
                Logger.Error($"[{TaskName}] 点胶系统初始化失败（等待信号超时）: {ex.Message}");
                PublishTaskStatusChanged(L("Init_Failed"), State);
                PublishInitProgress(0, L("Init_Failed"), true, true);
                throw;
            }
            catch (System.Exception ex)
            {
                State = TaskState.Error;
                Logger.Error($"[{TaskName}] 点胶系统初始化失败: {ex.Message}");
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
