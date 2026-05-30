# Dispense 步骤编辑器集成 Spec

## Why

当前 `StepType.DISPENSE` 在枚举中已定义但完全未实现：无 `DispenseDetail` 数据模型、无 `DispenseStepAction` 执行器、无 `DispenseDetailView/ViewModel` 编辑器。用户无法在自定义步骤序列中创建和编辑点胶步骤，也无法从 DXF 文件导入线段/圆弧数据到点胶步骤中执行。需要在自定义步骤编辑器中集成 Dispense 工具，实现从 DXF 解析到点胶工艺执行的全流程操作。

## What Changes

- **新增 DispenseDetail 数据模型**：在 ProcessStep.cs 中添加 DispenseDetail 属性，支持点/圆弧两种点胶模式、Z向校准开关、线段引用列表、全局工艺参数
- **新增 DispenseDetailView/DispenseDetailViewModel**：点胶步骤详情编辑弹窗，支持导入线段/圆弧数据、逐段配置工艺参数、预览与执行
- **新增 DispenseStepAction**：IProcessStepAction 实现，运行时执行点胶步骤
- **扩展 ProcessSequenceEditorViewModel**：在 NavigateToDetailView 中添加 DISPENSE 路由分支
- **扩展 ProcessStepExecutor**：添加 StepType.DISPENSE 执行分支
- **扩展 AddEditStepDialogViewModel**：在步骤类型选择中启用 DISPENSE
- **关键约束**：Dispense 工具中线段和圆弧只导入不创建，确保数据源唯一性（数据来源于 CadPointEditor 的 DXF 解析/ROI 工具生成）

## Impact

- Affected specs: process-sequence-editor（步骤编辑器路由扩展）, refine-dot-point-editor（复用 DotProcessParams 模型）, vision-capture-dispense（复用点胶执行服务）
- Affected code:
  - `StationTasks/Models/ProcessStep.cs` — 新增 DispenseDetail 属性
  - `StationTasks/Actions/` — 新增 DispenseStepAction
  - `StationTasks/StationTasksModule.cs` — 注册 DispenseStepAction
  - `Module/Controls/StepDetails/` — 新增 DispenseDetailView/DispenseDetailViewModel
  - `Module/Controls/StepEditor/ProcessSequenceEditorViewModel.cs` — 添加 DISPENSE 路由
  - `Module/Controls/StepEditor/AddEditStepDialogViewModel.cs` — 启用 DISPENSE 类型
  - `StationTasks/Execution/ProcessStepExecutor.cs` — 添加 DISPENSE 执行分支
  - `Core/Models/DispenseDetail.cs` — 新增点胶步骤详情模型
  - `Core/Models/DispenseSegmentRef.cs` — 新增线段引用模型（轻量引用，非完整副本）
  - 多语言资源文件 — 新增 DispenseDetail 相关 UI 文本

## ADDED Requirements

### REQ-DISPENSE-DETAIL-MODEL: DispenseDetail 数据模型

系统 SHALL 在 Core 层提供 `DispenseDetail` 数据模型，作为 ProcessStep 的 DispenseDetail 属性值：

```
DispenseDetail : BindableBase
  // === 点胶模式 ===
  - DispenseMode: DispenseStepMode          // 枚举: Dot(点), Arc(圆弧)
  - EnableZCalibration: bool                // 是否启用Z向校准，默认 false
  - ZCalibrationHeight: double              // Z向校准高度 mm，默认 0.0
  - ZCalibrationSpeed: double               // Z向校准速度 mm/s，默认 5.0

  // === 线段引用（只导入不创建） ===
  - SegmentRefs: ObservableCollection<DispenseSegmentRef>  // 引用的线段/圆弧列表

  // === 全局工艺参数（未单独配置的段使用此默认值） ===
  - DefaultMoveSpeed: double                // 默认运动速度 mm/s，默认 10.0
  - DefaultSafeHeight: double               // 默认安全高度 mm，默认 5.0
  - DefaultApproachHeight: double           // 默认接近高度 mm，默认 3.0
  - DefaultDispenseAmount: double           // 默认出胶量，默认 1.0
  - DefaultPreDelay: double                 // 默认开胶前延时 ms，默认 0.0
  - DefaultPostDelay: double                // 默认关胶后延时 ms，默认 50.0
  - DefaultDispensingPressure: double       // 默认点胶气压 MPa，默认 0.30
  - DefaultSuckBackTime: double             // 默认回吸时间 ms，默认 100.0
  - DefaultGlueTriggerOffsetMm: double      // 默认开胶触发距离 mm，默认 0.5
  - DefaultCornerDecel: double              // 默认拐角减速系数，默认 0.3
  - DefaultTeachHeight: double              // 默认示教高度 mm，默认 0.0
  - DefaultHeightCompensation: double       // 默认高度补偿 mm，默认 0.0

  // === 执行控制 ===
  - StandbyHeight: double                   // 待机高度 mm，默认 10.0
  - ExecuteDryRunFirst: bool                // 执行前先空跑，默认 true
```

```
DispenseStepMode 枚举:
  Dot = 0     // 单点点胶
  Arc = 1     // 圆弧点胶
```

```
DispenseSegmentRef : BindableBase  // 线段引用（轻量级，指向实际数据源）
  - SourceSegmentId: string               // 来源段ID（如 "LINE_001", "ARC_003"）
  - SourceEntityType: CadEntityType        // 来源图元类型（Line/Arc/Circle等）
  - IsEnabled: bool                       // 是否参与走胶，默认 true
  - IsSelected: bool                      // 用户选中标记，默认 false
  - UseDefaultParams: bool                // 使用全局默认参数，默认 true

  // === 逐段覆盖参数（仅当 UseDefaultParams=false 时生效） ===
  - OverrideMoveSpeed: double              // 覆盖运动速度
  - OverrideSafeHeight: double             // 覆盖安全高度
  - OverrideApproachHeight: double         // 覆盖接近高度
  - OverrideDispenseAmount: double         // 覆盖出胶量
  - OverridePreDelay: double               // 覆盖开胶前延时
  - OverridePostDelay: double              // 覆盖关胶后延时
  - OverrideDispensingPressure: double     // 覆盖点胶气压
  - OverrideSuckBackTime: double           // 覆盖回吸时间
  - OverrideGlueTriggerOffsetMm: double    // 覆盖开胶触发距离
  - OverrideCornerDecel: double            // 覆盖拐角减速系数
  - OverrideTeachHeight: double            // 覆盖示教高度
  - OverrideHeightCompensation: double     // 覆盖高度补偿

  // === 只读显示属性（从源段实时读取） ===
  - SourceLayerName: string                // 来源图层名
  - SourceLength: double                   // 来源段长度
  - SourcePointCount: int                  // 来源段采样点数
```

#### Scenario: 创建 DispenseDetail
- **WHEN** 用户在步骤序列编辑器中添加 DISPENSE 类型步骤
- **THEN** ProcessStep.DispenseDetail 自动初始化为默认值
- **AND** DispenseMode 默认为 Dot
- **AND** SegmentRefs 为空集合
- **AND** 所有默认工艺参数使用合理默认值

#### Scenario: 线段引用只导入不创建
- **WHEN** 用户在 DispenseDetail 编辑器中导入线段数据
- **THEN** 系统从当前配方的 DispenserStationParams.Segments 中读取已有线段
- **AND** 创建 DispenseSegmentRef 引用指向源段，不创建新线段
- **AND** 引用的 SourceSegmentId 与源段 SegmentId 一一对应
- **AND** 如果源段被删除，引用标记为无效（SourceLayerName 显示 "⚠ 来源已删除"）

### REQ-DISPENSE-DETAIL-VIEW: DispenseDetailView 编辑弹窗

系统 SHALL 提供 DispenseDetailView / DispenseDetailViewModel 作为点胶步骤详情编辑弹窗，遵循现有 StepDetails 目录下的编辑器模式。

**布局结构**：

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  💧 点胶步骤配置                                                    [× 关闭] │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─ 点胶模式 ────────────────────────────────────────────────────────────┐  │
│  │  ○ 单点点胶(Dot)    ○ 圆弧点胶(Arc)    ☑ 启用Z向校准               │  │
│  │  Z校准高度: [0.0] mm    Z校准速度: [5.0] mm/s                       │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  ┌─ 线段数据导入 ───────────────────────────────────────────────────────┐  │
│  │  [📥 导入线段]  [📥 导入圆弧]  [🗑 移除选中]  [全选] [反选]         │  │
│  │                                                                      │  │
│  │  ┌────────────────────────────────────────────────────────────────┐  │  │
│  │  │ ☑│ ID       │ 类型 │ 图层  │ 长度  │ 点数 │ 启用 │ 使用默认 │  │  │
│  │  ├───┼──────────┼──────┼───────┼───────┼──────┼──────┼──────────┤  │  │
│  │  │ ☑│ LINE_001 │ Line │ 0     │25.300 │ 26   │ ☑    │ ☑        │  │  │
│  │  │ ☑│ ARC_003  │ Arc  │ 0     │15.780 │ 16   │ ☑    │ ☐        │  │  │
│  │  └────────────────────────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  ┌─ 工艺参数配置 ───────────────────────────────────────────────────────┐  │
│  │  ┌─ 全局默认参数 ─────────────────────────────────────────────────┐  │  │
│  │  │  速度: [10.0] mm/s  安全高度: [5.0] mm  接近高度: [3.0] mm   │  │  │
│  │  │  出胶量: [1.0]      开胶前延时: [0] ms   关胶后延时: [50] ms  │  │  │
│  │  │  气压: [0.30] MPa   回吸时间: [100] ms   开胶距离: [0.5] mm  │  │  │
│  │  │  减速系数: [0.3]    示教高度: [0.0] mm   高度补偿: [0.0] mm  │  │  │
│  │  └────────────────────────────────────────────────────────────────┘  │  │
│  │                                                                      │  │
│  │  ┌─ 选中段覆盖参数（当"使用默认"未勾选时显示）──────────────────┐  │  │
│  │  │  段: ARC_003                                                     │  │  │
│  │  │  速度: [8.0] mm/s  安全高度: [5.0] mm  ...                     │  │  │
│  │  └────────────────────────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  ┌─ 执行控制 ───────────────────────────────────────────────────────────┐  │
│  │  待机高度: [10.0] mm    ☑ 执行前先空跑                             │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│                                    [取消]  [确定]                           │
└─────────────────────────────────────────────────────────────────────────────┘
```

#### Scenario: 打开 DispenseDetail 编辑弹窗
- **WHEN** 用户双击 ProcessSequenceEditor 中的 DISPENSE 步骤
- **THEN** 系统通过 DialogHost 弹出 DispenseDetailView
- **AND** ViewModel 绑定到该步骤的 DispenseDetail 数据
- **AND** 如果 DispenseDetail 为 null，自动创建默认实例

#### Scenario: 导入线段数据
- **WHEN** 用户点击"导入线段"按钮
- **THEN** 系统从当前配方的 DispenserStationParams.Segments 中筛选 EntityType 为 Line 类型的段
- **AND** 显示可选线段列表（排除已导入的段）
- **AND** 用户选择后，为每个选中段创建 DispenseSegmentRef 引用
- **AND** 引用的只读属性（SourceLayerName/SourceLength/SourcePointCount）从源段实时读取

#### Scenario: 导入圆弧数据
- **WHEN** 用户点击"导入圆弧"按钮
- **THEN** 系统从当前配方的 DispenserStationParams.Segments 中筛选 EntityType 为 Arc/Circle 类型的段
- **AND** 显示可选圆弧列表（排除已导入的段）
- **AND** 用户选择后，为每个选中段创建 DispenseSegmentRef 引用

#### Scenario: 逐段参数覆盖
- **WHEN** 用户取消某段的"使用默认"勾选
- **THEN** 该段行下方展开覆盖参数编辑区
- **AND** 覆盖参数初始值复制自全局默认参数
- **AND** 修改覆盖参数不影响全局默认值和其他段

#### Scenario: Z向校准配置
- **WHEN** 用户勾选"启用Z向校准"
- **THEN** 显示Z校准高度和Z校准速度输入框
- **AND** 执行点胶前，系统先移动Z轴到校准高度进行高度确认

#### Scenario: 确定保存
- **WHEN** 用户点击"确定"按钮
- **THEN** 系统将编辑后的 DispenseDetail 数据写回 ProcessStep.DispenseDetail
- **AND** 自动保存步骤序列

### REQ-DISPENSE-STEP-ACTION: DispenseStepAction 执行器

系统 SHALL 提供 `DispenseStepAction : IProcessStepAction`，在运行时执行点胶步骤。

```
DispenseStepAction : IProcessStepAction
  - SupportedStepType → StepType.DISPENSE
  - ExecuteAsync(ProcessStep step, StationTaskBase task, CancellationToken token)
```

#### Scenario: 执行点胶步骤（Dot 模式）
- **WHEN** DispenseStepAction 执行 Dot 模式的点胶步骤
- **THEN** 系统遍历 SegmentRefs 中 IsEnabled 的引用
- **AND** 从 DispenserStationParams.Segments 中按 SourceSegmentId 查找源段获取点位数据
- **AND** 对每个源段调用 IDispenseExecuteService.ExecuteSinglePointLineAsync
- **AND** 工艺参数优先使用段覆盖参数，否则使用全局默认参数

#### Scenario: 执行点胶步骤（Arc 模式）
- **WHEN** DispenseStepAction 执行 Arc 模式的点胶步骤
- **THEN** 系统遍历 SegmentRefs 中 IsEnabled 的 Arc 类型引用
- **AND** 从 DispenserStationParams.Segments 中按 SourceSegmentId 查找源段获取点位数据
- **AND** 对每个源段调用 IDispenseExecuteService.ExecutePathAsync（连续插补走弧线）
- **AND** 工艺参数优先使用段覆盖参数，否则使用全局默认参数

#### Scenario: Z向校准执行
- **WHEN** DispenseDetail.EnableZCalibration 为 true
- **THEN** 在点胶执行前，系统先移动Z轴到 ZCalibrationHeight 高度
- **AND** 以 ZCalibrationSpeed 速度缓慢下降确认实际接触高度
- **AND** 校准完成后更新 EffectiveZHeight

#### Scenario: 源段缺失处理
- **WHEN** SegmentRef 引用的源段在 DispenserStationParams.Segments 中不存在
- **THEN** 跳过该引用并记录警告日志
- **AND** 继续执行剩余有效引用

#### Scenario: 执行前空跑
- **WHEN** DispenseDetail.ExecuteDryRunFirst 为 true
- **THEN** 先执行一次空跑（IDispenseExecuteService.DryRunAsync）
- **AND** 空跑成功后再执行真实点胶

#### Scenario: 急停安全
- **WHEN** 执行过程中收到急停或取消信号
- **THEN** 立即安全关胶（SafeGlueOff）
- **AND** 停止当前运动
- **AND** Z轴优先抬起至安全高度

### REQ-DISPENSE-EDITOR-ROUTE: 步骤编辑器路由集成

系统 SHALL 在 ProcessSequenceEditorViewModel 中添加 DISPENSE 步骤类型的路由。

#### Scenario: 双击 DISPENSE 步骤
- **WHEN** 用户双击 ProcessSequenceEditor 中的 DISPENSE 类型步骤
- **THEN** NavigateToDetailView 路由到 ShowDispenseDetailDialog
- **AND** 弹出 DispenseDetailView 编辑弹窗

#### Scenario: 新增 DISPENSE 步骤
- **WHEN** 用户在 AddEditStepDialog 中选择 DISPENSE 步骤类型
- **THEN** DISPENSE 出现在可选步骤类型列表中
- **AND** 创建的 ProcessStep 自动初始化 DispenseDetail 为默认实例

### REQ-DISPENSE-DATA-UNIQUENESS: 数据源唯一性保证

系统 SHALL 确保 Dispense 工具中的线段和圆弧数据只导入不创建，数据源唯一性由以下机制保证：

1. **DispenseSegmentRef 为轻量引用**：仅存储 SourceSegmentId，不存储点位数据副本
2. **运行时实时查找**：执行时从 DispenserStationParams.Segments 按 ID 查找源段获取实际点位
3. **导入来源唯一**：导入对话框仅显示当前配方中已存在的线段/圆弧，不可手动输入新段
4. **删除保护**：源段在 CadPointEditor 中被删除时，引用自动标记为无效

#### Scenario: 引用数据实时同步
- **WHEN** 源段的工艺参数在 CadPointEditor 中被修改
- **THEN** DispenseSegmentRef 的只读显示属性实时反映最新值
- **AND** 如果段使用默认参数（UseDefaultParams=true），执行时使用 DispenseDetail 的全局默认值而非源段值

#### Scenario: 防止重复导入
- **WHEN** 用户尝试导入已在 SegmentRefs 中存在的段
- **THEN** 系统自动过滤已导入的段，仅显示未导入的段
- **AND** 导入对话框中已导入的段显示为灰色不可选

### REQ-DISPENSE-MULTILINGUAL: 多语言支持

系统 SHALL 为所有新增 UI 文本提供中英文双语支持。

#### Scenario: 语言切换
- **WHEN** 用户切换系统语言
- **THEN** DispenseDetailView 中所有标签、按钮、提示文本自动切换为对应语言
- **AND** 新增 Lang Key 遵循 `DispenseDetail_` 前缀命名规范

## MODIFIED Requirements

### Requirement: ProcessSequenceEditorViewModel 路由扩展

**原有文件**：`Module/Controls/StepEditor/ProcessSequenceEditorViewModel.cs`

**变更**：
- 在 `NavigateToDetailView` 方法中添加 `StepType.DISPENSE` 分支，路由到 `ShowDispenseDetailDialog`
- 新增 `ShowDispenseDetailDialog(ProcessStep step)` 方法，按现有模式创建 DispenseDetailViewModel 并弹出 DialogHost

### Requirement: ProcessStep 模型扩展

**原有文件**：`StationTasks/Models/ProcessStep.cs`

**变更**：
- 新增 `DispenseDetail DispenseDetail` 属性（可序列化）
- 在序列化/反序列化逻辑中包含 DispenseDetail

### Requirement: ProcessStepExecutor 执行扩展

**原有文件**：`StationTasks/Execution/ProcessStepExecutor.cs`

**变更**：
- 在 `ExecuteSingleStepAsync` 的 switch 中添加 `StepType.DISPENSE` 分支
- 分支内调用已注册的 DispenseStepAction.ExecuteAsync

### Requirement: AddEditStepDialogViewModel 步骤类型启用

**原有文件**：`Module/Controls/StepEditor/AddEditStepDialogViewModel.cs`

**变更**：
- 在可用步骤类型列表中启用 DISPENSE（如果当前被过滤掉）

## REMOVED Requirements

无。所有现有功能保持兼容。
