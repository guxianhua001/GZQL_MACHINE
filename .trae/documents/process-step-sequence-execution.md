# 计划：完善步骤序列启动/暂停/停止执行流程

## 问题分析

当前 `ProcessSequenceService.StartTask()` 只做了两件事：
1. 设置 `TaskItem.Status = Running`（纯 UI 数据模型状态）
2. 发布 `ProcessStepSequenceEvent`（步骤列表事件）

但**没有真正调用 `ITask.RunAsync()` 来启动任务**，也没有将 `ProcessSequenceService` 的 Pause/Stop 与 `ITask.PauseAsync()/StopAsync()` 关联。

### 架构决策：使用现有工站任务 + ProcessStepExecutor

**不创建 ProcessStepRunnerTask**，原因：
- 每个工站（Loading/Assembly/Dispensing）已经拥有对应的物理轴、IO、信号交互
- 步骤编辑器编辑的步骤（如 GOTO 移动）需要操作具体工站的轴
- 创建一个没有轴配置的 "Runner" 任务无法执行实际运动

**正确方案**：让现有工站任务（如 LoadingTask）在 `ExecuteCycleAsync` 中检查是否有待执行的步骤序列，如果有则用 `ProcessStepExecutor` 执行，否则执行默认硬编码流程。这通过 `StationTaskBase` 基类统一实现，子类无需修改。

## 实现步骤

### 步骤 1：删除 ProcessStepRunnerTask

- 删除 `StationTasks\Tasks\ProcessStepRunnerTask.cs`
- 从 `StationTasksModule.cs` 的 `RegisterMany` 中移除 `typeof(ProcessStepRunnerTask)`

### 步骤 2：修改 StationTaskBase — 统一处理步骤序列执行

在 `StationTaskBase` 中将 `ExecuteCycleAsync` 从 abstract 改为有默认实现的方法：

```csharp
// 新增 abstract 方法，子类实现硬编码流程
protected abstract Task ExecuteHardcodedCycleAsync(CancellationToken token);

// ExecuteCycleAsync 有默认实现：优先执行步骤序列，否则执行硬编码流程
protected override async Task ExecuteCycleAsync(CancellationToken token)
{
    // 优先检查是否有 UI 编辑器发布的步骤序列
    if (PendingSteps != null)
    {
        var steps = PendingSteps as ObservableCollection<ProcessStep>;
        if (steps != null && steps.Count > 0)
        {
            var actions = GetProcessStepActions() as IEnumerable<IProcessStepAction>;
            if (actions != null)
                await this.ExecuteProcessStepSequenceAsync(steps, actions, token);
            else
                Logger.Warn($"[{TaskName}] 未注册步骤动作，无法执行步骤序列");
        }
        ClearPendingSteps();
        return;
    }
    // 没有步骤序列时，执行子类硬编码流程
    await ExecuteHardcodedCycleAsync(token);
}
```

### 步骤 3：修改 TaskBase — 将 ExecuteCycleAsync 改为 virtual

`TaskBase.ExecuteCycleAsync` 从 `abstract` 改为 `virtual`，让 `StationTaskBase` 可以 override 它。

### 步骤 4：修改所有工站任务子类

将 `ExecuteCycleAsync` 重命名为 `ExecuteHardcodedCycleAsync`：
- `LoadingTask.cs`
- `AssemblyTask.cs`
- `DispensingTask.cs`
- `LoadingPickTask.cs`

### 步骤 5：修改 StationTaskBase — 实现 GetProcessStepActions

在 `StationTaskBase` 中提供默认的 `GetProcessStepActions()` 实现，创建 `GotoStepAction` 等动作实例（需要 `IRecipePoolService` 和 `ILoggerService`，已在 `RecipeStationBase` 中可用）。

将 `GetProcessStepActions()` 从 `virtual object` 改为在 `RecipeStationBase` 中实现：

```csharp
protected override object GetProcessStepActions()
{
    return new List<IProcessStepAction>
    {
        new GotoStepAction(_recipePoolService, _logger)
    };
}
```

### 步骤 6：修改 ProcessSequenceService — 关联 ITask 控制

将 `ProcessSequenceService` 的 Start/Pause/Stop 与 `ITask` 的实际控制方法关联：

```csharp
public void StartTask()
{
    if (CurrentTask == null) return;
    CurrentTask.Status = TaskItem.TaskStatusEnum.Running;
    // 发布步骤序列事件
    _eventAggregator.GetEvent<ProcessStepSequenceEvent>().Publish(new ProcessStepSequencePayload
    {
        StationId = null,  // 广播给所有工站
        Steps = CurrentTask.Steps
    });
}

public void PauseTask()
{
    if (CurrentTask == null || CurrentTask.Status != TaskItem.TaskStatusEnum.Running) return;
    CurrentTask.Status = TaskItem.TaskStatusEnum.Paused;
    // 通过事件通知工站任务暂停
    _eventAggregator.GetEvent<ProcessStepSequenceControlEvent>().Publish(
        new ProcessStepSequenceControlPayload { Action = SequenceControlAction.Pause });
}

public void StopTask()
{
    if (CurrentTask == null) return;
    CurrentTask.Status = TaskItem.TaskStatusEnum.Stopped;
    // 通过事件通知工站任务停止
    _eventAggregator.GetEvent<ProcessStepSequenceControlEvent>().Publish(
        new ProcessStepSequenceControlPayload { Action = SequenceControlAction.Stop });
}
```

### 步骤 7：创建 ProcessStepSequenceControlEvent

在 `Core\Events` 中创建控制事件，用于步骤编辑器控制工站任务的暂停/停止：

```csharp
public enum SequenceControlAction { Pause, Resume, Stop }

public class ProcessStepSequenceControlEvent : PubSubEvent<ProcessStepSequenceControlPayload> { }

public class ProcessStepSequenceControlPayload
{
    public SequenceControlAction Action { get; set; }
    public string StationId { get; set; }  // null = 广播
}
```

### 步骤 8：StationTaskBase 订阅控制事件

在 `StationTaskBase` 构造函数中订阅 `ProcessStepSequenceControlEvent`，收到后调用对应的 `PauseAsync()/ResumeAsync()/StopAsync()`。

### 步骤 9：修改 ProcessSequenceEditorViewModel — 添加 Resume 命令

当前只有 Start/Pause/Stop，需要添加 Resume 功能。

## 修改文件清单

| # | 文件 | 操作 |
|---|------|------|
| 1 | `StationTasks\Tasks\ProcessStepRunnerTask.cs` | 删除 |
| 2 | `StationTasks\StationTasksModule.cs` | 移除 ProcessStepRunnerTask 注册 |
| 3 | `MotionControl\Services\TaskBase.cs` | `ExecuteCycleAsync` 从 abstract 改为 virtual |
| 4 | `MotionControl\Services\StationTaskBase.cs` | override `ExecuteCycleAsync`，添加步骤序列执行逻辑 |
| 5 | `StationTasks\Tasks\LoadingTask.cs` | `ExecuteCycleAsync` → `ExecuteHardcodedCycleAsync` |
| 6 | `StationTasks\Tasks\AssemblyTask.cs` | `ExecuteCycleAsync` → `ExecuteHardcodedCycleAsync` |
| 7 | `StationTasks\Tasks\DispensingTask.cs` | `ExecuteCycleAsync` → `ExecuteHardcodedCycleAsync` |
| 8 | `StationTasks\Tasks\LoadingPickTask.cs` | `ExecuteCycleAsync` → `ExecuteHardcodedCycleAsync` |
| 9 | `StationTasks\Tasks\RecipeStationBase.cs` | 实现 `GetProcessStepActions()` |
| 10 | `Core\Events\ProcessStepSequenceEvent.cs` | 添加 `ProcessStepSequenceControlEvent` |
| 11 | `Module\Services\ProcessSequenceService.cs` | Pause/Stop 发布控制事件 |
| 12 | `Module\Services\IProcessSequenceService.cs` | 添加 ResumeTask 方法 |
| 13 | `Module\Operators\Editor\ProcessSequenceEditorViewModel.cs` | 添加 Resume 命令 |

## 执行流程图

```
步骤编辑器 Start
    │
    ├── ProcessStepSequenceEvent.Publish(steps)
    │       │
    │       └── StationTaskBase.OnProcessStepSequenceRequested()
    │               ├── 缓存 _pendingSteps
    │               └── _sequenceTcs.SetResult() 通知
    │
    └── StationTaskBase.ExecuteCycleAsync() (已有任务循环中)
            ├── 检测 PendingSteps != null
            ├── GetProcessStepActions() → [GotoStepAction, ...]
            ├── ProcessStepExecutor.ExecuteAsync(steps, token)
            │       ├── 逐步骤执行，每步通过 RunStep 包装
            │       ├── 支持暂停(CheckPauseAsync)、急停(CancellationToken)
            │       └── 支持可恢复异常(RecoverableException)
            └── ClearPendingSteps()

步骤编辑器 Pause
    │
    └── ProcessStepSequenceControlEvent.Publish(Pause)
            │
            └── StationTaskBase.OnSequenceControlRequested(Pause)
                    └── this.PauseAsync() → _isPaused = true
                        (RunStep 中的 CheckPauseAsync 会阻塞)

步骤编辑器 Resume
    │
    └── ProcessStepSequenceControlEvent.Publish(Resume)
            │
            └── StationTaskBase.OnSequenceControlRequested(Resume)
                    └── this.ResumeAsync() → _isPaused = false, _pauseTcs.SetResult()

步骤编辑器 Stop
    │
    └── ProcessStepSequenceControlEvent.Publish(Stop)
            │
            └── StationTaskBase.OnSequenceControlRequested(Stop)
                    └── this.StopAsync() → _cts.Cancel()
```
