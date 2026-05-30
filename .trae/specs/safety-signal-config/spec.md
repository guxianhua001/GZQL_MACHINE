# 安全信号配置与设备通用设置 Spec

## Why
当前 `SystemStateService` 的安全门/光幕监控和蜂鸣器输出始终生效，不尊重 `AppSettings` 中的 `EnableSafetyGate`/`EnableBuzzer` 配置，导致 DeviceConfigView 中的设置形同虚设。需要打通配置与运行时的链路，并增加光幕独立控制和安全事件日志等通用设备设置。同时，将设备配置持久化从已废弃的 `Interfaces.DeviceConfigService` 迁移到 `Core` 项目的 `AppSettings + IAppSettingService + appsettings.json` 体系，消除对 `Interfaces` 项目的依赖。

## What Changes
- `AppSettings` 模型新增 `EnableGrating`（光幕启用）和 `EnableSafetyEventLog`（安全事件日志）属性，复用已有 `EnableSafetyGate`/`EnableBuzzer` 属性
- `DeviceConfigViewModel` 改为注入 `IAppSettingService` 进行配置读写，移除对 `Interfaces.DeviceConfigService` 的静态调用依赖
- `DeviceConfigChangedEvent` 从 `Interfaces.Events` 迁移到 `Core.Events`，使用 `AppSettings` 作为事件载荷
- `SystemStateService` 订阅 `DeviceConfigChangedEvent`，根据配置动态启用/禁用安全门检测、光幕检测、蜂鸣器输出
- `MotionSystemConfig.SignalConfig` 信号分组细化，支持 `SafetyGates` 和 `Grating` 两个独立分组
- `SystemStateService.InitializeMappings()` 分别加载安全门和光幕信号到独立列表
- 安全事件日志记录安全信号触发/恢复的详细信息
- `DeviceConfigView` UI 增加光幕开关和安全事件日志开关，安全相关设置归入"安全信号"分组

## Impact
- Affected specs: 无
- Affected code:
  - `Core/Configuration/AppSettings.cs` — 新增 EnableGrating、EnableSafetyEventLog 属性
  - `Core/Events/DeviceConfigChangedEvent.cs` — 新增事件类（从 Interfaces 迁移）
  - `MotionControl/Services/SystemStateService.cs` — 核心修改：订阅配置事件、条件检测
  - `ModuleCore/ViewModels/DeviceConfigViewModel.cs` — 改用 IAppSettingService，移除 DeviceConfigService 依赖
  - `ModuleCore/Views/DeviceConfigView.xaml` — UI 扩展
  - `Interfaces/Events/DeviceConfigChangedEvent.cs` — 标记废弃或删除
  - `Interfaces/Service/DeviceConfigService.cs` — 标记废弃或删除（后续清理）
  - 硬件配置 JSON — 信号分组需区分 SafetyGates / Grating

## ADDED Requirements

### Requirement: 设备配置持久化迁移至 AppSettings
系统 SHALL 将设备配置（EnableSafetyGate、EnableBuzzer、EnableGrating、EnableSafetyEventLog 等）存储在 `AppSettings` 中，通过 `IAppSettingService` 进行读写，持久化到 `appsettings.json`。

#### Scenario: 通过 IAppSettingService 读取设备配置
- **WHEN** `DeviceConfigViewModel` 加载配置
- **THEN** 从 `IAppSettingService.Settings` 读取 EnableSafetyGate、EnableBuzzer、EnableGrating、EnableSafetyEventLog 等属性

#### Scenario: 通过 IAppSettingService 保存设备配置
- **WHEN** 用户在 DeviceConfigView 中点击保存
- **THEN** `DeviceConfigViewModel` 将属性写入 `IAppSettingService.Settings` 并调用 `Save()`

#### Scenario: 配置向后兼容
- **WHEN** appsettings.json 中缺少新增的 EnableGrating 或 EnableSafetyEventLog 字段
- **THEN** 使用 C# 属性默认值（EnableGrating=true, EnableSafetyEventLog=true），不报错

### Requirement: DeviceConfigChangedEvent 迁移至 Core
系统 SHALL 将 `DeviceConfigChangedEvent` 从 `Interfaces.Events` 迁移到 `Core.Events`，事件载荷改为 `AppSettings` 类型。

#### Scenario: 配置保存后发布事件
- **WHEN** `DeviceConfigViewModel` 通过 `IAppSettingService.Save()` 保存配置
- **THEN** 发布 `DeviceConfigChangedEvent`，载荷为当前 `AppSettings` 实例

### Requirement: 安全门监控可配置
系统 SHALL 在 `SystemStateService.CheckSafetyAndEStop()` 中根据 `AppSettings.EnableSafetyGate` 决定是否检测安全门信号。

#### Scenario: 安全门启用时触发暂停
- **WHEN** `EnableSafetyGate` 为 true 且自动程序处于 RUNNING 状态
- **AND** 安全门信号被触发（DI 读取为 active）
- **THEN** 系统自动切换到 PAUSE 状态

#### Scenario: 安全门禁用时跳过检测
- **WHEN** `EnableSafetyGate` 为 false
- **AND** 安全门信号被触发
- **THEN** 系统不响应安全门信号，保持当前状态不变

#### Scenario: 安全门禁用时恢复运行不检查安全门
- **WHEN** `EnableSafetyGate` 为 false 且系统处于 PAUSE 状态
- **AND** 用户请求恢复运行
- **THEN** 系统不检查安全门信号，直接恢复到 RUNNING

### Requirement: 光幕监控可配置
系统 SHALL 在 `SystemStateService.CheckSafetyAndEStop()` 中根据 `AppSettings.EnableGrating` 决定是否检测光幕信号。光幕信号与安全门信号独立控制。

#### Scenario: 光幕启用时触发暂停
- **WHEN** `EnableGrating` 为 true 且自动程序处于 RUNNING 状态
- **AND** 光幕信号被触发
- **THEN** 系统自动切换到 PAUSE 状态

#### Scenario: 光幕禁用时跳过检测
- **WHEN** `EnableGrating` 为 false
- **AND** 光幕信号被触发
- **THEN** 系统不响应光幕信号，保持当前状态不变

### Requirement: 蜂鸣器输出可配置
系统 SHALL 在 `SystemStateService.WriteBuzzer()` 中根据 `AppSettings.EnableBuzzer` 决定是否输出蜂鸣器信号。

#### Scenario: 蜂鸣器启用时正常输出
- **WHEN** `EnableBuzzer` 为 true 且系统状态需要蜂鸣器报警（如 ESTOP、ALARM）
- **THEN** 蜂鸣器按脉冲模式正常输出

#### Scenario: 蜂鸣器禁用时静默
- **WHEN** `EnableBuzzer` 为 false
- **THEN** 蜂鸣器不输出任何信号，`WriteBuzzer()` 直接返回

### Requirement: 安全事件日志
系统 SHALL 在 `EnableSafetyEventLog` 为 true 时，记录安全信号的触发和恢复事件到日志系统。

#### Scenario: 安全信号触发时记录日志
- **WHEN** `EnableSafetyEventLog` 为 true
- **AND** 安全门或光幕信号从非激活变为激活
- **THEN** 系统记录 Warn 级别日志，包含信号名称、触发时间、当前状态

#### Scenario: 安全信号恢复时记录日志
- **WHEN** `EnableSafetyEventLog` 为 true
- **AND** 安全门或光幕信号从激活变为非激活
- **THEN** 系统记录 Info 级别日志，包含信号名称、恢复时间

#### Scenario: 安全事件日志禁用
- **WHEN** `EnableSafetyEventLog` 为 false
- **THEN** 系统仅记录基本的状态转换日志，不记录安全信号的详细变化

### Requirement: 配置变更实时生效
系统 SHALL 在 `AppSettings` 保存后，通过 `DeviceConfigChangedEvent` 事件通知 `SystemStateService`，使安全门/光幕/蜂鸣器配置变更立即生效，无需重启。

#### Scenario: 配置保存后立即生效
- **WHEN** 用户在 DeviceConfigView 中修改安全门/光幕/蜂鸣器设置并保存
- **THEN** `SystemStateService` 在下一次信号轮询周期中使用新配置

### Requirement: 信号分组细化
系统 SHALL 支持将安全信号分为 `SafetyGates`（安全门）和 `Grating`（光幕）两个独立分组，分别由 `EnableSafetyGate` 和 `EnableGrating` 控制。

#### Scenario: 信号分组加载
- **WHEN** `SystemStateService.InitializeMappings()` 加载信号配置
- **THEN** `SafetyGates` 分组的信号加载到 `_safetyGateSignals` 列表
- **AND** `Grating` 分组的信号加载到 `_gratingSignals` 列表
- **AND** 原有 `_safetySignals` 列表保留为两者的合并（用于 CanStart 等综合判断）

### Requirement: DeviceConfigView 安全信号分组 UI
系统 SHALL 在 DeviceConfigView 中将安全门、光幕、蜂鸣器、安全事件日志归入"安全信号"视觉分组，使用 Expander 或 GroupBox 组织。

#### Scenario: 安全信号设置区域展示
- **WHEN** 用户打开 DeviceConfigView
- **THEN** 可看到"安全信号"分组，包含安全门开关、光幕开关、蜂鸣器开关、安全事件日志开关

## MODIFIED Requirements

### Requirement: AppSettings 模型
新增 `EnableGrating`（bool，默认 true）、`EnableSafetyEventLog`（bool，默认 true）、`EnableSafetyGate`（bool，默认 true）、`EnableBuzzer`（bool，默认 false）属性。其中 EnableSafetyGate 和 EnableBuzzer 原先在 DeviceConfig 中，现统一迁移到 AppSettings。

### Requirement: SystemStateService 安全检测逻辑
原 `CheckSafetyAndEStop()` 方法统一检测 `_safetySignals`，现修改为分别检测 `_safetyGateSignals` 和 `_gratingSignals`，并根据对应配置决定是否跳过。

### Requirement: SystemStateService 蜂鸣器输出逻辑
原 `WriteBuzzer()` 方法无条件输出，现修改为先检查 `_buzzerEnabled` 配置。

### Requirement: SystemStateService CanStart 条件
原 `CanStart` 同时检查 `_safetySignals` 和 `_estopSignals`，现修改为根据 `EnableSafetyGate` 和 `EnableGrating` 分别判断安全信号是否需要满足。

### Requirement: SystemStateService RequestResume 条件
原 `RequestResume()` 检查 `_safetySignals` 是否仍然激活，现修改为根据配置分别检查安全门和光幕信号。

### Requirement: DeviceConfigViewModel
改为注入 `IAppSettingService` 进行配置读写，移除对 `Interfaces.DeviceConfigService` 的静态调用依赖。新增 `EnableGrating` 和 `EnableSafetyEventLog` 绑定属性。

### Requirement: DeviceConfigView XAML
新增光幕开关和安全事件日志开关 CheckBox，将安全相关设置归入"安全信号"分组区域。

## REMOVED Requirements

### Requirement: Interfaces.DeviceConfigService 废弃与清理
**Reason**: `Interfaces` 项目已废弃，设备配置持久化统一使用 `Core.IAppSettingService + AppSettings + appsettings.json`
**Migration**: `DeviceConfigViewModel` 改用 `IAppSettingService`；`DeviceConfigChangedEvent` 迁移到 `Core.Events`；`DeviceConfig` 中的属性合并到 `AppSettings`
**清理**: 移除 `DeviceConfigService` 中未使用的方法：`ChangeConfigDirectory()`、`CleanupExpiredDataAsync()` 及其私有依赖 `CleanDirectoryAsync()`、`GetFilesAsync()`
