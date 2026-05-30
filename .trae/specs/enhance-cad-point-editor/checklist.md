# Checklist — CadPointEditorView 增强验证

## Core 层（通用资产，零 UI 依赖）

### 数据模型
- [ ] CadEntityType 枚举定义完整（Line/Arc/Circle/LwPolyline/Ellipse）
- [ ] CadEntity 基类含 Id/LayerName/EntityType/Color/IsSelected/IsVisible，继承 BindableBase
- [ ] CadLine 含 StartPoint/EndPoint（X,Y,Z）
- [ ] CadArc 含 Center/Radius/StartAngle/EndAngle
- [ ] CadCircle 含 Center/Radius
- [ ] CadLwPolyline 含 Vertices(List\<PointF\>)/IsClosed/Width
- [ ] CadEllipse 含 Center/MajorAxis/MinorAxis/StartAngle/EndAngle/Rotation
- [ ] DxfParseResult 含 Layers(Dictionary)/Extents(BoundingBox)/ParseWarnings
- [ ] BoundingBox 含 MinX/MaxX/MinY/MaxY + Contains/Union/ExpandToInclude 方法
- [ ] CadPoint 已扩展 MachineX/MachineY/MachineZ（double? 可空），向后兼容
- [ ] DispenseSegment 含所有工艺参数属性（MoveSpeed/DispenseAmount/PreDelay/PostDelay/CornerDecel/ZHeight/SafeHeight），均支持 UI 绑定
- [ ] DispenseSegment.Length 只读计算属性正确计算轨迹长度
- [ ] CoordinateTransform 支持平移+旋转+缩放的 Transform/InverseTransform 方法
- [ ] RoiRegion 含 RoiType 枚举和各类型参数属性

### DXF 解析服务（Core）
- [ ] IDxfParserService 接口定义在 Core/Services/
- [ ] DxfParserService 实现无 WPF/SkiaSharp/Prism 依赖（纯 C#）
- [ ] LINE 实体解析：组码 10/20/30 起点、11/21/31 终点 正确
- [ ] ARC 实体解析：组码 10/20/30 圆心、40 半径、50 起始角、51 终止角 正确
- [ ] CIRCLE 实体解析正确
- [ ] LWPOLYLINE 实体解析：顶点序列 + 闭合标志 正确
- [ ] ELLIPSE 实体解析正确
- [ ] 图元按 Layer 名(组码8)正确分组到 Layers 字典
- [ ] 全局 Extents 边界框计算正确
- [ ] Discretize Line 线性插值采样正确
- [ ] Discretize Arc/Circle 角度等分采样正确（逆时针方向，考虑起止角）
- [ ] Discretize Ellipse 参数方程采样正确
- [ ] Discretize LwPolyline 各段分别离散化后正确拼接
- [ ] 原 Module/Services/DxfParser.cs 的 ExtractPoints 已标记 [Obsolete] 或已迁移删除

### ROI 工具服务（Core）
- [ ] IRoiToolService 接口定义在 Core/Services/
- [ ] RoiToolService 实现无 WPF 依赖
- [ ] LineRoi 采样点均匀分布在起止点之间，间距 = pitchMM
- [ ] PolylineRoi 各段插值后正确拼接无重复点
- [ ] ArcRoi 角度等分采样坐标计算正确 (x=cx+r·cosθ, y=cy+r·sinθ)
- [ ] FreehandRoi 重采样平滑可用

### 坐标对齐服务（Core）
- [ ] ICoordinateAlignService 接口定义在 Core/Services/
- [ ] Mode1 单点偏移：SetMapFiducial + SetMachineFiducial → AutoCalculate → 所有 CAD 点的 Machine 坐标已更新
- [ ] Mode2 多点映射：SetPointMapping 独立存储每点机械坐标
- [ ] TransformToMachine 根据当前模式返回正确结果
- [ ] GetTransform 返回当前变换矩阵
- [ ] 支持旋转角度的仿射变换（可选高级功能）

## Module 层（项目特定）

### SkiaCanvasControl 独立控件
- [ ] SkiaCanvasControl.xaml 内嵌 SKElement，PaintSurface 事件正确绑定
- [ ] DependencyProperty 定义完整：Entities / SelectedEntity / ZoomFactor / ShowGrid / CurrentRoiPreview
- [ ] CLR Event 定义完整：CoordinateChanged / EntitySelected / EntityDoubleClicked
- [ ] SKCanvas 渲染：背景白 + 可选网格 + 图层着色图元 + 选中高亮 + ROI 预览 + 坐标标尺
- [ ] BASE_FRAME 层图元显示灰色，DISPENSE_GLUE 层图元显示彩色
- [ ] 鼠标滚轮缩放以鼠标位置为中心
- [ ] 鼠标中键/右键拖拽平移正常
- [ ] 左键点击命中测试选中最顶层图元并触发 EntitySelected 事件
- [ ] Ctrl+左键或拖拽框选支持多选
- [ ] 双击触发 EntityDoubleClicked 事件
- [ ] 鼠标移动时 CoordinateChanged 事件报告正确的 CAD 坐标
- [ ] SkiaCanvasViewModel 管理 Entities/ZoomFactor/PanOffset/ShowGrid 等状态
- [ ] FitToAll 功能使图形居中适配画布
- [ ] ResetView 恢复默认视图

### CadPointEditorControl 独立编辑器控件
- [ ] CadPointEditorControl.xaml 采用 Grid 两列布局（左画布 + 右面板）
- [ ] 顶部 Step Indicator 显示 6 步操作流程（导入→确认→编辑→对齐→预览→执行）
- [ ] 当前步骤高亮蓝色，已完成步骤显示绿色 ✓，未到步骤置灰
- [ ] 每步下方显示操作提示文字
- [ ] 点击步骤标题可跳转到该步骤（展开对应面板）
- [ ] 右侧面板根据 CurrentStep 动态切换内容：
  - Step1: 文件选择 + 导入按钮
  - Step2: 图层勾选 + 轨迹段数摘要
  - Step3: 轨迹段 DataGrid + 参数编辑区 + 批量操作
  - Step4: 对齐模式切换 + Fiducial 输入/Teach + AutoCalculate
  - Step5: DryRun 按钮 + 状态
  - Step6: Site 选择 + Execute 按钮 + 安全警告
- [ ] DependencyProperty 定义：Segments(输出) / CurrentStep / FilePath
- [ ] RoutedEvent: ExecuteRequestEvent 可被外部宿主监听
- [ ] 控件不依赖特定父窗口布局，可独立嵌入使用

### CadPointEditorViewModel（精简版）
- [ ] 构造函数通过 DI 注入 Core 服务（IDxfParserService, IRoiToolService, ICoordinateAlignService）
- [ ] IDispenseExecuteService 注入可为 null（控件在无运动卡环境也能运行编辑功能）
- [ ] CurrentStep / StepStatus[] / GoNext / GoPrev / GoToStep 步骤流转逻辑正确
- [ ] ImportDxfCommand 调用 _dxfParser.Parse() → 构建 DispenseSegment 列表 → 更新 Segments → 刷新画布 Entities
- [ ] 导入后自动建议进入下一步骤
- [ ] ROI 工具命令设置画布绘制模式 → 确认后调用 _roiTool.SamplePoints() → 追加新 Segment
- [ ] 对齐命令对接 _alignService（Mode1/Mode2 切换、Teach、AutoCalculate）
- [ ] 执行命令对接 _executeService（DryRun / ExecutePath）

### 点胶执行服务（Module）
- [ ] IDispenseExecuteService 接口定义完整
- [ ] DispenseExecuteService 注入 IMotionService 和 ILoggerService
- [ ] DryRunAsync 只运动不出胶，沿轨迹逐段移动
- [ ] ExecutePathAsync 完整流程：安全高度→Z下降→PreDelay→开胶→插补运动→关胶→PostDelay→Z回升
- [ ] ExecuteSinglePointAsync 定点出胶流程正确
- [ ] ProgressChanged 事件发布当前段号和总数
- [ ] StatusChanged 事件发布运行状态
- [ ] CancellationToken 可中断执行

### DI 注册与集成
- [ ] Core 服务注册为 Singleton（IDxfParserService, IRoiToolService, ICoordinateAlignService）
- [ ] Module 服务注册（IDispenseExecuteService）
- [ ] 原 CadPointEditorView.xaml 改为内嵌 CadPointEditorControl 的薄包装
- [ ] Core.csproj 无新增 WPF/SkiaSharp 依赖
- [ ] Prism ViewModelLocator 能正确解析 CadPointEditorControl 的 ViewModel

## 编译与运行验证
- [ ] 全项目编译通过（Release 配置），零 error
- [ ] Core 项目可独立编译（无 Module 引用循环）
- [ ] 手动验证：能打开标准 DXF 文件 → 画布显示外框(灰)+定位孔(灰)+5段弧线(彩色)
- [ ] 手动验证：点击弧线 → 高亮 + 右侧 Step3 面板显示该段参数
- [ ] 手动验证：双击轨迹段 → 展开详细参数编辑区 → 修改 Speed/Glue 保存生效
- [ ] 手动验证：勾选/取消部分段的 IsEnabled → 只影响启用段
- [ ] 手动验证：Mode1 对齐 → Teach MapFiducial → Teach MachineFiducial → AutoCalculate → 坐标变换正确
- [ ] 手动验证：选择线段 ROI 工具 → 在画布绘制 → 确认生成采样点
- [ ] 手动验证：Step Indicator 流转正确（导入完成→自动高亮 Step2）
- [ ] MaterialDesign UI 风格与项目其他页面一致
