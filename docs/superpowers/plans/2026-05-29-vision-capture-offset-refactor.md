# VisionCaptureView Offset Compensation 重构 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 重构 VisionCaptureView 的 Offset Compensation 区域，删除 CompX/CompY，增加 OffsetA；Parsed Data 自动写入 Offset X/Y/A；坐标变换详情调整；针头偏移链接全局变量；针头补偿改为 DecimalUpDown；点胶操作增加针头下降 Checkbox。

**Architecture:** 在现有 WPF+Prism+MaterialDesign 架构上，修改 VisionCaptureView.xaml 的 UI 布局和 VisionCaptureViewModel.cs 的数据逻辑。PhotoPositionRow 模型同步调整。BezierArcDispenseService 需支持 OffsetA 和 NeedleDescend 参数。

**Tech Stack:** WPF, Prism, MaterialDesignInXAML, C# .NET 9

---

## File Structure

| File | Action | Responsibility |
|------|--------|---------------|
| `Module\Controls\Dispense\VisionCaptureView.xaml` | Modify | Offset Compensation 区域 UI 重构、坐标变换详情调整、针头偏移链接全局变量、针头补偿 DecimalUpDown、点胶操作 Checkbox |
| `Module\Controls\Dispense\VisionCaptureViewModel.cs` | Modify | OffsetA 属性、ParsedData→Offset 自动写入、NeedleDescend 属性、链接全局变量逻辑 |
| `Module\Controls\Dispense\PhotoPositionRow.cs` | Modify | 删除 CompX/CompY/CompXExpression/CompYExpression，增加 OffsetA/OffsetAExpression/CalculatedOffsetA |
| `StationTasks\Services\BezierArcDispenseService.cs` | Modify | ExecuteDotDispenseAsync/ExecuteArcDispenseAsync 支持 OffsetA 和 NeedleDescend |
| `MainApp\Languages\Strings.zh-CN.xaml` | Modify | 新增/修改语言键 |
| `MainApp\Languages\Strings.en-US.xaml` | Modify | 新增/修改语言键 |

---

### Task 1: PhotoPositionRow 模型重构 - 删除 CompX/CompY，增加 OffsetA

**Files:**
- Modify: `Module\Controls\Dispense\PhotoPositionRow.cs`

- [ ] **Step 1: 删除 CompX/CompY 相关属性，增加 OffsetA 属性**

删除以下属性：
- `_needleCompX` / `NeedleCompX`
- `_needleCompY` / `NeedleCompY`
- `_compXExpression` / `CompXExpression`
- `_compYExpression` / `CompYExpression`
- `CalculatedCompX`
- `CalculatedCompY`

新增以下属性：
```csharp
private double _offsetA;
/// <summary>
/// 角度偏移基础值
/// </summary>
public double OffsetA
{
    get => _offsetA;
    set
    {
        if (SetProperty(ref _offsetA, value))
            RaisePropertyChanged(nameof(CalculatedOffsetA));
    }
}

private string _offsetAExpression;
/// <summary>
/// OffsetA计算表达式
/// </summary>
public string OffsetAExpression
{
    get => _offsetAExpression;
    set
    {
        if (SetProperty(ref _offsetAExpression, value))
            RaisePropertyChanged(nameof(CalculatedOffsetA));
    }
}

/// <summary>
/// 计算后的OffsetA = OffsetA + 表达式结果
/// </summary>
public double CalculatedOffsetA => OffsetA + EvaluateExpression(OffsetAExpression);
```

---

### Task 2: VisionCaptureViewModel 属性重构

**Files:**
- Modify: `Module\Controls\Dispense\VisionCaptureViewModel.cs`

- [ ] **Step 1: 删除 NeedleCompX/NeedleCompY 及其链接变量，增加 OffsetA 及 NeedleDescend**

删除：
- `_needleCompX` / `NeedleCompX` 属性
- `_needleCompY` / `NeedleCompY` 属性
- `_needleCompXLinkedVar` / `NeedleCompXLinkedVar` 属性
- `_needleCompYLinkedVar` / `NeedleCompYLinkedVar` 属性
- `IsNeedleCompXLinked` / `IsNeedleCompYLinked` 属性

新增：
```csharp
private double _offsetA;
/// <summary>
/// 角度偏移值，从 ParsedData 自动写入
/// </summary>
public double OffsetA
{
    get => _offsetA;
    set => SetProperty(ref _offsetA, value);
}

private bool _needleDescend = true;
/// <summary>
/// 是否针头下降执行点胶
/// </summary>
public bool NeedleDescend
{
    get => _needleDescend;
    set => SetProperty(ref _needleDescend, value);
}
```

- [ ] **Step 2: 修改 SelectedRow setter**

将：
```csharp
NeedleCompX = value.CalculatedCompX;
NeedleCompY = value.CalculatedCompY;
```
改为：
```csharp
OffsetA = value.CalculatedOffsetA;
```

- [ ] **Step 3: 修改 ParsedData 自动写入逻辑**

在 `ExecuteCaptureAsync` 方法中，ParsedData 赋值后，增加自动写入 Offset X/Y/A：
```csharp
ParsedData = new ObservableCollection<KeyValuePair<string, double>>(result.ParsedData);

// Parsed Data 自动写入 Offset Compensation 区域
var pd = result.ParsedData;
if (pd.TryGetValue("offsetX", out var ox)) SelectedRow.NeedleOffsetX = ox;
else if (pd.TryGetValue("X", out var px)) SelectedRow.NeedleOffsetX = px;
if (pd.TryGetValue("offsetY", out var oy)) SelectedRow.NeedleOffsetY = oy;
else if (pd.TryGetValue("Y", out var py)) SelectedRow.NeedleOffsetY = py;
if (pd.TryGetValue("offsetA", out var oa)) SelectedRow.OffsetA = oa;
else if (pd.TryGetValue("U", out var pu)) SelectedRow.OffsetA = pu;

// 同步到 ViewModel
NeedleOffsetX = SelectedRow.CalculatedOffsetX;
NeedleOffsetY = SelectedRow.CalculatedOffsetY;
OffsetA = SelectedRow.CalculatedOffsetA;
```

- [ ] **Step 4: 修改全局变量保存/加载逻辑**

在 `SaveToGlobalVariablesAsync` 中，删除 NeedleCompX/NeedleCompY 相关，增加 OffsetA：
```csharp
UpdateOrAddGlobalVariable(variableList, "OffsetA", OffsetA.ToString("F6"), "角度偏移");
```

在 `LoadFromGlobalVariablesAsync` 中，删除 NeedleCompX/NeedleCompY 相关，增加 OffsetA：
```csharp
var oaVar = variables.FirstOrDefault(v => v.Name == "OffsetA");
if (oaVar != null && double.TryParse(oaVar.Value, out var oa)) OffsetA = oa;
```

- [ ] **Step 5: 修改配置持久化**

在 `PhotoPositionRowConfig` 中删除 `NeedleCompX`/`NeedleCompY`/`CompXExpression`/`CompYExpression`，增加 `OffsetA`/`OffsetAExpression`。

在配置保存/加载处同步修改。

- [ ] **Step 6: 修改 ExecuteDispenseAsync 传递 NeedleDescend**

在 `ExecuteDispenseAsync` 中将 `NeedleDescend` 传递给 `BezierArcDispenseService`。

---

### Task 3: BezierArcDispenseService 支持 OffsetA 和 NeedleDescend

**Files:**
- Modify: `StationTasks\Services\BezierArcDispenseService.cs`

- [ ] **Step 1: 修改 ExecuteDotDispenseAsync 签名和逻辑**

增加 `double offsetA` 和 `bool needleDescend` 参数：
```csharp
public async Task ExecuteDotDispenseAsync(
    Dictionary<string, double> visionData,
    double photoDx, double photoDy,
    int dxAxisId, int dyAxisId, int dz1AxisId,
    int coordId,
    double speed, double dzSafePos, double dzDispensePos,
    bool dryRun, bool needleDescend, double offsetA,
    CancellationToken token)
```

在方法中：
- 从全局变量读取 `OffsetA` 替代 `NeedleCompX/NeedleCompY`
- 当 `needleDescend == false` 时，跳过 `MoveAbsAsync(dz1AxisId, dzDispensePos, ...)` 步骤

- [ ] **Step 2: 修改 ExecuteArcDispenseAsync 签名和逻辑**

同样增加 `bool needleDescend` 和 `double offsetA` 参数，逻辑同上。

---

### Task 4: VisionCaptureView.xaml UI 重构

**Files:**
- Modify: `Module\Controls\Dispense\VisionCaptureView.xaml`

- [ ] **Step 1: Offset Compensation 区域重构**

将原 4 行 Grid（OffsetX, OffsetY, CompX, CompY）改为 3 行（OffsetX, OffsetY, OffsetA）：

删除 Row2 (CompX) 和 Row3 (CompY)，增加 OffsetA 行：
```xml
<TextBlock Grid.Row="2" Grid.Column="0" Text="{lang:Lang VisionCapture_Label_OffsetA}" Style="{StaticResource ParamLabel}" Margin="0,0,6,0" />
<TextBox Grid.Row="2" Grid.Column="1" Text="{Binding OffsetA}" FontSize="11"
         Style="{StaticResource MaterialDesignOutlinedTextBox}" Padding="4,2" Margin="0,0,4,0" />
<TextBox Grid.Row="2" Grid.Column="2" Text="{Binding OffsetAExpression}" FontSize="11"
         Style="{StaticResource MaterialDesignOutlinedTextBox}" Padding="4,2" Margin="0,0,4,0"
         materialDesign:HintAssist.Hint="{lang:Lang VisionCapture_ExpressionHint}" />
<TextBlock Grid.Row="2" Grid.Column="3" Text="{Binding CalculatedOffsetA, StringFormat='= {0:F3}'}"
           FontSize="10" Foreground="{StaticResource PrimaryBlue}" VerticalAlignment="Center" Margin="2,0,0,0" />
```

Grid.RowDefinitions 从 4 行改为 3 行。

- [ ] **Step 2: 坐标变换详情 - Dot 模式调整**

① 拍照位置：增加 Dz₁ 显示
```xml
<Run Text="Dx=" /><Run Text="{Binding PhotoDx, StringFormat=F3}" Foreground="{StaticResource PrimaryBlue}" FontWeight="Medium" />
<Run Text=" Dy=" /><Run Text="{Binding PhotoDy, StringFormat=F3}" Foreground="{StaticResource PrimaryBlue}" FontWeight="Medium" />
<Run Text=" Dz₁=" /><Run Text="{Binding PhotoDz1, StringFormat=F3}" Foreground="{StaticResource PrimaryBlue}" FontWeight="Medium" />
```

② 目标偏移：ΔX=OffsetX, ΔY=OffsetY, ΔA=OffsetA
```xml
<Run Text="ΔX=" /><Run Text="{Binding NeedleOffsetX, StringFormat=F3}" Foreground="{StaticResource PrimaryBlue}" FontWeight="Medium" />
<Run Text=" ΔY=" /><Run Text="{Binding NeedleOffsetY, StringFormat=F3}" Foreground="{StaticResource PrimaryBlue}" FontWeight="Medium" />
<Run Text=" ΔA=" /><Run Text="{Binding OffsetA, StringFormat=F3}" Foreground="{StaticResource PrimaryBlue}" FontWeight="Medium" />
```

③ 针头偏移：OX OY 链接全局变量（参照 NeedleAlignerView 样式）
```xml
<!-- 针头偏移 OX -->
<Grid Margin="0,0,0,6">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="Auto" />
    </Grid.ColumnDefinitions>
    <materialDesign:PackIcon Grid.Column="0" Kind="AxisXArrow" Width="16" Height="16"
                             Foreground="#E53935" VerticalAlignment="Center" Margin="0,0,4,0" />
    <TextBlock Grid.Column="1" Text="{Binding NeedleOffsetX, StringFormat=F3}"
               FontWeight="Bold" Foreground="#E53935" FontSize="11" VerticalAlignment="Center" />
    <Button Grid.Column="2" Command="{Binding UnlinkNeedleOffsetXCommand}"
            Style="{StaticResource MaterialDesignIconButton}" Padding="0" Width="22" Height="22"
            ToolTip="{lang:Lang VisionCapture_UnlinkGlobalVariable}">
        <materialDesign:PackIcon Kind="LinkOff" Width="14" Height="14"
                                 Foreground="{Binding IsNeedleOffsetXLinked, Converter={StaticResource LinkedToBrushConverter}}"
                                 VerticalAlignment="Center" />
    </Button>
    <ComboBox Grid.Column="3" ItemsSource="{Binding AvailableGlobalVariables}"
              SelectedValuePath="Name" DisplayMemberPath="Name"
              SelectedValue="{Binding NeedleOffsetXLinkedVar, UpdateSourceTrigger=PropertyChanged}"
              Width="120" FontSize="10"
              materialDesign:HintAssist.Hint="{lang:Lang VisionCapture_LinkGlobalVariable}" />
</Grid>
<!-- 针头偏移 OY 类似 -->
```

④ 针头补偿：改为 DecimalUpDown 可人工输入
```xml
<!-- 针头补偿 -->
<StackPanel Margin="0,0,0,4">
    <TextBlock Text="{lang:Lang VisionCapture_NeedleCompensation}" FontWeight="SemiBold" FontSize="11" Foreground="{StaticResource TextPrimary}" />
    <materialDesign:DecimalUpDown Value="{Binding NeedleCompX}" ValueStep="0.001" Minimum="-999" Maximum="999" Margin="0,4,0,2" />
</StackPanel>
```

注意：此处针头补偿保留为可人工输入的数值（DecimalUpDown），不再链接全局变量。

- [ ] **Step 3: 坐标变换详情 - Arc 模式调整**

同 Dot 模式，修改拍照位坐标显示（增加 Dz₁）、目标偏移（ΔX/ΔY/ΔA）、针头偏移（链接全局变量）、针头补偿（DecimalUpDown）。

- [ ] **Step 4: 点胶操作区域增加针头下降 Checkbox**

在点胶操作面板的 WrapPanel 前增加：
```xml
<CheckBox Content="{lang:Lang VisionCapture_NeedleDescend}"
          IsChecked="{Binding NeedleDescend}"
          Margin="0,0,0,8" FontSize="12"
          ToolTip="{lang:Lang VisionCapture_NeedleDescendTooltip}" />
```

- [ ] **Step 5: 增加 LinkedToBrushConverter 资源引用**

在 UserControl.Resources 中增加（如不存在）：
```xml
<converters:LinkedToBrushConverter x:Key="LinkedToBrushConverter" />
```

---

### Task 5: 多语言支持

**Files:**
- Modify: `MainApp\Languages\Strings.zh-CN.xaml`
- Modify: `MainApp\Languages\Strings.en-US.xaml`

- [ ] **Step 1: 新增/修改语言键**

zh-CN:
```xml
<sys:String x:Key="VisionCapture_Label_OffsetA">偏移A：</sys:String>
<sys:String x:Key="VisionCapture_NeedleDescend">针头下降</sys:String>
<sys:String x:Key="VisionCapture_NeedleDescendTooltip">勾选后执行点胶时针头会下降到点胶位，取消勾选则仅移动XY不下降</sys:String>
<sys:String x:Key="VisionCapture_LinkGlobalVariable">链接全局变量</sys:String>
<sys:String x:Key="VisionCapture_UnlinkGlobalVariable">取消链接全局变量</sys:String>
```

en-US:
```xml
<sys:String x:Key="VisionCapture_Label_OffsetA">Offset A:</sys:String>
<sys:String x:Key="VisionCapture_NeedleDescend">Needle Descend</sys:String>
<sys:String x:Key="VisionCapture_NeedleDescendTooltip">When checked, the needle descends to dispense position; when unchecked, only XY moves without descending</sys:String>
<sys:String x:Key="VisionCapture_LinkGlobalVariable">Link Global Variable</sys:String>
<sys:String x:Key="VisionCapture_UnlinkGlobalVariable">Unlink Global Variable</sys:String>
```

删除不再使用的语言键：`VisionCapture_Label_CompX`, `VisionCapture_Label_CompY`

---

### Task 6: 构建验证

- [ ] **Step 1: 运行构建，修复编译错误**

Run: `dotnet build`
Expected: BUILD SUCCEEDED

- [ ] **Step 2: 检查所有引用已更新**

确保所有对 `NeedleCompX`/`NeedleCompY`/`CompXExpression`/`CompYExpression`/`CalculatedCompX`/`CalculatedCompY` 的引用已替换。
