using Core.Events;
using MotionControl.Exceptions;
using MotionControl.Interfaces;
using Prism.Events;
using System.Threading;
using System.Threading.Tasks;

namespace StationTasks.Tasks
{
    /// <summary>
    /// LoadingTask partial class — 整机初始化动作（HomeAsync 重写）。
    /// 初始化逻辑不写在工站主任务中，独立放在 Init.cs 文件。
    /// 回零命令使用控制卡内已配置的回零参数（ExecuteHomeAxisAsync），无需设置回零参数。
    /// 工站间协调使用 SignalToStation / WaitForSignalAsync 信号交互。
    /// </summary>
    public partial class LoadingTask
    {
        /// <summary> 上下料轴回零运动速度（mm/s） </summary>
        private const double InitLoadingAxisVelocity = 10.0;
        /// <summary> 工站间信号等待超时（ms），允许慢速回零 </summary>
        private const int SignalWaitTimeoutMs = 120000;

        /// <summary>
        /// 上下料工站整机初始化（重写 HomeAsync）。
        /// 时序：
        /// 1. 等待所有Z轴归零完成（点胶Z轴 + 组装Z轴）
        /// 2. 回零 Y/Rz/Rx → 回到待机位
        /// </summary>
        public override async Task HomeAsync()
        {
            // 设置取消令牌，支持初始化过程中的停止/急停
            _cts = new CancellationTokenSource();
            State = TaskState.Homing;
            Logger.Info($"[{TaskName}] 开始上下料系统初始化...");
            PublishTaskStatusChanged(L("Init_Initializing"), State);
            PublishInitProgress(0, L("Init_Loading_Start"));

            try
            {
                // 预加载位置数据
                await RunStep("预加载位置数据", PreloadPositionsAsync);
                await RefreshPositionsCacheAsync();

                // 重置工站间协调信号（防止上次初始化残留导致跳过等待）
                ResetInitSignals();

                // ===== 等待所有Z轴归零完成（点胶Z轴 + 组装Z轴） =====
                Logger.Info($"[{TaskName}] 等待所有Z轴归零完成...");
                PublishTaskStatusChanged(L("Init_Loading_WaitZ"), State);
                PublishInitProgress(10, L("Init_Loading_WaitZ"));

                // 等待点胶Z轴完成
                await WaitForSignalAsync("LoadingStation", "DispensingZComplete", true, SignalWaitTimeoutMs);
                Logger.Info($"[{TaskName}] 点胶Z轴已完成。");
                PublishInitProgress(30, L("Init_Loading_DispensingZDone"));

                // 等待组装Z轴完成
                await WaitForSignalAsync("LoadingStation", "AssemblyZComplete", true, SignalWaitTimeoutMs);
                Logger.Info($"[{TaskName}] 组装Z轴已完成，开始上下料轴回零。");
                PublishInitProgress(50, L("Init_Loading_AssemblyZDone"));

                // ===== 阶段2：上下料轴（Y, Rz, Rx）回零 =====
                PublishTaskStatusChanged(L("Init_Loading_AxesHoming"), State);
                PublishInitProgress(55, L("Init_Loading_AxesHoming"));

                int[] axes = { AxisY, AxisRz, AxisRx };
                string[] axisNames = { "Y", "Rz", "Rx" };
                int axisIndex = 0;

                foreach (var (axisId, axisName) in axes.Zip(axisNames, (id, name) => (id, name)))
                {
                    CurrentToken.ThrowIfCancellationRequested();
                    if (axisId < 0) continue;

                    Logger.Info($"[{TaskName}] {axisName} 轴回零中...");
                    PublishTaskStatusChanged(L("Init_HomeAxis", axisName), State);
                    PublishInitProgress(55 + axisIndex * 8, L("Init_HomeAxis", axisName));
                    await ExecuteHomeAxisAsync(axisId);
                    axisIndex++;
                }

                // ===== 阶段3：上下料轴回到待机位 =====
                PublishTaskStatusChanged(L("Init_Loading_AxesStandby"), State);
                int standbyIndex = 0;
                foreach (var (axisId, axisName) in axes.Zip(axisNames, (id, name) => (id, name)))
                {
                    CurrentToken.ThrowIfCancellationRequested();
                    if (axisId < 0) continue;

                    PublishInitProgress(80 + standbyIndex * 6, L("Init_StandbyPosition", axisName));
                    await ExecuteMoveAsync(axisId, "StandbyPosition", InitLoadingAxisVelocity);
                    standbyIndex++;
                }

                State = TaskState.Idle;
                Logger.Info($"[{TaskName}] 上下料系统初始化完成，进入待机。");
                PublishTaskStatusChanged(L("Init_Idle"), State);
                PublishInitProgress(100, L("Init_Loading_Complete"), true);
            }
            catch (System.OperationCanceledException)
            {
                State = TaskState.Error;
                Logger.Warn($"[{TaskName}] 上下料系统初始化被取消。");
                PublishTaskStatusChanged(L("Init_Canceled"), State);
                PublishInitProgress(0, L("Init_Canceled"), true, true);
                throw;
            }
            catch (RecoverableException ex)
            {
                State = TaskState.Error;
                Logger.Error($"[{TaskName}] 上下料系统初始化失败（等待信号超时）: {ex.Message}");
                PublishTaskStatusChanged(L("Init_Failed"), State);
                PublishInitProgress(0, L("Init_Failed"), true, true);
                throw;
            }
            catch (System.Exception ex)
            {
                State = TaskState.Error;
                Logger.Error($"[{TaskName}] 上下料系统初始化失败: {ex.Message}");
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
