# PICK 保压时间 (Holding Time) 实现方案

## 背景

当前状态：
- `PickDetail.PickHoldingTime` 字段已存在（默认 500ms），UI 已可配置
- **但 `PickStepAction` 不存在**，PICK 步骤在运行时被跳过
- `WaitStepAction` + `WaitDetail` 已有完整的延时机制（支持 ms/s/min）
- 步骤执行器 `ProcessStepExecutor` 通过 while 循环顺序执行步骤

## 三种实现方案对比

### 方案 A：创建 PickStepAction，内含 Task.Delay（推荐）

**原理：** 在 PICK 步骤的执行器内部，夹紧动作完成后直接调用 `Task.Delay(PickHoldingTime)`

**执行流程：**
```
PickStepAction.ExecuteAsync():
  1. 遍历 PickDetail.PickMoves，逐个执行运动（复用 GotoStepAction 逻辑）
  2. 调用 IGripperService.ClampAsync(PickDetail.ClampPosition)  ← 夹紧
  3. await Task.Delay(PickDetail.PickHoldingTime)               ← ★ 保压延时
  4. 检查真空/夹持确认信号（如果启用）
  5. 完成
```

**优点：**
- ✅ 保压时间是 PICK 步骤的内在属性，逻辑内聚
- ✅ 不需要用户手动插入 WAIT 步骤
- ✅ 无硬编码，时间值从 PickDetail 配置读取
- ✅ 延时受 CancellationToken 控制（急停可打断）
- ✅ 受 RunStep 安全包装保护（暂停/可恢复异常）

**缺点：**
- ⚠️ 需要创建 PickStepAction（但这是必须的，PICK 步骤当前根本没有执行器）

---

### 方案 B：自动插入 WAIT 步骤

**原理：** 在步骤序列中，PICK 步骤之后自动插入一个 WAIT 步骤

**执行流程：**
```
ProcessStepExecutor.ExecuteSingleStepAsync():
  case StepType.PICK:
    await ExecuteWithRunStepAsync(step, token);   // 执行 PICK
    // 自动查找/插入后续 WAIT 步骤
    if (step.PickDetail.PickHoldingTime > 0)
      await Task.Delay(step.PickDetail.PickHoldingTime, token);
```

**优点：**
- ✅ 不需要创建独立的 PickStepAction

**缺点：**
- ❌ 违反单一职责原则，在 Executor 中硬编码 PICK 的特殊逻辑
- ❌ WAIT 步骤不在步骤列表中可见，用户无法感知保压步骤
- ❌ 难以调试（步骤日志中看不到保压延时）
- ❌ 如果用户想手动控制保压时间，无法通过 UI 操作

---

### 方案 C：用户手动在 PICK 后添加 WAIT 步骤

**原理：** 不做任何自动处理，让用户在步骤编辑器中手动添加 WAIT 步骤

**执行流程：**
```
步骤序列：
  1. GOTO Standby
  2. PICK (夹紧)
  3. WAIT 500ms    ← 用户手动添加
  4. GOTO Place
  5. RELEASE
```

**优点：**
- ✅ 最灵活，用户完全控制
- ✅ 不需要创建 PickStepAction
- ✅ WAIT 步骤在列表中可见

**缺点：**
- ❌ 每次创建 PICK 都要手动添加 WAIT，操作繁琐
- ❌ 容易遗漏，导致保压不足
- ❌ PickHoldingTime 字段变成摆设
- ❌ 不符合工业自动化"配置即执行"的设计理念

---

## 推荐方案：A（创建 PickStepAction）

### 实施步骤

#### Step 1: 创建 PickStepAction

**文件：** `StationTasks/Actions/PickStepAction.cs`

```csharp
public class PickStepAction : IProcessStepAction
{
    public StepType SupportedStepType => StepType.PICK;

    public async Task ExecuteAsync(ProcessStep step, StationTaskBase task, CancellationToken token)
    {
        var pickDetail = step.PickDetail;
        if (pickDetail == null) return;

        // 1. 执行取料运动序列（复用 GotoStepAction 的运动逻辑）
        foreach (var subMove in pickDetail.PickMoves)
        {
            await ExecuteSubMoveAsync(subMove, task, token);
        }

        // 2. 执行夹紧动作
        var gripperService = task.Container.Resolve<IGripperService>();
        await gripperService.ClampAsync(pickDetail.ClampPosition, token);

        // 3. ★ 保压延时（从配置读取，无硬编码）
        if (pickDetail.PickHoldingTime > 0)
        {
            _logger.Info($"PICK 保压延时: {pickDetail.PickHoldingTime}ms");
            await Task.Delay(pickDetail.PickHoldingTime, token);
        }

        // 4. 真空检测（如果启用）
        if (pickDetail.IsVacuumOn && pickDetail.VacuumCheckDelay > 0)
        {
            await Task.Delay(pickDetail.VacuumCheckDelay, token);
            // 检查真空信号...
        }
    }
}
```

#### Step 2: 注册到 DI 容器

**文件：** `StationTasks/StationTasksModule.cs`

在 RegisterMany 数组中添加 `typeof(PickStepAction)`

#### Step 3: 在 ProcessStepExecutor 中添加 PICK 分支

**文件：** `StationTasks/Actions/ProcessStepExecutor.cs`

在 `ExecuteSingleStepAsync` 的 switch 中添加：
```csharp
case StepType.PICK:
    await ExecuteWithRunStepAsync(stepLabel, step, token);
    return currentIndex + 1;
```

#### Step 4: 验证

- 创建包含 PICK 步骤的配方
- 运行序列，确认夹紧后有保压延时
- 急停测试：保压期间按急停，确认可打断
- 暂停测试：保压期间暂停，确认恢复后继续

### 文件修改清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `StationTasks/Actions/PickStepAction.cs` | 新建 | PICK 步骤执行器 |
| `StationTasks/StationTasksModule.cs` | 修改 | DI 注册 PickStepAction |
| `StationTasks/Actions/ProcessStepExecutor.cs` | 修改 | switch 添加 PICK 分支 |
