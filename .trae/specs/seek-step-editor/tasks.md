# Tasks

- [x] Task 1: 新增 SeekDetail 和 SeekChannelRow 数据模型
  - [ ] SubTask 1.1: 在 `StationTasks/Models/ProcessStep.cs` 中新增 `SeekChannelRow` 类（Sub, LinkedChannel, TargetForce, ForceMin, ForceMax, LinkedVariableName, Description, CurrentForce[JsonIgnore]）
  - [ ] SubTask 1.2: 在 `StationTasks/Models/ProcessStep.cs` 中新增 `SeekDetail` 类（ObservableCollection<SeekChannelRow> ChannelRows）
  - [ ] SubTask 1.3: 在 `ProcessStep` 类中新增 `SeekDetail` 属性，标注 `[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]`

- [x] Task 2: 在 MotionControl 层封装模拟量通道读取
  - [ ] SubTask 2.1: 在 `IMotionService` 接口中新增 `ReadAnalogChannelAsync(int slaveNo, int channel)` 方法，返回 double（物理量 N）
  - [ ] SubTask 2.2: 在 `IMotionService` 接口中新增 `ReadAnalogChannelsAsync(Dictionary<int, int> channelMap)` 批量读取方法
  - [ ] SubTask 2.3: 在 `MotionService` 中注入 `IDeviceService` 和 `IADValueConverter`，实现上述两个方法
  - [ ] SubTask 2.4: 在 `MotionControlModule.RegisterTypes` 中注册 `IADValueConverter` → `UniversalADValueConverter`（Singleton）
  - [ ] SubTask 2.5: 在 `MotionControlModule.RegisterTypes` 中注册 `IDeviceService` → `LctDeviceService`（从 PrimModel 迁移，确保单例唯一性）

- [x] Task 3: 新增 SeekDetailViewModel
  - [ ] SubTask 3.1: 创建 `Module/Editor/SeekDetailViewModel.cs`，继承 `BindableBase`
  - [ ] SubTask 3.2: 实现 `Step` 属性和 `InitializeFromStep()` 方法，从 ProcessStep.SeekDetail 加载通道行
  - [ ] SubTask 3.3: 实现通道行 CRUD 命令：`OnAddChannelRow`、`OnDeleteChannelRow`、Sub 自动重排
  - [ ] SubTask 3.4: 实现导入导出命令：`OnImportAsync`、`OnExportAsync`（JSON 文件序列化）
  - [ ] SubTask 3.5: 实现实时刷新逻辑：`OnStartRefresh`、`OnStopRefresh`，使用 DispatcherTimer 100ms 间隔调用 `IMotionService.ReadAnalogChannelAsync`
  - [ ] SubTask 3.6: 实现全局变量绑定：加载全局变量列表，构建下拉选项，绑定到 SeekChannelRow.LinkedVariableName
  - [ ] SubTask 3.7: 实现 `OnSave` 命令，将通道行回写到 ProcessStep.SeekDetail
  - [ ] SubTask 3.8: 实现窗口关闭时自动停止刷新（IDisposable 或 DialogClosing 事件）

- [x] Task 4: 新增 SeekDetailView 弹出页面
  - [ ] SubTask 4.1: 创建 `Module/Editor/SeekDetailView.xaml`，MaterialDesign 风格 DialogHost 弹窗
  - [ ] SubTask 4.2: 实现 DataGrid 表格布局：Sub、链接通道(ComboBox)、Target(N)、Force(实时,只读,颜色指示)、force_min、force_max、全局变量(ComboBox+链接图标)、Description
  - [ ] SubTask 4.3: 实现工具栏：添加、删除、导入、导出、刷新实时数据、停止刷新 按钮
  - [ ] SubTask 4.4: 实现底部"确认继续"按钮，绑定 OnSave 命令
  - [ ] SubTask 4.5: 创建 `SeekDetailView.xaml.cs` 代码后置（仅 InitializeComponent）
  - [ ] SubTask 4.6: 实现 Force 列颜色转换器（范围内绿色，超限红色），参考 ForceValueToColorConverter

- [x] Task 5: 在步骤编辑器中集成 SEEK 路由
  - [ ] SubTask 5.1: 在 `ProcessSequenceEditorViewModel.NavigateToDetailView` 中新增 `StepType.SEEK` → `ShowSeekDetailDialog` 路由
  - [ ] SubTask 5.2: 实现 `ShowSeekDetailDialog` 方法，遵循现有 DialogHost 弹窗模式
  - [ ] SubTask 5.3: 在 `PrimModel.RegisterTypes` 中注册 `SeekDetailView`/`SeekDetailViewModel` 导航

- [x] Task 6: 新增 SeekStepAction 执行逻辑
  - [ ] SubTask 6.1: 创建 `StationTasks/Actions/SeekStepAction.cs`，实现 `IProcessStepAction`
  - [ ] SubTask 6.2: 实现执行逻辑：遍历 SeekDetail.ChannelRows，调用 `IMotionService.ReadAnalogChannelAsync` 读取力值
  - [ ] SubTask 6.3: 实现全局变量同步：通过 `IRecipePoolService` 查找全局变量并写入力值
  - [ ] SubTask 6.4: 实现力值超限判断：根据 force_min/force_max 判定，超限时发布报警事件
  - [ ] SubTask 6.5: 在 `ProcessStepExecutor` 中注册 SeekStepAction

# Task Dependencies
- [Task 2] depends on [Task 1] — MotionService 读取方法需要 SeekChannelRow 模型定义
- [Task 3] depends on [Task 1] — ViewModel 依赖数据模型
- [Task 3] depends on [Task 2] — ViewModel 实时刷新依赖 MotionService 读取方法
- [Task 4] depends on [Task 3] — View 绑定 ViewModel
- [Task 5] depends on [Task 3, Task 4] — 路由集成依赖 VM 和 View
- [Task 6] depends on [Task 1, Task 2] — 执行逻辑依赖模型和读取方法
