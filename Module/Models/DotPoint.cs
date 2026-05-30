using System;
using Prism.Mvvm;

namespace Module.Models
{
    /// <summary>
    /// 点位数据模型，表示运动控制中的一个目标点位
    /// </summary>
    public class DotPoint : BindableBase
    {
        private string _group = "ASSY_001";
        private string _pointId = "DOT_001";
        private double _dx;
        private double _dy;
        private double _dz2;
        private double _dz3;
        private double _rx;
        private double _ry;
        private double _dz2Compensation;
        private double _dz3Compensation;
        private bool _isEnabled = true;
        private bool _isSelected = true;

        /// <summary>
        /// 组别标识
        /// </summary>
        public string Group
        {
            get => _group;
            set => SetProperty(ref _group, value);
        }

        /// <summary>
        /// 点位标识
        /// </summary>
        public string PointId
        {
            get => _pointId;
            set => SetProperty(ref _pointId, value);
        }

        /// <summary>
        /// X方向偏移量
        /// </summary>
        public double Dx
        {
            get => _dx;
            set => SetProperty(ref _dx, value);
        }

        /// <summary>
        /// Y方向偏移量
        /// </summary>
        public double Dy
        {
            get => _dy;
            set => SetProperty(ref _dy, value);
        }

        /// <summary>
        /// Z2轴高度值，范围 -200~200
        /// </summary>
        public double Dz2
        {
            get => _dz2;
            set => SetProperty(ref _dz2, Math.Clamp(value, -200, 200), onChanged: () => RaisePropertyChanged(nameof(EffectiveDz2)));
        }

        /// <summary>
        /// Z3轴高度值，范围 -200~200
        /// </summary>
        public double Dz3
        {
            get => _dz3;
            set => SetProperty(ref _dz3, Math.Clamp(value, -200, 200), onChanged: () => RaisePropertyChanged(nameof(EffectiveDz3)));
        }

        /// <summary>
        /// X轴旋转角度，范围 -360~360
        /// </summary>
        public double Rx
        {
            get => _rx;
            set => SetProperty(ref _rx, Math.Clamp(value, -360, 360));
        }

        /// <summary>
        /// Y轴旋转角度，范围 -360~360
        /// </summary>
        public double Ry
        {
            get => _ry;
            set => SetProperty(ref _ry, Math.Clamp(value, -360, 360));
        }

        /// <summary>
        /// Z2轴补偿值，范围 -50~50
        /// </summary>
        public double Dz2Compensation
        {
            get => _dz2Compensation;
            set => SetProperty(ref _dz2Compensation, Math.Clamp(value, -50, 50), onChanged: () => RaisePropertyChanged(nameof(EffectiveDz2)));
        }

        /// <summary>
        /// Z3轴补偿值，范围 -50~50
        /// </summary>
        public double Dz3Compensation
        {
            get => _dz3Compensation;
            set => SetProperty(ref _dz3Compensation, Math.Clamp(value, -50, 50), onChanged: () => RaisePropertyChanged(nameof(EffectiveDz3)));
        }

        /// <summary>
        /// 是否启用该点位
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        /// <summary>
        /// 是否选中（用于批量操作和执行过滤）
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>
        /// Z2轴有效高度（Dz2 + Dz2Compensation）
        /// </summary>
        public double EffectiveDz2 => _dz2 + _dz2Compensation;

        /// <summary>
        /// Z3轴有效高度（Dz3 + Dz3Compensation）
        /// </summary>
        public double EffectiveDz3 => _dz3 + _dz3Compensation;

        public DotPoint() { }
    }
}
