# TaskMonitorView 步骤显示修复计划

## 问题分析

### 问题1: 停止自定义流程时，高亮行未返回第一步

**根因：** `ProcessStepExecutor.ExecuteAsync` 中，当取消/异常发生时，`step.IsCurrent = false`（第71行）不会执行，导致当前步骤的 `IsCurrent` 残留为 `true`。同时，停止后没有将高亮重置到第一步。

**涉及视图：**
- [ProcessSequenceEditorView.xaml](file:///c:/WorkFiles/GZQL_MACHINE/Module/Operators/Editor/ProcessSequenceEditorView.xaml) 第114行：DataGrid 行通过 `IsCurrent` 绑定高亮

### 问题2: 高亮行颜色不够醒目

**当前实现：**
- TaskMonitorView：`StepItemStyle` 只有 `RetryCount` 触发的橙色背景，**没有 `IsCurrent` 触发的高亮**。当前步骤仅靠小 `▶` 图标区分。
- ProcessSequenceEditorView：使用 `LightGoldenrodYellow`（淡黄色），与"警告"含义混淆，不够醒目。

### 问题3: 步骤名只显示 "[1]GOTO" 没有位置名称

**根因：** `ProcessStepExecutor.ExecuteSingleStepAsync` 第88行：
```csharp
string stepLabel = $"[{step.Seq}] {step.Step}";  // 产生 "[1] GOTO"
```
没有利用 `SubMoves` 中的 `PositionName` 信息。

### 问题4: 步骤信息只显示在 AssemblyTask 的监控栏

**根因：** `ProcessStepExecutor` 通过 `_task.ExecuteStepSafeAsync(stepLabel, ...)` 发布步骤状态，始终使用主任务（如 AssemblyTask）的 `TaskId`。当 `GotoStepAction` 在其他工站上执行 `ExecuteMoveAsync` 时，目标工站不会发布 `TaskStatusChangedEvent`，因此其监控栏无步骤显示。

**数据流：**
```
ProcessStepExecutor → _task.ExecuteStepSafeAsync(stepLabel) → RunStep(stepName)
  → PublishTaskStatusChanged(stepName, State) → TaskStatusChangedEvent
    → TaskMonitorViewModel.OnTaskStatusChanged() → TaskDisplayModel.UpdateStatus()
      → 只更新主任务（如 AssemblyTask）的 StepHistory
```

### 问题5: 不要影响自定义流程 TaskMonitorView 显示

**约束：** 主任务（自定义流程启动的工站）的监控栏仍需正常显示步骤，跨工站发布是增量添加，不替换现有行为。

---

## 实施步骤

### Step 1: ProcessStepExecutor — try/finally 重置 IsCurrent + FormatStepLabel

**文件:** [ProcessStepExecutor.cs](file:///c:/WorkFiles/GZQL_MACHINE/StationTasks/Actions/ProcessStepExecutor.cs)

**变更1：** 在 `ExecuteAsync` 中添加 try/finally，确保停止/取消/异常时重置所有 `IsCurrent`，并将第一步设为高亮：

```csharp
public async Task ExecuteAsync(ObservableCollection<ProcessStep> steps, CancellationToken token)
{
    if (steps == null || steps.Count == 0) { ... return; }

    foreach (var s in steps) s.IsCurrent = false;

    try
    {
        int currentIndex = 0;
        while (currentIndex >= 0 && currentIndex < steps.Count)
        {
            token.ThrowIfCancellationRequested();
            var step = steps[currentIndex];
            step.IsCurrent = true;
            int nextIndex = await ExecuteSingleStepAsync(step, steps, currentIndex, token);
            step.IsCurrent = false;
            currentIndex = nextIndex;
        }
    }
    finally
    {
        // 停止/取消/异常时重置所有步骤高亮，回到第一步
        foreach (var s in steps) s.IsCurrent = false;
        if (steps.Count > 0) steps[0].IsCurrent = true;
    }
}
```

**变更2：** 新增 `FormatStepLabel` 方法，替换 `ExecuteSingleStepAsync` 中的内联格式化：

```csharp
private string FormatStepLabel(ProcessStep step)
{
    string label = $"[{step.Seq}] {step.Step}";

    if (step.Step == StepType.GOTO && step.SubMoves?.Count > 0)
    {
        var posNames = step.SubMoves
            .Where(sm => !string.IsNullOrEmpty(sm.PositionName))
            .Select(sm => sm.PositionName)
            .Distinct()
            .Take(3);
        var posText = string.Join(", ", posNames);
        if (!string.IsNullOrEmpty(posText))
            label += $" → {posText}";
    }

    return label;
}
```

在 `ExecuteSingleStepAsync` 中使用：
```csharp
string stepLabel = FormatStepLabel(step);  // 替换原来的 $"[{step.Seq}] {step.Step}"
```

### Step 2: TaskMonitorView — IsCurrent DataTrigger 高亮行背景色

**文件:** [TaskMonitorView.xaml](file:///c:/WorkFiles/GZQL_MACHINE/MotionControl/Views/TaskMonitorView.xaml)

**变更：** 在 `StepItemStyle`（第40-53行）中添加 `IsCurrent` 的 DataTrigger：

```xml
<Style x:Key="StepItemStyle" TargetType="ListBoxItem">
    <Setter Property="Padding" Value="8,4"/>
    <Setter Property="BorderBrush" Value="#E0E0E0"/>
    <Setter Property="BorderThickness" Value="0,0,0,1"/>
    <Style.Triggers>
        <!-- 当前执行步骤高亮 -->
        <DataTrigger Binding="{Binding IsCurrent}" Value="True">
            <Setter Property="Background" Value="#E3F2FD"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
        </DataTrigger>
        <!-- 重试高亮 -->
        <DataTrigger Binding="{Binding RetryCount}" Value="1">
            <Setter Property="Background" Value="#FFF3E0"/>
        </DataTrigger>
        <DataTrigger Binding="{Binding RetryCount}" Value="2">
            <Setter Property="Background" Value="#FFE0B2"/>
        </DataTrigger>
    </Style.Triggers>
</Style>
```

### Step 3: ProcessSequenceEditorView — 高亮行颜色改进

**文件:** [ProcessSequenceEditorView.xaml](file:///c:/WorkFiles/GZQL_MACHINE/Module/Operators/Editor/ProcessSequenceEditorView.xaml)

**变更：** 将第117行的 `LightGoldenrodYellow` 改为更醒目的蓝色系：

```xml
<DataTrigger Binding="{Binding IsCurrent}" Value="True">
    <Setter Property="Background" Value="#E3F2FD"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
</DataTrigger>
```

### Step 4: StationTaskBase — 新增 PublishStepStatus 公开方法

**文件:** [StationTaskBase.cs](file:///c:/WorkFiles/GZQL_MACHINE/MotionControl/Services/StationTaskBase.cs)

**变更：** 新增公开方法，供 GotoStepAction 在跨工站执行时通知目标工站的监控栏：

```csharp
/// <summary>
/// 公开步骤状态发布方法，供 GotoStepAction 在跨工站执行时通知目标工站的监控栏
/// </summary>
public void PublishStepStatus(string stepName)
{
    PublishTaskStatusChanged(stepName, State);
}
```

### Step 5: GotoStepAction — 跨工站执行时通知目标工站监控栏

**文件:** [GotoStepAction.cs](file:///c:/WorkFiles/GZQL_MACHINE/StationTasks/Actions/GotoStepAction.cs)

**变更：** 在 `ExecuteAsync` 的 foreach 循环中，当 `targetTask != task` 时，发布步骤状态到目标工站：

```csharp
foreach (var subMove in step.SubMoves)
{
    token.ThrowIfCancellationRequested();

    StationTaskBase targetTask = ResolveTargetTask(subMove.StationId, task);
    int axisId = ResolveAxisId(subMove, targetTask);

    // ... 偏移量计算（不变） ...

    double speed = subMove.Speed > 0 ? subMove.Speed : 10.0;

    // 跨工站执行时，通知目标工站的监控栏显示当前移动
    if (targetTask != task)
    {
        string axisName = targetTask.GetAxisNameById(axisId);
        string moveLabel = $"{axisName} → {subMove.PositionName}";
        targetTask.PublishStepStatus(moveLabel);
    }

    _logger.Info($"GOTO SubMove [{subMove.SubSeq}]: ...");
    await targetTask.ExecuteMoveAsync(axisId, subMove.PositionName, speed, totalOffset);
}
```

**注意：** `GetAxisNameById` 当前是 `protected` 方法，需要改为 `public` 以便 GotoStepAction 调用。

### Step 6: 编译验证

编译所有项目确保 0 错误。

---

## 文件变更清单

| 文件 | 变更类型 | 说明 |
|------|---------|------|
| `StationTasks/Actions/ProcessStepExecutor.cs` | 修改 | try/finally 重置 IsCurrent 回到第一步；FormatStepLabel 包含位置名称 |
| `MotionControl/Views/TaskMonitorView.xaml` | 修改 | StepItemStyle 添加 IsCurrent DataTrigger 高亮 (#E3F2FD) |
| `Module/Operators/Editor/ProcessSequenceEditorView.xaml` | 修改 | 高亮色从 LightGoldenrodYellow 改为 #E3F2FD |
| `MotionControl/Services/StationTaskBase.cs` | 修改 | 新增 PublishStepStatus 公开方法；GetAxisNameById 改为 public |
| `StationTasks/Actions/GotoStepAction.cs` | 修改 | 跨工站执行时调用 targetTask.PublishStepStatus() |

## 对需求5的保障措施

- 主任务的步骤发布路径（ProcessStepExecutor → ExecuteStepSafeAsync → RunStep → PublishTaskStatusChanged）**完全不变**
- 跨工站发布是**增量添加**：仅在 `targetTask != task` 时额外发布，不影响主任务的 StepHistory
- 移除 GotoStepAction 中的测试延迟 `await Task.Delay(1800)`
