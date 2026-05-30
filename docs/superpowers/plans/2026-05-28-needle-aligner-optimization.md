# NeedleAlignerView 优化实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 优化 NeedleAlignerView，使补偿值可链接全局变量（带链接图标），运动参数增加文本提示，搜索点设置增加示教按钮，修改应用补偿逻辑为"写入全局变量+保存参数"并删除执行运动。

**Architecture:** 参照 VisionCaptureViewModel 的全局变量链接模式，为 NeedleAlignerViewModel 新增 IRecipePoolService 依赖，添加 AvailableGlobalVariables 集合和 CompensationX/Y/ZLinkedVar 属性，通过 LoadGlobalVariablesAsync/SaveGlobalVariablesAsync 实现全局变量读写。应用补偿逻辑从"移动到位置+清零"改为"写入全局变量+保存参数"。搜索点示教按钮通过 IPositionMotionController.TeachAsync 读取当前坐标。

**Tech Stack:** WPF + PRISM + MaterialDesignInXAML + IRecipePoolService + IParameterStorage + IPositionMotionController

---

## 文件结构

| 操作 | 文件路径 | 职责 |
|------|---------|------|
| 修改 | `Module/Controls/Maintenance/NeedleAlignerViewModel.cs` | 新增 IRecipePoolService、全局变量链接属性、示教命令、修改应用补偿逻辑 |
| 修改 | `Module/Controls/Maintenance/NeedleAlignerView.xaml` | 补偿值增加链接图标+ComboBox、运动参数增加提示、搜索点增加示教按钮 |
| 修改 | `MainApp/Languages/Strings.zh-CN.xaml` | 新增多语言键 |
| 修改 | `MainApp/Languages/Strings.en-US.xaml` | 新增多语言键 |

---

### Task 1: 重构 ViewModel — 新增 IRecipePoolService、全局变量链接、示教命令、修改应用补偿逻辑

**Files:**
- Modify: `Module/Controls/Maintenance/NeedleAlignerViewModel.cs`

- [ ] **Step 1: 新增 IRecipePoolService 依赖和全局变量链接属性**

在 ViewModel 中：

1. 新增 `using Recipe.Interfaces;` 和 `using Core.Models;`（GlobalVariable）和 `using System.Linq;` 引用
2. 新增 `_recipePoolService` 字段和构造函数参数
3. 新增以下属性：

```csharp
private ObservableCollection<GlobalVariable> _availableGlobalVariables = new();
public ObservableCollection<GlobalVariable> AvailableGlobalVariables
{
    get => _availableGlobalVariables;
    set => SetProperty(ref _availableGlobalVariables, value);
}

private string _compensationXLinkedVar;
public string CompensationXLinkedVar
{
    get => _compensationXLinkedVar;
    set
    {
        if (SetProperty(ref _compensationXLinkedVar, value))
        {
            RaisePropertyChanged(nameof(IsCompensationXLinked));
            if (!string.IsNullOrEmpty(value))
            {
                var gv = AvailableGlobalVariables.FirstOrDefault(v => v.Name == value);
                if (gv != null && double.TryParse(gv.Value, out var val))
                    CompensationManager.CompensationX = val;
            }
        }
    }
}

private string _compensationYLinkedVar;
public string CompensationYLinkedVar
{
    get => _compensationYLinkedVar;
    set
    {
        if (SetProperty(ref _compensationYLinkedVar, value))
        {
            RaisePropertyChanged(nameof(IsCompensationYLinked));
            if (!string.IsNullOrEmpty(value))
            {
                var gv = AvailableGlobalVariables.FirstOrDefault(v => v.Name == value);
                if (gv != null && double.TryParse(gv.Value, out var val))
                    CompensationManager.CompensationY = val;
            }
        }
    }
}

private string _compensationZLinkedVar;
public string CompensationZLinkedVar
{
    get => _compensationZLinkedVar;
    set
    {
        if (SetProperty(ref _compensationZLinkedVar, value))
        {
            RaisePropertyChanged(nameof(IsCompensationZLinked));
            if (!string.IsNullOrEmpty(value))
            {
                var gv = AvailableGlobalVariables.FirstOrDefault(v => v.Name == value);
                if (gv != null && double.TryParse(gv.Value, out var val))
                    CompensationManager.CompensationZ = val;
            }
        }
    }
}

public bool IsCompensationXLinked => !string.IsNullOrEmpty(CompensationXLinkedVar);
public bool IsCompensationYLinked => !string.IsNullOrEmpty(CompensationYLinkedVar);
public bool IsCompensationZLinked => !string.IsNullOrEmpty(CompensationZLinkedVar);
```

- [ ] **Step 2: 新增搜索点示教命令**

```csharp
public DelegateCommand<int> TeachSearchPointCommand { get; }

// 构造函数中：
TeachSearchPointCommand = new DelegateCommand<int>(
    async step => await TeachSearchPointAsync(step),
    _ => !IsCalibrating)
    .ObservesProperty(() => IsCalibrating);

/// <summary>
/// 示教搜索点：读取当前运动位置并写入对应搜索点
/// </summary>
private async Task TeachSearchPointAsync(int step)
{
    try
    {
        var stationId = $"NeedleCalibration_System{SystemNumber}";
        var result = await _motionController.TeachAsync(stationId);

        if (result != null && result.Count > 0)
        {
            double x = 0, y = 0;
            if (result.TryGetValue("X", out double rx) || result.TryGetValue("Rx", out rx))
                x = rx;
            if (result.TryGetValue("Y", out double ry) || result.TryGetValue("GantryY", out ry))
                y = ry;

            switch (step)
            {
                case 1:
                    Parameters.SearchPoint1 = new PointF((float)x, (float)y);
                    break;
                case 2:
                    Parameters.SearchPoint2 = new PointF((float)x, (float)y);
                    break;
                case 3:
                    Parameters.SearchPoint3 = new PointF((float)x, (float)y);
                    break;
                case 4:
                    Parameters.SearchPoint4 = new PointF((float)x, (float)y);
                    break;
            }

            AddLog(string.Format(
                _localization.GetResourceOrDefault("NeedleAligner_Log_TeachSearchPoint", "搜索点{0}示教完成: X={1:F3}, Y={2:F3}"),
                step, x, y));
        }
    }
    catch (Exception ex)
    {
        AddLog(string.Format(
            _localization.GetResourceOrDefault("NeedleAligner_Log_TeachSearchPointError", "搜索点示教失败: {0}"),
            ex.Message));
    }
}
```

- [ ] **Step 3: 修改 ApplyCompensationAsync — 删除执行运动，改为写入全局变量+保存参数**

将 `ApplyCompensationAsync` 方法替换为：

```csharp
/// <summary>
/// 应用补偿值：将当前补偿值写入全局变量，然后保存参数
/// 考虑设备安全，不执行运动
/// </summary>
private async Task ApplyCompensationAsync()
{
    try
    {
        var compensation = new PointF(
            (float)CompensationManager.CompensationX,
            (float)CompensationManager.CompensationY,
            (float)CompensationManager.CompensationZ);

        _dialogService.ShowDialog("NotificationDialog", new DialogParameters
        {
            { "title", _localization.GetResourceOrDefault("NeedleAligner_Dialog_ApplyTitle", "确认应用补偿") },
            { "message", string.Format(
                _localization.GetResourceOrDefault("NeedleAligner_Dialog_ApplyToGlobalMessage",
                    "将以下补偿值写入全局变量：\nX={0:F3}, Y={1:F3}, Z={2:F3}\n并保存参数，确定继续吗？"),
                compensation.X, compensation.Y, compensation.Z) },
            { "icon", MaterialDesignThemes.Wpf.PackIconKind.HelpCircle }
        }, async result =>
        {
            if (result.Result == ButtonResult.OK || result.Result == ButtonResult.Yes)
            {
                await WriteCompensationToGlobalVariablesAsync(compensation);
                await SaveParametersAsync();

                AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_CompensationAppliedToGlobal", "补偿值已写入全局变量并保存参数"));
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_Compensation",
                        "补偿值: X={0:F3}, Y={1:F3}, Z={2:F3}"),
                    compensation.X, compensation.Y, compensation.Z));
            }
        });
    }
    catch (Exception ex)
    {
        AddLog(string.Format(
            _localization.GetResourceOrDefault("NeedleAligner_Log_ApplyCompensationError", "应用补偿值失败: {0}"),
            ex.Message));
        _logger.Error(ex, "应用针头补偿值失败");
    }
}

/// <summary>
/// 将补偿值写入全局变量（链接变量或默认变量名）
/// </summary>
private async Task WriteCompensationToGlobalVariablesAsync(PointF compensation)
{
    var poolId = _recipePoolService?.CurrentPoolName ?? "Default";
    var variables = (await _recipePoolService.LoadGlobalVariablesAsync(poolId)).ToList();

    UpdateOrAddGlobalVariable(variables, "NeedleAligner_CompX", compensation.X.ToString("F6"), "针头校准X补偿");
    UpdateOrAddGlobalVariable(variables, "NeedleAligner_CompY", compensation.Y.ToString("F6"), "针头校准Y补偿");
    UpdateOrAddGlobalVariable(variables, "NeedleAligner_CompZ", compensation.Z.ToString("F6"), "针头校准Z补偿");

    UpdateOrAddGlobalVariable(variables, "NeedleAligner_CompX_LinkedVar", CompensationXLinkedVar ?? "", "针头X补偿链接的全局变量名");
    UpdateOrAddGlobalVariable(variables, "NeedleAligner_CompY_LinkedVar", CompensationYLinkedVar ?? "", "针头Y补偿链接的全局变量名");
    UpdateOrAddGlobalVariable(variables, "NeedleAligner_CompZ_LinkedVar", CompensationZLinkedVar ?? "", "针头Z补偿链接的全局变量名");

    for (int i = 0; i < variables.Count; i++)
        variables[i].Index = i + 1;

    await _recipePoolService.SaveGlobalVariablesAsync(poolId, variables);

    _eventAggregator?.GetEvent<GlobalVariablesChangedEvent>()?.Publish(poolId);
}

/// <summary>
/// 更新或添加全局变量
/// </summary>
private void UpdateOrAddGlobalVariable(List<GlobalVariable> variables, string name, string value, string comment)
{
    var existing = variables.FirstOrDefault(v => v.Name == name);
    if (existing != null)
    {
        existing.Value = value;
    }
    else
    {
        variables.Add(new GlobalVariable
        {
            Name = name,
            Type = GlobalVariableType.Double,
            Value = value,
            Comment = comment
        });
    }
}
```

- [ ] **Step 4: 新增 LoadGlobalVariablesAsync 和 SaveLinkedVarsAsync**

```csharp
/// <summary>
/// 从配方池加载全局变量列表，恢复链接关系
/// </summary>
private async Task LoadGlobalVariablesAsync()
{
    try
    {
        if (_recipePoolService == null) return;

        var poolId = _recipePoolService.CurrentPoolName ?? "Default";
        var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);

        AvailableGlobalVariables.Clear();
        foreach (var v in variables)
            AvailableGlobalVariables.Add(v);

        var cxLink = variables.FirstOrDefault(v => v.Name == "NeedleAligner_CompX_LinkedVar");
        var cyLink = variables.FirstOrDefault(v => v.Name == "NeedleAligner_CompY_LinkedVar");
        var czLink = variables.FirstOrDefault(v => v.Name == "NeedleAligner_CompZ_LinkedVar");

        CompensationXLinkedVar = cxLink?.Value;
        CompensationYLinkedVar = cyLink?.Value;
        CompensationZLinkedVar = czLink?.Value;

        RaisePropertyChanged(nameof(IsCompensationXLinked));
        RaisePropertyChanged(nameof(IsCompensationYLinked));
        RaisePropertyChanged(nameof(IsCompensationZLinked));
    }
    catch (Exception ex)
    {
        _logger.Warn($"[NeedleAligner] 加载全局变量失败: {ex.Message}");
    }
}
```

- [ ] **Step 5: 修改构造函数 — 注入 IRecipePoolService，初始化加载**

1. 构造函数新增 `IRecipePoolService recipePoolService` 参数
2. 赋值 `_recipePoolService = recipePoolService;`
3. 构造函数末尾在 `_ = LoadParametersAsync();` 之后添加：

```csharp
_ = LoadGlobalVariablesAsync().ConfigureAwait(false);
```

- [ ] **Step 6: 修改 SaveParametersAsync — 同步保存链接变量名到参数**

在 `SaveParametersAsync` 方法中，`CompensationManager.SaveToParameters(Parameters);` 之后添加：

```csharp
Parameters.CompensationXLinkedVar = CompensationXLinkedVar;
Parameters.CompensationYLinkedVar = CompensationYLinkedVar;
Parameters.CompensationZLinkedVar = CompensationZLinkedVar;
```

- [ ] **Step 7: 修改 LoadParametersAsync — 恢复链接变量名**

在 `LoadParametersAsync` 方法中，`CompensationManager.LoadFromParameters(Parameters);` 之后添加：

```csharp
CompensationXLinkedVar = Parameters.CompensationXLinkedVar;
CompensationYLinkedVar = Parameters.CompensationYLinkedVar;
CompensationZLinkedVar = Parameters.CompensationZLinkedVar;
```

- [ ] **Step 8: 在 NeedleCalibrationParams 模型中新增链接变量名字段**

在 `Core/Models/NeedleCalibrationParams.cs` 中，`CompensationStorageZ` 之后添加：

```csharp
public string CompensationXLinkedVar { get; set; }
public string CompensationYLinkedVar { get; set; }
public string CompensationZLinkedVar { get; set; }
```

在 `Clone()` 方法中添加：

```csharp
CompensationXLinkedVar = this.CompensationXLinkedVar,
CompensationYLinkedVar = this.CompensationYLinkedVar,
CompensationZLinkedVar = this.CompensationZLinkedVar,
```

- [ ] **Step 9: 新增 using 引用**

确保 ViewModel 顶部有：

```csharp
using System.Linq;
using Recipe.Interfaces;
```

- [ ] **Step 10: 验证构建**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\Module\Module.csproj --no-restore`
Expected: Build succeeded, 0 errors

---

### Task 2: 新增多语言资源键

**Files:**
- Modify: `MainApp/Languages/Strings.zh-CN.xaml`
- Modify: `MainApp/Languages/Strings.en-US.xaml`

- [ ] **Step 1: 在 zh-CN 资源文件中添加新键**

在 `NeedleAligner_Log_ResetError` 行之后添加：

```xml
<sys:String x:Key="NeedleAligner_LinkGlobalVariable">链接全局变量</sys:String>
<sys:String x:Key="NeedleAligner_UnlinkGlobalVariable">取消链接</sys:String>
<sys:String x:Key="NeedleAligner_TeachSearchPoint">示教</sys:String>
<sys:String x:Key="NeedleAligner_SearchRangeTip">搜索时的移动范围，单位mm</sys:String>
<sys:String x:Key="NeedleAligner_ZSearchCountTip">Z方向搜索接触的次数</sys:String>
<sys:String x:Key="NeedleAligner_SearchSpeedTip">粗搜索移动速度，单位mm/s</sys:String>
<sys:String x:Key="NeedleAligner_FineSearchSpeedTip">精细搜索移动速度，单位mm/s</sys:String>
<sys:String x:Key="NeedleAligner_NeedleBaseHeightTip">针头在零位时的基准高度，单位mm</sys:String>
<sys:String x:Key="NeedleAligner_Dialog_ApplyToGlobalMessage">将以下补偿值写入全局变量：&#10;X={0:F3}, Y={1:F3}, Z={2:F3}&#10;并保存参数，确定继续吗？</sys:String>
<sys:String x:Key="NeedleAligner_Log_CompensationAppliedToGlobal">补偿值已写入全局变量并保存参数</sys:String>
<sys:String x:Key="NeedleAligner_Log_TeachSearchPoint">搜索点{0}示教完成: X={1:F3}, Y={2:F3}</sys:String>
<sys:String x:Key="NeedleAligner_Log_TeachSearchPointError">搜索点示教失败: {0}</sys:String>
<sys:String x:Key="NeedleAligner_CompensationLinked">已链接</sys:String>
```

- [ ] **Step 2: 在 en-US 资源文件中添加新键**

在 `NeedleAligner_Log_ResetError` 行之后添加：

```xml
<sys:String x:Key="NeedleAligner_LinkGlobalVariable">Link Global Variable</sys:String>
<sys:String x:Key="NeedleAligner_UnlinkGlobalVariable">Unlink</sys:String>
<sys:String x:Key="NeedleAligner_TeachSearchPoint">Teach</sys:String>
<sys:String x:Key="NeedleAligner_SearchRangeTip">Search movement range in mm</sys:String>
<sys:String x:Key="NeedleAligner_ZSearchCountTip">Number of Z-direction search contacts</sys:String>
<sys:String x:Key="NeedleAligner_SearchSpeedTip">Coarse search speed in mm/s</sys:String>
<sys:String x:Key="NeedleAligner_FineSearchSpeedTip">Fine search speed in mm/s</sys:String>
<sys:String x:Key="NeedleAligner_NeedleBaseHeightTip">Needle base height at zero position in mm</sys:String>
<sys:String x:Key="NeedleAligner_Dialog_ApplyToGlobalMessage">Write the following compensation values to global variables:&#10;X={0:F3}, Y={1:F3}, Z={2:F3}&#10;And save parameters. Continue?</sys:String>
<sys:String x:Key="NeedleAligner_Log_CompensationAppliedToGlobal">Compensation values written to global variables and parameters saved</sys:String>
<sys:String x:Key="NeedleAligner_Log_TeachSearchPoint">Search point {0} taught: X={1:F3}, Y={2:F3}</sys:String>
<sys:String x:Key="NeedleAligner_Log_TeachSearchPointError">Search point teach failed: {0}</sys:String>
<sys:String x:Key="NeedleAligner_CompensationLinked">Linked</sys:String>
```

- [ ] **Step 3: 验证构建**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\MainApp\MainApp.csproj`
Expected: Build succeeded

---

### Task 3: 重构 NeedleAlignerView.xaml — 补偿链接图标、运动参数提示、搜索点示教按钮

**Files:**
- Modify: `Module/Controls/Maintenance/NeedleAlignerView.xaml`

- [ ] **Step 1: 搜索点设置卡片增加示教按钮**

将每个搜索点行从：

```xml
<Grid Style="{StaticResource ParamRowStyle}">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>
    <TextBlock Grid.Column="0" Text="P1" ... />
    <TextBox Grid.Column="1" Text="{Binding Parameters.SearchPoint1.X, StringFormat=F3}" ... />
    <TextBox Grid.Column="2" Text="{Binding Parameters.SearchPoint1.Y, StringFormat=F3}" ... />
</Grid>
```

改为：

```xml
<Grid Style="{StaticResource ParamRowStyle}">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="Auto" />
    </Grid.ColumnDefinitions>
    <TextBlock Grid.Column="0" Text="P1" Style="{StaticResource ParamLabelStyle}"
               FontWeight="SemiBold" Foreground="{DynamicResource PrimaryHueMidBrush}" />
    <TextBox Grid.Column="1" Text="{Binding Parameters.SearchPoint1.X, StringFormat=F3}"
             Style="{StaticResource ParamTextBoxStyle}" />
    <TextBox Grid.Column="2" Text="{Binding Parameters.SearchPoint1.Y, StringFormat=F3}"
             Style="{StaticResource ParamTextBoxStyle}" />
    <Button Grid.Column="3" Command="{Binding TeachSearchPointCommand}" CommandParameter="1"
            Style="{StaticResource MaterialDesignOutlinedButton}"
            Padding="6,2" Margin="4,0,0,0"
            materialDesign:ButtonAssist.CornerRadius="4"
            ToolTip="{lang:Lang NeedleAligner_TeachSearchPoint}">
        <materialDesign:PackIcon Kind="CrosshairsGps" Width="14" Height="14" />
    </Button>
</Grid>
```

P2/P3/P4 同理，CommandParameter 分别为 2/3/4。

- [ ] **Step 2: 运动参数卡片增加文本提示**

在每个运动参数 TextBox 上添加 `materialDesign:HintAssist.Hint` 和 `ToolTip`。例如搜索范围：

```xml
<TextBox Grid.Column="1"
         Text="{Binding Parameters.SearchRange, StringFormat=F3}"
         Style="{StaticResource ParamTextBoxStyle}"
         materialDesign:HintAssist.Hint="{lang:Lang NeedleAligner_SearchRange}"
         ToolTip="{lang:Lang NeedleAligner_SearchRangeTip}" />
```

对所有 5 个运动参数（SearchRange、ZSearchCount、SearchSpeed、FineSearchSpeed、NeedleBaseHeight）都添加对应的 ToolTip。

- [ ] **Step 3: 补偿详情区域增加全局变量链接**

将补偿详情区域从纯 TextBlock 显示改为带链接图标的编辑器。替换校准结果卡片中的"补偿详情"Border：

```xml
<!-- 补偿详情 - 带全局变量链接 -->
<Border Background="#FAFAFA" CornerRadius="6" Padding="12,10" Margin="0,0,0,8">
    <StackPanel>
        <TextBlock Text="{lang:Lang NeedleAligner_CompensationDetails}"
                   FontSize="11" FontWeight="SemiBold" Foreground="#9E9E9E" Margin="0,0,0,8" />

        <!-- 补偿X -->
        <Grid Margin="0,0,0,6">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>
            <materialDesign:PackIcon Grid.Column="0" Kind="AxisXArrow" Width="16" Height="16"
                                     Foreground="#E53935" VerticalAlignment="Center" Margin="0,0,4,0" />
            <TextBlock Grid.Column="1" Text="{Binding CompensationManager.CompensationX, StringFormat=F3}"
                       Style="{StaticResource ResultValueStyle}" Foreground="#E53935" FontWeight="Bold" />
            <materialDesign:PackIcon Grid.Column="2" Kind="Link" Width="14" Height="14"
                                     Foreground="{DynamicResource PrimaryHueMidBrush}"
                                     VerticalAlignment="Center" Margin="4,0"
                                     Visibility="{Binding IsCompensationXLinked, Converter={StaticResource BoolToVis}}" />
            <ComboBox Grid.Column="3" ItemsSource="{Binding AvailableGlobalVariables}"
                      SelectedValuePath="Name"
                      DisplayMemberPath="Name"
                      SelectedValue="{Binding CompensationXLinkedVar, UpdateSourceTrigger=PropertyChanged}"
                      Width="120" FontSize="10"
                      materialDesign:HintAssist.Hint="{lang:Lang NeedleAligner_LinkGlobalVariable}" />
        </Grid>

        <!-- 补偿Y -->
        <Grid Margin="0,0,0,6">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>
            <materialDesign:PackIcon Grid.Column="0" Kind="AxisYArrow" Width="16" Height="16"
                                     Foreground="#43A047" VerticalAlignment="Center" Margin="0,0,4,0" />
            <TextBlock Grid.Column="1" Text="{Binding CompensationManager.CompensationY, StringFormat=F3}"
                       Style="{StaticResource ResultValueStyle}" Foreground="#43A047" FontWeight="Bold" />
            <materialDesign:PackIcon Grid.Column="2" Kind="Link" Width="14" Height="14"
                                     Foreground="{DynamicResource PrimaryHueMidBrush}"
                                     VerticalAlignment="Center" Margin="4,0"
                                     Visibility="{Binding IsCompensationYLinked, Converter={StaticResource BoolToVis}}" />
            <ComboBox Grid.Column="3" ItemsSource="{Binding AvailableGlobalVariables}"
                      SelectedValuePath="Name"
                      DisplayMemberPath="Name"
                      SelectedValue="{Binding CompensationYLinkedVar, UpdateSourceTrigger=PropertyChanged}"
                      Width="120" FontSize="10"
                      materialDesign:HintAssist.Hint="{lang:Lang NeedleAligner_LinkGlobalVariable}" />
        </Grid>

        <!-- 补偿Z -->
        <Grid Margin="0,0,0,6">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>
            <materialDesign:PackIcon Grid.Column="0" Kind="AxisZArrow" Width="16" Height="16"
                                     Foreground="#1E88E5" VerticalAlignment="Center" Margin="0,0,4,0" />
            <TextBlock Grid.Column="1" Text="{Binding CompensationManager.CompensationZ, StringFormat=F3}"
                       Style="{StaticResource ResultValueStyle}" Foreground="#1E88E5" FontWeight="Bold" />
            <materialDesign:PackIcon Grid.Column="2" Kind="Link" Width="14" Height="14"
                                     Foreground="{DynamicResource PrimaryHueMidBrush}"
                                     VerticalAlignment="Center" Margin="4,0"
                                     Visibility="{Binding IsCompensationZLinked, Converter={StaticResource BoolToVis}}" />
            <ComboBox Grid.Column="3" ItemsSource="{Binding AvailableGlobalVariables}"
                      SelectedValuePath="Name"
                      DisplayMemberPath="Name"
                      SelectedValue="{Binding CompensationZLinkedVar, UpdateSourceTrigger=PropertyChanged}"
                      Width="120" FontSize="10"
                      materialDesign:HintAssist.Hint="{lang:Lang NeedleAligner_LinkGlobalVariable}" />
        </Grid>
    </StackPanel>
</Border>
```

- [ ] **Step 4: 在 Resources 中添加 BooleanToVisibilityConverter**

在 `UserControl.Resources` 中添加（如果尚不存在）：

```xml
<BooleanToVisibilityConverter x:Key="BoolToVis" />
```

- [ ] **Step 5: 验证构建**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\Module\Module.csproj --no-restore`
Expected: Build succeeded, 0 errors

---

### Task 4: 全量构建验证

**Files:** 无修改

- [ ] **Step 1: 执行全量构建**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\MainApp\MainApp.csproj`
Expected: Build succeeded, 0 errors

- [ ] **Step 2: 检查 NeedleAlignerView 在 MaintenanceView 中可正常导航**

确认 `NeedleAlignerView` 已在 Module 的 `RegisterForNavigation` 中注册（应已存在）。

---

## 自审检查清单

### 1. 规格覆盖
- ✅ 补偿值链接全局变量（带链接图标）→ Task 1 Step 1 + Task 3 Step 3
- ✅ 运动参数增加文本提示 → Task 3 Step 2
- ✅ 搜索点设置增加示教按钮 → Task 1 Step 2 + Task 3 Step 1
- ✅ 应用补偿：当前补偿值应用到全局变量 → Task 1 Step 3
- ✅ 考虑设备安全，删除执行运动 → Task 1 Step 3（移除 MoveToPositionSafelyAsync 调用）
- ✅ 保存参数 → Task 1 Step 3（ApplyCompensationAsync 中调用 SaveParametersAsync）

### 2. 占位符扫描
- 无 TBD/TODO/占位符

### 3. 类型一致性
- CompensationXLinkedVar/Y/Z → string 类型，与 VisionCaptureViewModel 一致
- AvailableGlobalVariables → ObservableCollection<GlobalVariable>，与 VisionCaptureViewModel 一致
- TeachSearchPointCommand → DelegateCommand<int>，CommandParameter 为整数
- WriteCompensationToGlobalVariablesAsync 使用 PointF，与现有 compensation 变量类型一致
