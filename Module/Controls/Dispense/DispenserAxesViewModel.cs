using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Module.ViewModels
{
    public class DispenserAxesViewModel : BindableBase
    {
        private readonly IDialogService _dialogService;

        private ObservableCollection<DispenserAxis> _axes;

        public ObservableCollection<DispenserAxis> Axes
        {
            get => _axes;
            set => SetProperty(ref _axes, value);
        }

        public ICommand HomeCommand { get; }
        public ICommand EmergencyStopCommand { get; }

        public DispenserAxesViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;

            // 初始化轴数据
            Axes = new ObservableCollection<DispenserAxis>
            {
                new DispenserAxis { AxisName = "X", Position = 150.000, Speed = 5.0, StepSize = 0.10, Unit = "mm", SpeedUnit = "mm/s" },
                new DispenserAxis { AxisName = "Y", Position = 80.000, Speed = 5.0, StepSize = 0.10, Unit = "mm", SpeedUnit = "mm/s" },
                new DispenserAxis { AxisName = "Z₁", Position = 0.000, Speed = 2.0, StepSize = 0.01, Unit = "mm", SpeedUnit = "mm/s" },
                new DispenserAxis { AxisName = "Z₂", Position = 0.000, Speed = 2.0, StepSize = 0.01, Unit = "mm", SpeedUnit = "mm/s" }
            };

            HomeCommand = new DelegateCommand(OnHome);
            EmergencyStopCommand = new DelegateCommand(OnEmergencyStop);
        }

        private void OnHome()
        {
            // 模拟回零
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Homing dispenser axes..." } }, null);
            // 实际应调用设备服务
            // 可在此处重置位置
            foreach (var axis in Axes)
            {
                axis.Position = 0;
            }
        }

        private void OnEmergencyStop()
        {
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Emergency stop triggered!" } }, null);
        }
    }

    public class DispenserAxis : BindableBase
    {
        private string _axisName;
        private double _position;
        private double _speed;
        private double _stepSize;
        private string _unit;
        private string _speedUnit;

        public string AxisName
        {
            get => _axisName;
            set => SetProperty(ref _axisName, value);
        }

        public double Position
        {
            get => _position;
            set => SetProperty(ref _position, value);
        }

        public double Speed
        {
            get => _speed;
            set => SetProperty(ref _speed, value);
        }

        public double StepSize
        {
            get => _stepSize;
            set => SetProperty(ref _stepSize, value);
        }

        public string Unit
        {
            get => _unit;
            set => SetProperty(ref _unit, value);
        }

        public string SpeedUnit
        {
            get => _speedUnit;
            set => SetProperty(ref _speedUnit, value);
        }

        public ICommand MoveNegativeCommand { get; }
        public ICommand MovePositiveCommand { get; }

        public DispenserAxis()
        {
            MoveNegativeCommand = new DelegateCommand(() => Position -= StepSize);
            MovePositiveCommand = new DelegateCommand(() => Position += StepSize);
        }
    }
}
