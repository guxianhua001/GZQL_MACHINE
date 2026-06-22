# 双龙门标定控件 Spec

## Why
双龙门点胶设备存在两套独立 X 轴 + 共享下层 Y 轴的机构，目前缺少统一的标定界面，导致两套龙门坐标系无法融合，跨龙门 XY 定位存在累积误差。需在维护页面新增"双龙门标定"控件，以共享 Y 轴为公共基准，对两台上相机（Cam1/Cam2）分别进行全域仿射标定，并通过 Y 基准统一两套龙门坐标，彻底消除跨龙门误差。

## 机构梳理（核心纽带）

```
                    ┌─────────────── 共用下层 Y 轴（全局 Y 基准） ───────────────┐
                    │                                                          │
   ┌───────────────┴───────────────┐                ┌─────────────────────────┴──┐
   │  龙门 1（左）                  │                │  龙门 2（右）                │
   │  · Dx 独立 X 轴                │                │  · 独立 X 轴                 │
   │  · Dy 独立 Y 轴（上层微调）    │                │  · 共用下层 Y（无独立 Y）    │
   │  · 上相机 Cam1                 │                │  · 上相机 Cam2               │
   └────────────────────────────────┘                └─────────────────────────────┘
```

- **公共基准轴**：下层共用 Y 轴 —— 全局统一 Y 基准，是两套龙门坐标融合的核心纽带
- **龙门 1（左）**：Dx 独立 X 轴 + Dy 独立 Y 轴 + 上相机 Cam1（具备 Dy 微调能力）
- **龙门 2（右）**：独立 X 轴 + 共用下层 Y + 上相机 Cam2（无独立 Y，Y 直接跟随共用轴）

## What Changes
- 在维护页 TabControl 新增"双龙门标定"Tab 项（第 5 个 Tab）
- 新增 `DualGantryCalibrationView` / `DualGantryCalibrationViewModel`（位于 `Module/Controls/Maintenance/`）
- 新增 `IDualGantryCalibrationService` 接口（位于 `Core/Abstraction/`）与实现（位于 `Module/Services/`）
- 新增 `DualGantryCalibrationConfig` / `DualGantryCalibrationPoint` / `DualGantryCalibrationData` 数据模型（位于 `Core/Models/`）
- 复用已有 `AffineCalibrationService` 进行单龙门仿射标定
- 新增"跨龙门 Y 基准对齐"算法：以共用 Y 轴为纽带，计算两套龙门坐标系的 Y 方向偏移与旋转修正
- 在 `PrimModel.cs` 注册新视图、服务、导航
- 补充多语言资源 Key（zh-CN / en-US），前缀 `DualGantryCalib_`
- 在 `版本修改记录.txt` 追加版本记录

## Impact
- Affected specs: 维护模块、N点标定、相机标定、运动控制
- Affected code:
  - `Module/Controls/Maintenance/DualGantryCalibrationView.xaml(.cs)` (新增)
  - `Module/Controls/Maintenance/DualGantryCalibrationViewModel.cs` (新增)
  - `Module/Controls/Maintenance/MaintenanceView.xaml` (新增 Tab 项 + ContentControl DataTrigger)
  - `Core/Abstraction/IDualGantryCalibrationService.cs` (新增)
  - `Module/Services/DualGantryCalibrationService.cs` (新增)
  - `Core/Models/DualGantryCalibrationConfig.cs` (新增)
  - `Core/Models/DualGantryCalibrationPoint.cs` (新增)
  - `Core/Models/DualGantryCalibrationData.cs` (新增)
  - `Module/PrimModel.cs` (DI 注册)
  - `MainApp/Languages/Strings.zh-CN.xaml` (多语言)
  - `MainApp/Languages/Strings.en-US.xaml` (多语言)
  - `MainApp/bin/Debug/net9.0-windows7.0/版本修改记录.txt` (版本记录)

---

## ADDED Requirements

### Requirement: 双龙门标定控件入口
系统 SHALL 在维护页 TabControl 中新增"双龙门标定"Tab，作为第 5 个 Tab 项，提供双龙门全域标定入口。

#### Scenario: 用户进入双龙门标定
- **WHEN** 用户在维护页点击"双龙门标定"Tab
- **THEN** 系统延迟加载 `DualGantryCalibrationView`
- **AND** Tab 图标使用 `materialDesign:PackIcon Kind="VectorCombine"`
- **AND** Tab 文本使用 `{lang:Lang Maintenance_Tab_DualGantryCalibration}` 多语言绑定

#### Scenario: 懒加载
- **WHEN** Tab 未被选中
- **THEN** ViewModel 不创建，避免不必要的依赖注入与资源占用

---

### Requirement: 机构配置卡片
系统 SHALL 提供机构配置卡片，明确两套龙门的轴映射关系，作为标定前提。

#### Scenario: 显示机构拓扑
- **WHEN** 用户进入双龙门标定控件
- **THEN** 顶部显示机构拓扑示意图（文字+图标形式），明确标注：
  - 公共基准轴：共用 Y 轴名称（默认 `GantryY`）
  - 龙门 1：X 轴名（默认 `Dx`）、Y 轴名（默认 `Dy`）、相机 TCP 连接名（Cam1）
  - 龙门 2：X 轴名（默认 `X2`）、Y 轴名（绑定至共用 Y）、相机 TCP 连接名（Cam2）

#### Scenario: 轴名配置
- **WHEN** 用户修改轴名下拉框
- **THEN** 系统从 `IAxisConfigurationService` 获取当前工站可用轴列表填充下拉框
- **AND** 龙门 2 的 Y 轴下拉框默认锁定为共用 Y 轴名，提示"跟随公共基准轴"

#### Scenario: 工站选择
- **WHEN** 用户切换工站标识（如 DispenserStation / LoadingStation）
- **THEN** 可用轴列表刷新，轴名重置为默认值

---

### Requirement: 龙门 1 标定模块（Cam1 + Dx + Dy）
系统 SHALL 提供龙门 1 的 N 点仿射标定功能，使用 Cam1 视觉数据建立 CAD→机械坐标映射。

#### Scenario: 标定点表格
- **WHEN** 用户进入龙门 1 标定区
- **THEN** 显示标定点 DataGrid，列包含：序号、名称、机械 X（Dx）、机械 Y（Dy）、视觉 X、视觉 Y、状态、操作
- **AND** 默认生成 9 个空点位（3×3 网格），可增删

#### Scenario: 单点示教
- **WHEN** 用户点击某行的"示教"按钮
- **THEN** 系统通过 `IPositionMotionController.TeachAsync` 读取当前 Dx、Dy 轴坐标，填入该行机械 X/Y
- **AND** 状态列变为"已示教"（橙色）

#### Scenario: 单点移动
- **WHEN** 用户点击某行的"移动"按钮
- **THEN** 系统通过 `IPositionMotionController.GotoAsync` 移动 Dx、Dy 到该行机械坐标
- **AND** 移动前自动抬 Z 到安全高度（如配置了 Z 轴）

#### Scenario: 视觉数据接收
- **WHEN** 启用视觉数据且 TCP 连接名已配置
- **THEN** 系统订阅 `ITCPEventService.CameraMessageReceived`，解析 JSON/分隔符格式视觉坐标
- **AND** 自动填充当前选中行的视觉 X/Y
- **WHEN** 未启用视觉数据
- **THEN** 视觉 X/Y 列可手动输入

#### Scenario: 自动标定流程
- **WHEN** 用户点击"开始自动标定"
- **THEN** 系统按序执行：移动到点位 → 拍照触发 → 等待视觉数据 → 填充 → 延时 → 下一点
- **AND** 进度条实时更新，状态文本显示当前步骤
- **AND** 标定期间"开始"按钮禁用，"停止"按钮启用
- **WHEN** 用户点击"停止"
- **THEN** 系统安全停止流程，运动轴减速停止

#### Scenario: 计算仿射标定
- **WHEN** 已标定点数 ≥ 3 且用户点击"计算标定"
- **THEN** 系统调用 `AffineCalibrationService.Solve()` 计算 CAD→机械仿射参数（A/B/C/D/Tx/Ty）
- **AND** 显示 RMS 误差、等效旋转角、缩放因子、质量评级
- **AND** 龙门 1 标定结果独立存储

---

### Requirement: 龙门 2 标定模块（Cam2 + X2 + 共用 Y）
系统 SHALL 提供龙门 2 的 N 点仿射标定功能，使用 Cam2 视觉数据建立 CAD→机械坐标映射，Y 轴使用共用下层 Y。

#### Scenario: 标定点表格
- **WHEN** 用户进入龙门 2 标定区
- **THEN** 显示与龙门 1 结构相同的标定点 DataGrid
- **AND** 机械 Y 列绑定至共用 Y 轴坐标

#### Scenario: 单点示教与移动
- **WHEN** 用户执行示教/移动
- **THEN** 系统操作 X2 轴与共用 Y 轴（GantryY）
- **AND** 龙门 2 标定时，共用 Y 轴运动会影响龙门 1，需在 UI 提示"Y 轴为公共基准轴，运动将同步影响龙门 1"

#### Scenario: 视觉数据接收
- **WHEN** 启用视觉数据
- **THEN** 系统订阅 Cam2 对应的 TCP 连接，独立于 Cam1
- **AND** 两个 TCP 连接名独立配置

#### Scenario: 计算仿射标定
- **WHEN** 已标定点数 ≥ 3 且用户点击"计算标定"
- **THEN** 系统计算龙门 2 的仿射参数，独立存储
- **AND** 显示与龙门 1 相同的结果指标

---

### Requirement: 跨龙门 Y 基准对齐（核心纽带）
系统 SHALL 提供跨龙门 Y 基准对齐功能，以共用 Y 轴为纽带融合两套龙门坐标系，消除跨龙门 XY 误差。

#### Scenario: Y 基准对齐前提
- **WHEN** 龙门 1 与龙门 2 均已完成仿射标定（各 ≥3 点）
- **THEN** "跨龙门对齐"按钮启用
- **AND** 否则按钮禁用并提示"请先完成两套龙门的仿射标定"

#### Scenario: 公共基准点采集
- **WHEN** 用户点击"采集公共基准点"
- **THEN** 系统提示用户将共用 Y 轴移动到至少 2 个不同的 Y 位置
- **AND** 在每个 Y 位置，分别用 Cam1 和 Cam2 拍照同一标定物（或机械坐标对齐）
- **AND** 记录 (Y_common, Cam1_visionY, Cam2_visionY) 数据对

#### Scenario: Y 基准对齐计算
- **WHEN** 用户点击"跨龙门对齐"
- **THEN** 系统计算两套龙门坐标系的 Y 方向偏移量 `DeltaY = f(Y_common, Cam1, Cam2)`
- **AND** 计算两套坐标系在共用 Y 基准下的旋转修正角（若存在 X 方向倾斜）
- **AND** 生成"跨龙门变换参数"：`{OffsetX, OffsetY, RotationDeg, Scale}`
- **AND** 显示对齐残差，残差 > 0.05mm 时警告

#### Scenario: 跨龙门坐标变换
- **WHEN** 跨龙门对齐完成
- **THEN** 系统提供"坐标变换验证"功能：输入龙门 1 坐标 → 输出龙门 2 等效坐标
- **AND** 变换公式：`X2 = OffsetX + X1·cos(θ) - Y1·sin(θ)`，`Y2 = OffsetY + X1·sin(θ) + Y1·cos(θ)`

---

### Requirement: 文件操作
系统 SHALL 提供标定数据的保存、加载、导入、导出功能。

#### Scenario: 保存配置
- **WHEN** 用户点击"保存"
- **THEN** 系统将机构配置、两套龙门标定点、两套仿射结果、跨龙门变换参数持久化到 `Config/Calibration/DualGantryCalibration_<名称>.json`
- **AND** 记录上次文件名，启动时自动加载

#### Scenario: 另存为 / 导入 / 导出
- **WHEN** 用户点击"另存为"
- **THEN** 系统弹出 `IFileDialogService` 输入新文件名后保存
- **WHEN** 用户点击"导入"
- **THEN** 系统弹出文件对话框选择 JSON 文件加载
- **WHEN** 用户点击"导出"
- **THEN** 系统弹出文件对话框选择导出路径后保存

#### Scenario: 自动加载
- **WHEN** 控件初始化
- **THEN** 系统从 `IParameterStorage` 读取上次文件名，从 `Config/Calibration/` 加载该文件
- **AND** 加载失败时状态栏显示"自动加载失败"，不阻塞界面

---

### Requirement: 状态反馈与日志
系统 SHALL 提供实时状态反馈与日志记录。

#### Scenario: 状态栏
- **WHEN** 任何操作执行
- **THEN** 底部状态栏显示操作结果，颜色区分：成功(绿)/警告(橙)/错误(红)/信息(蓝)
- **AND** 状态文本支持多语言

#### Scenario: 日志记录
- **WHEN** 标定流程执行
- **THEN** 通过 `ILoggerService` 记录 Info/Warn/Error 级别日志
- **AND** 关键节点（开始、完成、错误）记录到日志文件

---

### Requirement: 运动控制安全性
系统 SHALL 确保标定流程中的运动控制安全性，符合工业设备控制要求。

#### Scenario: 急停支持
- **WHEN** 标定流程执行中用户触发急停
- **THEN** 系统立即停止所有运动轴
- **AND** 通过 `CancellationTokenSource` 取消标定流程
- **AND** 状态恢复为就绪

#### Scenario: 防撞保护
- **WHEN** 执行水平移动前
- **THEN** 系统先抬升 Z 轴到安全高度（如配置了 Z 轴）
- **AND** 移动完成后再下降 Z 轴到目标位置

#### Scenario: 共用 Y 轴互锁
- **WHEN** 龙门 2 标定中需运动共用 Y 轴
- **THEN** 系统检查龙门 1 是否处于运动状态
- **AND** 若龙门 1 运动中，等待其停止后再执行共用 Y 轴运动
- **AND** UI 提示"Y 轴为公共基准轴，运动将同步影响另一龙门"

#### Scenario: 操作确认
- **WHEN** 用户执行关键操作（计算标定、跨龙门对齐、应用变换）
- **THEN** 系统弹出确认对话框，用户确认后才执行

---

### Requirement: 多语言支持
系统 SHALL 为所有新增 UI 文本提供中英文双语支持。

#### Scenario: 语言切换
- **WHEN** 用户切换系统语言
- **THEN** 双龙门标定控件所有文本自动切换
- **AND** XAML 使用 `{lang:Lang Key}` 标记扩展
- **AND** ViewModel 中通过 `ILocalizationService` 获取本地化文本
- **AND** 所有新增 Key 同步添加到 `Strings.zh-CN.xaml` 和 `Strings.en-US.xaml`

---

### Requirement: UI 设计规范
系统 SHALL 遵循项目现有 UI 设计规范，与维护页其他 Tab 保持视觉一致性。

#### Scenario: 布局结构
- **WHEN** 构建双龙门标定界面
- **THEN** 采用左右分栏布局：
  - 左栏（ScrollViewer）：机构配置卡片 + 文件操作卡片
  - 右栏：上下分栏，上为龙门 1 标定区，下为龙门 2 标定区，最下方为跨龙门对齐区
- **AND** 使用 `materialDesign:Card UniformCornerRadius="8" Padding="16"` 作为容器
- **AND** 卡片标题使用 `materialDesign:PackIcon` + `TextBlock`（PrimaryHueMidBrush 前景色）

#### Scenario: 图标规范
- **WHEN** 使用图标
- **THEN** 统一使用 `<materialDesign:PackIcon>`，不使用 emoji
- **AND** 龙门 1 区使用 `Kind="Numeric1BoxOutline"`，龙门 2 区使用 `Kind="Numeric2BoxOutline"`，跨龙门区使用 `Kind="LinkVariant"`

#### Scenario: 颜色规范
- **WHEN** 显示状态
- **THEN** 龙门 1 主色 `#1565C0`（蓝），龙门 2 主色 `#00897B`（青），跨龙门对齐主色 `#6A1B9A`（紫）
- **AND** 状态颜色：已示教=橙(#FB8C00)、已标定=绿(#43A047)、错误=红(#E53935)

## MODIFIED Requirements

### Requirement: MaintenanceView TabControl 扩展
`MaintenanceView.xaml` SHALL 在 TabControl 中新增第 5 个 TabItem，并在 ContentControl 的 DataTrigger 中添加 `SelectedTabIndex=4` 的内容模板映射到 `DualGantryCalibrationView`。

### Requirement: PrimModel DI 注册扩展
`PrimModel.RegisterTypes()` 中 SHALL 新增：
- `IDualGantryCalibrationService` → `DualGantryCalibrationService` (Singleton)
- `DualGantryCalibrationView` / `DualGantryCalibrationViewModel` (RegisterForNavigation)

## REMOVED Requirements
无移除需求。本次为纯新增功能，不影响现有 N 点标定模块。
