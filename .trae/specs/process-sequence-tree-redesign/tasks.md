# Tasks

- [x] Task 1: 新增 Method 模型与 TaskRunMode 枚举
  - [x] SubTask 1.1: 创建 `Module/Models/ProcessMethod.cs`，包含 Name、Steps、IsEnabled、IsExpanded、IsSelected 属性，继承 BindableBase
  - [x] SubTask 1.2: 在 `Module/Models/WorkOrderData.cs` 中为 TaskItem 新增 RunMode（TaskRunMode 枚举：Active/Passive）、IsEnabled、Methods 属性；保留 Steps 兼容属性聚合 Methods 的动作
  - [x] SubTask 1.3: 在 `StationTasks/Models/ProcessStep.cs` 中新增 IsEnabled 属性（默认 true，JSON 序列化兼容）
  - [x] SubTask 1.4: 在 StepType 枚举中新增 RUNTASK 值

- [x] Task 2: 重构 ProcessSequenceEditorView 为左右双栏树形布局
  - [x] SubTask 2.1: 重写 `ProcessSequenceEditorView.xaml`，采用 Grid 双栏布局：左侧 TreeView（35%宽）+ 右侧 ContentControl 详情面板（65%宽）
  - [x] SubTask 2.2: 顶部保留序列文件工具栏，底部保留执行控制栏与验证面板
  - [x] SubTask 2.3: 实现 TreeView 的 HierarchicalDataTemplate，三级层级：TaskItem → ProcessMethod → ProcessStep，每级显示对应 PackIcon 与状态徽章
  - [x] SubTask 2.4: 实现右侧详情面板 DataTemplateSelector，根据选中节点类型切换任务/方法/动作详情视图
  - [x] SubTask 2.5: 节点禁用状态可视化（灰色文字 + BlockHelper 图标）

- [x] Task 3: 实现树形节点右键上下文菜单
  - [x] SubTask 3.1: 任务节点右键菜单：新建方法、复制、粘贴、删除、重命名、禁用/启用、设为主动/被动（默认任务禁用删除项）
  - [x] SubTask 3.2: 方法节点右键菜单：新建动作、复制、粘贴、删除、重命名、禁用/启用
  - [x] SubTask 3.3: 动作节点右键菜单：复制、粘贴、删除、禁用/启用、执行选中工具（触发 RunSingleStepAsync）
  - [x] SubTask 3.4: 菜单项绑定对应 Command，使用 PlacementTarget.Tag 传递 DataContext

- [x] Task 4: 扩展 ProcessSequenceService 实现树形 CRUD 与剪贴板
  - [x] SubTask 4.1: 新增 AddMethod/DeleteMethod/RenameMethod 命令
  - [x] SubTask 4.2: 新增 CopyNode/PasteNode 命令，支持深拷贝节点并重置运行时状态
  - [x] SubTask 4.3: 新增 ToggleNodeEnabled 命令，统一处理任务/方法/动作的启用禁用
  - [x] SubTask 4.4: 新增 SetTaskRunMode 命令
  - [x] SubTask 4.5: 扩展 SelectedNode 属性，区分 SelectedTask/SelectedMethod/SelectedStep
  - [x] SubTask 4.6: 扩展序列化 DTO 为 Methods 列表格式，加载时自动迁移旧 Steps 格式

- [x] Task 5: 实现运行任务工具（调用任务动作）
  - [x] SubTask 5.1: 创建 `StationTasks/Models/RunTaskDetail.cs` 模型，包含 TargetTaskName 属性
  - [x] SubTask 5.2: 创建 `StationTasks/Actions/IRunTaskExecutor.cs` 接口（由 ProcessSequenceService 实现，避免倒置依赖）
  - [x] SubTask 5.3: 创建 `Module/Controls/StepDetails/RunTaskDetailViewModel.cs` + `RunTaskDetailView.xaml` 用于选择目标 Passive 任务
  - [x] SubTask 5.4: 在 ProcessStep 新增 RunTaskDetail 属性，在 PrimModel.cs 注册 RunTaskDetailViewModel

- [x] Task 6: 扩展 ProcessStepExecutor 支持方法层级与禁用跳过
  - [x] SubTask 6.1: 修改 ExecuteAsync 接收扁平化步骤列表（由 Service 扁平化方法层级）
  - [x] SubTask 6.2: 跳过 IsEnabled=false 的动作/方法，记录跳过日志
  - [x] SubTask 6.3: 处理 RUNTASK 步骤时调用 IRunTaskExecutor，传递调用栈用于循环检测
  - [x] SubTask 6.4: 保持 BranchConfig/CheckDetail 步骤引用与 RenumberSteps 逻辑兼容（Seq 在方法内重新编号）

- [x] Task 7: 实现同方法内动作拖拽排序
  - [x] SubTask 7.1: 在 TreeView 启用 AllowDrop，实现 DragOver/Drop 事件处理
  - [x] SubTask 7.2: 限制拖拽仅在同方法内的动作节点之间生效
  - [x] SubTask 7.3: 拖拽完成后调用 RenumberSteps 重编号并更新 BranchConfig/CheckDetail 引用
  - [x] SubTask 7.4: 提供拖拽视觉反馈（插入线指示器）
  - [x] SubTask 7.5: 在 spec 文档中记录跨方法拖拽评估结论

- [x] Task 8: 多语言资源与本地化
  - [x] SubTask 8.1: 在 `MainApp/Languages/Strings.zh-CN.xaml` 新增所有 PSE_ 键（树形节点、右键菜单、运行模式、方法相关）
  - [x] SubTask 8.2: 在 `MainApp/Languages/Strings.en-US.xaml` 同步新增对应英文翻译
  - [x] SubTask 8.3: 验证所有新增 UI 字符串使用 `{lang:Lang KEY}` 绑定，无硬编码字符串

- [x] Task 9: 验证与回归测试
  - [x] SubTask 9.1: 验证旧格式序列文件加载自动迁移为 Methods 结构
  - [x] SubTask 9.2: 验证任务/方法/动作的复制粘贴深拷贝正确，运行时状态重置
  - [x] SubTask 9.3: 验证禁用节点在执行时被正确跳过并记录日志
  - [x] SubTask 9.4: 验证 Passive 任务不可直接启动，调用任务动作正确执行
  - [x] SubTask 9.5: 验证循环调用检测触发报警
  - [x] SubTask 9.6: 验证同方法内拖拽排序后 Seq 重编号与步骤引用更新正确

# Task Dependencies
- [Task 2] depends on [Task 1]（模型扩展是布局重构的前提）
- [Task 3] depends on [Task 2]（右键菜单依赖 TreeView 节点）
- [Task 4] depends on [Task 1]（Service 命令依赖新模型）
- [Task 5] depends on [Task 1]（调用任务动作依赖 RUNTASK 枚举与 RunMode）
- [Task 6] depends on [Task 1] and [Task 5]（执行器依赖方法层级与 RunTaskStepAction）
- [Task 7] depends on [Task 2] and [Task 4]（拖拽依赖 TreeView 与 Service 命令）
- [Task 8] 可与 [Task 2] 并行（资源键可预先定义）
- [Task 9] depends on all other tasks
