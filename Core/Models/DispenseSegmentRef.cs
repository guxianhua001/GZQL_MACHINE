using Prism.Mvvm;
using System.Text.Json.Serialization;

namespace Core.Models
{
    /// <summary>
    /// 点胶分段引用模型——轻量引用，指向 DispenseSegment 源，携带覆盖参数
    /// </summary>
    public class DispenseSegmentRef : BindableBase
    {
        #region 来源标识

        private string _sourceSegmentId;
        /// <summary>来源分段ID（如 "LINE_001"、"ARC_003"）</summary>
        public string SourceSegmentId
        {
            get => _sourceSegmentId;
            set => SetProperty(ref _sourceSegmentId, value);
        }

        private CadEntityType _sourceEntityType;
        /// <summary>来源CAD图元类型</summary>
        public CadEntityType SourceEntityType
        {
            get => _sourceEntityType;
            set => SetProperty(ref _sourceEntityType, value);
        }

        #endregion

        #region 开关控制

        private bool _isEnabled = true;
        /// <summary>是否参与点胶（默认 true）</summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        private bool _isSelected;
        /// <summary>用户选择标记（默认 false）</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _useDefaultParams = true;
        /// <summary>是否使用全局默认参数（默认 true，false 时使用 Override 参数）</summary>
        public bool UseDefaultParams
        {
            get => _useDefaultParams;
            set => SetProperty(ref _useDefaultParams, value);
        }

        #endregion

        #region 覆盖工艺参数

        private double _overrideJumpSpeed = 20.0;
        /// <summary>覆盖空移速度 mm/s</summary>
        public double OverrideJumpSpeed
        {
            get => _overrideJumpSpeed;
            set => SetProperty(ref _overrideJumpSpeed, value);
        }

        private double _overrideInterpSpeed = 1.0;
        /// <summary>覆盖连续插补速度 mm/s</summary>
        public double OverrideInterpSpeed
        {
            get => _overrideInterpSpeed;
            set => SetProperty(ref _overrideInterpSpeed, value);
        }

        private double _overrideMoveSpeed = 10.0;
        /// <summary>覆盖运动速度 mm/s</summary>
        public double OverrideMoveSpeed
        {
            get => _overrideMoveSpeed;
            set => SetProperty(ref _overrideMoveSpeed, value);
        }

        private double _overrideSafeHeight = 5.0;
        /// <summary>覆盖安全抬升高度 mm</summary>
        public double OverrideSafeHeight
        {
            get => _overrideSafeHeight;
            set => SetProperty(ref _overrideSafeHeight, value);
        }

        private double _overrideApproachHeight = 3.0;
        /// <summary>覆盖逼近高度 mm</summary>
        public double OverrideApproachHeight
        {
            get => _overrideApproachHeight;
            set => SetProperty(ref _overrideApproachHeight, value);
        }

        private double _overrideDispenseAmount = 1.0;
        /// <summary>覆盖出胶量（相对值，保留兼容）</summary>
        public double OverrideDispenseAmount
        {
            get => _overrideDispenseAmount;
            set => SetProperty(ref _overrideDispenseAmount, value);
        }

        private double _overrideDispenseTime = 180.0;
        /// <summary>覆盖点胶时间 ms</summary>
        public double OverrideDispenseTime
        {
            get => _overrideDispenseTime;
            set => SetProperty(ref _overrideDispenseTime, value);
        }

        private double _overridePreDelay = 0.0;
        /// <summary>覆盖起点开胶延时 ms</summary>
        public double OverridePreDelay
        {
            get => _overridePreDelay;
            set => SetProperty(ref _overridePreDelay, value);
        }

        private double _overridePostDelay = 50.0;
        /// <summary>覆盖终点关胶延时 ms</summary>
        public double OverridePostDelay
        {
            get => _overridePostDelay;
            set => SetProperty(ref _overridePostDelay, value);
        }

        private double _overrideEarlyCloseGlueDelayMs = 100.0;
        /// <summary>覆盖提前关胶延时 ms（连续插补模式）</summary>
        public double OverrideEarlyCloseGlueDelayMs
        {
            get => _overrideEarlyCloseGlueDelayMs;
            set => SetProperty(ref _overrideEarlyCloseGlueDelayMs, Math.Clamp(value, 0.0, 5000.0));
        }

        private double _overrideDispensingPressure = 0.30;
        /// <summary>覆盖点胶气压 MPa</summary>
        public double OverrideDispensingPressure
        {
            get => _overrideDispensingPressure;
            set => SetProperty(ref _overrideDispensingPressure, value);
        }

        private double _overrideSuckBackTime = 100.0;
        /// <summary>覆盖回吸时间 ms</summary>
        public double OverrideSuckBackTime
        {
            get => _overrideSuckBackTime;
            set => SetProperty(ref _overrideSuckBackTime, value);
        }

        private double _overrideGlueTriggerOffsetMm = 0.5;
        /// <summary>覆盖开胶触发距离 mm</summary>
        public double OverrideGlueTriggerOffsetMm
        {
            get => _overrideGlueTriggerOffsetMm;
            set => SetProperty(ref _overrideGlueTriggerOffsetMm, value);
        }

        private double _overrideCornerDecel = 0.3;
        /// <summary>覆盖拐角减速系数</summary>
        public double OverrideCornerDecel
        {
            get => _overrideCornerDecel;
            set => SetProperty(ref _overrideCornerDecel, value);
        }

        private double _overrideTeachHeight = 0.0;
        /// <summary>覆盖示教高度 mm</summary>
        public double OverrideTeachHeight
        {
            get => _overrideTeachHeight;
            set => SetProperty(ref _overrideTeachHeight, value);
        }

        private double _overrideHeightCompensation = 0.0;
        /// <summary>覆盖高度补偿值 mm</summary>
        public double OverrideHeightCompensation
        {
            get => _overrideHeightCompensation;
            set => SetProperty(ref _overrideHeightCompensation, value);
        }

        private double _overrideXyCompensationX;
        /// <summary>覆盖人工 XY 补偿 X（mm）</summary>
        public double OverrideXyCompensationX
        {
            get => _overrideXyCompensationX;
            set => SetProperty(ref _overrideXyCompensationX, value);
        }

        private double _overrideXyCompensationY;
        /// <summary>覆盖人工 XY 补偿 Y（mm）</summary>
        public double OverrideXyCompensationY
        {
            get => _overrideXyCompensationY;
            set => SetProperty(ref _overrideXyCompensationY, value);
        }

        #endregion

        #region 只读显示属性

        private string _sourceLayerName;
        /// <summary>来源图层名称（运行时填充，不序列化）</summary>
        public string SourceLayerName
        {
            get => _sourceLayerName;
            set => SetProperty(ref _sourceLayerName, value);
        }

        private double _sourceLength;
        /// <summary>来源分段长度 mm（运行时计算，不序列化）</summary>
        [JsonIgnore]
        public double SourceLength
        {
            get => _sourceLength;
            set => SetProperty(ref _sourceLength, value);
        }

        private int _sourcePointCount;
        /// <summary>来源分段采样点数（运行时计算，不序列化）</summary>
        [JsonIgnore]
        public int SourcePointCount
        {
            get => _sourcePointCount;
            set => SetProperty(ref _sourcePointCount, value);
        }

        #endregion
    }
}
