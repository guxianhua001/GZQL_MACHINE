using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Core.Abstraction;
using Core.Models;

namespace Stations.TaskParameters
{
    public class AssemblyStationParams : TaskParametersBase, INotifyPropertyChanged
    {
        public override string Identifier => "AssemblyStation";

        private string _taskName = "AssemblyStation";
        [Category("基本信息")]
        [DisplayName("任务名称")]
        [Description("当前任务的名称")]
        public override string TaskName
        {
            get => _taskName;
            set => SetProperty(ref _taskName, value);
        }

        private int _taskId = 3;
        [Category("基本信息")]
        [DisplayName("任务ID")]
        [Description("当前任务的唯一ID")]
        [DisplayFormat(DataFormatString = "F0")]
        [ReadOnly(true)]
        public override int TaskId
        {
            get => _taskId;
            set => SetProperty(ref _taskId, value);
        }

        private string _stationId = "AS-001";
        [Category("工站配置")]
        [DisplayName("工站编号")]
        [Description("装配工站的唯一标识符")]
        [Required(ErrorMessage = "工站编号是必填项")]
        public string StationId
        {
            get => _stationId;
            set => SetProperty(ref _stationId, value);
        }

        private int _cycleTime = 30;
        [Category("工站配置")]
        [DisplayName("节拍时间(秒)")]
        [Description("完成一个装配周期的标准时间")]
        [Range(1, 3600, ErrorMessage = "节拍时间必须在1-3600秒之间")]
        public int CycleTime
        {
            get => _cycleTime;
            set => SetProperty(ref _cycleTime, value);
        }

        private int _operatorCount = 2;
        [Category("工站配置")]
        [DisplayName("操作员数量")]
        [Description("本工站配备的操作人员数量")]
        [Range(1, 10, ErrorMessage = "操作员数量必须在1-10之间")]
        public int OperatorCount
        {
            get => _operatorCount;
            set => SetProperty(ref _operatorCount, value);
        }

        private bool _autoMode = true;
        [Category("运行模式")]
        [DisplayName("自动模式")]
        [Description("是否启用自动运行模式")]
        public bool AutoMode
        {
            get => _autoMode;
            set => SetProperty(ref _autoMode, value);
        }

        private bool _qualityCheckEnabled = true;
        [Category("质量控制")]
        [DisplayName("启用质量检测")]
        [Description("是否在装配过程中进行质量检测")]
        public bool QualityCheckEnabled
        {
            get => _qualityCheckEnabled;
            set => SetProperty(ref _qualityCheckEnabled, value);
        }

        private double _targetQualityRate = 99.5;
        [Category("质量控制")]
        [DisplayName("目标合格率(%)")]
        [Description("预期的产品合格率目标")]
        [Range(0, 100, ErrorMessage = "合格率必须在0-100之间")]
        public double TargetQualityRate
        {
            get => _targetQualityRate;
            set => SetProperty(ref _targetQualityRate, value);
        }

        private int _maxConsecutiveDefects = 3;
        [Category("质量控制")]
        [DisplayName("最大连续缺陷数")]
        [Description("触发自动停线的最大连续缺陷数量")]
        [Range(1, 10, ErrorMessage = "最大连续缺陷数必须在1-10之间")]
        public int MaxConsecutiveDefects
        {
            get => _maxConsecutiveDefects;
            set => SetProperty(ref _maxConsecutiveDefects, value);
        }

        private string _productModel = "Model-X";
        [Category("产品信息")]
        [DisplayName("产品型号")]
        [Description("当前装配的产品型号")]
        [Required(ErrorMessage = "产品型号是必填项")]
        public string ProductModel
        {
            get => _productModel;
            set => SetProperty(ref _productModel, value);
        }

        private int _batchSize = 100;
        [Category("产品信息")]
        [DisplayName("批次大小")]
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

        private int _componentBufferSize = 50;
        [Category("物料管理")]
        [DisplayName("组件缓冲数量")]
        [Description("每个组件的缓冲区容量")]
        [Range(10, 200, ErrorMessage = "缓冲数量必须在10-200之间")]
        public int ComponentBufferSize
        {
            get => _componentBufferSize;
            set => SetProperty(ref _componentBufferSize, value);
        }

        private bool _autoReplenishment = true;
        [Category("物料管理")]
        [DisplayName("自动补料")]
        [Description("是否启用自动物料补充")]
        public bool AutoReplenishment
        {
            get => _autoReplenishment;
            set => SetProperty(ref _autoReplenishment, value);
        }

        private int _lowLevelThreshold = 10;
        [Category("物料管理")]
        [DisplayName("低料位阈值")]
        [Description("触发补料警告的物料最低数量")]
        [Range(1, 50, ErrorMessage = "低料位阈值必须在1-50之间")]
        public int LowLevelThreshold
        {
            get => _lowLevelThreshold;
            set => SetProperty(ref _lowLevelThreshold, value);
        }
        private int _clampPos = 500;
        [Category("工艺参数")]
        [DisplayName("夹紧位置(mm)")]
        [Description("夹爪夹紧位置(mm)")]
        [Range(1, 1000, ErrorMessage = "必须在1-1000之间")]
        public int ClampPos
        {
            get => _clampPos;
            set => SetProperty(ref _clampPos, value);
        }
        private int _releasePos = 800;
        [Category("工艺参数")]
        [DisplayName("松开位置(mm)")]
        [Description("夹爪松开位置(mm)")]
        [Range(1, 1000, ErrorMessage = "必须在1-1000之间")]
        public int ReleasePos
        {
            get => _releasePos;
            set => SetProperty(ref _releasePos, value);
        }
        private double _torqueMin = 10.5;
        [Category("工艺参数")]
        [DisplayName("扭矩最小值(N·m)")]
        [Description("螺丝拧紧的最小扭矩值")]
        [Range(0.1, 100.0, ErrorMessage = "扭矩值必须在0.1-100之间")]
        public double TorqueMin
        {
            get => _torqueMin;
            set => SetProperty(ref _torqueMin, value);
        }

        private double _torqueMax = 12.5;
        [Category("工艺参数")]
        [DisplayName("扭矩最大值(N·m)")]
        [Description("螺丝拧紧的最大扭矩值")]
        [Range(0.1, 100.0, ErrorMessage = "扭矩值必须在0.1-100之间")]
        public double TorqueMax
        {
            get => _torqueMax;
            set => SetProperty(ref _torqueMax, value);
        }

        private double _pressureSetting = 0.6;
        [Category("工艺参数")]
        [DisplayName("气压设定(MPa)")]
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
        [Category("工艺参数")]
        [DisplayName("夹持保持时间(ms)")]
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
        [Category("工艺参数")]
        [DisplayName("小步下降高度(mm)")]
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
        [Category("工艺参数")]
        [DisplayName("小步平移位(mm)")]
        [Description("装配过程中的小步平移距离")]
        [Range(0, 2, ErrorMessage = "必须在0-2毫米之间")]
        public double StepTranslate
        {
            get => _stepTranslate;
            set => SetProperty(ref _stepTranslate, value);
        }

        private double _slotAngleTolerance = 0.5;
        [Category("装配精度")]
        [DisplayName("槽位角度公差(度)")]
        [Description("拨片与孔位的安装角度允许偏差")]
        [Range(0.1, 5.0, ErrorMessage = "必须在0.1-5之间")]
        public double SlotAngleTolerance
        {
            get => _slotAngleTolerance;
            set => SetProperty(ref _slotAngleTolerance, value);
        }
        private double _slotOffsetTolerance = 0.5;
        [Category("装配精度")]
        [DisplayName("槽位中心与底边偏移公差(mm)")]
        [Description("槽位中心与底边的安装位置允许偏差")]
        [Range(0.1, 5.0, ErrorMessage = "必须在0.1-5之间")]
        public double SlotOffsetTolerance
        {
            get => _slotOffsetTolerance;
            set => SetProperty(ref _slotOffsetTolerance, value);
        }
        // 槽位中心X偏差上限
        private double _slotCenterXMaxOffset = 0.5;
        [Category("装配精度")]
        [DisplayName("槽位中心X偏差上限(mm)")]
        [Description("拨片与孔位的安装位置允许偏差")]
        [Range(0.1, 10.0, ErrorMessage = "必须在0.1-5之间")]
        public double SlotCenterXMaxOffset
        {
            get => _slotCenterXMaxOffset;
            set => SetProperty(ref _slotCenterXMaxOffset, value);
        }
        private double _slotCenterYMaxOffset = 0.5;
        [Category("装配精度")]
        [DisplayName("槽位中心Y偏差上限(mm)")]
        [Description("拨片与孔位的安装位置允许偏差")]
        [Range(0.1, 5.0, ErrorMessage = "必须在0.1-5之间")]
        public double SlotCenterYMaxOffset
        {
            get => _slotCenterYMaxOffset;
            set => SetProperty(ref _slotCenterYMaxOffset, value);
        }
        private double _UAxisMinAngle = 0.5;
        [Category("装配精度")]
        [DisplayName("U轴最小角度(度)")]
        [Description("U轴旋转的最小允许角度")]
        [Range(-90.0, 90.0, ErrorMessage = "必须在-90.0-90之间")]
        public double UAxisMinAngle
        {
            get => _UAxisMinAngle;
            set => SetProperty(ref _UAxisMinAngle, value);
        }
        private double _UAxisMaxAngle = 10.0;
        [Category("装配精度")]
        [DisplayName("U轴最大角度(度)")]
        [Description("U轴旋转的最大允许角度")]
        [Range(0.1, 90.0, ErrorMessage = "必须在0.1-10之间")]
        public double UAxisMaxAngle
        {
            get => _UAxisMaxAngle;
            set => SetProperty(ref _UAxisMaxAngle, value);
        }
        private double _UAxisCorrectionSpeed = 1.5;
        [Category("装配精度")]
        [DisplayName("U轴校正速度(度/s)")]
        [Description("U轴旋转的校正速度")]
        [Range(0.1, 10.0, ErrorMessage = "必须在0.1-10之间")]
        public double UAxisCorrectionSpeed
        {
            get => _UAxisCorrectionSpeed;
            set => SetProperty(ref _UAxisCorrectionSpeed, value);
        }

        // ActuatorCorrectionMaxRetries
        private int _actuatorCorrectionMaxRetries = 3;
        [Category("装配精度")]
        [DisplayName("执行器校正最大重试次数")]
        [Description("当执行器校正失败时，允许的最大重试次数")]
        [Range(1, 10)]
        public int ActuatorCorrectionMaxRetries
        {
            get => _actuatorCorrectionMaxRetries;
            set => SetProperty(ref _actuatorCorrectionMaxRetries, value);
        }
        // ActuatorXTolerance
        private double _actuatorXTolerance = 0.05;
        [Category("装配精度")]
        [DisplayName("执行器X轴允许误差 (mm)")]
        [Description("执行器在X轴方向上的允许偏差")]
        [Range(0.01, 0.5)]
        public double ActuatorXTolerance
        {
            get => _actuatorXTolerance;
            set => SetProperty(ref _actuatorXTolerance, value);
        }
        // ActuatorFirstPhotoHeight
        private double _actuatorFirstPhotoHeight = 0.05;
        [Category("装配精度")]
        [DisplayName("执行器首次拍照高度 (mm)")]
        [Description("执行器在首次拍照时的Z轴位置")]
        [Range(0.01, 35.0)]
        public double ActuatorFirstPhotoHeight
        {
            get => _actuatorFirstPhotoHeight;
            set => SetProperty(ref _actuatorFirstPhotoHeight, value);
        }
        // ActuatorSecondPhotoHeight
        private double _actuatorSecondPhotoHeight = 0.05;
        [Category("装配精度")]
        [DisplayName("执行器二次拍照高度 (mm)")]
        [Description("执行器在二次拍照时的Z轴位置")]
        [Range(0.01, 35.0)]
        public double ActuatorSecondPhotoHeight
        {
            get => _actuatorSecondPhotoHeight;
            set => SetProperty(ref _actuatorSecondPhotoHeight, value);
        }

        private double _actuatorStandardX1Spacing = 2.6;
        [Category("装配精度")]
        [DisplayName("执行器标准间距 (mm)")]
        [Description("执行器1号的标准间距，用于X方向校正")]
        [Range(0.01, 20.0)]
        public double ActuatorStandardX1Spacing
        {
            get => _actuatorStandardX1Spacing;
            set => SetProperty(ref _actuatorStandardX1Spacing, value);
        }

        private double _actuatorStandardY1Spacing = 0.45;
        [Category("装配精度")]
        [DisplayName("执行器标准间距 (mm)")]
        [Description("执行器1号的标准间距，用于Y方向校正")]
        [Range(0.01, 20.0)]
        public double ActuatorStandardY1Spacing
        {
            get => _actuatorStandardY1Spacing;
            set => SetProperty(ref _actuatorStandardY1Spacing, value);
        }

        private double _actuatorStandardX2Spacing = 2.6;
        [Category("装配精度")]
        [DisplayName("执行器标准间距 (mm)")]
        [Description("执行器2号的标准间距，用于X方向校正")]
        [Range(0.01, 20.0)]
        public double ActuatorStandardX2Spacing
        {
            get => _actuatorStandardX2Spacing;
            set => SetProperty(ref _actuatorStandardX2Spacing, value);
        }
        private double _actuatorStandardY2Spacing = 0.45;
        [Category("装配精度")]
        [DisplayName("执行器标准间距 (mm)")]
        [Description("执行器2号的标准间距，用于Y方向校正")]
        [Range(0.01, 20.0)]
        public double ActuatorStandardY2Spacing
        {
            get => _actuatorStandardY2Spacing;
            set => SetProperty(ref _actuatorStandardY2Spacing, value);
        }
        private double _actuatorStandardX3Spacing = 2.6;
        [Category("装配精度")]
        [DisplayName("执行器标准间距 (mm)")]
        [Description("执行器3号的标准间距，用于X方向校正")]
        [Range(0.01, 20.0)]
        public double ActuatorStandardX3Spacing
        {
            get => _actuatorStandardX3Spacing;
            set => SetProperty(ref _actuatorStandardX3Spacing, value);
        }
        private double _actuatorStandardY3Spacing = 0.45;
        [Category("装配精度")]
        [DisplayName("执行器标准间距 (mm)")]
        [Description("执行器3号的标准间距，用于Y方向校正")]
        [Range(0.01, 20.0)]
        public double ActuatorStandardY3Spacing
        {
            get => _actuatorStandardY3Spacing;
            set => SetProperty(ref _actuatorStandardY3Spacing, value);
        }
        private double _actuatorStandardX4Spacing = 2.6;
        [Category("装配精度")]
        [DisplayName("执行器标准间距 (mm)")]
        [Description("执行器4号的标准间距，用于X方向校正")]
        [Range(0.01, 20.0)]
        public double ActuatorStandardX4Spacing
        {
            get => _actuatorStandardX4Spacing;
            set => SetProperty(ref _actuatorStandardX4Spacing, value);
        }
        private double _actuatorStandardY4Spacing = 0.45;
        [Category("装配精度")]
        [DisplayName("执行器标准间距 (mm)")]
        [Description("执行器4号的标准间距，用于Y方向校正")]
        [Range(0.01, 20.0)]
        public double ActuatorStandardY4Spacing
        {
            get => _actuatorStandardY4Spacing;
            set => SetProperty(ref _actuatorStandardY4Spacing, value);
        }
        private double _actuatorStandardX5Spacing = 2.6;
        [Category("装配精度")]
        [DisplayName("执行器标准间距 (mm)")]
        [Description("执行器5号的标准间距，用于X方向校正")]
        [Range(0.01, 20.0)]
        public double ActuatorStandardX5Spacing
        {
            get => _actuatorStandardX5Spacing;
            set => SetProperty(ref _actuatorStandardX5Spacing, value);
        }
        private double _actuatorStandardY5Spacing = 0.45;
        [Category("装配精度")]
        [DisplayName("执行器标准间距 (mm)")]
        [Description("执行器5号的标准间距，用于Y方向校正")]
        [Range(0.01, 20.0)]
        public double ActuatorStandardY5Spacing
        {
            get => _actuatorStandardY5Spacing;
            set => SetProperty(ref _actuatorStandardY5Spacing, value);
        }
        private double _actuatorStandardX6Spacing = 2.6;
        [Category("装配精度")]
        [DisplayName("执行器标准间距 (mm)")]
        [Description("执行器6号的标准间距，用于X方向校正")]
        [Range(0.01, 20.0)]
        public double ActuatorStandardX6Spacing
        {
            get => _actuatorStandardX6Spacing;
            set => SetProperty(ref _actuatorStandardX6Spacing, value);
        }
        private double _actuatorXMaxError = 2.8;
        [Category("装配精度")]
        [DisplayName("执行器X轴最大误差 (mm)")]
        [Description("执行器在X轴方向上的最大允许误差")]
        [Range(0.01, 0.5)]
        public double ActuatorXMaxError
        {
            get => _actuatorXMaxError;
            set => SetProperty(ref _actuatorXMaxError, value);
        }
        private int _visionInspectionTimeout = 5000;
        [Category("设备参数")]
        [DisplayName("视觉检测超时(ms)")]
        [Description("视觉检测系统的超时时间")]
        [Range(1000, 30000, ErrorMessage = "超时时间必须在1000-30000毫秒之间")]
        public int VisionInspectionTimeout
        {
            get => _visionInspectionTimeout;
            set => SetProperty(ref _visionInspectionTimeout, value);
        }

        private bool _enableDataLogging = true;
        [Category("数据记录")]
        [DisplayName("启用数据记录")]
        [Description("是否记录生产过程中的关键数据")]
        public bool EnableDataLogging
        {
            get => _enableDataLogging;
            set => SetProperty(ref _enableDataLogging, value);
        }

        private int _dataLogInterval = 1000;
        [Category("数据记录")]
        [DisplayName("数据记录间隔(ms)")]
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
        [Category("工艺参数")]
        [DisplayName("小步下降速度(mm/s)")]
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
        [Category("工艺参数")]
        [DisplayName("小步平移速度(mm/s)")]
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
        [Category("工艺参数")]
        [DisplayName("XY轴前进后退速度(mm/s)")]
        [Description("装配过程中的XY轴前进和后退的速度")]
        [Range(0, 30, ErrorMessage = "必须在0-30毫米每秒之间")]
        public double AssemblySpeed
        {
            get => _xySpeed;
            set => SetProperty(ref _xySpeed, value);
        }
        private double _safeZHeight = 30.0;
        [Category("运动控制")]
        [DisplayName("安全高度 (mm)")]
        [Description("安全移动高度")]
        [Range(-30.0, 30.0)]
        public double SafeZHeight
        {
            get => _safeZHeight;
            set => SetProperty(ref _safeZHeight, value);
        }
        #endregion  

        // 计算属性示例
        //[Browsable(false)]
        //public string ComponentsSummary => RequiredComponents != null ?
        //    string.Join(", ", RequiredComponents.Take(3)) +
        //    (RequiredComponents.Count > 3 ? "..." : "") : "无";

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