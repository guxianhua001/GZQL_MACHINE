# Tasks

- [x] Task 1: 创建 DispenseDetail 和 DispenseSegmentRef 数据模型
  - [x] SubTask 1.1: 在 Core/Models/ 下创建 DispenseStepMode 枚举（Dot=0, Arc=1）
  - [x] SubTask 1.2: 在 Core/Models/ 下创建 DispenseSegmentRef 类（轻量引用模型，含 SourceSegmentId、覆盖参数、只读显示属性）
  - [x] SubTask 1.3: 在 Core/Models/ 下创建 DispenseDetail 类（含 DispenseMode、EnableZCalibration、SegmentRefs、全局默认工艺参数、执行控制参数）
  - [x] SubTask 1.4: 在 ProcessStep.cs 中添加 DispenseDetail 属性及序列化支持

- [x] Task 2: 创建 DispenseDetailView 和 DispenseDetailViewModel
  - [x] SubTask 2.1: 创建 DispenseDetailViewModel（注入 IContainerProvider、IRecipePoolService、IStationRegistry 等，实现导入线段/圆弧、参数配置、确定/取消逻辑）
  - [x] SubTask 2.2: 创建 DispenseDetailView.xaml（点胶模式选择区、线段导入区DataGrid、全局默认参数区、选中段覆盖参数区、执行控制区、确定/取消按钮）
  - [x] SubTask 2.3: 创建 DispenseDetailView.xaml.cs（极简 code-behind，遵循现有 StepDetails 模式）
  - [x] SubTask 2.4: 实现导入线段对话框逻辑（从 DispenserStationParams.Segments 筛选 Line 类型，排除已导入段）
  - [x] SubTask 2.5: 实现导入圆弧对话框逻辑（从 DispenserStationParams.Segments 筛选 Arc/Circle 类型，排除已导入段）
  - [x] SubTask 2.6: 实现逐段参数覆盖编辑（UseDefaultParams 切换、覆盖参数展开/折叠）

- [x] Task 3: 创建 DispenseStepAction 执行器
  - [x] SubTask 3.1: 创建 DispenseStepAction 类（实现 IProcessStepAction，SupportedStepType = StepType.DISPENSE）
  - [x] SubTask 3.2: 实现 Dot 模式执行逻辑（遍历 SegmentRefs → 查找源段 → 执行逐点点胶）
  - [x] SubTask 3.3: 实现 Arc 模式执行逻辑（遍历 Arc 类型 SegmentRefs → 查找源段 → 执行连续插补走胶）
  - [x] SubTask 3.4: 实现 Z向校准逻辑（EnableZCalibration 时先校准再点胶）
  - [x] SubTask 3.5: 实现安全逻辑（源段缺失跳过+日志警告、急停安全关胶、执行前空跑）
  - [x] SubTask 3.6: 在 StationTasksModule.cs 中注册 DispenseStepAction

- [x] Task 4: 集成步骤编辑器路由
  - [x] SubTask 4.1: 在 ProcessSequenceEditorViewModel.NavigateToDetailView 中添加 StepType.DISPENSE 分支
  - [x] SubTask 4.2: 新增 ShowDispenseDetailDialog 方法（创建 ViewModel → 绑定 Step → DialogHost 弹窗）
  - [x] SubTask 4.3: 在 ProcessStepExecutor.ExecuteSingleStepAsync 中添加 StepType.DISPENSE 分支
  - [x] SubTask 4.4: AddEditStepDialogViewModel 已使用 Enum.GetValues 显示所有 StepType（DISPENSE 已自动包含）

- [x] Task 5: 多语言资源添加
  - [x] SubTask 5.1: 在 Strings.zh-CN.xaml 中添加 DispenseDetail_ 前缀的所有 Lang Key（48条）
  - [x] SubTask 5.2: 在 Strings.en-US.xaml 中添加对应的英文翻译（48条）

# Task Dependencies
- [Task 2] depends on [Task 1] — ViewModel 和 View 依赖数据模型
- [Task 3] depends on [Task 1] — 执行器依赖 DispenseDetail 模型
- [Task 4] depends on [Task 2, Task 3] — 路由集成依赖 View 和 Action 都已就绪
- [Task 5] 可与 [Task 2] 并行 — 多语言资源独立于代码逻辑
