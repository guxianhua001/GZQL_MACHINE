# Tasks

- [x] Task 1: 创建数据模型层（Core/Models/）
  - [x] SubTask 1.1: 创建 `DualGantryCalibrationConfig.cs` —— 机构配置（工站、轴名、TCP 连接名、共用 Y 轴名、点数、延时）
  - [x] SubTask 1.2: 创建 `DualGantryCalibrationPoint.cs` —— 标定点模型（继承 BindableBase，含序号/名称/机械XY/视觉XY/状态/状态颜色）
  - [x] SubTask 1.3: 创建 `DualGantryCalibrationData.cs` —— 完整标定数据（配置 + 龙门1点列表 + 龙门2点列表 + 公共基准点列表 + 两套仿射结果 + 跨龙门变换参数）
  - [x] SubTask 1.4: 创建 `GantryTransform.cs` —— 跨龙门变换参数模型（OffsetX/OffsetY/RotationDeg/Scale/残差）

- [x] Task 2: 创建服务接口与实现
  - [x] SubTask 2.1: 创建 `IDualGantryCalibrationService.cs`（Core/Abstraction/）—— 接口定义（自动标定、单点示教/移动、TCP 订阅、仿射计算、跨龙门对齐、事件）
  - [x] SubTask 2.2: 创建 `DualGantryCalibrationService.cs`（Module/Services/）—— 实现要点：
    - 注入 `IPositionMotionController`、`ITCPEventService`、`ILoggerService`
    - 龙门 1/龙门 2 独立的自动标定流程（复用 NPointCalibrationService 模式）
    - 双 TCP 连接独立订阅（Cam1/Cam2）
    - 跨龙门 Y 基准对齐算法：基于公共基准点计算 OffsetX/OffsetY/RotationDeg
    - CancellationTokenSource 控制取消
    - 线程安全：共用 Y 轴互锁

- [x] Task 3: 创建 ViewModel（Module/Controls/Maintenance/DualGantryCalibrationViewModel.cs）
  - [x] SubTask 3.1: 继承 BindableBase，构造函数注入 INPointCalibrationService 等依赖
  - [x] SubTask 3.2: 实现属性：工站列表、轴名列表、机构配置、龙门1/2标定点集合、两套仿射结果、跨龙门变换参数、状态文本/颜色、文件名
  - [x] SubTask 3.3: 实现命令：示教/移动/删除/添加点（龙门1与龙门2各一套）、开始/停止自动标定、计算标定、采集公共基准点、跨龙门对齐、坐标变换验证、保存/另存为/导入/导出
  - [x] SubTask 3.4: 实现自动加载逻辑（构造函数末尾异步加载上次配置）
  - [x] SubTask 3.5: 实现共用 Y 轴互锁提示与安全抬 Z 逻辑

- [x] Task 4: 创建视图（Module/Controls/Maintenance/DualGantryCalibrationView.xaml）
  - [x] SubTask 4.1: 左栏 ScrollViewer：机构配置卡片（拓扑示意图 + 轴名下拉 + 工站选择 + TCP 配置）+ 文件操作卡片
  - [x] SubTask 4.2: 右栏上区：龙门 1 标定卡片（DataGrid + 操作按钮 + 结果显示），主色 #1565C0
  - [x] SubTask 4.3: 右栏中区：龙门 2 标定卡片（DataGrid + 操作按钮 + 结果显示），主色 #00897B
  - [x] SubTask 4.4: 右栏下区：跨龙门对齐卡片（公共基准点列表 + 对齐按钮 + 变换参数显示 + 坐标变换验证），主色 #6A1B9A
  - [x] SubTask 4.5: 底部状态栏（Border + 状态颜色绑定）
  - [x] SubTask 4.6: 引用 MaintenanceSharedStyles.xaml，使用 `{lang:Lang Key}` 多语言绑定，PackIcon 图标

- [x] Task 5: 修改 MaintenanceView.xaml 集成新 Tab
  - [x] SubTask 5.1: 在 TabControl 中新增第 5 个 TabItem（图标 VectorCombine + 文本 Maintenance_Tab_DualGantryCalibration）
  - [x] SubTask 5.2: 在 ContentControl.Style 中新增 DataTrigger Value=4 映射到 DualGantryCalibrationView

- [x] Task 6: 修改 PrimModel.cs 注册 DI
  - [x] SubTask 6.1: RegisterTypes 中新增 `RegisterSingleton<IDualGantryCalibrationService, DualGantryCalibrationService>()`
  - [x] SubTask 6.2: RegisterTypes 中新增 `RegisterForNavigation<DualGantryCalibrationView, DualGantryCalibrationViewModel>()`

- [x] Task 7: 补充多语言资源
  - [x] SubTask 7.1: 在 Strings.zh-CN.xaml 添加 `DualGantryCalib_*` 与 `Maintenance_Tab_DualGantryCalibration` 中文条目
  - [x] SubTask 7.2: 在 Strings.en-US.xaml 添加对应英文条目
  - [x] SubTask 7.3: 校验 zh-CN 与 en-US 的 Key 集合完全一致（0 重复、0 缺失）

- [x] Task 8: 追加版本修改记录
  - [x] SubTask 8.1: 在 `MainApp/bin/Debug/net9.0-windows7.0/版本修改记录.txt` 顶部追加 v2026.06.23 双龙门标定控件记录

- [x] Task 9: 编译验证
  - [x] SubTask 9.1: 执行 dotnet build GZQL_MACHINE.sln，确保无错误无警告
  - [x] SubTask 9.2: 启动应用，进入维护页 → 双龙门标定 Tab，验证界面渲染正常

# Task Dependencies
- Task 2 depends on Task 1（服务实现依赖数据模型）
- Task 3 depends on Task 1, Task 2（ViewModel 依赖模型与服务接口）
- Task 4 depends on Task 3（视图依赖 ViewModel）
- Task 5 depends on Task 4（集成需视图已创建）
- Task 6 depends on Task 2, Task 4（注册需服务与视图已创建）
- Task 7 可与 Task 1-6 并行
- Task 8 可在任意阶段执行
- Task 9 depends on Task 1-7 全部完成
