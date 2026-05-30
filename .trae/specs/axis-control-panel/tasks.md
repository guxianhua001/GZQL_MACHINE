# Tasks

- [x] Task 1: 创建 MotionControl 项目基础架构和事件驱动状态监控
  - [x] 1.1 创建 AxisStateChangedEvent.cs 事件类（包含 AxisId、Position、Status 等信息）
  - [x] 1.2 扩展 IMotionService 接口，添加 IObservable<AxisStateChangedEvent> 支持
  - [x] 1.3 在 MotionService 实现中发布状态变更事件（基于硬件回调或轮询转事件）
  - [x] 1.4 创建 BoolToJogLedBrushConverter.cs 转换器

- [x] Task 2: 实现 SafeJogBehavior（替代 JogButtonHelper）
  - [x] 2.1 创建 SafeJogBehavior.cs，继承 Behavior<Button>
  - [x] 2.2 实现 PreviewMouseDown/Up、LostMouseCapture、LostFocus、Window.Deactivated 四重停止机制
  - [x] 2.3 添加 IsJogging 附加属性用于绑定 LED 指示灯
  - [x] 2.4 使用 CancellationTokenSource 管理 Jog 生命周期
  - [x] 2.5 测试验证：快速按住→移开→松开，确认轴立即停止且无残留运动

- [x] Task 3: 创建 SingleAxisViewModel（单轴核心逻辑）
  - [x] 3.1 创建 SingleAxisViewModel.cs，实现事件驱动状态订阅
  - [x] 3.2 使用 SemaphoreSlim 防止重入，Throttle 防抖
  - [x] 3.3 实现所有轴控制命令：MovePositive/Negative、Home、Stop、ClearPosition、ClearAlarm、ServoOn/Off
  - [x] 3.4 暴露属性：AxisId、Name、Position、Speed、StepSize、IsJogging、IsServoOn/IsMEL/IsORG/IsPEL/IsALM/IsASTP、LocalizedHomeStatus
  - [x] 3.5 实现 IDisposable 正确释放订阅资源

- [x] Task 4: 创建 SingleAxisControlView（单轴 UI 卡片）
  - [x] 4.1 创建 SingleAxisControlView.xaml，按 spec 表格布局所有控件
  - [x] 4.2 Jog- Jog+ 按钮附着 SafeJogBehavior，旁边放置 Ellipse 作为 Jog LED
  - [x] 4.3 使用 AxisDirectionToIconConverter 动态显示方向图标
  - [x] 4.4 状态指示灯使用 Ellipse + BoolToBrush 绑定
  - [x] 4.5 速度滑块范围 1-30 mm/s，步距 ComboBox 预设选项

- [x] Task 5: 创建 StationAxisViewModel 和 StationAxisView（工站级）
  - [x] 5.1 创建 StationAxisViewModel.cs，管理工站下的 ObservableCollection<SingleAxisViewModel>
  - [x] 5.2 从 IHardwareConfigLoader 加载该工站的 AxisConfig 列表并初始化 SingleAxisViewModel
  - [x] 5.3 创建 StationAxisView.xaml，ItemsControl 展示 SingleAxisControlView 列表
  - [x] 5.4 实现紧急停止命令（遍历所有轴调用 Stop）

- [x] Task 6: 创建 AxisControlPanelView（主面板）
  - [x] 6.1 创建 AxisControlPanelViewModel.cs，从 hwcfg.xml 按 StationId 分组加载所有工站
  - [x] 6.2 创建 AxisControlPanelView.xaml，使用 MaterialDesignNavigationRailTabControl（TabStripPlacement=Right）
  - [x] 6.3 TabItem Header 包含 PackIcon + 工站名称
  - [x] 6.4 内容区域绑定 StationAxisView
  - [x] 6.5 面板顶部放置显著的全局紧急停止按钮（红色醒目样式）

- [x] Task 7: 集成到 MainWindow
  - [x] 7.1 修改 MainWindow.xaml，添加右侧固定工具栏（60px 宽度）和 Drawer 面板
  - [x] 7.2 工具栏中添加"轴控制" ToggleButton 按钮（PackIcon Kind="AxisArrow"）
  - [x] 7.3 点击后展开 Drawer 风格侧边栏显示 AxisControlPanelView（遮罩层+面板绑定 IsAxisPanelOpen）
  - [x] 7.4 实现展开/收起功能（ToggleButton 绑定 + 遮罩层点击关闭）

- [x] Task 8: 清理旧代码与注册模块
  - [x] 8.1 确认新视图通过 ViewModelLocator 自动绑定（无需显式注册）
  - [x] 8.2 删除 Module/JogButtonHelper.cs
  - [x] 8.3 删除 Module/WorkStation/Axes/ 目录下 6 个旧轴控文件
  - [x] 8.4 注释 PrimModel.cs 中旧视图注册代码
  - [x] 8.5 更新 MainWindowViewModel.cs：添加 IsAxisPanelOpen 属性，修改 OpenAxisOperationCommand 为切换面板

# Task Dependencies
- [Task 2] depends on [Task 1] ✅
- [Task 3] depends on [Task 1] ✅
- [Task 4] depends on [Task 2, Task 3] ✅
- [Task 5] depends on [Task 3, Task 4] ✅
- [Task 6] depends on [Task 5] ✅
- [Task 7] depends on [Task 6] ✅
- [Task 8] depends on [Task 6, Task 7] ✅
