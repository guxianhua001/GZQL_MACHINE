#if HAS_HALCON
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Core.Abstraction;
using Core.Models;
using Core.Services;
using HalconDotNet;
using Module.Services;
using Module.ViewModels;
using MotionControl.Interfaces;
using Prism.Events;
using Prism.Ioc;

namespace Module.Controls
{
    /// <summary>
    /// 独立点胶轨迹编辑器控件——封装 6 步操作流程的完整 UI
    /// 包含：步骤引导条、HalconCanvas 画布、动态右侧面板、全局状态栏
    /// 设计为自包含可复用控件，不依赖特定父窗口，通过 DependencyProperty 暴露关键接口
    /// </summary>
    public partial class CadPointEditorControl : UserControl
    {
        #region 依赖属性定义

        // 输出：所有轨迹段集合（外部宿主可绑定读取）
        public static readonly DependencyProperty SegmentsProperty =
            DependencyProperty.Register(nameof(Segments), typeof(ObservableCollection<DispenseSegment>),
                typeof(CadPointEditorControl), new PropertyMetadata(null));

        // 当前操作步骤（1-6）
        public static readonly DependencyProperty CurrentStepProperty =
            DependencyProperty.Register(nameof(CurrentStep), typeof(int),
                typeof(CadPointEditorControl), new PropertyMetadata(1, OnCurrentStepChanged));

        // DXF 文件路径
        public static readonly DependencyProperty FilePathProperty =
            DependencyProperty.Register(nameof(FilePath), typeof(string),
                typeof(CadPointEditorControl), new PropertyMetadata(string.Empty));

        #endregion

        #region CLR 属性访问器

        /// <summary>输出：所有轨迹段集合（外部宿主可绑定读取）</summary>
        public ObservableCollection<DispenseSegment> Segments
        {
            get => (ObservableCollection<DispenseSegment>)GetValue(SegmentsProperty);
            set => SetValue(SegmentsProperty, value);
        }

        /// <summary>当前操作步骤（范围 1~6）</summary>
        public int CurrentStep
        {
            get => (int)GetValue(CurrentStepProperty);
            set => SetValue(CurrentStepProperty, value);
        }

        /// <summary>当前加载的 DXF 文件路径</summary>
        public string FilePath
        {
            get => (string)GetValue(FilePathProperty);
            set => SetValue(FilePathProperty, value);
        }

        #endregion

        #region 路由事件定义

        /// <summary>
        /// 执行请求路由事件——用户在 Step6 点击"执行走胶"时触发
        /// 外部宿主可通过 AddHandler 监听此事件以接管执行逻辑
        /// </summary>
        public static readonly RoutedEvent ExecuteRequestEvent =
            EventManager.RegisterRoutedEvent(nameof(ExecuteRequest),
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(CadPointEditorControl));

        /// <summary>执行请求事件（add/remove 访问器）</summary>
        public event RoutedEventHandler ExecuteRequest
        {
            add => AddHandler(ExecuteRequestEvent, value);
            remove => RemoveHandler(ExecuteRequestEvent, value);
        }

        #endregion

        #region 私有字段

        // 内部 ViewModel 引用
        private CadPointEditorViewModel _viewModel;

        #endregion

        #region 构造函数

        /// <summary>
        /// 无参构造函数——初始化控件、创建 ViewModel 并建立双向绑定
        /// 控件内部管理自己的 DataContext，不依赖 Prism ViewModelLocator
        /// </summary>
        public CadPointEditorControl()
        {
            InitializeComponent();
            IDxfParserService dxfParser = null;
            IDxfImportHelper dxfImportHelper = null;
            IRoiToolService roiTool = null;
            ICoordinateAlignService alignService = null;
            IDispenseExecuteService dispenseService = null;
            IMotionService motionService = null;
            ILocalizationService localizationService = null;
            IDispenseSegmentStore dispenseSegmentStore = null;
            IEventAggregator eventAggregator = null;
            try
            {
                var container = ContainerLocator.Container;
                if (container != null)
                {
                    dxfParser = container.Resolve<IDxfParserService>();
                    dxfImportHelper = container.Resolve<IDxfImportHelper>();
                    roiTool = container.Resolve<IRoiToolService>();
                    alignService = container.Resolve<ICoordinateAlignService>();
                    dispenseService = container.Resolve<IDispenseExecuteService>();
                    motionService = container.Resolve<IMotionService>();
                    localizationService = container.Resolve<ILocalizationService>();
                    dispenseSegmentStore = container.Resolve<IDispenseSegmentStore>();
                    eventAggregator = container.Resolve<IEventAggregator>();
                }
            }
            catch { /* 服务未注册时忽略 */ }

            _viewModel = new CadPointEditorViewModel(dxfParser, dxfImportHelper, roiTool, alignService, dispenseService, motionService, localizationService, dispenseSegmentStore, eventAggregator);
            DataContext = _viewModel;
            Loaded += OnLoaded;
            SetupBindings();
            RegisterCanvasEvents();
            RegisterDataGridEvents();
        }

        #endregion

        #region 绑定与事件桥接

        /// <summary>
        /// 建立控件 DependencyProperty 与内部 ViewModel 属性之间的双向同步
        /// 使外部宿主通过 DP 设置/读取数据时能正确反映到内部状态
        /// </summary>
        private void SetupBindings()
        {
            // Segments DP ↔ ViewModel.Segments 双向同步
            SetBinding(SegmentsProperty,
                new System.Windows.Data.Binding("Segments") { Source = _viewModel, Mode = System.Windows.Data.BindingMode.OneWay });
            // CurrentStep DP ↔ ViewModel.CurrentStep 双向同步
            SetBinding(CurrentStepProperty,
                new System.Windows.Data.Binding("CurrentStep") { Source = _viewModel, Mode = System.Windows.Data.BindingMode.TwoWay });
            // FilePath DP ↔ ViewModel.FilePath 双向同步
            SetBinding(FilePathProperty,
                new System.Windows.Data.Binding("FilePath") { Source = _viewModel, Mode = System.Windows.Data.BindingMode.TwoWay });

            // HalconCanvas 的数据绑定已在 XAML 中通过 DataContext 继承自动建立，
            // 无需代码重复绑定，避免与 XAML 绑定冲突
        }

        /// <summary>
        /// 注册 HalconCanvas 控件的 CLR 事件，将坐标变化和图元选中事件转发给 ViewModel 处理
        /// </summary>
        private void RegisterCanvasEvents()
        {
            if (halconCanvas == null) return;
            halconCanvas.CoordinateChanged += OnCanvasCoordinateChanged;
            halconCanvas.CanvasPointClicked += OnCanvasPointClicked;
            halconCanvas.EntitySelected += OnCanvasEntitySelected;
            halconCanvas.EntityDoubleClicked += OnCanvasEntityDoubleClicked;
            halconCanvas.RoiCompleted += OnCanvasRoiCompleted;

            // 监听 ViewModel 的 ROI 激活属性变化，同步到 HalconCanvas.DrawMode
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                _viewModel.FitToAllRequested += OnFitToAllRequested;
                _viewModel.ResetViewRequested += OnResetViewRequested;
                _viewModel.CanvasRefreshRequested += OnCanvasRefreshRequested;
            }
        }

        /// <summary>
        /// Loaded 事件——DI 容器就绪后重新尝试解析 ILocalizationService 并刷新步骤标题
        /// 解决 XAML 加载时容器未初始化导致 _localizationService 为 null 的问题
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            try
            {
                var container = ContainerLocator.Container;
                if (container != null)
                {
                    var svc = container.Resolve<ILocalizationService>();
                    if (svc != null)
                    {
                        _viewModel.RefreshLocalization(svc);
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// 注册 Step3 DataGrid 的多选事件，将选中项同步到 ViewModel
        /// </summary>
        private void RegisterDataGridEvents()
        {
        }

        /// <summary>递归查找可视化树中指定类型的子元素</summary>
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            var children = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < children; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T result) return result;
                var found = FindVisualChild<T>(child);
                if (found != null) return found;
            }
            return null;
        }

        private void UnregisterCanvasEvents()
        {
            if (halconCanvas == null) return;
            halconCanvas.CoordinateChanged -= OnCanvasCoordinateChanged;
            halconCanvas.CanvasPointClicked -= OnCanvasPointClicked;
            halconCanvas.EntitySelected -= OnCanvasEntitySelected;
            halconCanvas.EntityDoubleClicked -= OnCanvasEntityDoubleClicked;
            halconCanvas.RoiCompleted -= OnCanvasRoiCompleted;

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _viewModel.FitToAllRequested -= OnFitToAllRequested;
                _viewModel.ResetViewRequested -= OnResetViewRequested;
                _viewModel.CanvasRefreshRequested -= OnCanvasRefreshRequested;
            }
        }

        /// <summary>
        /// ViewModel 请求 FitToAll 的事件处理——调用 HalconCanvas 的 FitToAll() 自适应视口
        /// </summary>
        private void OnFitToAllRequested()
        {
            halconCanvas?.FitToAll();
        }

        /// <summary>
        /// ViewModel 请求 ResetView 的事件处理——调用 HalconCanvas 的 ResetView() 重置视口
        /// </summary>
        private void OnResetViewRequested()
        {
            halconCanvas?.ResetView();
        }

        /// <summary>
        /// ViewModel 请求刷新画布的事件处理——调用 HalconCanvas 的 RenderEntities() 重新渲染
        /// 用于采样点数变更后确保画布可靠刷新
        /// </summary>
        private void OnCanvasRefreshRequested()
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] OnCanvasRefreshRequested called");
            if (halconCanvas == null)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] halconCanvas is NULL, cannot render");
                return;
            }
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Calling halconCanvas.RenderEntities()");
            halconCanvas.RenderEntities();
        }

        /// <summary>
        /// ViewModel 属性变化回调——将 ROI 激活状态同步到 HalconCanvas.DrawMode
        /// </summary>
        private void OnViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_viewModel == null || halconCanvas == null) return;

            switch (e.PropertyName)
            {
                case nameof(_viewModel.IsLineRoiActive):
                    if (_viewModel.IsLineRoiActive)
                        halconCanvas.StartRoiDrawing(RoiDrawMode.Line);
                    else if (halconCanvas.DrawMode == RoiDrawMode.Line)
                        halconCanvas.DrawMode = RoiDrawMode.None;
                    break;
                case nameof(_viewModel.IsPolylineRoiActive):
                    if (_viewModel.IsPolylineRoiActive)
                        halconCanvas.StartRoiDrawing(RoiDrawMode.Polyline);
                    else if (halconCanvas.DrawMode == RoiDrawMode.Polyline)
                        halconCanvas.DrawMode = RoiDrawMode.None;
                    break;
                case nameof(_viewModel.IsArcRoiActive):
                    if (_viewModel.IsArcRoiActive)
                        halconCanvas.StartRoiDrawing(RoiDrawMode.CircularArc);
                    else if (halconCanvas.DrawMode == RoiDrawMode.CircularArc)
                        halconCanvas.DrawMode = RoiDrawMode.None;
                    break;
            }
        }

        /// <summary>
        /// HalconCanvas ROI 绘制完成回调——从 ROIController 提取几何参数，转换为 RoiRegion 设置到 ViewModel
        /// </summary>
        private void OnCanvasRoiCompleted(HalconDotNet.HObject roiObj)
        {
            if (_viewModel == null || halconCanvas == null) return;

            try
            {
                // Polyline 模式不使用 ROIController，_roiDict 为空，
                // 直接从 roiObj (XLD) 提取顶点，无需查询 roiDict
                if (halconCanvas.DrawMode == RoiDrawMode.Polyline)
                {
                    if (roiObj == null || !roiObj.IsInitialized()) return;

                    HOperatorSet.GetContourXld(roiObj, out HTuple rows, out HTuple cols);
                    var vertices = new List<Core.Models.PointF>();
                    for (int i = 0; i < rows.Length; i++)
                    {
                        var cadPt = ImageToCad(rows[i].D, cols[i].D);
                        vertices.Add(new Core.Models.PointF((float)cadPt.cadX, (float)cadPt.cadY));
                    }

                    _viewModel.CurrentRoiPreview = new RoiRegion(RoiType.Polyline)
                    {
                        PolylineVertices = vertices
                    };
                    return;
                }

                // Line / CircularArc 模式从 ROIController 提取几何参数
                var roiDict = halconCanvas.GetRoiDict();
                var roi = roiDict.Values.LastOrDefault();
                if (roi == null) return;

                RoiRegion roiRegion = null;

                switch (halconCanvas.DrawMode)
                {
                    case RoiDrawMode.Line:
                        if (roi is HalconWrapper.Model.ROILine lineRoi)
                        {
                            roiRegion = new RoiRegion(RoiType.Line);
                            var startCad = ImageToCad(lineRoi.StartY, lineRoi.StartX);
                            var endCad = ImageToCad(lineRoi.EndY, lineRoi.EndX);
                            roiRegion.LineStartPoint = new Core.Models.PointF((float)startCad.cadX, (float)startCad.cadY);
                            roiRegion.LineEndPoint = new Core.Models.PointF((float)endCad.cadX, (float)endCad.cadY);
                        }
                        break;

                    case RoiDrawMode.CircularArc:
                        if (roi is HalconWrapper.Model.ROICircularArc arcRoi)
                        {
                            roiRegion = new RoiRegion(RoiType.Arc);
                            var centerCad = ImageToCad(arcRoi.midR, arcRoi.midC);
                            roiRegion.ArcCenter = new Core.Models.PointF((float)centerCad.cadX, (float)centerCad.cadY);
                            roiRegion.ArcRadius = arcRoi.radius;
                            roiRegion.ArcStartAngle = arcRoi.startPhi * 180.0 / Math.PI;
                            roiRegion.ArcEndAngle = (arcRoi.startPhi + arcRoi.extentPhi) * 180.0 / Math.PI;
                        }
                        break;
                }

                if (roiRegion != null)
                {
                    _viewModel.CurrentRoiPreview = roiRegion;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CadPointEditor] OnCanvasRoiCompleted 异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 图像坐标 → CAD 坐标转换（委托给 HalconCanvasControl.ImageToCad）
        /// </summary>
        private (double cadX, double cadY) ImageToCad(double row, double col)
        {
            if (halconCanvas != null)
                return halconCanvas.ImageToCad(row, col);
            return (col, -row);
        }

        #endregion

        #region 事件处理器

        /// <summary>DP CurrentStep 变更时的回调——通知 ViewModel 切换右侧面板模板</summary>
        private static void OnCurrentStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (CadPointEditorControl)d;
            ctrl._viewModel?.OnStepChanged((int)e.NewValue);
        }

        /// <summary>
        /// HalconCanvas 坐标变化事件转发——更新 ViewModel 坐标显示字符串（鼠标移动时持续触发）
        /// </summary>
        private void OnCanvasCoordinateChanged(double cadX, double cadY)
        {
            _viewModel?.UpdateCoordinateDisplay(cadX, cadY);
        }

        /// <summary>
        /// HalconCanvas 画布点击事件转发——更新 ViewModel 的最后点击坐标缓存
        /// 仅在鼠标点击画布时触发，避免鼠标移开后坐标丢失
        /// </summary>
        private void OnCanvasPointClicked(double cadX, double cadY)
        {
            _viewModel?.OnCanvasPointClicked(cadX, cadY);
        }

        /// <summary>
        /// HalconCanvas 图元选中事件转发——同步到 ViewModel
        /// </summary>
        private void OnCanvasEntitySelected(CadEntity entity)
        {
            _viewModel?.OnEntitySelected(entity);
        }

        /// <summary>
        /// HalconCanvas 图元双击事件转发——ViewModel 可打开编辑界面或高亮对应 Segment
        /// </summary>
        private void OnCanvasEntityDoubleClicked(CadEntity entity)
        {
            _viewModel?.OnEntityDoubleClicked(entity);
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 触发执行请求路由事件——供外部宿主调用或内部命令使用
        /// 将 Segments 集合作为参数传递给事件处理者
        /// </summary>
        public void RaiseExecuteRequest()
        {
            var args = new RoutedEventArgs(ExecuteRequestEvent, this);
            RaiseEvent(args);
        }

        /// <summary>
        /// 重置控件到初始状态（清除数据、回到 Step 1）
        /// </summary>
        public void Reset()
        {
            _viewModel?.ResetAll();
        }

        /// <summary>
        /// 从外部直接设置 Segments 数据（用于从配方/配置恢复场景）
        /// </summary>
        /// <param name="segments">要加载的轨迹段集合</param>
        public void LoadSegments(ObservableCollection<DispenseSegment> segments)
        {
            _viewModel?.LoadSegments(segments);
        }

        #endregion
    }
}
#endif
