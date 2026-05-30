# 点胶工艺参数同步修复——恢复每段独立参数 + 保存时同步

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复上一轮双向同步实现导致的"所有段参数变成相同值"bug，恢复每段独立参数，仅在用户显式保存时同步到 DispenseDetail 默认参数。

**Architecture:** 移除自动正向同步（DefaultParamsChangedEvent → 覆盖所有段），改为仅保留反向同步（段参数变更 → 更新 DispenseDetail 默认值作为"最近编辑参考"）。正向同步仅在新建段时作为初始值使用。

**Tech Stack:** WPF + Prism EventAggregator + MVVM

---

## 问题根因

上一轮实现的 `OnDefaultParamsChanged` 在 CadPointEditorViewModel 中**遍历所有段强制覆盖参数**，导致：

```
用户修改段A.MoveSpeed → SegmentParamChangedEvent → DispenseDetail.DefaultMoveSpeed 更新
→ OnDefaultParamChanged() → DefaultParamsChangedEvent → OnDefaultParamsChanged
→ 遍历所有段：seg.MoveSpeed = payload.MoveSpeed → 段B/C/D全被覆盖！
```

正确行为：每段参数独立，不应被默认参数自动覆盖。

---

## 文件变更清单

| 文件 | 操作 | 职责 |
|------|------|------|
| `Module/Controls/Cad/CadPointEditorViewModel.cs` | 修改 | 移除 OnDefaultParamsChanged 批量覆盖逻辑，改为仅初始化新段时使用默认值 |
| `Core/Models/DispenseDetail.cs` | 修改 | 移除 OnDefaultParamChanged 中的事件发布（不再自动正向同步） |
| `Core/Events/DispenseParamsSyncEvent.cs` | 修改 | 移除 DefaultParamsChangedEvent（不再需要正向同步事件） |
| `Module/Controls/StepDetails/DispenseDetailViewModel.cs` | 修改 | 反向同步保留，但不再需要 SuppressEvents 守卫（因为正向同步已移除） |

---

### Task 1: 移除正向同步事件定义

**Files:**
- Modify: `Core/Events/DispenseParamsSyncEvent.cs`

- [ ] **Step 1: 删除 DefaultParamsChangedEvent 和 DefaultParamsPayload**

将 `DispenseParamsSyncEvent.cs` 中的 `DefaultParamsChangedEvent` 类和 `DefaultParamsPayload` 类整体删除，仅保留 `SegmentParamChangedEvent` 和 `SegmentParamPayload`。

修改后的文件内容应为：

```csharp
using Core.Models;

namespace Core.Events
{
    /// <summary>
    /// 段参数变更事件——Step3EditParamsPanel 修改段参数时发布，
    /// DispenseDetailViewModel 订阅以更新默认参数（作为最近编辑参考）
    /// </summary>
    public class SegmentParamChangedEvent : PubSubEvent<SegmentParamPayload> { }

    /// <summary>
    /// 段参数变更载荷
    /// </summary>
    public class SegmentParamPayload
    {
        public string PropertyName { get; init; }
        public DispenseSegment Segment { get; init; }
    }
}
```

- [ ] **Step 2: 构建验证**

Run: `dotnet build Core\Core.csproj --no-restore -v q`
Expected: 编译错误——引用了 `DefaultParamsChangedEvent` 的地方报错，这是预期的，将在后续 Task 中修复

---

### Task 2: 移除 DispenseDetail 中的正向同步事件发布

**Files:**
- Modify: `Core/Models/DispenseDetail.cs`

- [ ] **Step 1: 移除 OnDefaultParamChanged 方法中的事件发布逻辑**

将 `OnDefaultParamChanged` 方法体清空（保留方法签名，因为属性 setter 仍调用它，后续可移除）：

```csharp
private void OnDefaultParamChanged()
{
    // 正向同步已移除——默认参数变更不再自动覆盖所有段
    // 段参数独立管理，仅反向同步（段→默认值）保留
}
```

- [ ] **Step 2: 移除 SuppressEvents 属性和静态 _eventAggregator 字段**

删除以下代码：

```csharp
// 删除这些行：
private static IEventAggregator _eventAggregator;
public static void SetEventAggregator(IEventAggregator ea) => _eventAggregator = ea;
public bool SuppressEvents { get; set; }
```

同时删除文件顶部的 `using Prism.Events;`（如果不再需要）。

- [ ] **Step 3: 构建验证**

Run: `dotnet build Core\Core.csproj --no-restore -v q`
Expected: 编译错误——`SuppressEvents` 引用处报错，将在后续 Task 修复

---

### Task 3: 移除 CadPointEditorViewModel 中的正向同步订阅和批量覆盖逻辑

**Files:**
- Modify: `Module/Controls/Cad/CadPointEditorViewModel.cs`

- [ ] **Step 1: 移除 OnDefaultParamsChanged 订阅**

在构造函数中，删除以下行：

```csharp
// 删除：
_eventAggregator?.GetEvent<DefaultParamsChangedEvent>().Subscribe(OnDefaultParamsChanged);
```

- [ ] **Step 2: 删除 OnDefaultParamsChanged 方法**

删除整个 `OnDefaultParamsChanged` 方法及其 `#region` 包裹：

```csharp
// 删除整个 region：
#region 正向同步——DispenseDetail 默认参数变更同步到所有段
// ... 整个方法
#endregion
```

- [ ] **Step 3: 修改新段创建逻辑——使用 DispenseDetail 默认参数作为初始值**

在 `CreateSegmentFromEntity` 方法中，创建 `DispenseSegment` 后，从共享的默认参数源获取初始值。由于 `_eventAggregator` 不再用于正向同步，但仍需保留用于反向同步（发布 `SegmentParamChangedEvent`），所以保留构造函数参数。

新段创建时需要从 DispenseDetail 获取默认参数。通过 `IDispenseSegmentStore` 获取当前步骤的 DispenseDetail：

找到 `CreateSegmentFromEntity` 方法中创建 segment 的位置（约 L1716），在 `return segment;` 之前添加默认参数初始化：

```csharp
var segment = new DispenseSegment($"{prefix}_{index:D03}", entity.EntityType, layerName)
{
    SourceEntity = entity,
    OriginalSourceEntity = entity,
    OriginalEntityData = OriginalEntityData.FromEntity(entity)
};

// 离散化逻辑保持不变...

// 从共享存储获取当前 DispenseDetail 的默认参数作为新段初始值
ApplyDefaultParamsToSegment(segment);

return segment;
```

同样在第二个创建点（约 L1928，手动添加段的场景）也添加 `ApplyDefaultParamsToSegment(segment);`。

- [ ] **Step 4: 添加 ApplyDefaultParamsToSegment 辅助方法**

```csharp
/// <summary>
/// 将 DispenseDetail 默认参数应用到新创建的段（仅初始化时使用，不覆盖已有段）
/// </summary>
private void ApplyDefaultParamsToSegment(DispenseSegment segment)
{
    if (segment == null) return;

    var detail = _dispenseSegmentStore?.CurrentDispenseDetail;
    if (detail == null) return;

    segment.JumpSpeed = detail.DefaultJumpSpeed;
    segment.MoveSpeed = detail.DefaultMoveSpeed;
    segment.SafeHeight = detail.DefaultSafeHeight;
    segment.ApproachHeight = detail.DefaultApproachHeight;
    segment.CornerDecel = detail.DefaultCornerDecel;
    segment.DispenseAmount = detail.DefaultDispenseAmount;
    segment.PreDelay = detail.DefaultPreDelay;
    segment.PostDelay = detail.DefaultPostDelay;
    segment.DispensingPressure = detail.DefaultDispensingPressure;
    segment.SuckBackTime = detail.DefaultSuckBackTime;
    segment.GlueTriggerOffsetMm = detail.DefaultGlueTriggerOffsetMm;
    segment.TeachHeight = detail.DefaultTeachHeight;
    segment.HeightCompensation = detail.DefaultHeightCompensation;
}
```

- [ ] **Step 5: 在 IDispenseSegmentStore 接口添加 CurrentDispenseDetail 属性**

在 `Core/Abstraction/IDispenseSegmentStore.cs` 中添加：

```csharp
/// <summary>当前点胶步骤的 DispenseDetail（用于获取默认参数初始化新段）</summary>
DispenseDetail CurrentDispenseDetail { get; set; }
```

在实现类 `Core/Services/DispenseSegmentStore.cs` 中添加：

```csharp
public DispenseDetail CurrentDispenseDetail { get; set; }
```

- [ ] **Step 6: 在 DispenseDetailViewModel 中设置 CurrentDispenseDetail**

在 `DispenseDetailViewModel.cs` 的 Step 属性 setter 中（设置 `_step` 之后），添加：

```csharp
_dispenseSegmentStore.CurrentDispenseDetail = value?.DispenseDetail;
```

- [ ] **Step 7: 构建验证**

Run: `dotnet build Module\Module.csproj --no-restore -v q`
Expected: PASS（0 error）

---

### Task 4: 清理 DispenseDetailViewModel 中的 SuppressEvents 引用

**Files:**
- Modify: `Module/Controls/StepDetails/DispenseDetailViewModel.cs`

- [ ] **Step 1: 移除 OnSegmentParamChanged 中的 SuppressEvents 守卫**

由于正向同步已移除，不再需要防循环守卫。将 `OnSegmentParamChanged` 方法中的 SuppressEvents 相关代码删除：

```csharp
private void OnSegmentParamChanged(SegmentParamPayload payload)
{
    if (_step?.DispenseDetail == null || payload.Segment == null) return;

    // 移除：_syncingFromSegment = true;
    // 移除：_step.DispenseDetail.SuppressEvents = true;

    switch (payload.PropertyName)
    {
        // ... case 分支保持不变
    }

    // 移除：_step.DispenseDetail.SuppressEvents = false;
    // 移除：_syncingFromSegment = false;
}
```

- [ ] **Step 2: 移除 _syncingFromSegment 字段**

删除 `private bool _syncingFromSegment;` 字段声明。

- [ ] **Step 3: 构建验证**

Run: `dotnet build Module\Module.csproj --no-restore -v q`
Expected: PASS（0 error）

---

### Task 5: 全项目构建验证

**Files:** 无变更

- [ ] **Step 1: 全项目构建**

Run: `dotnet build GZQL_MACHINE.sln --no-restore -v q`
Expected: 0 Error

- [ ] **Step 2: 检查无残留 DefaultParamsChangedEvent 引用**

Run: `rg "DefaultParamsChangedEvent" --type cs`
Expected: 0 matches

- [ ] **Step 3: 检查无残留 SuppressEvents 引用**

Run: `rg "SuppressEvents" --type cs`
Expected: 0 matches

---

## 同步行为总结（修复后）

| 场景 | 旧行为（bug） | 新行为（修复后） |
|------|-------------|---------------|
| 修改 DispenseDetail 默认参数 | 自动覆盖所有段参数 → **bug** | 不再自动覆盖，仅影响新创建的段 |
| 修改 Step3EditParamsPanel 段参数 | 反向同步到默认值 → 触发正向同步 → 覆盖所有段 | 反向同步到默认值（作为参考），不触发正向覆盖 |
| 新建段 | 使用 DispenseSegment 构造函数默认值 | 使用 DispenseDetail 当前默认值初始化 |
| 每段参数独立性 | ❌ 所有段被覆盖为相同值 | ✅ 每段参数独立，互不影响 |
