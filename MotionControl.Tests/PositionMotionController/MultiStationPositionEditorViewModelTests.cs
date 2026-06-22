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
using System.Threading.Tasks;
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
            _motionServiceMock
                .Setup(m => m.MoveAbsAsync(It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>(), default))
                .Returns(Task.CompletedTask);
            // 多轴同时启动走 MoveAbsMultiAxisAsync（避免并行 MoveAbsAsync 引发运动卡 DLL 交叉干扰）
            _motionServiceMock
                .Setup(m => m.MoveAbsMultiAxisAsync(It.IsAny<IReadOnlyList<(int axisId, double position, double velocity)>>(), default))
                .Returns(Task.CompletedTask);
            // 回零检查默认返回已回零（1=已回零）
            _motionServiceMock.Setup(m => m.CheckHomeDoneAsync(It.IsAny<int>())).ReturnsAsync(1);
            // 轴状态默认已使能
            _motionServiceMock.Setup(m => m.GetAxisState(It.IsAny<int>()))
                .Returns(new AxisState { IsEnabled = true, IsHomeOk = true });

            _dialogServiceMock
                .Setup(d => d.ShowDialog("ConfirmationDialog", It.IsAny<IDialogParameters>(), It.IsAny<System.Action<IDialogResult>>()))
                .Callback<string, IDialogParameters, System.Action<IDialogResult>>((_, __, cb) =>
                    cb(new DialogResult(ButtonResult.Yes)));
        }

        private void SetupEventAggregator()
        {
            var recipeChangedEvent = new Recipe.Events.RecipeChangedEvent();
            var poolChangedEvent = new Recipe.Events.RecipePoolChangedEvent();
            var stationRegisteredEvent = new Core.Events.StationRegisteredEvent();
            var savePositionEditorEvent = new Recipe.Events.SavePositionEditorEvent();

            _eaMock.Setup(e => e.GetEvent<Recipe.Events.RecipeChangedEvent>())
                .Returns(recipeChangedEvent);
            _eaMock.Setup(e => e.GetEvent<Recipe.Events.RecipePoolChangedEvent>())
                .Returns(poolChangedEvent);
            _eaMock.Setup(e => e.GetEvent<Core.Events.StationRegisteredEvent>())
                .Returns(stationRegisteredEvent);
            _eaMock.Setup(e => e.GetEvent<Recipe.Events.SavePositionEditorEvent>())
                .Returns(savePositionEditorEvent);
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
        public void SetSelectedAxisColumn_工站轴列名有效()
        {
            var vm = CreateViewModel();
            // SetSelectedAxisColumn 接受 DataTable 列索引
            // 索引2对应X轴（0=PositionName, 1=IsReadOnly隐藏, 2=X）
            vm.SetSelectedAxisColumn(2);
            Assert.Equal("X", vm.SelectedAxisColumnName);

            // 索引0对应PositionName，非轴列——实现 intentionally 保留上一次有效选择
            vm.SetSelectedAxisColumn(0);
            Assert.Equal("X", vm.SelectedAxisColumnName);
        }

        /// <summary>
        /// 验证"多轴同时启动"走 MoveAbsMultiAxisAsync（而非并行 MoveAbsAsync）。
        /// 并行 MoveAbsAsync 会触发运动卡 DLL 交叉干扰，导致部分轴"不动位"。
        /// 通过反射直接调用私有 GotoSimultaneousAsync，绕过 UI 弹窗依赖。
        /// </summary>
        [Fact]
        public async Task GotoSimultaneousAsync_调用MoveAbsMultiAxisAsync而非并行MoveAbsAsync()
        {
            var vm = CreateViewModel();
            // 目标位置：X=10.0, Y=20.0
            var targetPositions = new Dictionary<string, double> { { "X", 10.0 }, { "Y", 20.0 } };

            // 反射调用私有方法 GotoSimultaneousAsync
            var method = typeof(MultiStationPositionEditorViewModel).GetMethod(
                "GotoSimultaneousAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
            var task = (Task)method.Invoke(vm, new object[] { targetPositions, 10.0 });
            await task;

            // 应调用一次 MoveAbsMultiAxisAsync（包含2个轴），且不调用 MoveAbsAsync
            _motionServiceMock.Verify(
                m => m.MoveAbsMultiAxisAsync(
                    It.Is<IReadOnlyList<(int axisId, double position, double velocity)>>(list => list.Count == 2),
                    default),
                Times.Once);
            _motionServiceMock.Verify(
                m => m.MoveAbsAsync(It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>(), default),
                Times.Never);
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
