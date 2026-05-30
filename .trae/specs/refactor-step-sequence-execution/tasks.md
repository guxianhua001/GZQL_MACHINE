# Tasks

- [x] Task 1: 重构 StationTaskBase — 移除步骤序列逻辑，恢复 ExecuteCycleAsync
  - [x] SubTask 1.1: 移除 `_pendingSteps` 字段及相关代码（OnProcessStepSequenceRequested、OnProcessStepSequenceControlRequested、ClearPendingSteps、GetProcessStepActions、ExecutePendingStepsAsync）
  - [x] SubTask 1.2: 移除 ProcessStepSequenceEvent 和 ProcessStepSequenceControlEvent 的事件订阅
  - [x] SubTask 1.3: 将 ExecuteCycleAsync 恢复为 virtual 方法（非 override），移除 ExecuteHardcodedCycleAsync 抽象方法
  - [x] SubTask 1.4: 新增 RunCustomSequenceAsync 方法，复用 RunAsync 的状态管理和异常处理逻辑

- [x] Task 2: 重构子类任务 — 合并 ExecuteHardcodedCycleAsync 回 ExecuteCycleAsync
  - [x] SubTask 2.1: LoadingTask — 将 ExecuteHardcodedCycleAsync 实现合并到 ExecuteCycleAsync override
  - [x] SubTask 2.2: DispensingTask — 同上
  - [x] SubTask 2.3: AssemblyTask — 同上

- [x] Task 3: 重构 RecipeStationBase — 移除步骤序列相关重写
  - [x] SubTask 3.1: 移除 GetProcessStepActions override
  - [x] SubTask 3.2: 移除 ExecutePendingStepsAsync override

- [x] Task 4: TaskItem 新增 StationId 属性
  - [x] SubTask 4.1: 在 TaskItem 类中添加 StationId 属性，默认值为 null
  - [x] SubTask 4.2: 更新 ProcessSequenceEditorView UI，添加工站选择下拉框

- [x] Task 5: 重构 ProcessSequenceService — 独立执行引擎
  - [x] SubTask 5.1: 注入 IStationRegistry，添加 FindStationTask 方法根据 StationId 查找目标 StationTaskBase
  - [x] SubTask 5.2: 添加 _executionCts、_isExecuting 字段，实现单任务互斥机制
  - [x] SubTask 5.3: 重写 StartTask — 通过 IStationRegistry 获取目标 StationTaskBase，创建 ProcessStepExecutor，调用 RunCustomSequenceAsync
  - [x] SubTask 5.4: 重写 StopTask — 取消 CTS 并调用 StationTaskBase.StopAsync
  - [x] SubTask 5.5: 重写 PauseTask — 调用 StationTaskBase.PauseAsync
  - [x] SubTask 5.6: 重写 ResumeTask — 调用 StationTaskBase.ResumeAsync
  - [x] SubTask 5.7: 移除 ProcessStepSequenceEvent 和 ProcessStepSequenceControlEvent 的发布代码
  - [x] SubTask 5.8: 移除 ProcessStepSequenceRequested 事件

- [x] Task 6: 更新 IProcessSequenceService 接口
  - [x] SubTask 6.1: 新增 IsExecuting 属性
  - [x] SubTask 6.2: 移除 ProcessStepSequenceRequested 事件声明

- [x] Task 7: 编译验证与代码检查
  - [x] SubTask 7.1: 编译 Core、MotionControl、StationTasks、Module 项目，确保 0 错误
  - [x] SubTask 7.2: 验证硬编码流程（RunAsync → ExecuteCycleAsync）正常工作
  - [x] SubTask 7.3: 验证步骤序列流程（StartTask → RunCustomSequenceAsync → ProcessStepExecutor）正常工作
  - [x] SubTask 7.4: 验证单任务互斥机制
  - [x] SubTask 7.5: 验证暂停/恢复/停止生命周期
  - [x] SubTask 7.6: 验证异常处理（可恢复异常、致命异常、取消异常）

# Task Dependencies
- [Task 2] depends on [Task 1] (子类合并依赖基类方法签名变更)
- [Task 3] depends on [Task 1] (RecipeStationBase 移除重写依赖基类方法移除)
- [Task 5] depends on [Task 1] (ProcessSequenceService 重构依赖 RunCustomSequenceAsync 新增)
- [Task 5] depends on [Task 4] (StartTask 需要 TaskItem.StationId 查找目标工站)
- [Task 6] depends on [Task 5] (接口更新依赖实现变更)
- [Task 7] depends on [Task 1-6] (编译验证依赖所有代码变更完成)
- [Task 1] and [Task 4] can be parallelized
