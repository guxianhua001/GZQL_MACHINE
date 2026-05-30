# 移除 ProcessSequenceEditorView 中 Station 下拉选项

## 背景

ProcessSequenceEditorView 的工具栏中有一个 Station 下拉框，绑定 `StationOptions` / `SelectedStationId`，用于为 TaskItem 设置 `StationId`。但实际任务执行时，系统会根据当前轴所在工站自动匹配对应的 Task，无需手动指定默认工站。该下拉框属于冗余 UI，需要移除。

## 依赖分析

### Station 下拉框涉及的代码链

| 层级 | 文件 | 代码 | 作用 |
|------|------|------|------|
| **View** | `Module/Editor/ProcessSequenceEditorView.xaml` L137-139 | `TextBlock "Station:"` + `ComboBox` | UI 显示 |
| **ViewModel** | `Module/Editor/ProcessSequenceEditorViewModel.cs` L67-71 | `StationOptions` 初始化 + `_stationRegistry.GetAllStations()` | 数据源 |
| **ViewModel** | `Module/Editor/ProcessSequenceEditorViewModel.cs` L243-256 | `StationOptions` 属性 + `SelectedStationId` 属性 | 绑定属性 |
| **ViewModel** | `Module/Editor/ProcessSequenceEditorViewModel.cs` L124 | `RaisePropertyChanged(nameof(SelectedStationId))` | 切换任务时通知 |
| **ViewModel** | `Module/Editor/ProcessSequenceEditorViewModel.cs` L32,54 | `_stationRegistry` 字段 | 依赖注入 |
| **Model** | `Module/Models/WorkOrderData.cs` L239-245 | `TaskItem.StationId` 属性 | 模型字段 |
| **Service** | `Module/Services/ProcessSequenceService.cs` L174-193,204 | `FindStationTask()` + `StartTask()` | 运行时使用 StationId |

### 关键发现

1. **`TaskItem.StationId` 在序列化时被忽略**：`SequenceTaskData` 不包含 `StationId` 字段，保存/加载 JSON 时不会持久化该值
2. **`_stationRegistry` 在 ViewModel 中仅用于构建 `StationOptions`**，移除下拉框后该依赖可一并清理
3. **任务执行流程中 StationId 的实际作用是多余的**，分析如下：

### 任务执行流程分析

当前 `StartTask()` 的执行链：

```
StartTask()
  → FindStationTask(CurrentTask.StationId)   // 用 StationId 查找"宿主工站"
  → stationTask.RunCustomSequenceAsync(...)
    → ProcessStepExecutor(stationTask, ...)
      → 每个步骤的 action.ExecuteAsync(step, task, token)
        → GotoStepAction: ResolveTargetTask(subMove.StationId, task)
          → SubMove 指定了 StationId → 路由到对应工站执行
          → SubMove 未指定 StationId → 使用传入的 task（宿主工站）
```

### 宿主工站（StationTaskBase）在 ProcessStepExecutor 中的实际作用

宿主工站**不是"运动执行者"，而是"执行上下文"**，提供以下运行时基础设施：

| 用途 | 代码 | 说明 |
|------|------|------|
| 暂停/急停/单步保护 | `_task.ExecuteStepSafeAsync()` | RunStep 包装器，提供可恢复异常保护 |
| 故障步骤标记 | `_task.LastFaultStepName` | 标记哪个步骤出了故障 |
| 排除自身 | `_task.StationId` in `CollectTargetStations()` | 收集跨工站目标时排除宿主 |
| 事件通信 | `_task.Ea` | 事件聚合器，发布状态变更事件 |
| 工站注册表 | `_task.StationRegistry` | 用于查找其他工站 |
| 日志/报警来源 | `_task.TaskName` | 报警和日志的 source 标识 |

### 为什么"取默认工站作为宿主"是设计过度

1. **SubMove.StationId 已明确运动路由**：`GotoStepAction.ResolveTargetTask()` 根据每个 SubMove 的 StationId 自动路由到正确工站
2. **宿主工站只提供上下文，不决定运动**：`ExecuteStepSafeAsync` 提供的是暂停保护、状态发布等通用能力，与"哪个工站"无关
3. **"取第一个可用工站"是不确定行为**：依赖注册顺序，多工站场景下不可预测
4. **当前 StationId 从未被持久化**：每次加载 JSON 后 StationId 都是 null，说明现有逻辑已经在走降级路径

## 设计方案对比

### 方案 A：复用已有工站（取第一个可用工站作为宿主）

移除 `TaskItem.StationId`，`FindStationTask` 简化为无参方法，始终取第一个可用工站。

- **优点**：改动最小，仅涉及 4 个文件
- **缺点**：逻辑上不清晰——序列执行为什么要"借用"某个工站？宿主工站的 `TaskName` 会出现在日志/报警中，可能误导

### 方案 B：新建轻量级 SequenceExecutionContext（推荐 ✅）

创建一个不继承 `StationTaskBase` 的 `SequenceExecutionContext`，只提供 `ProcessStepExecutor` 真正需要的能力接口。

**分析 ProcessStepExecutor 对宿主的实际依赖：**

| 依赖 | 用途 | SequenceExecutionContext 如何提供 |
|------|------|------|
| `_task.ExecuteStepSafeAsync()` | 暂停/急停/单步保护 | 需要实现——但这是 `RunStep` 的核心逻辑，不能省略 |
| `_task.LastFaultStepName` | 故障步骤标记 | 简单属性，直接提供 |
| `_task.StationId` | CollectTargetStations 排除自身 | 可设为空或虚拟值 |
| `_task.Ea` | 事件聚合器 | 直接注入 IEventAggregator |
| `_task.StationRegistry` | 查找其他工站 | 直接注入 IStationRegistry |
| `_task.TaskName` | 日志/报警来源 | 设为 "ProcessSequence" |
| `action.ExecuteAsync(step, task, token)` | Action 接口签名 | **需要修改接口** |

**核心障碍**：`IProcessStepAction.ExecuteAsync` 的第二个参数是 `StationTaskBase task`，所有 Action（GotoStepAction、VisionStepAction、Scan3DStepAction、DashboardStepAction）都依赖这个参数调用 `task.ExecuteMoveAsync()`、`task.PublishStepStatus()` 等方法。要替换为 `SequenceExecutionContext`，需要：

1. 抽取 `StationTaskBase` 中 Action 真正使用的方法为接口（如 `IStepExecutionContext`）
2. 让 `StationTaskBase` 和 `SequenceExecutionContext` 都实现该接口
3. 修改 `IProcessStepAction.ExecuteAsync` 签名为 `ExecuteAsync(step, IStepExecutionContext, token)`

这是一个较大的重构，涉及接口变更和所有 Action 的适配。

### 方案 C：新建虚拟工站（继承 StationTaskBase）

创建 `VirtualSequenceTask : StationTaskBase`，注册到工站注册表，专做序列执行宿主。

- **优点**：无需修改接口签名，复用现有基础设施
- **缺点**：
  - `StationTaskBase` 构造函数需要 `IMotionService`, `IPositionProvider`, `IStationInteractionService`, `ISystemStateService`, `ISpeedOverrideService` 等大量依赖
  - 虚拟工站会出现在工站列表中，需要过滤
  - 违反 LSP——虚拟工站不是真正的工站，却继承了工站的全部能力

### 推荐方案：方案 A（当前阶段）+ 方案 B（后续优化）

**当前阶段采用方案 A**，理由：
1. 改动最小，风险最低，满足"移除 Station 下拉框"的直接需求
2. 宿主工站的选择不影响实际运动执行（由 SubMove.StationId 路由）
3. "取第一个可用工站"虽然逻辑上不够优雅，但功能上完全正确

**后续优化可考虑方案 B**，将 `StationTaskBase` 中 Action 依赖的能力抽取为 `IStepExecutionContext` 接口，实现职责分离。但这属于架构重构，应作为独立任务。

## 实施步骤（方案 A）

### 步骤 1：移除 XAML 中 Station 下拉框 UI

**文件**：`Module/Editor/ProcessSequenceEditorView.xaml`

删除以下代码（L136-139）：
```xml
<Separator/>
<TextBlock Text="Station:" VerticalAlignment="Center" FontSize="12" Margin="0,0,4,0"/>
<ComboBox Width="100" ItemsSource="{Binding StationOptions}"
          SelectedItem="{Binding SelectedStationId, UpdateSourceTrigger=PropertyChanged}" FontSize="12"/>
```

### 步骤 2：清理 ViewModel 中 Station 相关代码

**文件**：`Module/Editor/ProcessSequenceEditorViewModel.cs`

1. 移除 `_stationRegistry` 字段（L32）和构造函数参数（L45）及赋值（L54）
2. 移除 `StationOptions` 初始化代码（L67-71）
3. 移除 `StationOptions` 属性声明（L243-244）及其 XML 注释（L241-242）
4. 移除 `SelectedStationId` 属性声明（L248-256）及其 XML 注释（L245-247）
5. 移除 `RaisePropertyChanged(nameof(SelectedStationId))` 调用（L124）

### 步骤 3：移除 TaskItem.StationId 属性

**文件**：`Module/Models/WorkOrderData.cs`

移除 `TaskItem` 类中的 `StationId` 属性（L239-245）及其注释。

### 步骤 4：简化 ProcessSequenceService 中的 FindStationTask

**文件**：`Module/Services/ProcessSequenceService.cs`

1. 将 `FindStationTask(string stationId)` 简化为 `FindStationTask()`，移除 `stationId` 参数
2. 方法体只保留"取第一个可用 StationTaskBase"的逻辑，移除按 StationId 查找的分支
3. 更新 `StartTask()` 中的调用：`FindStationTask(CurrentTask.StationId)` → `FindStationTask()`

### 步骤 5：构建验证

执行 `dotnet build` 确认无编译错误。

## 影响评估

- **序列化兼容性**：✅ `StationId` 从未被序列化到 JSON，旧文件可正常加载
- **任务执行**：✅ 移除后 `FindStationTask` 始终取第一个可用工站作为执行宿主，实际运动路由由 `SubMove.StationId` 决定，行为与之前 StationId 为 null 时一致
- **UI 布局**：✅ 移除下拉框后工具栏更简洁，Task 选择 + Step 编辑功能不受影响
- **架构清晰度**：✅ 消除了"默认工站"这一模糊概念，执行宿主与运动路由职责分离更明确

## 后续优化方向

将 `StationTaskBase` 中 Action 依赖的能力（`ExecuteStepSafeAsync`, `PublishStepStatus`, `CompleteStepStatus`, `TaskLogger`, `Ea`, `StationRegistry`, `LastFaultStepName`）抽取为 `IStepExecutionContext` 接口，创建独立的 `SequenceExecutionContext` 实现，彻底解耦序列执行与工站任务。
