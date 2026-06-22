# 流程序列编辑器树形层级重构 Spec

## Why

当前 ProcessSequenceEditorView 使用扁平的 Task 下拉框 + DataGrid 步骤列表，无法表达"任务-方法-动作"的多层级结构，缺乏复制/粘贴/禁用/启用等编辑能力，且动作流程只能线性排列，难以满足复杂工业流程的模块化复用与可视化编辑需求。需要重构为左侧树形层级 + 右侧详情视图的布局，引入"方法"层级与"运行模式"概念，并评估拖拽式树形流程编辑的可行性。

## What Changes

* **重构 ProcessSequenceEditorView 布局**：左侧 TreeView 显示"任务→方法→动作"三级树形结构，右侧显示选中节点的详情编辑面板 **BREAKING**

* **新增 Method 模型层级**：在 TaskItem 与 ProcessStep 之间引入 Method 容器，每个 Method 持有独立的动作列表

* **新增 TaskRunMode 枚举**：任务可设置为 Active（主动执行）或 Passive（被其他流程调用）

* **新增运行任务工具**：在动作列表中可插入"调用任务"动作，运行时调用指定 Passive 任务的 Method

* **新增树形节点右键菜单**：复制/粘贴/删除/重命名/禁用/启用，按节点类型（任务/方法/动作）差异化菜单

* **新增动作节点右键菜单**：复制/粘贴/禁用/启用/执行选中工具

* **扩展 ProcessStep 模型**：新增 IsEnabled、IsDisabled 属性支持禁用语义

* **扩展 TaskItem 模型**：新增 RunMode、IsEnabled、Methods 列表

* **扩展 ProcessSequenceService**：新增树形 CRUD 命令、剪贴板、禁用/启用、调用任务动作执行

* **扩展 ProcessStepExecutor**：执行时跳过 IsEnabled=false 的步骤；支持调用 Passive 任务

* **评估拖拽式树形流程编辑**：在动作层级引入拖拽排序（同 Method 内重排），跨 Method 拖拽作为后续扩展评估项

* **多语言支持**：所有新增 UI 字符串在 zh-CN / en-US 资源文件中同步添加 PSE\_ 前缀键

* 可拓展多任务同时执行，也可每次执行一个任务。

## Impact

* Affected specs: process-sequence-editor（已完成的基础编辑器）、refactor-step-sequence-execution（执行引擎）

* Affected code:

  * `Module/Controls/StepEditor/ProcessSequenceEditorView.xaml` + `ProcessSequenceEditorViewModel.cs`（布局重构 + 树形绑定）

  * `Module/Models/WorkOrderData.cs`（TaskItem 扩展 RunMode/IsEnabled/Methods）

  * `StationTasks/Models/ProcessStep.cs`（新增 IsEnabled 属性）

  * `Module/Models/ProcessMethod.cs`（**新增** Method 模型）

  * `Module/Models/RunTaskAction.cs`（**新增** 调用任务动作模型）

  * `Module/Services/IProcessSequenceService.cs` + `ProcessSequenceService.cs`（树形 CRUD、剪贴板、禁用/启用、调用任务执行）

  * `StationTasks/Actions/ProcessStepExecutor.cs`（跳过禁用步骤、调用 Passive 任务）

  * `StationTasks/Actions/RunTaskStepAction.cs`（**新增** 调用任务动作执行器）

  * `MainApp/Languages/Strings.zh-CN.xaml` + `Strings.en-US.xaml`（新增 PSE\_ 键）

## ADDED Requirements

### Requirement: 树形层级任务列表布局

系统 SHALL 将 ProcessSequenceEditorView 重构为左侧 TreeView + 右侧详情面板的双栏布局，左侧树形展示"任务→方法→动作"三级层级结构。

#### Scenario: 树形结构展示

* **WHEN** 用户打开 ProcessSequenceEditorView

* **THEN** 左侧 TreeView 显示所有任务作为根节点

* **AND** 每个任务节点下展开显示其包含的方法列表

* **AND** 每个方法节点下展开显示其包含的动作列表

* **AND** 右侧详情面板显示当前选中节点的编辑界面

#### Scenario: 选中节点切换详情

* **WHEN** 用户在树中选中任务节点

* **THEN** 右侧显示任务属性编辑面板（名称、运行模式、启用状态）

* **WHEN** 用户在树中选中方法节点

* **THEN** 右侧显示方法属性编辑面板（名称、启用状态）

* **WHEN** 用户在树中选中动作节点

* **THEN** 右侧显示该动作的详情编辑对话框入口（沿用现有 \*DetailView）

#### Scenario: 节点图标与状态可视化

* **WHEN** 树节点渲染时

* **THEN** 任务节点显示 Folder 图标，Passive 任务额外显示 CallMade 徽章

* **AND** 方法节点显示 FunctionVariant 图标

* **AND** 动作节点显示对应 StepType 的图标

* **AND** 禁用节点显示灰色文字与 BlockHelper 图标

### Requirement: Method 方法层级模型

系统 SHALL 在 TaskItem 与 ProcessStep 之间引入 Method 容器，每个 Task 包含多个 Method，每个 Method 持有独立的动作列表。

#### Scenario: 创建方法

* **WHEN** 用户在任务节点上右键选择"新建方法"

* **THEN** 在该任务下新增一个 Method，默认名称为"方法N"

* **AND** 新方法自动选中并在右侧显示编辑面板

#### Scenario: 方法持有独立动作列表

* **WHEN** 用户在方法节点下新增动作

* **THEN** 动作仅添加到该方法的 Steps 集合中

* **AND** 其他方法的动作列表不受影响

#### Scenario: 方法执行顺序

* **WHEN** 任务以 Active 模式执行

* **THEN** 按方法在任务中的顺序依次执行每个启用方法

* **AND** 每个方法内按动作顺序执行其启用动作

### Requirement: 任务运行模式

系统 SHALL 为每个 TaskItem 提供运行模式设置：Active（主动执行）或 Passive（被其他流程调用）。

#### Scenario: 设置运行模式

* **WHEN** 用户在任务属性面板切换运行模式

* **THEN** TaskItem.RunMode 更新为 Active 或 Passive

* **AND** 树节点图标立即反映模式变化

#### Scenario: 主动执行任务

* **WHEN** 用户点击 Start 按钮且当前任务为 Active 模式

* **THEN** 系统按方法顺序执行该任务的所有启用方法

* **AND** 执行状态在 UI 中实时显示

#### Scenario: 被动任务不可直接启动

* **WHEN** 用户选中 Passive 模式任务并点击 Start

* **THEN** 系统提示"被动任务不可直接执行，请通过调用任务动作触发"

* **AND** 不启动执行

### Requirement: 运行任务工具（调用任务动作）

系统 SHALL 提供一种"调用任务"动作类型，允许在动作列表中插入对 Passive 任务的调用。

#### Scenario: 插入调用任务动作

* **WHEN** 用户在方法内点击"添加动作"并选择"调用任务"类型

* **THEN** 新增一个 RunTaskAction 步骤，StepType 为 RUNTASK

* **AND** 双击该动作弹出选择目标 Passive 任务的对话框

#### Scenario: 执行调用任务动作

* **WHEN** ProcessStepExecutor 遇到 StepType.RUNTASK 步骤

* **THEN** 解析目标任务名称，从 IProcessSequenceService 查找对应 Passive 任务

* **AND** 在当前 StationTaskBase 上下文中按方法顺序执行目标任务的所有启用方法

* **AND** 目标任务执行完成后返回原任务继续执行后续动作

#### Scenario: 防止循环调用

* **WHEN** 调用任务动作形成循环引用（A 调用 B，B 调用 A）

* **THEN** 系统检测到循环并触发 AlarmService 报警

* **AND** 终止执行并显示循环调用链

### Requirement: 树形节点右键菜单

系统 SHALL 为树形节点提供差异化的右键上下文菜单，支持复制/粘贴/删除/重命名/禁用/启用等操作。

#### Scenario: 任务节点右键菜单

* **WHEN** 用户右键点击任务节点

* **THEN** 菜单显示：新建方法、复制、粘贴、删除、重命名、禁用/启用、设为主动/被动

* **AND** 默认任务的"删除"项禁用

#### Scenario: 方法节点右键菜单

* **WHEN** 用户右键点击方法节点

* **THEN** 菜单显示：新建动作、复制、粘贴、删除、重命名、禁用/启用

#### Scenario: 动作节点右键菜单

* **WHEN** 用户右键点击动作节点

* **THEN** 菜单显示：复制、粘贴、删除、禁用/启用、执行选中工具

* **AND** "执行选中工具"触发 RunSingleStepAsync 单步执行

#### Scenario: 复制粘贴节点

* **WHEN** 用户复制一个节点后粘贴

* **THEN** 在目标位置创建深拷贝副本

* **AND** 副本名称追加"\_Copy"后缀

* **AND** 副本的 IsCurrent、IsSingleExecuting、HasActiveAlarm 等运行时状态重置

#### Scenario: 禁用启用节点

* **WHEN** 用户对节点选择"禁用"

* **THEN** 节点 IsEnabled 设为 false

* **AND** 节点在树中显示灰色与禁用图标

* **AND** 执行时跳过该节点（任务/方法/动作均适用）

### Requirement: 动作流程拖拽排序

系统 SHALL 支持在同一 Method 内通过拖拽对动作节点重新排序，并评估跨 Method 拖拽的可行性。

#### Scenario: 同方法内拖拽排序

* **WHEN** 用户在树中拖拽一个动作节点到同方法内的另一位置

* **THEN** 动作在该方法的 Steps 集合中移动到新位置

* **AND** Seq 自动重新编号

* **AND** BranchConfig/CheckDetail 中的步骤引用按 RenumberSteps 逻辑更新

#### Scenario: 跨方法拖拽评估

* **WHEN** 评估跨 Method 拖拽动作的可行性

* **THEN** 第一阶段仅支持同方法内拖拽

* **AND** 跨方法拖拽作为后续扩展项，需评估步骤引用跨方法重写的复杂性

* **AND** 在 spec 文档中记录评估结论：跨方法拖拽可行但需引入方法作用域的步骤引用解析

### Requirement: 禁用步骤执行跳过

系统 SHALL 在执行序列时跳过 IsEnabled=false 的任务、方法或动作。

#### Scenario: 跳过禁用动作

* **WHEN** ProcessStepExecutor 遍历到 IsEnabled=false 的动作

* **THEN** 跳过该动作不执行

* **AND** 在执行日志中记录"跳过禁用步骤: \[Seq] Step"

#### Scenario: 跳过禁用方法

* **WHEN** 任务执行遍历到 IsEnabled=false 的方法

* **THEN** 跳过该方法的所有动作

* **AND** 在执行日志中记录"跳过禁用方法: 方法名"

#### Scenario: 跳过禁用任务

* **WHEN** 调用任务动作目标为 IsEnabled=false 的任务

* **THEN** 跳过该任务的执行

* **AND** 在执行日志中记录"跳过禁用任务: 任务名"

## MODIFIED Requirements

### Requirement: ProcessSequenceEditorView 布局重构

原布局为顶部工具栏 + DataGrid 单栏结构。修改为左侧 TreeView（占 35% 宽度）+ 右侧详情面板（占 65% 宽度）的双栏布局。顶部保留序列文件加载/保存工具栏，底部保留执行控制栏与验证面板。

### Requirement: TaskItem 模型扩展

原 TaskItem 仅包含 Name、Steps、Status、IsDefault。扩展为：

```csharp
public class TaskItem : BindableBase
{
    public string Name { get; set; }
    public ObservableCollection<ProcessMethod> Methods { get; set; } // 新增：替代 Steps
    public TaskStatusEnum Status { get; set; }
    public bool IsDefault { get; set; }
    public TaskRunMode RunMode { get; set; } // 新增：Active/Passive
    public bool IsEnabled { get; set; } = true; // 新增
    // 兼容：Steps 属性聚合所有方法的动作（仅用于旧序列化兼容）
    public ObservableCollection<ProcessStep> Steps { get; } 
}
```

### Requirement: ProcessStep 模型扩展

原 ProcessStep 无启用状态。新增：

```csharp
public bool IsEnabled { get; set; } = true;
```

### Requirement: ProcessStepExecutor 执行逻辑

原执行器按索引线性遍历 Steps。修改为：

1. 接收 Method 列表而非扁平 Steps
2. 遍历每个启用 Method，再遍历其启用 Steps
3. 遇到 RUNTASK 步骤时递归调用目标 Passive 任务
4. 维护调用栈检测循环引用

### Requirement: 序列化持久化格式

原 SequenceTaskData 包含 Name/IsDefault/Status/Steps。修改为包含 Methods 列表。加载旧格式时自动迁移：将 Steps 包装为单个默认方法。

## REMOVED Requirements

无移除项。现有功能通过重构保留。

## 跨方法拖拽评估结论

### 评估背景
任务→方法→动作三级树形结构中，动作节点当前仅支持同方法内拖拽排序。跨方法拖拽（将动作从方法A移动到方法B）需要解决以下复杂性：

### 技术挑战
1. **步骤引用跨方法重写**：BranchConfig.Conditions[].TargetStepSeq 和 CheckDetail.OnPassJumpStepSeq/OnFailJumpStepSeq 使用 Seq 号引用步骤。跨方法移动后，源方法和目标方法的 Seq 需要分别重新编号，但跨方法的 Seq 引用会失效（Seq 仅在方法内唯一）。
2. **步骤输出参数作用域**：_stepOutputs 字典中的键格式为 "步骤{N}_{Step}结果"，跨方法后 Seq 冲突会导致输出参数覆盖。
3. **执行流扁平化顺序**：执行时方法按顺序扁平化，跨方法移动改变了执行顺序，可能影响依赖前序步骤输出的逻辑。

### 评估结论
- **可行性**：跨方法拖拽技术可行，但需要引入"方法作用域的步骤引用"机制（如 "方法名.步骤N" 格式替代纯 Seq 引用）
- **建议**：第一阶段仅支持同方法内拖拽（已实现）；跨方法拖拽作为后续扩展项，需同步重构 BranchConfig/CheckDetail 的引用解析逻辑
- **替代方案**：用户可通过"复制+粘贴+删除"组合操作实现跨方法移动，虽不如拖拽便捷但功能等价
