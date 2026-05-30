using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using Core.Models;
using HalconWrapper;
using HalconDotNet;
using ToolStripItem = System.Windows.Forms.ToolStripItem;
using ToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace Module.Controls
{
    /// <summary>
    /// ROI 绘制模式枚举——定义点胶机所需的 7 种 ROI 交互模式
    /// 包含几何形状绘制（Region/XLD）和涂抹/擦除模式
    /// </summary>
    public enum RoiDrawMode
    {
        None,           // 无绘制模式，正常浏览/选中图元
        Rectangle2,     // 旋转矩形（Region 类型）
        Circle,         // 圆形（Region 类型）
        Line,           // 线段（XLD 类型）
        Polyline,       // 折线（XLD 类型，点击添加顶点，右键/双击结束）
        CircularArc,    // 圆弧（XLD 类型）
        Paint,          // 涂抹模式（Region 类型，按住左键拖动）
        Eraser          // 擦除模式（Region 类型，按住左键拖动）
    }

    /// <summary>
    /// 基于 Halcon VMHWindowControl 的 CAD 图元画布控件
    /// 使用 WindowsFormsHost 嵌入 WinForms 的 VMHWindowControl 实现图元渲染
    /// 支持渲染 CadEntity 集合、缩放/平移交互、图元选中和 7 种 ROI 交互模式
    /// </summary>
    public partial class HalconCanvasControl : UserControl, IDisposable
    {
        #region 依赖属性定义

        // 要渲染的 CAD 图元集合
        public static readonly DependencyProperty EntitiesProperty =
            DependencyProperty.Register(nameof(Entities), typeof(ObservableCollection<CadEntity>),
                typeof(HalconCanvasControl), new PropertyMetadata(null, OnEntitiesChanged));

        // 当前选中的图元
        public static readonly DependencyProperty SelectedEntityProperty =
            DependencyProperty.Register(nameof(SelectedEntity), typeof(CadEntity),
                typeof(HalconCanvasControl), new PropertyMetadata(null, OnVisualPropertyChanged));

        // 缩放比例（仅用于状态栏显示，不再控制视口渲染）
        public static readonly DependencyProperty ZoomFactorProperty =
            DependencyProperty.Register(nameof(ZoomFactor), typeof(double),
                typeof(HalconCanvasControl), new PropertyMetadata(1.0));

        // X 平移偏移（仅用于状态栏显示，不再控制视口渲染）
        public static readonly DependencyProperty PanOffsetXProperty =
            DependencyProperty.Register(nameof(PanOffsetX), typeof(double),
                typeof(HalconCanvasControl), new PropertyMetadata(0.0));

        // Y 平移偏移（仅用于状态栏显示，不再控制视口渲染）
        public static readonly DependencyProperty PanOffsetYProperty =
            DependencyProperty.Register(nameof(PanOffsetY), typeof(double),
                typeof(HalconCanvasControl), new PropertyMetadata(0.0));

        // 是否显示网格（暂未实现，不触发重绘）
        public static readonly DependencyProperty ShowGridProperty =
            DependencyProperty.Register(nameof(ShowGrid), typeof(bool),
                typeof(HalconCanvasControl), new PropertyMetadata(false));

        // 当前正在绘制的 ROI 预览
        public static readonly DependencyProperty CurrentRoiPreviewProperty =
            DependencyProperty.Register(nameof(CurrentRoiPreview), typeof(RoiRegion),
                typeof(HalconCanvasControl), new PropertyMetadata(null, OnVisualPropertyChanged));

        // 当前 ROI 绘制模式（7 种交互模式之一）
        public static readonly DependencyProperty DrawModeProperty =
            DependencyProperty.Register(nameof(DrawMode), typeof(RoiDrawMode),
                typeof(HalconCanvasControl), new PropertyMetadata(RoiDrawMode.None, OnDrawModeChanged));

        // 涂抹/擦除笔刷大小（像素）
        public static readonly DependencyProperty BrushSizeProperty =
            DependencyProperty.Register(nameof(BrushSize), typeof(int),
                typeof(HalconCanvasControl), new PropertyMetadata(5));

        // 是否正在绘制中（只读，由内部状态驱动）
        private static readonly DependencyPropertyKey IsDrawingPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(IsDrawing), typeof(bool),
                typeof(HalconCanvasControl), new PropertyMetadata(false));
        public static readonly DependencyProperty IsDrawingProperty = IsDrawingPropertyKey.DependencyProperty;

        // 当前正在绘制的 ROI 区域结果（只读）
        private static readonly DependencyPropertyKey ActiveRoiRegionPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(ActiveRoiRegion), typeof(HObject),
                typeof(HalconCanvasControl), new PropertyMetadata(null));
        public static readonly DependencyProperty ActiveRoiRegionProperty = ActiveRoiRegionPropertyKey.DependencyProperty;

        // 选中轨迹段的采样点集合——用于在图形上显示 X 标记
        public static readonly DependencyProperty SelectedSegmentPointsProperty =
            DependencyProperty.Register(nameof(SelectedSegmentPoints), typeof(List<CadPoint>),
                typeof(HalconCanvasControl), new PropertyMetadata(null, OnSelectedSegmentPointsChanged));

        // 选中点位的索引——用于在图形上高亮显示选中的点
        public static readonly DependencyProperty SelectedPointIndexProperty =
            DependencyProperty.Register(nameof(SelectedPointIndex), typeof(int),
                typeof(HalconCanvasControl), new PropertyMetadata(-1, OnSelectedSegmentPointsChanged));

        #endregion

        #region CLR 属性访问器

        /// <summary>要渲染的 CAD 图元集合</summary>
        public ObservableCollection<CadEntity> Entities
        {
            get => (ObservableCollection<CadEntity>)GetValue(EntitiesProperty);
            set => SetValue(EntitiesProperty, value);
        }

        /// <summary>当前选中的图元</summary>
        public CadEntity SelectedEntity
        {
            get => (CadEntity)GetValue(SelectedEntityProperty);
            set => SetValue(SelectedEntityProperty, value);
        }

        /// <summary>缩放比例</summary>
        public double ZoomFactor
        {
            get => (double)GetValue(ZoomFactorProperty);
            set => SetValue(ZoomFactorProperty, value);
        }

        /// <summary>X 平移偏移（像素）</summary>
        public double PanOffsetX
        {
            get => (double)GetValue(PanOffsetXProperty);
            set => SetValue(PanOffsetXProperty, value);
        }

        /// <summary>Y 平移偏移（像素）</summary>
        public double PanOffsetY
        {
            get => (double)GetValue(PanOffsetYProperty);
            set => SetValue(PanOffsetYProperty, value);
        }

        /// <summary>是否显示网格</summary>
        public bool ShowGrid
        {
            get => (bool)GetValue(ShowGridProperty);
            set => SetValue(ShowGridProperty, value);
        }

        /// <summary>当前正在绘制的 ROI 预览</summary>
        public RoiRegion CurrentRoiPreview
        {
            get => (RoiRegion)GetValue(CurrentRoiPreviewProperty);
            set => SetValue(CurrentRoiPreviewProperty, value);
        }

        /// <summary>当前 ROI 绘制模式，默认 None（正常浏览模式）</summary>
        public RoiDrawMode DrawMode
        {
            get => (RoiDrawMode)GetValue(DrawModeProperty);
            set => SetValue(DrawModeProperty, value);
        }

        /// <summary>涂抹/擦除笔刷大小（像素），默认 5</summary>
        public int BrushSize
        {
            get => (int)GetValue(BrushSizeProperty);
            set => SetValue(BrushSizeProperty, value);
        }

        /// <summary>是否正在绘制中（只读属性）</summary>
        public bool IsDrawing
        {
            get => (bool)GetValue(IsDrawingProperty);
            private set => SetValue(IsDrawingPropertyKey, value);
        }

        /// <summary>当前正在绘制的 ROI 区域结果（只读属性），可为 HRegion 或 HXLDCont</summary>
        public HObject ActiveRoiRegion
        {
            get => (HObject)GetValue(ActiveRoiRegionProperty);
            private set => SetValue(ActiveRoiRegionPropertyKey, value);
        }

        /// <summary>选中轨迹段的采样点集合——在图形上用 X 标记显示</summary>
        public List<CadPoint> SelectedSegmentPoints
        {
            get => (List<CadPoint>)GetValue(SelectedSegmentPointsProperty);
            set => SetValue(SelectedSegmentPointsProperty, value);
        }

        /// <summary>选中点位的索引——用于在图形上高亮显示选中的点</summary>
        public int SelectedPointIndex
        {
            get => (int)GetValue(SelectedPointIndexProperty);
            set => SetValue(SelectedPointIndexProperty, value);
        }

        /// <summary>
        /// 开始批量更新——暂停集合变更触发的自动渲染，避免逐个 Add 导致多次重绘闪烁
        /// 必须与 EndBatchUpdate 配对使用
        /// </summary>
        public void BeginBatchUpdate()
        {
            _suppressRender = true;
        }

        /// <summary>
        /// 结束批量更新——恢复自动渲染，并执行一次完整渲染和视口适配
        /// </summary>
        public void EndBatchUpdate()
        {
            _suppressRender = false;
            RenderEntities();
            FitToAll();
        }

        #endregion

        #region CLR 事件

        /// <summary>
        /// 实时坐标回调事件，参数为 (cadX, cadY) 的 CAD 坐标值
        /// 在鼠标移动时持续触发，用于状态栏坐标显示等场景
        /// </summary>
        public event Action<double, double> CoordinateChanged;

        /// <summary>
        /// 画布点击事件，参数为 (cadX, cadY) 的 CAD 坐标值
        /// 仅在鼠标点击画布时触发（非拖拽），用于"从画布选取"等需要精确坐标的场景
        /// </summary>
        public event Action<double, double> CanvasPointClicked;

        /// <summary>
        /// 图元选中事件，在鼠标左键释放完成命中测试后触发
        /// 参数为被选中的 CadEntity 实例（可能为 null 表示取消选中）
        /// </summary>
        public event Action<CadEntity> EntitySelected;

        /// <summary>
        /// 图元双击编辑事件，在鼠标双击完成命中测试后触发
        /// 参数为被双击的 CadEntity 实例
        /// </summary>
        public event Action<CadEntity> EntityDoubleClicked;

        /// <summary>
        /// ROI 绘制完成事件——当 ROI 绘制完成时触发（双击/右键结束绘制时）
        /// 参数为 HObject：Region 类型（Rectangle2/Circle/Paint/Eraser）返回 HRegion，
        /// XLD 类型（Line/Polyline/CircularArc）返回 HXLDCont
        /// </summary>
        public event Action<HObject> RoiCompleted;

        #endregion

        #region 私有字段

        // 嵌入的 Halcon 窗口控件
        private VMHWindowControl _halconControl;

        // 白色背景画布图像（Halcon 窗口必须有图像才能绘图）
        private HImage _canvasImage;

        // 坐标变换偏移量——将 CAD 坐标映射到图像坐标
        // col = cadX - _offsetX, row = -cadY + _offsetY
        // 这样 CAD 数据从图像左上角开始排列，背景图像尺寸匹配数据范围
        private double _offsetX = 0;
        private double _offsetY = 0;
        private int _imgWidth = 100;
        private int _imgHeight = 100;

        // 是否已执行过 Dispose
        private bool _disposed = false;

        // 批量更新期间暂停渲染，避免逐个 Add 触发多次重绘导致闪烁
        private bool _suppressRender = false;

        // ✅ 性能优化：坐标变换结果缓存
        // Key: 实体HashCode, Value: 已转换的图像坐标系XLD对象
        // 避免缩放/平移时重复执行TransformCadToImage计算
        private readonly System.Collections.Generic.Dictionary<int, HObject> _transformCache = new();
        private bool _cacheValid = false; // 缓存是否有效（偏移量变化时失效）

        // 命中检测的容差阈值（CAD 单位）
        private const double HitToleranceCad = 5.0;

        /// <summary>
        /// 获取当前 Halcon 窗口的缩放因子（基于视口范围与窗口尺寸的比值）
        /// 用于命中检测容差计算和 Paint/Eraser 笔刷大小调整
        /// </summary>
        private double GetCurrentZoomFactor()
        {
            if (_halconControl == null || _disposed) return 1.0;
            var hWindow = _halconControl.mCtrl_HWindow?.HalconWindow;
            if (hWindow == null) return 1.0;

            try
            {
                HOperatorSet.GetPart(hWindow, out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);
                double viewportWidth = col2.D - col1.D;
                if (viewportWidth <= 0) return 1.0;
                double windowWidth = ActualWidth > 0 ? ActualWidth : 800;
                return windowWidth / viewportWidth;
            }
            catch
            {
                return 1.0;
            }
        }

        // ========== ROI 绘制模式相关字段 ==========

        // 外部维护的 ROI 字典（与 ROIController.ROIList 同步）
        private Dictionary<string, HalconWrapper.Model.ROI> _roiDict = new();

        /// <summary>
        /// 获取当前 ROI 字典（供外部读取 ROI 几何参数）
        /// </summary>
        public Dictionary<string, HalconWrapper.Model.ROI> GetRoiDict() => _roiDict;

        // ROI 绘制起始点（图像坐标）
        private double _roiStartRow, _roiStartCol;

        // ROI 绘制是否已按下鼠标
        private bool _roiMouseDown;

        // 折线模式的顶点列表（图像坐标）
        private List<System.Windows.Point> _polylineVertices;

        // 涂抹/擦除模式的累积区域
        private HRegion _paintMaskRegion;

        // 涂抹/擦除模式的笔刷预览
        private HRegion _brushPreviewRegion;

        #endregion

        #region 构造函数与初始化

        /// <summary>
        /// 无参构造函数，初始化控件、创建 VMHWindowControl 实例并注册事件
        /// 设计时跳过 Halcon 初始化，避免设计器加载原生 halcon.dll 失败
        /// </summary>
        public HalconCanvasControl()
        {
            InitializeComponent();

            // 设计时跳过 Halcon 初始化：设计器进程无法加载原生 halcon.dll，
            // 直接创建 VMHWindowControl 会触发 HLICreateProcedure 入口点找不到的异常
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            {
                _designPlaceholder.Visibility = Visibility.Visible;
                _winFormHost.Visibility = Visibility.Collapsed;
                return;
            }

            InitHalconControl();
            RegisterMouseEvents();
        }

        /// <summary>
        /// 初始化 Halcon 控件：创建 VMHWindowControl 并嵌入 WindowsFormsHost，
        /// 生成白色背景图像作为画布底图
        /// </summary>
        private void InitHalconControl()
        {
            _halconControl = new VMHWindowControl();
            _winFormHost.Child = _halconControl;

            // 不设置 DrawModel=true，保留 VMHWindowControl 内置的鼠标交互
            // （右键菜单、滚轮缩放、拖拽平移等），这是 Halcon 查看图形的标准方式

            CreateWhiteCanvasImage(100, 100);
            _halconControl.Image = _canvasImage;

            // 隐藏 VMHWindowControl 自带的状态栏（显示图像坐标/灰度值）
            // 避免图像坐标与 WPF 状态栏的 CAD 坐标混淆
            _halconControl.hideStatusBar();

            // 覆盖右键菜单"适应图片"的默认行为
            // 默认行为 DispImageFitImage() 基于背景图像尺寸（2000x1500）重置视口，导致视口过大
            // 替换为调用 FitToAll()，基于实际数据包围盒设置正确视口
            OverrideFitImageMenuItem();

            // 注册 Halcon 原生鼠标事件（WinForms 级别，不经过 WPF 路由）
            // WPF 鼠标事件在 WindowsFormsHost 内无法触发，必须使用 Halcon 事件
            if (_halconControl.mCtrl_HWindow != null)
            {
                _halconControl.mCtrl_HWindow.HMouseMove += OnHWindowMouseMove;
                _halconControl.mCtrl_HWindow.HMouseDown += OnHWindowMouseDown;
                _halconControl.mCtrl_HWindow.HMouseUp += OnHWindowMouseUp;
            }
        }

        /// <summary>
        /// 覆盖 VMHWindowControl 右键菜单中"适应图片"的默认行为
        /// 默认的 DispImageFitImage() 基于背景图像尺寸（2000x1500）重置视口，
        /// 导致视口范围远大于实际数据，图形变得极小甚至不可见
        /// 替换为调用 FitToAll()，基于实际数据包围盒计算正确的 SetPart 视口
        /// </summary>
        private void OverrideFitImageMenuItem()
        {
            try
            {
                var contextMenu = _halconControl.mCtrl_HWindow?.ContextMenuStrip;
                if (contextMenu == null) return;

                foreach (ToolStripItem item in contextMenu.Items)
                {
                    var fitText = TryFindResource("HalconCanvas_Menu_FitImage") as string ?? "Fit Image";
                    if (item.Text == fitText)
                    {
                        // 移除所有默认点击事件处理器
                        item.Click -= null; // 无法直接移除匿名委托
                        // 禁用原菜单项，添加新的菜单项替代
                        item.Enabled = false;
                        item.Visible = false;
                        break;
                    }
                }

                // 添加自定义的"适应图片"菜单项
                var fitMenuItemText = TryFindResource("HalconCanvas_Menu_FitImage") as string ?? "Fit Image";
                var customFitItem = new ToolStripMenuItem(fitMenuItemText);
                customFitItem.Click += (s, e) => FitToAll();
                contextMenu.Items.Insert(0, customFitItem);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HalconCanvas] OverrideFitImageMenuItem 异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建白色背景的 HImage 作为画布底图
        /// Halcon 窗口必须有图像才能在上面绘制 XLD 轮廓
        /// </summary>
        private void CreateWhiteCanvasImage(int width, int height)
        {
            if (_canvasImage != null && _canvasImage.IsInitialized())
                _canvasImage.Dispose();

            _canvasImage = new HImage();
            _canvasImage.GenImageConst("byte", width, height);
            HObject fullRegion;
            HOperatorSet.GenRectangle1(out fullRegion, 0, 0, height - 1, width - 1);
            HObject paintedImage;
            HOperatorSet.PaintRegion(fullRegion, _canvasImage, out paintedImage, 255, "fill");
            _canvasImage.Dispose();
            _canvasImage = new HImage(paintedImage);
            fullRegion.Dispose();
            paintedImage.Dispose();

            _imgWidth = width;
            _imgHeight = height;
        }

        /// <summary>
        /// 将 CAD 坐标系的 XLD 轮廓变换到图像坐标系（带缓存优化）
        /// 优先从缓存读取，避免重复的坐标变换计算
        /// </summary>
        private HObject TransformCadToImageWithCache(CadEntity entity, HObject cadXld)
        {
            if (cadXld == null || !cadXld.IsInitialized())
                return new HObject();

            try
            {
                // 检查缓存是否有效（偏移量未变化）
                if (!_cacheValid)
                {
                    InvalidateTransformCache();
                    _cacheValid = true;
                }

                // 尝试从缓存获取
                int hash = entity.GetHashCode();
                if (_transformCache.TryGetValue(hash, out var cached))
                {
                    return cached.Clone(); // 返回副本，避免原始对象被Dispose影响
                }

                // 缓存未命中，执行实际变换
                var result = TransformCadToImage(cadXld);

                // 存入缓存
                _transformCache[hash] = result.Clone();

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HalconCanvas] TransformCadToImageWithCache 异常: {ex.Message}");
                return new HObject();
            }
        }

        /// <summary>
        /// 使坐标变换缓存失效（偏移量或视口变化时调用）
        /// </summary>
        private void InvalidateTransformCache()
        {
            // 释放所有缓存的HObject
            foreach (var kvp in _transformCache)
            {
                try { kvp.Value?.Dispose(); } catch { }
            }
            _transformCache.Clear();
            _cacheValid = false;
        }

        /// <summary>
        /// 将 CAD 坐标系的 XLD 轮廓变换到图像坐标系的底层实现
        /// 逐点读取 XLD 坐标，用 CadToImage 公式转换后重新生成 XLD
        /// 变换公式：col = cadX - _offsetX, row = -cadY + _offsetY
        /// （CAD Y轴向上 → Halcon Row轴向下需要翻转）
        /// </summary>
        private HObject TransformCadToImage(HObject xld)
        {
            if (xld == null || !xld.IsInitialized())
                return new HObject();

            try
            {
                HObject result = new HObject();
                result.GenEmptyObj();

                int countObj = xld.CountObj();
                for (int i = 1; i <= countObj; i++)
                {
                    HObject singleContour = xld.SelectObj(i);
                    HTuple rows, cols;
                    HOperatorSet.GetContourXld(singleContour, out rows, out cols);

                    int numPoints = rows.Length;
                    double[] newRows = new double[numPoints];
                    double[] newCols = new double[numPoints];

                    for (int j = 0; j < numPoints; j++)
                    {
                        double cadY = rows[j].D;
                        double cadX = cols[j].D;
                        var img = CadToImage(cadX, cadY);
                        newRows[j] = img.row;
                        newCols[j] = img.col;
                    }

                    HObject transformed;
                    HOperatorSet.GenContourPolygonXld(out transformed, newRows, newCols);
                    result = result.ConcatObj(transformed);
                    transformed.Dispose();
                    singleContour.Dispose();
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HalconCanvas] TransformCadToImage 异常: {ex.Message}");
                return new HObject();
            }
        }

        /// <summary>
        /// 注册 WPF 层面的鼠标交互事件
        /// 包含浏览模式和 ROI 绘制模式所需的所有事件
        /// </summary>
        private void RegisterMouseEvents()
        {
            // 鼠标交互已改用 Halcon 原生 HMouseDown/HMouseUp/HMouseMove 事件
            // 保留 WPF 双击事件（WindowsFormsHost 会转发双击）
            MouseDoubleClick += OnMouseDoubleClick;
        }

        #endregion

        #region 依赖属性变更回调

        /// <summary>
        /// Entities 集合变更时的回调——重新订阅 CollectionChanged 事件并触发重绘
        /// </summary>
        private static void OnEntitiesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (HalconCanvasControl)d;
            System.Diagnostics.Debug.WriteLine($"[HalconCanvas] OnEntitiesChanged: OldCount={((System.Collections.IList)e.OldValue)?.Count ?? 0}, NewCount={((System.Collections.IList)e.NewValue)?.Count ?? 0}");

            if (e.OldValue is ObservableCollection<CadEntity> oldCollection)
            {
                oldCollection.CollectionChanged -= ctrl.OnEntityCollectionChanged;
                foreach (var entity in oldCollection)
                    entity.PropertyChanged -= ctrl.OnEntityPropertyChanged;
            }
            if (e.NewValue is ObservableCollection<CadEntity> newCollection)
            {
                newCollection.CollectionChanged += ctrl.OnEntityCollectionChanged;
                foreach (var entity in newCollection)
                    entity.PropertyChanged += ctrl.OnEntityPropertyChanged;
            }
            ctrl.RenderEntities();
            // 实体集合替换后自动适配视口（解决 DXF 导入坐标范围超出画布的问题）
            ctrl.FitToAll();
        }

        /// <summary>
        /// 单个图元属性变更时重绘（如 IsVisible 变更）
        /// </summary>
        private void OnEntityPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!_suppressRender)
                RenderEntities();
        }

        /// <summary>
        /// 视觉相关属性（选中/ROI）变更时的回调——触发重绘
        /// </summary>
        private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (HalconCanvasControl)d;
            if (!ctrl._disposed && !ctrl._suppressRender)
                ctrl.RenderEntities();
        }

        /// <summary>
        /// SelectedSegmentPoints 变更回调——触发重绘以更新点位 X 标记
        /// </summary>
        private static void OnSelectedSegmentPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (HalconCanvasControl)d;
            if (!ctrl._disposed && !ctrl._suppressRender)
                ctrl.RenderEntities();
        }

        /// <summary>
        /// DrawMode 属性变更时的回调——切换 ROI 绘制模式，清理旧状态并初始化新模式
        /// </summary>
        private static void OnDrawModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (HalconCanvasControl)d;
            RoiDrawMode newMode = (RoiDrawMode)e.NewValue;
            ctrl.OnDrawModeChangedInternal(newMode);
        }

        /// <summary>
        /// Entities 集合内容增删时的处理——触发重新渲染
        /// </summary>
        private void OnEntityCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[HalconCanvas] OnEntityCollectionChanged: Action={e.Action}, NewItems={e.NewItems?.Count}, OldItems={e.OldItems?.Count}, Entities.Count={Entities?.Count}");

            if (e.OldItems != null)
                foreach (var item in e.OldItems)
                    if (item is CadEntity entity)
                        entity.PropertyChanged -= OnEntityPropertyChanged;

            if (e.NewItems != null)
                foreach (var item in e.NewItems)
                    if (item is CadEntity entity)
                        entity.PropertyChanged += OnEntityPropertyChanged;

            if (!_suppressRender)
            {
                RenderEntities();

                if (e.Action == NotifyCollectionChangedAction.Add ||
                    e.Action == NotifyCollectionChangedAction.Reset ||
                    e.Action == NotifyCollectionChangedAction.Replace)
                {
                    FitToAll();
                }
            }
        }

        #endregion

        #region 核心渲染方法

        /// <summary>
        /// 核心渲染方法：清空 HWindow 后重新渲染所有可见图元
        /// 使用 VMHWindowControl.DispObj() 存储 XLD 对象，ViewWindow 自动管理缩放/平移/重绘
        /// 设计时直接返回，不执行任何 Halcon 操作
        /// </summary>
        public void RenderEntities()
        {
            if (_halconControl == null || _disposed)
                return;

            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                return;

            try
            {
                var hWindow = _halconControl.mCtrl_HWindow?.HalconWindow;
                if (hWindow == null)
                    return;

                _halconControl.ClearROI();

                int renderedCount = 0;
                if (Entities != null)
                {
                    foreach (var entity in Entities)
                    {
                        if (!entity.IsVisible)
                            continue;

                        bool isSelected = (entity == SelectedEntity);
                        string color = GetLayerColor(entity);
                        if (isSelected)
                            color = "#FFD700";

                        try
                        {
                            HObject hObj = entity.ToHObject();

                            if (hObj != null && hObj.IsInitialized())
                            {
                                HObject imgObj = TransformCadToImageWithCache(entity, hObj);
                                hObj.Dispose();

                                if (imgObj != null && imgObj.IsInitialized())
                                {
                                    hWindow.SetDraw("margin");
                                    hWindow.SetColor(color);
                                    hWindow.SetLineWidth(isSelected ? 3.0 : 1.5);

                                    _halconControl.DispObj(imgObj, color);

                                    imgObj.Dispose();
                                    renderedCount++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[HalconCanvas] 渲染图元异常: Type={entity.EntityType}, {ex.Message}");
                        }
                    }
                }

                if (CurrentRoiPreview != null)
                {
                    if (hWindow != null)
                        RenderRoiPreview(hWindow);
                }

                RenderPointMarkers();

                _halconControl.WindowH._hWndControl.Repaint();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HalconCanvas] RenderEntities 异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[HalconCanvas] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 渲染选中轨迹段的采样点 X 标记
        /// 使用 DispObj() 而非 DispCross() 直接绘制，确保 ClearROI() 能正确清除旧标记
        /// 选中点位用绿色大十字高亮，普通点位用红色小十字
        /// </summary>
        private void RenderPointMarkers()
        {
            if (_halconControl == null || _disposed || SelectedSegmentPoints == null)
                return;

            try
            {
                var hWindow = _halconControl.mCtrl_HWindow?.HalconWindow;
                if (hWindow == null)
                    return;

                double zoomFactor = GetCurrentZoomFactor();
                double crossSize = Math.Clamp(4.0 / zoomFactor, 1.5, 8.0);
                double selectedCrossSize = crossSize * 1.5;
                int selectedIdx = SelectedPointIndex;

                int renderedPoints = 0;
                for (int i = 0; i < SelectedSegmentPoints.Count; i++)
                {
                    var pt = SelectedSegmentPoints[i];
                    var imgCoord = CadToImage(pt.X, pt.Y);
                    double size = (i == selectedIdx) ? selectedCrossSize : crossSize;
                    string color = (i == selectedIdx) ? "#00C853" : "red";

                    // 生成 X 形标记的 XLD 轮廓（两条对角线）
                    // 对角线1: 左上 → 右下, 对角线2: 右上 → 左下
                    HObject cross;
                    double r = imgCoord.row, c = imgCoord.col;
                    double[] rows1 = { r - size, r + size };
                    double[] cols1 = { c - size, c + size };
                    double[] rows2 = { r - size, r + size };
                    double[] cols2 = { c + size, c - size };
                    HOperatorSet.GenContourPolygonXld(out HObject line1, rows1, cols1);
                    HOperatorSet.GenContourPolygonXld(out HObject line2, rows2, cols2);
                    cross = line1.ConcatObj(line2);
                    line1.Dispose();
                    line2.Dispose();

                    hWindow.SetLineWidth(i == selectedIdx ? 2.5 : 1.0);
                    hWindow.SetColor(color);
                    hWindow.DispObj(cross);
                    // 同时添加到 hObjectList 以支持缩放/平移时的自动重绘
                    _halconControl.DispObj(cross, color);
                    cross.Dispose();
                    renderedPoints++;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HalconCanvas] RenderPointMarkers 异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[HalconCanvas] RenderPointMarkers Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 根据图层名称返回对应的 Halcon 颜色字符串
        /// BASE_FRAME → 灰色, DISPENSE_GLUE → 蓝色, 其他 → 黑色
        /// </summary>
        private string GetLayerColor(CadEntity entity)
        {
            return entity.LayerName.ToUpperInvariant() switch
            {
                "BASE_FRAME" => "#808080",       // 灰色
                _ => "#2196F3"                   // 默认蓝色
            };
        }

        /// <summary>
        /// 在 HWindow 上渲染 ROI 预览区域（绿色虚线效果）
        /// 根据 RoiRegion.Type 分别处理直线/折线/圆弧/手绘形态
        /// </summary>
        private void RenderRoiPreview(HWindow hWindow)
        {
            if (CurrentRoiPreview == null || hWindow == null)
                return;

            try
            {
                // 设置 ROI 预览样式：绿色、较粗线宽
                hWindow.SetColor("#00FF00");
                hWindow.SetLineWidth(2.0);

                HObject roiObj = null;

                switch (CurrentRoiPreview.Type)
                {
                    case RoiType.Line:
                        // 直线 ROI：连接起点和终点
                        if (CurrentRoiPreview.LineStartPoint != null && CurrentRoiPreview.LineEndPoint != null)
                        {
                            double[] rows = { CurrentRoiPreview.LineStartPoint.Y, CurrentRoiPreview.LineEndPoint.Y };
                            double[] cols = { CurrentRoiPreview.LineStartPoint.X, CurrentRoiPreview.LineEndPoint.X };
                            HOperatorSet.GenContourPolygonXld(out roiObj, rows, cols);
                        }
                        break;

                    case RoiType.Polyline:
                        // 折线 ROI：遍历顶点生成多段线轮廓
                        if (CurrentRoiPreview.PolylineVertices != null && CurrentRoiPreview.PolylineVertices.Count >= 2)
                        {
                            int count = CurrentRoiPreview.PolylineVertices.Count;
                            double[] rows = new double[count];
                            double[] cols = new double[count];
                            for (int i = 0; i < count; i++)
                            {
                                rows[i] = CurrentRoiPreview.PolylineVertices[i].Y;
                                cols[i] = CurrentRoiPreview.PolylineVertices[i].X;
                            }
                            HOperatorSet.GenContourPolygonXld(out roiObj, rows, cols);
                        }
                        break;

                    case RoiType.Arc:
                        // 圆弧 ROI：采样圆弧上的点生成轮廓
                        if (CurrentRoiPreview.ArcRadius > 0 && CurrentRoiPreview.ArcCenter != null)
                        {
                            double startRad = CurrentRoiPreview.ArcStartAngle * Math.PI / 180.0;
                            double endRad = CurrentRoiPreview.ArcEndAngle * Math.PI / 180.0;
                            double sweep = endRad - startRad;
                            if (sweep <= 0) sweep += 2 * Math.PI;

                            int sampleCount = 36;
                            List<double> rowList = new List<double>();
                            List<double> colList = new List<double>();
                            for (int i = 0; i <= sampleCount; i++)
                            {
                                double t = (double)i / sampleCount;
                                double angle = startRad + sweep * t;
                                rowList.Add(CurrentRoiPreview.ArcCenter.Y + CurrentRoiPreview.ArcRadius * Math.Sin(angle));
                                colList.Add(CurrentRoiPreview.ArcCenter.X + CurrentRoiPreview.ArcRadius * Math.Cos(angle));
                            }
                            HOperatorSet.GenContourPolygonXld(out roiObj, rowList.ToArray(), colList.ToArray());
                        }
                        break;

                    case RoiType.Freehand:
                        // 手绘 ROI：使用原始笔迹点生成轮廓
                        if (CurrentRoiPreview.FreehandRawPoints != null && CurrentRoiPreview.FreehandRawPoints.Count >= 2)
                        {
                            int count = CurrentRoiPreview.FreehandRawPoints.Count;
                            double[] rows = new double[count];
                            double[] cols = new double[count];
                            for (int i = 0; i < count; i++)
                            {
                                rows[i] = CurrentRoiPreview.FreehandRawPoints[i].Y;
                                cols[i] = CurrentRoiPreview.FreehandRawPoints[i].X;
                            }
                            HOperatorSet.GenContourPolygonXld(out roiObj, rows, cols);
                        }
                        break;
                }

                // 显示 ROI 轮廓对象
                if (roiObj != null && roiObj.IsInitialized())
                {
                    HOperatorSet.SetColor(hWindow, "#00FF00");
                    hWindow.DispObj(roiObj);
                    roiObj.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HalconCanvas] RenderRoiPreview 异常: {ex.Message}");
            }
        }

        #endregion

        #region 坐标转换方法

        /// <summary>
        /// CAD 坐标 → 图像坐标（Halcon 像素坐标）转换
        /// CAD 坐标系 Y 轴向上，Halcon 图像坐标系 Row 轴向下
        /// 变换公式：col = cadX - _offsetX, row = -cadY + _offsetY
        /// _offsetX/_offsetY 由 FitToAll 根据数据包围盒动态计算
        /// </summary>
        public (double row, double col) CadToImage(double cadX, double cadY)
        {
            double col = cadX - _offsetX;
            double row = -cadY + _offsetY;
            return (row, col);
        }

        /// <summary>
        /// 图像坐标（Halcon 像素坐标）→ CAD 坐标转换（CadToImage 的逆运算）
        /// 用于鼠标点击位置的命中检测和实时坐标显示
        /// 变换公式：cadX = col + _offsetX, cadY = -row + _offsetY
        /// </summary>
        public (double cadX, double cadY) ImageToCad(double row, double col)
        {
            double cadX = col + _offsetX;
            double cadY = -row + _offsetY;
            return (cadX, cadY);
        }

        #endregion

        #region 鼠标事件处理

        // 鼠标按下位置（用于区分点击和拖拽）
        private double _mouseDownRow, _mouseDownCol;
        private bool _mousePressed;

        /// <summary>
        /// Halcon 原生鼠标移动事件——坐标显示 + ROI 拖拽预览
        /// </summary>
        private void OnHWindowMouseMove(object sender, HMouseEventArgs e)
        {
            if (_halconControl == null || _disposed)
                return;

            try
            {
                var hWindow = _halconControl.mCtrl_HWindow?.HalconWindow;
                if (hWindow == null)
                    return;

                double row, col;
                int buttonState;
                try
                {
                    hWindow.GetMpositionSubPix(out row, out col, out buttonState);
                }
                catch (HalconDotNet.HOperatorException)
                {
                    return;
                }

                var cadCoord = ImageToCad(row, col);
                CoordinateChanged?.Invoke(cadCoord.cadX, cadCoord.cadY);

                if (DrawMode != RoiDrawMode.None)
                {
                    switch (DrawMode)
                    {
                        case RoiDrawMode.Paint:
                            HandlePaintMove(row, col);
                            break;
                        case RoiDrawMode.Eraser:
                            HandleEraserMove(row, col);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HalconCanvas] OnHWindowMouseMove 异常: {ex.Message}");
            }
        }

        /// <summary>
        /// Halcon 原生鼠标按下事件——ROI 绘制开始 / 记录点击位置
        /// </summary>
        private void OnHWindowMouseDown(object sender, HMouseEventArgs e)
        {
            if (_halconControl == null || _disposed)
                return;

            try
            {
                var hWindow = _halconControl.mCtrl_HWindow?.HalconWindow;
                if (hWindow == null)
                    return;

                double row, col;
                int buttonState;
                try
                {
                    hWindow.GetMpositionSubPix(out row, out col, out buttonState);
                }
                catch (HalconDotNet.HOperatorException)
                {
                    return;
                }

                if (buttonState == 2 && DrawMode == RoiDrawMode.Polyline)
                {
                    FinishPolyline();
                    return;
                }

                if (buttonState == 1)
                {
                    _mouseDownRow = row;
                    _mouseDownCol = col;
                    _mousePressed = true;

                    if (DrawMode != RoiDrawMode.None)
                    {
                        HandleRoiMouseDown(row, col);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HalconCanvas] OnHWindowMouseDown 异常: {ex.Message}");
            }
        }

        /// <summary>
        /// Halcon 原生鼠标释放事件——ROI 绘制完成 / 实体选中检测
        /// </summary>
        private void OnHWindowMouseUp(object sender, HMouseEventArgs e)
        {
            if (_halconControl == null || _disposed)
                return;

            try
            {
                var hWindow = _halconControl.mCtrl_HWindow?.HalconWindow;
                if (hWindow == null)
                    return;

                double row = 0, col = 0;
                try
                {
                    hWindow.GetMpositionSubPix(out row, out col, out int _);
                }
                catch (HalconDotNet.HOperatorException)
                {
                    _mousePressed = false;
                    return;
                }

                if (_mousePressed)
                {
                    _mousePressed = false;

                    if (DrawMode != RoiDrawMode.None)
                    {
                        HandleRoiMouseUp(row, col);
                        return;
                    }

                    double dist = Math.Sqrt(Math.Pow(row - _mouseDownRow, 2) + Math.Pow(col - _mouseDownCol, 2));
                    if (dist < 5)
                    {
                        var cadHit = ImageToCad(row, col);

                        // 触发画布点击事件，携带精确的 CAD 坐标
                        // 用于"从画布选取"等场景，避免鼠标移开后坐标丢失
                        CanvasPointClicked?.Invoke(cadHit.cadX, cadHit.cadY);

                        CadEntity hitEntity = null;
                        if (Entities != null)
                        {
                            for (int i = Entities.Count - 1; i >= 0; i--)
                            {
                                var entity = Entities[i];
                                if (!entity.IsVisible) continue;
                                if (IsHit(entity, cadHit.cadX, cadHit.cadY))
                                {
                                    hitEntity = entity;
                                    break;
                                }
                            }
                        }
                        SelectedEntity = hitEntity;
                        EntitySelected?.Invoke(hitEntity);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HalconCanvas] OnHWindowMouseUp 异常: {ex.Message}");
            }
        }

        /// <summary>
        /// ROI 模式下鼠标按下——仅在 ROI 尚未创建时初始化，已存在则由 ROIController 处理拖拽
        /// </summary>
        private void HandleRoiMouseDown(double row, double col)
        {
            _roiStartRow = row;
            _roiStartCol = col;
            _roiMouseDown = true;
            IsDrawing = true;

            // ROI 已存在时不再重复创建，让 ROIController 的 mouseDownAction 处理手柄拖拽
            if (_roiDict.ContainsKey("CanvasROI"))
                return;

            switch (DrawMode)
            {
                case RoiDrawMode.Rectangle2:
                    StartRectangle2(row, col);
                    break;
                case RoiDrawMode.Circle:
                    StartCircle(row, col);
                    break;
                case RoiDrawMode.Line:
                    StartLine(row, col);
                    break;
                case RoiDrawMode.CircularArc:
                    StartCircularArc(row, col);
                    break;
                case RoiDrawMode.Polyline:
                    AddPolylinePoint(row, col);
                    break;
                case RoiDrawMode.Paint:
                case RoiDrawMode.Eraser:
                    break;
            }
        }

        /// <summary>
        /// ROI 模式下鼠标释放——完成几何形状绘制
        /// </summary>
        private void HandleRoiMouseUp(double row, double col)
        {
            if (!_roiMouseDown) return;
            _roiMouseDown = false;

            switch (DrawMode)
            {
                case RoiDrawMode.Paint:
                case RoiDrawMode.Eraser:
                    break;
                case RoiDrawMode.Rectangle2:
                case RoiDrawMode.Circle:
                case RoiDrawMode.Line:
                case RoiDrawMode.CircularArc:
                    FinishCurrentShape();
                    break;
                case RoiDrawMode.Polyline:
                    break;
            }
        }

        /// <summary>
        /// WPF 双击事件——ROI 模式下结束折线，正常模式下触发实体双击编辑
        /// </summary>
        private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DrawMode != RoiDrawMode.None)
            {
                if (DrawMode == RoiDrawMode.Polyline)
                    FinishPolyline();
                else if (_roiMouseDown)
                    FinishCurrentShape();
                e.Handled = true;
                return;
            }

            // 正常模式双击：实体编辑
            double row = 0, col = 0;
            if (_halconControl?.mCtrl_HWindow?.HalconWindow != null)
            {
                try
                {
                    _halconControl.mCtrl_HWindow.HalconWindow.GetMpositionSubPix(
                        out row, out col, out int _);
                }
                catch (HalconDotNet.HOperatorException)
                {
                    return;
                }
            }

            var cadHit = ImageToCad(row, col);
            if (Entities != null)
            {
                for (int i = Entities.Count - 1; i >= 0; i--)
                {
                    var entity = Entities[i];
                    if (!entity.IsVisible) continue;
                    if (IsHit(entity, cadHit.cadX, cadHit.cadY))
                    {
                        EntityDoubleClicked?.Invoke(entity);
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        #endregion

        #region 命中检测方法

        /// <summary>
        /// 判断 CAD 点击位置是否命中指定图元，容差随缩放自适应
        /// </summary>
        private bool IsHit(CadEntity entity, double cadX, double cadY)
        {
            double toleranceCad = HitToleranceCad / GetCurrentZoomFactor();

            return entity.EntityType switch
            {
                CadEntityType.Line => IsHitLine((CadLine)entity, cadX, cadY, toleranceCad),
                CadEntityType.Arc => IsHitArc((CadArc)entity, cadX, cadY, toleranceCad),
                CadEntityType.Circle => IsHitCircle((CadCircle)entity, cadX, cadY, toleranceCad),
                CadEntityType.LwPolyline => IsHitPolyline((CadLwPolyline)entity, cadX, cadY, toleranceCad),
                CadEntityType.Ellipse => IsHitEllipse((CadEllipse)entity, cadX, cadY, toleranceCad),
                CadEntityType.Spline => IsHitSpline((CadSpline)entity, cadX, cadY, toleranceCad),
                _ => false
            };
        }

        /// <summary>
        /// 直线命中检测——计算点到线段的最短距离
        /// 使用向量投影法判断点是否在线段的容差范围内
        /// </summary>
        private bool IsHitLine(CadLine line, double px, double py, double tolerance)
        {
            double dx = line.EndX - line.StartX;
            double dy = line.EndY - line.StartY;
            double lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-10)
            {
                // 退化为点的情况
                double distToPoint = Math.Sqrt(Math.Pow(px - line.StartX, 2) + Math.Pow(py - line.StartY, 2));
                return distToPoint <= tolerance;
            }

            // 计算投影参数 t ∈ [0,1]
            double t = Math.Clamp(((px - line.StartX) * dx + (py - line.StartY) * dy) / lenSq, 0, 1);
            // 最近点坐标
            double nearX = line.StartX + t * dx;
            double nearY = line.StartY + t * dy;
            // 距离判断
            double distToSegment = Math.Sqrt(Math.Pow(px - nearX, 2) + Math.Pow(py - nearY, 2));
            return distToSegment <= tolerance;
        }

        /// <summary>
        /// 圆弧命中检测——先判断点到圆心的距离是否接近半径，
        /// 再验证投影角度是否在圆弧的角度范围内
        /// </summary>
        private bool IsHitArc(CadArc arc, double px, double py, double tolerance)
        {
            double distToCenter = Math.Sqrt(Math.Pow(px - arc.CenterX, 2) + Math.Pow(py - arc.CenterY, 2));
            // 点到圆周的距离
            double distToArc = Math.Abs(distToCenter - arc.Radius);
            if (distToArc > tolerance)
                return false;

            // 验证角度是否在圆弧范围内
            double angle = Math.Atan2(py - arc.CenterY, px - arc.CenterX) * 180.0 / Math.PI;
            return IsAngleInArcRange(angle, arc.StartAngle, arc.EndAngle);
        }

        /// <summary>
        /// 圆形命中检测——判断点到圆周的距离是否在容差内
        /// </summary>
        private bool IsHitCircle(CadCircle circle, double px, double py, double tolerance)
        {
            double distToCenter = Math.Sqrt(Math.Pow(px - circle.CenterX, 2) + Math.Pow(py - circle.CenterY, 2));
            return Math.Abs(distToCenter - circle.Radius) <= tolerance;
        }

        /// <summary>
        /// 多段线命中检测——逐段检查点到每条线段的距离
        /// 任一段命中即视为整体命中
        /// </summary>
        private bool IsHitPolyline(CadLwPolyline polyline, double px, double py, double tolerance)
        {
            if (polyline.Vertices == null || polyline.Vertices.Count < 2)
                return false;

            int segCount = polyline.IsClosed ? polyline.Vertices.Count : polyline.Vertices.Count - 1;
            for (int i = 0; i < segCount; i++)
            {
                int j = (i + 1) % polyline.Vertices.Count;
                var p1 = polyline.Vertices[i];
                var p2 = polyline.Vertices[j];

                // 复用直线距离计算
                double dx = p2.X - p1.X;
                double dy = p2.Y - p1.Y;
                double lenSq = dx * dx + dy * dy;
                if (lenSq < 1e-10) continue;

                double t = Math.Clamp(((px - p1.X) * dx + (py - p1.Y) * dy) / lenSq, 0, 1);
                double nearX = p1.X + t * dx;
                double nearY = p1.Y + t * dy;
                double dist = Math.Sqrt(Math.Pow(px - nearX, 2) + Math.Pow(py - nearY, 2));

                if (dist <= tolerance)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 椭圆命中检测——将点变换到椭圆的局部坐标系后判断到边界的距离
        /// 使用简化版近似算法：归一化距离判断
        /// </summary>
        private bool IsHitEllipse(CadEllipse ellipse, double px, double py, double tolerance)
        {
            // 将点平移到椭圆中心
            double dx = px - ellipse.CenterX;
            double dy = py - ellipse.CenterY;

            // 反向旋转到椭圆局部坐标系（消除旋转角影响）
            double rotRad = -ellipse.RotationAngle * Math.PI / 180.0;
            double localX = dx * Math.Cos(rotRad) - dy * Math.Sin(rotRad);
            double localY = dx * Math.Sin(rotRad) + dy * Math.Cos(rotRad);

            // 归一化距离：在局部坐标系中点到椭圆边界的近似距离
            if (ellipse.MajorAxisLength < 1e-6 || ellipse.MinorAxisLength < 1e-6)
                return false;

            double normDist = Math.Sqrt(Math.Pow(localX / ellipse.MajorAxisLength, 2) +
                                        Math.Pow(localY / ellipse.MinorAxisLength, 2));
            // 归一化距离 ≈ 1 表示在边界上，偏差乘以等效半径得到实际距离
            double equivRadius = (ellipse.MajorAxisLength + ellipse.MinorAxisLength) / 2;
            double boundaryDist = Math.Abs(normDist - 1) * equivRadius;

            return boundaryDist <= tolerance;
        }

        /// <summary>
        /// 样条曲线命中检测——将曲线离散化为折线段后逐段检测最短距离
        /// 复用 CadEntityHalconExtensions 的 DiscretizeSplineForHitTest 获取采样点
        /// 闭合样条的首尾连接段也会纳入检测
        /// </summary>
        private bool IsHitSpline(CadSpline spline, double px, double py, double tolerance)
        {
            var points = CadEntityHalconExtensions.DiscretizeSplineForHitTest(spline);
            if (points == null || points.Count < 2)
                return false;

            int segCount = spline.IsClosed ? points.Count : points.Count - 1;
            for (int i = 0; i < segCount; i++)
            {
                int j = (i + 1) % points.Count;
                var p1 = points[i];
                var p2 = points[j];

                double dx = p2.X - p1.X;
                double dy = p2.Y - p1.Y;
                double lenSq = dx * dx + dy * dy;
                if (lenSq < 1e-10) continue;

                double t = Math.Clamp(((px - p1.X) * dx + (py - p1.Y) * dy) / lenSq, 0, 1);
                double nearX = p1.X + t * dx;
                double nearY = p1.Y + t * dy;
                double dist = Math.Sqrt(Math.Pow(px - nearX, 2) + Math.Pow(py - nearY, 2));

                if (dist <= tolerance)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 判断角度是否在圆弧的起止角度范围内
        /// 处理跨越 0°/360° 的情况（如从 300° 到 60°）
        /// </summary>
        private bool IsAngleInArcRange(double angle, double startAngle, double endAngle)
        {
            // 归一化角度到 [0, 360)
            angle = angle % 360;
            if (angle < 0) angle += 360;

            double normStart = startAngle % 360;
            if (normStart < 0) normStart += 360;
            double normEnd = endAngle % 360;
            if (normEnd < 0) normEnd += 360;

            if (normEnd >= normStart)
                return angle >= normStart && angle <= normEnd;
            else
                return angle >= normStart || angle <= normEnd;
        }

        #endregion

        #region ROI 绘制模式核心逻辑

        /// <summary>
        /// DrawMode 属性变更的内部处理——清理旧模式状态并初始化新模式
        /// 切换到 None 时自动取消当前绘制
        /// </summary>
        private void OnDrawModeChangedInternal(RoiDrawMode newMode)
        {
            if (newMode == RoiDrawMode.None)
            {
                CancelRoiDrawing();
            }
            // ROI 绘制模式：不设置 DrawModel=true
            // ROIController 的鼠标交互需要 HWndCtrl 的鼠标事件正常工作
            // ViewWindow 的缩放/平移与 ROI 交互共存（先检查 ROI 命中，再走平移）
            if (newMode == RoiDrawMode.Paint || newMode == RoiDrawMode.Eraser)
            {
                InitPaintEraserState();
            }
            RenderEntities();
        }

        /// <summary>
        /// 初始化涂抹/擦除模式的累积区域和笔刷预览
        /// </summary>
        private void InitPaintEraserState()
        {
            // 释放旧的累积区域
            if (_paintMaskRegion != null && _paintMaskRegion.IsInitialized())
            {
                _paintMaskRegion.Dispose();
                _paintMaskRegion = null;
            }
            if (_brushPreviewRegion != null && _brushPreviewRegion.IsInitialized())
            {
                _brushPreviewRegion.Dispose();
                _brushPreviewRegion = null;
            }
        }

        /// <summary>
        /// 开始指定模式的 ROI 绘制——等效于设置 DrawMode 属性
        /// 提供编程式 API 供外部调用
        /// </summary>
        /// <param name="mode">要启动的 ROI 绘制模式</param>
        public void StartRoiDrawing(RoiDrawMode mode)
        {
            DrawMode = mode;
        }

        /// <summary>
        /// 取消当前 ROI 绘制——清理所有中间状态，恢复到 None 模式
        /// 不触发 RoiCompleted 事件
        /// </summary>
        public void CancelRoiDrawing()
        {
            // 清理 ROIController 中注册的 ROI
            if (_halconControl != null && !_disposed)
            {
                try
                {
                    _halconControl.WindowH.notDisplayRoi();
                }
                catch { }
            }
            _roiDict.Clear();
            _roiMouseDown = false;

            _polylineVertices?.Clear();

            if (_paintMaskRegion != null && _paintMaskRegion.IsInitialized())
            {
                _paintMaskRegion.Dispose();
                _paintMaskRegion = null;
            }
            if (_brushPreviewRegion != null && _brushPreviewRegion.IsInitialized())
            {
                _brushPreviewRegion.Dispose();
                _brushPreviewRegion = null;
            }

            IsDrawing = false;
            SetValue(ActiveRoiRegionPropertyKey, null);

            DrawMode = RoiDrawMode.None;
            RenderEntities();
        }

        /// <summary>
        /// 获取当前绘制结果的 HObject（HRegion 或 HXLDCont）
        /// 调用者需根据 DrawMode 判断返回类型
        /// </summary>
        /// <returns>当前的 ROI 结果对象，可能为 null</returns>
        public HObject GetResultingRegion()
        {
            return ActiveRoiRegion;
        }

        // ==================== 各 ROI 模式的具体实现 ====================

        /// <summary>
        /// 完成当前几何形状绘制并触发 RoiCompleted 事件
        /// </summary>
        private void FinishCurrentShape()
        {
            try
            {
                // 从 _roiDict 获取刚创建的 ROI
                var lastRoi = _roiDict.Values.LastOrDefault();
                if (lastRoi == null) return;

                HObject result = null;
                if (DrawMode == RoiDrawMode.Rectangle2 || DrawMode == RoiDrawMode.Circle)
                {
                    result = lastRoi.GetRegion();
                }
                else if (DrawMode == RoiDrawMode.Line || DrawMode == RoiDrawMode.CircularArc)
                {
                    result = lastRoi.GetXLD();
                }

                if (result != null && result.IsInitialized())
                {
                    ActiveRoiRegion = result;
                    RoiCompleted?.Invoke(result);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HalconCanvas] FinishCurrentShape 异常: {ex.Message}");
            }
            finally
            {
                IsDrawing = false;
            }
        }

        // ---------- ROIRectangle2 旋转矩形 ----------

        /// <summary>
        /// 开始旋转矩形绘制——在点击位置创建初始 ROIRectangle2 实例
        /// </summary>
        private void StartRectangle2(double row, double col)
        {
            // 使用 ViewWindow.genRect2 注册 ROI 到 ROIController，支持自动重绘和交互拖拽
            double defaultLen = _halconControl.hv_imageHeight / 4.0;
            double defaultWid = _halconControl.hv_imageWidth / 4.0;
            _halconControl.WindowH.genRect2(
                "CanvasROI", row, col, 0, defaultLen, defaultWid, ref _roiDict);
        }

        // ---------- ROICircle 圆形 ----------

        /// <summary>
        /// 开始圆形绘制——在点击位置创建初始 ROICircle 实例
        /// </summary>
        private void StartCircle(double row, double col)
        {
            double defaultRadius = Math.Min(_halconControl.hv_imageHeight, _halconControl.hv_imageWidth) / 4.0;
            _halconControl.WindowH.genCircle(
                "CanvasROI", row, col, defaultRadius, ref _roiDict);
        }

        // ---------- ROILine 线段 ----------

        /// <summary>
        /// 开始线段绘制——在点击位置创建初始 ROILine 实例
        /// </summary>
        private void StartLine(double row, double col)
        {
            _halconControl.WindowH.genLine(
                "CanvasROI", row, col, row + 50, col + 50, ref _roiDict);
        }

        // ---------- ROIPolyline 折线 ----------

        /// <summary>
        /// 向折线添加一个顶点——每次左键点击追加一个点
        /// </summary>
        private void AddPolylinePoint(double row, double col)
        {
            if (_polylineVertices == null)
                _polylineVertices = new List<System.Windows.Point>();

            _polylineVertices.Add(new System.Windows.Point(col, row));
            IsDrawing = true;
            RenderPolylinePreview();
        }

        /// <summary>
        /// 折线鼠标移动——显示从上一个顶点到当前位置的预览线段
        /// </summary>
        private void HandlePolylineMove(double row, double col)
        {
            if (_polylineVertices == null || _polylineVertices.Count == 0) return;
            // 渲染已有顶点 + 到鼠标位置的预览线
            RenderPolylinePreviewWithPreview(row, col);
        }

        /// <summary>
        /// 完成折线绘制——将所有顶点合并为 XLD 轮廓并触发 RoiCompleted
        /// </summary>
        private void FinishPolyline()
        {
            if (_polylineVertices == null || _polylineVertices.Count < 2)
            {
                // 不足 2 个点，无法构成折线
                CancelRoiDrawing();
                return;
            }

            try
            {
                int count = _polylineVertices.Count;
                double[] rows = new double[count];
                double[] cols = new double[count];
                for (int i = 0; i < count; i++)
                {
                    rows[i] = _polylineVertices[i].Y;
                    cols[i] = _polylineVertices[i].X;
                }

                HOperatorSet.GenContourPolygonXld(out HObject xld, rows, cols);
                if (xld != null && xld.IsInitialized())
                {
                    ActiveRoiRegion = xld;
                    RoiCompleted?.Invoke(xld);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HalconCanvas] FinishPolyline 异常: {ex.Message}");
            }
            finally
            {
                IsDrawing = false;
                _polylineVertices?.Clear();
            }
        }

        /// <summary>
        /// 渲染折线的当前顶点预览（不含动态预览线）
        /// </summary>
        private void RenderPolylinePreview()
        {
            if (_polylineVertices == null || _polylineVertices.Count < 2) return;
            RenderPolylinePreviewInternal(null, null);
        }

        /// <summary>
        /// 渲染折线预览（含到鼠标位置的动态预览线段）
        /// </summary>
        private void RenderPolylinePreviewWithPreview(double previewRow, double previewCol)
        {
            RenderPolylinePreviewInternal(previewRow, previewCol);
        }

        /// <summary>
        /// 折线预览渲染内部实现
        /// </summary>
        private void RenderPolylinePreviewInternal(double? previewRow, double? previewCol)
        {
            if (_halconControl == null || _disposed) return;
            try
            {
                var hWindow = _halconControl.mCtrl_HWindow?.HalconWindow;
                if (hWindow == null) return;

                // 清除旧实体并重绘背景
                _halconControl.WindowH._hWndControl.ClearHObjectList();
                _halconControl.WindowH._hWndControl.ClearROI();

                // 先渲染 CAD 图元（复用 RenderEntities 的图元部分）
                RenderCadEntitiesOnly(hWindow);

                // 渲染折线预览
                hWindow.SetColor("#00FF00");
                hWindow.SetLineWidth(2.0);

                int count = _polylineVertices?.Count ?? 0;
                if (count >= 2)
                {
                    double[] rows = new double[count];
                    double[] cols = new double[count];
                    for (int i = 0; i < count; i++)
                    {
                        rows[i] = _polylineVertices[i].Y;
                        cols[i] = _polylineVertices[i].X;
                    }
                    HObject polylineXld;
                    HOperatorSet.GenContourPolygonXld(out polylineXld, rows, cols);
                    if (polylineXld != null && polylineXld.IsInitialized())
                    {
                        HOperatorSet.SetColor(hWindow, "#00FF00");
                        hWindow.DispObj(polylineXld);
                        polylineXld.Dispose();
                    }
                }

                // 渲染动态预览线段（从最后一个顶点到鼠标位置）
                if (previewRow.HasValue && previewCol.HasValue && count > 0)
                {
                    var lastPt = _polylineVertices[count - 1];
                    hWindow.DispLine(lastPt.Y, lastPt.X, previewRow.Value, previewCol.Value);
                }

                // 绘制所有顶点标记
                hWindow.SetDraw("fill");
                for (int i = 0; i < count; i++)
                {
                    hWindow.DispRectangle2(_polylineVertices[i].Y, _polylineVertices[i].X, 0, 3, 3);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HalconCanvas] RenderPolylinePreview 异常: {ex.Message}");
            }
        }

        // ---------- ROICircularArc 圆弧 ----------

        /// <summary>
        /// 开始圆弧绘制——在点击位置创建初始 ROICircularArc 实例
        /// </summary>
        private void StartCircularArc(double row, double col)
        {
            _halconControl.WindowH.genCircleArr(
                "CanvasROI", row, col, 50, ref _roiDict);
        }

        // ---------- Paint 涂抹模式 ----------

        /// <summary>
        /// 涂抹模式鼠标移动——按住左键时持续将笔刷区域合并到累积 MaskRegion
        /// 使用 VMHWindowControl.Paint() 方法生成单次笔刷区域
        /// </summary>
        private void HandlePaintMove(double row, double col)
        {
            if (!_roiMouseDown || _halconControl == null) return;

            try
            {
                // 计算缩放因子用于调整笔刷大小
                double zoomFactor = GetCurrentZoomFactor();
                HRegion brush = _halconControl.Paint(row, col, zoomFactor);

                if (brush != null && brush.IsInitialized())
                {
                    // 累积合并到掩膜区域
                    if (_paintMaskRegion == null || !_paintMaskRegion.IsInitialized())
                    {
                        _paintMaskRegion = brush.Clone();
                    }
                    else
                    {
                        _paintMaskRegion = _paintMaskRegion.Union2(brush);
                        brush.Dispose();
                    }
                    // 更新只读属性
                    ActiveRoiRegion = _paintMaskRegion;
                    // 实时预览涂抹结果
                    RenderPaintEraserPreview(true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HalconCanvas] Paint Move 异常: {ex.Message}");
            }
        }

        // ---------- Eraser 擦除模式 ----------

        /// <summary>
        /// 擦除模式鼠标移动——按住左键时持续从累积 MaskRegion 中减去笔刷区域
        /// 使用 VMHWindowControl.Eraser() 方法生成单次笔刷区域
        /// </summary>
        private void HandleEraserMove(double row, double col)
        {
            if (!_roiMouseDown || _halconControl == null) return;

            try
            {
                double zoomFactor = GetCurrentZoomFactor();
                HRegion brush = _halconControl.Eraser(row, col, zoomFactor);

                if (brush != null && brush.IsInitialized())
                {
                    // 从掩膜区域中差集（擦除）
                    if (_paintMaskRegion == null || !_paintMaskRegion.IsInitialized())
                    {
                        // 如果没有现有区域，擦除模式初始化为一个空区域
                        // 后续差集操作会基于此空区域进行
                        _paintMaskRegion = new HRegion();
                        _paintMaskRegion.GenEmptyRegion();
                    }
                    _paintMaskRegion = _paintMaskRegion.Difference(brush);
                    brush.Dispose();
                    // 更新只读属性
                    ActiveRoiRegion = _paintMaskRegion;
                    // 实时预览擦除结果
                    RenderPaintEraserPreview(false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HalconCanvas] Eraser Move 异常: {ex.Message}");
            }
        }

        // ==================== ROI 预览渲染 ====================

        /// <summary>
        /// 渲染涂抹/擦除模式的实时预览
        /// </summary>
        /// <param name="isPaint">true 为涂抹模式（红色），false 为擦除模式（绿色）</param>
        private void RenderPaintEraserPreview(bool isPaint)
        {
            if (_halconControl == null || _disposed) return;

            try
            {
                var hWindow = _halconControl.mCtrl_HWindow?.HalconWindow;
                if (hWindow == null) return;

                // 清除旧实体并重绘背景
                _halconControl.WindowH._hWndControl.ClearHObjectList();
                _halconControl.WindowH._hWndControl.ClearROI();

                // 渲染 CAD 图元
                RenderCadEntitiesOnly(hWindow);

                // 显示累积的区域
                if (_paintMaskRegion != null && _paintMaskRegion.IsInitialized())
                {
                    hWindow.SetDraw("margin");
                    hWindow.SetColor(isPaint ? "#FF0000" : "#00FF00");
                    hWindow.SetLineWidth(1.0);
                    hWindow.DispObj(_paintMaskRegion);
                }

                // 显示当前笔刷位置预览
                if (_halconControl.BrushRegion != null && _halconControl.BrushRegion.IsInitialized())
                {
                    hWindow.SetDraw("margin");
                    hWindow.SetColor(isPaint ? "#FF6666" : "#66FF66");
                    hWindow.DispObj(_halconControl.BrushRegion);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HalconCanvas] RenderPaintEraserPreview 异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 仅渲染 CAD 图元（不含 ROI 预览）——供各 ROI 预览方法复用
        /// 使用直接 hWindow.DispObj() 渲染（ROI 模式下 DrawModel=true，无需存储对象）
        /// </summary>
        private void RenderCadEntitiesOnly(HWindow hWindow)
        {
            if (Entities == null) return;

            hWindow.SetDraw("margin");
            hWindow.SetLineWidth(1.5);

            foreach (var entity in Entities)
            {
                if (!entity.IsVisible) continue;

                bool isSelected = (entity == SelectedEntity);
                string color = GetLayerColor(entity);
                if (isSelected) color = "#FFD700";

                try
                {
                    HObject hObj = entity.EntityType switch
                    {
                        CadEntityType.Line => ((CadLine)entity).ToHObject(),
                        CadEntityType.Arc => ((CadArc)entity).ToHObject(),
                        CadEntityType.Circle => ((CadCircle)entity).ToHObject(),
                        CadEntityType.LwPolyline => ((CadLwPolyline)entity).ToHObject(),
                        CadEntityType.Ellipse => ((CadEllipse)entity).ToHObject(),
                        _ => null
                    };

                    if (hObj != null && hObj.IsInitialized())
                    {
                        // CAD 坐标系 → 图像坐标系（Y 轴翻转 + 居中偏移）
                        HObject imgObj = TransformCadToImage(hObj);
                        hObj.Dispose();

                        if (imgObj != null && imgObj.IsInitialized())
                        {
                            hWindow.SetLineWidth(isSelected ? 3.0 : 1.5);
                            HOperatorSet.SetColor(hWindow, color);
                            hWindow.DispObj(imgObj);
                            imgObj.Dispose();
                        }
                        else
                        {
                            imgObj?.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HalconCanvas] RenderCadEntitiesOnly 异常: {ex.Message}");
                }
            }
        }

        #endregion

        #region 公共辅助方法

        /// <summary>
        /// 自适应全部图形——根据所有实体的总包围盒自动设置 Halcon 窗口视口
        /// 将 CAD 包围盒转换为图像坐标后使用 SetPart 显示，并触发重绘
        /// </summary>
        /// <summary>
        /// 根据所有可见图元的包围盒自适应 Halcon 窗口视口
        /// 同时动态更新坐标偏移量和背景图像尺寸，使数据从图像左上角开始排列
        /// 这样右键"适应图片"也会基于正确的图像尺寸设置视口
        /// </summary>
        public void FitToAll()
        {
            // ✅ 性能优化：视口变化时使坐标变换缓存失效
            InvalidateTransformCache();

            if (Entities == null || Entities.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[HalconCanvas] FitToAll: 跳过（Entities为空, Count={Entities?.Count ?? 0}）");
                return;
            }

            if (_halconControl == null || _disposed)
                return;

            var hWindow = _halconControl.mCtrl_HWindow?.HalconWindow;
            if (hWindow == null)
                return;

            var combinedBbox = new BoundingBox();
            foreach (var entity in Entities)
            {
                if (!entity.IsVisible)
                    continue;
                var bbox = entity.GetBoundingBox();
                if (!bbox.IsEmpty)
                    combinedBbox = combinedBbox.Union(bbox);
            }

            if (combinedBbox.IsEmpty)
            {
                System.Diagnostics.Debug.WriteLine($"[HalconCanvas] FitToAll: 包围盒为空");
                return;
            }

            double margin = 40;

            // 计算坐标偏移量：使 CAD 数据从图像 (margin, margin) 位置开始
            // col = cadX - _offsetX → cadX=minX 时 col=margin → _offsetX = minX - margin
            // row = -cadY + _offsetY → cadY=maxY 时 row=margin → _offsetY = maxY + margin
            _offsetX = combinedBbox.MinX - margin;
            _offsetY = combinedBbox.MaxY + margin;

            // 计算图像尺寸
            int imgWidth = (int)Math.Ceiling(combinedBbox.Width + 2 * margin);
            int imgHeight = (int)Math.Ceiling(combinedBbox.Height + 2 * margin);
            if (imgWidth < 1) imgWidth = 1;
            if (imgHeight < 1) imgHeight = 1;

            System.Diagnostics.Debug.WriteLine($"[HalconCanvas] FitToAll: BBox=[{combinedBbox.MinX:F1},{combinedBbox.MaxX:F1}]x[{combinedBbox.MinY:F1},{combinedBbox.MaxY:F1}], offset=({_offsetX:F1},{_offsetY:F1}), imgSize={imgWidth}x{imgHeight}");

            // 创建匹配数据范围的背景图像
            CreateWhiteCanvasImage(imgWidth, imgHeight);
            _halconControl.Image = _canvasImage;

            // 设置视口为整个图像范围——SetPart(0, 0, height-1, width-1)
            // 这样右键"适应图片"也会设置相同的视口
            hWindow.SetPart(0, 0, imgHeight - 1, imgWidth - 1);

            // 重新渲染所有图元（使用新的坐标偏移量）
            RenderEntities();
        }

        /// <summary>
        /// 重置视图——调用 DispImageFitImage 让 Halcon 窗口恢复初始显示状态
        /// </summary>
        public void ResetView()
        {
            if (_halconControl != null && !_disposed)
            {
                try
                {
                    _halconControl.DispImageFitImage();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HalconCanvas] ResetView 异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 将视口聚焦到指定CAD坐标区域，在线段周围留出边距
        /// 用于"显示基准线段/目标线段"等场景
        /// </summary>
        public void FitToCadRegion(double cadX1, double cadY1, double cadX2, double cadY2)
        {
            // ✅ 性能优化：视口变化时使坐标变换缓存失效
            InvalidateTransformCache();

            if (_halconControl == null || _disposed) return;

            try
            {
                var hWindow = _halconControl.mCtrl_HWindow?.HalconWindow;
                if (hWindow == null) return;

                var img1 = CadToImage(cadX1, cadY1);
                var img2 = CadToImage(cadX2, cadY2);

                double minRow = Math.Min(img1.row, img2.row);
                double maxRow = Math.Max(img1.row, img2.row);
                double minCol = Math.Min(img1.col, img2.col);
                double maxCol = Math.Max(img1.col, img2.col);

                double margin = Math.Max(maxRow - minRow, maxCol - minCol) * 1.5;
                if (margin < 50) margin = 50;

                hWindow.SetPart(
                    (int)(minRow - margin),
                    (int)(minCol - margin),
                    (int)(maxRow + margin),
                    (int)(maxCol + margin));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HalconCanvas] FitToCadRegion 异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前 CAD→图像 坐标转换偏移量（offsetX, offsetY）
        /// 用于外部计算图像像素坐标：row = -cadY + offsetY, col = cadX - offsetX
        /// </summary>
        public (double offsetX, double offsetY) GetCadToImageOffset()
        {
            return (_offsetX, _offsetY);
        }

        #endregion

        #region IDisposable 实现

        /// <summary>
        /// 释放资源——清理 Halcon 对象防止内存泄漏
        /// 包括白色背景图像和 VMHWindowControl 的鼠标事件注销
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源的核心实现，支持派生类重写
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // 注销 HWindow 鼠标事件
                try
                {
                    if (_halconControl?.mCtrl_HWindow != null)
                    {
                        _halconControl.mCtrl_HWindow.HMouseMove -= OnHWindowMouseMove;
                        _halconControl.mCtrl_HWindow.HMouseDown -= OnHWindowMouseDown;
                        _halconControl.mCtrl_HWindow.HMouseUp -= OnHWindowMouseUp;
                    }
                }
                catch { }

                // 释放白色背景图像
                try
                {
                    if (_canvasImage != null && _canvasImage.IsInitialized())
                    {
                        _canvasImage.Dispose();
                        _canvasImage = null;
                    }
                }
                catch { }

                // 清理 ROI 绘制模式的资源
                _roiDict.Clear();
                try
                {
                    if (_paintMaskRegion != null && _paintMaskRegion.IsInitialized())
                    {
                        _paintMaskRegion.Dispose();
                        _paintMaskRegion = null;
                    }
                }
                catch { }
                try
                {
                    if (_brushPreviewRegion != null && _brushPreviewRegion.IsInitialized())
                    {
                        _brushPreviewRegion.Dispose();
                        _brushPreviewRegion = null;
                    }
                }
                catch { }
                _polylineVertices?.Clear();

                // ✅ 性能优化：释放坐标变换缓存
                InvalidateTransformCache();

                // 清理 WindowsFormsHost 的子控件
                try
                {
                    if (_winFormHost != null)
                    {
                        _winFormHost.Child = null;
                    }
                }
                catch { }
            }

            _disposed = true;
        }

        #endregion
    }
}
