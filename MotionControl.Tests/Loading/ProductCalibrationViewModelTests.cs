using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using Framework.Dialogs;
using Moq;
using Prism.Events;
using Recipe.Interfaces;
using System.Threading.Tasks;
using TCPIPModule.Interfaces;
using Xunit;
using Module.ViewModels;

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
            mock.Setup(s => s.ReadCurrentPositionsAsync())
                .ReturnsAsync(new CurrentPositionResult
                {
                    X = 120, Y = 180, Z = 25, Rx = 0.5, Rz = -0.2,
                    Dx = 10.5, Dy = 20.3, Dz = 5.0
                });
            mock.Setup(s => s.GoToPhotoPositionAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>()))
                .Returns(Task.CompletedTask);
            mock.Setup(s => s.MoveToReferencePositionAsync(It.IsAny<double>(), It.IsAny<double>()))
                .Returns(Task.CompletedTask);
            mock.Setup(s => s.MoveCameraToPhotoPositionAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>()))
                .Returns(Task.CompletedTask);
            mock.Setup(s => s.TriggerCaptureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new FiducialCaptureResult { Success = true, X = 50.5, Y = 60.3 });
            mock.Setup(s => s.RotateToReferenceAngleAsync(It.IsAny<double>(), It.IsAny<double>()))
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

        private ProductCalibrationViewModel CreateViewModel()
        {
            var calibService = CreateMockCalibrationService();
            var tcpEvent = new Mock<ITCPEventService>();
            var tcpClient = new Mock<ITCPClientManagerService>();
            var paramStorage = new Mock<IParameterStorage>();
            var fileDialog = new Mock<IFileDialogService>();
            var localization = new Mock<ILocalizationService>();
            var logger = new Mock<ILoggerService>();
            var eventAgg = new Mock<IEventAggregator>();

            return new ProductCalibrationViewModel(
                calibService.Object,
                tcpEvent.Object,
                tcpClient.Object,
                paramStorage.Object,
                fileDialog.Object,
                localization.Object,
                logger.Object,
                eventAgg.Object);
        }

        [Fact]
        public void ViewModel_HasAllCommands()
        {
            var vm = CreateViewModel();

            Assert.NotNull(vm.MoveToReferenceCommand);
            Assert.NotNull(vm.TeachReferenceCommand);
            Assert.NotNull(vm.MoveToPhoto1Command);
            Assert.NotNull(vm.TeachPhoto1Command);
            Assert.NotNull(vm.Capture1Command);
            Assert.NotNull(vm.MoveToPhoto2Command);
            Assert.NotNull(vm.TeachPhoto2Command);
            Assert.NotNull(vm.Capture2Command);
            Assert.NotNull(vm.RotateCommand);
            Assert.NotNull(vm.SaveConfigCommand);
            Assert.NotNull(vm.LoadConfigCommand);
            Assert.NotNull(vm.ImportConfigCommand);
            Assert.NotNull(vm.ExportConfigCommand);
        }

        [Fact]
        public void ViewModel_DefaultValues_AreSet()
        {
            var vm = CreateViewModel();

            Assert.Equal(0, vm.RefRx);
            Assert.Equal(0, vm.RefRz);
            Assert.Equal(0, vm.Photo1Dx);
            Assert.Equal(0, vm.Photo1Dy);
            Assert.Equal(0, vm.Photo1Dz);
            Assert.Equal(0, vm.Photo2Dx);
            Assert.Equal(0, vm.Photo2Dy);
            Assert.Equal(0, vm.Photo2Dz);
            Assert.Equal(5000, vm.CaptureTimeoutMs);
        }

        [Fact]
        public void ViewModel_RotateCommand_CannotExecuteBeforeCapture()
        {
            var vm = CreateViewModel();

            Assert.False(vm.RotateCommand.CanExecute());
        }

        [Fact]
        public void ViewModel_GlobalVariableLinkProperties_Work()
        {
            var vm = CreateViewModel();

            Assert.False(vm.IsDeltaXLinked);
            Assert.False(vm.IsDeltaYLinked);
            Assert.False(vm.IsDeltaAngleLinked);

            vm.DeltaXLinkedVar = "TestVar";
            Assert.True(vm.IsDeltaXLinked);

            vm.DeltaXLinkedVar = null;
            Assert.False(vm.IsDeltaXLinked);
        }
    }
}
