# 步骤序列执行架构重构 Spec

## Why
当前步骤序列执行逻辑嵌入在 StationTaskBase.ExecuteCycleAsync 中，通过 `_pendingSteps` 机制与硬编码流程混合，导致职责不清、耦合度高。ProcessSequenceService 仅通过事件广播与 StationTaskBase 通信，无法精确控制单个工站的任务执行，且所有工站同时接收相同步骤序列（StationId=null），存在并发执行风险。需要将步骤序列执行从 StationTaskBase 中解耦，由 ProcessSequenceService 独立管理执行生命周期。

## What Changes
- **恢复 StationTaskBase.ExecuteCycleAsync**：移除 `_pendingSteps` 分支逻辑，仅保留硬编码工艺流程
- **移除 ExecuteHardcodedCycleAsync**：将硬编码流程合并回 ExecuteCycleAsync
- **移除 StationTaskBase 中的事件订阅**：删除 ProcessStepSequenceEvent 和 ProcessStepSequenceControlEvent 的订阅和处理
- **新增 StationTaskBase.RunCustomSequenceAsync**：提供公开方法供外部调用者执行自定义序列，复用 RunStep 安全保护机制
- **重构 ProcessSequenceService**：注入 IStationRegistry 获取 StationTaskBase 实例，独立管理步骤序列执行
- **TaskItem 新增 StationId**：关联任务与目标工站
- **实现单任务互斥机制**：同一时刻仅允许执行一个任务

## Impact
- Affected specs: 步骤流程编辑器与执行引擎
- Affected code:
  - `MotionControl/Services/StationTaskBase.cs` — 移除步骤序列逻辑，新增 RunCustomSequenceAsync
  - `MotionControl/Services/TaskBase.cs` — 可能需要调整 _cts 访问级别
  - `StationTasks/Tasks/RecipeStationBase.cs` — 移除 GetProcessStepActions 和 ExecutePendingStepsAsync 重写
  - `StationTasks/Tasks/LoadingTask.cs` — ExecuteHardcodedCycleAsync 合并回 ExecuteCycleAsync
  - `StationTasks/Tasks/DispensingTask.cs` — 同上
  - `StationTasks/Tasks/AssemblyTask.cs` — 同上
  - `Module/Services/ProcessSequenceService.cs` — 重构为独立执行引擎
  - `Module/Services/IProcessSequenceService.cs` — 接口扩展
  - `Module/Models/WorkOrderData.cs` — TaskItem 新增 StationId

## ADDED Requirements

### Requirement: StationTaskBase.RunCustomSequenceAsync 自定义序列执行
系统 SHALL 在 StationTaskBase 中提供 RunCustomSequenceAsync 方法，允许外部调用者以与 RunAsync 相同的安全保护机制执行自定义序列。

#### Scenario: 正常执行自定义序列
- **WHEN** 外部调用者调用 RunCustomSequenceAsync(sequence, token)
- **THEN** StationTaskBase 设置状态为 Running，创建链接 CTS，执行传入的序列
- **AND** 序列中的每个步骤通过 RunStep 包装执行，享受暂停/急停/单步/可恢复异常保护
- **AND** 序列执行完毕后状态变为 Completed

#### Scenario: 自定义序列被取消
- **WHEN** 外部取消令牌被触发或调用 StopAsync
- **THEN** StationTaskBase 捕获 OperationCanceledException，状态变为 Stopped

#### Scenario: 自定义序列发生致命异常
- **WHEN** 序列执行中抛出 StepFailureException 或其他未处理异常
- **THEN** StationTaskBase 执行 EmergencyStopAsync，状态变为 Error，发布 EmergencyStopAllEvent

#### Scenario: 任务已在运行时调用
- **WHEN** StationTaskBase.State == Running 时调用 RunCustomSequenceAsync
- **THEN** 方法抛出 InvalidOperationException，拒绝执行

### Requirement: ProcessSequenceService 独立执行引擎
系统 SHALL 由 ProcessSequenceService 独立管理步骤序列的执行生命周期，通过 IStationRegistry 获取目标 StationTaskBase 实例并调用其 RunCustomSequenceAsync 方法。

#### Scenario: 启动任务执行
- **WHEN** 用户点击 StartTask 按钮
- **THEN** ProcessSequenceService 检查是否已有任务在执行（单任务互斥）
- **AND** 通过 IStationRegistry 找到目标 StationTaskBase 实例
- **AND** 创建 ProcessStepExecutor 和步骤动作列表
- **AND** 调用 StationTaskBase.RunCustomSequenceAsync 执行步骤序列
- **AND** CurrentTask.Status 更新为 Running

#### Scenario: 单任务互斥
- **WHEN** 已有任务正在执行时用户点击 StartTask
- **THEN** 系统拒绝启动新任务，记录警告日志
- **AND** 不影响当前正在执行的任务

#### Scenario: 暂停任务
- **WHEN** 用户点击 PauseTask 按钮
- **THEN** ProcessSequenceService 调用 StationTaskBase.PauseAsync
- **AND** CurrentTask.Status 更新为 Paused
- **AND** 当前步骤执行完成后暂停

#### Scenario: 恢复任务
- **WHEN** 用户点击 ResumeTask 按钮
- **THEN** ProcessSequenceService 调用 StationTaskBase.ResumeAsync
- **AND** CurrentTask.Status 更新为 Running
- **AND** 从暂停点继续执行

#### Scenario: 停止任务
- **WHEN** 用户点击 StopTask 按钮
- **THEN** ProcessSequenceService 取消执行 CTS 并调用 StationTaskBase.StopAsync
- **AND** CurrentTask.Status 更新为 Stopped

#### Scenario: 紧急停止
- **WHEN** 用户触发紧急停止
- **THEN** StationTaskBase.EmergencyStopAsync 被调用（通过现有急停机制）
- **AND** 所有轴立即停止，状态变为 Error

### Requirement: TaskItem 关联目标工站
系统 SHALL 在 TaskItem 中新增 StationId 属性，用于关联任务与目标工站。

#### Scenario: 启动带 StationId 的任务
- **WHEN** TaskItem.StationId 非空且匹配已注册工站
- **THEN** ProcessSequenceService 通过 IStationRegistry.GetStation(stationId) 找到目标工站
- **AND** 在该工站的 StationTaskBase 上执行步骤序列

#### Scenario: StationId 为空时的默认行为
- **WHEN** TaskItem.StationId 为空
- **THEN** ProcessSequenceService 使用第一个可用的 StationTaskBase 实例
- **AND** 记录警告日志提示未指定目标工站

#### Scenario: StationId 匹配不到工站
- **WHEN** TaskItem.StationId 非空但无匹配的已注册工站
- **THEN** 记录错误日志，拒绝启动任务

## MODIFIED Requirements

### Requirement: StationTaskBase.ExecuteCycleAsync 恢复为纯硬编码流程
原 ExecuteCycleAsync 包含 `_pendingSteps` 分支逻辑，优先执行步骤序列再回退到硬编码流程。修改后 ExecuteCycleAsync 仅包含硬编码工艺流程，不再处理步骤序列。

修改后逻辑：
```csharp
protected override async Task ExecuteCycleAsync(CancellationToken token)
{
    // 子类直接重写此方法实现硬编码工艺流程
    // 不再检查 _pendingSteps
}
```

### Requirement: ProcessSequenceService.StartTask 从事件广播改为直接调用
原 StartTask 通过 IEventAggregator 发布 ProcessStepSequenceEvent 广播步骤序列。修改为通过 IStationRegistry 获取目标 StationTaskBase 并直接调用 RunCustomSequenceAsync。

### Requirement: IProcessSequenceService 接口扩展
新增属性：
- `bool IsExecuting { get; }` — 指示是否有任务正在执行

## REMOVED Requirements

### Requirement: StationTaskBase 中的步骤序列事件订阅
**Reason**: 步骤序列执行职责从 StationTaskBase 转移到 ProcessSequenceService，StationTaskBase 不再需要订阅 ProcessStepSequenceEvent 和 ProcessStepSequenceControlEvent
**Migration**: ProcessSequenceService 直接调用 StationTaskBase.RunCustomSequenceAsync，不再通过事件中转

### Requirement: ExecuteHardcodedCycleAsync 抽象方法
**Reason**: 恢复 ExecuteCycleAsync 为直接重写的方法，ExecuteHardcodedCycleAsync 不再需要
**Migration**: 各子类将 ExecuteHardcodedCycleAsync 的实现合并回 ExecuteCycleAsync

### Requirement: _pendingSteps 缓存机制
**Reason**: 步骤序列不再通过事件注入 StationTaskBase，无需缓存
**Migration**: ProcessSequenceService 直接持有步骤数据并传递给 ProcessStepExecutor

### Requirement: GetProcessStepActions 和 ExecutePendingStepsAsync 虚方法
**Reason**: 步骤动作创建和执行逻辑转移到 ProcessSequenceService
**Migration**: ProcessSequenceService 直接创建 IProcessStepAction 列表
