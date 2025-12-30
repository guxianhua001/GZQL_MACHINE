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
}
