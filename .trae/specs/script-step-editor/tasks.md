# Tasks

- [x] Task 1: 扩展 ProcessStep 数据模型
  - [x] 1.1: 在 StepType 枚举中新增 SCRIPT
  - [x] 1.2: 新增 ScriptDetail 类（ScriptCode, ReferencedAssemblies, ReferencedNamespaces, Description）
  - [x] 1.3: 在 ProcessStep 中新增 ScriptDetail 属性

- [x] Task 2: 创建 ScriptStepAction 执行器
  - [x] 2.1: 实现 IProcessStepAction，SupportedStepType = StepType.SCRIPT
  - [x] 2.2: 基于 Natasha 动态编译脚本，遵循 ScriptAction 约定
  - [x] 2.3: 注入 IRecipePoolService 加载全局变量，注入 ILoggerService 记录日志
  - [x] 2.4: 实现脚本缓存（代码不变则复用编译结果）
  - [x] 2.5: 编译/运行时异常包装为 RecoverableException

- [x] Task 3: 注册 ScriptStepAction 到 DI 和 Executor
  - [x] 3.1: StationTasksModule.cs RegisterMany 添加 typeof(ScriptStepAction)
  - [x] 3.2: ProcessStepExecutor.cs switch 添加 case StepType.SCRIPT

- [x] Task 4: 扩展 ProcessStepExecutor 输出参数传递
  - [x] 4.1: 在 ProcessStepExecutor 中维护 _stepOutputs 字典（步骤输出参数累积）
  - [x] 4.2: 步骤执行完成后，若该步骤有 BranchConfig.OutputParameters，将输出参数添加到字典
  - [x] 4.3: 将 _stepOutputs 传递给 ScriptStepAction.ExecuteAsync

- [x] Task 5: 创建 ScriptDetailViewModel
  - [x] 5.1: 属性：Step, ScriptCode, ReferencedAssemblies, ReferencedNamespaces, Description
  - [x] 5.2: 属性：GlobalVariables（全局变量列表）, StepOutputParameters（前序步骤输出参数列表）
  - [x] 5.3: 属性：CompileResult（编译结果消息）, IsCompileSuccess（编译是否成功）
  - [x] 5.4: 属性：ExecuteResult（执行结果消息）, IsExecuting
  - [x] 5.5: 命令：InsertGlobalVariableCommand, InsertStepOutputCommand
  - [x] 5.6: 命令：CompileCommand（仅编译检查）, ExecuteCommand（编译+执行预览）
  - [x] 5.7: 命令：SaveCommand, CloseCommand
  - [x] 5.8: InitializeFromStep() 从 Step.ScriptDetail 加载，为空则生成默认模板
  - [x] 5.9: 编译逻辑使用 Natasha，编译结果格式化显示

- [x] Task 6: 创建 ScriptDetailView.xaml
  - [x] 6.1: 三段式布局（标题栏 → 内容区 → 底部操作栏）
  - [x] 6.2: 标题栏：深色渐变，PackIcon Kind="CodeTags"，标题 "SCRIPT"
  - [x] 6.3: 左侧代码编辑区：Consolas 字体 TextBox，AcceptsReturn/AcceptsTab
  - [x] 6.4: 右侧变量引用面板：全局变量列表 + 步骤输出参数列表，点击插入代码
  - [x] 6.5: 右侧引用管理区：可折叠的程序集/命名空间管理
  - [x] 6.6: 编译/执行结果展示区
  - [x] 6.7: 底部操作栏：编译 + 执行 + 取消 + 确认继续
  - [x] 6.8: 创建 ScriptDetailView.xaml.cs 代码后置

- [x] Task 7: 注册 ViewModel 和导航
  - [x] 7.1: PrimModel.cs 注册 ScriptDetailViewModel
  - [x] 7.2: ProcessSequenceEditorViewModel 添加 SCRIPT 导航分支 + ShowScriptDetailDialog 方法

- [x] Task 8: 构建验证
  - [x] 8.1: dotnet build 确认无编译错误

# Task Dependencies
- [Task 2] depends on [Task 1]
- [Task 3] depends on [Task 2]
- [Task 4] depends on [Task 2]
- [Task 5] depends on [Task 1]
- [Task 6] depends on [Task 5]
- [Task 7] depends on [Task 5, Task 6]
- [Task 8] depends on [Task 3, Task 4, Task 7]
