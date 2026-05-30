# GotoStepAction 轴解析修复 & ProcessSequenceService 架构优化

## 问题分析

### 问题1: GotoStepAction 轴ID解析使用了错误的工站任务

**当前行为：**
```
GotoStepAction.ExecuteAsync(step, task, token)
  → ResolveAxisId(subMove, task)  // task 是 ProcessSequenceService 传入的 _activeStationTask
    → task.FindAxisIdByName(subMove.Axis)  // 只在 task 的轴列表中查找
```

**问题根因：** `ProcessSequenceService.StartTask()` 通过 `CurrentTask.StationId` 找到一个 `StationTaskBase` 实例，将所有步骤序列都交给这一个任务执行。但 `SubMove` 对象有独立的 `StationId` 属性，表示该 SubMove 应该由哪个工站执行。例如：

- TaskItem.StationId = "AssemblyStation" → ProcessSequenceService 选择 AssemblyTask 执行
- SubMove.StationId = "LoadingStation", Axis = "Y" → 应该在 LoadingTask 上查找 Y 轴
- 但当前代码用 AssemblyTask.FindAxisIdByName("Y") → AssemblyTask 没有 Y 轴 → 返回 0 → 失败

**关键洞察：** 一个步骤序列可能包含跨工站的 SubMove。例如装配工序可能需要先移动点胶工位的轴，再移动装配工位的轴。当前架构只绑定一个 StationTaskBase，无法处理跨工站操作。

### 问题2: ProcessSequenceService 架构需要支持多任务映射同一工站

**当前架构：**
```
TaskItem (UI层) → StationId → FindStationTask() → 单个 StationTaskBase → RunCustomSequenceAsync
```

**问题：**
- 多个 TaskItem 可能映射到同一个 StationTaskBase（如两个不同的装配任务都映射到 AssemblyTask）
- 当前只支持一个任务执行，但无法区分硬编码任务和自定义编辑器任务
- 缺少对"哪个 StationTaskBase 负责执行哪个 SubMove"的明确路由

---

## 实施步骤

### Step 1: 修改 GotoStepAction — 根据 SubMove.StationId 路由到正确的工站任务

**核心变更：** GotoStepAction 需要注入 `IStationRegistry`，在 `ResolveAxisId` 中根据 `SubMove.StationId` 找到正确的 StationTaskBase，而不是始终使用传入的 task 参数。

**文件:** `StationTasks/Actions/GotoStepAction.cs`

**变更内容：**

1. 新增 `IStationRegistry` 依赖注入：
```csharp
private readonly IStationRegistry _stationRegistry;

public GotoStepAction(IRecipePoolService recipePoolService, ILoggerService logger, IStationRegistry stationRegistry)
{
    _recipePoolService = recipePoolService;
    _logger = logger;
    _stationRegistry = stationRegistry;
}
```

2. 修改 `ResolveAxisId` 方法，根据 `SubMove.StationId` 查找正确的工站任务：
```csharp
/// <summary>
/// 解析轴ID：优先使用 AxisId，否则根据 SubMove.StationId 找到对应工站任务再通过轴名查找
/// </summary>
private int ResolveAxisId(SubMove subMove, StationTaskBase defaultTask)
{
    if (subMove.AxisId > 0)
        return subMove.AxisId;

    if (!string.IsNullOrEmpty(subMove.Axis))
    {
        // 根据 SubMove.StationId 查找目标工站任务
        StationTaskBase targetTask = ResolveTargetTask(subMove.StationId, defaultTask);
        int resolvedId = targetTask.FindAxisIdByName(subMove.Axis);
        if (resolvedId > 0)
            return resolvedId;
    }

    _logger.Warn($"SubMove [{subMove.SubSeq}] 无法解析轴ID，AxisId={subMove.AxisId}, Axis={subMove.Axis}，使用原始值");
    return subMove.AxisId;
}

/// <summary>
/// 根据 StationId 查找目标工站任务，未指定时使用默认任务
/// </summary>
private StationTaskBase ResolveTargetTask(string stationId, StationTaskBase defaultTask)
{
    if (string.IsNullOrEmpty(stationId))
        return defaultTask;

    var station = _stationRegistry.GetAllStations()
        .FirstOrDefault(s => s.StationIdentifier == stationId);
    if (station is StationTaskBase task)
        return task;

    _logger.Warn($"SubMove 指定的工站 '{stationId}' 未找到，使用默认工站 '{defaultTask.TaskName}'");
    return defaultTask;
}
```

3. 修改 `ExecuteAsync` 中的移动执行，根据 SubMove.StationId 路由到正确的工站：
```csharp
foreach (var subMove in step.SubMoves)
{
    token.ThrowIfCancellationRequested();
    int axisId = ResolveAxisId(subMove, task);
    // ... 偏移量计算 ...
    StationTaskBase targetTask = ResolveTargetTask(subMove.StationId, task);
    await targetTask.ExecuteMoveAsync(axisId, subMove.PositionName, speed, totalOffset);
}
```

4. 更新 `ProcessSequenceService.CreateStepActions()` 传入 `IStationRegistry`：
```csharp
new GotoStepAction(_recipePoolService, _logger, _stationRegistry)
```

### Step 2: 修改 ProcessSequenceService — 支持多任务映射同一工站

**当前问题：** `ProcessSequenceService` 使用单个 `_activeStationTask` 执行所有步骤，但步骤序列可能包含跨工站的 SubMove。

**架构变更：**

1. **保留 3 个硬编码任务不变** — LoadingTask、AssemblyTask、DispensingTask 继续作为 DI 单例存在

2. **不创建新的 StationTaskBase 实例** — 自定义编辑器的步骤序列复用现有的 3 个硬编码任务实例。理由：
   - StationTaskBase 绑定了物理轴、运动服务、信号交互等硬件资源
   - 创建新实例会导致轴控制冲突（两个任务实例同时控制同一轴）
   - 正确做法是一个步骤序列可以跨工站使用多个 StationTaskBase 实例

3. **修改执行模型** — 从"一个序列绑定一个工站"改为"一个序列可跨工站执行"：
   - TaskItem.StationId 表示序列的**主工站**（用于 RunCustomSequenceAsync 的状态管理）
   - SubMove.StationId 表示每个子移动的**目标工站**（用于轴解析和移动执行）

4. **单任务互斥保持不变** — `_isExecuting` 标志确保同一时刻只有一个序列在执行

**文件:** `Module/Services/ProcessSequenceService.cs`

**变更内容：**

1. 修改 `StartTask` 方法，支持 StationId 为空时的默认行为：
```csharp
/// <summary> 启动当前任务：通过 IStationRegistry 获取目标工站，调用 RunCustomSequenceAsync 执行步骤序列 </summary>
public void StartTask()
{
    if (CurrentTask == null) return;
    if (_isExecuting)
    {
        _logger.Warn("[ProcessSequence] 已有任务正在执行，拒绝启动新任务");
        return;
    }
    
    // 查找主工站（用于 RunCustomSequenceAsync 的状态管理）
    var stationTask = FindStationTask(CurrentTask.StationId);
    if (stationTask == null) return;
    
    var steps = CurrentTask.Steps;
    if (steps == null || steps.Count == 0)
    {
        _logger.Warn("[ProcessSequence] 当前任务没有步骤，无法启动");
        return;
    }
    
    _executionCts = new CancellationTokenSource();
    _isExecuting = true;
    _activeStationTask = stationTask;
    CurrentTask.Status = TaskItem.TaskStatusEnum.Running;
    _logger.Info($"[ProcessSequence] 启动任务: {CurrentTask.Name}，共 {steps.Count} 个步骤，主工站: {stationTask.TaskName}");

    _ = ExecuteSequenceAsync(stationTask, steps, _executionCts.Token);
}
```

2. 修改 `CreateStepActions` 传入 `_stationRegistry`：
```csharp
private List<IProcessStepAction> CreateStepActions()
{
    return new List<IProcessStepAction>
    {
        new GotoStepAction(_recipePoolService, _logger, _stationRegistry)
    };
}
```

### Step 3: 更新 StationTasksModule DI 注册

**文件:** `StationTasks/StationTasksModule.cs`

**变更：** 更新 GotoStepAction 的 DI 注册，添加 `IStationRegistry` 参数。由于 `GotoStepAction` 现在需要 `IStationRegistry`，但 DI 容器可以自动解析，只需确保构造函数参数正确。

**注意：** 检查当前 DI 注册是否支持自动注入 `IStationRegistry`。如果 `GotoStepAction` 是通过 `new` 手动创建的（在 `ProcessSequenceService.CreateStepActions` 中），则不需要修改 DI 注册，只需修改 `new GotoStepAction(...)` 调用。

### Step 4: 编译验证

编译所有项目确保 0 错误。

---

## 架构说明

### 硬编码任务 vs 自定义编辑器任务

```
┌─────────────────────────────────────────────────────────┐
│                    硬编码任务 (3个)                       │
│  LoadingTask, AssemblyTask, DispensingTask              │
│  - DI 单例，自注册到 IStationRegistry                    │
│  - 绑定物理轴、运动服务、信号交互                         │
│  - 通过 RunAsync + ExecuteCycleAsync 执行硬编码工艺流程   │
│  - 通过 RunCustomSequenceAsync 执行自定义步骤序列         │
└─────────────────────────────────────────────────────────┘
                          ↑ 复用
┌─────────────────────────────────────────────────────────┐
│                自定义编辑器任务 (TaskItem)                │
│  ProcessSequenceService.Tasks 集合                       │
│  - UI 层数据模型，不绑定物理资源                          │
│  - 包含 ProcessStep 列表和 StationId                     │
│  - 通过硬编码任务的 RunCustomSequenceAsync 执行           │
│  - 多个 TaskItem 可映射到同一个 StationTaskBase           │
└─────────────────────────────────────────────────────────┘
```

### SubMove 跨工站路由

```
ProcessStep (GOTO)
  ├── SubMove 1: StationId="LoadingStation", Axis="Y"   → LoadingTask.ExecuteMoveAsync
  ├── SubMove 2: StationId="AssemblyStation", Axis="X"  → AssemblyTask.ExecuteMoveAsync
  └── SubMove 3: StationId=null, Axis="Z"               → defaultTask.ExecuteMoveAsync
```

### 执行流程

```
ProcessSequenceService.StartTask()
  → FindStationTask(CurrentTask.StationId) → 主工站 StationTaskBase
  → ExecuteSequenceAsync(stationTask, steps, token)
    → stationTask.RunCustomSequenceAsync(sequence, token)
      → ProcessStepExecutor.ExecuteAsync(steps, token)
        → GotoStepAction.ExecuteAsync(step, task, token)
          → ResolveTargetTask(subMove.StationId, task) → 目标工站 StationTaskBase
          → targetTask.ExecuteMoveAsync(axisId, positionName, speed, offset)
```

---

## 文件变更清单

| 文件 | 变更类型 | 说明 |
|------|---------|------|
| `StationTasks/Actions/GotoStepAction.cs` | 修改 | 注入 IStationRegistry，新增 ResolveTargetTask，修改 ResolveAxisId 和 ExecuteAsync |
| `Module/Services/ProcessSequenceService.cs` | 修改 | CreateStepActions 传入 _stationRegistry |
