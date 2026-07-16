using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Newtonsoft.Json;
using Prism.Mvvm;
using Prism.Commands;
using Prism.Events;
using Prism.Services.Dialogs;
using Recipe.Interfaces;
using Recipe.Events;
using MotionControl.Interfaces;
using MotionControl.Models;
using MotionControl.Exceptions;
using TCPIPModule.Interfaces;
using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using Core.Events;
using StationTasks.Services;
using MotionControl.Services;
using Module.Models;
using Module.Services;
using Module.ViewModels;

namespace Module.ViewModels
{
    public class VisionCaptureViewModel : BindableBase
    {
        private readonly IRecipePoolService _recipePoolService;
        private readonly IPositionProvider _positionProvider;
        private readonly ITCPEventService _tcpEventService;
        private readonly ITCPClientManagerService _tcpClientManager;
        private readonly IMotionService _motionService;
        private readonly IStationRegistry _stationRegistry;
        private readonly VisionCaptureService _visionCaptureService;
        private readonly BezierArcDispenseService _bezierArcDispenseService;
        private readonly IDotDispenseService _dotDispenseService;
        private readonly IDispenseExecuteService _dispenseExecuteService;
        private readonly IAxisParameterService _axisParameterService;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localizationService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDialogService _dialogService;
        private readonly IConfigFileRetentionService _configRetentionService;

        private Dictionary<string, double> _allPositions = new Dictionary<string, double>();
        private CancellationTokenSource _dispenseCts;
        /// <summary>拍照位行运动（移动/拍照/回安全）取消令牌</summary>
        private CancellationTokenSource _rowMotionCts;
        private readonly ManualResetEventSlim _pauseEvent = new ManualResetEventSlim(true);

        /// <summary>各组拍照位行缓存（组名 → 行列表），切换组时保存/恢复，不依赖 WorkOrder</summary>
        private readonly Dictionary<string, List<PhotoPositionRow>> _groupRowsCache = new Dictionary<string, List<PhotoPositionRow>>();

        /// <summary>切换组时抑制 SelectedGroup setter 触发的自动加载，避免与手动切换逻辑冲突</summary>
        private bool _suppressGroupChangeReload;

        private ObservableCollection<string> _groups = new ObservableCollection<string>();
        public ObservableCollection<string> Groups
        {
            get => _groups;
            set => SetProperty(ref _groups, value);
        }

        private string _selectedGroup;
        private Task _reloadRowsTask = Task.CompletedTask;
        public string SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                var oldGroup = _selectedGroup;
                if (SetProperty(ref _selectedGroup, value))
                {
                    if (!_suppressGroupChangeReload)
                        _reloadRowsTask = OnSelectedGroupChanged(oldGroup);
                    if (!string.IsNullOrEmpty(value))
                        CurrentStep = WorkflowStep.Step1_ConfigCapture;
                    RaisePropertyChanged(nameof(GroupDisplay));
                    DeleteGroupCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        private ObservableCollection<PhotoPositionRow> _photoPositionRows = new ObservableCollection<PhotoPositionRow>();
        public ObservableCollection<PhotoPositionRow> PhotoPositionRows
        {
            get => _photoPositionRows;
            set => SetProperty(ref _photoPositionRows, value);
        }

        private PhotoPositionRow _selectedRow;
        public PhotoPositionRow SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (_selectedRow != null)
                    _selectedRow.PropertyChanged -= OnSelectedRowPropertyChanged;

                if (!SetProperty(ref _selectedRow, value))
                {
                    NotifyTargetOffsetChanged();
                    return;
                }

                RaisePropertyChanged(nameof(HasSelectedRow));
                RaisePropertyChanged(nameof(CurrentDotParams));
                RaisePropertyChanged(nameof(CurrentArcParams));

                if (value != null)
                {
                    value.PropertyChanged += OnSelectedRowPropertyChanged;
                    OffsetXExpressionLinkedVar = value.OffsetXExpression;
                    OffsetYExpressionLinkedVar = value.OffsetYExpression;
                    // ViewModel 级 NeedleOffset 仅反映全局变量链接，不与 Row 的 CalculatedOffset 混用
                    if (IsNeedleOffsetXLinked)
                        NeedleOffsetX = ReadLinkedVariableValue(NeedleOffsetXLinkedVar);
                    else
                        NeedleOffsetX = 0;
                    if (IsNeedleOffsetYLinked)
                        NeedleOffsetY = ReadLinkedVariableValue(NeedleOffsetYLinkedVar);
                    else
                        NeedleOffsetY = 0;
                    RaisePropertyChanged(nameof(IsOffsetXExpressionLinked));
                    RaisePropertyChanged(nameof(IsOffsetYExpressionLinked));
                    RaisePropertyChanged(nameof(NeedleOffsetRowXExpression));
                    RaisePropertyChanged(nameof(NeedleOffsetRowYExpression));
                    RaisePropertyChanged(nameof(NeedleOffsetRowX));
                    RaisePropertyChanged(nameof(NeedleOffsetRowY));
                    ApplyTrajectoryTypeSideEffects();
                    RefreshPhotoPosition(value);
                }

                NotifyTargetOffsetChanged();
            }
        }

        /// <summary>
        /// 目标偏移 ΔX，与 Offset Compensation 区域的 CalculatedOffsetX 保持同步
        /// </summary>
        public double TargetOffsetX => SelectedRow?.CalculatedOffsetX ?? 0;

        /// <summary>
        /// 目标偏移 ΔY，与 Offset Compensation 区域的 CalculatedOffsetY 保持同步
        /// </summary>
        public double TargetOffsetY => SelectedRow?.CalculatedOffsetY ?? 0;

        /// <summary>
        /// 当前选中行针头X偏移表达式（UI 双向绑定，避免嵌套路径刷新问题）
        /// </summary>
        public string NeedleOffsetRowXExpression
        {
            get => SelectedRow?.NeedleOffsetXExpression ?? string.Empty;
            set
            {
                if (SelectedRow == null) return;
                if (SelectedRow.NeedleOffsetXExpression == value) return;
                SelectedRow.NeedleOffsetXExpression = value ?? string.Empty;
                NotifyTargetOffsetChanged();
                TryRecalculateMachinePointsIfCaptured();
            }
        }

        /// <summary>
        /// 当前选中行针头Y偏移表达式（UI 双向绑定）
        /// </summary>
        public string NeedleOffsetRowYExpression
        {
            get => SelectedRow?.NeedleOffsetYExpression ?? string.Empty;
            set
            {
                if (SelectedRow == null) return;
                if (SelectedRow.NeedleOffsetYExpression == value) return;
                SelectedRow.NeedleOffsetYExpression = value ?? string.Empty;
                NotifyTargetOffsetChanged();
                TryRecalculateMachinePointsIfCaptured();
            }
        }

        /// <summary>
        /// 当前选中行针头X基础值（行级，参与 CalculatedOffsetX；与全局标定 NeedleOffsetX 分离）
        /// </summary>
        public double NeedleOffsetRowX
        {
            get => SelectedRow?.NeedleOffsetX ?? 0;
            set
            {
                if (SelectedRow == null) return;
                if (Math.Abs(SelectedRow.NeedleOffsetX - value) < 1e-9) return;
                SelectedRow.NeedleOffsetX = value;
                NotifyTargetOffsetChanged();
                TryRecalculateMachinePointsIfCaptured();
            }
        }

        /// <summary>
        /// 当前选中行针头Y基础值（行级，参与 CalculatedOffsetY）
        /// </summary>
        public double NeedleOffsetRowY
        {
            get => SelectedRow?.NeedleOffsetY ?? 0;
            set
            {
                if (SelectedRow == null) return;
                if (Math.Abs(SelectedRow.NeedleOffsetY - value) < 1e-9) return;
                SelectedRow.NeedleOffsetY = value;
                NotifyTargetOffsetChanged();
                TryRecalculateMachinePointsIfCaptured();
            }
        }

        /// <summary>
        /// 选中行偏移属性变化时，刷新目标偏移显示；PositionName 变化时自动解析各轴坐标
        /// </summary>
        private void OnSelectedRowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(PhotoPositionRow.PositionName):
                    // 输入拍照位名称后，从位置编辑器所有工站解析各轴坐标并显示
                    if (sender is PhotoPositionRow namedRow)
                    {
                        RefreshRowParsedCoordinates(namedRow);
                        TryRecalculateMachinePointsIfCaptured();
                    }
                    break;
                case nameof(PhotoPositionRow.CalculatedOffsetX):
                case nameof(PhotoPositionRow.CalculatedOffsetY):
                case nameof(PhotoPositionRow.NeedleOffsetX):
                case nameof(PhotoPositionRow.NeedleOffsetY):
                case nameof(PhotoPositionRow.NeedleOffsetXExpression):
                case nameof(PhotoPositionRow.NeedleOffsetYExpression):
                case nameof(PhotoPositionRow.OffsetXExpression):
                case nameof(PhotoPositionRow.OffsetYExpression):
                    NotifyTargetOffsetChanged();
                    TryRecalculateMachinePointsIfCaptured();
                    break;
                case nameof(PhotoPositionRow.DispenseType):
                case nameof(PhotoPositionRow.TrajectoryOverride):
                    // 轨迹类型变更：Dot 与 Arc/Line/Polyline 切换工艺参数面板
                    ApplyTrajectoryTypeSideEffects();
                    break;
                case nameof(PhotoPositionRow.DotParamsNeedle1):
                case nameof(PhotoPositionRow.DotParamsNeedle2):
                    RaisePropertyChanged(nameof(CurrentDotParams));
                    break;
                case nameof(PhotoPositionRow.ArcParamsNeedle1):
                case nameof(PhotoPositionRow.ArcParamsNeedle2):
                    RaisePropertyChanged(nameof(CurrentArcParams));
                    break;
            }
        }

        /// <summary>
        /// 轨迹类型变更后的统一副作用：同步 Dot/路径面板可见性并刷新当前工艺参数绑定。
        /// </summary>
        private void ApplyTrajectoryTypeSideEffects()
        {
            RaisePropertyChanged(nameof(EffectiveTrajectoryType));
            RaisePropertyChanged(nameof(SelectedTrajectoryType));
            SyncDotArcModeFromEffectiveType();
            RaisePropertyChanged(nameof(CurrentDotParams));
            RaisePropertyChanged(nameof(CurrentArcParams));
        }

        /// <summary>
        /// 按 EffectiveTrajectoryType 同步 IsDotMode/IsArcMode：
        /// Dot→单点工艺参数；Arc/Line/Polyline→路径(连续插补)工艺参数。
        /// </summary>
        private void SyncDotArcModeFromEffectiveType()
        {
            var isDot = EffectiveTrajectoryType == TrajectoryType.Dot;
            var isArc = !isDot;
            // 强制通知 UI（嵌套绑定变更时 IsDotMode/IsArcMode 可能未触发 PropertyChanged）
            _isDotMode = isDot;
            _isArcMode = isArc;
            RaisePropertyChanged(nameof(IsDotMode));
            RaisePropertyChanged(nameof(IsArcMode));
            RaisePropertyChanged(nameof(DispenseTypeTabIndex));
        }

        /// <summary>
        /// 通知目标偏移显示属性已更新
        /// </summary>
        private void NotifyTargetOffsetChanged()
        {
            RaisePropertyChanged(nameof(TargetOffsetX));
            RaisePropertyChanged(nameof(TargetOffsetY));
        }

        /// <summary>
        /// 加载坐标转换参数期间抑制 ComboBox 回写，避免误触发链接状态
        /// </summary>
        private bool _isLoadingTransformParams;

        /// <summary>
        /// 判断全局变量是否可作为链接目标（仅 Double 类型可链接）
        /// </summary>
        private static bool IsLinkableGlobalVariable(GlobalVariable v)
        {
            return v.Type == GlobalVariableType.Double;
        }

        /// <summary>
        /// 规范化全局变量链接名：空白视为未链接，非数值类型视为无效链接
        /// </summary>
        private static string NormalizeLinkedVarName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Trim();
        }

        /// <summary>
        /// 刷新可链接的全局变量列表（仅保留 Double 类型变量）
        /// </summary>
        private void RefreshLinkableGlobalVariables()
        {
            var linkable = AvailableGlobalVariables
                .Where(IsLinkableGlobalVariable)
                .ToList();
            LinkableGlobalVariables = new ObservableCollection<GlobalVariable>(linkable);
            RaisePropertyChanged(nameof(IsNeedleOffsetXLinked));
            RaisePropertyChanged(nameof(IsNeedleOffsetYLinked));
            RaisePropertyChanged(nameof(IsArcNeedleOffsetXLinked));
            RaisePropertyChanged(nameof(IsArcNeedleOffsetYLinked));
            RaisePropertyChanged(nameof(IsOffsetXExpressionLinked));
            RaisePropertyChanged(nameof(IsOffsetYExpressionLinked));
            RaisePropertyChanged(nameof(IsCameraNeedleDistanceXLinked));
            RaisePropertyChanged(nameof(IsCameraNeedleDistanceYLinked));
        }

        private ObservableCollection<string> _availableConnections = new ObservableCollection<string>();
        public ObservableCollection<string> AvailableConnections
        {
            get => _availableConnections;
            set => SetProperty(ref _availableConnections, value);
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private WorkflowStep _currentStep = WorkflowStep.Step1_ConfigCapture;
        /// <summary>
        /// 当前工作流步骤，驱动界面步骤指示器的状态变化和内容区切换
        /// </summary>
        public WorkflowStep CurrentStep
        {
            get => _currentStep;
            set
            {
                if (SetProperty(ref _currentStep, value))
                {
                    RaisePropertyChanged(nameof(Step1State));
                    RaisePropertyChanged(nameof(Step2State));
                    RaisePropertyChanged(nameof(CurrentStepTitle));
                    RaisePropertyChanged(nameof(IsStep1Active));
                    RaisePropertyChanged(nameof(IsStep2Active));
                    GoPrevCommand?.RaiseCanExecuteChanged();
                    GoNextCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 步骤1状态：配置&拍照
        /// </summary>
        public StepState Step1State => GetStepState(WorkflowStep.Step1_ConfigCapture);
        /// <summary>
        /// 步骤2状态：预览&点胶
        /// </summary>
        public StepState Step2State => GetStepState(WorkflowStep.Step2_PreviewDispense);

        /// <summary>
        /// 是否处于步骤1（配置&拍照），用于内容区Visibility切换
        /// </summary>
        public bool IsStep1Active => CurrentStep == WorkflowStep.Step1_ConfigCapture;
        /// <summary>
        /// 是否处于步骤2（预览&点胶），用于内容区Visibility切换
        /// </summary>
        public bool IsStep2Active => CurrentStep == WorkflowStep.Step2_PreviewDispense;

        private StepState GetStepState(WorkflowStep step)
        {
            if (step < CurrentStep) return StepState.Done;
            if (step == CurrentStep) return StepState.Active;
            return StepState.Pending;
        }

        private bool _isExecuting;
        public bool IsExecuting
        {
            get => _isExecuting;
            set
            {
                if (SetProperty(ref _isExecuting, value))
                {
                    RaisePropertyChanged(nameof(CanStartDispense));
                    RaisePropertyChanged(nameof(CanStop));
                    RaisePropertyChanged(nameof(CanPause));
                    RaisePropertyChanged(nameof(CanResume));
                }
            }
        }

        private bool _isPaused;
        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                if (SetProperty(ref _isPaused, value))
                {
                    RaisePropertyChanged(nameof(CanStartDispense));
                    RaisePropertyChanged(nameof(CanStop));
                    RaisePropertyChanged(nameof(CanPause));
                    RaisePropertyChanged(nameof(CanResume));
                }
            }
        }

        public bool CanStartDispense => !IsExecuting && !IsPaused && SelectedRow != null && CapturedTargetPoints.Count > 0;
        public bool CanStop => IsExecuting || IsPaused;
        public bool CanPause => IsExecuting && !IsPaused;
        public bool CanResume => IsPaused;

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
            set
            {
                if (SetProperty(ref _machinePoints, value))
                {
                    RaisePropertyChanged(nameof(HasMachinePoints));
                    RaisePropertyChanged(nameof(PointsDisplay));
                }
            }
        }

        public bool HasMachinePoints => MachinePoints.Count > 0;

        /// <summary>当前是否已选中拍照位行（用于工艺参数面板可见性）</summary>
        public bool HasSelectedRow => SelectedRow != null;

        private ObservableCollection<UIElement> _arcPathGeometry = new ObservableCollection<UIElement>();
        public ObservableCollection<UIElement> ArcPathGeometry
        {
            get => _arcPathGeometry;
            set
            {
                if (SetProperty(ref _arcPathGeometry, value))
                    RaisePropertyChanged(nameof(HasArcPathGeometry));
            }
        }

        public bool HasArcPathGeometry => ArcPathGeometry.Count > 0;

        /// <summary>底部状态栏 - 分组显示文本（本地化格式）</summary>
        public string GroupDisplay => string.IsNullOrEmpty(SelectedGroup)
            ? L("VisionCapture_Status_GroupNone")
            : string.Format(L("VisionCapture_Status_GroupFormat"), SelectedGroup);

        /// <summary>底部状态栏 - 运行模式显示文本（本地化格式）</summary>
        public string ModeDisplay => CurrentRunMode == null
            ? L("VisionCapture_Status_ModeNone")
            : string.Format(L("VisionCapture_Status_ModeFormat"), CurrentRunMode);

        /// <summary>底部状态栏 - 点数显示文本（本地化格式）</summary>
        public string PointsDisplay => string.Format(L("VisionCapture_Status_PointsFormat"), MachinePoints.Count);

        private ObservableCollection<string> _siteFeatureNames = new ObservableCollection<string>();
        public ObservableCollection<string> SiteFeatureNames
        {
            get => _siteFeatureNames;
            set => SetProperty(ref _siteFeatureNames, value);
        }

        private string _selectedSiteFeatureName;
        public string SelectedSiteFeatureName
        {
            get => _selectedSiteFeatureName;
            set
            {
                if (SetProperty(ref _selectedSiteFeatureName, value))
                {
                    SelectedRow = PhotoPositionRows.FirstOrDefault(r => r.SiteFeatureName == value);
                }
            }
        }

        private bool _isDotMode = true;
        public bool IsDotMode
        {
            get => _isDotMode;
            set
            {
                if (SetProperty(ref _isDotMode, value))
                    RaisePropertyChanged(nameof(DispenseTypeTabIndex));
            }
        }

        private bool _isArcMode;
        public bool IsArcMode
        {
            get => _isArcMode;
            set
            {
                if (SetProperty(ref _isArcMode, value))
                    RaisePropertyChanged(nameof(DispenseTypeTabIndex));
            }
        }

        /// <summary>
        /// 点胶类型Tab索引，0=Dot模式，1=Arc模式，用于TabControl双向绑定
        /// </summary>
        public int DispenseTypeTabIndex
        {
            get => IsArcMode ? 1 : 0;
            set
            {
                IsDotMode = value == 0;
                IsArcMode = value == 1;
            }
        }

        private double _photoDx;
        public double PhotoDx { get => _photoDx; set => SetProperty(ref _photoDx, value); }

        private double _photoDy;
        public double PhotoDy { get => _photoDy; set => SetProperty(ref _photoDy, value); }

        private double _photoDz1;
        /// <summary>选中行 Photo Position 的 Dz₁（0=未获取）</summary>
        public double PhotoDz1 { get => _photoDz1; set => SetProperty(ref _photoDz1, value); }

        private double _photoDz2;
        /// <summary>选中行 Photo Position 的 Dz₂（0=未获取）</summary>
        public double PhotoDz2 { get => _photoDz2; set => SetProperty(ref _photoDz2, value); }

        private double _photoDz3;
        /// <summary>选中行 Photo Position 的 Dz₃/相机Z（0=未获取）</summary>
        public double PhotoDz3 { get => _photoDz3; set => SetProperty(ref _photoDz3, value); }

        private double _targetDeltaX;
        public double TargetDeltaX { get => _targetDeltaX; set => SetProperty(ref _targetDeltaX, value); }

        private double _targetDeltaY;
        public double TargetDeltaY { get => _targetDeltaY; set => SetProperty(ref _targetDeltaY, value); }

        private double _finalX;
        public double FinalX { get => _finalX; set => SetProperty(ref _finalX, value); }

        private double _finalY;
        public double FinalY { get => _finalY; set => SetProperty(ref _finalY, value); }

        private double _visionCenterX;
        public double VisionCenterX { get => _visionCenterX; set => SetProperty(ref _visionCenterX, value); }

        private double _visionCenterY;
        public double VisionCenterY { get => _visionCenterY; set => SetProperty(ref _visionCenterY, value); }

        private double _point1X;
        public double Point1X { get => _point1X; set => SetProperty(ref _point1X, value); }

        private double _point1Y;
        public double Point1Y { get => _point1Y; set => SetProperty(ref _point1Y, value); }

        private double _point2X;
        public double Point2X { get => _point2X; set => SetProperty(ref _point2X, value); }

        private double _point2Y;
        public double Point2Y { get => _point2Y; set => SetProperty(ref _point2Y, value); }

        private double _point3X;
        public double Point3X { get => _point3X; set => SetProperty(ref _point3X, value); }

        private double _point3Y;
        public double Point3Y { get => _point3Y; set => SetProperty(ref _point3Y, value); }

        private double _p1DeltaX;
        public double P1DeltaX { get => _p1DeltaX; set => SetProperty(ref _p1DeltaX, value); }

        private double _p1DeltaY;
        public double P1DeltaY { get => _p1DeltaY; set => SetProperty(ref _p1DeltaY, value); }

        private double _p2DeltaX;
        public double P2DeltaX { get => _p2DeltaX; set => SetProperty(ref _p2DeltaX, value); }

        private double _p2DeltaY;
        public double P2DeltaY { get => _p2DeltaY; set => SetProperty(ref _p2DeltaY, value); }

        private double _p3DeltaX;
        public double P3DeltaX { get => _p3DeltaX; set => SetProperty(ref _p3DeltaX, value); }

        private double _p3DeltaY;
        public double P3DeltaY { get => _p3DeltaY; set => SetProperty(ref _p3DeltaY, value); }

        private double _p1MechX;
        public double P1MechX { get => _p1MechX; set => SetProperty(ref _p1MechX, value); }

        private double _p1MechY;
        public double P1MechY { get => _p1MechY; set => SetProperty(ref _p1MechY, value); }

        private double _p2MechX;
        public double P2MechX { get => _p2MechX; set => SetProperty(ref _p2MechX, value); }

        private double _p2MechY;
        public double P2MechY { get => _p2MechY; set => SetProperty(ref _p2MechY, value); }

        private double _p3MechX;
        public double P3MechX { get => _p3MechX; set => SetProperty(ref _p3MechX, value); }

        private double _p3MechY;
        public double P3MechY { get => _p3MechY; set => SetProperty(ref _p3MechY, value); }

        private int _bezierPointCount;
        public int BezierPointCount { get => _bezierPointCount; set => SetProperty(ref _bezierPointCount, value); }

        /// <summary>
        /// 是否已解析到相机目标点（兼容旧引用；等价于 CapturedTargetPoints 非空）
        /// </summary>
        public bool HasParsedArcData => CapturedTargetPoints.Count > 0;

        /// <summary>相机返回的目标点集合（PointX/Y=相机坐标，MechX/Y=PhotoDx/Dy+target+偏移叠加后）</summary>
        private ObservableCollection<TargetPointItem> _capturedTargetPoints = new ObservableCollection<TargetPointItem>();
        public ObservableCollection<TargetPointItem> CapturedTargetPoints
        {
            get => _capturedTargetPoints;
            set => SetProperty(ref _capturedTargetPoints, value);
        }

        /// <summary>可选轨迹类型（排除 Auto，仅用户显式选择）</summary>
        public TrajectoryType[] AvailableTrajectoryTypes { get; } =
            { TrajectoryType.Dot, TrajectoryType.Line, TrajectoryType.Arc, TrajectoryType.Polyline };

        /// <summary>
        /// 相机回报 Type（已废弃自动检测，保留属性避免旧绑定残留；不再驱动有效类型）。
        /// </summary>
        private TrajectoryType _cameraReportedType = TrajectoryType.Dot;
        public TrajectoryType CameraReportedType
        {
            get => _cameraReportedType;
            set => SetProperty(ref _cameraReportedType, value);
        }

        private bool _isLastCaptureOk = true;
        /// <summary>最近一次视觉拍照结果（分号格式 result 码：1=OK, 0=NG）。NG 时阻止点胶执行。</summary>
        public bool IsLastCaptureOk
        {
            get => _isLastCaptureOk;
            set => SetProperty(ref _isLastCaptureOk, value);
        }

        #region 双针头选择（与 Step3EditParamsPanel 对齐，每套参数独立）

        private int _currentNeedleIndex;
        /// <summary>当前针头索引（0=针头1/Dz₂, 1=针头2/Dz₃）</summary>
        public int CurrentNeedleIndex
        {
            get => _currentNeedleIndex;
            set
            {
                if (_currentNeedleIndex == value) return;
                var oldIndex = _currentNeedleIndex;
                SetProperty(ref _currentNeedleIndex, value);
                // 切换偏移数据（相机针头距离/校针偏差按针头独立）
                SwitchNeedleOffsetData(oldIndex, value);
                RaisePropertyChanged(nameof(IsNeedle1Selected));
                RaisePropertyChanged(nameof(IsNeedle2Selected));
                // 刷新当前针头相关的计算属性
                RaisePropertyChanged(nameof(CurrentDotParams));
                RaisePropertyChanged(nameof(CurrentArcParams));
                RaisePropertyChanged(nameof(CameraNeedleDistanceX));
                RaisePropertyChanged(nameof(CameraNeedleDistanceY));
                RaisePropertyChanged(nameof(NeedleOffsetX));
                RaisePropertyChanged(nameof(NeedleOffsetY));
                RaisePropertyChanged(nameof(CameraNeedleDistanceXLinkedVar));
                RaisePropertyChanged(nameof(CameraNeedleDistanceYLinkedVar));
                RaisePropertyChanged(nameof(NeedleOffsetXLinkedVar));
                RaisePropertyChanged(nameof(NeedleOffsetYLinkedVar));
                RaisePropertyChanged(nameof(IsCameraNeedleDistanceXLinked));
                RaisePropertyChanged(nameof(IsCameraNeedleDistanceYLinked));
                RaisePropertyChanged(nameof(IsNeedleOffsetXLinked));
                RaisePropertyChanged(nameof(IsNeedleOffsetYLinked));
                TryRecalculateMachinePointsIfCaptured();
            }
        }

        /// <summary>是否选中针头1（Dz₂）</summary>
        public bool IsNeedle1Selected
        {
            get => _currentNeedleIndex == 0;
            set { if (value) CurrentNeedleIndex = 0; }
        }

        /// <summary>是否选中针头2（Dz₃）</summary>
        public bool IsNeedle2Selected
        {
            get => _currentNeedleIndex == 1;
            set { if (value) CurrentNeedleIndex = 1; }
        }

        /// <summary>当前针头的 Dot 模式工艺参数（按 CurrentNeedleIndex 从 SelectedRow 取）</summary>
        public DotProcessParams CurrentDotParams =>
            _currentNeedleIndex == 0 ? SelectedRow?.DotParamsNeedle1 : SelectedRow?.DotParamsNeedle2;

        /// <summary>当前针头的路径模式工艺参数（按 CurrentNeedleIndex 从 SelectedRow 取）</summary>
        public DispenseSegment CurrentArcParams =>
            _currentNeedleIndex == 0 ? SelectedRow?.ArcParamsNeedle1 : SelectedRow?.ArcParamsNeedle2;

        // 双针头偏移数据备份（相机针头距离 + 校针偏差 + 链接变量名，每针头独立）
        private readonly double[] _camDistXByNeedle = new double[2];
        private readonly double[] _camDistYByNeedle = new double[2];
        private readonly double[] _needleOffsetXByNeedle = new double[2];
        private readonly double[] _needleOffsetYByNeedle = new double[2];
        private readonly string[] _camDistXLinkedVarByNeedle = new string[2];
        private readonly string[] _camDistYLinkedVarByNeedle = new string[2];
        private readonly string[] _needleOffsetXLinkedVarByNeedle = new string[2];
        private readonly string[] _needleOffsetYLinkedVarByNeedle = new string[2];

        /// <summary>
        /// 切换针头时保存/恢复偏移数据（相机针头距离、校针偏差及其链接变量名）。
        /// 使用 _isLoadingTransformParams 抑制链接变量 setter 的副作用。
        /// </summary>
        private void SwitchNeedleOffsetData(int oldIndex, int newIndex)
        {
            // 1. 保存当前值到旧针头槽位
            _camDistXByNeedle[oldIndex] = CameraNeedleDistanceX;
            _camDistYByNeedle[oldIndex] = CameraNeedleDistanceY;
            _needleOffsetXByNeedle[oldIndex] = NeedleOffsetX;
            _needleOffsetYByNeedle[oldIndex] = NeedleOffsetY;
            _camDistXLinkedVarByNeedle[oldIndex] = CameraNeedleDistanceXLinkedVar;
            _camDistYLinkedVarByNeedle[oldIndex] = CameraNeedleDistanceYLinkedVar;
            _needleOffsetXLinkedVarByNeedle[oldIndex] = NeedleOffsetXLinkedVar;
            _needleOffsetYLinkedVarByNeedle[oldIndex] = NeedleOffsetYLinkedVar;

            // 2. 加载新针头槽位（抑制链接变量副作用）
            var prevLoading = _isLoadingTransformParams;
            _isLoadingTransformParams = true;
            try
            {
                CameraNeedleDistanceX = _camDistXByNeedle[newIndex];
                CameraNeedleDistanceY = _camDistYByNeedle[newIndex];
                NeedleOffsetX = _needleOffsetXByNeedle[newIndex];
                NeedleOffsetY = _needleOffsetYByNeedle[newIndex];
                CameraNeedleDistanceXLinkedVar = _camDistXLinkedVarByNeedle[newIndex];
                CameraNeedleDistanceYLinkedVar = _camDistYLinkedVarByNeedle[newIndex];
                NeedleOffsetXLinkedVar = _needleOffsetXLinkedVarByNeedle[newIndex];
                NeedleOffsetYLinkedVar = _needleOffsetYLinkedVarByNeedle[newIndex];
            }
            finally
            {
                _isLoadingTransformParams = prevLoading;
            }
        }

        #endregion

        /// <summary>
        /// 有效轨迹类型：直接使用用户 Override；Auto（旧配置）按 Dot 处理，不再自动检测。
        /// </summary>
        public TrajectoryType EffectiveTrajectoryType
        {
            get
            {
                var ov = SelectedRow?.TrajectoryOverride ?? TrajectoryType.Dot;
                return ov == TrajectoryType.Auto ? TrajectoryType.Dot : ov;
            }
        }

        /// <summary>
        /// 当前选中行轨迹类型（Process Params ComboBox 双向绑定入口，避免嵌套路径未触发 VM 刷新）。
        /// </summary>
        public TrajectoryType SelectedTrajectoryType
        {
            get => EffectiveTrajectoryType;
            set
            {
                if (SelectedRow == null) return;
                var normalized = value == TrajectoryType.Auto ? TrajectoryType.Dot : value;
                if (SelectedRow.TrajectoryOverride != normalized)
                    SelectedRow.TrajectoryOverride = normalized;
                ApplyTrajectoryTypeSideEffects();
            }
        }

        /// <summary>已解析的弧线中心点X（来自原始数据CenterX）</summary>
        private double _parsedCenterX;
        public double ParsedCenterX { get => _parsedCenterX; set => SetProperty(ref _parsedCenterX, value); }

        /// <summary>已解析的弧线中心点Y（来自原始数据CenterY）</summary>
        private double _parsedCenterY;
        public double ParsedCenterY { get => _parsedCenterY; set => SetProperty(ref _parsedCenterY, value); }

        private double _parsedP1X;
        public double ParsedP1X { get => _parsedP1X; set => SetProperty(ref _parsedP1X, value); }

        private double _parsedP1Y;
        public double ParsedP1Y { get => _parsedP1Y; set => SetProperty(ref _parsedP1Y, value); }

        private double _parsedP2X;
        public double ParsedP2X { get => _parsedP2X; set => SetProperty(ref _parsedP2X, value); }

        private double _parsedP2Y;
        public double ParsedP2Y { get => _parsedP2Y; set => SetProperty(ref _parsedP2Y, value); }

        private double _parsedP3X;
        public double ParsedP3X { get => _parsedP3X; set => SetProperty(ref _parsedP3X, value); }

        private double _parsedP3Y;
        public double ParsedP3Y { get => _parsedP3Y; set => SetProperty(ref _parsedP3Y, value); }

        private RunMode _currentRunMode = RunMode.DryRun;
        public RunMode CurrentRunMode
        {
            get => _currentRunMode;
            set
            {
                if (SetProperty(ref _currentRunMode, value))
                    RaisePropertyChanged(nameof(ModeDisplay));
            }
        }

        private double _cameraCenterX;
        public double CameraCenterX
        {
            get => _cameraCenterX;
            set => SetProperty(ref _cameraCenterX, value);
        }

        private double _cameraCenterY;
        public double CameraCenterY
        {
            get => _cameraCenterY;
            set => SetProperty(ref _cameraCenterY, value);
        }

        private double _needleOffsetX;
        public double NeedleOffsetX
        {
            get => _needleOffsetX;
            set => SetProperty(ref _needleOffsetX, value);
        }

        private double _needleOffsetY;
        public double NeedleOffsetY
        {
            get => _needleOffsetY;
            set => SetProperty(ref _needleOffsetY, value);
        }

        private double _arcNeedleOffsetX;
        public double ArcNeedleOffsetX
        {
            get => _arcNeedleOffsetX;
            set => SetProperty(ref _arcNeedleOffsetX, value);
        }

        private double _arcNeedleOffsetY;
        public double ArcNeedleOffsetY
        {
            get => _arcNeedleOffsetY;
            set => SetProperty(ref _arcNeedleOffsetY, value);
        }

        /// <summary>
        /// Arc模式专用手动补偿X（NumberUpDown，不关联全局变量）
        /// </summary>
        private double _arcNeedleCompX;
        public double ArcNeedleCompX
        {
            get => _arcNeedleCompX;
            set => SetProperty(ref _arcNeedleCompX, value);
        }

        /// <summary>
        /// Arc模式专用手动补偿Y（NumberUpDown，不关联全局变量）
        /// </summary>
        private double _arcNeedleCompY;
        public double ArcNeedleCompY
        {
            get => _arcNeedleCompY;
            set => SetProperty(ref _arcNeedleCompY, value);
        }

        private bool _needleDescend = true;
        /// <summary>
        /// 是否针头下降执行点胶
        /// </summary>
        public bool NeedleDescend
        {
            get => _needleDescend;
            set => SetProperty(ref _needleDescend, value);
        }

        private ObservableCollection<GlobalVariable> _availableGlobalVariables = new ObservableCollection<GlobalVariable>();
        public ObservableCollection<GlobalVariable> AvailableGlobalVariables
        {
            get => _availableGlobalVariables;
            set => SetProperty(ref _availableGlobalVariables, value);
        }

        private ObservableCollection<GlobalVariable> _linkableGlobalVariables = new ObservableCollection<GlobalVariable>();
        /// <summary>
        /// 供链接下拉框使用的全局变量（已排除 VisionCapture 内部持久化变量）
        /// </summary>
        public ObservableCollection<GlobalVariable> LinkableGlobalVariables
        {
            get => _linkableGlobalVariables;
            private set => SetProperty(ref _linkableGlobalVariables, value);
        }

        private string _needleOffsetXLinkedVar;
        public string NeedleOffsetXLinkedVar
        {
            get => _needleOffsetXLinkedVar;
            set => ApplyNeedleOffsetLinkedVar(ref _needleOffsetXLinkedVar, value,
                nameof(NeedleOffsetXLinkedVar), nameof(IsNeedleOffsetXLinked),
                v => NeedleOffsetX = v, () => NeedleOffsetX = 0);
        }

        private string _needleOffsetYLinkedVar;
        public string NeedleOffsetYLinkedVar
        {
            get => _needleOffsetYLinkedVar;
            set => ApplyNeedleOffsetLinkedVar(ref _needleOffsetYLinkedVar, value,
                nameof(NeedleOffsetYLinkedVar), nameof(IsNeedleOffsetYLinked),
                v => NeedleOffsetY = v, () => NeedleOffsetY = 0);
        }

        public bool IsNeedleOffsetXLinked => !string.IsNullOrWhiteSpace(NeedleOffsetXLinkedVar)
            && AvailableGlobalVariables.Any(v => string.Equals(v.Name, NeedleOffsetXLinkedVar, StringComparison.OrdinalIgnoreCase));
        public bool IsNeedleOffsetYLinked => !string.IsNullOrWhiteSpace(NeedleOffsetYLinkedVar)
            && AvailableGlobalVariables.Any(v => string.Equals(v.Name, NeedleOffsetYLinkedVar, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Arc模式独立的针头偏移链接变量（与Dot模式分开，避免双向绑定冲突）
        /// </summary>
        private string _arcNeedleOffsetXLinkedVar;
        public string ArcNeedleOffsetXLinkedVar
        {
            get => _arcNeedleOffsetXLinkedVar;
            set => ApplyNeedleOffsetLinkedVar(ref _arcNeedleOffsetXLinkedVar, value,
                nameof(ArcNeedleOffsetXLinkedVar), nameof(IsArcNeedleOffsetXLinked),
                v => ArcNeedleOffsetX = v, () => ArcNeedleOffsetX = 0);
        }

        private string _arcNeedleOffsetYLinkedVar;
        public string ArcNeedleOffsetYLinkedVar
        {
            get => _arcNeedleOffsetYLinkedVar;
            set => ApplyNeedleOffsetLinkedVar(ref _arcNeedleOffsetYLinkedVar, value,
                nameof(ArcNeedleOffsetYLinkedVar), nameof(IsArcNeedleOffsetYLinked),
                v => ArcNeedleOffsetY = v, () => ArcNeedleOffsetY = 0);
        }

        public bool IsArcNeedleOffsetXLinked => !string.IsNullOrWhiteSpace(ArcNeedleOffsetXLinkedVar)
            && AvailableGlobalVariables.Any(v => string.Equals(v.Name, ArcNeedleOffsetXLinkedVar, StringComparison.OrdinalIgnoreCase));
        public bool IsArcNeedleOffsetYLinked => !string.IsNullOrWhiteSpace(ArcNeedleOffsetYLinkedVar)
            && AvailableGlobalVariables.Any(v => string.Equals(v.Name, ArcNeedleOffsetYLinkedVar, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// 应用针头偏移全局变量链接。
        /// 注意：必须显式传入 linkedVarPropertyName，因为 SetProperty 的 [CallerMemberName]
        /// 在此私有方法中只能捕获方法名而非属性名，导致绑定无法感知属性变更。
        /// </summary>
        private void ApplyNeedleOffsetLinkedVar(
            ref string backingField,
            string value,
            string linkedVarPropertyName,
            string isLinkedPropertyName,
            Action<double> applyLinkedValue,
            Action applyUnlinkedValue)
        {
            if (_isLoadingTransformParams)
            {
                if (SetProperty(ref backingField, NormalizeLinkedVarName(value), linkedVarPropertyName))
                    RaisePropertyChanged(isLinkedPropertyName);
                return;
            }

            var normalized = NormalizeLinkedVarName(value);
            if (!SetProperty(ref backingField, normalized, linkedVarPropertyName))
                return;

            RaisePropertyChanged(isLinkedPropertyName);
            if (!string.IsNullOrEmpty(normalized))
            {
                var gv = AvailableGlobalVariables.FirstOrDefault(v =>
                    string.Equals(v.Name, normalized, StringComparison.OrdinalIgnoreCase));
                if (gv != null && double.TryParse(gv.Value, out var val))
                    applyLinkedValue(val);
            }
            else
            {
                applyUnlinkedValue();
            }
        }

        private double ReadLinkedVariableValue(string varName)
        {
            if (string.IsNullOrEmpty(varName)) return 0;
            var gv = AvailableGlobalVariables.FirstOrDefault(v => v.Name == varName);
            if (gv != null && double.TryParse(gv.Value, out var val))
                return val;
            return 0;
        }

        private string _offsetXExpressionLinkedVar;
        public string OffsetXExpressionLinkedVar
        {
            get => _offsetXExpressionLinkedVar;
            set
            {
                var normalized = NormalizeLinkedVarName(value);
                if (SetProperty(ref _offsetXExpressionLinkedVar, normalized))
                {
                    RaisePropertyChanged(nameof(IsOffsetXExpressionLinked));
                    if (_isLoadingTransformParams) return;
                    if (SelectedRow != null)
                        SelectedRow.OffsetXExpression = normalized;
                    if (!string.IsNullOrEmpty(normalized))
                    {
                        _ = UpdateGlobalVariableValueAsync(normalized, SelectedRow?.CalculatedOffsetX ?? 0);
                    }

                    NotifyTargetOffsetChanged();
                }
            }
        }

        private string _offsetYExpressionLinkedVar;
        public string OffsetYExpressionLinkedVar
        {
            get => _offsetYExpressionLinkedVar;
            set
            {
                var normalized = NormalizeLinkedVarName(value);
                if (SetProperty(ref _offsetYExpressionLinkedVar, normalized))
                {
                    RaisePropertyChanged(nameof(IsOffsetYExpressionLinked));
                    if (_isLoadingTransformParams) return;
                    if (SelectedRow != null)
                        SelectedRow.OffsetYExpression = normalized;
                    if (!string.IsNullOrEmpty(normalized))
                    {
                        _ = UpdateGlobalVariableValueAsync(normalized, SelectedRow?.CalculatedOffsetY ?? 0);
                    }

                    NotifyTargetOffsetChanged();
                }
            }
        }

        public bool IsOffsetXExpressionLinked => !string.IsNullOrWhiteSpace(OffsetXExpressionLinkedVar)
            && AvailableGlobalVariables.Any(v => string.Equals(v.Name, OffsetXExpressionLinkedVar, StringComparison.OrdinalIgnoreCase));
        public bool IsOffsetYExpressionLinked => !string.IsNullOrWhiteSpace(OffsetYExpressionLinkedVar)
            && AvailableGlobalVariables.Any(v => string.Equals(v.Name, OffsetYExpressionLinkedVar, StringComparison.OrdinalIgnoreCase));

        private double _cameraNeedleDistanceX;
        /// <summary>
        /// 相机与胶针固定距离X（可链接全局变量）
        /// </summary>
        public double CameraNeedleDistanceX
        {
            get => _cameraNeedleDistanceX;
            set => SetProperty(ref _cameraNeedleDistanceX, value);
        }

        private double _cameraNeedleDistanceY;
        /// <summary>
        /// 相机与胶针固定距离Y（可链接全局变量）
        /// </summary>
        public double CameraNeedleDistanceY
        {
            get => _cameraNeedleDistanceY;
            set => SetProperty(ref _cameraNeedleDistanceY, value);
        }

        private string _cameraNeedleDistanceXLinkedVar;
        /// <summary>
        /// 相机胶针距离X链接的全局变量名
        /// </summary>
        public string CameraNeedleDistanceXLinkedVar
        {
            get => _cameraNeedleDistanceXLinkedVar;
            set
            {
                var normalized = NormalizeLinkedVarName(value);
                if (SetProperty(ref _cameraNeedleDistanceXLinkedVar, normalized))
                {
                    RaisePropertyChanged(nameof(IsCameraNeedleDistanceXLinked));
                    if (_isLoadingTransformParams) return;
                    if (!string.IsNullOrEmpty(normalized))
                    {
                        var gv = AvailableGlobalVariables.FirstOrDefault(v => v.Name == normalized);
                        if (gv != null && double.TryParse(gv.Value, out var val))
                            CameraNeedleDistanceX = val;
                    }
                    else
                    {
                        CameraNeedleDistanceX = 0;
                    }
                }
            }
        }

        private string _cameraNeedleDistanceYLinkedVar;
        /// <summary>
        /// 相机胶针距离Y链接的全局变量名
        /// </summary>
        public string CameraNeedleDistanceYLinkedVar
        {
            get => _cameraNeedleDistanceYLinkedVar;
            set
            {
                var normalized = NormalizeLinkedVarName(value);
                if (SetProperty(ref _cameraNeedleDistanceYLinkedVar, normalized))
                {
                    RaisePropertyChanged(nameof(IsCameraNeedleDistanceYLinked));
                    if (_isLoadingTransformParams) return;
                    if (!string.IsNullOrEmpty(normalized))
                    {
                        var gv = AvailableGlobalVariables.FirstOrDefault(v => v.Name == normalized);
                        if (gv != null && double.TryParse(gv.Value, out var val))
                            CameraNeedleDistanceY = val;
                    }
                    else
                    {
                        CameraNeedleDistanceY = 0;
                    }
                }
            }
        }

        public bool IsCameraNeedleDistanceXLinked => !string.IsNullOrWhiteSpace(CameraNeedleDistanceXLinkedVar)
            && AvailableGlobalVariables.Any(v => string.Equals(v.Name, CameraNeedleDistanceXLinkedVar, StringComparison.OrdinalIgnoreCase));
        public bool IsCameraNeedleDistanceYLinked => !string.IsNullOrWhiteSpace(CameraNeedleDistanceYLinkedVar)
            && AvailableGlobalVariables.Any(v => string.Equals(v.Name, CameraNeedleDistanceYLinkedVar, StringComparison.OrdinalIgnoreCase));

        private ObservableCollection<string> _availableSafePositions = new ObservableCollection<string>();
        public ObservableCollection<string> AvailableSafePositions
        {
            get => _availableSafePositions;
            set => SetProperty(ref _availableSafePositions, value);
        }

        private string _safePositionName = "SafePosition";
        public string SafePositionName
        {
            get => _safePositionName;
            set
            {
                if (SetProperty(ref _safePositionName, value))
                    RefreshSafePositionDisplay();
            }
        }

        private string _standbyPositionName = "StandbyPosition";
        public string StandbyPositionName
        {
            get => _standbyPositionName;
            set => SetProperty(ref _standbyPositionName, value);
        }

        private string _dispensePositionName = "DispensePosition";
        public string DispensePositionName
        {
            get => _dispensePositionName;
            set => SetProperty(ref _dispensePositionName, value);
        }

        private string _photoHeightName = "PhotoHeight";
        public string PhotoHeightName
        {
            get => _photoHeightName;
            set => SetProperty(ref _photoHeightName, value);
        }

        private double _safePositionDx;
        /// <summary>
        /// 安全位置Dx示教值
        /// </summary>
        public double SafePositionDx
        {
            get => _safePositionDx;
            set => SetProperty(ref _safePositionDx, value);
        }

        private double _safePositionDy;
        /// <summary>
        /// 安全位置Dy示教值
        /// </summary>
        public double SafePositionDy
        {
            get => _safePositionDy;
            set => SetProperty(ref _safePositionDy, value);
        }

        private double _safePositionDz1;
        /// <summary>
        /// 安全位置Dz1示教值
        /// </summary>
        public double SafePositionDz1
        {
            get => _safePositionDz1;
            set => SetProperty(ref _safePositionDz1, value);
        }

        private string _currentFilePath;
        /// <summary>
        /// 当前加载的配置文件路径
        /// </summary>
        public string CurrentFilePath
        {
            get => _currentFilePath;
            set => SetProperty(ref _currentFilePath, value);
        }

        private string _currentFileName;
        /// <summary>
        /// 当前加载的文件名（显示用）
        /// </summary>
        public string CurrentFileName
        {
            get => _currentFileName;
            set => SetProperty(ref _currentFileName, value);
        }

        public DelegateCommand<PhotoPositionRow> ExecuteCaptureCommand { get; }
        public DelegateCommand<PhotoPositionRow> MoveToTeachPositionCommand { get; }
        public DelegateCommand<PhotoPositionRow> StopRowMotionCommand { get; }
        public DelegateCommand ExecuteDispenseCommand { get; }
        public DelegateCommand PreviewMachinePointsCommand { get; }
        public DelegateCommand SaveTransformParamsCommand { get; }
        public DelegateCommand StopCommand { get; }
        public DelegateCommand PauseCommand { get; }
        public DelegateCommand ResumeCommand { get; }
        public DelegateCommand GoPrevCommand { get; }
        public DelegateCommand GoNextCommand { get; }
        public DelegateCommand SaveConfigCommand { get; }
        public DelegateCommand LoadConfigCommand { get; }
        /// <summary>新建组命令（不依赖 WorkOrder）</summary>
        public DelegateCommand AddGroupCommand { get; }
        /// <summary>删除当前组命令</summary>
        public DelegateCommand DeleteGroupCommand { get; }
        /// <summary>添加拍照位命令</summary>
        public DelegateCommand AddPhotoPositionCommand { get; }
        /// <summary>删除指定拍照位命令</summary>
        public DelegateCommand<PhotoPositionRow> DeletePhotoPositionCommand { get; }
        /// <summary>立即返回安全位命令（按行速度执行抬轴+XY回安全位）</summary>
        public DelegateCommand<PhotoPositionRow> ReturnToSafeCommand { get; }
        public DelegateCommand UnlinkNeedleOffsetXCommand { get; }
        public DelegateCommand UnlinkNeedleOffsetYCommand { get; }
        public DelegateCommand UnlinkArcNeedleOffsetXCommand { get; }
        public DelegateCommand UnlinkArcNeedleOffsetYCommand { get; }
        public DelegateCommand UnlinkNeedleDistanceXCommand { get; }
        public DelegateCommand UnlinkNeedleDistanceYCommand { get; }
        public DelegateCommand UnlinkOffsetXExpressionCommand { get; }
        public DelegateCommand UnlinkOffsetYExpressionCommand { get; }

        /// <summary>
        /// 当前步骤标题，用于底部状态栏显示
        /// </summary>
        public string CurrentStepTitle => CurrentStep switch
        {
            WorkflowStep.Step1_ConfigCapture => L("VisionCapture_Step1_Title"),
            WorkflowStep.Step2_PreviewDispense => L("VisionCapture_Step2_Title"),
            _ => L("VisionCapture_Step1_Title")
        };

        /// <summary>
        /// 获取多语言文本（便捷方法）
        /// </summary>
        private string L(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            if (_localizationService != null)
                return _localizationService.GetResource(key);

            var resource = Application.Current?.TryFindResource(key);
            return resource?.ToString() ?? $"[{key}]";
        }

        public VisionCaptureViewModel(
            IRecipePoolService recipePoolService,
            IPositionProvider positionProvider,
            ITCPEventService tcpEventService,
            ITCPClientManagerService tcpClientManager,
            IMotionService motionService,
            IStationRegistry stationRegistry,
            VisionCaptureService visionCaptureService,
            BezierArcDispenseService bezierArcDispenseService,
            IDotDispenseService dotDispenseService,
            IDispenseExecuteService dispenseExecuteService,
            ILoggerService logger,
            ILocalizationService localizationService,
            IEventAggregator eventAggregator,
            IDialogService dialogService,
            IAxisParameterService axisParameterService,
            IConfigFileRetentionService configRetentionService)
        {
            _recipePoolService = recipePoolService;
            _positionProvider = positionProvider;
            _tcpEventService = tcpEventService;
            _tcpClientManager = tcpClientManager;
            _motionService = motionService;
            _stationRegistry = stationRegistry;
            _visionCaptureService = visionCaptureService;
            _bezierArcDispenseService = bezierArcDispenseService;
            _dotDispenseService = dotDispenseService;
            _dispenseExecuteService = dispenseExecuteService;
            _axisParameterService = axisParameterService;
            _logger = logger;
            _localizationService = localizationService;
            _eventAggregator = eventAggregator;
            _dialogService = dialogService;
            _configRetentionService = configRetentionService;

            // 订阅 MachinePoints 集合变化以更新 PointsDisplay
            _machinePoints.CollectionChanged += (s, e) => RaisePropertyChanged(nameof(PointsDisplay));

            ExecuteCaptureCommand = new DelegateCommand<PhotoPositionRow>(
                async row => await ExecuteCaptureAsync(row),
                row => row != null && !row.IsPositionInvalid && !row.IsExecuting && !IsExecuting
            );
            MoveToTeachPositionCommand = new DelegateCommand<PhotoPositionRow>(
                async row => await MoveToTeachPositionAsync(row),
                row => row != null && !row.IsPositionInvalid && !row.IsExecuting && !IsExecuting
            );
            StopRowMotionCommand = new DelegateCommand<PhotoPositionRow>(
                row => StopRowMotion(row),
                row => row != null && row.IsExecuting
            );
            ExecuteDispenseCommand = new DelegateCommand(
                async () => await ExecuteDispenseAsync(),
                () => SelectedRow != null && !SelectedRow.IsPositionInvalid && CapturedTargetPoints.Count > 0 && !IsExecuting
            );
            PreviewMachinePointsCommand = new DelegateCommand(
                async () => await PreviewMachinePointsAsync()
            );
            SaveTransformParamsCommand = new DelegateCommand(
                async () => await SaveTransformParamsAsync()
            );
            StopCommand = new DelegateCommand(() => _dispenseCts?.Cancel());
            PauseCommand = new DelegateCommand(() => { _pauseEvent.Reset(); IsPaused = true; }, () => CanPause);
            ResumeCommand = new DelegateCommand(() => { _pauseEvent.Set(); IsPaused = false; }, () => CanResume);

            GoPrevCommand = new DelegateCommand(
                () =>
                {
                    if (CurrentStep > WorkflowStep.Step1_ConfigCapture)
                        CurrentStep = WorkflowStep.Step1_ConfigCapture;
                },
                () => CurrentStep > WorkflowStep.Step1_ConfigCapture
            );
            GoNextCommand = new DelegateCommand(
                () =>
                {
                    if (CurrentStep < WorkflowStep.Step2_PreviewDispense)
                        CurrentStep = WorkflowStep.Step2_PreviewDispense;
                },
                () => CurrentStep < WorkflowStep.Step2_PreviewDispense
            );

            SaveConfigCommand = new DelegateCommand(
                async () => await SaveConfigToFileAsync(),
                () => true
            );
            LoadConfigCommand = new DelegateCommand(
                async () => await LoadConfigFromFileAsync(),
                () => true
            );
            AddGroupCommand = new DelegateCommand(
                () => AddGroup(),
                () => !IsExecuting
            );
            DeleteGroupCommand = new DelegateCommand(
                async () => await DeleteGroupAsync(),
                () => !string.IsNullOrEmpty(SelectedGroup) && !IsExecuting
            );
            AddPhotoPositionCommand = new DelegateCommand(
                () => AddPhotoPosition(),
                () => !IsExecuting
            );
            DeletePhotoPositionCommand = new DelegateCommand<PhotoPositionRow>(
                row => DeletePhotoPosition(row),
                row => row != null && !IsExecuting
            );
            // 立即返回待机位：抬 Z 轴 → Dx/Dy 回待机位（Y 不动），按行速度执行
            ReturnToSafeCommand = new DelegateCommand<PhotoPositionRow>(
                async row => await ReturnToSafeFromRowAsync(row),
                row => row != null && !IsExecuting
            );

            UnlinkNeedleOffsetXCommand = new DelegateCommand(() => NeedleOffsetXLinkedVar = null);
            UnlinkNeedleOffsetYCommand = new DelegateCommand(() => NeedleOffsetYLinkedVar = null);
            UnlinkArcNeedleOffsetXCommand = new DelegateCommand(() => ArcNeedleOffsetXLinkedVar = null);
            UnlinkArcNeedleOffsetYCommand = new DelegateCommand(() => ArcNeedleOffsetYLinkedVar = null);
            UnlinkNeedleDistanceXCommand = new DelegateCommand(() => CameraNeedleDistanceXLinkedVar = null);
            UnlinkNeedleDistanceYCommand = new DelegateCommand(() => CameraNeedleDistanceYLinkedVar = null);
            UnlinkOffsetXExpressionCommand = new DelegateCommand(() => OffsetXExpressionLinkedVar = null);
            UnlinkOffsetYExpressionCommand = new DelegateCommand(() => OffsetYExpressionLinkedVar = null);

            // 使用示例初始数据预填充弧线解析显示，收到实际视觉数据后自动替换
            InitializeDefaultArcData();

            // 订阅拍照位行集合变化，为每行挂接 PositionName 变更监听
            PhotoPositionRows.CollectionChanged += OnPhotoPositionRowsCollectionChanged;

            _ = InitializeAsync();
            LoadConnections();

            _eventAggregator.GetEvent<SaveParametersCompletedEvent>().Subscribe(OnPositionsUpdated, ThreadOption.UIThread);
            _eventAggregator.GetEvent<RecipeChangedEvent>().Subscribe(OnRecipeChanged, ThreadOption.UIThread);
            _eventAggregator.GetEvent<StationParameterSavedEvent>().Subscribe(OnStationPositionSaved, ThreadOption.UIThread);
            _eventAggregator.GetEvent<GlobalVariablesChangedEvent>().Subscribe(OnGlobalVariablesChanged, ThreadOption.UIThread);

            // 订阅配方池切换事件：切换池时从新池 ExtensionData 重新加载视觉采集配置文件（参考 ZScanDetailViewModel 模式）
            _eventAggregator.GetEvent<RecipePoolChangedEvent>().Subscribe(OnRecipePoolChanged, ThreadOption.UIThread);
        }

        /// <summary>配方池切换时从新池 ExtensionData 重新加载视觉采集配置文件</summary>
        private void OnRecipePoolChanged(string poolName)
        {
            _ = TryAutoLoadConfigAsync();
            _logger.Info(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_RecipePoolSwitchedReload",
                "[VisionCapture] 配方池切换，已从新池重新加载配置（池={0}）"), poolName));
        }

        /// <summary>
        /// 使用示例目标点预填充显示；收到实际视觉数据后由 TryParseTargetPoints 替换。
        /// </summary>
        private void InitializeDefaultArcData()
        {
            const double p1x = -12.174, p1y = 594.432;
            const double p2x = -14.246, p2y = 594.988;
            const double p3x = -16.318, p3y = 595.692;

            CapturedTargetPoints.Clear();
            CapturedTargetPoints.Add(new TargetPointItem { Index = 1, PointX = p1x, PointY = p1y });
            CapturedTargetPoints.Add(new TargetPointItem { Index = 2, PointX = p2x, PointY = p2y });
            CapturedTargetPoints.Add(new TargetPointItem { Index = 3, PointX = p3x, PointY = p3y });

            // 兼容旧 UI 字段显示
            ParsedP1X = p1x; ParsedP1Y = p1y;
            ParsedP2X = p2x; ParsedP2Y = p2y;
            ParsedP3X = p3x; ParsedP3Y = p3y;
            Point1X = p1x; Point1Y = p1y;
            Point2X = p2x; Point2Y = p2y;
            Point3X = p3x; Point3Y = p3y;

            SyncDotArcModeFromEffectiveType();
        }

        private async void OnPositionsUpdated(string recipeName)
        {
            try
            {
                await _positionProvider.InvalidateCacheAsync();
                _allPositions = await MergeAllPositionsAsync();
                RefreshAvailablePositions();
                RefreshSafePositionDisplay();
                RefreshPhotoPosition(SelectedRow);
                _logger.Info(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_PositionDataSynced", "[VisionCapture] 位置数据已同步更新 (recipe={0}), _allPositions count={1}, SafePositionName={2}, Dx={3}, Dy={4}, Dz1={5}"), recipeName, _allPositions?.Count ?? 0, SafePositionName, SafePositionDx, SafePositionDy, SafePositionDz1));
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_PositionDataSyncFailed", "[VisionCapture] 位置数据同步失败: {0}"), ex.Message));
            }
        }

        private async void OnRecipeChanged(string recipeName)
        {
            try
            {
                await _positionProvider.InvalidateCacheAsync();
                _allPositions = await MergeAllPositionsAsync();
                RefreshAvailablePositions();
                RefreshSafePositionDisplay();
                RefreshPhotoPosition(SelectedRow);
                await LoadTransformParamsAsync();
            }
            catch
            {
            }
        }

        private async void OnStationPositionSaved(string stationIdentifier)
        {
            try
            {
                await _positionProvider.InvalidateCacheAsync();
                _allPositions = await MergeAllPositionsAsync();
                RefreshAvailablePositions();
                RefreshSafePositionDisplay();
                RefreshPhotoPosition(SelectedRow);
                _logger.Info(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_StationPositionSaved", "[VisionCapture] 工站 [{0}] 位置已保存, _allPositions count={1}, SafePositionName={2}, Dx={3}, Dy={4}, Dz1={5}"), stationIdentifier, _allPositions?.Count ?? 0, SafePositionName, SafePositionDx, SafePositionDy, SafePositionDz1));
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_StationPosSaveSyncFailed", "[VisionCapture] 工站位置保存后同步失败: {0}"), ex.Message));
            }
        }

        /// <summary>
        /// 外部全局变量变更时重新加载，同步链接变量值和链接状态
        /// </summary>
        private async void OnGlobalVariablesChanged(string poolId)
        {
            try
            {
                var currentPoolId = _recipePoolService.CurrentPoolName ?? "Default";
                if (!string.Equals(poolId, currentPoolId, StringComparison.OrdinalIgnoreCase))
                    return;

                var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);

                AvailableGlobalVariables.Clear();
                foreach (var v in variables)
                    AvailableGlobalVariables.Add(v);

                RefreshLinkableGlobalVariables();

                if (IsNeedleOffsetXLinked)
                    NeedleOffsetX = ReadLinkedVariableValue(NeedleOffsetXLinkedVar);
                if (IsNeedleOffsetYLinked)
                    NeedleOffsetY = ReadLinkedVariableValue(NeedleOffsetYLinkedVar);
                if (IsArcNeedleOffsetXLinked)
                    ArcNeedleOffsetX = ReadLinkedVariableValue(ArcNeedleOffsetXLinkedVar);
                if (IsArcNeedleOffsetYLinked)
                    ArcNeedleOffsetY = ReadLinkedVariableValue(ArcNeedleOffsetYLinkedVar);
                if (IsCameraNeedleDistanceXLinked)
                    CameraNeedleDistanceX = ReadLinkedVariableValue(CameraNeedleDistanceXLinkedVar);
                if (IsCameraNeedleDistanceYLinked)
                    CameraNeedleDistanceY = ReadLinkedVariableValue(CameraNeedleDistanceYLinkedVar);
                if (IsOffsetXExpressionLinked || IsOffsetYExpressionLinked)
                {
                    SelectedRow?.NotifyCalculatedPropertiesChanged();
                }

                TryRecalculateMachinePointsIfCaptured();
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_GlobalVarChangeSyncFailed", "[VisionCapture] 全局变量变更同步失败: {0}"), ex.Message));
            }
        }

        /// <summary>
        /// 偏移/拍照位变更后，若已有相机目标点则按新公式重算 MechX/Y 预览。
        /// </summary>
        private void TryRecalculateMachinePointsIfCaptured()
        {
            if (SelectedRow == null || CapturedTargetPoints.Count == 0)
                return;
            _ = ComputeMachinePointsFromCameraAsync(SelectedRow);
        }

        /// <summary>
        /// 刷新所有行的下拉位置列表，保留当前选中值
        /// </summary>
        private void RefreshAvailablePositions()
        {
            var positionNames = new HashSet<string>();
            foreach (var key in _allPositions.Keys)
            {
                var parts = key.Split('.');
                if (parts.Length >= 3)
                    positionNames.Add($"{parts[0]}.{parts[1]}");
            }
            var sortedPositions = positionNames.OrderBy(p => p).ToList();

            var currentSafePosition = SafePositionName;
            AvailableSafePositions = new ObservableCollection<string>(sortedPositions);
            SafePositionName = currentSafePosition;

            foreach (var row in PhotoPositionRows)
            {
                // 拍照位名称手动输入，坐标按 PositionName 从位置编辑器解析
                row.AvailablePositions = new ObservableCollection<string>(sortedPositions);
                RefreshRowParsedCoordinates(row);
            }
        }

        private async Task InitializeAsync()
        {
            _logger.Info(_localizationService.GetResourceOrDefault("VisCap_Log_InitStart", "[VisionCapture] 开始初始化..."));
            try
            {
                await LoadGroupsAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_InitGroupFailed", "[VisionCapture] 初始化Group失败: {0}"), ex.Message));
            }
            try
            {
                await TryAutoLoadConfigAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_AutoLoadConfigException", "[VisionCapture] 自动加载配置异常: {0}"), ex.Message));
            }
            _logger.Info(_localizationService.GetResourceOrDefault("VisCap_Log_InitComplete", "[VisionCapture] 初始化完成"));
        }

        /// <summary>
        /// 初始化组列表（不再从 WorkOrder 获取；若无配置则创建默认组）
        /// </summary>
        private async Task LoadGroupsAsync()
        {
            try
            {
                _allPositions = await MergeAllPositionsAsync();
                RefreshSafePositionDisplay();

                if (Groups.Count == 0)
                {
                    Groups.Add("Group1");
                    _groupRowsCache["Group1"] = new List<PhotoPositionRow>();
                    SelectedGroup = "Group1";
                }

                await LoadTransformParamsAsync();
                await _reloadRowsTask;
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_LoadGroupListFailed", "[VisionCapture] 加载Group列表失败: {0}"), ex.Message));
            }
        }

        /// <summary>
        /// 切换组：先缓存当前组行，再加载目标组行（独立于 WorkOrder）
        /// </summary>
        private async Task OnSelectedGroupChanged(string previousGroup)
        {
            try
            {
                // 切换前缓存当前组的拍照位行
                if (!string.IsNullOrEmpty(previousGroup))
                    CacheCurrentGroupRows(previousGroup);

                if (string.IsNullOrEmpty(SelectedGroup))
                {
                    ClearPhotoPositionRows();
                    return;
                }

                _allPositions = await MergeAllPositionsAsync();
                LoadRowsFromGroupCache(SelectedGroup);
                RefreshAvailablePositions();
                RefreshSafePositionDisplay();
                RefreshPhotoPosition(SelectedRow);
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_LoadSiteFeatureFailed", "[VisionCapture] 加载组拍照位失败: {0}"), ex.Message));
            }
        }

        /// <summary>将当前 PhotoPositionRows 缓存到指定组</summary>
        private void CacheCurrentGroupRows(string groupName)
        {
            if (string.IsNullOrEmpty(groupName)) return;
            _groupRowsCache[groupName] = PhotoPositionRows.ToList();
        }

        /// <summary>从组缓存加载拍照位行到 UI</summary>
        private void LoadRowsFromGroupCache(string groupName)
        {
            ClearPhotoPositionRows();
            SiteFeatureNames.Clear();

            if (!_groupRowsCache.TryGetValue(groupName, out var cached) || cached == null)
            {
                _groupRowsCache[groupName] = new List<PhotoPositionRow>();
                return;
            }

            var positionNames = BuildAvailablePositionNames();
            foreach (var row in cached)
            {
                row.AvailablePositions = new ObservableCollection<string>(positionNames);
                MigrateLegacyRowOffsetExpressions(row);
                PhotoPositionRows.Add(row);
                SiteFeatureNames.Add(row.PositionName);
            }

            if (PhotoPositionRows.Count > 0)
            {
                SelectedRow = PhotoPositionRows[0];
                SelectedSiteFeatureName = SelectedRow.PositionName;
            }
        }

        /// <summary>清空拍照位行并解除 PropertyChanged 订阅</summary>
        private void ClearPhotoPositionRows()
        {
            SelectedRow = null;
            // CollectionChanged 会自动解除各行订阅
            PhotoPositionRows.Clear();
        }

        /// <summary>构建位置编辑器可用位置名列表（Station.PositionName）</summary>
        private List<string> BuildAvailablePositionNames()
        {
            var positionNames = new HashSet<string>();
            if (_allPositions == null) return positionNames.OrderBy(p => p).ToList();
            foreach (var key in _allPositions.Keys)
            {
                var parts = key.Split('.');
                if (parts.Length >= 3)
                    positionNames.Add($"{parts[0]}.{parts[1]}");
            }
            return positionNames.OrderBy(p => p).ToList();
        }

        /// <summary>
        /// 新建组：弹出 GroupEditorDialog 输入组名，加入本地 Groups（不写 WorkOrder）
        /// </summary>
        private void AddGroup()
        {
            _dialogService.ShowDialog("GroupEditorDialog", null, result =>
            {
                if (result.Result != ButtonResult.OK) return;
                var newGroup = result.Parameters.GetValue<Site>("group");
                var name = newGroup?.Name?.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    StatusMessage = L("VisionCapture_Status_GroupNameEmpty");
                    return;
                }
                if (Groups.Contains(name))
                {
                    StatusMessage = string.Format(L("VisionCapture_Status_GroupExists"), name);
                    return;
                }

                // 切换前缓存当前组
                if (!string.IsNullOrEmpty(SelectedGroup))
                    CacheCurrentGroupRows(SelectedGroup);

                Groups.Add(name);
                _groupRowsCache[name] = new List<PhotoPositionRow>();
                SelectedGroup = name;
                StatusMessage = string.Format(L("VisionCapture_Status_GroupAdded"), name);
                _logger.Info(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_AddGroup", "[VisionCapture] 新建组: {0}"), name));
            });
        }

        /// <summary>
        /// 删除当前组（需确认），并切换到剩余组或清空
        /// </summary>
        private async Task DeleteGroupAsync()
        {
            if (string.IsNullOrEmpty(SelectedGroup)) return;
            var groupName = SelectedGroup;
            var confirmed = await ShowConfirmationAsync(
                L("VisionCapture_Confirm_DeleteGroupTitle"),
                string.Format(L("VisionCapture_Confirm_DeleteGroupMessage"), groupName));
            if (!confirmed) return;

            Groups.Remove(groupName);
            _groupRowsCache.Remove(groupName);

            _suppressGroupChangeReload = true;
            try
            {
                if (Groups.Count > 0)
                {
                    var next = Groups[0];
                    _selectedGroup = null;
                    SetProperty(ref _selectedGroup, next, nameof(SelectedGroup));
                    RaisePropertyChanged(nameof(GroupDisplay));
                    LoadRowsFromGroupCache(next);
                    RefreshAvailablePositions();
                    RefreshSafePositionDisplay();
                }
                else
                {
                    SelectedGroup = null;
                    ClearPhotoPositionRows();
                    SiteFeatureNames.Clear();
                }
            }
            finally
            {
                _suppressGroupChangeReload = false;
            }

            StatusMessage = string.Format(L("VisionCapture_Status_GroupDeleted"), groupName);
            _logger.Info(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_DeleteGroup", "[VisionCapture] 删除组: {0}"), groupName));
            DeleteGroupCommand?.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// 拍照位行集合变化时，为新增行订阅 PositionName 变更以自动解析坐标
        /// </summary>
        private void OnPhotoPositionRowsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (PhotoPositionRow row in e.NewItems)
                    row.PropertyChanged += OnPhotoPositionRowPropertyChanged;
            }
            if (e.OldItems != null)
            {
                foreach (PhotoPositionRow row in e.OldItems)
                    row.PropertyChanged -= OnPhotoPositionRowPropertyChanged;
            }
            AddPhotoPositionCommand?.RaiseCanExecuteChanged();
            DeletePhotoPositionCommand?.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// 任意行 PositionName 变更时，从位置编辑器所有工站解析各轴坐标。
        /// IsPositionInvalid 变更时刷新运动命令可用性（按钮置灰/恢复）。
        /// </summary>
        private void OnPhotoPositionRowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PhotoPositionRow.PositionName) && sender is PhotoPositionRow row)
            {
                // 用户输入或旧配置带工站前缀时，统一规范为短名称
                var normalized = NormalizePhotoPositionName(row.PositionName);
                if (!string.Equals(normalized, row.PositionName, StringComparison.Ordinal))
                    row.PositionName = normalized;
                RefreshRowParsedCoordinates(row);
                if (row == SelectedRow)
                    RefreshPhotoPosition(row);
                // 同步 SiteFeatureNames 列表（Step2 下拉用）
                SyncSiteFeatureNamesFromRows();
                // 名称变更后同步当前组缓存
                if (!string.IsNullOrEmpty(SelectedGroup))
                    CacheCurrentGroupRows(SelectedGroup);
            }
            else if (e.PropertyName == nameof(PhotoPositionRow.IsPositionInvalid))
            {
                // 位置有效性变化时刷新运动/点胶命令可用性
                ExecuteCaptureCommand?.RaiseCanExecuteChanged();
                MoveToTeachPositionCommand?.RaiseCanExecuteChanged();
                StopRowMotionCommand?.RaiseCanExecuteChanged();
                ExecuteDispenseCommand?.RaiseCanExecuteChanged();
            }
            else if ((e.PropertyName == nameof(PhotoPositionRow.TrajectoryOverride)
                      || e.PropertyName == nameof(PhotoPositionRow.DispenseType))
                     && sender is PhotoPositionRow trajectoryRow
                     && trajectoryRow == SelectedRow)
            {
                // 集合行订阅兜底：嵌套绑定写 TrajectoryOverride 时确保工艺参数面板切换
                ApplyTrajectoryTypeSideEffects();
            }
        }

        /// <summary>根据当前行同步 SiteFeatureNames</summary>
        private void SyncSiteFeatureNamesFromRows()
        {
            SiteFeatureNames.Clear();
            foreach (var row in PhotoPositionRows)
                SiteFeatureNames.Add(row.PositionName);
        }

        /// <summary>
        /// 刷新拍照位置坐标显示，从位置数据中读取 Dx/Dy/Dz₁/Dz₂/Dz₃；值为 0 视为未获取。
        /// </summary>
        private void RefreshPhotoPosition(PhotoPositionRow row)
        {
            if (row == null || _allPositions == null)
            {
                PhotoDx = 0; PhotoDy = 0; PhotoDz1 = 0; PhotoDz2 = 0; PhotoDz3 = 0;
                return;
            }
            PhotoDx = TryGetTaughtPosition(row.PositionName, "Dx") ?? 0;
            PhotoDy = TryGetTaughtPosition(row.PositionName, "Dy") ?? 0;
            PhotoDz1 = TryGetTaughtPosition(row.PositionName, "Dz₁") ?? 0;
            PhotoDz2 = TryGetTaughtPosition(row.PositionName, "Dz₂") ?? 0;
            PhotoDz3 = TryGetTaughtPosition(row.PositionName, "Dz₃") ?? 0;
        }

        /// <summary>
        /// 按 row.PositionName 从位置编辑器解析 Dx/Dy/Dz₁/Dz₂/Dz₃/Y/Rx/Rz。
        /// 轴值缺失或为 0 视为未获取；拍照运动必需轴 Dx/Dy/Dz₃ 任一未获取则标记 IsPositionInvalid。
        /// </summary>
        private void RefreshRowParsedCoordinates(PhotoPositionRow row)
        {
            if (row == null) return;
            // 空名称或位置表未加载时不标记无效（避免初始化阶段的误报）
            if (string.IsNullOrEmpty(row.PositionName) || _allPositions == null)
            {
                row.IsPositionInvalid = false;
                return;
            }

            var dx = TryGetTaughtPosition(row.PositionName, "Dx");
            var dy = TryGetTaughtPosition(row.PositionName, "Dy");
            var dz1 = TryGetTaughtPosition(row.PositionName, "Dz₁");
            var dz2 = TryGetTaughtPosition(row.PositionName, "Dz₂");
            var dz3 = TryGetTaughtPosition(row.PositionName, "Dz₃");
            var y = TryGetTaughtPosition(row.PositionName, "Y");
            var rx = TryGetTaughtPosition(row.PositionName, "Rx");
            var rz = TryGetTaughtPosition(row.PositionName, "Rz");

            // 运动必需：Dx/Dy/Dz₃（相机 Z）；其余轴缺失仅影响显示与姿态轴运动
            bool requiredOk = dx.HasValue && dy.HasValue && dz3.HasValue;
            row.IsPositionInvalid = !requiredOk;

            if (row.IsPositionInvalid)
            {
                StatusMessage = string.Format(L("VisionCapture_Status_PositionNotFound"), row.PositionName);
                _logger.Warn(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_PositionNotFound",
                    "[VisionCapture] 位置名 '{0}' 缺少有效 Dx/Dy/Dz₃（0 视为未获取），运动有碰撞风险"), row.PositionName));
            }

            row.UpdateParsedCoordinates(dx, dy, dz1, dz2, dz3, y, rx, rz);
            SyncSafeHeightFromEditorIfDefault(row);
        }

        /// <summary>安全抬升高度出厂默认值（DotProcessParams/DispenseSegment 构造默认），视为"未手动修改"的标记值</summary>
        private const double DefaultSafeHeightSentinel = -20.0;

        /// <summary>
        /// 若工艺参数的安全高度仍为出厂默认值（未手动修改过），自动用位置编辑器 SafePosition 的 Dz₁ 高度回填，
        /// 避免用户凭手感填错安全高度造成撞机；一旦用户手动修改过（值不再等于默认值）则不再自动覆盖。
        /// </summary>
        private void SyncSafeHeightFromEditorIfDefault(PhotoPositionRow row)
        {
            if (row == null) return;
            var dz1Safe = TryGetEditorSafePositionAxis("Dz₁");
            if (!dz1Safe.HasValue) return;

            if (row.DotParamsNeedle1 != null && row.DotParamsNeedle1.SafeHeight == DefaultSafeHeightSentinel)
                row.DotParamsNeedle1.SafeHeight = dz1Safe.Value;
            if (row.DotParamsNeedle2 != null && row.DotParamsNeedle2.SafeHeight == DefaultSafeHeightSentinel)
                row.DotParamsNeedle2.SafeHeight = dz1Safe.Value;
            if (row.ArcParamsNeedle1 != null && row.ArcParamsNeedle1.SafeHeight == DefaultSafeHeightSentinel)
                row.ArcParamsNeedle1.SafeHeight = dz1Safe.Value;
            if (row.ArcParamsNeedle2 != null && row.ArcParamsNeedle2.SafeHeight == DefaultSafeHeightSentinel)
                row.ArcParamsNeedle2.SafeHeight = dz1Safe.Value;
        }

        private void RefreshSafePositionDisplay()
        {
            if (_allPositions == null) return;
            if (string.IsNullOrEmpty(SafePositionName)) return;
            var dx = TryGetPositionRaw(SafePositionName, "Dx");
            var dy = TryGetPositionRaw(SafePositionName, "Dy");
            var dz1 = TryGetPositionRaw(SafePositionName, "Dz₁");
            SafePositionDx = dx ?? 0;
            SafePositionDy = dy ?? 0;
            SafePositionDz1 = dz1 ?? 0;

            if (!dx.HasValue || !dy.HasValue || !dz1.HasValue)
            {
                var missingAxes = new List<string>();
                if (!dx.HasValue) missingAxes.Add("Dx");
                if (!dy.HasValue) missingAxes.Add("Dy");
                if (!dz1.HasValue) missingAxes.Add("Dz₁");
                StatusMessage = string.Format(L("VisionCapture_Status_SafePosMissingAxis"), SafePositionName, string.Join(", ", missingAxes));
                _logger.Warn(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_SafePosMissingAxis", "[VisionCapture] 安全位 '{0}' 缺少轴: {1}，请选择包含Dx/Dy/Dz₁轴工站的位置"), SafePositionName, string.Join(", ", missingAxes)));
            }
        }

        /// <summary>
        /// 读取示教位置轴坐标；键不存在或值为 0 时返回 null（按未获取处理，避免把默认 0 当有效目标）。
        /// 仅用于当前拍照位轴校验；SafePosition/Standby 请用 TryGetPositionRaw。
        /// </summary>
        private double? TryGetTaughtPosition(string positionName, string axisName)
        {
            if (_allPositions == null || string.IsNullOrEmpty(positionName) || string.IsNullOrEmpty(axisName))
                return null;
            var key = ResolvePositionKey(positionName, axisName);
            if (key == null || !_allPositions.TryGetValue(key, out var value))
                return null;
            // 工业约定：示教值为 0 表示该轴未配置/未获取，禁止当作目标位置下发
            if (value == 0.0)
                return null;
            return value;
        }

        /// <summary>
        /// 读取位置轴坐标（允许 0）；仅键不存在时返回 null。用于 SafePosition/Standby 等安全位，不做非 0 校验。
        /// </summary>
        private double? TryGetPositionRaw(string positionName, string axisName)
        {
            if (_allPositions == null || string.IsNullOrEmpty(positionName) || string.IsNullOrEmpty(axisName))
                return null;
            var key = ResolvePositionKey(positionName, axisName);
            if (key == null || !_allPositions.TryGetValue(key, out var value))
                return null;
            return value;
        }

        /// <summary>位置编辑器固定安全位名称；抬 Z 时只读此位置，不使用可配置 SafePositionName（避免误匹配 NewPosition3 等）。</summary>
        private const string EditorSafePositionName = "SafePosition";
        /// <summary>位置编辑器固定待机位名称；回安全时 Dx/Dy 只读此位置。</summary>
        private const string EditorStandbyPositionName = "StandbyPosition";

        /// <summary>
        /// 从位置编辑器读取固定 SafePosition 的轴坐标；仅匹配位置名段恰好为 SafePosition 的键。
        /// </summary>
        private double? TryGetEditorSafePositionAxis(string axisName)
        {
            return TryGetEditorNamedPositionAxis(EditorSafePositionName, axisName);
        }

        /// <summary>
        /// 从位置编辑器读取固定 StandbyPosition 的轴坐标；仅匹配位置名段恰好为 StandbyPosition 的键。
        /// </summary>
        private double? TryGetEditorStandbyPositionAxis(string axisName)
        {
            return TryGetEditorNamedPositionAxis(EditorStandbyPositionName, axisName);
        }

        /// <summary>从位置编辑器读取固定位置名的轴坐标（精确匹配位置名段，避免后缀误匹配）。</summary>
        private double? TryGetEditorNamedPositionAxis(string editorPositionName, string axisName)
        {
            if (_allPositions == null || string.IsNullOrEmpty(editorPositionName) || string.IsNullOrEmpty(axisName))
                return null;

            var plainKey = $"{editorPositionName}.{axisName}";
            if (_allPositions.TryGetValue(plainKey, out var plainVal))
                return plainVal;

            foreach (var kvp in _allPositions)
            {
                var parts = kvp.Key.Split('.');
                if (parts.Length >= 3
                    && string.Equals(parts[^2], editorPositionName, StringComparison.Ordinal)
                    && string.Equals(parts[^1], axisName, StringComparison.Ordinal))
                {
                    return kvp.Value;
                }
            }
            return null;
        }

        private string ResolvePositionKey(string positionName, string axisName)
        {
            if (string.IsNullOrEmpty(positionName)) return null;
            var exactKey = $"{positionName}.{axisName}";
            if (_allPositions.ContainsKey(exactKey))
                return exactKey;
            var suffix = $".{positionName}.{axisName}";
            foreach (var key in _allPositions.Keys)
            {
                if (key.EndsWith(suffix))
                    return key;
            }
            return exactKey;
        }

        private string MigratePositionName(string oldName)
        {
            if (string.IsNullOrEmpty(oldName)) return oldName;
            var parts = oldName.Split('.');
            if (parts.Length >= 2)
            {
                var testKey = $"{oldName}.Dx";
                if (_allPositions.ContainsKey(testKey))
                    return oldName;
            }
            foreach (var key in _allPositions.Keys)
            {
                var keyParts = key.Split('.');
                if (keyParts.Length >= 3 && keyParts[1] == oldName)
                    return $"{keyParts[0]}.{oldName}";
            }
            return oldName;
        }

        /// <summary>
        /// 拍照位名称规范化：界面与持久化只保留位置名段（如 NewPosition1），不带工站前缀。
        /// 解析坐标时由 ResolvePositionKey 后缀匹配 {Station}.NewPosition1。
        /// </summary>
        private static string NormalizePhotoPositionName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;
            var trimmed = name.Trim();
            var parts = trimmed.Split('.');
            return parts.Length >= 2 ? parts[^1] : trimmed;
        }

        /// <summary>开始行级运动前创建/重置取消令牌</summary>
        private CancellationToken BeginRowMotion()
        {
            _rowMotionCts?.Cancel();
            _rowMotionCts?.Dispose();
            _rowMotionCts = new CancellationTokenSource();
            StopRowMotionCommand?.RaiseCanExecuteChanged();
            return _rowMotionCts.Token;
        }

        /// <summary>停止当前行级运动：取消令牌并下发各轴 Stop</summary>
        private void StopRowMotion(PhotoPositionRow row)
        {
            _rowMotionCts?.Cancel();
            try
            {
                var axisIdMap = ResolveAxisIdMap();
                string[] axes = { "Dz₁", "Dz₂", "Dz₃", "Dx", "Dy", "Y", "Rx", "Rz" };
                foreach (var axisName in axes)
                {
                    if (axisIdMap.TryGetValue(axisName, out var axisId))
                        _motionService.StopAxis(axisId);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_StopRowMotionFailed",
                    "[VisionCapture] 停止行运动失败: {0}"), ex.Message));
            }
            StatusMessage = L("VisionCapture_Status_MotionStopped");
            _logger.Info(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_RowMotionStopped",
                "[VisionCapture] 用户停止行运动: {0}"), row?.PositionName ?? "-"));
        }

        private void EndRowMotion(PhotoPositionRow row)
        {
            if (row != null) row.IsExecuting = false;
            IsExecuting = false;
            StopRowMotionCommand?.RaiseCanExecuteChanged();
            RaiseCanExecuteChanged();
        }

        private async Task<Dictionary<string, double>> MergeAllPositionsAsync()
        {
            var merged = new Dictionary<string, double>();
            var stations = _stationRegistry.GetAllStations();
            foreach (var station in stations)
            {
                try
                {
                    var positions = await _positionProvider.GetPositionsAsync(station.StationIdentifier);
                    if (positions == null) continue;
                    foreach (var kvp in positions)
                    {
                        var prefixedKey = $"{station.StationIdentifier}.{kvp.Key}";
                        if (!merged.ContainsKey(prefixedKey))
                            merged[prefixedKey] = kvp.Value;
                    }
                }
                catch
                {
                }
            }
            return merged;
        }

        private void LoadConnections()
        {
            AvailableConnections.Clear();
            foreach (var name in _tcpEventService.GetServerNames())
                AvailableConnections.Add(name);
            foreach (var name in _tcpClientManager.Clients.Keys)
                AvailableConnections.Add(name);
        }

        private Dictionary<string, int> ResolveAxisIdMap()
        {
            var axisConfigs = _motionService.GetAxisConfigurations();
            var result = new Dictionary<string, int>();
            string[] axisNames = { "Dx", "Dy", "Dz₁", "Dz₂", "Dz₃", "Y", "Rx", "Rz" };
            string[] mapKeys = { "Dx", "Dy", "Dz₁", "Dz₂", "Dz₃", "Y", "Rx", "Rz" };

            for (int i = 0; i < axisNames.Length; i++)
            {
                var config = axisConfigs.FirstOrDefault(a => a.Name == axisNames[i]);
                if (config != null)
                    result[mapKeys[i]] = config.LogicalId;
            }
            return result;
        }

        /// <summary>
        /// 拍照/移动前优先抬起 Z 轴到位置编辑器固定 SafePosition 各自高度（多轴同步）。
        /// 非 0 校验只针对当前拍照位；安全高度只从 SafePosition 读取，不使用 SafePositionName 配置。
        /// </summary>
        /// <returns>true=抬升成功，调用方可继续；false=无轴可抬或运动失败，调用方应中止</returns>
        /// <param name="photoPositionName">当前拍照位名称；为空时抬升所有已配置 Z 轴</param>
        private async Task<bool> RaiseZAxesToSafeAsync(
            Dictionary<string, int> axisIdMap,
            double speed,
            CancellationToken token,
            string photoPositionName = null)
        {
            if (axisIdMap == null) return false;

            var moves = new List<(int axisId, double position, double velocity)>();
            string[] zAxes = { "Dz₁", "Dz₂", "Dz₃" };
            foreach (var axisName in zAxes)
            {
                if (!axisIdMap.TryGetValue(axisName, out var axisId))
                    continue;

                // 非0检查仅针对当前拍照位：该轴在拍照位未示教(0/缺失)则不抬该轴
                if (!string.IsNullOrEmpty(photoPositionName)
                    && !TryGetTaughtPosition(photoPositionName, axisName).HasValue)
                {
                    _logger.Info(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_SkipRaiseZAxis",
                        "[VisionCapture] 拍照位 '{0}' 的 {1} 未示教(0)，跳过抬升"), photoPositionName, axisName));
                    continue;
                }

                // 只从位置编辑器固定 SafePosition 读取安全高度（允许 0）
                var safeZ = TryGetEditorSafePositionAxis(axisName);
                if (!safeZ.HasValue)
                {
                    _logger.Warn(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_SafeZKeyMissing",
                        "[VisionCapture] 位置编辑器 SafePosition 缺少 {0} 键，无法抬升"), axisName));
                    return false;
                }
                moves.Add((axisId, safeZ.Value, speed));
            }

            if (moves.Count == 0)
            {
                _logger.Warn(_localizationService.GetResourceOrDefault("VisCap_Log_NoZAxisToRaise",
                    "[VisionCapture] 无需要抬升的 Z 轴（拍照位未示教或轴未配置）"));
                return false;
            }

            try
            {
                await _motionService.MoveAbsMultiAxisAsync(moves, token);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_RaiseZAxesFailed",
                    "[VisionCapture] SafePosition Z 轴抬升失败: {0}"), ex.Message));
                return false;
            }
        }

        private int ResolveCoordId()
        {
            var axisConfigs = _motionService.GetAxisConfigurations();
            var dxConfig = axisConfigs.FirstOrDefault(a => a.Name == "Dx");
            if (dxConfig == null)
            {
                _logger.Warn(_localizationService.GetResourceOrDefault("VisCap_Log_DxAxisConfigNotFound", "[VisionCapture] 未找到Dx轴配置，CoordId 回退到 0"));
                return 0;
            }

            // 从 hwcfg.xml 的 InterpolationSystems 查找含有 Dx 轴的插补系
            var systems = _axisParameterService.LoadInterpolationSystems().ToList();
            foreach (var sys in systems)
            {
                // sys.Axes 格式: "actCardId-actAxisId"
                // dxConfig.AxisId 是 actAxisId
                foreach (var axisEntry in sys.Axes)
                {
                    var parts = axisEntry.Split('-');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int actAxisId))
                    {
                        if (actAxisId == dxConfig.AxisId)
                        {
                            _logger.Info(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_DxMatchedInterpSystem", "[VisionCapture] Dx(actAxisId={0}) 匹配插补系 CoordId={1}"), dxConfig.AxisId, sys.CoordId));
                            return sys.CoordId;
                        }
                    }
                }
            }

            _logger.Warn(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_DxNotInInterpSystem", "[VisionCapture] Dx(actAxisId={0}) 不在任何插补系中，CoordId 回退到 0"), dxConfig.AxisId));
            return 0;
        }

        /// <summary>
        /// 统一偏移：相机针头间距 + 校针补偿（ViewModel 级 NeedleOffset）。
        /// XY 工艺补偿已移至 DotParams/ArcParams，不参与 Mech 公式。
        /// </summary>
        private ((double X, double Y) CamDist, (double X, double Y) NeedleCalib) GetVisionCaptureNeedleOffsets()
        {
            return ((CameraNeedleDistanceX, CameraNeedleDistanceY), (NeedleOffsetX, NeedleOffsetY));
        }

        /// <summary>本次拍照结果是否包含视觉中心 CenterX/Y</summary>
        private bool _captureHasVisionCenter;

        /// <summary>
        /// 解析 targetX/Y：相机中心与目标点的间距。
        /// 若拍照结果含 CenterX/Y，则 target = Point - Center；否则 Point 即为 target 偏移。
        /// </summary>
        private (double X, double Y) ResolveTargetOffset(double pointX, double pointY)
        {
            if (_captureHasVisionCenter)
                return (pointX - ParsedCenterX, pointY - ParsedCenterY);
            return (pointX, pointY);
        }

        /// <summary>
        /// 解析视觉点胶工具分号格式：result;x1,y1;x2,y2;...
        /// parts[0]=结果(1=OK,0=NG)，parts[1..]=点坐标 X,Y（InvariantCulture 解析）。
        /// 例: "1;1.100,4.400;2.200,5.500" → OK, 点(1.1,4.4)(2.2,5.5)
        /// </summary>
        private bool TryParseSemicolonPointsFormat(string raw, out bool isOk, out List<(double X, double Y)> points)
        {
            isOk = false;
            points = new List<(double X, double Y)>();
            if (string.IsNullOrWhiteSpace(raw)) return false;

            // 按分号分割；首段必须为纯整数结果码才认定为该格式
            var parts = raw.Trim().Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 1) return false;
            if (!int.TryParse(parts[0].Trim(), out var resultCode)) return false;

            // 结果码仅允许 0/1；其它值不认定为该格式（避免误匹配旧 key=value）
            if (resultCode != 0 && resultCode != 1) return false;

            isOk = resultCode == 1;
            var inv = System.Globalization.CultureInfo.InvariantCulture;

            // 解析后续段为 X,Y 点对
            for (int i = 1; i < parts.Length; i++)
            {
                var xy = parts[i].Split(',');
                if (xy.Length < 2) continue;
                if (!double.TryParse(xy[0].Trim(), System.Globalization.NumberStyles.Float, inv, out var x)) continue;
                if (!double.TryParse(xy[1].Trim(), System.Globalization.NumberStyles.Float, inv, out var y)) continue;
                points.Add((x, y));
            }
            return true;
        }

        /// <summary>
        /// 解析相机返回的 Type + 目标点集（N / P1X..PnY）。兼容旧 CenterX+P1..P3 与 offsetX/Y 单点。
        /// 优先解析视觉点胶工具的分号格式：result;x1,y1;x2,y2;...（1=OK,0=NG）。
        /// 当默认解析器因 Type=字符串 丢弃数值时，从 RawResponse 回退提取。
        /// </summary>
        private bool TryParseTargetPoints(Dictionary<string, double> pd, string rawResponse = null)
        {
            CapturedTargetPoints.Clear();
            _captureHasVisionCenter = false;
            pd ??= new Dictionary<string, double>();

            // 0) 优先：视觉点胶工具分号格式 result;x,y;x,y;...
            if (TryParseSemicolonPointsFormat(rawResponse, out var semicolonOk, out var semicolonPoints))
            {
                IsLastCaptureOk = semicolonOk;
                if (!semicolonOk)
                {
                    _logger.Warn(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_CaptureResultNG",
                        "[VisionCapture] 视觉返回结果 NG(0)，原始: {0}"), rawResponse));
                }
                // 分号格式不再自动检测轨迹类型，完全由用户 TrajectoryOverride 决定
                for (int i = 0; i < semicolonPoints.Count; i++)
                {
                    CapturedTargetPoints.Add(new TargetPointItem
                    {
                        Index = i + 1,
                        PointX = semicolonPoints[i].X,
                        PointY = semicolonPoints[i].Y
                    });
                }
                // 兼容旧 UI：填充 P1..P3 显示字段
                if (semicolonPoints.Count >= 1) { Point1X = ParsedP1X = semicolonPoints[0].X; Point1Y = ParsedP1Y = semicolonPoints[0].Y; }
                if (semicolonPoints.Count >= 2) { Point2X = ParsedP2X = semicolonPoints[1].X; Point2Y = ParsedP2Y = semicolonPoints[1].Y; }
                if (semicolonPoints.Count >= 3) { Point3X = ParsedP3X = semicolonPoints[2].X; Point3Y = ParsedP3Y = semicolonPoints[2].Y; }
                SyncDotArcModeFromEffectiveType();
                RaisePropertyChanged(nameof(HasParsedArcData));
                return semicolonPoints.Count > 0;
            }

            // 若字典为空但有原始响应，尝试从 RawResponse 提取数值键（跳过 Type 等非数值）
            if (pd.Count == 0 && !string.IsNullOrEmpty(rawResponse))
                pd = ExtractNumericPairsFromRaw(rawResponse);

            if (pd.Count == 0 && string.IsNullOrEmpty(rawResponse)) return false;

            // 检测视觉中心：CenterX/Y 可与 PnX 同包返回，用于计算 target = Point - Center
            if (TryGetIgnoreCase(pd, "CenterX", out var centerXVal))
            {
                TryGetIgnoreCase(pd, "CenterY", out var centerYVal);
                ParsedCenterX = centerXVal;
                ParsedCenterY = centerYVal;
                VisionCenterX = centerXVal;
                VisionCenterY = centerYVal;
                _captureHasVisionCenter = true;
            }

            // 轨迹类型不再从相机数据解析，完全由用户 TrajectoryOverride 决定
            var points = new List<(double X, double Y)>();

            // 1) 新格式：N 或顺序发现 PnX/PnY（不要求 CenterX）
            int maxN = 0;
            if (TryGetIgnoreCase(pd, "N", out var nVal) && nVal >= 1)
                maxN = (int)nVal;
            else
            {
                for (int i = 1; i <= 64; i++)
                {
                    if (TryGetIgnoreCase(pd, $"P{i}X", out _) || TryGetIgnoreCase(pd, $"P{i}Y", out _))
                        maxN = i;
                    else if (maxN > 0)
                        break;
                }
            }

            if (maxN > 0)
            {
                for (int i = 1; i <= maxN; i++)
                {
                    TryGetIgnoreCase(pd, $"P{i}X", out var px);
                    TryGetIgnoreCase(pd, $"P{i}Y", out var py);
                    points.Add((px, py));
                }
            }

            // 2) 旧格式兼容：CenterX + P1X → 取 P1..P3
            if (points.Count == 0 && TryGetIgnoreCase(pd, "CenterX", out var cxLegacy) && TryGetIgnoreCase(pd, "P1X", out _))
            {
                TryGetIgnoreCase(pd, "CenterY", out var cyLegacy);
                ParsedCenterX = cxLegacy; ParsedCenterY = cyLegacy;
                VisionCenterX = cxLegacy; VisionCenterY = cyLegacy;
                _captureHasVisionCenter = true;
                for (int i = 1; i <= 3; i++)
                {
                    if (!TryGetIgnoreCase(pd, $"P{i}X", out var px)) break;
                    TryGetIgnoreCase(pd, $"P{i}Y", out var py);
                    points.Add((px, py));
                }
            }

            // 3) 单点：targetX/targetY（相机中心与目标点间距）或 offsetX/offsetY / X/Y
            if (points.Count == 0)
            {
                double ox = 0, oy = 0;
                bool has = TryGetIgnoreCase(pd, "targetX", out ox)
                    || TryGetIgnoreCase(pd, "offsetX", out ox)
                    || TryGetIgnoreCase(pd, "X", out ox);
                bool hasY = TryGetIgnoreCase(pd, "targetY", out oy)
                    || TryGetIgnoreCase(pd, "offsetY", out oy)
                    || TryGetIgnoreCase(pd, "Y", out oy);
                if (has || hasY)
                    points.Add((ox, oy));
            }

            if (points.Count == 0) return false;

            // 不再用相机 Type / 点数推断驱动有效轨迹；完全由用户 Override 决定
            for (int i = 0; i < points.Count; i++)
            {
                CapturedTargetPoints.Add(new TargetPointItem
                {
                    Index = i + 1,
                    PointX = points[i].X,
                    PointY = points[i].Y
                });
            }

            // 兼容旧 UI：填充 P1..P3 显示字段
            if (points.Count >= 1) { Point1X = ParsedP1X = points[0].X; Point1Y = ParsedP1Y = points[0].Y; }
            if (points.Count >= 2) { Point2X = ParsedP2X = points[1].X; Point2Y = ParsedP2Y = points[1].Y; }
            if (points.Count >= 3) { Point3X = ParsedP3X = points[2].X; Point3Y = ParsedP3Y = points[2].Y; }

            SyncDotArcModeFromEffectiveType();
            RaisePropertyChanged(nameof(HasParsedArcData));
            return true;
        }

        /// <summary>忽略大小写从字典取值</summary>
        private static bool TryGetIgnoreCase(Dictionary<string, double> pd, string key, out double value)
        {
            if (pd.TryGetValue(key, out value)) return true;
            foreach (var kv in pd)
            {
                if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = kv.Value;
                    return true;
                }
            }
            value = 0;
            return false;
        }

        /// <summary>
        /// 从原始响应提取可解析为 double 的 key=value（跳过 Type=Dot 等非数值，避免默认解析器整包失败）
        /// </summary>
        private static Dictionary<string, double> ExtractNumericPairsFromRaw(string raw)
        {
            var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(raw)) return result;
            var matches = System.Text.RegularExpressions.Regex.Matches(
                raw, @"([A-Za-z_][A-Za-z0-9_]*)\s*=\s*([-+]?[0-9]*\.?[0-9]+(?:[eE][-+]?[0-9]+)?)");
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                if (double.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var v))
                    result[m.Groups[1].Value] = v;
            }
            return result;
        }

        /// <summary>
        /// 对 CapturedTargetPoints 计算 Mech/MachinePoints，生成预览几何；不自动切换步骤，由用户手动点击下一步。
        /// 公式：Mech = PhotoDx/Dy + targetX/Y + 相机针头间距 + 校针补偿 + OFFSET
        /// </summary>
        private async Task ComputeMachinePointsFromCameraAsync(PhotoPositionRow row)
        {
            try
            {
                _allPositions = await MergeAllPositionsAsync();
                RefreshRowParsedCoordinates(row);

                double photoDx = row?.Dx ?? 0;
                double photoDy = row?.Dy ?? 0;
                if (row != null && !string.IsNullOrEmpty(row.PositionName))
                {
                    photoDx = TryGetTaughtPosition(row.PositionName, "Dx") ?? 0;
                    photoDy = TryGetTaughtPosition(row.PositionName, "Dy") ?? 0;
                }
                PhotoDx = photoDx;
                PhotoDy = photoDy;

                // 配置兼容：ArcNeedle* 与统一 NeedleOffset 同步显示
                ArcNeedleOffsetX = NeedleOffsetX;
                ArcNeedleOffsetY = NeedleOffsetY;

                var (camDist, needleCalib) = GetVisionCaptureNeedleOffsets();
                var rowOffset = (X: row?.CalculatedOffsetX ?? 0, Y: row?.CalculatedOffsetY ?? 0);

                var targetOffsets = CapturedTargetPoints
                    .Select(p => ResolveTargetOffset(p.PointX, p.PointY))
                    .ToList();
                var mechEndpoints = BezierArcDispenseService.ApplyVisionCaptureTransform(
                    (photoDx, photoDy), targetOffsets, camDist, needleCalib, rowOffset);
                // Arc/Line 按贝塞尔/直线离散，预览曲线与点胶路径一致
                var mech = BuildTrajectoryMachinePoints(row, mechEndpoints);
                // 标注点按实际执行顺序取首/中/尾，确保预览方向与点胶方向一致。
                var keyPoints = BuildTrajectoryKeyPoints(mech);

                for (int i = 0; i < CapturedTargetPoints.Count && i < mechEndpoints.Count; i++)
                {
                    CapturedTargetPoints[i].MechX = mechEndpoints[i].X;
                    CapturedTargetPoints[i].MechY = mechEndpoints[i].Y;
                }

                if (targetOffsets.Count > 0)
                {
                    TargetDeltaX = targetOffsets[0].X;
                    TargetDeltaY = targetOffsets[0].Y;
                }

                MachinePoints.Clear();
                for (int i = 0; i < mech.Count; i++)
                    MachinePoints.Add(new MachinePointItem { Index = i + 1, X = mech[i].X, Y = mech[i].Y });
                RaisePropertyChanged(nameof(HasMachinePoints));

                if (mechEndpoints.Count >= 1)
                {
                    P1MechX = mechEndpoints[0].X; P1MechY = mechEndpoints[0].Y;
                    FinalX = mech[0].X; FinalY = mech[0].Y;
                }
                if (mechEndpoints.Count >= 2) { P2MechX = mechEndpoints[1].X; P2MechY = mechEndpoints[1].Y; }
                if (mechEndpoints.Count >= 3) { P3MechX = mechEndpoints[2].X; P3MechY = mechEndpoints[2].Y; }
                BezierPointCount = mech.Count;

                GenerateArcPathGeometry(mech, keyPoints);

                StatusMessage = string.Format(L("VisionCapture_Status_VisionDataReady"), mech.Count);
                _logger.Info(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_ArcMachineCoordComplete",
                    "[VisionCapture] 目标点机械坐标完成: 点数={0} Type={1} 首点({2:F3},{3:F3})"),
                    mech.Count, EffectiveTrajectoryType, FinalX, FinalY));
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_AutoCalcArcCoordFailed",
                    "[VisionCapture] 自动计算机械坐标失败: {0}"), ex.Message));
            }
        }

        /// <summary>
        /// 新增一个拍照位（自动命名 PhotoPosN，避免重名），并设为当前选中行。
        /// </summary>
        private void AddPhotoPosition()
        {
            int n = PhotoPositionRows.Count + 1;
            string name = "PhotoPos" + n;
            while (PhotoPositionRows.Any(r => r.PositionName == name))
            {
                n++;
                name = "PhotoPos" + n;
            }
            var row = new PhotoPositionRow(name);
            // 继承当前选中行的连接/触发/超时等通用配置，减少重复输入
            if (SelectedRow != null)
            {
                row.ConnectionName = SelectedRow.ConnectionName;
                row.TriggerCommand = SelectedRow.TriggerCommand;
                row.Timeout = SelectedRow.Timeout;
                row.Speed = SelectedRow.Speed;
                row.DispenseType = SelectedRow.DispenseType;
                row.TrajectoryOverride = SelectedRow.TrajectoryOverride;
            }
            PhotoPositionRows.Add(row);
            SelectedRow = row;
            RefreshRowParsedCoordinates(row);
            SyncSiteFeatureNamesFromRows();
            // 同步缓存当前组
            if (!string.IsNullOrEmpty(SelectedGroup))
                CacheCurrentGroupRows(SelectedGroup);
            _logger.Info(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_AddPhotoPos", "[VisionCapture] 新增拍照位: {0}"), name));
        }

        /// <summary>
        /// 删除指定拍照位；若删除的是当前选中行则回退选中最后一行。
        /// </summary>
        private void DeletePhotoPosition(PhotoPositionRow row)
        {
            if (row == null) return;
            var name = row.PositionName;
            PhotoPositionRows.Remove(row);
            if (SelectedRow == row)
                SelectedRow = PhotoPositionRows.LastOrDefault();
            SyncSiteFeatureNamesFromRows();
            if (!string.IsNullOrEmpty(SelectedGroup))
                CacheCurrentGroupRows(SelectedGroup);
            _logger.Info(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_DeletePhotoPos", "[VisionCapture] 删除拍照位: {0}"), name));
        }

        private async Task ExecuteCaptureAsync(PhotoPositionRow row)
        {
            if (row == null || row.IsExecuting || IsExecuting) return;

            // 安全拦截：位置名不存在于位置编辑器时禁止拍照触发
            // （拍照位坐标为 0 会导致后续点胶坐标错误，存在碰撞/误点胶风险）
            if (row.IsPositionInvalid)
            {
                StatusMessage = string.Format(L("VisionCapture_Status_MoveBlockedInvalidPos"), row.PositionName);
                _logger.Warn(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_MoveBlockedInvalidPos",
                    "[VisionCapture] 拍照被阻止: 位置 '{0}' 在位置编辑器中不存在"), row.PositionName));
                return;
            }

            IsExecuting = true;
            row.IsExecuting = true;
            StatusMessage = L("VisionCapture_Status_PhotoExecuting");
            RaiseCanExecuteChanged();

            var cts = BeginRowMotion();
            try
            {
                var result = await _visionCaptureService.ExecuteCaptureAsync(
                    row.ConnectionName, row.TriggerCommand, row.Timeout, cts);

                RawResponse = result.RawResponse ?? L("VisionCapture_DataReceivedSuccess");
                var pd = result.ParsedData;

                // 解析 Type + 目标点集（含旧格式兼容；字典为空时从 RawResponse 回退）
                bool parsed = TryParseTargetPoints(pd, result.RawResponse);

                // 展示用：若默认解析器因 Type=字符串失败，用回退提取的数值填充
                if (pd.Count == 0 && CapturedTargetPoints.Count > 0)
                    pd = ExtractNumericPairsFromRaw(result.RawResponse);

                ParsedData = new ObservableCollection<KeyValuePair<string, double>>(pd);
                StatusMessage = string.Format(L("VisionCapture_Status_PhotoComplete"), pd.Count);

                if (parsed)
                {
                    await ComputeMachinePointsFromCameraAsync(row);
                }
                else
                {
                    // 解析失败：仍尝试把 offset 写入行（兼容旧链路），再预览
                    if (pd.TryGetValue("offsetX", out var ox)) row.NeedleOffsetX = ox;
                    else if (pd.TryGetValue("X", out var px)) row.NeedleOffsetX = px;
                    if (pd.TryGetValue("offsetY", out var oy)) row.NeedleOffsetY = oy;
                    else if (pd.TryGetValue("Y", out var py)) row.NeedleOffsetY = py;

                    var calcOffsetX = row.CalculatedOffsetX;
                    var calcOffsetY = row.CalculatedOffsetY;

                    if (IsNeedleOffsetXLinked)
                        await UpdateGlobalVariableValueAsync(NeedleOffsetXLinkedVar, calcOffsetX);
                    if (IsNeedleOffsetYLinked)
                        await UpdateGlobalVariableValueAsync(NeedleOffsetYLinkedVar, calcOffsetY);

                    if (IsNeedleOffsetXLinked)
                        NeedleOffsetX = ReadLinkedVariableValue(NeedleOffsetXLinkedVar);
                    if (IsNeedleOffsetYLinked)
                        NeedleOffsetY = ReadLinkedVariableValue(NeedleOffsetYLinkedVar);

                    NotifyTargetOffsetChanged();

                    await PreviewMachinePointsAsync();
                }
            }
            catch (OperationCanceledException)
            {
                try
                {
                    _allPositions = await MergeAllPositionsAsync();
                    var axisIdMap = ResolveAxisIdMap();
                    // 取消后按当前拍照位已示教的 Z 轴抬回安全高度
                    await RaiseZAxesToSafeAsync(axisIdMap, row.Speed, CancellationToken.None, row.PositionName);
                }
                catch { }
                StatusMessage = L("VisionCapture_PhotoCancelled");
            }
            catch (RecoverableException)
            {
                StatusMessage = L("VisionCapture_Status_VisionFault");
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(L("VisionCapture_Status_ErrorFormat"), ex.Message);
                _logger.Error(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_ExecuteFailed", "[VisionCapture] 执行失败: {0}"), ex.Message));
            }
            finally
            {
                EndRowMotion(row);
            }
        }

        /// <summary>
        /// 返回待机位（统一动作序列）：
        /// 1. 按当前拍照位已示教的 Dz₁/Dz₂/Dz₃ 抬起到 SafePosition（非 0 仅校验拍照位）
        /// 2. Dx、Dy 插补移至待机位（StandbyPosition），Y 轴不动
        /// </summary>
        /// <param name="photoPositionName">当前拍照位；用于决定抬升哪些 Z 轴</param>
        private async Task ReturnToSafePositionAsync(
            Dictionary<string, int> axisIdMap,
            int coordId,
            double speed,
            CancellationToken token,
            string photoPositionName = null)
        {
            if (axisIdMap == null) return;

            // Safe/Standby 从位置编辑器固定位置读取（允许 0，仅要求键存在）
            var dz1Safe = TryGetEditorSafePositionAxis("Dz₁");
            var standbyX = TryGetEditorStandbyPositionAxis("Dx");
            var standbyY = TryGetEditorStandbyPositionAxis("Dy");

            if (!dz1Safe.HasValue)
            {
                _logger.Warn(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_ReturnToSafeDz1Missing",
                    "[VisionCapture] 返回待机位失败: 位置编辑器 SafePosition 缺少 Dz₁ 轴配置")));
                return;
            }

            if (!standbyX.HasValue || !standbyY.HasValue)
            {
                _logger.Warn(_localizationService.GetResourceOrDefault("VisCap_Log_ReturnToStandbyPosDataIncomplete",
                    "[VisionCapture] 返回待机位失败: 位置编辑器 StandbyPosition 缺少 Dx/Dy 配置"));
                return;
            }

            if (!axisIdMap.TryGetValue("Dx", out var dxId) || !axisIdMap.TryGetValue("Dy", out var dyId))
            {
                _logger.Warn(_localizationService.GetResourceOrDefault("VisCap_Log_ReturnToStandbyAxisMissing",
                    "[VisionCapture] 返回待机位失败: 未找到 Dx/Dy 轴配置"));
                return;
            }

            // 步骤1：按拍照位已示教 Z 轴抬升到位置编辑器 SafePosition
            if (!await RaiseZAxesToSafeAsync(axisIdMap, speed, token, photoPositionName))
                return;

            // 步骤2：Dx/Dy 插补到待机位（Y 轴不动）
            await _motionService.MoveLineAbsAsync(coordId,
                new[] { dxId, dyId },
                new[] { standbyX.Value, standbyY.Value }, speed, token);

            _logger.Info(_localizationService.GetResourceOrDefault("VisCap_Log_ReturnedToStandbyPos",
                "[VisionCapture] 已返回待机位（Z 抬升 + Dx/Dy 待机，Y 不动）"));
        }

        /// <summary>
        /// Safe 列按钮入口：以指定行速度执行 ReturnToSafePositionAsync（抬 Z → Dx/Dy 回待机位，Y 不动）
        /// </summary>
        private async Task ReturnToSafeFromRowAsync(PhotoPositionRow row)
        {
            if (row == null || row.IsExecuting || IsExecuting) return;

            IsExecuting = true;
            row.IsExecuting = true;
            StatusMessage = L("VisionCapture_Status_ReturningToSafe");
            RaiseCanExecuteChanged();

            var cts = BeginRowMotion();
            try
            {
                _allPositions = await MergeAllPositionsAsync();
                var axisIdMap = ResolveAxisIdMap();
                var coordId = ResolveCoordId();

                await ReturnToSafePositionAsync(axisIdMap, coordId, row.Speed, cts, row.PositionName);

                StatusMessage = L("VisionCapture_Status_ReturnedToSafe");
            }
            catch (OperationCanceledException)
            {
                StatusMessage = L("VisionCapture_ReturnCancelled");
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(L("VisionCapture_Status_MoveFail"), ex.Message);
                _logger.Error(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_ReturnToSafeFailed", "[VisionCapture] 返回待机位失败: {0}"), ex.Message));
            }
            finally
            {
                EndRowMotion(row);
            }
        }

        private async Task MoveToTeachPositionAsync(PhotoPositionRow row)
        {
            if (row == null || row.IsExecuting || IsExecuting) return;

            // 安全拦截：必需轴 Dx/Dy/Dz₃ 未获取（含值为 0）时禁止运动
            if (row.IsPositionInvalid)
            {
                StatusMessage = string.Format(L("VisionCapture_Status_MoveBlockedInvalidPos"), row.PositionName);
                return;
            }

            var confirmed = await ShowConfirmationAsync(
                L("VisionCapture_MoveConfirmTitle"),
                string.Format(L("VisionCapture_MoveConfirmMessage"), row.SiteFeatureName));
            if (!confirmed) return;

            IsExecuting = true;
            row.IsExecuting = true;
            StatusMessage = L("VisionCapture_Status_MovingToTeach");
            RaiseCanExecuteChanged();

            var cts = BeginRowMotion();
            try
            {
                _allPositions = await MergeAllPositionsAsync();
                var axisIdMap = ResolveAxisIdMap();
                var coordId = ResolveCoordId();

                // 重新读取示教值：0 / 缺失一律按未获取，禁止下发
                var targetX = TryGetTaughtPosition(row.PositionName, "Dx");
                var targetY = TryGetTaughtPosition(row.PositionName, "Dy");
                var photoDz3 = TryGetTaughtPosition(row.PositionName, "Dz₃");
                if (!targetX.HasValue || !targetY.HasValue || !photoDz3.HasValue)
                {
                    StatusMessage = string.Format(L("VisionCapture_Status_MoveBlockedInvalidPos"), row.PositionName);
                    _logger.Warn(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_TeachPosIncomplete",
                        "[VisionCapture] 移动被阻止: '{0}' 缺少有效 Dx/Dy/Dz₃"), row.PositionName));
                    return;
                }

                if (!axisIdMap.TryGetValue("Dx", out var dxId)
                    || !axisIdMap.TryGetValue("Dy", out var dyId)
                    || !axisIdMap.TryGetValue("Dz₃", out var dz3Id))
                {
                    StatusMessage = L("VisionCapture_Status_AxisConfigMissing");
                    _logger.Warn(_localizationService.GetResourceOrDefault("VisCap_Log_TeachAxisMissing",
                        "[VisionCapture] 移动失败: 未找到 Dx/Dy/Dz₃ 轴配置"));
                    return;
                }

                // 1. 仅抬升当前拍照位已示教(非0)的 Dz₁/Dz₂/Dz₃ 到位置编辑器 SafePosition
                if (!await RaiseZAxesToSafeAsync(axisIdMap, row.Speed, cts, row.PositionName))
                {
                    StatusMessage = L("VisionCapture_Status_RaiseZFailed");
                    return;
                }

                // 2. Rz / Rx / Y 同时非插补移动到拍照位示教值（已配置轴必须有有效示教）
                var postureMoves = new List<(int axisId, double position, double velocity)>();
                string[] postureAxes = { "Rz", "Rx", "Y" };
                foreach (var axisName in postureAxes)
                {
                    if (!axisIdMap.TryGetValue(axisName, out var axisId))
                        continue;
                    var taught = TryGetTaughtPosition(row.PositionName, axisName);
                    if (!taught.HasValue)
                    {
                        StatusMessage = string.Format(L("VisionCapture_Status_AxisTeachMissing"), axisName, row.PositionName);
                        _logger.Warn(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_PostureAxisMissing",
                            "[VisionCapture] 移动被阻止: '{0}' 的 {1} 未获取到有效位置"), row.PositionName, axisName));
                        return;
                    }
                    postureMoves.Add((axisId, taught.Value, row.Speed));
                }
                if (postureMoves.Count > 0)
                    await _motionService.MoveAbsMultiAxisAsync(postureMoves, cts);

                // 3. Dx/Dy 插补移动到拍照位
                await _motionService.MoveLineAbsAsync(coordId,
                    new[] { dxId, dyId },
                    new[] { targetX.Value, targetY.Value }, row.Speed, cts);

                // 4. Dz₃（相机 Z）下降到拍照高度
                await _motionService.MoveAbsAsync(dz3Id, photoDz3.Value, row.Speed, cts);

                StatusMessage = string.Format(L("VisionCapture_Status_MoveComplete"), targetX.Value, targetY.Value);
                _logger.Info(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_MoveComplete",
                    "[VisionCapture] 移动完成 [{0}]: Dx={1:F3}, Dy={2:F3}, Dz₃={3:F3}"),
                    row.SiteFeatureName, targetX.Value, targetY.Value, photoDz3.Value));
            }
            catch (OperationCanceledException)
            {
                StatusMessage = L("VisionCapture_MoveCancelled");
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(L("VisionCapture_Status_MoveFail"), ex.Message);
                _logger.Error(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_MoveFailed", "[VisionCapture] 移动失败: {0}"), ex.Message));
            }
            finally
            {
                EndRowMotion(row);
            }
        }

        private async Task ExecuteDispenseAsync()
        {
            if (SelectedRow == null || CapturedTargetPoints.Count == 0 || IsExecuting) return;

            // 安全拦截：位置名不存在于位置编辑器时禁止点胶（拍照位坐标为 0 会导致点胶坐标错误）
            if (SelectedRow.IsPositionInvalid)
            {
                StatusMessage = string.Format(L("VisionCapture_Status_MoveBlockedInvalidPos"), SelectedRow.PositionName);
                return;
            }

            // 安全拦截：视觉返回 NG(0) 时禁止点胶
            if (!IsLastCaptureOk)
            {
                StatusMessage = L("VisionCapture_Status_CaptureNGBlocked");
                _logger.Warn(_localizationService.GetResourceOrDefault("VisCap_Log_DispenseBlockedByNG",
                    "[VisionCapture] 点胶已阻止: 视觉返回结果 NG"));
                return;
            }

            IsExecuting = true;
            SelectedRow.IsExecuting = true;
            StatusMessage = L("VisionCapture_Status_Dispensing");
            RaiseCanExecuteChanged();

            _dispenseCts = new CancellationTokenSource();
            var token = _dispenseCts.Token;
            bool dryRun = CurrentRunMode == RunMode.DryRun;
            // 双针头系统：使用当前选中针头索引（0=针头1/Dz₂, 1=针头2/Dz₃）
            int needleIndex = CurrentNeedleIndex;

            try
            {
                _allPositions = await MergeAllPositionsAsync();
                RefreshRowParsedCoordinates(SelectedRow);

                // 安全前置：点胶运动前必须先将 Dz₁/Dz₂/Dz₃ 全部抬升至位置编辑器 SafePosition。
                // 不依赖手动填写的安全高度（人为输错会撞机），统一从位置编辑器读取的真实安全位抬升。
                var dispenseAxisIdMap = ResolveAxisIdMap();
                if (!await RaiseZAxesToSafeAsync(dispenseAxisIdMap, SelectedRow.Speed, token))
                {
                    StatusMessage = L("VisionCapture_Status_RaiseZFailed");
                    _logger.Warn(_localizationService.GetResourceOrDefault("VisCap_Log_DispenseRaiseZFailed",
                        "[VisionCapture] 点胶前 Z 轴抬升安全位失败，点胶已中止"));
                    return;
                }

                // 按 EffectiveTrajectoryType 分发：Dot→DotDispenseService；Line/Arc/Polyline→路径服务
                if (EffectiveTrajectoryType == TrajectoryType.Dot)
                {
                    await ExecuteDotFlowAsync(dryRun, needleIndex, token);
                    StatusMessage = dryRun ? L("VisionCapture_Status_DotDryRunComplete") : L("VisionCapture_Status_DotDispenseComplete");
                }
                else
                {
                    await ExecutePathFlowAsync(dryRun, needleIndex, token);
                    StatusMessage = dryRun ? L("VisionCapture_Status_ArcDryRunComplete") : L("VisionCapture_Status_ArcDispenseComplete");
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = L("VisionCapture_DispenseCancelled");
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(L("VisionCapture_Status_DispenseError"), ex.Message);
                _logger.Error(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_DispenseFailed", "[VisionCapture] 点胶失败: {0}"), ex.Message));
            }
            finally
            {
                IsPaused = false;
                _pauseEvent.Set();
                SelectedRow.IsExecuting = false;
                IsExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Dot 多点：对 CapturedTargetPoints 逐点做坐标变换，构建 DotPoint 列表后交给 DotDispenseService 顺序执行。
        /// 与预览/路径模式一致，不再只取第一个目标点。
        /// </summary>
        private async Task ExecuteDotFlowAsync(bool dryRun, int needleIndex, CancellationToken token)
        {
            var row = SelectedRow;
            var dotParams = needleIndex == 0 ? row.DotParamsNeedle1 : row.DotParamsNeedle2;
            var (camDist, needleCalib) = GetVisionCaptureNeedleOffsets();
            var rowOffset = (X: row.CalculatedOffsetX, Y: row.CalculatedOffsetY);

            double teachHeight = dotParams.TeachHeight;
            double heightComp = dotParams.HeightCompensation;
            string basePointId = row.PositionName ?? "VisionDot";

            var points = new List<DotPoint>();
            if (CapturedTargetPoints.Count > 0)
            {
                // 与路径模式相同：对每个 Target Point 做拍照位→机械坐标变换
                for (int i = 0; i < CapturedTargetPoints.Count; i++)
                {
                    var pt = CapturedTargetPoints[i];
                    var target = ResolveTargetOffset(pt.PointX, pt.PointY);
                    var (x, y) = BezierArcDispenseService.ComputeMachineForVisionCapture(
                        (PhotoDx, PhotoDy), target, camDist, needleCalib, rowOffset);

                    points.Add(new DotPoint
                    {
                        Group = "VisionCapture",
                        PointId = $"{basePointId}_{i + 1}",
                        Dx = x,
                        Dy = y,
                        Y = row.Y,
                        Rx = row.Rx,
                        Rz = row.Rz,
                        Dz2 = teachHeight,
                        Dz3 = teachHeight,
                        Dz2Compensation = heightComp,
                        Dz3Compensation = heightComp,
                        IsSelected = true,
                        IsEnabled = true
                    });
                }
            }
            else
            {
                // 无视觉目标点时回退到界面上的 FinalX/FinalY（兼容旧单点流程）
                points.Add(new DotPoint
                {
                    Group = "VisionCapture",
                    PointId = basePointId,
                    Dx = FinalX,
                    Dy = FinalY,
                    Y = row.Y,
                    Rx = row.Rx,
                    Rz = row.Rz,
                    Dz2 = teachHeight,
                    Dz3 = teachHeight,
                    Dz2Compensation = heightComp,
                    Dz3Compensation = heightComp,
                    IsSelected = true,
                    IsEnabled = true
                });
            }

            // 界面显示用：更新为首点与末点（末点便于确认全部变换完成）
            FinalX = points[^1].Dx;
            FinalY = points[^1].Dy;

            _logger.Info(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_DotDispenseStart",
                "[VisionCapture] Dot点胶启动: 共{0}点 首点({1:F3},{2:F3}) 末点({3:F3},{4:F3}) 针头{5} 干跑={6}"),
                points.Count, points[0].Dx, points[0].Dy, FinalX, FinalY, needleIndex + 1, dryRun));

            if (dryRun)
                await _dotDispenseService.DryRunAsync(points, dotParams, needleIndex, token);
            else
                await _dotDispenseService.ExecuteDotDispenseAsync(points, dotParams, needleIndex, token);
        }

        /// <summary>
        /// 路径点胶（Line/Arc/Polyline）：ApplyVisionCaptureTransform → DispenseSegment → DispenseExecuteService。
        /// </summary>
        private async Task ExecutePathFlowAsync(bool dryRun, int needleIndex, CancellationToken token)
        {
            var row = SelectedRow;

            if (CapturedTargetPoints.Count < 1)
            {
                StatusMessage = L("VisionCapture_Status_ArcDataMissing");
                _logger.Warn(_localizationService.GetResourceOrDefault("VisCap_Log_ArcDataMissing",
                    "[VisionCapture] 路径模式缺少相机目标点，无法生成轨迹"));
                return;
            }

            var (camDist, needleCalib) = GetVisionCaptureNeedleOffsets();
            var rowOffset = (X: row.CalculatedOffsetX, Y: row.CalculatedOffsetY);
            var targetOffsets = CapturedTargetPoints
                .Select(p => ResolveTargetOffset(p.PointX, p.PointY))
                .ToList();
            var machineEndpoints = BezierArcDispenseService.ApplyVisionCaptureTransform(
                (PhotoDx, PhotoDy), targetOffsets, camDist, needleCalib, rowOffset);
            var machinePoints = BuildTrajectoryMachinePoints(row, machineEndpoints);

            if (machinePoints == null || machinePoints.Count == 0)
            {
                StatusMessage = L("VisionCapture_Status_ArcDataMissing");
                _logger.Warn(_localizationService.GetResourceOrDefault("VisCap_Log_ArcMachinePointsEmpty",
                    "[VisionCapture] 生成的机械坐标点为空，取消点胶"));
                return;
            }

            // EntityType：Arc→Arc，Line/Polyline→Line
            CadEntityType entityType = EffectiveTrajectoryType == TrajectoryType.Arc
                ? CadEntityType.Arc
                : CadEntityType.Line;

            // 按当前针头取路径工艺参数
            var arcParams = needleIndex == 0 ? row.ArcParamsNeedle1 : row.ArcParamsNeedle2;
            if (arcParams == null) arcParams = new DispenseSegment();
            var segment = new DispenseSegment
            {
                SegmentId = row.PositionName ?? "VisionPath",
                EntityType = entityType,
                LayerName = "VisionCapture",
                IsEnabled = true,
                IsSelected = true,
                JumpSpeed = arcParams.JumpSpeed,
                InterpSpeed = arcParams.InterpSpeed,
                MoveSpeed = arcParams.MoveSpeed,
                DispenseAmount = arcParams.DispenseAmount,
                PreDelay = arcParams.PreDelay,
                PostDelay = arcParams.PostDelay,
                EarlyCloseGlueDelayMs = arcParams.EarlyCloseGlueDelayMs,
                CornerDecel = arcParams.CornerDecel,
                TeachHeight = arcParams.TeachHeight,
                HeightCompensation = arcParams.HeightCompensation,
                SafeHeight = arcParams.SafeHeight,
                GlueTriggerOffsetMm = arcParams.GlueTriggerOffsetMm,
                DispenseTime = arcParams.DispenseTime,
                ApproachHeight = arcParams.ApproachHeight,
                DispensingPressure = arcParams.DispensingPressure,
                SuckBackTime = arcParams.SuckBackTime,
                Points = machinePoints.Select(p => new CadPoint { MachineX = p.X, MachineY = p.Y }).ToList()
            };

            string site = row.PositionName ?? "VisionCapture";
            _logger.Info(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_ArcDispenseStart",
                "[VisionCapture] 路径点胶启动: Type={0} 点数={1} 首点({2:F3},{3:F3}) 末点({4:F3},{5:F3}) 针头{6} 干跑={7} 工位={8}"),
                EffectiveTrajectoryType, machinePoints.Count,
                machinePoints[0].X, machinePoints[0].Y,
                machinePoints[^1].X, machinePoints[^1].Y,
                needleIndex + 1, dryRun, site));

            var segments = new[] { segment };
            if (dryRun)
                await _dispenseExecuteService.DryRunAsync(segments, needleIndex: needleIndex, token: token, pauseEvent: _pauseEvent);
            else
                await _dispenseExecuteService.ExecutePathAsync(segments, site, needleIndex: needleIndex, token: token, pauseEvent: _pauseEvent);
        }

        private async Task PreviewMachinePointsAsync()
        {
            if (SelectedRow == null)
            {
                StatusMessage = L("VisionCapture_NeedPhotoFirst");
                return;
            }

            if (CapturedTargetPoints.Count > 0)
            {
                await ComputeMachinePointsFromCameraAsync(SelectedRow);
                return;
            }

            StatusMessage = L("VisionCapture_NeedPhotoFirst");
        }

        /// <summary>
        /// 按轨迹类型生成预览/点胶路径。
        /// Arc 使用视觉返回的全部采样点，避免仅以 P1/P2/P3 拟合造成真实产品曲线截断。
        /// 相机坐标系的中间点法向与设备坐标系相反，需镜像至首尾弦线另一侧；
        /// 预览坐标轴反向仅影响显示，不能反转视觉采样点顺序。
        /// </summary>
        private List<(double X, double Y)> BuildTrajectoryMachinePoints(
            PhotoPositionRow row,
            IReadOnlyList<(double X, double Y)> mechEndpoints)
        {
            if (mechEndpoints == null || mechEndpoints.Count == 0)
                return new List<(double X, double Y)>();

            int segments = Math.Max(20, row?.ArcSegments ?? 20);
            var type = EffectiveTrajectoryType;

            if (type == TrajectoryType.Arc)
            {
                // 相机已输出整条实际曲线的采样点。保留全部点及点序，
                // 仅将中间点镜像到产品曲线所在的弦线另一侧。
                return MirrorArcIntermediatePoints(mechEndpoints);
            }

            if (type == TrajectoryType.Line && mechEndpoints.Count >= 2)
            {
                var end = mechEndpoints.Count >= 3 ? mechEndpoints[2] : mechEndpoints[1];
                return BezierArcDispenseService.DiscretizeLine(mechEndpoints[0], end, segments);
            }

            return mechEndpoints.ToList();
        }

        /// <summary>
        /// 将 Arc 中间采样点关于起点/终点组成的弦线镜像。
        /// 首尾点保持不变，修正相机与设备坐标系法向相反导致的曲线凸向错误。
        /// </summary>
        private static List<(double X, double Y)> MirrorArcIntermediatePoints(
            IReadOnlyList<(double X, double Y)> points)
        {
            var mirroredPoints = points?.ToList() ?? new List<(double X, double Y)>();
            if (mirroredPoints.Count < 3)
                return mirroredPoints;

            var start = mirroredPoints[0];
            var end = mirroredPoints[mirroredPoints.Count - 1];
            var chordX = end.X - start.X;
            var chordY = end.Y - start.Y;
            var chordLengthSquared = chordX * chordX + chordY * chordY;
            if (chordLengthSquared < 1e-9)
                return mirroredPoints;

            for (int i = 1; i < mirroredPoints.Count - 1; i++)
            {
                var point = mirroredPoints[i];
                var projectionRatio = ((point.X - start.X) * chordX + (point.Y - start.Y) * chordY)
                    / chordLengthSquared;
                var projectionX = start.X + projectionRatio * chordX;
                var projectionY = start.Y + projectionRatio * chordY;

                // 点关于直线的镜像：P' = 2 × 投影点 - 原点。
                mirroredPoints[i] = (2 * projectionX - point.X, 2 * projectionY - point.Y);
            }

            return mirroredPoints;
        }

        /// <summary>
        /// 提取实际执行顺序的全部轨迹点，用于 Arc Preview 坐标范围及首/中/尾标注。
        /// 坐标范围必须包含全部点，避免第 4 点及之后的点落在画布外。
        /// </summary>
        private static List<(double X, double Y)> BuildTrajectoryKeyPoints(
            IReadOnlyList<(double X, double Y)> mechEndpoints)
        {
            if (mechEndpoints == null || mechEndpoints.Count == 0)
                return null;
            return mechEndpoints.ToList();
        }

        private void GenerateArcPathGeometry(
            IReadOnlyList<(double X, double Y)> curvePoints,
            IReadOnlyList<(double X, double Y)> keyPoints = null)
        {
            ArcPathGeometry.Clear();
            if (curvePoints == null || curvePoints.Count < 2) return;

            double canvasWidth = 380;
            double canvasHeight = 220;
            double padding = 25;

            // 轨迹离散点与相机返回的全部关键点共同决定坐标轴范围。
            // Arc 仅用前三点生成贝塞尔曲线时，后续视觉点仍应处于预览坐标范围内。
            var allDisplayPoints = keyPoints == null || keyPoints.Count == 0
                ? curvePoints
                : curvePoints.Concat(keyPoints).ToList();

            double minX = allDisplayPoints.Min(p => p.X);
            double maxX = allDisplayPoints.Max(p => p.X);
            double minY = allDisplayPoints.Min(p => p.Y);
            double maxY = allDisplayPoints.Max(p => p.Y);

            double rangeX = maxX - minX;
            double rangeY = maxY - minY;
            if (rangeX < 0.001) rangeX = 1;
            if (rangeY < 0.001) rangeY = 1;

            double scaleX = (canvasWidth - 2 * padding) / rangeX;
            double scaleY = (canvasHeight - 2 * padding) / rangeY;
            double scale = Math.Min(scaleX, scaleY);

            double offsetX = padding + (canvasWidth - 2 * padding - rangeX * scale) / 2;
            double offsetY = padding + (canvasHeight - 2 * padding - rangeY * scale) / 2;

            // X 轴自左向右从大到小：maxX 在画布左侧，minX 在右侧。
            Func<double, double> toCanvasX = x => offsetX + (maxX - x) * scale;
            // Y 轴自上而下从大到小：maxY 在画布上方，minY 在下方
            Func<double, double> toCanvasY = y => offsetY + (maxY - y) * scale;

            // 绘制机器坐标网格和刻度，便于调试时对照关键点坐标。
            for (int i = 0; i <= 5; i++)
            {
                // 刻度自左向右：maxX → minX
                double gx = maxX - rangeX * i / 5.0;
                double cx = toCanvasX(gx);
                ArcPathGeometry.Add(new Line { X1 = cx, Y1 = padding, X2 = cx, Y2 = canvasHeight - padding, Stroke = Brushes.LightGray, StrokeThickness = 0.5 });
                AddCanvasText(gx.ToString("F2"), cx - 16, canvasHeight - padding + 2, 9, Brushes.Gray);
            }
            for (int i = 0; i <= 5; i++)
            {
                // 刻度自上而下：maxY → minY
                double gy = maxY - rangeY * i / 5.0;
                double cy = toCanvasY(gy);
                ArcPathGeometry.Add(new Line { X1 = padding, Y1 = cy, X2 = canvasWidth - padding, Y2 = cy, Stroke = Brushes.LightGray, StrokeThickness = 0.5 });
                AddCanvasText(gy.ToString("F2"), 2, cy - 7, 9, Brushes.Gray);
            }

            ArcPathGeometry.Add(new Line { X1 = padding, Y1 = canvasHeight - padding, X2 = canvasWidth - padding + 8, Y2 = canvasHeight - padding, Stroke = Brushes.DimGray, StrokeThickness = 1 });
            ArcPathGeometry.Add(new Line { X1 = padding, Y1 = padding - 8, X2 = padding, Y2 = canvasHeight - padding, Stroke = Brushes.DimGray, StrokeThickness = 1 });
            AddCanvasText("X", canvasWidth - padding + 10, canvasHeight - padding - 8, 10, Brushes.DimGray);
            AddCanvasText("Y", padding + 4, padding - 20, 10, Brushes.DimGray);

            var polyline = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0)),
                StrokeThickness = 2,
                Points = new PointCollection()
            };
            foreach (var pt in curvePoints)
            {
                polyline.Points.Add(new Point(toCanvasX(pt.X), toCanvasY(pt.Y)));
            }
            ArcPathGeometry.Add(polyline);

            // P1/P2/P3 标注使用实际点胶路径的首点/中点/末点。
            var markers = keyPoints ?? curvePoints;
            if (markers.Count >= 3)
            {
                int middleMarkerIndex = markers.Count / 2;
                int endMarkerIndex = markers.Count - 1;
                var startEllipse = new Ellipse
                {
                    Width = 10, Height = 10,
                    Fill = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)),
                    Stroke = Brushes.White,
                    StrokeThickness = 1.5
                };
                Canvas.SetLeft(startEllipse, toCanvasX(markers[0].X) - 5);
                Canvas.SetTop(startEllipse, toCanvasY(markers[0].Y) - 5);
                ArcPathGeometry.Add(startEllipse);
                AddPointLabel("P1", markers[0].X, markers[0].Y, toCanvasX, toCanvasY, 8, -22, Brushes.DarkGreen);

                var midEllipse = new Ellipse
                {
                    Width = 10, Height = 10,
                    Fill = new SolidColorBrush(Color.FromRgb(0xF5, 0x7C, 0x00)),
                    Stroke = Brushes.White,
                    StrokeThickness = 1.5
                };
                Canvas.SetLeft(midEllipse, toCanvasX(markers[middleMarkerIndex].X) - 5);
                Canvas.SetTop(midEllipse, toCanvasY(markers[middleMarkerIndex].Y) - 5);
                ArcPathGeometry.Add(midEllipse);
                AddPointLabel("P2", markers[middleMarkerIndex].X, markers[middleMarkerIndex].Y, toCanvasX, toCanvasY, 8, 8, Brushes.DarkOrange);

                var endEllipse = new Ellipse
                {
                    Width = 10, Height = 10,
                    Fill = new SolidColorBrush(Color.FromRgb(0xD3, 0x2F, 0x2F)),
                    Stroke = Brushes.White,
                    StrokeThickness = 1.5
                };
                Canvas.SetLeft(endEllipse, toCanvasX(markers[endMarkerIndex].X) - 5);
                Canvas.SetTop(endEllipse, toCanvasY(markers[endMarkerIndex].Y) - 5);
                ArcPathGeometry.Add(endEllipse);
                AddPointLabel("P3", markers[endMarkerIndex].X, markers[endMarkerIndex].Y, toCanvasX, toCanvasY, 8, -22, Brushes.DarkRed);
            }

            for (int i = 0; i < curvePoints.Count; i += Math.Max(1, curvePoints.Count / 15))
            {
                var dot = new Ellipse
                {
                    Width = 4, Height = 4,
                    Fill = new SolidColorBrush(Color.FromRgb(0x90, 0xA4, 0xAE))
                };
                Canvas.SetLeft(dot, toCanvasX(curvePoints[i].X) - 2);
                Canvas.SetTop(dot, toCanvasY(curvePoints[i].Y) - 2);
                ArcPathGeometry.Add(dot);
            }

            RaisePropertyChanged(nameof(HasArcPathGeometry));
        }

        private void AddPointLabel(string name, double x, double y, Func<double, double> toCanvasX, Func<double, double> toCanvasY, double offsetX, double offsetY, Brush foreground)
        {
            AddCanvasText($"{name} ({x:F2}, {y:F2})", toCanvasX(x) + offsetX, toCanvasY(y) + offsetY, 10, foreground);
        }

        private void AddPointLabel(string name, CoordinateTransformDetail point, Func<double, double> toCanvasX, Func<double, double> toCanvasY, double offsetX, double offsetY, Brush foreground)
        {
            AddPointLabel(name, point.FinalX, point.FinalY, toCanvasX, toCanvasY, offsetX, offsetY, foreground);
        }

        private void AddCanvasText(string text, double left, double top, double fontSize, Brush foreground)
        {
            var label = new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                Foreground = foreground,
                Background = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                Padding = new Thickness(1, 0, 1, 0)
            };
            Canvas.SetLeft(label, left);
            Canvas.SetTop(label, top);
            ArcPathGeometry.Add(label);
        }

        private async Task SaveTransformParamsAsync()
        {
            try
            {
                var poolId = _recipePoolService.CurrentPoolName ?? "Default";
                var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);
                var variableList = variables.ToList();

                UpdateOrAddGlobalVariable(variableList, "CameraCenterX", CameraCenterX.ToString("F6"), "相机中心X坐标", GlobalVariableType.Double);
                UpdateOrAddGlobalVariable(variableList, "CameraCenterY", CameraCenterY.ToString("F6"), "相机中心Y坐标", GlobalVariableType.Double);
                UpdateOrAddGlobalVariable(variableList, "NeedleOffsetX", NeedleOffsetX.ToString("F6"), "针尖X偏移", GlobalVariableType.Double);
                UpdateOrAddGlobalVariable(variableList, "NeedleOffsetY", NeedleOffsetY.ToString("F6"), "针尖Y偏移", GlobalVariableType.Double);

                UpdateOrAddGlobalVariable(variableList, "NeedleOffsetX_LinkedVar", NeedleOffsetXLinkedVar ?? "", "NeedleOffsetX链接的全局变量名", GlobalVariableType.String);
                UpdateOrAddGlobalVariable(variableList, "NeedleOffsetY_LinkedVar", NeedleOffsetYLinkedVar ?? "", "NeedleOffsetY链接的全局变量名", GlobalVariableType.String);
                UpdateOrAddGlobalVariable(variableList, "ArcNeedleOffsetX_LinkedVar", ArcNeedleOffsetXLinkedVar ?? "", "Arc模式NeedleOffsetX链接的全局变量名", GlobalVariableType.String);
                UpdateOrAddGlobalVariable(variableList, "ArcNeedleOffsetY_LinkedVar", ArcNeedleOffsetYLinkedVar ?? "", "Arc模式NeedleOffsetY链接的全局变量名", GlobalVariableType.String);
                UpdateOrAddGlobalVariable(variableList, "ArcNeedleOffsetX", ArcNeedleOffsetX.ToString("F6"), "Arc模式针头偏移X", GlobalVariableType.Double);
                UpdateOrAddGlobalVariable(variableList, "ArcNeedleOffsetY", ArcNeedleOffsetY.ToString("F6"), "Arc模式针头偏移Y", GlobalVariableType.Double);
                UpdateOrAddGlobalVariable(variableList, "ArcNeedleCompX", ArcNeedleCompX.ToString("F6"), "Arc模式专用补偿X", GlobalVariableType.Double);
                UpdateOrAddGlobalVariable(variableList, "ArcNeedleCompY", ArcNeedleCompY.ToString("F6"), "Arc模式专用补偿Y", GlobalVariableType.Double);

                UpdateOrAddGlobalVariable(variableList, "CameraNeedleDistanceX", CameraNeedleDistanceX.ToString("F6"), "相机胶针固定距离X", GlobalVariableType.Double);
                UpdateOrAddGlobalVariable(variableList, "CameraNeedleDistanceY", CameraNeedleDistanceY.ToString("F6"), "相机胶针固定距离Y", GlobalVariableType.Double);
                UpdateOrAddGlobalVariable(variableList, "CameraNeedleDistanceX_LinkedVar", CameraNeedleDistanceXLinkedVar ?? "", "相机胶针距离X链接的全局变量名", GlobalVariableType.String);
                UpdateOrAddGlobalVariable(variableList, "CameraNeedleDistanceY_LinkedVar", CameraNeedleDistanceYLinkedVar ?? "", "相机胶针距离Y链接的全局变量名", GlobalVariableType.String);

                for (int i = 0; i < variableList.Count; i++)
                    variableList[i].Index = i + 1;

                await _recipePoolService.SaveGlobalVariablesAsync(poolId, variableList);
                StatusMessage = L("VisionCapture_CoordParamsSaved");
                _logger.Info(_localizationService.GetResourceOrDefault("VisCap_Log_TransformParamsSaved", "[VisionCapture] 坐标转换参数已保存到全局变量"));
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(L("VisionCapture_Status_SaveFail"), ex.Message);
                _logger.Error(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_SaveTransformParamsFailed", "[VisionCapture] 保存坐标转换参数失败: {0}"), ex.Message));
            }
        }

        private async Task LoadTransformParamsAsync()
        {
            try
            {
                var poolId = _recipePoolService.CurrentPoolName ?? "Default";
                var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);

                _isLoadingTransformParams = true;
                try
                {
                    AvailableGlobalVariables.Clear();
                    foreach (var v in variables)
                        AvailableGlobalVariables.Add(v);

                    RefreshLinkableGlobalVariables();

                    var ccxVar = variables.FirstOrDefault(v => v.Name == "CameraCenterX");
                    var ccyVar = variables.FirstOrDefault(v => v.Name == "CameraCenterY");
                    var noxVar = variables.FirstOrDefault(v => v.Name == "NeedleOffsetX");
                    var noyVar = variables.FirstOrDefault(v => v.Name == "NeedleOffsetY");

                    if (ccxVar != null && double.TryParse(ccxVar.Value, out var ccx))
                        CameraCenterX = ccx;
                    if (ccyVar != null && double.TryParse(ccyVar.Value, out var ccy))
                        CameraCenterY = ccy;

                    var noxLink = variables.FirstOrDefault(v => v.Name == "NeedleOffsetX_LinkedVar");
                    var noyLink = variables.FirstOrDefault(v => v.Name == "NeedleOffsetY_LinkedVar");

                    NeedleOffsetXLinkedVar = NormalizeLinkedVarName(noxLink?.Value);
                    NeedleOffsetYLinkedVar = NormalizeLinkedVarName(noyLink?.Value);

                    if (noyLink != null && !string.IsNullOrWhiteSpace(noyLink.Value) && NeedleOffsetYLinkedVar == null)
                        _logger.Info(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_IgnoreInvalidNeedleOffsetYLink", "[VisionCapture] 忽略无效的 NeedleOffsetY 链接变量: {0}"), noyLink.Value.Trim()));
                    if (noxLink != null && !string.IsNullOrWhiteSpace(noxLink.Value) && NeedleOffsetXLinkedVar == null)
                        _logger.Info(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_IgnoreInvalidNeedleOffsetXLink", "[VisionCapture] 忽略无效的 NeedleOffsetX 链接变量: {0}"), noxLink.Value.Trim()));

                    var arcNoxLink = variables.FirstOrDefault(v => v.Name == "ArcNeedleOffsetX_LinkedVar");
                    var arcNoyLink = variables.FirstOrDefault(v => v.Name == "ArcNeedleOffsetY_LinkedVar");
                    ArcNeedleOffsetXLinkedVar = NormalizeLinkedVarName(arcNoxLink?.Value);
                    ArcNeedleOffsetYLinkedVar = NormalizeLinkedVarName(arcNoyLink?.Value);

                    if (IsArcNeedleOffsetXLinked)
                        ArcNeedleOffsetX = ReadLinkedVariableValue(ArcNeedleOffsetXLinkedVar);
                    else
                    {
                        var arcNoxVar = variables.FirstOrDefault(v => v.Name == "ArcNeedleOffsetX");
                        if (arcNoxVar != null && double.TryParse(arcNoxVar.Value, out var arcNox))
                            ArcNeedleOffsetX = arcNox;
                        else
                            ArcNeedleOffsetX = NeedleOffsetX;
                    }
                    if (IsArcNeedleOffsetYLinked)
                        ArcNeedleOffsetY = ReadLinkedVariableValue(ArcNeedleOffsetYLinkedVar);
                    else
                    {
                        var arcNoyVar = variables.FirstOrDefault(v => v.Name == "ArcNeedleOffsetY");
                        if (arcNoyVar != null && double.TryParse(arcNoyVar.Value, out var arcNoy))
                            ArcNeedleOffsetY = arcNoy;
                        else
                            ArcNeedleOffsetY = NeedleOffsetY;
                    }

                    var arcCompXVar = variables.FirstOrDefault(v => v.Name == "ArcNeedleCompX");
                    var arcCompYVar = variables.FirstOrDefault(v => v.Name == "ArcNeedleCompY");
                    if (arcCompXVar != null && double.TryParse(arcCompXVar.Value, out var arcCompX))
                        ArcNeedleCompX = arcCompX;
                    else
                        ArcNeedleCompX = 0;
                    if (arcCompYVar != null && double.TryParse(arcCompYVar.Value, out var arcCompY))
                        ArcNeedleCompY = arcCompY;
                    else
                        ArcNeedleCompY = 0;

                    if (IsNeedleOffsetXLinked)
                        NeedleOffsetX = ReadLinkedVariableValue(NeedleOffsetXLinkedVar);
                    else if (noxVar != null && double.TryParse(noxVar.Value, out var nox))
                        NeedleOffsetX = nox;
                    else
                        NeedleOffsetX = 0;

                    if (IsNeedleOffsetYLinked)
                        NeedleOffsetY = ReadLinkedVariableValue(NeedleOffsetYLinkedVar);
                    else if (noyVar != null && double.TryParse(noyVar.Value, out var noy))
                        NeedleOffsetY = noy;
                    else
                        NeedleOffsetY = 0;

                    RaisePropertyChanged(nameof(IsNeedleOffsetXLinked));
                    RaisePropertyChanged(nameof(IsNeedleOffsetYLinked));

                    var cndxLink = variables.FirstOrDefault(v => v.Name == "CameraNeedleDistanceX_LinkedVar");
                    var cndyLink = variables.FirstOrDefault(v => v.Name == "CameraNeedleDistanceY_LinkedVar");
                    CameraNeedleDistanceXLinkedVar = NormalizeLinkedVarName(cndxLink?.Value);
                    CameraNeedleDistanceYLinkedVar = NormalizeLinkedVarName(cndyLink?.Value);

                    if (IsCameraNeedleDistanceXLinked)
                        CameraNeedleDistanceX = ReadLinkedVariableValue(CameraNeedleDistanceXLinkedVar);
                    else
                    {
                        var cndXVar = variables.FirstOrDefault(v => v.Name == "CameraNeedleDistanceX");
                        if (cndXVar != null && double.TryParse(cndXVar.Value, out var cndx))
                            CameraNeedleDistanceX = cndx;
                        else
                            CameraNeedleDistanceX = 0;
                    }

                    if (IsCameraNeedleDistanceYLinked)
                        CameraNeedleDistanceY = ReadLinkedVariableValue(CameraNeedleDistanceYLinkedVar);
                    else
                    {
                        var cndYVar = variables.FirstOrDefault(v => v.Name == "CameraNeedleDistanceY");
                        if (cndYVar != null && double.TryParse(cndYVar.Value, out var cndy))
                            CameraNeedleDistanceY = cndy;
                        else
                            CameraNeedleDistanceY = 0;
                    }

                    RaisePropertyChanged(nameof(IsCameraNeedleDistanceXLinked));
                    RaisePropertyChanged(nameof(IsCameraNeedleDistanceYLinked));
                }
                finally
                {
                    _isLoadingTransformParams = false;
                }
            }
            catch (Exception ex)
            {
                _isLoadingTransformParams = false;
                _logger.Warn(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_LoadTransformParamsFailed", "[VisionCapture] 加载坐标转换参数失败: {0}"), ex.Message));
            }
        }

        private void UpdateOrAddGlobalVariable(List<GlobalVariable> variables, string name, string value, string comment, GlobalVariableType type)
        {
            var existing = variables.FirstOrDefault(v => v.Name == name);
            if (existing != null)
            {
                existing.Type = type;
                existing.Value = value;
                existing.Comment = comment;
            }
            else
            {
                variables.Add(new GlobalVariable
                {
                    Name = name,
                    Type = type,
                    Value = value,
                    Comment = comment
                });
            }
        }

        public async Task UpdateGlobalVariableValueAsync(string varName, double value)
        {
            if (string.IsNullOrEmpty(varName)) return;
            try
            {
                var gv = AvailableGlobalVariables.FirstOrDefault(v => v.Name == varName);
                if (gv != null)
                    gv.Value = value.ToString("F6");

                var poolId = _recipePoolService.CurrentPoolName ?? "Default";
                var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);
                var persistedGv = variables.FirstOrDefault(v => v.Name == varName);
                if (persistedGv != null)
                {
                    persistedGv.Value = value.ToString("F6");
                    await _recipePoolService.SaveGlobalVariablesAsync(poolId, variables);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_UpdateGlobalVarFailed", "[VisionCapture] 更新全局变量值失败: {0}"), ex.Message));
            }
        }

        private double GetVisionValue(Dictionary<string, double> data, string key, double defaultValue)
        {
            if (data != null && data.TryGetValue(key, out double value))
                return value;
            return defaultValue;
        }

        private async Task<bool> ShowConfirmationAsync(string title, string message)
        {
            var tcs = new TaskCompletionSource<bool>();
            _dialogService.ShowDialog("ConfirmationDialog", new DialogParameters
            {
                { "title", title },
                { "message", message }
            }, result => tcs.SetResult(result.Result == ButtonResult.Yes));
            return await tcs.Task;
        }

        private void RaiseCanExecuteChanged()
        {
            ExecuteCaptureCommand.RaiseCanExecuteChanged();
            MoveToTeachPositionCommand?.RaiseCanExecuteChanged();
            StopRowMotionCommand?.RaiseCanExecuteChanged();
            ReturnToSafeCommand?.RaiseCanExecuteChanged();
            ExecuteDispenseCommand.RaiseCanExecuteChanged();
            PreviewMachinePointsCommand.RaiseCanExecuteChanged();
            StopCommand?.RaiseCanExecuteChanged();
            PauseCommand?.RaiseCanExecuteChanged();
            ResumeCommand?.RaiseCanExecuteChanged();
            AddGroupCommand?.RaiseCanExecuteChanged();
            DeleteGroupCommand?.RaiseCanExecuteChanged();
            AddPhotoPositionCommand?.RaiseCanExecuteChanged();
            DeletePhotoPositionCommand?.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(CanStartDispense));
            RaisePropertyChanged(nameof(CanStop));
            RaisePropertyChanged(nameof(CanPause));
            RaisePropertyChanged(nameof(CanResume));
        }

        /// <summary>
        /// 获取配置文件默认目录
        /// </summary>
        private static string GetConfigDirectory()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory();
            var configDir = System.IO.Path.Combine(baseDir, "Config", "VisionCapture");
            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);
            return configDir;
        }

        /// <summary>
        /// 保存当前页面所有参数到JSON文件。
        /// 直接保存到默认路径（Config/VisionCapture/），以 VisionCapture_当前时间.json 命名。
        /// </summary>
        private async Task SaveConfigToFileAsync()
        {
            try
            {
                var configDir = GetConfigDirectory();
                var fileName = $"VisionCapture_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var filePath = System.IO.Path.Combine(configDir, fileName);

                var config = BuildCurrentConfig();
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                await File.WriteAllTextAsync(filePath, json);

                CurrentFilePath = filePath;
                CurrentFileName = fileName;
                await SaveCurrentFileToRecipePoolAsync();

                // 后台按数量清理旧文件，避免阻塞UI
                _ = _configRetentionService.CleanupFolderByCountAsync("VisionCapture", "VisionCapture_*.json", filePath);

                StatusMessage = string.Format(L("VisionCapture_Status_ConfigSaved"), CurrentFileName);
                _logger.Info(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_ConfigSaved", "[VisionCapture] 配置已保存: {0}"), filePath));
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(L("VisionCapture_Status_SaveFail"), ex.Message);
                _logger.Error(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_SaveConfigFailed", "[VisionCapture] 保存配置失败: {0}"), ex.Message));
            }
        }

        /// <summary>
        /// 从JSON文件加载配置
        /// </summary>
        private async Task LoadConfigFromFileAsync()
        {
            try
            {
                var configDir = GetConfigDirectory();
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    InitialDirectory = configDir
                };

                if (dialog.ShowDialog() != true) return;

                await LoadConfigFromPathAsync(dialog.FileName);
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(L("VisionCapture_Status_LoadFail"), ex.Message);
                _logger.Error(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_LoadConfigFailed", "[VisionCapture] 加载配置失败: {0}"), ex.Message));
            }
        }

        /// <summary>
        /// 从指定路径加载配置
        /// </summary>
        private async Task LoadConfigFromPathAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                StatusMessage = L("VisionCapture_Status_FileNotFound");
                return;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var config = JsonConvert.DeserializeObject<VisionCaptureConfig>(json);
            if (config == null) return;

            await ApplyConfig(config);

            CurrentFilePath = filePath;
            CurrentFileName = System.IO.Path.GetFileName(filePath);
            //await SaveCurrentFileToRecipePoolAsync();

            StatusMessage = string.Format(L("VisionCapture_Status_ConfigLoaded"), CurrentFileName);
            _logger.Info(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_ConfigLoaded", "[VisionCapture] 配置已加载: {0}"), filePath));
        }

        /// <summary>
        /// 尝试从配方池自动加载上次使用的配置文件
        /// </summary>
        private async Task TryAutoLoadConfigAsync()
        {
            try
            {
                var poolName = _recipePoolService.CurrentPoolName ?? "Default";
                var extData = await _recipePoolService.GetExtensionDataAsync<VisionCaptureFileRecord>(poolName, "VisionCapture_CurrentFile");
                if (extData?.FilePath != null && File.Exists(extData.FilePath))
                {
                    _logger.Info(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_LoadConfigFromRecipePool", "[VisionCapture] 从配方池记录加载配置: {0}"), extData.FilePath));
                    await LoadConfigFromPathAsync(extData.FilePath);
                    return;
                }

                var configDir = GetConfigDirectory();
                var defaultPath = System.IO.Path.Combine(configDir, "VisionCapture.json");
                if (File.Exists(defaultPath))
                {
                    _logger.Info(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_LoadConfigFromDefault", "[VisionCapture] 配方池无记录，从默认路径加载: {0}"), defaultPath));
                    await LoadConfigFromPathAsync(defaultPath);
                    return;
                }

                _logger.Info(_localizationService.GetResourceOrDefault("VisCap_Log_NoConfigToLoad", "[VisionCapture] 无可加载的配置文件"));
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_AutoLoadConfigFailed", "[VisionCapture] 自动加载配置失败: {0}"), ex.Message));
            }
        }

        /// <summary>
        /// 将当前加载的文件路径保存到配方池ExtensionData
        /// </summary>
        private async Task SaveCurrentFileToRecipePoolAsync()
        {
            try
            {
                var poolName = _recipePoolService.CurrentPoolName ?? "Default";
                await _recipePoolService.SetExtensionDataAsync(poolName, "VisionCapture_CurrentFile",
                    new VisionCaptureFileRecord { FilePath = CurrentFilePath });
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(_localizationService.GetResourceOrDefault("VisCap_Log_SaveFileRecordToRecipePoolFailed", "[VisionCapture] 保存文件记录到配方池失败: {0}"), ex.Message));
            }
        }

        /// <summary>
        /// 从当前ViewModel状态构建配置对象（含全部组及其拍照位行）
        /// </summary>
        private VisionCaptureConfig BuildCurrentConfig()
        {
            // 保存前先缓存当前组行，确保 Groups 数据完整
            if (!string.IsNullOrEmpty(SelectedGroup))
                CacheCurrentGroupRows(SelectedGroup);

            var groupConfigs = new List<VisionCaptureGroupConfig>();
            foreach (var groupName in Groups)
            {
                if (!_groupRowsCache.TryGetValue(groupName, out var rows))
                    rows = new List<PhotoPositionRow>();
                groupConfigs.Add(new VisionCaptureGroupConfig
                {
                    Name = groupName,
                    Rows = rows.Select(ToRowConfig).ToList()
                });
            }

            // 双针头：将当前单值字段写回当前针头数组槽位，确保两针头数据最新
            _camDistXByNeedle[_currentNeedleIndex] = CameraNeedleDistanceX;
            _camDistYByNeedle[_currentNeedleIndex] = CameraNeedleDistanceY;
            _needleOffsetXByNeedle[_currentNeedleIndex] = NeedleOffsetX;
            _needleOffsetYByNeedle[_currentNeedleIndex] = NeedleOffsetY;
            _camDistXLinkedVarByNeedle[_currentNeedleIndex] = CameraNeedleDistanceXLinkedVar;
            _camDistYLinkedVarByNeedle[_currentNeedleIndex] = CameraNeedleDistanceYLinkedVar;
            _needleOffsetXLinkedVarByNeedle[_currentNeedleIndex] = NeedleOffsetXLinkedVar;
            _needleOffsetYLinkedVarByNeedle[_currentNeedleIndex] = NeedleOffsetYLinkedVar;

            var config = new VisionCaptureConfig
            {
                SafePositionName = SafePositionName,
                StandbyPositionName = StandbyPositionName,
                DispensePositionName = DispensePositionName,
                CameraCenterX = CameraCenterX,
                CameraCenterY = CameraCenterY,
                CurrentNeedleIndex = _currentNeedleIndex,
                // 针头1 偏移
                CameraNeedleDistanceX = _camDistXByNeedle[0],
                CameraNeedleDistanceY = _camDistYByNeedle[0],
                NeedleOffsetX = _needleOffsetXByNeedle[0],
                NeedleOffsetY = _needleOffsetYByNeedle[0],
                // 配置兼容：ArcNeedle* 与统一 NeedleOffset 同步写出
                ArcNeedleOffsetX = _needleOffsetXByNeedle[0],
                ArcNeedleOffsetY = _needleOffsetYByNeedle[0],
                ArcNeedleCompX = ArcNeedleCompX,
                ArcNeedleCompY = ArcNeedleCompY,
                NeedleOffsetXLinkedVar = _needleOffsetXLinkedVarByNeedle[0],
                NeedleOffsetYLinkedVar = _needleOffsetYLinkedVarByNeedle[0],
                ArcNeedleOffsetXLinkedVar = ArcNeedleOffsetXLinkedVar,
                ArcNeedleOffsetYLinkedVar = ArcNeedleOffsetYLinkedVar,
                CameraNeedleDistanceXLinkedVar = _camDistXLinkedVarByNeedle[0],
                CameraNeedleDistanceYLinkedVar = _camDistYLinkedVarByNeedle[0],
                // 针头2 偏移（独立参数）
                CameraNeedleDistanceXNeedle2 = _camDistXByNeedle[1],
                CameraNeedleDistanceYNeedle2 = _camDistYByNeedle[1],
                NeedleOffsetXNeedle2 = _needleOffsetXByNeedle[1],
                NeedleOffsetYNeedle2 = _needleOffsetYByNeedle[1],
                CameraNeedleDistanceXLinkedVarNeedle2 = _camDistXLinkedVarByNeedle[1],
                CameraNeedleDistanceYLinkedVarNeedle2 = _camDistYLinkedVarByNeedle[1],
                NeedleOffsetXLinkedVarNeedle2 = _needleOffsetXLinkedVarByNeedle[1],
                NeedleOffsetYLinkedVarNeedle2 = _needleOffsetYLinkedVarByNeedle[1],
                SelectedGroup = SelectedGroup,
                CurrentRunMode = CurrentRunMode,
                Groups = groupConfigs,
                // 兼容旧字段：当前组行仍写入 Rows
                Rows = PhotoPositionRows.Select(ToRowConfig).ToList()
            };
            return config;
        }

        /// <summary>将 PhotoPositionRow 转为持久化配置</summary>
        private static PhotoPositionRowConfig ToRowConfig(PhotoPositionRow r)
        {
            return new PhotoPositionRowConfig
            {
                PositionName = NormalizePhotoPositionName(r.PositionName),
                SiteFeatureName = r.SiteFeatureName,
                Speed = r.Speed,
                TriggerCommand = r.TriggerCommand,
                ConnectionName = r.ConnectionName,
                Timeout = r.Timeout,
                DispenseType = r.DispenseType,
                TrajectoryOverride = r.TrajectoryOverride,
                ArcSegments = r.ArcSegments,
                ArcHeight = r.ArcHeight,
                ArcDirection = r.ArcDirection,
                NeedleOffsetX = r.NeedleOffsetX,
                NeedleOffsetY = r.NeedleOffsetY,
                NeedleOffsetXExpression = r.NeedleOffsetXExpression,
                NeedleOffsetYExpression = r.NeedleOffsetYExpression,
                OffsetXExpression = r.OffsetXExpression,
                OffsetYExpression = r.OffsetYExpression,
                NeedleCompensationX = r.NeedleCompensationX,
                NeedleCompensationY = r.NeedleCompensationY,
                CompensationXExpression = r.CompensationXExpression,
                CompensationYExpression = r.CompensationYExpression,
                DotParamsNeedle1 = r.DotParamsNeedle1,
                DotParamsNeedle2 = r.DotParamsNeedle2,
                ArcParamsNeedle1 = r.ArcParamsNeedle1,
                ArcParamsNeedle2 = r.ArcParamsNeedle2,
                ArcTrackType = r.ArcTrackType
            };
        }

        /// <summary>
        /// 将旧版 OffsetX/YExpression 中的数学表达式迁移到 NeedleOffsetX/YExpression，避免隐藏项参与合计。
        /// </summary>
        private static void MigrateLegacyRowOffsetExpressions(PhotoPositionRow row)
        {
            if (row == null) return;
            if (string.IsNullOrWhiteSpace(row.NeedleOffsetXExpression)
                && IsLegacyMathOffsetExpression(row.OffsetXExpression))
            {
                row.NeedleOffsetXExpression = row.OffsetXExpression.Trim();
                row.OffsetXExpression = null;
            }
            if (string.IsNullOrWhiteSpace(row.NeedleOffsetYExpression)
                && IsLegacyMathOffsetExpression(row.OffsetYExpression))
            {
                row.NeedleOffsetYExpression = row.OffsetYExpression.Trim();
                row.OffsetYExpression = null;
            }
        }

        /// <summary>判断旧 OffsetExpression 是否为数学表达式（非全局变量链接名）</summary>
        private static bool IsLegacyMathOffsetExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) return false;
            var trimmed = expression.Trim();
            // 含运算符或小数点，视为数学表达式
            if (trimmed.IndexOfAny(new[] { '+', '-', '*', '/', '(', ')' }) >= 0) return true;
            return double.TryParse(trimmed, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _);
        }

        /// <summary>从持久化配置创建 PhotoPositionRow；缺失 TrajectoryOverride 时按旧 DispenseType/ArcTrackType 迁移</summary>
        private PhotoPositionRow CreateRowFromConfig(PhotoPositionRowConfig rowConfig)
        {
            var name = NormalizePhotoPositionName(rowConfig.PositionName ?? rowConfig.SiteFeatureName ?? "PhotoPos1");
            var row = new PhotoPositionRow(name)
            {
                Speed = rowConfig.Speed,
                TriggerCommand = rowConfig.TriggerCommand,
                ConnectionName = rowConfig.ConnectionName,
                Timeout = rowConfig.Timeout,
                DispenseType = rowConfig.DispenseType,
                ArcSegments = rowConfig.ArcSegments,
                ArcHeight = rowConfig.ArcHeight,
                ArcDirection = rowConfig.ArcDirection,
                NeedleOffsetX = rowConfig.NeedleOffsetX,
                NeedleOffsetY = rowConfig.NeedleOffsetY,
                NeedleOffsetXExpression = rowConfig.NeedleOffsetXExpression,
                NeedleOffsetYExpression = rowConfig.NeedleOffsetYExpression,
                OffsetXExpression = rowConfig.OffsetXExpression,
                OffsetYExpression = rowConfig.OffsetYExpression,
                NeedleCompensationX = rowConfig.NeedleCompensationX,
                NeedleCompensationY = rowConfig.NeedleCompensationY,
                CompensationXExpression = rowConfig.CompensationXExpression,
                CompensationYExpression = rowConfig.CompensationYExpression,
                ArcTrackType = rowConfig.ArcTrackType
            };

            // TrajectoryOverride：有值则用；缺失则从旧 DispenseType/ArcTrackType 迁移
            if (rowConfig.TrajectoryOverride.HasValue)
            {
                row.TrajectoryOverride = rowConfig.TrajectoryOverride.Value;
            }
            else
            {
#pragma warning disable CS0618
                if (rowConfig.DispenseType == DispenseType.Dot)
                    row.TrajectoryOverride = TrajectoryType.Dot;
                else if (rowConfig.ArcTrackType == ArcTrackType.Line)
                    row.TrajectoryOverride = TrajectoryType.Line;
                else
                    row.TrajectoryOverride = TrajectoryType.Arc;
#pragma warning restore CS0618
            }
            // 旧 Auto（跟随相机）已取消自动检测，迁移为 Dot
            if (row.TrajectoryOverride == TrajectoryType.Auto)
                row.TrajectoryOverride = TrajectoryType.Dot;

            // 旧配置 OffsetXExpression 可能存有隐藏数学表达式（如 0.5），迁移到可见字段后清空
            MigrateLegacyRowOffsetExpressions(row);

            // 双针头工艺参数：旧配置 DotParams/ArcParams → Needle1；新配置直接加载两针头
            if (rowConfig.DotParamsNeedle1 != null) row.DotParamsNeedle1 = rowConfig.DotParamsNeedle1;
            if (rowConfig.DotParamsNeedle2 != null) row.DotParamsNeedle2 = rowConfig.DotParamsNeedle2;
            if (rowConfig.ArcParamsNeedle1 != null) row.ArcParamsNeedle1 = rowConfig.ArcParamsNeedle1;
            if (rowConfig.ArcParamsNeedle2 != null) row.ArcParamsNeedle2 = rowConfig.ArcParamsNeedle2;

            // 旧配置迁移：NeedleCompensationX/Y → XyCompensationX/Y（统一补偿入口）
            // 旧值非 0 且新值为默认 0 时，将旧补偿迁入 DotParams/ArcParams 的 XY 补偿字段
            MigrateNeedleCompToXyCompensation(row, rowConfig);

            return row;
        }

        /// <summary>
        /// 旧配置兼容：将 NeedleCompensationX/Y 迁移到 DotParams.XyCompensation / ArcParams.XyCompensation。
        /// 仅当新 XY 补偿为默认 0 且旧 NeedleComp 非 0 时执行，避免覆盖用户已设置的新值。
        /// </summary>
        private static void MigrateNeedleCompToXyCompensation(PhotoPositionRow row, PhotoPositionRowConfig rowConfig)
        {
            if (rowConfig.NeedleCompensationX == 0 && rowConfig.NeedleCompensationY == 0) return;

            // 旧配置仅迁移到针头1（DotParamsNeedle1/ArcParamsNeedle1）
            if (row.DotParamsNeedle1 != null && row.DotParamsNeedle1.XyCompensationX == 0 && row.DotParamsNeedle1.XyCompensationY == 0)
            {
                row.DotParamsNeedle1.XyCompensationX = rowConfig.NeedleCompensationX;
                row.DotParamsNeedle1.XyCompensationY = rowConfig.NeedleCompensationY;
            }
            if (row.ArcParamsNeedle1 != null && row.ArcParamsNeedle1.XyCompensationX == 0 && row.ArcParamsNeedle1.XyCompensationY == 0)
            {
                row.ArcParamsNeedle1.XyCompensationX = rowConfig.NeedleCompensationX;
                row.ArcParamsNeedle1.XyCompensationY = rowConfig.NeedleCompensationY;
            }
        }

        /// <summary>
        /// 将配置对象应用到当前ViewModel（组列表独立于 WorkOrder）
        /// </summary>
        private async Task ApplyConfig(VisionCaptureConfig config)
        {
            SafePositionName = MigratePositionName(config.SafePositionName ?? "SafePosition");
            StandbyPositionName = MigratePositionName(config.StandbyPositionName ?? "StandbyPosition");
            DispensePositionName = MigratePositionName(config.DispensePositionName ?? "DispensePosition");
            CameraCenterX = config.CameraCenterX;
            CameraCenterY = config.CameraCenterY;

            // 双针头：先确保选中针头1，再加载针头1偏移到单值字段
            _currentNeedleIndex = 0;
            CameraNeedleDistanceX = config.CameraNeedleDistanceX;
            CameraNeedleDistanceY = config.CameraNeedleDistanceY;
            // 统一校针偏差：优先 NeedleOffset；若为 0 且旧 ArcNeedleOffset 有值则迁移
            NeedleOffsetX = config.NeedleOffsetX != 0 ? config.NeedleOffsetX : config.ArcNeedleOffsetX;
            NeedleOffsetY = config.NeedleOffsetY != 0 ? config.NeedleOffsetY : config.ArcNeedleOffsetY;
            ArcNeedleOffsetX = NeedleOffsetX;
            ArcNeedleOffsetY = NeedleOffsetY;
            ArcNeedleCompX = config.ArcNeedleCompX;
            ArcNeedleCompY = config.ArcNeedleCompY;
            NeedleOffsetXLinkedVar = NormalizeLinkedVarName(config.NeedleOffsetXLinkedVar);
            NeedleOffsetYLinkedVar = NormalizeLinkedVarName(config.NeedleOffsetYLinkedVar);
            ArcNeedleOffsetXLinkedVar = NormalizeLinkedVarName(config.ArcNeedleOffsetXLinkedVar);
            ArcNeedleOffsetYLinkedVar = NormalizeLinkedVarName(config.ArcNeedleOffsetYLinkedVar);
            CameraNeedleDistanceXLinkedVar = NormalizeLinkedVarName(config.CameraNeedleDistanceXLinkedVar);
            CameraNeedleDistanceYLinkedVar = NormalizeLinkedVarName(config.CameraNeedleDistanceYLinkedVar);

            // 针头2 偏移直接写入数组槽位（不经过 setter，避免副作用）
            _camDistXByNeedle[1] = config.CameraNeedleDistanceXNeedle2;
            _camDistYByNeedle[1] = config.CameraNeedleDistanceYNeedle2;
            _needleOffsetXByNeedle[1] = config.NeedleOffsetXNeedle2;
            _needleOffsetYByNeedle[1] = config.NeedleOffsetYNeedle2;
            _camDistXLinkedVarByNeedle[1] = NormalizeLinkedVarName(config.CameraNeedleDistanceXLinkedVarNeedle2);
            _camDistYLinkedVarByNeedle[1] = NormalizeLinkedVarName(config.CameraNeedleDistanceYLinkedVarNeedle2);
            _needleOffsetXLinkedVarByNeedle[1] = NormalizeLinkedVarName(config.NeedleOffsetXLinkedVarNeedle2);
            _needleOffsetYLinkedVarByNeedle[1] = NormalizeLinkedVarName(config.NeedleOffsetYLinkedVarNeedle2);

            CurrentRunMode = config.CurrentRunMode;
            // 应用保存的针头选择（触发 SwitchNeedleOffsetData 加载对应针头偏移）
            CurrentNeedleIndex = config.CurrentNeedleIndex;

            _allPositions = await MergeAllPositionsAsync();

            // 重建组缓存（优先使用 Groups；旧配置仅有 SelectedGroup+Rows 时兼容迁移）
            _groupRowsCache.Clear();
            _suppressGroupChangeReload = true;
            try
            {
                Groups.Clear();
                ClearPhotoPositionRows();
                SiteFeatureNames.Clear();

                if (config.Groups != null && config.Groups.Count > 0)
                {
                    foreach (var g in config.Groups)
                    {
                        if (string.IsNullOrWhiteSpace(g.Name)) continue;
                        Groups.Add(g.Name);
                        var rows = (g.Rows ?? new List<PhotoPositionRowConfig>())
                            .Select(CreateRowFromConfig).ToList();
                        _groupRowsCache[g.Name] = rows;
                    }
                }
                else
                {
                    // 旧配置兼容：用 SelectedGroup + Rows 构建单组
                    var groupName = string.IsNullOrWhiteSpace(config.SelectedGroup) ? "Group1" : config.SelectedGroup;
                    Groups.Add(groupName);
                    var rows = (config.Rows ?? new List<PhotoPositionRowConfig>())
                        .Select(CreateRowFromConfig).ToList();
                    _groupRowsCache[groupName] = rows;
                }

                var targetGroup = !string.IsNullOrEmpty(config.SelectedGroup) && Groups.Contains(config.SelectedGroup)
                    ? config.SelectedGroup
                    : Groups.FirstOrDefault();
                _selectedGroup = null; // 强制触发后续加载
                SetProperty(ref _selectedGroup, targetGroup, nameof(SelectedGroup));
                RaisePropertyChanged(nameof(GroupDisplay));
            }
            finally
            {
                _suppressGroupChangeReload = false;
            }

            if (!string.IsNullOrEmpty(SelectedGroup))
                LoadRowsFromGroupCache(SelectedGroup);

            // 若旧配置有 Rows 且当前组缓存为空，则用 Rows 填充（双保险）
            if (PhotoPositionRows.Count == 0 && config.Rows != null && config.Rows.Count > 0)
            {
                foreach (var rowConfig in config.Rows)
                {
                    var row = CreateRowFromConfig(rowConfig);
                    PhotoPositionRows.Add(row);
                    RefreshRowParsedCoordinates(row);
                }
                SyncSiteFeatureNamesFromRows();
                if (PhotoPositionRows.Count > 0)
                    SelectedRow = PhotoPositionRows[0];
                if (!string.IsNullOrEmpty(SelectedGroup))
                    CacheCurrentGroupRows(SelectedGroup);
            }
            else
            {
                foreach (var row in PhotoPositionRows)
                    RefreshRowParsedCoordinates(row);
            }

            RefreshAvailablePositions();
            RefreshSafePositionDisplay();
            DeleteGroupCommand?.RaiseCanExecuteChanged();
        }
    }
}

/// <summary>
/// VisionCapture页面配置持久化模型
/// </summary>
public class VisionCaptureConfig
{
    public string SafePositionName { get; set; } = "SafePosition";
    public string StandbyPositionName { get; set; } = "StandbyPosition";
    public string DispensePositionName { get; set; } = "DispensePosition";
    public double CameraCenterX { get; set; }
    public double CameraCenterY { get; set; }
    /// <summary>当前选中针头索引（0=针头1, 1=针头2）</summary>
    public int CurrentNeedleIndex { get; set; }
    // 针头1 偏移（旧字段保持兼容，等价于 Needle1 后缀字段）
    public double CameraNeedleDistanceX { get; set; }
    public double CameraNeedleDistanceY { get; set; }
    public double NeedleOffsetX { get; set; }
    public double NeedleOffsetY { get; set; }
    public double ArcNeedleOffsetX { get; set; }
    public double ArcNeedleOffsetY { get; set; }
    public double ArcNeedleCompX { get; set; }
    public double ArcNeedleCompY { get; set; }
    public string NeedleOffsetXLinkedVar { get; set; }
    public string NeedleOffsetYLinkedVar { get; set; }
    public string ArcNeedleOffsetXLinkedVar { get; set; }
    public string ArcNeedleOffsetYLinkedVar { get; set; }
    public string CameraNeedleDistanceXLinkedVar { get; set; }
    public string CameraNeedleDistanceYLinkedVar { get; set; }
    // 针头2 偏移（独立参数）
    public double CameraNeedleDistanceXNeedle2 { get; set; }
    public double CameraNeedleDistanceYNeedle2 { get; set; }
    public double NeedleOffsetXNeedle2 { get; set; }
    public double NeedleOffsetYNeedle2 { get; set; }
    public string CameraNeedleDistanceXLinkedVarNeedle2 { get; set; }
    public string CameraNeedleDistanceYLinkedVarNeedle2 { get; set; }
    public string NeedleOffsetXLinkedVarNeedle2 { get; set; }
    public string NeedleOffsetYLinkedVarNeedle2 { get; set; }
    public string SelectedGroup { get; set; }
    public RunMode CurrentRunMode { get; set; }
    /// <summary>各组配置（独立于 WorkOrder，含组名与拍照位行）</summary>
    public List<VisionCaptureGroupConfig> Groups { get; set; } = new();
    /// <summary>当前组拍照位行（兼容旧配置；新保存时与 Groups 中当前组同步）</summary>
    public List<PhotoPositionRowConfig> Rows { get; set; } = new();
}

/// <summary>
/// VisionCapture 单个组的持久化模型
/// </summary>
public class VisionCaptureGroupConfig
{
    /// <summary>组名称</summary>
    public string Name { get; set; }
    /// <summary>该组下的拍照位行配置</summary>
    public List<PhotoPositionRowConfig> Rows { get; set; } = new();
}

/// <summary>
/// 单个拍照位配置持久化模型
/// </summary>
public class PhotoPositionRowConfig
{
    /// <summary>拍照位名称（手动输入，对应位置编辑器位置名）。新主字段。</summary>
    public string PositionName { get; set; }
    /// <summary>旧版名称字段，保留用于兼容旧配置加载（加载时迁移到 PositionName）</summary>
    public string SiteFeatureName { get; set; }
    // 旧版位置名引用字段，保留用于兼容旧配置加载（加载时迁移到 PositionName）
    public string DxPositionName { get; set; }
    public string DyPositionName { get; set; }
    public string Dz1PositionName { get; set; }
    public string YPositionName { get; set; }
    public double Speed { get; set; } = 10.0;
    public string TriggerCommand { get; set; } = "TRIGGER";
    public string ConnectionName { get; set; }
    public int Timeout { get; set; } = 5000;
    public DispenseType DispenseType { get; set; }
    public int ArcSegments { get; set; } = 20;
    public double ArcHeight { get; set; }
    public double ArcDirection { get; set; }
    public bool ReturnToSafeAfterCapture { get; set; } = true;
    public double NeedleOffsetX { get; set; }
    public double NeedleOffsetY { get; set; }
    /// <summary>针头X偏移表达式，如 0.5 或 0.1+0.2</summary>
    public string NeedleOffsetXExpression { get; set; }
    /// <summary>针头Y偏移表达式</summary>
    public string NeedleOffsetYExpression { get; set; }
    public string OffsetXExpression { get; set; }
    public string OffsetYExpression { get; set; }
    public double NeedleCompensationX { get; set; }
    public double NeedleCompensationY { get; set; }
    public string CompensationXExpression { get; set; }
    public string CompensationYExpression { get; set; }

    /// <summary>针头1(Dz₂) Dot 模式工艺参数</summary>
    public DotProcessParams DotParamsNeedle1 { get; set; } = new DotProcessParams();
    /// <summary>针头2(Dz₃) Dot 模式工艺参数</summary>
    public DotProcessParams DotParamsNeedle2 { get; set; } = new DotProcessParams();
    /// <summary>针头1(Dz₂) 路径模式工艺参数</summary>
    public DispenseSegment ArcParamsNeedle1 { get; set; } = new DispenseSegment();
    /// <summary>针头2(Dz₃) 路径模式工艺参数</summary>
    public DispenseSegment ArcParamsNeedle2 { get; set; } = new DispenseSegment();

    /// <summary>Dot(单点)模式工艺参数（旧兼容，等价于 DotParamsNeedle1）</summary>
    public DotProcessParams DotParams { get => DotParamsNeedle1; set => DotParamsNeedle1 = value; }
    /// <summary>Arc(连续点胶)模式工艺参数（旧兼容，等价于 ArcParamsNeedle1）</summary>
    public DispenseSegment ArcParams { get => ArcParamsNeedle1; set => ArcParamsNeedle1 = value; }
    /// <summary>Arc 模式轨迹子类型：弧线/直线（旧字段，保留兼容）</summary>
    public ArcTrackType ArcTrackType { get; set; } = ArcTrackType.Arc;
    /// <summary>轨迹类型覆盖；null 表示旧配置缺失，加载时按 DispenseType/ArcTrackType 迁移</summary>
    public TrajectoryType? TrajectoryOverride { get; set; }
}

/// <summary>
/// 配方池中记录当前加载文件的扩展数据
/// </summary>
public class VisionCaptureFileRecord
{
    public string FilePath { get; set; }
}
