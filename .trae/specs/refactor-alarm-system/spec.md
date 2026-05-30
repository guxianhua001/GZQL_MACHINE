# 报警系统重构 Spec

## Why
现有报警系统功能简陋（仅3级分类、无确认/复位流程、无阈值配置、无防抖机制、无实时弹窗通知），且代码分散在 Interfaces/ModuleCore/MainApp 多个项目中，耦合度高。需要完全移除旧代码，新建独立模块项目，基于 SQLite + EF Core 实现工业4级报警系统，支持完整生命周期管理。

## What Changes
- **新建 AlarmModule 项目**：在 Modules 文件夹中创建独立的 Prism 模块项目
- **完全移除旧报警代码**：删除 Interfaces/Alarm/ 下所有文件、AlarmService.cs、AlarmReportingView/ViewModel、AlarmDbContext 及迁移
- **新数据库**：从 SQL Server 迁移到 SQLite（本地文件数据库，无需外部数据库服务器）
- **新 ORM**：EF Core 8 + SQLite 提供程序（注：Prisma ORM 为 JavaScript/TypeScript 专用，.NET 项目使用 EF Core）
- **新报警模型**：实现12项必填字段 + 工业4级分类 + 完整生命周期状态机
- **新 UI**：实时报警列表 + 历史查询 + 阈值配置 + 数据导出
- **单行代码触发**：设计 `IAlarmService.TriggerAlarm()` 抽象层

## Impact
- Affected specs: 无直接关联
- Affected code:
  - `Interfaces/Alarm/` — 全部删除（PersistentAlarm, AlarmDbContext, AlarmRepository 等）
  - `Interfaces/Service/AlarmService.cs` — 删除
  - `MainApp/Migrations/` — 删除 EF 迁移文件
  - `MainApp/AlarmDbContextDesignFactory.cs` — 删除
  - `MainApp/App.xaml.cs` — 移除旧报警 DI 注册，添加新模块注册
  - `ModuleCore/Views/AlarmReportingView.xaml` — 删除
  - `ModuleCore/ViewModels/AlarmReportingViewModel.cs` — 删除
  - `Module/PrimModel.cs` — 更新报警导航菜单注册
  - `MotionControl/` — 更新报警事件引用

## ADDED Requirements

### Requirement: 报警数据模型（AlarmRecord）
系统 SHALL 提供 `AlarmRecord` 实体模型，包含以下字段：

| 字段 | 类型 | 约束 | 说明 |
|------|------|------|------|
| Id | long | PK, auto-increment | 报警ID |
| AlarmTime | DateTime | NOT NULL, 精度毫秒 | 报警时间 |
| AlarmLevel | AlarmLevel enum | NOT NULL | 报警等级(1-4) |
| AlarmCode | string(50) | NOT NULL | 故障代码 |
| AlarmSource | string(100) | NOT NULL | 报警源(设备/轴/模块名) |
| AlarmType | AlarmType enum | NOT NULL | 报警类型 |
| Description | string(500) | NOT NULL | 报警描述 |
| TriggerValue | double? | nullable | 实时触发值 |
| ThresholdValue | double? | nullable | 阈值 |
| Status | AlarmStatus enum | NOT NULL | 报警状态 |
| ConfirmedBy | string(50) | nullable | 确认人 |
| ConfirmedTime | DateTime? | nullable | 确认时间 |
| ResetBy | string(50) | nullable | 复位人 |
| ResetTime | DateTime? | nullable | 复位时间 |
| ProcessingNotes | string(1000) | nullable | 处理备注 |
| SuppressedUntil | DateTime? | nullable | 抑制截止时间 |

枚举定义：
- `AlarmLevel`: Emergency=1, Serious=2, General=3, Prompt=4
- `AlarmType`: HardwareFault=1, ParameterOutOfLimit=2, CommunicationError=3, ProcessError=4
- `AlarmStatus`: Unconfirmed=1, Confirmed=2, Reset=3, Eliminated=4

### Requirement: 工业4级报警等级系统
系统 SHALL 实现以下4级报警等级及对应系统行为：

| 等级 | 名称 | 颜色 | 系统行为 |
|------|------|------|---------|
| Level 1 | 紧急停机 | 红色 | 立即触发全局急停，持续蜂鸣 |
| Level 2 | 严重故障 | 橙色 | 暂停当前任务，间歇蜂鸣 |
| Level 3 | 一般报警 | 黄色 | 单次提示音，可继续生产 |
| Level 4 | 提示预警 | 蓝色 | 仅视觉提示，无蜂鸣 |

### Requirement: 报警生命周期管理
系统 SHALL 实现完整的报警状态机：

```
Unconfirmed → Confirmed → Reset → Eliminated
     ↓             ↓
 (可批量确认)   (可批量复位)
```

状态转换规则：
- **触发**：新报警创建时状态为 Unconfirmed
- **确认**：记录确认人和确认时间，状态变为 Confirmed
- **复位**：记录复位人和复位时间，状态变为 Reset
- **消除**：故障条件消除后自动或手动变为 Eliminated
- **批量操作**：支持"确认全部"和"复位全部"

### Requirement: 单行代码报警触发（IAlarmService）
系统 SHALL 提供简洁的报警触发抽象层，设备/模块只需一行代码即可触发报警并持久化：

```csharp
// 单行触发报警
await _alarmService.TriggerAlarmAsync("AXIS_Z_FAULT", AlarmLevel.Serious, "Z轴伺服报警");

// 带阈值的参数超限报警
await _alarmService.TriggerAlarmAsync("TEMP_HIGH", AlarmLevel.General, "温度超限",
    triggerValue: 85.3, thresholdValue: 80.0);
```

接口定义：
```csharp
public interface IAlarmService
{
    Task TriggerAlarmAsync(string alarmCode, AlarmLevel level, string description,
        string source = null, AlarmType type = AlarmType.HardwareFault,
        double? triggerValue = null, double? thresholdValue = null);

    Task ConfirmAsync(long alarmId, string confirmedBy);
    Task ResetAsync(long alarmId, string resetBy);
    Task EliminateAsync(long alarmId);
    Task ConfirmAllAsync(string confirmedBy);
    Task ResetAllAsync(string resetBy);

    IObservable<AlarmRecord> AlarmTriggered { get; }
    ObservableCollection<AlarmRecord> ActiveAlarms { get; }
    Task<PagedResult<AlarmRecord>> QueryAsync(AlarmQueryParams parameters);
    Task ExportToExcelAsync(string filePath, AlarmQueryParams parameters);
}
```

### Requirement: 实时报警弹窗通知
系统 SHALL 在报警触发时弹出通知窗口，根据报警等级显示不同灯光和蜂鸣：
- Level 1/2：模态弹窗，必须人工确认后关闭
- Level 3/4：非模态 Toast 通知，自动消失

### Requirement: 报警防抖机制
系统 SHALL 实现重复报警抑制：相同 AlarmCode + AlarmSource 的报警在可配置时间窗口内（默认60秒）不重复触发，仅更新已有记录的时间戳。

### Requirement: 阈值配置界面
系统 SHALL 提供阈值配置界面，允许用户在不修改代码的情况下设置报警阈值：
- 报警代码 → 阈值 → 报警等级 → 抑制时间窗口
- 配置持久化到 SQLite 数据库

### Requirement: 报警查询与过滤
系统 SHALL 提供多条件过滤查询：
- 时间范围
- 报警等级
- 报警源
- 报警状态
- 故障类型
- 分页显示

### Requirement: Excel 数据导出
系统 SHALL 支持将报警数据导出为 Excel 文件（.xlsx），支持按查询条件导出。

### Requirement: 报警统计与趋势分析
系统 SHALL 提供报警统计面板：
- 按等级分布统计
- 按报警源频率排名
- 按时间段趋势图
- 高频报警 TOP10

### Requirement: SQLite 数据持久化
系统 SHALL 使用 SQLite 本地文件数据库存储报警数据：
- 数据库文件路径：`Config/alarms.db`
- 自动创建数据库和表结构（Code First）
- 历史数据永久保存，重启不丢失
- 支持自动归档（超过配置天数的数据归档）

## MODIFIED Requirements

### Requirement: 报警模块架构
原报警代码分散在 Interfaces/ModuleCore/MainApp 三个项目中。现重构为独立 Prism 模块项目 `AlarmModule`，位于 Modules 文件夹中，所有报警相关代码集中管理。

## REMOVED Requirements

### Requirement: 旧报警系统
**Reason**: 功能简陋，无法满足工业4级报警需求
**Migration**: 完全替换为新 AlarmModule，所有旧代码删除

- `Interfaces/Alarm/PersistentAlarm.cs` — 删除
- `Interfaces/Alarm/XAlarmEventArgs.cs` — 删除
- `Interfaces/Alarm/AlarmQueryParameters.cs` — 删除
- `Interfaces/Alarm/AlarmDbContext.cs` — 删除
- `Interfaces/Alarm/AlarmDbContextFactory.cs` — 删除
- `Interfaces/Alarm/DbContextConfigurator.cs` — 删除
- `Interfaces/Alarm/IAlarmRepository.cs` — 删除
- `Interfaces/Alarm/AlarmRepository.cs` — 删除
- `Interfaces/Service/AlarmService.cs` — 删除
- `MainApp/Migrations/` — 删除
- `MainApp/AlarmDbContextDesignFactory.cs` — 删除
- `ModuleCore/Views/AlarmReportingView.xaml/.cs` — 删除
- `ModuleCore/ViewModels/AlarmReportingViewModel.cs` — 删除
- `MainApp/App.xaml.cs` 中旧报警 DI 注册 — 移除
- `Interfaces.csproj` 中 EF Core SQL Server 包引用 — 移除
