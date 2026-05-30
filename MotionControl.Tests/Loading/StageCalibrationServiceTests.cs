using Core.Abstraction;
using Core.Models;
using Core.Services;
using Module.Services;
using Moq;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace MotionControl.Tests
{
    public class StageCalibrationServiceTests
    {
        private StageCalibrationService CreateService(
            out Mock<ILoadUnloadController> controller,
            out Mock<Core.Utilities.ILoggerService> logger,
            out Mock<IZScanConfigService> configService)
        {
            controller = new Mock<ILoadUnloadController>();
            logger = new Mock<Core.Utilities.ILoggerService>();
            configService = new Mock<IZScanConfigService>();

            controller.Setup(c => c.CanExecuteMotion()).Returns(true);
            controller.Setup(c => c.GetRealTimePositionsAsync())
                .ReturnsAsync(new Dictionary<string, double>
                {
                    { "X", 100.0 }, { "Y", 150.5 }, { "Z", 25.0 }, { "Rx", 0.123 }, { "Rz", -0.456 }, { "Ry", 0.0 }
                });

            return new StageCalibrationService(controller.Object, logger.Object, configService.Object);
        }

        [Fact]
        public async Task GoToPhotoPositionAsync_CallsMoveToPosition()
        {
            var service = CreateService(out var controller, out _, out _);

            await service.GoToPhotoPositionAsync(100, 200, 30, 0.5, -0.2);

            controller.Verify(c => c.MoveToAssemblyPositionAsync(It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task TeachCurrentPositionAsync_ReturnsCurrentPositions()
        {
            var service = CreateService(out _, out _, out _);

            var result = await service.TeachCurrentPositionAsync();

            Assert.NotNull(result);
            Assert.Equal(150.5, result.Y, 1);
            Assert.Equal(100.0, result.X, 1);
            Assert.Equal(25.0, result.Z, 1);
        }

        [Fact]
        public async Task CaptureFiducialAsync_ReturnsResult()
        {
            var service = CreateService(out _, out _, out _);

            var result = await service.CaptureFiducialAsync(1);

            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(100.0, result.X, 1);
            Assert.Equal(150.5, result.Y, 1);
        }

        [Fact]
        public async Task ApplyCorrectionAsync_WithValidData_Succeeds()
        {
            var service = CreateService(out var controller, out _, out _);

            await service.ApplyCorrectionAsync(0.1, -0.05, 0.02);

            controller.Verify(c => c.MoveToAssemblyPositionAsync(It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task SaveCalibrationDataAsync_DoesNotThrow()
        {
            var service = CreateService(out _, out _, out var configService);
            configService.Setup(c => c.Save(It.IsAny<ZScanConfigFile>(), It.IsAny<string>()));

            await service.SaveCalibrationDataAsync();
        }

        [Fact]
        public async Task LoadCalibrationDataAsync_DoesNotThrow()
        {
            var service = CreateService(out _, out _, out var configService);
            configService.Setup(c => c.Load(It.IsAny<string>()))
                .Returns(new ZScanConfigFile());

            await service.LoadCalibrationDataAsync();
        }

        [Fact]
        public void GetCurrentCalibrationData_ReturnsDefaultData()
        {
            var service = CreateService(out _, out _, out _);

            var data = service.GetCurrentCalibrationData();

            Assert.NotNull(data);
            Assert.NotNull(data.Fiducial1);
            Assert.NotNull(data.Fiducial2);
        }

        [Fact]
        public void ApplyCalibrationData_UpdatesCurrentData()
        {
            var service = CreateService(out _, out _, out _);
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
            var service = CreateService(out _, out _, out _);

            service.ApplyCalibrationData(null);

            var current = service.GetCurrentCalibrationData();
            Assert.NotNull(current);
        }

        [Fact]
        public async Task GoToPhotoPositionAsync_WhenMotionProhibited_Throws()
        {
            var service = CreateService(out var controller, out _, out _);
            controller.Setup(c => c.CanExecuteMotion()).Returns(false);

            await Assert.ThrowsAsync<System.InvalidOperationException>(
                () => service.GoToPhotoPositionAsync(1, 2, 3, 0, 0));
        }

        [Fact]
        public async Task ApplyCorrectionAsync_WhenMotionProhibited_Throws()
        {
            var service = CreateService(out var controller, out _, out _);
            controller.Setup(c => c.CanExecuteMotion()).Returns(false);

            await Assert.ThrowsAsync<System.InvalidOperationException>(
                () => service.ApplyCorrectionAsync(0.1, 0.2, 0.3));
        }

        [Fact]
        public async Task SaveAndLoad_RoundTrip()
        {
            var service = CreateService(out _, out _, out _);
            var tempDir = Path.Combine(Path.GetTempPath(), "StageCalibTest_" + System.Guid.NewGuid().ToString("N")[..8]);

            try
            {
                var data = new StageCalibrationData
                {
                    Fiducial1 = new StageCalibrationFiducialData { PhotoX = 111, PhotoY = 222, RefX = 100, RefY = 200 },
                    Fiducial2 = new StageCalibrationFiducialData { PhotoX = 333, PhotoY = 444, RefX = 300, RefY = 400 }
                };
                service.ApplyCalibrationData(data);
                await service.SaveCalibrationDataAsync();

                var service2 = CreateService(out _, out _, out _);
                await service2.LoadCalibrationDataAsync();

                var loaded = service2.GetCurrentCalibrationData();
                Assert.Equal(111, loaded.Fiducial1.PhotoX);
                Assert.Equal(444, loaded.Fiducial2.PhotoY);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}
