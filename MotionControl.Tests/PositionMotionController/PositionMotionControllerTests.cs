using Core.Abstraction;
using Core.Models;
using Core.Services;
using Core.Utilities;
using MotionControl.Interfaces;
using MotionControl.Models;
using MotionControl.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using System;

namespace MotionControl.Tests.PositionMotionController
{
    public class PositionMotionControllerTests
    {
        private readonly Mock<IStationRegistry> _stationRegistryMock;
        private readonly Mock<IMotionService> _motionServiceMock;
        private readonly Mock<ISystemStateService> _systemStateMock;
        private readonly Mock<IAxisConfigurationService> _axisConfigMock;
        private readonly Mock<ILoggerService> _loggerMock;

        public PositionMotionControllerTests()
        {
            _stationRegistryMock = new Mock<IStationRegistry>();
            _motionServiceMock = new Mock<IMotionService>();
            _systemStateMock = new Mock<ISystemStateService>();
            _axisConfigMock = new Mock<IAxisConfigurationService>();
            _loggerMock = new Mock<ILoggerService>();
        }

        private PositionMotionControllerImpl CreateController()
        {
            return new PositionMotionControllerImpl(
                _stationRegistryMock.Object,
                _motionServiceMock.Object,
                _systemStateMock.Object,
                _axisConfigMock.Object,
                _loggerMock.Object);
        }

        #region T1-T3: TeachAsync Tests

        [Fact]
        public async Task TeachAsync_读取轴位置并返回结果()
        {
            var controller = CreateController();
            var stationId = "StationA";
            var motionOps = new Mock<IStationMotionOperations>();
            motionOps.Setup(o => o.FindAxisIdByName("X")).Returns(1);
            motionOps.Setup(o => o.FindAxisIdByName("Y")).Returns(2);

            SetupStation(stationId, new List<AxisDefinition>
            {
                new() { Name = "X" },
                new() { Name = "Y" }
            }, motionOps);

            _motionServiceMock.Setup(m => m.GetAxisPosition(1)).Returns(100.5);
            _motionServiceMock.Setup(m => m.GetAxisPosition(2)).Returns(200.3);

            var result = await controller.TeachAsync(stationId);

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal(100.5, result["X"]);
            Assert.Equal(200.3, result["Y"]);
        }

        [Fact]
        public async Task TeachAsync_工站不存在时抛出异常()
        {
            var controller = CreateController();
            _stationRegistryMock.Setup(r => r.GetAllStations()).Returns(new List<IStationParameterProvider>());

            await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
                controller.TeachAsync("NonExistentStation"));
        }

        [Fact]
        public async Task TeachAsync_无轴配置时返回空字典()
        {
            var controller = CreateController();
            var stationId = "StationA";
            var motionOps = new Mock<IStationMotionOperations>();

            SetupStation(stationId, new List<AxisDefinition>(), motionOps);

            var result = await controller.TeachAsync(stationId);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        #endregion

        #region T4-T6: GotoAsync Tests

        [Fact]
        public async Task GotoAsync_调用正确轴的正确位置和速度()
        {
            var controller = CreateController();
            var stationId = "StationA";
            var targetPositions = new Dictionary<string, double> { ["X"] = 100.0, ["Y"] = 200.0 };
            var velocity = 15.0;
            var motionOps = new Mock<IStationMotionOperations>();
            motionOps.Setup(o => o.FindAxisIdByName("X")).Returns(1);
            motionOps.Setup(o => o.FindAxisIdByName("Y")).Returns(2);

            SetupStation(stationId, new List<AxisDefinition>
            {
                new() { Name = "X" },
                new() { Name = "Y" }
            }, motionOps);

            await controller.GotoAsync(stationId, targetPositions, velocity);

            motionOps.Verify(o => o.ExecuteMoveAsync(1, "PositionEditor", 15.0, 0.0), Times.Once());
            motionOps.Verify(o => o.ExecuteMoveAsync(2, "PositionEditor", 15.0, 0.0), Times.Once());
        }

        [Fact]
        public async Task GotoAsync_速度为0时使用默认速度()
        {
            var controller = CreateController();
            var stationId = "StationA";
            var targetPositions = new Dictionary<string, double> { ["X"] = 100.0 };
            var motionOps = new Mock<IStationMotionOperations>();
            motionOps.Setup(o => o.FindAxisIdByName("X")).Returns(1);

            SetupStation(stationId, new List<AxisDefinition>
            {
                new() { Name = "X" }
            }, motionOps);

            await controller.GotoAsync(stationId, targetPositions, 0);

            motionOps.Verify(o => o.ExecuteMoveAsync(It.IsAny<int>(), It.IsAny<string>(), 10.0, 0.0), Times.Once());
        }

        [Fact]
        public async Task GotoAsync_工站不存在时抛出异常()
        {
            var controller = CreateController();
            _stationRegistryMock.Setup(r => r.GetAllStations()).Returns(new List<IStationParameterProvider>());

            await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
                controller.GotoAsync("NonExistentStation",
                    new Dictionary<string, double> { ["X"] = 100.0 }, 10.0));
        }

        #endregion

        #region T7: Stop Test

        [Fact]
        public void Stop_调用所有轴的停止方法()
        {
            var controller = CreateController();
            var stationId = "StationA";
            var motionOps = new Mock<IStationMotionOperations>();
            motionOps.Setup(o => o.FindAxisIdByName("X")).Returns(1);
            motionOps.Setup(o => o.FindAxisIdByName("Y")).Returns(2);
            motionOps.Setup(o => o.FindAxisIdByName("Z")).Returns(3);

            SetupStation(stationId, new List<AxisDefinition>
            {
                new() { Name = "X" },
                new() { Name = "Y" },
                new() { Name = "Z" }
            }, motionOps);

            controller.Stop(stationId);

            _motionServiceMock.Verify(m => m.StopAxis(1), Times.Once());
            _motionServiceMock.Verify(m => m.StopAxis(2), Times.Once());
            _motionServiceMock.Verify(m => m.StopAxis(3), Times.Once());
        }

        #endregion

        #region T8-T10: CanExecuteMotion Tests

        [Fact]
        public void CanExecuteMotion_系统运行中返回false()
        {
            var controller = CreateController();
            _systemStateMock.Setup(s => s.CurrentState).Returns(StationState.RUNNING);

            var result = controller.CanExecuteMotion("StationA");

            Assert.False(result);
        }

        [Fact]
        public void CanExecuteMotion_系统空闲时返回true()
        {
            var controller = CreateController();
            _systemStateMock.Setup(s => s.CurrentState).Returns(StationState.WAITRUN);

            SetupStation("StationA",
                new List<AxisDefinition> { new() { Name = "X" } },
                new Mock<IStationMotionOperations>());

            var result = controller.CanExecuteMotion("StationA");

            Assert.True(result);
        }

        [Fact]
        public void CanExecuteMotion_工站不存在返回false()
        {
            var controller = CreateController();
            _systemStateMock.Setup(s => s.CurrentState).Returns(StationState.WAITRUN);
            _stationRegistryMock.Setup(r => r.GetAllStations()).Returns(new List<IStationParameterProvider>());

            var result = controller.CanExecuteMotion("NonExistentStation");

            Assert.False(result);
        }

        #endregion

        #region Helper Methods

        private void SetupStation(string stationId, List<AxisDefinition> axes, Mock<IStationMotionOperations> motionOps)
        {
            _axisConfigMock.Setup(a => a.GetAxesForStation(stationId)).Returns(axes);
            var testStation = new TestStationDouble(stationId, motionOps.Object);
            var stations = new List<IStationParameterProvider> { testStation };
            _stationRegistryMock.Setup(r => r.GetAllStations()).Returns(stations);
        }

        #endregion

        private class TestStationDouble : IStationParameterProvider, IStationMotionOperations
        {
            private readonly IStationMotionOperations _motionOps;

            public string StationIdentifier { get; set; }
            public string CurrentPoolName { get; set; } = "";
            public string CurrentRecipeName { get; set; } = "";
            public object CurrentParameters { get; set; } = null!;
            public bool HasUnsavedChanges { get; set; }
            public string StationIdentifierValue => StationIdentifier;

            public TestStationDouble(string id, IStationMotionOperations ops)
            {
                StationIdentifier = id;
                _motionOps = ops;
            }

            public int FindAxisIdByName(string axisName) => _motionOps.FindAxisIdByName(axisName);
            public Task ExecuteMoveAsync(int axisId, string positionName, double velocity, double offset = 0)
                => _motionOps.ExecuteMoveAsync(axisId, positionName, velocity, offset);
        }
    }
}
