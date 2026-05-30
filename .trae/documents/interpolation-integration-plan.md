# 插补功能集成与Step5增强实施计划

## 一、总体架构

遵循现有 WPF + Prism + MaterialDesign 分层架构，避免倒置依赖：

```
Core (模型层)          → DispenseSegment 增加 TeachHeight / HeightCompensation
MotionControl (运动层) → IMotionCard / IMotionService 增加连续插补接口
Module/Services (服务层) → DispenseExecuteService 集成连续插补
Module/ViewModels (VM层) → CadPointEditorViewModel 增加 Step5 真实运行逻辑
Module/Controls (视图层) → Step5SimulatePanel 增加真实空跑/点胶按钮
```

***

## 二、详细步骤

### Step 1：IMotionCard 增加连续插补方法

**文件**: `MotionControl/Interfaces/IMotionCard.cs`

新增方法签名：

```csharp
int ContiOpenList(int coordId, int axisCount, int[] axisIds);
int ContiLineUnit(int coordId, int axisCount, int[] axisIds, double[] targetPos, ushort posiMode, int mark);
int ContiStartList(int coordId);
int ContiCloseList(int coordId);
int ContiSetLookaheadMode(int coordId, int mode, int fifoSize, int reserved1, int reserved2);
int SetVectorProfileUnit(int coordId, double startVel, double maxVel, double acc, double dec, double endVel);
int SetVectorSProfile(int coordId, int reserved, double sPara);
int SetArcLimit(int coordId, int reserved1, int reserved2, int reserved3);
int CheckCoordMotionDone(int coordId);
```

### Step 2：LTDMC 卡实现连续插补

**文件**: `MotionControl/Card/LtdmcMotionCard.cs`

在 `LtdmcMotionCard` 中实现 Step 1 新增的接口方法，直接调用 `LTDMC` 静态方法：

* `ContiOpenList` → `LTDMC.dmc_conti_open_list`

* `ContiLineUnit` → `LTDMC.dmc_conti_line_unit`

* `ContiStartList` → `LTDMC.dmc_conti_start_list`

* `ContiCloseList` → `LTDMC.dmc_conti_close_list`

* `ContiSetLookaheadMode` → `LTDMC.dmc_conti_set_lookahead_mode`

* `SetVectorProfileUnit` → `LTDMC.dmc_set_vector_profile_unit`

* `SetVectorSProfile` → `LTDMC.dmc_set_vector_s_profile`

* `SetArcLimit` → `LTDMC.dmc_set_arc_limit`

* `CheckCoordMotionDone` → `LTDMC.dmc_check_done_multicoor`\
  short dmc\_conti\_pause\_list(WORD CardNo,WORD Crd)

  功  能：暂停连续插补

  参  数：CardNo          卡号

  Crd               坐标系号，取值范围：0\~7

  增加连续插补暂停功能

### Step 3：IMotionService 增加连续插补接口

**文件**: `MotionControl/Interfaces/IMotionService.cs`

新增方法：

```csharp
void InitializeContinuousInterpolation(int coordId, int[] axisIds);
void AddLineSegment(int coordId, double[] targetPos, ushort posiMode = 1, int mark = 0);
void ExecuteContinuousInterpolation(int coordId);
Task<bool> WaitForCoordMotionCompletionAsync(int coordId, TimeSpan timeout);
```

### Step 4：MotionService 实现连续插补

**文件**: `MotionControl/Services/MotionService.cs`

实现 Step 3 新增的接口方法，委托给对应的 `IMotionCard`：

* `InitializeContinuousInterpolation` → 调用 card 的 `SetVectorProfileUnit` + `ContiSetLookaheadMode` + `SetVectorSProfile` + `SetArcLimit` + `ContiOpenList`

* `AddLineSegment` → 调用 card 的 `SetVectorProfileUnit` + `ContiLineUnit`

* `ExecuteContinuousInterpolation` → 调用 card 的 `ContiStartList` + `ContiCloseList`

* `WaitForCoordMotionCompletionAsync` → 轮询 card 的 `CheckCoordMotionDone`

### Step 5：DispenseSegment 增加示教高度和高度补偿

**文件**: `Core/Models/DispenseSegment.cs`

新增属性：

```csharp
private double _teachHeight = 0.0;
/// <summary>示教高度 mm（示教时自动记录当前Z轴位置）</summary>
public double TeachHeight { get => _teachHeight; set => SetProperty(ref _teachHeight, value); }

private double _heightCompensation = 0.0;
/// <summary>高度补偿值 mm（换针后补偿或人工手动补偿，最终工作高度 = TeachHeight + HeightCompensation）</summary>
public double HeightCompensation { get => _heightCompensation; set => SetProperty(ref _heightCompensation, value); }
```

修改 `ZHeight` 的计算逻辑：`ZHeight` 改为只读计算属性 `= TeachHeight + HeightCompensation`，或保持 `ZHeight` 可写但提供 `EffectiveZHeight => ZHeight + HeightCompensation`。

**决策**：保持 `ZHeight` 向后兼容，新增 `EffectiveZHeight` 只读属性：

```csharp
[JsonIgnore]
public double EffectiveZHeight => ZHeight + HeightCompensation;
```

### Step 6：DispenseExecuteService 集成连续插补

**文件**: `Module/Services/DispenseExecuteService.cs`

重构 `DryRunAsync` 和 `ExecutePathAsync`，将逐点 `MoveLineAbsAsync` 替换为连续插补：

**DryRunAsync 新逻辑**：

1. `_motionService.InitializeContinuousInterpolation(CoordId, new[] { AxisDx, AxisDy })`
2. 遍历所有启用段的所有点，调用 `_motionService.AddLineSegment(CoordId, new[] { x, y })`
3. `_motionService.ExecuteContinuousInterpolation(CoordId)`
4. `_motionService.WaitForCoordMotionCompletionAsync(CoordId, timeout)`

**ExecutePathAsync 新逻辑**：

1. 遍历每个启用段：

   * Z轴上升到安全高度

   * XY移动到段起点上方

   * Z轴下降到 `EffectiveZHeight`

   * 开胶延时

   * 开胶

   * 初始化连续插补 → 添加该段所有点 → 执行连续插补 → 等待完成

   * 关胶

   * 关胶延时

   * Z轴回升安全高度

### Step 7：Step5SimulatePanel UI 增强

**文件**: `Module/Controls/Step5SimulatePanel.xaml`

增加三个执行模式按钮：

```xml
<!-- 执行模式选择 -->
<materialDesign:Card Padding="12" Margin="0,0,0,8">
    <StackPanel>
        <TextBlock Text="执行模式" FontSize="13" FontWeight="SemiBold" Margin="0,0,0,8"/>
        <RadioButton Content="空跑仿真（UI模拟）" IsChecked="{Binding IsSimMode}" GroupName="ExecMode"/>
        <RadioButton Content="真实空跑（运动不出胶）" IsChecked="{Binding IsRealDryRunMode}" GroupName="ExecMode"/>
        <RadioButton Content="真实点胶（运动+出胶）" IsChecked="{Binding IsRealDispenseMode}" GroupName="ExecMode"/>
    </StackPanel>
</materialDesign:Card>

<!-- 执行按钮 -->
<Button Content="▶ 开始执行" Command="{Binding ExecuteRunCommand}" ... />
```

**文件**: `Module/Controls/Step5SimulatePanel.xaml.cs` — 无需修改

### Step 8：CadPointEditorViewModel 增加 Step5 执行逻辑

**文件**: `Module/ViewModels/CadPointEditorViewModel.cs`

新增属性：

```csharp
private bool _isSimMode = true;
public bool IsSimMode { get => _isSimMode; set => SetProperty(ref _isSimMode, value); }

private bool _isRealDryRunMode;
public bool IsRealDryRunMode { get => _isRealDryRunMode; set => SetProperty(ref _isRealDryRunMode, value); }

private bool _isRealDispenseMode;
public bool IsRealDispenseMode { get => _isRealDispenseMode; set => SetProperty(ref _isRealDispenseMode, value); }
```

新增命令：

```csharp
public DelegateCommand ExecuteRunCommand => _executeRunCommand ??= new DelegateCommand(ExecuteRun, () => CanExecute);
```

`ExecuteRun` 逻辑：

```csharp
private async void ExecuteRun()
{
    if (IsSimMode) { /* 原有 ExecuteDryRun 逻辑 */ }
    else if (IsRealDryRunMode) { /* 调用 _dispenseExecuteService.DryRunAsync */ }
    else if (IsRealDispenseMode) { /* 调用 _dispenseExecuteService.ExecutePathAsync */ }
}
```

**依赖注入**：在 `CadPointEditorViewModel` 构造函数中注入 `IDispenseExecuteService`（通过方法注入或属性注入，因为 ViewModel 由 Prism 导航创建）。

**解决 CanExecute 不使能问题**：当前 `CanExecute => Segments.Any(s => s.IsEnabled) && !_isSimulating`，需确保 `Segments` 集合变更时触发 `RaisePropertyChanged(nameof(CanExecute))`。

### Step 9：执行时画布高亮当前轨迹段

**文件**: `Module/ViewModels/CadPointEditorViewModel.cs`

在 `ExecuteRun` 中，订阅 `DispenseExecuteService.ProgressChanged` 事件：

```csharp
_dispenseExecuteService.ProgressChanged += (msg, current, total) =>
{
    Application.Current.Dispatcher.Invoke(() =>
    {
        SimStatusText = msg;
        SimProgress = (double)current / total * 100;
        // 高亮当前执行的段
        var seg = enabledSegments.ElementAtOrDefault(current - 1);
        if (seg?.SourceEntity != null)
            SelectedEntity = seg.SourceEntity;
    });
};
```

### Step 10：示教高度功能

**文件**: `Module/ViewModels/CadPointEditorViewModel.cs`

新增命令：

```csharp
public DelegateCommand TeachHeightCommand => ... 
```

逻辑：读取当前 Z 轴位置 → 更新选中段的 `TeachHeight`：

```csharp
private void ExecuteTeachHeight()
{
    if (_selectedSegment == null) return;
    double currentZ = _motionService.GetAxisState(AxisDz1).Position;
    _selectedSegment.TeachHeight = currentZ;
    _selectedSegment.ZHeight = currentZ; // 同步更新工作高度
}
```

**UI**：在 Step4 参数面板中增加"示教高度"按钮和高度补偿输入框。

### Step 11：解决空跑仿真按钮不能使能

**问题分析**：`CanExecute` 依赖 `Segments.Any(s => s.IsEnabled)`，但 `Segments` 是 `ObservableCollection`，其内容变更不会自动触发 `CanExecute` 通知。

**修复**：在 `Segments.CollectionChanged` 事件中调用 `RaisePropertyChanged(nameof(CanExecute))`，同时在 `DispenseSegment.IsEnabled` 变更时也触发。

***

## 三、修改文件清单

| 文件                                             | 修改内容                                                   |
| ---------------------------------------------- | ------------------------------------------------------ |
| `MotionControl/Interfaces/IMotionCard.cs`      | 新增连续插补方法                                               |
| `MotionControl/Card/LtdmcMotionCard.cs`        | 实现连续插补方法                                               |
| `MotionControl/Interfaces/IMotionService.cs`   | 新增连续插补接口                                               |
| `MotionControl/Services/MotionService.cs`      | 实现连续插补                                                 |
| `Core/Models/DispenseSegment.cs`               | 新增 TeachHeight / HeightCompensation / EffectiveZHeight |
| `Module/Services/IDispenseExecuteService.cs`   | 无需修改（接口已足够）                                            |
| `Module/Services/DispenseExecuteService.cs`    | 集成连续插补替代逐点插补                                           |
| `Module/Controls/Step5SimulatePanel.xaml`      | 增加执行模式选择和按钮                                            |
| `Module/ViewModels/CadPointEditorViewModel.cs` | 增加 Step5 执行逻辑、示教高度、CanExecute 修复                       |

***

## 四、实施顺序

1. **Step 1-4**：MotionControl 层连续插补（底层 → 上层）
2. **Step 5**：DispenseSegment 模型扩展
3. **Step 6**：DispenseExecuteService 集成连续插补
4. **Step 11**：修复 CanExecute 使能问题
5. **Step 7-8**：Step5 UI 和 ViewModel 增强
6. **Step 9**：画布高亮当前执行段
7. **Step 10**：示教高度功能

