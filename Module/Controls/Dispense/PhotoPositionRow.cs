using Core.Models;
using MotionControl.Interfaces;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Module.ViewModels
{
    public enum DispenseType
    {
        Dot,
        Arc
    }

    public enum RunMode
    {
        DryRun,
        Dispense
    }

    /// <summary>
    /// 轨迹类型：由用户在 Override 中显式指定，不再自动检测。
    /// Auto 仅保留兼容旧配置，运行时按 Dot 处理。
    /// </summary>
    public enum TrajectoryType
    {
        /// <summary>旧兼容：不再用于自动跟随相机，运行时等价 Dot</summary>
        Auto,
        /// <summary>画X 单点 → DotDispenseService</summary>
        Dot,
        /// <summary>直线 ROI → 连续插补</summary>
        Line,
        /// <summary>弧形 ROI → 连续插补（相机已采样）</summary>
        Arc,
        /// <summary>折线 ROI → 连续插补</summary>
        Polyline
    }

    /// <summary>
    /// Arc(连续点胶)模式下的轨迹子类型：弧线(贝塞尔)或直线(P1→P3)。已由 TrajectoryType 替代，保留兼容旧配置。
    /// </summary>
    [Obsolete("使用 TrajectoryType / TrajectoryOverride 替代")]
    public enum ArcTrackType
    {
        /// <summary>弧线：贝塞尔二次曲线插补</summary>
        Arc,
        /// <summary>直线：P1→P3 等距采样直线插补</summary>
        Line
    }

    /// <summary>
    /// 相机返回的目标点显示项：PointX/Y 为相机坐标，MechX/Y 为叠加偏移后的针头坐标
    /// </summary>
    public class TargetPointItem : BindableBase
    {
        private int _index;
        public int Index
        {
            get => _index;
            set => SetProperty(ref _index, value);
        }

        private double _pointX;
        /// <summary>相机返回的目标点机械坐标 X（9点仿射后）</summary>
        public double PointX
        {
            get => _pointX;
            set => SetProperty(ref _pointX, value);
        }

        private double _pointY;
        /// <summary>相机返回的目标点机械坐标 Y（9点仿射后）</summary>
        public double PointY
        {
            get => _pointY;
            set => SetProperty(ref _pointY, value);
        }

        private double _mechX;
        /// <summary>叠加固定间距+校针偏差+手动补偿后的针头坐标 X</summary>
        public double MechX
        {
            get => _mechX;
            set => SetProperty(ref _mechX, value);
        }

        private double _mechY;
        /// <summary>叠加固定间距+校针偏差+手动补偿后的针头坐标 Y</summary>
        public double MechY
        {
            get => _mechY;
            set => SetProperty(ref _mechY, value);
        }
    }

    /// <summary>
    /// 工作流步骤枚举，用于标识当前操作所处的阶段
    /// </summary>
    public enum WorkflowStep
    {
        Step1_ConfigCapture = 1,
        Step2_PreviewDispense = 2
    }

    /// <summary>
    /// 步骤状态枚举，用于标识每个步骤的视觉状态
    /// </summary>
    public enum StepState
    {
        Pending,
        Active,
        Done
    }

    public class VisionCaptureResult : BindableBase
    {
        private string _rawResponse;
        public string RawResponse
        {
            get => _rawResponse;
            set => SetProperty(ref _rawResponse, value);
        }

        private ObservableCollection<KeyValuePair<string, double>> _parsedData = new ObservableCollection<KeyValuePair<string, double>>();
        public ObservableCollection<KeyValuePair<string, double>> ParsedData
        {
            get => _parsedData;
            set => SetProperty(ref _parsedData, value);
        }

        private ObservableCollection<MachinePointItem> _machinePoints = new ObservableCollection<MachinePointItem>();
        public ObservableCollection<MachinePointItem> MachinePoints
        {
            get => _machinePoints;
            set => SetProperty(ref _machinePoints, value);
        }
    }

    public class MachinePointItem : BindableBase
    {
        private int _index;
        public int Index
        {
            get => _index;
            set => SetProperty(ref _index, value);
        }

        private double _x;
        public double X
        {
            get => _x;
            set => SetProperty(ref _x, value);
        }

        private double _y;
        public double Y
        {
            get => _y;
            set => SetProperty(ref _y, value);
        }
    }

    /// <summary>
    /// Motion Params 区单轴显示项：实时位置 + 安全位置示教值
    /// </summary>
    public class AxisPositionDisplayItem : BindableBase
    {
        private string _axisName;
        /// <summary>轴名称（如 Dx/Dy/Dz₁）</summary>
        public string AxisName
        {
            get => _axisName;
            set => SetProperty(ref _axisName, value);
        }

        private int _axisId = -1;
        /// <summary>逻辑轴号，用于从 MotionService 缓存读取实时位置</summary>
        public int AxisId
        {
            get => _axisId;
            set => SetProperty(ref _axisId, value);
        }

        private double _realtimePosition;
        /// <summary>轴实时位置（mm）</summary>
        public double RealtimePosition
        {
            get => _realtimePosition;
            set => SetProperty(ref _realtimePosition, value);
        }

        private double _safePosition;
        /// <summary>安全位置示教值（mm，来自位置编辑器 SafePosition）</summary>
        public double SafePosition
        {
            get => _safePosition;
            set => SetProperty(ref _safePosition, value);
        }

        private bool _hasSafePosition;
        /// <summary>是否已从位置编辑器解析到安全位置</summary>
        public bool HasSafePosition
        {
            get => _hasSafePosition;
            set => SetProperty(ref _hasSafePosition, value);
        }
    }

    public class PhotoPositionRow : BindableBase
    {
        private string _positionName;
        /// <summary>
        /// 拍照位名称（可编辑，手动输入，对应位置编辑器中的位置名）。
        /// Dx/Dy/Dz₁/Y/Rx/Rz 坐标由 ViewModel 按 PositionName 从位置编辑器解析后填充。
        /// </summary>
        public string PositionName
        {
            get => _positionName;
            set => SetProperty(ref _positionName, value);
        }

        /// <summary>
        /// 旧版只读名称访问器（向后兼容 XAML/ViewModel 旧引用）。
        /// 新逻辑请使用 <see cref="PositionName"/>。
        /// </summary>
        public string SiteFeatureName => PositionName;

        private bool _isPositionInvalid;
        /// <summary>
        /// 位置名在位置编辑器中不存在（无任何轴坐标可解析）时为 true，UI 显示红色警告图标。
        /// 由 ViewModel 在 RefreshRowParsedCoordinates 中根据解析结果设置。
        /// 运动命令入口会检查此标志，为 true 时阻止运动以防碰撞。
        /// </summary>
        public bool IsPositionInvalid
        {
            get => _isPositionInvalid;
            set => SetProperty(ref _isPositionInvalid, value);
        }

        private ObservableCollection<string> _availablePositions = new ObservableCollection<string>();
        public ObservableCollection<string> AvailablePositions
        {
            get => _availablePositions;
            set => SetProperty(ref _availablePositions, value);
        }

        #region 只读解析坐标（从位置编辑器按 PositionName 解析，由 ViewModel 调用 UpdateParsedCoordinates 填充）

        private double _dx;
        /// <summary>Dx 轴坐标（只读解析）</summary>
        public double Dx
        {
            get => _dx;
            private set => SetProperty(ref _dx, value);
        }

        private double _dy;
        /// <summary>Dy 轴坐标（只读解析）</summary>
        public double Dy
        {
            get => _dy;
            private set => SetProperty(ref _dy, value);
        }

        private double _dz1;
        /// <summary>Dz₁ 轴坐标（只读解析）</summary>
        public double Dz1
        {
            get => _dz1;
            private set => SetProperty(ref _dz1, value);
        }

        private double _y;
        /// <summary>Y 轴坐标（只读解析）</summary>
        public double Y
        {
            get => _y;
            private set => SetProperty(ref _y, value);
        }

        private double _rx;
        /// <summary>Rx 轴坐标（只读解析）</summary>
        public double Rx
        {
            get => _rx;
            private set => SetProperty(ref _rx, value);
        }

        private double _rz;
        /// <summary>Rz 轴坐标（只读解析）</summary>
        public double Rz
        {
            get => _rz;
            private set => SetProperty(ref _rz, value);
        }

        /// <summary>
        /// 由 ViewModel 调用：按当前 PositionName 从位置编辑器解析的坐标值批量更新。
        /// 解析失败的字段保持原值（不置 0，避免误用导致碰撞）。
        /// </summary>
        public void UpdateParsedCoordinates(double? dx, double? dy, double? dz1, double? y, double? rx, double? rz)
        {
            if (dx.HasValue) Dx = dx.Value;
            if (dy.HasValue) Dy = dy.Value;
            if (dz1.HasValue) Dz1 = dz1.Value;
            if (y.HasValue) Y = y.Value;
            if (rx.HasValue) Rx = rx.Value;
            if (rz.HasValue) Rz = rz.Value;
        }

        #endregion

        #region 点胶工艺参数子对象（双针头，每套参数独立）

        private DotProcessParams _dotParamsNeedle1 = new DotProcessParams();
        /// <summary>针头1(Dz₂) Dot 模式工艺参数</summary>
        public DotProcessParams DotParamsNeedle1
        {
            get => _dotParamsNeedle1;
            set => SetProperty(ref _dotParamsNeedle1, value);
        }

        private DotProcessParams _dotParamsNeedle2 = new DotProcessParams();
        /// <summary>针头2(Dz₃) Dot 模式工艺参数</summary>
        public DotProcessParams DotParamsNeedle2
        {
            get => _dotParamsNeedle2;
            set => SetProperty(ref _dotParamsNeedle2, value);
        }

        private DispenseSegment _arcParamsNeedle1 = new DispenseSegment();
        /// <summary>针头1(Dz₂) 路径(连续插补)模式工艺参数</summary>
        public DispenseSegment ArcParamsNeedle1
        {
            get => _arcParamsNeedle1;
            set => SetProperty(ref _arcParamsNeedle1, value);
        }

        private DispenseSegment _arcParamsNeedle2 = new DispenseSegment();
        /// <summary>针头2(Dz₃) 路径(连续插补)模式工艺参数</summary>
        public DispenseSegment ArcParamsNeedle2
        {
            get => _arcParamsNeedle2;
            set => SetProperty(ref _arcParamsNeedle2, value);
        }

        /// <summary>Dot 模式工艺参数（旧兼容，等价于 DotParamsNeedle1）</summary>
        [Obsolete("使用 DotParamsNeedle1/DotParamsNeedle2 替代")]
        public DotProcessParams DotParams
        {
            get => _dotParamsNeedle1;
            set => SetProperty(ref _dotParamsNeedle1, value);
        }

        /// <summary>Arc 模式工艺参数（旧兼容，等价于 ArcParamsNeedle1）</summary>
        [Obsolete("使用 ArcParamsNeedle1/ArcParamsNeedle2 替代")]
        public DispenseSegment ArcParams
        {
            get => _arcParamsNeedle1;
            set => SetProperty(ref _arcParamsNeedle1, value);
        }

        private ArcTrackType _arcTrackType = ArcTrackType.Arc;
        /// <summary>旧 Arc 轨迹子类型，保留兼容；新逻辑使用 TrajectoryOverride</summary>
        [Obsolete("使用 TrajectoryOverride 替代")]
        public ArcTrackType ArcTrackType
        {
            get => _arcTrackType;
            set => SetProperty(ref _arcTrackType, value);
        }

        private TrajectoryType _trajectoryOverride = TrajectoryType.Dot;
        /// <summary>
        /// 轨迹类型（用户显式指定）：Dot/Line/Arc/Polyline。不再自动检测。
        /// Auto 仅兼容旧配置，运行时按 Dot 处理。
        /// </summary>
        public TrajectoryType TrajectoryOverride
        {
            get => _trajectoryOverride;
            set
            {
                if (SetProperty(ref _trajectoryOverride, value))
                {
                    // 同步旧字段，便于旧 UI/配置过渡
                    if (value == TrajectoryType.Dot || value == TrajectoryType.Auto)
                        DispenseType = DispenseType.Dot;
                    else
                        DispenseType = DispenseType.Arc;
                }
            }
        }

        #endregion

        private double _speed = 10.0;
        public double Speed
        {
            get => _speed;
            set => SetProperty(ref _speed, value);
        }

        private string _triggerCommand = "TRIGGER";
        public string TriggerCommand
        {
            get => _triggerCommand;
            set => SetProperty(ref _triggerCommand, value);
        }

        private string _connectionName;
        public string ConnectionName
        {
            get => _connectionName;
            set => SetProperty(ref _connectionName, value);
        }

        private int _timeout = 5000;
        public int Timeout
        {
            get => _timeout;
            set => SetProperty(ref _timeout, value);
        }

        private DispenseType _dispenseType = DispenseType.Dot;
        /// <summary>旧点胶类型，保留兼容；新逻辑使用 TrajectoryOverride</summary>
        [Obsolete("使用 TrajectoryOverride 替代")]
        public DispenseType DispenseType
        {
            get => _dispenseType;
            set => SetProperty(ref _dispenseType, value);
        }

        private int _arcSegments = 20;
        public int ArcSegments
        {
            get => _arcSegments;
            set => SetProperty(ref _arcSegments, value);
        }

        private double _arcHeight;
        /// <summary>
        /// Arc弧高(mm)。0表示按旧项目默认使用P1/P3弦长20%自动计算。
        /// </summary>
        public double ArcHeight
        {
            get => _arcHeight;
            set => SetProperty(ref _arcHeight, value);
        }

        private double _arcDirection;
        /// <summary>
        /// Arc弧线方向；0表示使用视觉P2所在方向，1/-1强制指定弧线侧向。
        /// </summary>
        public double ArcDirection
        {
            get => _arcDirection;
            set => SetProperty(ref _arcDirection, value);
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isExecuting;
        public bool IsExecuting
        {
            get => _isExecuting;
            set => SetProperty(ref _isExecuting, value);
        }

        private bool _returnToSafeAfterCapture = true;
        /// <summary>
        /// 拍照完成后是否返回安全位
        /// </summary>
        public bool ReturnToSafeAfterCapture
        {
            get => _returnToSafeAfterCapture;
            set => SetProperty(ref _returnToSafeAfterCapture, value);
        }

        private double _needleOffsetX;
        /// <summary>
        /// 针头X偏移基础值
        /// </summary>
        public double NeedleOffsetX
        {
            get => _needleOffsetX;
            set
            {
                if (SetProperty(ref _needleOffsetX, value))
                    RaisePropertyChanged(nameof(CalculatedOffsetX));
            }
        }

        private double _needleOffsetY;
        /// <summary>
        /// 针头Y偏移基础值
        /// </summary>
        public double NeedleOffsetY
        {
            get => _needleOffsetY;
            set
            {
                if (SetProperty(ref _needleOffsetY, value))
                    RaisePropertyChanged(nameof(CalculatedOffsetY));
            }
        }

        private string _offsetXExpression;
        /// <summary>
        /// OffsetX计算表达式，如 "0.1+0.2+0.3"，最终值 = NeedleOffsetX + 表达式结果
        /// </summary>
        public string OffsetXExpression
        {
            get => _offsetXExpression;
            set
            {
                if (SetProperty(ref _offsetXExpression, value))
                    RaisePropertyChanged(nameof(CalculatedOffsetX));
            }
        }

        private string _offsetYExpression;
        /// <summary>
        /// OffsetY计算表达式
        /// </summary>
        public string OffsetYExpression
        {
            get => _offsetYExpression;
            set
            {
                if (SetProperty(ref _offsetYExpression, value))
                    RaisePropertyChanged(nameof(CalculatedOffsetY));
            }
        }

        /// <summary>
        /// 计算后的OffsetX = NeedleOffsetX + 表达式结果
        /// </summary>
        public double CalculatedOffsetX => NeedleOffsetX + EvaluateExpression(OffsetXExpression);

        /// <summary>
        /// 计算后的OffsetY = NeedleOffsetY + 表达式结果
        /// </summary>
        public double CalculatedOffsetY => NeedleOffsetY + EvaluateExpression(OffsetYExpression);

        private double _needleCompensationX;
        /// <summary>
        /// 针头X补偿基础值
        /// </summary>
        public double NeedleCompensationX
        {
            get => _needleCompensationX;
            set
            {
                if (SetProperty(ref _needleCompensationX, value))
                    RaisePropertyChanged(nameof(CalculatedCompensationX));
            }
        }

        private double _needleCompensationY;
        /// <summary>
        /// 针头Y补偿基础值
        /// </summary>
        public double NeedleCompensationY
        {
            get => _needleCompensationY;
            set
            {
                if (SetProperty(ref _needleCompensationY, value))
                    RaisePropertyChanged(nameof(CalculatedCompensationY));
            }
        }

        private string _compensationXExpression;
        /// <summary>
        /// CompensationX计算表达式，最终值 = NeedleCompensationX + 表达式结果
        /// </summary>
        public string CompensationXExpression
        {
            get => _compensationXExpression;
            set
            {
                if (SetProperty(ref _compensationXExpression, value))
                    RaisePropertyChanged(nameof(CalculatedCompensationX));
            }
        }

        private string _compensationYExpression;
        /// <summary>
        /// CompensationY计算表达式
        /// </summary>
        public string CompensationYExpression
        {
            get => _compensationYExpression;
            set
            {
                if (SetProperty(ref _compensationYExpression, value))
                    RaisePropertyChanged(nameof(CalculatedCompensationY));
            }
        }

        /// <summary>
        /// 计算后的CompensationX = NeedleCompensationX + 表达式结果
        /// </summary>
        public double CalculatedCompensationX => NeedleCompensationX + EvaluateExpression(CompensationXExpression);

        /// <summary>
        /// 计算后的CompensationY = NeedleCompensationY + 表达式结果
        /// </summary>
        public double CalculatedCompensationY => NeedleCompensationY + EvaluateExpression(CompensationYExpression);

        /// <summary>
        /// 安全计算数学表达式，如 "0.1+0.2+0.3"，失败返回0
        /// </summary>
        private static double EvaluateExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return 0;
            try
            {
                var result = new DataTable().Compute(expression, null);
                return Convert.ToDouble(result);
            }
            catch
            {
                return 0;
            }
        }

        public PhotoPositionRow(string positionName)
        {
            PositionName = positionName;
        }

        /// <summary>
        /// 外部通知计算属性已变更（全局变量值变化时由ViewModel调用）
        /// </summary>
        public void NotifyCalculatedPropertiesChanged()
        {
            RaisePropertyChanged(nameof(CalculatedOffsetX));
            RaisePropertyChanged(nameof(CalculatedOffsetY));
            RaisePropertyChanged(nameof(CalculatedCompensationX));
            RaisePropertyChanged(nameof(CalculatedCompensationY));
        }

        public async Task LoadPositionsAsync(IPositionProvider positionProvider, string stationId)
        {
            if (string.IsNullOrEmpty(stationId)) return;
            try
            {
                var positions = await positionProvider.GetPositionsAsync(stationId);
                var positionNames = new HashSet<string>();
                foreach (var key in positions.Keys)
                {
                    var parts = key.Split('.');
                    if (parts.Length >= 2)
                    {
                        positionNames.Add(parts[0]);
                    }
                }
                AvailablePositions = new ObservableCollection<string>(positionNames.OrderBy(p => p));
            }
            catch
            {
                AvailablePositions = new ObservableCollection<string>();
            }
        }
    }
}
