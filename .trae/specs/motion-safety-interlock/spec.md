# 运动安全区域互锁保护 Spec

## Why
当前 `IMotionService.MoveAbsAsync/MoveRelAsync` 在执行运动前不做安全区域检测，轴可能进入危险区域导致设备碰撞或损坏。需要引入基于位置的安全区域互锁机制：当 Z₁ 轴处于安全高度以下时，限制 X/Y 轴的运动方向和范围，任何违反规则的移动请求必须被拒绝并触发报警。

## What Changes
- 新增 `ISafetyZoneMonitor` 接口：定义安全区域检查、互锁规则验证、违规报警方法
- 新增 `SafetyZoneMonitor` 实现：读取当前轴位置，根据互锁规则判断目标位置是否允许
- 修改 `MotionService.MoveAbsAsync/MoveRelAsync`：在执行运动前调用 `ISafetyZoneMonitor.CheckMoveAllowed()`，拒绝不安全的移动并发布报警事件
- 新增 `SafetyViolationEvent` 事件：安全区域违规时发布，供 UI 层显示报警提示
- 安全区域配置模型 `SafetyZoneConfig`：包含各轴的安全高度阈值、危险区域边界
- **新增安全区域配置 UI**：`SafetyZoneConfigView` + `SafetyZoneConfigViewModel`，提供安全区域参数设置界面和实时状态可视化

## Impact
- Affected specs: safety-signal-config（可复用其报警事件模式）
- Affected code:
  - `MotionControl/Interfaces/ISafetyZoneMonitor.cs` — 新增接口
  - `MotionControl/Services/SafetyZoneMonitor.cs` — 新增实现
  - `MotionControl/Models/SafetyZoneConfig.cs` — 新增配置模型
  - `MotionControl/Events/SafetyViolationEvent.cs` — 新增事件
  - `MotionControl/Services/MotionService.cs` — MoveAbsAsync/MoveRelAsync 增加安全检查
  - `MotionControl/MotionControlModule.cs` — DI 注册 ISafetyZoneMonitor
  - **`Module/Controls/Maintenance/SafetyZoneConfigView.xaml`** — 新增配置视图 XAML
  - **`Module/Controls/Maintenance/SafetyZoneConfigViewModel.cs`** — 新增配置 ViewModel

## ADDED Requirements

### Requirement: 安全区域配置模型
系统 SHALL 提供 `SafetyZoneConfig` 模型，定义各轴的安全高度阈值和危险区域边界。

#### Scenario: 配置 Dx/Dy/Dz₁ 的安全区域参数
- **WHEN** 系统初始化或加载配置
- **THEN** `SafetyZoneConfig` 包含以下属性：
  - `SafeHeightZ1` (double): Dz₁ 轴安全高度阈值（默认 50.0mm），Z₁ ≥ 此值为安全区
  - `DangerZoneXMin` (double): X 轴危险区域下限（默认 0.0mm），X < 此值为危险区
  - `DangerZoneXMax` (double): X 轴危险区域上限（默认 200.0mm）
  - `DangerZoneYMin` (double): Y 轴危险区域下限（默认 0.0mm）
  - `DangerZoneYMax` (double): Y 轴危险区域上限（默认 200.0mm）
  - `Enabled` (bool): 是否启用安全互锁（默认 true）

### Requirement: ISafetyZoneMonitor 接口
系统 SHALL 定义 `ISafetyZoneMonitor` 接口，提供安全区域检查能力。

#### Scenario: 检查单轴绝对移动是否允许
- **WHEN** 调用 `CheckMoveAllowed(int axisId, double targetPosition)`
- **THEN** 返回 `(bool allowed, string reason)`：
  - 若目标位置不违反任何互锁规则 → `(true, null)`
  - 若违反规则 → `(false, "违规原因描述")`

#### Scenario: 检查多轴插补移动是否允许
- **WHEN** 调用 `CheckInterpolationMoveAllowed(int[] axisIds, double[] targetPositions)`
- **THEN** 对每个轴逐一调用 `CheckMoveAllowed`，任一轴不允许则整体返回 false

#### Scenario: 获取当前轴是否在危险区域
- **WHEN** 调用 `IsInDangerZone(int axisId)`
- **THEN** 返回该轴当前位置是否在其危险区域内

#### Scenario: 获取当前安全状态摘要
- **WHEN** 调用 `GetSafetyStatus()`
- **THEN** 返回 `SafetyStatus` 对象，包含各轴当前位置、是否在危险区、哪些互锁规则生效

### Requirement: 互锁规则 — Z₁ 高度与 X 轴负向锁定
系统 SHALL 在 Dz₁ 轴低于安全高度时，禁止 Dx 轴向负方向移动（进入 X 危险区域）。

#### Scenario: Z₁ 低于安全高度，X 尝试向负方向移动
- **WHEN** Dz₁ 当前位置 < `SafeHeightZ1`
- **AND** 请求将 Dx 移动到 < `DangerZoneXMin` 的位置
- **THEN** `CheckMoveAllowed(DxId, targetX)` 返回 `(false, "Z₁低于安全高度，禁止X轴向负方向进入危险区域")`
- **AND** 不执行运动

#### Scenario: Z₁ 低于安全高度，X 向正方向移动（安全）
- **WHEN** Dz₁ 当前位置 < `SafeHeightZ1`
- **AND** 请求将 Dx 移动到 ≥ `DangerZoneXMin` 的位置
- **THEN** `CheckMoveAllowed(DxId, targetX)` 返回 `(true, null)`

#### Scenario: Z₁ 高于或等于安全高度，X 可自由移动
- **WHEN** Dz₁ 当前位置 ≥ `SafeHeightZ1`
- **THEN** X 轴不受此规则限制，可向任意方向移动

### Requirement: 互锁规则 — Z₁ + X 双重条件锁定 Y 轴
系统 SHALL 在 Dz₁ 低于安全高度 **且** Dx 已在危险区域内时，禁止 Dy 轴移动。

#### Scenario: Z₁ 低 + X 在危险区，Y 尝试移动
- **WHEN** Dz₁ 当前位置 < `SafeHeightZ1`
- **AND** Dx 当前位置 < `DangerZoneXMin`（X 已在危险区）
- **AND** 请求 Dy 向任意方向移动
- **THEN** `CheckMoveAllowed(DyId, targetY)` 返回 `(false, "Z₁低于安全高度且X已在危险区域，禁止Y轴移动")`

#### Scenario: Z₁ 低但 X 未在危险区，Y 可自由移动
- **WHEN** Dz₁ 当前位置 < `SafeHeightZ1`
- **AND** Dx 当前位置 ≥ `DangerZoneXMin`（X 未在危险区）
- **THEN** Y 轴不受此规则限制，可自由移动

#### Scenario: Z₁ 高于安全高度，Y 可自由移动（无论 X 位置）
- **WHEN** Dz₁ 当前位置 ≥ `SafeHeightZ1`
- **THEN** Y 轴不受此规则限制

### Requirement: MotionService 集成安全检查
系统 SHALL 在 `MotionService.MoveAbsAsync` 和 `MoveRelAsync` 执行运动前调用安全检查。

#### Scenario: 绝对移动通过安全检查
- **WHEN** 调用 `MoveAbsAsync(axisId, position, velocity, token)`
- **AND** `ISafetyZoneMonitor.CheckMoveAllowed(axisId, position)` 返回 true
- **THEN** 正常执行运动

#### Scenario: 绝对移动未通过安全检查
- **WHEN** 调用 `MoveAbsAsync(axisId, position, velocity, token)`
- **AND** `ISafetyZoneMonitor.CheckMoveAllowed(axisId, position)` 返回 false
- **THEN** 抛出 `SafetyViolationException`（包含 reason 和轴信息）
- **AND** 发布 `SafetyViolationEvent` 事件
- **AND** 不执行运动
- **AND** 记录 Error 级别日志

#### Scenario: 相对移动通过安全检查
- **WHEN** 调用 `MoveRelAsync(axisId, distance, velocity, token)`
- **AND** 计算目标位置 = 当前位置 + distance 后通过安全检查
- **THEN** 正常执行运动

#### Scenario: 相对移动未通过安全检查
- **WHEN** 调用 `MoveRelAsync(axisId, distance, velocity, token)`
- **AND** 目标位置未通过安全检查
- **THEN** 同绝对移动的违规处理流程

#### Scenario: 安全互锁禁用时跳过检查
- **WHEN** `SafetyZoneConfig.Enabled` 为 false
- **THEN** 所有运动请求跳过安全检查，直接执行

### Requirement: 安全违规报警事件
系统 SHALL 在安全区域违规时发布 `SafetyViolationEvent`，供 UI 层订阅显示报警。

#### Scenario: 发布违规事件
- **WHEN** 运动请求被安全互锁阻止
- **THEN** 发布 `SafetyViolationEvent`，载荷包含：
  - `AxisId`: 被阻止的轴 ID
  - `AxisName`: 轴名称（如 "Dx"）
  - `TargetPosition`: 目标位置
  - `CurrentPosition`: 当前位置
  - `Reason`: 违规原因文本
  - `Timestamp`: 违规时间
  - `RuleName`: 触发的规则名称（如 "Z1_X_Negative_Lock" 或 "Z1_X_Danger_Y_Lock"）

#### Scenario: UI 订阅报警事件
- **WHEN** ViewModel 订阅 `SafetyViolationEvent`
- **AND** 发生安全违规
- **THEN** 可获取违规详情并更新 UI 报警提示（如红色闪烁警告条）

### Requirement: Jog 操作集成安全检查
系统 SHALL 在 `SafeJogBehavior.StartJog` 中增加安全检查，防止点动操作进入危险区域。

#### Scenario: Jog 启动前检查
- **WHEN** 用户长按 Jog 按钮
- **AND** 目标方向会导致进入危险区域
- **THEN** Jog 不启动
- **AND** 触发视觉反馈（如按钮变红闪烁）

### Requirement: 安全区域配置 UI
系统 SHALL 提供 `SafetyZoneConfigView` 配置界面，允许用户设置安全高度和危险区域边界，并实时显示当前轴位置与安全/危险区域的相对关系。

#### Scenario: 安全区域参数配置
- **WHEN** 用户打开安全区域配置界面
- **THEN** 可看到以下可编辑参数（使用 DecimalUpDown 输入）：
  - Z₁ 轴安全高度 (mm)：默认 50.0
  - X 轴危险区下限 (mm)：默认 0.0
  - X 轴危险区上限 (mm)：默认 200.0
  - Y 轴危险区下限 (mm)：默认 0.0
  - Y 轴危险区上限 (mm)：默认 200.0
  - 启用安全互锁开关：CheckBox，默认启用

#### Scenario: 实时安全状态可视化
- **WHEN** 安全区域配置界面显示时
- **THEN** 显示一个 2D 区域示意图（参考用户提供的坐标图），包含：
  - X/Y 坐标轴标注（X-/X+, Y-/Y+）
  - 绿色矩形表示各轴的安全区域
  - 红色矩形表示危险区域
  - 当前轴位置标记点（实时更新，从 ISafetyZoneMonitor.GetSafetyStatus() 获取）
  - Z₁ 当前位置与安全高度的对比指示（高于=绿色，低于=红色）

#### Scenario: 保存安全区域配置
- **WHEN** 用户修改参数后点击保存按钮
- **THEN** 参数写入 SafetyZoneConfig 并持久化到 JSON 文件
- **AND** SafetyZoneMonitor 立即使用新参数

#### Scenario: 违规报警提示条
- **WHEN** 发生安全违规事件（SafetyViolationEvent）
- **THEN** 界面顶部或底部显示红色报警提示条，包含：
  - 违规轴名称、目标位置、原因描述
  - 提示条持续显示直到用户手动关闭或违规条件解除
  - 支持多语言（zh-CN / en-US）

## MODIFIED Requirements

### Requirement: IMotionService
原 `MoveAbsAsync/MoveRelAsync` 直接转发到底层卡操作。现修改为先经过 `ISafetyZoneMonitor` 安全检查，通过后才执行实际运动。安全检查耗时须 < 1ms（纯内存计算，无 IO）。

### Requirement: SafeJogBehavior
原 `StartJog` 直接调用 `motionService.JogStart`。现修改为先调用 `safetyMonitor.CheckMoveAllowed` 判断目标方向是否允许，不允许时不启动 Jog 并记录日志。

### Requirement: MotionControlModule DI 注册
新增 `ISafetyZoneMonitor` → `SafetyZoneMonitor` 的 Singleton 注册。
