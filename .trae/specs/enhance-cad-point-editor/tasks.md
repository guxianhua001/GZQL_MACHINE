# Tasks — CadPointEditorView 增强实现

## 阶段一：Core 数据模型层（跨项目复用，零 UI 依赖）

- [ ] **T1: 创建 CAD 图元数据模型（Core/Models/）**
  - [ ] T1.1 创建 `Core/Models/CadEntityType.cs`：枚举 Line / Arc / Circle / LwPolyline / Ellipse
  - [ ] T1.2 创建 `Core/Models/CadEntity.cs`：基类（Id, LayerName, EntityType, Color, IsSelected, IsVisible），继承 Prism.Mvvm.BindableBase
  - [ ] T1.3 创建具体图元类：
    - `CadLine`：StartPoint(X,Y,Z), EndPoint(X,Y,Z)
    - `CadArc`：Center(X,Y,Z), Radius, StartAngle, EndAngle（逆时针）
    - `CadCircle`：Center(X,Y,Z), Radius
    - `CadLwPolyline`：Vertices(List\<PointF\>), IsClosed, Width
    - `CadEllipse`：Center, MajorAxis(Vector), MinorAxis(Vector), StartAngle, EndAngle, Rotation
  - [ ] T1.4 创建 `Core/Models/DxfParseResult.cs`：Layers(Dictionary\<string,List\<CadEntity\>\>), Extents(BoundingBox), ParseWarnings(List\<string\>)
  - [ ] T1.5 创建 `Core/Models/BoundingBox.cs`：MinX/MaxX/MinY/MaxY + Contains(Point) / Union(BoundingBox) / ExpandToInclude 方法

- [ ] **T2: 创建分段轨迹与坐标模型（Core/Models/）**
  - [ ] T2.1 扩展 `Core/Models/CadPoint.cs`：添加 MachineX/MachineY/MachineZ 属性（double? 可空，未对齐时为 null）
  - [ ] T2.2 创建 `Core/Models/DispenseSegment.cs`：
    - SegmentId, EntityType, SourceEntity(CadEntity), Points(List\<CadPoint\>), IsEnabled, LayerName, Length(只读计算)
    - 工艺参数全部 BindableBase 属性：MoveSpeed(default 10), DispenseAmount(default 1), PreDelay(default 50), PostDelay(default 50), CornerDecel(default 0.8), ZHeight(default 0), SafeHeight(default 5)
  - [ ] T2.3 创建 `Core/Models/CoordinateTransform.cs`：Tx/Ty/Tz, RotationAngle, Scale; Transform(CadPoint)→CadPoint, InverseTransform(CadPoint)→CadPoint; 支持构造仿射变换矩阵 3×3
  - [ ] T2.4 创建 `Core/Models/RoiRegion.cs`：
    - RoiType 枚举：Line / Polyline / Arc / Freehand
    - 各类型参数属性
    - SamplePoints(double pitchMM) → List\<CadPoint\> 抽象方法（具体算法由 RoiToolService 实现）

## 阶段二：Core 服务层（纯 C# 逻辑，可单元测试）

- [ ] **T3: 实现 DXF 解析服务（Core/Services/）**
  - [ ] T3.1 创建 `Core/Services/IDxfParserService.cs` 接口：
    - `DxfParseResult Parse(string filePath)`
    - `List<CadPoint> Discretize(CadEntity entity, double pitchMM)`
    - `List<CadPoint> DiscretizeAll(List<CadEntity> entities, double pitchMM)`
  - [ ] T3.2 创建 `Core/Services/DxfParserService.cs` 实现：
    - DXF 文本解析器：逐行读取 ENTITIES 段，按组码对解析实体
    - LINE 解析：组码 10/20/30 起点 → 11/21/31 终点
    - ARC 解析：组码 10/20/30 圆心 → 40 半径 → 50 起始角 → 51 终止角
    - CIRCLE 解析：组码 10/20/30 圆心 → 40 半径
    - LWPOLYLINE 解析：组码 10/20/30 顶点序列（含凸包标志组码 70 判断闭合）+ 组码 43 线宽
    - ELLIPSE 解析：组码 10/20/30 中心 → 11/21/31 长轴端点 → 40 长短比 → 50 起始角 → 51 终止角
    - 图层识别：每个实体的组码 8 值作为 LayerName
    - 全局 Extents 计算：遍历所有实体取包围盒并集
    - Discretize 离散化实现：
      - Line → 线性插值（length/pitch 个点）
      - Arc/Circle → 角度等分（考虑起止角方向，逆时针为正）
      - Ellipse → 参数方程 t∈[0,1] 等分采样
      - LwPolyline → 各子段分别离散化后拼接
  - [ ] T3.3 原 `Module/Services/DxfParser.cs` 的 ExtractPoints 方法保留 `[Obsolete]` 兼容包装或删除

- [ ] **T4: 实现 ROI 工具服务（Core/Services/）**
  - [ ] T4.1 创建 `Core/Services/IRoiToolService.cs` 接口：
    - `RoiRegion CreateLineRoi(PointF start, PointF end)`
    - `RoiRegion CreatePolylineRoi(List<PointF> vertices)`
    - `RoiRegion CreateArcRoi(PointF center, double radius, double startAngle, double endAngle)`
    - `RoiRegion CreateFreehandRoi(List<PointF> rawPoints)`
    - `List<CadPoint> SamplePoints(RoiRegion roi, double pitchMM)`
  - [ ] T4.2 创建 `Core/Services/RoiToolService.cs` 实现：
    - LineRoi 采样：两点间线性插值，间距 = pitchMM
    - PolylineRoi 采样：各段分别线性插值后拼接
    - ArcRoi 采样：角度等分，x=cx+r·cos(θ), y=cy+r·sin(θ)
    - FreehandRoi 采样：先计算累积弦长重采样，再可选贝塞尔平滑

- [ ] **T5: 实现坐标对齐服务（Core/Services/）**
  - [ ] T5.1 创建 `Core/Services/ICoordinateAlignService.cs` 接口：
    - AlignMode 枚举：FirstPoint / AllPoints
    - `void SetMode(AlignMode mode)`
    - `void SetMapFiducial(double x, double y, double z)`
    - `void SetMachineFiducial(double x, double y, double z, double rx, double rz)`
    - `void AutoCalculate()` — Mode1: 用 fiducial 偏移计算所有点的 Machine 坐标
    - `void SetPointMapping(string pointId, double mx, double my, double mz)` — Mode2
    - `CadPoint TransformToMachine(CadPoint cadPoint)`
    - `CoordinateTransform GetTransform()`
  - [ ] T5.2 创建 `Core/Services/CoordinateAlignService.cs` 实现：
    - Mode1 内部维护 CoordinateTransform 对象，AutoCalculate 时遍历所有已注册的 CadPoint 应用变换
    - Mode2 内部维护 Dictionary\<string, (mx,my,mz)\> 映射表
    - TransformToMachine 根据当前模式选择计算方式

## 阶段三：Module 可视化控件层（WPF/SkiaSharp）

- [ ] **T6: 创建 SkiaCanvasControl 独立控件（Module/Controls/）**
  - [ ] T6.1 创建 `Module/Controls/SkiaCanvasControl.xaml`：UserControl，内嵌 `<skia:SKElement x:Name="SkiaElement" PaintSurface="OnPaintSurface" />`
  - [ ] T6.2 创建 `Module/Controls/SkiaCanvasControl.xaml.cs`：
    - DependencyProperty 定义：Entities(Obs\<CadEntity\>), SelectedEntity(CadEntity), ZoomFactor(double), ShowGrid(bool), CurrentRoiPreview(RoiRegion)
    - CLR Event 定义：CoordinateChanged(Action\<double,double\>), EntitySelected(Action\<CadEntity\>), EntityDoubleClicked(Action\<CadEntity\>)
    - PaintSurface 渲染逻辑（SKCanvas）：
      1. 白色背景 + 可选网格（ShowGrid）
      2. 按 Z-order 绘制非选中图元（图层着色：BASE_FRAME=灰, DISPENSE_GLUE=蓝）
      3. 绘制选中图元（高亮色 + 加粗线宽）
      4. 绘制 ROI 预览（绿色虚线）
      5. 绘制坐标标尺（可选）
    - 鼠标事件处理：
      - Wheel → ZoomFactor 变更（以鼠标位置为中心缩放）
      - MiddleButtonDown/Move → PanOffset 更新（平移）
      - LeftButtonUp → 命中测试选中最顶层图元 → 触发 EntitySelected
      - Ctrl+LeftButton 拖拽 → 框选多图元
      - DoubleClick → 触发 EntityDoubleClicked
      - MouseMove → 坐标转换后触发 CoordinateChanged
    - CAD↔Screen 坐标转换方法（私有）：ToScreen(x,y) / ToCad(screenX,screenY)，含 Y 翻转 + 缩放 + 平移
  - [ ] T6.3 创建 `Module/ViewModels/SkiaCanvasViewModel.cs`：
    - 管理 Entities/Obs\<CadEntity\>, SelectedEntity, ZoomFactor, PanOffsetX/Y, ShowGrid, ShowRuler
    - 提供 ResetView/FitToAll/ZoomIn/ZoomOut/PanTo 命令
    - FitToAll 逻辑：根据 Entities 计算 BoundingBox → 自动设置 ZoomFactor 和 PanOffset 使图形居中适配

- [ ] **T7: 创建 CadPointEditorControl 独立编辑器控件（Module/Controls/）**
  - [ ] T7.1 创建 `Module/Controls/CadPointEditorControl.xaml`：
    - UserControl，整体布局为 Grid 两列（左画布 65% / 右面板 35%）
    - 顶部：Step Indicator 操作流程引导条（6 步：导入→确认→编辑→对齐→预览→执行）
    - 左侧区域：嵌入 SkiaCanvasControl + 底部工具栏（图层过滤 ComboBox + ROI 工具 ToggleButton 组 + 缩放按钮 + 坐标状态 TextBlock）
    - 右侧区域：ContentControl 根据 CurrentStep 切换不同面板内容
      - Step1 面板：文件选择按钮 + 文件路径显示 + 导入按钮
      - Step2 面板：图层勾选列表 CheckBoxList + 轨迹段数摘要
      - Step3 面板：轨迹段 DataGrid + 选中段参数编辑 Expander + 批量操作按钮
      - Step4 面板：Mode 切换 RadioButtons + MapFiducial 输入+Teach + MachineFiducial 显示 + AutoCalculate
      - Step5 面板：DryRun 按钮 + 仿真状态显示
      - Step6 面板：Site 选择 + Z校正 + Execute 按钮 + 安全警告提示
    - 底部全局状态栏
  - [ ] T7.2 创建 `Module/Controls/CadPointEditorControl.xaml.cs`：
    - DependencyProperty：Segments(Obs\<DispenseSegment\> — 输出绑定用), CurrentStep(int), FilePath(string)
    - RoutedEvent：ExecuteRequestEvent（外部宿主可监听执行请求并自行处理运动控制）
    - 内部持有 SkiaCanvasControl 引用和 CadPointEditorViewModel 引用
  - [ ] T7.3 创建/重构 `Module/ViewModels/CadPointEditorViewModel.cs`：
    - **精简职责**：步骤流转管理（CurrentStep、StepStatus[]、GoNext/GoPrev/GoToStep）、面板数据准备、命令分发到 Core 服务
    - 注入 IDxfParserService, IRoiToolService, ICoordinateAlignService（来自 Core）
    - 注入 IDispenseExecuteService（Module 特有，可为 null — 控件在无运动卡环境也能运行编辑功能）
    - ImportDxfCommand → 调用 _dxfParser.Parse() → 构建 DispenseSegment 列表 → 赋值给 Segments → 传给画布 Entities
    - 步骤完成自动检测（如 Step1 导入成功后自动建议进入 Step2）
    - ROI 工具命令 → 设置 SkiaCanvasControl 的绘制模式 → 确认后调用 _roiTool.SamplePoints() → 追加新 Segment

## 阶段四：Module 点胶执行服务（依赖 IMotionService）

- [ ] **T8: 实现点胶执行服务（Module/Services/）**
  - [ ] T8.1 创建 `Module/Services/IDispenseExecuteService.cs` 接口：
    - `Task DryRunAsync(IEnumerable<DispenseSegment> segments, CancellationToken token)`
    - `Task ExecutePathAsync(IEnumerable<DispenseSegment> segments, string site, CancellationToken token)`
    - `Task ExecuteSinglePointAsync(CadPoint point, CancellationToken token)`
    - `event Action<string, int, int> ProgressChanged` // (statusText, currentSegIndex, totalSegs)
    - `event Action<string> StatusChanged` // "Running" | "Paused" | "Completed" | "Error"
  - [ ] T8.2 创建 `Module/Services/DispenseExecuteService.cs` 实现：
    - 注入 IMotionService（运动控制）、ILoggerService
    - ExecutePathAsync 核心循环：
      ```
      foreach (var seg in enabledSegments):
        1. await _motion.MoveAbsAsync(AxisDz1, seg.SafeHeight, vel)   // Z 到安全高度
        2. await _motion.MoveLineAbsAsync(coord, [Dx,Dy], [startX,startY], vel)  // XY 到起点
        3. await _motion.MoveAbsAsync(AxisDz1, seg.ZHeight, vel)     // Z 下降到点胶高度
        4. await Task.Delay(seg.PreDelay, token)                       // 起点延时
        5. _motion.WriteDo(GlueIoPort, true)                          // 开胶
        6. foreach (point in seg.Points):                              // 逐点插补
             await _motion.MoveLineAbsAsync(coord, [Dx,Dy], [point.MachineX, point.MachineY], seg.MoveSpeed)
        7. _motion.WriteDo(GlueIoPort, false)                         // 关胶
        8. await Task.Delay(seg.PostDelay, token)                      // 终点延时
        9. await _motion.MoveAbsAsync(AxisDz1, seg.SafeHeight, vel)   // Z 回安全高度
      ```
    - 发布 ProgressChanged 事件供 UI 更新进度条

## 阶段五：集成与薄包装

- [ ] **T9: DI 注册与原有 View 适配**
  - [ ] T9.1 在 Module 项目中注册 Core 服务到 Prism DI 容器（singleton）：
    - `containerRegistry.RegisterSingleton<IDxfParserService, DxfParserService>()`
    - `containerRegistry.RegisterSingleton<IRoiToolService, RoiToolService>()`
    - `containerRegistry.RegisterSingleton<ICoordinateAlignService, CoordinateAlignService>()`
  - [ ] T9.2 注册 Module 服务（transient 或 singleton）：
    - `containerRegistry.Register<IDispenseExecuteService, DispenseExecuteService>()`
  - [ ] T9.3 修改原 `Module/WorkStation/Dispense/CadPointEditorView.xaml`：将原有 StackPanel 内容替换为 `<controls:CadPointEditorControl />`，保留项目特定的外围布局上下文
  - [ ] T9.4 确保 Core.csproj 无需新增 WPF/SkiaSharp 依赖（纯 C# 项目不变）；Module.csproj 已有 SkiaSharp 引用无需变更

- [ ] **T10: 编译验证**
  - [ ] T10.1 全项目编译通过（Release），零 error
  - [ ] T10.2 Core 项目可独立编译（无 Module 依赖）
  - [ ] T10.3 手动验证基本流程：打开 DXF → 画布显示 → 点击弧线选中 → 右侧面板显示参数

# Task Dependencies

- [T2] depends on [T1] — 分段轨迹模型依赖 CadEntity 基类和 CadPoint 扩展
- [T3] depends on [T1] — DXF 解析返回 CadEntity 模型
- [T4] depends on [T2] — ROI 服务使用 RoiRegion 和 CadPoint
- [T5] depends on [T2] — 对齐服务使用 CoordinateTransform 和 CadPoint
- [T6] depends on [T1] — 画布控件渲染 CadEntity
- [T7] depends on [T3, T4, T5, T6] — 编辑器控件组合所有服务和画布
- [T8] depends on [T2, T5] — 执行服务使用 DispenseSegment 和坐标变换结果
- [T9] depends on [T3, T4, T5, T7, T8] — DI 注册所有服务接口和实现
- [T10] depends on [T7, T9] — 全部整合后编译验证

**可并行执行的独立任务组**：
- Group A (无依赖): T1, T2 可并行
- Group B (依赖 A): T3, T4, T5 可并行（各自只依赖 A 中部分）
- Group C (依赖 A): T6 可与 B 并行
- Group D (依赖 B+C): T7, T8 可并行
- Group E (依赖 B+C+D): T9
- Group F (依赖 D+E): T10
