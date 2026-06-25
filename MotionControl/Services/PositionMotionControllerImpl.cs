using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Abstraction;
using Core.Models;
using Core.Services;
using Core.Utilities;
using MotionControl.Interfaces;
using MotionControl.Models;

namespace MotionControl.Services
{
    public class PositionMotionControllerImpl : IPositionMotionController
    {
        private readonly IStationRegistry _stationRegistry;
        private readonly IMotionService _motion;
        private readonly ISystemStateService _systemState;
        private readonly IAxisConfigurationService _axisConfig;
        private readonly ILoggerService _logger;
        private readonly IMotionInterlockService _motionInterlock;

        private const double DefaultVelocity = 10.0;

        public PositionMotionControllerImpl(
            IStationRegistry stationRegistry,
            IMotionService motion,
            ISystemStateService systemState,
            IAxisConfigurationService axisConfig,
            ILoggerService logger,
            IMotionInterlockService motionInterlock)
        {
            _stationRegistry = stationRegistry;
            _motion = motion;
            _systemState = systemState;
            _axisConfig = axisConfig;
            _logger = logger;
            _motionInterlock = motionInterlock;
        }

        public async Task<Dictionary<string, double>> TeachAsync(string stationIdentifier)
        {
            var motionOps = ResolveMotionOps(stationIdentifier);
            var axes = _axisConfig.GetAxesForStation(stationIdentifier);
            var result = new Dictionary<string, double>();

            foreach (var axis in axes)
            {
                var axisId = motionOps.FindAxisIdByName(axis.Name);
                if (axisId >= 0)
                {
                    try
                    {
                        var position = _motion.GetAxisPosition(axisId);
                        result[axis.Name] = position;
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"Teach: 读取轴 {axis.Name}(ID={axisId}) 位置失败: {ex.Message}");
                    }
                }
            }

            return result;
        }

        public async Task GotoAsync(string stationIdentifier, Dictionary<string, double> targetPositions, double velocity)
        {
            _motionInterlock.EnsureManualMotionAllowed();
            var motionOps = ResolveMotionOps(stationIdentifier);
            var effectiveVelocity = velocity > 0 ? velocity : DefaultVelocity;

            foreach (var kvp in targetPositions)
            {
                var axisName = kvp.Key;
                var targetPos = kvp.Value;
                var axisId = motionOps.FindAxisIdByName(axisName);

                if (axisId >= 0)
                {
                    await motionOps.ExecuteMoveAsync(axisId, "PositionEditor", effectiveVelocity);
                }
            }
        }

        public void Stop(string stationIdentifier)
        {
            var axes = _axisConfig.GetAxesForStation(stationIdentifier);

            foreach (var axis in axes)
            {
                var motionOps = ResolveMotionOps(stationIdentifier);
                var axisId = motionOps.FindAxisIdByName(axis.Name);
                if (axisId >= 0)
                {
                    try
                    {
                        _motion.StopAxis(axisId);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"Stop: 停止轴 {axis.Name}(ID={axisId}) 失败: {ex.Message}");
                    }
                }
            }
        }

        public bool CanExecuteMotion(string stationIdentifier)
        {
            if (!_motionInterlock.CanExecuteManualMotion)
                return false;

            var runningStates = new[]
            {
                StationState.RUNNING,
                StationState.RESETING,
                StationState.CLEAR
            };

            if (runningStates.Contains(_systemState.CurrentState))
                return false;

            var station = _stationRegistry.GetAllStations()
                .FirstOrDefault(s => s.StationIdentifier == stationIdentifier);

            if (station == null)
                return false;

            if (station is not IStationMotionOperations)
                return false;

            return true;
        }

        private IStationMotionOperations ResolveMotionOps(string stationIdentifier)
        {
            var station = _stationRegistry.GetAllStations()
                .FirstOrDefault(s => s.StationIdentifier == stationIdentifier);

            if (station == null)
                throw new InvalidOperationException(
                    $"工站 '{stationIdentifier}' 未在工站注册表中找到，无法执行运动操作");

            if (station is not IStationMotionOperations motionOps)
                throw new InvalidOperationException(
                    $"工站 '{stationIdentifier}' 不支持直接运动操作（未实现 IStationMotionOperations）");

            return motionOps;
        }
    }
}
