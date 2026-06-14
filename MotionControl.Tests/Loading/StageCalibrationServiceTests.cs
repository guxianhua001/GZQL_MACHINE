using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using Module.Services;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using TCPIPModule.Interfaces;
using Xunit;

namespace MotionControl.Tests
{
    public class StageCalibrationServiceTests
    {
        private StageCalibrationService CreateService(
            out Mock<ILoadUnloadController> controller,
            out Mock<IPositionMotionController> motionController,
            out Mock<ITCPEventService> tcpEventService,
            out Mock<ILoggerService> logger,
            out Mock<IZScanConfigService> configService)
        {
            controller = new Mock<ILoadUnloadController>();
            motionController = new Mock<IPositionMotionController>();
            tcpEventService = new Mock<ITCPEventService>();
            logger = new Mock<ILoggerService>();
            configService = new Mock<IZScanConfigService>();

            controller.Setup(c => c.CanExecuteMotion()).Returns(true);
            controller.Setup(c => c.GetRealTimePositionsAsync())
                .ReturnsAsync(new Dictionary<string, double>
                {
                    { "X", 100.0 }, { "Y", 150.5 }, { "Z", 25.0 }, { "Rx", 0.123 }, { "Rz", -0.456 }, { "Ry", 0.0 }
                });

            motionController.Setup(m => m.CanExecuteMotion(It.IsAny<string>())).Returns(true);
            motionController.Setup(m => m.TeachAsync(It.IsAny<string>()))
                .ReturnsAsync(new Dictionary<string, double>
                {
                    { "X", 100.0 }, { "Y", 150.5 }, { "Z", 25.0 }, { "Rx", 0.123 }, { "Rz", -0.456 },
                    { "Dx", 10.5 }, { "Dy", 20.3 }, { "Dz", 5.0 }
                });
            motionController.Setup(m => m.GotoAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, double>>(), It.IsAny<double>()))
                .Returns(Task.CompletedTask);

            return new StageCalibrationService(
                controller.Object,
                motionController.Object,
                tcpEventService.Object,
                logger.Object,
                configService.Object);
        }

        [Fact]
        public async Task GoToPhotoPositionAsync_CallsMoveToPosition()
        {
            var service = CreateService(out var controller, out _, out _, out _, out _);

            await service.GoToPhotoPositionAsync(100, 200, 30, 0.5, -0.2);

            controller.Verify(c => c.MoveToAssemblyPositionAsync(It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task TeachCurrentPositionAsync_ReturnsCurrentPositions()
        {
            var service = CreateService(out _, out _, out _, out _, out _);

            var result = await service.TeachCurrentPositionAsync();

            Assert.NotNull(result);
            Assert.Equal(150.5, result.Y, 1);
            Assert.Equal(100.0, result.X, 1);
            Assert.Equal(25.0, result.Z, 1);
        }

        [Fact]
        public async Task CaptureFiducialAsync_ReturnsResult()
        {
            var service = CreateService(out _, out _, out _, out _, out _);

            var result = await service.CaptureFiducialAsync(1);

            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(100.0, result.X, 1);
            Assert.Equal(150.5, result.Y, 1);
        }

        [Fact]
        public async Task MoveToReferencePositionAsync_CallsGoto()
        {
            var service = CreateService(out _, out var motion, out _, out _, out _);

            await service.MoveToReferencePositionAsync(0.5, -0.2);

            motion.Verify(m => m.GotoAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, double>>(), It.IsAny<double>()), Times.Once);
        }

        [Fact]
        public async Task MoveCameraToPhotoPositionAsync_CallsGoto()
        {
            var service = CreateService(out _, out var motion, out _, out _, out _);

            await service.MoveCameraToPhotoPositionAsync(10.5, 20.3, 5.0);

            motion.Verify(m => m.GotoAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, double>>(), It.IsAny<double>()), Times.Once);
        }

        [Fact]
        public async Task RotateToReferenceAngleAsync_CallsGoto()
        {
            var service = CreateService(out _, out var motion, out _, out _, out _);

            await service.RotateToReferenceAngleAsync(-0.456, 0.1);

            motion.Verify(m => m.GotoAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, double>>(), It.IsAny<double>()), Times.Once);
        }

        [Fact]
        public async Task ReadCurrentPositionsAsync_ReturnsAllAxes()
        {
            var service = CreateService(out _, out _, out _, out _, out _);

            var result = await service.ReadCurrentPositionsAsync();

            Assert.NotNull(result);
            Assert.Equal(10.5, result.Dx, 1);
            Assert.Equal(20.3, result.Dy, 1);
            Assert.Equal(5.0, result.Dz, 1);
        }

        [Fact]
        public async Task ApplyCorrectionAsync_WithValidData_Succeeds()
        {
            var service = CreateService(out var controller, out _, out _, out _, out _);

            await service.ApplyCorrectionAsync(0.1, -0.05, 0.02);

            controller.Verify(c => c.MoveToAssemblyPositionAsync(It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public void GetCurrentCalibrationData_ReturnsDefaultData()
        {
            var service = CreateService(out _, out _, out _, out _, out _);

            var data = service.GetCurrentCalibrationData();

            Assert.NotNull(data);
            Assert.NotNull(data.Fiducial1);
            Assert.NotNull(data.Fiducial2);
        }

        [Fact]
        public void ApplyCalibrationData_UpdatesCurrentData()
        {
            var service = CreateService(out _, out _, out _, out _, out _);
            var newData = new StageCalibrationData
            {
                Fiducial1 = new StageCalibrationFiducialData
                {
                    PhotoX = 200, PhotoY = 300, RefX = 195, RefY = 295
                },
                Fiducial2 = new StageCalibrationFiducialData
                {
                    PhotoX = 400, PhotoY = 500, RefX = 395, RefY = 495
                }
            };

            service.ApplyCalibrationData(newData);

            var current = service.GetCurrentCalibrationData();
            Assert.Equal(200, current.Fiducial1.PhotoX);
            Assert.Equal(500, current.Fiducial2.PhotoY);
        }

        [Fact]
        public void ApplyCalibrationData_NullData_DoesNotThrow()
        {
            var service = CreateService(out _, out _, out _, out _, out _);

            service.ApplyCalibrationData(null);

            var current = service.GetCurrentCalibrationData();
            Assert.NotNull(current);
        }

        [Fact]
        public async Task GoToPhotoPositionAsync_WhenMotionProhibited_Throws()
        {
            var service = CreateService(out var controller, out _, out _, out _, out _);
            controller.Setup(c => c.CanExecuteMotion()).Returns(false);

            await Assert.ThrowsAsync<System.InvalidOperationException>(
                () => service.GoToPhotoPositionAsync(1, 2, 3, 0, 0));
        }

        [Fact]
        public async Task MoveToReferencePositionAsync_WhenMotionProhibited_Throws()
        {
            var service = CreateService(out _, out var motion, out _, out _, out _);
            motion.Setup(m => m.CanExecuteMotion(It.IsAny<string>())).Returns(false);

            await Assert.ThrowsAsync<System.InvalidOperationException>(
                () => service.MoveToReferencePositionAsync(0.5, -0.2));
        }
    }
}
