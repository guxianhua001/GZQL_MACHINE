using Core.Abstraction;
using Core.Models;
using Core.Services;
using Core.Utilities;
using Moq;
using MotionControl.Interfaces;
using MotionControl.Models;
using Prism.Events;
using Prism.Services.Dialogs;
using Recipe.Interfaces;
using Recipe.ViewModels;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Xunit;

namespace MotionControl.Tests.PositionMotionController
{
    public class MultiStationPositionEditorViewModelTests
    {
        private readonly Mock<IRecipePoolService> _recipePoolMock;
        private readonly Mock<IAxisConfigurationService> _axisConfigMock;
        private readonly Mock<IStationRegistry> _stationRegistryMock;
        private readonly Mock<ILoggerService> _loggerMock;
        private readonly Mock<IDialogService> _dialogServiceMock;
        private readonly Mock<IRecipeDialogService> _recipeDialogMock;
        private readonly Mock<IEventAggregator> _eaMock;
        private readonly Mock<IMotionService> _motionServiceMock;
        private readonly Mock<IStationParameterProvider> _stationMock;
        private readonly Mock<Core.Abstraction.ILocalizationService> _localizationMock;

        public MultiStationPositionEditorViewModelTests()
        {
            _recipePoolMock = new Mock<IRecipePoolService>();
            _axisConfigMock = new Mock<IAxisConfigurationService>();
            _stationRegistryMock = new Mock<IStationRegistry>();
            _loggerMock = new Mock<ILoggerService>();
            _dialogServiceMock = new Mock<IDialogService>();
            _recipeDialogMock = new Mock<IRecipeDialogService>();
            _eaMock = new Mock<IEventAggregator>();
            _motionServiceMock = new Mock<IMotionService>();

            _stationMock = new Mock<IStationParameterProvider>();
            _localizationMock = new Mock<Core.Abstraction.ILocalizationService>();
            _localizationMock.Setup(l => l.GetResource(It.IsAny<string>(), It.IsAny<object[]>()))
                .Returns((string key, object[] args) => string.Format(key, args));
            _localizationMock.Setup(l => l.GetResourceOrDefault(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string key, string fallback) => fallback ?? key);

            _stationMock.Setup(s => s.StationIdentifier).Returns("TestStation");
            _stationMock.Setup(s => s.CurrentPoolName).Returns("TestPool");
            _stationMock.Setup(s => s.CurrentRecipeName).Returns("Default");

            SetupEventAggregator();
            _stationRegistryMock.Setup(r => r.GetAllStations())
                .Returns(new List<IStationParameterProvider> { _stationMock.Object });
            _axisConfigMock.Setup(a => a.GetAxesForStation(It.IsAny<string>()))
                .Returns(new List<AxisDefinition>
                {
                    new() { Name = "X" },
                    new() { Name = "Y" }
                });
            _motionServiceMock.Setup(m => m.GetTaskConfigurations())
                .Returns(new List<TaskConfig> { new() { TaskId = 1, Type = "TestStation" } });
            _motionServiceMock.Setup(m => m.GetAxisConfigurations())
                .Returns(new List<AxisConfig>
                {
                    new() { Name = "X", LogicalId = 1, TaskId = 1 },
                    new() { Name = "Y", LogicalId = 2, TaskId = 1 }
                });

            _dialogServiceMock
                .Setup(d => d.ShowDialog("ConfirmationDialog", It.IsAny<IDialogParameters>(), It.IsAny<System.Action<IDialogResult>>()))
                .Callback<string, IDialogParameters, System.Action<IDialogResult>>((_, __, cb) =>
                    cb(new DialogResult(ButtonResult.Yes)));

            _recipeDialogMock
                .Setup(r => r.ShowConfirmationDialogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>()))
                .ReturnsAsync("多轴同时启动");
        }

        private void SetupEventAggregator()
        {
            var recipeChangedEvent = new Recipe.Events.RecipeChangedEvent();
            var poolChangedEvent = new Recipe.Events.RecipePoolChangedEvent();
            var stationRegisteredEvent = new Core.Events.StationRegisteredEvent();

            _eaMock.Setup(e => e.GetEvent<Recipe.Events.RecipeChangedEvent>())
                .Returns(recipeChangedEvent);
            _eaMock.Setup(e => e.GetEvent<Recipe.Events.RecipePoolChangedEvent>())
                .Returns(poolChangedEvent);
            _eaMock.Setup(e => e.GetEvent<Core.Events.StationRegisteredEvent>())
                .Returns(stationRegisteredEvent);
        }

        private MultiStationPositionEditorViewModel CreateViewModel()
        {
            return new MultiStationPositionEditorViewModel(
                _recipePoolMock.Object,
                _axisConfigMock.Object,
                _stationRegistryMock.Object,
                _loggerMock.Object,
                _dialogServiceMock.Object,
                _recipeDialogMock.Object,
                _eaMock.Object,
                _motionServiceMock.Object,
                _localizationMock.Object);
        }

        #region T11-T12: TeachCommand Tests

        [Fact]
        public void TeachCommand_有选中行且可用时读取轴位置()
        {
            var vm = CreateViewModel();
            SetSelectedRow(vm, "P1", 10.0, 20.0);
            _motionServiceMock.Setup(m => m.GetAxisPosition(1)).Returns(100.0);
            _motionServiceMock.Setup(m => m.GetAxisPosition(2)).Returns(200.0);

            vm.TeachCommand.Execute(null);

            _motionServiceMock.Verify(m => m.GetAxisPosition(1), Times.Once());
            _motionServiceMock.Verify(m => m.GetAxisPosition(2), Times.Once());
        }

        [Fact]
        public void TeachCommand_无选中行时不调用()
        {
            var vm = CreateViewModel();

            vm.TeachCommand.Execute(null);

            _motionServiceMock.Verify(m => m.GetAxisPosition(It.IsAny<int>()), Times.Never());
        }

        #endregion

        #region T13: Teach完成后更新DataTable

        [Fact]
        public void TeachCommand_完成后更新DataTable当行数据()
        {
            var vm = CreateViewModel();
            SetSelectedRow(vm, "P1", 0.0, 0.0);
            _motionServiceMock.Setup(m => m.GetAxisPosition(1)).Returns(100.5);
            _motionServiceMock.Setup(m => m.GetAxisPosition(2)).Returns(200.3);

            vm.TeachCommand.Execute(null);

            Assert.Equal(100.5, vm.SelectedRow["X"]);
            Assert.Equal(200.3, vm.SelectedRow["Y"]);
        }

        #endregion

        #region T14: ReplayCommand Tests

        [Fact]
        public async Task ReplayCommand_多轴同时启动模式并行调用MoveAbsAsync()
        {
            var vm = CreateViewModel();
            vm.SelectedSpeed = 15.0;
            SetSelectedRow(vm, "P1", 50.0, 60.0);

            vm.ReplayCommand.Execute(null);

            await Task.Delay(100);

            _motionServiceMock.Verify(m => m.MoveAbsAsync(1, 50.0, 15.0, default), Times.Once());
            _motionServiceMock.Verify(m => m.MoveAbsAsync(2, 60.0, 15.0, default), Times.Once());
            _motionServiceMock.Verify(m => m.MoveLineAbsAsync(It.IsAny<int>(), It.IsAny<int[]>(), It.IsAny<double[]>(), It.IsAny<double>(), default), Times.Never());
        }

        [Fact]
        public async Task ReplayCommand_单轴顺序模式调用MoveAbsAsync()
        {
            _recipeDialogMock
                .Setup(r => r.ShowConfirmationDialogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>()))
                .ReturnsAsync("单轴顺序移动");

            var vm = CreateViewModel();
            vm.SelectedSpeed = 15.0;
            SetSelectedRow(vm, "P1", 50.0, 60.0);

            vm.ReplayCommand.Execute(null);

            await Task.Delay(100);

            _motionServiceMock.Verify(m => m.MoveAbsAsync(1, 50.0, 15.0, default), Times.Once());
            _motionServiceMock.Verify(m => m.MoveAbsAsync(2, 60.0, 15.0, default), Times.Once());
        }

        #endregion

        #region T15: StopCommand Tests

        [Fact]
        public void StopCommand_调用StopAxis()
        {
            var vm = CreateViewModel();
            SetSelectedRow(vm, "P1", 10.0, 20.0);

            vm.StopCommand.Execute(null);

            _motionServiceMock.Verify(m => m.StopAxis(1), Times.Once());
            _motionServiceMock.Verify(m => m.StopAxis(2), Times.Once());
        }

        [Fact]
        public void StopCommand_无选中行时仍可调用()
        {
            var vm = CreateViewModel();

            vm.StopCommand.Execute(null);

            _motionServiceMock.Verify(m => m.StopAxis(1), Times.Once());
            _motionServiceMock.Verify(m => m.StopAxis(2), Times.Once());
        }

        [Fact]
        public void TeachCommand_选中行后CanExecute为true()
        {
            var vm = CreateViewModel();
            Assert.False(vm.TeachCommand.CanExecute(null));

            SetSelectedRow(vm, "P1", 10.0, 20.0);

            Assert.True(vm.TeachCommand.CanExecute(null));
        }

        #endregion

        #region Helper Methods

        private void SetSelectedRow(MultiStationPositionEditorViewModel vm, string posName, double x, double y)
        {
            if (vm.PositionsTable.Rows.Count == 0)
            {
                var row = vm.PositionsTable.NewRow();
                row["PositionName"] = posName;
                row["IsReadOnly"] = false;
                row["X"] = x;
                row["Y"] = y;
                row["Comment"] = "";
                vm.PositionsTable.Rows.Add(row);
            }
            vm.SelectedRow = vm.PositionsTable.DefaultView[0];
        }

        #endregion
    }
}
