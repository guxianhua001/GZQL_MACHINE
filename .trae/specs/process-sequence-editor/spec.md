# 步骤流程编辑器与执行引擎 Spec

## Why
当前 ProcessSequenceEditorView 编辑器与运行时执行引擎完全脱节——编辑器中定义的步骤序列无法被 StationTaskBase 消费执行，所有 Task 子类都是硬编码的 RunStep 调用。需要打通编辑器到运行时的数据通道，实现"编辑即配置，配置即执行"的闭环。

## What Changes
- **重构 ProcessSequenceEditorView**：完善步骤管理功能（创建、编辑、删除、排序、参数配置）
- **重构 GotoDetailView**：增加工站-轴选择、配方位置选择器、速度设置、Offset全局变量选择
- **新增 ProcessStepExecutor**：运行时步骤执行引擎，从 ProcessStep 序列驱动动作执行
- **新增 GotoStepAction**：GOTO 类型步骤的动作类，解析 SubMove 列表调用 StationTaskBase 运动原语
- **实现 StartTaskCommand**：将编辑器中的步骤序列传递给执行引擎运行
- **实现 SaveToJsonCommand / 自动加载**：步骤序列数据持久化与启动恢复

## Impact
- Affected specs: 步骤编辑器、运行时任务执行、配方位置系统
- Affected code:
  - `Module/Operators/Editor/ProcessSequenceEditorView.xaml` + ViewModel
  - `Module/Operators/Editor/GotoDetailView.xaml` + ViewModel
  - `Module/Models/ProcessStep.cs` (SubMove 模型扩展)
  - `MotionControl/Services/StationTaskBase.cs` (新增步骤执行入口)
  - `StationTasks/` 新增 ProcessStepExecutor、GotoStepAction 等类
  - `Module/Services/ProcessSequenceService.cs` (持久化扩展)

## ADDED Requirements

### Requirement: ProcessSequenceEditorView 步骤管理
系统 SHALL 提供完整的步骤管理功能，支持步骤的创建、编辑、删除、排序及参数配置。

#### Scenario: 创建步骤
- **WHEN** 用户点击"Add Step"按钮
- **THEN** 弹出 AddEditStepDialog 对话框，用户选择 StepType 后创建新步骤
- **AND** 新步骤追加到序列末尾，Seq 自动递增

#### Scenario: 删除步骤
- **WHEN** 用户选中一个步骤并点击"Delete"按钮
- **THEN** 该步骤从序列中移除，剩余步骤的 Seq 自动重新编号

#### Scenario: 排序步骤
- **WHEN** 用户点击"Move Up"或"Move Down"按钮
- **THEN** 选中步骤与相邻步骤交换位置，Seq 自动重新编号

#### Scenario: 编辑步骤参数
- **WHEN** 用户在 DataGrid 中直接编辑 CompFeature/SiteFeature/Camera/Purpose
- **THEN** 修改实时反映到 ProcessStep 模型

#### Scenario: 选择 GOTO 类型触发详情配置
- **WHEN** 用户选择 StepType 为 GOTO 的步骤
- **THEN** StepDetailRegion 自动导航到 GotoDetailView
- **AND** GotoDetailView 以模态对话框形式弹出，展示该步骤的详细配置

### Requirement: GotoDetailView 详细配置页面
系统 SHALL 提供 GotoDetailView 模态对话框，包含工站-轴选择、配方位置选择器、速度设置和 Offset 配置。

#### Scenario: 工站-轴选择
- **WHEN** 用户在 GotoDetailView 中添加 SubMove
- **THEN** Axis 下拉框显示当前配方中所有工站的所有轴名称（格式："工站名.轴名"）
- **AND** 用户选择轴后，系统记录对应的 axisId

#### Scenario: 配方轴位置选择器
- **WHEN** 用户在 SubMove 行中点击 PositionName 下拉框
- **THEN** 下拉框显示当前配方中选中轴所属工站的所有位置点名
- **AND** 选中后 PositionName 自动填入

#### Scenario: 速度设置控件
- **WHEN** 用户在 SubMove 行中编辑 Speed 列
- **THEN** 输入值必须为正数，超出范围时显示验证错误提示
- **AND** 速度值受全局速度百分比 ISpeedOverrideService 覆盖

#### Scenario: Offset 配置项
- **WHEN** 用户在 SubMove 行中编辑 Offset 列
- **THEN** 提供两种模式：直接输入数值 或 从全局变量列表中选择
- **AND** 选择全局变量后，Offset 值在运行时从 GlobalVariable 动态解析

### Requirement: ProcessStepExecutor 运行时执行引擎
系统 SHALL 提供 ProcessStepExecutor 类，从 ProcessStep 序列驱动动作执行，遵循 StationTaskBase 的 RunStep 安全保护机制。

#### Scenario: 执行 GOTO 步骤
- **WHEN** ProcessStepExecutor 遍历到 StepType.GOTO 类型的步骤
- **THEN** 解析该步骤的 SubMove 列表，按顺序调用 MoveToAsync(axisId, positionName, speed)
- **AND** 每个 SubMove 的 Offset 叠加到目标位置上
- **AND** 如果 Offset 引用了全局变量，运行时从 IRecipePoolService 动态解析值

#### Scenario: 步骤执行受 RunStep 保护
- **WHEN** ProcessStepExecutor 执行任意步骤
- **THEN** 每个步骤通过 StationTaskBase.RunStep 包装执行
- **AND** 享受暂停/急停/单步/可恢复异常重试等安全保护

#### Scenario: CHECK 步骤跳转
- **WHEN** ProcessStepExecutor 遇到 StepType.CHECK 类型的步骤
- **THEN** 根据 CheckDetail 的 OnPassAction/OnFailAction 决定下一步
- **AND** SkipTo 动作跳转到指定 Seq 的步骤继续执行

### Requirement: StartTaskCommand 启动执行
系统 SHALL 实现 StartTaskCommand，将编辑器中的步骤序列传递给执行引擎运行。

#### Scenario: 启动任务执行
- **WHEN** 用户点击 StartTask 按钮
- **THEN** 系统将当前 ProcessStep 序列传递给 ProcessStepExecutor
- **AND** ProcessStepExecutor 在 StationTaskBase.ExecuteCycleAsync 中按序执行步骤
- **AND** 当前执行步骤在 UI 中高亮显示（IsCurrent 标记）

#### Scenario: 暂停/恢复/停止任务
- **WHEN** 用户点击 Pause/Resume/Stop 按钮
- **THEN** 调用 StationTaskBase 对应的 PauseAsync/ResumeAsync/StopAsync 方法
- **AND** 执行引擎在当前步骤完成后响应控制命令

### Requirement: 步骤序列数据持久化
系统 SHALL 实现 SaveToJsonCommand 保存步骤序列数据，并在应用启动时自动加载最后一次使用的配置。

#### Scenario: 保存步骤序列
- **WHEN** 用户点击"Save Sequence"按钮
- **THEN** 步骤序列序列化为 JSON 文件保存到 Config/ProcessSequences/ 目录
- **AND** 文件名包含工站标识和时间戳

#### Scenario: 自动加载配置
- **WHEN** 应用启动并导航到 ProcessSequenceEditorView
- **THEN** 自动加载最后一次保存的配置文件
- **AND** 最后使用的文件路径记录在 IAppSettingService.ExtensionData["LastProcessSequencePath"] 中

#### Scenario: 手动加载配置
- **WHEN** 用户点击"Load Sequence"按钮
- **THEN** 弹出文件选择对话框，用户选择 JSON 文件后加载步骤序列

## MODIFIED Requirements

### Requirement: SubMove 模型扩展
原 SubMove 模型仅包含 Axis/PositionName/Offset/Speed 字符串字段。扩展为支持工站-轴映射和全局变量引用。

修改后模型：
```csharp
public class SubMove : BindableBase
{
    public string SubSeq { get; set; }
    public string StationId { get; set; }        // 新增：工站标识（如 "LoadingStation"）
    public string Axis { get; set; }             // 轴名（如 "Y"）
    public int AxisId { get; set; }              // 新增：轴逻辑ID（运行时使用）
    public string PositionName { get; set; }
    public string Description { get; set; }
    public double Offset { get; set; }
    public string OffsetVariableName { get; set; } // 新增：引用的全局变量名（空则使用固定值）
    public double Speed { get; set; }
}
```

### Requirement: GotoDetailView 改为模态对话框
原 GotoDetailView 嵌入在 StepDetailRegion 中。修改为通过 DialogHost 弹出模态对话框，与 RecoverableFaultDialogView 一致。

## REMOVED Requirements
无移除项。
