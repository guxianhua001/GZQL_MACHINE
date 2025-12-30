// NeedleCalibrationParams.cs
using Core.Abstraction;
using Core.Models;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace Stations.TaskParameters
{
    /// <summary>
    /// 针头校准参数 - 独立存储版本
    /// </summary>
    public class NeedleCalibrationParams : TaskParametersBase, INotifyPropertyChanged
    {
        public override string Identifier => "NeedleCalibration";

        private string _calibrationName = "Default";
        [Category("基本信息")]
        [DisplayName("校准名称")]
        [Description("当前校准配置的名称")]
        public string CalibrationName
        {
            get => _calibrationName;
            set => SetProperty(ref _calibrationName, value);
        }

        private PointF _searchPoint1 = new PointF(0, 0);
        private PointF _searchPoint2 = new PointF(0, 0);
        private PointF _searchPoint3 = new PointF(0, 0);
        private PointF _searchPoint4 = new PointF(0, 0);
        private PointF _referenceXYZ = new PointF(0, 0, 0);
        private PointF _compensationXYZ = new PointF(0, 0, 0);
        private PointF _currentXYZ = new PointF(0, 0, 0);
        private double _searchRange = 1.0;
        private int _zSearchCount = 3;
        private double _searchSpeed = 5.0;
        private double _fineSearchSpeed = 1.0;
        private double _needleBaseHeight = 0.1;

        #region 搜索点设置
        [Category("搜索点设置")]
        [DisplayName("搜索点1")]
        [Description("第一个搜索点的坐标")]
        public PointF SearchPoint1
        {
            get => _searchPoint1;
            set => SetProperty(ref _searchPoint1, value);
        }

        [Category("搜索点设置")]
        [DisplayName("搜索点2")]
        [Description("第二个搜索点的坐标")]
        public PointF SearchPoint2
        {
            get => _searchPoint2;
            set => SetProperty(ref _searchPoint2, value);
        }

        [Category("搜索点设置")]
        [DisplayName("搜索点3")]
        [Description("第三个搜索点的坐标")]
        public PointF SearchPoint3
        {
            get => _searchPoint3;
            set => SetProperty(ref _searchPoint3, value);
        }

        [Category("搜索点设置")]
        [DisplayName("搜索点4")]
        [Description("第四个搜索点的坐标")]
        public PointF SearchPoint4
        {
            get => _searchPoint4;
            set => SetProperty(ref _searchPoint4, value);
        }
        #endregion

        #region 坐标参数
        [Category("坐标参数")]
        [DisplayName("基准XYZ坐标")]
        [Description("基准点的XYZ坐标")]
        public PointF ReferenceXYZ
        {
            get => _referenceXYZ;
            set => SetProperty(ref _referenceXYZ, value);
        }

        [Category("坐标参数")]
        [DisplayName("补偿XYZ坐标")]
        [Description("补偿值的XYZ坐标")]
        public PointF CompensationXYZ
        {
            get => _compensationXYZ;
            set => SetProperty(ref _compensationXYZ, value);
        }

        [Category("坐标参数")]
        [DisplayName("当前XYZ坐标")]
        [Description("当前测量值的XYZ坐标")]
        public PointF CurrentXYZ
        {
            get => _currentXYZ;
            set => SetProperty(ref _currentXYZ, value);
        }
        #endregion

        #region 搜索参数
        [Category("搜索参数")]
        [DisplayName("搜索范围 (mm)")]
        [Description("搜索点的移动范围")]
        [Range(0.1, 10.0)]
        public double SearchRange
        {
            get => _searchRange;
            set => SetProperty(ref _searchRange, value);
        }

        [Category("搜索参数")]
        [DisplayName("Z方向搜索次数")]
        [Description("Z方向寻找次数")]
        [Range(1, 10)]
        public int ZSearchCount
        {
            get => _zSearchCount;
            set => SetProperty(ref _zSearchCount, value);
        }

        [Category("搜索参数")]
        [DisplayName("搜索速度 (mm/s)")]
        [Description("搜索移动速度")]
        [Range(0.1, 20.0)]
        public double SearchSpeed
        {
            get => _searchSpeed;
            set => SetProperty(ref _searchSpeed, value);
        }

        [Category("搜索参数")]
        [DisplayName("精细搜索速度 (mm/s)")]
        [Description("精细搜索移动速度")]
        [Range(0.1, 5.0)]
        public double FineSearchSpeed
        {
            get => _fineSearchSpeed;
            set => SetProperty(ref _fineSearchSpeed, value);
        }
        #endregion

        [Category("针头参数")]
        [DisplayName("针头基准高度 (mm)")]
        [Description("针头在零位时的基准高度")]
        public double NeedleBaseHeight
        {
            get => _needleBaseHeight;
            set => SetProperty(ref _needleBaseHeight, value);
        }

        // 深拷贝方法
        public NeedleCalibrationParams Clone()
        {
            return new NeedleCalibrationParams
            {
                ReferenceXYZ = this.ReferenceXYZ != null ? new PointF(this.ReferenceXYZ.X, this.ReferenceXYZ.Y, this.ReferenceXYZ.Z) : new PointF(),
                CurrentXYZ = this.CurrentXYZ != null ? new PointF(this.CurrentXYZ.X, this.CurrentXYZ.Y, this.CurrentXYZ.Z) : new PointF(),
                CompensationXYZ = this.CompensationXYZ != null ? new PointF(this.CompensationXYZ.X, this.CompensationXYZ.Y, this.CompensationXYZ.Z) : new PointF(),
                SearchPoint1 = this.SearchPoint1 != null ? new PointF(this.SearchPoint1.X, this.SearchPoint1.Y) : new PointF(),
                SearchPoint2 = this.SearchPoint2 != null ? new PointF(this.SearchPoint2.X, this.SearchPoint2.Y) : new PointF(),
                SearchPoint3 = this.SearchPoint3 != null ? new PointF(this.SearchPoint3.X, this.SearchPoint3.Y) : new PointF(),
                SearchPoint4 = this.SearchPoint4 != null ? new PointF(this.SearchPoint4.X, this.SearchPoint4.Y) : new PointF(),
                SearchRange = this.SearchRange,
                ZSearchCount = this.ZSearchCount,
                SearchSpeed = this.SearchSpeed,
                FineSearchSpeed = this.FineSearchSpeed,
                NeedleBaseHeight = this.NeedleBaseHeight
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}