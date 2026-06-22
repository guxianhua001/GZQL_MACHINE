# TaskMonitorView 多工站监控布局优化方案

## 摘要

将 TaskMonitorView 从单列竖向卡片列表重构为**紧凑卡片网格 + 点击展开步骤历史**的布局，解决工站数量多时无法全局观察各工站步骤及状态的问题。同时进行性能优化（共享定时器、移除 DropShadowEffect、延迟渲染步骤历史），确保运动控制场景的快速响应性。

---

## 一、当前状态分析

### 1.1 现有布局结构
- **宿主位置**：[OverView.xaml](file:///c:/WorkFiles/GZQL_MACHINE/Module/Views/OverView.xaml#L117-L144) 右栏，固定宽度 380px
- **视图文件**：[TaskMonitorView.xaml](file:///c:/WorkFiles/GZQL_MACHINE/MotionControl/Views/TaskMonitorView.xaml)
- **布局**：`ScrollViewer` → `ItemsControl(StackPanel Vertical)` → `Border(Width=360)` 卡片
- **单卡片高度**：约 300-400px（含状态栏 + 初始化进度 + 当前步骤 + 步骤历史 ListBox MaxHeight=180）

### 1.2 核心问题
1. **无法全局观察**：380px 宽仅能单列显示，每卡片 300+px 高，可见区域仅 2-3 个工站，其余需垂直滚动
2. **性能隐患**：
   - 外层 `StackPanel` 无 UI 虚拟化
   - 每卡片 `DropShadowEffect` 渲染开销大
   - 每个 `TaskDisplayModel` 独立 `DispatcherTimer`（N 工站 = N 定时器）
   - 步骤历史 `ListBox` `CanContentScroll="False"` 牺牲虚拟化
3. **图标使用 emoji**：`✔` `▶` 违反项目规范（应使用 `materialDesign:PackIcon`）

### 1.3 数据模型层级
```
TaskMonitorViewModel.Tasks : ObservableCollection<TaskDisplayModel>
  └── TaskDisplayModel (包装 ITask)
        ├── TaskName, State, CurrentTime, CurrentStepElapsed
        ├── IsInitializing, InitProgress, InitMessage
        ├── StationId
        ├── DispatcherTimer (1s, 独立) ← 需优化为共享
        └── StepHistory : ObservableCollection<StepRecord> (MaxHistoryCount=50)
              └── StepRecord: StepName, RetryCount, IsCurrent, DurationText, StatusText
```

---

## 二、设计方案

### 2.1 布局策略：紧凑卡片 WrapPanel + 点击展开

**核心思路**：默认显示紧凑卡片（仅状态+工站名+当前步骤+耗时），点击卡片展开步骤历史（手风琴式），保持全局视图。

**卡片尺寸**：
- 默认 `ItemWidth = 170px`（380px 栏宽可容纳 2 列）
- 通过 ViewModel `CardWidth` 属性可配置（115px=3列，85px=4列）

**紧凑卡片内容**（约 90px 高）：
```
┌─────────────────────────┐
│ ● 工站名        00:00   │  ← 状态灯 + 名称 + 当前步骤耗时
│ 当前步骤名称(省略)       │  ← 当前步骤名
│ ▼ 展开步骤历史          │  ← 点击展开提示
└─────────────────────────┘
```

**展开后**（约 90 + 180px）：
```
┌─────────────────────────┐
│ ● 工站名        00:00   │
│ 当前步骤名称(省略)       │
│ ▲ 收起                  │
├─────────────────────────┤
│ ✔ 步骤1      00:05      │
│ ▶ 步骤2      00:03      │  ← 步骤历史 ListBox
│   步骤3      ...        │
└─────────────────────────┘
```

### 2.2 性能优化策略

| 优化项 | 现状 | 优化后 | 收益 |
|--------|------|--------|------|
| DispatcherTimer | 每模型独立定时器 | ViewModel 共享 1 个定时器 | N 倍定时器开销 → 1 倍 |
| DropShadowEffect | 每卡片独立阴影 | 移除，改用 Border 背景区分 | 减少渲染层 |
| 步骤历史渲染 | 始终渲染 ListBox | `IsExpanded=False` 时 `Visibility=Collapsed` | 未展开卡片零开销 |
| ItemsPanel | StackPanel（无虚拟化） | WrapPanel（轻量，适合 <50 项） | 布局计算更快 |
| 事件派发 | `Dispatcher.Invoke` 同步 | 保持同步（运动控制需确定性） | 不变，保证安全 |

> **说明**：工业设备工站数典型 <20，WrapPanel 无虚拟化可接受。若未来 >50 工站，可引入 `VirtualizingWrapPanel`（需自定义），当前不过度设计。

### 2.3 可配置布局

ViewModel 暴露 `CardWidth` 属性（默认 170）：
- `CardWidth = 170`：2 列（380px 栏宽）
- `CardWidth = 115`：3 列
- `CardWidth = 85`：4 列（极简模式）

WrapPanel 的 `ItemWidth` 绑定到 `CardWidth`，自动换行。

---

## 三、具体改动

### 3.1 [TaskMonitorViewModel.cs](file:///c:/WorkFiles/GZQL_MACHINE/MotionControl/ViewModels/TaskMonitorViewModel.cs)

**新增**：
1. `CardWidth` 属性（默认 170，支持本地化配置）
2. 共享 `DispatcherTimer _sharedTimer`（1s 间隔）
3. `_sharedTimer.Tick` 调用所有 `TaskDisplayModel.OnSharedTimerTick()`
4. 实现 `IDisposable`，在析构时停止共享定时器

**修改**：
- `LoadTasks()`、`OnStationRegistered()`：创建 `TaskDisplayModel` 时不再启动独立定时器
- `OnStationUnregistered()`：`Dispose` 模型时不再停止定时器（由共享定时器统一管理）

```csharp
public class TaskMonitorViewModel : BindableBase, IDisposable
{
    private DispatcherTimer _sharedTimer;
    private int _cardWidth = 170;
    /// <summary>紧凑卡片宽度（控制布局密度），默认 170=2列</summary>
    public int CardWidth
    {
        get => _cardWidth;
        set => SetProperty(ref _cardWidth, value);
    }

    public TaskMonitorViewModel(...)
    {
        // ... 现有订阅 ...
        InitializeSharedTimer();
        LoadTasks();
    }

    /// <summary>初始化共享定时器，替代各模型独立定时器，降低 N 倍定时器开销</summary>
    private void InitializeSharedTimer()
    {
        _sharedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _sharedTimer.Tick += OnSharedTimerTick;
        _sharedTimer.Start();
    }

    /// <summary>共享定时器回调：统一刷新所有工站的时钟与当前步骤耗时</summary>
    private void OnSharedTimerTick(object sender, EventArgs e)
    {
        foreach (var task in Tasks)
        {
            task.OnSharedTimerTick();
        }
    }

    public void Dispose()
    {
        _sharedTimer?.Stop();
        foreach (var task in Tasks) task.Dispose();
    }
}
```

### 3.2 [TaskDisplayModel.cs](file:///c:/WorkFiles/GZQL_MACHINE/MotionControl/Models/TaskDisplayModel.cs)

**新增**：
1. `IsExpanded` 属性（控制步骤历史展开，默认 false）
2. `CurrentStepName` 属性（显式属性，替代脆弱的 `StepHistory/StepName` 绑定）
3. `OnSharedTimerTick()` 方法（由 VM 共享定时器调用）

**修改**：
1. 移除 `_timer` 字段及启动/停止逻辑
2. `UpdateStatus()` 中更新 `CurrentStepName`
3. `Dispose()` 移除 `_timer.Stop()`

```csharp
private bool _isExpanded;
/// <summary>是否展开步骤历史（手风琴式）</summary>
public bool IsExpanded
{
    get => _isExpanded;
    set => SetProperty(ref _isExpanded, value);
}

private string _currentStepName = "";
/// <summary>当前执行步骤名称（显式属性，替代集合当前项绑定）</summary>
public string CurrentStepName
{
    get => _currentStepName;
    set => SetProperty(ref _currentStepName, value);
}

/// <summary>共享定时器回调：刷新时钟与当前步骤耗时</summary>
public void OnSharedTimerTick()
{
    CurrentTime = DateTime.Now.ToString("HH:mm:ss");
    if (_currentStepRecord != null && _currentStepRecord.IsCurrent)
    {
        CurrentStepElapsed = (DateTime.Now - _stepStartTime).ToString(@"mm\:ss");
    }
}
```

在 `UpdateStatus()` 中，当创建/更新 `_currentStepRecord` 时同步设置 `CurrentStepName = payload.CurrentStepName`。

### 3.3 [TaskMonitorView.xaml](file:///c:/WorkFiles/GZQL_MACHINE/MotionControl/Views/TaskMonitorView.xaml)

**整体结构改动**：
```xml
<ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled">
    <ItemsControl ItemsSource="{Binding Tasks}">
        <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
                <!-- WrapPanel 自动换行，ItemWidth 绑定控制密度 -->
                <WrapPanel ItemWidth="{Binding CardWidth}" Orientation="Horizontal"/>
            </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
            <DataTemplate DataType="{x:Type models:TaskDisplayModel}">
                <Border Margin="4" Background="White" CornerRadius="6" Padding="8">
                    <!-- 移除 DropShadowEffect，改用细边框 -->
                    <Border BorderBrush="#E0E0E0" BorderThickness="1" CornerRadius="6">
                        <StackPanel>
                            <!-- 紧凑头部：状态灯 + 工站名 + 耗时 -->
                            <Grid Margin="0,0,0,4">
                                <StackPanel Orientation="Horizontal">
                                    <Ellipse Style="{StaticResource StatusLight}" Margin="0,0,4,0"/>
                                    <TextBlock Text="{Binding TaskName}" FontSize="12" FontWeight="Bold" TextTrimming="CharacterEllipsis"/>
                                </StackPanel>
                                <TextBlock HorizontalAlignment="Right" Text="{Binding CurrentStepElapsed}" FontSize="11" Foreground="#E65100" FontFamily="Consolas"/>
                            </Grid>
                            <!-- 当前步骤 -->
                            <TextBlock Text="{Binding CurrentStepName}" FontSize="11" Foreground="#1976D2" TextTrimming="CharacterEllipsis" Margin="0,0,0,4"/>
                            <!-- 初始化进度条（仅初始化时显示） -->
                            <Border Visibility="{Binding IsInitializing, Converter={StaticResource BoolToVis}}" .../>
                            <!-- 展开/收起切换按钮 -->
                            <ToggleButton IsChecked="{Binding IsExpanded}" Style="{StaticResource ExpandToggleButtonStyle}"/>
                            <!-- 步骤历史（展开时显示） -->
                            <ListBox ItemsSource="{Binding StepHistory}"
                                     Visibility="{Binding IsExpanded, Converter={StaticResource BoolToVis}}"
                                     MaxHeight="160" .../>
                        </StackPanel>
                    </Border>
                </Border>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</ScrollViewer>
```

**关键样式改动**：
1. **StatusLight**：保持现有状态颜色映射，尺寸缩小为 10x10
2. **StepItemStyle**：保持重试高亮，替换 emoji 为 PackIcon
3. **新增 ExpandToggleButtonStyle**：使用 `materialDesign:PackIcon Kind="ChevronDown/ChevronUp"` 切换图标
4. **步骤状态图标**：`✔` → `PackIcon Kind="Check"`，`▶` → `PackIcon Kind="Play"`

**步骤行图标替换**：
```xml
<!-- 旧：emoji -->
<TextBlock Text="✔" .../>
<TextBlock Text="▶" .../>

<!-- 新：PackIcon -->
<materialDesign:PackIcon Kind="Check" Width="12" Height="12" Foreground="#4CAF50"
                          Visibility="{Binding IsCurrent, Converter={StaticResource InverseBoolToVis}}"/>
<materialDesign:PackIcon Kind="Play" Width="12" Height="12" Foreground="#1976D2"
                          Visibility="{Binding IsCurrent, Converter={StaticResource BoolToVis}}"/>
```

### 3.4 多语言资源

新增资源键（通过 `ILocalizationService`，需在资源文件添加）：
- `TaskMonitor_Expand`：展开步骤历史 / Expand Steps
- `TaskMonitor_Collapse`：收起步骤历史 / Collapse Steps
- `TaskMonitor_NoStep`：暂无步骤 / No Step

由于 `ToggleButton.Content` 需要根据 `IsChecked` 切换文本，使用 `DataTrigger` 绑定本地化资源。

### 3.5 不变项

- `StepRecord` 模型不变
- `TaskStatusChangedEvent`、`StationInitProgressEvent` 订阅逻辑不变
- `ListBoxAutoScroll` 附加属性继续使用
- `StationStateView` 等其他视图不受影响
- `OverView.xaml` 宿主布局不变（380px 栏宽）

---

## 四、假设与决策

### 4.1 假设
1. 工站数量典型 <20，WrapPanel 无虚拟化可接受（性能足够）
2. 380px 栏宽为硬约束，不调整 OverView 布局
3. 步骤历史展开为多卡片可同时展开（不限单卡片）

### 4.2 关键决策
| 决策点 | 选择 | 理由 |
|--------|------|------|
| 布局面板 | WrapPanel | 自动换行，支持 ItemWidth 控制密度，轻量 |
| 虚拟化 | 不引入 | 工业场景工站数 <20，过度设计；保留 ScrollViewer |
| 定时器 | 共享单定时器 | N 倍开销降为 1 倍，显著优化 |
| 展开交互 | 手风琴式（ToggleButton） | 保持全局视图，按需查看详情 |
| 阴影效果 | 移除 DropShadowEffect | 渲染开销大，改用细边框 |
| 当前步骤绑定 | 显式 CurrentStepName 属性 | 替代脆弱的集合当前项绑定 |
| 图标 | materialDesign:PackIcon | 遵循项目规范，不使用 emoji |
| 布局配置 | CardWidth 属性 | 用户可调整密度（2/3/4 列） |

---

## 五、验证步骤

### 5.1 编译验证
- `dotnet build` 确保无编译错误
- 检查 XAML 资源引用完整性

### 5.2 功能验证
1. **布局验证**：
   - 3 个工站：2 列显示，第 3 个换行
   - 调整 `CardWidth` 为 115，验证 3 列布局
2. **展开交互**：
   - 点击 ToggleButton，步骤历史显示/隐藏
   - 多卡片可同时展开
3. **状态更新**：
   - 工站运行时状态灯颜色正确
   - 当前步骤名实时更新
   - 耗时计时正常（共享定时器）
4. **初始化进度**：
   - `IsInitializing=True` 时进度条显示
   - 完成后自动隐藏
5. **多语言**：
   - 切换语言，工站名、展开按钮文本更新

### 5.3 性能验证
1. **定时器开销**：检查仅 1 个 DispatcherTimer 运行（VS 诊断工具）
2. **渲染流畅度**：滚动无卡顿（移除 DropShadowEffect 后）
3. **内存**：未展开卡片不渲染步骤历史 ListBox

### 5.4 回归验证
- 工站注册/注销动态增删正常
- 步骤重试高亮（淡橙/深橙）正常
- 自动滚动到最新步骤正常

---

## 六、影响范围

| 文件 | 改动类型 | 影响程度 |
|------|----------|----------|
| `MotionControl/Views/TaskMonitorView.xaml` | 重写布局 | 高 |
| `MotionControl/ViewModels/TaskMonitorViewModel.cs` | 新增共享定时器、CardWidth | 中 |
| `MotionControl/Models/TaskDisplayModel.cs` | 新增 IsExpanded、CurrentStepName，移除独立定时器 | 中 |
| 多语言资源文件 | 新增 3 个键 | 低 |
| `OverView.xaml` | 不变 | 无 |
| `StepRecord.cs` | 不变 | 无 |

---

## 七、版本记录

在 `MainApp/bin/Debug/net9.0-windows7.0/版本修改记录.txt` 追加：
```
v2026.06.23 — TaskMonitorView 多工站监控布局优化：紧凑卡片网格+点击展开步骤历史，共享定时器性能优化，PackIcon 替换 emoji
```
