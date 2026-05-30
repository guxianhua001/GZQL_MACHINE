# DeleteSelectedSegmentsCommand Bug 修复设计文档

**日期：** 2026-05-26
**状态：** ✅ 已批准
**方案：** 方案B - 概念分离（新增 IsSelected 属性）

---

## 1. 问题背景与目标

### 1.1 问题描述

**Bug 现象：** 在 Step3EditParamsPanel 中点击"删除选中"按钮时，**所有线段都被删除了**，而不是仅删除用户选中的线段。

**触发场景：**
1. 用户点击"全选"按钮 → 所有轨迹段的 `IsEnabled = true`
2. 用户点击"删除选中"按钮 → 所有 `IsEnabled == true` 的段被删除
3. **结果：所有线段都被删除** ❌

### 1.2 根本原因

**概念混淆：`IsEnabled` ≠ `IsSelected`**

| 属性 | 实际含义 | 当前误用 |
|------|---------|---------|
| `IsEnabled` | 是否启用参与走胶（批量设速/设胶的对象） | ❌ 被当作"选中删除"的标记 |
| `IsSelected` | 用户明确选中要操作的项 | ✅ DispenseSegment 模型中缺失此属性 |

**问题代码位置：** [CadPointEditorViewModel.cs:1736-1750](../../Module/Controls/Cad/CadPointEditorViewModel.cs#L1736-L1750)

```csharp
// 错误实现：使用 IsEnabled 作为删除筛选条件
var toDelete = Segments.Where(s => s.IsEnabled).ToList();
```

### 1.3 修复目标

1. ✅ 彻底修复 bug：删除操作只作用于用户明确勾选的线段
2. ✅ 概念清晰：区分"启用"和"选中"
3. ✅ 不影响现有功能：批量设速/设胶仍使用 `IsEnabled`
4. ✅ 符合架构规范：与 DotPoint 模型设计一致
5. ✅ 提升用户体验：复选框可视化选择状态
6. ✅ 工业控制安全性：破坏性操作需要明确的用户意图表达

---

## 2. 解决方案设计

### 2.1 方案对比

| 方案 | 思路 | 优点 | 缺点 | 推荐度 |
|-----|------|------|------|-------|
| A: 最小化修复 | 只删除当前选中的单条线段 | 改动最小、风险最低 | 无法批量删除、与命令名不符 | ⭐⭐ |
| **B: 概念分离** | **新增 IsSelected 属性** | **语义清晰、可扩展性好、符合架构规范** | 需修改3个文件 | **⭐⭐⭐⭐⭐** |
| C: 折中方案 | 增加确认对话框 | 改动较小、防止误操作 | 未解决根本问题、扩展性差 | ⭐⭐⭐ |

**最终选择：方案B（概念分离）**

### 2.2 设计原则

1. **单一职责原则**：`IsEnabled` 管理启用状态，`IsSelected` 管理选中状态
2. **安全优先**：新增属性默认值为 `false`（新线段默认不选中）
3. **一致性**：参考 [DotPoint.cs](../../Module/Models/DotPoint.cs#L127-L134) 的成熟设计
4. **可扩展性**：为未来选择性操作（复制、移动等）预留空间

---

## 3. 详细技术设计

### 3.1 数据模型层变更

#### 文件：[DispenseSegment.cs](../../Core/Models/DispenseSegment.cs)

**变更位置：** "开关控制"区域（约第95行后）

**新增代码：**
```csharp
#region 开关控制

private bool _isEnabled = true;
/// <summary>是否启用参与走胶（默认 true），同时作为批量操作的选择依据</summary>
public bool IsEnabled
{
    get => _isEnabled;
    set => SetProperty(ref _isEnabled, value);
}

// ===== 新增属性开始 =====
private bool _isSelected;
/// <summary>用户是否选中该轨迹段（用于删除等破坏性操作），默认 false</summary>
public bool IsSelected
{
    get => _isSelected;
    set { SetProperty(ref _isSelected, value); }
}
// ===== 新增属性结束 =====

#endregion
```

**设计要点：**
- 默认值为 `false`（安全原则）
- 使用 `SetProperty` 触发通知（支持 UI 自动更新）
- 与 DotPoint 模型的 IsSelected 设计保持一致

---

### 3.2 ViewModel 层变更

#### 文件：[CadPointEditorViewModel.cs](../../Module/Controls/Cad/CadPointEditorViewModel.cs)

##### 3.2.1 删除命令修复（第1736-1750行）

**修改前：**
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

**修改后：**
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

**关键改进：**
- 筛选条件从 `IsEnabled` 改为 `IsSelected`
- 只删除用户明确勾选的线段
- 不影响批量设速/设胶操作（它们仍使用 `IsEnabled`）

##### 3.2.2 全选命令修改（第1628-1633行）

**修改前：**
```csharp
private void ExecuteSelectAllSegments()
{
    foreach (var seg in Segments)
        seg.IsEnabled = true;
    DeleteSelectedSegmentsCommand.RaiseCanExecuteChanged();
}
```

**修改后：**
```csharp
/// <summary>全选所有轨迹段（同时设置 IsEnabled 和 IsSelected）</summary>
private void ExecuteSelectAllSegments()
{
    foreach (var seg in Segments)
    {
        seg.IsEnabled = true;   // 参与批量操作
        seg.IsSelected = true;  // 标记为选中（用于删除）
    }
    DeleteSelectedSegmentsCommand.RaiseCanExecuteChanged();
}
```

**设计说明：**
- 全选时同时设置两个属性
- 确保批量操作和删除操作的目标集合一致
- 符合用户直觉："全选"意味着选中所有项进行任何操作

##### 3.2.3 反选命令修改（第1635-1641行）

**修改前：**
```csharp
private void ExecuteInvertSelection()
{
    foreach (var seg in Segments)
        seg.IsEnabled = !seg.IsEnabled;
    DeleteSelectedSegmentsCommand.RaiseCanExecuteChanged();
}
```

**修改后：**
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

**设计说明：**
- 反选后 `IsSelected` 与 `IsEnabled` 保持同步
- 避免出现"启用了但未选中"或"选中了但未启用"的混乱状态

##### 3.2.4 CanExecute 条件修复（第935-939行）

**修改前：**
```csharp
/// <summary>删除启用轨迹段命令（删除 IsEnabled 为 true 的段）</summary>
public DelegateCommand DeleteSelectedSegmentsCommand =>
    _deleteSelectedSegmentsCommand ??= new DelegateCommand(
        ExecuteDeleteSelectedSegments,
        () => Segments.Any(s => s.IsEnabled));
```

**修改后：**
```csharp
/// <summary>删除选中轨迹段命令（删除 IsSelected 为 true 的段）</summary>
public DelegateCommand DeleteSelectedSegmentsCommand =>
    _deleteSelectedSegmentsCommand ??= new DelegateCommand(
        ExecuteDeleteSelectedSegments,
        () => Segments.Any(s => s.IsSelected));
```

**设计要点：**
- 按钮仅在用户勾选了至少一条线段时可用
- 避免误操作：未选中任何线段时按钮禁用
- 注释更新为准确的描述

##### 3.2.5 属性变更回调更新（第2340-2355行）

**修改前：**
```csharp
private void OnSegmentPropertyChanged(object sender, PropertyChangedEventArgs e)
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

**修改后：**
```csharp
/// <summary>段属性变更回调——IsEnabled 或 IsSelected 变更时触发 CanExecute 重新评估</summary>
private void OnSegmentPropertyChanged(object sender, PropertyChangedEventArgs e)
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

**关键改进：**
- 监听 `IsSelected` 属性变化
- 当用户在 DataGrid 中勾选/取消复选框时，UI 立即响应

---

### 3.3 UI 层变更

#### 文件：[Step3EditParamsPanel.xaml](../../Module/Controls/Cad/Step3EditParamsPanel.xaml)

##### 3.3.1 DataGrid 新增复选框列（第78行后）

**修改位置：** 轨迹段 DataGrid 的 Columns 区域

**修改前：**
```xml
<DataGridCheckBoxColumn Header="{lang:Lang Step3_Header_Enabled}"
                        Binding="{Binding IsEnabled, UpdateSourceTrigger=PropertyChanged}"
                        Width="50" IsReadOnly="False"/>
```

**修改后：**
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

**设计要点：**
- 放在 `IsEnabled` 列后面，逻辑顺序清晰
- 使用 `MaterialDesignCheckBox` 样式，与项目 UI 风格统一
- 宽度设为 50px，与其他复选框列一致
- 支持实时更新（`UpdateSourceTrigger=PropertyChanged`）

##### 3.3.2 多语言资源键新增

**中文资源文件：** [Strings.zh-CN.xaml](../../MainApp/Languages/Strings.zh-CN.xaml)
```xml
<sys:String x:Key="Step3_Header_Selected">选中</sys:String>
```

**英文资源文件：** [Strings.en-US.xaml](../../MainApp/Languages/Strings.en-US.xaml)
```xml
<sys:String x:Key="Step3_Header_Selected">Selected</sys:String>
```

---

## 4. 变更清单总览

| 序号 | 文件路径 | 变更类型 | 主要工作 | 工作量 |
|-----|---------|---------|---------|-------|
| 1 | [DispenseSegment.cs](../../Core/Models/DispenseSegment.cs) | 新增属性 | 添加 `IsSelected` 属性（5行） | ⭐ 低 |
| 2 | [CadPointEditorViewModel.cs](../../Module/Controls/Cad/CadPointEditorViewModel.cs) | 修改5处 | 删除命令+全选命令+反选命令+CanExecute+回调（约30行变更） | ⭐⭐ 中 |
| 3 | [Step3EditParamsPanel.xaml](../../Module/Controls/Cad/Step3EditParamsPanel.xaml) | UI调整 | DataGrid新增复选框列（5行） | ⭐ 低 |
| 4 | [Strings.zh-CN.xaml](../../MainApp/Languages/Strings.zh-CN.xaml) | 资源文件 | 新增1个中文资源键 | ⭐ 极低 |
| 5 | [Strings.en-US.xaml](../../MainApp/Languages/Strings.en-US.xaml) | 资源文件 | 新增1个英文资源键 | ⭐ 极低 |

**总计工作量：约 20-30 分钟**

---

## 5. 设计优势分析

### 5.1 彻底修复 Bug
- ✅ 删除操作只作用于用户明确勾选的线段
- ✅ 全选后再点击删除，行为符合预期
- ✅ 批量设速/设胶不受影响（仍使用 `IsEnabled`）

### 5.2 符合架构规范
- ✅ 与 DotPoint 模型设计保持一致
- ✅ 遵循 MVVM 模式，数据驱动 UI
- ✅ 单一职责原则：`IsEnabled` 管启用，`IsSelected` 管选中

### 5.3 用户体验提升
- ✅ 复选框让用户清晰看到哪些线段将被删除
- ✅ 未选中任何项时删除按钮自动禁用，防止误操作
- ✅ 支持灵活的多选/反选操作

### 5.4 工业控制安全性
- ✅ 破坏性操作需要明确的用户意图表达
- ✅ 符合工业设备控制的快速响应性和安全性要求

### 5.5 可扩展性好
未来可轻松支持更多选择性操作：
- 复制选中段
- 移动选中段到新位置
- 导出选中段数据
- 批量修改选中段的特定参数

---

## 6. 注意事项

### 6.1 向后兼容性
- 新增的 `IsSelected` 属性默认值为 `false`
- 已有序列化数据不受影响（新字段会被忽略或使用默认值）
- 建议在数据加载时初始化所有段的 `IsSelected = false`

### 6.2 性能考虑
- DataGrid 使用虚拟化（已配置 `VirtualizingStackPanel.IsVirtualizing="True"`）
- 即使有大量线段，UI 响应仍然流畅
- 符合性能要求："设计要考虑软件性能，尤其是运动控制这一块"

### 6.3 测试要点

#### 功能测试
- [ ] 全选 → 删除 → 验证只删除选中项
- [ ] 反选 → 删除 → 验证只删除反选后的选中项
- [ ] 手动勾选部分项 → 删除 → 验证精确删除
- [ ] 未勾选任何项 → 验证删除按钮禁用
- [ ] 全选 → 批量设速 → 验证所有段都被设置（`IsEnabled` 不受影响）

#### 边界测试
- [ ] 只有1条线段时的删除操作
- [ ] 有大量线段（100+）时的性能测试
- [ ] 快速连续勾选/取消复选框的响应速度
- [ ] 删除最后一条线段后的状态处理

#### 回归测试
- [ ] 批量设速功能正常（使用 `IsEnabled`）
- [ ] 批量设胶功能正常（使用 `IsEnabled`）
- [ ] 单点模式参数编辑正常
- [ ] 连续插补模式参数编辑正常
- [ ] ROI 工具正常工作
- [ ] 数据保存/加载功能正常

---

## 7. 风险评估

| 风险项 | 影响程度 | 发生概率 | 缓解措施 |
|-------|---------|---------|---------|
| 已有序列化数据兼容性 | 低 | 中 | 新属性有默认值，不影响旧数据 |
| 性能下降（大量线段） | 低 | 低 | DataGrid 已启用虚拟化 |
| 用户习惯改变 | 中 | 中 | 复选框直观易理解，学习成本低 |
| 其他模块依赖 IsEnabled | 低 | 低 | 仅修改删除命令逻辑，其他不变 |

**总体风险等级：** 🟢 低风险

---

## 8. 后续优化建议（可选）

### 8.1 短期优化（本次不实施）
- [ ] 在删除按钮旁添加提示文本："已选中 N 项"
- [ ] 删除前增加轻量级确认提示（Toast 通知）
- [ ] 支持 Shift+Click 连续选择复选框

### 8.2 中长期规划
- [ ] 增加"撤销删除"功能（维护删除历史栈）
- [ ] 支持拖拽排序选中项
- [ ] 增加右键菜单：复制/移动/导出选中项
- [ ] 支持按条件批量选择（如：选择所有长度 > 10mm 的段）

---

## 9. 总结

本设计方案通过**概念分离**策略，彻底解决了 DeleteSelectedSegmentsCommand 的 bug。核心思路是为 DispenseSegment 模型新增 `IsSelected` 属性，明确区分"启用"和"选中"两个概念。

**核心价值：**
1. **正确性**：彻底修复 bug，确保删除操作的精确性
2. **规范性**：符合 MVVM 架构和项目编码规范
3. **安全性**：符合工业控制的安全要求
4. **可维护性**：代码清晰易懂，便于后续扩展
5. **用户体验**：提供直观的可视化交互

**推荐立即实施。**
