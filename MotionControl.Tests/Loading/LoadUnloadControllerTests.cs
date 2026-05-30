using Module.Services;
using Moq;
using MotionControl.Interfaces;
using Core.Abstraction;
using Core.Services;
using Core.Utilities;
using MotionControl.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace MotionControl.Tests
{
    public class LoadUnloadControllerTests
    {
        private LoadUnloadControllerImpl CreateController(
            out Mock<IStationRegistry> stationRegistry,
            out Mock<IMotionService> motion,
            out Mock<IGripperService> gripper,
            out Mock<ISystemStateService> systemState,
            out Mock<IAxisConfigurationService> axisConfig,
            out Mock<ILoggerService> logger)
        {
            stationRegistry = new Mock<IStationRegistry>();
            motion = new Mock<IMotionService>();
            gripper = new Mock<IGripperService>();
            systemState = new Mock<ISystemStateService>();
            axisConfig = new Mock<IAxisConfigurationService>();
            logger = new Mock<ILoggerService>();

            systemState.Setup(s => s.CurrentState).Returns(StationState.STOP);
            stationRegistry.Setup(s => s.GetAllStations())
                .Returns(new List<IStationParameterProvider>().AsReadOnly());

            return new LoadUnloadControllerImpl(
                stationRegistry.Object,
                motion.Object,
                gripper.Object,
                systemState.Object,
                axisConfig.Object,
                logger.Object);
        }

        [Fact]
        public void CanExecuteMotion_WhenIdle_NoStation_ReturnsFalse()
        {
            var controller = CreateController(out _, out _, out _, out var ss, out _, out _);
            ss.Setup(s => s.CurrentState).Returns(StationState.STOP);

            var result = controller.CanExecuteMotion();
            Assert.False(result);
        }

        [Fact]
        public void CanExecuteMotion_WhenRunning_ReturnsFalse()
        {
            var controller = CreateController(out _, out _, out _, out var ss, out _, out _);
            ss.Setup(s => s.CurrentState).Returns(StationState.RUNNING);

            var result = controller.CanExecuteMotion();
            Assert.False(result);
        }

        [Fact]
        public void StopMotion_WithNoStations_DoesNotThrow()
        {
            var controller = CreateController(out _, out _, out _, out _, out _, out _);
            controller.StopMotion();
        }

        [Fact]
        public async Task ClampAsync_CallsGripperServiceClamp()
        {
            var controller = CreateController(out _, out _, out var gripper, out _, out _, out _);
            gripper.Setup(g => g.ClampAsync(100, default)).Returns(Task.CompletedTask);

            await controller.ClampAsync();

            gripper.Verify(g => g.ClampAsync(100, default), Times.Once);
        }

        [Fact]
        public async Task ReleaseAsync_CallsGripperServiceRelease()
        {
            var controller = CreateController(out _, out _, out var gripper, out _, out _, out _);
            gripper.Setup(g => g.ReleaseAsync(0, default)).Returns(Task.CompletedTask);

            await controller.ReleaseAsync();

            gripper.Verify(g => g.ReleaseAsync(0, default), Times.Once);
        }

        [Fact]
        public async Task MoveGripperToAngleAsync_CallsMoveToPosition()
        {
            var controller = CreateController(out _, out _, out var gripper, out _, out _, out _);
            gripper.Setup(g => g.MoveToPositionAsync(90.0, 10, default)).Returns(Task.CompletedTask);

            await controller.MoveGripperToAngleAsync(90.0);

            gripper.Verify(g => g.MoveToPositionAsync(90.0, 10, default), Times.Once);
        }

        [Fact]
        public async Task GetAxisReadyStatusAsync_NoStation_ReturnsAllFalse()
        {
            var controller = CreateController(out _, out _, out _, out _, out _, out _);

            var result = await controller.GetAxisReadyStatusAsync();

            Assert.False(result["Y"]);
            Assert.False(result["Rx"]);
            Assert.False(result["Rz"]);
            Assert.False(result["Ry"]);
        }

        [Fact]
        public async Task GetRealTimePositionsAsync_NoStation_ReturnsAllZero()
        {
            var controller = CreateController(out _, out _, out _, out _, out _, out _);

            var result = await controller.GetRealTimePositionsAsync();

            Assert.Equal(0, result["Y"]);
            Assert.Equal(0, result["Rx"]);
            Assert.Equal(0, result["Rz"]);
            Assert.Equal(0, result["Ry"]);
        }

        [Fact]
        public void GetVacuumStatus_DefaultOff()
        {
            var controller = CreateController(out _, out _, out _, out _, out _, out _);
            Assert.Equal(VacuumStatus.Off, controller.GetVacuumStatus());
        }

        [Fact]
        public void GetGripperVacuumStatus_DefaultOff()
        {
            var controller = CreateController(out _, out _, out _, out _, out _, out _);
            Assert.Equal(VacuumStatus.Off, controller.GetGripperVacuumStatus());
        }
    }
}
