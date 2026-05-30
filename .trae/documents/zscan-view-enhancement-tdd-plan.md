# ZScanView 功能完善 - TDD 实施计划

## 需求概述

1. **相机Z高度与点胶针头标定功能**（含换针补偿）
2. **Z相实际值链接全局变量**（显示链接图标）
3. **3D相机返回圆弧数据（多点）的处理方案**：读取真实产品圆弧高度，补偿CAD圆弧高度公差
4. **JSON持久化**：默认路径 `Debug\net9.0-windows7.0\Config\`，支持新建/删除表格，单JSON文件保存与加载
5. **数据格式指定**：表格可指定返回数据格式（double/double[]），对应不同的3D返回值
6. **Step3EditParamsPanel CAD参考Z值提取**
7. **3D相机返回已是提取后轨迹**：无需滤波/拟合，直接差值补偿

---

## 架构分析

### 现有代码结构
- `ZScanView.xaml` → 容器页面，嵌入 `ZScanDetailView`
- `ZScanDetailView.xaml` → 主功能视图（左侧图片+右侧表格+运动控制）
- `ZScanDetailViewModel.cs` → 核心ViewModel（运动控制、TCP通讯、数据解析、自动计算引擎）
- `ZScanPointDetail` → 逐点测量数据模型
- `ZScanSummaryItem` → 汇总行数据模型
- `GlobalVariable` → 全局变量模型（支持 Double/DoubleArray 等类型）
- `NeedleCompensationManager` → 针头补偿管理器（清零法：基准补偿 - 当前偏差）
- `NeedleCalibrationParams` → 针头校准参数（搜索点、基准XYZ、补偿XYZ）
- `JsonConfigurationService` / `JsonParameterStorage` → JSON持久化服务
- 多语言：`LangExtension` + `Strings.zh-CN.xaml` / `Strings.en-US.xaml`
- 测试框架：xUnit + Moq（`MotionControl.Tests` 项目）

### 关键设计决策
- 遵循 WPF+PRISM+MaterialDesign 架构
- 依赖倒置：ViewModel 依赖接口，不依赖具体实现
- 运动控制逻辑需符合工业设备：快速响应性、安全性
- 3D相机返回已是提取后轨迹 → **无需滤波/拟合，直接差值补偿**
- 圆弧数据处理：3D返回多点 → 按DataIndex映射到表格行 → 直接差值补偿CAD标称值

---

## TDD 实施步骤

### Phase 1: 数据模型层（Core/Models）

#### Task 1.1: ZScan标定配置模型
**文件**: `Core/Models/ZScanCalibrationConfig.cs`（新建）

**测试先行** → `MotionControl.Tests/ZScanCalibrationConfigTests.cs`

测试用例：
- `CalibrationConfig_DefaultValues_AreCorrect` — 默认值验证
- `CalibrationConfig_NeedleOffset_AppliedCorrectly` — 换针偏移计算
- `CalibrationConfig_TotalOffset_IncludesNeedleCompensation` — 总偏移 = 相机Z偏移 + 针头补偿Z
- `CalibrationConfig_Serialization_RoundTrip` — JSON序列化/反序列化往返

模型定义：
```csharp
public class ZScanCalibrationConfig
{
    public string ConfigName { get; set; }
    public double CameraZOffset { get; set; }        // 相机Z高度偏移
    public double NeedleZOffset { get; set; }         // 针头Z偏移（换针补偿）
    public DateTime LastCalibrationTime { get; set; } // 上次标定时间
    public string Operator { get; set; }              // 操作员
    public double TotalZOffset => CameraZOffset + NeedleZOffset; // 总偏移
}
```

#### Task 1.2: ZScan表格配置模型（支持多表格+数据格式指定）
**文件**: `Core/Models/ZScanTableConfig.cs`（新建）

**测试先行** → `MotionControl.Tests/ZScanTableConfigTests.cs`

测试用例：
- `ZScanTableConfig_DefaultFormat_IsDouble` — 默认数据格式为Double
- `ZScanTableConfig_ArrayFormat_SupportsDoubleArray` — DoubleArray格式支持
- `ZScanTableConfig_GlobalVariableLink_Serialization` — 全局变量链接序列化
- `ZScanTableConfig_Collection_Serialization` — 多表格集合序列化

模型定义：
```csharp
public enum ZScanDataFormat
{
    Double,      // 单个double值
    DoubleArray  // double[]数组
}

public class ZScanGlobalVariableLink
{
    public bool IsLinked { get; set; }               // 是否已链接
    public string VariableName { get; set; }          // 全局变量名
    public GlobalVariableType VariableType { get; set; } // 变量类型
}

public class ZScanTableConfig
{
    public string TableName { get; set; }             // 表格名称
    public ZScanDataFormat DataFormat { get; set; }   // 返回数据格式
    public ZScanGlobalVariableLink ZActualLink { get; set; } // Z实测值全局变量链接
    public ObservableCollection<ZScanPointDetail> Points { get; set; }
    public ZScanCalibrationConfig Calibration { get; set; }
}

public class ZScanConfigFile
{
    public List<ZScanTableConfig> Tables { get; set; }
    public string DefaultTableName { get; set; }
}
```

#### Task 1.3: ZScanPointDetail 扩展（增加全局变量链接属性）
**文件**: `Module/Models/ZScanSummaryItem.cs`（修改）

**测试先行** → `MotionControl.Tests/ZScanPointDetailTests.cs`

测试用例：
- `ZScanPointDetail_GlobalVariableLink_DefaultNull` — 默认无链接
- `ZScanPointDetail_SetGlobalVariableLink_UpdatesIsLinked` — 设置链接后IsLinked为true
- `ZScanPointDetail_DeltaZCalculation_WithCalibrationOffset` — 带标定偏移的DeltaZ计算

扩展属性：
```csharp
// ZScanPointDetail 新增
private ZScanGlobalVariableLink _zActualLink;
public ZScanGlobalVariableLink ZActualLink { get; set; }
public bool IsZActualLinked => ZActualLink?.IsLinked == true;
```

---

### Phase 2: 持久化服务层（Core/Services）

#### Task 2.1: ZScan JSON持久化服务
**文件**: `Core/Services/ZScanConfigService.cs`（新建）

**测试先行** → `MotionControl.Tests/ZScanConfigServiceTests.cs`

测试用例：
- `SaveAndLoad_RoundTrip_PreservesAllData` — 保存后加载，数据完整
- `Load_NonExistentFile_ReturnsDefault` — 加载不存在文件返回默认值
- `Save_CreatesDirectory_IfNotExists` — 保存时自动创建目录
- `Save_MultipleTables_AllPreserved` — 多表格保存后全部保留
- `DefaultPath_IsConfigUnderBaseDirectory` — 默认路径验证

服务接口：
```csharp
public interface IZScanConfigService
{
    ZScanConfigFile Load(string fileName = "ZScanConfig.json");
    void Save(ZScanConfigFile config, string fileName = "ZScanConfig.json");
    string GetConfigPath();
}
```

实现要点：
- 默认路径：`AppDomain.CurrentDomain.BaseDirectory + "Config\ZScan\"`
- 使用 `Newtonsoft.Json`（项目已有依赖）
- `CamelCasePropertyNamesContractResolver` + `Formatting.Indented`

---

### Phase 3: 标定功能（相机Z高度与针头标定）

#### Task 3.1: 标定服务
**文件**: `Core/Services/ZScanCalibrationService.cs`（新建）

**测试先行** → `MotionControl.Tests/ZScanCalibrationServiceTests.cs`

测试用例：
- `Calibrate_WithValidData_UpdatesCameraZOffset` — 有效数据标定更新偏移
- `ApplyNeedleCompensation_AddsToOffset` — 换针补偿叠加到偏移
- `GetCompensatedZ_ReturnsMeasuredPlusTotalOffset` — 补偿后Z = 实测Z + 总偏移
- `ResetCalibration_ClearsAllOffsets` — 重置清零所有偏移
- `Calibration_WithNeedleChange_UpdatesNeedleOffset` — 换针后更新针头偏移

服务接口：
```csharp
public interface IZScanCalibrationService
{
    double CameraZOffset { get; }
    double NeedleZOffset { get; }
    double TotalZOffset { get; }
    void CalibrateCameraZ(double measuredZ, double referenceZ);
    void ApplyNeedleCompensation(double deltaZ);
    double GetCompensatedZ(double measuredZ);
    void ResetCalibration();
    event Action CalibrationChanged;
}
```

标定流程：
1. 移动到标定位置 → 3D相机测量Z值 → 与已知参考高度对比 → 计算CameraZOffset
2. 换针时：测量新旧针头高度差 → 更新NeedleZOffset
3. 补偿计算：`CompensatedZ = MeasuredZ + TotalZOffset`

#### Task 3.2: 标定UI（ZScanDetailView中增加标定卡片）
**文件**: `Module/Controls/Configuration/ZScanDetailView.xaml`（修改）

在运动控制按钮组下方增加标定卡片：
- 标定按钮（Calibrate Camera Z）
- 换针补偿输入框 + 应用按钮
- 当前偏移值显示（Camera Offset / Needle Offset / Total Offset）
- 上次标定时间

---

### Phase 4: 全局变量链接功能

#### Task 4.1: 全局变量链接服务
**文件**: `Core/Services/ZScanGlobalVariableLinkService.cs`（新建）

**测试先行** → `MotionControl.Tests/ZScanGlobalVariableLinkServiceTests.cs`

测试用例：
- `LinkToVariable_ValidName_SetsIsLinked` — 链接有效变量名后IsLinked=true
- `Unlink_SetsIsLinkedFalse` — 取消链接后IsLinked=false
- `GetLinkedValue_DoubleType_ReturnsCorrectValue` — 获取Double类型链接值
- `GetLinkedValue_DoubleArrayType_ReturnsArray` — 获取DoubleArray类型链接值
- `WriteBackValue_UpdatesGlobalVariable` — 回写值更新全局变量

服务接口：
```csharp
public interface IZScanGlobalVariableLinkService
{
    bool LinkVariable(string variableName, GlobalVariableType expectedType);
    void UnlinkVariable();
    object GetLinkedValue();
    void WriteBackValue(object value);
    bool IsLinked { get; }
    string LinkedVariableName { get; }
}
```

#### Task 4.2: Z_actual列显示链接图标
**文件**: `Module/Controls/Configuration/ZScanDetailView.xaml`（修改）

将 `Z_actual` 列从 `DataGridTextColumn` 改为 `DataGridTemplateColumn`：
- 未链接时：普通TextBlock显示数值
- 已链接时：数值 + `<materialDesign:PackIcon Kind="Link" />` 图标
- 点击图标可打开链接/取消链接对话框

```xml
<DataGridTemplateColumn Header="Z_actual" Width="90">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="{Binding ZMeasured, StringFormat=F3}" VerticalAlignment="Center"/>
                <materialDesign:PackIcon Kind="Link" Width="12" Height="12"
                    Foreground="#1565C0" VerticalAlignment="Center" Margin="2,0,0,0"
                    Visibility="{Binding IsZActualLinked, Converter={StaticResource BooleanToVisibilityConverter}}"/>
            </StackPanel>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

---

### Phase 5: 多表格管理 + 数据格式指定

#### Task 5.1: ZScanViewModel重构（支持多表格）
**文件**: `Module/Controls/Configuration/ZScanDetailViewModel.cs`（修改）

**测试先行** → `MotionControl.Tests/ZScanDetailViewModelTests.cs`

测试用例：
- `AddTable_CreatesNewTableWithDefaultName` — 新建表格
- `DeleteTable_RemovesFromCollection` — 删除表格
- `SwitchTable_UpdatesPointDetails` — 切换表格更新数据
- `SetDataFormat_Double_SetsColumnVisibility` — Double格式隐藏数组相关列
- `SetDataFormat_DoubleArray_ShowsDataIndexColumn` — DoubleArray格式显示DataIndex列

ViewModel新增属性：
```csharp
public ObservableCollection<ZScanTableConfig> Tables { get; set; }
public ZScanTableConfig SelectedTable { get; set; }
public ICommand AddTableCommand { get; }
public ICommand DeleteTableCommand { get; }
public ICommand SaveConfigCommand { get; }
public ICommand LoadConfigCommand { get; }
```

#### Task 5.2: 多表格UI
**文件**: `Module/Controls/Configuration/ZScanDetailView.xaml`（修改）

在数据表格上方增加：
- 表格选择下拉框（ComboBox绑定Tables集合）
- 新建表格按钮（`<materialDesign:PackIcon Kind="Plus" />`）
- 删除表格按钮（`<materialDesign:PackIcon Kind="DeleteOutline" />`）
- 数据格式选择下拉框（Double / DoubleArray）
- 保存/加载配置按钮

---

### Phase 6: 圆弧数据处理（3D相机返回多点）

#### Task 6.1: 圆弧数据差值补偿算法
**文件**: `Core/Services/ZScanArcCompensationService.cs`（新建）

**测试先行** → `MotionControl.Tests/ZScanArcCompensationServiceTests.cs`

测试用例：
- `Compensate_SinglePoint_ReturnsDeltaZ` — 单点补偿返回偏差
- `Compensate_ArcPoints_MapsByDataIndex` — 圆弧多点按DataIndex映射
- `Compensate_ArcPoints_AllDeltasCalculated` — 所有点偏差已计算
- `Compensate_WithCalibrationOffset_AppliedCorrectly` — 带标定偏移的补偿
- `Compensate_DoubleArray_WritesBackToGlobalVariable` — DoubleArray回写全局变量
- `Compensate_MoreMeasuredPointsThanTableRows_OnlyMapsExisting` — 测量点多于表格行时只映射已有行

核心算法：
```
3D相机返回 double[] arcHeights（已是提取后的轨迹，无需滤波/拟合）
对每个表格行 point：
  if DataFormat == Double:
    point.ZMeasured = arcHeights[0] + TotalZOffset  // 单值+标定偏移
  elif DataFormat == DoubleArray:
    point.ZMeasured = arcHeights[point.DataIndex] + TotalZOffset  // 按索引取值+标定偏移
  point.DeltaZ = point.ZMeasured - point.Nominal  // 直接差值补偿
```

设计要点：
- **无需滤波/拟合**：3D相机返回的已是提取后的轨迹
- **直接差值补偿**：`DeltaZ = ZMeasured - Nominal`，Nominal来自CAD参考Z值
- **DataIndex映射**：DoubleArray格式下，每个表格行通过DataIndex指定取数组中的哪个元素
- **全局变量回写**：如果Z_actual链接了全局变量，补偿后的值回写到对应全局变量

#### Task 6.2: Step3EditParamsPanel CAD参考Z值提取
**文件**: `Module/Controls/Cad/Step3EditParamsPanel.xaml`（修改）

在高度参数组（第三组-青色）中增加：
- "提取CAD Z值"按钮 → 从CAD轨迹段提取Z标称值
- 提取逻辑：遍历选中段的采样点，取Z值作为Nominal填入ZScan表格

---

### Phase 7: ViewModel集成 + UI完善

#### Task 7.1: ZScanDetailViewModel集成所有功能
**文件**: `Module/Controls/Configuration/ZScanDetailViewModel.cs`（修改）

集成点：
1. 注入 `IZScanConfigService`、`IZScanCalibrationService`、`IZScanGlobalVariableLinkService`
2. `UpdatePointDetailsFromCameraData` 改用 `ZScanArcCompensationService` 处理
3. 增加 `SaveConfigCommand` / `LoadConfigCommand`
4. 增加标定相关命令和属性
5. 增加全局变量链接命令

#### Task 7.2: ZScanDetailView.xaml UI完善
**文件**: `Module/Controls/Configuration/ZScanDetailView.xaml`（修改）

修改内容：
1. 增加标定卡片区域
2. Z_actual列改为TemplateColumn（含链接图标）
3. 增加多表格管理工具栏
4. 增加数据格式选择下拉框
5. 增加保存/加载按钮

#### Task 7.3: 多语言资源更新
**文件**: `MainApp/Languages/Strings.zh-CN.xaml` + `Strings.en-US.xaml`（修改）

新增键值（约30个）：
- `ZScanDetail_Calibration` / 标定 / Calibration
- `ZScanDetail_CalibrateCameraZ` / 标定相机Z高度 / Calibrate Camera Z
- `ZScanDetail_NeedleCompensation` / 换针补偿 / Needle Compensation
- `ZScanDetail_CameraOffset` / 相机偏移 / Camera Offset
- `ZScanDetail_TotalOffset` / 总偏移 / Total Offset
- `ZScanDetail_LinkGlobalVar` / 链接全局变量 / Link Global Variable
- `ZScanDetail_UnlinkGlobalVar` / 取消链接 / Unlink
- `ZScanDetail_NewTable` / 新建表格 / New Table
- `ZScanDetail_DeleteTable` / 删除表格 / Delete Table
- `ZScanDetail_DataFormat` / 数据格式 / Data Format
- `ZScanDetail_SaveConfig` / 保存配置 / Save Config
- `ZScanDetail_LoadConfig` / 加载配置 / Load Config
- `ZScanDetail_ExtractCADZ` / 提取CAD Z值 / Extract CAD Z
- 等等...

---

## 文件变更清单

### 新建文件
| 文件 | 用途 |
|------|------|
| `Core/Models/ZScanCalibrationConfig.cs` | 标定配置模型 |
| `Core/Models/ZScanTableConfig.cs` | 表格配置+数据格式+全局变量链接模型 |
| `Core/Services/ZScanConfigService.cs` | JSON持久化服务 |
| `Core/Services/ZScanCalibrationService.cs` | 标定服务 |
| `Core/Services/ZScanArcCompensationService.cs` | 圆弧差值补偿服务 |
| `Core/Services/ZScanGlobalVariableLinkService.cs` | 全局变量链接服务 |
| `Core/Interfaces/IZScanConfigService.cs` | 持久化服务接口 |
| `Core/Interfaces/IZScanCalibrationService.cs` | 标定服务接口 |
| `Core/Interfaces/IZScanArcCompensationService.cs` | 补偿服务接口 |
| `Core/Interfaces/IZScanGlobalVariableLinkService.cs` | 链接服务接口 |
| `MotionControl.Tests/ZScanCalibrationConfigTests.cs` | 标定配置测试 |
| `MotionControl.Tests/ZScanTableConfigTests.cs` | 表格配置测试 |
| `MotionControl.Tests/ZScanPointDetailTests.cs` | 点详情扩展测试 |
| `MotionControl.Tests/ZScanConfigServiceTests.cs` | 持久化服务测试 |
| `MotionControl.Tests/ZScanCalibrationServiceTests.cs` | 标定服务测试 |
| `MotionControl.Tests/ZScanArcCompensationServiceTests.cs` | 补偿算法测试 |
| `MotionControl.Tests/ZScanGlobalVariableLinkServiceTests.cs` | 链接服务测试 |
| `MotionControl.Tests/ZScanDetailViewModelTests.cs` | ViewModel测试 |

### 修改文件
| 文件 | 修改内容 |
|------|----------|
| `Module/Models/ZScanSummaryItem.cs` | ZScanPointDetail增加ZActualLink、IsZActualLinked属性 |
| `Module/Controls/Configuration/ZScanDetailViewModel.cs` | 集成标定、链接、多表格、持久化功能 |
| `Module/Controls/Configuration/ZScanDetailView.xaml` | 增加标定卡片、链接图标、多表格工具栏 |
| `Module/Controls/Cad/Step3EditParamsPanel.xaml` | 增加提取CAD Z值按钮 |
| `MainApp/Languages/Strings.zh-CN.xaml` | 新增约30个中文键值 |
| `MainApp/Languages/Strings.en-US.xaml` | 新增约30个英文键值 |

---

## 依赖注入注册

在 `FrameworkModule.cs` 或对应模块的 `RegisterTypes` 中注册：
```csharp
containerRegistry.RegisterSingleton<IZScanConfigService, ZScanConfigService>();
containerRegistry.RegisterSingleton<IZScanCalibrationService, ZScanCalibrationService>();
containerRegistry.RegisterSingleton<IZScanArcCompensationService, ZScanArcCompensationService>();
containerRegistry.Register<IZScanGlobalVariableLinkService, ZScanGlobalVariableLinkService>();
```

---

## 圆弧数据处理方案（回答疑问）

**问题**：如果3D相机返回一组圆弧数据（很多点），该如何处理？

**方案**：
1. 3D相机返回的已是提取后的轨迹数据（`double[]`），无需滤波/拟合
2. 表格配置数据格式为 `DoubleArray` 时，每个表格行通过 `DataIndex` 指定取数组中的哪个索引
3. 补偿公式：`ZMeasured[i] = arcHeights[DataIndex] + TotalZOffset`
4. 差值补偿：`DeltaZ[i] = ZMeasured[i] - Nominal[i]`，Nominal来自CAD参考Z值
5. 如果Z_actual链接了全局变量（DoubleArray类型），整个补偿后数组回写到全局变量
6. **目的达成**：读取真实产品圆弧高度 → 补偿CAD圆弧高度公差

**示例**：
```
3D返回: double[] = {5.012, 5.025, 5.051, 5.049, 5.218}  // 5个圆弧采样点
表格行:
  Pt#1: DataIndex=0, Nominal=5.000 → ZMeasured=5.012, DeltaZ=+0.012
  Pt#2: DataIndex=1, Nominal=5.010 → ZMeasured=5.025, DeltaZ=+0.015
  Pt#3: DataIndex=2, Nominal=5.020 → ZMeasured=5.051, DeltaZ=+0.031
  ...
```

---

## 执行顺序

1. Phase 1 → 数据模型（含测试）
2. Phase 2 → 持久化服务（含测试）
3. Phase 3 → 标定功能（含测试）
4. Phase 4 → 全局变量链接（含测试）
5. Phase 5 → 多表格管理（含测试）
6. Phase 6 → 圆弧数据处理（含测试）
7. Phase 7 → ViewModel集成 + UI完善 + 多语言
