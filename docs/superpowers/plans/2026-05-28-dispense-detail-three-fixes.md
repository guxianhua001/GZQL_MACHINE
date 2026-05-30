# DispenseDetailView 三项缺陷修复实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复 DispenseDetailView 的线段导入不可用、Step3 提取CAD Z高度按钮不可用、参数模式切换区域颜色对比度不足三个问题

**Architecture:** 
1. 线段导入：DispenseDetailViewModel 当前通过 IStationRegistry 获取 Segments（工站可能未注册/Segments为空），改为通过 IEventAggregator 事件订阅 CadPointEditorViewModel 的 Segments 集合，或更简单地——在 ShowDispenseDetailDialog 时直接传入当前 CadPointEditorViewModel 的 Segments 引用
2. 提取CAD Z高度：CadPointEditorViewModel 的 ExtractCADZValuesCommand 缺少 RaiseCanExecuteChanged 调用
3. 颜色对比度：Step3EditParamsPanel 参数模式切换 Card 的 Background="#E3F2FD"（浅蓝）与默认文字颜色对比度不足

**Tech Stack:** WPF + PRISM + MaterialDesign In XAML

---

## 文件结构

| 操作 | 文件 | 职责 |
|------|------|------|
| 修改 | `Module\Controls\StepDetails\DispenseDetailViewModel.cs` | 添加 SourceSegments 属性，修改 GetSourceSegments 逻辑 |
| 修改 | `Module\Controls\StepEditor\ProcessSequenceEditorViewModel.cs` | ShowDispenseDetailDialog 时传入 CadPointEditorViewModel.Segments |
| 修改 | `Module\Controls\Cad\CadPointEditorViewModel.cs` | SelectedSegment setter 中添加 ExtractCADZValuesCommand.RaiseCanExecuteChanged() |
| 修改 | `Module\Controls\Cad\Step3EditParamsPanel.xaml` | 参数模式切换区域颜色调整 |
| 修改 | `MainApp\Languages\Strings.zh-CN.xaml` | 新增多语言键（如需） |
| 修改 | `MainApp\Languages\Strings.en-US.xaml` | 新增多语言键（如需） |

---

### Task 1: 修复线段数据导入——从 Step3EditParamsPanel 的 Segments 获取数据

**问题根因：** `DispenseDetailViewModel.GetSourceSegments()` 通过 `IStationRegistry.GetStation("DispenserStation")` 获取 Segments，但工站可能未注册或 `DispenserStationParams.Segments` 为空。实际数据源是 `CadPointEditorViewModel.Segments`（用户在 Step3 中导入 DXF 后提取的轨迹段）。

**方案：** 在 `DispenseDetailViewModel` 中添加 `SourceSegments` 属性，由外部调用者（`ProcessSequenceEditorViewModel`）在创建时注入当前 CadPointEditorViewModel 的 Segments 引用。

**Files:**
- Modify: `Module\Controls\StepDetails\DispenseDetailViewModel.cs`
- Modify: `Module\Controls\StepEditor\ProcessSequenceEditorViewModel.cs`

- [ ] **Step 1: 在 DispenseDetailViewModel 中添加 SourceSegments 属性**

在 `DispenseDetailViewModel.cs` 的 `#region 可用源段集合` 区域中，修改 `AvailableSourceSegments` 为由外部注入的源：

```csharp
#region 可用源段集合

private ObservableCollection<DispenseSegment> _sourceSegments;
/// <summary>外部注入的源分段集合（来自 CadPointEditorViewModel.Segments）</summary>
public ObservableCollection<DispenseSegment> SourceSegments
{
    get => _sourceSegments;
    set => SetProperty(ref _sourceSegments, value);
}

#endregion
```

删除原有的 `AvailableSourceSegments` 属性。

- [ ] **Step 2: 修改 GetSourceSegments 方法**

将 `GetSourceSegments()` 改为优先使用 `SourceSegments`，回退到 IStationRegistry：

```csharp
/// <summary>
/// 获取源分段集合——优先使用外部注入的 SourceSegments，回退到工站参数
/// </summary>
private List<DispenseSegment> GetSourceSegments()
{
    if (_sourceSegments != null && _sourceSegments.Count > 0)
        return _sourceSegments.ToList();

    try
    {
        var station = _stationRegistry.GetStation("DispenserStation");
        if (station is IStationParameterProvider provider)
        {
            var paramsObj = provider.CurrentParameters;
            if (paramsObj is DispenserStationParams dispenserParams)
            {
                return dispenserParams.Segments ?? new List<DispenseSegment>();
            }
        }
    }
    catch (Exception ex)
    {
        _logger.Warn($"获取点胶工站段数据失败: {ex.Message}");
    }
    return new List<DispenseSegment>();
}
```

- [ ] **Step 3: 修改 ProcessSequenceEditorViewModel 的 ShowDispenseDetailDialog**

在 `ProcessSequenceEditorViewModel.cs` 的 `ShowDispenseDetailDialog` 方法中，获取当前 CadPointEditorViewModel 的 Segments 并注入：

```csharp
private async void ShowDispenseDetailDialog(ProcessStep step)
{
    var vm = _containerProvider.Resolve<DispenseDetailViewModel>();
    var view = new DispenseDetailView();
    view.DataContext = vm;
    vm.Step = step;

    // 注入 CadPointEditorViewModel 的 Segments 作为线段导入数据源
    try
    {
        var cadVm = _containerProvider.Resolve<CadPointEditorViewModel>();
        if (cadVm?.Segments != null && cadVm.Segments.Count > 0)
            vm.SourceSegments = cadVm.Segments;
    }
    catch { /* CadPointEditorViewModel 未注册时忽略 */ }

    await ShowDialogSafely(view);
    await AutoSaveSequenceAsync();
}
```

注意：需确认 CadPointEditorViewModel 是否在 DI 容器中注册。如果未注册为单例/瞬态，则需要通过其他方式获取（如 IRegionManager 中活动视图的 DataContext）。需检查 PrimModel.cs 中的注册方式。

- [ ] **Step 4: 验证 CadPointEditorViewModel 的 DI 注册方式**

检查 `PrimModel.cs` 中 CadPointEditorViewModel 的注册。如果是 `RegisterForNavigation`，则无法通过 `Resolve` 获取实例（它是导航时才创建的）。此时需要改为通过 Region 导航获取活动视图的 DataContext，或使用 IEventAggregator 发布 Segments 数据。

替代方案：在 ProcessSequenceEditorViewModel 中维护对当前 CadPointEditorView 活跃实例的引用，通过 Region 获取。

- [ ] **Step 5: 构建验证**

Run: `dotnet build MainApp\MainApp.csproj`
Expected: Build succeeded, 0 errors

---

### Task 2: 修复提取 CAD Z 高度按钮不可用

**问题根因：** `CadPointEditorViewModel.ExtractCADZValuesCommand` 使用 lazy 初始化，CanExecute 条件依赖 `_selectedSegment`，但 `SelectedSegment` 的 setter 中从未调用 `ExtractCADZValuesCommand.RaiseCanExecuteChanged()`，导致按钮始终处于禁用状态。

**Files:**
- Modify: `Module\Controls\Cad\CadPointEditorViewModel.cs`

- [ ] **Step 1: 在 SelectedSegment setter 中添加 RaiseCanExecuteChanged**

在 `CadPointEditorViewModel.cs` 的 `SelectedSegment` 属性 setter 中，`ApplySegmentSplitCommand.RaiseCanExecuteChanged()` 之后添加：

```csharp
ExtractCADZValuesCommand.RaiseCanExecuteChanged();
```

完整 setter 片段（约 line 380-409）：
```csharp
set
{
    if (SetProperty(ref _selectedSegment, value))
    {
        RaisePropertyChanged(nameof(HasSelectedSegment));
        RaisePropertyChanged(nameof(ShowContinuousInterpolationParams));
        SelectedSegmentPoints = value?.Points;
        SegmentSplitCount = value?.SamplePointCount > 0 ? value.SamplePointCount : value?.Points?.Count ?? 1;
        SyncSelectedEntityFromSegment(value);
        ApplySegmentSplitCommand.RaiseCanExecuteChanged();
        ExtractCADZValuesCommand.RaiseCanExecuteChanged();
    }
}
```

- [ ] **Step 2: 构建验证**

Run: `dotnet build MainApp\MainApp.csproj`
Expected: Build succeeded, 0 errors

---

### Task 3: 修复参数模式切换区域颜色对比度不足

**问题根因：** `Step3EditParamsPanel.xaml` 中参数模式切换 Card 的 `Background="#E3F2FD"`（Material Design 浅蓝 100），RadioButton 使用默认前景色（在浅色主题下接近黑色，在深色主题下可能不可见），整体对比度不足导致文字不清晰。

**方案：** 将 Card 背景色改为更深的蓝色调（如 `#BBDEFB` Blue 100 → `#1565C0` Blue 800 文字 + `#E3F2FD` 背景），或改为白色背景加左侧色条强调。同时显式设置 RadioButton 和标题文字的前景色确保对比度。

**Files:**
- Modify: `Module\Controls\Cad\Step3EditParamsPanel.xaml`

- [ ] **Step 1: 修改参数模式切换 Card 的样式**

将 `Step3EditParamsPanel.xaml` 中约 line 23-42 的参数模式切换区域：

```xml
<!-- 参数模式切换 -->
<materialDesign:Card Padding="10" Margin="0,0,0,8" Background="#E3F2FD">
    <StackPanel>
        <TextBlock Text="{lang:Lang Step3_Section_DispenseMode}" FontWeight="Bold" FontSize="12" Margin="0,0,0,6"/>
        <StackPanel Orientation="Horizontal">
            <RadioButton Content="{lang:Lang Step3_Radio_ContinuousInterpolation}"
                         IsChecked="{Binding IsContinuousInterpolationMode}"
                         GroupName="DispenseMode" Margin="0,0,16,0"
                         FontSize="12"/>
            <RadioButton Content="{lang:Lang Step3_Radio_SinglePoint}"
                         IsChecked="{Binding IsSinglePointMode}"
                         GroupName="DispenseMode"
                         FontSize="12"/>
        </StackPanel>
        <TextBlock Text="{lang:Lang Step3_Desc_ModeSwitch}"
                   FontSize="10" Foreground="#9E9E9E" TextWrapping="Wrap" Margin="0,4,0,0"/>
    </StackPanel>
</materialDesign:Card>
```

改为白色背景 + 左侧蓝色边条 + 显式前景色：

```xml
<!-- 参数模式切换 -->
<materialDesign:Card Padding="10" Margin="0,0,0,8">
    <DockPanel>
        <Border DockPanel.Dock="Left" Width="4" Background="#1565C0" CornerRadius="2,0,0,2" Margin="0,0,10,0"/>
        <StackPanel>
            <TextBlock Text="{lang:Lang Step3_Section_DispenseMode}" FontWeight="Bold" FontSize="12"
                       Foreground="#1565C0" Margin="0,0,0,6"/>
            <StackPanel Orientation="Horizontal">
                <RadioButton Content="{lang:Lang Step3_Radio_ContinuousInterpolation}"
                             IsChecked="{Binding IsContinuousInterpolationMode}"
                             GroupName="DispenseMode" Margin="0,0,16,0"
                             FontSize="12" Foreground="#212121"/>
                <RadioButton Content="{lang:Lang Step3_Radio_SinglePoint}"
                             IsChecked="{Binding IsSinglePointMode}"
                             GroupName="DispenseMode"
                             FontSize="12" Foreground="#212121"/>
            </StackPanel>
            <TextBlock Text="{lang:Lang Step3_Desc_ModeSwitch}"
                       FontSize="10" Foreground="#757575" TextWrapping="Wrap" Margin="0,4,0,0"/>
        </StackPanel>
    </DockPanel>
</materialDesign:Card>
```

- [ ] **Step 2: 构建验证**

Run: `dotnet build MainApp\MainApp.csproj`
Expected: Build succeeded, 0 errors

---

### Task 4: 最终构建验证

- [ ] **Step 1: 完整构建**

Run: `dotnet build MainApp\MainApp.csproj`
Expected: Build succeeded, 0 errors

- [ ] **Step 2: 检查多语言资源完整性**

确认 Strings.zh-CN.xaml 和 Strings.en-US.xaml 中所有新增的 lang:Lang 键都已定义（本计划未新增语言键，但需确认现有键完整）。

---

## Self-Review

**1. Spec coverage:**
- ✅ 问题1（线段导入不可用）→ Task 1
- ✅ 问题2（提取CAD Z高度按钮不可用）→ Task 2
- ✅ 问题3（参数模式切换颜色不清晰）→ Task 3

**2. Placeholder scan:**
- Task 1 Step 4 中有"需确认"和"替代方案"描述，这是因为 CadPointEditorViewModel 的 DI 注册方式需要实际检查后才能确定最终方案。实施时需先检查再决定。

**3. Type consistency:**
- `SourceSegments` 类型为 `ObservableCollection<DispenseSegment>`，与 `CadPointEditorViewModel.Segments` 类型一致
- `GetSourceSegments()` 返回 `List<DispenseSegment>`，使用 `.ToList()` 转换 ObservableCollection
- `ExtractCADZValuesCommand` 是 `DelegateCommand`（非泛型），`RaiseCanExecuteChanged()` 方法签名匹配
