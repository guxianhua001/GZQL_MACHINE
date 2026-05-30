using Prism.Mvvm;
using System.Collections.ObjectModel;

namespace Core.Models
{
    /// <summary>
    /// 点胶步骤详情模型——DISPENSE 步骤的完整配置，包含模式、校准、默认参数及分段引用
    /// </summary>
    public class DispenseDetail : BindableBase
    {
        #region 模式与校准

        private DispenseStepMode _dispenseMode = DispenseStepMode.Dot;
        /// <summary>点胶模式：Dot=单点, Arc=弧线</summary>
        public DispenseStepMode DispenseMode
        {
            get => _dispenseMode;
            set => SetProperty(ref _dispenseMode, value);
        }

        private bool _enableZCalibration;
        /// <summary>是否启用Z轴校准（默认 false）</summary>
        public bool EnableZCalibration
        {
            get => _enableZCalibration;
            set => SetProperty(ref _enableZCalibration, value);
        }

        private double _zCompensation3D;
        /// <summary>Z向补偿（来自3D相机）mm</summary>
        public double ZCompensation3D
        {
            get => _zCompensation3D;
            set => SetProperty(ref _zCompensation3D, value);
        }

        private string _zCompensation3DLinkedVar;
        /// <summary>Z向补偿（3D相机）链接的全局变量名</summary>
        public string ZCompensation3DLinkedVar
        {
            get => _zCompensation3DLinkedVar;
            set => SetProperty(ref _zCompensation3DLinkedVar, value);
        }

        private double _zCompensationCalibrator;
        /// <summary>Z补偿（来自校准器）mm</summary>
        public double ZCompensationCalibrator
        {
            get => _zCompensationCalibrator;
            set => SetProperty(ref _zCompensationCalibrator, value);
        }

        private string _zCompensationCalibratorLinkedVar;
        /// <summary>Z补偿（校准器）链接的全局变量名</summary>
        public string ZCompensationCalibratorLinkedVar
        {
            get => _zCompensationCalibratorLinkedVar;
            set => SetProperty(ref _zCompensationCalibratorLinkedVar, value);
        }

        private double _manualZCompensation;
        /// <summary>手动Z补偿（人工输入）mm</summary>
        public double ManualZCompensation
        {
            get => _manualZCompensation;
            set => SetProperty(ref _manualZCompensation, value);
        }

        #endregion

        #region 分段引用集合

        /// <summary>分段引用集合，每个引用指向一个源 DispenseSegment</summary>
        public ObservableCollection<DispenseSegmentRef> SegmentRefs { get; }

        #endregion

        #region 默认工艺参数

        private double _defaultJumpSpeed = 20.0;
        /// <summary>默认空移速度 mm/s</summary>
        public double DefaultJumpSpeed
        {
            get => _defaultJumpSpeed;
            set { SetProperty(ref _defaultJumpSpeed, value); }
        }

        private double _defaultMoveSpeed = 10.0;
        /// <summary>默认运动速度 mm/s</summary>
        public double DefaultMoveSpeed
        {
            get => _defaultMoveSpeed;
            set { SetProperty(ref _defaultMoveSpeed, value); }
        }

        private double _defaultSafeHeight = 5.0;
        /// <summary>默认安全抬升高度 mm</summary>
        public double DefaultSafeHeight
        {
            get => _defaultSafeHeight;
            set { SetProperty(ref _defaultSafeHeight, value); }
        }

        private double _defaultApproachHeight = 3.0;
        /// <summary>默认逼近高度 mm</summary>
        public double DefaultApproachHeight
        {
            get => _defaultApproachHeight;
            set { SetProperty(ref _defaultApproachHeight, value); }
        }

        private double _defaultDispenseAmount = 1.0;
        /// <summary>默认出胶量（相对值）</summary>
        public double DefaultDispenseAmount
        {
            get => _defaultDispenseAmount;
            set { SetProperty(ref _defaultDispenseAmount, value); }
        }

        private double _defaultPreDelay = 0.0;
        /// <summary>默认起点开胶延时 ms</summary>
        public double DefaultPreDelay
        {
            get => _defaultPreDelay;
            set { SetProperty(ref _defaultPreDelay, value); }
        }

        private double _defaultPostDelay = 50.0;
        /// <summary>默认终点关胶延时 ms</summary>
        public double DefaultPostDelay
        {
            get => _defaultPostDelay;
            set { SetProperty(ref _defaultPostDelay, value); }
        }

        private double _defaultDispensingPressure = 0.30;
        /// <summary>默认点胶气压 MPa</summary>
        public double DefaultDispensingPressure
        {
            get => _defaultDispensingPressure;
            set { SetProperty(ref _defaultDispensingPressure, value); }
        }

        private double _defaultSuckBackTime = 100.0;
        /// <summary>默认回吸时间 ms</summary>
        public double DefaultSuckBackTime
        {
            get => _defaultSuckBackTime;
            set { SetProperty(ref _defaultSuckBackTime, value); }
        }

        private double _defaultGlueTriggerOffsetMm = 0.5;
        /// <summary>默认开胶触发距离 mm</summary>
        public double DefaultGlueTriggerOffsetMm
        {
            get => _defaultGlueTriggerOffsetMm;
            set { SetProperty(ref _defaultGlueTriggerOffsetMm, value); }
        }

        private double _defaultCornerDecel = 0.3;
        /// <summary>默认拐角减速系数</summary>
        public double DefaultCornerDecel
        {
            get => _defaultCornerDecel;
            set { SetProperty(ref _defaultCornerDecel, value); }
        }

        private double _defaultTeachHeight = 0.0;
        /// <summary>默认示教高度 mm</summary>
        public double DefaultTeachHeight
        {
            get => _defaultTeachHeight;
            set { SetProperty(ref _defaultTeachHeight, value); }
        }

        private double _defaultHeightCompensation = 0.0;
        /// <summary>默认高度补偿值 mm</summary>
        public double DefaultHeightCompensation
        {
            get => _defaultHeightCompensation;
            set { SetProperty(ref _defaultHeightCompensation, value); }
        }

        #endregion

        #region 执行控制

        private bool _isDryRunMode = true;
        /// <summary>空跑模式（默认 true，不出胶只走轨迹，安全验证用）</summary>
        public bool IsDryRunMode
        {
            get => _isDryRunMode;
            set => SetProperty(ref _isDryRunMode, value);
        }

        private bool _isRealDispenseMode;
        /// <summary>真实点胶模式（与空跑互斥，启用时实际出胶）</summary>
        public bool IsRealDispenseMode
        {
            get => _isRealDispenseMode;
            set
            {
                if (SetProperty(ref _isRealDispenseMode, value))
                    IsDryRunMode = !value;
            }
        }

        #endregion

        public DispenseDetail()
        {
            SegmentRefs = new ObservableCollection<DispenseSegmentRef>();
        }
    }
}
