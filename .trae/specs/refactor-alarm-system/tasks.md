# Tasks

- [x] Task 1: 创建 AlarmModule 项目结构
  - [x] SubTask 1.1: 在 Modules 文件夹中创建 AlarmModule 类库项目（net9.0-windows），添加 Prism.DryIoc、EF Core SQLite、ClosedXML 等 NuGet 引用
  - [x] SubTask 1.2: 创建 AlarmModule.cs Prism 模块入口，实现 IModule 接口
  - [x] SubTask 1.3: 将 AlarmModule 添加到解决方案文件和 App.xaml.cs 模块目录

- [x] Task 2: 实现报警数据模型和数据库访问层
  - [x] SubTask 2.1: 创建枚举类型（AlarmLevel, AlarmType, AlarmStatus）
  - [x] SubTask 2.2: 创建 AlarmRecord 实体模型（含所有12+必填字段）
  - [x] SubTask 2.3: 创建 AlarmThresholdConfig 阈值配置实体
  - [x] SubTask 2.4: 创建 AlarmDbContext（EF Core + SQLite），配置表映射和索引
  - [x] SubTask 2.5: 创建 IAlarmRepository 接口和 AlarmRepository 实现（CRUD + 分页查询 + 批量操作）
  - [x] SubTask 2.6: 创建 AlarmQueryParams 和 PagedResult<T> 查询模型

- [x] Task 3: 实现 IAlarmService 核心服务
  - [x] SubTask 3.1: 定义 IAlarmService 接口（TriggerAlarmAsync, ConfirmAsync, ResetAsync, EliminateAsync, ConfirmAllAsync, ResetAllAsync, QueryAsync, ExportToExcelAsync, AlarmTriggered, ActiveAlarms）
  - [x] SubTask 3.2: 实现 AlarmService — 报警触发（含防抖机制：相同 Code+Source 在配置时间窗口内不重复）
  - [x] SubTask 3.3: 实现 AlarmService — 生命周期状态转换（确认/复位/消除）
  - [x] SubTask 3.4: 实现 AlarmService — 实时报警集合（ActiveAlarms: ObservableCollection<AlarmRecord>）
  - [x] SubTask 3.5: 实现 AlarmService — 报警触发事件（AlarmTriggered: IObservable<AlarmRecord>，通过 Prism EventAggregator 发布）
  - [x] SubTask 3.6: 实现 AlarmService — 分页查询（多条件过滤：时间/等级/源/状态/类型）
  - [x] SubTask 3.7: 实现 AlarmService — Excel 导出（ClosedXML）

- [x] Task 4: 实现报警弹窗通知服务
  - [x] SubTask 4.1: 创建 IAlarmNotificationService 接口
  - [x] SubTask 4.2: 实现 AlarmNotificationService — Level 1/2 模态弹窗 + Level 3/4 Toast 通知
  - [x] SubTask 4.3: 订阅 AlarmTriggered 事件，根据等级触发不同灯光/蜂鸣

- [x] Task 5: 实现报警 UI 视图
  - [x] SubTask 5.1: 创建 AlarmListView.xaml — 实时报警列表（未确认计数徽章、颜色编码、批量操作按钮）
  - [x] SubTask 5.2: 创建 AlarmListViewModel — 绑定 ActiveAlarms、确认/复位命令
  - [x] SubTask 5.3: 创建 AlarmHistoryView.xaml — 历史报警查询（多条件过滤、分页、Excel导出）
  - [x] SubTask 5.4: 创建 AlarmHistoryViewModel — 查询参数、分页逻辑、导出逻辑
  - [x] SubTask 5.5: 创建 AlarmThresholdView.xaml — 阈值配置界面
  - [x] SubTask 5.6: 创建 AlarmThresholdViewModel — 阈值 CRUD、持久化
  - [x] SubTask 5.7: 创建 AlarmStatsView.xaml — 统计面板（等级分布、频率排名、趋势图）
  - [x] SubTask 5.8: 创建 AlarmStatsViewModel — 统计数据聚合

- [x] Task 6: 移除旧报警代码
  - [x] SubTask 6.1: 删除 Interfaces/Alarm/ 目录下所有文件
  - [x] SubTask 6.2: 删除 Interfaces/Service/AlarmService.cs
  - [x] SubTask 6.3: 删除 MainApp/Migrations/ 目录
  - [x] SubTask 6.4: 删除 MainApp/AlarmDbContextDesignFactory.cs
  - [x] SubTask 6.5: 删除 ModuleCore/Views/AlarmReportingView.xaml/.cs
  - [x] SubTask 6.6: 删除 ModuleCore/ViewModels/AlarmReportingViewModel.cs
  - [x] SubTask 6.7: 清理 MainApp/App.xaml.cs 中旧报警 DI 注册
  - [x] SubTask 6.8: 清理 Interfaces.csproj 中 SQL Server 相关包引用
  - [x] SubTask 6.9: 更新 ModuleCore/ModuleCore.cs 中旧报警视图注册
  - [x] SubTask 6.10: 更新 Module/PrimModel.cs 中报警导航菜单注册

- [x] Task 7: 更新现有代码中的报警引用
  - [x] SubTask 7.1: 更新 MotionControl 中的 AxisAlarmEvent 使用新的 IAlarmService
  - [x] SubTask 7.2: 更新 StationTaskBase 中的报警触发逻辑

- [x] Task 8: 全量编译验证
  - [x] SubTask 8.1: 执行 dotnet build 确保无编译错误

# Task Dependencies
- [Task 2] depends on [Task 1] — 数据模型需要项目结构
- [Task 3] depends on [Task 2] — 服务层依赖数据访问层
- [Task 4] depends on [Task 3] — 通知服务依赖 IAlarmService 事件
- [Task 5] depends on [Task 3] — UI 绑定服务层接口
- [Task 6] depends on [Task 5] — 先建新再删旧，确保功能连续
- [Task 7] depends on [Task 3] [Task 6] — 更新引用在新服务就绪和旧代码移除后
- [Task 8] depends on [Task 7] — 最终编译验证
