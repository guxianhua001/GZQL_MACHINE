using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Core.Models
{
    public enum PathType
    {
        Bezier,     // 贝塞尔曲线
        Line,       // 直线
        Spline,     // 样条曲线
        Circle      // 圆形
    }

    public class PathTypeItem : INotifyPropertyChanged
    {
        private string _name;
        private PathType _type;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public PathType Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class PathPoint : INotifyPropertyChanged
    {
        private int _index;
        private double _x;
        private double _y;
        private double _segmentLength;
        private double _accumulatedLength;

        public int Index
        {
            get => _index;
            set { _index = value; OnPropertyChanged(); }
        }

        public double X
        {
            get => _x;
            set { _x = value; OnPropertyChanged(); }
        }

        public double Y
        {
            get => _y;
            set { _y = value; OnPropertyChanged(); }
        }

        public double SegmentLength
        {
            get => _segmentLength;
            set { _segmentLength = value; OnPropertyChanged(); }
        }

        public double AccumulatedLength
        {
            get => _accumulatedLength;
            set { _accumulatedLength = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class AxisPathPoint : PathPoint
    {
        private double _axisOffsetX;
        private double _axisOffsetY;

        public double AxisOffsetX
        {
            get => _axisOffsetX;
            set { _axisOffsetX = value; OnPropertyChanged(); }
        }

        public double AxisOffsetY
        {
            get => _axisOffsetY;
            set { _axisOffsetY = value; OnPropertyChanged(); }
        }
    }

    public class NeedlePathPoint : AxisPathPoint
    {
        private double _needleX;
        private double _needleY;
        private double _speed;
        private double _dispensingTime;

        public double NeedleX
        {
            get => _needleX;
            set { _needleX = value; OnPropertyChanged(); }
        }

        public double NeedleY
        {
            get => _needleY;
            set { _needleY = value; OnPropertyChanged(); }
        }

        public double Speed
        {
            get => _speed;
            set { _speed = value; OnPropertyChanged(); }
        }

        public double DispensingTime
        {
            get => _dispensingTime;
            set { _dispensingTime = value; OnPropertyChanged(); }
        }
    }

    /// <summary>
    /// 点胶路径参数
    /// </summary>
    [Serializable]
    public class DispensingPathParams
    {
        /// <summary>
        /// 段数
        /// </summary>
        public int PathSegmentCount { get; set; } = 20;

        /// <summary>
        /// 移动速度 (mm/s)
        /// </summary>
        public double PathMoveSpeed { get; set; } = 1.0;

        /// <summary>
        /// 点胶时间 (ms)
        /// </summary>
        public double PathDispensingTime { get; set; } = 100;

        /// <summary>
        /// 弧线方向 (1=向外，-1=向内)
        /// </summary>
        public double ArcDirection { get; set; } = -1.0;

        /// <summary>
        /// 轨迹类型
        /// </summary>
        public int SelectedPathTypeIndex { get; set; } = 0;

        /// <summary>
        /// 自动调整弧线方向
        /// </summary>
        public bool AutoAdjustArcDirection { get; set; } = true;

        /// <summary>
        /// 相机与针头X偏移
        /// </summary>
        public double CameraNeedleOffsetX { get; set; } = 5.0;

        /// <summary>
        /// 相机与针头Y偏移
        /// </summary>
        public double CameraNeedleOffsetY { get; set; } = 5.0;

        /// <summary>
        /// 校针补偿X
        /// </summary>
        public double NeedleCompensationX { get; set; } = 0.1;

        /// <summary>
        /// 校针补偿Y
        /// </summary>
        public double NeedleCompensationY { get; set; } = 0.1;

        /// <summary>
        /// 当前选择的序号
        /// </summary>
        public int SelectedIndex { get; set; } = 1;

        // 点胶高度（按位置1-6）
        public double SafeHeight1 { get; set; } = 0;
        public double SafeHeight2 { get; set; } = 0;
        public double SafeHeight3 { get; set; } = 0;
        public double SafeHeight4 { get; set; } = 0;
        public double SafeHeight5 { get; set; } = 0;
        public double SafeHeight6 { get; set; } = 0;

        // 补偿值（按位置1-6）
        public double Compensation1 { get; set; } = 0;
        public double Compensation2 { get; set; } = 0;
        public double Compensation3 { get; set; } = 0;
        public double Compensation4 { get; set; } = 0;
        public double Compensation5 { get; set; } = 0;
        public double Compensation6 { get; set; } = 0;

        public double ManualNeedleCompensationX { get; set; } = 0.0;

        public double ManualNeedleCompensationY { get; set; } = 0.0;

        public double DescentDelay { get; set; } // 下降到点胶高度后的延时（毫秒）
        public double EndDelay { get; set; }     // 到达终点后的延时（毫秒）
        /// <summary>
        /// 最后保存时间
        /// </summary>
        public DateTime LastSaved { get; set; } = DateTime.Now;
    }
}
