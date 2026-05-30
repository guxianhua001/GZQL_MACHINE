using Core.Abstraction;
using Core.Models;
using Moq;
using Prism.Services.Dialogs;
using System.Threading.Tasks;
using Xunit;

namespace MotionControl.Tests
{
    public class ProductCalibrationViewModelTests
    {
        private Mock<IStageCalibrationService> CreateMockCalibrationService()
        {
            var mock = new Mock<IStageCalibrationService>();
            mock.Setup(s => s.CaptureFiducialAsync(It.IsAny<int>()))
                .ReturnsAsync((int idx) => new FiducialCaptureResult
                {
                    Success = true,
                    X = 100.05,
                    Y = 150.03,
                    Angle = 0.012
                });
            mock.Setup(s => s.TeachCurrentPositionAsync())
                .ReturnsAsync(new CurrentPositionResult
                {
                    X = 120, Y = 180, Z = 25, Rx = 0.5, Rz = -0.2
                });
            mock.Setup(s => s.GoToPhotoPositionAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>()))
                .Returns(Task.CompletedTask);
            mock.Setup(s => s.ApplyCorrectionAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>()))
                .Returns(Task.CompletedTask);
            mock.Setup(s => s.SaveCalibrationDataAsync())
                .Returns(Task.CompletedTask);
            mock.Setup(s => s.LoadCalibrationDataAsync())
                .Returns(Task.CompletedTask);
            mock.Setup(s => s.GetCurrentCalibrationData())
                .Returns(new StageCalibrationData());
            return mock;
        }

        [Fact]
        public void FiducialData_DefaultValues_AreSet()
        {
            var mockDialog = new Mock<IDialogService>();
            var fiducial = new Module.ViewModels.FiducialData(mockDialog.Object, "Test");

            Assert.Equal(100, fiducial.PhotoX);
            Assert.Equal(150, fiducial.PhotoY);
            Assert.Equal(20, fiducial.PhotoZ);
        }

        [Fact]
        public void FiducialData_OffsetDisplay_ShowsCorrectFormat()
        {
            var mockDialog = new Mock<IDialogService>();
            var fiducial = new Module.ViewModels.FiducialData(mockDialog.Object, "Test");

            var display = fiducial.OffsetDisplay;
            Assert.Contains("ΔX", display);
            Assert.Contains("ΔY", display);
            Assert.Contains("ΔAngle", display);
        }

        [Fact]
        public void FiducialData_CorrectCommand_CanExecuteAfterCapture()
        {
            var mockDialog = new Mock<IDialogService>();
            var fiducial = new Module.ViewModels.FiducialData(mockDialog.Object, "Test");

            Assert.False(fiducial.CorrectCommand.CanExecute(null));

            fiducial.OnCaptureFromService(new FiducialCaptureResult
            {
                Success = true,
                X = 100.05,
                Y = 150.03,
                Angle = 0.012
            });

            Assert.True(fiducial.CorrectCommand.CanExecute(null));
        }

        [Fact]
        public void FiducialData_OnCaptureFromService_UpdatesMeasuredValues()
        {
            var mockDialog = new Mock<IDialogService>();
            var fiducial = new Module.ViewModels.FiducialData(mockDialog.Object, "Test");

            fiducial.OnCaptureFromService(new FiducialCaptureResult
            {
                Success = true,
                X = 100.05,
                Y = 150.03,
                Angle = 0.012
            });

            Assert.Equal(100.05, fiducial.MeasuredX, 3);
            Assert.Equal(150.03, fiducial.MeasuredY, 3);
            Assert.Equal(0.012, fiducial.MeasuredAngle, 3);
        }

        [Fact]
        public void FiducialData_OnTeachFromService_UpdatesPhotoPosition()
        {
            var mockDialog = new Mock<IDialogService>();
            var fiducial = new Module.ViewModels.FiducialData(mockDialog.Object, "Test");

            fiducial.OnTeachFromService(new CurrentPositionResult
            {
                X = 120, Y = 180, Z = 25, Rx = 0.5, Rz = -0.2
            });

            Assert.Equal(120, fiducial.PhotoX);
            Assert.Equal(180, fiducial.PhotoY);
            Assert.Equal(25, fiducial.PhotoZ);
            Assert.Equal(0.5, fiducial.PhotoRx, 3);
            Assert.Equal(-0.2, fiducial.PhotoRz, 3);
        }

        [Fact]
        public void FiducialData_ToData_MapsAllProperties()
        {
            var mockDialog = new Mock<IDialogService>();
            var fiducial = new Module.ViewModels.FiducialData(mockDialog.Object, "Test");
            fiducial.PhotoX = 111;
            fiducial.PhotoY = 222;
            fiducial.PhotoZ = 33;
            fiducial.PhotoRx = 0.4;
            fiducial.PhotoRz = -0.1;
            fiducial.RefX = 100;
            fiducial.RefY = 200;
            fiducial.RefAngle = 0.5;

            var data = fiducial.ToData();

            Assert.Equal(111, data.PhotoX);
            Assert.Equal(222, data.PhotoY);
            Assert.Equal(33, data.PhotoZ);
            Assert.Equal(0.4, data.PhotoRx, 3);
            Assert.Equal(-0.1, data.PhotoRz, 3);
            Assert.Equal(100, data.RefX);
            Assert.Equal(200, data.RefY);
            Assert.Equal(0.5, data.RefAngle, 3);
        }

        [Fact]
        public void FiducialData_FromData_RestoresAllProperties()
        {
            var mockDialog = new Mock<IDialogService>();
            var fiducial = new Module.ViewModels.FiducialData(mockDialog.Object, "Test");

            var data = new StageCalibrationFiducialData
            {
                PhotoX = 555, PhotoY = 666, PhotoZ = 77,
                PhotoRx = 1.2, PhotoRz = -3.4,
                RefX = 500, RefY = 600, RefAngle = 0.9,
                MeasuredX = 501, MeasuredY = 601, MeasuredAngle = 0.91
            };

            fiducial.FromData(data);

            Assert.Equal(555, fiducial.PhotoX);
            Assert.Equal(666, fiducial.PhotoY);
            Assert.Equal(77, fiducial.PhotoZ);
            Assert.Equal(500, fiducial.RefX);
            Assert.Equal(600, fiducial.RefY);
            Assert.Equal(501, fiducial.MeasuredX);
        }

        [Fact]
        public void FiducialData_FromData_Null_DoesNotThrow()
        {
            var mockDialog = new Mock<IDialogService>();
            var fiducial = new Module.ViewModels.FiducialData(mockDialog.Object, "Test");

            fiducial.FromData(null);

            Assert.Equal(100, fiducial.PhotoX);
        }

        [Fact]
        public void ViewModel_HasSaveAndLoadCommands()
        {
            var mockDialog = new Mock<IDialogService>();
            var mockService = CreateMockCalibrationService();
            var vm = new Module.ViewModels.ProductCalibrationViewModel(mockDialog.Object, mockService.Object);

            Assert.NotNull(vm.SaveCalibrationCommand);
            Assert.NotNull(vm.LoadCalibrationCommand);
            Assert.True(vm.SaveCalibrationCommand.CanExecute(null));
            Assert.True(vm.LoadCalibrationCommand.CanExecute(null));
        }

        [Fact]
        public async Task ViewModel_SaveCalibration_CallsService()
        {
            var mockDialog = new Mock<IDialogService>();
            var mockService = CreateMockCalibrationService();
            var vm = new Module.ViewModels.ProductCalibrationViewModel(mockDialog.Object, mockService.Object);

            vm.SaveCalibrationCommand.Execute(null);

            mockService.Verify(s => s.SaveCalibrationDataAsync(), Times.Once);
        }

        [Fact]
        public async Task ViewModel_LoadCalibration_CallsService()
        {
            var mockDialog = new Mock<IDialogService>();
            var mockService = CreateMockCalibrationService();
            var vm = new Module.ViewModels.ProductCalibrationViewModel(mockDialog.Object, mockService.Object);

            vm.LoadCalibrationCommand.Execute(null);

            mockService.Verify(s => s.LoadCalibrationDataAsync(), Times.Once);
        }
    }
}
