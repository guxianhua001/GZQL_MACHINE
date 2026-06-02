# Tasks

- [x] Task 1: 创建安全区域基础设施（接口、模型、事件、异常）
  - [x] 1.1 创建 `SafetyZoneConfig` 配置模型（MotionControl/Models/SafetyZoneConfig.cs）
  - [x] 1.2 创建 `ISafetyZoneMonitor` 接口（MotionControl/Interfaces/ISafetyZoneMonitor.cs）
  - [x] 1.3 创建 `SafetyViolationEvent` 事件类（MotionControl/Events/SafetyViolationEvent.cs）
  - [x] 1.4 创建 `SafetyViolationException` 异常类（MotionControl/Exceptions/SafetyViolationException.cs）

- [x] Task 2: 实现 SafetyZoneMonitor 核心逻辑
  - [x] 2.1 创建 `SafetyZoneMonitor` 实现（MotionControl/Services/SafetyZoneMonitor.cs），注入 IMotionService 读取轴位置
  - [x] 2.2 实现互锁规则：Z₁ 高度 < SafeHeight → X 轴负向锁定
  - [x] 2.3 实现互锁规则：Z₁ 低 + X 在危险区 → Y 轴全向锁定
  - [x] 2.4 实现 CheckMoveAllowed / CheckInterpolationMoveAllowed / IsInDangerZone / GetSafetyStatus 方法
  - [x] 2.5 实现 Enable 开关，禁用时所有检查直接返回 true

- [x] Task 3: 集成到 MotionService
  - [x] 3.1 MotionService 构造函数注入 ISafetyZoneMonitor
  - [x] 3.2 MoveAbsAsync 增加安全检查：先 CheckMoveAllowed → 通过后执行 → 不通过抛异常+发布事件
  - [x] 3.3 MoveRelAsync 增加安全检查：计算目标位置后同上
  - [x] 3.4 MoveLineAbsAsync 增加安全检查：调用 CheckInterpolationMoveAllowed

- [x] Task 4: 集成到 SafeJogBehavior
  - [x] 4.1 SafeJogBehavior 注入 ISafetyZoneMonitor（通过 Attached Property）
  - [x] 4.2 StartJog 中增加安全检查：判断 Jog 方向的目标位置是否允许
  - [x] 4.3 不允许时记录 Warn 日志，不启动 Jog

- [x] Task 5: 安全区域配置 UI
  - [x] 5.1 创建 `SafetyZoneConfigViewModel`（Module/Controls/Maintenance/SafetyZoneConfigViewModel.cs）
    - 绑定 SafetyZoneConfig 各属性（SafeHeightZ1, DangerZoneXMin/Max, DangerZoneYMin/Max, Enabled）
    - 订阅 ISafetyZoneMonitor.GetSafetyStatus() 实时更新轴位置显示
    - 订阅 SafetyViolationEvent 显示报警提示条
    - SaveCommand 保存配置到 JSON 并通知 SafetyZoneMonitor 刷新参数
  - [x] 5.2 创建 `SafetyZoneConfigView.xaml`（Module/Controls/Maintenance/SafetyZoneConfigView.xaml）
    - 左侧：参数编辑区（GroupBox，DecimalUpDown 输入各阈值 + 启用开关 + 保存按钮）
    - 右侧：2D 区域可视化图（Canvas 绘制坐标轴、安全区绿框、危险区红框、当前位置标记点、Z₁高度指示器）
    - 底部：违规报警提示条（红色背景，默认 Collapsed，收到事件时 Visible）
    - 参考 AxisSettingView 样式：GroupBox 分组 + DecimalUpDown 参数输入

- [x] Task 6: DI 注册与编译验证
  - [x] 6.1 MotionControlModule 注册 ISafetyZoneMonitor → SafetyZoneMonitor（Singleton）
  - [x] 6.2 编译 MotionControl 项目，确保无错误
  - [x] 6.3 编译主项目，确保无错误

# Task Dependencies
- [Task 2] depends on [Task 1]
- [Task 3] depends on [Task 2]
- [Task 4] depends on [Task 2]
- [Task 5] depends on [Task 1, Task 2]
- [Task 6] depends on [Task 3, Task 4, Task 5]
