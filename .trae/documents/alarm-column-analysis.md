# 步骤编辑器 Alarm 列功能分析报告

## 一、功能概述

Alarm 列是步骤编辑器（ProcessSequenceEditorView）DataGrid 的最后一列（耗时列之前），提供**步骤级报警配置**功能。每个工艺步骤可独立配置：是否启用报警、自定义报警代码、报警等级。当步骤执行过程中发生异常时，系统根据此配置触发报警、标记步骤状态、并在 UI 上提供视觉反馈。

## 二、数据模型

### StepAlarmConfig 类（Core 层）

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `IsEnabled` | bool | false | 是否启用步骤级报警 |
| `AlarmCode` | string | "" | 自定义报警代码（如 SCAN_TIMEOUT、VISION_FAIL），为空时自动生成 |
| `AlarmLevel` | int | 3 | 报警等级：1=紧急 2=严重 3=一般 4=提示 |

继承 `BindableBase`，支持 MVVM 双向绑定。放在 Core 层以便 MotionControl 和 StationTasks 都能引用。

### ProcessStep 中的关联属性

| 属性 | 类型 | JsonIgnore | 说明 |
|------|------|------------|------|
| `AlarmConfig` | StepAlarmConfig | 否 | 报警配置对象，序列化保存 |
| `IsAlarmEnabled` | bool | 否 | 扁平属性，AlarmConfig.IsEnabled 变化时联动通知 |
| `HasActiveAlarm` | bool | 是 | 运行时状态，步骤实际触发报警后为 true |
| `ErrorMessage` | string | 是 | 运行时错误信息 |
| `HasError` | bool | 是 | ErrorMessage 非空时为 true |

## 三、UI 层实现

### 3.1 Alarm 列显示模式（CellTemplate）

- **启用时**：红色铃铛图标 + `"{AlarmCode} L{AlarmLevel}"` 格式文本（如 `SCAN_FAIL L3`）
- **未启用时**：灰色"（未启用）"文本
- **Tooltip**：显示报警配置详情（代码、等级、等级含义说明）

### 3.2 Alarm 列编辑模式（CellEditingTemplate）

- **CheckBox**：控制 IsEnabled
- **TextBox**：编辑 AlarmCode（宽度70）
- **ComboBox**：选择 AlarmLevel（1-4，宽度48）

### 3.3 报警触发后的 UI 反馈

| 反馈方式 | 触发条件 | 效果 |
|----------|----------|------|
| 行背景色变红 | `HasActiveAlarm = true` | 浅红色背景 `#FFEBEE` + Tooltip 显示报警代码和等级 |
| Seq 列错误图标 | `HasError = true` | 红色 AlertCircle 图标 |
| 顶部报警横幅 | `HasAnyStepError = true` | 红色横幅显示错误信息 + "清除错误"按钮 |

## 四、运行时执行链路

### 4.1 完整数据流

```
用户在 Alarm 列编辑 (IsEnabled/AlarmCode/AlarmLevel)
    ↓ [双向绑定]
ProcessStep.AlarmConfig (StepAlarmConfig)
    ↓ [序列化保存到 JSON 配方文件]

运行时执行:
ProcessStepExecutor.ExecuteWithRunStepAsync → 传递 step.AlarmConfig
    ↓
StationTaskBase.RunStep(stepName, action, publishStatus, alarmConfig)
    ↓ [捕获 RecoverableException]
    ├── 设置 LastFaultStepName = stepName
    ├── 发布 StepFaultedEvent → step.HasActiveAlarm = true → 行背景变红
    ├── 发布 StepErrorEvent → step.ErrorMessage = "[ErrorCode] 消息" → 错误图标显示
    ├── if alarmConfig.IsEnabled:
    │   AlarmService.TriggerAlarmAsync(自定义AlarmCode, 自定义AlarmLevel)
    │   else:
    │   AlarmService.TriggerAlarmAsync("RECOVERABLE_FAULT", General)
    └── 暂停等待操作员恢复
```

### 4.2 设置 HasActiveAlarm = true 的 5 条路径

| 路径 | 触发条件 |
|------|----------|
| StepFaultedEvent 回调 | RunStep 捕获 RecoverableException |
| OperationCanceledException | 步骤被取消且 LastFaultStepName 非空且 AlarmConfig 启用 |
| 通用 Exception | 步骤执行异常且 AlarmConfig 启用 |
| LastFaultStepName 匹配 | 步骤执行完成后检查 LastFaultStepName 匹配 |
| CHECK 超限报警 | OnMaxExceededAction.Alarm 触发 |

### 4.3 清除 HasActiveAlarm 的 3 条路径

| 路径 | 触发条件 |
|------|----------|
| 任务启动时 | ProcessSequenceService 遍历所有步骤重置 |
| JSON 加载时 | EnsureAlarmConfigInitialized 反序列化后重置 |
| 操作员点击"清除错误" | ClearStepErrorCommand 手动清除 |

### 4.4 报警代码和等级的决策逻辑

| 场景 | alarmCode | level | type |
|------|-----------|-------|------|
| 步骤报警启用 + 有自定义代码 | 自定义 AlarmCode | 自定义 AlarmLevel | ProcessError |
| 步骤报警启用 + 无自定义代码 | `STEP_FAULT_{stepName}` | 自定义 AlarmLevel | ProcessError |
| 步骤报警未启用 | `RECOVERABLE_FAULT` | General(3) | ProcessError |
| 致命异常 | `STEP_FATAL_ERROR` | Serious(2) | ProcessError |

## 五、报警服务（AlarmService）行为

- **防抖机制**：相同 Code + Source 在 60 秒窗口内不重复触发，只更新时间
- **持久化**：创建 AlarmRecord 写入 SQLite 数据库
- **初始状态**：`Status = AlarmStatus.Unconfirmed`
- **通知**：通过 AlarmTriggered 事件和 INotificationService 通知 UI

## 六、设计亮点

1. **配置与运行时分离**：AlarmConfig 参与序列化（配方保存），HasActiveAlarm/ErrorMessage 标记 JsonIgnore（运行时状态）
2. **事件驱动**：通过 StepFaultedEvent/StepErrorEvent 解耦 Executor 和 UI，避免直接引用
3. **渐进式反馈**：行背景色 → 错误图标 → 顶部横幅，三级视觉反馈
4. **灵活配置**：每个步骤可独立配置报警代码和等级，支持自定义和自动生成
5. **安全设计**：报警标记在任务启动时自动清除，避免旧报警干扰新运行周期
