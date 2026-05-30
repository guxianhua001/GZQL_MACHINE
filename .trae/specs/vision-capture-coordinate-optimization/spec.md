# VisionCapture 坐标显示与操作优化 Spec

## Why
VisionCaptureView 的 Machine Coordinates 在没有贝塞尔曲线数据时也显示空表格造成困惑；Coordinate Transform 卡片内容与 Transform Details 重复且无独立价值；Transform Details 预览坐标时未同步视觉返回数据；相机到针头固定距离和针头补偿值无法链接全局变量；Dot/Arc 模式的操作按钮不完整，缺少停止、暂停、继续等关键控制。

## What Changes
- Machine Coordinates 仅在贝塞尔曲线所有坐标计算完成后才显示
- 删除 Coordinate Transform 卡片（参数配置已包含在 Transform Details 中）
- Transform Details 预览坐标时根据视觉返回数据同步更新数值
- NeedleOffsetX/Y 和 NeedleCompX/Y 支持链接全局变量（下拉选择或手动输入）
- Dot 模式操作按钮：【执行点胶】【停止】【预览坐标】
- Arc 模式操作按钮：【执行点胶】【暂停】【继续】【停止】【预览坐标】

## Impact
- Affected specs: vision-capture-dispense（基于其 VisionCaptureView/ViewModel）
- Affected code:
  - `Module/WorkStation/Dispense/VisionCaptureView.xaml` — UI 布局调整
  - `Module/WorkStation/Dispense/VisionCaptureViewModel.cs` — 逻辑调整
  - `StationTasks/Services/BezierArcDispenseService.cs` — 暂停/继续支持

## ADDED Requirements

### Requirement: Machine Coordinates 条件显示
系统 SHALL 仅在贝塞尔曲线所有坐标点计算完成后才显示 Machine Coordinates 区域。

#### Scenario: 无贝塞尔数据时隐藏
- **WHEN** MachinePoints 集合为空或未计算
- **THEN** Machine Coordinates 区域（标题+DataGrid）不可见

#### Scenario: 有贝塞尔数据时显示
- **WHEN** 预览坐标计算完成，MachinePoints 集合有数据
- **THEN** Machine Coordinates 区域可见，显示所有坐标点

### Requirement: Transform Details 预览坐标同步视觉数据
系统 SHALL 在预览坐标时根据视觉返回数据同步更新 Transform Details 中各步骤的数值。

#### Scenario: 拍照后预览坐标
- **WHEN** 用户完成拍照并点击【预览坐标】
- **THEN** Transform Details 各步骤数值基于最新视觉返回数据计算
- **AND** 步骤1（拍照位）显示当前 PhotoDx/PhotoDy
- **AND** 步骤2（视觉原始坐标）显示最新解析的视觉数据
- **AND** 步骤3（距离计算）基于最新视觉数据计算
- **AND** 步骤4（偏移/补偿）使用当前参数值
- **AND** 步骤5（最终坐标）基于以上数据实时计算

#### Scenario: 参数变更后预览坐标
- **WHEN** 用户修改 NeedleOffsetX/Y 或 NeedleCompX/Y 后点击【预览坐标】
- **THEN** Transform Details 使用修改后的参数重新计算所有数值

### Requirement: 相机到针头距离和针头补偿链接全局变量
系统 SHALL 支持 NeedleOffsetX/Y 和 NeedleCompX/Y 参数链接全局变量，用户可选择从全局变量读取或手动输入。

#### Scenario: 链接全局变量
- **WHEN** 用户在 NeedleOffsetX/Y 或 NeedleCompX/Y 输入框旁点击链接按钮
- **THEN** 显示全局变量下拉列表，列出所有可用全局变量
- **AND** 选择后，该参数值从选定的全局变量实时读取
- **AND** 链接状态下输入框显示为只读，显示链接的全局变量名

#### Scenario: 取消链接
- **WHEN** 用户再次点击链接按钮取消链接
- **THEN** 参数恢复为手动输入模式，当前值保留为链接时的数值

#### Scenario: 保存链接配置
- **WHEN** 用户保存配置
- **THEN** 链接关系（参数名→全局变量名）持久化到配方池
- **AND** 下次加载时自动恢复链接关系

### Requirement: Dot 模式操作按钮
系统 SHALL 在 Dot 模式下提供【执行点胶】【停止】【预览坐标】三个操作按钮。

#### Scenario: Dot 模式按钮显示
- **WHEN** 当前选中行的 DispenseType 为 Dot
- **THEN** 显示【执行点胶】【停止】【预览坐标】三个按钮
- **AND** 【执行点胶】在空闲状态可用，执行中禁用
- **AND** 【停止】在执行中可用，空闲时禁用
- **AND** 【预览坐标】在空闲状态且有视觉数据时可用

#### Scenario: Dot 模式停止执行
- **WHEN** 用户在 Dot 点胶执行中点击【停止】
- **THEN** 通过 CancellationToken 取消当前点胶操作
- **AND** Z轴优先抬起至安全高度

### Requirement: Arc 模式操作按钮
系统 SHALL 在 Arc 模式下提供【执行点胶】【暂停】【继续】【停止】【预览坐标】五个操作按钮。

#### Scenario: Arc 模式按钮显示
- **WHEN** 当前选中行的 DispenseType 为 Arc
- **THEN** 显示【执行点胶】【暂停】【继续】【停止】【预览坐标】五个按钮
- **AND** 【执行点胶】在空闲状态可用，执行中/暂停中禁用
- **AND** 【暂停】在执行中可用，空闲/暂停中禁用
- **AND** 【继续】在暂停中可用，空闲/执行中禁用
- **AND** 【停止】在执行中/暂停中可用，空闲时禁用
- **AND** 【预览坐标】在空闲状态且有视觉数据时可用

#### Scenario: Arc 模式暂停执行
- **WHEN** 用户在 Arc 点胶执行中点击【暂停】
- **THEN** 当前段插补走胶完成后暂停
- **AND** Z轴保持当前位置（不抬起）
- **AND** 状态切换为"暂停中"

#### Scenario: Arc 模式继续执行
- **WHEN** 用户在暂停状态点击【继续】
- **THEN** 从暂停点继续执行剩余段插补走胶
- **AND** 状态切换为"执行中"

#### Scenario: Arc 模式停止执行
- **WHEN** 用户在执行中或暂停中点击【停止】
- **THEN** 通过 CancellationToken 取消当前点胶操作
- **AND** Z轴优先抬起至安全高度

## MODIFIED Requirements

### Requirement: Coordinate Transform 卡片删除
原 Coordinate Transform 卡片（6个参数输入+Save按钮）删除。参数配置功能合并到 Transform Details 区域中，Transform Details 的步骤4已包含 NeedleOffsetX/Y 和 NeedleCompX/Y 的显示和编辑，步骤4增强为支持全局变量链接。

### Requirement: 操作按钮区域重构
原操作按钮区域（SiteFeature选择+RunMode选择+执行点胶+预览坐标）重构为按 Dot/Arc 模式分别显示不同的操作按钮集。SiteFeature 选择保留，RunMode 选择保留，但操作按钮根据模式动态切换。

## REMOVED Requirements

### Requirement: Coordinate Transform 独立卡片
**Reason**: 与 Transform Details 内容重复，参数编辑功能已包含在 Transform Details 中
**Migration**: Transform Details 步骤4增强为支持编辑和全局变量链接，替代原 Coordinate Transform 卡片的参数配置功能
