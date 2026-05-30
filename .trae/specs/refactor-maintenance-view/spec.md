# MaintenanceView 模块重构开发规格

## Why
双针头点胶系统缺少统一的维护功能界面，针头与相机中心标定、换针校准、校准验证等核心维护操作分散或缺失，需重构 MaintenanceView 模块，提供完整的维护操作界面，确保点胶位置精度和高度一致性。

## What Changes
- 新增 MaintenanceView 主视图，作为维护模块入口，包含 TabControl 导航至三个子模块
- 新增 NeedleCameraAlignmentView 子视图（针头与相机中心标定），重构自旧项目 NeedleCalibrationView
- 新增 NeedleAlignerView 子视图（换针与针头校准），重构自旧项目 NeedleAlignerView
- 新增 NeedleCalibrationVerifyView 子视图（针头校准验证），提供校准后验证测试界面
- 新增对应 ViewModel 实现，采用 MVVM 模式，通过 DI 注入依赖
- 实现 INeedleService 接口并注册到 DI 容器
- 注册 NeedleCompensationManager 到 DI 容器
- 在 PrimModel 中注册所有新增视图和服务的 DI 映射
- 在导航系统中添加维护模块入口
- 补充多语言资源 Key（zh-CN / en-US）

## Impact
- Affected specs: 维护模块、针头校准、相机标定、补偿管理
- Affected code:
  - `Module/Controls/Maintenance/` (新增目录)
  - `Module/PrimModel.cs` (DI注册与导航)
  - `Core/Abstraction/INeedleService.cs` (已有接口，需实现)
  - `Core/Services/NeedleCompensationManager.cs` (需注册DI)
  - `MainApp/Languages/Strings.zh-CN.xaml` (多语言)
  - `MainApp/Languages/Strings.en-US.xaml` (多语言)

---

## ADDED Requirements

### Requirement: MaintenanceView 主视图
系统 SHALL 提供 MaintenanceView 作为维护模块的主入口视图，包含 TabControl 导航至三个子模块。

#### Scenario: 用户进入维护模块
- **WHEN** 用户在导航栏点击"维护"入口
- **THEN** 系统显示 MaintenanceView 主视图，默认选中"针头与相机中心标定"Tab
- **AND** TabControl 包含三个 Tab 项：针头与相机中心标定、换针与针头校准、针头校准验证

#### Scenario: Tab 切换
- **WHEN** 用户点击不同 Tab
- **THEN** 系统切换到对应子视图，保持各子视图状态独立

---

### Requirement: 针头与相机中心标定模块 (NeedleCameraAlignmentView)
系统 SHALL 提供针头与相机中心标定功能，用于建立相机视觉引导点胶的坐标映射关系。

#### Scenario: 系统选择
- **WHEN** 用户进入针头与相机中心标定模块
- **THEN** 界面顶部显示双系统选择（对针系统1 / 对针系统2），默认选中系统1
- **AND** 切换系统时自动加载对应系统的标定参数

#### Scenario: 示教相机中心坐标
- **WHEN** 用户点击"示教相机中心"按钮
- **THEN** 系统通过 IPositionMotionController 读取当前 DispX 和 GantryY 轴坐标，填入相机中心 X/Y 字段
- **AND** 自动计算相机与针尖距离偏差 ΔX/ΔY

#### Scenario: 示教针尖位置
- **WHEN** 用户点击"示教针尖位置"按钮
- **THEN** 系统通过 IPositionMotionController 读取当前 DispX、GantryY 和 DispZ 轴坐标，填入针尖 X/Y/Z 字段
- **AND** 自动计算 ΔX/ΔY 偏差值

#### Scenario: 示教针尖高度
- **WHEN** 用户点击"示教针尖高度"按钮
- **THEN** 系统通过 IPositionMotionController 读取当前 DispZ 轴坐标，填入针尖高度字段

#### Scenario: 计算当前针头高度
- **WHEN** 用户点击"计算针尖高度"按钮
- **THEN** 系统根据公式 `CurrentNeedleHeight = NeedleTipZ - (TargetPlaneZ - BasePlaneZ) + CompensationZ` 计算并显示当前针头高度
- **AND** 若基准面高度或目标面高度未设置，显示警告提示

#### Scenario: 手动补偿输入
- **WHEN** 用户在补偿 X/Y/Z 输入框中输入值
- **THEN** 系统允许手动微调补偿值，用于校针器校准后仍存在误差的情况

#### Scenario: 参数保存与加载
- **WHEN** 用户点击"保存参数"按钮
- **THEN** 系统将当前标定参数持久化到 Config/Calibration/ 目录，按系统编号区分存储
- **WHEN** 用户点击"加载参数"按钮
- **THEN** 系统从文件加载对应系统的标定参数并更新界面

#### Scenario: 状态反馈
- **WHEN** 任何操作执行完成或失败
- **THEN** 界面底部状态栏显示操作结果消息，使用颜色区分成功(绿)/警告(橙)/错误(红)/信息(蓝)

---

### Requirement: 换针与针头校准模块 (NeedleAlignerView)
系统 SHALL 提供换针与针头校准功能，通过4点寻边定位和接触式标定获取针头XYZ补偿数据。

#### Scenario: 校准参数设置
- **WHEN** 用户进入换针与针头校准模块
- **THEN** 界面左侧显示参数设置区域，包含：
  - 四个搜索点坐标 (Point1~4, X/Y)
  - 基准坐标 (ReferenceXYZ, X/Y/Z)
  - 运动参数：搜索范围、Z方向搜索次数、搜索速度、精细搜索速度、针头基准高度、3D相机基准高度

#### Scenario: 执行校准流程
- **WHEN** 用户点击"开始校准"按钮
- **THEN** 系统执行4点寻边定位流程确定针头XY方向中心坐标
- **AND** 通过接触式标定方法获取Z方向高度数据
- **AND** 进度条实时更新校准进度
- **AND** 状态文本实时显示当前校准步骤
- **AND** 校准期间"开始校准"按钮禁用，"停止校准"按钮启用

#### Scenario: 校准完成
- **WHEN** 校准流程成功完成
- **THEN** 系统使用 NeedleCompensationManager 计算增量补偿
- **AND** 显示当前测量值(XYZ)、补偿值(XYZ)、基准补偿、增量补偿
- **AND** 自动保存补偿管理器状态到参数
- **AND** 检查补偿值突变（超过1mm时发出警告）

#### Scenario: 停止校准
- **WHEN** 用户在校准过程中点击"停止校准"按钮
- **THEN** 系统安全停止校准流程，运动轴减速停止

#### Scenario: 补偿管理
- **WHEN** 用户点击"重置所有补偿"按钮
- **THEN** 系统弹出确认对话框，确认后将基准和增量补偿全部清零
- **WHEN** 用户点击"重置基准补偿"按钮
- **THEN** 系统将当前增量补偿合并到基准补偿，然后清零增量补偿（总补偿不变）
- **WHEN** 用户点击"重置增量补偿"按钮
- **THEN** 系统清零增量补偿，保留基准补偿
- **WHEN** 用户点击"查看补偿历史"按钮
- **THEN** 系统在日志区域显示补偿变更历史记录

#### Scenario: 应用补偿
- **WHEN** 用户点击"应用补偿"按钮
- **THEN** 系统将 NeedleCompensationManager 的总补偿值应用到运动控制系统

#### Scenario: 参数保存与加载
- **WHEN** 用户点击"保存参数"按钮
- **THEN** 系统将校准参数和补偿管理器状态持久化
- **WHEN** 用户点击"加载参数"按钮
- **THEN** 系统从存储加载参数并初始化补偿管理器

#### Scenario: 校准日志
- **WHEN** 校准流程执行中
- **THEN** 日志区域实时显示校准步骤信息
- **AND** 日志使用 ConcurrentQueue + Timer 批量更新机制，避免UI卡顿
- **AND** 日志条目限制100条，超出自动清理旧条目

---

### Requirement: 针头校准验证模块 (NeedleCalibrationVerifyView)
系统 SHALL 提供针头校准验证功能，用于确认针头补偿数据的准确性和有效性。

#### Scenario: 执行验证流程
- **WHEN** 用户点击"执行验证"按钮
- **THEN** 系统执行以下验证步骤：
  1. 移动到校准器上方，执行4点寻边获取当前针头XY中心
  2. 接触式测量获取当前Z高度
  3. 将测量值与基准值对比，计算偏差
- **AND** 进度条和状态文本实时更新

#### Scenario: 验证结果判定
- **WHEN** 验证流程完成
- **THEN** 系统根据偏差值判定验证结果：
  - 偏差 ≤ 0.05mm：通过（绿色）
  - 0.05mm < 偏差 ≤ 0.15mm：警告（橙色），建议重新校准
  - 偏差 > 0.15mm：失败（红色），必须重新校准
- **AND** 显示各轴(XYZ)的偏差值和判定结果

#### Scenario: 验证报告
- **WHEN** 验证完成
- **THEN** 系统生成验证报告，包含：验证时间、操作员、系统编号、各轴偏差值、判定结果
- **AND** 验证报告可保存到文件

---

### Requirement: INeedleService 实现
系统 SHALL 实现 INeedleService 接口，提供针头使用计数管理功能。

#### Scenario: 针头使用计数
- **WHEN** 点胶操作执行
- **THEN** 系统自动递增对应针头的使用计数
- **WHEN** 使用计数达到最大值
- **THEN** 系统发出针头寿命警告

#### Scenario: 针头重置
- **WHEN** 用户执行换针操作
- **THEN** 系统重置对应针头的使用计数

---

### Requirement: 多语言支持
系统 SHALL 为所有新增UI文本提供中英文双语支持。

#### Scenario: 语言切换
- **WHEN** 用户切换系统语言
- **THEN** 维护模块所有界面文本自动切换到对应语言
- **AND** 使用 `{lang:Lang Key}` 标记扩展实现XAML绑定
- **AND** ViewModel中使用 `ILocalizationService` 获取本地化文本

---

### Requirement: 运动控制安全性
系统 SHALL 确保校准和标定流程中的运动控制安全性。

#### Scenario: 急停支持
- **WHEN** 校准或标定流程执行中用户触发急停
- **THEN** 系统立即停止所有运动轴
- **AND** 校准流程安全终止，状态恢复为就绪

#### Scenario: 防撞保护
- **WHEN** 执行水平移动前
- **THEN** 系统先抬升Z轴到安全高度，再执行水平移动
- **AND** 移动完成后再下降Z轴到目标位置

#### Scenario: 操作确认
- **WHEN** 用户执行关键操作（重置补偿、应用补偿等）
- **THEN** 系统弹出确认对话框，用户确认后才执行操作

---

### Requirement: UI 设计规范
系统 SHALL 遵循项目现有 UI 设计规范，保持视觉一致性。

#### Scenario: MaterialDesign 卡片布局
- **WHEN** 构建维护模块界面
- **THEN** 使用 MaterialDesign Card 组件作为内容容器
- **AND** 使用 PackIcon 作为按钮图标，不使用 emoji
- **AND** 界面布局参考 LoadUnloadView 的三栏卡片式设计

#### Scenario: 工业设备操作习惯
- **WHEN** 设计操作流程
- **THEN** 关键操作提供确认机制
- **AND** 错误状态有明确颜色提示（红/橙/绿/蓝）
- **AND** 操作步骤有清晰指引

## MODIFIED Requirements

### Requirement: DI 注册扩展
PrimModel.RegisterTypes() 中 SHALL 新增以下注册：
- `INeedleService` → `NeedleService` (Singleton)
- `NeedleCompensationManager` (Singleton)
- `MaintenanceView` / `MaintenanceViewModel` (Navigation)
- `NeedleCameraAlignmentView` / `NeedleCameraAlignmentViewModel` (Navigation)
- `NeedleAlignerView` / `NeedleAlignerViewModel` (Navigation)
- `NeedleCalibrationVerifyView` / `NeedleCalibrationVerifyViewModel` (Navigation)

### Requirement: 导航系统扩展
PrimModel.OnInitialized() 中 SHALL 在导航列表中添加维护模块入口，使用 `WrenchOutline` 图标，DisplayNameKey 为 `Nav_Maintenance`，UserLevel 为 1。

## REMOVED Requirements

无移除需求。本次为纯新增功能，不影响现有模块。
