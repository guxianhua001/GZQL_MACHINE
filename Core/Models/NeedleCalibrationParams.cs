// NeedleCalibrationParams.cs
using Core.Abstraction;
using Core.Models;
using Newtonsoft.Json;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace Core.Models
{
    // 事件类
    public class NeedleCalibrationCompletedEventArgs
    {
        public int SystemNumber { get; set; }
        public NeedleCalibrationParams Parameters { get; set; }
        public DateTime CompletionTime { get; set; }
    }

    public class NeedleParametersSavedEventArgs
    {
        public int SystemNumber { get; set; }
        public NeedleCalibrationParams Parameters { get; set; }
    }

    // 事件
    public class NeedleCalibrationCompletedEvent : PubSubEvent<NeedleCalibrationCompletedEventArgs> { }
    public class NeedleParametersSavedEvent : PubSubEvent<NeedleParametersSavedEventArgs> { }

    /// <summary>搜索点使用的寻针传感器类型（对应运动参数 SensorDiX / SensorDiY）</summary>
    public enum NeedleSearchSensorKind
    {
        /// <summary>X 方向传感器（SensorDiX）</summary>
        SensorX = 0,
        /// <summary>Y 方向传感器（SensorDiY）</summary>
        SensorY = 1
    }

    /// <summary>
    /// 针头校准参数 - 独立存储版本
    /// </summary>
    public class NeedleCalibrationParams : TaskParametersBase, INotifyPropertyChanged
    {
        public override string Identifier => "NeedleCalibration";

        public int SystemNumber { get; set; } = 1; // 系统编号
        public DateTime LastCalibrationTime { get; set; }
        public string Operator { get; set; } = "未知操作员";

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
        /// <summary>搜索点1 使用的传感器：X 或 Y</summary>
        private NeedleSearchSensorKind _searchPoint1SensorKind = NeedleSearchSensorKind.SensorX;
        private NeedleSearchSensorKind _searchPoint2SensorKind = NeedleSearchSensorKind.SensorX;
        private NeedleSearchSensorKind _searchPoint3SensorKind = NeedleSearchSensorKind.SensorY;
        private NeedleSearchSensorKind _searchPoint4SensorKind = NeedleSearchSensorKind.SensorY;
        private PointF _referenceXYZ = new PointF(0, 0, 0);
        private PointF _compensationXYZ = new PointF(0, 0, 0);
        private PointF _currentXYZ = new PointF(0, 0, 0);
        private double _searchRange = 1.0;
        private int _zSearchCount = 3;
        private double _searchSpeed = 5.0;
        private double _fineSearchSpeed = 1.0;
        private double _safeHeight = -20.0;
        /// <summary>系统1 Z 向寻探高度：针头进入十字激光检测平面的 Z 坐标</summary>
        private double _searchNeedleHeightSystem1;
        /// <summary>系统2 Z 向寻探高度</summary>
        private double _searchNeedleHeightSystem2;
        /// <summary>Z 下探高度：在寻针高度基础上再下探，换针后针尖偏高时可加大以可靠触探</summary>
        private double _zProbeDescentHeight;
        /// <summary>Z 最低高度：精细下探极限，寻针高度+下探高度超过此值时以此为准</summary>
        private double _zMinHeight;
        private PointF _alignPositionSystem1 = new PointF(0, 0, 0);
        private PointF _alignPositionSystem2 = new PointF(0, 0, 0);
        /// <summary>X方向寻针传感器 DI 端口号</summary>
        private int _sensorDiX = 30;
        /// <summary>Y方向寻针传感器 DI 端口号</summary>
        private int _sensorDiY = 29;
        public bool IsValid { get; set; } = true;

        #region 搜索点设置
        [Category("搜索点设置")]
        [DisplayName("搜索点1")]
        [Description("第一个搜索点的坐标")]
        public PointF SearchPoint1
        {
            get => _searchPoint1;
            set
            {
                if (SetProperty(ref _searchPoint1, value))
                {
                    OnPropertyChanged(nameof(SearchPoint1X));
                    OnPropertyChanged(nameof(SearchPoint1Y));
                }
            }
        }

        /// <summary>搜索点1 X 坐标（界面可编辑）</summary>
        [Browsable(false)]
        public float SearchPoint1X
        {
            get => _searchPoint1.X;
            set => SetSearchPointAxis(ref _searchPoint1, value, true, nameof(SearchPoint1), nameof(SearchPoint1X), nameof(SearchPoint1Y));
        }

        /// <summary>搜索点1 Y 坐标（界面可编辑）</summary>
        [Browsable(false)]
        public float SearchPoint1Y
        {
            get => _searchPoint1.Y;
            set => SetSearchPointAxis(ref _searchPoint1, value, false, nameof(SearchPoint1), nameof(SearchPoint1X), nameof(SearchPoint1Y));
        }

        /// <summary>搜索点1 传感器选择（SensorDiX / SensorDiY）</summary>
        [Browsable(false)]
        public NeedleSearchSensorKind SearchPoint1SensorKind
        {
            get => _searchPoint1SensorKind;
            set => SetProperty(ref _searchPoint1SensorKind, value);
        }

        [Category("搜索点设置")]
        [DisplayName("搜索点2")]
        [Description("第二个搜索点的坐标")]
        public PointF SearchPoint2
        {
            get => _searchPoint2;
            set
            {
                if (SetProperty(ref _searchPoint2, value))
                {
                    OnPropertyChanged(nameof(SearchPoint2X));
                    OnPropertyChanged(nameof(SearchPoint2Y));
                }
            }
        }

        /// <summary>搜索点2 X 坐标（界面可编辑）</summary>
        [Browsable(false)]
        public float SearchPoint2X
        {
            get => _searchPoint2.X;
            set => SetSearchPointAxis(ref _searchPoint2, value, true, nameof(SearchPoint2), nameof(SearchPoint2X), nameof(SearchPoint2Y));
        }

        /// <summary>搜索点2 Y 坐标（界面可编辑）</summary>
        [Browsable(false)]
        public float SearchPoint2Y
        {
            get => _searchPoint2.Y;
            set => SetSearchPointAxis(ref _searchPoint2, value, false, nameof(SearchPoint2), nameof(SearchPoint2X), nameof(SearchPoint2Y));
        }

        [Browsable(false)]
        public NeedleSearchSensorKind SearchPoint2SensorKind
        {
            get => _searchPoint2SensorKind;
            set => SetProperty(ref _searchPoint2SensorKind, value);
        }

        [Category("搜索点设置")]
        [DisplayName("搜索点3")]
        [Description("第三个搜索点的坐标")]
        public PointF SearchPoint3
        {
            get => _searchPoint3;
            set
            {
                if (SetProperty(ref _searchPoint3, value))
                {
                    OnPropertyChanged(nameof(SearchPoint3X));
                    OnPropertyChanged(nameof(SearchPoint3Y));
                }
            }
        }

        /// <summary>搜索点3 X 坐标（界面可编辑）</summary>
        [Browsable(false)]
        public float SearchPoint3X
        {
            get => _searchPoint3.X;
            set => SetSearchPointAxis(ref _searchPoint3, value, true, nameof(SearchPoint3), nameof(SearchPoint3X), nameof(SearchPoint3Y));
        }

        /// <summary>搜索点3 Y 坐标（界面可编辑）</summary>
        [Browsable(false)]
        public float SearchPoint3Y
        {
            get => _searchPoint3.Y;
            set => SetSearchPointAxis(ref _searchPoint3, value, false, nameof(SearchPoint3), nameof(SearchPoint3X), nameof(SearchPoint3Y));
        }

        [Browsable(false)]
        public NeedleSearchSensorKind SearchPoint3SensorKind
        {
            get => _searchPoint3SensorKind;
            set => SetProperty(ref _searchPoint3SensorKind, value);
        }

        [Category("搜索点设置")]
        [DisplayName("搜索点4")]
        [Description("第四个搜索点的坐标")]
        public PointF SearchPoint4
        {
            get => _searchPoint4;
            set
            {
                if (SetProperty(ref _searchPoint4, value))
                {
                    OnPropertyChanged(nameof(SearchPoint4X));
                    OnPropertyChanged(nameof(SearchPoint4Y));
                }
            }
        }

        /// <summary>搜索点4 X 坐标（界面可编辑）</summary>
        [Browsable(false)]
        public float SearchPoint4X
        {
            get => _searchPoint4.X;
            set => SetSearchPointAxis(ref _searchPoint4, value, true, nameof(SearchPoint4), nameof(SearchPoint4X), nameof(SearchPoint4Y));
        }

        /// <summary>搜索点4 Y 坐标（界面可编辑）</summary>
        [Browsable(false)]
        public float SearchPoint4Y
        {
            get => _searchPoint4.Y;
            set => SetSearchPointAxis(ref _searchPoint4, value, false, nameof(SearchPoint4), nameof(SearchPoint4X), nameof(SearchPoint4Y));
        }

        [Browsable(false)]
        public NeedleSearchSensorKind SearchPoint4SensorKind
        {
            get => _searchPoint4SensorKind;
            set => SetProperty(ref _searchPoint4SensorKind, value);
        }

        #endregion

        #region 坐标参数
        [Category("坐标参数")]
        [DisplayName("基准XYZ坐标")]
        [Description("固定示教基准（增量法下永不自动变更）")]
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

        [Category("运动参数")]
        [DisplayName("安全高度 (mm)")]
        [Description("水平移动前 Z 轴抬升高度，防止碰撞")]
        [Range(0.0, 200.0)]
        public double SafeHeight
        {
            get => _safeHeight;
            set => SetProperty(ref _safeHeight, Math.Clamp(value, -50.0, 50.0));
        }

        [Category("运动参数")]
        [DisplayName("系统1寻针高度 (mm)")]
        [Description("系统1 XY 边界扫描前 Z 下降至十字激光检测平面的高度")]
        [Range(-100.0, 100.0)]
        public double SearchNeedleHeightSystem1
        {
            get => _searchNeedleHeightSystem1;
            set
            {
                if (SetProperty(ref _searchNeedleHeightSystem1, Math.Clamp(value, -100.0, 100.0)))
                    OnPropertyChanged(nameof(SearchNeedleHeight));
            }
        }

        [Category("运动参数")]
        [DisplayName("系统2寻针高度 (mm)")]
        [Description("系统2 XY 边界扫描前 Z 下降至十字激光检测平面的高度")]
        [Range(-100.0, 100.0)]
        public double SearchNeedleHeightSystem2
        {
            get => _searchNeedleHeightSystem2;
            set
            {
                if (SetProperty(ref _searchNeedleHeightSystem2, Math.Clamp(value, -100.0, 100.0)))
                    OnPropertyChanged(nameof(SearchNeedleHeight));
            }
        }

        /// <summary>
        /// 当前系统寻针高度（界面绑定：按 SystemNumber 读写 System1/System2 存储字段）
        /// </summary>
        [JsonIgnore]
        [Browsable(false)]
        public double SearchNeedleHeight
        {
            get => SystemNumber == 1 ? SearchNeedleHeightSystem1 : SearchNeedleHeightSystem2;
            set
            {
                if (SystemNumber == 1)
                    SearchNeedleHeightSystem1 = value;
                else
                    SearchNeedleHeightSystem2 = value;
            }
        }

        [Category("运动参数")]
        [DisplayName("Z下探高度 (mm)")]
        [Description("在寻针高度基础上再下探，防止换针后针尖偏高导致检测不到")]
        [Range(-10.0, 10.0)]
        public double ZProbeDescentHeight
        {
            get => _zProbeDescentHeight;
            set => SetProperty(ref _zProbeDescentHeight, Math.Clamp(value, -10.0, 10.0));
        }

        [Category("运动参数")]
        [DisplayName("Z最低高度 (mm)")]
        [Description("Z 向精细下探极限；寻针高度+下探高度超过此值时以此高度为准")]
        [Range(-100.0, 100.0)]
        public double ZMinHeight
        {
            get => _zMinHeight;
            set => SetProperty(ref _zMinHeight, Math.Clamp(value, -100.0, 100.0));
        }

        [Category("对针位置")]
        [DisplayName("系统1对针位置 (Dx Dy Dz₂)")]
        [Description("系统1对针器 XYZ 坐标（手动移动到对针位）")]
        public PointF AlignPositionSystem1
        {
            get => _alignPositionSystem1;
            set => SetProperty(ref _alignPositionSystem1, value);
        }

        [Category("对针位置")]
        [DisplayName("系统2对针位置 (Dx Dy Dz₃)")]
        [Description("系统2对针器 XYZ 坐标（手动移动到对针位）")]
        public PointF AlignPositionSystem2
        {
            get => _alignPositionSystem2;
            set => SetProperty(ref _alignPositionSystem2, value);
        }

        [Category("传感器")]
        [DisplayName("X传感器DI")]
        [Description("X方向寻针传感器 DI 端口号，低电平触发")]
        public int SensorDiX
        {
            get => _sensorDiX;
            set => SetProperty(ref _sensorDiX, value);
        }

        [Category("传感器")]
        [DisplayName("Y传感器DI")]
        [Description("Y方向寻针传感器 DI 端口号，低电平触发")]
        public int SensorDiY
        {
            get => _sensorDiY;
            set => SetProperty(ref _sensorDiY, value);
        }

        /// <summary>当前已应用 TCP 补偿偏移 X（Apply 后等于 PendingIncrement）</summary>
        public double? TcpTotalOffsetX { get; set; }
        /// <summary>当前已应用 TCP 补偿偏移 Y</summary>
        public double? TcpTotalOffsetY { get; set; }
        /// <summary>当前已应用 TCP 补偿偏移 Z</summary>
        public double? TcpTotalOffsetZ { get; set; }

        // 兼容旧版 JSON 字段
        public double? CompensationStorageX { get; set; }
        public double? CompensationStorageY { get; set; }
        public double? CompensationStorageZ { get; set; }

        // 全局变量链接名称
        public string CompensationXLinkedVar { get; set; }
        public string CompensationYLinkedVar { get; set; }
        public string CompensationZLinkedVar { get; set; }

        /// <summary>X轴补偿表达式，如 "0.1+0.2"</summary>
        public string CompensationXExpression { get; set; }

        /// <summary>Y轴补偿表达式</summary>
        public string CompensationYExpression { get; set; }

        /// <summary>Z轴补偿表达式</summary>
        public string CompensationZExpression { get; set; }

        /// <summary>获取指定索引搜索点的传感器类型（0~3）</summary>
        public NeedleSearchSensorKind GetSearchPointSensorKind(int pointIndex) => pointIndex switch
        {
            0 => SearchPoint1SensorKind,
            1 => SearchPoint2SensorKind,
            2 => SearchPoint3SensorKind,
            3 => SearchPoint4SensorKind,
            _ => NeedleSearchSensorKind.SensorX
        };

        // 深拷贝方法
        public NeedleCalibrationParams Clone()
        {
            return new NeedleCalibrationParams
            {
                SystemNumber = this.SystemNumber,
                CalibrationName = this.CalibrationName,
                LastCalibrationTime = this.LastCalibrationTime,
                ReferenceXYZ = this.ReferenceXYZ != null ? new PointF(this.ReferenceXYZ.X, this.ReferenceXYZ.Y, this.ReferenceXYZ.Z) : new PointF(),
                CurrentXYZ = this.CurrentXYZ != null ? new PointF(this.CurrentXYZ.X, this.CurrentXYZ.Y, this.CurrentXYZ.Z) : new PointF(),
                CompensationXYZ = this.CompensationXYZ != null ? new PointF(this.CompensationXYZ.X, this.CompensationXYZ.Y, this.CompensationXYZ.Z) : new PointF(),
                SearchPoint1 = this.SearchPoint1 != null ? new PointF(this.SearchPoint1.X, this.SearchPoint1.Y) : new PointF(),
                SearchPoint2 = this.SearchPoint2 != null ? new PointF(this.SearchPoint2.X, this.SearchPoint2.Y) : new PointF(),
                SearchPoint3 = this.SearchPoint3 != null ? new PointF(this.SearchPoint3.X, this.SearchPoint3.Y) : new PointF(),
                SearchPoint4 = this.SearchPoint4 != null ? new PointF(this.SearchPoint4.X, this.SearchPoint4.Y) : new PointF(),
                SearchPoint1SensorKind = this.SearchPoint1SensorKind,
                SearchPoint2SensorKind = this.SearchPoint2SensorKind,
                SearchPoint3SensorKind = this.SearchPoint3SensorKind,
                SearchPoint4SensorKind = this.SearchPoint4SensorKind,
                SearchRange = this.SearchRange,
                ZSearchCount = this.ZSearchCount,
                SearchSpeed = this.SearchSpeed,
                FineSearchSpeed = this.FineSearchSpeed,
                SafeHeight = this.SafeHeight,
                SearchNeedleHeightSystem1 = this.SearchNeedleHeightSystem1,
                SearchNeedleHeightSystem2 = this.SearchNeedleHeightSystem2,
                ZProbeDescentHeight = this.ZProbeDescentHeight,
                ZMinHeight = this.ZMinHeight,
                AlignPositionSystem1 = this.AlignPositionSystem1 != null
                    ? new PointF(this.AlignPositionSystem1.X, this.AlignPositionSystem1.Y, this.AlignPositionSystem1.Z)
                    : new PointF(),
                AlignPositionSystem2 = this.AlignPositionSystem2 != null
                    ? new PointF(this.AlignPositionSystem2.X, this.AlignPositionSystem2.Y, this.AlignPositionSystem2.Z)
                    : new PointF(),
                SensorDiX = this.SensorDiX,
                SensorDiY = this.SensorDiY,

                TcpTotalOffsetX = this.TcpTotalOffsetX ?? this.CompensationStorageX,
                TcpTotalOffsetY = this.TcpTotalOffsetY ?? this.CompensationStorageY,
                TcpTotalOffsetZ = this.TcpTotalOffsetZ ?? this.CompensationStorageZ,
                CompensationStorageX = this.TcpTotalOffsetX ?? this.CompensationStorageX,
                CompensationStorageY = this.TcpTotalOffsetY ?? this.CompensationStorageY,
                CompensationStorageZ = this.TcpTotalOffsetZ ?? this.CompensationStorageZ,

                // 复制全局变量链接名称
                CompensationXLinkedVar = this.CompensationXLinkedVar,
                CompensationYLinkedVar = this.CompensationYLinkedVar,
                CompensationZLinkedVar = this.CompensationZLinkedVar,

                CompensationXExpression = this.CompensationXExpression,
                CompensationYExpression = this.CompensationYExpression,
                CompensationZExpression = this.CompensationZExpression,

                Operator = this.Operator,
                IsValid = this.IsValid
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

        /// <summary>更新搜索点单轴坐标并通知绑定刷新</summary>
        private void SetSearchPointAxis(
            ref PointF point,
            float value,
            bool isX,
            string pointPropertyName,
            string xPropertyName,
            string yPropertyName)
        {
            var current = isX ? point.X : point.Y;
            if (Math.Abs(current - value) < 0.0001f) return;

            if (isX)
                point.X = value;
            else
                point.Y = value;

            OnPropertyChanged(isX ? xPropertyName : yPropertyName);
            OnPropertyChanged(pointPropertyName);
        }

        /// <summary>切换针头系统后刷新界面绑定的全部参数字段</summary>
        public void NotifyUiBindingsRefresh()
        {
            OnPropertyChanged(nameof(SearchPoint1));
            OnPropertyChanged(nameof(SearchPoint1X));
            OnPropertyChanged(nameof(SearchPoint1Y));
            OnPropertyChanged(nameof(SearchPoint2));
            OnPropertyChanged(nameof(SearchPoint2X));
            OnPropertyChanged(nameof(SearchPoint2Y));
            OnPropertyChanged(nameof(SearchPoint3));
            OnPropertyChanged(nameof(SearchPoint3X));
            OnPropertyChanged(nameof(SearchPoint3Y));
            OnPropertyChanged(nameof(SearchPoint4));
            OnPropertyChanged(nameof(SearchPoint4X));
            OnPropertyChanged(nameof(SearchPoint4Y));
            OnPropertyChanged(nameof(SearchPoint1SensorKind));
            OnPropertyChanged(nameof(SearchPoint2SensorKind));
            OnPropertyChanged(nameof(SearchPoint3SensorKind));
            OnPropertyChanged(nameof(SearchPoint4SensorKind));
            OnPropertyChanged(nameof(ReferenceXYZ));
            OnPropertyChanged(nameof(CurrentXYZ));
            OnPropertyChanged(nameof(CompensationXYZ));
            OnPropertyChanged(nameof(SearchRange));
            OnPropertyChanged(nameof(ZSearchCount));
            OnPropertyChanged(nameof(SearchSpeed));
            OnPropertyChanged(nameof(FineSearchSpeed));
            OnPropertyChanged(nameof(SafeHeight));
            OnPropertyChanged(nameof(SearchNeedleHeightSystem1));
            OnPropertyChanged(nameof(SearchNeedleHeightSystem2));
            OnPropertyChanged(nameof(SearchNeedleHeight));
            OnPropertyChanged(nameof(ZProbeDescentHeight));
            OnPropertyChanged(nameof(ZMinHeight));
            OnPropertyChanged(nameof(AlignPositionSystem1));
            OnPropertyChanged(nameof(AlignPositionSystem2));
            OnPropertyChanged(nameof(SensorDiX));
            OnPropertyChanged(nameof(SensorDiY));
        }
    }
    /// <summary>
    /// 对针系统状态信息
    /// </summary>
    public class NeedleCalibrationStatus
    {
        public int SystemNumber { get; set; }
        public bool HasParameters { get; set; }
        public DateTime? LastCalibrationTime { get; set; }
        public string CalibrationName { get; set; }
        public bool IsCalibrated { get; set; }
        public string Operator { get; set; }

        // 校准结果
        public bool CalibrationSuccessful { get; set; }
        public double? CalibrationError { get; set; }

        public string StatusText
        {
            get
            {
                if (!HasParameters) return "未配置";
                if (IsCalibrated && LastCalibrationTime.HasValue)
                    return $"已校准 ({LastCalibrationTime.Value:MM-dd HH:mm})";
                return "未校准";
            }
        }
    }
}