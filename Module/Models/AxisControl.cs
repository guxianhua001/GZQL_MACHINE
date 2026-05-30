using Prism.Commands;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Module.Models
{
    /// <summary>
    /// 轴控制模型，用于 Jog 操作。
    /// </summary>
    public class AxisControl : BindableBase
    {
        private string _axisName;
        private double _position;
        private double _speed;
        private string _stepSize;

        // 步距选项（单位：mm 或 °，可根据实际调整）
        public ObservableCollection<string> StepSizeOptions { get; } = new ObservableCollection<string>
        {
            "0.01", "0.05", "0.10", "0.50", "1.00"
        };

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

        public string StepSize
        {
            get => _stepSize;
            set => SetProperty(ref _stepSize, value);
        }

        public ICommand MoveNegativeCommand { get; }
        public ICommand MovePositiveCommand { get; }

        public AxisControl(string name, double initialPos, double initialSpeed)
        {
            AxisName = name;
            Position = initialPos;
            Speed = initialSpeed;
            StepSize = "0.10"; // 默认步距

            MoveNegativeCommand = new DelegateCommand(OnMoveNegative);
            MovePositiveCommand = new DelegateCommand(OnMovePositive);
        }

        private void OnMoveNegative()
        {
            double step = ParseStepSize(StepSize);
            Position -= step;
        }

        private void OnMovePositive()
        {
            double step = ParseStepSize(StepSize);
            Position += step;
        }

        private double ParseStepSize(string stepString)
        {
            if (double.TryParse(stepString, out double value))
                return value;
            return 0.1;
        }
    }
}
