# Tasks

- [x] Task 1: 创建维护模块目录结构与 INeedleService 实现
  - [x] SubTask 1.1: 创建 Module/Controls/Maintenance/ 目录
  - [x] SubTask 1.2: 实现 NeedleService 类（实现 INeedleService 接口），放在 Module/Services/ 下
  - [x] SubTask 1.3: 在 PrimModel.RegisterTypes() 中注册 INeedleService→NeedleService (Singleton) 和 NeedleCompensationManager (Singleton)

- [x] Task 2: 实现 MaintenanceView 主视图及 ViewModel
  - [x] SubTask 2.1: 创建 MaintenanceViewModel.cs，继承 BindableBase，包含 SelectedTabIndex 属性
  - [x] SubTask 2.2: 创建 MaintenanceView.xaml，包含 Header 区域 + TabControl（三个 Tab 项），使用 MaterialDesign 卡片风格
  - [x] SubTask 2.3: 在 PrimModel 中注册 MaintenanceView/MaintenanceViewModel 导航

- [x] Task 3: 实现 NeedleCameraAlignmentView（针头与相机中心标定）
  - [x] SubTask 3.1: 创建 NeedleCameraAlignmentViewModel.cs，包含双系统切换、示教命令、参数保存/加载、状态反馈
  - [x] SubTask 3.2: 创建 NeedleCameraAlignmentView.xaml，三栏布局：左侧系统选择+参数显示、中间示教操作区、右侧补偿设置+状态
  - [x] SubTask 3.3: 在 PrimModel 中注册 NeedleCameraAlignmentView/NeedleCameraAlignmentViewModel 导航

- [x] Task 4: 实现 NeedleAlignerView（换针与针头校准）
  - [x] SubTask 4.1: 创建 NeedleAlignerViewModel.cs，包含校准流程控制、补偿管理、日志队列、参数保存/加载
  - [x] SubTask 4.2: 创建 NeedleAlignerView.xaml，三栏布局：左侧参数设置、中间校准控制+进度+结果、右侧补偿管理+日志
  - [x] SubTask 4.3: 在 PrimModel 中注册 NeedleAlignerView/NeedleAlignerViewModel 导航

- [x] Task 5: 实现 NeedleCalibrationVerifyView（针头校准验证）
  - [x] SubTask 5.1: 创建 NeedleCalibrationVerifyViewModel.cs，包含验证流程、结果判定、报告生成
  - [x] SubTask 5.2: 创建 NeedleCalibrationVerifyView.xaml，布局：系统选择+验证操作+结果展示+报告区
  - [x] SubTask 5.3: 在 PrimModel 中注册 NeedleCalibrationVerifyView/NeedleCalibrationVerifyViewModel 导航

- [x] Task 6: 添加导航入口与多语言资源
  - [x] SubTask 6.1: 在 PrimModel.OnInitialized() 中添加维护模块导航项（WrenchOutline 图标，UserLevel=1）
  - [x] SubTask 6.2: 在 Strings.zh-CN.xaml 中添加所有维护模块多语言 Key
  - [x] SubTask 6.3: 在 Strings.en-US.xaml 中添加所有维护模块多语言 Key

# Task Dependencies
- [Task 2] depends on [Task 1] (需要 DI 注册先就绪)
- [Task 3] depends on [Task 1] (需要 DI 注册先就绪)
- [Task 4] depends on [Task 1] (需要 DI 注册先就绪)
- [Task 5] depends on [Task 1] (需要 DI 注册先就绪)
- [Task 6] depends on [Task 2, Task 3, Task 4, Task 5] (所有视图完成后添加导航和语言资源)
- [Task 3, Task 4, Task 5] 可并行开发
