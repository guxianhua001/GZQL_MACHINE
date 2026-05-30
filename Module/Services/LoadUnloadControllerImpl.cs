using Core.Abstraction;
using Core.Services;
using Core.Utilities;
using MotionControl.Interfaces;
using MotionControl.Models;
using System;
using System.Collections.Generic;
using System.Linq;
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

        #region 真空控制

        public async Task ChuckVacuumOnAsync()
        {
            var ops = ResolveOps();
            await ops.ExecuteManualProcess("载台真空开", async () =>
            {
                ops.WriteDO(GetDoId("PlatVacValve"), true);
                ops.WriteDO(GetDoId("PlatBreakVacValve"), false);
                await ops.TriggerCylinderAsync(GetDoId("PlatVacValve"), true,
                    GetDiId("PlatVacSensor"), 3000);
            });
            _chuckVacuumStatus = VacuumStatus.On;
        }

        public async Task ChuckVacuumOffAsync()
        {
            var ops = ResolveOps();
            await ops.ExecuteManualProcess("载台真空关", async () =>
            {
                ops.WriteDO(GetDoId("PlatVacValve"), false);
                ops.WriteDO(GetDoId("PlatBreakVacValve"), true);
                await Task.Delay(200);
                ops.WriteDO(GetDoId("PlatBreakVacValve"), false);
            });
            _chuckVacuumStatus = VacuumStatus.Off;
        }

        public async Task<bool> ChuckVacuumCheckAsync()
        {
            _chuckVacuumStatus = VacuumStatus.Checking;
            try
            {
                var ops = ResolveOps();
                bool result = await ops.ExecuteManualProcess("载台真空检测", async () =>
                {
                    await Task.CompletedTask;
                }).ContinueWith(t => ops.ReadDI(GetDiId("PlatVacSensor")));
                _chuckVacuumStatus = result ? VacuumStatus.On : VacuumStatus.Off;
                return result;
            }
            catch
            {
                _chuckVacuumStatus = VacuumStatus.Unknown;
                return false;
            }
        }

        public async Task GripperVacuumOnAsync()
        {
            var ops = ResolveOps();
            await ops.ExecuteManualProcess("夹爪真空开", async () =>
            {
                ops.WriteDO(GetDoId("GripperVacValve"), true);
                await Task.Delay(200);
            });
            _gripperVacuumStatus = VacuumStatus.On;
        }

        public async Task GripperVacuumOffAsync()
        {
            var ops = ResolveOps();
            await ops.ExecuteManualProcess("夹爪真空关", async () =>
            {
                ops.WriteDO(GetDoId("GripperVacValve"), false);
                await Task.Delay(200);
            });
            _gripperVacuumStatus = VacuumStatus.Off;
        }

        public async Task<bool> GripperVacuumCheckAsync()
        {
            var ops = ResolveOps();
            return await Task.Run(() => ops.ReadDI(GetDiId("GripperVacSensor")));
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

        public async Task MoveGripperToAngleAsync(double angle)
        {
            await _gripperService.MoveToPositionAsync(angle, 10);
        }

        #endregion

        #region 自动流程

        public async Task AutoPickUpAsync()
        {
            var ops = ResolveOps();
            var axisY = ops.FindAxisIdByName("Y");
            var axisRx = ops.FindAxisIdByName("Rx");
            var axisRz = ops.FindAxisIdByName("Rz");

            await ops.ExecuteManualProcess("自动取料", async () =>
            {
                ops.WriteDO(GetDoId("PlatVacValve"), true);
                ops.WriteDO(GetDoId("PlatBreakVacValve"), false);
                await ops.TriggerCylinderAsync(GetDoId("PlatVacValve"), true,
                    GetDiId("PlatVacSensor"), 3000);

                if (axisY >= 0) await ops.ExecuteMoveAsync(axisY, "取料位", DefaultVelocity);

                await ops.TriggerCylinderAsync(GetDoId("PlatVacValve"), true,
                    GetDiId("PlatVacSensor"), 3000);

                if (axisY >= 0) await ops.ExecuteMoveAsync(axisY, "待机位", DefaultVelocity);
                if (axisRx >= 0) await ops.ExecuteMoveAsync(axisRx, "装配位1", DefaultVelocity);
                if (axisRz >= 0) await ops.ExecuteMoveAsync(axisRz, "装配位1", DefaultVelocity);
            });
            _chuckVacuumStatus = VacuumStatus.On;
        }

        public async Task AutoScanAsync()
        {
            var ops = ResolveOps();
            var axisY = ops.FindAxisIdByName("Y");

            await ops.ExecuteManualProcess("自动扫描", async () =>
            {
                if (axisY >= 0) await ops.ExecuteMoveAsync(axisY, "3D扫描位", DefaultVelocity);
                await Task.Delay(500);
                if (axisY >= 0) await ops.ExecuteMoveAsync(axisY, "待机位", DefaultVelocity);
            });
        }

        public async Task AutoUnloadAsync()
        {
            var ops = ResolveOps();
            var axisY = ops.FindAxisIdByName("Y");
            var axisRx = ops.FindAxisIdByName("Rx");
            var axisRz = ops.FindAxisIdByName("Rz");

            await ops.ExecuteManualProcess("自动下料", async () =>
            {
                if (axisY >= 0) await ops.ExecuteMoveAsync(axisY, "出料位", DefaultVelocity);

                ops.WriteDO(GetDoId("PlatVacValve"), false);
                ops.WriteDO(GetDoId("PlatBreakVacValve"), true);
                await Task.Delay(200);
                ops.WriteDO(GetDoId("PlatBreakVacValve"), false);

                if (axisY >= 0) await ops.ExecuteMoveAsync(axisY, "待机位", DefaultVelocity);
                if (axisRx >= 0) await ops.ExecuteMoveAsync(axisRx, "待机位", DefaultVelocity);
                if (axisRz >= 0) await ops.ExecuteMoveAsync(axisRz, "待机位", DefaultVelocity);
            });
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

        private int GetDoId(string portName)
        {
            var outputs = _motion.GetOutputConfigurations();
            var config = outputs.FirstOrDefault(o => o.Name == portName);
            return config?.LogicalId ?? -1;
        }

        private int GetDiId(string portName)
        {
            var inputs = _motion.GetInputConfigurations();
            var config = inputs.FirstOrDefault(o => o.Name == portName);
            return config?.LogicalId ?? -1;
        }

        #endregion
    }
}
