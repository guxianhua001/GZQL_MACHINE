using Core.Abstraction;
using Core.Services;
using Core.Utilities;
using Moq;
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
        private readonly Mock<IEventAggregator> _eaMock;
        private readonly Mock<IPositionMotionController> _motionControllerMock;
        private readonly Mock<IStationParameterProvider> _stationMock;
        private readonly Mock<Core.Abstraction.ILocalizationService> _localizationMock;

        public MultiStationPositionEditorViewModelTests()
        {
            _recipePoolMock = new Mock<IRecipePoolService>();
            _axisConfigMock = new Mock<IAxisConfigurationService>();
            _stationRegistryMock = new Mock<IStationRegistry>();
            _loggerMock = new Mock<ILoggerService>();
            _dialogServiceMock = new Mock<IDialogService>();
            _eaMock = new Mock<IEventAggregator>();
            _motionControllerMock = new Mock<IPositionMotionController>();

            _stationMock = new Mock<IStationParameterProvider>();
            _localizationMock = new Mock<Core.Abstraction.ILocalizationService>();
            _localizationMock.Setup(l => l.GetResource(It.IsAny<string>(), It.IsAny<object[]>()))
                .Returns((string key, object[] args) => string.Format(key, args));
            _stationMock.Setup(s => s.StationIdentifier).Returns("TestStation");
            _stationMock.Setup(s => s.CurrentPoolName).Returns("TestPool");
            _stationMock.Setup(s => s.CurrentRecipeName).Returns("Default");

            SetupEventAggregator();
            _stationRegistryMock.Setup(r => r.GetAllStations())
                .Returns(new List<IStationParameterProvider> { _stationMock.Object });
            _axisConfigMock.Setup(a => a.GetAxesForStation(It.IsAny<string>()))
                .Returns(new List<Core.Models.AxisDefinition>
                {
                    new() { Name = "X" },
                    new() { Name = "Y" }
                });
            _motionControllerMock.Setup(m => m.CanExecuteMotion(It.IsAny<string>())).Returns(true);
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
                _eaMock.Object,
                _motionControllerMock.Object,
                _localizationMock.Object);
        }

        #region T11-T12: TeachCommand Tests

        [Fact]
        public void TeachCommand_有选中行且可用时调用TeachAsync()
        {
            var vm = CreateViewModel();
            SetSelectedRow(vm, "P1", 10.0, 20.0);
            _motionControllerMock.Setup(m => m.TeachAsync("TestStation"))
                .ReturnsAsync(new Dictionary<string, double> { ["X"] = 100.0, ["Y"] = 200.0 });

            vm.TeachCommand.Execute(null);

            _motionControllerMock.Verify(m => m.TeachAsync("TestStation"), Times.Once());
        }

        [Fact]
        public void TeachCommand_无选中行时不调用()
        {
            var vm = CreateViewModel();

            vm.TeachCommand.Execute(null);

            _motionControllerMock.Verify(m => m.TeachAsync(It.IsAny<string>()), Times.Never());
        }

        #endregion

        #region T13: Teach完成后更新DataTable

        [Fact]
        public async Task TeachCommand_完成后更新DataTable当行数据()
        {
            var vm = CreateViewModel();
            SetSelectedRow(vm, "P1", 0.0, 0.0);

            _motionControllerMock.Setup(m => m.TeachAsync("TestStation"))
                .ReturnsAsync(new Dictionary<string, double> { ["X"] = 100.5, ["Y"] = 200.3 });

            vm.TeachCommand.Execute(null);

            Assert.Equal(100.5, vm.SelectedRow["X"]);
            Assert.Equal(200.3, vm.SelectedRow["Y"]);
        }

        #endregion

        #region T14: ReplayCommand Tests

        [Fact]
        public async Task ReplayCommand_调用GotoAsync传入正确的位置和速度()
        {
            var vm = CreateViewModel();
            vm.SelectedSpeed = 15.0;
            SetSelectedRow(vm, "P1", 50.0, 60.0);

            vm.ReplayCommand.Execute(null);

            _motionControllerMock.Verify(m => m.GotoAsync(
                "TestStation",
                It.Is<Dictionary<string, double>>(d => d["X"] == 50.0 && d["Y"] == 60.0),
                15.0), Times.Once());
        }

        #endregion

        #region T15: StopCommand Tests

        [Fact]
        public void StopCommand_调用Stop方法()
        {
            var vm = CreateViewModel();
            SetSelectedRow(vm, "P1", 10.0, 20.0);

            vm.StopCommand.Execute(null);

            _motionControllerMock.Verify(m => m.Stop("TestStation"), Times.Once());
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
