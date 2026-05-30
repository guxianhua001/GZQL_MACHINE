# 轴控制面板 UI 重构 Spec

## Why
现有轴控制 UI（AxisOperationView、AxisGroupView）存在以下问题：
1. 以对话框形式实现，不符合右侧工具栏调出的需求
2. **JogButtonHelper 存在安全性 bug**：鼠标移开后轴仍可能继续运动
3. **使用 DispatcherTimer 轮询状态**：存在重入风险且实时性不足
4. **代码位于 Module 层**：导致倒置依赖（UI 层依赖业务层）
5. 架构不够清晰，需要完全重构以适应 MotionControl 模块的定位

## What Changes
- **删除** 现有轴控 UI 组件（Module/WorkStation/Axes/ 目录下的文件）
- **删除** Module/JogButtonHelper.cs（不再使用）
- **新增（MotionControl 项目内）**:
  - `MotionControl/Views/AxisControlPanelView.xaml` - 主面板视图
  - `MotionControl/Views/StationAxisView.xaml` - 单工站轴列表视图
  - `MotionControl/Views/SingleAxisControlView.xaml` - 单个轴控制卡片
  - `MotionControl/ViewModels/AxisControlPanelViewModel.cs` - 面板 ViewModel
  - `MotionControl/ViewModels/StationAxisViewModel.cs` - 工站 ViewModel
  - `MotionControl/ViewModels/SingleAxisViewModel.cs` - 单轴 ViewModel（核心逻辑）
  - `MotionControl/Behaviors/SafeJogBehavior.cs` - 安全的 Jog 行为（替代 JogButtonHelper）
  - `MotionControl/Converters/AxisDirectionToIconConverter.cs` - 方向图标转换器
- **修改** MainApp/MainWindow.xaml - 右侧工具栏集成
- **修改** MotionControl/MotionControlModule.cs - 注册新视图

## Impact
- Affected specs: MotionControl 模块（主要）、MainApp（入口）、Module（清理旧代码）
- Affected code:
  - `MotionControl/` - 新增所有轴控组件
  - `MainApp/Views/MainWindow.xaml` - 集成入口
  - `Module/WorkStation/Axes/` - 标记为废弃或删除
  - `Module/JogButtonHelper.cs` - 删除

## Architecture Principles

### 1. 无倒置依赖
```
MainApp (启动层)
  └── MotionControl (运动控制层) ← 所有轴控代码在此
        ├── Views (纯 XAML 视图)
        ├── ViewModels (MVVM 逻辑)
        ├── Behaviors (交互行为)
        └── Converters (值转换)
  
Module (业务层)
  └── 不再包含轴控 UI 代码
```

### 2. 事件驱动状态监控（禁止轮询）
- 使用 `IObservable<T>` / 响应式模式替代 DispatcherTimer
- 轴状态变化时由底层硬件层主动推送事件
- ViewModel 订阅事件并更新 UI（通过 PropertyChanged）
- 避免定时器重入问题

### 3. Jog 安全性保证（不依赖 JogButtonHelper）
- 使用 Blend Behavior / Attached Behavior 封装 Jog 逻辑
- 在 Behavior 内部管理完整的鼠标生命周期
- 状态机模式：Idle → Jogging → Stopping → Idle
- 多重安全网：MouseUp + LostMouseCapture + LostFocus + Deactivated

### 4. Jog 可视化状态指示
- Jog 按钮旁增加 LED 指示灯
- 按下时亮绿色（正在运动），松开时灭（已停止）
- 用于调试和确认 Jog 状态

## ADDED Requirements

### Requirement: MotionControl 内置轴控制面板
系统 SHALL 在 MotionControl 模块内实现完整的轴控制面板，无外部依赖。

#### Scenario: 面板初始化与数据加载
- **WHEN** 面板打开时
- **THEN** 从 IHardwareConfigLoader 加载 hwcfg.xml 配置
- **THEN** 按 TaskConfig.StationId 分组创建工站 Tab
- **THEN** 每个 Tab 加载对应 TaskId 下的 AxisConfig 列表

### Requirement: MaterialDesignNavigationRailTabControl 布局
面板 SHALL 使用 MaterialDesign NavigationRail 风格。

#### Scenario: 右侧 Tab 导航
- **WHEN** 面板渲染完成
- **THEN** 显示 TabControl，TabStripPlacement=Right
- **THEN** 每个 TabItem Header 包含 PackIcon 图标 + 工站名称
- **THEN** 内容区域显示当前选中工站的轴列表

### Requirement: 单轴完整控制功能
每个轴的控制卡片 SHALL 包含以下元素：

| 控件 | 功能 | 数据绑定 |
|------|------|----------|
| TextBlock | 轴号 | AxisId |
| TextBlock | 轴名称 | Name |
| TextBox | 相对移动距离 | StepSize |
| Button (-) | 相对负向移动 | MoveNegativeCommand |
| Button (+) | 相对正向移动 | MovePositiveCommand |
| Slider | 速度调节 (mm/s) | Speed (1-30) |
| Button (Jog-) | Jog 负向点动 | 附着 SafeJogBehavior |
| Button (Jog+) | Jog 正向点动 | 附着 SafeJogBehavior |
| Ellipse (LED) | Jog 状态指示 | IsJogging (True=绿, False=灰) |
| Button | 停止 | StopCommand |
| Button | 归零 | HomeCommand |
| Button | 清零 | ClearPositionCommand |
| Button | 清除报警 | ClearAlarmCommand |
| Button | 伺服ON | ServoOnCommand |
| Button | 伺服OFF | ServoOffCommand |
| Ellipse×6 | 状态指示灯 | IsServoOn/IsMEL/IsORG/IsPEL/IsALM/IsASTP |
| TextBlock | 是否初始化 | LocalizedHomeStatus |
| TextBlock | 实时位置 | Position (F3格式) |

#### Scenario: Jog 点动控制（安全性关键 - 重写）
- **WHEN** 用户按下 Jog 按钮（PreviewMouseDown）
- **THEN** 设置 IsJogging=true，LED 变绿
- **THEN** 调用 IMotionService.StartJogAsync(axisId, direction)
- **WHEN** 用户松开鼠标（PreviewMouseUp）OR 移出按钮范围（LostMouseCapture）OR 按钮失去焦点（LostFocus）OR 窗口失活（Deactivated）
- **THEN** 立即调用 IMotionService.StopAxisAsync(axisId)
- **THEN** 设置 IsJogging=false，LED 变灰
- **THEN** 无论何种情况，确保轴停止（三重保障）

#### Scenario: 方向图标动态显示
- **WHEN** 轴控制卡片加载
- **THEN** Jog-/Jog+ 按钮的图标根据 AxisConfig.Direction 动态设置
- **THEN** X轴→左右箭头, Y/Z轴→上下箭头, R轴→旋转箭头

### Requirement: 事件驱动状态监控（禁止定时器）
轴状态更新 SHALL 采用事件驱动机制。

#### Scenario: 轴状态变化推送
- **WHEN** 底层硬件检测到轴状态变化（位置、伺服、报警等）
- **THEN** IMotionService 通过事件/IObservable 推送 AxisStateChangedEventArgs
- **THEN** SingleAxisViewModel 接收事件并更新属性
- **THEN** UI 自动刷新（无需定时器轮询）

#### Scenario: 防止重入
- **WHEN** 状态更新事件触发
- **THEN** 使用 async/await + SemaphoreSlim 保证串行处理
- **THEN** 若上一更新未完成，丢弃或合并后续事件（不排队堆积）

### Requirement: 全局紧急停止
面板 SHALL 提供显著位置的紧急停止按钮。

#### Scenario: 触发紧急停止
- **WHEN** 用户点击红色"紧急停止"按钮
- **THEN** 并发调用所有轴的 StopAxisAsync
- **THEN** 取消所有进行中的 Jog 操作（IsJogging=false）
- **THEN** 显示模态确认对话框

## MODIFIED Requirements

### Requirement: MainWindow 右侧工具栏集成
MainWindow SHALL 提供轴控制面板入口。

- 右侧固定宽度工具栏（60px），包含轴控制图标按钮
- 点击后展开 Drawer 风格侧边栏（从右向左滑出）
- 再次点击或点击遮罩层收起
- 使用 Prism Region 或直接 Child 注入

### Requirement: 清理旧代码
删除或标记废弃以下组件：
- `Module/WorkStation/Axes/AxisOperationView.xaml`
- `Module/WorkStation/Axes/AxisGroupView.xaml`
- `Module/WorkStation/Axes/AxisOperationViewModel.cs`
- `Module/WorkStation/Axes/AxisGroupViewModel.cs`
- `Module/JogButtonHelper.cs`

## Technical Design Details

### SafeJogBehavior 实现伪代码
```csharp
public class SafeJogBehavior : Behavior<Button>
{
    // 附加属性
    public static readonly DependencyProperty AxisIdProperty = ...
    public static readonly DependencyProperty DirectionProperty = ...  // "Positive"/"Negative"
    public static readonly DependencyProperty IsJoggingProperty = ...  // 绑定到 LED
    
    private CancellationTokenSource _jogCts;
    
    protected override void OnAttached()
    {
        AssociatedObject.PreviewMouseLeftButtonDown += OnMouseDown;
        AssociatedObject.PreviewMouseLeftButtonUp += OnMouseUp;
        AssociatedObject.LostMouseCapture += OnLostCapture;
        AssociatedObject.LostFocus += OnLostFocus;
        // 监听窗口级事件作为最后保障
        Window.Deactivated += OnWindowDeactivated;
    }
    
    private async void OnMouseDown(...)
    {
        if (_jogCts != null) return;  // 防止重复
        _jogCts = new CancellationTokenSource();
        SetIsJogging(true);  // LED 亮
        await _motionService.StartJogAsync(AxisId, Direction, _jogCts.Token);
    }
    
    private async void StopJog()
    {
        _jogCts?.Cancel();
        _jogCts?.Dispose();
        _jogCts = null;
        await _motionService.StopAxisAsync(AxisId);
        SetIsJogging(false);  // LED 灭
    }
}
```

### 事件驱动状态监控架构
```csharp
// IMotionService 扩展
public interface IMotionService : IObservable<AxisStateChangedEvent>
{
    // 现有方法...
    IDisposable Subscribe(IObserver<AxisStateChangedEvent> observer);
}

// SingleAxisViewModel
public class SingleAxisViewModel : BindableBase, IDisposable
{
    private readonly IDisposable _subscription;
    private readonly SemaphoreSlim _updateLock = new(1, 1);
    
    public SingleAxisViewModel(int axisId, IMotionService motionService)
    {
        _subscription = motionService
            .Where(e => e.AxisId == axisId)
            .Throttle(TimeSpan.FromMilliseconds(50))  // 防抖
            .Select(e => e.Status)
            .Subscribe(async status => await UpdateStatusAsync(status));
    }
    
    private async Task UpdateStatusAsync(AxisStatus status)
    {
        if (!await _updateLock.WaitAsync(0)) return;  // 非阻塞，跳过重入
        try { /* 更新属性 */ }
        finally { _updateLock.Release(); }
    }
}
```

### 文件结构（MotionControl 项目内）
```
MotionControl/
├── Views/
│   ├── AxisControlPanelView.xaml      # 主面板（TabControl）
│   ├── StationAxisView.xaml           # 单工站轴列表
│   └── SingleAxisControlView.xaml     # 单轴控制卡片
├── ViewModels/
│   ├── AxisControlPanelViewModel.cs   # 面板逻辑（分组加载）
│   ├── StationAxisViewModel.cs        # 工站逻辑
│   └── SingleAxisViewModel.cs         # 单轴核心逻辑（事件驱动）
├── Behaviors/
│   └── SafeJogBehavior.cs             # 安全 Jog 行为
├── Converters/
│   ├── AxisDirectionToIconConverter.cs
│   └── BoolToJogLedBrushConverter.cs  # Jog LED 颜色转换
└── Events/
    └── AxisStateChangedEvent.cs       # 状态变更事件
```
