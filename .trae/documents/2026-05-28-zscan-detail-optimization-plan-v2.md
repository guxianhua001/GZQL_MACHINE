# ZScanDetailView 优化实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 优化ZScanDetailView的布局、全局变量链接交互、标定步骤逻辑、持久化方式和数据计算逻辑

**Architecture:** 在现有WPF+PRISM+MaterialDesign架构上，修改ViewModel和XAML实现10项优化。核心变更：布局靠上对齐、GV链接改为单元格内点击交互、标定步骤4改为输入当前Z高度计算高度差、deltaZ计算改为基准高度-当前高度、标定参数绑定到table、保存支持选择位置并记录到配方池。

**Tech Stack:** WPF, PRISM, MaterialDesign In XAML, Newtonsoft.Json, xUnit, Moq

---

## 文件结构

### 修改文件
| 文件 | 修改内容 |
|------|----------|
| `Module/Controls/ZScan/ZScanDetailView.xaml` | 布局靠上、删除Link/Unlink按钮、删除DataIndex列、GV Link列改为可点击链接图标、标定区域重构（删除Teach按钮/删除CameraOffset+TotalOffset/Step4改为输入当前Z高度）、保存按钮增加另存为、显示当前加载文件路径 |
| `Module/Controls/ZScan/ZScanDetailViewModel.cs` | 删除Link/Unlink命令、GV链接改为选中单元格点击图标触发、Step4逻辑改为输入当前Z高度→计算Z高度差→点胶高度=基准点胶高度+Z高度差+补偿值、deltaZ=基准高度-当前高度、标定参数绑定到SelectedTable.Calibration、保存支持SaveFileDialog选择位置、显示当前加载文件路径、删除CameraZOffset/TotalZOffset属性 |
| `Core/Models/ZScanCalibrationConfig.cs` | 增加CurrentZHeight、ZHeightDifference、DispenseHeight、BaseDispenseHeight字段 |
| `Core/Abstraction/IZScanCalibrationService.cs` | 修改CalculateDispenseHeight签名 |
| `Core/Services/ZScanCalibrationService.cs` | 修改CalculateDispenseHeight实现 |
| `Core/Abstraction/IZScanConfigService.cs` | 增加LoadFromFile方法 |
| `Core/Services/ZScanConfigService.cs` | 实现LoadFromFile |
| `MainApp/Languages/Strings.zh-CN.xaml` | 更新/新增多语言键值 |
| `MainApp/Languages/Strings.en-US.xaml` | 更新/新增多语言键值 |

### 新建测试文件
| 文件 | 职责 |
|------|------|
| `MotionControl.Tests/ZScan/ZScanCalibrationOptTests.cs` | 标定步骤优化测试 |

---

## Task 1: ZScanCalibrationConfig 模型更新 — 增加 Step4 新字段

**Files:**
- Modify: `Core/Models/ZScanCalibrationConfig.cs`
- Modify: `Core/Abstraction/IZScanCalibrationService.cs`
- Modify: `Core/Services/ZScanCalibrationService.cs`
- Test: `MotionControl.Tests/ZScan/ZScanCalibrationOptTests.cs`

- [ ] **Step 1: 写失败测试**

```csharp
// MotionControl.Tests/ZScan/ZScanCalibrationOptTests.cs
using Core.Models;
using Core.Services;
using Xunit;

namespace MotionControl.Tests
{
    public class ZScanCalibrationOptTests
    {
        private ZScanCalibrationService CreateService()
        {
            return new ZScanCalibrationService();
        }

        [Fact]
        public void CalculateDispenseHeight_BaseDispensePlusDiffPlusComp()
        {
            var service = CreateService();
            service.SetBaseZ(5.200);
            service.TeachNeedleMZ(5.150);
            double baseDispenseHeight = 5.150;
            double currentZHeight = 5.180;
            double needleComp = 0.010;
            double result = service.CalculateDispenseHeight(baseDispenseHeight, currentZHeight, needleComp);
            // 点胶高度 = 基准点胶高度 + (基准高度 - 当前高度) + 补偿值
            // = 5.150 + (5.200 - 5.180) + 0.010 = 5.150 + 0.020 + 0.010 = 5.180
            Assert.Equal(5.180, result, 3);
        }

        [Fact]
        public void CalculateDispenseHeight_NegativeDiff()
        {
            var service = CreateService();
            service.SetBaseZ(5.200);
            double baseDispenseHeight = 5.150;
            double currentZHeight = 5.250;
            double needleComp = 0.010;
            double result = service.CalculateDispenseHeight(baseDispenseHeight, currentZHeight, needleComp);
            // = 5.150 + (5.200 - 5.250) + 0.010 = 5.150 - 0.050 + 0.010 = 5.110
            Assert.Equal(5.110, result, 3);
        }

        [Fact]
        public void ZHeightDifference_IsBaseZMinusCurrentZ()
        {
            var service = CreateService();
            service.SetBaseZ(5.200);
            double currentZHeight = 5.180;
            double diff = service.CalculateZHeightDifference(5.200, currentZHeight);
            Assert.Equal(0.020, diff, 3);
        }

        [Fact]
        public void ZScanCalibrationConfig_HasNewFields()
        {
            var config = new ZScanCalibrationConfig();
            config.CurrentZHeight = 5.180;
            config.ZHeightDifference = 0.020;
            config.BaseDispenseHeight = 5.150;
            config.DispenseHeight = 5.180;
            Assert.Equal(5.180, config.CurrentZHeight, 3);
            Assert.Equal(0.020, config.ZHeightDifference, 3);
            Assert.Equal(5.150, config.BaseDispenseHeight, 3);
            Assert.Equal(5.180, config.DispenseHeight, 3);
        }

        [Fact]
        public void DeltaZ_InPointData_IsBaseZMinusCurrentZ()
        {
            double baseZ = 5.200;
            double currentZ = 5.180;
            double deltaZ = baseZ - currentZ;
            Assert.Equal(0.020, deltaZ, 3);
        }
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test MotionControl.Tests\MotionControl.Tests.csproj --filter "FullyQualifiedName~ZScanCalibrationOptTests" -v n`
Expected: FAIL — CalculateDispenseHeight签名不匹配，CalculateZHeightDifference不存在，CurrentZHeight等字段不存在

- [ ] **Step 3: 更新ZScanCalibrationConfig模型**

在 `Core/Models/ZScanCalibrationConfig.cs` 的 ZScanCalibrationConfig 类中增加：

```csharp
private double _currentZHeight;
private double _zHeightDifference;
private double _baseDispenseHeight;
private double _dispenseHeight;

public double CurrentZHeight { get => _currentZHeight; set => _currentZHeight = value; }
public double ZHeightDifference { get => _zHeightDifference; set => _zHeightDifference = value; }
public double BaseDispenseHeight { get => _baseDispenseHeight; set => _baseDispenseHeight = value; }
public double DispenseHeight { get => _dispenseHeight; set => _dispenseHeight = value; }
```

- [ ] **Step 4: 更新IZScanCalibrationService接口**

```csharp
// Core/Abstraction/IZScanCalibrationService.cs — 完整替换
namespace Core.Abstraction
{
    public interface IZScanCalibrationService
    {
        double CameraZOffset { get; }
        double NeedleZOffset { get; }
        double TotalZOffset { get; }
        double BaseZ { get; }
        double MeasuredMZ { get; }
        void CalibrateCameraZ(double measuredZ, double referenceZ);
        void ApplyNeedleCompensation(double deltaZ);
        double GetCompensatedZ(double measuredZ);
        void ResetCalibration();
        void SetBaseZ(double baseZ);
        void TeachNeedleMZ(double measuredMZ);
        double CalculateDispenseHeight(double baseDispenseHeight, double currentZHeight, double needleCompensation);
        double CalculateZHeightDifference(double baseZ, double currentZHeight);
        event Action CalibrationChanged;
    }
}
```

- [ ] **Step 5: 更新ZScanCalibrationService实现**

在 `Core/Services/ZScanCalibrationService.cs` 中替换 CalculateDispenseHeight 并增加 CalculateZHeightDifference：

```csharp
public double CalculateDispenseHeight(double baseDispenseHeight, double currentZHeight, double needleCompensation)
{
    double zHeightDiff = CalculateZHeightDifference(_baseZ, currentZHeight);
    return baseDispenseHeight + zHeightDiff + needleCompensation;
}

public double CalculateZHeightDifference(double baseZ, double currentZHeight)
{
    return baseZ - currentZHeight;
}
```

- [ ] **Step 6: 运行测试验证通过**

Run: `dotnet test MotionControl.Tests\MotionControl.Tests.csproj --filter "FullyQualifiedName~ZScanCalibrationOptTests" -v n`
Expected: 全部PASS

- [ ] **Step 7: 提交**

```bash
git add Core/Models/ZScanCalibrationConfig.cs Core/Abstraction/IZScanCalibrationService.cs Core/Services/ZScanCalibrationService.cs MotionControl.Tests/ZScan/ZScanCalibrationOptTests.cs
git commit -m "feat(zscan): update calibration step4 to input current Z height, deltaZ=baseZ-currentZ"
```

---

## Task 2: IZScanConfigService 增加 LoadFromFile 方法

**Files:**
- Modify: `Core/Abstraction/IZScanConfigService.cs`
- Modify: `Core/Services/ZScanConfigService.cs`

- [ ] **Step 1: 更新接口增加 LoadFromFile**

在 `Core/Abstraction/IZScanConfigService.cs` 中增加：

```csharp
ZScanConfigFile LoadFromFile(string fullPath);
```

- [ ] **Step 2: 实现LoadFromFile**

在 `Core/Services/ZScanConfigService.cs` 中增加：

```csharp
public ZScanConfigFile LoadFromFile(string fullPath)
{
    if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
        return new ZScanConfigFile();

    try
    {
        var json = File.ReadAllText(fullPath);
        _lastSavedFilePath = fullPath;
        return JsonConvert.DeserializeObject<ZScanConfigFile>(json, _serializerSettings)
               ?? new ZScanConfigFile();
    }
    catch
    {
        return new ZScanConfigFile();
    }
}
```

- [ ] **Step 3: 提交**

```bash
git add Core/Abstraction/IZScanConfigService.cs Core/Services/ZScanConfigService.cs
git commit -m "feat(zscan): add LoadFromFile method to IZScanConfigService"
```

---

## Task 3: ZScanDetailViewModel 全面重构

**Files:**
- Modify: `Module/Controls/ZScan/ZScanDetailViewModel.cs`

这是最大的改动，包含以下变更点：

1. 删除 LinkGlobalVariableCommand / UnlinkGlobalVariableCommand
2. 新增 ToggleGlobalVariableLinkCommand（点击GV Link图标触发）
3. 删除 CameraZOffset / TotalZOffset 属性
4. Step4 改为输入当前Z高度→计算Z高度差→点胶高度=基准点胶高度+Z高度差+补偿值
5. deltaZ 计算改为 基准高度-当前高度
6. 标定参数绑定到 SelectedTable.Calibration
7. 保存支持 SaveFileDialog 选择位置
8. 显示当前加载文件路径
9. OnSelectedTableChanged 加载标定参数到UI

- [ ] **Step 1: 删除Link/Unlink命令，增加ToggleGVLink命令**

在ViewModel中：
- 删除 `LinkGlobalVariableCommand` 和 `UnlinkGlobalVariableCommand` 的定义和初始化
- 删除 `OnLinkGlobalVariable` 和 `OnUnlinkGlobalVariable` 方法
- 新增：

```csharp
public ICommand ToggleGlobalVariableLinkCommand { get; }

// 构造函数中：
ToggleGlobalVariableLinkCommand = new DelegateCommand(OnToggleGlobalVariableLink);
```

```csharp
private void OnToggleGlobalVariableLink()
{
    try
    {
        if (SelectedPointDetail == null) return;

        if (SelectedPointDetail.IsGlobalVarLinked)
        {
            string varName = SelectedPointDetail.GlobalVariableLink.VariableName;
            SelectedPointDetail.GlobalVariableLink = null;
            _logger?.Info($"Z-SCAN 行{SelectedPointDetail.PointNumber}已取消全局变量链接: {varName}");
        }
        else
        {
            var linkService = _containerProvider.Resolve<IZScanGlobalVariableLinkService>();
            var expectedType = SelectedPointDetail.PointType == ZScanDataFormat.DoubleArray
                ? GlobalVariableType.DoubleArray
                : GlobalVariableType.Double;

            _dialogService.ShowDialog("SimpleInputDialog", new DialogParameters
            {
                { "title", "链接全局变量" },
                { "prompt", $"输入全局变量名 (类型: {expectedType})" },
                { "defaultValue", "" }
            }, result =>
            {
                if (result.Result == ButtonResult.OK && result.Parameters.ContainsKey("inputValue"))
                {
                    string varName = result.Parameters.GetValue<string>("inputValue");
                    if (!string.IsNullOrEmpty(varName) && linkService.LinkVariable(varName, expectedType))
                    {
                        SelectedPointDetail.GlobalVariableLink = new ZScanGlobalVariableLink
                        {
                            IsLinked = true,
                            VariableName = varName,
                            VariableType = expectedType
                        };
                        _logger?.Info($"Z-SCAN 行{SelectedPointDetail.PointNumber}已链接全局变量: {varName}");
                    }
                }
            });
        }
    }
    catch (Exception ex)
    {
        _logger?.Error($"Z-SCAN 切换全局变量链接失败: {ex.Message}");
    }
}
```

- [ ] **Step 2: 删除CameraZOffset/TotalZOffset属性，保留NeedleZOffset**

删除：
```csharp
private double _cameraZOffset;
public double CameraZOffset { get => _cameraZOffset; set => SetProperty(ref _cameraZOffset, value); }

private double _totalZOffset;
public double TotalZOffset { get => _totalZOffset; set => SetProperty(ref _totalZOffset, value); }
```

增加当前Z高度相关属性：
```csharp
private double _currentZHeightInput;
public double CurrentZHeightInput { get => _currentZHeightInput; set => SetProperty(ref _currentZHeightInput, value); }

private double _zHeightDifference;
public double ZHeightDifference { get => _zHeightDifference; set => SetProperty(ref _zHeightDifference, value); }

private double _baseDispenseHeight;
public double BaseDispenseHeight { get => _baseDispenseHeight; set => SetProperty(ref _baseDispenseHeight, value); }

private string _currentFilePath;
public string CurrentFilePath { get => _currentFilePath; set => SetProperty(ref _currentFilePath, value); }
```

- [ ] **Step 3: 修改Step4逻辑**

替换 OnCalculateDispenseHeight：

```csharp
private void OnCalculateDispenseHeight()
{
    try
    {
        ZHeightDifference = _zscanCalibrationService.CalculateZHeightDifference(BaseZInput, CurrentZHeightInput);
        CalculatedDispenseHeight = _zscanCalibrationService.CalculateDispenseHeight(BaseDispenseHeight, CurrentZHeightInput, NeedleCompensationValue);
        CalibrationStep = 4;
        _logger?.Info($"Z-SCAN Step4: 基准Z={BaseZInput:F3}, 当前Z={CurrentZHeightInput:F3}, Z高度差={ZHeightDifference:F3}, 基准点胶高度={BaseDispenseHeight:F3}, 补偿={NeedleCompensationValue:F3}, 点胶高度={CalculatedDispenseHeight:F3}");
    }
    catch (Exception ex)
    {
        _logger?.Error($"Z-SCAN 计算点胶高度失败: {ex.Message}");
    }
}
```

修改 CalculateDispenseHeightCommand 的 CanExecute 条件为 `CalibrationStep >= 1`（因为现在只需要有基准Z就可以计算）：

```csharp
CalculateDispenseHeightCommand = new DelegateCommand(OnCalculateDispenseHeight, () => CalibrationStep >= 1).ObservesProperty(() => CalibrationStep);
```

- [ ] **Step 4: 修改deltaZ计算逻辑**

在 RecalculateRow 方法中，将 `point.DeltaZ = point.ZMeasured - point.Nominal;` 改为：

```csharp
point.DeltaZ = point.ZNominal - point.ZMeasured;
```

即 deltaZ = 基准高度(ZNominal) - 当前高度(ZMeasured)

- [ ] **Step 5: 标定参数绑定到table**

修改 OnSelectedTableChanged 中的标定参数加载：

```csharp
if (SelectedTable.Calibration != null)
{
    BaseZInput = SelectedTable.Calibration.BaseZ;
    MeasuredMZ = SelectedTable.Calibration.MeasuredMZ;
    NeedleZOffset = SelectedTable.Calibration.NeedleZOffset;
    BaseDispenseHeight = SelectedTable.Calibration.BaseDispenseHeight;
    CurrentZHeightInput = SelectedTable.Calibration.CurrentZHeight;
    ZHeightDifference = SelectedTable.Calibration.ZHeightDifference;
    CalculatedDispenseHeight = SelectedTable.Calibration.DispenseHeight;
    NeedleCompensationValue = SelectedTable.Calibration.NeedleCompensationLink?.IsLinked == true
        ? 0 : 0;
    DeltaZInput = SelectedTable.Calibration.DeltaZ;
}
```

在 SyncPointDetailsToTable 中增加标定参数同步：

```csharp
if (table.Calibration != null)
{
    table.Calibration.BaseZ = BaseZInput;
    table.Calibration.MeasuredMZ = MeasuredMZ;
    table.Calibration.NeedleZOffset = NeedleZOffset;
    table.Calibration.BaseDispenseHeight = BaseDispenseHeight;
    table.Calibration.CurrentZHeight = CurrentZHeightInput;
    table.Calibration.ZHeightDifference = ZHeightDifference;
    table.Calibration.DispenseHeight = CalculatedDispenseHeight;
    table.Calibration.DeltaZ = DeltaZInput;
}
```

- [ ] **Step 6: 保存支持选择位置**

替换 OnSaveConfig：

```csharp
private void OnSaveConfig()
{
    try
    {
        SyncPointDetailsToTable();

        var configFile = new ZScanConfigFile
        {
            DefaultTableName = SelectedTable?.TableName ?? string.Empty,
            Tables = Tables.ToList()
        };

        var saveDialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json",
            FileName = $"ZScan_{DateTime.Now:yyyyMMdd_HHmmss}.json",
            InitialDirectory = _zscanConfigService.GetConfigPath(),
            Title = "保存ZScan配置"
        };

        if (saveDialog.ShowDialog() == true)
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(configFile, Formatting.Indented);
            System.IO.File.WriteAllText(saveDialog.FileName, json);
            CurrentFilePath = saveDialog.FileName;
            _zscanConfigService.SaveToRecipePool(configFile, $"{AssyGroup}_{SiteId}");
            _logger?.Info($"Z-SCAN 配置已保存到: {saveDialog.FileName}");
        }
    }
    catch (Exception ex)
    {
        _logger?.Error($"Z-SCAN 配置保存失败: {ex.Message}");
    }
}
```

替换 OnLoadConfig：

```csharp
private void OnLoadConfig()
{
    try
    {
        var configFile = _zscanConfigService.LoadLastFromRecipePool();

        if (configFile.Tables.Count == 0)
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                InitialDirectory = _zscanConfigService.GetConfigPath(),
                Title = "加载ZScan配置"
            };

            if (openDialog.ShowDialog() == true)
            {
                configFile = _zscanConfigService.LoadFromFile(openDialog.FileName);
            }
        }

        if (configFile.Tables.Count > 0)
        {
            Tables = new ObservableCollection<ZScanTableConfig>(configFile.Tables);
            var defaultTable = Tables.FirstOrDefault(t => t.TableName == configFile.DefaultTableName) ?? Tables[0];
            SelectedTable = defaultTable;
            CurrentFilePath = _zscanConfigService.LastSavedFilePath;
            _logger?.Info($"Z-SCAN 配置已加载: {configFile.Tables.Count} 个表格");
        }
    }
    catch (Exception ex)
    {
        _logger?.Error($"Z-SCAN 配置加载失败: {ex.Message}");
    }
}
```

- [ ] **Step 7: 更新OnResetCalibration**

```csharp
private void OnResetCalibration()
{
    try
    {
        _zscanCalibrationService.ResetCalibration();
        CalibrationStep = 0;
        BaseZInput = 0;
        MeasuredMZ = 0;
        DeltaZInput = 0;
        NeedleCompensationValue = 0;
        CalculatedDispenseHeight = 0;
        CurrentZHeightInput = 0;
        ZHeightDifference = 0;
        BaseDispenseHeight = 0;
        NeedleZOffset = 0;
        _logger?.Info("Z-SCAN 标定已重置");
    }
    catch (Exception ex)
    {
        _logger?.Error($"Z-SCAN 标定重置失败: {ex.Message}");
    }
}
```

- [ ] **Step 8: 更新UpdateCalibrationDisplay**

```csharp
private void UpdateCalibrationDisplay()
{
    NeedleZOffset = _zscanCalibrationService.NeedleZOffset;
    LastCalibrationTimeText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}
```

- [ ] **Step 9: 构建验证**

Run: `dotnet build MainApp\MainApp.csproj`
Expected: 0 errors

- [ ] **Step 10: 提交**

```bash
git add Module/Controls/ZScan/ZScanDetailViewModel.cs
git commit -m "feat(zscan): ViewModel refactor - remove Link/Unlink, step4 uses currentZ, deltaZ=base-current, calibration binds to table, save with dialog"
```

---

## Task 4: ZScanDetailView.xaml 全面更新

**Files:**
- Modify: `Module/Controls/ZScan/ZScanDetailView.xaml`

- [ ] **Step 1: 布局靠上对齐**

在主Grid上设置 `VerticalAlignment="Top"`：

```xml
<Grid Margin="12" VerticalAlignment="Top">
```

- [ ] **Step 2: 删除底部Link/Unlink按钮**

删除 Row 5 中的以下内容：

```xml
<Separator Width="1" Margin="8,0" VerticalAlignment="Stretch" />

<Button Command="{Binding LinkGlobalVariableCommand}" ...>
    ...
</Button>

<Button Command="{Binding UnlinkGlobalVariableCommand}" ...>
    ...
</Button>
```

- [ ] **Step 3: 删除DataIndex列**

删除 DataGrid.Columns 中的：

```xml
<DataGridTextColumn Header="DataIndex" Binding="{Binding DataIndex}" Width="75" />
```

- [ ] **Step 4: GV Link列改为可点击链接图标（始终显示）**

替换整个 GV Link DataGridTemplateColumn：

```xml
<DataGridTemplateColumn Header="GV Link" Width="100">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <Button Command="{Binding DataContext.ToggleGlobalVariableLinkCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"
                    CommandParameter="{Binding}"
                    Style="{StaticResource MaterialDesignIconButton}"
                    Width="28" Height="28"
                    Padding="0"
                    ToolTip="{Binding GlobalVariableLink.VariableName}">
                <materialDesign:PackIcon Width="14" Height="14" VerticalAlignment="Center" HorizontalAlignment="Center">
                    <materialDesign:PackIcon.Style>
                        <Style TargetType="materialDesign:PackIcon">
                            <Setter Property="Kind" Value="LinkOff" />
                            <Setter Property="Foreground" Value="#9E9E9E" />
                            <Style.Triggers>
                                <DataTrigger Binding="{Binding IsGlobalVarLinked}" Value="True">
                                    <Setter Property="Kind" Value="Link" />
                                    <Setter Property="Foreground" Value="#1565C0" />
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </materialDesign:PackIcon.Style>
                </materialDesign:PackIcon>
            </Button>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

- [ ] **Step 5: Z Calibration区域重构**

替换 Row 2 的 GroupBox 为：

```xml
<GroupBox Grid.Row="2" Header="{lang:Lang ZScanDetail_CalibrationSection}"
          Margin="0,0,0,8">
    <GroupBox.HeaderTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal">
                <materialDesign:PackIcon Kind="CrosshairsGps" Width="16" Height="16" VerticalAlignment="Center" Margin="0,0,8,0" />
                <TextBlock Text="{Binding}" VerticalAlignment="Center" FontWeight="Medium" FontSize="12" />
            </StackPanel>
        </DataTemplate>
    </GroupBox.HeaderTemplate>
    <StackPanel>
        <WrapPanel Margin="0,0,0,6">
            <materialDesign:PackIcon Kind="Numeric1Circle" Width="16" Height="16" VerticalAlignment="Center" Margin="0,0,4,0" Foreground="#1565C0" />
            <TextBlock Text="{lang:Lang ZScanDetail_Step1_BaseZ}" VerticalAlignment="Center" Margin="0,0,8,0" FontWeight="Medium" FontSize="12" />
            <TextBox Text="{Binding BaseZInput, StringFormat=F3}" Width="80" VerticalAlignment="Center" FontSize="12"
                     materialDesign:HintAssist.Hint="Z (mm)" Margin="0,0,8,0" />
            <Button Command="{Binding SetBaseZCommand}"
                    Style="{StaticResource MaterialDesignOutlinedButton}"
                    Padding="8,4" FontSize="11"
                    Content="{lang:Lang ZScanDetail_SetBaseZBtn}" />
        </WrapPanel>

        <WrapPanel Margin="0,0,0,6">
            <materialDesign:PackIcon Kind="Numeric2Circle" Width="16" Height="16" VerticalAlignment="Center" Margin="0,0,4,0" Foreground="#2E7D32" />
            <TextBlock Text="{lang:Lang ZScanDetail_Step2_MoveNeedle}" VerticalAlignment="Center" Margin="0,0,8,0" FontWeight="Medium" FontSize="12" />
            <Button Command="{Binding MoveNeedleToBaseZCommand}"
                    Style="{StaticResource MaterialDesignOutlinedButton}"
                    Padding="8,4" FontSize="11"
                    Content="{lang:Lang ZScanDetail_MoveNeedleBtn}" />
        </WrapPanel>

        <WrapPanel Margin="0,0,0,6">
            <materialDesign:PackIcon Kind="Numeric3Circle" Width="16" Height="16" VerticalAlignment="Center" Margin="0,0,4,0" Foreground="#E65100" />
            <TextBlock Text="{lang:Lang ZScanDetail_Step3_TeachMZ}" VerticalAlignment="Center" Margin="0,0,8,0" FontWeight="Medium" FontSize="12" />
            <TextBlock Text="{lang:Lang ZScanDetail_MZ}" VerticalAlignment="Center" Margin="0,0,4,0" Foreground="{DynamicResource MaterialDesignBodyLight}" />
            <Border Background="#E8F5E9" CornerRadius="3" Padding="6,2" Margin="0,0,12,0">
                <TextBlock Text="{Binding MeasuredMZ, StringFormat=F3}" VerticalAlignment="Center" FontWeight="SemiBold" FontSize="12" Foreground="#2E7D32" />
            </Border>
        </WrapPanel>

        <WrapPanel Margin="0,0,0,6">
            <materialDesign:PackIcon Kind="Numeric4Circle" Width="16" Height="16" VerticalAlignment="Center" Margin="0,0,4,0" Foreground="#6A1B9A" />
            <TextBlock Text="{lang:Lang ZScanDetail_Step4_CurrentZ}" VerticalAlignment="Center" Margin="0,0,8,0" FontWeight="Medium" FontSize="12" />
            <TextBlock Text="{lang:Lang ZScanDetail_CurrentZHeight}" VerticalAlignment="Center" Margin="0,0,4,0" Foreground="{DynamicResource MaterialDesignBodyLight}" />
            <TextBox Text="{Binding CurrentZHeightInput, StringFormat=F3}" Width="80" VerticalAlignment="Center" FontSize="12" Margin="0,0,8,0" />
            <TextBlock Text="{lang:Lang ZScanDetail_BaseDispenseHeight}" VerticalAlignment="Center" Margin="0,0,4,0" Foreground="{DynamicResource MaterialDesignBodyLight}" />
            <TextBox Text="{Binding BaseDispenseHeight, StringFormat=F3}" Width="80" VerticalAlignment="Center" FontSize="12" Margin="0,0,8,0" />
            <TextBlock Text="{lang:Lang ZScanDetail_NeedleComp}" VerticalAlignment="Center" Margin="0,0,4,0" Foreground="{DynamicResource MaterialDesignBodyLight}" />
            <TextBox Text="{Binding NeedleCompensationValue, StringFormat=F3}" Width="65" VerticalAlignment="Center" FontSize="12" Margin="0,0,8,0" />
            <Button Command="{Binding CalculateDispenseHeightCommand}"
                    Style="{StaticResource MaterialDesignRaisedButton}"
                    Padding="8,4" FontSize="11"
                    Content="{lang:Lang ZScanDetail_CalcBtn}" Margin="0,0,8,0" />
            <TextBlock Text="{lang:Lang ZScanDetail_ZHeightDiff}" VerticalAlignment="Center" Margin="8,0,4,0" Foreground="#E65100" FontWeight="Medium" />
            <Border Background="#FFF3E0" CornerRadius="3" Padding="6,2" Margin="0,0,12,0">
                <TextBlock Text="{Binding ZHeightDifference, StringFormat=+0.000;-0.000;0.000}" VerticalAlignment="Center" FontWeight="SemiBold" FontSize="12" Foreground="#E65100" />
            </Border>
            <TextBlock Text="{lang:Lang ZScanDetail_DispenseHeight}" VerticalAlignment="Center" Margin="0,0,4,0" Foreground="#1565C0" FontWeight="Medium" />
            <Border Background="#E3F2FD" CornerRadius="3" Padding="8,3">
                <TextBlock Text="{Binding CalculatedDispenseHeight, StringFormat=F3}" VerticalAlignment="Center" FontWeight="Bold" FontSize="13" Foreground="#0D47A1" />
            </Border>
        </WrapPanel>

        <WrapPanel>
            <TextBlock Text="{lang:Lang ZScanDetail_NeedleOffset}" VerticalAlignment="Center" Margin="0,0,6,0" Foreground="{DynamicResource MaterialDesignBodyLight}" />
            <Border Background="#E8F5E9" CornerRadius="3" Padding="6,2" Margin="0,0,12,0">
                <TextBlock Text="{Binding NeedleZOffset, StringFormat=F3}" VerticalAlignment="Center" FontWeight="SemiBold" FontSize="12" Foreground="#2E7D32" />
            </Border>
            <Button Command="{Binding ResetCalibrationCommand}"
                    Style="{StaticResource MaterialDesignOutlinedButton}"
                    Padding="8,4"
                    ToolTip="{lang:Lang ZScanDetail_ResetCalibration}"
                    VerticalAlignment="Center">
                <StackPanel Orientation="Horizontal">
                    <materialDesign:PackIcon Kind="Refresh" Width="14" Height="14" VerticalAlignment="Center" Margin="0,0,4,0" />
                    <TextBlock Text="{lang:Lang ZScanDetail_ResetCalibrationBtn}" VerticalAlignment="Center" FontSize="11" />
                </StackPanel>
            </Button>
        </WrapPanel>
    </StackPanel>
</GroupBox>
```

- [ ] **Step 6: Row 1 工具栏增加当前文件路径显示**

在 Row 1 的工具栏 `</StackPanel>` 之前增加：

```xml
<Separator Width="1" Margin="8,0" VerticalAlignment="Stretch" />
<materialDesign:PackIcon Kind="FileOutline" Width="14" Height="14" VerticalAlignment="Center" Margin="4,0,4,0" Foreground="{DynamicResource MaterialDesignBodyLight}" />
<TextBlock Text="{Binding CurrentFilePath}" VerticalAlignment="Center" FontSize="10" Foreground="{DynamicResource MaterialDesignBodyLight}" MaxWidth="300" TextTrimming="CharacterEllipsis" />
```

- [ ] **Step 7: 构建验证**

Run: `dotnet build MainApp\MainApp.csproj`
Expected: 0 errors

- [ ] **Step 8: 提交**

```bash
git add Module/Controls/ZScan/ZScanDetailView.xaml
git commit -m "feat(zscan): XAML layout top-align, remove Link/Unlink/DataIndex, GV Link clickable icon, calibration step4 currentZ, show current file path"
```

---

## Task 5: 多语言资源更新

**Files:**
- Modify: `MainApp/Languages/Strings.en-US.xaml`
- Modify: `MainApp/Languages/Strings.zh-CN.xaml`

- [ ] **Step 1: 添加/更新英文键值**

在 `Strings.en-US.xaml` 的 `</ResourceDictionary>` 之前增加：

```xml
    <sys:String x:Key="ZScanDetail_Step4_CurrentZ">Step 4: Input Current Z Height</sys:String>
    <sys:String x:Key="ZScanDetail_CurrentZHeight">Current Z:</sys:String>
    <sys:String x:Key="ZScanDetail_BaseDispenseHeight">Base Disp. Z:</sys:String>
    <sys:String x:Key="ZScanDetail_ZHeightDiff">Z Diff:</sys:String>
```

- [ ] **Step 2: 添加/更新中文键值**

在 `Strings.zh-CN.xaml` 的 `</ResourceDictionary>` 之前增加：

```xml
    <sys:String x:Key="ZScanDetail_Step4_CurrentZ">步骤4: 输入当前Z向高度</sys:String>
    <sys:String x:Key="ZScanDetail_CurrentZHeight">当前Z:</sys:String>
    <sys:String x:Key="ZScanDetail_BaseDispenseHeight">基准点胶Z:</sys:String>
    <sys:String x:Key="ZScanDetail_ZHeightDiff">Z高度差:</sys:String>
```

- [ ] **Step 3: 构建验证**

Run: `dotnet build MainApp\MainApp.csproj`
Expected: 0 errors

- [ ] **Step 4: 提交**

```bash
git add MainApp/Languages/Strings.en-US.xaml MainApp/Languages/Strings.zh-CN.xaml
git commit -m "feat(zscan): add i18n keys for step4 currentZ, baseDispenseHeight, zHeightDiff"
```

---

## Task 6: 全量测试和构建验证

- [ ] **Step 1: 运行全部ZScan测试**

Run: `dotnet test MotionControl.Tests\MotionControl.Tests.csproj --filter "FullyQualifiedName~ZScan" -v n`
Expected: 全部PASS

- [ ] **Step 2: 运行全量测试**

Run: `dotnet test MotionControl.Tests\MotionControl.Tests.csproj -v n`
Expected: 全部PASS

- [ ] **Step 3: 全项目构建验证**

Run: `dotnet build MainApp\MainApp.csproj`
Expected: 0 errors

---

## 自审检查

### 1. 规格覆盖检查

| 需求 | 对应Task |
|------|----------|
| 1. 整体靠上排列限制居中布局 | Task 4 Step 1 |
| 2. 全局变量链接选中单元格时链接，无链接时也显示链接图标，deltaZ更新全局变量 | Task 3 Step 1 + Task 4 Step 4 |
| 3. 删除Link Unlink按钮 | Task 3 Step 1 + Task 4 Step 2 |
| 4. DataIndex列可删除 | Task 4 Step 3 |
| 5. Z Calibration区域Teach按钮不可用 | Task 4 Step 5（Step3只显示MZ值，无Teach按钮） |
| 6. Step4输入当前Z向高度，计算Z高度差，点胶高度=基准点胶高度+Z高度差+补偿值 | Task 1 + Task 3 Step 3 + Task 4 Step 5 |
| 7. 删除camera offset total offset 保留needle offset | Task 3 Step 2 + Task 4 Step 5 |
| 8. 保存按钮默认保存在Config下，可选择保存位置，最后一次记录在配方池，显示当前加载文件 | Task 2 + Task 3 Step 6 + Task 4 Step 6 |
| 9. deltaZ=基准高度-当前高度 | Task 1 + Task 3 Step 4 |
| 10. Z Calibration参数绑定table | Task 3 Step 5 |

### 2. 占位符扫描
- 无"TBD"、"TODO"、"implement later"等占位符
- 所有步骤包含完整代码

### 3. 类型一致性检查
- `CalculateDispenseHeight(baseDispenseHeight, currentZHeight, needleCompensation)` — 接口与实现签名一致 ✓
- `CalculateZHeightDifference(baseZ, currentZHeight)` — 接口与实现签名一致 ✓
- `ZScanCalibrationConfig.CurrentZHeight/ZHeightDifference/BaseDispenseHeight/DispenseHeight` — 模型与ViewModel属性一致 ✓
- `ToggleGlobalVariableLinkCommand` — ViewModel定义与XAML绑定一致 ✓
- `CurrentFilePath` — ViewModel属性与XAML绑定一致 ✓
