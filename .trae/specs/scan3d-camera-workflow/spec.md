# 3D 相机扫描工作流 Spec

## Why
现有 GotoDetailView 仅支持纯轴移动，VisionDetailView 仅支持"发送触发→等待响应→解析"的简单模式，两者均无法支持 3D 相机拍照所需的"运动+IO触发+移动中实时TCP数据接收"复合工作流。需要新增 SCAN 步骤类型，将运动控制、IO 信号触发、TCP/IP 实时数据接收和数据解析整合为一条完整的 3D 相机扫描流水线。

## What Changes
- **新增 Scan3DStepAction**：实现 `IProcessStepAction`，支持 `StepType.SCAN`，编排7步连续动作工作流
- **增强 ScanDetail 模型**：添加 3D 相机专用配置字段（IO端口、TCP连接、各位置名、速度、延时等），所有参数外部化
- **重构 ScanDetailViewModel**：从 Region 导航模式迁移到 DialogHost 模态弹窗模式，与 GOTO/VISION 一致；添加 3D 相机配置区域和数据解析面板
- **重构 ScanDetailView.xaml**：重新设计 UI 布局，包含运动配置区、IO/通讯配置区、数据解析面板
- **新增 3D 相机数据解析器**：支持 `Camera=3DCAMERA;VISION_RESULT:SUCCESS:val1,val2,...` 格式解析
- **新增路由**：在 `NavigateToDetailView` 中添加 SCAN 步骤分支
- **注册 Scan3DStepAction**：在 StationTasksModule 中注册为 IProcessStepAction

## Impact
- Affected specs: tcpip-vision-integration（复用 ITCPEventService 接口）
- Affected code:
  - `StationTasks/Models/ProcessStep.cs` — ScanDetail 模型增强
  - `StationTasks/Actions/` — 新增 Scan3DStepAction
  - `StationTasks/StationTasksModule.cs` — 注册新 Action
  - `Module/Operators/Editor/ScanDetailViewModel.cs` — 重构
  - `Module/Operators/Editor/ScanDetailView.xaml` — 重构
  - `Module/Operators/Editor/ProcessSequenceEditorViewModel.cs` — 添加 SCAN 路由
  - `StationTasks/Services/` — 新增 Camera3DDataParser

## ADDED Requirements

### Requirement: 3D 相机扫描步骤动作（Scan3DStepAction）
系统 SHALL 提供 `Scan3DStepAction`，实现 `IProcessStepAction` 接口，`SupportedStepType = SCAN`，按以下7步顺序编排执行：

1. Z 轴抬升至初始位置
2. X 轴移动至起始点
3. Z 轴下降至拍照高度
4. 触发 IO 拍照信号（异步自动复位，延时不阻塞后续流程）
5. X 轴移动至终点（移动过程中通过 TCP/IP 实时接收并解析 3D 相机回传数据）
6. Z 轴抬升至安全高度
7. X 轴返回待机位置

#### Scenario: 正常执行 3D 相机扫描流程
- **WHEN** 执行 SCAN 类型步骤且 ScanDetail 配置完整
- **THEN** 系统按7步顺序执行，IO 触发异步复位不阻塞，X 轴移动期间实时接收 TCP 数据并解析，最终将解析结果映射到全局变量

#### Scenario: TCP 数据接收超时
- **WHEN** X 轴移动期间未在配置超时时间内收到 3D 相机数据
- **THEN** 抛出 RecoverableException，提示操作员检查相机连接

#### Scenario: IO 触发信号自动复位
- **WHEN** 触发 IO 拍照信号后
- **THEN** 系统异步执行延时复位（可配置延时毫秒数），复位操作不阻塞工作流后续步骤

### Requirement: ScanDetail 模型增强
系统 SHALL 扩展 `ScanDetail` 模型，添加以下外部化配置字段：

- **运动配置**：ZAxisId、XAxisId、ZInitPosition（初始位置名）、XStartPosition（起始点位置名）、ZPhotoPosition（拍照高度位置名）、XEndPosition（终点位置名）、ZSafePosition（安全高度位置名）、XStandbyPosition（待机位置名）、MoveSpeed（移动速度）
- **IO 配置**：TriggerIoPort（触发IO端口号）、IoResetDelayMs（IO自动复位延时毫秒）
- **通讯配置**：CommunicationType、ConnectionName、ResponseTimeout
- **数据解析**：ParseScript、VariableMappings（复用现有 VariableMapping 类）
- **Tab 配置**：TabCount（Tab 数量，默认6）、TabHeightKeys（Tab 高度键名列表，如 Tab1Height, Tab2Height...）

所有字段均有合理默认值，源代码中禁止硬编码数值。

### Requirement: 3D 相机数据解析器（Camera3DDataParser）
系统 SHALL 提供 `Camera3DDataParser`，实现 `IVisionDataParser` 接口，支持以下数据格式解析：

```
Camera=3DCAMERA;VISION_RESULT:SUCCESS:14.164,10.713,9.399,11.682,13.871,11.75,0,0,...,0
```

解析规则：
- 按 `;` 分割，找到以 `VISION_RESULT:` 开头的段
- 提取状态（SUCCESS/FAIL）和数值列表
- 前 N 个数值（N = TabCount）分别映射为 Tab1Height, Tab2Height, ... TabNHeight
- 状态非 SUCCESS 时抛出 RecoverableException

#### Scenario: 解析成功
- **WHEN** 输入 `Camera=3DCAMERA;VISION_RESULT:SUCCESS:14.164,10.713,9.399,11.682,13.871,11.75,0,0`
- **THEN** 返回 `{"Tab1Height": 14.164, "Tab2Height": 10.713, "Tab3Height": 9.399, "Tab4Height": 11.682, "Tab5Height": 13.871, "Tab6Height": 11.75}`

#### Scenario: 解析失败状态
- **WHEN** 输入 `Camera=3DCAMERA;VISION_RESULT:FAIL:`
- **THEN** 抛出 RecoverableException，提示视觉检测失败

### Requirement: SCAN 步骤详细编辑器（ScanDetailView 重构）
系统 SHALL 重构 `ScanDetailView` 和 `ScanDetailViewModel`：

- 从 Region 导航模式迁移到 DialogHost 模态弹窗模式（与 GOTO/VISION 一致）
- UI 包含以下区域：
  1. **运动配置区**：Z轴/X轴选择、各位置名下拉（从 IPositionProvider 加载）、移动速度
  2. **IO 配置区**：触发IO端口号、自动复位延时
  3. **通讯配置区**：通讯方式选择、TCP连接下拉、响应超时
  4. **数据解析区**：解析脚本编辑、Tab数量配置、变量映射表格
  5. **数据解析面板**：实时显示解析结果（Tab高度值表格，含上限/下限/实测值/偏差/状态列）
  6. **执行测试区**：示例数据填充、执行测试按钮、结果展示

### Requirement: SCAN 步骤路由注册
系统 SHALL 在 `ProcessSequenceEditorViewModel.NavigateToDetailView` 中添加 `StepType.SCAN` 分支，以 DialogHost 弹窗方式展示 `ScanDetailView`。

### Requirement: Scan3DStepAction DI 注册
系统 SHALL 在 `StationTasksModule.RegisterTypes` 中将 `Scan3DStepAction` 注册为 `IProcessStepAction` 单例。

## MODIFIED Requirements

### Requirement: ScanDetail 模型
原 `ScanDetail` 模型仅包含 ScanMode/StepX/StepY/ScanRangeX/ScanRangeY/ScanMoves/ScanData 字段。现需扩展为 3D 相机专用配置模型，保留原有字段兼容性，新增运动/IO/通讯/解析配置字段。原有 ScanMoves 和 ScanData 字段保留但不再作为 3D 扫描的主要配置方式。

## REMOVED Requirements
无。所有现有功能保持兼容。
