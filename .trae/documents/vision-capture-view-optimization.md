# VisionCaptureView 优化计划

## 需求对照

| # | 需求 | 当前状态 | 改动范围 |
|---|------|---------|---------|
| 1 | 取消 Station: DispenserStation 显示 | UI 显示了 Station 标签 | 删除 XAML 中 Station 行 |
| 2 | 取消刷新按钮 | UI 有 Refresh 按钮 | 删除 XAML 中 Refresh 按钮 |
| 3 | 轴位置从已注册工站自动搜索 | 硬编码 StationId/DxAxisName 等 | 重构 ViewModel，通过 IStationRegistry + IMotionService.GetAxisConfigurations 自动解析轴ID和位置 |
| 4 | 连接下拉选项从 TCP 模块读取 | ConnectionName 是手动输入的 TextBox | 改为 ComboBox，数据源从 ITCPEventService.GetServerNames() + ITCPClientManagerService.Clients.Keys 读取 |
| 5 | 操作列取消点胶按钮，每行设置 Dot/Arc 模式 | 操作列有"拍照"+"点胶"两个按钮 | 操作列只保留"拍照"按钮，DispenseType 列保留 |
| 6 | 状态不放在 DataGrid 最后一列，放单独位置 | StatusMessage 在 DataGrid 最后一列 | 从 DataGrid 移除状态列，在 DataGrid 下方添加独立状态栏 |
| 7 | 点胶操作区域可选 Dot/Arc | 点胶操作在 DataGrid 行内按钮 | 新增独立点胶操作区：选中行 → 选择 Dot/Arc → 执行 |
| 8 | 数据解析区分 Dot/Arc 模式 | 解析逻辑未区分 | Dot 模式解析 needleX/needleY；Arc 模式解析 startX/startY/midX/midY/endX/endY |
| 9 | Arc 起点/中间点/终点从视觉数据解析 | ArcStartKey/MidKey/EndKey 手动配置 | Arc 模式下自动从 ParsedData 中提取三点，无需手动配置 key |
| 10 | 可查看贝塞尔弧线最终机械坐标列表 | 无此功能 | 新增"预览机械坐标"按钮，显示离散化后的机械坐标列表 |
| 11 | 空跑模式/点胶模式 | 无此功能 | 新增 RunMode 枚举(DryRun/Dispense)，空跑模式只运动不出胶 |

## 实施步骤

### Step 1: 重构 PhotoPositionRow 模型

**文件**: `Module/WorkStation/Dispense/PhotoPositionRow.cs`

改动：
- 移除 `ArcStartKey`/`ArcMidKey`/`ArcEndKey` 属性（Arc 三点从视觉数据自动提取）
- 保留 `ArcSegments` 属性
- 移除 `RawResponse`/`ParsedData`/`IsExecuting`/`StatusMessage`（这些移到 ViewModel 层管理）
- 新增 `IsSelected` 属性（用于选中行）
- `ConnectionName` 改为下拉选项，新增 `AvailableConnections` 集合

### Step 2: 新增 RunMode 枚举和 VisionCaptureResult 模型

**文件**: `Module/WorkStation/Dispense/PhotoPositionRow.cs`（同文件追加）

改动：
- 新增 `RunMode` 枚举：`DryRun`（空跑）/ `Dispense`（点胶）
- 新增 `VisionCaptureResult` 类：持有 RawResponse、ParsedData、MachinePoints（机械坐标列表）

### Step 3: 重构 VisionCaptureViewModel

**文件**: `Module/WorkStation/Dispense/VisionCaptureViewModel.cs`

核心改动：

**3.1 依赖注入新增**
- `IStationRegistry` — 获取已注册工站列表
- `ITCPClientManagerService` — 获取客户端连接名列表
- `IMotionService` — 获取轴配置（AxisConfig），实现轴名→轴ID自动映射

**3.2 移除硬编码**
- 删除 `StationId`/`Dz1AxisId`/`DxAxisId`/`DyAxisId`/`YAxisId`/`CoordId`/`DxAxisName`/`DyAxisName`/`Dz1AxisName`/`YAxisName` 硬编码属性
- 新增 `ResolveAxisId(string stationIdentifier, string axisName)` 方法：通过 `IMotionService.GetAxisConfigurations()` + `GetTaskConfigurations()` 自动查找轴ID
- 新增 `ResolveCoordId(string stationIdentifier)` 方法：通过 TaskConfig 查找坐标系ID

**3.3 连接列表加载**
- 新增 `ObservableCollection<string> AvailableConnections` 属性
- `LoadConnectionsAsync()` 方法：合并 `ITCPEventService.GetServerNames()` 和 `ITCPClientManagerService.Clients.Keys`

**3.4 状态管理移到 ViewModel**
- 新增 `PhotoPositionRow SelectedRow` 属性（当前选中行）
- 新增 `VisionCaptureResult CurrentResult` 属性（当前结果）
- 新增 `string StatusMessage` 属性（独立状态栏）
- 新增 `ObservableCollection<(double X, double Y)> MachinePoints` 属性（贝塞尔机械坐标列表）
- 新增 `RunMode CurrentRunMode` 属性（空跑/点胶模式切换）

**3.5 数据解析区分 Dot/Arc**
- Dot 模式：从 ParsedData 中提取 `needleX`/`needleY`
- Arc 模式：从 ParsedData 中自动查找三点（按 key 前缀匹配 `start`/`mid`/`end`，或取前3个坐标对）

**3.6 预览机械坐标**
- 新增 `PreviewMachinePointsCommand`：对当前选中行的 ParsedData 执行贝塞尔离散化 + 坐标转换，结果显示在 MachinePoints 列表

**3.7 空跑/点胶模式**
- `ExecuteDispenseAsync` 中根据 `CurrentRunMode` 决定是否调用出胶逻辑
- 空跑模式：只执行轴运动（移动到各点），不触发点胶阀

### Step 4: 重构 VisionCaptureView.xaml

**文件**: `Module/WorkStation/Dispense/VisionCaptureView.xaml`

布局重构为4个区域：

**区域1: Group 选择**（简化）
- 只保留 Group ComboBox，移除 Station 显示和 Refresh 按钮

**区域2: 拍照位配置 DataGrid**（精简）
- 列：SiteFeature | Dx位置 | Dy位置 | Dz₁位置 | Y位置 | Spd | 触发命令 | 连接(ComboBox) | 类型(Dot/Arc) | 操作(仅拍照)
- 移除状态列
- 连接列改为 ComboBox，ItemsSource 绑定 DataContext.AvailableConnections

**区域3: 状态 + 视觉数据 + 点胶操作**（合并重组）
- 3a: 状态栏（独立显示当前执行状态）
- 3b: 视觉数据显示（RawResponse + ParsedData，区分 Dot/Arc 解析结果）
- 3c: 点胶操作区：RunMode 切换(DryRun/Dispense) + Dot/Arc 选择 + 执行按钮 + 预览机械坐标按钮
- 3d: 贝塞尔机械坐标列表（DataGrid 或 ListView，显示序号/X/Y）

**区域4: 坐标转换参数**（保留不变）

### Step 5: 更新 VisionCaptureService

**文件**: `StationTasks/Services/VisionCaptureService.cs`

改动：
- 返回值增加 `string RawResponse`，新增 `VisionCaptureResult` 返回类型包含 RawResponse + ParsedData
- 方法签名简化：移除各轴ID参数，改为接收 `Dictionary<string, int> axisIdMap`（轴名→轴ID映射）

### Step 6: 更新 BezierArcDispenseService

**文件**: `StationTasks/Services/BezierArcDispenseService.cs`

改动：
- 新增 `List<(double X, double Y)> ComputeMachinePoints(...)` 公开方法：只计算不运动，供预览使用
- 新增 `dryRun` 参数：空跑模式只运动不出胶
- Arc 三点提取逻辑改为从 ParsedData 自动提取

### Step 7: 编译验证

- StationTasks 项目编译
- Module 项目编译（LogViewer 既有问题除外）

## 依赖关系

```
Step 1 (PhotoPositionRow) ──┐
Step 2 (枚举/模型) ─────────┤
                             ├──→ Step 3 (ViewModel) ──→ Step 4 (View) ──→ Step 7 (编译)
Step 5 (VisionCaptureService)┤
Step 6 (BezierArcDispense) ──┘
```

Step 1/2/5/6 可并行，Step 3 依赖它们，Step 4 依赖 Step 3。
