using Core.Events;
using MotionControl.Exceptions;
using MotionControl.Interfaces;
using Prism.Events;
using System.Threading;
using System.Threading.Tasks;

namespace StationTasks.Tasks
{
    /// <summary>
    /// AssemblyTask partial class — 整机初始化动作（HomeAsync 重写）。
    /// 初始化逻辑不写在工站主任务中，独立放在 Init.cs 文件。
    /// 回零命令使用控制卡内已配置的回零参数（ExecuteHomeAxisAsync），无需设置回零参数。
    /// 工站间协调使用 SignalToStation / WaitForSignalAsync 信号交互。
    /// </summary>
    public partial class AssemblyTask
    {
        /// <summary> Z轴回零+待机位运动速度（mm/s，工业安全速度） </summary>
        private const double InitZAxisVelocity = 20.0;
        /// <summary> XY轴回零后回到待机位运动速度（mm/s） </summary>
        private const double InitXYAxisVelocity = 50.0;
        /// <summary> 旋转轴回零后回到待机位运动速度（mm/s） </summary>
        private const double InitRotaryAxisVelocity = 30.0;
        /// <summary> 工站间信号等待超时（ms），允许慢速回零 </summary>
        private const int SignalWaitTimeoutMs = 120000;

        /// <summary>
        /// 组装工站整机初始化（重写 HomeAsync）。
        /// 时序：
        /// 1. 回零 Z → 回到待机位
        /// 2. 通知点胶/上下料：组装Z轴完成
        /// 3. 等待点胶Z轴完成（所有Z轴归零前提）
        /// 4. 回零 Cy/Ey → 回到待机位
        /// 5. 等待点胶工站回零完成
        /// 6. 回零 X/Ry → 回到待机位
        /// </summary>
        public override async Task HomeAsync()
        {
            // 设置取消令牌，支持初始化过程中的停止/急停
            _cts = new CancellationTokenSource();
            State = TaskState.Homing;
            Logger.Info($"[{TaskName}] 开始组装系统初始化...");
            PublishTaskStatusChanged(L("Init_Initializing"), State);
            PublishInitProgress(0, L("Init_Assembly_Start"));

            try
            {
                // 预加载位置数据
                await RunStep("预加载位置数据", PreloadPositionsAsync);
                await RefreshPositionsCacheAsync();

                // 重置工站间协调信号（防止上次初始化残留导致跳过等待）
                ResetInitSignals();

                // ===== 阶段1：组装Z轴回零 → 待机位 =====
                Logger.Info($"[{TaskName}] 阶段1：组装Z轴回零...");
                PublishTaskStatusChanged(L("Init_Assembly_ZHoming"), State);
                PublishInitProgress(10, L("Init_Assembly_ZHoming"));

                CurrentToken.ThrowIfCancellationRequested();
                if (AxisZ >= 0)
                {
                    Logger.Info($"[{TaskName}] Z 轴回零中...");
                    PublishTaskStatusChanged(L("Init_HomeAxis", "Z"), State);
                    PublishInitProgress(15, L("Init_HomeAxis", "Z"));
                    await ExecuteHomeAxisAsync(AxisZ);

                    // Z轴回到待机位
                    PublishTaskStatusChanged(L("Init_Assembly_ZStandby"), State);
                    PublishInitProgress(30, L("Init_StandbyPosition", "Z"));
                    await ExecuteMoveAsync(AxisZ, "StandbyPosition", InitZAxisVelocity);
                }

                // 通知点胶/上下料：组装Z轴回零完成
                SignalToStation("DispensingStation", "AssemblyZComplete", true);
                SignalToStation("LoadingStation", "AssemblyZComplete", true);
                Logger.Info($"[{TaskName}] 已通知点胶/上下料：组装Z轴回零完成。");

                // ===== 等待点胶Z轴完成（所有Z轴归零前提） =====
                PublishTaskStatusChanged(L("Init_Assembly_WaitDispensingZ"), State);
                PublishInitProgress(40, L("Init_Assembly_WaitDispensingZ"));
                await WaitForSignalAsync("AssemblyStation", "DispensingZComplete", true, SignalWaitTimeoutMs);
                Logger.Info($"[{TaskName}] 点胶Z轴已完成，开始组装辅助轴回零。");

                // ===== 阶段2：组装辅助轴（Cy, Ey）回零 → 待机位 =====
                PublishTaskStatusChanged(L("Init_Assembly_AuxHoming"), State);
                PublishInitProgress(50, L("Init_Assembly_AuxHoming"));

                int[] auxAxes = { AxisCy, AxisEy };
                string[] auxAxisNames = { "Cy", "Ey" };
                int aIndex = 0;

                foreach (var (axisId, axisName) in auxAxes.Zip(auxAxisNames, (id, name) => (id, name)))
                {
                    CurrentToken.ThrowIfCancellationRequested();
                    if (axisId < 0) continue;

                    Logger.Info($"[{TaskName}] {axisName} 轴回零中...");
                    PublishTaskStatusChanged(L("Init_HomeAxis", axisName), State);
                    PublishInitProgress(50 + aIndex * 10, L("Init_HomeAxis", axisName));
                    await ExecuteHomeAxisAsync(axisId);
                    aIndex++;
                }

                // 辅助轴回到待机位
                PublishTaskStatusChanged(L("Init_Assembly_AuxStandby"), State);
                foreach (var (axisId, axisName) in auxAxes.Zip(auxAxisNames, (id, name) => (id, name)))
                {
                    CurrentToken.ThrowIfCancellationRequested();
                    if (axisId < 0) continue;

                    PublishInitProgress(70 + aIndex * 5, L("Init_StandbyPosition", axisName));
                    await ExecuteMoveAsync(axisId, "StandbyPosition", InitRotaryAxisVelocity);
                }

                // ===== 等待点胶工站回零完成 =====
                PublishTaskStatusChanged(L("Init_Assembly_WaitDispensingComplete"), State);
                PublishInitProgress(80, L("Init_Assembly_WaitDispensingComplete"));
                await WaitForSignalAsync("AssemblyStation", "DispensingComplete", true, SignalWaitTimeoutMs);
                Logger.Info($"[{TaskName}] 点胶工站回零完成，开始组装主轴回零。");

                // ===== 阶段4：组装主轴（X, Ry）回零 → 待机位 =====
                PublishTaskStatusChanged(L("Init_Assembly_MainHoming"), State);
                PublishInitProgress(85, L("Init_Assembly_MainHoming"));

                int[] mainAxes = { AxisX, AxisRy };
                string[] mainAxisNames = { "X", "Ry" };
                int mIndex = 0;

                foreach (var (axisId, axisName) in mainAxes.Zip(mainAxisNames, (id, name) => (id, name)))
                {
                    CurrentToken.ThrowIfCancellationRequested();
                    if (axisId < 0) continue;

                    Logger.Info($"[{TaskName}] {axisName} 轴回零中...");
                    PublishTaskStatusChanged(L("Init_HomeAxis", axisName), State);
                    PublishInitProgress(85 + mIndex * 5, L("Init_HomeAxis", axisName));
                    await ExecuteHomeAxisAsync(axisId);
                    mIndex++;
                }

                // 主轴回到待机位
                PublishTaskStatusChanged(L("Init_Assembly_MainStandby"), State);
                foreach (var (axisId, axisName) in mainAxes.Zip(mainAxisNames, (id, name) => (id, name)))
                {
                    CurrentToken.ThrowIfCancellationRequested();
                    if (axisId < 0) continue;

                    PublishInitProgress(95 + mIndex * 3, L("Init_StandbyPosition", axisName));
                    await ExecuteMoveAsync(axisId, "StandbyPosition", InitXYAxisVelocity);
                }

                State = TaskState.Idle;
                Logger.Info($"[{TaskName}] 组装系统初始化完成，进入待机。");
                PublishTaskStatusChanged(L("Init_Idle"), State);
                PublishInitProgress(100, L("Init_Assembly_Complete"), true);
            }
            catch (System.OperationCanceledException)
            {
                State = TaskState.Error;
                Logger.Warn($"[{TaskName}] 组装系统初始化被取消。");
                PublishTaskStatusChanged(L("Init_Canceled"), State);
                PublishInitProgress(0, L("Init_Canceled"), true, true);
                throw;
            }
            catch (RecoverableException ex)
            {
                State = TaskState.Error;
                Logger.Error($"[{TaskName}] 组装系统初始化失败（等待信号超时）: {ex.Message}");
                PublishTaskStatusChanged(L("Init_Failed"), State);
                PublishInitProgress(0, L("Init_Failed"), true, true);
                throw;
            }
            catch (System.Exception ex)
            {
                State = TaskState.Error;
                Logger.Error($"[{TaskName}] 组装系统初始化失败: {ex.Message}");
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
