# CadAlignmentView 第三步 — 图形化选取界面实施计划

## 📋 功能概述

将 CadAlignmentView 第三步"旋转角度"的点位选取方式从 **DataGrid 表格选取** 升级为 **图形化 CAD 窗口直接点击选取**，让用户能在导入的 DXF 图形上直观地看到并选取基准/目标线段的起点和终点。

## 🎯 核心目标

1. **图形化预览** — 在 HalconCanvas 中渲染导入的 DXF 图形（线条、圆弧、多段线等）
2. **交互式点选** — 用户在图形窗口中直接点击点位作为线段起点/终点
3. **视觉反馈** — 选取的点位高亮显示、已选线段用不同颜色绘制
4. **智能推荐（可选）** — 根据点位分布自动推荐可能的基准/目标线段

---

## 🔍 技术现状分析

### 已有能力（可复用）

| 组件 | 能力 | 文件位置 |
|------|------|---------|
| **HalconCanvasControl** | 渲染 CadEntity 集合、点击坐标捕获、实体选中、ROI绘制、FitToAll自适应 | Module/Controls/Cad/HalconCanvasControl.xaml.cs |
| **IDxfParserService.Parse()** | 解析DXF文件返回 DxfParseResult（含Layers字典+Extents包围盒） | Core/Services/IDxfParserService.cs |
| **DxfParseResult.Layers** | Dictionary<string, List<CadEntity>> 按图层分组的图元 | Core/Models/DxfParseResult.cs |
| **CadEntity** | 抽象基类，含 LayerName/EntityType/Color/IsSelected/GetBoundingBox() | Core/Models/CadEntity.cs |
| **CadEntityType** | Line/Arc/Circle/LwPolyline/Polyline/Ellipse/Spline/Unknown | Core/Models/CadEntityType.cs |
| **DxfParser.ExtractPoints()** | 已修复支持 layerName=null 提取所有图层VERTEX | Module/Services/DxfParser.cs |

### 当前问题

```
现状: DataGrid 表格 → 用户只能看到 X/Y/Z 数值 → 不知道点位在图形上的位置
目标: HalconCanvas 图形 → 用户直接在图形上点击 → 直观可见所选位置
```

### 关键事件接口（HalconCanvasControl 已提供）

```csharp
// 点击画布获取 CAD 坐标
public event Action<double, double> CanvasPointClicked;  // (cadX, cadY)

// 选中图元实体
public event Action<CadEntity> EntitySelected;

// 渲染图元集合
public void RenderEntities();

// 自适应视口到数据范围
public void FitToAll();

// 图像坐标→CAD坐标转换
public (double cadX, double cadY) ImageToCad(double row, double col);
```

---

## 🏗️ 架构设计

### 整体布局（第三步Tab改造后）

```
┌─────────────────────────────────────────────────────────────────┐
│ SectionCard: 向量方向角计算                                       │
│ ┌───────────────────────────────────┬───────────────────────────┐ │
│ │                                   │                           │ │
│ │  ① [导入DXF]  文件名.dxf          │  操作按钮区               │ │
│ │                                   │  [从CAD选基准] [从CAD选目标]│ │
│ ├───────────────────────────────────┤  状态提示文字              │ │
│ │                                   │                           │ │
│ │     🖥️ HalconCanvas 图形区域      │  ┌─────────────────────┐  │ │
│ │     (渲染DXF图形)                 │  │ 点位列表 DataGrid    │  │ │
│ │     (点击选取点位)                │  │ # | X | Y | 角色    │  │ │
│ │     (高亮已选线段)                │  │ 1 | ..|..|基准起点   │  │ │
│ │                                   │  │ 2 | ..|..|基准终点   │  │ │
│ │  ●━━━● 基准线段(蓝色)             │  │ 3 | ..|..|目标起点   │  │ │
│ │       ╲                          │  │ 4 | ..|..|目标终点   │  │ │
│ │        ╲ θ                       │  └─────────────────────┘  │ │
│ │         ╲                        │                           │ │
│ │  ●━━━━━● 目标线段(红色)          │  角度结果卡片             │ │
│ │                                   │  α_base=xx° α_target=xx°│ │
│ │  ○ 未选中的其他点位(灰色)          │  [③ 计算旋转角度]        │ │
│ │                                   │                           │ │
│ └───────────────────────────────────┴───────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

### 数据流

```
DXF文件 → IDxfParserService.Parse() → DxfParseResult
                                            ↓
                              Dictionary<string, List<CadEntity>>
                                            ↓
                    ┌─────────────────────────┴─────────────────────┐
                    ↓                                           ↓
           HalconCanvasControl                            ViewModel
           .RenderEntities()                         ImportedCadPoints
           显示所有图元                               (用于DataGrid+索引)
                    ↑                                           ↓
           CanvasPointClicked(cadX,cadY)              OnCadPointSelected()
                    ↓                                           ↓
        找最近点位 / 创建新点位                   BaseStartIndex/EndIndex
                                                    TargetStartIndex/EndIndex
                    ↓                                           ↓
        高亮显示已选点位/线段                     UpdateCadPointRoles()
                                                    ComputeCadRotationAngle()
```

---

## 📐 实施步骤

### Phase 1: 基础图形化渲染（核心功能）

#### Step 1.1 — ViewModel 层增强

**文件**: `Module/Controls/Assembly/CadAlignmentViewModel.cs`

新增属性：
```csharp
// DXF 解析结果缓存（含完整 CadEntity 图元信息）
private DxfParseResult _dxfParseResult;
public DxfParseResult DxfParseResult { get => _dxfParseResult; set => SetProperty(ref _dxfParseResult, value); }

// 所有图元的扁平列表（供 HalconCanvas 渲染）
private ObservableCollection<CadEntity> _cadEntities = new();
public ObservableCollection<CadEntity> CadEntities { get => _cadEntities; set => SetProperty(ref _cadEntities, value); }

// 图形区域是否需要刷新
private bool _canvasRefreshPending;
public bool CanvasRefreshPending { get => _canvasRefreshPending; set => SetProperty(ref _canvasRefreshPending, value); }
```

修改 `OnImportDxf()` 方法：
```csharp
private void OnImportDxf()
{
    // ... 现有的 OpenFileDialog 代码 ...

    // 新增：使用 IDxfParserService 解析完整图元信息
    try
    {
        var dxfParser = ContainerLocator.Container?.Resolve<IDxfParserService>();
        if (dxfParser != null)
        {
            _dxfParseResult = dxfParser.Parse(dialog.FileName);  // 完整解析
            // 将所有图层图元合并为扁平列表
            CadEntities.Clear();
            foreach (var layerEntities in _dxfParseResult.Layers.Values)
                foreach (var entity in layerEntities)
                    CadEntities.Add(entity);
        }
    }
    catch { /* 回退到仅提取点位 */ }

    // ... 现有的 DxfParser.ExtractPoints 代码保持不变 ...
}
```

新增方法 — 处理画布点击：
```csharp
/// <summary>HalconCanvas 点击回调：根据点击坐标找到最近点位并分配角色</summary>
public void OnCanvasPointClicked(double cadX, double cadY)
{
    if (!_isPickingBaseline && !_isPickingTarget) return;
    if (ImportedCadPoints.Count == 0) return;

    // 找到距离点击位置最近的点位
    int nearestIdx = FindNearestPointIndex(cadX, cadY);
    if (nearestIdx < 0) return;

    // 复用现有的 OnCadPointSelected 逻辑
    var point = ImportedCadPoints[nearestIdx];
    OnCadPointSelected(point);
}

/// <summary>找到距离指定坐标最近的点位索引</summary>
private int FindNearestPointIndex(double x, double y)
{
    int nearestIdx = -1;
    double minDist = double.MaxValue;
    for (int i = 0; i < ImportedCadPoints.Count; i++)
    {
        var pt = ImportedCadPoints[i];
        double dx = pt.X - x;
        double dy = pt.Y - y;
        double dist = dx * dx + dy * dy;  // 平方距离避免开方
        if (dist < minDist)
        {
            minDist = dist;
            nearestIdx = i;
        }
    }
    // 设置最大点击容差（如10个单位内才算命中）
    return Math.Sqrt(minDist) < 10.0 ? nearestIdx : -1;
}
```

#### Step 1.2 — View 层嵌入 HalconCanvas

**文件**: `Module/Controls/Assembly/CadAlignmentView.xaml`

在第三步 Tab 的 DataGrid 上方（或替换 DataGrid 区域），添加 HalconCanvas：

```xml
<!-- CAD 图形预览与交互选取区域 -->
<Border Style="{StaticResource RefCard}" Margin="0,8,0,0"
        Visibility="{Binding HasCadDrawingLoaded, Converter={StaticResource BoolToVis}}">
    <DockPanel>
        <!-- 标题栏 -->
        <DockPanel DockPanel.Dock="Top" Margin="0,0,0,8">
            <TextBlock Text="📐 CAD 图形预览" FontSize="11" FontWeight="SemiBold" Foreground="#555"
                       VerticalAlignment="Center"/>
            <TextBlock Text="(点击图形选取点位)" Foreground="#7B1FA2" FontSize="10"
                       VerticalAlignment="Center" Margin="8,0,0,0"/>
        </DockPanel>

        <!-- HalconCanvas 图形区域 -->
        <controls:HalconCanvasControl x:Name="alignmentCanvas"
                                      Height="280"
                                      ItemsSource="{Binding CadEntities}"
                                      MouseLeftButtonDown="OnAlignmentCanvasClick"/>
    </DockPanel>
</Border>
```

保留精简版 DataGrid 在右侧或下方（仅显示已选点位的摘要信息）。

**文件**: `Module/Controls/Assembly/CadAlignmentView.xaml.cs`

添加画布点击事件桥接：
```csharp
private void OnAlignmentCanvasClick(object sender, MouseButtonEventArgs e)
{
    if (DataContext is ViewModels.CadAlignmentViewModel vm)
    {
        // 获取 HalconCanvasControl 引用并转换坐标
        if (sender is Controls.Cad.HalconCanvasControl canvas)
        {
            var pos = e.GetPosition(canvas);
            // 使用 canvas.ImageToCad 转换为 CAD 坐标
            var cadCoord = canvas.ImageToCad(pos.Y, pos.X);
            vm.OnCanvasPointClicked(cadCoord.cadX, cadCoord.cadY);
        }
    }
}
```

#### Step 1.3 — DI 注册

**文件**: `Module/PrimModel.cs` 或相关注册处

确认 `IDxfParserService` 已注册到 DI 容器（CadPointEditorControl 中已有注册参考）。如果未在 CadAlignmentView 所在模块中可用，需要在构造函数中使用 `ContainerLocator.Container.Resolve<IDxfParserService>()` 获取。

---

### Phase 2: 视觉反馈增强

#### Step 2.1 — 选中点位高亮

在 HalconCanvas 中对已选中的点位/线段进行特殊渲染：

- **基准起点**: 蓝色实心圆 + "P1" 标签
- **基准终点**: 绿色实心圆 + "P2" 标签
- **目标起点**: 紫色实心圆 + "P3" 标签
- **目标终点**: 红色实心圆 + "P4" 标签
- **基准连线**: 蓝色粗线段 (P1→P2)
- **目标连线**: 红色粗线段 (P3→P4)

实现方式：在 `UpdateCadPointRoles()` 之后触发画布重绘，通过在 CadEntities 集合之上叠加绘制选中标记。

#### Step 2.2 — 选取模式光标提示

当 `_isPickingBaseline || _isPickingTarget` 为 true 时：
- 光标变为十字准星样式
- 画布边缘显示浮动提示："请点击基准线段的【起点】"

---

### Phase 3: 智能推荐（可选增强）

#### Step 3.1 — 自动推荐线段

基于导入点位的几何分析，自动推荐最可能作为基准和目标的线段：

```csharp
/// <summary>基于点位分布智能推荐基准/目标线段</summary>
public void AutoRecommendLines()
{
    if (ImportedCadPoints.Count < 4) return;

    // 策略1: 找最长线段作为基准（通常代表主要特征边）
    var longestPair = FindLongestSegment();

    // 策略2: 找与基准线段夹角最大的线段作为目标（代表旋转后的对应边）
    var bestTargetPair = FindBestTargetSegment(longestPair);

    // 应用推荐但不自动计算（用户仍需确认）
    BaseStartIndex = longestPair.Item1;
    BaseEndIndex = longestPair.Item2;
    TargetStartIndex = bestTargetPair.Item1;
    TargetEndIndex = bestTargetPair.Item2;
    UpdateCadPointRoles();
}

private (int, int) FindLongestSegment()
{
    int maxI = -1, maxJ = -1;
    double maxDistSq = 0;
    for (int i = 0; i < ImportedCadPoints.Count; i++)
        for (int j = i + 1; j < ImportedCadPoints.Count; j++)
        {
            var dx = ImportedCadPoints[j].X - ImportedCadPoints[i].X;
            var dy = ImportedCadPoints[j].Y - ImportedCadPoints[i].Y;
            double distSq = dx * dx + dy * dy;
            if (distSq > maxDistSq) { maxDistSq = distSq; maxI = i; maxJ = j; }
        }
    return (maxI, maxJ);
}
```

UI 上添加「🪄 智能推荐」按钮调用此方法。

---

## 📁 文件变更清单

| 文件 | 变更类型 | 说明 |
|------|---------|------|
| `Module/Controls/Assembly/CadAlignmentViewModel.cs` | **修改** | 新增 DxfParseResult/CadEntities 属性；OnImportDxf 增强；新增 OnCanvasPointClicked/FindNearestPointIndex/AutoRecommendLines |
| `Module/Controls/Assembly/CadAlignmentView.xaml` | **修改** | 第三步Tab嵌入 HalconCanvasControl；调整布局为左右分栏（图形区+操作区）；精简 DataGrid 为摘要表格 |
| `Module/Controls/Assembly/CadAlignmentView.xaml.cs` | **修改** | 添加 OnAlignmentCanvasClick 事件处理；HalconCanvas 事件绑定 |
| `Module/PrimModel.cs` | **检查** | 确认 IDxfParserService 已注册 |

---

## ⚠️ 注意事项与技术风险

### 1. HalconCanvasControl 依赖
- **风险**: HalconCanvasControl 依赖 HalconDotNet 运行时库
- **缓解**: 项目中已有 CadPointEditorControl 成功使用，说明环境已就绪
- **备选**: 如果 HalconCanvas 无法嵌入，退回到 WPF Canvas + Path/Shape 轻量级渲染

### 2. 坐标系一致性
- **要点**: HalconCanvas 使用图像坐标系(row, col)，CAD 使用笛卡尔坐标(x, y)
- **必须**: 使用 `ImageToCad(row, col)` 和 `CadToImage(cadX, cadY)` 进行转换
- **参考**: CadPointEditorControl.xaml.cs L350-L355 已有转换实现

### 3. 性能考虑
- **大量图元**: DXF 文件可能有数千个实体，需确保 HalconCanvas.RenderEntities() 性能
- **优化**: 可设置 HalcanCanvas 的 LOD（细节层次）或只渲染特定类型实体
- **内存**: DxfParseResult 缓存在 ViewModel 生命周期内，页面关闭时应释放

### 4. 选取精度
- **最近点搜索**: FindNearestPointIndex 的容差半径需根据实际图形缩放级别动态调整
- **吸附效果**: 当鼠标靠近某点位时可显示吸附动画（可选增强）

---

## 🧪 测试场景

### 场景1: 完整流程
1. 打开 CadAlignmentView → 进入第三步
2. 点击"① 导入DXF文件" → 选择测试DXF
3. 图形在 HalconCanvas 中正确渲染
4. 点击"从CAD选取基准" → 状态提示变化
5. 在图形上依次点击两个点位 → 基准线段蓝色高亮
6. 点击"从CAD选取目标" → 再点击两个点位 → 目标线段红色高亮
7. 点击"③ 计算旋转角度" → 结果正确

### 场景2: 边界情况
- DXF 文件无有效图元 → 提示错误
- 只点击一个点就切换模式 → 部分完成状态保持
- 点击空白区域（无附近点位） → 忽略或提示
- 连续快速点击同一位置 → 防抖处理

### 场景3: 智能推荐
- 导入包含矩形特征的DXF → 推荐两条垂直边
- 用户接受推荐 → 直接进入计算
- 用户拒绝推荐 → 手动重新选取

---

## 📊 工作量估算

| Phase | 任务 | 复杂度 |
|-------|------|--------|
| Phase 1 | 基础图形化渲染 + 点击选取 | 中等 |
| Phase 2 | 视觉反馈增强（高亮/光标/标签） | 中等 |
| Phase 3 | 智能推荐算法 | 较低 |

**建议优先级**: Phase 1 → Phase 2 → Phase 3（Phase 3 为锦上添花）

---

**计划编制日期**: 2026-05-19
**适用版本**: net9.0-windows7.0
**依赖组件**: HalconCanvasControl, IDxfParserService, DxfParseResult, CadEntity
