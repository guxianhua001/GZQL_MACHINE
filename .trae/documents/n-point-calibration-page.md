# N点标定页面 实现计划

## 概述

在 Module 项目中新增 N点标定页面，支持 X/Y 轴选配、参数导入/导出/保存、自动加载上次配置、机械点位示教/移动、自动标定（可设延时）、TCP/IP 视觉数据接收或手动输入。页面风格参考 Module 项目 Controls 中现有校准页面（NeedleCameraAlignmentView / ProductCalibrationView）。

---

## 当前状态分析

### 已有相关能力
- **仿射标定算法**：`Core/Services/AffineCalibrationService.cs` 已实现 N点最小二乘仿射标定（>=3点）
- **标定点模型**：`Core/Models/AffineCalibrationPoint.cs`（BindableBase，含 Index/Name/CadX/CadY/MachineX/MachineY/Residual）
- **TCP 通讯**：`ITCPEventService.CameraMessageReceived` 事件驱动，`SendCommandWithResponseAsync` 发送并等待响应
- **运动控制**：`IPositionMotionController.TeachAsync/GotoAsync` 示教/走位，`IMotionService` 底层轴操作
- **配置存储**：`IParameterStorage`（JSON，默认 `Config/Parameters/`），`IFileDialogService` 文件对话框
- **UI 模式**：Card + ScrollViewer + DataGrid + PackIcon + LangExtension 多语言
- **导航注册**：`PrimModel.cs` 中 `RegisterForNavigation` + `NavigateList.Add`

### 需新增内容
- N点标定专用数据模型（配置 + 点位 + 标定结果）
- N点标定服务接口与实现（自动标定流程、TCP 数据接收解析）
- N点标定视图与 ViewModel
- 多语言资源条目
- 导航注册

---

## 实现方案

### 1. 数据模型层（Core/Models/）

#### 1.1 `NPointCalibrationConfig.cs`
标定配置模型，包含：
- `AxisConfig`：轴配置（SelectedAxes: X/Y 可选，轴名映射）
- `TcpConfig`：TCP 通讯配置（EnableVisionData, ConnectionName, TriggerCommand, DataFormat）
- `AutoCalibConfig`：自动标定配置（EnableAuto, DelayMs, PointCount）
- `LastFilePath`：上次使用的配置文件路径（用于自动加载）

```csharp
public class NPointCalibrationConfig
{
    public bool EnableAxisX { get; set; } = true;
    public bool EnableAxisY { get; set; } = true;
    public bool EnableVisionData { get; set; } = true;
    public string TcpConnectionName { get; set; } = string.Empty;
    public string TriggerCommand { get; set; } = string.Empty;
    public int AutoCalibDelayMs { get; set; } = 500;
    public int PointCount { get; set; } = 9;
    public string LastFileName { get; set; } = string.Empty;
}
```

#### 1.2 `NPointCalibrationPoint.cs`
标定点模型，继承 `BindableBase`，支持 WPF 双向绑定：
- `Index`：序号
- `Name`：点位名称（如 P1, P2...）
- `MachineX/MachineY`：机械坐标（示教填入）
- `VisionX/VisionY`：视觉坐标（TCP 接收或手动输入）
- `IsCalibrated`：是否已标定
- `StatusColor`：状态颜色（Gray/Orange/Green）

#### 1.3 `NPointCalibrationData.cs`
完整标定数据，用于序列化/反序列化：
- `Config`：NPointCalibrationConfig
- `Points`：List<NPointCalibrationPoint>
- `CalibrationResult`：仿射标定结果（6参数 + RMS误差）

---

### 2. 服务层（Core/Abstraction/ + Module/Services/）

#### 2.1 `INPointCalibrationService.cs`（Core/Abstraction/）
```csharp
public interface INPointCalibrationService
{
    // 自动标定流程
    Task StartAutoCalibrationAsync(int pointCount, int delayMs, CancellationToken ct);
    void StopAutoCalibration();
    bool IsAutoCalibrating { get; }

    // 单点标定
    Task<NPointCalibrationPoint> TeachPointAsync(int index);
    Task MoveToPointAsync(NPointCalibrationPoint point);

    // TCP 视觉数据
    void SubscribeVisionData(string connectionName);
    void UnsubscribeVisionData();

    // 仿射计算
    AffineCalibrationResult ComputeCalibration(IList<NPointCalibrationPoint> points);

    // 事件
    event Action<int, NPointCalibrationPoint> PointCalibrated;      // 单点标定完成
    event Action<NPointCalibrationPoint> VisionDataReceived;        // 视觉数据到达
    event Action<AffineCalibrationResult> CalibrationCompleted;     // 全部标定完成
    event Action<string> CalibrationError;                          // 标定错误
}
```

#### 2.2 `NPointCalibrationService.cs`（Module/Services/）
实现要点：
- 注入 `IPositionMotionController`、`ITCPEventService`、`IMotionService`、`ILoggerService`
- 自动标定流程：循环 N 个点 -> 示教当前位置 -> 发送触发命令 -> 等待视觉数据 -> 填充表格 -> 延时 -> 下一点
- TCP 数据接收：订阅 `ITCPEventService.CameraMessageReceived`，解析 JSON/分隔符格式数据
- 仿射计算：调用已有的 `AffineCalibrationService.Solve()`
- 线程安全：`CancellationTokenSource` 控制自动标定取消

---

### 3. 配置存储（使用 IParameterStorage + 自定义路径）

- 默认路径：`Config/Calibration/`
- 文件格式：JSON
- 自动加载：启动时读取 `LastFileName`，从默认路径加载
- 保存时记录 `LastFileName`（仅文件名，不含路径）
- 导入/导出：使用 `IFileDialogService` 打开文件对话框

---

### 4. 视图层（Module/Controls/Calibration/）

#### 4.1 `NPointCalibrationView.xaml`
布局参考 NeedleCameraAlignmentView 左右分栏风格：

```
┌─────────────────────────────────────────────────────────────┐
│  [PackIcon] N点标定                                          │
├──────────────────────────┬──────────────────────────────────┤
│  左侧操作面板 (ScrollViewer)  │  右侧数据表格区域              │
│                              │                                │
│  ┌─ 轴配置 Card ──────────┐  │  ┌─ 标定数据 Card ──────────┐ │
│  │ ☑ X轴  ☑ Y轴           │  │  │ DataGrid:               │ │
│  │ 点数: [9]               │  │  │ 序号|名称|机械X|机械Y|   │ │
│  └────────────────────────┘  │  │     |视觉X|视觉Y|状态|操作│ │
│                              │  │                          │ │
│  ┌─ 通讯配置 Card ─────────┐  │  │ [添加行] [删除行]        │ │
│  │ ☑ 接收视觉数据           │  │  └──────────────────────────┘ │
│  │ 连接名: [ComboBox]      │  │                                │
│  │ 触发命令: [TextBox]     │  │  ┌─ 标定结果 Card ──────────┐ │
│  └────────────────────────┘  │  │ A= B= C= D= Tx= Ty=      │ │
│                              │  │ RMS误差=  等效旋转角=      │ │
│  ┌─ 自动标定 Card ─────────┐  │  │ 质量评级: ★★★★☆          │ │
│  │ 延时(ms): [500]         │  │  └──────────────────────────┘ │
│  │ [开始自动标定] [停止]    │  │                                │
│  └────────────────────────┘  │                                │
│                              │                                │
│  ┌─ 文件操作 Card ─────────┐  │                                │
│  │ 当前文件: config_01.json │  │                                │
│  │ [保存] [另存为] [导入]   │  │                                │
│  │ [导出]                   │  │                                │
│  └────────────────────────┘  │                                │
├──────────────────────────┴──────────────────────────────────┤
│  状态栏: [状态指示] 标定状态信息                               │
└─────────────────────────────────────────────────────────────┘
```

**UI 规范**：
- Card：`materialDesign:Card UniformCornerRadius="8" Padding="16"`
- Card 标题：`materialDesign:PackIcon` + `TextBlock`（PrimaryHueMidBrush 前景色）
- 轴选择：`CheckBox` 绑定 `EnableAxisX/EnableAxisY`
- TCP 连接名：`ComboBox` 绑定可用连接列表
- DataGrid：`AutoGenerateColumns="False" CanUserAddRows="False"`
- 点位操作列：示教按钮（`PackIcon Kind="CrosshairsGps"`）、移动按钮（`PackIcon Kind="ArrowRight"`）
- 状态颜色：X轴=#E53935, Y轴=#43A047
- 底部状态栏：Border + 状态颜色绑定
- 多语言：`{lang:Lang Key}` 标记扩展
- 按钮图标：`materialDesign:PackIcon`，不使用 emoji

#### 4.2 `NPointCalibrationView.xaml.cs`
- 代码后置仅保留最少的 UI 辅助逻辑
- `prism:ViewModelLocator.AutoWireViewModel="True"` 自动关联 ViewModel

---

### 5. ViewModel 层（Module/Controls/Calibration/）

#### 5.1 `NPointCalibrationViewModel.cs`
继承 `BindableBase`，构造函数注入：
- `INPointCalibrationService` — 标定服务
- `IPositionMotionController` — 运动控制
- `ITCPEventService` — TCP 通讯
- `ITCPClientManagerService` — TCP 客户端管理（获取连接名列表）
- `IParameterStorage` — 参数存储
- `IFileDialogService` — 文件对话框
- `ILocalizationService` — 本地化
- `ILoggerService` — 日志
- `IEventAggregator` — 事件总线

**核心属性**：
```csharp
// 轴配置
bool EnableAxisX, EnableAxisY
int PointCount

// TCP 配置
bool EnableVisionData
ObservableCollection<string> TcpConnections
string SelectedTcpConnection
string TriggerCommand

// 自动标定
int AutoCalibDelayMs
bool IsAutoCalibrating

// 标定数据
ObservableCollection<NPointCalibrationPoint> Points
NPointCalibrationConfig Config

// 文件操作
string CurrentFileName  // 仅文件名，不含路径

// 标定结果
AffineCalibrationResult CalibrationResult
string StatusText
Brush StatusColor
```

**核心命令**：
```csharp
DelegateCommand StartAutoCalibCommand
DelegateCommand StopAutoCalibCommand
DelegateCommand<NPointCalibrationPoint> TeachPointCommand
DelegateCommand<NPointCalibrationPoint> MoveToPointCommand
DelegateCommand<NPointCalibrationPoint> DeletePointCommand
DelegateCommand AddPointCommand
DelegateCommand SaveConfigCommand
DelegateCommand SaveAsConfigCommand
DelegateCommand ImportConfigCommand
DelegateCommand ExportConfigCommand
DelegateCommand ComputeCalibrationCommand
```

**自动加载逻辑**：
```csharp
// 构造函数末尾
async Task InitializeAsync()
{
    await LoadTcpConnectionsAsync();
    await TryAutoLoadConfigAsync();
}

async Task TryAutoLoadConfigAsync()
{
    // 1. 从 IParameterStorage 加载默认配置获取 LastFileName
    // 2. 如果 LastFileName 非空，从 Config/Calibration/ 加载该文件
    // 3. CurrentFileName = Path.GetFileName(LastFileName)（仅显示文件名）
}
```

---

### 6. 导航注册（Module/PrimModel.cs）

#### 6.1 RegisterTypes 中添加
```csharp
containerRegistry.RegisterForNavigation<NPointCalibrationView, NPointCalibrationViewModel>();
containerRegistry.RegisterSingleton<INPointCalibrationService, NPointCalibrationService>();
```

#### 6.2 OnInitialized 中添加导航项
```csharp
Navigate.NavigateList.Add(new NavigateItem()
{
    ViewName = "NPointCalibrationView",
    IconKind = "VectorIntersection",  // 或 "CrosshairsGps"
    DisplayName = localizationService.GetResourceOrDefault("Nav_NPointCalibration", "N点标定"),
    DisplayNameKey = "Nav_NPointCalibration",
    UserLevel = 0,
    Display = true
});
```

---

### 7. 多语言资源（MainApp/Languages/）

在 `Strings.zh-CN.xaml` 和 `Strings.en-US.xaml` 中添加所有 UI 文本条目，命名约定 `NPointCalib_` 前缀：

**中文条目示例**：
```xml
<sys:String x:Key="Nav_NPointCalibration">N点标定</sys:String>
<sys:String x:Key="NPointCalib_Title">N点标定</sys:String>
<sys:String x:Key="NPointCalib_AxisConfig">轴配置</sys:String>
<sys:String x:Key="NPointCalib_EnableAxisX">启用X轴</sys:String>
<sys:String x:Key="NPointCalib_EnableAxisY">启用Y轴</sys:String>
<sys:String x:Key="NPointCalib_PointCount">标定点数</sys:String>
<sys:String x:Key="NPointCalib_TcpConfig">通讯配置</sys:String>
<sys:String x:Key="NPointCalib_EnableVisionData">接收视觉数据</sys:String>
<sys:String x:Key="NPointCalib_ConnectionName">连接名称</sys:String>
<sys:String x:Key="NPointCalib_TriggerCommand">触发命令</sys:String>
<sys:String x:Key="NPointCalib_AutoCalib">自动标定</sys:String>
<sys:String x:Key="NPointCalib_DelayMs">延时(ms)</sys:String>
<sys:String x:Key="NPointCalib_StartAutoCalib">开始自动标定</sys:String>
<sys:String x:Key="NPointCalib_StopAutoCalib">停止</sys:String>
<sys:String x:Key="NPointCalib_CalibData">标定数据</sys:String>
<sys:String x:Key="NPointCalib_PointName">名称</sys:String>
<sys:String x:Key="NPointCalib_MachineX">机械X</sys:String>
<sys:String x:Key="NPointCalib_MachineY">机械Y</sys:String>
<sys:String x:Key="NPointCalib_VisionX">视觉X</sys:String>
<sys:String x:Key="NPointCalib_VisionY">视觉Y</sys:String>
<sys:String x:Key="NPointCalib_Status">状态</sys:String>
<sys:String x:Key="NPointCalib_Operation">操作</sys:String>
<sys:String x:Key="NPointCalib_Teach">示教</sys:String>
<sys:String x:Key="NPointCalib_Move">移动</sys:String>
<sys:String x:Key="NPointCalib_AddPoint">添加</sys:String>
<sys:String x:Key="NPointCalib_DeletePoint">删除</sys:String>
<sys:String x:Key="NPointCalib_CalibResult">标定结果</sys:String>
<sys:String x:Key="NPointCalib_FileOperation">文件操作</sys:String>
<sys:String x:Key="NPointCalib_CurrentFile">当前文件</sys:String>
<sys:String x:Key="NPointCalib_Save">保存</sys:String>
<sys:String x:Key="NPointCalib_SaveAs">另存为</sys:String>
<sys:String x:Key="NPointCalib_Import">导入</sys:String>
<sys:String x:Key="NPointCalib_Export">导出</sys:String>
<sys:String x:Key="NPointCalib_Compute">计算标定</sys:String>
<sys:String x:Key="NPointCalib_Idle">空闲</sys:String>
<sys:String x:Key="NPointCalib_Calibrating">标定中...</sys:String>
<sys:String x:Key="NPointCalib_Completed">标定完成</sys:String>
<sys:String x:Key="NPointCalib_Error">标定错误</sys:String>
<sys:String x:Key="NPointCalib_NoFile">未加载文件</sys:String>
<sys:String x:Key="NPointCalib_SaveSuccess">保存成功</sys:String>
<sys:String x:Key="NPointCalib_LoadSuccess">加载成功</sys:String>
<sys:String x:Key="NPointCalib_AutoLoadFailed">自动加载失败</sys:String>
<sys:String x:Key="NPointCalib_MinPointsRequired">标定至少需要3个点</sys:String>
<sys:String x:Key="NPointCalib_VisionDataReceived">视觉数据已接收</sys:String>
<sys:String x:Key="NPointCalib_MovingToPoint">正在移动到点位 {0}...</sys:String>
<sys:String x:Key="NPointCalib_WaitingVisionData">等待视觉数据...</sys:String>
<sys:String x:Key="NPointCalib_PointCalibrated">点位 {0} 标定完成</sys:String>
<sys:String x:Key="NPointCalib_RmsError">RMS误差</sys:String>
<sys:String x:Key="NPointCalib_RotationAngle">等效旋转角</sys:String>
<sys:String x:Key="NPointCalib_ScaleFactor">缩放因子</sys:String>
<sys:String x:Key="NPointCalib_QualityRating">质量评级</sys:String>
```

---

## 文件变更清单

| 操作 | 文件路径 | 说明 |
|------|----------|------|
| 新增 | `Core/Models/NPointCalibrationConfig.cs` | 标定配置模型 |
| 新增 | `Core/Models/NPointCalibrationPoint.cs` | 标定点模型 |
| 新增 | `Core/Models/NPointCalibrationData.cs` | 完整标定数据模型 |
| 新增 | `Core/Abstraction/INPointCalibrationService.cs` | 标定服务接口 |
| 新增 | `Module/Services/NPointCalibrationService.cs` | 标定服务实现 |
| 新增 | `Module/Controls/Calibration/NPointCalibrationView.xaml` | 标定页面视图 |
| 新增 | `Module/Controls/Calibration/NPointCalibrationView.xaml.cs` | 视图代码后置 |
| 新增 | `Module/Controls/Calibration/NPointCalibrationViewModel.cs` | 标定页面 ViewModel |
| 修改 | `Module/PrimModel.cs` | 注册视图+服务+导航项 |
| 修改 | `MainApp/Languages/Strings.zh-CN.xaml` | 中文语言资源 |
| 修改 | `MainApp/Languages/Strings.en-US.xaml` | 英文语言资源 |

---

## 假设与决策

1. **标定算法**：复用已有的 `AffineCalibrationService.Solve()`，N点最小二乘仿射标定（>=3点有效）
2. **TCP 数据格式**：假设视觉返回 JSON 格式 `{"X": 123.45, "Y": 67.89}` 或逗号分隔 `"123.45,67.89"`，服务层支持两种解析
3. **轴配置**：X/Y 轴通过 CheckBox 选配，未选中的轴对应坐标列只读
4. **自动标定流程**：移动到点位 -> 示教机械坐标 -> 发送触发命令 -> 等待视觉数据 -> 填充表格 -> 延时 -> 下一点位
5. **配置存储**：使用 `IParameterStorage` 的自定义路径重载功能，默认路径 `Config/Calibration/`
6. **文件名显示**：`CurrentFileName` 属性仅存储/显示文件名（`Path.GetFileName()`），不含路径
7. **导航权限**：UserLevel=0，操作员可见
8. **ViewModel 基类**：使用 `BindableBase`（与 NeedleCameraAlignmentViewModel 一致）
9. **服务生命周期**：`INPointCalibrationService` 注册为 Singleton，全局共享标定状态

---

## 验证步骤

1. 编译通过，无错误无警告
2. 导航菜单显示"N点标定"项，点击可进入页面
3. 轴配置：勾选/取消 X/Y 轴，DataGrid 对应列可编辑/只读状态正确
4. 点数设置：修改点数后，Points 集合正确增减
5. 示教功能：点击示教按钮，读取当前轴位置填入机械坐标
6. 移动功能：点击移动按钮，轴运动到对应点位
7. 自动标定：开始后按序移动、示教、触发视觉、填充、延时，完成后显示结果
8. TCP 接收：启用视觉数据时，TCP 数据自动填充视觉坐标列
9. 手动输入：禁用视觉数据时，视觉坐标列可手动编辑
10. 文件操作：保存/另存为/导入/导出功能正常
11. 自动加载：上次使用的配置文件在启动时自动加载，文件名显示正确（仅文件名）
12. 多语言：中英文切换后所有文本正确显示
13. 仿射计算：>=3点时计算按钮可用，结果正确显示
