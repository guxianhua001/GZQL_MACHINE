using Newtonsoft.Json;
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

        private int _needleIndex;
        /// <summary>
        /// 点胶针头索引（0=针头1/Dz₂轴, 1=针头2/Dz₃轴）
        /// Dz₁轴为相机/3D扫描轴，不作为点胶轴使用
        /// </summary>
        public int NeedleIndex
        {
            get => _needleIndex;
            set => SetProperty(ref _needleIndex, value switch
            {
                0 => 0,
                1 => 1,
                _ => 0 // 非法值回退到针头1
            });
        }

        /// <summary>是否选中针头1（Dz₂轴）</summary>
        public bool IsNeedle1Selected
        {
            get => _needleIndex == 0;
            set { if (value) NeedleIndex = 0; }
        }

        /// <summary>是否选中针头2（Dz₃轴）</summary>
        public bool IsNeedle2Selected
        {
            get => _needleIndex == 1;
            set { if (value) NeedleIndex = 1; }
        }

        private bool _enableZCalibration;
        /// <summary>是否启用校准（X/Y/Z Comp 校准器 + Z Comp 3D Camera，默认 false）</summary>
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
        /// <summary>Z Comp（校准器）链接的全局变量名</summary>
        public string ZCompensationCalibratorLinkedVar
        {
            get => _zCompensationCalibratorLinkedVar;
            set => SetProperty(ref _zCompensationCalibratorLinkedVar, value);
        }

        private double _xCompensationCalibrator;
        /// <summary>X Comp（校准器）mm（可链接全局变量）</summary>
        public double XCompensationCalibrator
        {
            get => _xCompensationCalibrator;
            set => SetProperty(ref _xCompensationCalibrator, value);
        }

        private string _xCompensationCalibratorLinkedVar;
        /// <summary>X Comp（校准器）链接的全局变量名</summary>
        public string XCompensationCalibratorLinkedVar
        {
            get => _xCompensationCalibratorLinkedVar;
            set => SetProperty(ref _xCompensationCalibratorLinkedVar, value);
        }

        private double _yCompensationCalibrator;
        /// <summary>Y Comp（校准器）mm（可链接全局变量）</summary>
        public double YCompensationCalibrator
        {
            get => _yCompensationCalibrator;
            set => SetProperty(ref _yCompensationCalibrator, value);
        }

        private string _yCompensationCalibratorLinkedVar;
        /// <summary>Y Comp（校准器）链接的全局变量名</summary>
        public string YCompensationCalibratorLinkedVar
        {
            get => _yCompensationCalibratorLinkedVar;
            set => SetProperty(ref _yCompensationCalibratorLinkedVar, value);
        }

        private bool _enableComp;
        /// <summary>是否启用 XY 方向补偿（默认 false）</summary>
        public bool EnableComp
        {
            get => _enableComp;
            set => SetProperty(ref _enableComp, value);
        }

        private double _xCompensation;
        /// <summary>X 方向补偿 mm（可链接全局变量）</summary>
        public double XCompensation
        {
            get => _xCompensation;
            set => SetProperty(ref _xCompensation, value);
        }

        private string _xCompensationLinkedVar;
        /// <summary>X 方向补偿链接的全局变量名</summary>
        public string XCompensationLinkedVar
        {
            get => _xCompensationLinkedVar;
            set => SetProperty(ref _xCompensationLinkedVar, value);
        }

        private double _yCompensation;
        /// <summary>Y 方向补偿 mm（可链接全局变量，常用于 CAD 标定偏差）</summary>
        public double YCompensation
        {
            get => _yCompensation;
            set => SetProperty(ref _yCompensation, value);
        }

        private string _yCompensationLinkedVar;
        /// <summary>Y 方向补偿链接的全局变量名</summary>
        public string YCompensationLinkedVar
        {
            get => _yCompensationLinkedVar;
            set => SetProperty(ref _yCompensationLinkedVar, value);
        }

        private bool _enableRotationComp;
        /// <summary>是否启用旋转补偿（产品旋转后按 Coord Transform 换算新坐标进行点胶）</summary>
        public bool EnableRotationComp
        {
            get => _enableRotationComp;
            set => SetProperty(ref _enableRotationComp, value);
        }

        private double _rotationAngle;
        /// <summary>产品旋转角度（度数，可链接全局变量获取实际旋转角度）</summary>
        public double RotationAngle
        {
            get => _rotationAngle;
            set => SetProperty(ref _rotationAngle, value);
        }

        private string _rotationAngleLinkedVar;
        /// <summary>产品旋转角度链接的全局变量名（运行时从全局变量读取实际旋转角度）</summary>
        public string RotationAngleLinkedVar
        {
            get => _rotationAngleLinkedVar;
            set => SetProperty(ref _rotationAngleLinkedVar, value);
        }

        private string _angleCompensationLinkedVar;
        /// <summary>角度补偿链接的全局变量名（运行时从全局变量读取补偿角度；未链接时按 0 处理，与 RotationAngle 相加后参与坐标变换）</summary>
        public string AngleCompensationLinkedVar
        {
            get => _angleCompensationLinkedVar;
            set => SetProperty(ref _angleCompensationLinkedVar, value);
        }

        #endregion

        #region 针头偏移补偿（旋转后坐标→实际点胶针头坐标）

        // 旋转后坐标(RotatedX/Y)得到的是相机中心坐标，实际点胶针头与相机中心存在固定间距。
        // 最终点胶坐标 = RotatedX + 相机与针头固定距离X + 对针补偿X（Y 同理）。
        // X/Y Comp(校准器)与 X/Y Compensation 在 DISPENSE 详情页配置，由 Enable Calibration / Enable Comp 控制。

        private bool _enableNeedleOffsetComp;
        /// <summary>是否启用针头偏移补偿（将相机中心坐标换算为实际针头点胶坐标）</summary>
        public bool EnableNeedleOffsetComp
        {
            get => _enableNeedleOffsetComp;
            set => SetProperty(ref _enableNeedleOffsetComp, value);
        }

        private double _cameraNeedleOffsetX;
        /// <summary>相机与针头固定距离 X（mm，可链接全局变量或针头标定偏移量）</summary>
        public double CameraNeedleOffsetX
        {
            get => _cameraNeedleOffsetX;
            set => SetProperty(ref _cameraNeedleOffsetX, value);
        }

        private double _cameraNeedleOffsetY;
        /// <summary>相机与针头固定距离 Y（mm，可链接全局变量或针头标定偏移量）</summary>
        public double CameraNeedleOffsetY
        {
            get => _cameraNeedleOffsetY;
            set => SetProperty(ref _cameraNeedleOffsetY, value);
        }

        private string _cameraNeedleOffsetXLinkedVar;
        /// <summary>相机与针头固定距离 X 链接的全局变量名</summary>
        public string CameraNeedleOffsetXLinkedVar
        {
            get => _cameraNeedleOffsetXLinkedVar;
            set => SetProperty(ref _cameraNeedleOffsetXLinkedVar, value);
        }

        private string _cameraNeedleOffsetYLinkedVar;
        /// <summary>相机与针头固定距离 Y 链接的全局变量名</summary>
        public string CameraNeedleOffsetYLinkedVar
        {
            get => _cameraNeedleOffsetYLinkedVar;
            set => SetProperty(ref _cameraNeedleOffsetYLinkedVar, value);
        }

        private bool _linkCameraNeedleOffsetToCalibration;
        /// <summary>
        /// 是否链接相机与针头固定距离（勾选时取全局变量值，不勾选则为 0）。
        /// </summary>
        public bool LinkCameraNeedleOffsetToCalibration
        {
            get => _linkCameraNeedleOffsetToCalibration;
            set => SetProperty(ref _linkCameraNeedleOffsetToCalibration, value);
        }

        private double _needleAlignCompX;
        /// <summary>对针补偿 X（mm，可链接全局变量，默认对应对针校准结果）</summary>
        public double NeedleAlignCompX
        {
            get => _needleAlignCompX;
            set => SetProperty(ref _needleAlignCompX, value);
        }

        private double _needleAlignCompY;
        /// <summary>对针补偿 Y（mm，可链接全局变量，默认对应对针校准结果）</summary>
        public double NeedleAlignCompY
        {
            get => _needleAlignCompY;
            set => SetProperty(ref _needleAlignCompY, value);
        }

        private string _needleAlignCompXLinkedVar;
        /// <summary>对针补偿 X 链接的全局变量名（默认 NeedleAligner_CompX_LinkedVar）</summary>
        public string NeedleAlignCompXLinkedVar
        {
            get => _needleAlignCompXLinkedVar;
            set => SetProperty(ref _needleAlignCompXLinkedVar, value);
        }

        private string _needleAlignCompYLinkedVar;
        /// <summary>对针补偿 Y 链接的全局变量名（默认 NeedleAligner_CompY_LinkedVar）</summary>
        public string NeedleAlignCompYLinkedVar
        {
            get => _needleAlignCompYLinkedVar;
            set => SetProperty(ref _needleAlignCompYLinkedVar, value);
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

        private double _defaultInterpSpeed = 1.0;
        /// <summary>默认连续插补速度 mm/s</summary>
        public double DefaultInterpSpeed
        {
            get => _defaultInterpSpeed;
            set { SetProperty(ref _defaultInterpSpeed, value); }
        }

        private double _defaultMoveSpeed = 20.0;
        /// <summary>默认运动速度 mm/s</summary>
        public double DefaultMoveSpeed
        {
            get => _defaultMoveSpeed;
            set { SetProperty(ref _defaultMoveSpeed, value); }
        }

        /// <summary>安全高度未设置时的安全兜底默认值 mm，与出厂默认值保持一致</summary>
        public const double DefaultSafeHeightFallback = -20.0;

        private double _defaultSafeHeight = -20.0;
        /// <summary>默认安全抬升高度 mm</summary>
        public double DefaultSafeHeight
        {
            get => _defaultSafeHeight;
            set { SetProperty(ref _defaultSafeHeight, value); }
        }

        /// <summary>
        /// 安全兜底：若从未配置过默认安全高度（历史数据遗留为 0），0 通常意味着针头处于工件表面附近，
        /// 直接用于抬升存在撞针风险，运动执行时统一改用该计算值而不是原始 DefaultSafeHeight。
        /// </summary>
        [JsonIgnore]
        public double EffectiveDefaultSafeHeight => DefaultSafeHeight == 0 ? DefaultSafeHeightFallback : DefaultSafeHeight;

        private double _defaultApproachHeight = -3.0;
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
        /// <summary>默认终点关胶延时 ms（连续插补结束后泄压等待）</summary>
        public double DefaultPostDelay
        {
            get => _defaultPostDelay;
            set { SetProperty(ref _defaultPostDelay, value); }
        }

        private double _defaultEarlyCloseGlueDelayMs = 100.0;
        /// <summary>默认提前关胶延时 ms（连续插补模式：轨迹结束前关阀，补偿胶阀滞后）</summary>
        public double DefaultEarlyCloseGlueDelayMs
        {
            get => _defaultEarlyCloseGlueDelayMs;
            set { SetProperty(ref _defaultEarlyCloseGlueDelayMs, Math.Clamp(value, 0.0, 5000.0)); }
        }

        private double _defaultDispensingPressure = -0.30;
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

        private double _defaultGlueTriggerOffsetMm = -0.5;
        /// <summary>默认开胶触发距离 mm</summary>
        public double DefaultGlueTriggerOffsetMm
        {
            get => _defaultGlueTriggerOffsetMm;
            set { SetProperty(ref _defaultGlueTriggerOffsetMm, value); }
        }

        private double _defaultPreDispenseDelay = 50.0;
        /// <summary>默认预出胶延时 ms（到达起点后等待时间）</summary>
        public double DefaultPreDispenseDelay
        {
            get => _defaultPreDispenseDelay;
            set => SetProperty(ref _defaultPreDispenseDelay, value);
        }

        private double _defaultDispenseTime = 1800.0;
        /// <summary>默认点胶时间 ms（单点模式出胶持续时间）</summary>
        public double DefaultDispenseTime
        {
            get => _defaultDispenseTime;
            set => SetProperty(ref _defaultDispenseTime, value);
        }

        private double _defaultCornerDecel = 0.1;
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

        private double _defaultXyCompensationX;
        /// <summary>默认人工 XY 补偿 X（mm，来自 Step3 段级 XY Offset）</summary>
        public double DefaultXyCompensationX
        {
            get => _defaultXyCompensationX;
            set => SetProperty(ref _defaultXyCompensationX, value);
        }

        private double _defaultXyCompensationY;
        /// <summary>默认人工 XY 补偿 Y（mm，来自 Step3 段级 XY Offset）</summary>
        public double DefaultXyCompensationY
        {
            get => _defaultXyCompensationY;
            set => SetProperty(ref _defaultXyCompensationY, value);
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

        /// <summary>运行时检查点（暂停/恢复用，不写入工艺 JSON）</summary>
        [JsonIgnore]
        public DispenseExecutionCheckpoint ExecutionCheckpoint { get; set; }

        /// <summary>清除运行时检查点（新一次 Run 或 Stop 后调用）</summary>
        public void ClearExecutionCheckpoint()
        {
            ExecutionCheckpoint = null;
        }

        public DispenseDetail()
        {
            SegmentRefs = new ObservableCollection<DispenseSegmentRef>();
        }
    }
}
