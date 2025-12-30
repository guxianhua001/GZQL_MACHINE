
using System;
using System.ComponentModel;

namespace Stations.Models
{
    public class ExtensionParameter : INotifyPropertyChanged
    {
        private int _index;
        private double _referenceHeight;
        private double _upperLimit;
        private double _lowerLimit;
        private double _realTimeHeight;
        private double _compensation;
        private bool _isOutOfLimit;

        public int Index
        {
            get => _index;
            set { _index = value; OnPropertyChanged(); }
        }

        public double ReferenceHeight
        {
            get => _referenceHeight;
            set { _referenceHeight = value; OnPropertyChanged(); }
        }

        public double UpperLimit
        {
            get => _upperLimit;
            set { _upperLimit = value; OnPropertyChanged(); CheckLimits(); }
        }

        public double LowerLimit
        {
            get => _lowerLimit;
            set { _lowerLimit = value; OnPropertyChanged(); CheckLimits(); }
        }

        public double RealTimeHeight
        {
            get => _realTimeHeight;
            set { _realTimeHeight = value; OnPropertyChanged(); CheckLimits(); OnPropertyChanged(nameof(RealTimeHeightDisplay)); }
        }
        public double H2Height
        {
            get
            {
                // H2高度 = 实时高度 - 0.4
                if (double.IsNaN(RealTimeHeight))
                    return double.NaN;

                return Math.Round(RealTimeHeight - 0.4, 3);
            }
        }
        public double Compensation
        {
            get => _compensation;
            set { _compensation = value; OnPropertyChanged(); }
        }

        public bool IsOutOfLimit
        {
            get => _isOutOfLimit;
            set { _isOutOfLimit = value; OnPropertyChanged(); OnPropertyChanged(nameof(RealTimeHeightDisplay)); }
        }

        private void CheckLimits()
        {
            IsOutOfLimit = RealTimeHeight > UpperLimit || RealTimeHeight < LowerLimit;
        }
        // 显示用属性，用于格式化显示
        public string RealTimeHeightDisplay
        {
            get
            {
                if (double.IsNaN(RealTimeHeight) || Math.Abs(RealTimeHeight) < 0.000001)
                    return "-";
                return RealTimeHeight.ToString("F3");
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}