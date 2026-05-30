# DispenseDetailView 优化实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 优化 DispenseDetailView 的三项功能——点胶模式背景色修改、Execution Control 区域重构（删除待机高度/增加Z向补偿+全局变量链接/增加空跑与真实点胶模式）、Step3 参数保存后同步到 Dispense 工具

**Architecture:**
1. 点胶模式 Card 背景色：从 `#E8F5E9`（浅绿）改为白色背景+左侧蓝色边条+显式前景色，与 Step3EditParamsPanel 已修复的模式保持一致
2. Execution Control 重构：删除 StandbyHeight，新增 ZCompensation（可链接全局变量+手动补偿值），新增 DryRun/RealDispense 模式 CheckBox
3. Step3 参数同步：CadPointEditorViewModel 中每段参数变更时，通过 IDispenseSegmentStore 通知 DispenseDetailViewModel 刷新 SegmentRefs 的来源信息

**Tech Stack:** WPF + PRISM + MaterialDesign In XAML + IDispenseSegmentStore

---

## 文件结构

| 操作 | 文件 | 职责 |
|------|------|------|
| 修改 | `Module\Controls\StepDetails\DispenseDetailView.xaml` | 点胶模式 Card 样式、Execution Control 区域重构 |
| 修改 | `Module\Controls\StepDetails\DispenseDetailViewModel.cs` | 删除 StandbyHeight 属性，新增 ZCompensation/GlobalVariable/DispenseMode 属性 |
| 修改 | `Core\Models\DispenseDetail.cs` | 删除 StandbyHeight 字段，新增 ZCompensation/GlobalVariable/DispenseMode 字段 |
| 修改 | `StationTasks\Actions\DispenseStepAction.cs` | 适配新 Execution Control 字段（删除 StandbyHeight 引用，使用 ZCompensation） |
| 修改 | `Core\Abstraction\IDispenseSegmentStore.cs` | 新增 SegmentsChanged 事件通知 |
| 修改 | `Core\Services\DispenseSegmentStore.cs` | 实现 SegmentsChanged 事件触发 |
| 修改 | `Module\Controls\Cad\CadPointEditorViewModel.cs` | Segments 属性变更时通知 Store |
| 修改 | `MainApp\Languages\Strings.zh-CN.xaml` | 新增多语言键 |
| 修改 | `MainApp\Languages\Strings.en-US.xaml` | 新增多语言键 |

---

### Task 1: 点胶模式 Card 背景色修改

**问题：** DispenseDetailView 中点胶模式 Card 使用 `Background="#E8F5E9"`（浅绿），文字颜色未显式设置，对比度不足。

**方案：** 与 Step3EditParamsPanel 已修复的模式一致——白色背景+左侧蓝色边条+显式前景色。

**Files:**
- Modify: `Module\Controls\StepDetails\DispenseDetailView.xaml:36-60`

- [ ] **Step 1: 修改 Dispense Mode Section 的 Card 样式**

将 DispenseDetailView.xaml 第 36-60 行的点胶模式区域：

```xml
<!-- Dispense Mode Section -->
<materialDesign:Card Padding="10" Margin="0,0,0,8" Background="#E8F5E9">
    <StackPanel>
        <TextBlock Text="{lang:Lang DispenseDetail_DispenseMode}" FontWeight="Bold" FontSize="13" Margin="0,0,0,6"/>
        <StackPanel Orientation="Horizontal" Margin="0,0,0,4">
            <RadioButton Content="{lang:Lang DispenseDetail_DotMode}"
                         IsChecked="{Binding IsDotMode}"
                         GroupName="DispenseModeGroup"
                         Margin="0,0,16,0" />
            <RadioButton Content="{lang:Lang DispenseDetail_ArcMode}"
                         IsChecked="{Binding IsArcMode}"
                         GroupName="DispenseModeGroup" />
        </StackPanel>
        <CheckBox Content="{lang:Lang DispenseDetail_EnableZCalibration}"
                  IsChecked="{Binding EnableZCalibration}"
                  Margin="0,4,0,0" />
        <StackPanel Orientation="Horizontal" Margin="24,4,0,0"
                    Visibility="{Binding EnableZCalibration, Converter={StaticResource BoolToVisibilityConverter}}">
            <TextBlock Text="{lang:Lang DispenseDetail_ZCalibrationHeight}" VerticalAlignment="Center" Margin="0,0,4,0"/>
            <TextBox Style="{StaticResource MaterialDesignOutlinedTextBox}"
                     Text="{Binding ZCalibrationHeight, StringFormat=F1, UpdateSourceTrigger=LostFocus}"
                     Padding="4,2" FontSize="11" Width="70" Margin="0,0,4,0"/>
            <TextBlock Text="mm" VerticalAlignment="Center" FontSize="11" Foreground="Gray" Margin="0,0,16,0"/>
            <TextBlock Text="{lang:Lang DispenseDetail_ZCalibrationSpeed}" VerticalAlignment="Center" Margin="0,0,4,0"/>
            <TextBox Style="{StaticResource MaterialDesignOutlinedTextBox}"
                     Text="{Binding ZCalibrationSpeed, StringFormat=F1, UpdateSourceTrigger=LostFocus}"
                     Padding="4,2" FontSize="11" Width="70" Margin="0,0,4,0"/>
            <TextBlock Text="mm/s" VerticalAlignment="Center" FontSize="11" Foreground="Gray"/>
        </StackPanel>
    </StackPanel>
</materialDesign:Card>
```

改为：

```xml
<!-- Dispense Mode Section -->
<materialDesign:Card Padding="10" Margin="0,0,0,8">
    <DockPanel>
        <Border DockPanel.Dock="Left" Width="4" Background="#1565C0" CornerRadius="2,0,0,2" Margin="0,0,10,0"/>
        <StackPanel>
            <TextBlock Text="{lang:Lang DispenseDetail_DispenseMode}" FontWeight="Bold" FontSize="13"
                       Foreground="#1565C0" Margin="0,0,0,6"/>
            <StackPanel Orientation="Horizontal" Margin="0,0,0,4">
                <RadioButton Content="{lang:Lang DispenseDetail_DotMode}"
                             IsChecked="{Binding IsDotMode}"
                             GroupName="DispenseModeGroup"
                             Margin="0,0,16,0" Foreground="#212121"/>
                <RadioButton Content="{lang:Lang DispenseDetail_ArcMode}"
                             IsChecked="{Binding IsArcMode}"
                             GroupName="DispenseModeGroup" Foreground="#212121"/>
            </StackPanel>
            <CheckBox Content="{lang:Lang DispenseDetail_EnableZCalibration}"
                      IsChecked="{Binding EnableZCalibration}"
                      Margin="0,4,0,0" Foreground="#212121"/>
            <StackPanel Orientation="Horizontal" Margin="24,4,0,0"
                        Visibility="{Binding EnableZCalibration, Converter={StaticResource BoolToVisibilityConverter}}">
                <TextBlock Text="{lang:Lang DispenseDetail_ZCalibrationHeight}" VerticalAlignment="Center" Margin="0,0,4,0"/>
                <TextBox Style="{StaticResource MaterialDesignOutlinedTextBox}"
                         Text="{Binding ZCalibrationHeight, StringFormat=F1, UpdateSourceTrigger=LostFocus}"
                         Padding="4,2" FontSize="11" Width="70" Margin="0,0,4,0"/>
                <TextBlock Text="mm" VerticalAlignment="Center" FontSize="11" Foreground="Gray" Margin="0,0,16,0"/>
                <TextBlock Text="{lang:Lang DispenseDetail_ZCalibrationSpeed}" VerticalAlignment="Center" Margin="0,0,4,0"/>
                <TextBox Style="{StaticResource MaterialDesignOutlinedTextBox}"
                         Text="{Binding ZCalibrationSpeed, StringFormat=F1, UpdateSourceTrigger=LostFocus}"
                         Padding="4,2" FontSize="11" Width="70" Margin="0,0,4,0"/>
                <TextBlock Text="mm/s" VerticalAlignment="Center" FontSize="11" Foreground="Gray"/>
            </StackPanel>
        </StackPanel>
    </DockPanel>
</materialDesign:Card>
```

- [ ] **Step 2: 构建验证**

Run: `dotnet build MainApp\MainApp.csproj`
Expected: Build succeeded, 0 errors

---

### Task 2: Execution Control 重构——删除待机高度，增加 Z 向补偿+全局变量链接+空跑/真实点胶模式

**需求分析：**
- **删除** StandbyHeight（待机高度）——执行层改用 SafeHeight 替代
- **新增** ZCompensation（Z向补偿值）——手动输入补偿值
- **新增** ZCompensationGlobalVariable（Z向补偿全局变量名）——可链接全局变量，带链接图标
- **新增** IsDryRunMode（空跑模式）——CheckBox，勾选=空跑不出胶，不勾选=真实点胶
- **新增** IsRealDispenseMode（真实点胶模式）——CheckBox，与空跑互斥

**UI 设计：** Execution Control 区域改为两行布局：
- 第一行：Z向补偿（手动值输入 + 全局变量下拉 + 链接图标）
- 第二行：空跑/真实点胶模式 CheckBox

**Files:**
- Modify: `Core\Models\DispenseDetail.cs`
- Modify: `Module\Controls\StepDetails\DispenseDetailViewModel.cs`
- Modify: `Module\Controls\StepDetails\DispenseDetailView.xaml`
- Modify: `StationTasks\Actions\DispenseStepAction.cs`
- Modify: `MainApp\Languages\Strings.zh-CN.xaml`
- Modify: `MainApp\Languages\Strings.en-US.xaml`

- [ ] **Step 1: 修改 DispenseDetail 模型——删除 StandbyHeight，新增字段**

在 `Core\Models\DispenseDetail.cs` 的 `#region 执行控制` 中：

删除：
```csharp
private double _standbyHeight = 10.0;
/// <summary>待机高度 mm（步骤开始前Z轴抬升到此高度）</summary>
public double StandbyHeight
{
    get => _standbyHeight;
    set => SetProperty(ref _standbyHeight, value);
}
```

替换为：
```csharp
private double _zCompensation = 0.0;
/// <summary>Z向补偿值 mm（手动输入，叠加到全局变量值或直接使用）</summary>
public double ZCompensation
{
    get => _zCompensation;
    set => SetProperty(ref _zCompensation, value);
}

private string _zCompensationGlobalVariable;
/// <summary>Z向补偿关联的全局变量名（为空时使用手动补偿值）</summary>
public string ZCompensationGlobalVariable
{
    get => _zCompensationGlobalVariable;
    set => SetProperty(ref _zCompensationGlobalVariable, value);
}

private bool _isDryRunMode = true;
/// <summary>空跑模式（默认 true，不出胶只走轨迹，安全验证用）</summary>
public bool IsDryRunMode
{
    get => _isDryRunMode;
    set => SetProperty(ref _isDryRunMode, value);
}

private bool _isRealDispenseMode;
/// <summary>真实点胶模式（与空跑互斥，启用时实际出胶）</summary>
public bool IsRealDispenseMode
{
    get => _isRealDispenseMode;
    set
    {
        if (SetProperty(ref _isRealDispenseMode, value))
            IsDryRunMode = !value;
    }
}
```

- [ ] **Step 2: 修改 DispenseDetailViewModel——删除 StandbyHeight 属性，新增属性**

在 `Module\Controls\StepDetails\DispenseDetailViewModel.cs` 的 `#region 执行控制` 中：

删除：
```csharp
public double StandbyHeight
{
    get => _step?.DispenseDetail?.StandbyHeight ?? 10.0;
    set { if (_step?.DispenseDetail != null) _step.DispenseDetail.StandbyHeight = value; }
}

public bool ExecuteDryRunFirst
{
    get => _step?.DispenseDetail?.ExecuteDryRunFirst ?? true;
    set { if (_step?.DispenseDetail != null) _step.DispenseDetail.ExecuteDryRunFirst = value; }
}
```

替换为：
```csharp
public double ZCompensation
{
    get => _step?.DispenseDetail?.ZCompensation ?? 0.0;
    set { if (_step?.DispenseDetail != null) _step.DispenseDetail.ZCompensation = value; }
}

public string ZCompensationGlobalVariable
{
    get => _step?.DispenseDetail?.ZCompensationGlobalVariable;
    set { if (_step?.DispenseDetail != null) _step.DispenseDetail.ZCompensationGlobalVariable = value; }
}

public bool IsDryRunMode
{
    get => _step?.DispenseDetail?.IsDryRunMode ?? true;
    set { if (_step?.DispenseDetail != null) _step.DispenseDetail.IsDryRunMode = value; }
}

public bool IsRealDispenseMode
{
    get => _step?.DispenseDetail?.IsRealDispenseMode ?? false;
    set { if (_step?.DispenseDetail != null) _step.DispenseDetail.IsRealDispenseMode = value; }
}
```

同时在 Step setter 中删除 `RaisePropertyChanged(nameof(StandbyHeight));` 和 `RaisePropertyChanged(nameof(ExecuteDryRunFirst));`，替换为：
```csharp
RaisePropertyChanged(nameof(ZCompensation));
RaisePropertyChanged(nameof(ZCompensationGlobalVariable));
RaisePropertyChanged(nameof(IsDryRunMode));
RaisePropertyChanged(nameof(IsRealDispenseMode));
```

添加全局变量选项加载逻辑。在 DispenseDetailViewModel 中新增：

```csharp
private ObservableCollection<string> _globalVariableOptions;
/// <summary>全局变量名称下拉选项</summary>
public ObservableCollection<string> GlobalVariableOptions
{
    get => _globalVariableOptions;
    set => SetProperty(ref _globalVariableOptions, value);
}

/// <summary>从配方池加载全局变量名称列表</summary>
private async Task LoadGlobalVariablesAsync()
{
    try
    {
        var poolId = _recipePoolService.CurrentPoolName;
        if (string.IsNullOrEmpty(poolId)) return;

        var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);
        var options = new ObservableCollection<string> { "" };
        foreach (var v in variables)
            options.Add(v.Name);
        GlobalVariableOptions = options;
    }
    catch
    {
        GlobalVariableOptions = new ObservableCollection<string> { "" };
    }
}
```

在构造函数末尾调用 `LoadGlobalVariablesAsync().ConfigureAwait(false);`。

需要添加 using：`using System.Threading.Tasks;`（如果尚未存在）。

- [ ] **Step 3: 修改 DispenseDetailView.xaml——Execution Control 区域重构**

将 DispenseDetailView.xaml 中 Execution Control Section（约 line 455-470）：

```xml
<!-- Execution Control Section -->
<materialDesign:Card Padding="10" Margin="0,0,0,8">
    <StackPanel>
        <TextBlock Text="{lang:Lang DispenseDetail_ExecutionControl}" FontWeight="Bold" FontSize="13" Margin="0,0,0,6"/>
        <StackPanel Orientation="Horizontal" Margin="0,0,0,4">
            <TextBlock Text="{lang:Lang DispenseDetail_StandbyHeight}" VerticalAlignment="Center" Margin="0,0,4,0"/>
            <TextBox Style="{StaticResource MaterialDesignOutlinedTextBox}"
                     Text="{Binding StandbyHeight, StringFormat=F1, UpdateSourceTrigger=LostFocus}"
                     Padding="4,2" FontSize="11" Width="70" Margin="0,0,4,0"/>
            <TextBlock Text="mm" VerticalAlignment="Center" FontSize="11" Foreground="Gray"/>
        </StackPanel>
        <CheckBox Content="{lang:Lang DispenseDetail_ExecuteDryRunFirst}"
                  IsChecked="{Binding ExecuteDryRunFirst}" Margin="0,4,0,0"/>
    </StackPanel>
</materialDesign:Card>
```

替换为：

```xml
<!-- Execution Control Section -->
<materialDesign:Card Padding="10" Margin="0,0,0,8">
    <StackPanel>
        <TextBlock Text="{lang:Lang DispenseDetail_ExecutionControl}" FontWeight="Bold" FontSize="13"
                   Foreground="#1565C0" Margin="0,0,0,8"/>

        <!-- Z向补偿行：手动值 + 全局变量链接 -->
        <Grid Margin="0,0,0,8">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="80"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <TextBlock Grid.Column="0" Text="{lang:Lang DispenseDetail_ZCompensation}"
                       VerticalAlignment="Center" Margin="0,0,6,0" FontSize="12" Foreground="#212121"/>
            <TextBox Grid.Column="1" Style="{StaticResource MaterialDesignOutlinedTextBox}"
                     Text="{Binding ZCompensation, StringFormat=F2, UpdateSourceTrigger=LostFocus}"
                     Padding="4,2" FontSize="11" Margin="0,0,4,0"/>
            <TextBlock Grid.Column="2" Text="mm" VerticalAlignment="Center" FontSize="11" Foreground="Gray" Margin="0,0,12,0"/>

            <!-- 全局变量链接 -->
            <materialDesign:PackIcon Grid.Column="3" Kind="LinkVariant"
                                     VerticalAlignment="Center" Margin="0,0,4,0"
                                     Width="16" Height="16" Foreground="#42A5F5">
                <materialDesign:PackIcon.Style>
                    <Style TargetType="materialDesign:PackIcon">
                        <Setter Property="Visibility" Value="Collapsed"/>
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding ZCompensationGlobalVariable}" Value="{x:Null}">
                                <Setter Property="Visibility" Value="Collapsed"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </materialDesign:PackIcon.Style>
            </materialDesign:PackIcon>
            <ComboBox Grid.Column="4"
                      ItemsSource="{Binding GlobalVariableOptions}"
                      SelectedItem="{Binding ZCompensationGlobalVariable, UpdateSourceTrigger=PropertyChanged}"
                      IsEditable="True" FontSize="11" Margin="0,0,4,0"/>
            <TextBlock Grid.Column="5" Text="{lang:Lang DispenseDetail_GlobalVar}"
                       VerticalAlignment="Center" FontSize="10" Foreground="#9E9E9E"/>
        </Grid>

        <!-- 空跑/真实点胶模式 -->
        <StackPanel Orientation="Horizontal" Margin="0,0,0,4">
            <CheckBox Content="{lang:Lang DispenseDetail_DryRunMode}"
                      IsChecked="{Binding IsDryRunMode}"
                      Margin="0,0,24,0" Foreground="#212121"/>
            <CheckBox Content="{lang:Lang DispenseDetail_RealDispenseMode}"
                      IsChecked="{Binding IsRealDispenseMode}"
                      Foreground="#212121"/>
        </StackPanel>
        <TextBlock Text="{lang:Lang DispenseDetail_ModeWarning}"
                   FontSize="10" Foreground="#F44336" TextWrapping="Wrap" Margin="0,2,0,0"
                   Visibility="{Binding IsRealDispenseMode, Converter={StaticResource BoolToVisibilityConverter}}"/>
    </StackPanel>
</materialDesign:Card>
```

需要在 DispenseDetailView.xaml 的 Resources 中添加 NullToVisibilityConverter（如果尚未存在）：

```xml
<localConverters:NullToVisibilityConverter x:Key="NullToVisConv"/>
```

并在 UserControl 标签中确认 xmlns:localConverters 命名空间已声明。如果不存在，添加：
```xml
xmlns:conv="clr-namespace:Module.Converters"
```

- [ ] **Step 4: 修改 DispenseStepAction——适配新字段**

在 `StationTasks\Actions\DispenseStepAction.cs` 中：

1. 删除所有 `detail.StandbyHeight` 引用，替换为 `detail.DefaultSafeHeight`（空跑和真实执行结束后抬升到安全高度而非待机高度）

2. 将 `detail.ExecuteDryRunFirst` 替换为 `detail.IsDryRunMode`

3. 在 `ExecuteAsync` 方法中，根据 `IsDryRunMode` 决定执行路径：

```csharp
public async Task ExecuteAsync(ProcessStep step, StationTaskBase task, CancellationToken token)
{
    var detail = step.DispenseDetail;
    if (detail == null)
    {
        _logger.Warn($"DISPENSE 步骤 [{step.Seq}] 没有 DispenseDetail，跳过执行");
        return;
    }

    var sourceSegments = GetSourceSegments();
    var segDict = sourceSegments.Where(s => !string.IsNullOrEmpty(s.SegmentId))
        .ToDictionary(s => s.SegmentId, s => s);

    int dxAxisId = ResolveAxisId("Dx", task);
    int dyAxisId = ResolveAxisId("Dy", task);
    int dzAxisId = ResolveAxisId("Dz₁", task);

    try
    {
        if (detail.EnableZCalibration)
            await ExecuteZCalibrationAsync(detail, dzAxisId, token);

        if (detail.IsDryRunMode)
        {
            await ExecuteDryRunAsync(detail, segDict, dxAxisId, dyAxisId, dzAxisId, token);
        }
        else
        {
            switch (detail.DispenseMode)
            {
                case DispenseStepMode.Dot:
                    await ExecuteDotModeAsync(detail, segDict, dxAxisId, dyAxisId, dzAxisId, token);
                    break;
                case DispenseStepMode.Arc:
                    await ExecuteArcModeAsync(detail, segDict, dxAxisId, dyAxisId, dzAxisId, token);
                    break;
                default:
                    _logger.Warn($"DISPENSE 步骤 [{step.Seq}] 未知点胶模式: {detail.DispenseMode}");
                    break;
            }
        }
    }
    catch (OperationCanceledException)
    {
        SafeGlueOff();
        _logger.Warn($"DISPENSE 步骤 [{step.Seq}] 已取消，已安全关胶");
        throw;
    }
    catch (Exception ex)
    {
        SafeGlueOff();
        _logger.Error(ex, $"DISPENSE 步骤 [{step.Seq}] 执行异常，已安全关胶");
        throw;
    }
}
```

4. 在 `ExecuteDryRunAsync` 中，将 `detail.StandbyHeight` 替换为 `detail.DefaultSafeHeight`：

```csharp
await _motionService.MoveAbsAsync(dzAxisId, detail.DefaultSafeHeight, moveSpeed, token);
```

5. 在 `ExecuteDotModeAsync` 和 `ExecuteArcModeAsync` 中，同样将 `detail.StandbyHeight` 替换为 `detail.DefaultSafeHeight`。

6. 新增 ZCompensation 应用逻辑。在 `CreateSegmentWithParams` 方法中，Z向补偿叠加到 EffectiveZHeight：

```csharp
private double ResolveZCompensation(DispenseDetail detail)
{
    double compensation = detail.ZCompensation;

    if (!string.IsNullOrEmpty(detail.ZCompensationGlobalVariable))
    {
        try
        {
            var poolId = _recipePoolService.CurrentPoolName;
            if (!string.IsNullOrEmpty(poolId))
            {
                var variables = _recipePoolService.LoadGlobalVariablesAsync(poolId).Result;
                var gv = variables.FirstOrDefault(v => v.Name == detail.ZCompensationGlobalVariable);
                if (gv != null && double.TryParse(gv.Value, out double gvValue))
                    compensation += gvValue;
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"解析Z向补偿全局变量 '{detail.ZCompensationGlobalVariable}' 失败: {ex.Message}");
        }
    }

    return compensation;
}
```

在 `CreateSegmentWithParams` 末尾添加：
```csharp
seg.HeightCompensation += ResolveZCompensation(detail);
```

- [ ] **Step 5: 添加多语言键**

在 `MainApp\Languages\Strings.zh-CN.xaml` 中添加（在 DispenseDetail_ExecuteDryRunFirst 之后）：

```xml
<sys:String x:Key="DispenseDetail_ZCompensation">Z向补偿</sys:String>
<sys:String x:Key="DispenseDetail_GlobalVar">全局变量</sys:String>
<sys:String x:Key="DispenseDetail_DryRunMode">空跑模式</sys:String>
<sys:String x:Key="DispenseDetail_RealDispenseMode">真实点胶</sys:String>
<sys:String x:Key="DispenseDetail_ModeWarning">⚠ 真实点胶模式已启用，将实际出胶！</sys:String>
```

在 `MainApp\Languages\Strings.en-US.xaml` 中添加（在 DispenseDetail_ExecuteDryRunFirst 之后）：

```xml
<sys:String x:Key="DispenseDetail_ZCompensation">Z Compensation</sys:String>
<sys:String x:Key="DispenseDetail_GlobalVar">Global Var</sys:String>
<sys:String x:Key="DispenseDetail_DryRunMode">Dry Run Mode</sys:String>
<sys:String x:Key="DispenseDetail_RealDispenseMode">Real Dispense</sys:String>
<sys:String x:Key="DispenseDetail_ModeWarning">⚠ Real dispense mode enabled, glue will be applied!</sys:String>
```

- [ ] **Step 6: 构建验证**

Run: `dotnet build MainApp\MainApp.csproj`
Expected: Build succeeded, 0 errors

---

### Task 3: Step3 参数保存后同步到 Dispense 工具

**问题根因：** CadPointEditorViewModel 中每段点胶参数（MoveSpeed、TeachHeight、HeightCompensation 等）修改后，DispenseDetailViewModel 的 SegmentRefs 不会自动更新。SegmentRefs 只在导入时创建一次，之后源段的参数变更不会反映到 DispenseDetailView。

**方案：** 在 IDispenseSegmentStore 中增加 SegmentsChanged 事件，CadPointEditorViewModel 在 Segments 集合变更或段属性变更时触发事件，DispenseDetailViewModel 订阅该事件并刷新 SegmentRefs 的来源信息。

**Files:**
- Modify: `Core\Abstraction\IDispenseSegmentStore.cs`
- Modify: `Core\Services\DispenseSegmentStore.cs`
- Modify: `Module\Controls\Cad\CadPointEditorViewModel.cs`
- Modify: `Module\Controls\StepDetails\DispenseDetailViewModel.cs`

- [ ] **Step 1: 修改 IDispenseSegmentStore 接口——增加 SegmentsChanged 事件**

在 `Core\Abstraction\IDispenseSegmentStore.cs` 中添加事件：

```csharp
using Core.Models;
using System;
using System.Collections.ObjectModel;

namespace Core.Abstraction
{
    /// <summary>
    /// 点胶轨迹段共享存储接口——桥接 CadPointEditorViewModel 与 DispenseDetailViewModel
    /// CadPointEditorViewModel 在 Segments 变化时注册到此处，DispenseDetailViewModel 导入时从此处读取
    /// </summary>
    public interface IDispenseSegmentStore
    {
        /// <summary>当前可用的轨迹段集合（来自 CAD 编辑器）</summary>
        ObservableCollection<DispenseSegment> CurrentSegments { get; }

        /// <summary>注册轨迹段集合引用</summary>
        void RegisterSegments(ObservableCollection<DispenseSegment> segments);

        /// <summary>清除注册的轨迹段引用</summary>
        void ClearSegments();

        /// <summary>当 Segments 内容变更时触发（集合增删或段属性修改）</summary>
        event Action SegmentsChanged;

        /// <summary>通知 Segments 内容已变更（由注册方调用）</summary>
        void NotifySegmentsChanged();
    }
}
```

- [ ] **Step 2: 修改 DispenseSegmentStore 实现——增加事件触发**

在 `Core\Services\DispenseSegmentStore.cs` 中：

```csharp
using Core.Abstraction;
using Core.Models;
using System;
using System.Collections.ObjectModel;

namespace Core.Services
{
    /// <summary>
    /// 点胶轨迹段共享存储实现——单例，在 DI 容器中注册
    /// CadPointEditorViewModel 注册 Segments 引用，DispenseDetailViewModel 读取
    /// </summary>
    public class DispenseSegmentStore : IDispenseSegmentStore
    {
        private ObservableCollection<DispenseSegment> _currentSegments;

        public ObservableCollection<DispenseSegment> CurrentSegments => _currentSegments;

        public event Action SegmentsChanged;

        public void RegisterSegments(ObservableCollection<DispenseSegment> segments)
        {
            _currentSegments = segments;
        }

        public void ClearSegments()
        {
            _currentSegments = null;
        }

        public void NotifySegmentsChanged()
        {
            SegmentsChanged?.Invoke();
        }
    }
}
```

- [ ] **Step 3: 修改 CadPointEditorViewModel——段属性变更时通知 Store**

在 `Module\Controls\Cad\CadPointEditorViewModel.cs` 中：

1. 在 `OnSegmentsCollectionChanged` 方法末尾添加通知：

```csharp
private void OnSegmentsCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
{
    RaisePropertyChanged(nameof(SegmentSummaryDisplay));
    ApplySegmentSplitCommand.RaiseCanExecuteChanged();
    _dispenseSegmentStore?.NotifySegmentsChanged();
}
```

2. 在 `SelectedSegment` setter 中，当段属性可能变更时（用户在 Step3EditParamsPanel 中编辑参数后切换选中段），触发通知：

在 `SelectedSegment` setter 的 `if (SetProperty(...))` 块末尾添加：
```csharp
_dispenseSegmentStore?.NotifySegmentsChanged();
```

3. 在 `ExecuteApplySegmentSplit` 方法末尾（成功采样后）添加通知：
```csharp
_dispenseSegmentStore?.NotifySegmentsChanged();
```

- [ ] **Step 4: 修改 DispenseDetailViewModel——订阅 SegmentsChanged 事件**

在 `Module\Controls\StepDetails\DispenseDetailViewModel.cs` 中：

1. 在构造函数中订阅事件：

```csharp
public DispenseDetailViewModel(
    IContainerProvider containerProvider,
    ILoggerService logger,
    IRecipePoolService recipePoolService,
    IStationRegistry stationRegistry,
    IDispenseSegmentStore dispenseSegmentStore)
{
    _containerProvider = containerProvider;
    _logger = logger;
    _recipePoolService = recipePoolService;
    _stationRegistry = stationRegistry;
    _dispenseSegmentStore = dispenseSegmentStore;

    // 订阅 Segments 变更通知，自动刷新 SegmentRefs 来源信息
    if (_dispenseSegmentStore != null)
        _dispenseSegmentStore.SegmentsChanged += OnSourceSegmentsChanged;

    ImportLinesCommand = new DelegateCommand(OnImportLines);
    ImportArcsCommand = new DelegateCommand(OnImportArcs);
    RemoveSelectedCommand = new DelegateCommand(OnRemoveSelected);
    SelectAllCommand = new DelegateCommand(OnSelectAll);
    InvertSelectionCommand = new DelegateCommand(OnInvertSelection);
    CloseCommand = new DelegateCommand(OnClose);
    SaveCommand = new DelegateCommand(OnSave);

    LoadGlobalVariablesAsync().ConfigureAwait(false);
}
```

2. 添加事件处理方法：

```csharp
/// <summary>
/// 源段集合变更时刷新 SegmentRefs 的来源信息
/// </summary>
private void OnSourceSegmentsChanged()
{
    RefreshSourceSegmentInfo();
}
```

3. 添加 IDisposable 支持（取消订阅防止内存泄漏）。让 DispenseDetailViewModel 实现 IDisposable：

```csharp
public class DispenseDetailViewModel : BindableBase, INavigationAware, IDisposable
```

添加 Dispose 方法：
```csharp
public void Dispose()
{
    if (_dispenseSegmentStore != null)
        _dispenseSegmentStore.SegmentsChanged -= OnSourceSegmentsChanged;
}
```

- [ ] **Step 5: 构建验证**

Run: `dotnet build MainApp\MainApp.csproj`
Expected: Build succeeded, 0 errors

---

### Task 4: 最终构建验证与清理

- [ ] **Step 1: 完整构建**

Run: `dotnet build MainApp\MainApp.csproj`
Expected: Build succeeded, 0 errors

- [ ] **Step 2: 检查多语言资源完整性**

确认以下新增键在 Strings.zh-CN.xaml 和 Strings.en-US.xaml 中都已定义：
- DispenseDetail_ZCompensation
- DispenseDetail_GlobalVar
- DispenseDetail_DryRunMode
- DispenseDetail_RealDispenseMode
- DispenseDetail_ModeWarning

- [ ] **Step 3: 确认删除的旧字段无残留引用**

搜索以下关键词确认无残留引用：
- `StandbyHeight`（应仅在 DispenseDetail.cs 中保留 JsonProperty 兼容性注释，其他位置全部删除）
- `ExecuteDryRunFirst`（应全部替换为 IsDryRunMode）

---

## Self-Review

**1. Spec coverage:**
- ✅ 需求1（点胶模式背景色修改）→ Task 1
- ✅ 需求2（Execution Control 删除待机高度 + 增加Z向补偿+全局变量链接+空跑/真实点胶模式）→ Task 2
- ✅ 需求3（Step3 参数保存后同步到 Dispense 工具）→ Task 3

**2. Placeholder scan:**
- 无 TBD/TODO/占位符

**3. Type consistency:**
- `ZCompensation` 类型 `double`，与 DispenseSegment.HeightCompensation 兼容
- `ZCompensationGlobalVariable` 类型 `string`，与 SeekDetailViewModel.LinkedVariableName 一致
- `IsDryRunMode` / `IsRealDispenseMode` 类型 `bool`，互斥逻辑在 DispenseDetail.IsRealDispenseMode setter 中处理
- `GlobalVariableOptions` 类型 `ObservableCollection<string>`，与 SeekDetailViewModel 一致
- `SegmentsChanged` 事件类型 `Action`，与 Store 实现匹配
