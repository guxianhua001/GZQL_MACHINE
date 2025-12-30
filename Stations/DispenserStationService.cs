using Core.Abstraction;
using Core.Utilities;
using System;

namespace Stations
{
    public class DispenserStationService : IStationCancelOperationService
    {
        private readonly DispenserStation _dispenserStation;
        private readonly ILoggerService _logger;

        public DispenserStationService(DispenserStation dispenserStation, ILoggerService logger)
        {
            _dispenserStation = dispenserStation;
            _logger = logger;
        }

        public void CancelCurrentOperation()
        {
            try
            {
                _logger.Info("执行点胶工站取消操作");

                // 停止所有轴运动
                StopAllAxes();

                // 取消所有进行中的操作
                _dispenserStation?.CancelCurrentOperation();
            }
            catch (Exception ex)
            {
                _logger.Error($"取消点胶工站操作时发生异常: {ex.Message}");
            }
        }

        public void StopAllAxes()
        {
            try
            {
                _logger.Info("停止所有轴运动");
                _dispenserStation?.StopAllAxes();
            }
            catch (Exception ex)
            {
                _logger.Error($"停止轴运动时发生异常: {ex.Message}");
            }
        }
    }
}
