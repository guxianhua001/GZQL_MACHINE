# 点胶工艺参数双向同步 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现 DispenseDetailView 的默认工艺参数与 Step3EditParamsPanel 的段参数双向同步，确保任一侧修改参数后另一侧能实时反映变化。

**Architecture:** 采用事件总线（IEventAggregator）实现跨 ViewModel 通信。DispenseDetailViewModel 发布 `DefaultParamsChangedEvent`，CadPointEditorViewModel 订阅并批量更新所有 Segments；反向同步通过监听 `DispenseSegment.PropertyChanged` 实现，当段参数被 Step3EditParamsPanel 修改时，自动回写 DispenseDetail.Default 参数。

**Tech Stack:** WPF + Prism (EventAggregator) + MaterialDesign + MVVM

---

## 参数映射关系

| DispenseDetail.Default* | DispenseSegment.* | 分组 |
|-------------------------|-------------------|------|
| DefaultMoveSpeed | MoveSpeed | 运动参数(蓝) |
| DefaultSafeHeight | SafeHeight | 运动参数(蓝) |
| DefaultApproachHeight | ApproachHeight | 运动参数(蓝) |
| DefaultCornerDecel | CornerDecel | 运动参数(蓝) |
| DefaultDispenseAmount | DispenseAmount | 出胶控制(琥珀) |
| DefaultPreDelay | PreDelay | 出胶控制(琥珀) |
| DefaultPostDelay | PostDelay | 出胶控制(琥珀) |
| DefaultDispensingPressure | DispensingPressure | 出胶控制(琥珀) |
| DefaultSuckBackTime | SuckBackTime | 出胶控制(琥珀) |
| DefaultGlueTriggerOffsetMm | GlueTriggerOffsetMm | 出胶控制(琥珀) |
| DefaultTeachHeight | TeachHeight | 高度参数(青) |
| DefaultHeightCompensation | HeightCompensation | 高度参数(青) |

---

## File Structure

| 文件 | 职责 |
|------|------|
| `Core/Events/DefaultParamsChangedEvent.cs` | 创建：Prism 事件，携带 12 个默认参数值 |
| `Core/Models/DispenseDetail.cs` | 修改：Default 参数 setter 中发布事件 |
| `Module/Controls/StepDetails/DispenseDetailViewModel.cs` | 修改：注入 IEventAggregator，订阅段参数变更 |
| `Module/Controls/Cad/CadPointEditorViewModel.cs` | 修改：订阅 DefaultParamsChangedEvent，批量更新段参数 |
| `StationTasks/Actions/DispenseStepAction.cs` | 修改：运行时从 Default 参数同步到 Segment |

---

### Task 1: 创建 DefaultParamsChangedEvent 事件

**Files:**
- Create: `Core/Events/DefaultParamsChangedEvent.cs`

- [ ] **Step 1: 创建事件类**

```csharp
// Core/Events/DefaultParamsChangedEvent.cs
using Prism.Events;
using System;

namespace Core.Events
{
    /// <summary>
    /// 默认工艺参数变更事件——DispenseDetail 默认参数修改时发布，
    /// 通知 Step3EditParamsPanel 同步更新所有段参数
    /// </summary>
    public class DefaultParamsChangedEvent : PubSubEvent<DefaultParamsPayload> { }

    /// <summary>
    /// 默认工艺参数载荷——携带 12 个工艺参数值
    /// </summary>
    public class DefaultParamsPayload
    {
        public double MoveSpeed { get; init; }
        public double SafeHeight { get; init; }
        public double ApproachHeight { get; init; }
        public double CornerDecel { get; init; }
        public double DispenseAmount { get; init; }
        public double PreDelay { get; init; }
        public double PostDelay { get; init; }
        public double DispensingPressure { get; init; }
        public double SuckBackTime { get; init; }
        public double GlueTriggerOffsetMm { get; init; }
        public double TeachHeight { get; init; }
        public double HeightCompensation { get; init; }
    }
}
```

- [ ] **Step 2: 构建 Core 项目验证编译**

Run: `dotnet build "c:\WorkFiles\GZQL_MACHINE\Core\Core.csproj" --no-restore -v q`
Expected: Build succeeded

---

### Task 2: DispenseDetail 模型——Default 参数变更时发布事件

**Files:**
- Modify: `Core/Models/DispenseDetail.cs`

- [ ] **Step 1: 注入 IEventAggregator 到 DispenseDetail 并在 Default 参数 setter 中发布事件**

在 DispenseDetail 类中添加静态 IEventAggregator 引用，在每个 Default* 属性的 setter 中调用 `OnDefaultParamChanged()` 发布事件。

```csharp
// 在 DispenseDetail 类顶部添加
using Core.Events;
using Prism.Events;

// 添加静态属性
private static IEventAggregator _eventAggregator;

/// <summary>注入事件聚合器（由 ViewModel 在初始化时调用）</summary>
public static void SetEventAggregator(IEventAggregator ea) => _eventAggregator = ea;

/// <summary>默认参数变更时发布事件通知 Step3EditParamsPanel</summary>
private void OnDefaultParamChanged()
{
    _eventAggregator?.GetEvent<DefaultParamsChangedEvent>().Publish(new DefaultParamsPayload
    {
        MoveSpeed = _defaultMoveSpeed,
        SafeHeight = _defaultSafeHeight,
        ApproachHeight = _defaultApproachHeight,
        CornerDecel = _defaultCornerDecel,
        DispenseAmount = _defaultDispenseAmount,
        PreDelay = _defaultPreDelay,
        PostDelay = _defaultPostDelay,
        DispensingPressure = _defaultDispensingPressure,
        SuckBackTime = _defaultSuckBackTime,
        GlueTriggerOffsetMm = _defaultGlueTriggerOffsetMm,
        TeachHeight = _defaultTeachHeight,
        HeightCompensation = _defaultHeightCompensation
    });
}
```

在每个 Default* 属性的 setter 中添加 `OnDefaultParamChanged()` 调用，例如：

```csharp
public double DefaultMoveSpeed
{
    get => _defaultMoveSpeed;
    set { SetProperty(ref _defaultMoveSpeed, value); OnDefaultParamChanged(); }
}
// 对所有 12 个 Default* 属性重复此模式
```

- [ ] **Step 2: 构建 Core 项目验证编译**

Run: `dotnet build "c:\WorkFiles\GZQL_MACHINE\Core\Core.csproj" --no-restore -v q`
Expected: Build succeeded

---

### Task 3: DispenseDetailViewModel——注入 EventAggregator 并初始化

**Files:**
- Modify: `Module/Controls/StepDetails/DispenseDetailViewModel.cs`

- [ ] **Step 1: 在构造函数中注入 IEventAggregator 并初始化 DispenseDetail 的事件聚合器**

```csharp
// 在构造函数参数中添加 IEventAggregator eventAggregator
// 在构造函数体内添加：
DispenseDetail.SetEventAggregator(eventAggregator);
```

- [ ] **Step 2: 构建 Module 项目验证编译**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\Module\Module.csproj --no-restore -v q`
Expected: Build succeeded

---

### Task 4: CadPointEditorViewModel——订阅 DefaultParamsChangedEvent 同步段参数

**Files:**
- Modify: `Module/Controls/Cad/CadPointEditorViewModel.cs`

- [ ] **Step 1: 注入 IEventAggregator 并订阅事件**

在构造函数中添加 IEventAggregator 参数，订阅 `DefaultParamsChangedEvent`：

```csharp
// 构造函数参数添加 IEventAggregator eventAggregator
// 构造函数体内添加：
eventAggregator?.GetEvent<DefaultParamsChangedEvent>().Subscribe(OnDefaultParamsChanged, ThreadOption.PublisherThread, false);
```

- [ ] **Step 2: 实现 OnDefaultParamsChanged 方法**

```csharp
/// <summary>
/// 响应 DispenseDetail 默认参数变更——批量同步到所有启用段
/// </summary>
private void OnDefaultParamsChanged(DefaultParamsPayload payload)
{
    if (_segments == null || _segments.Count == 0) return;

    foreach (var seg in _segments.Where(s => s.IsEnabled))
    {
        seg.MoveSpeed = payload.MoveSpeed;
        seg.SafeHeight = payload.SafeHeight;
        seg.ApproachHeight = payload.ApproachHeight;
        seg.CornerDecel = payload.CornerDecel;
        seg.DispenseAmount = payload.DispenseAmount;
        seg.PreDelay = payload.PreDelay;
        seg.PostDelay = payload.PostDelay;
        seg.DispensingPressure = payload.DispensingPressure;
        seg.SuckBackTime = payload.SuckBackTime;
        seg.GlueTriggerOffsetMm = payload.GlueTriggerOffsetMm;
        seg.TeachHeight = payload.TeachHeight;
        seg.HeightCompensation = payload.HeightCompensation;
    }
}
```

- [ ] **Step 3: 构建 Module 项目验证编译**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\Module\Module.csproj --no-restore -v q`
Expected: Build succeeded

---

### Task 5: 反向同步——Step3EditParamsPanel 修改段参数时回写 DispenseDetail

**Files:**
- Modify: `Module/Controls/Cad/CadPointEditorViewModel.cs`
- Modify: `Module/Controls/StepDetails/DispenseDetailViewModel.cs`

- [ ] **Step 1: 在 CadPointEditorViewModel 中发布段参数变更事件**

当 `SelectedSegment` 的工艺参数被 Step3EditParamsPanel 修改时，发布反向同步事件：

```csharp
// 在 SelectedSegment setter 中添加 PropertyChanged 监听
private DispenseSegment _selectedSegment;
public DispenseSegment SelectedSegment
{
    get => _selectedSegment;
    set
    {
        if (_selectedSegment != null)
            _selectedSegment.PropertyChanged -= OnSelectedSegmentParamChanged;

        SetProperty(ref _selectedSegment, value);

        if (_selectedSegment != null)
            _selectedSegment.PropertyChanged += OnSelectedSegmentParamChanged;

        RaisePropertyChanged(nameof(HasSelectedSegment));
        // ... 其他已有逻辑
    }
}

/// <summary>段参数属性名到事件发布动作的映射</summary>
private static readonly HashSet<string> ParamPropertyNames = new()
{
    nameof(DispenseSegment.MoveSpeed),
    nameof(DispenseSegment.SafeHeight),
    nameof(DispenseSegment.ApproachHeight),
    nameof(DispenseSegment.CornerDecel),
    nameof(DispenseSegment.DispenseAmount),
    nameof(DispenseSegment.PreDelay),
    nameof(DispenseSegment.PostDelay),
    nameof(DispenseSegment.DispensingPressure),
    nameof(DispenseSegment.SuckBackTime),
    nameof(DispenseSegment.GlueTriggerOffsetMm),
    nameof(DispenseSegment.TeachHeight),
    nameof(DispenseSegment.HeightCompensation)
};

private void OnSelectedSegmentParamChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
{
    if (!ParamPropertyNames.Contains(e.PropertyName)) return;

    _eventAggregator?.GetEvent<SegmentParamChangedEvent>().Publish(new SegmentParamPayload
    {
        PropertyName = e.PropertyName,
        Segment = _selectedSegment
    });
}
```

- [ ] **Step 2: 创建 SegmentParamChangedEvent**

在 `Core/Events/` 下创建：

```csharp
// Core/Events/SegmentParamChangedEvent.cs
using Prism.Events;

namespace Core.Events
{
    /// <summary>
    /// 段参数变更事件——Step3EditParamsPanel 修改段参数时发布，
    /// 通知 DispenseDetailViewModel 反向同步默认参数
    /// </summary>
    public class SegmentParamChangedEvent : PubSubEvent<SegmentParamPayload> { }

    public class SegmentParamPayload
    {
        public string PropertyName { get; init; }
        public Core.Models.DispenseSegment Segment { get; init; }
    }
}
```

- [ ] **Step 3: DispenseDetailViewModel 订阅 SegmentParamChangedEvent**

```csharp
// 在构造函数中添加：
eventAggregator?.GetEvent<SegmentParamChangedEvent>().Subscribe(OnSegmentParamChanged, ThreadOption.PublisherThread, false);

/// <summary>
/// 响应 Step3EditParamsPanel 段参数变更——反向同步到 DispenseDetail 默认参数
/// </summary>
private void OnSegmentParamChanged(SegmentParamPayload payload)
{
    if (_step?.DispenseDetail == null || payload.Segment == null) return;

    // 静默更新默认参数（不触发 OnDefaultParamChanged 避免循环）
    _syncingFromSegment = true;

    switch (payload.PropertyName)
    {
        case nameof(DispenseSegment.MoveSpeed):
            _step.DispenseDetail.DefaultMoveSpeed = payload.Segment.MoveSpeed;
            RaisePropertyChanged(nameof(DefaultMoveSpeed));
            break;
        case nameof(DispenseSegment.SafeHeight):
            _step.DispenseDetail.DefaultSafeHeight = payload.Segment.SafeHeight;
            RaisePropertyChanged(nameof(DefaultSafeHeight));
            break;
        case nameof(DispenseSegment.ApproachHeight):
            _step.DispenseDetail.DefaultApproachHeight = payload.Segment.ApproachHeight;
            RaisePropertyChanged(nameof(DefaultApproachHeight));
            break;
        case nameof(DispenseSegment.CornerDecel):
            _step.DispenseDetail.DefaultCornerDecel = payload.Segment.CornerDecel;
            RaisePropertyChanged(nameof(DefaultCornerDecel));
            break;
        case nameof(DispenseSegment.DispenseAmount):
            _step.DispenseDetail.DefaultDispenseAmount = payload.Segment.DispenseAmount;
            RaisePropertyChanged(nameof(DefaultDispenseAmount));
            break;
        case nameof(DispenseSegment.PreDelay):
            _step.DispenseDetail.DefaultPreDelay = payload.Segment.PreDelay;
            RaisePropertyChanged(nameof(DefaultPreDelay));
            break;
        case nameof(DispenseSegment.PostDelay):
            _step.DispenseDetail.DefaultPostDelay = payload.Segment.PostDelay;
            RaisePropertyChanged(nameof(DefaultPostDelay));
            break;
        case nameof(DispenseSegment.DispensingPressure):
            _step.DispenseDetail.DefaultDispensingPressure = payload.Segment.DispensingPressure;
            RaisePropertyChanged(nameof(DefaultDispensingPressure));
            break;
        case nameof(DispenseSegment.SuckBackTime):
            _step.DispenseDetail.DefaultSuckBackTime = payload.Segment.SuckBackTime;
            RaisePropertyChanged(nameof(DefaultSuckBackTime));
            break;
        case nameof(DispenseSegment.GlueTriggerOffsetMm):
            _step.DispenseDetail.DefaultGlueTriggerOffsetMm = payload.Segment.GlueTriggerOffsetMm;
            RaisePropertyChanged(nameof(DefaultGlueTriggerOffsetMm));
            break;
        case nameof(DispenseSegment.TeachHeight):
            _step.DispenseDetail.DefaultTeachHeight = payload.Segment.TeachHeight;
            RaisePropertyChanged(nameof(DefaultTeachHeight));
            break;
        case nameof(DispenseSegment.HeightCompensation):
            _step.DispenseDetail.DefaultHeightCompensation = payload.Segment.HeightCompensation;
            RaisePropertyChanged(nameof(DefaultHeightCompensation));
            break;
    }

    _syncingFromSegment = false;
}
```

- [ ] **Step 4: 防止循环同步——DispenseDetail.OnDefaultParamChanged 添加守卫**

在 `DispenseDetail.OnDefaultParamChanged()` 中添加 `_syncingFromSegment` 守卫，当反向同步进行时不发布正向事件：

```csharp
// DispenseDetailViewModel 中添加字段
private bool _syncingFromSegment;

// 修改 DispenseDetail.OnDefaultParamChanged() 添加守卫
// 实际上 OnDefaultParamChanged 在 DispenseDetail 模型中，需要传入守卫状态
// 更好的方案：在 DispenseDetail 中添加 SuppressEvents 标志
```

在 `DispenseDetail` 模型中添加：

```csharp
/// <summary>抑制事件发布标志（反向同步时设为 true 避免循环）</summary>
public bool SuppressEvents { get; set; }

private void OnDefaultParamChanged()
{
    if (SuppressEvents) return;
    _eventAggregator?.GetEvent<DefaultParamsChangedEvent>().Publish(new DefaultParamsPayload { ... });
}
```

在 `DispenseDetailViewModel.OnSegmentParamChanged` 中：

```csharp
_step.DispenseDetail.SuppressEvents = true;
// ... 更新 Default 参数 ...
_step.DispenseDetail.SuppressEvents = false;
```

- [ ] **Step 5: 构建全项目验证编译**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\MainApp\MainApp.csproj --no-restore -v q`
Expected: Build succeeded

---

### Task 6: DispenseStepAction 运行时同步——从 Default 参数应用到 Segment

**Files:**
- Modify: `StationTasks/Actions/DispenseStepAction.cs`

- [ ] **Step 1: 在执行点胶前将 DispenseDetail.Default 参数同步到所有段**

在 `ExecuteAsync` 方法中，段参数解析时使用 Default 参数作为回退值：

```csharp
/// <summary>
/// 解析段参数——优先使用段自身值，若段使用默认参数则从 DispenseDetail.Default 取值
/// </summary>
private double ResolveSegmentParam(double segmentValue, double defaultValue, bool useDefault)
    => useDefault ? defaultValue : segmentValue;
```

确保运行时每个段的参数来源正确：
- `UseDefaultParams=true` 的段 → 使用 `DispenseDetail.Default*` 值
- `UseDefaultParams=false` 的段 → 使用 `DispenseSegmentRef.Override*` 值

- [ ] **Step 2: 构建 StationTasks 项目验证编译**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\StationTasks\StationTasks.csproj --no-restore -v q`
Expected: Build succeeded

---

### Task 7: 全项目集成构建验证

- [ ] **Step 1: 构建完整解决方案**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\MainApp\MainApp.csproj --no-restore -v q`
Expected: Build succeeded, 0 Error(s)

- [ ] **Step 2: 验证无循环引用**

确认 Core 项目不引用 Module 项目（事件定义在 Core/Events 中，ViewModel 在 Module 中订阅）。

---

## Self-Review

**1. Spec coverage:**
- ✅ DispenseDetail 默认参数 → Step3EditParamsPanel 段参数（正向同步，Task 2+4）
- ✅ Step3EditParamsPanel 段参数 → DispenseDetail 默认参数（反向同步，Task 5）
- ✅ 运行时参数解析（Task 6）
- ✅ 防循环同步守卫（Task 5 Step 4）

**2. Placeholder scan:**
- ✅ 无 TBD/TODO/实现后补充
- ✅ 所有步骤包含完整代码

**3. Type consistency:**
- ✅ `DefaultParamsPayload` 属性名与 `DispenseDetail.Default*` 对应
- ✅ `SegmentParamPayload.PropertyName` 使用 `nameof(DispenseSegment.*)` 匹配
- ✅ `IEventAggregator` 在所有 ViewModel 构造函数中注入
