using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Core.Abstraction;
using Core.Events;
using Core.Models;
using Core.Services;
using Microsoft.Win32;
using Module.Services;
using MotionControl.Interfaces;
using Prism.Commands;
using Prism.Events;
using Prism.Ioc;
using Prism.Mvvm;
using Recipe.Interfaces;
using StationTasks.Params;

namespace Module.ViewModels
{
    /// <summary>
    /// CadPointEditorControl 的核心协调 ViewModel——管理 6 步操作流程、画布状态、轨迹段和命令分发
    /// 职责：步骤流转控制 + 画布属性代理 + DXF 导入/ROI 工具/坐标对齐/执行走胶的命令处理
    /// 设计为独立可复用，DI 服务注入可为 null（无运动卡时自动禁用相关功能）
    /// </summary>
    public class CadPointEditorViewModel : BindableBase
    {
        #region 视口请求事件

        /// <summary>
        /// 请求画布执行 FitToAll 自适应视口的事件
        /// 由 CadPointEditorControl 订阅并调用 halconCanvas.FitToAll()
        /// </summary>
        public event Action FitToAllRequested;

        /// <summary>
        /// 请求画布执行 ResetView 重置视口的事件
        /// 由 CadPointEditorControl 订阅并调用 halconCanvas.ResetView()
        /// </summary>
        public event Action ResetViewRequested;

        /// <summary>
        /// 请求画布执行 RenderEntities 刷新渲染的事件
        /// 由 CadPointEditorControl 订阅并调用 halconCanvas.RenderEntities()
        /// 用于采样点数变更后确保画布可靠刷新
        /// </summary>
        public event Action CanvasRefreshRequested;

        #endregion

        #region DI 注入的服务

        // DXF 解析服务（解析 .dxf 文件为 CadEntity 集合）
        private readonly IDxfParserService _dxfParser;

        // ✅ 新增：DXF 统一导入服务（保证与其他 ViewModel 使用相同导入逻辑）
        private readonly IDxfImportHelper _dxfImportHelper;

        // ROI 工具服务（创建和采样 ROI 区域）
        private readonly IRoiToolService _roiToolService;

        // 坐标对齐服务（CAD→机械坐标系映射转换）
        private readonly ICoordinateAlignService _alignService;

        // 点胶执行服务（连续插补走胶/空跑）
        private readonly IDispenseExecuteService _dispenseExecuteService;

        // 运动控制服务（读取轴位置、示教高度）
        private readonly IMotionService _motionService;

        private readonly IDispenseSegmentStore _dispenseSegmentStore;
        private readonly IEventAggregator _eventAggregator;

        // 本地化服务（多语言支持）
        private ILocalizationService _localizationService;

        #endregion

        #region 步骤信息模型

        /// <summary>
        /// 操作步骤信息模型——描述每个步骤的显示内容和状态
        /// </summary>
        public class StepInfo : BindableBase
        {
            private int _number;
            /// <summary>步骤序号（1~6）</summary>
            public int Number { get => _number; set => SetProperty(ref _number, value); }

            private string _title = string.Empty;
            /// <summary>步骤标题文字</summary>
            public string Title { get => _title; set => SetProperty(ref _title, value); }

            private string _icon = string.Empty;
            /// <summary>MaterialDesign PackIcon Kind 名称</summary>
            public string Icon { get => _icon; set => SetProperty(ref _icon, value); }

            private string _hint = string.Empty;
            /// <summary>操作提示文字</summary>
            public string Hint { get => _hint; set => SetProperty(ref _hint, value); }

            private bool _isCompleted;
            /// <summary>该步骤是否已完成</summary>
            public bool IsCompleted { get => _isCompleted; set => SetProperty(ref _isCompleted, value); }

            private bool _isCurrent;
            /// <summary>该步骤是否为当前激活步骤</summary>
            public bool IsCurrent { get => _isCurrent; set => SetProperty(ref _isCurrent, value); }

            /// <summary>是否显示右侧连接线（最后一步不显示）</summary>
            public bool ShowConnector => Number < 6;

            /// <summary>连接线颜色（根据完成状态切换）</summary>
            public string ConnectorColor => IsCompleted ? "#4CAF50" : "#BDBDBD";
        }

        #endregion

        #region 步骤面板数据模型（用于 DataTemplateSelector）

        /// <summary>Step1 面板数据标记——导入图纸</summary>
        public class Step1PanelData { }

        /// <summary>Step2 面板数据标记——确认轨迹</summary>
        public class Step2PanelData { }

        /// <summary>Step3 面板数据标记——编辑参数</summary>
        public class Step3PanelData { }

        /// <summary>Step4 面板数据标记——坐标对齐</summary>
        public class Step4PanelData { }

        /// <summary>Step5 面板数据标记——预览仿真</summary>
        public class Step5PanelData { }

        /// <summary>Step6 面板数据标记——执行走胶</summary>
        public class Step6PanelData { }

        /// <summary>
        /// 步骤面板 DataTemplate 选择器——根据 Content 的具体类型返回对应步骤的 DataTemplate
        /// </summary>
        public class StepPanelTemplateSelector : System.Windows.Controls.DataTemplateSelector
        {
            public override System.Windows.DataTemplate SelectTemplate(object item, System.Windows.DependencyObject container)
            {
                return item switch
                {
                    Step1PanelData => FindTemplate("Step1Template", container),
                    Step2PanelData => FindTemplate("Step2Template", container),
                    Step3PanelData => FindTemplate("Step3Template", container),
                    Step4PanelData => FindTemplate("Step4Template", container),
                    Step5PanelData => FindTemplate("Step5Template", container),
                    Step6PanelData => FindTemplate("Step6Template", container),
                    _ => base.SelectTemplate(item, container)
                };
            }

            // 在 ContentControl 的 Resources 中查找指定 key 的 DataTemplate
            private static System.Windows.DataTemplate FindTemplate(string key, System.Windows.DependencyObject container)
            {
                var cc = container as System.Windows.FrameworkElement ??
                         (container as System.Windows.Controls.ContentPresenter);
                if (cc?.TryFindResource(key) is System.Windows.DataTemplate template)
                    return template;
                return null;
            }
        }

        #endregion

        #region 图层可见性模型

        /// <summary>
        /// 图层可见性项模型——用于 Step2 图层列表 CheckBox 绑定
        /// </summary>
        public class LayerCheckItem : BindableBase
        {
            private string _layerName;
            /// <summary>图层名称</summary>
            public string LayerName { get => _layerName; set => SetProperty(ref _layerName, value); }

            private bool _isVisible = true;
            /// <summary>该图层是否可见</summary>
            public bool IsVisible { get => _isVisible; set => SetProperty(ref _isVisible, value); }
        }

        #endregion

        #region 私有字段 — 步骤管理

        // 当前步骤序号（1~6）
        private int _currentStep = 1;

        // 已解析的 DXF 结果缓存（用于 Step2 图层筛选等后续操作）
        private DxfParseResult _parsedDxfResult;

        // 离散化采样间距（mm），用于将图元转为 DispenseSegment 的点序列
        private const double DefaultDiscretizePitchMM = 0.5;

        #endregion

        #region 私有字段 — 仿真状态

        // 仿真进度百分比 (0~100)
        private double _simProgress;

        // 是否正在仿真中
        private bool _isSimulating;

        // 仿真状态文本
        private string _simStatusText;

        // 仿真取消令牌源
        private System.Threading.CancellationTokenSource _simCts;

        #endregion

        #region 绑定属性 — 步骤管理

        private int _currentStepValue = 1;
        /// <summary>当前操作步骤（1~6）</summary>
        public int CurrentStep
        {
            get => _currentStepValue;
            set
            {
                if (SetProperty(ref _currentStepValue, value) && value >= 1 && value <= 6)
                {
                    UpdateStepStates(value);
                    // 同步切换右侧面板数据对象（触发 DataTemplateSelector 选择对应模板）
                    CurrentStepPanel = value switch
                    {
                        1 => new Step1PanelData(),
                        2 => new Step2PanelData(),
                        3 => new Step3PanelData(),
                        4 => new Step4PanelData(),
                        5 => new Step5PanelData(),
                        6 => new Step6PanelData(),
                        _ => null
                    };
                    RaisePropertyChanged(nameof(CurrentStepTitle));
                }
            }
        }

        /// <summary>6 个步骤信息集合（绑定到 Step Indicator ItemsControl）</summary>
        public ObservableCollection<StepInfo> Steps { get; } = new();

        /// <summary>当前步骤标题文本（绑定到状态栏）</summary>
        public string CurrentStepTitle => _currentStepValue >= 1 && _currentStepValue <= Steps.Count
            ? Steps[_currentStepValue - 1].Title : string.Empty;

        #endregion

        #region 绑定属性 — 画布代理

        private ObservableCollection<CadEntity> _canvasEntities = new();
        /// <summary>画布渲染的 CAD 图元集合（代理到 HalconCanvasControl.Entities）</summary>
        public ObservableCollection<CadEntity> CanvasEntities
        {
            get => _canvasEntities;
            set => SetProperty(ref _canvasEntities, value);
        }

        private CadEntity _selectedEntity;
        /// <summary>当前选中的 CAD 图元（双向绑定到 HalconCanvasControl）</summary>
        public CadEntity SelectedEntity
        {
            get => _selectedEntity;
            set
            {
                if (SetProperty(ref _selectedEntity, value))
                {
                    // 画布点击选中图元时，同步更新 SelectedSegment
                    SyncSelectedSegmentFromEntity(value);
                }
            }
        }

        private double _zoomFactor = 1.0;
        /// <summary>缩放比例（双向绑定到 HalconCanvasControl.ZoomFactor）</summary>
        public double ZoomFactor
        {
            get => _zoomFactor;
            set => SetProperty(ref _zoomFactor, value);
        }

        private double _panOffsetX;
        /// <summary>X 平移偏移（双向绑定到 HalconCanvasControl.PanOffsetX）</summary>
        public double PanOffsetX
        {
            get => _panOffsetX;
            set => SetProperty(ref _panOffsetX, value);
        }

        private double _panOffsetY;
        /// <summary>Y 平移偏移（双向绑定到 HalconCanvasControl.PanOffsetY）</summary>
        public double PanOffsetY
        {
            get => _panOffsetY;
            set => SetProperty(ref _panOffsetY, value);
        }

        private bool _showGrid;
        /// <summary>是否显示网格（绑定到 HalconCanvasControl.ShowGrid）</summary>
        public bool ShowGrid
        {
            get => _showGrid;
            set => SetProperty(ref _showGrid, value);
        }

        private RoiRegion _currentRoiPreview;
        /// <summary>当前正在绘制的 ROI 预览区域</summary>
        public RoiRegion CurrentRoiPreview
        {
            get => _currentRoiPreview;
            set
            {
                if (SetProperty(ref _currentRoiPreview, value))
                {
                    ConfirmRoiCommand.RaiseCanExecuteChanged();
                }
            }
        }

        // 实时坐标显示字符串 X 分量
        private string _coordinateDisplay = "---";
        /// <summary>X 坐标显示文本（绑定到底部状态栏）</summary>
        public string CoordinateDisplay
        {
            get => _coordinateDisplay;
            set => SetProperty(ref _coordinateDisplay, value);
        }

        // 实时坐标显示字符串 Y 分量
        private string _coordinateDisplayY = "";
        /// <summary>Y 坐标显示文本（绑定到底部状态栏）</summary>
        public string CoordinateDisplayY
        {
            get => _coordinateDisplayY;
            set => SetProperty(ref _coordinateDisplayY, value);
        }

        #endregion

        #region 绑定属性 — 步骤面板选择

        // 当前步骤面板数据对象（用于 ContentControl 的 DataTemplate 选择）
        private object _currentStepPanel;
        /// <summary>
        /// 当前步骤对应的面板数据对象——ContentControl 通过 DataTemplateSelector 根据此对象的类型选择显示哪个步骤面板
        /// 每次 CurrentStep 变化时自动更新
        /// </summary>
        public object CurrentStepPanel
        {
            get => _currentStepPanel;
            set => SetProperty(ref _currentStepPanel, value);
        }

        #endregion

        #region 绑定属性 — 轨迹段管理

        // 所有轨迹段集合
        private ObservableCollection<DispenseSegment> _segments = new();
        /// <summary>所有轨迹段集合（输出给外部宿主使用）</summary>
        public ObservableCollection<DispenseSegment> Segments
        {
            get => _segments;
            set => SetProperty(ref _segments, value);
        }

        private DispenseSegment _selectedSegment;
        /// <summary>DataGrid 当前选中的轨迹段</summary>
        public DispenseSegment SelectedSegment
        {
            get => _selectedSegment;
            set
            {
                if (_selectedSegment != null)
                    _selectedSegment.PropertyChanged -= OnSelectedSegmentParamChanged;

                if (SetProperty(ref _selectedSegment, value))
                {
                    if (_selectedSegment != null)
                        _selectedSegment.PropertyChanged += OnSelectedSegmentParamChanged;

                    RaisePropertyChanged(nameof(HasSelectedSegment));
                    RaisePropertyChanged(nameof(ShowContinuousInterpolationParams));
                    SelectedSegmentPoints = value?.Points;
                    SegmentSplitCount = value?.SamplePointCount > 0 ? value.SamplePointCount : value?.Points?.Count ?? 1;
                    SyncSelectedEntityFromSegment(value);
                    ApplySegmentSplitCommand.RaiseCanExecuteChanged();
                    ExtractCADZValuesCommand.RaiseCanExecuteChanged();

                    _dispenseSegmentStore.CurrentSelectedSegment = _selectedSegment;
                    _eventAggregator?.GetEvent<SelectedSegmentChangedEvent>().Publish(
                        new SelectedSegmentPayload { Segment = _selectedSegment });
                }
            }
        }

        /// <summary>段工艺参数属性名集合——用于过滤 PropertyChanged 事件</summary>
        private static readonly HashSet<string> ParamPropertyNames = new()
        {
            nameof(DispenseSegment.JumpSpeed),
            nameof(DispenseSegment.MoveSpeed),
            nameof(DispenseSegment.SafeHeight),
            nameof(DispenseSegment.ApproachHeight),
            nameof(DispenseSegment.CornerDecel),
            nameof(DispenseSegment.DispenseAmount),
            nameof(DispenseSegment.PreDelay),
            nameof(DispenseSegment.PostDelay),
            nameof(DispenseSegment.DispensingPressure),
            nameof(DispenseSegment.SuckBackTime),
            nameof(DispenseSegment.GlueTriggerOffsetMm),
            nameof(DispenseSegment.TeachHeight),
            nameof(DispenseSegment.HeightCompensation)
        };

        /// <summary>
        /// 监听选中段工艺参数变更——发布反向同步事件到 DispenseDetailViewModel
        /// </summary>
        private void OnSelectedSegmentParamChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!ParamPropertyNames.Contains(e.PropertyName)) return;

            _eventAggregator?.GetEvent<SegmentParamChangedEvent>().Publish(new SegmentParamPayload
            {
                PropertyName = e.PropertyName,
                Segment = _selectedSegment
            });
        }

        /// <summary>是否有选中段（控制 Expander 可见性）</summary>
        public bool HasSelectedSegment => _selectedSegment != null;
        private List<CadPoint> _selectedSegmentPoints;
        /// <summary>选中轨迹段的采样点集合——在 Halcon 图形上用 X 标记显示</summary>
        public List<CadPoint> SelectedSegmentPoints
        {
            get => _selectedSegmentPoints;
            set
            {
                // 创建新列表副本，确保 DataGrid 刷新显示
                // 直接引用 segment.Points 时，RenumberPoints 修改 Id 属性
                // 但 DataGrid 绑定 List 不会自动刷新单元格值
                List<CadPoint> points = value != null ? new List<CadPoint>(value) : null;
                if (points != null)
                    RenumberPoints(points);
                SetProperty(ref _selectedSegmentPoints, points);
            }
        }

        private int _segmentSplitCount = 1;
        /// <summary>当前选中段的采样点数（用于重新离散化）</summary>
        public int SegmentSplitCount
        {
            get => _segmentSplitCount;
            set
            {
                if (SetProperty(ref _segmentSplitCount, value))
                    ApplySegmentSplitCommand.RaiseCanExecuteChanged();
            }
        }

        #endregion

        #region 绑定属性 — Step1: 文件导入

        private string _filePath = string.Empty;
        /// <summary>用户选择的 DXF 文件路径</summary>
        public string FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value);
        }

        /// <summary>是否已选择文件路径（控制导入按钮启用状态）</summary>
        public bool HasFilePath => !string.IsNullOrWhiteSpace(_filePath);

        private string _importStatusMessage = string.Empty;
        /// <summary>导入操作的状态消息（显示在 Step1 面板底部）</summary>
        public string ImportStatusMessage
        {
            get => _importStatusMessage;
            set => SetProperty(ref _importStatusMessage, value);
        }

        private string _segmentFilePath = string.Empty;
        /// <summary>轨迹段配置文件路径（绑定到 Step1 轨迹段加载卡片输入框）</summary>
        public string SegmentFilePath
        {
            get => _segmentFilePath;
            set
            {
                if (SetProperty(ref _segmentFilePath, value))
                {
                    RaisePropertyChanged(nameof(HasSegmentFilePath));
                    LoadSegmentsCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>是否已选择轨迹段文件路径（控制加载按钮启用状态）</summary>
        public bool HasSegmentFilePath => !string.IsNullOrWhiteSpace(_segmentFilePath);

        #endregion

        #region 绑定属性 — Step2: 确认轨迹

        private ObservableCollection<LayerCheckItem> _layerCheckList = new();
        /// <summary>图层可见性列表（绑定到 Step2 ListBox）</summary>
        public ObservableCollection<LayerCheckItem> LayerCheckList
        {
            get => _layerCheckList;
            set => SetProperty(ref _layerCheckList, value);
        }

        private List<string> _layerNames = new();
        /// <summary>图层名称列表（绑定到工具栏 ComboBox）</summary>
        public List<string> LayerNames
        {
            get => _layerNames;
            set => SetProperty(ref _layerNames, value);
        }

        private string _selectedLayer;
        /// <summary>工具栏当前选中的图层名</summary>
        public string SelectedLayer
        {
            get => _selectedLayer;
            set
            {
                if (SetProperty(ref _selectedLayer, value))
                    ApplyLayerFilter();
            }
        }

        /// <summary>轨迹段摘要文本（如 "检测到 N 条轨迹段，M 个图层"）</summary>
        public string SegmentSummaryDisplay =>
            _parsedDxfResult != null
                ? string.Format(L("CadPoint_Status_Summary"), _parsedDxfResult.TotalEntityCount, _parsedDxfResult.LayerNames.Count)
                : L("CadPoint_Status_NoData");

        #endregion

        #region 绑定属性 — Step3: 编辑参数（ROI 工具）

        private bool _isLineRoiActive;
        /// <summary>线段 ROI 工具是否激活</summary>
        public bool IsLineRoiActive
        {
            get => _isLineRoiActive;
            set
            {
                if (SetProperty(ref _isLineRoiActive, value) && value)
                    DeactivateOtherRois("line");
            }
        }

        private bool _isPolylineRoiActive;
        /// <summary>折线 ROI 工具是否激活</summary>
        public bool IsPolylineRoiActive
        {
            get => _isPolylineRoiActive;
            set
            {
                if (SetProperty(ref _isPolylineRoiActive, value) && value)
                    DeactivateOtherRois("polyline");
            }
        }

        private bool _isArcRoiActive;
        /// <summary>圆弧 ROI 工具是否激活</summary>
        public bool IsArcRoiActive
        {
            get => _isArcRoiActive;
            set
            {
                if (SetProperty(ref _isArcRoiActive, value) && value)
                    DeactivateOtherRois("arc");
            }
        }

        #endregion

        #region 绑定属性 — Step4: 坐标对齐

        private AlignMode _alignMode = AlignMode.Affine;
        /// <summary>当前坐标对齐模式</summary>
        public AlignMode AlignMode
        {
            get => _alignMode;
            set => SetProperty(ref _alignMode, value);
        }

        private bool _isModeAffine = true;
        /// <summary>是否为N点仿射模式</summary>
        public bool IsModeAffine
        {
            get => _isModeAffine;
            set
            {
                if (SetProperty(ref _isModeAffine, value) && value)
                {
                    IsModePointMapping = false;
                    AlignMode = AlignMode.Affine;
                }
            }
        }

        private bool _isModePointMapping;
        /// <summary>是否为逐点映射模式</summary>
        public bool IsModePointMapping
        {
            get => _isModePointMapping;
            set
            {
                if (SetProperty(ref _isModePointMapping, value) && value)
                {
                    IsModeAffine = false;
                    AlignMode = AlignMode.PointMapping;
                }
            }
        }

        private string _transformStatus = string.Empty;
        /// <summary>坐标变换状态提示文本</summary>
        public string TransformStatus
        {
            get => _transformStatus;
            set => SetProperty(ref _transformStatus, value);
        }

        private string _transformMatrixDisplay = string.Empty;
        /// <summary>变换矩阵参数文本（如 "Tx=12.3 Ty=-5.1 θ=2.5° S=1.00"）</summary>
        public string TransformMatrixDisplay
        {
            get => _transformMatrixDisplay;
            set => SetProperty(ref _transformMatrixDisplay, value);
        }

        private ObservableCollection<CadPoint> _transformedPointsPreview = new ObservableCollection<CadPoint>();
        /// <summary>变换后坐标预览（前5个点，用于Expander折叠区显示）</summary>
        public ObservableCollection<CadPoint> TransformedPointsPreview
        {
            get => _transformedPointsPreview;
            set => SetProperty(ref _transformedPointsPreview, value);
        }

        #endregion

        #region 绑定属性 — Step4: N点仿射标定

        private List<AffineCalibrationPoint> _affineCalibrationPointsNeedle1 = new();
        private List<AffineCalibrationPoint> _affineCalibrationPointsNeedle2 = new();
        private ObservableCollection<AffineCalibrationPoint> _affineCalibrationPoints = new();

        /// <summary>针头1仿射标定点集合（备份）</summary>
        public List<AffineCalibrationPoint> AffineCalibrationPointsNeedle1
        {
            get => _affineCalibrationPointsNeedle1;
            set => SetProperty(ref _affineCalibrationPointsNeedle1, value);
        }

        /// <summary>针头2仿射标定点集合（备份）</summary>
        public List<AffineCalibrationPoint> AffineCalibrationPointsNeedle2
        {
            get => _affineCalibrationPointsNeedle2;
            set => SetProperty(ref _affineCalibrationPointsNeedle2, value);
        }

        /// <summary>当前针头的仿射标定点集合（根据CurrentNeedleIndex切换）</summary>
        public ObservableCollection<AffineCalibrationPoint> AffineCalibrationPoints
        {
            get => _affineCalibrationPoints;
            set => SetProperty(ref _affineCalibrationPoints, value);
        }

        private AffineCalibrationPoint _selectedAffinePoint;
        /// <summary>当前选中的仿射标定点</summary>
        public AffineCalibrationPoint SelectedAffinePoint
        {
            get => _selectedAffinePoint;
            set => SetProperty(ref _selectedAffinePoint, value);
        }

        private AffineCalibrationResult _affineResultNeedle1;
        private AffineCalibrationResult _affineResultNeedle2;
        private AffineCalibrationResult _affineResult;

        /// <summary>针头1仿射标定计算结果</summary>
        public AffineCalibrationResult AffineResultNeedle1
        {
            get => _affineResultNeedle1;
            set => SetProperty(ref _affineResultNeedle1, value);
        }

        /// <summary>针头2仿射标定计算结果</summary>
        public AffineCalibrationResult AffineResultNeedle2
        {
            get => _affineResultNeedle2;
            set => SetProperty(ref _affineResultNeedle2, value);
        }

        /// <summary>当前针头的仿射标定计算结果（根据CurrentNeedleIndex切换）</summary>
        public AffineCalibrationResult AffineResult
        {
            get => _affineResult;
            set
            {
                if (SetProperty(ref _affineResult, value))
                {
                    // 同步到对应针头的结果
                    if (_currentNeedleIndex == 0)
                        _affineResultNeedle1 = value;
                    else
                        _affineResultNeedle2 = value;
                    RaisePropertyChanged(nameof(HasAffineResult));
                    RaisePropertyChanged(nameof(AffineQualityText));
                    RaisePropertyChanged(nameof(AffineResultDisplay));
                    ApplyTransformToSegmentsCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>是否已有仿射计算结果</summary>
        public bool HasAffineResult => _affineResult != null;

        /// <summary>仿射标定质量评级文本</summary>
        public string AffineQualityText
        {
            get
            {
                if (_affineResult == null) return string.Empty;
                var grade = _affineResult.RmsError < 0.05 ? L("Step4_Affine_Quality_Good")
                          : _affineResult.RmsError < 0.10 ? L("Step4_Affine_Quality_Acceptable")
                          : L("Step4_Affine_Quality_Poor");
                return $"{L("Step4_Affine_RmsLabel")}: {_affineResult.RmsError:F4}mm | {grade}";
            }
        }

        /// <summary>仿射参数结果文本</summary>
        public string AffineResultDisplay
        {
            get
            {
                if (_affineResult == null) return string.Empty;
                return $"A={_affineResult.A:F4} B={_affineResult.B:F4} C={_affineResult.C:F4} D={_affineResult.D:F4}\n"
                     + $"Tx={_affineResult.Tx:F3} Ty={_affineResult.Ty:F3} | "
                     + $"θ={_affineResult.EquivalentRotationDeg:F2}° Sx={_affineResult.EquivalentScaleX:F4} Sy={_affineResult.EquivalentScaleY:F4}";
            }
        }

        /// <summary>是否正在从画布选取仿射CAD坐标</summary>
        private bool _isPickingAffineCadCoord;

        #endregion

        #region 绑定属性 — Step4: 逐点映射

        private List<PointMappingPoint> _pointMappingPointsNeedle1 = new();
        private List<PointMappingPoint> _pointMappingPointsNeedle2 = new();
        private ObservableCollection<PointMappingPoint> _pointMappingPoints = new();
        /// <summary>逐点映射点集合</summary>
        public ObservableCollection<PointMappingPoint> PointMappingPoints
        {
            get => _pointMappingPoints;
            set => SetProperty(ref _pointMappingPoints, value);
        }

        private PointMappingPoint _selectedMappingPoint;
        /// <summary>当前选中的逐点映射点</summary>
        public PointMappingPoint SelectedMappingPoint
        {
            get => _selectedMappingPoint;
            set => SetProperty(ref _selectedMappingPoint, value);
        }

        /// <summary>是否正在从画布选取映射CAD坐标</summary>
        private bool _isPickingMappingCadCoord;

        #endregion

        #region 绑定属性 — 双针头选择

        private int _currentNeedleIndex;
        private int _previousNeedleIndex; // 追踪上一次针头索引，用于保存数据

        /// <summary>当前针头索引（0=Dz1/针头1, 1=Dz2/针头2）</summary>
        public int CurrentNeedleIndex
        {
            get => _currentNeedleIndex;
            set
            {
                if (SetProperty(ref _currentNeedleIndex, value))
                {
                    RaisePropertyChanged(nameof(IsNeedle1Selected));
                    RaisePropertyChanged(nameof(IsNeedle2Selected));
                    SwitchNeedleData();
                }
            }
        }

        /// <summary>切换针头时保存当前数据并加载目标针头数据</summary>
        private void SwitchNeedleData()
        {
            // 1. 保存当前数据到上一次针头的备份
            SaveCurrentNeedleData(_previousNeedleIndex);

            // 2. 加载目标针头的数据
            LoadNeedleData(_currentNeedleIndex);

            // 3. 更新上一次索引
            _previousNeedleIndex = _currentNeedleIndex;
        }

        /// <summary>保存指定针头的仿射标定数据和逐点映射数据</summary>
        private void SaveCurrentNeedleData(int needleIndex)
        {
            if (needleIndex == 0)
            {
                _affineCalibrationPointsNeedle1 = new List<AffineCalibrationPoint>(_affineCalibrationPoints);
                _affineResultNeedle1 = _affineResult;
                _pointMappingPointsNeedle1 = new List<PointMappingPoint>(_pointMappingPoints);
            }
            else
            {
                _affineCalibrationPointsNeedle2 = new List<AffineCalibrationPoint>(_affineCalibrationPoints);
                _affineResultNeedle2 = _affineResult;
                _pointMappingPointsNeedle2 = new List<PointMappingPoint>(_pointMappingPoints);
            }
        }

        /// <summary>加载指定针头的仿射标定数据和逐点映射数据到UI集合</summary>
        private void LoadNeedleData(int needleIndex)
        {
            // 切换仿射标定点（内容交换，保持同一ObservableCollection引用）
            var affineSource = needleIndex == 0 ? _affineCalibrationPointsNeedle1 : _affineCalibrationPointsNeedle2;
            _affineCalibrationPoints.Clear();
            foreach (var p in affineSource)
                _affineCalibrationPoints.Add(p);

            // 切换仿射结果
            _affineResult = needleIndex == 0 ? _affineResultNeedle1 : _affineResultNeedle2;
            RaisePropertyChanged(nameof(AffineResult));
            RaisePropertyChanged(nameof(HasAffineResult));
            RaisePropertyChanged(nameof(AffineQualityText));
            RaisePropertyChanged(nameof(AffineResultDisplay));
            ApplyTransformToSegmentsCommand.RaiseCanExecuteChanged();

            // 切换逐点映射点（内容交换）
            var mappingSource = needleIndex == 0 ? _pointMappingPointsNeedle1 : _pointMappingPointsNeedle2;
            _pointMappingPoints.Clear();
            foreach (var p in mappingSource)
                _pointMappingPoints.Add(p);

            ComputeAffineTransformCommand.RaiseCanExecuteChanged();
            DeleteAffinePointCommand.RaiseCanExecuteChanged();
        }

        /// <summary>是否选中针头1（Dz1）</summary>
        public bool IsNeedle1Selected
        {
            get => _currentNeedleIndex == 0;
            set { if (value) CurrentNeedleIndex = 0; }
        }

        /// <summary>是否选中针头2（Dz2）</summary>
        public bool IsNeedle2Selected
        {
            get => _currentNeedleIndex == 1;
            set { if (value) CurrentNeedleIndex = 1; }
        }

        #endregion

        #region 绑定属性 — Step5 & Step6: 执行

        /// <summary>可选线段ID列表（绑定到 Step6 下拉框）</summary>
        public ObservableCollection<string> SegmentIds { get; } = new();

        private string _selectedSegmentId;
        /// <summary>当前选中的线段ID（选中时画布高亮对应线段）</summary>
        public string SelectedSegmentId
        {
            get => _selectedSegmentId;
            set
            {
                if (SetProperty(ref _selectedSegmentId, value))
                {
                    var seg = Segments.FirstOrDefault(s => s.SegmentId == value);
                    if (seg != null)
                        SelectedSegment = seg;
                }
            }
        }

        private bool _zCorrectionEnabled = true;
        /// <summary>是否启用 Z 高度校正</summary>
        public bool ZCorrectionEnabled
        {
            get => _zCorrectionEnabled;
            set => SetProperty(ref _zCorrectionEnabled, value);
        }

        private LineDispenseMode _lineDispenseMode = LineDispenseMode.ContinuousInterpolation;
        /// <summary>线条点胶操作模式（单点/连续插补）</summary>
        public LineDispenseMode LineDispenseMode
        {
            get => _lineDispenseMode;
            set
            {
                if (SetProperty(ref _lineDispenseMode, value))
                {
                    RaisePropertyChanged(nameof(IsSinglePointMode));
                    RaisePropertyChanged(nameof(IsContinuousInterpolationMode));
                    RaisePropertyChanged(nameof(ShowContinuousInterpolationParams));
                    RaisePropertyChanged(nameof(ExecuteButtonText));
                    RaisePropertyChanged(nameof(CanExecute));
                }
            }
        }

        /// <summary>是否为单点模式（便捷属性，供UI绑定）</summary>
        public bool IsSinglePointMode
        {
            get => _lineDispenseMode == LineDispenseMode.SinglePoint;
            set { if (value) LineDispenseMode = LineDispenseMode.SinglePoint; }
        }

        /// <summary>是否为连续插补模式（便捷属性，供UI绑定）</summary>
        public bool IsContinuousInterpolationMode
        {
            get => _lineDispenseMode == LineDispenseMode.ContinuousInterpolation;
            set { if (value) LineDispenseMode = LineDispenseMode.ContinuousInterpolation; }
        }

        /// <summary>是否允许执行（有轨迹段且非仿真中）</summary>
        public bool CanExecute => Segments.Any(s => s.IsEnabled) && !_isSimulating;

        private DotProcessParams _singlePointProcessParams = new DotProcessParams();
        /// <summary>单点模式全局工艺参数（复用点涂A参数体系）</summary>
        public DotProcessParams SinglePointProcessParams
        {
            get => _singlePointProcessParams;
            set => SetProperty(ref _singlePointProcessParams, value);
        }

        private double _standbyHeight = 10.0;
        /// <summary>待机高度 mm（单点模式循环结束后Z轴抬升目标，范围 0~200）</summary>
        public double StandbyHeight
        {
            get => _standbyHeight;
            set => SetProperty(ref _standbyHeight, Math.Clamp(value, 0.0, 200.0));
        }

        /// <summary>是否显示连续插补段参数编辑区（有选中段 且 为连续插补模式）</summary>
        public bool ShowContinuousInterpolationParams => HasSelectedSegment && IsContinuousInterpolationMode;

        /// <summary>执行按钮文本（根据模式动态切换）</summary>
        public string ExecuteButtonText => IsSinglePointMode
            ? L("Step6_Btn_ExecuteSinglePoint")
            : L("Step6_Btn_ExecutePath");

        #endregion

        #region 绑定属性 — 全局状态栏

        private string _globalStatus;
        /// <summary>全局状态文本（显示在底部状态栏左侧）</summary>
        public string GlobalStatus
        {
            get => _globalStatus;
            set => SetProperty(ref _globalStatus, value);
        }

        /// <summary>已选轨迹段数量显示文本</summary>
        public string SegmentCountDisplay => string.Format(L("CadPoint_SegmentCount_Format"), Segments.Count(s => s.IsEnabled));

        /// <summary>已选轨迹段总长度显示文本</summary>
        public string TotalLengthDisplay => string.Format(L("CadPoint_TotalLength_Format"), Segments.Where(s => s.IsEnabled).Sum(s => s.Length));

        /// <summary>坐标对齐状态显示文本</summary>
        public string AlignStatusDisplay
        {
            get
            {
                if (_alignService == null) return L("CadPoint_AlignStatus_Unavailable");
                var modeText = _alignMode == AlignMode.Affine
                    ? L("CadPoint_AlignMode_Affine")
                    : L("CadPoint_AlignMode_PointMapping");
                return string.Format(L("CadPoint_AlignStatus_Format"), modeText);
            }
        }

        /// <summary>底部状态栏 - "当前步骤:" 标签文本（本地化）</summary>
        public string CurrentStepLabelDisplay => L("CadPoint_Label_CurrentStep");

        #endregion

        #region 绑定属性 — 仿真状态

        /// <summary>仿真进度值 (0~100)</summary>
        public double SimProgress
        {
            get => _simProgress;
            set => SetProperty(ref _simProgress, value);
        }

        /// <summary>是否正在仿真中</summary>
        public bool IsSimulating
        {
            get => _isSimulating;
            set
            {
                if (SetProperty(ref _isSimulating, value))
                {
                    RaisePropertyChanged(nameof(CanExecute));
                }
            }
        }

        /// <summary>仿真状态描述文本</summary>
        public string SimStatusText
        {
            get => _simStatusText;
            set => SetProperty(ref _simStatusText, value);
        }

        private bool _isSimMode = true;
        /// <summary>空跑仿真模式（UI模拟）</summary>
        public bool IsSimMode
        {
            get => _isSimMode;
            set => SetProperty(ref _isSimMode, value);
        }

        private bool _isRealDryRunMode;
        /// <summary>真实空跑模式（运动不出胶）</summary>
        public bool IsRealDryRunMode
        {
            get => _isRealDryRunMode;
            set => SetProperty(ref _isRealDryRunMode, value);
        }

        private bool _descendInDryRun;
        /// <summary>空跑时是否下降到工作高度（false=保持在安全高度）</summary>
        public bool DescendInDryRun
        {
            get => _descendInDryRun;
            set => SetProperty(ref _descendInDryRun, value);
        }

        private bool _isRealDispenseMode = true;
        /// <summary>真实点胶模式（Step6 默认为 true，运动+出胶）</summary>
        public bool IsRealDispenseMode
        {
            get => _isRealDispenseMode;
            set => SetProperty(ref _isRealDispenseMode, value);
        }

        #endregion

        #region 委托命令 — 步骤导航

        private DelegateCommand<int?> _goToStepCommand;
        /// <summary>跳转到指定步骤命令（参数为目标步骤号 1~6）</summary>
        public DelegateCommand<int?> GoToStepCommand =>
            _goToStepCommand ??= new DelegateCommand<int?>(GoToStep, CanGoToStep);

        private DelegateCommand _goNextCommand;
        /// <summary>下一步命令</summary>
        public DelegateCommand GoNextCommand =>
            _goNextCommand ??= new DelegateCommand(() => GoToStep(CurrentStep + 1), () => CurrentStep < 6);

        private DelegateCommand _goPrevCommand;
        /// <summary>上一步命令</summary>
        public DelegateCommand GoPrevCommand =>
            _goPrevCommand ??= new DelegateCommand(() => GoToStep(CurrentStep - 1), () => CurrentStep > 1);

        #endregion

        #region 委托命令 — Step1: 文件导入

        private DelegateCommand _selectFileCommand;
        /// <summary>选择文件命令——打开文件对话框选择 .dxf 文件</summary>
        public DelegateCommand SelectFileCommand =>
            _selectFileCommand ??= new DelegateCommand(ExecuteSelectFile);

        private DelegateCommand _importDxfCommand;
        /// <summary>导入 DXF 命令——调用 DxfParser 解析文件并生成轨迹段</summary>
        public DelegateCommand ImportDxfCommand =>
            _importDxfCommand ??= new DelegateCommand(ExecuteImportDxf, () => HasFilePath);

        private DelegateCommand _loadTestEntitiesCommand;
        /// <summary>加载测试轨迹命令——生成一组测试图元验证 Halcon 窗口渲染是否正常</summary>
        public DelegateCommand LoadTestEntitiesCommand =>
            _loadTestEntitiesCommand ??= new DelegateCommand(LoadTestEntities);

        private DelegateCommand _saveSegmentsCommand;
        /// <summary>保存轨迹段到 JSON 文件命令</summary>
        public DelegateCommand SaveSegmentsCommand =>
            _saveSegmentsCommand ??= new DelegateCommand(ExecuteSaveSegments, () => Segments.Count > 0);

        private DelegateCommand _loadSegmentsCommand;
        /// <summary>从 JSON 文件加载轨迹段命令（从 SegmentFilePath 读取路径）</summary>
        public DelegateCommand LoadSegmentsCommand =>
            _loadSegmentsCommand ??= new DelegateCommand(ExecuteLoadSegments, () => HasSegmentFilePath);

        private DelegateCommand _selectSegmentFileCommand;
        /// <summary>浏览选择轨迹段配置文件命令</summary>
        public DelegateCommand SelectSegmentFileCommand =>
            _selectSegmentFileCommand ??= new DelegateCommand(ExecuteSelectSegmentFile);

        #endregion

        #region 委托命令 — Step3: 参数编辑与批量操作

        private DelegateCommand _selectAllSegmentsCommand;
        /// <summary>全选所有轨迹段命令（设置 IsEnabled = true）</summary>
        public DelegateCommand SelectAllSegmentsCommand =>
            _selectAllSegmentsCommand ??= new DelegateCommand(ExecuteSelectAllSegments);

        private DelegateCommand _invertSelectionCommand;
        /// <summary>反选轨迹段命令（IsEnabled 取反）</summary>
        public DelegateCommand InvertSelectionCommand =>
            _invertSelectionCommand ??= new DelegateCommand(ExecuteInvertSelection);

        private DelegateCommand _batchSetSpeedCommand;
        /// <summary>批量设置速度命令</summary>
        public DelegateCommand BatchSetSpeedCommand =>
            _batchSetSpeedCommand ??= new DelegateCommand(ExecuteBatchSetSpeed);

        private DelegateCommand _batchSetGlueCommand;
        /// <summary>批量设置胶量命令</summary>
        public DelegateCommand BatchSetGlueCommand =>
            _batchSetGlueCommand ??= new DelegateCommand(ExecuteBatchSetGlue);

        private DelegateCommand _batchSetAllCommand;
        /// <summary>批量设置全部参数命令（打开多参数对话框）</summary>
        public DelegateCommand BatchSetAllCommand =>
            _batchSetAllCommand ??= new DelegateCommand(ExecuteBatchSetAll);

        private DelegateCommand _deleteSelectedSegmentsCommand;
        /// <summary>删除选中轨迹段命令（删除 IsEnabled 为 true 的段）</summary>
        public DelegateCommand DeleteSelectedSegmentsCommand =>
            _deleteSelectedSegmentsCommand ??= new DelegateCommand(
                ExecuteDeleteSelectedSegments,
                () => Segments.Any(s => s.IsEnabled));

        private DelegateCommand _deleteSelectedPointCommand;
        /// <summary>删除选中轨迹段中选中的单个点位命令</summary>
        public DelegateCommand DeleteSelectedPointCommand =>
            _deleteSelectedPointCommand ??= new DelegateCommand(
                ExecuteDeleteSelectedPoint,
                () => _selectedSegment != null && _selectedPointIndex >= 0);

        private DelegateCommand<LineDispenseMode> _switchDispenseModeCommand;
        /// <summary>切换点胶模式命令</summary>
        public DelegateCommand<LineDispenseMode> SwitchDispenseModeCommand =>
            _switchDispenseModeCommand ??= new DelegateCommand<LineDispenseMode>(ExecuteSwitchDispenseMode);

        /// <summary>切换点胶模式——更新参数面板显示</summary>
        private void ExecuteSwitchDispenseMode(LineDispenseMode mode)
        {
            LineDispenseMode = mode;
            GlobalStatus = mode == LineDispenseMode.SinglePoint
                ? L("LineBC_Status_SwitchToSinglePoint")
                : L("LineBC_Status_SwitchToContinuousInterpolation");
        }

        private int _selectedPointIndex = -1;
        /// <summary>选中轨迹段中当前选中的点位索引（-1 表示未选中）</summary>
        public int SelectedPointIndex
        {
            get => _selectedPointIndex;
            set
            {
                if (SetProperty(ref _selectedPointIndex, value))
                {
                    DeleteSelectedPointCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 删除选中轨迹段中选中的单个点位
        /// </summary>
        private void ExecuteDeleteSelectedPoint()
        {
            if (_selectedSegment == null || _selectedPointIndex < 0)
                return;
            if (_selectedSegment.Points == null || _selectedPointIndex >= _selectedSegment.Points.Count)
                return;

            _selectedSegment.Points.RemoveAt(_selectedPointIndex);
            RenumberPoints(_selectedSegment.Points);
            SelectedPointIndex = -1;
            SelectedSegmentPoints = null;
            SelectedSegmentPoints = _selectedSegment.Points;
            RaisePropertyChanged(nameof(SegmentSummaryDisplay));
        }

        /// <summary>
        /// 重新编号点位序号（1, 2, 3...）
        /// </summary>
        private static void RenumberPoints(List<CadPoint> points)
        {
            if (points == null) return;
            for (int i = 0; i < points.Count; i++)
            {
                points[i].Id = (i + 1).ToString();
            }
        }

        private DelegateCommand _applySegmentSplitCommand;
        /// <summary>应用采样点数命令——按指定点数重新对选中段的原始图元进行离散化</summary>
        public DelegateCommand ApplySegmentSplitCommand =>
            _applySegmentSplitCommand ??= new DelegateCommand(ExecuteApplySegmentSplit,
                () => _selectedSegment != null && _segmentSplitCount >= 2);

        private DelegateCommand _teachHeightCommand;
        public DelegateCommand TeachHeightCommand =>
            _teachHeightCommand ??= new DelegateCommand(ExecuteTeachHeight, () => _selectedSegment != null);

        private DelegateCommand _extractCADZValuesCommand;
        public DelegateCommand ExtractCADZValuesCommand =>
            _extractCADZValuesCommand ??= new DelegateCommand(ExecuteExtractCADZValues, () => _selectedSegment != null && _selectedSegment.Points != null && _selectedSegment.Points.Count > 0);

        private void ExecuteExtractCADZValues()
        {
            if (_selectedSegment == null || _selectedSegment.Points == null || _selectedSegment.Points.Count == 0)
                return;

            try
            {
                var zValues = _selectedSegment.Points
                    .Where(p => p != null)
                    .Select(p => p.Z)
                    .Where(z => Math.Abs(z) > 0.0001)
                    .ToList();

                if (zValues.Count > 0)
                {
                    double avgZ = zValues.Average();
                    _selectedSegment.TeachHeight = Math.Round(avgZ, 3);
                    RaisePropertyChanged(nameof(_selectedSegment.TeachHeight));
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 应用采样点数——按 SegmentSplitCount 对选中段的原始图元重新离散化
        /// 只更新采样点 Points，不替换 SourceEntity，保持原始轨迹形状不变
        /// 例如：弧线默认89个点，设置20后重新采样为20个点，但弧线形状不变
        /// </summary>
        private void ExecuteApplySegmentSplit()
        {
            if (_selectedSegment == null || _segmentSplitCount < 2)
                return;

            try
            {
                List<CadPoint> newPoints = null;

                // 优先使用 OriginalSourceEntity（原始图元，如 CadArc/CadCircle）进行重新离散化
                if (_selectedSegment.OriginalSourceEntity != null && _dxfParser != null
                    && _selectedSegment.OriginalSourceEntity is not CadLwPolyline)
                {
                    newPoints = _dxfParser.DiscretizeByCount(_selectedSegment.OriginalSourceEntity, _segmentSplitCount);
                }

                // 如果 OriginalSourceEntity 不可用，尝试用 SourceEntity
                if ((newPoints == null || newPoints.Count == 0) && _selectedSegment.SourceEntity != null && _dxfParser != null
                    && _selectedSegment.SourceEntity is not CadLwPolyline)
                {
                    newPoints = _dxfParser.DiscretizeByCount(_selectedSegment.SourceEntity, _segmentSplitCount);
                }

                // 如果无法通过原始图元离散化，则基于现有点重采样
                if ((newPoints == null || newPoints.Count == 0) && 
                    _selectedSegment.Points != null && _selectedSegment.Points.Count >= 2)
                {
                    newPoints = ResamplePoints(_selectedSegment.Points, _segmentSplitCount);
                }

                if (newPoints == null || newPoints.Count == 0)
                {
                    GlobalStatus = L("CadPoint_Status_ResampleFailed");
                    return;
                }

                // 只更新采样点，不替换 SourceEntity，保持原始轨迹形状
                _selectedSegment.Points = newPoints;
                _selectedSegment.SamplePointCount = _segmentSplitCount;
                SelectedSegmentPoints = newPoints;
                RaisePropertyChanged(nameof(SegmentSummaryDisplay));

                GlobalStatus = string.Format(L("CadPoint_Status_ResampleSuccess"), _selectedSegment.SegmentId, _segmentSplitCount);
            }
            catch (Exception ex)
            {
                GlobalStatus = string.Format(L("CadPoint_Status_ResampleError"), ex.Message);
            }
        }

        /// <summary>
        /// 基于现有点集进行重采样，生成指定数量的均匀分布点
        /// 所有点（含首尾）统一通过插值计算，不硬编码起点/终点
        /// </summary>
        private List<CadPoint> ResamplePoints(List<CadPoint> originalPoints, int targetCount)
        {
            if (originalPoints == null || originalPoints.Count < 2 || targetCount < 2)
                return originalPoints;

            var result = new List<CadPoint>();
            
            // 计算总长度及各段累积长度
            double totalLength = 0;
            var cumulativeLengths = new List<double> { 0 };
            for (int i = 1; i < originalPoints.Count; i++)
            {
                double dx = originalPoints[i].X - originalPoints[i - 1].X;
                double dy = originalPoints[i].Y - originalPoints[i - 1].Y;
                double dz = originalPoints[i].Z - originalPoints[i - 1].Z;
                totalLength += Math.Sqrt(dx * dx + dy * dy + dz * dz);
                cumulativeLengths.Add(totalLength);
            }

            if (totalLength < 1e-9)
                return originalPoints;

            // 均匀采样：所有点统一按等间距插值
            for (int i = 0; i < targetCount; i++)
            {
                double t = (targetCount > 1) ? (double)i / (targetCount - 1) : 0;
                double targetLength = t * totalLength;

                // 找到目标长度所在的线段区间
                int segIdx = 0;
                for (int j = 1; j < cumulativeLengths.Count; j++)
                {
                    if (cumulativeLengths[j] >= targetLength)
                    {
                        segIdx = j - 1;
                        break;
                    }
                    if (j == cumulativeLengths.Count - 1)
                        segIdx = j - 1;
                }

                // 在当前线段上插值
                double segStart = cumulativeLengths[segIdx];
                double segEnd = cumulativeLengths[segIdx + 1];
                double segLen = segEnd - segStart;
                double progress = segLen > 1e-9 ? (targetLength - segStart) / segLen : 0;

                var p1 = originalPoints[segIdx];
                var p2 = originalPoints[segIdx + 1];

                result.Add(new CadPoint(
                    p1.X + (p2.X - p1.X) * progress,
                    p1.Y + (p2.Y - p1.Y) * progress,
                    p1.Z + (p2.Z - p1.Z) * progress)
                {
                    Id = (i + 1).ToString()
                });
            }

            return result;
        }

        #endregion

        #region 委托命令 — Step4: 坐标对齐（N点仿射 + 逐点映射）

        private DelegateCommand _addAffinePointCommand;
        /// <summary>添加仿射标定点命令</summary>
        public DelegateCommand AddAffinePointCommand =>
            _addAffinePointCommand ??= new DelegateCommand(ExecuteAddAffinePoint);

        private DelegateCommand<AffineCalibrationPoint> _deleteAffinePointCommand;
        /// <summary>删除仿射标定点命令（最少保留3点）</summary>
        public DelegateCommand<AffineCalibrationPoint> DeleteAffinePointCommand =>
            _deleteAffinePointCommand ??= new DelegateCommand<AffineCalibrationPoint>(
                ExecuteDeleteAffinePoint, p => _affineCalibrationPoints.Count > 3);

        private DelegateCommand<AffineCalibrationPoint> _pickAffineCadCoordCommand;
        /// <summary>从画布选取仿射CAD坐标命令</summary>
        public DelegateCommand<AffineCalibrationPoint> PickAffineCadCoordCommand =>
            _pickAffineCadCoordCommand ??= new DelegateCommand<AffineCalibrationPoint>(ExecutePickAffineCadCoord);

        private DelegateCommand<AffineCalibrationPoint> _teachAffineMachineCoordCommand;
        /// <summary>示教仿射机械坐标命令</summary>
        public DelegateCommand<AffineCalibrationPoint> TeachAffineMachineCoordCommand =>
            _teachAffineMachineCoordCommand ??= new DelegateCommand<AffineCalibrationPoint>(ExecuteTeachAffineMachineCoord);

        private DelegateCommand _computeAffineTransformCommand;
        /// <summary>计算N点仿射变换命令</summary>
        public DelegateCommand ComputeAffineTransformCommand =>
            _computeAffineTransformCommand ??= new DelegateCommand(ExecuteComputeAffineTransform,
                () => _affineCalibrationPoints.Count(p => p.MachineX != 0 || p.MachineY != 0) >= 3);

        private DelegateCommand _applyTransformToSegmentsCommand;
        /// <summary>应用变换到轨迹段命令</summary>
        public DelegateCommand ApplyTransformToSegmentsCommand =>
            _applyTransformToSegmentsCommand ??= new DelegateCommand(ExecuteApplyTransformToSegments,
                () => _affineResult != null);

        private DelegateCommand _addMappingPointCommand;
        /// <summary>添加逐点映射点命令</summary>
        public DelegateCommand AddMappingPointCommand =>
            _addMappingPointCommand ??= new DelegateCommand(ExecuteAddMappingPoint);

        private DelegateCommand<PointMappingPoint> _deleteMappingPointCommand;
        /// <summary>删除逐点映射点命令</summary>
        public DelegateCommand<PointMappingPoint> DeleteMappingPointCommand =>
            _deleteMappingPointCommand ??= new DelegateCommand<PointMappingPoint>(ExecuteDeleteMappingPoint);

        private DelegateCommand<PointMappingPoint> _pickMappingCadCoordCommand;
        /// <summary>从画布选取映射CAD坐标命令</summary>
        public DelegateCommand<PointMappingPoint> PickMappingCadCoordCommand =>
            _pickMappingCadCoordCommand ??= new DelegateCommand<PointMappingPoint>(ExecutePickMappingCadCoord);

        private DelegateCommand<PointMappingPoint> _teachMappingMachineCoordCommand;
        /// <summary>示教映射机械坐标命令</summary>
        public DelegateCommand<PointMappingPoint> TeachMappingMachineCoordCommand =>
            _teachMappingMachineCoordCommand ??= new DelegateCommand<PointMappingPoint>(ExecuteTeachMappingMachineCoord);

        private DelegateCommand _showSvgCommand;
        /// <summary>查看坐标对齐原理示意图</summary>
        public DelegateCommand ShowSvgCommand =>
            _showSvgCommand ??= new DelegateCommand(ExecuteShowSvg);

        #endregion

        #region 委托命令 — Step5: 预览仿真

        private DelegateCommand _dryRunCommand;
        /// <summary>Dry Run 空走仿真命令——模拟走胶路径但不实际出胶</summary>
        public DelegateCommand DryRunCommand =>
            _dryRunCommand ??= new DelegateCommand(ExecuteDryRun, () => CanExecute);

        private DelegateCommand _executeRunCommand;
        /// <summary>统一执行命令——根据当前模式选择仿真/真实空跑/真实点胶</summary>
        public DelegateCommand ExecuteRunCommand =>
            _executeRunCommand ??= new DelegateCommand(ExecuteRun, () => CanExecute);

        private DelegateCommand _pauseSimCommand;
        /// <summary>暂停仿真命令</summary>
        public DelegateCommand PauseSimCommand =>
            _pauseSimCommand ??= new DelegateCommand(ExecutePauseSim, () => _isSimulating);

        private DelegateCommand _stopSimCommand;
        /// <summary>停止命令——安全优先，始终使能，任何时候可点击</summary>
        public DelegateCommand StopSimCommand =>
            _stopSimCommand ??= new DelegateCommand(ExecuteStopSim);

        #endregion

        #region 委托命令 — Step6: 执行走胶

        private DelegateCommand _executePathCommand;
        /// <summary>执行完整路径走胶命令</summary>
        public DelegateCommand ExecutePathCommand =>
            _executePathCommand ??= new DelegateCommand(ExecutePath, () => CanExecute);

        #endregion

        #region 委托命令 — 画布视图

        private DelegateCommand _fitToAllCommand;
        /// <summary>适应窗口命令——自动缩放使所有图形居中完整显示</summary>
        public DelegateCommand FitToAllCommand =>
            _fitToAllCommand ??= new DelegateCommand(ExecuteFitToAll);

        private DelegateCommand _resetViewCommand;
        /// <summary>重置视图命令——恢复默认缩放和平移</summary>
        public DelegateCommand ResetViewCommand =>
            _resetViewCommand ??= new DelegateCommand(ExecuteResetView);

        #endregion

        #region 委托命令 — ROI 工具确认/取消

        private DelegateCommand _confirmRoiCommand;
        /// <summary>确认 ROI 区域命令——将预览 ROI 转换为 DispenseSegment 加入列表</summary>
        public DelegateCommand ConfirmRoiCommand =>
            _confirmRoiCommand ??= new DelegateCommand(ExecuteConfirmRoi, () => CurrentRoiPreview != null);

        private DelegateCommand _cancelRoiCommand;
        /// <summary>取消 ROI 绘制命令</summary>
        public DelegateCommand CancelRoiCommand =>
            _cancelRoiCommand ??= new DelegateCommand(ExecuteCancelRoi);

        #endregion

        #region 构造函数

        /// <summary>
        /// 主构造函数——接受 DI 注入的服务并初始化步骤信息和命令
        /// 所有服务参数均可为 null（缺失服务时对应功能按钮自动禁用）
        /// </summary>
        /// <param name="dxfParser">DXF 解析服务（可为 null）</param>
        /// <param name="roiTool">ROI 工具服务（可为 null）</param>
        /// <param name="alignService">坐标对齐服务（可为 null）</param>
        public CadPointEditorViewModel(
            IDxfParserService dxfParser = null,
            IDxfImportHelper dxfImportHelper = null,
            IRoiToolService roiToolService = null,
            ICoordinateAlignService alignService = null,
            IDispenseExecuteService dispenseExecuteService = null,
            IMotionService motionService = null,
            ILocalizationService localizationService = null,
            IDispenseSegmentStore dispenseSegmentStore = null,
            IEventAggregator eventAggregator = null)
        {
            _dxfParser = dxfParser;
            _dxfImportHelper = dxfImportHelper;
            _roiToolService = roiToolService;
            _alignService = alignService;
            _dispenseExecuteService = dispenseExecuteService;
            _motionService = motionService;
            _localizationService = localizationService;
            _dispenseSegmentStore = dispenseSegmentStore;
            _eventAggregator = eventAggregator;

            // 订阅语言变更事件以刷新所有本地化文本
            if (_localizationService != null)
            {
                _localizationService.LanguageChanged += OnLanguageChanged;
            }

            _globalStatus = L("CadPoint_Status_Ready");
            _simStatusText = L("Step5_Status_Waiting");

            // 初始化 6 个步骤信息
            InitializeSteps();

            // 初始化集合属性
            _canvasEntities = new ObservableCollection<CadEntity>();
            _segments = new ObservableCollection<DispenseSegment>();
            _layerCheckList = new ObservableCollection<LayerCheckItem>();

            // 初始化仿射标定点集合（默认3个空行，用户在实际流程中示教）
            _affineCalibrationPoints = new ObservableCollection<AffineCalibrationPoint>
            {
                new() { Index = 0, Name = "P1" },
                new() { Index = 1, Name = "P2" },
                new() { Index = 2, Name = "P3" },
            };

            // 初始化逐点映射点集合
            _pointMappingPoints = new ObservableCollection<PointMappingPoint>();

            // 监听 Segments 集合变化以更新状态栏摘要和 CanExecute
            _segments.CollectionChanged += OnSegmentsCollectionChanged;

            // 将 Segments 注册到共享存储，供 DispenseDetailViewModel 导入使用
            _dispenseSegmentStore?.RegisterSegments(_segments);

            // 从配方参数恢复上次配置路径，并尝试自动加载
            RestorePathFromStationParams();
            TryAutoLoadLastConfig();
        }

        #endregion

        /// <summary>
        /// 将 DispenseDetail 默认参数应用到新创建的段（仅初始化时使用，不覆盖已有段）
        /// </summary>
        private void ApplyDefaultParamsToSegment(DispenseSegment segment)
        {
            if (segment == null) return;
            var detail = _dispenseSegmentStore?.CurrentDispenseDetail;
            if (detail == null) return;

            segment.JumpSpeed = detail.DefaultJumpSpeed;
            segment.MoveSpeed = detail.DefaultMoveSpeed;
            segment.SafeHeight = detail.DefaultSafeHeight;
            segment.ApproachHeight = detail.DefaultApproachHeight;
            segment.CornerDecel = detail.DefaultCornerDecel;
            segment.DispenseAmount = detail.DefaultDispenseAmount;
            segment.PreDelay = detail.DefaultPreDelay;
            segment.PostDelay = detail.DefaultPostDelay;
            segment.DispensingPressure = detail.DefaultDispensingPressure;
            segment.SuckBackTime = detail.DefaultSuckBackTime;
            segment.GlueTriggerOffsetMm = detail.DefaultGlueTriggerOffsetMm;
            segment.TeachHeight = detail.DefaultTeachHeight;
            segment.HeightCompensation = detail.DefaultHeightCompensation;
        }

        #region 本地化便捷方法

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

        /// <summary>
        /// 延迟注入 ILocalizationService——用于控件 Loaded 事件中 DI 容器就绪后的补救注入
        /// </summary>
        public void RefreshLocalization(ILocalizationService svc)
        {
            if (svc == null) return;
            if (_localizationService != null)
            {
                _localizationService.LanguageChanged -= OnLanguageChanged;
            }
            _localizationService = svc;
            _localizationService.LanguageChanged += OnLanguageChanged;
            var currentStep = _currentStepValue;
            InitializeSteps();
            if (currentStep >= 1 && currentStep <= Steps.Count)
                UpdateStepStates(currentStep);
        }

        /// <summary>
        /// 语言变更事件处理器：刷新所有本地化的显示属性
        /// </summary>
        private void OnLanguageChanged(object sender, LanguageChangedEventArgs e)
        {
            var currentStep = _currentStepValue;
            InitializeSteps();
            if (currentStep >= 1 && currentStep <= Steps.Count)
                UpdateStepStates(currentStep);

            RaisePropertyChanged(nameof(CurrentStepLabelDisplay));
            RaisePropertyChanged(nameof(CurrentStepTitle));
            RaisePropertyChanged(nameof(SegmentCountDisplay));
            RaisePropertyChanged(nameof(TotalLengthDisplay));
            RaisePropertyChanged(nameof(AlignStatusDisplay));

            _globalStatus = L("CadPoint_Status_Ready");
            _simStatusText = L("Step5_Status_Waiting");
            RaisePropertyChanged(nameof(GlobalStatus));
            RaisePropertyChanged(nameof(SimStatusText));
        }

        #endregion

        #region 初始化方法

        /// <summary>
        /// 初始化 6 个步骤信息——定义每步标题、图标和提示文字
        /// </summary>
        private void InitializeSteps()
        {
            Steps.Clear();
            Steps.Add(new StepInfo { Number = 1, Title = L("CadPoint_Step1_Title"), Icon = "FileImport", Hint = L("CadPoint_Step1_Hint") });
            Steps.Add(new StepInfo { Number = 2, Title = L("CadPoint_Step2_Title"), Icon = "CheckCircleOutline", Hint = L("CadPoint_Step2_Hint") });
            Steps.Add(new StepInfo { Number = 3, Title = L("CadPoint_Step3_Title"), Icon = "TuneVertical", Hint = L("CadPoint_Step3_Hint") });
            Steps.Add(new StepInfo { Number = 4, Title = L("CadPoint_Step4_Title"), Icon = "AxisArrow", Hint = L("CadPoint_Step4_Hint") });
            Steps.Add(new StepInfo { Number = 5, Title = L("CadPoint_Step5_Title"), Icon = "PlayCircleOutline", Hint = L("CadPoint_Step5_Hint") });
            Steps.Add(new StepInfo { Number = 6, Title = L("CadPoint_Step6_Title"), Icon = "Play", Hint = L("CadPoint_Step6_Hint") });

            // 标记第 1 步为当前步骤
            UpdateStepStates(1);
        }

        #endregion

        #region 步骤流转方法

        /// <summary>
        /// 跳转到指定步骤——更新所有步骤的 IsCurrent/IsCompleted 状态
        /// 同时标记已完成的前置步骤
        /// </summary>
        /// <param name="step">目标步骤号（1~6）</param>
        public void GoToStep(int? step)
        {
            if (step == null || step < 1 || step > 6) return;
            int s = step.Value;
            SelectedSegment = null;
            CurrentStep = s;
            GoNextCommand.RaiseCanExecuteChanged();
            GoPrevCommand.RaiseCanExecuteChanged();
            GoToStepCommand.RaiseCanExecuteChanged();
            GlobalStatus = string.Format(L("CadPoint_Status_CurrentStepFormat"), Steps[s - 1].Title);
            FitToAllRequested?.Invoke();
        }

        private bool CanGoToStep(int? step) => step.HasValue && step >= 1 && step <= 6;

        /// <summary>
        /// 更新所有步骤的 IsCurrent 和 IsCompleted 标记
        /// 小于 currentStep 的步骤标记为已完成，等于的标记为当前
        /// </summary>
        private void UpdateStepStates(int currentStep)
        {
            for (int i = 0; i < Steps.Count; i++)
            {
                var step = Steps[i];
                step.IsCurrent = (i + 1 == currentStep);
                step.IsCompleted = (i + 1 < currentStep);
            }
        }

        /// <summary>
        /// 由控件 code-behind 在 DP 变更时调用——同步内部状态
        /// </summary>
        public void OnStepChanged(int newStep)
        {
            if (newStep >= 1 && newStep <= 6 && newStep != _currentStepValue)
            {
                _currentStepValue = newStep;
                UpdateStepStates(newStep);
                RaisePropertyChanged(nameof(CurrentStep));
                RaisePropertyChanged(nameof(CurrentStepTitle));
            }
        }

        #endregion

        #region Step1: 文件导入命令实现

        /// <summary>
        /// 打开文件对话框让用户选择 .dxf 文件
        /// 选择后更新 FilePath 属性，但不立即执行导入
        /// </summary>
        private void ExecuteSelectFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "DXF files (*.dxf)|*.dxf|All files (*.*)|*.*",
                Title = L("CadPoint_Dialog_SelectDxfFile"),
                InitialDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "LibreCAD")
            };
            if (dialog.ShowDialog() == true)
            {
                FilePath = dialog.FileName;
                ImportStatusMessage = string.Format(L("CadPoint_Status_FileSelected"), System.IO.Path.GetFileName(dialog.FileName));
                ImportDxfCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 浏览选择轨迹段配置文件（JSON），选择后更新 SegmentFilePath 属性
        /// </summary>
        private void ExecuteSelectSegmentFile()
        {
            var initialDir = System.IO.Path.GetDirectoryName(_segmentFilePath);
            if (string.IsNullOrEmpty(initialDir) || !System.IO.Directory.Exists(initialDir))
                initialDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Segments");
            if (!System.IO.Directory.Exists(initialDir))
                initialDir = AppDomain.CurrentDomain.BaseDirectory;

            var dialog = new OpenFileDialog
            {
                Filter = L("CadPoint_Filter_JsonAll"),
                DefaultExt = ".json",
                Title = L("CadPoint_Dialog_LoadTrajectory"),
                InitialDirectory = initialDir
            };
            if (dialog.ShowDialog() == true)
            {
                SegmentFilePath = dialog.FileName;
                ExecuteLoadSegments();
            }
        }

        /// <summary>
        /// 执行 DXF 文件导入——使用 IDxfImportHelper 统一导入方法，
        /// 保证与 CadAlignmentViewModel 使用完全相同的解析和过滤逻辑
        /// </summary>
        private async void ExecuteImportDxf()
        {
            if (!HasFilePath)
            {
                ImportStatusMessage = L("CadPoint_Status_NoFile");
                return;
            }

            try
            {
                GlobalStatus = L("CadPoint_Status_Parsing");
                ImportStatusMessage = L("CadPoint_Status_ParsingWait");

                var filePath = FilePath;

                var (importResult, newSegments, canvasEntities) = await Task.Run(() =>
                {
                    var result = _dxfImportHelper.Import(filePath, DxfImportOptions.ForDispenseEditor);
                    _parsedDxfResult = result.ParseResult;

                    if (!result.IsSuccess)
                        return (result, null, null);

                    var segments = new ObservableCollection<DispenseSegment>();
                    var entities = result.DisplayEntities;
                    int idx = 0;

                    foreach (var entity in entities)
                    {
                        var seg = CreateSegmentFromEntity(entity, idx++, entity.LayerName);
                        if (seg != null)
                            segments.Add(seg);
                    }

                    foreach (var entity in entities)
                    {
#if HAS_HALCON
                        try { entity.ToHObject(); } catch { }
#endif
                    }

                    return (result, segments, entities);
                });

                if (!importResult.IsSuccess)
                {
                    ImportStatusMessage = string.Format(L("CadPoint_Status_ParseFailedNoEntity"), string.Join("; ", _parsedDxfResult?.ParseWarnings ?? new List<string>()));
                    GlobalStatus = L("CadPoint_Status_ParseFailed");
                    return;
                }

                _layerCheckList.Clear();
                Segments.Clear();

                foreach (var entity in canvasEntities)
                    _layerCheckList.Add(new LayerCheckItem { LayerName = entity.LayerName, IsVisible = true });

                foreach (var seg in newSegments)
                    Segments.Add(seg);

                CanvasEntities = canvasEntities;

                _layerNames = importResult.LayerNames;
                SelectedLayer = _layerNames.FirstOrDefault();
                RaisePropertyChanged(nameof(LayerNames));

                GlobalStatus = string.Format(L("CadPoint_Status_ImportSuccess"), Segments.Count);
                RaisePropertyChanged(nameof(SegmentSummaryDisplay));

                ImportStatusMessage = string.Format(L("CadPoint_Status_ImportDetail"), _parsedDxfResult.TotalEntityCount, Segments.Count);
                if (_parsedDxfResult.ParseWarnings.Count > 0)
                    ImportStatusMessage += string.Format(L("CadPoint_Status_ImportWarning"), string.Join("; ", _parsedDxfResult.ParseWarnings));

                FitCanvasToExtents();
                GoToStep(2);
            }
            catch (Exception ex)
            {
                ImportStatusMessage = string.Format(L("CadPoint_Status_ImportException"), ex.Message);
                GlobalStatus = L("CadPoint_Status_ImportError");
            }
        }

        /// <summary>
        /// 回退的旧版 DXF 导入方法（当 IDxfImportHelper 不可用时使用）
        /// 保持向后兼容性
        /// </summary>
        private void ExecuteImportDxfLegacy()
        {
            if (_dxfParser == null)
            {
                ImportStatusMessage = L("CadPoint_Status_DxfServiceUnavailable");
                return;
            }

            try
            {
                _parsedDxfResult = _dxfParser.Parse(FilePath);

                if (!_parsedDxfResult.IsSuccess)
                {
                    ImportStatusMessage = string.Format(L("CadPoint_Status_ParseFailedNoEntity"), string.Join("; ", _parsedDxfResult.ParseWarnings));
                    return;
                }

                Segments.Clear();
                _layerCheckList.Clear();

                var newCanvasEntities = new ObservableCollection<CadEntity>();
                var newSegments = new ObservableCollection<DispenseSegment>();

                int segmentIndex = 0;
                foreach (var layerPair in _parsedDxfResult.Layers)
                {
                    string layerName = layerPair.Key;
                    var entities = layerPair.Value;

                    _layerCheckList.Add(new LayerCheckItem { LayerName = layerName, IsVisible = true });

                    foreach (var entity in entities)
                    {
                        entity.LayerName = layerName;
                        newCanvasEntities.Add(entity);

                        var segment = CreateSegmentFromEntity(entity, segmentIndex++, layerName);
                        if (segment != null)
                            newSegments.Add(segment);
                    }
                }

                CanvasEntities = newCanvasEntities;
                foreach (var seg in newSegments)
                    Segments.Add(seg);

                _layerNames = _parsedDxfResult.LayerNames.ToList();
                SelectedLayer = _layerNames.FirstOrDefault();
                RaisePropertyChanged(nameof(LayerNames));
                RaisePropertyChanged(nameof(SegmentSummaryDisplay));

                FitCanvasToExtents();
                GoToStep(2);
            }
            catch (Exception ex)
            {
                ImportStatusMessage = string.Format(L("CadPoint_Status_ImportException"), ex.Message);
            }
        }

        /// <summary>
        /// 从单个 CadEntity 创建对应的 DispenseSegment
        /// 使用 DxfParserService.Discretize() 进行离散化采样
        /// </summary>
        private DispenseSegment CreateSegmentFromEntity(CadEntity entity, int index, string layerName)
        {
            try
            {
                string prefix = entity.EntityType switch
                {
                    CadEntityType.Line => "LINE",
                    CadEntityType.Arc => "ARC",
                    CadEntityType.Polyline or CadEntityType.LwPolyline => "POLY",
                    CadEntityType.Circle => "CIRC",
                    CadEntityType.Ellipse => "ELLIP",
                    _ => "SEG"
                };

                var segment = new DispenseSegment($"{prefix}_{index:D03}", entity.EntityType, layerName)
                {
                    SourceEntity = entity,
                    OriginalSourceEntity = entity,
                    OriginalEntityData = OriginalEntityData.FromEntity(entity)
                };

                // 如果有离散化服务则进行采样
                if (_dxfParser != null)
                {
                    try
                    {
                        segment.Points = _dxfParser.Discretize(entity, DefaultDiscretizePitchMM);
                    }
                    catch
                    {
                        segment.Points = new List<CadPoint>();
                    }
                }

                ApplyDefaultParamsToSegment(segment);

                return segment;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Step2: 图层筛选

        /// <summary>
        /// 根据图层可见性列表过滤 CanvasEntities 的显示
        /// 不可见图元的 IsVisible 设为 false，触发画布重绘
        /// </summary>
        private void ApplyLayerFilter()
        {
            if (_layerCheckList.Count == 0) return;

            var visibleLayers = _layerCheckList.Where(l => l.IsVisible).Select(l => l.LayerName).ToHashSet();
            foreach (var entity in CanvasEntities)
            {
                entity.IsVisible = visibleLayers.Contains(entity.LayerName);
            }
        }

        #endregion

        #region Step3: 参数编辑与批量操作

        /// <summary>全选所有轨迹段（设置 IsEnabled = true）</summary>
        private void ExecuteSelectAllSegments()
        {
            foreach (var seg in Segments)
            {
                seg.IsEnabled = true;
            }
            RaisePropertyChanged(nameof(SegmentCountDisplay));
            RaisePropertyChanged(nameof(TotalLengthDisplay));
            DeleteSelectedSegmentsCommand.RaiseCanExecuteChanged();
        }

        /// <summary>反选轨迹段的启用状态</summary>
        private void ExecuteInvertSelection()
        {
            foreach (var seg in Segments)
            {
                seg.IsEnabled = !seg.IsEnabled;
            }
            RaisePropertyChanged(nameof(SegmentCountDisplay));
            RaisePropertyChanged(nameof(TotalLengthDisplay));
            DeleteSelectedSegmentsCommand.RaiseCanExecuteChanged();
        }

        /// <summary>批量设置速度——对 IsEnabled 为 true 的段设置目标速度</summary>
        private void ExecuteBatchSetSpeed()
        {
            var targets = Segments.Where(s => s.IsEnabled).ToList();
            if (targets.Count == 0) { GlobalStatus = L("CadPoint_Error_NoTrajectorySelected"); return; }

            var firstSeg = targets[0];
            var dialog = new Views.BatchSetSpeedDialog();
            var window = new Window
            {
                Title = L("Step3_BatchSet_SpeedTitle"),
                Content = dialog,
                Width = 360,
                Height = double.NaN,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow,
                WindowStyle = WindowStyle.ToolWindow
            };

            dialog.DataContext = new BatchSetSpeedViewModel(firstSeg.JumpSpeed, firstSeg.MoveSpeed, window);

            if (window.ShowDialog() == true)
            {
                var vm = dialog.DataContext as BatchSetSpeedViewModel;
                if (vm != null)
                {
                    foreach (var seg in targets)
                    {
                        seg.JumpSpeed = vm.JumpSpeed;
                        seg.MoveSpeed = vm.MoveSpeed;
                    }
                    GlobalStatus = string.Format(L("CadPoint_Status_BatchSetSpeed"), vm.JumpSpeed, vm.MoveSpeed, targets.Count);
                }
            }
        }

        /// <summary>批量设置胶量——对 IsEnabled 为 true 的段设置目标胶量</summary>
        private void ExecuteBatchSetGlue()
        {
            var targets = Segments.Where(s => s.IsEnabled).ToList();
            if (targets.Count == 0) { GlobalStatus = L("CadPoint_Error_NoTrajectorySelected"); return; }
            string input = ShowInputDialog(L("CadPoint_Dialog_InputGlue"), "1.0");
            if (double.TryParse(input, out double glue) && glue >= 0)
            {
                foreach (var seg in targets)
                    seg.DispenseAmount = glue;
                GlobalStatus = string.Format(L("CadPoint_Status_BatchSetGlue"), glue, targets.Count);
            }
        }

        /// <summary>批量设置全部参数——打开多参数对话框，支持选择性地批量设置多个参数</summary>
        private void ExecuteBatchSetAll()
        {
            var targets = Segments.Where(s => s.IsEnabled).ToList();
            if (targets.Count == 0) { GlobalStatus = L("CadPoint_Error_NoTrajectorySelected"); return; }

            var dialog = new Views.BatchSetParamsDialog();

            var window = new Window
            {
                Title = L("CadPoint_Dialog_BatchSetTitle"),
                Content = dialog,
                Width = 470,
                Height = 520,
                ResizeMode = System.Windows.ResizeMode.CanResizeWithGrip,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow,
                WindowStyle = WindowStyle.ToolWindow
            };

            dialog.DataContext = new BatchSetParamsViewModel(targets[0], window);

            if (window.ShowDialog() == true)
            {
                var vm = dialog.DataContext as BatchSetParamsViewModel;
                if (vm?.BatchParamItems != null)
                {
                    int changedCount = 0;
                    foreach (var param in vm.BatchParamItems.Where(p => p.IsEnabled))
                    {
                        foreach (var seg in targets)
                            param.ApplyTo(seg);
                        changedCount++;
                    }
                    GlobalStatus = string.Format(L("CadPoint_Status_BatchSetResult"), changedCount, targets.Count);
                }
            }
        }

        /// <summary>删除所有 IsEnabled 为 true 的轨迹段</summary>
        private void ExecuteDeleteSelectedSegments()
        {
            var toDelete = Segments.Where(s => s.IsEnabled).ToList();
            if (toDelete.Count == 0) return;

            foreach (var seg in toDelete)
            {
                if (seg.SourceEntity != null)
                    CanvasEntities.Remove(seg.SourceEntity);
                Segments.Remove(seg);
            }

            SelectedSegment = null;
            DeleteSelectedSegmentsCommand.RaiseCanExecuteChanged();
            RefreshStatusBarSummary();
        }

        #endregion

        #region Step3: ROI 工具

        /// <summary>
        /// 取消其他 ROI 工具的激活状态，确保同时只有一个 ROI 类型处于激活态
        /// </summary>
        /// <param name="keepType">要保留激活的 ROI 类型标识 ("line"/"polyline"/"arc")</param>
        private void DeactivateOtherRois(string keepType)
        {
            if (keepType != "line") IsLineRoiActive = false;
            if (keepType != "polyline") IsPolylineRoiActive = false;
            if (keepType != "arc") IsArcRoiActive = false;
        }

        /// <summary>
        /// 确认 ROI 区域——将当前预览的 ROI 转换为新的 DispenseSegment 并加入列表
        /// </summary>
        private void ExecuteConfirmRoi()
        {
            if (CurrentRoiPreview == null) return;

            string prefix = CurrentRoiPreview.Type switch
            {
                RoiType.Line => "ROI_LINE",
                RoiType.Polyline => "ROI_POLY",
                RoiType.Arc => "ROI_ARC",
                _ => "ROI"
            };

            var segment = new DispenseSegment($"{prefix}_{Segments.Count:D03}",
                CurrentRoiPreview.Type == RoiType.Arc ? CadEntityType.Arc :
                CurrentRoiPreview.Type == RoiType.Line ? CadEntityType.Line : CadEntityType.Polyline,
                "ROI_MANUAL")
            {
                Points = _roiToolService?.SamplePoints(CurrentRoiPreview, DefaultDiscretizePitchMM)
                         ?? CurrentRoiPreview.SamplePoints()
            };

            ApplyDefaultParamsToSegment(segment);

            Segments.Add(segment);
            CurrentRoiPreview = null;

            // 取消所有 ROI 工具激活状态
            IsLineRoiActive = false;
            IsPolylineRoiActive = false;
            IsArcRoiActive = false;

            GlobalStatus = string.Format(L("CadPoint_Status_RoiAdded"), segment.SegmentId);
        }

        /// <summary>取消 ROI 绘制——清除预览并关闭所有 ROI 工具</summary>
        private void ExecuteCancelRoi()
        {
            CurrentRoiPreview = null;
            IsLineRoiActive = false;
            IsPolylineRoiActive = false;
            IsArcRoiActive = false;
        }

        #endregion

        #region Step4: 坐标对齐命令实现（N点仿射 + 逐点映射）

        /// <summary>添加仿射标定点（默认3个空行）</summary>
        private void ExecuteAddAffinePoint()
        {
            int idx = _affineCalibrationPoints.Count;
            _affineCalibrationPoints.Add(new AffineCalibrationPoint
            {
                Index = idx,
                Name = $"P{idx + 1}",
                CadX = 0, CadY = 0,
                MachineX = 0, MachineY = 0
            });
            ComputeAffineTransformCommand.RaiseCanExecuteChanged();
            DeleteAffinePointCommand.RaiseCanExecuteChanged();
        }

        /// <summary>删除仿射标定点（最少保留3点）</summary>
        private void ExecuteDeleteAffinePoint(AffineCalibrationPoint point)
        {
            if (point == null || _affineCalibrationPoints.Count <= 3) return;
            _affineCalibrationPoints.Remove(point);
            // 重新编号
            for (int i = 0; i < _affineCalibrationPoints.Count; i++)
            {
                _affineCalibrationPoints[i].Index = i;
                _affineCalibrationPoints[i].Name = $"P{i + 1}";
            }
            ComputeAffineTransformCommand.RaiseCanExecuteChanged();
            DeleteAffinePointCommand.RaiseCanExecuteChanged();
        }

        /// <summary>从画布选取仿射CAD坐标——标记拾取状态，等待画布点击</summary>
        private void ExecutePickAffineCadCoord(AffineCalibrationPoint point)
        {
            if (point == null) return;
            _isPickingAffineCadCoord = true;
            _isPickingMappingCadCoord = false;
            SelectedAffinePoint = point;
            GlobalStatus = L("Step4_Status_PickCadCoord");
        }

        /// <summary>示教仿射机械坐标——读取运动卡当前Dx/Dy位置</summary>
        private void ExecuteTeachAffineMachineCoord(AffineCalibrationPoint point)
        {
            if (point == null) return;

            if (_motionService == null)
            {
                // 无运动卡时使用模拟数据
                var rnd = new Random();
                point.MachineX = Math.Round(rnd.NextDouble() * 200 - 100, 3);
                point.MachineY = Math.Round(rnd.NextDouble() * 200 - 100, 3);
                point.MachineDz = Math.Round(rnd.NextDouble() * 50 - 25, 3);
            }
            else
            {
                try
                {
                    // 读取Dx(8)/Dy(6)轴当前位置
                    const int AxisDx = 8;
                    const int AxisDy = 6;
                    point.MachineX = Math.Round(_motionService.GetAxisState(AxisDx).ActualPosition, 3);
                    point.MachineY = Math.Round(_motionService.GetAxisState(AxisDy).ActualPosition, 3);

                    // 根据当前针头读取对应Dz轴
                    const int AxisDz1 = 4;
                    const int AxisDz2 = 5;
                    point.MachineDz = Math.Round(
                        _motionService.GetAxisState(_currentNeedleIndex == 0 ? AxisDz1 : AxisDz2).ActualPosition, 3);
                }
                catch (Exception ex)
                {
                    GlobalStatus = string.Format(L("Step4_Status_TeachMachineFailed"), ex.Message);
                    return;
                }
            }

            ComputeAffineTransformCommand.RaiseCanExecuteChanged();
            GlobalStatus = string.Format(L("Step4_Status_TeachAffineSuccess"), point.Name, point.MachineX, point.MachineY, point.MachineDz);
        }

        /// <summary>计算N点仿射变换——调用AffineCalibrationService.Solve()</summary>
        private void ExecuteComputeAffineTransform()
        {
            try
            {
                var validPoints = _affineCalibrationPoints
                    .Where(p => (p.MachineX != 0 || p.MachineY != 0) && (p.CadX != 0 || p.CadY != 0))
                    .ToList();

                if (validPoints.Count < 3)
                {
                    GlobalStatus = L("Step4_Error_NeedMorePoints");
                    return;
                }

                var cadPoints = validPoints.Select(p => (p.CadX, p.CadY)).ToList();
                var machinePoints = validPoints.Select(p => (p.MachineX, p.MachineY)).ToList();

                var result = AffineCalibrationService.Solve(cadPoints, machinePoints);

                // 回填残差到每个标定点
                for (int i = 0; i < validPoints.Count && i < result.Residuals.Count; i++)
                {
                    validPoints[i].Residual = result.Residuals[i];
                }

                AffineResult = result;
                TransformStatus = $"✅ {L("Step4_Status_AffineComputed")}: {validPoints.Count} {L("Step4_Status_Points")}, RMS={result.RmsError:F4}mm";
                GlobalStatus = TransformStatus;
            }
            catch (Exception ex)
            {
                GlobalStatus = $"{L("Step4_Error_AffineFailed")}: {ex.Message}";
                TransformStatus = $"❌ {L("Step4_Error_AffineFailed")}: {ex.Message}";
            }
        }

        /// <summary>应用仿射变换到所有轨迹段点</summary>
        private void ExecuteApplyTransformToSegments()
        {
            // 仅检查当前选中针头是否有变换结果
            var currentResult = _currentNeedleIndex == 0 ? _affineResultNeedle1 : _affineResultNeedle2;
            if (currentResult == null)
            {
                GlobalStatus = string.Format(L("Step4_Error_NeedleNotCalibrated"), _currentNeedleIndex + 1);
                return;
            }

            try
            {
                // 计算当前针头的Z基准高度（当前标定点的Z平均值）
                double avgDz = 0;
                var validDz = _affineCalibrationPoints.Where(p => p.MachineDz != 0).ToList();
                if (validDz.Count > 0) avgDz = validDz.Average(p => p.MachineDz);

                int count = 0;
                foreach (var seg in Segments)
                {
                    if (seg.Points == null) continue;
                    foreach (var pt in seg.Points)
                    {
                        var (mx, my) = AffineCalibrationService.Transform(currentResult, pt.X, pt.Y);
                        pt.MachineX = mx;
                        pt.MachineY = my;
                        pt.MachineZ = avgDz;
                        count++;
                    }
                }

                // 同步到对齐服务
                if (_alignService != null)
                {
                    _alignService.SetMode(AlignMode.Affine);
                    var allPoints = Segments.SelectMany(s => s.Points).ToList();
                    _alignService.RegisterPoints(allPoints);
                }

                TransformStatus = $"✅ {L("Step4_Status_TransformApplied")}: {count} {L("Step4_Status_Points")}";
                GlobalStatus = TransformStatus;
                RaisePropertyChanged(nameof(AlignStatusDisplay));
                UpdateTransformedPointsPreview(Segments.SelectMany(s => s.Points).ToList());

                // 刷新采样点位列表，使 Step3 DataGrid 显示更新后的 MachineX/MachineY
                if (_selectedSegment != null)
                    SelectedSegmentPoints = _selectedSegment.Points;
            }
            catch (Exception ex)
            {
                GlobalStatus = string.Format(L("Step4_Status_ApplyTransformFailed"), ex.Message);
            }
        }

        /// <summary>更新变换后坐标预览（取前5个已变换的点）</summary>
        private void UpdateTransformedPointsPreview(List<CadPoint> allPoints)
        {
            TransformedPointsPreview.Clear();
            var preview = allPoints.Where(p => p.MachineX.HasValue).Take(5);
            foreach (var p in preview)
                TransformedPointsPreview.Add(p);
        }

        /// <summary>添加逐点映射点</summary>
        private void ExecuteAddMappingPoint()
        {
            int idx = _pointMappingPoints.Count;
            _pointMappingPoints.Add(new PointMappingPoint
            {
                Index = idx,
                Name = $"P{idx + 1}"
            });
        }

        /// <summary>删除逐点映射点</summary>
        private void ExecuteDeleteMappingPoint(PointMappingPoint point)
        {
            if (point == null) return;
            _pointMappingPoints.Remove(point);
            for (int i = 0; i < _pointMappingPoints.Count; i++)
            {
                _pointMappingPoints[i].Index = i;
                _pointMappingPoints[i].Name = $"P{i + 1}";
            }
        }

        /// <summary>从画布选取映射CAD坐标——标记拾取状态，等待画布点击</summary>
        private void ExecutePickMappingCadCoord(PointMappingPoint point)
        {
            if (point == null) return;
            _isPickingMappingCadCoord = true;
            _isPickingAffineCadCoord = false;
            SelectedMappingPoint = point;
            GlobalStatus = L("Step4_Status_PickCadCoord");
        }

        /// <summary>示教映射机械坐标——读取运动卡当前Dx/Dy/Dz位置</summary>
        private void ExecuteTeachMappingMachineCoord(PointMappingPoint point)
        {
            if (point == null) return;

            if (_motionService == null)
            {
                // 无运动卡时使用模拟数据
                var rnd = new Random();
                point.MachineDx = Math.Round(rnd.NextDouble() * 200 - 100, 3);
                point.MachineDy = Math.Round(rnd.NextDouble() * 200 - 100, 3);
                point.MachineDz = Math.Round(rnd.NextDouble() * 50, 3);
            }
            else
            {
                try
                {
                    // 读取Dx(8)/Dy(6)轴当前位置
                    const int AxisDx = 8;
                    const int AxisDy = 6;
                    point.MachineDx = Math.Round(_motionService.GetAxisState(AxisDx).ActualPosition, 3);
                    point.MachineDy = Math.Round(_motionService.GetAxisState(AxisDy).ActualPosition, 3);

                    // 根据当前针头选择Z轴: Dz1→AxisDz₂(logicalId=4), Dz2→AxisDz₃(logicalId=5)
                    int axisDz = _currentNeedleIndex == 0 ? 4 : 5;
                    point.MachineDz = Math.Round(_motionService.GetAxisState(axisDz).ActualPosition, 3);
                }
                catch (Exception ex)
                {
                    GlobalStatus = string.Format(L("Step4_Status_TeachMachineFailed"), ex.Message);
                    return;
                }
            }

            GlobalStatus = string.Format(L("Step4_Status_TeachMappingSuccess"), point.Name, point.MachineDx, point.MachineDy, point.MachineDz);
        }

        /// <summary>弹出坐标对齐原理示意图窗口</summary>
        private void ExecuteShowSvg()
        {
            try
            {
                var dlg = new Editor.SvgPopupWindow();
                dlg.Owner = System.Windows.Application.Current.MainWindow;
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                GlobalStatus = string.Format(L("Step4_Status_OpenDiagramFailed"), ex.Message);
            }
        }

        #endregion

        #region Step5: 预览仿真命令实现

        /// <summary>
        /// Dry Run 空走仿真——遍历所有启用的轨迹段，逐段模拟执行
        /// 不实际出胶，仅更新进度条和状态文本供用户观察
        /// 支持暂停和停止操作
        /// </summary>
        private async void ExecuteDryRun()
        {
            var enabledSegments = Segments.Where(s => s.IsEnabled).ToList();
            if (enabledSegments.Count == 0)
            {
                GlobalStatus = L("CadPoint_Error_NoExecutableTrajectory");
                return;
            }

            _simCts = new System.Threading.CancellationTokenSource();
            IsSimulating = true;
            SimProgress = 0;

            try
            {
                if (LineDispenseMode == LineDispenseMode.SinglePoint)
                {
                    // 单点模式：逐点仿真，模拟每个点的完整工艺周期
                    await ExecuteDryRunSinglePoint(enabledSegments);
                }
                else
                {
                    // 连续插补模式：按段仿真
                    await ExecuteDryRunContinuous(enabledSegments);
                }

                SimStatusText = L("CadPoint_Status_DryRunFinish");
                SimProgress = 100;
                GlobalStatus = L("CadPoint_Status_DryRunSimulationComplete");
            }
            catch (OperationCanceledException)
            {
                SimStatusText = L("CadPoint_Status_SimStopped");
                GlobalStatus = L("CadPoint_Status_SimStopped");
            }
            finally
            {
                IsSimulating = false;
                _simCts?.Dispose();
                _simCts = null;
            }
        }

        /// <summary>连续插补模式仿真——按段级别迭代</summary>
        private async Task ExecuteDryRunContinuous(List<DispenseSegment> enabledSegments)
        {
            int total = enabledSegments.Count;
            for (int i = 0; i < total; i++)
            {
                _simCts!.Token.ThrowIfCancellationRequested();

                var seg = enabledSegments[i];
                SimStatusText = $"[{L("Step3_Radio_ContinuousInterpolation")}] {seg.SegmentId} ({i + 1}/{total})...";
                SimProgress = (double)(i + 1) / total * 100;

                SelectedEntity = seg.SourceEntity;

                // 模拟每段执行耗时（按长度比例，至少 200ms）
                int delayMs = Math.Max(200, (int)(seg.Length * 50));
                await Task.Delay(delayMs, _simCts.Token);
            }
        }

        /// <summary>单点模式仿真——逐点迭代，模拟每个点的完整工艺周期</summary>
        private async Task ExecuteDryRunSinglePoint(List<DispenseSegment> enabledSegments)
        {
            // 计算总点数
            int totalPoints = enabledSegments.Sum(s => s.Points?.Count ?? 0);
            if (totalPoints == 0) return;

            DispenseSegment? lastSeg = null;
            int pointIndex = 0;
            foreach (var seg in enabledSegments)
            {
                if (seg.Points == null || seg.Points.Count == 0) continue;

                // 切换段时更新选中段（触发 SelectedSegmentPoints 和 SelectedEntity 刷新）
                if (lastSeg != seg)
                {
                    SelectedSegment = seg;
                    lastSeg = seg;
                }

                for (int ptIdx = 0; ptIdx < seg.Points.Count; ptIdx++)
                {
                    _simCts!.Token.ThrowIfCancellationRequested();
                    pointIndex++;

                    // 高亮当前执行的点位
                    SelectedPointIndex = ptIdx;

                    SimStatusText = $"[{L("Step3_Radio_SinglePoint")}] {seg.SegmentId} - P{pointIndex} ({pointIndex}/{totalPoints})...";
                    SimProgress = (double)pointIndex / totalPoints * 100;

                    // 模拟单点工艺周期：抬升→XY定位→Z下降→出胶→关胶→抬升
                    int delayMs = Math.Max(100, (int)(SinglePointProcessParams.DispenseTime + SinglePointProcessParams.PreDispenseDelay + SinglePointProcessParams.PostDelay));
                    await Task.Delay(delayMs, _simCts.Token);
                }
            }

            // 仿真结束后清除点位高亮
            SelectedPointIndex = -1;
        }

        /// <summary>暂停仿真（通过取消令牌实现）</summary>
        private void ExecutePauseSim()
        {
            // TODO: 实现真正的暂停逻辑（需要更复杂的异步状态机）
            // 目前简化为停止后可重新开始
            GlobalStatus = L("CadPoint_Status_SimPaused");
        }

        /// <summary>停止仿真——取消正在进行的异步任务</summary>
        private void ExecuteStopSim()
        {
            _simCts?.Cancel();
        }

        /// <summary>
        /// 统一执行入口——Step5 仅支持仿真/真实空跑，真实点胶在 Step6
        /// </summary>
        private async void ExecuteRun()
        {
            var enabledSegments = Segments.Where(s => s.IsEnabled).ToList();
            if (enabledSegments.Count == 0)
            {
                GlobalStatus = L("CadPoint_Error_NoExecutableTrajectory");
                return;
            }

            if (IsSimMode)
            {
                ExecuteDryRun();
                return;
            }

            if (_dispenseExecuteService == null)
            {
                GlobalStatus = L("CadPoint_Status_DispenseUnavailable");
                return;
            }

            _simCts = new System.Threading.CancellationTokenSource();
            IsSimulating = true;
            SimProgress = 0;

            try
            {
                _dispenseExecuteService.ProgressChanged += OnExecuteProgressChanged;

                if (IsRealDryRunMode)
                {
                    if (LineDispenseMode == LineDispenseMode.SinglePoint)
                    {
                        // 单点模式空跑：逐点执行运动，不出胶
                        GlobalStatus = DescendInDryRun
                            ? L("CadPoint_Status_DryRunStart_Descend") + $" ({L("Step3_Radio_SinglePoint")})"
                            : L("CadPoint_Status_DryRunStart_Safe") + $" ({L("Step3_Radio_SinglePoint")})";
                        await _dispenseExecuteService.ExecuteSinglePointLineAsync(
                            enabledSegments, SinglePointProcessParams, StandbyHeight, CurrentNeedleIndex, _simCts.Token,
                            dryRun: !DescendInDryRun);
                    }
                    else
                    {
                        // 连续插补模式空跑
                        GlobalStatus = DescendInDryRun ? L("CadPoint_Status_DryRunStart_Descend") : L("CadPoint_Status_DryRunStart_Safe");
                        await _dispenseExecuteService.DryRunAsync(enabledSegments, DescendInDryRun, CurrentNeedleIndex, _simCts.Token);
                    }
                    GlobalStatus = L("CadPoint_Status_DryRunCompleted");
                }
                else if (IsRealDispenseMode)
                {
                    if (LineDispenseMode == LineDispenseMode.SinglePoint)
                    {
                        GlobalStatus = L("LineBC_Status_SinglePointExecuting");
                        await _dispenseExecuteService.ExecuteSinglePointLineAsync(
                            enabledSegments, SinglePointProcessParams, StandbyHeight, CurrentNeedleIndex, _simCts.Token);
                        GlobalStatus = L("LineBC_Status_SinglePointCompleted");
                    }
                    else
                    {
                        GlobalStatus = L("LineBC_Status_ContinuousInterpolationExecuting");
                        await _dispenseExecuteService.ExecutePathAsync(enabledSegments, "B/C", CurrentNeedleIndex, _simCts.Token);
                        GlobalStatus = L("LineBC_Status_ContinuousInterpolationCompleted");
                    }
                }

                SimStatusText = L("CadPoint_Status_ExecutionComplete");
                SimProgress = 100;
            }
            catch (OperationCanceledException)
            {
                SimStatusText = L("CadPoint_Status_ExecutionStopped");
                GlobalStatus = L("CadPoint_Status_ExecutionStopped");
            }
            catch (Exception ex)
            {
                SimStatusText = string.Format(L("CadPoint_Status_ExecutionError"), ex.Message);
                GlobalStatus = string.Format(L("CadPoint_Status_ExecutionError"), ex.Message);
            }
            finally
            {
                _dispenseExecuteService.ProgressChanged -= OnExecuteProgressChanged;
                IsSimulating = false;
                _simCts?.Dispose();
                _simCts = null;
            }
        }

        /// <summary>执行进度回调——更新进度条和高亮当前段</summary>
        private void OnExecuteProgressChanged(string message, int current, int total)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                SimStatusText = message;
                SimProgress = total > 0 ? (double)current / total * 100 : 0;

                var enabledSegments = Segments.Where(s => s.IsEnabled).ToList();
                var seg = enabledSegments.ElementAtOrDefault(current - 1);
                if (seg?.SourceEntity != null)
                    SelectedEntity = seg.SourceEntity;
            });
        }

        /// <summary>
        /// 示教高度——读取当前Z轴位置作为选中段的示教高度
        /// </summary>
        private void ExecuteTeachHeight()
        {
            if (_selectedSegment == null) return;

            if (_motionService == null)
            {
                GlobalStatus = L("CadPoint_Status_MotionUnavailable");
                return;
            }

            try
            {
                // 读取Z1轴当前位置作为示教高度
                const int AxisDz1 = 3;
                double currentZ = _motionService.GetAxisState(AxisDz1).ActualPosition;
                _selectedSegment.TeachHeight = currentZ;
                _selectedSegment.ZHeight = currentZ;
                GlobalStatus = string.Format(L("CadPoint_Status_TeachHeightSuccess"), _selectedSegment.SegmentId, currentZ);
            }
            catch (Exception ex)
            {
                GlobalStatus = string.Format(L("CadPoint_Status_TeachHeightFailed"), ex.Message);
            }
        }

        #endregion

        #region Step6: 执行走胶命令实现

        /// <summary>
        /// 执行走胶——根据 LineDispenseMode 分发到不同的执行路径
        /// 连续插补模式：调用 DispenseExecuteService.ExecutePathAsync
        /// 单点模式：调用 DispenseExecuteService.ExecuteSinglePointLineAsync
        /// </summary>
        private void ExecutePath()
        {
            var enabledSegments = Segments.Where(s => s.IsEnabled).ToList();
            if (enabledSegments.Count == 0)
            {
                GlobalStatus = L("CadPoint_Error_NoExecutableTrajectory");
                return;
            }

            // 直接调用统一执行入口
            ExecuteRun();
        }

        #endregion

        #region 画布视图命令实现

        /// <summary>适配全部图形——根据包围盒计算缩放和平移，使所有图元居中显示</summary>
        private void ExecuteFitToAll()
        {
            FitCanvasToExtents();
            GlobalStatus = L("CadPoint_Status_ViewFitted");
        }

        /// <summary>
        /// 根据解析结果的包围盒自动适配画布视口
        /// 不再设置 ZoomFactor/PanOffset（这些属性不再控制视口），
        /// 改为通过 FitToAllRequested 事件通知 HalconCanvasControl 调用 FitToAll()
        /// FitToAll() 使用 SetPart() 直接设置 Halcon 窗口视口，确保图元正确居中显示
        /// </summary>
        private void FitCanvasToExtents()
        {
            FitToAllRequested?.Invoke();
        }

        /// <summary>重置视图——通过事件通知画布恢复默认视口</summary>
        private void ExecuteResetView()
        {
            ResetViewRequested?.Invoke();
            GlobalStatus = L("CadPoint_Status_ViewReset");
        }

        #endregion

        #region 画布事件回调

        /// <summary>
        /// 更新坐标显示字符串——由 HalconCanvasControl.CoordinateChanged 事件回调
        /// </summary>
        /// <param name="cadX">CAD X 坐标</param>
        /// <param name="cadY">CAD Y 坐标</param>
        // 画布上最后点击的 CAD 坐标（用于"从画布选取"基准点）
        private double? _lastCanvasClickX;
        private double? _lastCanvasClickY;

        public void UpdateCoordinateDisplay(double cadX, double cadY)
        {
            CoordinateDisplay = cadX.ToString("F3");
            CoordinateDisplayY = cadY.ToString("F3");
        }

        /// <summary>
        /// 画布点击回调——仅在鼠标点击画布时触发，缓存点击坐标用于"从画布选取"
        /// 同时处理仿射/映射CAD坐标拾取状态
        /// </summary>
        public void OnCanvasPointClicked(double cadX, double cadY)
        {
            _lastCanvasClickX = cadX;
            _lastCanvasClickY = cadY;

            // 处理仿射CAD坐标拾取
            if (_isPickingAffineCadCoord && _selectedAffinePoint != null)
            {
                _selectedAffinePoint.CadX = cadX;
                _selectedAffinePoint.CadY = cadY;
                _isPickingAffineCadCoord = false;
                ComputeAffineTransformCommand.RaiseCanExecuteChanged();
                GlobalStatus = string.Format(L("Step4_Status_PickedAffineCad"), _selectedAffinePoint.Name, cadX, cadY);
                return;
            }

            // 处理逐点映射CAD坐标拾取
            if (_isPickingMappingCadCoord && _selectedMappingPoint != null)
            {
                _selectedMappingPoint.CadX = cadX;
                _selectedMappingPoint.CadY = cadY;
                _isPickingMappingCadCoord = false;
                GlobalStatus = string.Format(L("Step4_Status_PickedMappingCad"), _selectedMappingPoint.Name, cadX, cadY);
                return;
            }
        }

        /// <summary>
        /// 图元选中回调——尝试查找对应 DispenseSegment 并设为选中
        /// 实现 DataGrid 行选中 ↔ 画布图元高亮的联动
        /// </summary>
        public void OnEntitySelected(CadEntity entity)
        {
            SelectedEntity = entity;
            // 查找包含该 SourceEntity 的 Segment 并选中
            var matchingSeg = Segments.FirstOrDefault(s => s.SourceEntity == entity);
            if (matchingSeg != null && matchingSeg != _selectedSegment)
                SelectedSegment = matchingSeg;
        }

        /// <summary>
        /// 图元双击回调——跳转到 Step3 并展开该段的详细参数面板
        /// </summary>
        public void OnEntityDoubleClicked(CadEntity entity)
        {
            var matchingSeg = Segments.FirstOrDefault(s => s.SourceEntity == entity);
            if (matchingSeg != null)
            {
                SelectedSegment = matchingSeg;
                if (CurrentStep < 3)
                    GoToStep(3);
            }
        }

        /// <summary>
        /// 根据 SelectedSegment 反向同步画布上的选中图元
        /// 实现 DataGrid 行选中 → 画布高亮的反向联动
        /// </summary>
        private void SyncSelectedEntityFromSegment(DispenseSegment segment)
        {
            if (segment?.SourceEntity != null)
                SelectedEntity = segment.SourceEntity;
        }

        /// <summary>
        /// 画布点击选中图元时，同步更新 SelectedSegment
        /// 通过 SourceEntity 匹配找到对应的 DispenseSegment
        /// </summary>
        private void SyncSelectedSegmentFromEntity(CadEntity entity)
        {
            if (entity == null)
            {
                SelectedSegment = null;
                return;
            }

            // 在 Segments 集合中查找 SourceEntity 匹配的段
            DispenseSegment matched = null;
            foreach (var seg in Segments)
            {
                if (seg.SourceEntity == entity)
                {
                    matched = seg;
                    break;
                }
            }

            // 只在匹配到不同段时更新，避免循环触发
            if (matched != _selectedSegment)
                SelectedSegment = matched;
        }

        #endregion

        #region 集合变更监听

        /// <summary>
        /// Segments 集合内容变化时的处理——刷新状态栏摘要显示和命令可执行状态
        /// </summary>
        private void OnSegmentsCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RefreshStatusBarSummary();
            RaisePropertyChanged(nameof(CanExecute));
            RefreshSegmentIds();

            // 监听新增段的 IsEnabled 变化，触发 CanExecute 重新评估
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is DispenseSegment seg)
                        seg.PropertyChanged += OnSegmentPropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is DispenseSegment seg)
                        seg.PropertyChanged -= OnSegmentPropertyChanged;
                }
            }

            DryRunCommand.RaiseCanExecuteChanged();
            ExecuteRunCommand.RaiseCanExecuteChanged();
            ExecutePathCommand.RaiseCanExecuteChanged();
            DeleteSelectedSegmentsCommand.RaiseCanExecuteChanged();
            SaveSegmentsCommand.RaiseCanExecuteChanged();
        }

        /// <summary>段属性变更回调——IsEnabled 变更时触发 CanExecute 重新评估</summary>
        private void OnSegmentPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DispenseSegment.IsEnabled))
            {
                RaisePropertyChanged(nameof(CanExecute));
                RaisePropertyChanged(nameof(SegmentCountDisplay));
                RaisePropertyChanged(nameof(TotalLengthDisplay));
                DryRunCommand.RaiseCanExecuteChanged();
                ExecuteRunCommand.RaiseCanExecuteChanged();
                ExecutePathCommand.RaiseCanExecuteChanged();
                DeleteSelectedSegmentsCommand.RaiseCanExecuteChanged();
            }
            if (e.PropertyName == nameof(DispenseSegment.SegmentId))
            {
                RefreshSegmentIds();
            }
        }

        /// <summary>刷新 SegmentIds 集合（Step6 下拉框数据源）</summary>
        private void RefreshSegmentIds()
        {
            SegmentIds.Clear();
            foreach (var seg in Segments)
                SegmentIds.Add(seg.SegmentId);
        }

        /// <summary>刷新状态栏摘要（段数、总长度、对齐状态）</summary>
        private void RefreshStatusBarSummary()
        {
            RaisePropertyChanged(nameof(SegmentCountDisplay));
            RaisePropertyChanged(nameof(TotalLengthDisplay));
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 重置所有状态到初始值——清除数据、回到 Step 1
        /// </summary>
        public void ResetAll()
        {
            // 停止正在运行的仿真
            _simCts?.Cancel();

            // 清空集合
            CanvasEntities.Clear();
            Segments.Clear();
            _layerCheckList.Clear();

            // 重置字段
            _parsedDxfResult = null;
            FilePath = string.Empty;
            ImportStatusMessage = string.Empty;
            _selectedSegment = null;
            _selectedEntity = null;
            _simProgress = 0;
            _simStatusText = L("Step5_Status_Waiting");
            _isSimulating = false;

            // 重置视图（通过事件通知画布重置视口）
            ZoomFactor = 1.0;
            PanOffsetX = 0;
            PanOffsetY = 0;
            ShowGrid = false;
            CurrentRoiPreview = null;
            ResetViewRequested?.Invoke();

            // 重置 ROI 工具
            IsLineRoiActive = false;
            IsPolylineRoiActive = false;
            IsArcRoiActive = false;

            // 回到 Step 1
            GoToStep(1);

            // 刷新所有依赖属性
            RaisePropertyChanged(nameof(HasSelectedSegment));
            RaisePropertyChanged(nameof(SegmentSummaryDisplay));
            RaisePropertyChanged(nameof(CanExecute));
            GlobalStatus = L("CadPoint_Status_Ready");
        }

        /// <summary>
        /// 从外部加载轨迹段数据（用于配方恢复场景）
        /// 加载后重建 CanvasEntities 并跳转到 Step 3
        /// </summary>
        /// <param name="segments">要加载的轨迹段集合</param>
        public void LoadSegments(ObservableCollection<DispenseSegment> segments)
        {
            if (segments == null) return;

            ResetAll();

            foreach (var seg in segments)
            {
                Segments.Add(seg);
                if (seg.SourceEntity != null)
                    CanvasEntities.Add(seg.SourceEntity);
            }

            // 重建图层列表
            var layers = segments.Select(s => s.LayerName).Distinct().Where(l => !string.IsNullOrEmpty(l)).ToList();
            foreach (var layer in layers)
                _layerCheckList.Add(new LayerCheckItem { LayerName = layer, IsVisible = true });
            _layerNames = layers;
            RaisePropertyChanged(nameof(LayerNames));

            GlobalStatus = string.Format(L("CadPoint_Status_SegmentsLoaded"), Segments.Count);
            GoToStep(3);
        }

        #endregion

        #region 辅助方法 — 输入对话框

        /// <summary>
        /// 显示简单的字符串输入对话框——用于批量设速/设胶等需要用户输入数值的场景
        /// </summary>
        /// <param name="prompt">提示文字</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>用户输入的字符串（取消返回 null）</returns>
        private string ShowInputDialog(string prompt, string defaultValue = "")
        {
            string result = null;
            var inputWindow = new Window
            {
                Title = L("CadPoint_Dialog_InputTitle"),
                Width = 360,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Owner = Application.Current?.MainWindow
            };

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 8) });

            var textBox = new TextBox { Text = defaultValue, Margin = new Thickness(0, 0, 0, 12) };
            panel.Children.Add(textBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var okButton = new Button { Content = L("CadPoint_Dialog_Btn_OK"), MinWidth = 70, Margin = new Thickness(4, 0, 0, 0), IsDefault = true };
            var cancelButton = new Button { Content = L("CadPoint_Dialog_Btn_Cancel"), MinWidth = 70, Margin = new Thickness(4, 0, 0, 0), IsCancel = true };
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            panel.Children.Add(buttonPanel);

            inputWindow.Content = panel;
            okButton.Click += (s, e) => { result = textBox.Text; inputWindow.Close(); };
            cancelButton.Click += (s, e) => inputWindow.Close();
            textBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter) { result = textBox.Text; inputWindow.Close(); }
                if (e.Key == System.Windows.Input.Key.Escape) inputWindow.Close();
            };

            inputWindow.ShowDialog();
            return result;
        }

        #endregion

        #region 测试方法

        /// <summary>
        /// 加载测试图元——绕过 DXF 解析器，直接创建几何图元验证渲染管线
        /// 创建一个矩形(4条LINE)、一个圆(CIRCLE)和一段弧(ARC)
        /// </summary>
        /// <summary>
        /// 保存轨迹段到 JSON 文件——使用 SaveFileDialog 让用户选择保存位置
        /// 序列化时跳过 SourceEntity（CadEntity 抽象类不可序列化）和 Length（只读计算属性）
        /// </summary>
        private void ExecuteSaveSegments()
        {
            if (Segments.Count == 0) return;

            var defaultDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Segments");
            if (!System.IO.Directory.Exists(defaultDir))
                System.IO.Directory.CreateDirectory(defaultDir);

            string fileName = $"segments_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            string fullSavePath = System.IO.Path.Combine(defaultDir, fileName);

            try
            {
                // 保存当前针头数据到备份
                SaveCurrentNeedleData(_currentNeedleIndex);

                // 构建保存数据：轨迹段 + 坐标对齐参数
                var saveData = new Core.Models.SegmentSaveData
                {
                    Segments = Segments.ToList(),
                    AlignData = new Core.Models.CoordinateAlignData
                    {
                        AlignMode = IsModeAffine ? "Affine" : "PointMapping",
                        AffineCalibrationPointsNeedle1 = _affineCalibrationPointsNeedle1.ToList(),
                        AffineCalibrationPointsNeedle2 = _affineCalibrationPointsNeedle2.ToList(),
                        PointMappingPointsNeedle1 = _pointMappingPointsNeedle1.ToList(),
                        PointMappingPointsNeedle2 = _pointMappingPointsNeedle2.ToList(),
                        CurrentNeedleIndex = CurrentNeedleIndex,
                        AffineResultDataNeedle1 = _affineResultNeedle1 != null ? new Core.Models.AffineResultData
                        {
                            A = _affineResultNeedle1.A, B = _affineResultNeedle1.B,
                            C = _affineResultNeedle1.C, D = _affineResultNeedle1.D,
                            Tx = _affineResultNeedle1.Tx, Ty = _affineResultNeedle1.Ty,
                            RmsError = _affineResultNeedle1.RmsError,
                            PointCount = _affineResultNeedle1.PointCount
                        } : null,
                        AffineResultDataNeedle2 = _affineResultNeedle2 != null ? new Core.Models.AffineResultData
                        {
                            A = _affineResultNeedle2.A, B = _affineResultNeedle2.B,
                            C = _affineResultNeedle2.C, D = _affineResultNeedle2.D,
                            Tx = _affineResultNeedle2.Tx, Ty = _affineResultNeedle2.Ty,
                            RmsError = _affineResultNeedle2.RmsError,
                            PointCount = _affineResultNeedle2.PointCount
                        } : null
                    }
                };

                var options = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNameCaseInsensitive = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                string json = System.Text.Json.JsonSerializer.Serialize(saveData, options);
                System.IO.File.WriteAllText(fullSavePath, json);
                RecordSegmentConfigPath(fullSavePath);
                GlobalStatus = string.Format(L("CadPoint_Status_SegmentsSaved"), Segments.Count, System.IO.Path.GetFileName(fullSavePath));
            }
            catch (Exception ex)
            {
                GlobalStatus = string.Format(L("CadPoint_Status_SaveFailed"), ex.Message);
            }
        }

        /// <summary>
        /// 从 SegmentFilePath 指定的 JSON 文件加载轨迹段
        /// 加载后恢复 CanvasEntities 和 Segments，并刷新画布显示
        /// </summary>
        private void ExecuteLoadSegments()
        {
            var path = _segmentFilePath;
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                GlobalStatus = L("CadPoint_Status_NoValidConfig");
                return;
            }

            try
            {
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                string json = System.IO.File.ReadAllText(path);

                // 尝试按新格式（SegmentSaveData）反序列化
                var saveData = System.Text.Json.JsonSerializer.Deserialize<Core.Models.SegmentSaveData>(json, options);
                List<Core.Models.DispenseSegment> loaded;
                Core.Models.CoordinateAlignData alignData = null;

                if (saveData?.Segments != null && saveData.Segments.Count > 0)
                {
                    // 新格式：包含轨迹段 + 对齐数据
                    loaded = saveData.Segments;
                    alignData = saveData.AlignData;
                }
                else
                {
                    // 旧格式兼容：纯轨迹段列表
                    loaded = System.Text.Json.JsonSerializer.Deserialize<List<Core.Models.DispenseSegment>>(json, options);
                }

                if (loaded == null || loaded.Count == 0)
                {
                    GlobalStatus = L("CadPoint_Error_NoTrajectoryInFile");
                    return;
                }

                // 清空当前数据
                CanvasEntities.Clear();
                Segments.Clear();
                _layerCheckList.Clear();
                SelectedSegment = null;

                // 恢复轨迹段
                foreach (var seg in loaded)
                    Segments.Add(seg);

                // 从轨迹段重建 CanvasEntities（用于画布渲染）
                RebuildCanvasEntitiesFromSegments();

                // 重建图层列表
                RebuildLayerList();

                // 恢复坐标对齐数据
                if (alignData != null)
                {
                    // 恢复针头1仿射标定点
                    if (alignData.AffineCalibrationPointsNeedle1 != null && alignData.AffineCalibrationPointsNeedle1.Count > 0)
                    {
                        _affineCalibrationPointsNeedle1 = new List<AffineCalibrationPoint>(alignData.AffineCalibrationPointsNeedle1);
                    }
                    // 恢复针头2仿射标定点
                    if (alignData.AffineCalibrationPointsNeedle2 != null && alignData.AffineCalibrationPointsNeedle2.Count > 0)
                    {
                        _affineCalibrationPointsNeedle2 = new List<AffineCalibrationPoint>(alignData.AffineCalibrationPointsNeedle2);
                    }
                    // 兼容旧版数据：如果旧版 AffineCalibrationPoints 有数据，迁移到针头1
                    if ((_affineCalibrationPointsNeedle1 == null || _affineCalibrationPointsNeedle1.Count == 0)
                        && alignData.AffineCalibrationPoints != null && alignData.AffineCalibrationPoints.Count > 0)
                    {
                        _affineCalibrationPointsNeedle1 = new List<AffineCalibrationPoint>(alignData.AffineCalibrationPoints);
                    }

                    // 恢复针头1逐点映射点
                    if (alignData.PointMappingPointsNeedle1 != null && alignData.PointMappingPointsNeedle1.Count > 0)
                    {
                        _pointMappingPointsNeedle1 = new List<PointMappingPoint>(alignData.PointMappingPointsNeedle1);
                    }
                    // 恢复针头2逐点映射点
                    if (alignData.PointMappingPointsNeedle2 != null && alignData.PointMappingPointsNeedle2.Count > 0)
                    {
                        _pointMappingPointsNeedle2 = new List<PointMappingPoint>(alignData.PointMappingPointsNeedle2);
                    }
                    // 兼容旧版数据：如果旧版 PointMappingPoints 有数据，迁移到针头1
                    if ((_pointMappingPointsNeedle1 == null || _pointMappingPointsNeedle1.Count == 0)
                        && alignData.PointMappingPoints != null && alignData.PointMappingPoints.Count > 0)
                    {
                        _pointMappingPointsNeedle1 = new List<PointMappingPoint>(alignData.PointMappingPoints);
                    }

                    // 恢复针头索引（先设置 _currentNeedleIndex 但不触发切换）
                    _currentNeedleIndex = alignData.CurrentNeedleIndex;
                    _previousNeedleIndex = _currentNeedleIndex;
                    RaisePropertyChanged(nameof(CurrentNeedleIndex));
                    RaisePropertyChanged(nameof(IsNeedle1Selected));
                    RaisePropertyChanged(nameof(IsNeedle2Selected));

                    // 恢复针头1仿射结果
                    if (alignData.AffineResultDataNeedle1 != null)
                    {
                        _affineResultNeedle1 = new AffineCalibrationResult
                        {
                            A = alignData.AffineResultDataNeedle1.A,
                            B = alignData.AffineResultDataNeedle1.B,
                            C = alignData.AffineResultDataNeedle1.C,
                            D = alignData.AffineResultDataNeedle1.D,
                            Tx = alignData.AffineResultDataNeedle1.Tx,
                            Ty = alignData.AffineResultDataNeedle1.Ty,
                            RmsError = alignData.AffineResultDataNeedle1.RmsError,
                            PointCount = alignData.AffineResultDataNeedle1.PointCount
                        };
                    }
                    // 恢复针头2仿射结果
                    if (alignData.AffineResultDataNeedle2 != null)
                    {
                        _affineResultNeedle2 = new AffineCalibrationResult
                        {
                            A = alignData.AffineResultDataNeedle2.A,
                            B = alignData.AffineResultDataNeedle2.B,
                            C = alignData.AffineResultDataNeedle2.C,
                            D = alignData.AffineResultDataNeedle2.D,
                            Tx = alignData.AffineResultDataNeedle2.Tx,
                            Ty = alignData.AffineResultDataNeedle2.Ty,
                            RmsError = alignData.AffineResultDataNeedle2.RmsError,
                            PointCount = alignData.AffineResultDataNeedle2.PointCount
                        };
                    }
                    // 兼容旧版数据：如果旧版 AffineResultData 有数据，迁移到针头1
                    if (_affineResultNeedle1 == null && alignData.AffineResultData != null)
                    {
                        _affineResultNeedle1 = new AffineCalibrationResult
                        {
                            A = alignData.AffineResultData.A,
                            B = alignData.AffineResultData.B,
                            C = alignData.AffineResultData.C,
                            D = alignData.AffineResultData.D,
                            Tx = alignData.AffineResultData.Tx,
                            Ty = alignData.AffineResultData.Ty,
                            RmsError = alignData.AffineResultData.RmsError,
                            PointCount = alignData.AffineResultData.PointCount
                        };
                    }

                    // 恢复对齐模式
                    switch (alignData.AlignMode)
                    {
                        case "Affine":
                            IsModeAffine = true;
                            break;
                        case "PointMapping":
                            IsModePointMapping = true;
                            break;
                        default:
                            IsModeAffine = true;
                            break;
                    }

                    // 加载当前针头的数据到UI集合
                    LoadNeedleData(_currentNeedleIndex);
                }

                // 适配视口
                FitCanvasToExtents();

                RecordSegmentConfigPath(path);
                GlobalStatus = string.Format(L("CadPoint_Status_LoadSuccess"), Segments.Count, CanvasEntities.Count)
                    + (alignData != null ? string.Format(L("CadPoint_Status_LoadAlignMode"), alignData.AlignMode) : "");
                GoToStep(2);
            }
            catch (Exception ex)
            {
                GlobalStatus = string.Format(L("CadPoint_Status_LoadFailed"), ex.Message);
            }
        }

        /// <summary>
        /// 从 DispenserStationParams 恢复上次配置路径到共享存储
        /// </summary>
        private void RestorePathFromStationParams()
        {
            try
            {
                var registry = ContainerLocator.Container?.Resolve<IStationRegistry>();
                var station = registry?.GetStation("DispenserStation");
                if (station is IStationParameterProvider provider &&
                    provider.CurrentParameters is StationTasks.Params.DispenserStationParams dsp &&
                    !string.IsNullOrWhiteSpace(dsp.LastSegmentConfigPath))
                {
                    _dispenseSegmentStore.LastSegmentConfigPath = dsp.LastSegmentConfigPath;
                }
            }
            catch { /* 静默处理，配方参数可能尚未加载 */ }
        }

        /// <summary>
        /// 记录轨迹段配置文件路径到共享存储和配方参数
        /// </summary>
        private void RecordSegmentConfigPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            _dispenseSegmentStore.LastSegmentConfigPath = path;
            SyncPathToStationParams(path);
            SegmentFilePath = path;
        }

        /// <summary>
        /// 同步路径到 DispenserStationParams 并触发配方持久化保存
        /// </summary>
        private void SyncPathToStationParams(string path)
        {
            try
            {
                var registry = ContainerLocator.Container?.Resolve<IStationRegistry>();
                var station = registry?.GetStation("DispenserStation");
                if (station is IStationParameterProvider provider &&
                    provider.CurrentParameters is StationTasks.Params.DispenserStationParams dsp)
                {
                    dsp.LastSegmentConfigPath = path;

                    var recipePoolService = ContainerLocator.Container?.Resolve<IRecipePoolService>();
                    if (recipePoolService != null)
                    {
                        recipePoolService.StageStationParameters(provider.StationIdentifier, dsp);
                        recipePoolService.CommitStagedParametersAsync(
                            provider.CurrentPoolName, provider.CurrentRecipeName).ConfigureAwait(false);
                    }
                }
            }
            catch { /* 静默处理，不影响主流程 */ }
        }

        /// <summary>
        /// 尝试恢复上次使用的轨迹段配置文件路径并自动加载
        /// </summary>
        public void TryAutoLoadLastConfig()
        {
            var path = _dispenseSegmentStore?.LastSegmentConfigPath;
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return;

            SegmentFilePath = path;
            ExecuteLoadSegments();
        }

        /// <summary>
        /// 从 Segments 中的 SourceEntity 重建 CanvasEntities
        /// 如果 SourceEntity 为 null（从 JSON 加载时），则根据 EntityType 和 Points 重建图元：
        /// - Line 类型：用首尾两点创建 CadLine
        /// - 其他曲线类型（Arc/Circle/Ellipse/LwPolyline）：用全部采样点创建 CadLwPolyline，保留曲线形状
        /// </summary>
        private void RebuildCanvasEntitiesFromSegments()
        {
            var newEntities = new ObservableCollection<CadEntity>();

            foreach (var seg in Segments)
            {
                if (seg.SourceEntity != null)
                {
                    newEntities.Add(seg.SourceEntity);
                }
                else if (seg.Points != null && seg.Points.Count >= 2)
                {
                    // 优先从 OriginalEntityData 重建原始图元（如 CadArc/CadCircle）
                    // 这样加载保存的轨迹段时，弧线仍然是弧线，而不是折线
                    CadEntity entity = null;
                    if (seg.OriginalEntityData != null)
                    {
                        entity = seg.OriginalEntityData.ToEntity();
                        if (entity != null)
                        {
                            seg.OriginalSourceEntity = entity;
                        }
                    }

                    // 如果无法从 OriginalEntityData 重建，则根据 EntityType 和 Points 创建
                    if (entity == null)
                    {
                        if (seg.EntityType == CadEntityType.Line)
                        {
                            entity = new CadLine(
                                seg.Points[0].X, seg.Points[0].Y,
                                seg.Points[seg.Points.Count - 1].X, seg.Points[seg.Points.Count - 1].Y)
                            {
                                LayerName = seg.LayerName ?? "LOADED"
                            };
                        }
                        else
                        {
                            var vertices = seg.Points.Select(p => new Core.Models.PointF((float)p.X, (float)p.Y)).ToList();
                            bool isClosed = seg.EntityType == CadEntityType.Circle;
                            entity = new CadLwPolyline(vertices, isClosed)
                            {
                                LayerName = seg.LayerName ?? "LOADED"
                            };
                        }

                        // 首次创建时保存为 OriginalSourceEntity 和 OriginalEntityData
                        if (seg.OriginalSourceEntity == null)
                            seg.OriginalSourceEntity = entity;
                        if (seg.OriginalEntityData == null)
                            seg.OriginalEntityData = OriginalEntityData.FromEntity(entity);
                    }

                    newEntities.Add(entity);
                    seg.SourceEntity = entity;
                }
            }

            CanvasEntities = newEntities;
        }

        /// <summary>
        /// 从 CanvasEntities 重建图层列表
        /// </summary>
        private void RebuildLayerList()
        {
            _layerCheckList.Clear();
            var layers = CanvasEntities.Select(e => e.LayerName).Distinct().ToList();
            _layerNames = layers;
            foreach (var layer in layers)
                _layerCheckList.Add(new LayerCheckItem { LayerName = layer, IsVisible = true });
            if (layers.Count > 0)
                SelectedLayer = layers[0];
            RaisePropertyChanged(nameof(LayerNames));
            RaisePropertyChanged(nameof(SegmentSummaryDisplay));
        }

        public void LoadTestEntities()
        {
            CanvasEntities.Clear();
            Segments.Clear();
            _layerCheckList.Clear();

            // 矩形边框：4条线段 (0,0)→(200,0)→(200,120)→(0,120)→(0,0)
            CanvasEntities.Add(new CadLine(0, 0, 200, 0) { LayerName = "BASE_FRAME" });
            CanvasEntities.Add(new CadLine(200, 0, 200, 120) { LayerName = "BASE_FRAME" });
            CanvasEntities.Add(new CadLine(200, 120, 0, 120) { LayerName = "BASE_FRAME" });
            CanvasEntities.Add(new CadLine(0, 120, 0, 0) { LayerName = "BASE_FRAME" });

            // 点胶轨迹1：L形折线（3条线段模拟直角点胶路径）
            CanvasEntities.Add(new CadLine(20, 20, 20, 80) { LayerName = "DISPENSE_GLUE" });
            CanvasEntities.Add(new CadLine(20, 80, 80, 80) { LayerName = "DISPENSE_GLUE" });

            // 点胶轨迹2：U形折线（4条线段模拟U型点胶路径）
            CanvasEntities.Add(new CadLine(120, 20, 120, 80) { LayerName = "DISPENSE_GLUE" });
            CanvasEntities.Add(new CadLine(120, 80, 180, 80) { LayerName = "DISPENSE_GLUE" });
            CanvasEntities.Add(new CadLine(180, 80, 180, 20) { LayerName = "DISPENSE_GLUE" });

            // 圆：中心(50,50)，半径8
            CanvasEntities.Add(new CadCircle(50, 50, 8) { LayerName = "DISPENSE_GLUE" });

            // 弧：中心(150,50)，半径30，起角0°，终角180°
            CanvasEntities.Add(new CadArc(150, 50, 30, 0, 180) { LayerName = "DISPENSE_GLUE" });

            // 手动构建包围盒
            _parsedDxfResult = new DxfParseResult(
                new Dictionary<string, List<CadEntity>>
                {
                    { "BASE_FRAME", CanvasEntities.Where(e => e.LayerName == "BASE_FRAME").ToList() },
                    { "DISPENSE_GLUE", CanvasEntities.Where(e => e.LayerName == "DISPENSE_GLUE").ToList() }
                },
                new BoundingBox(-5, 205, -5, 125),
                new List<string>());

            _layerCheckList.Add(new LayerCheckItem { LayerName = "BASE_FRAME", IsVisible = true });
            _layerCheckList.Add(new LayerCheckItem { LayerName = "DISPENSE_GLUE", IsVisible = true });
            _layerNames = new List<string> { "BASE_FRAME", "DISPENSE_GLUE" };
            SelectedLayer = "DISPENSE_GLUE";
            RaisePropertyChanged(nameof(LayerNames));
            RaisePropertyChanged(nameof(SegmentSummaryDisplay));

            GlobalStatus = string.Format(L("CadPoint_Status_TestMode"), CanvasEntities.Count, _layerCheckList.Count);

            // 自动适配视口并跳转到 Step2 显示画布
            FitCanvasToExtents();
            GoToStep(2);
        }

        #endregion
    }

    #region 批量设置参数对话框支持类

    /// <summary>批量设置参数项模型——表示单个可批量设置的参数</summary>
    public class BatchParamItem : BindableBase
    {
        public string DisplayName { get; set; }
        public string Unit { get; set; }
        private bool _isEnabled = true;
        public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
        private double _value;
        public double Value { get => _value; set => SetProperty(ref _value, value); }

        public BatchParamItem(string displayName, string unit, double defaultValue)
        {
            DisplayName = displayName;
            Unit = unit;
            Value = defaultValue;
        }

        public virtual void ApplyTo(DispenseSegment seg) { }
    }

    /// <summary>具体的批量设置参数项——带类型化的应用逻辑</summary>
    public class TypedBatchParamItem : BatchParamItem
    {
        private readonly Action<DispenseSegment, double> _applyAction;

        public TypedBatchParamItem(string displayName, string unit, double defaultValue, Action<DispenseSegment, double> applyAction)
            : base(displayName, unit, defaultValue)
        {
            _applyAction = applyAction;
        }

        public override void ApplyTo(DispenseSegment seg)
        {
            _applyAction(seg, Value);
        }
    }

    /// <summary>批量设置参数对话框 ViewModel</summary>
    public class BatchSetParamsViewModel : BindableBase
    {
        public ObservableCollection<BatchParamItem> BatchParamItems { get; } = new();
        public DelegateCommand ConfirmCommand { get; }
        public DelegateCommand CancelCommand { get; }

        private readonly Window _dialogWindow;

        /// <summary>获取多语言文本（便捷方法）</summary>
        private static string L(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;
            var resource = Application.Current?.TryFindResource(key);
            return resource?.ToString() ?? $"[{key}]";
        }

        public BatchSetParamsViewModel(DispenseSegment referenceSegment, Window dialogWindow)
        {
            _dialogWindow = dialogWindow;

            BatchParamItems.Add(new TypedBatchParamItem(L("CadPoint_BatchParam_JumpSpeed"), "mm/s", referenceSegment.JumpSpeed,
                (seg, val) => seg.JumpSpeed = val));
            BatchParamItems.Add(new TypedBatchParamItem(L("CadPoint_BatchParam_MoveSpeed"), "mm/s", referenceSegment.MoveSpeed,
                (seg, val) => seg.MoveSpeed = val));
            BatchParamItems.Add(new TypedBatchParamItem(L("CadPoint_BatchParam_SafeHeight"), "mm", referenceSegment.SafeHeight,
                (seg, val) => seg.SafeHeight = val));
            BatchParamItems.Add(new TypedBatchParamItem(L("CadPoint_BatchParam_ApproachHeight"), "mm", referenceSegment.ApproachHeight,
                (seg, val) => seg.ApproachHeight = val));
            BatchParamItems.Add(new TypedBatchParamItem(L("CadPoint_BatchParam_CornerDecel"), "", referenceSegment.CornerDecel,
                (seg, val) => seg.CornerDecel = val));
            BatchParamItems.Add(new TypedBatchParamItem(L("CadPoint_BatchParam_GlueTriggerOffset"), "mm", referenceSegment.GlueTriggerOffsetMm,
                (seg, val) => seg.GlueTriggerOffsetMm = val));
            BatchParamItems.Add(new TypedBatchParamItem(L("CadPoint_BatchParam_PreDelay"), "ms", referenceSegment.PreDelay,
                (seg, val) => seg.PreDelay = val));
            BatchParamItems.Add(new TypedBatchParamItem(L("CadPoint_BatchParam_PostDelay"), "ms", referenceSegment.PostDelay,
                (seg, val) => seg.PostDelay = val));

            ConfirmCommand = new DelegateCommand(() =>
            {
                if (_dialogWindow != null)
                    _dialogWindow.DialogResult = true;
            });

            CancelCommand = new DelegateCommand(() =>
            {
                if (_dialogWindow != null)
                    _dialogWindow.DialogResult = false;
            });
        }
    }

    public class BatchSetSpeedViewModel : BindableBase
    {
        private double _jumpSpeed;
        public double JumpSpeed { get => _jumpSpeed; set => SetProperty(ref _jumpSpeed, value); }

        private double _moveSpeed;
        public double MoveSpeed { get => _moveSpeed; set => SetProperty(ref _moveSpeed, value); }

        public DelegateCommand ConfirmCommand { get; }
        public DelegateCommand CancelCommand { get; }

        private readonly Window _dialogWindow;

        public BatchSetSpeedViewModel(double jumpSpeed, double moveSpeed, Window dialogWindow)
        {
            _dialogWindow = dialogWindow;
            JumpSpeed = jumpSpeed;
            MoveSpeed = moveSpeed;

            ConfirmCommand = new DelegateCommand(() =>
            {
                if (_dialogWindow != null)
                    _dialogWindow.DialogResult = true;
            });

            CancelCommand = new DelegateCommand(() =>
            {
                if (_dialogWindow != null)
                    _dialogWindow.DialogResult = false;
            });
        }
    }

    #endregion
}
