# SEEK 步骤编辑器弹出页面 Spec

## Why
SEEK 步骤类型已在 `StepType` 枚举中预留，但缺少完整的编辑界面、数据模型和执行逻辑。需要实现 SEEK 弹出页面，支持力控模拟量通道的配置、实时力值监控、全局变量绑定，以及在工艺执行时一次性读取力值并同步到全局变量。

## What Changes
- 新增 `SeekDetail` 数据模型，包含 SEEK 步骤的通道配置行集合
- 新增 `SeekChannelRow` 数据模型，表示单行通道配置（链接通道、Target、Force范围、全局变量绑定、描述）
- 新增 `SeekDetailViewModel`，管理通道行 CRUD、实时刷新、导入导出、全局变量绑定
- 新增 `SeekDetailView` 弹出页面（MaterialDesign DialogHost），包含 DataGrid 表格和操作按钮
- 在 `IMotionService` 接口中新增模拟量通道读取方法 `ReadAnalogChannelAsync`
- 在 `MotionService` 实现中封装 `IDeviceService.GetAnalogInput` 调用
- 在 `ProcessStep` 模型中新增 `SeekDetail` 属性
- 在 `ProcessSequenceEditorViewModel` 中新增 SEEK 步骤的路由处理
- 新增 `SeekStepAction`，执行 SEEK 步骤时读取力值并同步到全局变量
- 注册 `IADValueConverter` 到 DI 容器，替换硬编码转换逻辑

## Impact
- Affected specs: process-sequence-editor, refactor-step-sequence-execution
- Affected code:
  - `StationTasks/Models/ProcessStep.cs` — 新增 SeekDetail 属性
  - `MotionControl/Interfaces/IMotionService.cs` — 新增模拟量读取接口
  - `MotionControl/Services/MotionService.cs` — 实现模拟量读取
  - `Module/Editor/ProcessSequenceEditorViewModel.cs` — 新增 SEEK 路由
  - `Module/PrimModel.cs` — 注册新服务和视图
  - `Core/Abstraction/IADValueConverter.cs` — 无变更
  - `Core/Services/UniversalADValueConverter.cs` — 注册到 DI

## ADDED Requirements

### Requirement: SeekDetail 数据模型
系统 SHALL 提供 `SeekDetail` 数据模型，包含 `ObservableCollection<SeekChannelRow>` 属性，支持 JSON 序列化/反序列化。

#### Scenario: 创建默认 SeekDetail
- **WHEN** 用户在步骤编辑器中选择 SEEK 步骤类型
- **THEN** 系统创建包含一个默认空行的 SeekDetail 实例

### Requirement: SeekChannelRow 数据模型
系统 SHALL 提供 `SeekChannelRow` 数据模型，包含以下字段：
- `Sub` (int) — 子序号，自动递增
- `LinkedChannel` (int) — 链接的模拟量通道号（对应 IDeviceService 的从站通道）
- `TargetForce` (double) — 目标力值（单位 N）
- `ForceMin` (double) — 力值下限
- `ForceMax` (double) — 力值上限
- `LinkedVariableName` (string) — 绑定的全局变量名（可为空）
- `Description` (string) — 描述信息
- `CurrentForce` (double) — 当前实时力值（运行时，不序列化）

#### Scenario: 通道行创建
- **WHEN** 用户添加新的通道行
- **THEN** 系统创建默认行，Sub 自动递增，LinkedChannel 默认 0，TargetForce 默认 0.3N，ForceMin 默认 -2.0N，ForceMax 默认 +2.0N

### Requirement: SEEK 弹出页面 UI
系统 SHALL 提供 SEEK 步骤编辑弹出页面，采用 MaterialDesign DialogHost 模态弹窗模式，与现有 GotoDetail/VisionDetail 风格一致。

#### Scenario: 弹出页面布局
- **WHEN** 用户在步骤编辑器中双击 SEEK 步骤
- **THEN** 系统弹出 SEEK 编辑页面，包含：
  - 标题栏：显示"SEEK 步骤配置"
  - DataGrid 表格：列包含 Sub、链接通道、Target(N)、Force(实时)、force_min、force_max、全局变量(带链接图标)、Description
  - 工具栏按钮：添加、删除、导入、导出、刷新实时数据、停止刷新
  - 底部按钮：确认继续

#### Scenario: 实时力值刷新
- **WHEN** 用户点击"刷新实时数据"按钮
- **THEN** 系统以 100ms 间隔读取所有通道行的模拟量值，通过 IADValueConverter 转换为力值，更新 CurrentForce 列显示
- **AND** Force 列根据 force_min/force_max 范围着色（范围内绿色，超限红色）

#### Scenario: 停止刷新
- **WHEN** 用户点击"停止刷新"按钮
- **THEN** 系统停止定时读取，Force 列保留最后读取值

#### Scenario: 关闭页面自动停止刷新
- **WHEN** 用户关闭 SEEK 弹出页面（确认或取消）
- **THEN** 系统自动停止实时刷新定时器，释放资源

### Requirement: 通道行 CRUD 操作
系统 SHALL 支持通道行的添加、删除操作。

#### Scenario: 添加通道行
- **WHEN** 用户点击"添加"按钮
- **THEN** 系统在表格末尾新增一行，Sub 自动递增，其他字段取默认值

#### Scenario: 删除通道行
- **WHEN** 用户选中一行并点击"删除"按钮
- **THEN** 系统删除选中行，重新排列 Sub 序号

### Requirement: 导入导出功能
系统 SHALL 支持通道配置的导入和导出。

#### Scenario: 导出配置
- **WHEN** 用户点击"导出"按钮
- **THEN** 系统将当前通道行集合序列化为 JSON 文件，通过文件对话框选择保存路径

#### Scenario: 导入配置
- **WHEN** 用户点击"导入"按钮
- **THEN** 系统通过文件对话框选择 JSON 文件，反序列化后替换当前通道行集合，Sub 自动重排

### Requirement: 全局变量绑定
系统 SHALL 支持每行通道绑定一个全局变量，用于执行时写入力值。

#### Scenario: 选择全局变量
- **WHEN** 用户在全局变量列的下拉框中选择变量
- **THEN** 系统将该变量名绑定到当前行的 LinkedVariableName
- **AND** 全局变量列显示链接图标，表示已绑定

#### Scenario: 取消绑定
- **WHEN** 用户在全局变量列的下拉框中选择空选项
- **THEN** 系统清除当前行的 LinkedVariableName，链接图标消失

### Requirement: 模拟量读取封装到 MotionControl
系统 SHALL 在 MotionControl 层封装模拟量通道读取功能，通过 `IMotionService` 对外提供。

#### Scenario: 读取单通道模拟量
- **WHEN** 调用 `IMotionService.ReadAnalogChannelAsync(int slaveNo, int channel)` 
- **THEN** 系统通过 `IDeviceService.GetAnalogInput` 读取原始 AD 值，通过 `IADValueConverter` 转换为物理量（N）后返回

#### Scenario: 批量读取模拟量
- **WHEN** 调用 `IMotionService.ReadAnalogChannelsAsync(Dictionary<int, int> channelMap)`
- **THEN** 系统并行读取所有指定通道，返回通道号到力值的映射字典

### Requirement: SEEK 步骤执行
系统 SHALL 提供 `SeekStepAction`，在工艺执行到 SEEK 步骤时一次性读取力值并同步到全局变量。

#### Scenario: 执行 SEEK 步骤
- **WHEN** 工艺执行器执行 SEEK 步骤
- **THEN** 系统遍历 SeekDetail 的所有通道行
- **AND** 对每行通过 `IMotionService.ReadAnalogChannelAsync` 读取当前力值
- **AND** 若该行绑定了全局变量（LinkedVariableName 非空），将力值写入对应全局变量
- **AND** 判断力值是否在 force_min ~ force_max 范围内，超限则发布报警事件

#### Scenario: 力值超限处理
- **WHEN** 读取的力值超出 force_min/force_max 范围
- **THEN** 系统根据 StepAlarmConfig 配置执行相应动作（报警/停止/继续）

### Requirement: IADValueConverter 注册到 DI
系统 SHALL 将 `UniversalADValueConverter` 注册为 `IADValueConverter` 的单例实现。

#### Scenario: DI 注册
- **WHEN** 应用启动时 MotionControlModule 初始化
- **THEN** `UniversalADValueConverter` 被注册为 `IADValueConverter` 的 Singleton 实现
- **AND** `MotionService` 通过构造函数注入获取 `IADValueConverter` 实例

## MODIFIED Requirements

### Requirement: ProcessStep 模型扩展
`ProcessStep` 模型 SHALL 新增 `SeekDetail` 属性，类型为 `SeekDetail`，标注 `[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]`，仅在 SEEK 步骤时有值。

### Requirement: ProcessSequenceEditorViewModel 路由扩展
`ProcessSequenceEditorViewModel.NavigateToDetailView` SHALL 新增对 `StepType.SEEK` 的路由处理，弹出 `SeekDetailView`。

## REMOVED Requirements
无
