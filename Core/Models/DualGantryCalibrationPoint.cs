using Prism.Mvvm;
using System.Windows.Media;

namespace Core.Models
{
    /// <summary>
    /// 双龙门标定点模型——存储单个标定点的机械坐标与视觉坐标
    /// 继承BindableBase支持WPF双向绑定
    /// </summary>
    public class DualGantryCalibrationPoint : BindableBase
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

        private bool _isTaught;
        /// <summary>是否已示教</summary>
        public bool IsTaught
        {
            get => _isTaught;
            set
            {
                SetProperty(ref _isTaught, value);
                RaisePropertyChanged(nameof(StatusColor));
            }
        }

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

        /// <summary>
        /// 状态颜色：已标定=Green(#43A047), 已示教=Orange(#FB8C00), 未示教=Gray
        /// 优先级：已标定 > 已示教 > 未示教
        /// </summary>
        public Brush StatusColor
        {
            get
            {
                if (IsCalibrated) return _calibratedBrush;
                if (IsTaught) return _taughtBrush;
                return Brushes.Gray;
            }
        }

        // 静态画刷避免重复创建（线程安全由CLR静态初始化保证）
        private static readonly Brush _calibratedBrush = new SolidColorBrush(Color.FromRgb(0x43, 0xA0, 0x47));
        private static readonly Brush _taughtBrush = new SolidColorBrush(Color.FromRgb(0xFB, 0x8C, 0x00));
    }
}
