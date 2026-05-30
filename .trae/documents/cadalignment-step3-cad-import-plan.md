# CadAlignmentView 第三步旋转角度 — CAD导入与点位提取功能实施计划

## 📋 功能概述

为CadAlignmentView第三步"旋转角度"添加CAD图纸导入功能，支持从DXF文件提取点位数据，并通过交互式选取实现基准线段和目标线段的提取，最终计算旋转角度θ。

## 🎯 核心目标

1. **导入DXF文件**：从DXF CAD图纸中解析并提取所有点位坐标
2. **点位预览与选择**：以DataGrid表格形式展示导入的点位，支持点击选取
3. **线段提取**：通过两次点击分别确定线段的起点和终点
4. **旋转角度计算**：基于选取的基准线和目标线段自动计算方向角差值θ

## ✅ 已完成的实现（基于之前的工作）

### 1. ViewModel层 (CadAlignmentViewModel.cs)

#### 新增属性

* `ImportedCadPoints` (ObservableCollection<CadPoint>) - 导入的CAD点位集合

* `SelectedCadPoint` (CadPoint) - DataGrid选中项，支持点击选取

* `BaseStartIndex` / `BaseEndIndex` (int) - 基准线段起点/终点索引

* `TargetStartIndex` / `TargetEndIndex` (int) - 目标线段起点/终点索引

* `CadFilePath` (string) - 导入文件路径显示

* `HasCadDrawingLoaded` (bool) - 控制UI可见性

* `CadPickStatus` (string) - 选取状态提示文本

* `_isPickingBaseline` / `_isPickingTarget` (bool) - 选取模式状态标志

#### 新增命令

* `ImportDxfCommand` - 导入DXF文件命令

* `PickBaselineFromCadCommand` - 从CAD选取基准线段命令

* `PickTargetFromCadCommand` - 从CAD选取目标线段命令

#### 核心方法实现

**① OnImportDxf() - DXF文件导入**

```csharp
// 功能流程：
// 1. 打开文件对话框，筛选DXF文件
// 2. 调用 DxfParser.ExtractPoints(fileName, null) 提取所有层点位
// 3. 将元组列表转换为 CadPoint 对象集合
// 4. 重置所有选取状态和计算结果
// 5. 更新UI状态提示
```

**② OnPickBaselineFromCad() / OnPickTargetFromCad() - 启动选取模式**

```csharp
// 功能流程：
// 1. 检查是否已导入CAD文件
// 2. 设置对应的选取模式标志 (_isPickingBaseline / _isPickingTarget)
// 3. 清空该线段的起终点索引
// 4. 更新状态提示文字，引导用户操作
```

**③ OnCadPointSelected(CadPoint) - DataGrid行点击处理**

```csharp
// 功能流程：
// 1. 获取点击行的索引位置
// 2. 根据当前选取模式（基准/目标）分配点位：
//   - 第一次点击 → 设为起点
//   - 第二次点击（不同行）→ 设为终点，立即计算方向角
// 3. 完成一条线段选取后自动退出选取模式
// 4. 如果两条线段都已完成，提示可进行角度计算
```

**④ ComputeCadRotationAngle() - 增强版角度计算**

```csharp
// 优先级逻辑：
// 1. 优先使用从CAD导入点位选取的线段（BaseEndIndex >= 0 && TargetEndIndex >= 0）
//    - 从 ImportedCadPoints 按索引取4个点
//    - 使用 atan2 计算两个向量方向角
//    - θ = α_base - α_target，归一化到 (-180°, +180°]
// 2. 回退到原有逻辑：从 CorrespondencePoints 按 PairIndex 取点
```

### 2. View层 (CadAlignmentView\.xaml)

#### UI布局结构（第三步Tab内）

```
┌─────────────────────────────────────────────────────┐
│ SectionCard: 向量方向角计算                           │
│ ┌─────────────────────────────────────────────────┐ │
│ │ ① 导入DXF文件 [📁导入DXF文件] 文件名.dxf         │ │
│ ├─────────────────────────────────────────────────┤ │
│ │ 基准线段: [ComboBox▼] [从CAD选取]                │ │
│ │ 目标线段: [ComboBox▼] [从CAD选取]                │ │
│ ├─────────────────────────────────────────────────┤ │
│ │ 📋 导入的CAD点位 (点击行依次设为起点/终点)       │ │
│ │ ┌─────┬──────┬──────┬──────┬──────┬──────┐      │ │
│ │ │ #   │ X    │ Y    │ Z    │ 基准 │ 目标 │      │ │
│ │ ├─────┼──────┼──────┼──────┼──────┼──────┤      │ │
│ │ │ 1   │ ...  │ ...  │ ...  │ 起点 │      │      │ │
│ │ │ 2   │ ...  │ ...  │ ...  │ 终点 │      │      │ │
│ │ │ 3   │ ...  │ ...  │ ...  │      │ 起点 │      │ │
│ │ │ 4   │ ...  │ ...  │ ...  │      │ 终点 │      │ │
│ │ └─────┴──────┴──────┴──────┴──────┴──────┘      │ │
│ │ 选取状态提示文字...                               │ │
│ ├─────────────────────────────────────────────────┤ │
│ │                        [③ 计算旋转角度]          │ │
│ └─────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────┘
```

#### 关键UI特性

1. **导入按钮区域**

   * 使用 SecondaryActionButton 样式

   * FileImportOutline 图标

   * 导入成功后显示文件名（带 StringNotEmptyToVis 转换器控制可见性）

2. **点位预览DataGrid**

   * 通过 HasCadDrawingLoaded 控制整体可见性

   * 列定义：# | X | Z | Y | 基准(模板列) | 目标(模板列)

   * 基准/目标列使用 DataTrigger 动态显示"起点"/"终点"标记

   * SelectedItem 绑定到 SelectedCadPoint（Mode=OneWayToSource）

   * 单选模式（SelectionMode="Single"），只读（IsReadOnly="True"）

3. **状态提示文字**

   * 绑定 CadPickStatus 属性

   * 橙色前景色（#FF5722）+ 斜体，突出显示当前操作指引

## 🔍 技术依赖验证清单

| 依赖项                       | 状态    | 文件路径                                                           |
| ------------------------- | ----- | -------------------------------------------------------------- |
| DxfParser.ExtractPoints() | ✅ 已存在 | Module\Services\DxfParser.cs                                   |
| CadPoint 模型类              | ✅ 已存在 | Core\Models\CadPoint.cs                                        |
| StringNotEmptyToVis 转换器   | ✅ 已存在 | Module\Converters\StringNotEmptyToVisibilityConverter.cs       |
| BoolToVis 转换器             | ✅ 已存在 | WPF内置                                                          |
| MaterialDesign图标库         | ✅ 已引用 | PackIcon: FileImportOutline, CrosshairsGps, VectorIntersection |

## 📊 数据流图

```
用户操作                    ViewModel                          Model/Service
─────────────────────────────────────────────────────────────────────────
点击"导入DXF"  ──────→  OnImportDxf()
                              │
                              ▼
                       OpenFileDialog.ShowDialog()
                              │
                              ▼
                   DxfParser.ExtractPoints(path, null)
                              │
                              ▼
                   List<(X,Y,Z)] → ObservableCollection<CadPoint>
                              │
                              ▼
                         更新UI显示点位列表
                              
点击"从CAD选取基准" ──→  OnPickBaselineFromCad()
                              │
                              ▼
                    _isPickingBaseline = true
                              │
                              ▼
                      更新状态提示文字

点击DataGrid某行  ─────→  OnCadPointSelected(point)
                              │
                              ▼
                    判断当前选取模式
                    ├── _isPickingBaseline → 分配给 BaseStartIndex/BaseEndIndex
                    └── _isPickingTarget  → 分配给 TargetStartIndex/TargetEndIndex
                              │
                              ▼
                    立即计算该线段方向角 α
                              │
                              ▼
                     更新 AlphaBaseDeg 或 AlphaTargetDeg

点击"计算旋转角度" ──→  ComputeCadRotationAngle()
                              │
                              ▼
              检查 BaseEndIndex && TargetEndIndex 是否有效
                              │
                              ▼
           从 ImportedCadPoints 取4个点 (p1,p2,p3,p4)
                              │
                              ▼
         alphaBase = atan2(p2.y-p1.y, p2.x-p1.x) * 180/π
         alphaTarget = atan2(p4.y-p3.y, p4.x-p3.x) * 180/π
                              │
                              ▼
                  theta = alphaBase - alphaTarget
                  归一化到 (-180°, +180°]
                              │
                              ▼
                 更新 ThetaDeg, Step3Done = true
```

## 🎨 用户操作流程

### 场景：使用CAD导入功能计算旋转角度

**步骤1：导入DXF文件**

1. 用户点击"① 导入DXF文件"按钮
2. 弹出文件选择对话框，用户选择 .dxf 文件
3. 系统解析文件，提取所有点位数据显示在表格中
4. 状态栏显示："✓ 已导入 N 个点位，请分别选取基准线段和目标线段"

**步骤2：选取基准线段**

1. 用户点击"从CAD选取"按钮（基准线段旁）
2. 状态提示变为："请在下方点位列表中点击选择基准线段的【起点】..."
3. 用户点击DataGrid第1行 → 该行"基准"列显示"起点"
4. 状态提示变为："基准【起点】已选: #1 (x,y)，请再点击一行作为【终点】"
5. 用户点击第2行 → 该行"基准"列显示"终点"
6. 自动计算基准方向角 α\_base 并显示
7. 状态提示变为："✓ 基准线段: #1→#2, α\_base=xx.xx°"

**步骤3：选取目标线段**

1. 用户点击"从CAD选取"按钮（目标线段旁）
2. 状态提示引导用户选取目标线段起点
3. 用户依次点击两行，完成目标线段选取
4. 自动计算目标方向角 α\_target 并显示
5. 如果基准线段也已选取完成，状态提示追加："| 两线段已就绪，可点击「③ 计算旋转角度」"

**步骤4：计算旋转角度**

1. 用户点击"③ 计算旋转角度"按钮
2. 系统计算 θ = α\_base - α\_target
3. 结果显示在角度结果卡片中（大字高亮）
4. Step3Done = true，可进入下一步骤

## ⚠️ 注意事项与边界情况

### 1. DxfParser layerName 参数

* 当前调用传入 `null`，表示提取所有图层的点位

* 如果需要按图层过滤，可修改为让用户选择图层名称

### 2. 点位索引边界检查

* ✅ 已实现：OnCadPointSelected 中检查 idx >= 0

* ✅ 已实现：选取终点时检查 idx != StartIndex（避免重复选同一点）

* ⚠️ 建议：增加 ImportedCadPoints.Count 边界检查

### 3. 选取模式互斥

* ✅ 已实现：启动基准选取时 \_isPickingTarget = false，反之亦然

* ✅ 已实现：每次启动选取前清空该线段的起终点索引

### 4. 状态重置时机

* ✅ 已实现：导入新DXF文件时重置所有选取状态和计算结果

* ⚠️ 建议：考虑添加"清除选取"按钮，允许用户重新选取而不必重新导入

### 5. DataGrid性能考虑

* 当前设置 Height="150"，限制可视区域避免大量点位时性能问题

* 如果点位数量超过100个，建议考虑虚拟化（VirtualizingStackPanel.IsVirtualizing="True"）

## 🧪 测试计划

### 单元测试要点

1. **DXF导入测试**

   * 测试空文件处理

   * 测试无效格式文件异常捕获

   * 测试正常DXF文件的点位提取准确性

2. **点位选取逻辑测试**

   * 测试起点/终点分配正确性

   * 测试重复点击同一行的拒绝逻辑

   * 测试选取模式切换时的状态清理

3. **角度计算测试**

   * 测试已知坐标的角度计算精度

   * 测试特殊角度（0°、90°、180°、-90°）

   * 测试角度归一化逻辑（如 270° → -90°）

### 集成测试场景

1. **完整流程测试**

   ```
   导入DXF → 选取基准线段 → 选取目标线段 → 计算角度 → 验证结果
   ```

2. **边界情况测试**

   * 只选取基准线段不选取目标线段直接点计算

   * 选取过程中切换到其他步骤再切回

   * 连续多次导入不同DXF文件

3. **UI交互测试**

   * DataGrid行点击响应速度

   * 状态提示文字实时更新

   * 按钮启用/禁用状态变化

## 📝 后续优化建议（可选）

### 短期优化

1. **可视化增强**

   * 在DataGraphView中绘制选取的线段（需要集成图形渲染）

   * 用不同颜色高亮基准线段（蓝色）和目标线段（红色）

2. **用户体验优化**

   * 添加键盘快捷键支持（如Esc取消选取）

   * 支持拖拽调整列宽

   * 添加点位搜索/过滤功能（当点位很多时）

3. **功能扩展**

   * 支持导入多个DXF文件合并点位

   * 支持导出选取结果为JSON/XML

   * 添加撤销/重做选取操作

### 长期优化

1. **图形化选取界面**

   * 替代DataGrid表格选取，改为在CAD图形窗口中直接点击点位

   * 需要集成Halcon或WPF图形渲染引擎

2. **智能推荐**

   * 根据点位分布自动推荐可能的基准/目标线段

   * 基于历史数据学习常用的选取模式

## ✅ 实施状态总结

| 模块            | 状态    | 完成度  |
| ------------- | ----- | ---- |
| ViewModel属性定义 | ✅ 完成  | 100% |
| ViewModel命令绑定 | ✅ 完成  | 100% |
| DXF导入逻辑       | ✅ 完成  | 100% |
| 点位选取逻辑        | ✅ 完成  | 100% |
| 角度计算增强        | ✅ 完成  | 100% |
| XAML UI布局     | ✅ 完成  | 100% |
| DataGrid绑定    | ✅ 完成  | 100% |
| 状态提示机制        | ✅ 完成  | 100% |
| 编译验证          | ⏳ 待执行 | -    |
| 功能测试          | ⏳ 待执行 | -    |

## 🚀 下一步行动

1. **编译验证**

   ```bash
   dotnet build c:\WorkFiles\GZQL_MACHINE\Module\Module.csproj
   ```

2. **修复编译错误**（如有）

   * 检查命名空间引用

   * 检查属性绑定路径

   * 检查转换器资源引用

3. **功能测试**

   * 准备测试用DXF文件

   * 执行完整操作流程

   * 验证计算结果准确性

4. **性能测试**

   * 测试大点位量（500+）时的响应速度

   * 验证内存占用是否合理

5. **文档更新**

   * 更新版本修改记录.txt

   * 补充关键方法的XML注释（已部分完成）

***

**计划编制日期**: 2026-05-19\
**适用版本**: net9.0-windows7.0\
**相关文件**:

* CadAlignmentViewModel.cs (L250-L740)

* CadAlignmentView\.xaml (L488-L624)

* DxfParser.cs (Module\Services)

* CadPoint.cs (Core\Models)

