# Dispense工具参数跟随选中段同步 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** DispenseDetailView 的工艺参数跟随 Step3EditParamsPanel 选中行变化——选中不同段时，DispenseDetailView 的默认参数自动更新为该段的参数值。

**Architecture:** 新增 `SelectedSegmentChangedEvent` 事件，CadPointEditorViewModel 在 SelectedSegment 变化时发布，DispenseDetailViewModel 订阅并批量更新所有默认参数。用户在 DispenseDetailView 修改参数时，通过已有的 `SegmentParamChangedEvent` 反向同步到选中段。

**Tech Stack:** WPF + Prism EventAggregator + MVVM

---

## 问题分析

当前同步机制只有 `SegmentParamChangedEvent`（段参数**值被修改**时触发），但**选中行切换**时不会触发任何同步事件。所以：

1. 用户在 Step3EditParamsPanel 点击段 B → DispenseDetailView 仍显示段 A 的参数（或初始默认值）
2. 用户在 DispenseDetailView 修改参数 → 不会同步到当前选中段

需要新增：选中段变更时，将选中段参数同步到 DispenseDetailView；DispenseDetailView 修改参数时，同步到选中段。

---

## 文件变更清单

| 文件 | 操作 | 职责 |
|------|------|------|
| `Core/Events/DispenseParamsSyncEvent.cs` | 修改 | 新增 `SelectedSegmentChangedEvent` 事件 |
| `Module/Controls/Cad/CadPointEditorViewModel.cs` | 修改 | SelectedSegment setter 中发布选中段变更事件 |
| `Module/Controls/StepDetails/DispenseDetailViewModel.cs` | 修改 | 订阅选中段变更事件，批量更新默认参数；修改默认参数 setter 时发布到选中段 |

---

### Task 1: 新增 SelectedSegmentChangedEvent 事件

**Files:**
- Modify: `Core/Events/DispenseParamsSyncEvent.cs`

- [ ] **Step 1: 在事件文件中添加新事件定义**

在 `DispenseParamsSyncEvent.cs` 末尾添加：

```csharp
using Prism.Events;

namespace Core.Events
{
    /// <summary>
    /// 段参数变更事件——Step3EditParamsPanel 修改段参数时发布，
    /// 通知 DispenseDetailViewModel 反向同步默认参数
    /// </summary>
    public class SegmentParamChangedEvent : PubSubEvent<SegmentParamPayload> { }

    /// <summary>
    /// 段参数变更载荷——携带变更属性名和段引用
    /// </summary>
    public class SegmentParamPayload
    {
        public string PropertyName { get; init; }
        public Core.Models.DispenseSegment Segment { get; init; }
    }

    /// <summary>
    /// 选中段变更事件——Step3EditParamsPanel 选中行切换时发布，
    /// 通知 DispenseDetailViewModel 将默认参数更新为选中段的参数
    /// </summary>
    public class SelectedSegmentChangedEvent : PubSubEvent<SelectedSegmentPayload> { }

    /// <summary>
    /// 选中段变更载荷——携带新选中的段引用（null 表示无选中）
    /// </summary>
    public class SelectedSegmentPayload
    {
        public Core.Models.DispenseSegment Segment { get; init; }
    }
}
```

- [ ] **Step 2: 构建验证**

Run: `dotnet build Core\Core.csproj --no-restore -v q`
Expected: Build succeeded, 0 error

---

### Task 2: CadPointEditorViewModel 发布选中段变更事件

**Files:**
- Modify: `Module/Controls/Cad/CadPointEditorViewModel.cs`

- [ ] **Step 1: 在 SelectedSegment setter 中发布事件**

找到 `SelectedSegment` 属性的 setter（约 L377-400），在 `SetProperty` 成功后添加事件发布：

当前代码：
```csharp
public DispenseSegment SelectedSegment
{
    get => _selectedSegment;
    set
    {
        if (_selectedSegment != null)
            _selectedSegment.PropertyChanged -= OnSelectedSegmentParamChanged;

        if (SetProperty(ref _selectedSegment, value))
        {
            if (_selectedSegment != null)
                _selectedSegment.PropertyChanged += OnSelectedSegmentParamChanged;

            RaisePropertyChanged(nameof(HasSelectedSegment));
            RaisePropertyChanged(nameof(ShowContinuousInterpolationParams));
            SelectedSegmentPoints = value?.Points;
            SegmentSplitCount = value?.SamplePointCount > 0 ? value.SamplePointCount : value?.Points?.Count ?? 1;
            SyncSelectedEntityFromSegment(value);
            ApplySegmentSplitCommand.RaiseCanExecuteChanged();
            ExtractCADZValuesCommand.RaiseCanExecuteChanged();
        }
    }
}
```

修改为：
```csharp
public DispenseSegment SelectedSegment
{
    get => _selectedSegment;
    set
    {
        if (_selectedSegment != null)
            _selectedSegment.PropertyChanged -= OnSelectedSegmentParamChanged;

        if (SetProperty(ref _selectedSegment, value))
        {
            if (_selectedSegment != null)
                _selectedSegment.PropertyChanged += OnSelectedSegmentParamChanged;

            RaisePropertyChanged(nameof(HasSelectedSegment));
            RaisePropertyChanged(nameof(ShowContinuousInterpolationParams));
            SelectedSegmentPoints = value?.Points;
            SegmentSplitCount = value?.SamplePointCount > 0 ? value.SamplePointCount : value?.Points?.Count ?? 1;
            SyncSelectedEntityFromSegment(value);
            ApplySegmentSplitCommand.RaiseCanExecuteChanged();
            ExtractCADZValuesCommand.RaiseCanExecuteChanged();

            _eventAggregator?.GetEvent<SelectedSegmentChangedEvent>().Publish(
                new SelectedSegmentPayload { Segment = _selectedSegment });
        }
    }
}
```

- [ ] **Step 2: 构建验证**

Run: `dotnet build Module\Module.csproj --no-restore -v q`
Expected: Build succeeded, 0 error

---

### Task 3: DispenseDetailViewModel 订阅选中段变更事件

**Files:**
- Modify: `Module/Controls/StepDetails/DispenseDetailViewModel.cs`

- [ ] **Step 1: 在构造函数中订阅事件**

找到构造函数中已有的 `SegmentParamChangedEvent` 订阅位置，在其后添加：

```csharp
_eventAggregator?.GetEvent<SegmentParamChangedEvent>().Subscribe(
    OnSegmentParamChanged, ThreadOption.PublisherThread, false);

_eventAggregator?.GetEvent<SelectedSegmentChangedEvent>().Subscribe(
    OnSelectedSegmentChanged, ThreadOption.PublisherThread, false);
```

- [ ] **Step 2: 添加 OnSelectedSegmentChanged 处理方法**

在 `OnSegmentParamChanged` 方法之后添加新方法：

```csharp
/// <summary>
/// 响应选中段变更——将选中段的参数同步到 DispenseDetail 默认参数
/// </summary>
private void OnSelectedSegmentChanged(SelectedSegmentPayload payload)
{
    if (_step?.DispenseDetail == null) return;

    var seg = payload.Segment;
    if (seg == null) return;

    _step.DispenseDetail.DefaultJumpSpeed = seg.JumpSpeed;
    _step.DispenseDetail.DefaultMoveSpeed = seg.MoveSpeed;
    _step.DispenseDetail.DefaultSafeHeight = seg.SafeHeight;
    _step.DispenseDetail.DefaultApproachHeight = seg.ApproachHeight;
    _step.DispenseDetail.DefaultCornerDecel = seg.CornerDecel;
    _step.DispenseDetail.DefaultDispenseAmount = seg.DispenseAmount;
    _step.DispenseDetail.DefaultPreDelay = seg.PreDelay;
    _step.DispenseDetail.DefaultPostDelay = seg.PostDelay;
    _step.DispenseDetail.DefaultDispensingPressure = seg.DispensingPressure;
    _step.DispenseDetail.DefaultSuckBackTime = seg.SuckBackTime;
    _step.DispenseDetail.DefaultGlueTriggerOffsetMm = seg.GlueTriggerOffsetMm;
    _step.DispenseDetail.DefaultTeachHeight = seg.TeachHeight;
    _step.DispenseDetail.DefaultHeightCompensation = seg.HeightCompensation;

    RaisePropertyChanged(nameof(DefaultJumpSpeed));
    RaisePropertyChanged(nameof(DefaultMoveSpeed));
    RaisePropertyChanged(nameof(DefaultSafeHeight));
    RaisePropertyChanged(nameof(DefaultApproachHeight));
    RaisePropertyChanged(nameof(DefaultCornerDecel));
    RaisePropertyChanged(nameof(DefaultDispenseAmount));
    RaisePropertyChanged(nameof(DefaultPreDelay));
    RaisePropertyChanged(nameof(DefaultPostDelay));
    RaisePropertyChanged(nameof(DefaultDispensingPressure));
    RaisePropertyChanged(nameof(DefaultSuckBackTime));
    RaisePropertyChanged(nameof(DefaultGlueTriggerOffsetMm));
    RaisePropertyChanged(nameof(DefaultTeachHeight));
    RaisePropertyChanged(nameof(DefaultHeightCompensation));
}
```

- [ ] **Step 3: 修改默认参数 setter——DispenseDetailView 修改参数时同步到选中段**

当前 `DefaultMoveSpeed` 等属性的 setter 只是写入 `_step.DispenseDetail`，不会同步到选中段。需要添加事件发布。

但这里有一个关键问题：如果在 `OnSelectedSegmentChanged` 中设置默认参数，会触发 setter → 发布 `SegmentParamChangedEvent` → 又写回段 → 形成循环。

解决方案：添加 `_syncingFromSelection` 标志，在选中段同步期间抑制反向事件发布。

首先添加字段：
```csharp
private bool _syncingFromSelection;
```

然后修改 `OnSelectedSegmentChanged` 方法，在同步前后设置标志：
```csharp
private void OnSelectedSegmentChanged(SelectedSegmentPayload payload)
{
    if (_step?.DispenseDetail == null) return;

    var seg = payload.Segment;
    if (seg == null) return;

    _syncingFromSelection = true;

    _step.DispenseDetail.DefaultJumpSpeed = seg.JumpSpeed;
    // ... 其他参数赋值
    RaisePropertyChanged(nameof(DefaultJumpSpeed));
    // ... 其他 RaisePropertyChanged

    _syncingFromSelection = false;
}
```

然后修改 `OnSegmentParamChanged` 方法，在开头添加守卫：
```csharp
private void OnSegmentParamChanged(SegmentParamPayload payload)
{
    if (_syncingFromSelection) return;
    if (_step?.DispenseDetail == null || payload.Segment == null) return;
    // ... 原有逻辑
}
```

最后，修改每个默认参数的 setter，使其在用户修改时发布 `SegmentParamChangedEvent` 到选中段。以 `DefaultMoveSpeed` 为例：

当前代码：
```csharp
public double DefaultMoveSpeed
{
    get => _step?.DispenseDetail?.DefaultMoveSpeed ?? 10.0;
    set { if (_step?.DispenseDetail != null) _step.DispenseDetail.DefaultMoveSpeed = value; }
}
```

修改为：
```csharp
public double DefaultMoveSpeed
{
    get => _step?.DispenseDetail?.DefaultMoveSpeed ?? 10.0;
    set
    {
        if (_step?.DispenseDetail == null) return;
        if (_step.DispenseDetail.DefaultMoveSpeed == value) return;
        _step.DispenseDetail.DefaultMoveSpeed = value;
        PublishParamToSelectedSegment(nameof(DispenseSegment.MoveSpeed), value);
    }
}
```

添加辅助方法：
```csharp
/// <summary>
/// 将用户在 DispenseDetailView 修改的参数同步到当前选中段
/// </summary>
private void PublishParamToSelectedSegment(string propertyName, double value)
{
    if (_syncingFromSelection) return;
    var store = _dispenseSegmentStore;
    if (store?.CurrentSegments == null) return;

    var selected = store.CurrentSegments.FirstOrDefault(s =>
        s == _eventAggregator?.GetEvent<SelectedSegmentChangedEvent>() /* 不行，需要另一种方式获取选中段 */);
}
```

等等——这里有个设计问题。DispenseDetailViewModel 没有直接持有当前选中段的引用。需要通过 `IDispenseSegmentStore` 传递。

更好的方案：在 `IDispenseSegmentStore` 中添加 `CurrentSelectedSegment` 属性，由 CadPointEditorViewModel 在 SelectedSegment 变化时设置。

- [ ] **Step 4: 在 IDispenseSegmentStore 添加 CurrentSelectedSegment**

在 `Core/Abstraction/IDispenseSegmentStore.cs` 中添加：
```csharp
/// <summary>当前选中的段（来自 CAD 编辑器选中行）</summary>
DispenseSegment CurrentSelectedSegment { get; set; }
```

在 `Core/Services/DispenseSegmentStore.cs` 中实现：
```csharp
public DispenseSegment CurrentSelectedSegment { get; set; }
```

- [ ] **Step 5: CadPointEditorViewModel 在 SelectedSegment setter 中设置 CurrentSelectedSegment**

在 SelectedSegment setter 的事件发布之前添加：
```csharp
_dispenseSegmentStore.CurrentSelectedSegment = _selectedSegment;
```

- [ ] **Step 6: 修改 PublishParamToSelectedSegment 辅助方法**

```csharp
/// <summary>
/// 将用户在 DispenseDetailView 修改的参数同步到当前选中段
/// </summary>
private void PublishParamToSelectedSegment(string propertyName, double value)
{
    if (_syncingFromSelection) return;
    var seg = _dispenseSegmentStore?.CurrentSelectedSegment;
    if (seg == null) return;

    switch (propertyName)
    {
        case nameof(DispenseSegment.JumpSpeed): seg.JumpSpeed = value; break;
        case nameof(DispenseSegment.MoveSpeed): seg.MoveSpeed = value; break;
        case nameof(DispenseSegment.SafeHeight): seg.SafeHeight = value; break;
        case nameof(DispenseSegment.ApproachHeight): seg.ApproachHeight = value; break;
        case nameof(DispenseSegment.CornerDecel): seg.CornerDecel = value; break;
        case nameof(DispenseSegment.DispenseAmount): seg.DispenseAmount = value; break;
        case nameof(DispenseSegment.PreDelay): seg.PreDelay = value; break;
        case nameof(DispenseSegment.PostDelay): seg.PostDelay = value; break;
        case nameof(DispenseSegment.DispensingPressure): seg.DispensingPressure = value; break;
        case nameof(DispenseSegment.SuckBackTime): seg.SuckBackTime = value; break;
        case nameof(DispenseSegment.GlueTriggerOffsetMm): seg.GlueTriggerOffsetMm = value; break;
        case nameof(DispenseSegment.TeachHeight): seg.TeachHeight = value; break;
        case nameof(DispenseSegment.HeightCompensation): seg.HeightCompensation = value; break;
    }
}
```

- [ ] **Step 7: 修改所有 13 个默认参数 setter**

每个默认参数 setter 都添加 `PublishParamToSelectedSegment` 调用。完整列表：

```csharp
public double DefaultJumpSpeed
{
    get => _step?.DispenseDetail?.DefaultJumpSpeed ?? 20.0;
    set
    {
        if (_step?.DispenseDetail == null) return;
        if (_step.DispenseDetail.DefaultJumpSpeed == value) return;
        _step.DispenseDetail.DefaultJumpSpeed = value;
        PublishParamToSelectedSegment(nameof(DispenseSegment.JumpSpeed), value);
    }
}

public double DefaultMoveSpeed
{
    get => _step?.DispenseDetail?.DefaultMoveSpeed ?? 10.0;
    set
    {
        if (_step?.DispenseDetail == null) return;
        if (_step.DispenseDetail.DefaultMoveSpeed == value) return;
        _step.DispenseDetail.DefaultMoveSpeed = value;
        PublishParamToSelectedSegment(nameof(DispenseSegment.MoveSpeed), value);
    }
}

public double DefaultSafeHeight
{
    get => _step?.DispenseDetail?.DefaultSafeHeight ?? 5.0;
    set
    {
        if (_step?.DispenseDetail == null) return;
        if (_step.DispenseDetail.DefaultSafeHeight == value) return;
        _step.DispenseDetail.DefaultSafeHeight = value;
        PublishParamToSelectedSegment(nameof(DispenseSegment.SafeHeight), value);
    }
}

public double DefaultApproachHeight
{
    get => _step?.DispenseDetail?.DefaultApproachHeight ?? 3.0;
    set
    {
        if (_step?.DispenseDetail == null) return;
        if (_step.DispenseDetail.DefaultApproachHeight == value) return;
        _step.DispenseDetail.DefaultApproachHeight = value;
        PublishParamToSelectedSegment(nameof(DispenseSegment.ApproachHeight), value);
    }
}

public double DefaultCornerDecel
{
    get => _step?.DispenseDetail?.DefaultCornerDecel ?? 0.3;
    set
    {
        if (_step?.DispenseDetail == null) return;
        if (_step.DispenseDetail.DefaultCornerDecel == value) return;
        _step.DispenseDetail.DefaultCornerDecel = value;
        PublishParamToSelectedSegment(nameof(DispenseSegment.CornerDecel), value);
    }
}

public double DefaultDispenseAmount
{
    get => _step?.DispenseDetail?.DefaultDispenseAmount ?? 1.0;
    set
    {
        if (_step?.DispenseDetail == null) return;
        if (_step.DispenseDetail.DefaultDispenseAmount == value) return;
        _step.DispenseDetail.DefaultDispenseAmount = value;
        PublishParamToSelectedSegment(nameof(DispenseSegment.DispenseAmount), value);
    }
}

public double DefaultPreDelay
{
    get => _step?.DispenseDetail?.DefaultPreDelay ?? 0.0;
    set
    {
        if (_step?.DispenseDetail == null) return;
        if (_step.DispenseDetail.DefaultPreDelay == value) return;
        _step.DispenseDetail.DefaultPreDelay = value;
        PublishParamToSelectedSegment(nameof(DispenseSegment.PreDelay), value);
    }
}

public double DefaultPostDelay
{
    get => _step?.DispenseDetail?.DefaultPostDelay ?? 50.0;
    set
    {
        if (_step?.DispenseDetail == null) return;
        if (_step.DispenseDetail.DefaultPostDelay == value) return;
        _step.DispenseDetail.DefaultPostDelay = value;
        PublishParamToSelectedSegment(nameof(DispenseSegment.PostDelay), value);
    }
}

public double DefaultDispensingPressure
{
    get => _step?.DispenseDetail?.DefaultDispensingPressure ?? 0.30;
    set
    {
        if (_step?.DispenseDetail == null) return;
        if (_step.DispenseDetail.DefaultDispensingPressure == value) return;
        _step.DispenseDetail.DefaultDispensingPressure = value;
        PublishParamToSelectedSegment(nameof(DispenseSegment.DispensingPressure), value);
    }
}

public double DefaultSuckBackTime
{
    get => _step?.DispenseDetail?.DefaultSuckBackTime ?? 100.0;
    set
    {
        if (_step?.DispenseDetail == null) return;
        if (_step.DispenseDetail.DefaultSuckBackTime == value) return;
        _step.DispenseDetail.DefaultSuckBackTime = value;
        PublishParamToSelectedSegment(nameof(DispenseSegment.SuckBackTime), value);
    }
}

public double DefaultGlueTriggerOffsetMm
{
    get => _step?.DispenseDetail?.DefaultGlueTriggerOffsetMm ?? 0.5;
    set
    {
        if (_step?.DispenseDetail == null) return;
        if (_step.DispenseDetail.DefaultGlueTriggerOffsetMm == value) return;
        _step.DispenseDetail.DefaultGlueTriggerOffsetMm = value;
        PublishParamToSelectedSegment(nameof(DispenseSegment.GlueTriggerOffsetMm), value);
    }
}

public double DefaultTeachHeight
{
    get => _step?.DispenseDetail?.DefaultTeachHeight ?? 0.0;
    set
    {
        if (_step?.DispenseDetail == null) return;
        if (_step.DispenseDetail.DefaultTeachHeight == value) return;
        _step.DispenseDetail.DefaultTeachHeight = value;
        PublishParamToSelectedSegment(nameof(DispenseSegment.TeachHeight), value);
    }
}

public double DefaultHeightCompensation
{
    get => _step?.DispenseDetail?.DefaultHeightCompensation ?? 0.0;
    set
    {
        if (_step?.DispenseDetail == null) return;
        if (_step.DispenseDetail.DefaultHeightCompensation == value) return;
        _step.DispenseDetail.DefaultHeightCompensation = value;
        PublishParamToSelectedSegment(nameof(DispenseSegment.HeightCompensation), value);
    }
}
```

- [ ] **Step 8: 构建验证**

Run: `dotnet build Module\Module.csproj --no-restore -v q`
Expected: Build succeeded, 0 error

---

### Task 4: 全项目构建验证

**Files:** 无变更

- [ ] **Step 1: 全项目构建**

Run: `dotnet build GZQL_MACHINE.sln --no-restore -v q`
Expected: 0 Error

- [ ] **Step 2: 检查无残留引用问题**

Run: `rg "DefaultParamsChangedEvent" --type cs`
Expected: 0 matches（确认上一轮修复的残留已清理）

---

## 同步行为总结（修复后）

| 场景 | 行为 |
|------|------|
| Step3 选中段 A | DispenseDetailView 参数更新为段 A 的值 |
| Step3 选中段 B | DispenseDetailView 参数更新为段 B 的值 |
| Step3 取消选中 | DispenseDetailView 参数保持上次选中段的值 |
| DispenseDetailView 修改参数 | 同步到当前选中段（通过 CurrentSelectedSegment） |
| Step3 修改段参数 | 反向同步到 DispenseDetailView 默认参数 |
| 每段参数独立性 | ✅ 每段参数独立，互不影响 |
| 防循环机制 | `_syncingFromSelection` 标志防止选中段同步时触发反向事件 |
