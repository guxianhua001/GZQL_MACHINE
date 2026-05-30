# Site Feature 选择 Home 时回零功能实现计划

## 需求分析

### 需求1: Site Feature 选择 Home 时，详细页面变更

**当前行为：** Site Feature 选择 "HOME" 时，弹出的 GotoDetailView 仍然显示普通的 SubMove 编辑界面（Station/Axis/Position/OffsetVar/Offset/Speed）。

**期望行为：** Site Feature 选择 "HOME" 时：

* Position 列强制显示 "Home"（不可编辑）

* 后面的列改为：**模式(Mode)**、**回零低速(MinVel)**、**高速(MaxVel)**

* 不显示 OffsetVar、Offset 列

**UI 效果示意：**

| Sub | Station        | Axis | Position | Mode | MinVel | MaxVel |
| --- | -------------- | ---- | -------- | ---- | ------ | ------ |
| 1a  | LoadingStation | Y    | Home     | 1    | 5      | 20     |
| 1b  | LoadingStation | Rx   | Home     | 1    | 5      | 20     |

### 需求2: Start 时执行各轴回零

**当前行为：** Start 启动自定义流程后，GOTO → HOME 步骤仍执行 `ExecuteMoveAsync`（绝对定位移动），不会调用 `Motion.HomeAsync`。

**期望行为：** 当 SiteFeature == "HOME" 的 GOTO 步骤执行时，对每个 SubMove 调用 `Motion.HomeAsync(axisId, mode, minVel, maxVel)` 执行回零操作。

***

## 实施步骤

### Step 1: SubMove 模型新增回零参数字段

**文件:** [ProcessStep.cs](file:///c:/WorkFiles/GZQL_MACHINE/StationTasks/Models/ProcessStep.cs)

在 `SubMove` 类中新增3个属性：

```csharp
private int _homeMode = 1;
/// <summary> 回零模式（1=标准模式等，参考运动控制卡 SDK 文档） </summary>
public int HomeMode { get => _homeMode; set => SetProperty(ref _homeMode, value); }

private double _homeMinVel = 5;
/// <summary> 回零低速（搜索原点时的速度） </summary>
public double HomeMinVel { get => _homeMinVel; set => SetProperty(ref _homeMinVel, value); }

private double _homeMaxVel = 20;
/// <summary> 回零高速（寻找原点时的速度） </summary>
public double HomeMaxVel { get => _homeMaxVel; set => SetProperty(ref _homeMaxVel, value); }
```

### Step 2: SubMoveRowViewModel 新增回零参数属性转发

**文件:** [SubMoveRowViewModel.cs](file:///c:/WorkFiles/GZQL_MACHINE/Module/Operators/Editor/SubMoveRowViewModel.cs)

转发 SubMove 的新增属性：

```csharp
public int HomeMode { get => _subMove.HomeMode; set => _subMove.HomeMode = value; }
public double HomeMinVel { get => _subMove.HomeMinVel; set => _subMove.HomeMinVel = value; }
public double HomeMaxVel { get => _subMove.HomeMaxVel; set => _subMove.HomeMaxVel = value; }
```

### Step 3: GotoDetailViewModel 感知 Home 模式

**文件:** [GotoDetailViewModel.cs](file:///c:/WorkFiles/GZQL_MACHINE/Module/Operators/Editor/GotoDetailViewModel.cs)

新增 `IsHomeMode` 属性，根据 `Step.SiteFeature` 判断是否为回零模式：

```csharp
/// <summary>
/// 是否为回零模式（SiteFeature == "HOME" 时为 true）
/// </summary>
public bool IsHomeMode => _step?.SiteFeature == "HOME";
```

在 `InitializeFromStep()` 中触发 `IsHomeMode` 通知：

```csharp
RaisePropertyChanged(nameof(IsHomeMode));
```

在 `OnSave()` 中，如果是 Home 模式，强制将所有 SubMove 的 PositionName 设为 "Home"：

```csharp
private void OnSave()
{
    if (_step != null)
    {
        var moves = SubMoveRows.Select(r => r.SubMove).ToList();
        if (IsHomeMode)
        {
            foreach (var move in moves)
                move.PositionName = "Home";
        }
        _step.SubMoves = new ObservableCollection<SubMove>(moves);
    }
    OnClose();
}
```

### Step 4: GotoDetailView\.xaml 根据 Home 模式切换列

**文件:** [GotoDetailView.xaml](file:///c:/WorkFiles/GZQL_MACHINE/Module/Operators/Editor/GotoDetailView.xaml)

使用 `BooleanToVisibilityConverter` 根据 `IsHomeMode` 切换列显示：

* **Position 列**：Home 模式下显示 "Home"（只读 TextBlock），非 Home 模式下显示 Position 下拉框

* **OffsetVar + Offset 列**：Home 模式下隐藏

* **Mode/MinVel/MaxVel 列**：Home 模式下显示，非 Home 模式下隐藏

* **Speed 列**：Home 模式下隐藏（用 MaxVel 替代）

```xml
<!-- Position 列：Home 模式只读，普通模式可编辑 -->
<DataGridTemplateColumn Header="Position" Width="120">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding PositionName}" VerticalAlignment="Center" />
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
    <DataGridTemplateColumn.CellEditingTemplate>
        <DataTemplate>
            <!-- Home 模式：只读显示 "Home" -->
            <TextBlock Text="Home" VerticalAlignment="Center"
                       Visibility="{Binding DataContext.IsHomeMode, RelativeSource={RelativeSource AncestorType=UserControl}, Converter={StaticResource BoolToVis}}" />
            <!-- 普通 GOTO 模式：Position 下拉框 -->
            <ComboBox ItemsSource="{Binding AvailablePositions}"
                      SelectedItem="{Binding PositionName, UpdateSourceTrigger=PropertyChanged}"
                      IsEditable="False"
                      Visibility="{Binding DataContext.IsHomeMode, RelativeSource={RelativeSource AncestorType=UserControl}, Converter={StaticResource InverseBoolToVis}}" />
        </DataTemplate>
    </DataGridTemplateColumn.CellEditingTemplate>
</DataGridTemplateColumn>

<!-- Home 模式专用列：Mode/MinVel/MaxVel -->
<DataGridTextColumn Header="Mode" Binding="{Binding HomeMode}" Width="60"
                    Visibility="{Binding DataContext.IsHomeMode, RelativeSource={RelativeSource AncestorType=UserControl}, Converter={StaticResource BoolToVis}}" />
<DataGridTextColumn Header="MinVel" Binding="{Binding HomeMinVel}" Width="65"
                    Visibility="{Binding DataContext.IsHomeMode, RelativeSource={RelativeSource AncestorType=UserControl}, Converter={StaticResource BoolToVis}}" />
<DataGridTextColumn Header="MaxVel" Binding="{Binding HomeMaxVel}" Width="65"
                    Visibility="{Binding DataContext.IsHomeMode, RelativeSource={RelativeSource AncestorType=UserControl}, Converter={StaticResource BoolToVis}}" />
```

注意：DataGridTextColumn 的 Visibility 绑定需要使用 `Binding` 标记扩展（不是常规的 DataTrigger），可能需要改用 `DataGridTemplateColumn` 或在 Style 中设置。实际实现时将采用 DataTrigger 方式。

### Step 5: GotoStepAction — SiteFeature == "HOME" 时执行回零

**文件:** [GotoStepAction.cs](file:///c:/WorkFiles/GZQL_MACHINE/StationTasks/Actions/GotoStepAction.cs)

在 `ExecuteAsync` 中，根据 `step.SiteFeature` 判断是否为回零步骤：

```csharp
public async Task ExecuteAsync(ProcessStep step, StationTaskBase task, CancellationToken token)
{
    if (step.SubMoves == null || step.SubMoves.Count == 0)
    {
        _logger.Warn($"GOTO 步骤 [{step.Seq}] 没有 SubMove，跳过执行");
        return;
    }

    bool isHome = step.SiteFeature == "HOME";

    foreach (var subMove in step.SubMoves)
    {
        token.ThrowIfCancellationRequested();
        StationTaskBase targetTask = ResolveTargetTask(subMove.StationId, task);
        int axisId = ResolveAxisId(subMove, targetTask);
        string axisName = targetTask.GetAxisNameById(axisId);

        if (isHome)
        {
            // 回零模式：调用 Motion.HomeAsync
            string moveLabel = $"[{step.Seq}] {axisName} → Home (mode={subMove.HomeMode}, vel={subMove.HomeMinVel}/{subMove.HomeMaxVel})";
            targetTask.PublishStepStatus(moveLabel);
            await targetTask.ExecuteHomeAsync(axisId, subMove.HomeMode, subMove.HomeMinVel, subMove.HomeMaxVel);
            targetTask.CompleteStepStatus();
        }
        else
        {
            // 普通 GOTO 模式：绝对定位移动（现有逻辑不变）
            double totalOffset = CalculateTotalOffset(subMove, globalVars);
            double speed = subMove.Speed > 0 ? subMove.Speed : 10.0;
            double posValue = await targetTask.GetPositionValueAsync(subMove.PositionName, axisName);
            string moveLabel = $"[{step.Seq}] {axisName} → {subMove.PositionName} ({posValue:F3})";
            targetTask.PublishStepStatus(moveLabel);
            await targetTask.ExecuteMoveAsync(axisId, subMove.PositionName, speed, totalOffset);
            targetTask.CompleteStepStatus();
        }
    }
}
```

### Step 6: StationTaskBase — 新增 ExecuteHomeAsync 公开方法

**文件:** [StationTaskBase.cs](file:///c:/WorkFiles/GZQL_MACHINE/MotionControl/Services/StationTaskBase.cs)

新增公开的回零执行方法，供 GotoStepAction 调用：

```csharp
/// <summary>
/// 公开的回零执行方法，供 GotoStepAction 在 HOME 步骤时调用
/// 通过 RunStep 包装，享受暂停/急停/单步/可恢复异常保护
/// </summary>
public async Task ExecuteHomeAsync(int axisId, int mode = 1, double minVel = 5, double maxVel = 20)
{
    await RunStep($"Home Axis {axisId}", async () =>
    {
        await Motion.HomeAsync(axisId, mode, minVel, maxVel, CurrentToken);
    }, publishStatus: false);
}
```

### Step 7: ProcessStepExecutor — FormatStepLabel 适配 HOME 步骤

**文件:** [ProcessStepExecutor.cs](file:///c:/WorkFiles/GZQL_MACHINE/StationTasks/Actions/ProcessStepExecutor.cs)

修改 `FormatStepLabel`，当 SiteFeature == "HOME" 时显示 "Home" 而非位置名：

```csharp
private string FormatStepLabel(ProcessStep step)
{
    string label = $"[{step.Seq}] {step.Step}";

    if (step.SiteFeature == "HOME")
    {
        label += " → Home";
    }
    else if (step.Step == StepType.GOTO && step.SubMoves?.Count > 0)
    {
        var posNames = step.SubMoves
            .Where(sm => !string.IsNullOrEmpty(sm.PositionName))
            .Select(sm => sm.PositionName)
            .Distinct()
            .Take(3);
        var posText = string.Join(", ", posNames);
        if (!string.IsNullOrEmpty(posText))
            label += $" → {posText}";
    }

    return label;
}
```

### Step 8: AutoGenerate 适配

**文件:** [ProcessSequenceService.cs](file:///c:/WorkFiles/GZQL_MACHINE/Module/Services/ProcessSequenceService.cs)

修改 `AutoGenerate()` 中的 HOME 步骤，添加回零参数：

```csharp
new ProcessStep { Seq = 1, Step = StepType.GOTO, CompFeature = "—", SiteFeature = "HOME",
    SubMoves = new ObservableCollection<SubMove>
    {
        new SubMove { SubSeq = "1a", Axis = "Y", PositionName = "Home", HomeMode = 1, HomeMinVel = 5, HomeMaxVel = 20 }
    } },
```

### Step 9: 编译验证

编译所有项目确保 0 错误。

***

## 文件变更清单

| 文件                                               | 变更类型 | 说明                                                                   |
| ------------------------------------------------ | ---- | -------------------------------------------------------------------- |
| `StationTasks/Models/ProcessStep.cs`             | 修改   | SubMove 新增 HomeMode/HomeMinVel/HomeMaxVel 属性                         |
| `Module/Operators/Editor/SubMoveRowViewModel.cs` | 修改   | 转发 HomeMode/HomeMinVel/HomeMaxVel 属性                                 |
| `Module/Operators/Editor/GotoDetailViewModel.cs` | 修改   | 新增 IsHomeMode 属性；OnSave 中 Home 模式强制 PositionName="Home"              |
| `Module/Operators/Editor/GotoDetailView.xaml`    | 修改   | Home 模式下 Position 只读、隐藏 OffsetVar/Offset/Speed、显示 Mode/MinVel/MaxVel |
| `StationTasks/Actions/GotoStepAction.cs`         | 修改   | SiteFeature=="HOME" 时调用 ExecuteHomeAsync 执行回零                        |
| `MotionControl/Services/StationTaskBase.cs`      | 修改   | 新增 ExecuteHomeAsync 公开方法                                             |
| `StationTasks/Actions/ProcessStepExecutor.cs`    | 修改   | FormatStepLabel 适配 HOME 步骤显示                                         |
| `Module/Services/ProcessSequenceService.cs`      | 修改   | AutoGenerate 适配 HOME 步骤回零参数                                          |

## 设计要点

1. **SubMove 复用**：回零步骤仍使用 SubMove 模型，通过 `HomeMode/HomeMinVel/HomeMaxVel` 字段传递回零参数，PositionName 强制为 "Home"。不新增 StepType.HOME 枚举值，保持向后兼容。

2. **UI 切换**：通过 `IsHomeMode` 布尔属性控制 DataGrid 列的显示/隐藏，同一弹窗适配两种模式。

3. **执行路由**：GotoStepAction 根据 `step.SiteFeature == "HOME"` 分支执行，调用 `ExecuteHomeAsync`（底层调用 `Motion.HomeAsync`），而非 `ExecuteMoveAsync`。

4. **向后兼容**：现有不含 HomeMode/HomeMinVel/HomeMaxVel 字段的 JSON 文件反序列化时，使用默认值（mode=1, minVel=5, maxVel=20）。

