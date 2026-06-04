using Core.Abstraction;
using Core.Services;
using Core.Utilities;
using MotionControl.Interfaces;
using MotionControl.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Module.Services
{
    public class LoadUnloadControllerImpl : ILoadUnloadController
    {
        private readonly IStationRegistry _stationRegistry;
        private readonly IMotionService _motion;
        private readonly IGripperService _gripperService;
        private readonly ISystemStateService _systemState;
        private readonly IAxisConfigurationService _axisConfig;
        private readonly ILoggerService _logger;

        private const string StationIdentifier = "LoadingStation";
        private const double DefaultVelocity = 50.0;

        private VacuumStatus _chuckVacuumStatus = VacuumStatus.Off;
        private VacuumStatus _gripperVacuumStatus = VacuumStatus.Off;

        public LoadUnloadControllerImpl(
            IStationRegistry stationRegistry,
            IMotionService motion,
            IGripperService gripperService,
            ISystemStateService systemState,
            IAxisConfigurationService axisConfig,
            ILoggerService logger)
        {
            _stationRegistry = stationRegistry;
            _motion = motion;
            _gripperService = gripperService;
            _systemState = systemState;
            _axisConfig = axisConfig;
            _logger = logger;
        }

        #region 平台真空控制（转发给 LoadingTask）

        /// <summary>
        /// 开平台真空：委托给 LoadingTask.StageVacuumOnAsync
        /// </summary>
        public async Task ChuckVacuumOnAsync()
        {
            var ops = ResolveOps();
            await ops.StageVacuumOnAsync();
            _chuckVacuumStatus = VacuumStatus.On;
        }

        /// <summary>
        /// 破平台真空：委托给 LoadingTask.StageVacuumOffAsync
        /// </summary>
        public async Task ChuckVacuumOffAsync()
        {
            var ops = ResolveOps();
            await ops.StageVacuumOffAsync();
            _chuckVacuumStatus = VacuumStatus.Off;
        }

        #endregion

        #region 夹爪真空控制（转发给 LoadingTask）

        /// <summary>
        /// 开夹爪真空：委托给 LoadingTask.GripperVacuumOnAsync
        /// </summary>
        public async Task GripperVacuumOnAsync()
        {
            var ops = ResolveOps();
            await ops.GripperVacuumOnAsync();
            _gripperVacuumStatus = VacuumStatus.On;
        }

        /// <summary>
        /// 关夹爪真空：委托给 LoadingTask.GripperVacuumOffAsync
        /// </summary>
        public async Task GripperVacuumOffAsync()
        {
            var ops = ResolveOps();
            await ops.GripperVacuumOffAsync();
            _gripperVacuumStatus = VacuumStatus.Off;
        }

        #endregion

        #region 轴定位

        public async Task MoveToPickPositionAsync()
        {
            var ops = ResolveOps();
            var axisY = ops.FindAxisIdByName("Y");
            await ops.ExecuteManualProcess("移动到取料位", async () =>
            {
                await ops.ExecuteMoveAsync(axisY, "取料位", DefaultVelocity);
            });
        }

        public async Task MoveToScanPositionAsync()
        {
            var ops = ResolveOps();
            var axisY = ops.FindAxisIdByName("Y");
            await ops.ExecuteManualProcess("移动到3D扫描位", async () =>
            {
                await ops.ExecuteMoveAsync(axisY, "3D扫描位", DefaultVelocity);
            });
        }

        public async Task MoveToUnloadPositionAsync()
        {
            var ops = ResolveOps();
            var axisY = ops.FindAxisIdByName("Y");
            await ops.ExecuteManualProcess("移动到出料位", async () =>
            {
                await ops.ExecuteMoveAsync(axisY, "出料位", DefaultVelocity);
            });
        }

        public async Task MoveToAssemblyPositionAsync(int siteIndex)
        {
            var ops = ResolveOps();
            var axisU = ops.FindAxisIdByName("Rx");
            var axisR = ops.FindAxisIdByName("Rz");
            var positionName = $"装配位{siteIndex}";
            await ops.ExecuteManualProcess($"移动到{positionName}", async () =>
            {
                if (axisU >= 0) await ops.ExecuteMoveAsync(axisU, positionName, DefaultVelocity);
                if (axisR >= 0) await ops.ExecuteMoveAsync(axisR, positionName, DefaultVelocity);
            });
        }

        public async Task HomeAllAsync()
        {
            var ops = ResolveOps();
            var axisY = ops.FindAxisIdByName("Y");
            var axisRx = ops.FindAxisIdByName("Rx");
            var axisRz = ops.FindAxisIdByName("Rz");

            await ops.ExecuteManualProcess("平台回零", async () =>
            {
                if (axisY >= 0) await ops.ExecuteHomeAsync(axisY, 1, 5, 20);
                if (axisRx >= 0) await ops.ExecuteHomeAsync(axisRx, 1, 5, 20);
                if (axisRz >= 0) await ops.ExecuteHomeAsync(axisRz, 1, 5, 20);

                if (axisY >= 0) await ops.ExecuteMoveAsync(axisY, "待机位", DefaultVelocity);
                if (axisRx >= 0) await ops.ExecuteMoveAsync(axisRx, "待机位", DefaultVelocity);
                if (axisRz >= 0) await ops.ExecuteMoveAsync(axisRz, "待机位", DefaultVelocity);
            });
        }

        #endregion

        #region 夹爪操作

        public async Task ClampAsync()
        {
            await _gripperService.ClampAsync(100);
        }

        public async Task ReleaseAsync()
        {
            await _gripperService.ReleaseAsync(0);
        }

        #endregion

        #region 自动流程（转发给 LoadingTask）

        /// <summary>
        /// 自动取料：委托给 LoadingTask.AutoPickUpFlowAsync
        /// </summary>
        public async Task AutoPickUpAsync()
        {
            var ops = ResolveOps();
            await ops.AutoPickUpFlowAsync(CancellationToken.None);
            _chuckVacuumStatus = VacuumStatus.On;
        }

        /// <summary>
        /// 自动扫描：委托给 LoadingTask.AutoScanFlowAsync
        /// </summary>
        public async Task AutoScanAsync()
        {
            var ops = ResolveOps();
            await ops.AutoScanFlowAsync(CancellationToken.None);
        }

        /// <summary>
        /// 自动下料：委托给 LoadingTask.AutoUnloadFlowAsync
        /// </summary>
        public async Task AutoUnloadAsync()
        {
            var ops = ResolveOps();
            await ops.AutoUnloadFlowAsync(CancellationToken.None);
            _chuckVacuumStatus = VacuumStatus.Off;
        }

        #endregion

        #region 状态查询

        public async Task<Dictionary<string, bool>> GetAxisReadyStatusAsync()
        {
            var ops = ResolveOpsOrNull();
            var result = new Dictionary<string, bool>();

            foreach (var axisName in new[] { "Y", "Rx", "Rz", "Ry" })
            {
                if (ops != null)
                {
                    var axisId = ops.FindAxisIdByName(axisName);
                    result[axisName] = axisId >= 0 && await ops.IsAxisHomedAsync(axisId);
                }
                else
                {
                    result[axisName] = false;
                }
            }

            return result;
        }

        public async Task<Dictionary<string, double>> GetRealTimePositionsAsync()
        {
            var ops = ResolveOpsOrNull();
            var result = new Dictionary<string, double>();

            foreach (var axisName in new[] { "Y", "Rx", "Rz", "Ry" })
            {
                if (ops != null)
                {
                    var axisId = ops.FindAxisIdByName(axisName);
                    if (axisId >= 0)
                    {
                        try { result[axisName] = _motion.GetAxisPosition(axisId); }
                        catch { result[axisName] = 0; }
                    }
                    else
                    {
                        result[axisName] = 0;
                    }
                }
                else
                {
                    result[axisName] = 0;
                }
            }

            return result;
        }

        public VacuumStatus GetVacuumStatus() => _chuckVacuumStatus;
        public VacuumStatus GetGripperVacuumStatus() => _gripperVacuumStatus;

        public bool CanExecuteMotion()
        {
            var runningStates = new[]
            {
                StationState.RUNNING,
                StationState.RESETING,
                StationState.CLEAR
            };

            if (runningStates.Contains(_systemState.CurrentState))
                return false;

            return ResolveOpsOrNull() != null;
        }

        #endregion

        #region 停止

        public void StopMotion()
        {
            var ops = ResolveOpsOrNull();
            if (ops == null) return;

            foreach (var axisName in new[] { "Y", "Rx", "Rz", "Ry" })
            {
                var axisId = ops.FindAxisIdByName(axisName);
                if (axisId >= 0)
                {
                    try { _motion.StopAxis(axisId); }
                    catch (Exception ex) { _logger.Warn($"StopMotion: 停止轴 {axisName} 失败: {ex.Message}"); }
                }
            }
        }

        #endregion

        #region 私有方法

        private ILoadUnloadStationOperations ResolveOps()
        {
            var station = _stationRegistry.GetAllStations()
                .FirstOrDefault(s => s.StationIdentifier == StationIdentifier);

            if (station == null)
                throw new InvalidOperationException(
                    $"工站 '{StationIdentifier}' 未在工站注册表中找到");

            if (station is not ILoadUnloadStationOperations ops)
                throw new InvalidOperationException(
                    $"工站 '{StationIdentifier}' 不支持上下料运动操作");

            return ops;
        }

        private ILoadUnloadStationOperations ResolveOpsOrNull()
        {
            try { return ResolveOps(); }
            catch { return null; }
        }

        #endregion
    }
}
