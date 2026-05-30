# DotPointEditorView 细化规范 — Dots 点胶单点编辑器

## Why

现有 `DotPointEditorView` 为简易原型，数据模型 `DotPoint` 仅有 X/Y/Z/Offset 字段，工艺参数编辑未实现，DryRun/Execute 均为模拟空壳，无法满足工业点胶机的实际生产需求：
- **数据模型不完整**：缺少 Dz₂/Dz₃ 双Z轴高度、Rx/Ry 旋转角度、双Z轴补偿等关键维度
- **工艺参数缺失**：未复用轨迹走胶的工艺参数体系，缺少出胶时间(ms)等核心参数
- **执行逻辑为空壳**：DryRun/Execute 仅弹对话框模拟，未对接 `IDispenseExecuteService`
- **Group 未关联工单**：AssyGroup 列为硬编码列表，未引用工单配置的 Site 部件
- **UI 布局不专业**：操作人员无法一眼看懂操作流程

本规范将 DotPointEditorView 升级为**行业级点胶单点编辑器**，实现完整的单点编辑、工艺参数管理、真实点胶执行和保存/加载功能。

## What Changes

- **重构 DotPoint 数据模型**：新增 Dz₂/Dz₃/Rx/Ry/Dz₂Compensation/Dz₃Compensation 字段，列名顺序为 Group → ID → Dx → Dy → Dz₂ → Dz₃ → Rx → Ry → Dz₂补偿 → Dz₃补偿 → 示教
- **新增 DotProcessParams 工艺参数模型**：复用轨迹走胶工艺参数体系（MoveSpeed/SafeHeight/PreDelay/PostDelay/DispenseTime 等），新增出胶时间(ms)
- **重构 DotPointEditorViewModel**：注入 IDispenseExecuteService/IMotionService/IWorkOrderService，实现真实 DryRun/Execute/Teach 逻辑
- **重构 DotPointEditorView UI**：专业工站布局（左数据区 + 右工艺参数区 + 底部执行区），操作流程清晰
- **Group 列关联工单配置**：引用 WorkOrderData.Sites 中 SiteFeatureType.AssyGroup 类型的部件
- **实现保存/加载**：工艺参数和点数据可序列化保存与加载

### 影响范围

#### Core 项目

| 文件 | 类型 | 说明 |
|------|------|------|
| `Core/Models/DotProcessParams.cs` | 新增 | 点胶工艺参数模型（复用轨迹走胶参数体系 + 出胶时间） |

#### Module 项目

| 文件 | 类型 | 说明 |
|------|------|------|
| `Module/Models/DotPoint.cs` | 重构 | 扩展为完整字段模型（Dz₂/Dz₃/Rx/Ry/双Z补偿） |
| `Module/WorkStation/Dispense/DotPointEditorView.xaml` | 重构 | 专业工站布局 UI |
| `Module/WorkStation/Dispense/DotPointEditorViewModel.cs` | 重构 | 完整业务逻辑 + 服务注入 |
| `Module/Services/IDotDispenseService.cs` | 新增 | 点胶单点执行服务接口 |
| `Module/Services/DotDispenseService.cs` | 新增 | 点胶单点执行服务实现 |

---

## ADDED Requirements

### REQ-DOT-MODEL: DotPoint 数据模型重构

系统 SHALL 重构 `DotPoint` 模型，包含以下字段（列名顺序严格如下）：

| 序号 | 字段名 | 属性名 | 类型 | 说明 |
|------|--------|--------|------|------|
| 1 | Group | Group | string | 装配组，可引用工单配置的 Site 部件（SiteFeatureType.AssyGroup） |
| 2 | ID | PointId | string | 点位唯一标识，自动编号（如 DOT_001） |
| 3 | Dx | Dx | double | X 轴坐标 (mm) |
| 4 | Dy | Dy | double | Y 轴坐标 (mm) |
| 5 | Dz₂ | Dz2 | double | Z₂ 轴高度 (mm) — 主点胶高度 |
| 6 | Dz₃ | Dz3 | double | Z₃ 轴高度 (mm) — 辅助/检测高度 |
| 7 | Rx | Rx | double | X 轴旋转角度 (°) |
| 8 | Ry | Ry | double | Y 轴旋转角度 (°) |
| 9 | Dz₂补偿 | Dz2Compensation | double | Z₂ 轴高度补偿 (mm) |
| 10 | Dz₃补偿 | Dz3Compensation | double | Z₃ 轴高度补偿 (mm) |
| 11 | 示教 | — | Button | 触发示教命令，读取当前运动轴位置填入 Dx/Dy/Dz₂ |

**额外属性**（非 DataGrid 列，内部使用）：
- `IsSelected: bool` — 行选中状态（用于批量操作和执行过滤）
- `IsEnabled: bool` — 是否启用（默认 true）
- `EffectiveDz2: double`（只读）= Dz2 + Dz2Compensation — Z₂ 有效工作高度
- `EffectiveDz3: double`（只读）= Dz3 + Dz3Compensation — Z₃ 有效工作高度

#### Scenario: 新增点位
- **WHEN** 用户点击"添加点位"按钮
- **THEN** 系统在 DataGrid 末尾新增一行，Group 默认为当前选中 Group，PointId 自动递增，其余数值字段默认为 0

#### Scenario: 示教单点
- **WHEN** 用户点击某行的"示教"按钮
- **THEN** 系统读取当前运动轴位置（IMotionService.GetAxisPosition），将 Dx/Dy/Dz₂ 更新为当前 X/Y/Z₁ 轴位置

### REQ-DOT-PROCESS-PARAMS: 点胶工艺参数模型

系统 SHALL 在 Core 层提供 `DotProcessParams` 模型，复用轨迹走胶的工艺参数体系并新增出胶时间：

```
DotProcessParams : BindableBase
  // === 运动参数 ===
  - MoveSpeed: double          // 运动速度 mm/s，默认 10.0，范围 0.1~50
  - SafeHeight: double         // 安全抬升高度 mm，默认 5.0，范围 0~200
  - ApproachHeight: double     // 接近高度 mm，默认 3.0（快速下降到此高度后慢速到位）
  - CornerDecel: double        // 减速系数 0~1，默认 0.3

  // === 出胶参数 ===
  - DispenseTime: double       // 出胶时间 ms，默认 200.0，范围 10~5000
  - PreDelay: double           // 开胶前延时 ms，默认 0.0，范围 0~5000
  - PostDelay: double          // 关胶后延时 ms，默认 50.0，范围 0~5000
  - GlueTriggerOffsetMm: double // 开胶触发距离 mm，默认 0.5，范围 0.05~5.0

  // === 阀控参数 ===
  - DispensingPressure: double // 点胶气压 MPa，默认 0.3，范围 0.1~1.0
  - SuckBackTime: double       // 回吸时间 ms，默认 100，范围 10~500

  // === 高度参数 ===
  - TeachHeight: double        // 示教高度 mm，默认 0.0
  - HeightCompensation: double // 高度补偿 mm，默认 0.0
  - EffectiveZHeight: double   // 只读 = TeachHeight + HeightCompensation
```

#### Scenario: 工艺参数应用到所有选中点
- **WHEN** 用户修改工艺参数后点击"应用参数"
- **THEN** 所有 IsSelected=true 的点位使用更新后的工艺参数执行

### REQ-DOT-GROUP: Group 关联工单 Site 部件

系统 SHALL 使 Group 列的下拉选项来源于工单配置的 Site 部件：

- Group 列的 ComboBox ItemsSource 绑定到 `WorkOrderData.Sites` 中 `SiteFeatureType == AssyGroup` 的 Site 列表
- 如果工单未配置 Site 数据，则回退到默认列表 ["ASSY_001", "ASSY_002", ...]
- 切换 Group 时，该点位的坐标基准可能变化（由上层对齐逻辑决定）

#### Scenario: 工单已配置 Site 部件
- **WHEN** 工单配置中存在 Site 且其 Features 包含 SiteFeatureType.AssyGroup 类型的条目
- **THEN** Group 下拉列表显示工单中配置的 AssyGroup 名称

### REQ-DOT-EXECUTE: 点胶执行逻辑（遵循行业标准）

系统 SHALL 提供空跑试运行和真实点胶两种执行模式，动作顺序遵循行业标准：

**点模式执行流程**（单点点胶）：

```
对每个 IsSelected && IsEnabled 的点位：
  1. Z 轴抬升到安全高度 (SafeHeight)
  2. XY 移动到点位坐标 (Dx, Dy)
  3. Z 轴下降到有效工作高度 (EffectiveDz2)
     - 两段式下降：快速到 ApproachHeight → 慢速到位
  4. 开胶前延时 (PreDelay)
  5. 开胶 (WriteDo GlueIoPort = true)
  6. 出胶时间等待 (DispenseTime ms)
  7. 关胶 (WriteDo GlueIoPort = false)
  8. 关胶后延时 (PostDelay)
  9. Z 轴抬升到安全高度 (SafeHeight)
```

**如果全部勾选（IsSelected）**：动作顺序为先 Z 轴抬升到安全高度再点胶，即：
- 首先统一抬升所有 Z 轴到安全高度
- 然后逐点执行 XY 定位 → Z 下降 → 出胶 → Z 抬升 循环

**空跑试运行**：与真实点胶相同的运动流程，但不出胶（跳过步骤 5~8），Z 轴保持在安全高度不下降

#### Scenario: 真实点胶执行
- **WHEN** 用户点击"真实点胶"按钮，且存在 IsSelected 的点位
- **THEN** 系统按上述流程逐点执行，发布 ProgressChanged 事件更新 UI 进度

#### Scenario: 全部勾选时的执行
- **WHEN** 所有点位均被勾选（IsSelected）
- **THEN** 系统先统一抬升 Z 轴到安全高度，再逐点执行点胶循环

#### Scenario: 急停中断
- **WHEN** 执行过程中用户触发急停或点击停止按钮
- **THEN** 系统立即安全关胶，停止当前运动，抛出 OperationCanceledException

### REQ-DOT-UI: 专业工站布局 UI

系统 SHALL 重构 DotPointEditorView 为专业工站布局，使操作人员一看就懂怎么操作：

**布局结构**：

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  🔵 Dots 点胶编辑器                                              [状态指示灯] │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌─ 工艺参数面板（右侧固定卡片）──────────────────────────────────────────┐  │
│  │  ┌─ 运动参数 ──────────┐  ┌─ 出胶参数 ──────────┐  ┌─ 高度参数 ────┐ │  │
│  │  │ 速度: [10.0] mm/s   │  │ 出胶时间: [200] ms  │  │ 示教高度:[0.0]│ │  │
│  │  │ 安全高度: [5.0] mm  │  │ 开胶前延时: [0] ms  │  │ 高度补偿:[0.0]│ │  │
│  │  │ 接近高度: [3.0] mm  │  │ 关胶后延时: [50] ms │  │ 有效高度: 0.0 │ │  │
│  │  │ 减速系数: [0.3]     │  │ 开胶距离: [0.5] mm  │  │ [示教高度]    │ │  │
│  │  └─────────────────────┘  │ 气压: [0.3] MPa    │  └───────────────┘ │  │
│  │                           │ 回吸时间: [100] ms  │                    │  │
│  │                           └─────────────────────┘                    │  │
│  │  [应用参数到选中点]  [保存工艺参数]  [加载工艺参数]                     │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│                                                                              │
│  ┌─ 点位数据区（主区域）────────────────────────────────────────────────┐  │
│  │  Group筛选: [全部▼]   [添加点位] [删除选中] [全选] [反选]            │  │
│  │                                                                      │  │
│  │  ┌───────────────────────────────────────────────────────────────┐   │  │
│  │  │ ☑ │ Group    │ ID      │ Dx    │ Dy    │ Dz₂   │ Dz₃   │...│   │  │
│  │  ├───┼──────────┼─────────┼───────┼───────┼───────┼───────┼───┤   │  │
│  │  │ ☑ │ ASSY_001 │ DOT_001 │10.500 │20.300 │ 5.011 │ 3.200 │...│   │  │
│  │  │ ☑ │ ASSY_001 │ DOT_002 │15.000 │25.100 │ 5.008 │ 3.150 │...│   │  │
│  │  │ ☐ │ ASSY_002 │ DOT_003 │20.500 │30.000 │ 5.003 │ 3.100 │...│   │  │
│  │  └───────────────────────────────────────────────────────────────┘   │  │
│  │  Rx │ Ry │ Dz₂补偿│ Dz₃补偿│ 示教 │                                │  │
│  │  ...│ ...│  0.000 │  0.000 │[示教]│                                │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│                                                                              │
│  ┌─ 执行控制区（底部固定栏）────────────────────────────────────────────┐  │
│  │  [▶ 空跑试运行]  [● 真实点胶]  [⏹ 停止]  │ 进度: ████░░ 3/6  │ ✅ 就绪 │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────────────┘
```

**UI 设计原则**：
1. **工艺参数在上方**：操作人员首先确认参数，再操作点位数据
2. **DataGrid 为主区域**：占据最大空间，列名清晰，支持行内编辑
3. **执行控制在底部**：固定可见，操作人员随时可触发执行或停止
4. **Group 筛选**：DataGrid 上方提供 Group 筛选下拉框，快速过滤显示特定 Group 的点位
5. **状态反馈**：执行区右侧显示当前状态（就绪/运行中/完成/错误），进度条显示执行进度

### REQ-DOT-SAVE-LOAD: 工艺参数和数据保存加载

系统 SHALL 支持工艺参数和点位数据的保存与加载：

**保存内容**：
- 工艺参数（DotProcessParams 全部属性）
- 点位数据（DotPoint 集合，含所有坐标和补偿值）

**保存格式**：JSON 文件，结构如下：
```json
{
  "ProcessParams": { ... DotProcessParams properties ... },
  "Points": [ ... DotPoint array ... ],
  "SavedAt": "2026-05-15T10:30:00",
  "Version": "1.0"
}
```

**加载行为**：
- 加载时验证 JSON 结构完整性
- 加载后自动刷新 UI（DataGrid + 工艺参数面板）
- 如果加载的 Group 在当前工单中不存在，保留原值但不报错

#### Scenario: 保存工艺参数和点位数据
- **WHEN** 用户点击"保存"按钮
- **THEN** 系统弹出文件保存对话框，将当前工艺参数和点位数据序列化为 JSON 文件

#### Scenario: 加载工艺参数和点位数据
- **WHEN** 用户点击"加载"按钮并选择 JSON 文件
- **THEN** 系统反序列化文件内容，更新工艺参数面板和 DataGrid 数据

---

## MODIFIED Requirements

### REQ-EXISTING-DOT-POINT: DotPoint 模型重构

**原有文件**：`Module/Models/DotPoint.cs`（68行，仅含 AssyGroup/SiteId/SubAssy/X/Y/Z/Offset/IsSelected）

**变更**：
- 移除 SubAssy/Offset 字段（不再需要）
- SiteId 重命名为 PointId
- AssyGroup 重命名为 Group
- X/Y/Z 重命名为 Dx/Dy/Dz2
- 新增 Dz3/Rx/Ry/Dz2Compensation/Dz3Compensation/IsEnabled/EffectiveDz2/EffectiveDz3
- 保留 IsSelected 用于批量操作

### REQ-EXISTING-DOT-VM: DotPointEditorViewModel 重构

**原有文件**：`Module/WorkStation/Dispense/DotPointEditorViewModel.cs`（234行，模拟空壳）

**变更后职责**：
- 注入 IDispenseExecuteService、IMotionService、IWorkOrderService（或直接引用 WorkOrderData）
- 管理 DotProcessParams 工艺参数实例
- 实现 TeachPoint：读取 IMotionService.GetAxisPosition 填入坐标
- 实现 DryRun：调用 IDispenseExecuteService.DryRunAsync 或自定义空跑逻辑
- 实现 Execute：逐点点胶执行（Z抬升→XY定位→Z下降→开胶→延时→关胶→Z抬升）
- 实现 Save/Load：JSON 序列化/反序列化
- Group 列表从 WorkOrderData.Sites 动态获取

### REQ-EXISTING-DOT-VIEW: DotPointEditorView 布局重构

**原有文件**：`Module/WorkStation/Dispense/DotPointEditorView.xaml`（148行，简单 StackPanel 布局）

**变更**：按 REQ-DOT-UI 的专业工站布局重构，DataGrid 列按指定顺序排列
