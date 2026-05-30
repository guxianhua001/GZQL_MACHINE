# 全局速度百分比控制系统 - 实施计划

## 一、系统设计概览

### 核心思路
在 `StationTaskBase.MoveToAsync()` 中拦截 velocity 参数，乘以全局速度百分比系数后再传递给 `IMotionService`。这样所有通过 `MoveToAsync` 的运动都会受到全局速度控制，而无需修改任何 Task 子类的代码。

### 架构图
```
UI 层
  ├── SpeedControlView (MotionControl 模块内的独立控件)
  │     └── Slider + 百分比显示 + 确认按钮
  ├── OverView.xaml (顶部状态栏速度显示)
  │     └── 速度百分比实时显示模块
  │
服务层
  ├── ISpeedOverrideService (新增接口)
  │     └── double SpeedPercent { get; set; }  // 1~100
  │     └── event SpeedChangedEvent
  │
  └── SpeedOverrideService (新增实现)
        └── 持久化到 IAppSettingService.ExtensionData
        └── 启动时从配置加载
        └── 值变更时自动保存

运动拦截层
  └── StationTaskBase.MoveToAsync()
        └── velocity * (_speedOverride.SpeedPercent / 100.0)
```

---

## 二、详细实施步骤

### 步骤1：新增 ISpeedOverrideService 接口

**文件**: `MotionControl/Interfaces/ISpeedOverrideService.cs`（新建）

```csharp
public interface ISpeedOverrideService
{
    double SpeedPercent { get; set; }   // 1~100
    event Action<double> SpeedChanged;
}
```

### 步骤2：实现 SpeedOverrideService

**文件**: `MotionControl/Services/SpeedOverrideService.cs`（新建）

- 注入 `IAppSettingService` 用于持久化
- 构造时从 `ExtensionData["GlobalSpeedPercent"]` 加载，默认 100
- `SpeedPercent` 的 setter 触发 `SpeedChanged` 事件并调用 `Save()`
- 值范围限制：`Math.Clamp(value, 1, 100)`
- `Save()` 方法将值写入 `IAppSettingService` 的 `ExtensionData`

### 步骤3：注册 DI

**文件**: `MotionControl/MotionControlModule.cs`

在 `RegisterTypes` 中添加：
```csharp
containerRegistry.RegisterSingleton<ISpeedOverrideService, SpeedOverrideService>();
```

### 步骤4：StationTaskBase 注入 ISpeedOverrideService 并拦截速度

**文件**: `MotionControl/Services/StationTaskBase.cs`

- 构造函数新增 `ISpeedOverrideService speedOverride` 参数
- `MoveToAsync()` 中将 `velocity` 乘以速度系数：
  ```csharp
  var actualVelocity = velocity * (_speedOverride.SpeedPercent / 100.0);
  await _motion.MoveAbsAsync(axisId, pos, actualVelocity, CurrentToken);
  ```
- 同样处理 `MoveLineAbsAsync` 等其他运动方法（如有）

### 步骤5：更新三个子任务构造函数

**文件**: `LoadingTask.cs`, `AssemblyTask.cs`, `DispensingTask.cs`

构造函数新增 `ISpeedOverrideService speedOverride` 参数，传递给基类。

### 步骤6：创建 SpeedControlView 用户控件

**文件**: `MotionControl/Views/SpeedControlView.xaml`（新建）

UI 设计（符合 OverView 工业风配色）：
```
┌─────────────────────────────────────────────┐
│  速度: [====●==================]  50%  [确认] │
└─────────────────────────────────────────────┘
```

- 左侧标签 "速度:"
- 中间 Slider（Minimum=1, Maximum=100, TickFrequency=1, IsSnapToTickEnabled=True）
- 百分比数字显示（大字体，整数）
- 确认按钮（点击后才生效，防止误操作）
- 当前生效值状态指示（确认前显示"待确认"，确认后显示"已生效"）

**文件**: `MotionControl/ViewModels/SpeedControlViewModel.cs`（新建）

- 注入 `ISpeedOverrideService`
- `PendingPercent` 属性：Slider 绑定的待确认值
- `CurrentPercent` 属性：当前生效值（从 service 读取）
- `ConfirmCommand`：将 PendingPercent 写入 service
- 订阅 `SpeedChanged` 事件同步 CurrentPercent

### 步骤7：注册 SpeedControlView

**文件**: `MotionControl/MotionControlModule.cs`

```csharp
containerRegistry.RegisterForNavigation<SpeedControlView, SpeedControlViewModel>();
```

### 步骤8：OverView 顶部状态栏添加速度显示

**文件**: `Module/Views/OverView.xaml`

在顶部操作栏的安全门、三色灯指示器后面添加速度显示模块：

```xml
<!-- 全局速度显示 -->
<Border Background="#E3F2FD" CornerRadius="4" Padding="8,4">
    <StackPanel Orientation="Horizontal">
        <materialDesign:PackIcon Kind="Speedometer" .../>
        <TextBlock Text="{Binding CurrentSpeedPercent, StringFormat='速度: {0}%'}" .../>
    </StackPanel>
</Border>
```

**文件**: `Module/ViewModels/OverViewModel.cs`

- 注入 `ISpeedOverrideService`
- 新增 `CurrentSpeedPercent` 属性
- 订阅 `SpeedChanged` 事件更新显示

### 步骤9：OverView 底部控制栏添加速度调节入口

**文件**: `Module/Views/OverView.xaml`

在底部控制栏的分隔线后、单步按钮前添加速度调节 Slider：

```xml
<!-- 分隔线 -->
<Border Width="1" Background="#4A5568" .../>
<!-- 速度调节 -->
<StackPanel Orientation="Horizontal">
    <TextBlock Text="速度" Foreground="#A0AEC0" .../>
    <Slider Minimum="1" Maximum="100" Value="{Binding SpeedPercent}" Width="120" .../>
    <TextBlock Text="{Binding SpeedPercent, StringFormat='{}{0}%'}" Foreground="White" .../>
</StackPanel>
```

**文件**: `Module/ViewModels/OverViewModel.cs`

- 新增 `SpeedPercent` 属性，双向绑定 Slider
- setter 中调用 `_speedOverride.SpeedPercent = value`（实时生效，无需确认按钮）
- 从 `ISpeedOverrideService.SpeedPercent` 初始化

---

## 三、文件变更清单

| 操作 | 文件路径 | 说明 |
|------|---------|------|
| 新建 | `MotionControl/Interfaces/ISpeedOverrideService.cs` | 速度覆盖服务接口 |
| 新建 | `MotionControl/Services/SpeedOverrideService.cs` | 速度覆盖服务实现 |
| 新建 | `MotionControl/Views/SpeedControlView.xaml` | 速度控制用户控件 |
| 新建 | `MotionControl/ViewModels/SpeedControlViewModel.cs` | 速度控制ViewModel |
| 修改 | `MotionControl/MotionControlModule.cs` | 注册新服务和视图 |
| 修改 | `MotionControl/Services/StationTaskBase.cs` | 注入速度服务，拦截velocity |
| 修改 | `StationTasks/Tasks/LoadingTask.cs` | 构造函数新增参数 |
| 修改 | `StationTasks/Tasks/AssemblyTask.cs` | 构造函数新增参数 |
| 修改 | `StationTasks/Tasks/DispensingTask.cs` | 构造函数新增参数 |
| 修改 | `Module/Views/OverView.xaml` | 顶部速度显示 + 底部速度调节 |
| 修改 | `Module/ViewModels/OverViewModel.cs` | 速度属性和事件订阅 |

---

## 四、关键设计决策

1. **速度拦截位置**：在 `StationTaskBase.MoveToAsync()` 中拦截，而非在 `MotionService` 中。原因：`MotionService` 是底层通用服务，手动JOG、回原点等操作不应受全局速度影响；只有 Task 层面的自动流程运动才需要速度覆盖。

2. **实时生效 vs 确认生效**：OverView 底部控制栏的 Slider 实时生效（操作员在运行中调节速度应立即响应）；SpeedControlView 独立控件有确认按钮（用于精确设置场景）。

3. **持久化方案**：使用 `IAppSettingService.ExtensionData`，键名 `"GlobalSpeedPercent"`，应用启动时自动恢复上次设定值。

4. **异常值处理**：`SpeedOverrideService.SpeedPercent` 的 setter 使用 `Math.Clamp(value, 1, 100)` 确保值始终在有效范围内。

5. **HomeAsync 不受影响**：回原点操作直接调用 `_motion.HomeAsync()`，不经过 `MoveToAsync()`，因此全局速度不影响回原点速度。
