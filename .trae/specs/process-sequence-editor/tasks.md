# Tasks

- [x] Task 1: 扩展 SubMove 模型，新增 StationId、AxisId、OffsetVariableName 字段
  - [x] SubTask 1.1: 修改 ProcessStep.cs 中 SubMove 类，添加新属性并保持 JSON 序列化兼容
  - [x] SubTask 1.2: 验证现有序列化/反序列化不受影响

- [x] Task 2: 重构 ProcessSequenceEditorView 步骤管理功能
  - [x] SubTask 2.1: 完善 AddStep/DeleteStep/MoveUp/MoveDown 命令，确保 Seq 自动重编号
  - [x] SubTask 2.2: 实现 StepType.GOTO 选中时弹出 GotoDetailView 模态对话框
  - [x] SubTask 2.3: 修复 DataGrid 编辑绑定，确保 CompFeature/SiteFeature/Camera/Purpose 实时同步

- [x] Task 3: 重构 GotoDetailView 详细配置页面
  - [x] SubTask 3.1: 改造为 DialogHost 模态对话框
  - [x] SubTask 3.2: 实现工站-轴选择组件，从配方加载可用轴列表（格式："工站名.轴名"）
  - [x] SubTask 3.3: 实现配方轴位置选择器，根据选中工站加载该工站的所有位置点名
  - [x] SubTask 3.4: 实现速度设置控件，添加正数验证
  - [x] SubTask 3.5: 实现 Offset 配置项，支持直接输入数值和从全局变量列表选择两种模式

- [x] Task 4: 创建运行时步骤执行引擎
  - [x] SubTask 4.1: 在 StationTasks 项目中创建 IProcessStepAction 接口和 GotoStepAction 类
  - [x] SubTask 4.2: GotoStepAction 解析 SubMove 列表，按序调用 MoveToAsync（含 Offset 叠加和全局变量解析）
  - [x] SubTask 4.3: 创建 ProcessStepExecutor 类，遍历 ProcessStep 序列，通过 RunStep 包装执行每个步骤
  - [x] SubTask 4.4: 实现 CHECK 步骤的 SkipTo 跳转逻辑

- [x] Task 5: 实现 StartTaskCommand 功能逻辑
  - [x] SubTask 5.1: 在 ProcessSequenceService 中实现 StartTask，将步骤序列传递给 ProcessStepExecutor
  - [x] SubTask 5.2: 在 StationTaskBase 中添加 ExecuteProcessStepSequenceAsync 入口方法
  - [x] SubTask 5.3: 实现当前执行步骤的 IsCurrent 标记和 UI 高亮通知

- [x] Task 6: 实现步骤序列数据持久化
  - [x] SubTask 6.1: 实现 SaveToJsonCommand，将步骤序列保存到 Config/ProcessSequences/ 目录
  - [x] SubTask 6.2: 实现自动加载机制，启动时从 IAppSettingService 读取最后使用的配置路径并加载
  - [x] SubTask 6.3: 实现 LoadSequence 手动加载功能

# Task Dependencies
- [Task 2] depends on [Task 1] (SubMove 模型扩展是编辑器重构的前提)
- [Task 3] depends on [Task 1] (GotoDetailView 需要 SubMove 新字段)
- [Task 4] depends on [Task 1] (执行引擎需要 SubMove 新字段)
- [Task 5] depends on [Task 4] (StartTask 依赖执行引擎)
- [Task 6] depends on [Task 1] (持久化需要完整模型)
- [Task 2] and [Task 3] and [Task 6] can be parallelized after Task 1
