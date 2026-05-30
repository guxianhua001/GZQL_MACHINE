using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Core.Abstraction;
using Core.Models;

namespace StationTasks.Params
{
    public class AssemblyStationParams : TaskParametersBase, INotifyPropertyChanged
    {
        public override string Identifier => "AssemblyStation";

        private string _taskName = "AssemblyStation";
        [Category("Basic Information")]
        [DisplayName("Task Name")]
        [Description("当前任务的名称")]
        public override string TaskName
        {
            get => _taskName;
            set => SetProperty(ref _taskName, value);
        }

        private int _taskId = 3;
        [Category("Basic Information")]
        [DisplayName("Task ID")]
        [Description("当前任务的唯一ID")]
        [DisplayFormat(DataFormatString = "F0")]
        [ReadOnly(true)]
        public override int TaskId
        {
            get => _taskId;
            set => SetProperty(ref _taskId, value);
        }

        private string _stationId = "AS-001";
        [Category("Workstation Configuration")]
        [DisplayName("工站编号")]
        [Description("装配工站的唯一标识符")]
        [Required(ErrorMessage = "工站编号是必填项")]
        public string StationId
        {
            get => _stationId;
            set => SetProperty(ref _stationId, value);
        }

        private int _cycleTime = 30;
        [Category("Workstation Configuration")]
        [DisplayName("Beat Time(秒)")]
        [Description("完成一个装配周期的标准时间")]
        [Range(1, 3600, ErrorMessage = "节拍时间必须在1-3600秒之间")]
        public int CycleTime
        {
            get => _cycleTime;
            set => SetProperty(ref _cycleTime, value);
        }

        private int _operatorCount = 2;
        [Category("Workstation Configuration")]
        [DisplayName("Number of operators")]
        [Description("本工站配备的操作人员数量")]
        [Range(1, 10, ErrorMessage = "操作员数量必须在1-10之间")]
        public int OperatorCount
        {
            get => _operatorCount;
            set => SetProperty(ref _operatorCount, value);
        }

        private bool _autoMode = true;
        [Category("Operating Mode")]
        [DisplayName("Auto Mode")]
        [Description("是否启用自动运行模式")]
        public bool AutoMode
        {
            get => _autoMode;
            set => SetProperty(ref _autoMode, value);
        }

        private bool _qualityCheckEnabled = true;
        [Category("Quality Control")]
        [DisplayName("Enable Quality Inspection")]
        [Description("是否在装配过程中进行质量检测")]
        public bool QualityCheckEnabled
        {
            get => _qualityCheckEnabled;
            set => SetProperty(ref _qualityCheckEnabled, value);
        }

        private double _targetQualityRate = 99.5;
        [Category("Quality Control")]
        [DisplayName("Target Qualification Rate(%)")]
        [Description("预期的产品合格率目标")]
        [Range(0, 100, ErrorMessage = "合格率必须在0-100之间")]
        public double TargetQualityRate
        {
            get => _targetQualityRate;
            set => SetProperty(ref _targetQualityRate, value);
        }

        private int _maxConsecutiveDefects = 3;
        [Category("Quality Control")]
        [DisplayName("Maximum Continuous Defect Count")]
        [Description("触发自动停线的最大连续缺陷数量")]
        [Range(1, 10, ErrorMessage = "最大连续缺陷数必须在1-10之间")]
        public int MaxConsecutiveDefects
        {
            get => _maxConsecutiveDefects;
            set => SetProperty(ref _maxConsecutiveDefects, value);
        }

        private string _productModel = "Model-X";
        [Category("Product Information")]
        [DisplayName("Product Model")]
        [Description("当前装配的产品型号")]
        [Required(ErrorMessage = "产品型号是必填项")]
        public string ProductModel
        {
            get => _productModel;
            set => SetProperty(ref _productModel, value);
        }

        private int _batchSize = 100;
        [Category("Product Information")]
        [DisplayName("Batch size")]
        [Description("每个生产批次的产品数量")]
        [Range(1, 1000, ErrorMessage = "批次大小必须在1-1000之间")]
        public int BatchSize
        {
            get => _batchSize;
            set => SetProperty(ref _batchSize, value);
        }

        //private List<string> _requiredComponents = new List<string> { "Motor", "Housing", "PCB" };
        //[Category("物料管理")]
        //[DisplayName("所需组件")]
        //[Description("装配所需的组件列表")]
        //public List<string> RequiredComponents
        //{
        //    get => _requiredComponents;
        //    set => SetProperty(ref _requiredComponents, value);
        //}

        private int _clampPos = 500;
        [Category("Process Parameters")]
        [DisplayName("Clamping position(mm)")]
        [Description("夹爪夹紧位置(mm)")]
        [Range(1, 1000, ErrorMessage = "必须在1-1000之间")]
        public int ClampPos
        {
            get => _clampPos;
            set => SetProperty(ref _clampPos, value);
        }
        private int _releasePos = 800;
        [Category("Process Parameters")]
        [DisplayName("Release Position(mm)")]
        [Description("夹爪松开位置(mm)")]
        [Range(1, 1000, ErrorMessage = "必须在1-1000之间")]
        public int ReleasePos
        {
            get => _releasePos;
            set => SetProperty(ref _releasePos, value);
        }
        private double _torqueMin = 10.5;
        [Category("Process Parameters")]
        [DisplayName("Minimum torque(N·m)")]
        [Description("螺丝拧紧的最小扭矩值")]
        [Range(0.1, 100.0, ErrorMessage = "扭矩值必须在0.1-100之间")]
        public double TorqueMin
        {
            get => _torqueMin;
            set => SetProperty(ref _torqueMin, value);
        }

        private double _torqueMax = 12.5;
        [Category("Process Parameters")]
        [DisplayName("Maximum torque(N·m)")]
        [Description("螺丝拧紧的最大扭矩值")]
        [Range(0.1, 100.0, ErrorMessage = "扭矩值必须在0.1-100之间")]
        public double TorqueMax
        {
            get => _torqueMax;
            set => SetProperty(ref _torqueMax, value);
        }

        private double _pressureSetting = 0.6;
        [Category("Process Parameters")]
        [DisplayName("Air Pressure Setting(MPa)")]
        [Description("气动工具的工作气压")]
        [Range(0.1, 1.0, ErrorMessage = "气压设定必须在0.1-1.0之间")]
        public double PressureSetting
        {
            get => _pressureSetting;
            set => SetProperty(ref _pressureSetting, value);
        }

        /// <summary>
        /// 夹爪夹取保持时间(ms)
        /// </summary>
        private int _clampHoldTime = 200;
        [Category("Process Parameters")]
        [DisplayName("Clamping hold time(ms)")]
        [Description("夹爪在夹取过程中的保持时间")]
        [Range(0, 2000, ErrorMessage = "必须在0-2000毫秒之间")]
        public int ClampHoldTime
        {
            get => _clampHoldTime;
            set => SetProperty(ref _clampHoldTime, value);
        }
        /// <summary>
        /// 装配小步下降高度(mm)
        /// </summary>
        private double _stepDownHeight = 0.1;
        [Category("Process Parameters")]
        [DisplayName("Small step decrease height(mm)")]
        [Description("装配过程中的小步下降高度")]
        [Range(0, 2, ErrorMessage = "必须在0-2毫米之间")]
        public double StepDownHeight
        {
            get => _stepDownHeight;
            set => SetProperty(ref _stepDownHeight, value);
        }
        /// <summary>
        /// 装配小步平移位
        /// </summary>
        private double _stepTranslate = 0.1;
        [Category("Process Parameters")]
        [DisplayName("Small step translation(mm)")]
        [Description("装配过程中的小步平移距离")]
        [Range(0, 2, ErrorMessage = "必须在0-2毫米之间")]
        public double StepTranslate
        {
            get => _stepTranslate;
            set => SetProperty(ref _stepTranslate, value);
        }
        /// <summary>
        /// 径向过压参数(Y方向)
        /// </summary>
        private double _radialOverpressureY = 0.1;
        [Category("Process Parameters")]
        [DisplayName("Radial overpressure (Y-direction) (mm)")]
        [Description("装配过程中的径向过压补偿值，用于调整Y方向的偏差")]
        [Range(-2, 2, ErrorMessage = "必须在-2到2毫米之间")]
        public double RadialOverpressureY
        {
            get => _radialOverpressureY;
            set => SetProperty(ref _radialOverpressureY, value);
        }
        /// <summary>
        /// 径向过压参数(Z方向)
        /// </summary>
        private double _radialOverpressureZ = 0.1;
        [Category("Process Parameters")]
        [DisplayName("Z-axis overpressure (Z-direction) (mm)")]
        [Description("装配过程中的径向过压补偿值，用于调整Z方向的偏差")]
        [Range(-2, 2, ErrorMessage = "必须在-2到2毫米之间")]
        public double RadialOverpressureZ
        {
            get => _radialOverpressureZ;
            set => SetProperty(ref _radialOverpressureZ, value);
        }

        private double _slotAngleTolerance = 0.5;
        [Category("Assembly accuracy")]
        [DisplayName("Slot Angle Tolerance (degrees)")]
        [Description("拨片与孔位的安装角度允许偏差,决定是否进行拨片的标准")]
        [Range(0.1, 5.0, ErrorMessage = "必须在0.1-5之间")]
        public double SlotAngleTolerance
        {
            get => _slotAngleTolerance;
            set => SetProperty(ref _slotAngleTolerance, value);
        }
        private double _slotOffsetTolerance = 0.5;
        [Category("Assembly accuracy")]
        [DisplayName("Center-to-bottom edge offset tolerance (mm)")]
        [Description("槽位中心与底边的安装位置允许偏差,决定是否进行拨片的标准")]
        [Range(0.1, 5.0, ErrorMessage = "必须在0.1-5之间")]
        public double SlotOffsetTolerance
        {
            get => _slotOffsetTolerance;
            set => SetProperty(ref _slotOffsetTolerance, value);
        }
        // 槽位中心X偏差上限
        private double _slotCenterXMaxOffset = 0.5;
        [Category("Assembly accuracy")]
        [DisplayName("Maximum X-Deviation at Slot Center (mm)")]
        [Description("拨片与孔位的安装位置允许偏差")]
        [Range(0.1, 10.0, ErrorMessage = "必须在0.1-5之间")]
        public double SlotCenterXMaxOffset
        {
            get => _slotCenterXMaxOffset;
            set => SetProperty(ref _slotCenterXMaxOffset, value);
        }
        private double _slotCenterYMaxOffset = 0.5;
        [Category("Assembly accuracy")]
        [DisplayName("aximum Y-Deviation at Slot Center (mm)")]
        [Description("拨片与孔位的安装位置允许偏差")]
        [Range(0.1, 5.0, ErrorMessage = "必须在0.1-5之间")]
        public double SlotCenterYMaxOffset
        {
            get => _slotCenterYMaxOffset;
            set => SetProperty(ref _slotCenterYMaxOffset, value);
        }
        private double _UAxisMinAngle = 0.5;
        [Category("Assembly accuracy")]
        [DisplayName("Minimum angle of the U-axis (degrees)")]
        [Description("U轴旋转的最小允许角度")]
        [Range(-90.0, 90.0, ErrorMessage = "必须在-90.0-90之间")]
        public double UAxisMinAngle
        {
            get => _UAxisMinAngle;
            set => SetProperty(ref _UAxisMinAngle, value);
        }
        private double _UAxisMaxAngle = 10.0;
        [Category("Assembly accuracy")]
        [DisplayName("Maximum angle of the U-axis (degrees)")]
        [Description("U轴旋转的最大允许角度")]
        [Range(0.1, 90.0, ErrorMessage = "必须在0.1-10之间")]
        public double UAxisMaxAngle
        {
            get => _UAxisMaxAngle;
            set => SetProperty(ref _UAxisMaxAngle, value);
        }
        private double _UAxisCorrectionSpeed = 1.5;
        [Category("Assembly accuracy")]
        [DisplayName("U-axis Calibration Speed (degrees/s)")]
        [Description("U轴旋转的校正速度")]
        [Range(0.1, 10.0, ErrorMessage = "必须在0.1-10之间")]
        public double UAxisCorrectionSpeed
        {
            get => _UAxisCorrectionSpeed;
            set => SetProperty(ref _UAxisCorrectionSpeed, value);
        }

        // ActuatorCorrectionMaxRetries
        private int _actuatorCorrectionMaxRetries = 3;
        [Category("Assembly accuracy")]
        [DisplayName("Maximum Retry Count for Actuator Calibration")]
        [Description("当执行器校正失败时，允许的最大重试次数")]
        [Range(1, 10)]
        public int ActuatorCorrectionMaxRetries
        {
            get => _actuatorCorrectionMaxRetries;
            set => SetProperty(ref _actuatorCorrectionMaxRetries, value);
        }
        // ActuatorXTolerance
        private double _actuatorXTolerance = 0.05;
        [Category("Assembly accuracy")]
        [DisplayName("Actuator X-axis tolerance (mm)")]
        [Description("执行器在X轴方向上的允许偏差")]
        [Range(0.01, 0.5)]
        public double ActuatorXTolerance
        {
            get => _actuatorXTolerance;
            set => SetProperty(ref _actuatorXTolerance, value);
        }
        // 执行器在X轴方向上的允许最大偏差
        private double _actuatorXMaxOffset = 1.5;
        [Category("Assembly accuracy")]
        [DisplayName("Maximum deviation of actuator X-axis (mm)")]
        [Description("执行器在X轴方向上的最大允许偏差")]
        [Range(0.1, 5.0)]
        public double ActuatorXMaxOffset
        {
            get => _actuatorXMaxOffset;
            set => SetProperty(ref _actuatorXMaxOffset, value);
        }

        private double _actuatorStandardX1Spacing = 2.6;
        [Category("Assembly accuracy")]
        [DisplayName("Actuator Standard Spacing (mm)")]
        [Description("执行器1号的标准间距，用于X方向校正")]
        [Range(0.01, 20.0)]
        public double ActuatorStandardX1Spacing
        {
            get => _actuatorStandardX1Spacing;
            set => SetProperty(ref _actuatorStandardX1Spacing, value);
        }

        private double _actuatorStandardY1Spacing = 0.45;
        [Category("Assembly accuracy")]
        [DisplayName("Actuator Standard Spacing (mm)")]
        [Description("执行器1号的标准间距，用于Y方向校正")]
        [Range(0.01, 20.0)]
        public double ActuatorStandardY1Spacing
        {
            get => _actuatorStandardY1Spacing;
            set => SetProperty(ref _actuatorStandardY1Spacing, value);
        }

        private double _actuatorStandardX2Spacing = 2.6;
        [Category("Assembly accuracy")]
        [DisplayName("Actuator Standard Spacing (mm)")]
        [Description("执行器2号的标准间距，用于X方向校正")]
        [Range(0.01, 20.0)]
        public double ActuatorStandardX2Spacing
        {
            get => _actuatorStandardX2Spacing;
            set => SetProperty(ref _actuatorStandardX2Spacing, value);
        }
        private double _actuatorStandardY2Spacing = 0.45;
        [Category("Assembly accuracy")]
        [DisplayName("Actuator Standard Spacing (mm)")]
        [Description("执行器2号的标准间距，用于Y方向校正")]
        [Range(0.01, 20.0)]
        public double ActuatorStandardY2Spacing
        {
            get => _actuatorStandardY2Spacing;
            set => SetProperty(ref _actuatorStandardY2Spacing, value);
        }
        private double _actuatorStandardX3Spacing = 2.6;
        [Category("Assembly accuracy")]
        [DisplayName("Actuator Standard Spacing  (mm)")]
        [Description("执行器3号的标准间距，用于X方向校正")]
        [Range(0.01, 20.0)]
        public double ActuatorStandardX3Spacing
        {
            get => _actuatorStandardX3Spacing;
            set => SetProperty(ref _actuatorStandardX3Spacing, value);
        }
        private double _actuatorStandardY3Spacing = 0.45;
        [Category("Assembly accuracy")]
        [DisplayName("Actuator Standard Spacing (mm)")]
        [Description("执行器3号的标准间距，用于Y方向校正")]
        [Range(0.01, 20.0)]
        public double ActuatorStandardY3Spacing
        {
            get => _actuatorStandardY3Spacing;
            set => SetProperty(ref _actuatorStandardY3Spacing, value);
        }
        private double _actuatorStandardX4Spacing = 2.6;
        [Category("Assembly accuracy")]
        [DisplayName("Actuator Standard Spacing (mm)")]
        [Description("执行器4号的标准间距，用于X方向校正")]
        [Range(0.01, 20.0)]
        public double ActuatorStandardX4Spacing
        {
            get => _actuatorStandardX4Spacing;
            set => SetProperty(ref _actuatorStandardX4Spacing, value);
        }
        private double _actuatorStandardY4Spacing = 0.45;
        [Category("Assembly accuracy")]
        [DisplayName("Actuator Standard Spacing (mm)")]
        [Description("执行器4号的标准间距，用于Y方向校正")]
        [Range(0.01, 20.0)]
        public double ActuatorStandardY4Spacing
        {
            get => _actuatorStandardY4Spacing;
            set => SetProperty(ref _actuatorStandardY4Spacing, value);
        }
        private double _actuatorStandardX5Spacing = 2.6;
        [Category("Assembly accuracy")]
        [DisplayName("Actuator Standard Spacing (mm)")]
        [Description("执行器5号的标准间距，用于X方向校正")]
        [Range(0.01, 20.0)]
        public double ActuatorStandardX5Spacing
        {
            get => _actuatorStandardX5Spacing;
            set => SetProperty(ref _actuatorStandardX5Spacing, value);
        }
        private double _actuatorStandardY5Spacing = 0.45;
        [Category("Assembly accuracy")]
        [DisplayName("Actuator Standard Spacing (mm)")]
        [Description("执行器5号的标准间距，用于Y方向校正")]
        [Range(0.01, 20.0)]
        public double ActuatorStandardY5Spacing
        {
            get => _actuatorStandardY5Spacing;
            set => SetProperty(ref _actuatorStandardY5Spacing, value);
        }
        private double _actuatorStandardX6Spacing = 2.6;
        [Category("Assembly accuracy")]
        [DisplayName("Actuator Standard Spacing (mm)")]
        [Description("执行器6号的标准间距，用于X方向校正")]
        [Range(0.01, 20.0)]
        public double ActuatorStandardX6Spacing
        {
            get => _actuatorStandardX6Spacing;
            set => SetProperty(ref _actuatorStandardX6Spacing, value);
        }
        private double _actuatorStandardY6Spacing = 2.6;
        [Category("Assembly accuracy")]
        [DisplayName("Actuator Standard Spacing (mm)")]
        [Description("执行器5号的标准间距，用于X方向校正")]
        [Range(0.01, 20.0)]
        public double ActuatorStandardY6Spacing
        {
            get => _actuatorStandardY6Spacing;
            set => SetProperty(ref _actuatorStandardY6Spacing, value);
        }

        private double _actuatorXMaxError = 2.8;
        [Category("Assembly accuracy")]
        [DisplayName("Maximum error of actuator X-axis (mm)")]
        [Description("执行器在X轴方向上的最大允许误差")]
        [Range(0.01, 0.5)]
        public double ActuatorXMaxError
        {
            get => _actuatorXMaxError;
            set => SetProperty(ref _actuatorXMaxError, value);
        }
        // slot拨片的x方向补偿
        private double _slotXCompensation = 0.0;
        [Category("装配精度)]")]
        [DisplayName("X-axis compensation for the picker (mm)")]
        [Description("拨片在X轴方向的补偿值，用于校正偏差")]
        [Range(-1.0, 1.0)]
        public double SlotXCompensation
        {
            get => _slotXCompensation;
            set => SetProperty(ref _slotXCompensation, value);
        }
        // slot拨片的z方向补偿
        private double _slotZCompensation = 0.0;
        [Category("Assembly accuracy")]
        [DisplayName("Z-axis compensation for the picker (mm)")]
        [Description("拨片在Z轴方向的补偿值，用于校正偏差")]
        [Range(-1.0, 1.0)]
        public double SlotZCompensation
        {
            get => _slotZCompensation;
            set => SetProperty(ref _slotZCompensation, value);
        }

        private int _visionInspectionTimeout = 5000;
        [Category("Equipment Parameters")]
        [DisplayName("Visual Inspection Timeout(ms)")]
        [Description("视觉检测系统的超时时间")]
        [Range(1000, 30000, ErrorMessage = "超时时间必须在1000-30000毫秒之间")]
        public int VisionInspectionTimeout
        {
            get => _visionInspectionTimeout;
            set => SetProperty(ref _visionInspectionTimeout, value);
        }

        private bool _enableDataLogging = true;
        [Category("Data Logging")]
        [DisplayName("Enable Data Logging")]
        [Description("是否记录生产过程中的关键数据")]
        public bool EnableDataLogging
        {
            get => _enableDataLogging;
            set => SetProperty(ref _enableDataLogging, value);
        }

        private int _dataLogInterval = 1000;
        [Category("Data Logging")]
        [DisplayName("Data recording interval(ms)")]
        [Description("记录数据的间隔时间")]
        [Range(100, 10000, ErrorMessage = "记录间隔必须在100-10000毫秒之间")]
        public int DataLogInterval
        {
            get => _dataLogInterval;
            set => SetProperty(ref _dataLogInterval, value);
        }

        #region 运动控制参数
        /// <summary>
        /// 装配小步下降速度(mm/s)
        /// </summary>
        private double _stepDownSpeed = 0.5;
        [Category("Motion Parameters")]
        [DisplayName("Small step decrease in speed(mm/s)")]
        [Description("装配过程中的小步下降速度")]
        [Range(0, 10, ErrorMessage = "必须在0-10毫米每秒之间")]
        public double StepDownSpeed
        {
            get => _stepDownSpeed;
            set => SetProperty(ref _stepDownSpeed, value);
        }
        /// <summary>
        /// 装配小步平移速度(mm/s)
        /// </summary>
        private double _stepTranslateSpeed = 0.5;
        [Category("Motion Parameters")]
        [DisplayName("Small step translation speed(mm/s)")]
        [Description("装配过程中的小步平移速度")]
        [Range(0, 10, ErrorMessage = "必须在0-10毫米每秒之间")]
        public double StepTranslateSpeed
        {
            get => _stepTranslateSpeed;
            set => SetProperty(ref _stepTranslateSpeed, value);
        }
        /// <summary>
        /// 装配时的XY轴前进后退速度(mm/s)
        /// </summary>
        private double _xySpeed = 5;
        [Category("Motion Parameters")]
        [DisplayName("XY-axis forward and reverse speed(mm/s)")]
        [Description("装配过程中的XY轴前进和后退的速度")]
        [Range(0, 30, ErrorMessage = "必须在0-30毫米每秒之间")]
        public double AssemblySpeed
        {
            get => _xySpeed;
            set => SetProperty(ref _xySpeed, value);
        }
        /// <summary>
        /// 拨片时的y轴速度(mm/s)
        /// </summary>
        private double _ySpeed = 0.5;
        [Category("Motion Parameters")]
        [DisplayName("Picking Process Y-Axis Speed(mm/s)")]
        [Description("拨片过程中的Y轴移动速度")]
        [Range(0, 5, ErrorMessage = "必须在0-5毫米每秒之间")]
        public double YSpeed
        {
            get => _ySpeed;
            set => SetProperty(ref _ySpeed, value);
        }
        /// <summary>
        /// 拨片时的z轴速度(mm/s)
        /// </summary>
        private double _zSpeed = 0.5;
        [Category("Motion Parameters")]
        [DisplayName("Picking Process Z-Axis Speed(mm/s)")]
        [Description("拨片过程中的Z轴移动速度")]
        [Range(0, 5, ErrorMessage = "必须在0-5毫米每秒之间")]
        public double ZSpeed
        {
            get => _zSpeed;
            set => SetProperty(ref _zSpeed, value);
        }
        
        private double _safeZHeight = 30.0;
        [Category("Motion Parameters")]
        [DisplayName("Safety Height (mm)")]
        [Description("安全移动高度")]
        [Range(-30.0, 30.0)]
        public double SafeZHeight
        {
            get => _safeZHeight;
            set => SetProperty(ref _safeZHeight, value);
        }
        // 组装过程中z轴下降到预装高度的速度
        private double _zDownSpeed = 5;
        [Category("Motion Parameters")]
        [DisplayName("Z-axis descent speed (mm/s)")]
        [Description("组装过程中Z轴下降到预装高度的速度")]
        [Range(0.1, 5)]
        public double ZDownSpeed
        {
            get => _zDownSpeed;
            set => SetProperty(ref _zDownSpeed, value);
        }
        #endregion

        [ParameterIgnore]

        public List<GlobalVariable> GlobalVariables { get; set; } = new List<GlobalVariable>();
        [ParameterIgnore]
        public Dictionary<string, FlexiblePosition> Positions { get; set; } = new Dictionary<string, FlexiblePosition>();

        public AssemblyStationParams()
        {
            // 确保字典至少包含两个默认位置 
            if (!Positions.ContainsKey("StandbyPosition"))
                Positions["StandbyPosition"] = new FlexiblePosition();
            if (!Positions.ContainsKey("SafePosition"))
                Positions["SafePosition"] = new FlexiblePosition();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}