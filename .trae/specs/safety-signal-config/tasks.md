# Tasks

- [x] Task 1: 扩展 AppSettings 模型，新增设备配置属性
  - [x] SubTask 1.1: 在 AppSettings 类中新增 EnableSafetyGate(bool, 默认true)、EnableBuzzer(bool, 默认false)、EnableGrating(bool, 默认true)、EnableSafetyEventLog(bool, 默认true) 属性
  - [x] SubTask 1.2: 验证 JSON 反序列化兼容性（新属性有 C# 默认值，旧文件缺少字段不报错）

- [x] Task 2: 迁移 DeviceConfigChangedEvent 到 Core.Events
  - [x] SubTask 2.1: 在 Core/Events/ 下新建 DeviceConfigChangedEvent 类，载荷类型为 AppSettings
  - [x] SubTask 2.2: 更新所有引用，从 Interfaces.Events.DeviceConfigChangedEvent 改为 Core.Events.DeviceConfigChangedEvent

- [x] Task 3: 重构 DeviceConfigViewModel，改用 IAppSettingService
  - [x] SubTask 3.1: 构造函数注入 IAppSettingService，移除 DeviceConfigService 静态调用
  - [x] SubTask 3.2: 新增 EnableGrating 和 EnableSafetyEventLog 绑定属性
  - [x] SubTask 3.3: LoadDeviceConfig() 改为从 IAppSettingService.Settings 读取
  - [x] SubTask 3.4: ExecuteSave() 改为写入 IAppSettingService.Settings 并调用 Save()，然后发布 Core.Events.DeviceConfigChangedEvent
  - [x] SubTask 3.5: ExecuteLoadDefault() 更新新属性默认值
  - [x] SubTask 3.6: 移除对 Interfaces.Services.DeviceConfigService 的 using 引用

- [x] Task 4: 更新 DeviceConfigView XAML，增加安全信号分组 UI
  - [x] SubTask 4.1: 将安全门、蜂鸣器 CheckBox 归入"安全信号" Expander 分组
  - [x] SubTask 4.2: 新增光幕启用 CheckBox（绑定 EnableGrating）
  - [x] SubTask 4.3: 新增安全事件日志 CheckBox（绑定 EnableSafetyEventLog）
  - [x] SubTask 4.4: 添加对应的 Lang 翻译键引用

- [x] Task 5: 修改 SystemStateService 核心逻辑，打通配置与运行时
  - [x] SubTask 5.1: 新增 _safetyGateEnabled、_gratingEnabled、_buzzerEnabled、_safetyEventLogEnabled 私有字段
  - [x] SubTask 5.2: 构造函数中从 IAppSettingService.Settings 初始化字段
  - [x] SubTask 5.3: 订阅 Core.Events.DeviceConfigChangedEvent，更新字段值
  - [x] SubTask 5.4: 修改 InitializeMappings()，将 SafetyGates 加载到 _safetyGateSignals，Grating 加载到 _gratingSignals，合并到 _safetySignals
  - [x] SubTask 5.5: 修改 CheckSafetyAndEStop()，根据 _safetyGateEnabled 和 _gratingEnabled 分别检测
  - [x] SubTask 5.6: 修改 WriteBuzzer()，根据 _buzzerEnabled 决定是否输出
  - [x] SubTask 5.7: 修改 CanStart 属性，根据配置分别判断安全信号条件
  - [x] SubTask 5.8: 修改 RequestResume()，根据配置分别检查安全门和光幕信号
  - [x] SubTask 5.9: 新增安全事件日志记录逻辑（信号触发/恢复时记录详细日志）

- [x] Task 6: 更新语言资源文件
  - [x] SubTask 6.1: 在 Strings.zh-CN.xaml 中添加新翻译键（光幕、安全事件日志、安全信号分组等）
  - [x] SubTask 6.2: 在 Strings.en-US.xaml 中添加对应英文翻译

- [x] Task 7: 更新硬件配置 JSON 示例
  - [x] SubTask 7.1: 确认/更新信号分组名称，区分 SafetyGates 和 Grating

- [x] Task 8: 清理 DeviceConfigService 未使用方法及废弃引用
  - [x] SubTask 8.1: 移除 DeviceConfigService 中未使用的方法：ChangeConfigDirectory()、CleanupExpiredDataAsync()、CleanDirectoryAsync()、GetFilesAsync()
  - [x] SubTask 8.2: 标记或删除 Interfaces.Events.DeviceConfigChangedEvent（已被 Core 版本替代）
  - [x] SubTask 8.3: 标记或删除 Interfaces.Service.DeviceConfigService 中被 AppSettings 替代的部分

# Task Dependencies
- [Task 2] depends on [Task 1]
- [Task 3] depends on [Task 1] and [Task 2]
- [Task 4] depends on [Task 3]
- [Task 5] depends on [Task 1] and [Task 2]
- [Task 6] depends on [Task 4]
- [Task 7] is independent
- [Task 8] depends on [Task 3] and [Task 5]
