# NeedleCameraAlignmentView 重构实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 参照 VisionCaptureView 的补偿编辑器模式重构 NeedleCameraAlignmentView，删除高度设置，将补偿改为表达式编辑器形式，状态栏移至底部，保存路径改为 Config/NeedleSystems，并集成配方池自动加载。

**Architecture:** 采用 VisionCaptureView 的"基础值 + 表达式 + 计算结果"三列补偿编辑器模式。ViewModel 新增 IRecipePoolService 依赖，通过 GetExtensionDataAsync/SetExtensionDataAsync 实现配方池自动加载/保存。保存路径统一到 Config/NeedleSystems 目录。UI 布局从三列改为两列（左侧系统选择+示教，右侧补偿编辑器），底部固定状态栏。

**Tech Stack:** WPF + PRISM + MaterialDesignInXAML + IRecipePoolService + IParameterStorage + DataTable.Compute 表达式求值

---

## 文件结构

| 操作 | 文件路径 | 职责 |
|------|---------|------|
| 修改 | `Module/Controls/Maintenance/NeedleCameraAlignmentView.xaml` | UI 布局重构：删除高度设置、补偿改为表达式编辑器、状态栏移底部 |
| 修改 | `Module/Controls/Maintenance/NeedleCameraAlignmentViewModel.cs` | 新增表达式属性、IRecipePoolService 依赖、配方池自动加载、保存路径修改 |
| 修改 | `Core/Models/NeedleCameraCalibrationParams.cs` | 新增表达式字段、删除高度相关字段 |
| 修改 | `MainApp/Languages/Strings.zh-CN.xaml` | 新增多语言键 |
| 修改 | `MainApp/Languages/Strings.en-US.xaml` | 新增多语言键 |
| 修改 | `Module/PrimModel.cs` | 注册 IRecipePoolService 依赖（如尚未注入） |

---

### Task 1: 更新 NeedleCameraCalibrationParams 数据模型

**Files:**
- Modify: `Core/Models/NeedleCameraCalibrationParams.cs`

- [ ] **Step 1: 修改数据模型，新增表达式字段，删除高度相关字段**

将 `NeedleCameraCalibrationParams.cs` 修改为：

```csharp
using System;

namespace Core.Models
{
    public class NeedleCameraCalibrationParams
    {
        public int SystemNumber { get; set; }
        public double CameraCenterX { get; set; }
        public double CameraCenterY { get; set; }
        public double NeedleTipX { get; set; }
        public double NeedleTipY { get; set; }
        public double NeedleTipZ { get; set; }
        public double CalibrationDeltaX { get; set; }
        public double CalibrationDeltaY { get; set; }
        public double CompensationX { get; set; }
        public double CompensationY { get; set; }
        public double CompensationZ { get; set; }
        public string CompensationXExpression { get; set; }
        public string CompensationYExpression { get; set; }
        public string CompensationZExpression { get; set; }
        public DateTime LastCalibrated { get; set; }
    }
}
```

变更说明：
- 删除 `BasePlaneZ`、`TargetPlaneZ`、`CurrentNeedleHeight` 字段
- 新增 `CompensationXExpression`、`CompensationYExpression`、`CompensationZExpression` 表达式字段

- [ ] **Step 2: 验证构建**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\Core\Core.csproj --no-restore`
Expected: Build succeeded

---

### Task 2: 更新 ViewModel — 新增表达式属性、IRecipePoolService、修改保存路径

**Files:**
- Modify: `Module/Controls/Maintenance/NeedleCameraAlignmentViewModel.cs`

- [ ] **Step 1: 新增 IRecipePoolService 依赖和表达式属性**

在 ViewModel 中：

1. 新增 `using Recipe.Interfaces;` 和 `using System.Data;` 引用
2. 新增 `_recipePoolService` 字段和构造函数参数
3. 新增表达式属性：`CompensationXExpression`、`CompensationYExpression`、`CompensationZExpression`
4. 新增计算属性：`CalculatedCompX`、`CalculatedCompY`、`CalculatedCompZ`
5. 新增 `EvaluateExpression` 私有方法（复用 PhotoPositionRow 的 DataTable.Compute 方式）
6. 新增 `NeedleCameraFileRecord` 内部类（记录最后使用的文件路径）
7. 新增 `TryAutoLoadFromRecipePoolAsync` 和 `SaveCurrentFileToRecipePoolAsync` 方法
8. 删除 `BasePlaneZ`、`TargetPlaneZ`、`CurrentNeedleHeight` 属性
9. 删除 `TeachNeedleTipZCommand`、`CalculateCurrentNeedleHeightCommand` 命令
10. 修改保存路径从 `Config/Calibration` 到 `Config/NeedleSystems`
11. 修改 `LoadCalibrationParameters` 加载表达式字段
12. 修改 `SaveCalibrationParameters` 保存表达式字段
13. 构造函数末尾调用 `TryAutoLoadFromRecipePoolAsync().ConfigureAwait(false)`

ViewModel 完整修改如下：

```csharp
using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using Recipe.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Module.ViewModels
{
    public class NeedleCameraAlignmentViewModel : BindableBase
    {
        private readonly IPositionMotionController _motionController;
        private readonly IParameterStorage _parameterStorage;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localization;
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IRecipePoolService _recipePoolService;

        private const string StationIdentifier = "Dispenser";

        private int _selectedSystemNumber = 1;
        private double _cameraCenterX;
        private double _cameraCenterY;
        private double _needleTipX;
        private double _needleTipY;
        private double _needleTipZ;
        private double _calibrationDeltaX;
        private double _calibrationDeltaY;
        private double _compensationX;
        private double _compensationY;
        private double _compensationZ;
        private string _compensationXExpression;
        private string _compensationYExpression;
        private string _compensationZExpression;
        private string _calibrationStatusMessage;
        private Brush _calibrationStatusColor = Brushes.LightGray;

        public NeedleCameraAlignmentViewModel(
            IPositionMotionController motionController,
            IParameterStorage parameterStorage,
            ILoggerService logger,
            ILocalizationService localization,
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            IRecipePoolService recipePoolService)
        {
            _motionController = motionController;
            _parameterStorage = parameterStorage;
            _logger = logger;
            _localization = localization;
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _recipePoolService = recipePoolService;

            TeachCameraCenterCommand = new DelegateCommand(ExecuteTeachCameraCenter);
            TeachNeedleTipCommand = new DelegateCommand(ExecuteTeachNeedleTip);
            SaveParametersCommand = new DelegateCommand(ExecuteSaveParameters);
            LoadParametersCommand = new DelegateCommand(ExecuteLoadParameters);
            ResetParametersCommand = new DelegateCommand(ExecuteResetParameters);

            LoadCalibrationParameters(_selectedSystemNumber);

            _ = TryAutoLoadFromRecipePoolAsync().ConfigureAwait(false);
        }

        #region 属性

        /// <summary>当前选择的系统编号（1或2）</summary>
        public int SelectedSystemNumber
        {
            get => _selectedSystemNumber;
            set
            {
                if (SetProperty(ref _selectedSystemNumber, value))
                {
                    LoadCalibrationParameters(_selectedSystemNumber);
                    UpdateStatus(
                        _localization.GetResource("NeedleCamera_Status_SystemSwitched", _selectedSystemNumber),
                        Brushes.LightBlue);
                }
            }
        }

        public double CameraCenterX
        {
            get => _cameraCenterX;
            set => SetProperty(ref _cameraCenterX, value);
        }

        public double CameraCenterY
        {
            get => _cameraCenterY;
            set => SetProperty(ref _cameraCenterY, value);
        }

        public double NeedleTipX
        {
            get => _needleTipX;
            set
            {
                SetProperty(ref _needleTipX, value);
                CalculateCalibrationDelta();
            }
        }

        public double NeedleTipY
        {
            get => _needleTipY;
            set
            {
                SetProperty(ref _needleTipY, value);
                CalculateCalibrationDelta();
            }
        }

        public double NeedleTipZ
        {
            get => _needleTipZ;
            set => SetProperty(ref _needleTipZ, value);
        }

        public double CalibrationDeltaX
        {
            get => _calibrationDeltaX;
            set => SetProperty(ref _calibrationDeltaX, value);
        }

        public double CalibrationDeltaY
        {
            get => _calibrationDeltaY;
            set => SetProperty(ref _calibrationDeltaY, value);
        }

        public double CompensationX
        {
            get => _compensationX;
            set
            {
                if (SetProperty(ref _compensationX, value))
                    RaisePropertyChanged(nameof(CalculatedCompX));
            }
        }

        public double CompensationY
        {
            get => _compensationY;
            set
            {
                if (SetProperty(ref _compensationY, value))
                    RaisePropertyChanged(nameof(CalculatedCompY));
            }
        }

        public double CompensationZ
        {
            get => _compensationZ;
            set
            {
                if (SetProperty(ref _compensationZ, value))
                    RaisePropertyChanged(nameof(CalculatedCompZ));
            }
        }

        public string CompensationXExpression
        {
            get => _compensationXExpression;
            set
            {
                if (SetProperty(ref _compensationXExpression, value))
                    RaisePropertyChanged(nameof(CalculatedCompX));
            }
        }

        public string CompensationYExpression
        {
            get => _compensationYExpression;
            set
            {
                if (SetProperty(ref _compensationYExpression, value))
                    RaisePropertyChanged(nameof(CalculatedCompY));
            }
        }

        public string CompensationZExpression
        {
            get => _compensationZExpression;
            set
            {
                if (SetProperty(ref _compensationZExpression, value))
                    RaisePropertyChanged(nameof(CalculatedCompZ));
            }
        }

        /// <summary>计算后的CompX = CompensationX + 表达式结果</summary>
        public double CalculatedCompX => CompensationX + EvaluateExpression(CompensationXExpression);

        /// <summary>计算后的CompY = CompensationY + 表达式结果</summary>
        public double CalculatedCompY => CompensationY + EvaluateExpression(CompensationYExpression);

        /// <summary>计算后的CompZ = CompensationZ + 表达式结果</summary>
        public double CalculatedCompZ => CompensationZ + EvaluateExpression(CompensationZExpression);

        public string CalibrationStatusMessage
        {
            get => _calibrationStatusMessage;
            set => SetProperty(ref _calibrationStatusMessage, value);
        }

        public Brush CalibrationStatusColor
        {
            get => _calibrationStatusColor;
            set => SetProperty(ref _calibrationStatusColor, value);
        }

        #endregion

        #region 命令

        public DelegateCommand TeachCameraCenterCommand { get; }
        public DelegateCommand TeachNeedleTipCommand { get; }
        public DelegateCommand SaveParametersCommand { get; }
        public DelegateCommand LoadParametersCommand { get; }
        public DelegateCommand ResetParametersCommand { get; }

        #endregion

        #region 命令实现

        /// <summary>示教相机中心：读取DispX和GantryY轴位置</summary>
        private async void ExecuteTeachCameraCenter()
        {
            try
            {
                var positions = await _motionController.TeachAsync(StationIdentifier);

                if (positions.TryGetValue("DispX", out double dispX))
                    CameraCenterX = dispX;
                if (positions.TryGetValue("GantryY", out double gantryY))
                    CameraCenterY = gantryY;

                CalculateCalibrationDelta();

                UpdateStatus(
                    _localization.GetResource("NeedleCamera_Status_CameraCenterTaught", CameraCenterX, CameraCenterY),
                    Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                _logger.Error($"TeachCameraCenter异常: {ex.Message}");
                UpdateStatus(
                    _localization.GetResource("NeedleCamera_Status_TeachFailed", ex.Message),
                    Brushes.Red);
            }
        }

        /// <summary>示教针尖位置：读取DispX、GantryY和DispZ轴位置</summary>
        private async void ExecuteTeachNeedleTip()
        {
            try
            {
                var positions = await _motionController.TeachAsync(StationIdentifier);

                if (positions.TryGetValue("DispX", out double dispX))
                    NeedleTipX = dispX;
                if (positions.TryGetValue("GantryY", out double gantryY))
                    NeedleTipY = gantryY;
                if (positions.TryGetValue("DispZ", out double dispZ))
                    NeedleTipZ = dispZ;

                CalculateCalibrationDelta();

                UpdateStatus(
                    _localization.GetResource("NeedleCamera_Status_NeedleTipTaught", NeedleTipX, NeedleTipY, NeedleTipZ),
                    Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                _logger.Error($"TeachNeedleTip异常: {ex.Message}");
                UpdateStatus(
                    _localization.GetResource("NeedleCamera_Status_TeachFailed", ex.Message),
                    Brushes.Red);
            }
        }

        private void ExecuteSaveParameters()
        {
            SaveCalibrationParameters(_selectedSystemNumber);
            _ = SaveCurrentFileToRecipePoolAsync().ConfigureAwait(false);
        }

        private void ExecuteLoadParameters()
        {
            LoadCalibrationParameters(_selectedSystemNumber);
        }

        private void ExecuteResetParameters()
        {
            CameraCenterX = 0;
            CameraCenterY = 0;
            NeedleTipX = 0;
            NeedleTipY = 0;
            NeedleTipZ = 0;
            CalibrationDeltaX = 0;
            CalibrationDeltaY = 0;
            CompensationX = 0;
            CompensationY = 0;
            CompensationZ = 0;
            CompensationXExpression = null;
            CompensationYExpression = null;
            CompensationZExpression = null;

            UpdateStatus(
                _localization.GetResource("NeedleCamera_Status_ParametersReset"),
                Brushes.LightGreen);
            _logger.Info($"系统{_selectedSystemNumber}参数已重置");
        }

        #endregion

        #region 私有方法

        /// <summary>计算相机与针尖的校准差值</summary>
        private void CalculateCalibrationDelta()
        {
            try
            {
                if (Math.Abs(CameraCenterX) > 0.001 || Math.Abs(CameraCenterY) > 0.001 ||
                    Math.Abs(NeedleTipX) > 0.001 || Math.Abs(NeedleTipY) > 0.001)
                {
                    CalibrationDeltaX = NeedleTipX - CameraCenterX;
                    CalibrationDeltaY = NeedleTipY - CameraCenterY;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"CalculateCalibrationDelta异常: {ex.Message}");
            }
        }

        /// <summary>安全计算数学表达式，如 "0.1+0.2+0.3"，失败返回0</summary>
        private static double EvaluateExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return 0;
            try
            {
                var result = new DataTable().Compute(expression, null);
                return Convert.ToDouble(result);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>更新状态栏信息</summary>
        private void UpdateStatus(string message, Brush color)
        {
            CalibrationStatusMessage = message;
            CalibrationStatusColor = color;
        }

        /// <summary>获取保存目录：Config/NeedleSystems</summary>
        private static string GetNeedleSystemsDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "NeedleSystems");
        }

        /// <summary>从存储加载标定参数</summary>
        private void LoadCalibrationParameters(int systemNumber)
        {
            try
            {
                string fileKey = systemNumber == 1
                    ? "NeedleCalibration_System1"
                    : "NeedleCalibration_System2";

                var parameters = _parameterStorage?.Load<NeedleCameraCalibrationParams>(fileKey, GetNeedleSystemsDirectory());

                if (parameters != null)
                {
                    CameraCenterX = parameters.CameraCenterX;
                    CameraCenterY = parameters.CameraCenterY;
                    NeedleTipX = parameters.NeedleTipX;
                    NeedleTipY = parameters.NeedleTipY;
                    NeedleTipZ = parameters.NeedleTipZ;
                    CalibrationDeltaX = parameters.CalibrationDeltaX;
                    CalibrationDeltaY = parameters.CalibrationDeltaY;
                    CompensationX = parameters.CompensationX;
                    CompensationY = parameters.CompensationY;
                    CompensationZ = parameters.CompensationZ;
                    CompensationXExpression = parameters.CompensationXExpression;
                    CompensationYExpression = parameters.CompensationYExpression;
                    CompensationZExpression = parameters.CompensationZExpression;

                    UpdateStatus(
                        _localization.GetResource("NeedleCamera_Status_LoadSuccess"),
                        Brushes.LightGreen);
                    _logger.Info($"系统{systemNumber}参数从文件加载成功");
                }
                else
                {
                    UpdateStatus(
                        _localization.GetResource("NeedleCamera_Status_NoSavedParams"),
                        Brushes.Orange);
                    _logger.Warn($"系统{systemNumber}参数文件不存在");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus(
                    _localization.GetResource("NeedleCamera_Status_LoadFailed", ex.Message),
                    Brushes.Red);
                _logger.Error($"LoadCalibrationParameters异常: {ex.Message}");
            }
        }

        /// <summary>保存标定参数到 Config/NeedleSystems 目录</summary>
        private void SaveCalibrationParameters(int systemNumber)
        {
            try
            {
                var parameters = new NeedleCameraCalibrationParams
                {
                    CameraCenterX = CameraCenterX,
                    CameraCenterY = CameraCenterY,
                    NeedleTipX = NeedleTipX,
                    NeedleTipY = NeedleTipY,
                    NeedleTipZ = NeedleTipZ,
                    CalibrationDeltaX = CalibrationDeltaX,
                    CalibrationDeltaY = CalibrationDeltaY,
                    CompensationX = CompensationX,
                    CompensationY = CompensationY,
                    CompensationZ = CompensationZ,
                    CompensationXExpression = CompensationXExpression,
                    CompensationYExpression = CompensationYExpression,
                    CompensationZExpression = CompensationZExpression,
                    LastCalibrated = DateTime.Now,
                    SystemNumber = systemNumber
                };

                string fileKey = systemNumber == 1
                    ? "NeedleCalibration_System1"
                    : "NeedleCalibration_System2";

                _parameterStorage?.Save(fileKey, parameters, GetNeedleSystemsDirectory());

                UpdateStatus(
                    _localization.GetResource("NeedleCamera_Status_SaveSuccess"),
                    Brushes.LightGreen);
                _logger.Info($"系统{systemNumber}参数保存到Config/NeedleSystems");
            }
            catch (Exception ex)
            {
                UpdateStatus(
                    _localization.GetResource("NeedleCamera_Status_SaveFailed", ex.Message),
                    Brushes.Red);
                _logger.Error($"SaveCalibrationParameters异常: {ex.Message}");
            }
        }

        /// <summary>从配方池自动加载上次使用的参数文件</summary>
        private async Task TryAutoLoadFromRecipePoolAsync()
        {
            try
            {
                var poolName = _recipePoolService?.CurrentPoolName ?? "Default";
                var extData = await _recipePoolService.GetExtensionDataAsync<NeedleCameraFileRecord>(
                    poolName, $"NeedleCamera_CurrentFile_System{_selectedSystemNumber}");

                if (extData?.FilePath != null && File.Exists(extData.FilePath))
                {
                    _logger.Info($"[NeedleCamera] 从配方池记录加载: {extData.FilePath}");
                    LoadCalibrationParameters(_selectedSystemNumber);
                    return;
                }

                var defaultDir = GetNeedleSystemsDirectory();
                var defaultPath = Path.Combine(defaultDir, $"NeedleCalibration_System{_selectedSystemNumber}.json");
                if (File.Exists(defaultPath))
                {
                    _logger.Info($"[NeedleCamera] 配方池无记录，从默认路径加载: {defaultPath}");
                    LoadCalibrationParameters(_selectedSystemNumber);
                    return;
                }

                _logger.Info("[NeedleCamera] 无可加载的参数文件");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleCamera] 自动加载参数失败: {ex.Message}");
            }
        }

        /// <summary>将当前文件路径保存到配方池ExtensionData</summary>
        private async Task SaveCurrentFileToRecipePoolAsync()
        {
            try
            {
                var poolName = _recipePoolService?.CurrentPoolName ?? "Default";
                var filePath = Path.Combine(GetNeedleSystemsDirectory(), $"NeedleCalibration_System{_selectedSystemNumber}.json");
                await _recipePoolService.SetExtensionDataAsync(poolName,
                    $"NeedleCamera_CurrentFile_System{_selectedSystemNumber}",
                    new NeedleCameraFileRecord { FilePath = filePath });
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleCamera] 保存文件记录到配方池失败: {ex.Message}");
            }
        }

        #endregion
    }

    /// <summary>记录最后使用的参数文件路径</summary>
    public class NeedleCameraFileRecord
    {
        public string FilePath { get; set; }
    }
}
```

- [ ] **Step 2: 验证构建**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\Module\Module.csproj --no-restore`
Expected: Build succeeded

---

### Task 3: 新增多语言资源键

**Files:**
- Modify: `MainApp/Languages/Strings.zh-CN.xaml`
- Modify: `MainApp/Languages/Strings.en-US.xaml`

- [ ] **Step 1: 在 zh-CN.xaml 中新增键**

在 `NeedleCamera_CompensationZ` 行之后添加：

```xml
    <sys:String x:Key="NeedleCamera_CompensationExpression">表达式</sys:String>
    <sys:String x:Key="NeedleCamera_CalculatedResult">计算结果</sys:String>
    <sys:String x:Key="NeedleCamera_OffsetCompensation">偏移补偿</sys:String>
```

- [ ] **Step 2: 在 en-US.xaml 中新增键**

在 `NeedleCamera_CompensationZ` 行之后添加：

```xml
    <sys:String x:Key="NeedleCamera_CompensationExpression">Expression</sys:String>
    <sys:String x:Key="NeedleCamera_CalculatedResult">Calculated</sys:String>
    <sys:String x:Key="NeedleCamera_OffsetCompensation">Offset Compensation</sys:String>
```

---

### Task 4: 重构 NeedleCameraAlignmentView.xaml 布局

**Files:**
- Modify: `Module/Controls/Maintenance/NeedleCameraAlignmentView.xaml`

- [ ] **Step 1: 重写整个 XAML 布局**

核心变更：
1. **删除高度设置卡片**（BasePlaneZ、TargetPlaneZ、NeedleTipZ 示教、CurrentNeedleHeight、CalculateHeight 按钮）
2. **补偿设置改为表达式编辑器**：参照 VisionCaptureView 的三列模式（基础值 | 表达式 | 计算结果）
3. **状态栏移至底部**：从右侧列移到页面底部，使用固定高度避免被切断
4. **布局从三列改为两列**：左侧（系统选择 + 示教操作 + 参数操作），右侧（当前参数 + 补偿编辑器）
5. **参数操作按钮移到左侧底部**

完整 XAML：

```xml
<UserControl x:Class="Module.Views.NeedleCameraAlignmentView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes"
             xmlns:prism="http://prismlibrary.com/"
             xmlns:converters="clr-namespace:Module.Converters"
             xmlns:lang="clr-namespace:Core.Markup;assembly=Core"
             prism:ViewModelLocator.AutoWireViewModel="True"
             mc:Ignorable="d"
             d:DesignHeight="700"
             d:DesignWidth="1100"
             MinWidth="1100"
             MinHeight="700"
             Background="#EEF2F5">
    <UserControl.Resources>
        <ResourceDictionary>
            <converters:IntToBoolConverter x:Key="IntToBoolConverter" />

            <Style x:Key="ParamRowStyle" TargetType="Grid">
                <Setter Property="Margin" Value="0,0,0,6" />
            </Style>

            <Style x:Key="ParamLabelStyle" TargetType="TextBlock">
                <Setter Property="FontSize" Value="12" />
                <Setter Property="Foreground" Value="#616161" />
                <Setter Property="VerticalAlignment" Value="Center" />
                <Setter Property="Margin" Value="0,0,6,0" />
            </Style>

            <Style x:Key="ReadOnlyTextBoxStyle" TargetType="TextBox">
                <Setter Property="IsReadOnly" Value="True" />
                <Setter Property="Background" Value="#F5F5F5" />
                <Setter Property="FontSize" Value="12" />
                <Setter Property="materialDesign:HintAssist.IsFloating" Value="False" />
                <Setter Property="Margin" Value="0,0,4,0" />
            </Style>

            <Style x:Key="EditableTextBoxStyle" TargetType="TextBox">
                <Setter Property="FontSize" Value="12" />
                <Setter Property="materialDesign:HintAssist.IsFloating" Value="True" />
                <Setter Property="Margin" Value="0,0,4,0" />
            </Style>

            <Style x:Key="CardHeaderStyle" TargetType="StackPanel">
                <Setter Property="Orientation" Value="Horizontal" />
                <Setter Property="Margin" Value="0,0,0,12" />
            </Style>

            <Style x:Key="StepLabelStyle" TargetType="TextBlock">
                <Setter Property="FontSize" Value="11" />
                <Setter Property="FontWeight" Value="SemiBold" />
                <Setter Property="Foreground" Value="#9E9E9E" />
                <Setter Property="Margin" Value="0,0,0,8" />
            </Style>
        </ResourceDictionary>
    </UserControl.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <Grid Grid.Row="0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="280" MinWidth="260" />
                <ColumnDefinition Width="*" MinWidth="500" />
            </Grid.ColumnDefinitions>

            <!-- ============================================================ -->
            <!-- LEFT COLUMN: 系统选择 + 示教操作 + 参数操作                     -->
            <!-- ============================================================ -->
            <ScrollViewer Grid.Column="0" VerticalScrollBarVisibility="Auto" Margin="0,0,10,0">
                <StackPanel>

                    <!-- 系统选择卡片 -->
                    <materialDesign:Card UniformCornerRadius="8" Padding="16" Margin="0,0,0,10"
                                         Background="{DynamicResource MaterialDesignCardBackground}">
                        <StackPanel>
                            <StackPanel Style="{StaticResource CardHeaderStyle}">
                                <materialDesign:PackIcon Kind="SwapHorizontal" Width="18" Height="18"
                                                         Foreground="{DynamicResource PrimaryHueMidBrush}"
                                                         Margin="0,0,8,0" VerticalAlignment="Center" />
                                <TextBlock Text="{lang:Lang NeedleCamera_SystemSelection}"
                                           Style="{StaticResource MaterialDesignHeadline6TextBlock}"
                                           Foreground="{DynamicResource PrimaryHueMidBrush}" />
                            </StackPanel>

                            <StackPanel Orientation="Horizontal" Margin="0,0,0,8">
                                <RadioButton Content="{lang:Lang NeedleCamera_System1}"
                                             IsChecked="{Binding SelectedSystemNumber, Converter={StaticResource IntToBoolConverter}, ConverterParameter=1}"
                                             Style="{StaticResource MaterialDesignRadioButton}"
                                             Margin="0,0,16,0" />
                                <RadioButton Content="{lang:Lang NeedleCamera_System2}"
                                             IsChecked="{Binding SelectedSystemNumber, Converter={StaticResource IntToBoolConverter}, ConverterParameter=2}"
                                             Style="{StaticResource MaterialDesignRadioButton}" />
                            </StackPanel>

                            <Border Background="#E3F2FD" CornerRadius="6" Padding="10,6">
                                <StackPanel Orientation="Horizontal">
                                    <materialDesign:PackIcon Kind="CheckCircle" Width="16" Height="16"
                                                             Foreground="#1565C0" Margin="0,0,8,0"
                                                             VerticalAlignment="Center" />
                                    <TextBlock Text="{lang:Lang NeedleCamera_CurrentSystem}"
                                               Foreground="#1565C0" FontSize="12" FontWeight="Medium"
                                               VerticalAlignment="Center" Margin="0,0,4,0" />
                                    <TextBlock Text="{Binding SelectedSystemNumber}"
                                               Foreground="#1565C0" FontSize="12" FontWeight="Medium"
                                               VerticalAlignment="Center" />
                                </StackPanel>
                            </Border>
                        </StackPanel>
                    </materialDesign:Card>

                    <!-- 相机中心示教卡片 -->
                    <materialDesign:Card UniformCornerRadius="8" Padding="16" Margin="0,0,0,10"
                                         Background="{DynamicResource MaterialDesignCardBackground}">
                        <StackPanel>
                            <StackPanel Style="{StaticResource CardHeaderStyle}">
                                <materialDesign:PackIcon Kind="CrosshairsGps" Width="18" Height="18"
                                                         Foreground="{DynamicResource PrimaryHueMidBrush}"
                                                         Margin="0,0,8,0" VerticalAlignment="Center" />
                                <TextBlock Text="{lang:Lang NeedleCamera_CameraCenterTeaching}"
                                           Style="{StaticResource MaterialDesignHeadline6TextBlock}"
                                           Foreground="{DynamicResource PrimaryHueMidBrush}" />
                            </StackPanel>

                            <TextBlock Text="{lang:Lang NeedleCamera_CameraCenterStep}"
                                       Style="{StaticResource StepLabelStyle}" />

                            <Grid Style="{StaticResource ParamRowStyle}">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>
                                <TextBlock Grid.Column="0" Text="{lang:Lang NeedleCamera_DispX}" Style="{StaticResource ParamLabelStyle}"
                                           FontWeight="SemiBold" Foreground="#E53935" />
                                <TextBlock Grid.Column="1" Text="{lang:Lang NeedleCamera_DispY}" Style="{StaticResource ParamLabelStyle}"
                                           FontWeight="SemiBold" Foreground="#43A047" />
                                <TextBox Grid.Column="2" Text="{Binding CameraCenterX, StringFormat=F3}"
                                         Style="{StaticResource ReadOnlyTextBoxStyle}" />
                                <TextBox Grid.Column="3" Text="{Binding CameraCenterY, StringFormat=F3}"
                                         Style="{StaticResource ReadOnlyTextBoxStyle}" />
                            </Grid>

                            <Button Command="{Binding TeachCameraCenterCommand}"
                                    Style="{StaticResource MaterialDesignRaisedButton}"
                                    HorizontalAlignment="Stretch"
                                    Padding="12,8"
                                    materialDesign:ButtonAssist.CornerRadius="6"
                                    ToolTip="{lang:Lang NeedleCamera_TeachCameraCenter}">
                                <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
                                    <materialDesign:PackIcon Kind="CrosshairsGps" Width="17" Height="17" Margin="0,0,8,0" />
                                    <TextBlock Text="{lang:Lang NeedleCamera_TeachCameraCenter}" FontWeight="SemiBold" />
                                </StackPanel>
                            </Button>
                        </StackPanel>
                    </materialDesign:Card>

                    <!-- 针尖示教卡片 -->
                    <materialDesign:Card UniformCornerRadius="8" Padding="16" Margin="0,0,0,10"
                                         Background="{DynamicResource MaterialDesignCardBackground}">
                        <StackPanel>
                            <StackPanel Style="{StaticResource CardHeaderStyle}">
                                <materialDesign:PackIcon Kind="Needle" Width="18" Height="18"
                                                         Foreground="{DynamicResource PrimaryHueMidBrush}"
                                                         Margin="0,0,8,0" VerticalAlignment="Center" />
                                <TextBlock Text="{lang:Lang NeedleCamera_NeedleTipTeaching}"
                                           Style="{StaticResource MaterialDesignHeadline6TextBlock}"
                                           Foreground="{DynamicResource PrimaryHueMidBrush}" />
                            </StackPanel>

                            <TextBlock Text="{lang:Lang NeedleCamera_NeedleTipStep}"
                                       Style="{StaticResource StepLabelStyle}" />

                            <Grid Style="{StaticResource ParamRowStyle}">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>
                                <TextBlock Grid.Column="0" Text="{lang:Lang NeedleCamera_DispX}" Style="{StaticResource ParamLabelStyle}"
                                           FontWeight="SemiBold" Foreground="#E53935" />
                                <TextBlock Grid.Column="1" Text="{lang:Lang NeedleCamera_DispY}" Style="{StaticResource ParamLabelStyle}"
                                           FontWeight="SemiBold" Foreground="#43A047" />
                                <TextBlock Grid.Column="2" Text="{lang:Lang NeedleCamera_DispZ}" Style="{StaticResource ParamLabelStyle}"
                                           FontWeight="SemiBold" Foreground="#1E88E5" />
                                <TextBox Grid.Column="3" Text="{Binding NeedleTipX, StringFormat=F3}"
                                         Style="{StaticResource ReadOnlyTextBoxStyle}" />
                                <TextBox Grid.Column="4" Text="{Binding NeedleTipY, StringFormat=F3}"
                                         Style="{StaticResource ReadOnlyTextBoxStyle}" />
                                <TextBox Grid.Column="5" Text="{Binding NeedleTipZ, StringFormat=F3}"
                                         Style="{StaticResource ReadOnlyTextBoxStyle}" />
                            </Grid>

                            <Button Command="{Binding TeachNeedleTipCommand}"
                                    Style="{StaticResource MaterialDesignRaisedButton}"
                                    HorizontalAlignment="Stretch"
                                    Padding="12,8"
                                    materialDesign:ButtonAssist.CornerRadius="6"
                                    ToolTip="{lang:Lang NeedleCamera_TeachNeedlePosition}">
                                <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
                                    <materialDesign:PackIcon Kind="Needle" Width="17" Height="17" Margin="0,0,8,0" />
                                    <TextBlock Text="{lang:Lang NeedleCamera_TeachNeedlePosition}" FontWeight="SemiBold" />
                                </StackPanel>
                            </Button>
                        </StackPanel>
                    </materialDesign:Card>

                    <!-- 参数操作卡片 -->
                    <materialDesign:Card UniformCornerRadius="8" Padding="16" Margin="0,0,0,0"
                                         Background="{DynamicResource MaterialDesignCardBackground}">
                        <StackPanel>
                            <StackPanel Style="{StaticResource CardHeaderStyle}">
                                <materialDesign:PackIcon Kind="ContentSave" Width="18" Height="18"
                                                         Foreground="{DynamicResource PrimaryHueMidBrush}"
                                                         Margin="0,0,8,0" VerticalAlignment="Center" />
                                <TextBlock Text="{lang:Lang NeedleCamera_ParameterOperations}"
                                           Style="{StaticResource MaterialDesignHeadline6TextBlock}"
                                           Foreground="{DynamicResource PrimaryHueMidBrush}" />
                            </StackPanel>

                            <Button Command="{Binding LoadParametersCommand}"
                                    Style="{StaticResource MaterialDesignOutlinedButton}"
                                    HorizontalAlignment="Stretch"
                                    Padding="10,6" Margin="0,0,0,8"
                                    materialDesign:ButtonAssist.CornerRadius="4"
                                    ToolTip="{lang:Lang NeedleCamera_LoadParameters}">
                                <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
                                    <materialDesign:PackIcon Kind="FolderOpen" Width="16" Height="16" Margin="0,0,8,0" />
                                    <TextBlock Text="{lang:Lang NeedleCamera_LoadParameters}" />
                                </StackPanel>
                            </Button>

                            <Button Command="{Binding SaveParametersCommand}"
                                    Style="{StaticResource MaterialDesignOutlinedButton}"
                                    HorizontalAlignment="Stretch"
                                    Padding="10,6" Margin="0,0,0,8"
                                    materialDesign:ButtonAssist.CornerRadius="4"
                                    ToolTip="{lang:Lang NeedleCamera_SaveParameters}">
                                <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
                                    <materialDesign:PackIcon Kind="ContentSave" Width="16" Height="16" Margin="0,0,8,0" />
                                    <TextBlock Text="{lang:Lang NeedleCamera_SaveParameters}" />
                                </StackPanel>
                            </Button>

                            <Button Command="{Binding ResetParametersCommand}"
                                    Style="{StaticResource MaterialDesignOutlinedButton}"
                                    HorizontalAlignment="Stretch"
                                    Padding="10,6"
                                    BorderBrush="#FF9800" Foreground="#E65100"
                                    materialDesign:ButtonAssist.CornerRadius="4"
                                    ToolTip="{lang:Lang NeedleCamera_ResetParameters}">
                                <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
                                    <materialDesign:PackIcon Kind="Refresh" Width="16" Height="16" Margin="0,0,8,0" />
                                    <TextBlock Text="{lang:Lang NeedleCamera_ResetParameters}" />
                                </StackPanel>
                            </Button>
                        </StackPanel>
                    </materialDesign:Card>

                </StackPanel>
            </ScrollViewer>

            <!-- ============================================================ -->
            <!-- RIGHT COLUMN: 当前参数 + 补偿编辑器                            -->
            <!-- ============================================================ -->
            <ScrollViewer Grid.Column="1" VerticalScrollBarVisibility="Auto">
                <StackPanel>

                    <!-- 当前参数显示卡片 -->
                    <materialDesign:Card UniformCornerRadius="8" Padding="16" Margin="0,0,0,10"
                                         Background="{DynamicResource MaterialDesignCardBackground}">
                        <StackPanel>
                            <StackPanel Style="{StaticResource CardHeaderStyle}">
                                <materialDesign:PackIcon Kind="ClipboardText" Width="18" Height="18"
                                                         Foreground="{DynamicResource PrimaryHueMidBrush}"
                                                         Margin="0,0,8,0" VerticalAlignment="Center" />
                                <TextBlock Text="{lang:Lang NeedleCamera_CurrentParameters}"
                                           Style="{StaticResource MaterialDesignHeadline6TextBlock}"
                                           Foreground="{DynamicResource PrimaryHueMidBrush}" />
                            </StackPanel>

                            <!-- CameraCenter -->
                            <TextBlock Text="{lang:Lang NeedleCamera_CameraCenter}" Style="{StaticResource StepLabelStyle}" />
                            <Grid Style="{StaticResource ParamRowStyle}">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>
                                <materialDesign:PackIcon Grid.Column="0" Kind="AxisXArrow" Width="16" Height="16"
                                                         Foreground="#E53935" VerticalAlignment="Center" Margin="0,0,4,0" />
                                <TextBox Grid.Column="1" Text="{Binding CameraCenterX, StringFormat=F3}"
                                         Style="{StaticResource ReadOnlyTextBoxStyle}" />
                            </Grid>
                            <Grid Style="{StaticResource ParamRowStyle}">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>
                                <materialDesign:PackIcon Grid.Column="0" Kind="AxisYArrow" Width="16" Height="16"
                                                         Foreground="#43A047" VerticalAlignment="Center" Margin="0,0,4,0" />
                                <TextBox Grid.Column="1" Text="{Binding CameraCenterY, StringFormat=F3}"
                                         Style="{StaticResource ReadOnlyTextBoxStyle}" />
                            </Grid>

                            <Rectangle Height="1" Fill="#E0E0E0" Margin="0,4,0,8" />

                            <!-- NeedleTip -->
                            <TextBlock Text="{lang:Lang NeedleCamera_NeedleTip}" Style="{StaticResource StepLabelStyle}" />
                            <Grid Style="{StaticResource ParamRowStyle}">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>
                                <materialDesign:PackIcon Grid.Column="0" Kind="AxisXArrow" Width="16" Height="16"
                                                         Foreground="#E53935" VerticalAlignment="Center" Margin="0,0,4,0" />
                                <TextBox Grid.Column="1" Text="{Binding NeedleTipX, StringFormat=F3}"
                                         Style="{StaticResource ReadOnlyTextBoxStyle}" />
                            </Grid>
                            <Grid Style="{StaticResource ParamRowStyle}">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>
                                <materialDesign:PackIcon Grid.Column="0" Kind="AxisYArrow" Width="16" Height="16"
                                                         Foreground="#43A047" VerticalAlignment="Center" Margin="0,0,4,0" />
                                <TextBox Grid.Column="1" Text="{Binding NeedleTipY, StringFormat=F3}"
                                         Style="{StaticResource ReadOnlyTextBoxStyle}" />
                            </Grid>
                            <Grid Style="{StaticResource ParamRowStyle}">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>
                                <materialDesign:PackIcon Grid.Column="0" Kind="AxisZArrow" Width="16" Height="16"
                                                         Foreground="#1E88E5" VerticalAlignment="Center" Margin="0,0,4,0" />
                                <TextBox Grid.Column="1" Text="{Binding NeedleTipZ, StringFormat=F3}"
                                         Style="{StaticResource ReadOnlyTextBoxStyle}" />
                            </Grid>

                            <Rectangle Height="1" Fill="#E0E0E0" Margin="0,4,0,8" />

                            <!-- Delta -->
                            <TextBlock Text="{lang:Lang NeedleCamera_Delta}" Style="{StaticResource StepLabelStyle}" />
                            <Grid Style="{StaticResource ParamRowStyle}">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>
                                <materialDesign:PackIcon Grid.Column="0" Kind="Delta" Width="16" Height="16"
                                                         Foreground="#E53935" VerticalAlignment="Center" Margin="0,0,4,0" />
                                <TextBox Grid.Column="1" Text="{Binding CalibrationDeltaX, StringFormat=F3}"
                                         Style="{StaticResource ReadOnlyTextBoxStyle}" />
                            </Grid>
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>
                                <materialDesign:PackIcon Grid.Column="0" Kind="Delta" Width="16" Height="16"
                                                         Foreground="#43A047" VerticalAlignment="Center" Margin="0,0,4,0" />
                                <TextBox Grid.Column="1" Text="{Binding CalibrationDeltaY, StringFormat=F3}"
                                         Style="{StaticResource ReadOnlyTextBoxStyle}" />
                            </Grid>
                        </StackPanel>
                    </materialDesign:Card>

                    <!-- 补偿编辑器卡片（参照 VisionCaptureView 三列模式） -->
                    <materialDesign:Card UniformCornerRadius="8" Padding="16" Margin="0,0,0,0"
                                         Background="{DynamicResource MaterialDesignCardBackground}">
                        <StackPanel>
                            <StackPanel Style="{StaticResource CardHeaderStyle}">
                                <materialDesign:PackIcon Kind="TuneVertical" Width="18" Height="18"
                                                         Foreground="{DynamicResource PrimaryHueMidBrush}"
                                                         Margin="0,0,8,0" VerticalAlignment="Center" />
                                <TextBlock Text="{lang:Lang NeedleCamera_OffsetCompensation}"
                                           Style="{StaticResource MaterialDesignHeadline6TextBlock}"
                                           Foreground="{DynamicResource PrimaryHueMidBrush}" />
                            </StackPanel>

                            <!-- 三列标题 -->
                            <Grid Margin="0,0,0,8">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="80" />
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="Auto" />
                                </Grid.ColumnDefinitions>
                                <TextBlock Grid.Column="0" Text="" Width="70" />
                                <TextBlock Grid.Column="1" Text="{lang:Lang NeedleCamera_CompensationValue}"
                                           FontSize="11" Foreground="#9E9E9E" HorizontalAlignment="Center" />
                                <TextBlock Grid.Column="2" Text="{lang:Lang NeedleCamera_CompensationExpression}"
                                           FontSize="11" Foreground="#9E9E9E" HorizontalAlignment="Center" />
                                <TextBlock Grid.Column="3" Text="{lang:Lang NeedleCamera_CalculatedResult}"
                                           FontSize="11" Foreground="#9E9E9E" HorizontalAlignment="Center" Width="80" />
                            </Grid>

                            <!-- CompensationX -->
                            <Grid Margin="0,0,0,6">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="80" />
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="Auto" />
                                </Grid.ColumnDefinitions>
                                <materialDesign:PackIcon Grid.Column="0" Kind="AxisXArrow" Width="16" Height="16"
                                                         Foreground="#E53935" VerticalAlignment="Center" Margin="0,0,6,0" />
                                <TextBox Grid.Column="1" Text="{Binding CompensationX, StringFormat=F3, UpdateSourceTrigger=PropertyChanged}"
                                         FontSize="11" Style="{StaticResource MaterialDesignOutlinedTextBox}"
                                         Padding="4,2" Margin="0,0,4,0" />
                                <TextBox Grid.Column="2" Text="{Binding CompensationXExpression, UpdateSourceTrigger=PropertyChanged}"
                                         FontSize="11" Style="{StaticResource MaterialDesignOutlinedTextBox}"
                                         Padding="4,2" Margin="0,0,4,0"
                                         materialDesign:HintAssist.Hint="{lang:Lang NeedleCamera_CompensationExpression}" />
                                <TextBlock Grid.Column="3" Text="{Binding CalculatedCompX, StringFormat='= {0:F3}'}"
                                           FontSize="11" Foreground="{DynamicResource PrimaryHueMidBrush}"
                                           VerticalAlignment="Center" Margin="4,0,0,0" Width="80" />
                            </Grid>

                            <!-- CompensationY -->
                            <Grid Margin="0,0,0,6">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="80" />
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="Auto" />
                                </Grid.ColumnDefinitions>
                                <materialDesign:PackIcon Grid.Column="0" Kind="AxisYArrow" Width="16" Height="16"
                                                         Foreground="#43A047" VerticalAlignment="Center" Margin="0,0,6,0" />
                                <TextBox Grid.Column="1" Text="{Binding CompensationY, StringFormat=F3, UpdateSourceTrigger=PropertyChanged}"
                                         FontSize="11" Style="{StaticResource MaterialDesignOutlinedTextBox}"
                                         Padding="4,2" Margin="0,0,4,0" />
                                <TextBox Grid.Column="2" Text="{Binding CompensationYExpression, UpdateSourceTrigger=PropertyChanged}"
                                         FontSize="11" Style="{StaticResource MaterialDesignOutlinedTextBox}"
                                         Padding="4,2" Margin="0,0,4,0"
                                         materialDesign:HintAssist.Hint="{lang:Lang NeedleCamera_CompensationExpression}" />
                                <TextBlock Grid.Column="3" Text="{Binding CalculatedCompY, StringFormat='= {0:F3}'}"
                                           FontSize="11" Foreground="{DynamicResource PrimaryHueMidBrush}"
                                           VerticalAlignment="Center" Margin="4,0,0,0" Width="80" />
                            </Grid>

                            <!-- CompensationZ -->
                            <Grid Margin="0,0,0,6">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="80" />
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="Auto" />
                                </Grid.ColumnDefinitions>
                                <materialDesign:PackIcon Grid.Column="0" Kind="AxisZArrow" Width="16" Height="16"
                                                         Foreground="#1E88E5" VerticalAlignment="Center" Margin="0,0,6,0" />
                                <TextBox Grid.Column="1" Text="{Binding CompensationZ, StringFormat=F3, UpdateSourceTrigger=PropertyChanged}"
                                         FontSize="11" Style="{StaticResource MaterialDesignOutlinedTextBox}"
                                         Padding="4,2" Margin="0,0,4,0" />
                                <TextBox Grid.Column="2" Text="{Binding CompensationZExpression, UpdateSourceTrigger=PropertyChanged}"
                                         FontSize="11" Style="{StaticResource MaterialDesignOutlinedTextBox}"
                                         Padding="4,2" Margin="0,0,4,0"
                                         materialDesign:HintAssist.Hint="{lang:Lang NeedleCamera_CompensationExpression}" />
                                <TextBlock Grid.Column="3" Text="{Binding CalculatedCompZ, StringFormat='= {0:F3}'}"
                                           FontSize="11" Foreground="{DynamicResource PrimaryHueMidBrush}"
                                           VerticalAlignment="Center" Margin="4,0,0,0" Width="80" />
                            </Grid>

                            <!-- Yellow info note -->
                            <Border Background="#FFF8E1" CornerRadius="6" Padding="10,8" Margin="0,6,0,0">
                                <StackPanel Orientation="Horizontal">
                                    <materialDesign:PackIcon Kind="InformationOutline" Width="16" Height="16"
                                                             Foreground="#F9A825" Margin="0,0,8,0"
                                                             VerticalAlignment="Center" />
                                    <TextBlock Text="{lang:Lang Maintenance_CompensationNote}"
                                               Foreground="#795548" FontSize="12"
                                               FontStyle="Italic" VerticalAlignment="Center"
                                               TextWrapping="Wrap" />
                                </StackPanel>
                            </Border>
                        </StackPanel>
                    </materialDesign:Card>

                </StackPanel>
            </ScrollViewer>
        </Grid>

        <!-- ============================================================ -->
        <!-- BOTTOM: 状态栏（固定底部，避免信息被切断）                        -->
        <!-- ============================================================ -->
        <Border Grid.Row="1" Background="{Binding CalibrationStatusColor}"
                CornerRadius="0,0,0,0" Padding="16,10" Margin="0,6,0,0">
            <StackPanel Orientation="Horizontal">
                <materialDesign:PackIcon Kind="CheckCircleOutline" Width="18" Height="18"
                                         Foreground="#333333" Margin="0,0,8,0"
                                         VerticalAlignment="Center" />
                <TextBlock Text="{Binding CalibrationStatusMessage}"
                           Foreground="#333333" FontWeight="SemiBold"
                           FontSize="13" VerticalAlignment="Center"
                           TextWrapping="Wrap" />
            </StackPanel>
        </Border>
    </Grid>
</UserControl>
```

- [ ] **Step 2: 验证构建**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\Module\Module.csproj --no-restore`
Expected: Build succeeded

---

### Task 5: 全量构建验证

**Files:**
- None

- [ ] **Step 1: 执行全量构建**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\MainApp\MainApp.csproj`
Expected: Build succeeded, 0 errors

- [ ] **Step 2: 检查是否有遗漏的编译错误**

如果构建失败，根据错误信息修复并重新构建，直到 0 errors。

---

## Self-Review Checklist

1. **Spec coverage:**
   - ✅ 参照 VisionCaptureView 补偿编辑器模式 → Task 4（三列补偿编辑器）
   - ✅ 删除高度设置 → Task 4（XAML 删除高度卡片）、Task 2（ViewModel 删除高度属性/命令）
   - ✅ 状态显示移至底部 → Task 4（底部固定 Border）
   - ✅ 保存到 Config/NeedleSystems → Task 2（GetNeedleSystemsDirectory 方法）
   - ✅ 最后使用的文件保存在配方池，初始化自动加载 → Task 2（TryAutoLoadFromRecipePoolAsync + SaveCurrentFileToRecipePoolAsync）

2. **Placeholder scan:** 无 TBD/TODO/占位符

3. **Type consistency:**
   - `NeedleCameraCalibrationParams` 新增的 3 个 Expression 字段与 ViewModel 属性类型一致（string）
   - `CalculatedCompX/Y/Z` 返回 double，与 XAML 中 `StringFormat='= {0:F3}'` 一致
   - `NeedleCameraFileRecord` 类定义在 ViewModel.cs 底部，与 VisionCaptureView 的 `VisionCaptureFileRecord` 模式一致
   - `EvaluateExpression` 方法签名与 PhotoPositionRow 中一致
