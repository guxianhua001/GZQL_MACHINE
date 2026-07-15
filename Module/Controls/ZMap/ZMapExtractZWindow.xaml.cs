using Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
#if HAS_HALCON
using HalconDotNet;
using HalconWrapper;
using HalconWrapper.Config;
using HalconWrapper.Model;
#endif

namespace Module.Controls.ZMap
{
    /// <summary>
    /// ZMAP高度提取悬浮工具窗口——独立弹出窗口，通过 Step3 面板新增按钮打开，
    /// 关闭时不影响 CadPointEditor 主流程的任何已有状态，仅在用户点击"应用"后
    /// 才把提取结果写回传入的采样点 CadPoint.Z（详见 ZMapExtractZViewModel）。
    ///
    /// 图像显示与ROI交互对齐参考Plugin.DispensePath：进程内用Halcon窗口
    /// VMHWindowControl直接显示高度图，ROI复用ROIController交互拖拽。其中：
    /// - 直线/圆弧：单个交互ROI，提取时按选中段点数沿ROI等距采样；
    /// - 折线：多顶点可"点击图像追加(方向可设)/插入中点/删除/拖拽"，顶点间显示骨架连线，
    ///   提取时按累计弧长等距重采样到目标点数；
    /// - 单点示教：点击图像逐点追加，每个点即一个输出采样点（需与选中段点数一致）。
    /// 顶点/示教点表格与图像双向联动（表格改坐标→图形刷新；拖拽/示教→表格刷新）。
    /// </summary>
    public partial class ZMapExtractZWindow : Window, INotifyPropertyChanged
    {
        // 直线/圆弧交互ROI在窗口内的固定键（折线/单点用整数下标作键）
        private const string RoiKey = "ZMapRoi";
        // 首尾点在该像素距离内视为闭合轨迹。
        // ROIPoint中心方块约4×4，人工拖到“重合”时中心仍常差数像素，2px过严导致无法自动闭合。
        private const double ClosedPolylineTolerancePixel = 12.0;

        public event PropertyChangedEventHandler PropertyChanged;

        #region 可绑定属性（顶点表格/示教工具栏，供XAML以RelativeSource=Window绑定）

        /// <summary>折线顶点/单点示教点集合（Col=列X，Row=行Y），与图像ROI双向联动。</summary>
        public ObservableCollection<ZMapRoiVertex> Vertices { get; } = new ObservableCollection<ZMapRoiVertex>();

        private ZMapRoiVertex _selectedVertex;
        /// <summary>表格选中的顶点：折线示教方向Auto时据此判定首/尾，插入以其为基准；选中时图像高亮对应点。</summary>
        public ZMapRoiVertex SelectedVertex
        {
            get => _selectedVertex;
            set { _selectedVertex = value; OnPropertyChanged(); OnSelectedVertexChanged(); }
        }

        private bool _isVertexRoi;
        /// <summary>当前ROI是否为顶点可编辑类型（折线/单点）——控制示教工具栏与表格显隐。</summary>
        public bool IsVertexRoi
        {
            get => _isVertexRoi;
            private set { _isVertexRoi = value; OnPropertyChanged(); }
        }

        private bool _teachModeEnabled = false;
        /// <summary>示教模式：勾选后点击图像空白处追加顶点/单点。</summary>
        public bool TeachModeEnabled
        {
            get => _teachModeEnabled;
            set { _teachModeEnabled = value; OnPropertyChanged(); }
        }

        private int _teachDirectionIndex;
        /// <summary>折线示教方向下拉索引：0=Auto/1=Head(向前)/2=Tail(向后)。</summary>
        public int TeachDirectionIndex
        {
            get => _teachDirectionIndex;
            set { _teachDirectionIndex = value; OnPropertyChanged(); }
        }

        private bool _reverseRoiDirection;
        /// <summary>
        /// ROI采样方向：false按起始顶点→末端，true反向。
        /// 起始顶点由顶点表首行/“设为起点”按钮决定，方向用于保证与CadPoint列表顺序一致。
        /// </summary>
        public bool ReverseRoiDirection
        {
            get => _reverseRoiDirection;
            set
            {
                if (_reverseRoiDirection == value) return;
                _reverseRoiDirection = value;
                OnPropertyChanged();
                UpdateVertexHint();
            }
        }

        private string _vertexHint = string.Empty;
        /// <summary>顶点/示教提示文本（折线重采样说明或单点需要的点数）。</summary>
        public string VertexHint
        {
            get => _vertexHint;
            private set { _vertexHint = value; OnPropertyChanged(); }
        }

        private bool _showCadPointProjection = true;
        /// <summary>
        /// 是否进入提取结果显示模式：仅显示等分生成的采样点和连线。
        /// 这些X标记与预览表同序，用于现场确认起点、方向和分点结果。
        /// </summary>
        public bool ShowCadPointProjection
        {
            get => _showCadPointProjection;
            set
            {
                if (_showCadPointProjection == value) return;
                _showCadPointProjection = value;
                OnPropertyChanged();
                RefreshCadPointProjectionOverlay();
            }
        }

        #endregion

        // 当前ROI类型（两种编译配置下都需要，供提示/显隐逻辑使用）
        private ZMapRoiType _roiType = ZMapRoiType.Polyline;
        // 图像尚未加载时暂存段级ROI，首次ShowHeightMap后再恢复。
        private ZMapRoiDefinition _pendingRoiDefinition;
        // 进入“仅显示提取结果”模式前暂存可编辑ROI，用于关闭结果显示后恢复示教状态。
        private ZMapRoiDefinition _roiBeforeProjectionDisplay;
        // 预览时由ROI按段数采样得到的有效图像像素点（仅显示，不参与ROI交互）。
        private readonly List<ZMapPixelPoint> _cadPointProjectionPixels = new List<ZMapPixelPoint>();
        // 预览表当前选中行对应的采样点，-1表示未选择。
        private int _selectedProjectionIndex = -1;

#if HAS_HALCON
        // 进程内Halcon显示控件（承载高度图与交互ROI）
        private VMHWindowControl _win;
        // 当前显示的高度图（服务构建的real单通道HImage，仅持有引用不释放）
        private HImage _image;
        // 与ROIController同步的ROI字典（拖拽时几何原地更新；genXXX以ref写入故不可为readonly）
        private Dictionary<string, ROI> _roiList = new Dictionary<string, ROI>();
        private bool _imageLoaded;
        private bool _mouseDown;
        // 标定像素拾取回调：由ViewModel发起，下一次图像左键单击只填标定行，不改变ROI。
        private Action<double, double> _pickCalibrationPixel;
        // 是否显示顶点编号：拖拽时每帧重绘文字开销大，默认关闭以保证流畅（可置true恢复编号）
        private bool _showVertexNumbers = false;
        // 拖拽/示教回写顶点集合时置位，避免触发表格属性变更→重复Render递归
        private bool _suppressRender;
        // 当前Vertices所属的ROI类型（在折线/单点间切换时重置默认顶点）
        private ZMapRoiType _verticesForType = ZMapRoiType.Polyline;
#endif

        // 选中段点数（单点示教需与之一致；由ViewModel在打开时写入）
        private int _targetPointCount;

        public ZMapExtractZWindow()
        {
            InitializeComponent();
            Vertices.CollectionChanged += Vertices_CollectionChanged;
#if HAS_HALCON
            // 设计时不加载Halcon原生库，避免设计器崩溃
            if (!System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            {
                _win = new VMHWindowControl();
                winFormHost.Child = _win;
                // 显示底部状态栏：鼠标悬停时以"Gray"直读像素高度值，便于示教Z基准点
                _win.showStatusBar();
                _win.hControl.MouseDown += OnHMouseDown;
                _win.hControl.MouseUp += OnHMouseUp;
            }
            Closed += (s, e) => { try { _win?.Dispose(); } catch { } };
#endif
        }

        /// <summary>由ViewModel写入选中段点数，用于单点示教提示与提取校验。</summary>
        public void SetTargetPointCount(int count)
        {
            _targetPointCount = count;
            UpdateVertexHint();
        }

        /// <summary>开始从HALCON画布拾取像素坐标；下一次左键单击完成后自动退出拾取模式。</summary>
        public void BeginPickCalibrationPixel(Action<double, double> onPicked)
        {
#if HAS_HALCON
            if (!_imageLoaded || _win == null) return;
            _pickCalibrationPixel = onPicked;
#endif
        }

        /// <summary>
        /// 显示已加载的高度图并按当前ROI类型生成默认交互ROI。
        /// himageObj为服务返回的装箱HImage（real单通道），null时忽略。
        /// </summary>
        public void ShowHeightMap(object himageObj, ZMapRoiType roiType)
        {
#if HAS_HALCON
            if (_win == null) return;
            _roiType = roiType;
            if (!(himageObj is HImage himage) || !himage.IsInitialized())
                return;
            _image = himage;
            _win.HobjectToHimage(himage);
            _imageLoaded = true;
            RestorePendingRoiDefinition();
            ApplyRoiType();
#endif
        }

        /// <summary>切换ROI类型时重新生成默认交互ROI（图像未加载则仅更新显隐）。</summary>
        public void SetRoiType(ZMapRoiType roiType)
        {
            _roiType = roiType;
#if HAS_HALCON
            if (_imageLoaded) { ApplyRoiType(); return; }
#endif
            IsVertexRoi = roiType == ZMapRoiType.Polyline || roiType == ZMapRoiType.SinglePoint;
            UpdateVertexHint();
        }

#if HAS_HALCON
        /// <summary>按当前ROI类型初始化交互ROI：直线/圆弧种默认几何，折线/单点种默认顶点并渲染。</summary>
        private void ApplyRoiType()
        {
            if (_win == null || !_imageLoaded) return;
            IsVertexRoi = _roiType == ZMapRoiType.Polyline || _roiType == ZMapRoiType.SinglePoint;
            try
            {
                double w = _win.hv_imageWidth;
                double h = _win.hv_imageHeight;
                if (w <= 0 || h <= 0) return;

                switch (_roiType)
                {
                    case ZMapRoiType.Line:
                        ResetInteractiveCanvas();
                        var linePoints = _pendingRoiDefinition?.ControlPoints;
                        _win.WindowH.genLine(RoiKey,
                            linePoints?.Count >= 2 ? linePoints[0].Row : h / 2,
                            linePoints?.Count >= 2 ? linePoints[0].Col : w / 4,
                            linePoints?.Count >= 2 ? linePoints[1].Row : h / 2,
                            linePoints?.Count >= 2 ? linePoints[1].Col : w * 3 / 4,
                            ref _roiList);
                        break;
                    case ZMapRoiType.CircularArc:
                        ResetInteractiveCanvas();
                        var arcPoints = _pendingRoiDefinition?.ControlPoints;
                        _win.WindowH.genCircleArr(RoiKey,
                            arcPoints?.Count >= 1 ? arcPoints[0].Row : h / 2,
                            arcPoints?.Count >= 1 ? arcPoints[0].Col : w / 2,
                            arcPoints?.Count >= 2 ? arcPoints[1].Col : Math.Min(w, h) / 4,
                            ref _roiList);
                        // 导出的第二/三控制点存储startPhi/extentPhi，恢复后保持弧段起止范围。
                        if (arcPoints?.Count >= 3 && _roiList.TryGetValue(RoiKey, out var savedArc) && savedArc is ROICircularArc arc)
                        {
                            arc.startPhi = arcPoints[1].Row;
                            arc.extentPhi = arcPoints[2].Col;
                            arc.startR = arc.midR - arc.radius * Math.Sin(arc.startPhi);
                            arc.startC = arc.midC + arc.radius * Math.Cos(arc.startPhi);
                            arc.extentR = arc.midR - arc.radius * Math.Sin(arc.startPhi + arc.extentPhi);
                            arc.extentC = arc.midC + arc.radius * Math.Cos(arc.startPhi + arc.extentPhi);
                        }
                        break;
                    case ZMapRoiType.Polyline:
                        if (_verticesForType != ZMapRoiType.Polyline || Vertices.Count < 2)
                            SeedDefaultVertices(w, h);
                        RenderVertexRoi();
                        break;
                    case ZMapRoiType.SinglePoint:
                        if (_verticesForType != ZMapRoiType.SinglePoint || Vertices.Count < 1)
                            SeedDefaultVertices(w, h);
                        RenderVertexRoi();
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ZMapWindow] ApplyRoiType 异常: {ex.Message}");
            }
            UpdateVertexHint();
        }

        /// <summary>按当前类型补种默认顶点（折线默认3点、单点默认1点，居中/四分之一位置）。</summary>
        private void SeedDefaultVertices(double w, double h)
        {
            _suppressRender = true;
            Vertices.Clear();
            if (_roiType == ZMapRoiType.Polyline)
            {
                Vertices.Add(new ZMapRoiVertex { Id = 0, Col = w / 4, Row = h / 2 });
                Vertices.Add(new ZMapRoiVertex { Id = 1, Col = w / 2, Row = h / 4 });
                Vertices.Add(new ZMapRoiVertex { Id = 2, Col = w * 3 / 4, Row = h / 2 });
            }
            else // SinglePoint
            {
                Vertices.Add(new ZMapRoiVertex { Id = 0, Col = w / 2, Row = h / 2 });
            }
            _verticesForType = _roiType;
            _suppressRender = false;
        }

        /// <summary>
        /// 渲染折线/单点：清屏重显图像→画骨架连线(折线)→生成可拖拽顶点ROI→标注编号。
        /// 顺序与Plugin.DispensePath一致，保证叠加层(连线/编号)在Repaint后仍保留、ROI可交互。
        /// </summary>
        private void RenderVertexRoi()
        {
            if (_win == null || !_imageLoaded) return;
            try
            {
                // 同时清空ROIController内部列表、窗口叠加层和本地镜像。
                // 仅ClearROI不会删除ROIController中的旧X手柄；删除顶点后它会继续被重绘，
                // 导致图像残留和多个点被错误高亮。
                ResetInteractiveCanvas();

                // 折线：顶点连线骨架（>=2点）。首尾点重合/近似重合时自动补首尾连线形成闭环。
                if (_roiType == ZMapRoiType.Polyline && Vertices.Count >= 2)
                {
                    var skeletonVertices = Vertices.ToList();
                    if (IsPolylineClosed())
                        skeletonVertices.Add(Vertices[0]);
                    HTuple rows = skeletonVertices.Select(v => v.Row).ToArray();
                    HTuple cols = skeletonVertices.Select(v => v.Col).ToArray();
                    HOperatorSet.GenContourPolygonXld(out HObject skeleton, rows, cols);
                    // DispHobject内部会复制一份存入叠加层，这里用完即释放本地对象
                    _win.WindowH.DispHobject(skeleton, "green");
                    skeleton.Dispose();
                }

                // 可拖拽顶点ROI（键为整数下标，便于拖拽后回写对应顶点）
                for (int i = 0; i < Vertices.Count; i++)
                    _win.WindowH.genPoint(i.ToString(), Vertices[i].Row, Vertices[i].Col, ref _roiList);

                // 顶点编号（HText.row/col语义为X/Y，故传Col/Row）。
                // 注意：拖拽时ROIController每帧Repaint会重绘全部叠加层，逐点SetMsg文字开销大导致卡顿，
                // 故默认关闭编号显示以保证拖拽流畅；如需编号可将 _showVertexNumbers 置true。
                if (_showVertexNumbers)
                {
                    for (int i = 0; i < Vertices.Count; i++)
                        _win.WindowH.DispText(new HText("yellow", i.ToString(), Vertices[i].Col, Vertices[i].Row, 16));
                }

                // 选中高亮走瞬态层，避免每次选中都重建全部点ROI与骨架。
                UpdateSelectedVertexHighlight();

                DrawCadPointProjectionOverlay();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ZMapWindow] RenderVertexRoi 异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 清空当前交互画布。ViewWindow.notDisplayRoi 会清空ROIController内部ROI，
        /// VMHWindowControl.ClearROI 会清空骨架/高亮等HObject叠加层并重新显示图像。
        /// 两步都需要，确保删除/重置/类型切换后没有旧轨迹或旧X手柄残留。
        /// </summary>
        private void ResetInteractiveCanvas()
        {
            _win.WindowH.notDisplayRoi();
            _win.ClearROI();
            _roiList.Clear();
        }

        /// <summary>
        /// 接收预览表中反查成功的像素点。点与CadPoint按预览Index对应，叠加层只用于可视化校验。
        /// </summary>
        public void SetCadPointProjection(IEnumerable<ZMapPixelPoint> pixels)
        {
            _cadPointProjectionPixels.Clear();
            if (pixels != null)
                _cadPointProjectionPixels.AddRange(pixels);
            // 新预览不能沿用上一轮表格行的高亮，确保预览点与表格Index从零开始一一对应。
            _selectedProjectionIndex = -1;
            // 新预览默认展示提取点，避免用户必须先找到开关而误认为未显示。
            ShowCadPointProjection = true;
            RefreshCadPointProjectionOverlay();
        }

        /// <summary>预览表选中行→图像高亮同Index提取点；无效点或取消选择则清除高亮。</summary>
        public void SelectCadPointProjection(int index)
        {
            _selectedProjectionIndex = index >= 0 && index < _cadPointProjectionPixels.Count ? index : -1;
            UpdateSelectedCadPointProjectionHighlight();
        }

        /// <summary>
        /// 根据开关切换“ROI编辑”和“仅显示提取结果”两种画面：
        /// 开启时隐藏原始ROI/顶点/骨架，仅显示等分后的新提取点和连线；
        /// 关闭时恢复此前的可编辑ROI。
        /// </summary>
        private void RefreshCadPointProjectionOverlay()
        {
#if HAS_HALCON
            if (!_imageLoaded || _win == null) return;

            if (_showCadPointProjection)
            {
                // 保存当前ROI，再清空ROIController与叠加层，确保结果画面不显示原始轨迹。
                _roiBeforeProjectionDisplay = ExportRoiDefinition();
                ResetInteractiveCanvas();
                DrawCadPointProjectionOverlay();
            }
            else
            {
                // 关闭结果显示：恢复原始ROI供继续拖拽编辑，不保留提取结果叠加层。
                _win.WindowH._hWndControl.SetTransientOverlay(null, null);
                if (_roiBeforeProjectionDisplay != null)
                    ImportRoiDefinition(_roiBeforeProjectionDisplay, (ZMapTeachDirection)TeachDirectionIndex, ReverseRoiDirection);
                else
                    ApplyRoiType();
            }
            UpdateSelectedCadPointProjectionHighlight();
#endif
        }

        /// <summary>
        /// 绘制等分后的提取结果：轨迹连线用黄色，点用青色X；
        /// ROI首尾闭合时轨迹补回起点连线（采样点本身不重复首点）。
        /// </summary>
        private void DrawCadPointProjectionOverlay()
        {
#if HAS_HALCON
            if (!_imageLoaded || !_showCadPointProjection || _cadPointProjectionPixels.Count == 0)
                return;
            try
            {
                // 先画轨迹连线，再画X，避免连线盖住点标记。
                if (_cadPointProjectionPixels.Count > 1)
                {
                    var trajectoryPixels = _cadPointProjectionPixels.ToList();
                    // 闭合ROI采样不重复首点，显示时需补回首点才能画出“末→首”闭合段。
                    if (_roiType == ZMapRoiType.Polyline && IsPolylineClosed())
                        trajectoryPixels.Add(_cadPointProjectionPixels[0]);

                    HTuple pathRows = trajectoryPixels.Select(p => p.Row).ToArray();
                    HTuple pathCols = trajectoryPixels.Select(p => p.Col).ToArray();
                    HOperatorSet.GenContourPolygonXld(out HObject trajectory, pathRows, pathCols);
                    _win.WindowH.DispHobject(trajectory, "yellow");
                    trajectory.Dispose();
                }

                // HALCON封装未暴露GenCross，以两条XLD对角线组合成X标记；
                // 所有X合并为一个HObject后一次加入叠加层，保证297点仍可流畅缩放/平移。
                HObject markers = null;
                const double markerSize = 4;
                foreach (var point in _cadPointProjectionPixels)
                {
                    HOperatorSet.GenContourPolygonXld(out HObject line1,
                        new[] { point.Row - markerSize, point.Row + markerSize },
                        new[] { point.Col - markerSize, point.Col + markerSize });
                    HOperatorSet.GenContourPolygonXld(out HObject line2,
                        new[] { point.Row - markerSize, point.Row + markerSize },
                        new[] { point.Col + markerSize, point.Col - markerSize });
                    HObject cross = line1.ConcatObj(line2);
                    line1.Dispose();
                    line2.Dispose();
                    if (markers == null)
                    {
                        markers = new HObject(cross);
                    }
                    else
                    {
                        HObject combined = markers.ConcatObj(cross);
                        markers.Dispose();
                        markers = combined;
                    }
                    cross.Dispose();
                }
                if (markers != null)
                {
                    _win.WindowH.DispHobject(markers, "cyan");
                    markers.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ZMapWindow] DrawCadPointProjectionOverlay 异常: {ex.Message}");
            }
#endif
        }

        /// <summary>
        /// 仅替换表格选中点的洋红大圆环。通过HWndCtrl的瞬态层更新，避免选中行时
        /// 清空并重新生成ROI、骨架以及全部青色提取点，保证表格快速连续选择的响应性。
        /// </summary>
        private void UpdateSelectedCadPointProjectionHighlight()
        {
#if HAS_HALCON
            if (!_imageLoaded || _win == null) return;
            if (_selectedProjectionIndex < 0 || !_showCadPointProjection)
            {
                _win.WindowH._hWndControl.SetTransientOverlay(null, null);
                return;
            }

            try
            {
                var selected = _cadPointProjectionPixels[_selectedProjectionIndex];
                HOperatorSet.GenCircle(out HObject highlight, selected.Row, selected.Col, 12);
                _win.WindowH._hWndControl.SetTransientOverlay(highlight, "magenta");
                highlight.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ZMapWindow] UpdateSelectedCadPointProjectionHighlight 异常: {ex.Message}");
            }
#endif
        }

        private void OnHMouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!_imageLoaded) return;
            try
            {
                // 标定像素拾取优先于ROI编辑，防止点击已有X手柄时意外移动轨迹。
                if (_pickCalibrationPixel != null)
                {
                    _win.hControl.HalconWindow.GetMpositionSubPix(out double pickRow, out double pickCol, out int pickButton);
                    if (pickButton == 1)
                    {
                        var callback = _pickCalibrationPixel;
                        _pickCalibrationPixel = null;
                        callback?.Invoke(pickCol, pickRow);
                    }
                    return;
                }
                if (!IsVertexRoi) return;
                _win.hControl.HalconWindow.GetMpositionSubPix(out double row, out double col, out int button);
                if (button != 1) return;

                // 按图像坐标选最近顶点（不依赖ActiveROI时序），同步表格选中行；拖拽仍由ROIController处理。
                int nearest = FindNearestVertexIndex(col, row, 20.0);
                if (nearest >= 0)
                {
                    _mouseDown = true;
                    SelectedVertex = Vertices[nearest];
                    return;
                }

                // 点击空白处：示教追加
                if (!TeachModeEnabled) return;
                AddVertexAt(col, row);
            }
            catch { }
        }

        /// <summary>在容差内查找距点击最近的顶点索引；未命中返回-1。</summary>
        private int FindNearestVertexIndex(double col, double row, double maxDist)
        {
            int best = -1;
            double bestDistSq = maxDist * maxDist;
            for (int i = 0; i < Vertices.Count; i++)
            {
                double dx = Vertices[i].Col - col;
                double dy = Vertices[i].Row - row;
                double distSq = dx * dx + dy * dy;
                if (distSq <= bestDistSq)
                {
                    bestDistSq = distSq;
                    best = i;
                }
            }
            return best;
        }

        private void OnHMouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!_mouseDown) return;
            _mouseDown = false;
            try
            {
                // 拖拽结束：从活动顶点ROI回写坐标到顶点集合并重绘
                var roi = _win.WindowH.smallestActiveROI(out string _, out string index);
                if (roi is ROIPoint p && int.TryParse(index, out int i) && i >= 0 && i < Vertices.Count)
                {
                    // MouseDown时ActiveROI可能尚未就绪；MouseUp再同步表格选中，保证图像→表双向联动。
                    if (!ReferenceEquals(SelectedVertex, Vertices[i]))
                        SelectedVertex = Vertices[i];

                    _suppressRender = true;
                    Vertices[i].Col = Math.Round(p.midC, 3);
                    Vertices[i].Row = Math.Round(p.midR, 3);
                    _suppressRender = false;
                }
                RenderVertexRoi();
                // 拖拽后重新判断首尾是否进入闭合容差，提示与骨架同步刷新。
                UpdateVertexHint();
            }
            catch { }
        }

        /// <summary>按示教方向在指定像素位置追加顶点（折线支持首/尾，单点始终追加）。</summary>
        private void AddVertexAt(double col, double row)
        {
            var v = new ZMapRoiVertex { Col = Math.Round(col, 3), Row = Math.Round(row, 3) };
            bool insertHead = false;
            if (_roiType == ZMapRoiType.Polyline)
            {
                var dir = (ZMapTeachDirection)TeachDirectionIndex;
                if (dir == ZMapTeachDirection.Head) insertHead = true;
                else if (dir == ZMapTeachDirection.Tail) insertHead = false;
                else insertHead = SelectedVertex != null && Vertices.IndexOf(SelectedVertex) == 0;
            }
            _suppressRender = true;
            if (insertHead && Vertices.Count > 0) Vertices.Insert(0, v);
            else Vertices.Add(v);
            Renumber();
            _suppressRender = false;
            RenderVertexRoi();
            UpdateVertexHint();
        }
#endif

        private void Vertices_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (ZMapRoiVertex v in e.NewItems)
                    v.PropertyChanged += Vertex_PropertyChanged;
            if (e.OldItems != null)
                foreach (ZMapRoiVertex v in e.OldItems)
                    v.PropertyChanged -= Vertex_PropertyChanged;
        }

        /// <summary>表格编辑顶点坐标后同步刷新图像ROI（拖拽/示教回写时以_suppressRender抑制）。</summary>
        private void Vertex_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
#if HAS_HALCON
            if (_suppressRender) return;
            if (e.PropertyName != nameof(ZMapRoiVertex.Col) && e.PropertyName != nameof(ZMapRoiVertex.Row)) return;
            if (_imageLoaded && IsVertexRoi) RenderVertexRoi();
#endif
        }

        private void Renumber()
        {
            for (int i = 0; i < Vertices.Count; i++)
                Vertices[i].Id = i;
        }

        /// <summary>
        /// 表格/图像选中顶点变化：只刷新瞬态高亮圆环，不重建全部点轮廓与骨架。
        /// 同时滚动Vertices表到对应行，保证图像→表格联动可见。
        /// </summary>
        private void OnSelectedVertexChanged()
        {
#if HAS_HALCON
            if (_imageLoaded && IsVertexRoi && !_showCadPointProjection && !_suppressRender)
                UpdateSelectedVertexHighlight();
#endif
            ScrollSelectedVertexIntoView();
        }

        /// <summary>将Vertices表滚动并聚焦到当前选中行（图像点选后表格自动高亮）。</summary>
        private void ScrollSelectedVertexIntoView()
        {
            if (VerticesDataGrid == null || SelectedVertex == null) return;
            // BeginInvoke：等绑定完成SelectedItem后再滚动，避免选中行仍在虚拟化可视区外。
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    VerticesDataGrid.UpdateLayout();
                    VerticesDataGrid.ScrollIntoView(SelectedVertex);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ZMapWindow] ScrollSelectedVertexIntoView 异常: {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// 仅替换当前选中顶点的洋红高亮。通过HWndCtrl瞬态层更新，
        /// 避免表格连续选行时清空并重建全部折线点ROI与骨架。
        /// </summary>
        private void UpdateSelectedVertexHighlight()
        {
#if HAS_HALCON
            if (!_imageLoaded || _win == null) return;
            // 提取结果显示模式由投影高亮占用瞬态层，不与顶点高亮混用。
            if (_showCadPointProjection || !IsVertexRoi)
                return;

            if (SelectedVertex == null)
            {
                _win.WindowH._hWndControl.SetTransientOverlay(null, null);
                return;
            }

            int si = Vertices.IndexOf(SelectedVertex);
            if (si < 0)
            {
                _win.WindowH._hWndControl.SetTransientOverlay(null, null);
                return;
            }

            try
            {
                HOperatorSet.GenCircle(out HObject highlight, Vertices[si].Row, Vertices[si].Col, 12);
                _win.WindowH._hWndControl.SetTransientOverlay(highlight, "magenta");
                highlight.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ZMapWindow] UpdateSelectedVertexHighlight 异常: {ex.Message}");
            }
#endif
        }

        #region 示教工具栏按钮（XAML Click）

        /// <summary>追加一个顶点/单点（默认在中心或上一个点右侧偏移，便于随后拖拽/编辑）。</summary>
        private void AddVertex_Click(object sender, RoutedEventArgs e)
        {
#if HAS_HALCON
            if (!_imageLoaded || !IsVertexRoi) return;
            double x = _win.hv_imageWidth / 2.0;
            double y = _win.hv_imageHeight / 2.0;
            if (Vertices.Count > 0) { x = Vertices[Vertices.Count - 1].Col + 30; y = Vertices[Vertices.Count - 1].Row; }
            _suppressRender = true;
            Vertices.Add(new ZMapRoiVertex { Col = Math.Round(x, 3), Row = Math.Round(y, 3) });
            Renumber();
            _suppressRender = false;
            RenderVertexRoi();
            UpdateVertexHint();
#endif
        }

        /// <summary>在选中顶点与其后继之间插入中点（仅折线，需选中非末点）。</summary>
        private void InsertVertex_Click(object sender, RoutedEventArgs e)
        {
#if HAS_HALCON
            if (!_imageLoaded || _roiType != ZMapRoiType.Polyline || SelectedVertex == null) return;
            int idx = Vertices.IndexOf(SelectedVertex);
            if (idx < 0 || idx >= Vertices.Count - 1) return;
            var a = Vertices[idx];
            var b = Vertices[idx + 1];
            _suppressRender = true;
            Vertices.Insert(idx + 1, new ZMapRoiVertex
            {
                Col = Math.Round((a.Col + b.Col) / 2, 3),
                Row = Math.Round((a.Row + b.Row) / 2, 3)
            });
            Renumber();
            _suppressRender = false;
            RenderVertexRoi();
            UpdateVertexHint();
#endif
        }

        /// <summary>
        /// 将选中顶点设为ROI起点。闭合折线循环旋转顶点；开放折线仅允许选首/末端，
        /// 选末端时反转顶点顺序，避免中间点作为起点造成路径断裂。
        /// </summary>
        private void SetStartVertex_Click(object sender, RoutedEventArgs e)
        {
#if HAS_HALCON
            if (!_imageLoaded || _roiType != ZMapRoiType.Polyline || SelectedVertex == null) return;
            int index = Vertices.IndexOf(SelectedVertex);
            if (index < 0) return;
            bool closed = IsPolylineClosed();
            if (!closed && index != 0 && index != Vertices.Count - 1)
                return;

            _suppressRender = true;
            var ordered = Vertices.ToList();
            if (closed)
            {
                ordered = ordered.Skip(index).Concat(ordered.Take(index)).ToList();
            }
            else if (index == Vertices.Count - 1)
            {
                ordered.Reverse();
            }
            Vertices.Clear();
            foreach (var vertex in ordered)
                Vertices.Add(vertex);
            Renumber();
            SelectedVertex = Vertices[0];
            _suppressRender = false;
            RenderVertexRoi();
#endif
        }

        /// <summary>删除选中顶点（折线保留至少2点、单点保留至少1点）。</summary>
        private void DeleteVertex_Click(object sender, RoutedEventArgs e)
        {
#if HAS_HALCON
            if (!_imageLoaded || SelectedVertex == null) return;
            int min = _roiType == ZMapRoiType.Polyline ? 2 : 1;
            if (Vertices.Count <= min) return;
            int idx = Vertices.IndexOf(SelectedVertex);
            if (idx < 0) return;
            _suppressRender = true;
            // 被删除的对象不能继续作为选中项，否则旧高亮会在下一轮重绘中保留。
            SelectedVertex = null;
            Vertices.RemoveAt(idx);
            Renumber();
            _suppressRender = false;
            RenderVertexRoi();
            UpdateVertexHint();
#endif
        }

        /// <summary>清空所有顶点并补种默认顶点（仅影响本窗口ROI，不改动选中段数据）。</summary>
        private void DeleteAllVertices_Click(object sender, RoutedEventArgs e)
        {
#if HAS_HALCON
            if (!_imageLoaded || !IsVertexRoi) return;
            SeedDefaultVertices(_win.hv_imageWidth, _win.hv_imageHeight);
            RenderVertexRoi();
            UpdateVertexHint();
#endif
        }

        #endregion

        /// <summary>
        /// 按选中线段点数对当前ROI采样，确保提取结果与CadPoint严格一一对应。
        /// 直线/圆弧沿ROI等距采样；折线按累计弧长等距重采样；单点直接返回示教点。
        /// 返回图像像素坐标(Col=列, Row=行)。
        /// </summary>
        public IReadOnlyList<ZMapPixelPoint> GetRoiSamplePoints(int pointCount)
        {
            var points = new List<ZMapPixelPoint>();
#if HAS_HALCON
            if (!_imageLoaded || _win == null)
                return points;
            try
            {
                switch (_roiType)
                {
                    case ZMapRoiType.Line:
                        if (pointCount >= 1) SampleLine(pointCount, points);
                        break;
                    case ZMapRoiType.CircularArc:
                        if (pointCount >= 1) SampleArc(pointCount, points);
                        break;
                    case ZMapRoiType.Polyline:
                        if (pointCount >= 1) SamplePolyline(pointCount, points);
                        break;
                    case ZMapRoiType.SinglePoint:
                        // 单点示教：每个示教点即一个输出点，直接返回（数量由ViewModel校验须等于选中段点数）
                        foreach (var v in Vertices)
                            points.Add(new ZMapPixelPoint { Col = v.Col, Row = v.Row });
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ZMapWindow] GetRoiSamplePoints 异常: {ex.Message}");
            }
#endif
            return points;
        }

#if HAS_HALCON
        /// <summary>直线ROI：起点→终点线性等距采样。</summary>
        private void SampleLine(int count, List<ZMapPixelPoint> points)
        {
            if (!_roiList.TryGetValue(RoiKey, out var roi) || !(roi is ROILine line))
                return;
            for (int i = 0; i < count; i++)
            {
                double t = count == 1 ? 0 : (double)i / (count - 1);
                if (ReverseRoiDirection) t = 1 - t;
                points.Add(new ZMapPixelPoint
                {
                    Col = line.StartX + t * (line.EndX - line.StartX),
                    Row = line.StartY + t * (line.EndY - line.StartY)
                });
            }
        }

        /// <summary>圆弧ROI：沿 startPhi→startPhi+extentPhi 角度等距采样。</summary>
        private void SampleArc(int count, List<ZMapPixelPoint> points)
        {
            if (!_roiList.TryGetValue(RoiKey, out var roi) || !(roi is ROICircularArc arc))
                return;
            for (int i = 0; i < count; i++)
            {
                double t = count == 1 ? 0 : (double)i / (count - 1);
                if (ReverseRoiDirection) t = 1 - t;
                double phi = arc.startPhi + arc.extentPhi * t;
                // 与ROICircularArc句柄一致：row=midR-r*sin(phi), col=midC+r*cos(phi)
                points.Add(new ZMapPixelPoint
                {
                    Col = arc.midC + arc.radius * Math.Cos(phi),
                    Row = arc.midR - arc.radius * Math.Sin(phi)
                });
            }
        }

        /// <summary>折线ROI：按顶点连线的累计弧长等距重采样到目标点数。</summary>
        private void SamplePolyline(int count, List<ZMapPixelPoint> points)
        {
            var verts = Vertices.Select(v => (col: v.Col, row: v.Row)).ToList();
            if (ReverseRoiDirection)
                verts.Reverse();
            if (verts.Count == 0) return;
            if (verts.Count == 1)
            {
                for (int i = 0; i < count; i++)
                    points.Add(new ZMapPixelPoint { Col = verts[0].col, Row = verts[0].row });
                return;
            }

            // 首尾重合时闭合：采样包含最后一段“末点→首点”，且不重复输出起点。
            bool isClosed = IsPolylineClosed();
            if (isClosed && verts.Count > 2)
                verts.Add(verts[0]);

            int segCount = verts.Count - 1;
            double[] segLen = new double[segCount];
            double total = 0;
            for (int i = 0; i < segCount; i++)
            {
                double dx = verts[i + 1].col - verts[i].col;
                double dy = verts[i + 1].row - verts[i].row;
                segLen[i] = Math.Sqrt(dx * dx + dy * dy);
                total += segLen[i];
            }
            if (total <= 1e-9)
            {
                for (int i = 0; i < count; i++)
                    points.Add(new ZMapPixelPoint { Col = verts[0].col, Row = verts[0].row });
                return;
            }

            for (int i = 0; i < count; i++)
            {
                // 闭合轨迹使用count作分母，最后一点不重复回到第一个点；开放轨迹保留首尾两端。
                double t = count == 1 ? 0 : (double)i / (isClosed ? count : count - 1);
                double target = t * total;
                double acc = 0;
                int seg = 0;
                while (seg < segCount - 1 && acc + segLen[seg] < target)
                {
                    acc += segLen[seg];
                    seg++;
                }
                double local = segLen[seg] > 1e-9 ? (target - acc) / segLen[seg] : 0;
                points.Add(new ZMapPixelPoint
                {
                    Col = verts[seg].col + local * (verts[seg + 1].col - verts[seg].col),
                    Row = verts[seg].row + local * (verts[seg + 1].row - verts[seg].row)
                });
            }
        }

        /// <summary>判断折线首尾是否重合（允许像素级容差），用于骨架显示及闭环弧长采样。</summary>
        private bool IsPolylineClosed()
        {
            if (Vertices.Count < 3) return false;
            var first = Vertices[0];
            var last = Vertices[Vertices.Count - 1];
            double dx = first.Col - last.Col;
            double dy = first.Row - last.Row;
            return dx * dx + dy * dy <= ClosedPolylineTolerancePixel * ClosedPolylineTolerancePixel;
        }
#endif

        /// <summary>是否已加载图像并存在可采样的ROI。</summary>
        public bool IsRoiComplete
        {
            get
            {
#if HAS_HALCON
                if (!_imageLoaded) return false;
                switch (_roiType)
                {
                    case ZMapRoiType.Polyline: return Vertices.Count >= 2;
                    case ZMapRoiType.SinglePoint: return Vertices.Count >= 1;
                    default: return _roiList.ContainsKey(RoiKey);
                }
#else
                return false;
#endif
            }
        }

        /// <summary>清除ROI仅影响本悬浮窗口，不修改选中线段数据；随后补种默认ROI便于重新框选。</summary>
        private void ClearRoi_Click(object sender, RoutedEventArgs e)
        {
            ClearRoi();
        }

        /// <summary>重置ROI：清空旧轨迹（顶点/交互ROI/叠加层）并重种默认ROI（不改变已加载图像）。</summary>
        public void ClearRoi()
        {
#if HAS_HALCON
            if (!_imageLoaded) return;
            // 先取消选中，避免高亮残留；再强制清空当前交互ROI与叠加层
            _suppressRender = true;
            SelectedVertex = null;
            _suppressRender = false;
            ResetInteractiveCanvas();
            if (IsVertexRoi)
            {
                // 顶点类：清掉旧顶点后按类型重种默认顶点（SeedDefaultVertices内部会重置_verticesForType）
                SeedDefaultVertices(_win.hv_imageWidth, _win.hv_imageHeight);
            }
            ApplyRoiType();
#endif
        }

        /// <summary>更新折线/单点提示文本（折线说明重采样与是否已自动闭合）。</summary>
        private void UpdateVertexHint()
        {
            if (_roiType == ZMapRoiType.SinglePoint)
                VertexHint = string.Format(L("ZMap_Hint_SinglePoint"), _targetPointCount, Vertices.Count);
            else if (_roiType == ZMapRoiType.Polyline)
            {
                string baseHint = string.Format(L("ZMap_Hint_Polyline"), _targetPointCount);
#if HAS_HALCON
                if (IsPolylineClosed())
                    VertexHint = baseHint + " " + L("ZMap_Hint_PolylineClosed");
                else
#endif
                    VertexHint = baseHint;
            }
            else
                VertexHint = string.Empty;
        }

        /// <summary>导出当前ROI为可JSON序列化定义，供所属DispenseSegment持久化。</summary>
        public ZMapRoiDefinition ExportRoiDefinition()
        {
            var result = new ZMapRoiDefinition { Type = _roiType };
#if HAS_HALCON
            if (_roiType == ZMapRoiType.Polyline || _roiType == ZMapRoiType.SinglePoint)
            {
                result.ControlPoints = Vertices.Select(v => new ZMapPixelPoint { Col = v.Col, Row = v.Row }).ToList();
            }
            else if (_roiType == ZMapRoiType.Line && _roiList.TryGetValue(RoiKey, out var lineRoi) && lineRoi is ROILine line)
            {
                result.ControlPoints.Add(new ZMapPixelPoint { Col = line.StartX, Row = line.StartY });
                result.ControlPoints.Add(new ZMapPixelPoint { Col = line.EndX, Row = line.EndY });
            }
            else if (_roiType == ZMapRoiType.CircularArc && _roiList.TryGetValue(RoiKey, out var arcRoi) && arcRoi is ROICircularArc arc)
            {
                result.ControlPoints.Add(new ZMapPixelPoint { Col = arc.midC, Row = arc.midR });
                result.ControlPoints.Add(new ZMapPixelPoint { Col = arc.radius, Row = arc.startPhi });
                result.ControlPoints.Add(new ZMapPixelPoint { Col = arc.extentPhi, Row = 0 });
            }
#endif
            return result;
        }

        /// <summary>导入段级ROI定义。图像未加载时暂存，加载后会按默认ROI初始化。</summary>
        public void ImportRoiDefinition(ZMapRoiDefinition definition, ZMapTeachDirection teachDirection, bool reverseRoiDirection = false)
        {
            if (definition == null) return;
            _roiType = definition.Type;
            TeachDirectionIndex = (int)teachDirection;
            ReverseRoiDirection = reverseRoiDirection;
            _pendingRoiDefinition = definition;
#if HAS_HALCON
            if (_imageLoaded)
            {
                RestorePendingRoiDefinition();
                ApplyRoiType();
            }
#endif
        }

        /// <summary>将暂存的段级折线/单点顶点恢复到交互集合；直线/圆弧由默认ROI恢复后续可继续编辑。</summary>
        private void RestorePendingRoiDefinition()
        {
#if HAS_HALCON
            if (_pendingRoiDefinition == null) return;
            if (_pendingRoiDefinition.Type == ZMapRoiType.Polyline || _pendingRoiDefinition.Type == ZMapRoiType.SinglePoint)
            {
                _suppressRender = true;
                Vertices.Clear();
                foreach (var point in _pendingRoiDefinition.ControlPoints ?? new List<ZMapPixelPoint>())
                    Vertices.Add(new ZMapRoiVertex { Id = Vertices.Count, Col = point.Col, Row = point.Row });
                _verticesForType = _pendingRoiDefinition.Type;
                _suppressRender = false;
            }
#endif
        }

        private static string L(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            var resource = Application.Current?.TryFindResource(key);
            return resource?.ToString() ?? $"[{key}]";
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
