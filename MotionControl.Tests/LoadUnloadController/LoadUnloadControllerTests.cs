using Core.Abstraction;
using Core.Services;
using Core.Utilities;
using Module.Services;
using Moq;
using MotionControl.Interfaces;
using MotionControl.Models;
using MotionControl.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MotionControl.Tests.LoadUnloadController
{
    public class LoadUnloadControllerTests
    {
        private readonly Mock<IStationRegistry> _stationRegistryMock;
        private readonly Mock<IMotionService> _motionMock;
        private readonly Mock<IGripperService> _gripperMock;
        private readonly Mock<ISystemStateService> _systemStateMock;
        private readonly Mock<IAxisConfigurationService> _axisConfigMock;
        private readonly Mock<ILoggerService> _loggerMock;
        private readonly TestLoadingStation _testStation;

        public LoadUnloadControllerTests()
        {
            _stationRegistryMock = new Mock<IStationRegistry>();
            _motionMock = new Mock<IMotionService>();
            _gripperMock = new Mock<IGripperService>();
            _systemStateMock = new Mock<ISystemStateService>();
            _axisConfigMock = new Mock<IAxisConfigurationService>();
            _loggerMock = new Mock<ILoggerService>();

            _testStation = new TestLoadingStation();

            _stationRegistryMock.Setup(r => r.GetAllStations())
                .Returns(new List<IStationParameterProvider> { _testStation });

            _systemStateMock.Setup(s => s.CurrentState).Returns(StationState.STOP);

            _motionMock.Setup(m => m.GetAxisPosition(It.IsAny<int>())).Returns(100.0);
            _motionMock.Setup(m => m.GetOutputConfigurations())
                .Returns(new List<IoConfig> { new() { Name = "PlatVacValve", LogicalId = 10 } }.AsReadOnly());
            _motionMock.Setup(m => m.GetInputConfigurations())
                .Returns(new List<IoConfig> { new() { Name = "PlatVacSensor", LogicalId = 20 } }.AsReadOnly());
        }

        private LoadUnloadControllerImpl CreateController()
        {
            return new LoadUnloadControllerImpl(
                _stationRegistryMock.Object,
                _motionMock.Object,
                _gripperMock.Object,
                _systemStateMock.Object,
                _axisConfigMock.Object,
                _loggerMock.Object);
        }

        #region T1: ChuckVacuumOnAsync

        [Fact]
        public async Task T1_ChuckVacuumOnAsync_委托给LoadingTask()
        {
            var ctrl = CreateController();
            await ctrl.ChuckVacuumOnAsync();
            Assert.Contains("StageVacuumOn", _testStation.FlowLog);
        }

        #endregion

        #region T2: ChuckVacuumOffAsync

        [Fact]
        public async Task T2_ChuckVacuumOffAsync_委托给LoadingTask()
        {
            var ctrl = CreateController();
            await ctrl.ChuckVacuumOffAsync();
            Assert.Contains("StageVacuumOff", _testStation.FlowLog);
        }

        #endregion

        #region T3: MoveToPickPositionAsync

        [Fact]
        public async Task T3_MoveToPickPositionAsync_调用ExecuteMoveAsync()
        {
            var ctrl = CreateController();
            await ctrl.MoveToPickPositionAsync();
            Assert.Contains("ExecuteMove:1,取料位", _testStation.MoveLog);
        }

        #endregion

        #region T4: HomeAllAsync

        [Fact]
        public async Task T4_HomeAllAsync_依次回零三轴()
        {
            var ctrl = CreateController();
            await ctrl.HomeAllAsync();
            Assert.Contains("Home:1", _testStation.HomeLog);
            Assert.Contains("Home:2", _testStation.HomeLog);
            Assert.Contains("Home:3", _testStation.HomeLog);
        }

        #endregion

        #region T5: ClampAsync

        [Fact]
        public async Task T5_ClampAsync_调用GripperService()
        {
            var ctrl = CreateController();
            await ctrl.ClampAsync();
            _gripperMock.Verify(g => g.ClampAsync(100, It.IsAny<CancellationToken>()), Times.Once());
        }

        #endregion

        #region T6: AutoPickUpAsync

        [Fact]
        public async Task T6_AutoPickUpAsync_委托给LoadingTask()
        {
            var ctrl = CreateController();
            await ctrl.AutoPickUpAsync();
            Assert.Contains("AutoPickUp", _testStation.FlowLog);
        }

        #endregion

        #region T7: CanExecuteMotion

        [Fact]
        public void T7a_CanExecuteMotion_系统运行时返回false()
        {
            _systemStateMock.Setup(s => s.CurrentState).Returns(StationState.RUNNING);
            var ctrl = CreateController();
            Assert.False(ctrl.CanExecuteMotion());
        }

        [Fact]
        public void T7b_CanExecuteMotion_系统空闲时返回true()
        {
            _systemStateMock.Setup(s => s.CurrentState).Returns(StationState.STOP);
            var ctrl = CreateController();
            Assert.True(ctrl.CanExecuteMotion());
        }

        #endregion

        #region T8: GetRealTimePositionsAsync

        [Fact]
        public async Task T8_GetRealTimePositionsAsync_返回轴位置字典()
        {
            var ctrl = CreateController();
            var positions = await ctrl.GetRealTimePositionsAsync();
            Assert.Equal(4, positions.Count);
            Assert.Equal(100.0, positions["Y"]);
        }

        #endregion

        #region Test Helper

        /// <summary>
        /// 测试用上下料工站，同时实现 IStationParameterProvider 和 ILoadUnloadStationOperations
        /// </summary>
        private class TestLoadingStation : IStationParameterProvider, ILoadUnloadStationOperations
        {
            public string StationIdentifier => "LoadingStation";
            public string CurrentPoolName => "TestPool";
            public string CurrentRecipeName => "Default";
            public object CurrentParameters => null;
            public bool HasUnsavedChanges => false;
            public string StationIdentifierValue => "LoadingStation";

            public List<string> DoWriteLog { get; } = new();
            public List<string> MoveLog { get; } = new();
            public List<string> HomeLog { get; } = new();
            public List<string> FlowLog { get; } = new();

            private readonly Dictionary<string, int> _axisMap = new()
            {
                ["Y"] = 1, ["Rx"] = 2, ["Rz"] = 3, ["Ry"] = 4
            };

            public Task ExecuteManualProcess(string processName, Func<Task> action)
            {
                return action();
            }

            public int FindAxisIdByName(string axisName)
            {
                return _axisMap.TryGetValue(axisName, out var id) ? id : -1;
            }

            public Task ExecuteMoveAsync(int axisId, string positionName, double velocity, double offset = 0)
            {
                MoveLog.Add($"ExecuteMove:{axisId},{positionName}");
                return Task.CompletedTask;
            }

            public Task ExecuteHomeAsync(int axisId, int mode = 1, double minVel = 5, double maxVel = 20)
            {
                HomeLog.Add($"Home:{axisId}");
                return Task.CompletedTask;
            }

            public Task<bool> IsAxisHomedAsync(int axisId)
            {
                return Task.FromResult(true);
            }

            public Task TriggerCylinderAsync(int doId, bool value, int diId = -1, int timeoutMs = 3000, int blindDelayMs = 300)
            {
                return Task.CompletedTask;
            }

            public void WriteDO(int logicalId, bool value)
            {
                DoWriteLog.Add($"{logicalId}={value}");
            }

            public bool ReadDI(int logicalId)
            {
                return true;
            }

            // 新增接口方法：测试桩实现（记录调用日志）
            public Task StageVacuumOnAsync(CancellationToken token = default) { FlowLog.Add("StageVacuumOn"); return Task.CompletedTask; }
            public Task StageVacuumOffAsync(CancellationToken token = default) { FlowLog.Add("StageVacuumOff"); return Task.CompletedTask; }
            public bool IsStageVacuumOn() => true;
            public Task GripperVacuumOnAsync(CancellationToken token = default) { FlowLog.Add("GripperVacuumOn"); return Task.CompletedTask; }
            public Task GripperVacuumOffAsync(CancellationToken token = default) { FlowLog.Add("GripperVacuumOff"); return Task.CompletedTask; }
            public bool IsGripperVacuumOn() => true;
            public Task AutoPickUpFlowAsync(CancellationToken token) { FlowLog.Add("AutoPickUp"); return Task.CompletedTask; }
            public Task AutoScanFlowAsync(CancellationToken token) { FlowLog.Add("AutoScan"); return Task.CompletedTask; }
            public Task AutoUnloadFlowAsync(CancellationToken token) { FlowLog.Add("AutoUnload"); return Task.CompletedTask; }
        }

        #endregion
    }
}
