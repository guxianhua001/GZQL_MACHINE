using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StationTasks.Tasks
{
    public partial class LoadingTask
    {
        private async Task PickPartAsync()
        {
            await RunStep("通知装配站取料并等待", RequestAndWaitPartAsync);
            await RunStep("夹紧工件", ClampPartAsync);
            await RunStep("Y轴升高至安全位", MoveYToSafeAsync);
        }
        private async Task RequestAndWaitPartAsync()
        {
            SignalToStation("AssemblyStation", "PartReady", true);
            await WaitForSignalAsync("AssemblyStation", "PartReceived", true, 5000);
        }
        private async Task ClampPartAsync() => await TriggerCylinderAsync(100, true, 101);
        private async Task MoveYToSafeAsync() => await MoveToAsync(AxisY, "SafePosition", 20);

        private async Task MoveToPlacePositionAsync()
        {
            var y = await GetPositionAsync("PlacePosition", "Y");
            var rx = await GetPositionAsync("PlacePosition", "U");
            var rz = await GetPositionAsync("PlacePosition", "R");

            // 简单多轴顺序运动
            await _motion.MoveAbsAsync(AxisY, y, 30, CurrentToken);
            await _motion.MoveAbsAsync(AxisRx, rx, 20, CurrentToken);
            await _motion.MoveAbsAsync(AxisRz, rz, 20, CurrentToken);
        }

        private async Task PlaceAndReleaseAsync()
        {
            SignalToStation("AssemblyStation", "PartReady", true);
            await WaitForSignalAsync("AssemblyStation", "PartReceived", true, timeoutMs: 5000);
        }

        private async Task UnclampPartAsync()
        {
            await TriggerCylinderAsync(ClawDoId, false);
        }

        private async Task ReturnToStandbyAsync()
        {
            var y = await GetPositionAsync("StandbyPosition", "Y");
            var rx = await GetPositionAsync("StandbyPosition", "U");
            var rz = await GetPositionAsync("StandbyPosition", "R");

            await _motion.MoveAbsAsync(AxisY, y, 30, CurrentToken);
            await _motion.MoveAbsAsync(AxisRx, rx, 20, CurrentToken);
            await _motion.MoveAbsAsync(AxisRz, rz, 20, CurrentToken);

            SignalToStation("AssemblyStation", "PartReady", false);
        }
        /// <summary>
        /// 手动操作示例：手动移动到拍照位（由 UI 按钮触发）
        /// </summary>
        public async Task ManualMoveToPhotoPositionAsync()
        {
            await ExecuteManualProcess("移动到拍照位", async () =>
            {
                await _motion.MoveAbsAsync(AxisY, await GetPositionAsync("PhotoPosition", "Y"), 10, CurrentToken);
                await _motion.MoveAbsAsync(AxisRx, await GetPositionAsync("PhotoPosition", "U"), 10, CurrentToken);
                // 可以在这里触发拍照信号
                // WriteDO(PhotoTriggerId, true);
                // await WaitTime(100);
                // WriteDO(PhotoTriggerId, false);
            });
        }

    }
}
