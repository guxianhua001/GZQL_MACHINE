# 相机拍照功能控件 Spec

## Why
DispensingView 区域7（Auto Paths Generation - Type D）当前为模拟实现，无法完成"视觉拍照→数据解析→点胶执行"的完整工作流。需要构建一个数据驱动的拍照功能控件，从配方池读取 Group/SiteFeature 配置，驱动轴运动到拍照位、触发相机、解析返回数据，并集成 Dot/Arc 两种点胶模式，实现灵活配置、拒绝硬编码的目标。

## What Changes
- **重构 AutoPathsGenerationView/ViewModel** 为 VisionCaptureView/VisionCaptureViewModel，实现完整的拍照+点胶工作流
- 新增 PhotoPositionRow ViewModel 层模型，每行对应一个 SiteFeature 的拍照位配置
- 新增 VisionCaptureService 服务，封装拍照执行逻辑（安全检查→Z轴抬起→XY移动→Z下降→触发拍照→等待数据→返回待机）
- 新增 BezierArcDispenseService 服务，封装贝塞尔弧线点胶逻辑（三点→二次贝塞尔→离散化→坐标系转换→多段插补走胶）
- 在 DispensingView.xaml 区域7中激活 VisionCaptureView
- 复用现有 IPositionProvider / ITCPEventService / IVisionDataParser / IMotionService 基础设施

## Impact
- Affected specs: tcpip-vision-integration（复用 ITCPEventService / IVisionDataParser）
- Affected code:
  - `Module/WorkStation/Dispense/AutoPathsGenerationView.xaml` → 重构为 VisionCaptureView.xaml
  - `Module/WorkStation/Dispense/AutoPathsGenerationViewModel.cs` → 重构为 VisionCaptureViewModel.cs
  - `Module/WorkStation/Dispense/AutoPathsGenerationView.xaml.cs` → 重构为 VisionCaptureView.xaml.cs
  - `Module/WorkStation/Dispense/DispensingView.xaml` — 区域7激活 VisionCaptureView
  - `Module/PrimModel.cs` — 注册 VisionCaptureView/ViewModel 映射
  - `StationTasks/Services/` — 新增 VisionCaptureService
  - `StationTasks/Services/` — 新增 BezierArcDispenseService
  - `StationTasks/StationTasksModule.cs` — 注册新服务

## ADDED Requirements

### Requirement: Group/SiteFeature 选择与拍照位配置表
系统 SHALL 从当前配方池的 WorkOrderData 中读取 Sites（Group）列表和 SiteFeature 子项，在 UI 中提供 Group 选择和 SiteFeature 拍照位配置表。

#### Scenario: 加载 Group 和 SiteFeature
- **WHEN** 用户进入 VisionCaptureView
- **THEN** 系统从 IRecipePoolService 读取 WorkOrderData
- **AND** Groups 下拉框显示所有 Site.Name
- **AND** 选中 Group 后，拍照位配置表显示该 Site 下所有 SiteFeature 子项

#### Scenario: 拍照位配置表行
- **WHEN** 拍照位配置表加载 SiteFeature 子项
- **THEN** 每行显示：SiteFeature名称 | Dx位置名 | Dy位置名 | Dz₁位置名 | Y位置名 | 速度 | 触发命令 | [执行]
- **AND** 位置名列通过 IPositionProvider.GetPositionsAsync(stationId) 动态加载可选位置名
- **AND** 选中位置名后，系统从 Positions 字典读取对应轴的目标位置值并显示

#### Scenario: 位置名引用方式
- **WHEN** 用户在位置名列中选择一个位置名（如 NewPosition1）
- **THEN** 系统从 DispenserStationParams.Positions 字典中读取该位置名下对应轴的坐标值
- **AND** 读取方式与 GotoDetailView 一致：通过 IPositionProvider 获取 "位置名.轴名" 格式的扁平字典

### Requirement: 拍照执行逻辑（安全优先）
系统 SHALL 提供安全的拍照执行流程，严格遵循"Z轴先抬起"的安全原则，支持超时重试和取消。

#### Scenario: 正常拍照执行
- **WHEN** 用户点击某行的 [执行] 按钮
- **THEN** 系统按以下顺序执行：
  1. 安全检查：验证所有位置名有效、Z轴安全高度已配置
  2. Z轴（Dz₁）抬起到安全高度位置
  3. Dx/Dy/Y 轴同时移动到拍照位（多轴插补）
  4. Z轴下降到拍照高度
  5. 通过 ITCPEventService 发送触发命令
  6. 等待视觉系统返回数据（受超时时间限制）
  7. 使用 IVisionDataParser 解析返回数据
  8. Z轴抬起回安全高度
  9. 可选：各轴返回待机位

#### Scenario: 拍照超时处理
- **WHEN** 触发拍照后在超时时间内未收到响应
- **THEN** 抛出 RecoverableException，提示操作员检查视觉系统连接
- **AND** 提供重试/暂停/停止选项
- **AND** 选择重试则重新触发拍照（不移动轴，从步骤5重新开始）

#### Scenario: 执行取消
- **WHEN** 用户在拍照执行过程中请求停止
- **THEN** 通过 CancellationToken 取消当前运动和等待操作
- **AND** Z轴优先抬起至安全高度后再停止

### Requirement: 视觉数据显示
系统 SHALL 在 UI 中显示视觉系统返回的原始数据和解析结果。

#### Scenario: 显示解析结果
- **WHEN** 视觉系统返回数据并解析成功
- **THEN** UI 显示原始返回字符串
- **AND** 显示解析后的键值对列表（如 offsetX=1.5, offsetY=-0.3）
- **AND** 数据按当前选中的 SiteFeature 行关联显示

### Requirement: Dot 点胶模式
系统 SHALL 支持 Dot（单点）点胶模式，在视觉数据解析后执行单点出胶动作。

#### Scenario: Dot 点胶执行
- **WHEN** SiteFeature 配置为 Dot 类型且视觉数据解析成功
- **THEN** 系统从视觉数据中提取针尖位置坐标
- **AND** 应用坐标系转换（视觉坐标→机械坐标）
- **AND** 移动轴到针尖位置
- **AND** 执行单点出胶动作（Z轴下降→出胶→Z轴抬起）

### Requirement: Arc 弧形点胶模式
系统 SHALL 支持 Arc（弧形）点胶模式，使用视觉返回的起点/中间点/终点生成二次贝塞尔弧线，离散化后多段插补走胶。

#### Scenario: Arc 弧形点胶执行
- **WHEN** SiteFeature 配置为 Arc 类型且视觉数据解析成功
- **THEN** 系统从视觉数据中提取起点(P₀)、中间点(P₁)、终点(P₂)坐标
- **AND** 构建二次贝塞尔曲线：B(t) = (1-t)²P₀ + 2(1-t)tP₁ + t²P₂
- **AND** 按 ArcSegments 参数将曲线离散化为 N 个插补点
- **AND** 将每个插补点从视觉坐标系转换到机械坐标系
- **AND** 使用 IMotionService 多段直线插补走胶

#### Scenario: 贝塞尔离散化
- **WHEN** 给定起点/中间点/终点和段数 N
- **THEN** 生成 N+1 个插补点，t 从 0 到 1 均匀采样
- **AND** 每个点的坐标为 B(i/N)，i = 0, 1, ..., N
- **AND** 默认段数 N = 20，可通过 ArcSegments 配置调整

#### Scenario: 坐标系转换
- **WHEN** 视觉坐标需要转换到机械坐标
- **THEN** 系统按以下步骤计算：
  1. 计算起点到相机中心的距离（向量）
  2. 相机中心到点胶针头的距离是固定值（NeedleOffset）
  3. 计算得到起始点真实坐标：真实坐标 = 视觉坐标 + 针尖偏移
  4. 根据贝塞尔弧线计算出其他点的集合
- **AND** 转换参数（CameraCenterX, CameraCenterY, NeedleOffsetX, NeedleOffsetY）从配方池全局变量读取
- **AND** 转换参数可通过 UI 配置并保存到配方池

### Requirement: 灵活配置（拒绝硬编码）
系统 SHALL 所有参数外部化配置，禁止源代码中硬编码任何数值。

#### Scenario: 参数外部化
- **WHEN** 系统运行时需要任何配置值
- **THEN** 该值从配方池（Positions字典/全局变量/WorkOrderData）中读取
- **AND** 不存在时使用合理默认值并在日志中警告
- **AND** 以下参数全部可配置：轴映射、位置名、速度、触发命令、TCP连接名、超时时间、点胶类型、贝塞尔段数、坐标系转换参数

### Requirement: UI 集成到 DispensingView 区域7
系统 SHALL 将 VisionCaptureView 集成到 DispensingView.xaml 的区域7（Auto Paths Generation - Type D）中。

#### Scenario: 区域7激活
- **WHEN** DispensingView 加载
- **THEN** 区域7的 Expander 展开后显示 VisionCaptureView
- **AND** 替换原有的已注释 AutoPathsGenerationView

## MODIFIED Requirements

### Requirement: AutoPathsGenerationView/ViewModel
原 AutoPathsGenerationView 和 AutoPathsGenerationViewModel 为模拟实现（硬编码示例数据、DialogService 弹窗模拟），现重构为 VisionCaptureView/VisionCaptureViewModel，实现完整的拍照+点胶工作流。原有 CapturedPoint 和 AutoPathPoint 模型类由新的 PhotoPositionRow 和视觉数据模型替代。

## REMOVED Requirements
无。所有现有功能保持兼容，仅替换模拟实现为真实实现。
