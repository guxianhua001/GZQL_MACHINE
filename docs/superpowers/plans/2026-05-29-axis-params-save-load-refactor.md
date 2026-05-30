# 轴参数载入/保存功能重构 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 增加独立的载入参数/保存参数按钮，将 Read From Card / Write To Card 改为纯卡操作（不自动保存文件），所有按钮在单轴和插补系模式都可见。

**Architecture:** 在 AxisParameterService 中将保存路径从 `Config/Parameters` 改为 `Config/AxisSettings`，所有轴参数仍合并为一个 JSON 文件。ViewModel 中分离卡操作与文件操作，新增 SaveParamsCommand / LoadParamsCommand。底部操作栏重构为通用操作区（所有模式可见）。

**Tech Stack:** WPF + Prism + MaterialDesignInXaml + Newtonsoft.Json + JsonParameterStorage

---

## 关键变更摘要

| 变更 | 说明 |
|------|------|
| 保存路径 | `Config/Parameters` → `Config/AxisSettings` |
| Read From Card | 移除自动保存文件逻辑，仅读取卡参数到内存 |
| Write To Card | 移除自动保存文件逻辑，仅将参数写入卡 |
| Write All To Card | 移除自动保存文件逻辑 |
| Read All From Card | 移除自动保存文件逻辑 |
| 新增 SaveParams | 保存当前所有轴参数+插补系参数到 JSON 文件 |
| 新增 LoadParams | 从 JSON 文件载入参数到内存 |
| 按钮可见性 | Read/Write/Save/Load 按钮在单轴和插补系模式都可见 |
| 底部栏布局 | 去掉模式分组 Badge，统一为一排按钮 |

---

### Task 1: 修改 AxisParameterService 保存路径

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\MotionControl\Services\AxisParameterService.cs:24`

- [ ] **Step 1: 修改 PARAMS_DIR 常量**

将第24行：
```csharp
private const string PARAMS_DIR = "Config/Parameters";
```
改为：
```csharp
private const string PARAMS_DIR = "Config/AxisSettings";
```

- [ ] **Step 2: 验证构建**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\MotionControl\MotionControl.csproj --no-restore`
Expected: BUILD SUCCESS

---

### Task 2: 分离卡操作与文件操作 — ViewModel 层

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\MotionControl\ViewModels\AxisSettingViewModel.cs`

- [ ] **Step 1: 新增 SaveParamsCommand 和 LoadParamsCommand 声明**

在现有 Command 声明区域（约第94-105行）添加：
```csharp
public DelegateCommand SaveParamsCommand { get; }
public DelegateCommand LoadParamsCommand { get; }
```

- [ ] **Step 2: 在构造函数中初始化新 Command**

在构造函数中（约第219行 `LoadSystemParamsCommand = new DelegateCommand(LoadSystemConfigurations);` 之后）添加：
```csharp
SaveParamsCommand = new DelegateCommand(OnSaveParams);
LoadParamsCommand = new DelegateCommand(OnLoadParams);
```

- [ ] **Step 3: 实现 OnSaveParams 方法**

在 `OnSaveSystemParams` 方法之后添加：
```csharp
/// <summary>
/// 保存所有参数（轴参数+插补系参数）到文件
/// </summary>
private void OnSaveParams()
{
    try
    {
        _parameterService.SaveAllAxisParameters(Axes);
        _parameterService.SaveAllInterpolationSystems(InterpolationSystems);
        ParametersChanged = false;
        MessageBox.Show(
            _loc.GetResource("AxisSetting_SaveParamsSuccess"),
            _loc.GetResource("AxisSetting_SaveToFileToolTip"),
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            string.Format(_loc.GetResource("AxisSetting_SaveParamsFailed"), ex.Message),
            _loc.GetResource("AxisSetting_Error"),
            MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

- [ ] **Step 4: 实现 OnLoadParams 方法**

在 `OnSaveParams` 方法之后添加：
```csharp
/// <summary>
/// 从文件载入所有参数（轴参数+插补系参数）
/// </summary>
private void OnLoadParams()
{
    try
    {
        var savedParams = _parameterService.LoadAllAxisParameters();
        foreach (var axis in Axes)
        {
            string key = $"{axis.CardId}-{axis.AxisId}";
            if (savedParams.ContainsKey(key))
            {
                axis.Params = savedParams[key];
                axis.Params.PropertyChanged += (s, e) => ParametersChanged = true;
            }
        }

        if (SelectedAxis != null)
        {
            string selectedKey = $"{SelectedAxis.CardId}-{SelectedAxis.AxisId}";
            if (savedParams.ContainsKey(selectedKey))
            {
                CurrentAxisParams = SelectedAxis.Params;
                RaisePropertyChanged(nameof(CurrentAxisParams));
            }
        }

        LoadSystemConfigurations();

        ParametersChanged = false;
        MessageBox.Show(
            _loc.GetResource("AxisSetting_LoadParamsSuccess"),
            _loc.GetResource("AxisSetting_LoadFromFileToolTip"),
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            string.Format(_loc.GetResource("AxisSetting_LoadParamsFailed"), ex.Message),
            _loc.GetResource("AxisSetting_Error"),
            MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

- [ ] **Step 5: 移除 WriteToCard 中的自动保存逻辑**

将 `WriteToCard` 方法（约第335-350行）改为：
```csharp
/// <summary>
/// 写入到卡：将选中轴参数写入控制卡
/// </summary>
private async void WriteToCard()
{
    if (SelectedAxis == null) return;

    try
    {
        await _parameterService.WriteToCardAsync(SelectedAxis);
        ParametersChanged = false;
        MessageBox.Show(
            _loc.GetResource("AxisSetting_WriteToCardSuccess"),
            _loc.GetResource("AxisSetting_WriteToCardToolTip"),
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            string.Format(_loc.GetResource("AxisSetting_WriteToCardFailed"), ex.Message),
            _loc.GetResource("AxisSetting_Error"),
            MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

- [ ] **Step 6: 移除 ReadFromCard 中的自动保存逻辑**

将 `ReadFromCard` 方法（约第355-372行）改为：
```csharp
/// <summary>
/// 从卡读取：从控制卡读取选中轴参数
/// </summary>
private async void ReadFromCard()
{
    if (SelectedAxis == null) return;

    try
    {
        await _parameterService.ReadFromCardAsync(SelectedAxis);
        CurrentAxisParams = SelectedAxis.Params;
        RaisePropertyChanged(nameof(CurrentAxisParams));
        ParametersChanged = false;
        MessageBox.Show(
            _loc.GetResource("AxisSetting_ReadFromCardSuccess"),
            _loc.GetResource("AxisSetting_ReadFromCardToolTip"),
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            string.Format(_loc.GetResource("AxisSetting_ReadFromCardFailed"), ex.Message),
            _loc.GetResource("AxisSetting_Error"),
            MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

- [ ] **Step 7: 移除 WriteAllToCard 中的自动保存逻辑**

将 `WriteAllToCard` 方法（约第377-405行）改为：
```csharp
/// <summary>
/// 写入所有轴参数到控制卡
/// </summary>
private async void WriteAllToCard()
{
    var dialog = new ParameterProgressDialog(_loc.GetResource("AxisSetting_WritingAllAxes"));

    try
    {
        if (Application.Current.MainWindow != null &&
            Application.Current.MainWindow.IsLoaded)
        {
            dialog.Owner = Application.Current.MainWindow;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        dialog.Show();

        await _parameterService.WriteAllToCardAsync(new ProgressReporterAdapter(dialog));
        ParametersChanged = false;
        dialog.Close();
        MessageBox.Show(
            _loc.GetResource("AxisSetting_WriteAllToCardSuccess"),
            _loc.GetResource("AxisSetting_SetAllAxesToolTip"),
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
    catch (Exception ex)
    {
        dialog.Close();
        MessageBox.Show(
            string.Format(_loc.GetResource("AxisSetting_WriteToCardFailed"), ex.Message),
            _loc.GetResource("AxisSetting_Error"),
            MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

- [ ] **Step 8: 移除 ReadAllFromCard 中的自动保存逻辑**

将 `ReadAllFromCard` 方法（约第410-445行）改为：
```csharp
/// <summary>
/// 从控制卡读取所有轴参数
/// </summary>
private async void ReadAllFromCard()
{
    var dialog = new ParameterProgressDialog(_loc.GetResource("AxisSetting_ReadingAllAxes"));

    try
    {
        if (Application.Current.MainWindow != null &&
            Application.Current.MainWindow.IsLoaded)
        {
            dialog.Owner = Application.Current.MainWindow;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        dialog.Show();

        await _parameterService.ReadAllFromCardAsync(new ProgressReporterAdapter(dialog));
        ParametersChanged = false;
        dialog.Close();

        if (SelectedAxis != null)
        {
            CurrentAxisParams = SelectedAxis.Params;
            RaisePropertyChanged(nameof(CurrentAxisParams));
        }

        MessageBox.Show(
            _loc.GetResource("AxisSetting_ReadAllFromCardSuccess"),
            _loc.GetResource("AxisSetting_ReadAllFromCardToolTip"),
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
    catch (Exception ex)
    {
        dialog.Close();
        MessageBox.Show(
            string.Format(_loc.GetResource("AxisSetting_ReadFromCardFailed"), ex.Message),
            _loc.GetResource("AxisSetting_Error"),
            MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

- [ ] **Step 9: 注入 ILocalizationService**

在构造函数参数中添加 `ILocalizationService loc`，添加字段：
```csharp
private readonly ILocalizationService _loc;
```

构造函数第一行添加：
```csharp
_loc = loc;
```

需要添加 using：
```csharp
using Core.Abstraction;
```

- [ ] **Step 10: 验证构建**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\MotionControl\MotionControl.csproj --no-restore`
Expected: BUILD SUCCESS

---

### Task 3: 添加多语言键

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\MainApp\Languages\Strings.zh-CN.xaml`
- Modify: `c:\WorkFiles\GZQL_MACHINE\MainApp\Languages\Strings.en-US.xaml`

- [ ] **Step 1: 在 zh-CN 文件末尾（`</ResourceDictionary>` 之前）添加**

```xml
<sys:String x:Key="AxisSetting_SaveParams">保存参数</sys:String>
<sys:String x:Key="AxisSetting_SaveParamsToolTip">保存所有参数到文件</sys:String>
<sys:String x:Key="AxisSetting_LoadParams">载入参数</sys:String>
<sys:String x:Key="AxisSetting_LoadParamsToolTip">从文件载入参数</sys:String>
<sys:String x:Key="AxisSetting_SaveParamsSuccess">参数已保存到文件</sys:String>
<sys:String x:Key="AxisSetting_SaveParamsFailed">保存参数失败: {0}</sys:String>
<sys:String x:Key="AxisSetting_LoadParamsSuccess">参数已从文件载入</sys:String>
<sys:String x:Key="AxisSetting_LoadParamsFailed">载入参数失败: {0}</sys:String>
<sys:String x:Key="AxisSetting_WriteToCardSuccess">参数已写入控制卡</sys:String>
<sys:String x:Key="AxisSetting_WriteToCardFailed">写入控制卡失败: {0}</sys:String>
<sys:String x:Key="AxisSetting_ReadFromCardSuccess">参数已从控制卡读取</sys:String>
<sys:String x:Key="AxisSetting_ReadFromCardFailed">从控制卡读取失败: {0}</sys:String>
<sys:String x:Key="AxisSetting_WriteAllToCardSuccess">所有轴参数已写入控制卡</sys:String>
<sys:String x:Key="AxisSetting_ReadAllFromCardSuccess">所有轴参数已从控制卡读取</sys:String>
<sys:String x:Key="AxisSetting_WritingAllAxes">写入所有轴参数</sys:String>
<sys:String x:Key="AxisSetting_ReadingAllAxes">从控制卡读取所有轴参数</sys:String>
<sys:String x:Key="AxisSetting_Error">错误</sys:String>
```

- [ ] **Step 2: 在 en-US 文件末尾（`</ResourceDictionary>` 之前）添加**

```xml
<sys:String x:Key="AxisSetting_SaveParams">Save Params</sys:String>
<sys:String x:Key="AxisSetting_SaveParamsToolTip">Save all parameters to file</sys:String>
<sys:String x:Key="AxisSetting_LoadParams">Load Params</sys:String>
<sys:String x:Key="AxisSetting_LoadParamsToolTip">Load parameters from file</sys:String>
<sys:String x:Key="AxisSetting_SaveParamsSuccess">Parameters saved to file</sys:String>
<sys:String x:Key="AxisSetting_SaveParamsFailed">Save parameters failed: {0}</sys:String>
<sys:String x:Key="AxisSetting_LoadParamsSuccess">Parameters loaded from file</sys:String>
<sys:String x:Key="AxisSetting_LoadParamsFailed">Load parameters failed: {0}</sys:String>
<sys:String x:Key="AxisSetting_WriteToCardSuccess">Parameters written to card</sys:String>
<sys:String x:Key="AxisSetting_WriteToCardFailed">Write to card failed: {0}</sys:String>
<sys:String x:Key="AxisSetting_ReadFromCardSuccess">Parameters read from card</sys:String>
<sys:String x:Key="AxisSetting_ReadFromCardFailed">Read from card failed: {0}</sys:String>
<sys:String x:Key="AxisSetting_WriteAllToCardSuccess">All axis parameters written to card</sys:String>
<sys:String x:Key="AxisSetting_ReadAllFromCardSuccess">All axis parameters read from card</sys:String>
<sys:String x:Key="AxisSetting_WritingAllAxes">Writing all axis parameters</sys:String>
<sys:String x:Key="AxisSetting_ReadingAllAxes">Reading all axis parameters from card</sys:String>
<sys:String x:Key="AxisSetting_Error">Error</sys:String>
```

- [ ] **Step 3: 验证构建**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\MainApp\MainApp.csproj --no-restore`
Expected: BUILD SUCCESS

---

### Task 4: 重构底部操作栏 — 按钮在两种模式都可见

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\MotionControl\Views\AxisSettingView.xaml`

- [ ] **Step 1: 替换整个底部 Border（Grid.Row="2"）内容**

将第556-616行的底部 Border 内容替换为：

```xml
<Border Grid.Row="2" BorderThickness="0,1,0,0" BorderBrush="{DynamicResource MaterialDesignDivider}" Padding="8,10,12,6" Background="#FAFAFC">
    <StackPanel Orientation="Horizontal">
        <Button Command="{Binding UploadParamsCommand}" Style="{DynamicResource MaterialDesignOutlinedButton}" ToolTip="{lang:Lang AxisSetting_ReadFromCardToolTip}" Margin="0,0,0,0" materialDesign:ButtonAssist.CornerRadius="4" BorderBrush="#1565C0" Foreground="#1565C0" Padding="10,4">
            <StackPanel Orientation="Horizontal">
                <materialDesign:PackIcon Kind="Upload" Width="15" Height="15" VerticalAlignment="Center"/>
                <TextBlock Text="{lang:Lang AxisSetting_ReadFromCard}" VerticalAlignment="Center" Margin="4,0,0,0" FontSize="12"/>
            </StackPanel>
        </Button>
        <Button Command="{Binding DownloadParamsCommand}" Style="{DynamicResource MaterialDesignOutlinedButton}" ToolTip="{lang:Lang AxisSetting_WriteToCardToolTip}" Margin="6,0,0,0" materialDesign:ButtonAssist.CornerRadius="4" BorderBrush="#1565C0" Foreground="#1565C0" Padding="10,4">
            <StackPanel Orientation="Horizontal">
                <materialDesign:PackIcon Kind="Download" Width="15" Height="15" VerticalAlignment="Center"/>
                <TextBlock Text="{lang:Lang AxisSetting_WriteToCard}" VerticalAlignment="Center" Margin="4,0,0,0" FontSize="12"/>
            </StackPanel>
        </Button>

        <Border Width="1" Background="{DynamicResource MaterialDesignDivider}" Margin="12,2,12,2"/>

        <Button Command="{Binding DownloadAllParametersCommand}" Style="{DynamicResource MaterialDesignRaisedButton}" Background="#FF1976D2" Foreground="White" ToolTip="{lang:Lang AxisSetting_SetAllAxesToolTip}" Margin="0,0,0,0" materialDesign:ButtonAssist.CornerRadius="4" Padding="10,4">
            <StackPanel Orientation="Horizontal">
                <materialDesign:PackIcon Kind="PlaylistPlay" Width="15" Height="15" VerticalAlignment="Center"/>
                <TextBlock Text="{lang:Lang AxisSetting_SetAllAxes}" Margin="4,0,0,0" VerticalAlignment="Center" FontSize="12"/>
            </StackPanel>
        </Button>
        <Button Command="{Binding ReadAllFromCardCommand}" Style="{DynamicResource MaterialDesignOutlinedButton}" BorderBrush="#1976D2" Foreground="#1976D2" Margin="6,0,0,0" materialDesign:ButtonAssist.CornerRadius="4" Padding="10,4">
            <Button.ToolTip><TextBlock Text="{lang:Lang AxisSetting_ReadAllFromCardToolTip}"/></Button.ToolTip>
            <StackPanel Orientation="Horizontal">
                <materialDesign:PackIcon Kind="PlaylistPlus" Width="15" Height="15" VerticalAlignment="Center"/>
                <TextBlock Text="{lang:Lang AxisSetting_ReadAllFromCard}" Margin="4,0,0,0" VerticalAlignment="Center" FontSize="12"/>
            </StackPanel>
        </Button>

        <Border Width="1" Background="{DynamicResource MaterialDesignDivider}" Margin="12,2,12,2"/>

        <Button Command="{Binding SaveParamsCommand}" Style="{DynamicResource MaterialDesignRaisedButton}" Background="{DynamicResource MaterialDesign.Brush.Primary.Dark}" Foreground="White" ToolTip="{lang:Lang AxisSetting_SaveParamsToolTip}" materialDesign:ButtonAssist.CornerRadius="4" Padding="10,4">
            <StackPanel Orientation="Horizontal">
                <materialDesign:PackIcon Kind="ContentSave" Width="15" Height="15" VerticalAlignment="Center"/>
                <TextBlock Text="{lang:Lang AxisSetting_SaveParams}" VerticalAlignment="Center" Margin="4,0,0,0" FontSize="12"/>
            </StackPanel>
        </Button>
        <Button Command="{Binding LoadParamsCommand}" Style="{DynamicResource MaterialDesignOutlinedButton}" BorderBrush="{DynamicResource PrimaryHueMidBrush}" Foreground="{DynamicResource PrimaryHueMidBrush}" Margin="6,0,0,0" ToolTip="{lang:Lang AxisSetting_LoadParamsToolTip}" materialDesign:ButtonAssist.CornerRadius="4" Padding="10,4">
            <StackPanel Orientation="Horizontal">
                <materialDesign:PackIcon Kind="FolderOpen" Width="15" Height="15" VerticalAlignment="Center"/>
                <TextBlock Text="{lang:Lang AxisSetting_LoadParams}" VerticalAlignment="Center" Margin="4,0,0,0" FontSize="12"/>
            </StackPanel>
        </Button>

        <Border Width="1" Background="{DynamicResource MaterialDesignDivider}" Margin="12,2,12,2" Visibility="{Binding IsSystemMode, Converter={StaticResource BoolToVisibilityConverter}}"/>

        <Button Command="{Binding SaveSystemParamsCommand}" Style="{DynamicResource MaterialDesignRaisedButton}" Background="#FFF57C00" Foreground="White" ToolTip="{lang:Lang AxisSetting_SaveSystemConfigToolTip}" Margin="0,0,0,0" Visibility="{Binding IsSystemMode, Converter={StaticResource BoolToVisibilityConverter}}" materialDesign:ButtonAssist.CornerRadius="4" Padding="10,4">
            <StackPanel Orientation="Horizontal">
                <materialDesign:PackIcon Kind="ContentSaveAll" Width="15" Height="15" VerticalAlignment="Center"/>
                <TextBlock Text="{lang:Lang AxisSetting_SaveInterpolationSystem}" VerticalAlignment="Center" Margin="4,0,0,0" FontSize="12"/>
            </StackPanel>
        </Button>
        <Button Command="{Binding ApplySystemParamsCommand}" Style="{DynamicResource MaterialDesignRaisedButton}" Background="#FF00838F" Foreground="White" ToolTip="{lang:Lang AxisSetting_ApplySystemSettingsToolTip}" Margin="6,0,0,0" Visibility="{Binding IsSystemMode, Converter={StaticResource BoolToVisibilityConverter}}" materialDesign:ButtonAssist.CornerRadius="4" Padding="10,4">
            <StackPanel Orientation="Horizontal">
                <materialDesign:PackIcon Kind="ShapePolygonPlus" Width="15" Height="15" VerticalAlignment="Center"/>
                <TextBlock Text="{lang:Lang AxisSetting_ApplyInterpolationSystem}" Margin="4,0,0,0" VerticalAlignment="Center" FontSize="12"/>
            </StackPanel>
        </Button>
    </StackPanel>
</Border>
```

**布局说明：**
- 所有模式通用按钮：Read From Card / Write To Card / Set All Axes / Read All From Card / Save Params / Load Params（无 Visibility 限制）
- 插补系专属按钮：Save Interp System / Apply Interp System（仅 IsSystemMode 可见）
- 使用分隔线 `Border Width="1"` 分组，替代 Badge 标签

- [ ] **Step 2: 验证构建**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\MotionControl\MotionControl.csproj --no-restore`
Expected: BUILD SUCCESS

---

### Task 5: 移除 AxisParameterService 中卡操作的自动保存

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\MotionControl\Services\AxisParameterService.cs`

- [ ] **Step 1: 移除 WriteAllToCardAsync 中的 SaveAllAxisParameters 调用**

将 `WriteAllToCardAsync` 方法（约第242-265行）中第262行删除：
```csharp
// 删除此行
SaveAllAxisParameters(allAxes);
```

保留其余逻辑不变。

- [ ] **Step 2: 移除 ReadAllFromCardAsync 中的 SaveAllAxisParameters 调用**

将 `ReadAllFromCardAsync` 方法（约第270-293行）中第290行删除：
```csharp
// 删除此行
SaveAllAxisParameters(allAxes);
```

保留其余逻辑不变。

- [ ] **Step 3: 验证构建**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\MotionControl\MotionControl.csproj --no-restore`
Expected: BUILD SUCCESS

---

### Task 6: 全量构建验证

- [ ] **Step 1: 全量构建**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\GZQL_MACHINE.sln --no-restore`
Expected: BUILD SUCCESS, 0 errors

- [ ] **Step 2: 确认 AxisSettings 目录结构**

确认 `bin\Debug\net9.0-windows7.0\Config\AxisSettings\` 目录下会生成：
- `AllAxisParameters.json` — 所有轴参数
- `AllInterpolationSystems.json` — 所有插补系参数
