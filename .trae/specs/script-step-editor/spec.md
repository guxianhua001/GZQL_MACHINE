# C# 脚本步骤编辑器 Spec

## Why
当前步骤编辑器缺少通用脚本执行能力，复杂的数据处理逻辑只能硬编码到各个 StepAction 中。需要新增 SCRIPT 步骤类型，允许用户在编辑器中编写 C# 脚本，运行时动态编译执行，支持引用全局变量和前序步骤的输出参数，实现"配置即逻辑"的灵活流程控制。

## What Changes
- 在 `StepType` 枚举中新增 `SCRIPT` 类型
- 新增 `ScriptDetail` 数据模型，包含脚本代码、引用的程序集、命名空间等配置
- 新增 `ScriptStepAction` 执行器，基于 Natasha 动态编译执行脚本
- 新增 `ScriptDetailViewModel`，管理脚本编辑、编译检查、全局变量/输出参数引用插入
- 新增 `ScriptDetailView` 弹出页面，提供代码编辑器、变量引用面板、编译/执行按钮
- 在 `ProcessStep` 模型中新增 `ScriptDetail` 属性
- 在 `ProcessSequenceEditorViewModel` 中新增 SCRIPT 步骤的路由处理
- 注册 `ScriptStepAction` 到 DI 和 ProcessStepExecutor

## Impact
- Affected specs: process-sequence-editor, refactor-step-sequence-execution
- Affected code:
  - `StationTasks/Models/ProcessStep.cs` — 新增 ScriptDetail 属性 + SCRIPT 枚举值
  - `StationTasks/Actions/` — 新增 ScriptStepAction
  - `StationTasks/StationTasksModule.cs` — 注册 ScriptStepAction
  - `StationTasks/Actions/ProcessStepExecutor.cs` — switch 添加 SCRIPT 分支
  - `Module/Editor/` — 新增 ScriptDetailViewModel + ScriptDetailView
  - `Module/PrimModel.cs` — 注册 ScriptDetailViewModel
  - `Module/Editor/ProcessSequenceEditorViewModel.cs` — 新增 SCRIPT 路由

## ADDED Requirements

### Requirement: SCRIPT 步骤类型
系统 SHALL 在 `StepType` 枚举中新增 `SCRIPT` 类型，用于标识自定义 C# 脚本步骤。

#### Scenario: 枚举扩展
- **WHEN** 用户在添加步骤对话框中选择步骤类型
- **THEN** 列表中包含 "SCRIPT" 选项

### Requirement: ScriptDetail 数据模型
系统 SHALL 提供 `ScriptDetail` 数据模型，包含以下字段：
- `ScriptCode` (string) — C# 脚本代码
- `ReferencedAssemblies` (ObservableCollection<string>) — 额外引用的程序集名称
- `ReferencedNamespaces` (ObservableCollection<string>) — 额外引用的命名空间
- `Description` (string) — 脚本说明

ScriptDetail 支持 JSON 序列化/反序列化，标注 `[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]`。

#### Scenario: 创建默认 ScriptDetail
- **WHEN** 用户在步骤编辑器中选择 SCRIPT 步骤类型
- **THEN** 系统创建包含默认脚本模板的 ScriptDetail 实例

### Requirement: 脚本约定
脚本 SHALL 遵循以下约定：
- 类名必须为 `ScriptAction`
- 包含 `public static Dictionary<string, string> Execute(IDictionary<string, string> globalVariables, IDictionary<string, string> stepOutputs)` 方法
- `globalVariables` 参数：当前配方池的所有全局变量（Key=变量名, Value=变量值字符串）
- `stepOutputs` 参数：前序步骤的输出参数（Key=参数名, Value=参数值字符串，来自 BranchOutputParameter）
- 返回值：脚本执行结果，Key 为输出参数名，Value 为输出值字符串
- 默认引用命名空间：System, System.Collections.Generic, System.Linq, System.Math
- 默认引用程序集：System.Runtime, System.Linq

#### Scenario: 脚本模板
- **WHEN** 用户创建新的 SCRIPT 步骤
- **THEN** 系统提供默认脚本模板：
```csharp
using System;
using System.Collections.Generic;
using System.Linq;

public class ScriptAction
{
    public static Dictionary<string, string> Execute(
        IDictionary<string, string> globalVariables,
        IDictionary<string, string> stepOutputs)
    {
        var result = new Dictionary<string, string>();

        // 读取全局变量: globalVariables["变量名"]
        // 读取步骤输出: stepOutputs["参数名"]
        // 写入结果: result["输出名"] = "值"

        return result;
    }
}
```

### Requirement: ScriptStepAction 执行器
系统 SHALL 提供 `ScriptStepAction`，实现 `IProcessStepAction` 接口，在工艺执行到 SCRIPT 步骤时动态编译并执行脚本。

#### Scenario: 执行脚本步骤
- **WHEN** 工艺执行器执行 SCRIPT 步骤
- **THEN** 系统通过 `IRecipePoolService.LoadGlobalVariablesAsync` 加载当前配方池的全局变量
- **AND** 收集前序步骤的输出参数（从 ProcessStepExecutor 维护的输出参数字典中获取）
- **AND** 使用 Natasha 动态编译脚本代码
- **AND** 调用 `ScriptAction.Execute(globalVariables, stepOutputs)` 执行
- **AND** 将返回的 Dictionary 写入全局变量（通过 `IRecipePoolService.SaveGlobalVariablesAsync`）
- **AND** 将返回的 Dictionary 存入步骤输出参数（供后续步骤引用）

#### Scenario: 脚本编译失败
- **WHEN** 脚本代码存在语法错误
- **THEN** 系统抛出 `RecoverableException`，包含编译错误详情
- **AND** 日志记录编译错误信息

#### Scenario: 脚本运行时异常
- **WHEN** 脚本执行过程中抛出异常
- **THEN** 系统抛出 `RecoverableException`，包含异常消息
- **AND** 日志记录运行时错误信息

#### Scenario: 脚本缓存
- **WHEN** 脚本代码未发生变化
- **THEN** 系统复用上次编译结果，不重复编译

### Requirement: SCRIPT 弹出页面 UI
系统 SHALL 提供 SCRIPT 步骤编辑弹出页面，采用 MaterialDesign DialogHost 模态弹窗模式，三段式布局（标题栏 → 内容区 → 底部操作栏），与现有 DetailView 风格一致。

#### Scenario: 弹出页面布局
- **WHEN** 用户在步骤编辑器中双击 SCRIPT 步骤
- **THEN** 系统弹出 SCRIPT 编辑页面，包含：
  - 标题栏：深色渐变背景，PackIcon Kind="CodeTags"，标题文字 "SCRIPT"
  - 左侧面板（约 70%宽度）：代码编辑区
    - TextBox，Consolas 字体，AcceptsReturn=True，AcceptsTab=True
    - 行号显示（可选）
  - 右侧面板（约 30%宽度）：变量引用面板
    - 全局变量列表：显示当前配方池所有全局变量名和当前值，点击可插入到代码中
    - 步骤输出参数列表：显示前序步骤的输出参数名，点击可插入到代码中
    - 引用程序集/命名空间管理（可折叠区域）
  - 底部操作栏：
    - 编译按钮：PackIcon Kind="Build"，编译当前脚本并显示结果
    - 执行按钮：PackIcon Kind="Play"，编译并执行脚本（仅编辑器预览，不影响实际流程）
    - 取消按钮：PackIcon Kind="Close"
    - 确认按钮：PackIcon Kind="CheckCircle"，保存并关闭

#### Scenario: 编译检查
- **WHEN** 用户点击"编译"按钮
- **THEN** 系统使用 Natasha 编译当前脚本代码
- **AND** 编译成功时显示绿色提示 "编译成功"
- **AND** 编译失败时显示红色错误详情（包含行号和错误信息）

#### Scenario: 插入全局变量引用
- **WHEN** 用户在右侧面板点击某个全局变量名
- **THEN** 系统在代码编辑器光标位置插入 `globalVariables["变量名"]`

#### Scenario: 插入步骤输出参数引用
- **WHEN** 用户在右侧面板点击某个输出参数名
- **THEN** 系统在代码编辑器光标位置插入 `stepOutputs["参数名"]`

#### Scenario: 执行预览
- **WHEN** 用户点击"执行"按钮
- **THEN** 系统编译并执行脚本（使用当前全局变量值）
- **AND** 在右侧面板底部显示执行结果（输出参数名=值）
- **AND** 执行异常时显示红色错误信息

### Requirement: ProcessStepExecutor 输出参数传递
系统 SHALL 在 ProcessStepExecutor 中维护步骤输出参数字典，使 SCRIPT 步骤能读取前序步骤的输出。

#### Scenario: 输出参数收集
- **WHEN** 任意步骤执行完成且该步骤有 BranchConfig.OutputParameters
- **THEN** 系统将输出参数添加到执行上下文的输出参数字典中
- **AND** 后续 SCRIPT 步骤可通过 stepOutputs 参数读取

### Requirement: ScriptDetailViewModel
系统 SHALL 提供 `ScriptDetailViewModel`，管理脚本编辑、编译检查、变量引用插入。

#### Scenario: 初始化
- **WHEN** ViewModel 的 Step 属性被设置
- **THEN** 从 Step.ScriptDetail 加载脚本代码、引用程序集、命名空间
- **AND** 从 IRecipePoolService 加载全局变量列表
- **AND** 从当前步骤序列收集前序步骤的输出参数

#### Scenario: 保存
- **WHEN** 用户点击"确认继续"按钮
- **THEN** 将当前脚本代码、引用程序集、命名空间写入 Step.ScriptDetail
- **AND** 关闭弹窗

## MODIFIED Requirements

### Requirement: ProcessStep 模型扩展
`ProcessStep` 模型 SHALL 新增 `ScriptDetail` 属性，类型为 `ScriptDetail`，标注 `[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]`，仅在 SCRIPT 步骤时有值。

### Requirement: ProcessSequenceEditorViewModel 路由扩展
`ProcessSequenceEditorViewModel.NavigateToDetailView` SHALL 新增对 `StepType.SCRIPT` 的路由处理，弹出 `ScriptDetailView`。

### Requirement: ProcessStepExecutor switch 扩展
`ProcessStepExecutor.ExecuteSingleStepAsync` 的 switch SHALL 新增 `case StepType.SCRIPT:` 分支。

## REMOVED Requirements
无
