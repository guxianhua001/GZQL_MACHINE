using Prism.Mvvm;
using System.Windows.Media;

namespace Core.Models
{
    /// <summary>
    /// N点标定点模型——存储单个标定点的机械坐标与视觉坐标
    /// 继承BindableBase支持WPF双向绑定
    /// </summary>
    public class NPointCalibrationPoint : BindableBase
    {
        private int _index;
        /// <summary>序号</summary>
        public int Index { get => _index; set => SetProperty(ref _index, value); }

        private string _name = "";
        /// <summary>点位名称（如 P1, P2...）</summary>
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        private double _machineX;
        /// <summary>机械坐标X（示教填入）</summary>
        public double MachineX { get => _machineX; set => SetProperty(ref _machineX, value); }

        private double _machineY;
        /// <summary>机械坐标Y（示教填入）</summary>
        public double MachineY { get => _machineY; set => SetProperty(ref _machineY, value); }

        private double _visionX;
        /// <summary>视觉坐标X（TCP接收或手动输入）</summary>
        public double VisionX { get => _visionX; set => SetProperty(ref _visionX, value); }

        private double _visionY;
        /// <summary>视觉坐标Y（TCP接收或手动输入）</summary>
        public double VisionY { get => _visionY; set => SetProperty(ref _visionY, value); }

        private bool _isCalibrated;
        /// <summary>是否已完成标定</summary>
        public bool IsCalibrated
        {
            get => _isCalibrated;
            set
            {
                SetProperty(ref _isCalibrated, value);
                RaisePropertyChanged(nameof(StatusColor));
            }
        }

        /// <summary>状态颜色：已标定=Green, 未标定=Gray</summary>
        public Brush StatusColor => IsCalibrated ? Brushes.Green : Brushes.Gray;
    }
}
