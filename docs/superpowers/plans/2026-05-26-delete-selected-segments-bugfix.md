# DeleteSelectedSegmentsCommand Bug 修复实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复 DeleteSelectedSegmentsCommand 的 bug，通过为 DispenseSegment 新增 IsSelected 属性实现概念分离，确保删除操作只作用于用户明确选中的线段。

**Architecture:** 采用 MVVM 模式修改三层架构：数据模型层新增 IsSelected 属性 → ViewModel 层修改删除/全选/反选/CanExecute 逻辑 → UI 层 DataGrid 新增复选框列。保持与 DotPoint 模型设计一致，不影响现有批量设速/设胶功能（仍使用 IsEnabled）。

**Tech Stack:** WPF .NET 9.0, Prism 8.x, MaterialDesignInXAML, C# 13, 多语言支持(zh-CN/en-US)

---

## 文件结构

### 需要修改的文件（5个）

| 序号 | 文件路径 | 职责 | 主要变更 |
|-----|---------|------|---------|
| 1 | `Core/Models/DispenseSegment.cs` | 轨迹段数据模型 | 新增 IsSelected 属性 |
| 2 | `Module/Controls/Cad/CadPointEditorViewModel.cs` | ViewModel 核心逻辑 | 修改5处：删除命令+全选+反选+CanExecute+属性回调 |
| 3 | `Module/Controls/Cad/Step3EditParamsPanel.xaml` | 参数面板UI | DataGrid 新增"选中"复选框列 |
| 4 | `MainApp/Languages/Strings.zh-CN.xaml` | 中文资源文件 | 新增 Step3_Header_Selected 键 |
| 5 | `MainApp/Languages/Strings.en-US.xaml` | 英文资源文件 | 新增 Step3_Header_Selected 键 |

### 变更依赖关系

```
Task 1 (数据模型)
    ↓
Task 2 (ViewModel) ← 依赖 Task 1 的 IsSelected 属性
    ↓
Task 3 (UI层) + Task 4 (多语言) ← 可并行
    ↓
Task 5 (编译测试) ← 依赖所有前置任务
```

---

## Task 1: DispenseSegment 数据模型 - 新增 IsSelected 属性

**Files:**
- Modify: `Core/Models/DispenseSegment.cs` (在"开关控制"区域新增)

**目标：** 为 DispenseSegment 模型新增 `IsSelected` 布尔属性，默认值为 false。

- [ ] **Step 1: 定位插入位置**

在 DispenseSegment.cs 中找到"开关控制"区域（约第95行），在 `IsEnabled` 属性定义结束后插入新代码。

当前代码位置：
```csharp
// 第 95-105 行区域
#region 开关控制

private bool _isEnabled = true;
/// <summary>是否启用参与走胶（默认 true），同时作为批量操作的选择依据</summary>
public bool IsEnabled
{
    get => _isEnabled;
    set => SetProperty(ref _isEnabled, value);
}

#endregion
```

- [ ] **Step 2: 插入 IsSelected 属性**

在 `IsEnabled` 属性的 `#endregion` 之前（或之后）添加以下代码：

```csharp
private bool _isSelected;
/// <summary>用户是否选中该轨迹段（用于删除等破坏性操作），默认 false</summary>
public bool IsSelected
{
    get => _isSelected;
    set { SetProperty(ref _isSelected, value); }
}
```

**完整代码块（包含上下文）：**
```csharp
#region 开关控制

private bool _isEnabled = true;
/// <summary>是否启用参与走胶（默认 true），同时作为批量操作的选择依据</summary>
public bool IsEnabled
{
    get => _isEnabled;
    set => SetProperty(ref _isEnabled, value);
}

private bool _isSelected;
/// <summary>用户是否选中该轨迹段（用于删除等破坏性操作），默认 false</summary>
public bool IsSelected
{
    get => _isSelected;
    set { SetProperty(ref _isSelected, value); }
}

#endregion
```

- [ ] **Step 3: 验证代码编译**

Run: `dotnet build Core/Core.csproj`
Expected: Build succeeded with no errors

- [ ] **Step 4: Commit**

```bash
git add Core/Models/DispenseSegment.cs
git commit -m "feat(DispenseSegment): add IsSelected property for selective deletion"
```

---

## Task 2: CadPointEditorViewModel - 修复删除与选择逻辑

**Files:**
- Modify: `Module/Controls/Cad/CadPointEditorViewModel.cs` (5处修改)

**目标：** 修改删除命令使用 IsSelected 筛选，更新全选/反选/CanExecute/属性回调逻辑。

### 2.1 修改 CanExecute 条件（第935-939行）

- [ ] **Step 1: 定位 DeleteSelectedSegmentsCommand 定义**

找到第 935-939 行的命令定义：

```csharp
/// <summary>删除启用轨迹段命令（删除 IsEnabled 为 true 的段）</summary>
public DelegateCommand DeleteSelectedSegmentsCommand =>
    _deleteSelectedSegmentsCommand ??= new DelegateCommand(
        ExecuteDeleteSelectedSegments,
        () => Segments.Any(s => s.IsEnabled));
```

- [ ] **Step 2: 修改 CanExecute 条件**

将 CanExecute 委托中的筛选条件从 `s => s.IsEnabled` 改为 `s => s.IsSelected`：

```csharp
/// <summary>删除选中轨迹段命令（删除 IsSelected 为 true 的段）</summary>
public DelegateCommand DeleteSelectedSegmentsCommand =>
    _deleteSelectedSegmentsCommand ??= new DelegateCommand(
        ExecuteDeleteSelectedSegments,
        () => Segments.Any(s => s.IsSelected));
```

### 2.2 修改全选命令（第1628-1636行）

- [ ] **Step 3: 定位 ExecuteSelectAllSegments 方法**

找到第 1628-1636 行的全选方法：

```csharp
/// <summary>全选所有轨迹段（设置 IsEnabled = true）</summary>
private void ExecuteSelectAllSegments()
{
    foreach (var seg in Segments)
        seg.IsEnabled = true;
    DeleteSelectedSegmentsCommand.RaiseCanExecuteChanged();
}
```

- [ ] **Step 4: 更新全选逻辑**

在全选循环中同时设置 `IsEnabled` 和 `IsSelected`：

```csharp
/// <summary>全选所有轨迹段（同时设置 IsEnabled 和 IsSelected）</summary>
private void ExecuteSelectAllSegments()
{
    foreach (var seg in Segments)
    {
        seg.IsEnabled = true;   // 参与批量操作（设速/设胶）
        seg.IsSelected = true;  // 标记为选中（用于删除）
    }
    DeleteSelectedSegmentsCommand.RaiseCanExecuteChanged();
}
```

### 2.3 修改反选命令（第1638-1644行）

- [ ] **Step 5: 定位 ExecuteInvertSelection 方法**

找到第 1638-1644 行的反选方法：

```csharp
/// <summary>反选轨迹段的启用状态（IsEnabled 取反）</summary>
private void ExecuteInvertSelection()
{
    foreach (var seg in Segments)
        seg.IsEnabled = !seg.IsEnabled;
    DeleteSelectedSegmentsCommand.RaiseCanExecuteChanged();
}
```

- [ ] **Step 6: 更新反选逻辑**

在反转 `IsEnabled` 后同步更新 `IsSelected`：

```csharp
/// <summary>反选轨迹段的启用和选中状态</summary>
private void ExecuteInvertSelection()
{
    foreach (var seg in Segments)
    {
        seg.IsEnabled = !seg.IsEnabled;   // 反转启用状态
        seg.IsSelected = seg.IsEnabled;   // 同步选中状态
    }
    DeleteSelectedSegmentsCommand.RaiseCanExecuteChanged();
}
```

### 2.4 修改删除命令（第1736-1750行）

- [ ] **Step 7: 定位 ExecuteDeleteSelectedSegments 方法**

找到第 1736-1750 行的删除方法：

```csharp
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
```

- [ ] **Step 8: 修复删除逻辑**

将筛选条件从 `s => s.IsEnabled` 改为 `s => s.IsSelected`：

```csharp
/// <summary>删除所有 IsSelected 为 true 的轨迹段</summary>
private void ExecuteDeleteSelectedSegments()
{
    var toDelete = Segments.Where(s => s.IsSelected).ToList();
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
```

### 2.5 修改属性变更回调（第2340-2355行）

- [ ] **Step 9: 定位 OnSegmentPropertyChanged 方法**

找到第 2340-2355 行的属性回调方法：

```csharp
/// <summary>段属性变更回调——IsEnabled 变更时触发 CanExecute 重新评估</summary>
private void OnSegmentPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
{
    if (e.PropertyName == nameof(DispenseSegment.IsEnabled))
    {
        RaisePropertyChanged(nameof(CanExecute));
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
```

- [ ] **Step 10: 扩展监听范围**

在 if 条件中增加对 `IsSelected` 属性的监听：

```csharp
/// <summary>段属性变更回调——IsEnabled 或 IsSelected 变更时触发 CanExecute 重新评估</summary>
private void OnSegmentPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
{
    if (e.PropertyName == nameof(DispenseSegment.IsEnabled) ||
        e.PropertyName == nameof(DispenseSegment.IsSelected))
    {
        RaisePropertyChanged(nameof(CanExecute));
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
```

- [ ] **Step 11: 编译验证**

Run: `dotnet build Module/Module.csproj`
Expected: Build succeeded with no errors

- [ ] **Step 12: Commit**

```bash
git add Module/Controls/Cad/CadPointEditorViewModel.cs
git commit -m "fix(DeleteSelectedSegments): use IsSelected instead of IsEnabled for deletion"
```

---

## Task 3: Step3EditParamsPanel UI - DataGrid 新增复选框列

**Files:**
- Modify: `Module/Controls/Cad/Step3EditParamsPanel.xaml` (DataGrid Columns 区域)

**目标：** 在轨迹段 DataGrid 中新增"选中"复选框列，绑定到 IsSelected 属性。

- [ ] **Step 1: 定位 DataGrid Columns 区域**

打开 Step3EditParamsPanel.xaml，找到显示 Segments 的 DataGrid（约第70-100行区域），定位到现有的 `IsEnabled` 复选框列：

```xml
<!-- 当前代码（约第77-79行） -->
<DataGridCheckBoxColumn Header="{lang:Lang Step3_Header_Enabled}"
                        Binding="{Binding IsEnabled, UpdateSourceTrigger=PropertyChanged}"
                        Width="50" IsReadOnly="False"/>
```

- [ ] **Step 2: 在 Enabled 列后插入 Selected 列**

在 `IsEnabled` 复选框列的结束标签后添加新的复选框列：

```xml
<!-- 启用复选框列（用于批量设速/设胶） -->
<DataGridCheckBoxColumn Header="{lang:Lang Step3_Header_Enabled}"
                        Binding="{Binding IsEnabled, UpdateSourceTrigger=PropertyChanged}"
                        Width="50"
                        IsReadOnly="False"/>

<!-- 选中复选框列（用于删除操作） - 新增 -->
<DataGridCheckBoxColumn Header="{lang:Lang Step3_Header_Selected}"
                        Binding="{Binding IsSelected, UpdateSourceTrigger=PropertyChanged}"
                        Width="50"
                        IsReadOnly="False"
                        ElementStyle="{StaticResource MaterialDesignCheckBox}"/>
```

**完整上下文（DataGrid.Columns 部分）：**
```xml
<DataGrid.Columns>
    <DataGridTextColumn Header="{lang:Lang Step3_Header_ID}" Binding="{Binding SegmentId}" Width="90"/>
    <DataGridCheckBoxColumn Header="{lang:Lang Step3_Header_Enabled}"
                            Binding="{Binding IsEnabled, UpdateSourceTrigger=PropertyChanged}"
                            Width="50"
                            IsReadOnly="False"/>
    <!-- 选中复选框列（用于删除操作） - 新增 -->
    <DataGridCheckBoxColumn Header="{lang:Lang Step3_Header_Selected}"
                            Binding="{Binding IsSelected, UpdateSourceTrigger=PropertyChanged}"
                            Width="50"
                            IsReadOnly="False"
                            ElementStyle="{StaticResource MaterialDesignCheckBox}"/>
    <DataGridTextColumn Header="{lang:Lang Step3_Header_Type}" Binding="{Binding EntityType}" Width="55"/>
    <!-- ... 其余列保持不变 ... -->
</DataGrid.Columns>
```

- [ ] **Step 3: 验证 XAML 语法**

检查 XAML 是否有语法错误：
- 确认标签正确闭合
- 确认绑定路径拼写正确（`IsSelected` 大小写敏感）
- 确认资源键引用格式正确

- [ ] **Step 4: Commit**

```bash
git add Module/Controls/Cad/Step3EditParamsPanel.xaml
git commit -m "feat(UI): add IsSelected checkbox column to segment DataGrid"
```

---

## Task 4: 多语言资源 - 新增"选中"列标题

**Files:**
- Modify: `MainApp/Languages/Strings.zh-CN.xaml`
- Modify: `MainApp/Languages/Strings.en-US.xaml`

**目标：** 为新增的"选中"复选框列添加中英文资源键。

### 4.1 中文资源（zh-CN）

- [ ] **Step 1: 定位 Step3 资源区域**

在 Strings.zh-CN.xaml 中搜索已有的 Step3 相关资源键（如 `Step3_Header_Enabled`），在其附近插入新键。

- [ ] **Step 2: 添加中文资源键**

```xml
<!-- 在 Step3_Header_Enabled 附近添加 -->
<sys:String x:Key="Step3_Header_Selected">选中</sys:String>
```

### 4.2 英文资源（en-US）

- [ ] **Step 3: 定位 Step3 资源区域**

在 Strings.en-US.xaml 中搜索对应的 Step3 区域。

- [ ] **Step 4: 添加英文资源键**

```xml
<sys:String x:Key="Step3_Header_Selected">Selected</sys:String>
```

- [ ] **Step 5: Commit**

```bash
git add MainApp/Languages/Strings.zh-CN.xaml MainApp/Languages/Strings.en-US.xaml
git commit -m "feat(i18n): add Step3_Header_Selected resource key (zh-CN/en-US)"
```

---

## Task 5: 编译验证与功能测试

**Files:** 无需修改（仅运行测试）

**目标：** 验证所有修改编译通过，功能符合预期。

- [ ] **Step 1: 完整项目编译**

Run: `dotnet build GZQL_MACHINE.sln`
Expected: Build succeeded, 0 errors, 0 warnings

- [ ] **Step 2: 功能测试清单**

请手动执行以下测试场景并记录结果：

#### 场景 A：基本删除功能
- [ ] **A1: 手动勾选部分线段的"选中"复选框**
- [ ] **A2: 点击"删除选中"按钮**
- [ ] **A3: 验证只删除了勾选的线段，未勾选的保留**
- [ ] **A4: 验证画布上的图元同步移除**

#### 场景 B：全选后删除
- [ ] **B1: 点击"全选"按钮**
- [ ] **B2: 验证所有线段的"启用"和"选中"复选框都被勾选**
- [ ] **B3: 点击"删除选中"按钮**
- [ ] **B4: 验证所有线段被删除（这是预期行为）**

#### 场景 C：反选后删除
- [ ] **C1: 先点击"全选"按钮**
- [ ] **C2: 再点击"反选"按钮**
- [ ] **C3: 验证所有复选框取消勾选**
- [ ] **C4: 手动勾选部分线段的"选中"复选框**
- [ ] **C5: 点击"删除选中"按钮**
- [ ] **C6: 验证只删除了手动勾选的线段**

#### 场景 D：按钮可用性
- [ ] **D1: 打开面板，不勾选任何"选中"复选框**
- [ ] **D2: 验证"删除选中"按钮处于禁用状态（灰色不可点击）**
- [ ] **D3: 勾选任意一个线段的"选中"复选框**
- [ ] **D4: 验证"删除选中"按钮变为可用状态（红色可点击）**
- [ ] **D5: 取消勾选所有"选中"复选框**
- [ ] **D6: 验证"删除选中"按钮再次变为禁用状态**

#### 场景 E：批量操作不受影响
- [ ] **E1: 全选所有线段**
- [ ] **E2: 取消部分线段的"启用"复选框（但保持"选中"勾选）**
- [ ] **E3: 点击"批量设速"按钮，输入速度值**
- [ ] **E4: 验证只有"启用"的线段速度被更改，"未启用"的不变**
- [ ] **E5: 点击"删除选中"按钮**
- [ ] **E6: 验证所有"选中"的线段都被删除（无论是否"启用"）**

#### 场景 F：边界情况
- [ ] **F1: 只有1条线段时的删除操作**
- [ ] **F2: 删除最后一条线段后的界面状态**
- [ ] **F3: 快速连续勾选/取消复选框的响应速度**

- [ ] **Step 3: 记录测试结果**

如果所有测试通过：
```bash
# 可选：打 tag 标记此修复版本
git tag -a v1.0.1-fix-delete-selected -m "Fix DeleteSelectedSegmentsCommand bug"
```

如果有任何测试失败，记录失败场景并回滚到上一次提交：
```bash
git revert HEAD~4  # 回滚最近4个提交（Task 1-4）
```

---

## 自审清单（Self-Review）

### ✅ Spec 覆盖率检查

| 设计文档章节 | 对应任务 | 覆盖状态 |
|-----------|---------|---------|
| 3.1 数据模型层 - IsSelected 属性 | Task 1 | ✅ 已覆盖 |
| 3.2.1 删除命令修复 | Task 2.4 (Step 7-8) | ✅ 已覆盖 |
| 3.2.2 全选命令修改 | Task 2.2 (Step 3-4) | ✅ 已覆盖 |
| 3.2.3 反选命令修改 | Task 2.3 (Step 5-6) | ✅ 已覆盖 |
| 3.2.4 CanExecute 条件修复 | Task 2.1 (Step 1-2) | ✅ 已覆盖 |
| 3.2.5 属性回调更新 | Task 2.5 (Step 9-10) | ✅ 已覆盖 |
| 3.3.1 DataGrid 复选框列 | Task 3 | ✅ 已覆盖 |
| 3.3.2 多语言资源键 | Task 4 | ✅ 已覆盖 |
| 6.3 测试要点 | Task 5 | ✅ 已覆盖 |

**结论：** 所有设计要求均已映射到具体任务 ✅

### ✅ 占位符扫描

- ❌ 无 TBD/TODO 标记
- ❌ 无"待补充"、"后续实现"表述
- ✅ 所有代码示例完整可执行
- ✅ 所有步骤包含具体的文件路径和行号
- ✅ 所有 Run 命令精确到参数和预期输出

**结论：** 无占位符问题 ✅

### ✅ 类型一致性检查

| 属性名 | Task 1 定义 | Task 2 使用 | Task 3 绑定 | 一致性 |
|-------|------------|------------|------------|--------|
| IsSelected | bool, 默认false | s.IsSelected | {Binding IsSelected} | ✅ 一致 |
| IsEnabled | 保持不变 | seg.IsEnabled | {Binding IsEnabled} | ✅ 一致 |
| 方法名 | - | ExecuteDeleteSelectedSegments | - | ✅ 一致 |
| 命令名 | - | DeleteSelectedSegmentsCommand | Command={Binding ...} | ✅ 一致 |

**结论：** 所有类型和命名一致 ✅

---

## 执行选项

**Plan complete and saved to `docs/superpowers/plans/2026-05-26-delete-selected-segments-bugfix.md`. Two execution options:**

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration
   - 优点：每个任务独立执行，出错不影响其他任务
   - 适合：需要严格质量控制的 bug 修复
   - 工作流：Task 1 → Review → Task 2 → Review → ... → Task 5 → Final Review

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints
   - 优点：速度快，上下文连续
   - 适合：开发者熟悉代码库，快速迭代
   - 工作流：一次性执行所有 Task，关键节点 checkpoint

**Which approach?**
