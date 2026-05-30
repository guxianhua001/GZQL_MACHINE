# CadPointEditorView 增强规范 — CAD 图纸导入与轨迹提取系统

## Why

现有 `CadPointEditorView` 为简易版原型，仅支持基础 DXF VERTEX 点提取 + Canvas 简单连线绘制，无法满足工业点胶机的实际需求：
- **DXF 解析能力不足**：仅解析 VERTEX 点，无法识别 ARC/LINE/POLYLINE/ELLIPSE 等图元
- **可视化控件简陋**：固定 300px Canvas，无 ROI 交互，无实时坐标显示，无分段选中
- **缺少轨迹编辑**：无法手动绘制线段 ROI 提取点位、无法分段设置点胶参数（速度/胶量/延时）
- **执行功能为空壳**：DryRun / ExecutePath 仅弹对话框模拟，未对接 `IMotionService` 的插补运动

本规范旨在将其升级为**行业级点胶机 CAD 轨迹编辑器**（独立可复用控件），支持 5 段椭圆弧独立编辑、可视化 ROI 取点、坐标对齐、真实插补运动执行，并通过向导式操作流程指引不熟悉人员。

## What Changes

### 核心设计原则

1. **Core 层 = 通用可复用资产**：数据模型、服务接口与实现、算法工具类全部放入 `Core` 项目，任何引用 Core 的项目均可直接使用 DXF 解析、ROI 工具、坐标变换能力
2. **Module 层 = 项目特定 UI 控件和 ViewModel**：WPF 控件、视图模型、DI 注册等绑定到具体项目的东西放 Module
3. **CadPointEditorControl 作为独立可复用 UserControl**：不依赖特定页面布局，可嵌入任意 Window/Page/UserControl 中
4. **操作流程向导化**：通过步骤指示器(Step Indicator) + 引导提示 + 操作状态反馈，让不熟悉的人员也能按步骤完成操作

### 新增功能
- **增强 DXF 解析引擎（Core）**：支持 ARC/LINE/LWPOLYLINE/CIRCLE/ELLIPSE 图元按图层分离提取
- **SkiaSharp 可视化控件（Module）**：独立可复用的 `SkiaCanvasControl` 用户控件，高性能矢量渲染 + 鼠标交互
- **手动 ROI 取点工具（Core 服务 + Module 控件交互）**：画任意线段 ROI → 提取离散坐标点轨迹
- **分段轨迹管理（Core 模型 + Module UI）**：每段弧线独立实体，可单独选中、启用/禁用、绑定工艺参数
- **CAD→机械坐标对齐（Core 服务 + Module UI）**：两种模式（示教首点自动计算 / 示教全部点），支持旋转校正
- **真实走胶执行（Module 服务）**：对接 `IMotionService` 实现 2 轴插补运动 + 出胶控制
- **操作流程向导（Module UI）**：Step Indicator 引导式操作界面

### 重构变更
- **DxfParser.cs（Core）**：从 `Module/Services/` 迁移到 `Core/Services/`，从静态 VERTEX 提取器重构为完整 DXF 图元解析服务
- **CadPointEditorView**：重构为独立的 `CadPointEditorControl` UserControl（可复用）
- **CadPointEditorViewModel**：拆分，通用协调逻辑保留，UI 特定逻辑分离

### 影响范围

#### Core 项目（通用层 — 跨项目复用）

| 文件 | 类型 | 说明 |
|------|------|------|
| `Core/Services/IDxfParserService.cs` | 新增接口 | DXF 解析服务接口 |
| `Core/Services/DxfParserService.cs` | 新增实现 | 完整 DXF 图元解析引擎 |
| `Core/Services/IRoiToolService.cs` | 新增接口 | ROI 工具服务接口 |
| `Core/Services/RoiToolService.cs` | 新增实现 | ROI 取点采样算法 |
| `Core/Services/ICoordinateAlignService.cs` | 新增接口 | 坐标对齐服务接口 |
| `Core/Services/CoordinateAlignService.cs` | 新增实现 | CAD→机械坐标变换 |
| `Core/Models/CadEntity.cs` | 新增 | CAD 图元基类 + 5 种具体图元类型 |
| `Core/Models/DxfParseResult.cs` | 新增 | DXF 解析结果容器 |
| `Core/Models/BoundingBox.cs` | 新增 | 边界框 + 几何工具方法 |
| `Core/Models/DispenseSegment.cs` | 新增 | 分段轨迹（含工艺参数） |
| `Core/Models/CoordinateTransform.cs` | 新增 | 坐标变换矩阵模型 |
| `Core/Models/RoiRegion.cs` | 新增 | ROI 区域定义 + 采样方法 |
| `Core/Models/CadPoint.cs` | 修改 | 扩展 MachineX/Y/Z 属性 |

#### Module 项目（项目特定层）

| 文件 | 类型 | 说明 |
|------|------|------|
| `Module/Controls/SkiaCanvasControl.xaml` + `.cs` | 新增 | 独立可复用 SkiaSharp 画布控件 |
| `Module/Controls/CadPointEditorControl.xaml` + `.cs` | 新增 | **独立可复用**的点胶编辑器完整控件 |
| `Module/ViewModels/SkiaCanvasViewModel.cs` | 新增 | 画布控件专用 VM |
| `Module/ViewModels/CadPointEditorViewModel.cs` | 重构 | 编辑器主 VM（精简后，委托给 Core 服务） |
| `Module/Services/IDispenseExecuteService.cs` | 新增接口 | 点胶执行服务（依赖 IMotionService） |
| `Module/Services/DispenseExecuteService.cs` | 新增实现 | 走胶执行逻辑 |
| `Module/WorkStation/Dispense/CadPointEditorView.xaml` | 修改 | 改为嵌入 CadPointEditorControl 的薄包装 |
| `Module/Services/DxfParser.cs` | 删除 | 迁移到 Core（保留兼容别名或删除） |

---

## ADDED Requirements

### REQ-LAYERING: 分层架构规范（Core vs Module 职责划分）

系统 SHALL 严格遵循以下分层规则：

**Core 层 SHALL 包含**（纯 C# 逻辑，零 WPF 依赖）：
- 所有数据模型（Model）：CadEntity 系列、DispenseSegment、CoordinateTransform、RoiRegion、BoundingBox
- 所有通用服务接口（IService）：IDxfParserService、IRoiToolService、ICoordinateAlignService
- 所有通用服务实现（Service）：DxfParserService、RoiToolService、CoordinateAlignService
- 算法和工具类：几何计算、DXF 组码解析、采样算法、坐标变换矩阵运算

**Core 层 SHALL NOT 包含**：
- WPF 依赖（UserControl/Window/DependencyProperty/SkiaSharp）
- Prism 依赖（ViewModelLocator/DialogService/RegionManager）
- 项目特定的业务逻辑（如 DispensingTask 对接）

**Module 层 SHALL 包含**：
- WPF 用户控件（SkiaCanvasControl、CadPointEditorControl）
- ViewModel（绑定 WPF 控件，调用 Core 服务）
- 项目特有服务（IDispenseExecuteService — 依赖 IMotionService 运动卡）
- DI 注册配置（将 Core 服务 + Module 服务注册到 Prism 容器）

**CadPointEditorControl SHALL 是一个独立可复用的 UserControl**：
- 不依赖父窗口的布局环境
- 通过 DependencyProperty 暴露关键输入/输出（如 FilePath 绑定、Segments 输出绑定、OnExecute 回调事件）
- 可在任何 XAML 中通过 `<controls:CadPointEditorControl />` 直接使用

### REQ-DXF-PARSE: DXF 图纸解析与图层分离（Core 服务）

系统 SHALL 在 Core 层提供 `IDxfParserService`，支持导入 `.dxf` 格式图纸并按图层分离提取以下图元：

| 图元类型 | 用途 | 关键几何参数 |
|---------|------|------------|
| LINE | 直线轨迹 | 起点(X,Y,Z), 终点(X,Y,Z) |
| ARC | 圆弧轨迹 | 圆心, 半径, 起始角, 终止角(逆时针) |
| CIRCLE | 整圆轨迹 | 圆心, 半径 |
| LWPOLYLINE | 多段线/折线 | 顶点序列, 闭合标志 |
| ELLIPSE | 椭圆弧轨迹 | 中心, 长轴/短轴向量, 起止角 |

**图层映射规则**：

| 图层名 | 用途 | 颜色标识 |
|--------|------|---------|
| BASE_FRAME | 外框矩形 + 定位孔圆（基准参考，不生成走胶路径） | 灰色 |
| DISPENSE_GLUE | 5 段椭圆弧（纯点胶轨迹） | 蓝色（可按段着色） |

#### Scenario: 导入标准点胶 DXF 图纸
- **WHEN** 调用方传入一个标准 DXF 文件路径
- **THEN** `IDxfParserService.Parse()` 返回：
  1. `Layers["BASE_FRAME"]` 包含 1 个 LINE（外框）+ 1 个 CIRCLE（定位孔）
  2. `Layers["DISPENSE_GLUE"]` 包含 5 个 ARC（或 ELLIPSE 弧段）
  3. `Extents` 正确反映图纸边界 (0,0) 到 (200,120)
  4. 图纸保持 1:1 真实尺寸（单位 mm）

#### Scenario: 图元离散化
- **WHEN** 调用 `Discretize(arcEntity, 1.0)` 对一段半圆弧（半径80mm，180°）以 1mm 间距采样
- **THEN** 返回约 252 个均匀分布的 CadPoint 点序列

### REQ-VISUALIZE: SkiaSharp 轨迹可视化控件（Module 独立控件）

系统 SHALL 在 Module 层提供独立可复用的 `SkiaCanvasControl` UserControl：

**渲染能力**：
- 矢量渲染所有 CAD 图元（直线/圆弧/椭圆/多段线），抗锯齿
- 不同图层使用不同颜色/线型区分
- 选中的图元高亮显示（加粗 + 高亮色边框）
- 当前执行中的轨迹段以动画方式高亮（走胶仿真时）
- 显示坐标网格/标尺（可切换显示）

**交互能力**：
- 鼠标滚轮缩放（以鼠标位置为中心）
- 鼠标中键/右键拖拽平移
- 左键点击选中单个图元（弧线/直线）
- Ctrl+左键 / 框选多个图元
- 双击图元触发编辑请求事件
- 实时显示鼠标位置的 CAD 坐标（通过事件/回调向外报告）

**作为独立控件的接口**：
```csharp
// DependencyProperties
public static readonly DependencyProperty EntitiesProperty       // ObservableCollection<CadEntity>
public static readonly DependencyProperty SelectedEntityProperty   // CadEntity
public static readonly DependencyProperty ZoomFactorProperty      // double
public static readonly DependencyProperty ShowGridProperty        // bool

// CLR Events
public event Action<double, double> CoordinateChanged;            // (cadX, cadY) 实时坐标回调
public event Action<CadEntity> EntitySelected;                    // 图元选中事件
public event Action<CadEntity> EntityDoubleClicked;               // 双击编辑事件
```

**坐标系**：
- CAD 坐标系：原点 (0,0) 在左下角，X 向右，Y 向上
- 缩放范围：0.01× ~ 100×

### REQ-ROI-TOOL: 手动 ROI 取点工具（Core 服务 + Module 交互）

系统 SHALL 在 Core 层提供 `IRoiToolService`，在 Module 层的画布控件中提供交互绘制能力：

**Core 层 — IRoiToolService 提供的能力**：

| 工具 | 操作方式 | 输出 |
|------|---------|------|
| 线段 ROI | 起止两点坐标 | 线段上均匀采样的 N 个离散点坐标 |
| 折线 ROI | 多个顶点坐标序列 | 各段插值点序列合并 |
| 圆弧 ROI | 圆心+半径+起止角 或 三点定弧 | 弧线上采样点 |
| 自由手绘 | 密集点列（来自画布笔迹） | 重采样后的平滑点集 |

**取点参数**（用户可配置）：
- 采样间距（mm）：默认 1.0mm
- 平滑滤波：对自由手绘结果进行贝塞尔平滑

**Module 层 — 画布上的 ROI 交互**：
- 用户选择 ROI 工具按钮 → 画布进入对应绘制模式
- 鼠标在画布上拖拽/点击绘制 → 实时预览 ROI 形状
- 确认后调用 `IRoiToolService.SamplePoints()` → 得到点序列 → 添加到轨迹

#### Scenario: 手动绘制线段 ROI 提取点位
- **WHEN** 用户选择「线段 ROI」工具，在画布上从 (20,30) 拖拽到 (80,50)，设置采样间距 0.5mm
- **THEN** 系统：
  1. 画布上实时显示绿色虚线 ROI 预览
  2. 确认后调用 RoiToolService 采样 ≈ 233 个点
  3. 点序列添加到当前轨迹段列表

### REQ-SEGMENT-MGMT: 分段轨迹管理与工艺参数绑定（Core 模型 + Module UI）

系统 SHALL 在 Core 层定义 `DispenseSegment` 数据模型，在 Module 层提供管理 UI：

**Core 层 — DispenseSegment 模型**：

```
DispenseSegment : BindableBase
  - SegmentId: string          // 如 "ARC_001"
  - EntityType: CadEntityType  // Line/Arc/Circle/Polyline/Ellipse
  - SourceEntity: CadEntity    // 来源 CAD 图元引用
  - Points: List<CadPoint>     // 离散化后的采样点序列
  - IsEnabled: bool            // 是否启用参与走胶
  - LayerName: string          // 来源图层
  - Length: double             // 轨迹长度(mm)，只读计算属性
  
  // === 工艺参数（BindableBase 属性，支持 UI 双向绑定）===
  - MoveSpeed: double          // 运动速度 mm/s，默认 10.0
  - DispenseAmount: double     // 出胶量（相对值），默认 1.0
  - PreDelay: double           // 起点开胶延时 ms，默认 50
  - PostDelay: double          // 终点关胶延时 ms，默认 50
  - CornerDecel: double        // 拐角减速系数 0~1，默认 0.8
  - ZHeight: double            // 点胶高度 mm，默认 0.0
  - SafeHeight: double         // 安全高度 mm，默认 5.0
```

**Module 层 — 右侧面板功能**：
- 轨迹段 DataGrid：☑启用 | ID | 类型 | 长度 | 速度 | 胶量
- 单击行 → 画布高亮对应段；双击行 → 展开详细参数编辑区
- 批量操作：全选/反选、批量设速度、批量设胶量

### REQ-COORDINATE-ALIGN: CAD 坐标与机械坐标对齐（Core 服务 + Module UI）

系统 SHALL 在 Core 层提供 `ICoordinateAlignService`，封装两种对齐模式：

**Mode 1: 示教首点 + 自动偏移计算**
1. 设置 Map Fiducial（CAD 基准点坐标）
2. Teach Machine Fiducial（机械坐标，由调用方通过运动卡获取实际位置后注入）
3. 计算 Δ 向量，对所有 CAD 点应用刚性变换
4. 支持旋转校正（两点示教 → 仿射变换矩阵）

**Mode 2: 示教全部点**
- 逐点建立 CAD→机械坐标映射（Dictionary）
- 不依赖几何关系

**Core 层 ICoordinateAlignService 接口**：
```csharp
void SetMode(AlignMode mode);                          // Mode1 / Mode2
void SetMapFiducial(double x, double y, double z);
void SetMachineFiducial(double x, double y, double z, double rx, double rz);
void AutoCalculate();                                   // Mode1: 自动计算所有点
void SetPointMapping(string pointId, double mx, double my, double mz);  // Mode2: 单点示教
CadPoint TransformToMachine(CadPoint cadPoint);          // 坐标转换
CoordinateTransform GetTransform();                     // 获取当前变换矩阵
```

### REQ-EXECUTE: 点胶轨迹执行（Module 服务 — 依赖运动卡）

系统 SHALL 在 Module 层提供 `IDispenseExecuteService`（因依赖 `IMotionService` 运动控制卡，属于项目特定逻辑，不放 Core）：

**Dry Run（空跑仿真）**：只运动不出胶，沿轨迹逐段移动，发布进度事件
**Execute Path（真实走胶）**：安全高度→Z下降→PreDelay→开胶→插补运动→关胶→PostDelay→Z回升
**单点点胶**：定点出胶（下降→开胶→延时→关胶→上升）
支持 CancellationToken 急停中断

### REQ-WORKFLOW: 操作流程向导引导（Module UI）

系统 SHALL 在 `CadPointEditorControl` 中内置**步骤指示器（Step Indicator）**，将操作流程分为清晰的阶段，每个阶段突出显示当前步骤并提供操作提示：

**操作流程分为 6 步**：

```
┌─────────────────────────────────────────────────────────────────────┐
│  Step1 ──→ Step2 ──→ Step3 ──→ Step4 ──→ Step5 ──→ Step6         │
│  [①导入]   [②确认]   [③编辑]   [④对齐]   [⑤预览]   [⑥执行]        │
│                                                                     │
│  当前步骤高亮(蓝色)，已完成步骤打勾(绿色)，未到步骤置灰              │
│  每步下方显示简短操作提示文字                                        │
└─────────────────────────────────────────────────────────────────────┘
```

| 步骤 | 名称 | 操作内容 | 提示文字 |
|------|------|---------|---------|
| ① | 导入图纸 | 选择 DXF 文件 → 自动解析 | "请选择符合规范的 .dxf 点胶图纸文件" |
| ② | 确认轨迹 | 查看画布上的图元 → 确认图层/段数正确 | "请确认轨迹段数量和位置是否正确，可取消勾选不需要的段" |
| ③ | 编辑参数 | 设置每段的速度/胶量/延时等工艺参数 | "双击轨迹段可编辑该段的点胶参数，也可批量设置" |
| ④ | 坐标对齐 | 示教基准点 → 建立 CAD↔机械坐标映射 | "请先移动点胶头到定位孔中心，点击 Teach 捕获机械坐标" |
| ⑤ | 预览仿真 | Dry Run 空跑查看轨迹是否正确 | "点击 DryRun 预览走胶路径，确认无误后再执行" |
| ⑥ | 执行走胶 | 真实点胶执行 | "确认参数和对齐数据无误后，点击 Execute 开始走胶" |

**向导行为规则**：
- 默认从 Step 1 开始，完成当前步的关键操作后才建议进入下一步（但不强制锁定，允许熟练用户跳步）
- 点击某一步骤标题可快速跳转到该步骤（展开对应面板区域）
- 当前步骤对应的操作面板区域高亮/展开，其他步骤面板折叠
- 全部步骤完成后，Step Indicator 全部显示为已完成状态（绿色 ✓）
- 每个步骤旁显示状态图标：⚪ 未开始 / 🔄 进行中 / ✅ 已完成 / ⚠️ 有警告

---

## MODIFIED Requirements

### REQ-EXISTING-DXF: DxfParser 迁移到 Core 并增强

**原有文件**：`Module/Services/DxfParser.cs`（静态 VERTEX 提取器）

**变更**：
- 将 DxfParser 功能迁移到 `Core/Services/DxfParserService.cs`，实现 `IDxfParserService`
- 原 `Module/Services/DxfParser.cs` 中的 `ExtractPoints()` 静态方法保留为兼容包装（内部调用新服务），或标记 `[Obsolete]` 引导迁移
- Core 项目需新增对 DxfParser 无外部依赖（纯文本解析，无需 WPF/MotionControl 等）

### REQ-EXISTING-VIEW: CadPointEditorView 重构为独立控件

**原有文件**：`Module/WorkStation/Dispense/CadPointEditorView.xaml`（纵向 StackPanel 布局）

**变更**：
- 新建 `Module/Controls/CadPointEditorControl.xaml` 作为**独立可复用 UserControl**
- 原 `CadPointEditorView.xaml` 改为薄包装（内嵌 CadPointEditorControl + 可选的项目特定扩展区）
- 布局从垂直堆叠改为专业工位布局（见下方布局图）

### REQ-EXISTING-VM: CadPointEditorViewModel 精简

**原有问题**：`CadPointEditorViewModel.cs` 有 600+ 行，包含 DXF 导入/CSV/对齐/示教/执行/Calibration 等所有逻辑

**变更后职责划分**：
- **CadPointEditorViewModel**（Module）：仅负责 UI 协调（步骤流转、面板展开/折叠、按钮命令分发），业务逻辑全部委托给 Core 服务
- **SkiaCanvasViewModel**（Module）：画布控件专用 VM（缩放/平移/选中/坐标显示）
- Core 服务承担：DXF 解析、ROI 采样、坐标变换、离散化算法

---

## MODIFIED Layout（更新后的 UI 布局）

```
┌──────────────────────────────────────────────────────────────────────────┐
│  ⭐ CAD Point Editor — 点胶轨迹编辑器                    [最小化] [关闭] │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌─ Step Indicator (操作流程引导条) ──────────────────────────────────┐  │
│  │  ①导入图纸 ──→ ②确认轨迹 ──→ ③编辑参数 ──→ ④坐标对齐 ──→ ⑤预览 ──→ ⑥执行  │  │
│  │  [✓]        [✓]         [●当前]      [ ]         [ ]        [ ]     │  │
│  │  "请选择 DXF 文件导入点胶轨迹"                                      │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
│                                                                          │
│  ┌────────────────────────────────────┬─────────────────────────────────┤│
│  │                                    │  ┌─ 当前步骤面板 ────────────┐ ││
│  │     SkiaSharp 可视化画布           │  │                           │ ││
│  │     (自适应填充左侧区域)           │  │  根据 Step 动态切换内容：    │ ││
│  │                                    │  │                           │ ││
│  │  ┌──────────────────────────────┐  │  │ Step①: [选择文件] [导入]   │ ││
│  │  │                              │  │  │        文件名: ________   │ ││
│  │  │  CAD 图形显示                 │  │  │                           │ ││
│  │  │  · 外框 BASE_FRAME (灰)      │  │  │ Step②: ☑BASE_FRAME        │ ││
│  │  │  · 定位孔 (灰)               │  │  │        ☑DISPENSE_GLUE     │ ││
│  │  │  · 5段弧线 (彩色独立)        │  │  │        轨迹段数: 5         │ ││
│  │  │  · 选中高亮                  │  │  │                           │ ││
│  │  │  · ROI 预览 (绿虚线)         │  │  │ Step③: 轨迹段列表:        │ ││
│  │  │                              │  │  │  ☑ ARC_001  弧 51.3mm  │ ││
│  │  └──────────────────────────────┘  │  │  ☑ ARC_002  弧 51.3mm  │ ││
│  │                                    │  │  [...]                   │ ││
│  │  画布工具栏:                       │  │  [批量设参] [删除]        │ ││
│  │  [图层▼] [ROI:线段][折线][圆弧]    │  │  ── 选中段参数 ──        │ ││
│  │  [适应窗口] [1:1] [网格☑]          │  │  Speed:[__] Glue:[__]    │ ││
│  │                                    │  │  PreDly:[__] PostDly:[__]│ ││
│  │  坐标: X=100.00  Y=60.00  mm      │  │                           │ ││
│  └────────────────────────────────────┴  │  Step④: (○)示教首点        │ ││
│                                       │  │        (·)示教全部        │ ││
│                                       │  │  MapFid: (15,15,0)[Teach]│ ││
│                                       │  │  MachFid:(__,__,__)[Teach]│ ││
│                                       │  │  [AutoCalculate]         │ ││
│                                       │  │                           │ ││
│                                       │  │  Step⑤: [▶ DryRun]       │ ││
│                                       │  │  状态: Ready              │ ││
│                                       │  │                           │ ││
│                                       │  │  Step⑥: Site:[ASSY_001▼] │ ││
│                                       │  │  Z校正:[☑]               │ ││
│                                       │  │  [● Execute Path]        │ ││
│                                       │  │  ⚠️ 确认参数无误后执行    │ ││
│                                       │  └───────────────────────────┘ ││
├───────────────────────────────────────┴─────────────────────────────────┤
│  全局状态栏: 就绪 | 段数: 5 | 总长度: 256.5mm | 对齐: Mode1 已校准      │
└──────────────────────────────────────────────────────────────────────────┘
```

## 架构总览（更新版）

```
========================= Core 项目（跨项目复用） =========================
│                                                                        │
│  ┌─ Models（数据模型，零 UI 依赖） ──────────────────────────────┐     │
│  │  CadEntity (基类) → CadLine / CadArc / CadCircle /            │     │
│  │                  CadLwPolyline / CadEllipse                    │     │
│  │  DispenseSegment (含工艺参数的 BindableBase)                   │     │
│  │  CoordinateTransform / RoiRegion / BoundingBox                │     │
│  │  DxfParseResult                                               │     │
│  │  CadPoint (扩展 MachineX/Y/Z)                                 │     │
│  └───────────────────────────────────────────────────────────────┘     │
│                                                                        │
│  ┌─ Services（通用服务接口+实现） ────────────────────────────────┐     │
│  │  IDxfParserService ← DxfParserService                        │     │
│  │    Parse(filePath) → DxfParseResult                          │     │
│  │    Discretize(entity, pitch) → List<CadPoint>                │     │
│  │                                                               │     │
│  │  IRoiToolService ← RoiToolService                            │     │
│  │    SamplePoints(roiRegion, pitch) → List<CadPoint>           │     │
│  │                                                               │     │
│  │  ICoordinateAlignService ← CoordinateAlignService            │     │
│  │    SetMode / SetMapFiducial / SetMachineFiducial            │     │
│  │    AutoCalculate / TransformToMachine / GetTransform         │     │
│  └───────────────────────────────────────────────────────────────┘     │
│                                                                        │
========================================================================


======================== Module 项目（GZQL_MACHINE 特有） =====================
│                                                                        │
│  ┌─ Controls（独立可复用 WPF 控件） ────────────────────────────┐     │
│  │  SkiaCanvasControl.xaml                                     │     │
│  │    SKElement 渲染 + 鼠标交互 + 缩放平移 + 选中/框选          │     │
│  │    DP: Entities, SelectedEntity, ZoomFactor, ShowGrid       │     │
│  │    Event: CoordinateChanged, EntitySelected, EntityDblClick  │     │
│  │                                                               │     │
│  │  CadPointEditorControl.xaml  ★ 独立可复用 ★                  │     │
│  │    内嵌 SkiaCanvasControl + StepIndicator + 右侧面板          │     │
│  │    DP: Segments(Output), OnExecuteRequest(Event)             │     │
│  │    可通过 <ctrls:CadPointEditorControl /> 直接使用            │     │
│  └───────────────────────────────────────────────────────────────┘     │
│                                                                        │
│  ┌─ ViewModels ─────────────────────────────────────────────────┐     │
│  │  SkiaCanvasViewModel (画布状态)                               │     │
│  │  CadPointEditorViewModel (主协调: 步骤流转 + 面板切换)        │     │
│  └───────────────────────────────────────────────────────────────┘     │
│                                                                        │
│  ┌─ Services（项目特有，依赖 IMotionCard） ─────────────────────┐     │
│  │  IDispenseExecuteService ← DispenseExecuteService           │     │
│  │    DryRunAsync / ExecutePathAsync / ExecuteSinglePointAsync │     │
│  └───────────────────────────────────────────────────────────────┘     │
│                                                                        │
│  ┌─ Views（薄包装，嵌入 CadPointEditorControl） ─────────────────┐     │
│  │  CadPointEditorView.xaml (原文件，改为内嵌 CadPointEditorControl│    │
│  └───────────────────────────────────────────────────────────────┘     │
│                                                                        │
│  ┌─ DI Registration ────────────────────────────────────────────┐     │
│  │  Core 服务: singleton IDxfParserService, IRoiToolService,    │     │
│  │              ICoordinateAlignService                         │     │
│  │  Module 服务: transient IDispenseExecuteService               │     │
│  └───────────────────────────────────────────────────────────────┘     │
│                                                                        │
========================================================================
```
