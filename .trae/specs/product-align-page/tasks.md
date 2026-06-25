# 产品对齐校准页面（Product Align）实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 增强现有 ProductCalibrationView，新增相机屏蔽、模式切换、基准特征点对比、Halcon计算、取反开关等能力，重新设计UI布局。

**Architecture:** 在现有 ProductCalibrationView/ViewModel 上扩展，Halcon 计算作为 IStageCalibrationService 新方法，TCP 自动接收通过订阅 ITCPEventService.CameraMessageReceived 事件实现。UI 参考 DualGantryCalibrationView 左右分栏风格。

**Tech Stack:** WPF + Prism + MaterialDesignInXAML + Halcon (HalconDotNet) + TCPIPModule

**Spec:** `.trae/specs/product-align-page/spec.md`

---

## 文件结构

| 文件 | 责任 | 改动 |
|------|------|------|
| `Core/Models/StageCalibrationData.cs` | 配置数据模型 | 扩展：新增字段 + ProductAlignResult + DataReceiveMode 枚举 |
| `Core/Abstraction/IStageCalibrationService.cs` | 服务接口 | 扩展：新增3个方法签名 |
| `Module/Services/StageCalibrationService.cs` | 服务实现 | 扩展：实现Halcon计算 + TCP订阅 |
| `Module/Controls/Loading/ProductCalibrationViewModel.cs` | 视图模型 | 扩展：新增属性/命令/逻辑 |
| `Module/Controls/Loading/ProductCalibrationView.xaml` | 视图 | 重写：左右分栏新布局 |
| `MainApp/Languages/Strings.zh-CN.xaml` | 中文资源 | 扩展：新增23个Key |
| `MainApp/Languages/Strings.en-US.xaml` | 英文资源 | 扩展：同步23个Key |
| `MainApp/bin/Debug/net9.0-windows7.0/版本修改记录.txt` | 版本记录 | 追加 |

---

### Task 1: 扩展数据模型

**Files:**
- Modify: `Core/Models/StageCalibrationData.cs`

- [ ] **Step 1: 在 StageCalibrationData.cs 末尾新增 ProductAlignResult 类和 DataReceiveMode 枚举**

在文件末尾（`StageCalibrationData` 类之后）追加：

```csharp
/// <summary>
/// Halcon计算结果：两个特征点的中心点、角度、距离
/// </summary>
public class ProductAlignResult
{
    /// <summary>中心点X（机械坐标）</summary>
    public double CenterX { get; set; }

    /// <summary>中心点Y（机械坐标）</summary>
    public double CenterY { get; set; }

    /// <summary>归一化角度[-180,180]度</summary>
    public double AngleDeg { get; set; }

    /// <summary>两点间距离</summary>
    public double Distance { get; set; }
}

/// <summary>数据接收模式</summary>
public enum DataReceiveMode
{
    /// <summary>手动触发拍照</summary>
    ManualTrigger,
    /// <summary>自动接收TCP推送</summary>
    AutoReceive
}
```

- [ ] **Step 2: 扩展 StageCalibrationConfig 类，新增字段**

在 `StageCalibrationConfig` 类的 `LastFileName` 属性之后追加：

```csharp
// === 新增字段（产品对齐校准扩展） ===

/// <summary>相机屏蔽（true=禁用相机，手动输入特征点）</summary>
public bool CameraShielded { get; set; }

/// <summary>数据接收模式：ManualTrigger / AutoReceive</summary>
public string DataReceiveMode { get; set; } = "ManualTrigger";

/// <summary>基准特征点中心X</summary>
public double ReferenceCenterX { get; set; }

/// <summary>基准特征点中心Y</summary>
public double ReferenceCenterY { get; set; }

/// <summary>基准角度（度，归一化[-180,180]）</summary>
public double ReferenceAngle { get; set; }

/// <summary>ΔX取反开关</summary>
public bool InvertDeltaX { get; set; }

/// <summary>ΔY取反开关</summary>
public bool InvertDeltaY { get; set; }

/// <summary>ΔAngle取反开关</summary>
public bool InvertDeltaAngle { get; set; }
```

- [ ] **Step 3: 编译验证**

Run: `dotnet build Core/Core.csproj`
Expected: BUILD SUCCEEDED，无错误

- [ ] **Step 4: 提交**

```bash
git add Core/Models/StageCalibrationData.cs
git commit -m "feat(core): 扩展StageCalibrationConfig新增相机屏蔽/模式/基准/取反字段，新增ProductAlignResult和DataReceiveMode"
```

---

### Task 2: 扩展 IStageCalibrationService 接口

**Files:**
- Modify: `Core/Abstraction/IStageCalibrationService.cs`

- [ ] **Step 1: 在接口末尾新增3个方法签名**

在 `IStageCalibrationService` 接口的 `ReadCurrentPositionsAsync` 方法之后追加：

```csharp
// ===== 新增：产品对齐校准扩展 =====

/// <summary>使用 Halcon 算子计算两个特征点的中心点、角度（归一化[-180,180]）、距离</summary>
/// <param name="p1X">特征点1机械坐标X</param>
/// <param name="p1Y">特征点1机械坐标Y</param>
/// <param name="p2X">特征点2机械坐标X</param>
/// <param name="p2Y">特征点2机械坐标Y</param>
ProductAlignResult CalculateCenterAndAngleWithHalcon(double p1X, double p1Y, double p2X, double p2Y);

/// <summary>订阅相机数据自动接收（解析 TCP 推送的视觉坐标）</summary>
/// <param name="tcpConnectionName">TCP连接名</param>
/// <param name="onDataReceived">数据回调（featureIndex 1或2, X, Y）</param>
void SubscribeCameraData(string tcpConnectionName, Action<int, double, double> onDataReceived);

/// <summary>取消订阅相机数据</summary>
void UnsubscribeCameraData();
```

- [ ] **Step 2: 编译验证（预期失败，因实现类尚未实现新方法）**

Run: `dotnet build Core/Core.csproj`
Expected: BUILD SUCCEEDED（接口扩展不影响 Core 编译）

Run: `dotnet build Module/Module.csproj`
Expected: BUILD FAILED，StageCalibrationService 未实现新接口方法

- [ ] **Step 3: 提交**

```bash
git add Core/Abstraction/IStageCalibrationService.cs
git commit -m "feat(core): IStageCalibrationService新增Halcon计算和TCP订阅方法签名"
```

---

### Task 3: 实现 StageCalibrationService 新方法

**Files:**
- Modify: `Module/Services/StageCalibrationService.cs`

- [ ] **Step 1: 添加 using HalconDotNet 引用**

在文件顶部 using 区域追加：

```csharp
using HalconDotNet;
```

- [ ] **Step 2: 添加 TCP 订阅相关私有字段**

在 `StageCalibrationService` 类的 `_currentData` 字段之后追加：

```csharp
/// <summary>当前订阅的TCP连接名</summary>
private string _subscribedTcpConnectionName = string.Empty;

/// <summary>当前数据接收回调</summary>
private Action<int, double, double> _dataReceivedCallback;

/// <summary>当前自动接收的目标特征点索引（1或2），由调用方在订阅时指定</summary>
private int _autoReceiveFeatureIndex = 1;
```

- [ ] **Step 3: 实现 CalculateCenterAndAngleWithHalcon 方法**

在 `ReadCurrentPositionsAsync` 方法之后追加：

```csharp
/// <summary>
/// 使用 Halcon 算子计算两个特征点的中心点、角度（归一化[-180,180]）、距离
/// 算子：distance_pp 计算距离，angle_ll 计算角度（P1→P2 与 X轴正方向夹角）
/// </summary>
public ProductAlignResult CalculateCenterAndAngleWithHalcon(double p1X, double p1Y, double p2X, double p2Y)
{
    _logger?.Info($"StageCalibration: Halcon计算 中心点/角度 P1({p1X:F3},{p1Y:F3}) P2({p2X:F3},{p2Y:F3})");

    // 1. 中心点：两点的中点
    double centerX = (p1X + p2X) / 2.0;
    double centerY = (p1Y + p2Y) / 2.0;

    // 2. 距离：Halcon distance_pp 算子（注意 Halcon 坐标系 Row=Y, Col=X）
    HOperatorSet.DistancePp(p1Y, p1X, p2Y, p2X, out HTuple distance);

    // 3. 角度：Halcon angle_ll 算子
    //    第一条线：P1→P2，第二条线：X轴正方向 (Row=0,Col=0)→(Row=0,Col=1)
    //    返回弧度，范围 [-pi, pi]
    HOperatorSet.AngleLl(p1Y, p1X, p2Y, p2X, 0, 0, 0, 1, out HTuple angle);
    double angleDeg = angle.D * 180.0 / Math.PI;

    var result = new ProductAlignResult
    {
        CenterX = centerX,
        CenterY = centerY,
        AngleDeg = NormalizeAngle(angleDeg),
        Distance = distance.D
    };

    _logger?.Info($"StageCalibration: Halcon计算完成 中心({result.CenterX:F3},{result.CenterY:F3}) 角度:{result.AngleDeg:F3}° 距离:{result.Distance:F3}");
    return result;
}

/// <summary>角度归一化到 [-180, 180]</summary>
private static double NormalizeAngle(double angleDeg)
{
    while (angleDeg > 180) angleDeg -= 360;
    while (angleDeg <= -180) angleDeg += 360;
    return angleDeg;
}
```

- [ ] **Step 4: 实现 SubscribeCameraData 和 UnsubscribeCameraData 方法**

在 `CalculateCenterAndAngleWithHalcon` 方法之后追加：

```csharp
/// <summary>
/// 订阅相机数据自动接收
/// 通过订阅 ITCPEventService.CameraMessageReceived 事件，解析 TCP 推送的视觉坐标
/// </summary>
public void SubscribeCameraData(string tcpConnectionName, Action<int, double, double> onDataReceived)
{
    // 先取消已有订阅，避免重复
    if (!string.IsNullOrEmpty(_subscribedTcpConnectionName))
        UnsubscribeCameraData();

    _subscribedTcpConnectionName = tcpConnectionName ?? string.Empty;
    _dataReceivedCallback = onDataReceived;

    _tcpEventService.CameraMessageReceived += OnCameraMessageReceived;
    _logger?.Info($"StageCalibration: 已订阅相机数据自动接收，连接:{tcpConnectionName}");
}

/// <summary>取消订阅相机数据</summary>
public void UnsubscribeCameraData()
{
    if (!string.IsNullOrEmpty(_subscribedTcpConnectionName))
    {
        _tcpEventService.CameraMessageReceived -= OnCameraMessageReceived;
        _logger?.Info("StageCalibration: 已取消订阅相机数据");
    }
    _subscribedTcpConnectionName = string.Empty;
    _dataReceivedCallback = null;
}

/// <summary>
/// 相机消息接收事件处理
/// 仅处理来自订阅连接的消息，解析后回调
/// </summary>
private void OnCameraMessageReceived(string cameraName, string message)
{
    if (_dataReceivedCallback == null) return;

    // 仅处理匹配订阅连接名的消息（cameraName 为空时也接受，兼容广播模式）
    if (!string.IsNullOrEmpty(_subscribedTcpConnectionName) &&
        !string.IsNullOrEmpty(cameraName) &&
        !string.Equals(cameraName, _subscribedTcpConnectionName, StringComparison.OrdinalIgnoreCase))
        return;

    try
    {
        var (x, y) = ParseVisionData(message);
        _logger?.Info($"StageCalibration: 自动接收数据 特征点{_autoReceiveFeatureIndex} X:{x:F3} Y:{y:F3}");
        _dataReceivedCallback?.Invoke(_autoReceiveFeatureIndex, x, y);
    }
    catch (Exception ex)
    {
        _logger?.Warn($"StageCalibration: 解析自动接收数据失败 - {ex.Message}, 原始消息:{message}");
    }
}

/// <summary>设置自动接收的目标特征点索引（1或2）</summary>
public void SetAutoReceiveFeatureIndex(int index)
{
    _autoReceiveFeatureIndex = (index == 1 || index == 2) ? index : 1;
}
```

- [ ] **Step 5: 编译验证**

Run: `dotnet build Module/Module.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 6: 提交**

```bash
git add Module/Services/StageCalibrationService.cs
git commit -m "feat(module): StageCalibrationService实现Halcon计算(angle_ll/distance_pp)和TCP自动接收订阅"
```

---

### Task 4: 添加多语言资源

**Files:**
- Modify: `MainApp/Languages/Strings.zh-CN.xaml`
- Modify: `MainApp/Languages/Strings.en-US.xaml`

- [ ] **Step 1: 在 Strings.zh-CN.xaml 的 ProductCalib_SaveSuccess 行之后追加中文资源**

在 `ProductCalib_LoadSuccess` 行之后追加：

```xml
<!-- 产品对齐校准扩展 -->
<sys:String x:Key="ProductCalib_CameraShielded">相机屏蔽</sys:String>
<sys:String x:Key="ProductCalib_DataReceiveMode">数据接收模式</sys:String>
<sys:String x:Key="ProductCalib_ManualTrigger">手动触发</sys:String>
<sys:String x:Key="ProductCalib_AutoReceive">自动接收</sys:String>
<sys:String x:Key="ProductCalib_FeatureResult">特征点计算结果</sys:String>
<sys:String x:Key="ProductCalib_CenterPoint">中心点</sys:String>
<sys:String x:Key="ProductCalib_Angle">角度</sys:String>
<sys:String x:Key="ProductCalib_Distance">距离</sys:String>
<sys:String x:Key="ProductCalib_Recalculate">重新计算</sys:String>
<sys:String x:Key="ProductCalib_ReferenceFeature">基准特征点</sys:String>
<sys:String x:Key="ProductCalib_ReferenceCenter">基准中心</sys:String>
<sys:String x:Key="ProductCalib_ReferenceAngle">基准角度</sys:String>
<sys:String x:Key="ProductCalib_SetAsReference">设为基准</sys:String>
<sys:String x:Key="ProductCalib_ManualInput">手动输入</sys:String>
<sys:String x:Key="ProductCalib_Deviation">偏差</sys:String>
<sys:String x:Key="ProductCalib_Invert">取反</sys:String>
<sys:String x:Key="ProductCalib_Calculating">计算中...</sys:String>
<sys:String x:Key="ProductCalib_CalcDone">计算完成</sys:String>
<sys:String x:Key="ProductCalib_SetRefDone">基准设置完成</sys:String>
<sys:String x:Key="ProductCalib_AutoReceiveOn">自动接收已开启</sys:String>
<sys:String x:Key="ProductCalib_AutoReceiveOff">自动接收已关闭</sys:String>
<sys:String x:Key="ProductCalib_CameraShieldedOn">相机已屏蔽，可手动输入</sys:String>
<sys:String x:Key="ProductCalib_Photo1AutoReceived">拍照位1自动接收数据</sys:String>
<sys:String x:Key="ProductCalib_Photo2AutoReceived">拍照位2自动接收数据</sys:String>
```

- [ ] **Step 2: 在 Strings.en-US.xaml 对应位置追加英文资源**

在 `ProductCalib_LoadSuccess` 行之后追加：

```xml
<!-- Product Align extensions -->
<sys:String x:Key="ProductCalib_CameraShielded">Camera Shielded</sys:String>
<sys:String x:Key="ProductCalib_DataReceiveMode">Data Receive Mode</sys:String>
<sys:String x:Key="ProductCalib_ManualTrigger">Manual Trigger</sys:String>
<sys:String x:Key="ProductCalib_AutoReceive">Auto Receive</sys:String>
<sys:String x:Key="ProductCalib_FeatureResult">Feature Calculation Result</sys:String>
<sys:String x:Key="ProductCalib_CenterPoint">Center Point</sys:String>
<sys:String x:Key="ProductCalib_Angle">Angle</sys:String>
<sys:String x:Key="ProductCalib_Distance">Distance</sys:String>
<sys:String x:Key="ProductCalib_Recalculate">Recalculate</sys:String>
<sys:String x:Key="ProductCalib_ReferenceFeature">Reference Feature</sys:String>
<sys:String x:Key="ProductCalib_ReferenceCenter">Reference Center</sys:String>
<sys:String x:Key="ProductCalib_ReferenceAngle">Reference Angle</sys:String>
<sys:String x:Key="ProductCalib_SetAsReference">Set As Reference</sys:String>
<sys:String x:Key="ProductCalib_ManualInput">Manual Input</sys:String>
<sys:String x:Key="ProductCalib_Deviation">Deviation</sys:String>
<sys:String x:Key="ProductCalib_Invert">Invert</sys:String>
<sys:String x:Key="ProductCalib_Calculating">Calculating...</sys:String>
<sys:String x:Key="ProductCalib_CalcDone">Calculation Done</sys:String>
<sys:String x:Key="ProductCalib_SetRefDone">Reference Set</sys:String>
<sys:String x:Key="ProductCalib_AutoReceiveOn">Auto Receive Enabled</sys:String>
<sys:String x:Key="ProductCalib_AutoReceiveOff">Auto Receive Disabled</sys:String>
<sys:String x:Key="ProductCalib_CameraShieldedOn">Camera Shielded, Manual Input</sys:String>
<sys:String x:Key="ProductCalib_Photo1AutoReceived">Photo1 Auto Received</sys:String>
<sys:String x:Key="ProductCalib_Photo2AutoReceived">Photo2 Auto Received</sys:String>
```

- [ ] **Step 3: 提交**

```bash
git add MainApp/Languages/Strings.zh-CN.xaml MainApp/Languages/Strings.en-US.xaml
git commit -m "feat(lang): 新增产品对齐校准23个多语言Key(zh-CN/en-US)"
```

---

### Task 5: 扩展 ProductCalibrationViewModel

**Files:**
- Modify: `Module/Controls/Loading/ProductCalibrationViewModel.cs`

- [ ] **Step 1: 添加 using Core.Models 引用确认（已有）并新增常量**

在类开头字段区追加保留变量名集合（用于全局变量链接过滤）：

```csharp
/// <summary>保留变量名集合：偏差值名 + _LinkedVar 后缀名，这些变量不出现在链接下拉框中</summary>
private static readonly HashSet<string> ReservedVarNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "DeltaX", "DeltaY", "DeltaAngle",
    "DeltaX_LinkedVar", "DeltaY_LinkedVar", "DeltaAngle_LinkedVar"
};
```

- [ ] **Step 2: 新增相机屏蔽与模式属性**

在 `#region 属性 — TCP配置` 之前新增：

```csharp
#region 属性 — 相机屏蔽与数据接收模式

private bool _cameraShielded;
/// <summary>相机屏蔽（true=禁用相机，手动输入特征点）</summary>
public bool CameraShielded
{
    get => _cameraShielded;
    set
    {
        if (SetProperty(ref _cameraShielded, value))
        {
            RaisePropertyChanged(nameof(IsCameraActive));
            // 屏蔽开启时取消自动接收订阅
            if (value && DataReceiveMode == DataReceiveMode.AutoReceive)
                UnsubscribeAutoReceive();
        }
    }
}

/// <summary>相机是否可用（屏蔽时为false）</summary>
public bool IsCameraActive => !CameraShielded;

private DataReceiveMode _dataReceiveMode = DataReceiveMode.ManualTrigger;
/// <summary>数据接收模式</summary>
public DataReceiveMode DataReceiveMode
{
    get => _dataReceiveMode;
    set
    {
        if (SetProperty(ref _dataReceiveMode, value))
        {
            // 切换到手动模式时取消订阅
            if (value == DataReceiveMode.ManualTrigger)
                UnsubscribeAutoReceive();
        }
    }
}

#endregion
```

- [ ] **Step 3: 新增特征点计算结果属性**

在 `#region 属性 — 偏差结果` 之前新增：

```csharp
#region 属性 — 特征点计算结果（Halcon）

private double _centerX;
/// <summary>中心点X（机械坐标）</summary>
public double CenterX { get => _centerX; set => SetProperty(ref _centerX, value); }

private double _centerY;
/// <summary>中心点Y（机械坐标）</summary>
public double CenterY { get => _centerY; set => SetProperty(ref _centerY, value); }

private double _resultAngle;
/// <summary>归一化角度[-180,180]度</summary>
public double ResultAngle { get => _resultAngle; set => SetProperty(ref _resultAngle, value); }

private double _pointDistance;
/// <summary>两点间距离</summary>
public double PointDistance { get => _pointDistance; set => SetProperty(ref _pointDistance, value); }

#endregion
```

- [ ] **Step 4: 新增基准特征点属性**

在 `#region 属性 — 偏差结果` 内的 `OffsetY` 属性之后追加：

```csharp
// === 基准特征点 ===
private double _referenceCenterX;
/// <summary>基准特征点中心X</summary>
public double ReferenceCenterX { get => _referenceCenterX; set => SetProperty(ref _referenceCenterX, value); }

private double _referenceCenterY;
/// <summary>基准特征点中心Y</summary>
public double ReferenceCenterY { get => _referenceCenterY; set => SetProperty(ref _referenceCenterY, value); }

private double _referenceAngle;
/// <summary>基准角度（度，归一化[-180,180]）</summary>
public double ReferenceAngle { get => _referenceAngle; set => SetProperty(ref _referenceAngle, value); }
```

- [ ] **Step 5: 新增取反开关属性**

在 `#region 属性 — 偏差结果` 末尾追加：

```csharp
// === 取反开关（每项独立） ===
private bool _invertDeltaX;
/// <summary>ΔX取反开关</summary>
public bool InvertDeltaX
{
    get => _invertDeltaX;
    set
    {
        if (SetProperty(ref _invertDeltaX, value))
            CalculateDeviationFromReference();
    }
}

private bool _invertDeltaY;
/// <summary>ΔY取反开关</summary>
public bool InvertDeltaY
{
    get => _invertDeltaY;
    set
    {
        if (SetProperty(ref _invertDeltaY, value))
            CalculateDeviationFromReference();
    }
}

private bool _invertDeltaAngle;
/// <summary>ΔAngle取反开关</summary>
public bool InvertDeltaAngle
{
    get => _invertDeltaAngle;
    set
    {
        if (SetProperty(ref _invertDeltaAngle, value))
            CalculateDeviationFromReference();
    }
}
```

- [ ] **Step 6: 新增命令定义**

在 `#region 命令` 区域的 `UnlinkDeltaAngleCommand` 之后追加：

```csharp
public DelegateCommand SetAsReferenceCommand { get; }
public DelegateCommand CalculateCommand { get; }
public DelegateCommand ToggleDataReceiveModeCommand { get; }
```

- [ ] **Step 7: 在构造函数初始化新命令**

在构造函数的 `UnlinkDeltaAngleCommand = new DelegateCommand(() => DeltaAngleLinkedVar = null);` 之后追加：

```csharp
SetAsReferenceCommand = new DelegateCommand(ExecuteSetAsReference, () => Photo1Captured && Photo2Captured);
CalculateCommand = new DelegateCommand(ExecuteRecalculate, () => Photo1Captured && Photo2Captured);
ToggleDataReceiveModeCommand = new DelegateCommand(ExecuteToggleDataReceiveMode);
```

- [ ] **Step 8: 新增自动接收订阅字段和当前特征点索引**

在类字段区追加：

```csharp
/// <summary>当前自动接收目标特征点索引（1或2），由用户选择拍照位决定</summary>
private int _currentAutoReceiveIndex = 1;
```

- [ ] **Step 9: 修改 ExecuteCapture1Async，拍照完成后触发Halcon计算**

将 `ExecuteCapture1Async` 方法中 `Photo1Captured = true;` 之后追加：

```csharp
// 通知命令可用性
SetAsReferenceCommand.RaiseCanExecuteChanged();
CalculateCommand.RaiseCanExecuteChanged();

// 两次都拍照完成则自动计算
if (Photo2Captured)
    ExecuteRecalculate();
```

- [ ] **Step 10: 修改 ExecuteCapture2Async，拍照完成后触发Halcon计算和偏差计算**

将 `ExecuteCapture2Async` 方法中 `Photo2Captured = true;` 之后的 `CalculateDeviations();` 调用替换为：

```csharp
// 通知命令可用性
SetAsReferenceCommand.RaiseCanExecuteChanged();
CalculateCommand.RaiseCanExecuteChanged();

// 两次都拍照完成则自动计算（使用Halcon）
if (Photo1Captured)
    ExecuteRecalculate();
```

并删除原 `CalculateDeviations()` 方法（被新的 `ExecuteRecalculate` + `CalculateDeviationFromReference` 替代）。

- [ ] **Step 11: 新增 ExecuteSetAsReference 方法**

在 `#region 偏差计算` 区域（替换原 `CalculateDeviations` 方法）追加：

```csharp
/// <summary>设为基准：将当前Halcon计算结果填入基准特征点</summary>
private void ExecuteSetAsReference()
{
    ReferenceCenterX = CenterX;
    ReferenceCenterY = CenterY;
    ReferenceAngle = ResultAngle;
    UpdateStatus(L("ProductCalib_SetRefDone", "基准设置完成"), Brushes.LightGreen);
}
```

- [ ] **Step 12: 新增 ExecuteRecalculate 方法（Halcon计算）**

在 `ExecuteSetAsReference` 之后追加：

```csharp
/// <summary>使用Halcon算子重新计算中心点、角度、距离，并计算与基准的偏差</summary>
private void ExecuteRecalculate()
{
    if (!Photo1Captured || !Photo2Captured) return;

    UpdateStatus(L("ProductCalib_Calculating", "计算中..."), Brushes.Orange);
    try
    {
        var result = _calibService.CalculateCenterAndAngleWithHalcon(
            Photo1VisionX, Photo1VisionY, Photo2VisionX, Photo2VisionY);

        CenterX = result.CenterX;
        CenterY = result.CenterY;
        ResultAngle = result.AngleDeg;
        PointDistance = result.Distance;

        // 计算与基准的偏差
        CalculateDeviationFromReference();

        // 写入链接的全局变量
        _ = WriteToGlobalVariablesAsync();

        UpdateStatus(L("ProductCalib_CalcDone", "计算完成"), Brushes.LightGreen);
    }
    catch (Exception ex)
    {
        _logger.Error(ex, "载台校准: Halcon计算失败");
        UpdateStatus($"{L("ProductCalib_Error", "错误")}: {ex.Message}", Brushes.Red);
    }
}
```

- [ ] **Step 13: 新增 CalculateDeviationFromReference 方法**

在 `ExecuteRecalculate` 之后追加：

```csharp
/// <summary>计算当前特征点与基准点的偏差（应用取反开关）</summary>
private void CalculateDeviationFromReference()
{
    // 原始偏差 = 当前 - 基准
    double rawDeltaX = CenterX - ReferenceCenterX;
    double rawDeltaY = CenterY - ReferenceCenterY;
    double rawDeltaAngle = NormalizeAngle(ResultAngle - ReferenceAngle);

    // 应用取反开关
    DeltaX = InvertDeltaX ? -rawDeltaX : rawDeltaX;
    DeltaY = InvertDeltaY ? -rawDeltaY : rawDeltaY;
    DeltaAngle = InvertDeltaAngle ? -rawDeltaAngle : rawDeltaAngle;
}

/// <summary>角度归一化到 [-180, 180]</summary>
private static double NormalizeAngle(double angleDeg)
{
    while (angleDeg > 180) angleDeg -= 360;
    while (angleDeg <= -180) angleDeg += 360;
    return angleDeg;
}
```

- [ ] **Step 14: 新增 ExecuteToggleDataReceiveMode 和自动接收订阅方法**

在 `#region 旋转校正` 之前新增 region：

```csharp
#region 数据接收模式

/// <summary>切换数据接收模式</summary>
private void ExecuteToggleDataReceiveMode()
{
    if (CameraShielded)
    {
        UpdateStatus(L("ProductCalib_CameraShieldedOn", "相机已屏蔽，可手动输入"), Brushes.Orange);
        return;
    }

    if (DataReceiveMode == DataReceiveMode.ManualTrigger)
    {
        DataReceiveMode = DataReceiveMode.AutoReceive;
        SubscribeAutoReceive();
        UpdateStatus(L("ProductCalib_AutoReceiveOn", "自动接收已开启"), Brushes.LightGreen);
    }
    else
    {
        DataReceiveMode = DataReceiveMode.ManualTrigger;
        UnsubscribeAutoReceive();
        UpdateStatus(L("ProductCalib_AutoReceiveOff", "自动接收已关闭"), Brushes.LightGray);
    }
}

/// <summary>订阅自动接收</summary>
private void SubscribeAutoReceive()
{
    if (CameraShielded) return;
    if (string.IsNullOrEmpty(SelectedTcpConnection))
    {
        UpdateStatus(L("ProductCalib_Error", "错误") + ": TCP连接未选择", Brushes.Red);
        return;
    }

    _calibService.SubscribeCameraData(SelectedTcpConnection, OnAutoDataReceived);
}

/// <summary>取消订阅自动接收</summary>
private void UnsubscribeAutoReceive()
{
    _calibService.UnsubscribeCameraData();
}

/// <summary>自动接收数据回调</summary>
private void OnAutoDataReceived(int featureIndex, double x, double y)
{
    Application.Current?.Dispatcher.Invoke(() =>
    {
        if (featureIndex == 1)
        {
            Photo1VisionX = x;
            Photo1VisionY = y;
            Photo1Captured = true;
            UpdateStatus(L("ProductCalib_Photo1AutoReceived", "拍照位1自动接收数据"), Brushes.LightGreen);
        }
        else if (featureIndex == 2)
        {
            Photo2VisionX = x;
            Photo2VisionY = y;
            Photo2Captured = true;
            UpdateStatus(L("ProductCalib_Photo2AutoReceived", "拍照位2自动接收数据"), Brushes.LightGreen);
        }

        SetAsReferenceCommand.RaiseCanExecuteChanged();
        CalculateCommand.RaiseCanExecuteChanged();

        // 两次都收到后自动计算
        if (Photo1Captured && Photo2Captured)
            ExecuteRecalculate();
    });
}

/// <summary>设置当前自动接收目标特征点索引（供UI选择拍照位时调用）</summary>
public void SetAutoReceiveTarget(int index)
{
    _currentAutoReceiveIndex = (index == 1 || index == 2) ? index : 1;
    if (_calibService is StageCalibrationService svc)
        svc.SetAutoReceiveFeatureIndex(_currentAutoReceiveIndex);
}

#endregion
```

- [ ] **Step 15: 修改 BuildCurrentConfig 和 LoadConfigFromFileAsync 持久化新字段**

在 `BuildCurrentConfig` 方法中追加新字段赋值（在 `LastFileName = CurrentFileName` 之前）：

```csharp
CameraShielded = CameraShielded,
DataReceiveMode = DataReceiveMode.ToString(),
ReferenceCenterX = ReferenceCenterX,
ReferenceCenterY = ReferenceCenterY,
ReferenceAngle = ReferenceAngle,
InvertDeltaX = InvertDeltaX,
InvertDeltaY = InvertDeltaY,
InvertDeltaAngle = InvertDeltaAngle,
```

在 `LoadConfigFromFileAsync` 方法中追加新字段读取（在 `DeltaAngleLinkedVar = config.DeltaAngleLinkedVar;` 之后）：

```csharp
CameraShielded = config.CameraShielded;
if (Enum.TryParse<DataReceiveMode>(config.DataReceiveMode, out var mode))
    DataReceiveMode = mode;
ReferenceCenterX = config.ReferenceCenterX;
ReferenceCenterY = config.ReferenceCenterY;
ReferenceAngle = config.ReferenceAngle;
InvertDeltaX = config.InvertDeltaX;
InvertDeltaY = config.InvertDeltaY;
InvertDeltaAngle = config.InvertDeltaAngle;
```

- [ ] **Step 16: 修改 Destroy 逻辑，取消订阅避免内存泄漏**

由于 `ProductCalibrationViewModel` 继承 `BindableBase`（非 ViewModelBase），需添加 Destroy 方法。在类末尾追加：

```csharp
/// <summary>页面销毁时取消订阅，防止内存泄漏</summary>
public void Destroy()
{
    UnsubscribeAutoReceive();
}
```

- [ ] **Step 17: 编译验证**

Run: `dotnet build Module/Module.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 18: 提交**

```bash
git add Module/Controls/Loading/ProductCalibrationViewModel.cs
git commit -m "feat(module): ProductCalibrationViewModel新增相机屏蔽/模式切换/基准对比/Halcon计算/取反开关"
```

---

### Task 6: 重写 ProductCalibrationView.xaml

**Files:**
- Modify: `Module/Controls/Loading/ProductCalibrationView.xaml`

- [ ] **Step 1: 重写整个 XAML 文件**

完整替换 `ProductCalibrationView.xaml` 内容：

```xml
<UserControl x:Class="Module.Views.ProductCalibrationView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes"
             xmlns:prism="http://prismlibrary.com/"
             xmlns:lang="clr-namespace:Core.Markup;assembly=Core"
             xmlns:common="clr-namespace:ModuleCore.UserControls;assembly=ModuleCore"
             xmlns:converters="clr-namespace:Module.Converters"
             prism:ViewModelLocator.AutoWireViewModel="True"
             MinHeight="600" MinWidth="900"
             Background="#EEF2F5">
    <UserControl.Resources>
        <ResourceDictionary>
            <converters:BooleanToBrushConverter x:Key="BoolToBrushConverter" />
            <converters:NullToVisibilityConverter x:Key="NullToVisibilityConverter" />
            <converters:InverseBooleanConverter x:Key="InverseBooleanConverter" />
            <BooleanToVisibilityConverter x:Key="BoolToVis" />

            <!-- 参数标签样式 -->
            <Style x:Key="ParamLabelStyle" TargetType="TextBlock">
                <Setter Property="FontSize" Value="12" />
                <Setter Property="Foreground" Value="#616161" />
                <Setter Property="VerticalAlignment" Value="Center" />
                <Setter Property="Margin" Value="0,0,6,0" />
            </Style>

            <!-- 卡片标题样式 -->
            <Style x:Key="CardHeaderStyle" TargetType="StackPanel">
                <Setter Property="Orientation" Value="Horizontal" />
                <Setter Property="Margin" Value="0,0,0,12" />
            </Style>
        </ResourceDictionary>
    </UserControl.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <!-- 主内容区：左配置栏 + 右操作区 -->
        <Grid Grid.Row="0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="320" MinWidth="280" MaxWidth="400" />
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="*" MinWidth="500" />
            </Grid.ColumnDefinitions>

            <!-- ============================================================ -->
            <!-- 左栏：配置                                                    -->
            <!-- ============================================================ -->
            <ScrollViewer Grid.Column="0" VerticalScrollBarVisibility="Auto"
                          HorizontalScrollBarVisibility="Disabled">
                <StackPanel Margin="8,8,4,8">

                    <!-- 机构配置卡片 -->
                    <materialDesign:Card UniformCornerRadius="8" Padding="16" Margin="0,0,0,10">
                        <StackPanel>
                            <StackPanel Style="{StaticResource CardHeaderStyle}">
                                <materialDesign:PackIcon Kind="AxisArrow" Width="18" Height="18"
                                                         Foreground="{DynamicResource PrimaryHueMidBrush}"
                                                         Margin="0,0,8,0" VerticalAlignment="Center" />
                                <TextBlock Text="{lang:Lang ProductCalib_MotionControl}"
                                           FontWeight="Bold" FontSize="14"
                                           Foreground="{DynamicResource PrimaryHueMidBrush}" />
                            </StackPanel>

                            <!-- 拍照基准位 Rx/Rz -->
                            <TextBlock Text="{lang:Lang ProductCalib_ReferencePosition}"
                                       Style="{StaticResource ParamLabelStyle}" Margin="0,0,0,4" />
                            <Grid Margin="0,0,0,4">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="24" />
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="24" />
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="Auto" />
                                </Grid.ColumnDefinitions>
                                <TextBlock Text="Rx" VerticalAlignment="Center" FontSize="11" />
                                <TextBox Grid.Column="1" Text="{Binding RefRx, StringFormat=F3, UpdateSourceTrigger=PropertyChanged}"
                                         Style="{StaticResource MaterialDesignOutlinedTextBox}" Padding="4,2" Margin="2,0,4,0" />
                                <TextBlock Grid.Column="2" Text="Rz" VerticalAlignment="Center" FontSize="11" />
                                <TextBox Grid.Column="3" Text="{Binding RefRz, StringFormat=F3, UpdateSourceTrigger=PropertyChanged}"
                                         Style="{StaticResource MaterialDesignOutlinedTextBox}" Padding="4,2" Margin="2,0,4,0" />
                                <Button Grid.Column="4" Command="{Binding TeachReferenceCommand}"
                                        Style="{StaticResource MaterialDesignOutlinedButton}" Padding="6,2" Margin="0,0,4,0"
                                        ToolTip="{lang:Lang ProductCalib_Teach}">
                                    <materialDesign:PackIcon Kind="Crosshairs" Width="14" Height="14" />
                                </Button>
                                <Button Grid.Column="5" Command="{Binding MoveToReferenceCommand}"
                                        Style="{StaticResource MaterialDesignOutlinedButton}" Padding="6,2"
                                        ToolTip="{lang:Lang ProductCalib_Move}">
                                    <materialDesign:PackIcon Kind="ArrowRight" Width="14" Height="14" />
                                </Button>
                            </Grid>
                        </StackPanel>
                    </materialDesign:Card>

                    <!-- 相机配置卡片 -->
                    <materialDesign:Card UniformCornerRadius="8" Padding="16" Margin="0,0,0,10">
                        <StackPanel>
                            <StackPanel Style="{StaticResource CardHeaderStyle}">
                                <materialDesign:PackIcon Kind="Camera" Width="18" Height="18"
                                                         Foreground="#1565C0" Margin="0,0,8,0" VerticalAlignment="Center" />
                                <TextBlock Text="{lang:Lang ProductCalib_TcpConfig}"
                                           FontWeight="Bold" FontSize="14" Foreground="#1565C0" />
                            </StackPanel>

                            <!-- TCP连接 -->
                            <Grid Margin="0,0,0,6">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>
                                <TextBlock Text="{lang:Lang ProductCalib_Connection}" Style="{StaticResource ParamLabelStyle}" />
                                <ComboBox Grid.Column="1" ItemsSource="{Binding TcpConnections}"
                                          SelectedItem="{Binding SelectedTcpConnection}"
                                          Style="{StaticResource MaterialDesignOutlinedComboBox}" Padding="4,2" />
                            </Grid>

                            <!-- 触发命令 -->
                            <Grid Margin="0,0,0,6">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>
                                <TextBlock Text="{lang:Lang ProductCalib_TriggerCmd}" Style="{StaticResource ParamLabelStyle}" />
                                <TextBox Grid.Column="1" Text="{Binding TriggerCommand, UpdateSourceTrigger=PropertyChanged}"
                                         Style="{StaticResource MaterialDesignOutlinedTextBox}" Padding="4,2" />
                            </Grid>

                            <!-- 超时 -->
                            <Grid Margin="0,0,0,10">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>
                                <TextBlock Text="{lang:Lang ProductCalib_Timeout}" Style="{StaticResource ParamLabelStyle}" />
                                <TextBox Grid.Column="1" Text="{Binding CaptureTimeoutMs, UpdateSourceTrigger=PropertyChanged}"
                                         Style="{StaticResource MaterialDesignOutlinedTextBox}" Padding="4,2" />
                            </Grid>

                            <Separator Margin="0,0,0,10" Background="{DynamicResource MaterialDesignDivider}" />

                            <!-- 数据接收模式切换 -->
                            <TextBlock Text="{lang:Lang ProductCalib_DataReceiveMode}" Style="{StaticResource ParamLabelStyle}" Margin="0,0,0,4" />
                            <StackPanel Orientation="Horizontal" Margin="0,0,0,10">
                                <ToggleButton Command="{Binding ToggleDataReceiveModeCommand}"
                                              IsChecked="{Binding DataReceiveMode, Converter={StaticResource InverseBooleanConverter}, Mode=OneWay}"
                                              Style="{StaticResource MaterialDesignSwitchToggleButton}"
                                              Content="{lang:Lang ProductCalib_AutoReceive}"
                                              IsEnabled="{Binding IsCameraActive}" />
                                <TextBlock Text="{lang:Lang ProductCalib_ManualTrigger}" VerticalAlignment="Center"
                                           Margin="12,0,0,0" FontSize="11" Foreground="#9E9E9E" />
                            </StackPanel>

                            <!-- 相机屏蔽 -->
                            <StackPanel Orientation="Horizontal">
                                <ToggleButton IsChecked="{Binding CameraShielded}"
                                              Style="{StaticResource MaterialDesignSwitchToggleButton}"
                                              Content="{lang:Lang ProductCalib_CameraShielded}" />
                                <materialDesign:PackIcon Kind="ShieldOff" Width="14" Height="14"
                                                         Margin="8,0,0,0" VerticalAlignment="Center"
                                                         Foreground="{Binding CameraShielded, Converter={StaticResource BoolToBrushConverter}}"
                                                         Visibility="{Binding CameraShielded, Converter={StaticResource BoolToVis}}" />
                            </StackPanel>
                        </StackPanel>
                    </materialDesign:Card>

                    <!-- 文件操作卡片 -->
                    <materialDesign:Card UniformCornerRadius="8" Padding="16" Margin="0,0,0,10">
                        <StackPanel>
                            <StackPanel Style="{StaticResource CardHeaderStyle}">
                                <materialDesign:PackIcon Kind="FileDocumentOutline" Width="18" Height="18"
                                                         Foreground="#1565C0" Margin="0,0,8,0" VerticalAlignment="Center" />
                                <TextBlock Text="{lang:Lang ProductCalib_FileOperation}"
                                           FontWeight="Bold" FontSize="14" Foreground="#1565C0" />
                            </StackPanel>

                            <WrapPanel Margin="0,0,0,8">
                                <Button Command="{Binding SaveConfigCommand}"
                                        Style="{StaticResource MaterialDesignOutlinedButton}" Padding="8,4" Margin="0,0,6,6">
                                    <StackPanel Orientation="Horizontal">
                                        <materialDesign:PackIcon Kind="ContentSave" Width="14" Height="14" Margin="0,0,4,0" />
                                        <TextBlock Text="{lang:Lang ProductCalib_Save}" FontSize="11" />
                                    </StackPanel>
                                </Button>
                                <Button Command="{Binding LoadConfigCommand}"
                                        Style="{StaticResource MaterialDesignOutlinedButton}" Padding="8,4" Margin="0,0,6,6">
                                    <StackPanel Orientation="Horizontal">
                                        <materialDesign:PackIcon Kind="FolderOpen" Width="14" Height="14" Margin="0,0,4,0" />
                                        <TextBlock Text="{lang:Lang ProductCalib_Load}" FontSize="11" />
                                    </StackPanel>
                                </Button>
                                <Button Command="{Binding ImportConfigCommand}"
                                        Style="{StaticResource MaterialDesignOutlinedButton}" Padding="8,4" Margin="0,0,6,6">
                                    <StackPanel Orientation="Horizontal">
                                        <materialDesign:PackIcon Kind="Import" Width="14" Height="14" Margin="0,0,4,0" />
                                        <TextBlock Text="{lang:Lang ProductCalib_Import}" FontSize="11" />
                                    </StackPanel>
                                </Button>
                                <Button Command="{Binding ExportConfigCommand}"
                                        Style="{StaticResource MaterialDesignOutlinedButton}" Padding="8,4">
                                    <StackPanel Orientation="Horizontal">
                                        <materialDesign:PackIcon Kind="Export" Width="14" Height="14" Margin="0,0,4,0" />
                                        <TextBlock Text="{lang:Lang ProductCalib_Export}" FontSize="11" />
                                    </StackPanel>
                                </Button>
                            </WrapPanel>

                            <Border Background="#E3F2FD" CornerRadius="4" Padding="8,4"
                                    Visibility="{Binding CurrentFileName, Converter={StaticResource NullToVisibilityConverter}}">
                                <TextBlock Text="{Binding CurrentFileName}" FontSize="11" Foreground="#1565C0"
                                           FontFamily="Consolas" TextWrapping="Wrap" />
                            </Border>
                        </StackPanel>
                    </materialDesign:Card>
                </StackPanel>
            </ScrollViewer>

            <GridSplitter Grid.Column="1" Width="2" Background="#E0E0E0" HorizontalAlignment="Stretch" />

            <!-- ============================================================ -->
            <!-- 右栏：操作区                                                  -->
            <!-- ============================================================ -->
            <ScrollViewer Grid.Column="2" VerticalScrollBarVisibility="Auto"
                          HorizontalScrollBarVisibility="Disabled" Margin="4,8,8,8">
                <StackPanel>

                    <!-- ① 拍照位1 + 拍照位2 -->
                    <materialDesign:Card UniformCornerRadius="8" Padding="16" Margin="0,0,0,10">
                        <StackPanel>
                            <StackPanel Style="{StaticResource CardHeaderStyle}">
                                <materialDesign:PackIcon Kind="CameraBurst" Width="18" Height="18"
                                                         Foreground="{DynamicResource PrimaryHueMidBrush}"
                                                         Margin="0,0,8,0" VerticalAlignment="Center" />
                                <TextBlock Text="{lang:Lang ProductCalib_MotionControl}"
                                           FontWeight="Bold" FontSize="14"
                                           Foreground="{DynamicResource PrimaryHueMidBrush}" />
                            </StackPanel>

                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>

                                <!-- 拍照位1 -->
                                <Border Grid.Column="0" Background="#F5F5F5" CornerRadius="6" Padding="10" Margin="0,0,5,0">
                                    <StackPanel>
                                        <Grid Margin="0,0,0,6">
                                            <Grid.ColumnDefinitions>
                                                <ColumnDefinition Width="Auto" />
                                                <ColumnDefinition Width="*" />
                                                <ColumnDefinition Width="Auto" />
                                            </Grid.ColumnDefinitions>
                                            <TextBlock Text="{lang:Lang ProductCalib_PhotoPosition1}" FontWeight="SemiBold" FontSize="12" Foreground="#1565C0" />
                                            <Ellipse Grid.Column="2" Width="10" Height="10" Margin="6,0,0,0"
                                                       Fill="{Binding Photo1Captured, Converter={StaticResource BoolToBrushConverter}}"
                                                       VerticalAlignment="Center" />
                                        </Grid>
                                        <Grid Margin="0,0,0,4">
                                            <Grid.ColumnDefinitions>
                                                <ColumnDefinition Width="20" />
                                                <ColumnDefinition Width="*" />
                                                <ColumnDefinition Width="20" />
                                                <ColumnDefinition Width="*" />
                                                <ColumnDefinition Width="20" />
                                                <ColumnDefinition Width="*" />
                                                <ColumnDefinition Width="Auto" />
                                            </Grid.ColumnDefinitions>
                                            <TextBlock Text="Dx" VerticalAlignment="Center" FontSize="10" Foreground="#E53935" />
                                            <TextBox Grid.Column="1" Text="{Binding Photo1Dx, StringFormat=F3, UpdateSourceTrigger=PropertyChanged}"
                                                     Style="{StaticResource MaterialDesignOutlinedTextBox}" Padding="3,2" Margin="2,0,3,0" FontSize="11" />
                                            <TextBlock Grid.Column="2" Text="Dy" VerticalAlignment="Center" FontSize="10" Foreground="#43A047" />
                                            <TextBox Grid.Column="3" Text="{Binding Photo1Dy, StringFormat=F3, UpdateSourceTrigger=PropertyChanged}"
                                                     Style="{StaticResource MaterialDesignOutlinedTextBox}" Padding="3,2" Margin="2,0,3,0" FontSize="11" />
                                            <TextBlock Grid.Column="4" Text="Dz" VerticalAlignment="Center" FontSize="10" Foreground="#1E88E5" />
                                            <TextBox Grid.Column="5" Text="{Binding Photo1Dz, StringFormat=F3, UpdateSourceTrigger=PropertyChanged}"
                                                     Style="{StaticResource MaterialDesignOutlinedTextBox}" Padding="3,2" Margin="2,0,3,0" FontSize="11" />
                                            <Button Grid.Column="6" Command="{Binding TeachPhoto1Command}"
                                                    Style="{StaticResource MaterialDesignOutlinedButton}" Padding="4,2"
                                                    ToolTip="{lang:Lang ProductCalib_Teach}">
                                                <materialDesign:PackIcon Kind="Crosshairs" Width="12" Height="12" />
                                            </Button>
                                        </Grid>
                                        <Grid Margin="0,0,0,4">
                                            <Grid.ColumnDefinitions>
                                                <ColumnDefinition Width="Auto" />
                                                <ColumnDefinition Width="Auto" />
                                                <ColumnDefinition Width="16" />
                                                <ColumnDefinition Width="Auto" />
                                                <ColumnDefinition Width="*" />
                                                <ColumnDefinition Width="Auto" />
                                                <ColumnDefinition Width="Auto" />
                                            </Grid.ColumnDefinitions>
                                            <TextBlock Text="{lang:Lang ProductCalib_VisionResult}" VerticalAlignment="Center"
                                                       FontSize="10" Foreground="Gray" Margin="0,0,4,0" />
                                            <TextBlock Grid.Column="1" VerticalAlignment="Center" FontSize="10">
                                                <Run Text="X:" /><Run Text="{Binding Photo1VisionX, StringFormat=F3}" Foreground="#E53935" />
                                            </TextBlock>
                                            <TextBlock Grid.Column="3" VerticalAlignment="Center" FontSize="10">
                                                <Run Text="Y:" /><Run Text="{Binding Photo1VisionY, StringFormat=F3}" Foreground="#43A047" />
                                            </TextBlock>
                                            <Button Grid.Column="5" Command="{Binding MoveToPhoto1Command}"
                                                    Style="{StaticResource MaterialDesignOutlinedButton}" Padding="4,2" Margin="0,0,3,0">
                                                <materialDesign:PackIcon Kind="ArrowRight" Width="12" Height="12" />
                                            </Button>
                                            <Button Grid.Column="6" Command="{Binding Capture1Command}"
                                                    Style="{StaticResource MaterialDesignRaisedButton}" Padding="4,2"
                                                    IsEnabled="{Binding IsCameraActive}">
                                                <StackPanel Orientation="Horizontal">
                                                    <materialDesign:PackIcon Kind="Camera" Width="12" Height="12" Margin="0,0,3,0" />
                                                    <TextBlock Text="{lang:Lang ProductCalib_Capture}" FontSize="10" />
                                                </StackPanel>
                                            </Button>
                                        </Grid>
                                    </StackPanel>
                                </Border>

                                <!-- 拍照位2 -->
                                <Border Grid.Column="1" Background="#F5F5F5" CornerRadius="6" Padding="10" Margin="5,0,0,0">
                                    <StackPanel>
                                        <Grid Margin="0,0,0,6">
                                            <Grid.ColumnDefinitions>
                                                <ColumnDefinition Width="Auto" />
                                                <ColumnDefinition Width="*" />
                                                <ColumnDefinition Width="Auto" />
                                            </Grid.ColumnDefinitions>
                                            <TextBlock Text="{lang:Lang ProductCalib_PhotoPosition2}" FontWeight="SemiBold" FontSize="12" Foreground="#6A1B9A" />
                                            <Ellipse Grid.Column="2" Width="10" Height="10" Margin="6,0,0,0"
                                                       Fill="{Binding Photo2Captured, Converter={StaticResource BoolToBrushConverter}}"
                                                       VerticalAlignment="Center" />
                                        </Grid>
                                        <Grid Margin="0,0,0,4">
                                            <Grid.ColumnDefinitions>
                                                <ColumnDefinition Width="20" />
                                                <ColumnDefinition Width="*" />
                                                <ColumnDefinition Width="20" />
                                                <ColumnDefinition Width="*" />
                                                <ColumnDefinition Width="20" />
                                                <ColumnDefinition Width="*" />
                                                <ColumnDefinition Width="Auto" />
                                            </Grid.ColumnDefinitions>
                                            <TextBlock Text="Dx" VerticalAlignment="Center" FontSize="10" Foreground="#E53935" />
                                            <TextBox Grid.Column="1" Text="{Binding Photo2Dx, StringFormat=F3, UpdateSourceTrigger=PropertyChanged}"
                                                     Style="{StaticResource MaterialDesignOutlinedTextBox}" Padding="3,2" Margin="2,0,3,0" FontSize="11" />
                                            <TextBlock Grid.Column="2" Text="Dy" VerticalAlignment="Center" FontSize="10" Foreground="#43A047" />
                                            <TextBox Grid.Column="3" Text="{Binding Photo2Dy, StringFormat=F3, UpdateSourceTrigger=PropertyChanged}"
                                                     Style="{StaticResource MaterialDesignOutlinedTextBox}" Padding="3,2" Margin="2,0,3,0" FontSize="11" />
                                            <TextBlock Grid.Column="4" Text="Dz" VerticalAlignment="Center" FontSize="10" Foreground="#1E88E5" />
                                            <TextBox Grid.Column="5" Text="{Binding Photo2Dz, StringFormat=F3, UpdateSourceTrigger=PropertyChanged}"
                                                     Style="{StaticResource MaterialDesignOutlinedTextBox}" Padding="3,2" Margin="2,0,3,0" FontSize="11" />
                                            <Button Grid.Column="6" Command="{Binding TeachPhoto2Command}"
                                                    Style="{StaticResource MaterialDesignOutlinedButton}" Padding="4,2"
                                                    ToolTip="{lang:Lang ProductCalib_Teach}">
                                                <materialDesign:PackIcon Kind="Crosshairs" Width="12" Height="12" />
                                            </Button>
                                        </Grid>
                                        <Grid Margin="0,0,0,4">
                                            <Grid.ColumnDefinitions>
                                                <ColumnDefinition Width="Auto" />
                                                <ColumnDefinition Width="Auto" />
                                                <ColumnDefinition Width="16" />
                                                <ColumnDefinition Width="Auto" />
                                                <ColumnDefinition Width="*" />
                                                <ColumnDefinition Width="Auto" />
                                                <ColumnDefinition Width="Auto" />
                                            </Grid.ColumnDefinitions>
                                            <TextBlock Text="{lang:Lang ProductCalib_VisionResult}" VerticalAlignment="Center"
                                                       FontSize="10" Foreground="Gray" Margin="0,0,4,0" />
                                            <TextBlock Grid.Column="1" VerticalAlignment="Center" FontSize="10">
                                                <Run Text="X:" /><Run Text="{Binding Photo2VisionX, StringFormat=F3}" Foreground="#E53935" />
                                            </TextBlock>
                                            <TextBlock Grid.Column="3" VerticalAlignment="Center" FontSize="10">
                                                <Run Text="Y:" /><Run Text="{Binding Photo2VisionY, StringFormat=F3}" Foreground="#43A047" />
                                            </TextBlock>
                                            <Button Grid.Column="5" Command="{Binding MoveToPhoto2Command}"
                                                    Style="{StaticResource MaterialDesignOutlinedButton}" Padding="4,2" Margin="0,0,3,0">
                                                <materialDesign:PackIcon Kind="ArrowRight" Width="12" Height="12" />
                                            </Button>
                                            <Button Grid.Column="6" Command="{Binding Capture2Command}"
                                                    Style="{StaticResource MaterialDesignRaisedButton}" Padding="4,2"
                                                    IsEnabled="{Binding IsCameraActive}">
                                                <StackPanel Orientation="Horizontal">
                                                    <materialDesign:PackIcon Kind="Camera" Width="12" Height="12" Margin="0,0,3,0" />
                                                    <TextBlock Text="{lang:Lang ProductCalib_Capture}" FontSize="10" />
                                                </StackPanel>
                                            </Button>
                                        </Grid>
                                    </StackPanel>
                                </Border>
                            </Grid>
                        </StackPanel>
                    </materialDesign:Card>

                    <!-- ② 特征点计算结果（Halcon） -->
                    <materialDesign:Card UniformCornerRadius="8" Padding="16" Margin="0,0,0,10">
                        <StackPanel>
                            <StackPanel Style="{StaticResource CardHeaderStyle}">
                                <materialDesign:PackIcon Kind="CalculatorVariant" Width="18" Height="18"
                                                         Foreground="#00897B" Margin="0,0,8,0" VerticalAlignment="Center" />
                                <TextBlock Text="{lang:Lang ProductCalib_FeatureResult}"
                                           FontWeight="Bold" FontSize="14" Foreground="#00897B" />
                                <Button Command="{Binding CalculateCommand}" HorizontalAlignment="Right"
                                        Style="{StaticResource MaterialDesignOutlinedButton}" Padding="6,2"
                                        ToolTip="{lang:Lang ProductCalib_Recalculate}">
                                    <StackPanel Orientation="Horizontal">
                                        <materialDesign:PackIcon Kind="Refresh" Width="12" Height="12" Margin="0,0,4,0" />
                                        <TextBlock Text="{lang:Lang ProductCalib_Recalculate}" FontSize="10" />
                                    </StackPanel>
                                </Button>
                            </StackPanel>

                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>

                                <!-- 中心点 -->
                                <Border Grid.Column="0" Background="#E0F2F1" CornerRadius="6" Padding="10" Margin="0,0,5,0">
                                    <StackPanel>
                                        <TextBlock Text="{lang:Lang ProductCalib_CenterPoint}" FontSize="10" Foreground="#00897B" Margin="0,0,0,4" />
                                        <TextBlock FontSize="13" FontWeight="Bold" Foreground="#00897B">
                                            <Run Text="X:" /><Run Text="{Binding CenterX, StringFormat=F3}" />
                                            <Run Text=" Y:" /><Run Text="{Binding CenterY, StringFormat=F3}" />
                                        </TextBlock>
                                    </StackPanel>
                                </Border>

                                <!-- 角度 -->
                                <Border Grid.Column="1" Background="#E0F2F1" CornerRadius="6" Padding="10" Margin="5,0,5,0">
                                    <StackPanel>
                                        <TextBlock Text="{lang:Lang ProductCalib_Angle}" FontSize="10" Foreground="#00897B" Margin="0,0,0,4" />
                                        <TextBlock Text="{Binding ResultAngle, StringFormat=F3°}" FontSize="13" FontWeight="Bold" Foreground="#00897B" />
                                    </StackPanel>
                                </Border>

                                <!-- 距离 -->
                                <Border Grid.Column="2" Background="#E0F2F1" CornerRadius="6" Padding="10" Margin="5,0,0,0">
                                    <StackPanel>
                                        <TextBlock Text="{lang:Lang ProductCalib_Distance}" FontSize="10" Foreground="#00897B" Margin="0,0,0,4" />
                                        <TextBlock Text="{Binding PointDistance, StringFormat=F3}" FontSize="13" FontWeight="Bold" Foreground="#00897B" />
                                    </StackPanel>
                                </Border>
                            </Grid>
                        </StackPanel>
                    </materialDesign:Card>

                    <!-- ③ 基准与偏差 -->
                    <materialDesign:Card UniformCornerRadius="8" Padding="16" Margin="0,0,0,10">
                        <StackPanel>
                            <StackPanel Style="{StaticResource CardHeaderStyle}">
                                <materialDesign:PackIcon Kind="Compare" Width="18" Height="18"
                                                         Foreground="#FF6F00" Margin="0,0,8,0" VerticalAlignment="Center" />
                                <TextBlock Text="{lang:Lang ProductCalib_ReferenceFeature}"
                                           FontWeight="Bold" FontSize="14" Foreground="#FF6F00" />
                                <Button Command="{Binding SetAsReferenceCommand}" HorizontalAlignment="Right"
                                        Style="{StaticResource MaterialDesignRaisedButton}" Padding="6,2"
                                        Background="#FF6F00" Foreground="White"
                                        ToolTip="{lang:Lang ProductCalib_SetAsReference}">
                                    <StackPanel Orientation="Horizontal">
                                        <materialDesign:PackIcon Kind="BookmarkPlus" Width="12" Height="12" Margin="0,0,4,0" />
                                        <TextBlock Text="{lang:Lang ProductCalib_SetAsReference}" FontSize="10" />
                                    </StackPanel>
                                </Button>
                            </StackPanel>

                            <!-- 基准输入区 -->
                            <Border Background="#FFF3E0" CornerRadius="6" Padding="10" Margin="0,0,0,10">
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="Auto" />
                                        <ColumnDefinition Width="*" />
                                        <ColumnDefinition Width="Auto" />
                                        <ColumnDefinition Width="*" />
                                        <ColumnDefinition Width="Auto" />
                                        <ColumnDefinition Width="*" />
                                    </Grid.ColumnDefinitions>
                                    <TextBlock Text="{lang:Lang ProductCalib_ReferenceCenter}" VerticalAlignment="Center" FontSize="11" Foreground="#FF6F00" Margin="0,0,6,0" />
                                    <TextBox Grid.Column="1" Text="{Binding ReferenceCenterX, StringFormat=F3, UpdateSourceTrigger=PropertyChanged}"
                                             Style="{StaticResource MaterialDesignOutlinedTextBox}" Padding="4,2" Margin="0,0,8,0" FontSize="11" />
                                    <TextBlock Grid.Column="2" Text="Y:" VerticalAlignment="Center" FontSize="11" Foreground="#FF6F00" Margin="0,0,4,0" />
                                    <TextBox Grid.Column="3" Text="{Binding ReferenceCenterY, StringFormat=F3, UpdateSourceTrigger=PropertyChanged}"
                                             Style="{StaticResource MaterialDesignOutlinedTextBox}" Padding="4,2" Margin="0,0,8,0" FontSize="11" />
                                    <TextBlock Grid.Column="4" Text="{lang:Lang ProductCalib_ReferenceAngle}" VerticalAlignment="Center" FontSize="11" Foreground="#FF6F00" Margin="0,0,4,0" />
                                    <TextBox Grid.Column="5" Text="{Binding ReferenceAngle, StringFormat=F3, UpdateSourceTrigger=PropertyChanged}"
                                             Style="{StaticResource MaterialDesignOutlinedTextBox}" Padding="4,2" FontSize="11" />
                                </Grid>
                            </Border>

                            <!-- 偏差结果（带取反开关） -->
                            <TextBlock Text="{lang:Lang ProductCalib_Deviation}" Style="{StaticResource ParamLabelStyle}" Margin="0,0,0,6" />
                            <Grid Margin="0,0,0,10">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>

                                <!-- ΔX -->
                                <Border Grid.Column="0" Background="#FFEBEE" CornerRadius="6" Padding="8" Margin="0,0,4,0">
                                    <StackPanel>
                                        <Grid>
                                            <Grid.ColumnDefinitions>
                                                <ColumnDefinition Width="Auto" />
                                                <ColumnDefinition Width="*" />
                                                <ColumnDefinition Width="Auto" />
                                            </Grid.ColumnDefinitions>
                                            <TextBlock Text="ΔX" VerticalAlignment="Center" FontSize="12" FontWeight="Bold" Foreground="#E53935" />
                                            <TextBlock Grid.Column="1" Text="{Binding DeltaX, StringFormat=F3}" VerticalAlignment="Center"
                                                       FontSize="13" FontWeight="Bold" Foreground="#E53935" Margin="6,0,0,0" />
                                            <ToggleButton Grid.Column="2" IsChecked="{Binding InvertDeltaX}"
                                                          Style="{StaticResource MaterialDesignSwitchToggleButton}"
                                                          ToolTip="{lang:Lang ProductCalib_Invert}" />
                                        </Grid>
                                        <common:GlobalVariableLinkControl
                                            DisplayValue="{Binding DeltaX}" DisplayForeground="#E53935"
                                            IsLinked="{Binding IsDeltaXLinked}" UnlinkCommand="{Binding UnlinkDeltaXCommand}"
                                            LinkedVariableName="{Binding DeltaXLinkedVar, UpdateSourceTrigger=PropertyChanged}"
                                            LinkableGlobalVariables="{Binding LinkableGlobalVariables}" ComboBoxWidth="90" />
                                    </StackPanel>
                                </Border>

                                <!-- ΔY -->
                                <Border Grid.Column="1" Background="#E8F5E9" CornerRadius="6" Padding="8" Margin="4,0,4,0">
                                    <StackPanel>
                                        <Grid>
                                            <Grid.ColumnDefinitions>
                                                <ColumnDefinition Width="Auto" />
                                                <ColumnDefinition Width="*" />
                                                <ColumnDefinition Width="Auto" />
                                            </Grid.ColumnDefinitions>
                                            <TextBlock Text="ΔY" VerticalAlignment="Center" FontSize="12" FontWeight="Bold" Foreground="#43A047" />
                                            <TextBlock Grid.Column="1" Text="{Binding DeltaY, StringFormat=F3}" VerticalAlignment="Center"
                                                       FontSize="13" FontWeight="Bold" Foreground="#43A047" Margin="6,0,0,0" />
                                            <ToggleButton Grid.Column="2" IsChecked="{Binding InvertDeltaY}"
                                                          Style="{StaticResource MaterialDesignSwitchToggleButton}"
                                                          ToolTip="{lang:Lang ProductCalib_Invert}" />
                                        </Grid>
                                        <common:GlobalVariableLinkControl
                                            DisplayValue="{Binding DeltaY}" DisplayForeground="#43A047"
                                            IsLinked="{Binding IsDeltaYLinked}" UnlinkCommand="{Binding UnlinkDeltaYCommand}"
                                            LinkedVariableName="{Binding DeltaYLinkedVar, UpdateSourceTrigger=PropertyChanged}"
                                            LinkableGlobalVariables="{Binding LinkableGlobalVariables}" ComboBoxWidth="90" />
                                    </StackPanel>
                                </Border>

                                <!-- ΔAngle -->
                                <Border Grid.Column="2" Background="#F3E5F5" CornerRadius="6" Padding="8" Margin="4,0,0,0">
                                    <StackPanel>
                                        <Grid>
                                            <Grid.ColumnDefinitions>
                                                <ColumnDefinition Width="Auto" />
                                                <ColumnDefinition Width="*" />
                                                <ColumnDefinition Width="Auto" />
                                            </Grid.ColumnDefinitions>
                                            <TextBlock Text="ΔA" VerticalAlignment="Center" FontSize="12" FontWeight="Bold" Foreground="#6A1B9A" />
                                            <TextBlock Grid.Column="1" Text="{Binding DeltaAngle, StringFormat=F3}" VerticalAlignment="Center"
                                                       FontSize="13" FontWeight="Bold" Foreground="#6A1B9A" Margin="6,0,0,0" />
                                            <ToggleButton Grid.Column="2" IsChecked="{Binding InvertDeltaAngle}"
                                                          Style="{StaticResource MaterialDesignSwitchToggleButton}"
                                                          ToolTip="{lang:Lang ProductCalib_Invert}" />
                                        </Grid>
                                        <common:GlobalVariableLinkControl
                                            DisplayValue="{Binding DeltaAngle}" DisplayForeground="#6A1B9A"
                                            IsLinked="{Binding IsDeltaAngleLinked}" UnlinkCommand="{Binding UnlinkDeltaAngleCommand}"
                                            LinkedVariableName="{Binding DeltaAngleLinkedVar, UpdateSourceTrigger=PropertyChanged}"
                                            LinkableGlobalVariables="{Binding LinkableGlobalVariables}" ComboBoxWidth="90" />
                                    </StackPanel>
                                </Border>
                            </Grid>
                        </StackPanel>
                    </materialDesign:Card>

                    <!-- ④ 旋转校正 -->
                    <materialDesign:Card UniformCornerRadius="8" Padding="16" Margin="0,0,0,10">
                        <StackPanel>
                            <StackPanel Style="{StaticResource CardHeaderStyle}">
                                <materialDesign:PackIcon Kind="RotateRight" Width="18" Height="18"
                                                         Foreground="#5E35B1" Margin="0,0,8,0" VerticalAlignment="Center" />
                                <TextBlock Text="{lang:Lang ProductCalib_RotateCorrection}"
                                           FontWeight="Bold" FontSize="14" Foreground="#5E35B1" />
                            </StackPanel>
                            <TextBlock Text="{lang:Lang ProductCalib_RotateDescription}"
                                       FontSize="11" Foreground="Gray" Margin="0,0,0,10" TextWrapping="Wrap" />
                            <Button Command="{Binding RotateCommand}"
                                    Style="{StaticResource MaterialDesignRaisedButton}" Padding="12,6"
                                    Background="#5E35B1" Foreground="White" HorizontalAlignment="Left">
                                <StackPanel Orientation="Horizontal">
                                    <materialDesign:PackIcon Kind="RotateRight" Width="16" Height="16" Margin="0,0,6,0" />
                                    <TextBlock Text="{lang:Lang ProductCalib_Rotate}" FontSize="12" />
                                </StackPanel>
                            </Button>
                        </StackPanel>
                    </materialDesign:Card>
                </StackPanel>
            </ScrollViewer>
        </Grid>

        <!-- 底部状态栏 -->
        <Border Grid.Row="1" Background="{Binding StatusColor}" CornerRadius="4" Padding="10,6" Margin="8,4,8,8">
            <TextBlock Text="{Binding StatusText}" FontSize="12" Foreground="White" FontWeight="Medium" />
        </Border>
    </Grid>
</UserControl>
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build Module/Module.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: 提交**

```bash
git add Module/Controls/Loading/ProductCalibrationView.xaml
git commit -m "feat(module): 重写ProductCalibrationView为左右分栏布局，新增特征点计算/基准偏差/取反开关/相机屏蔽/模式切换区域"
```

---

### Task 7: 追加版本修改记录

**Files:**
- Modify: `MainApp/bin/Debug/net9.0-windows7.0/版本修改记录.txt`

- [ ] **Step 1: 在版本修改记录.txt 末尾追加本次修改记录**

```text

========================================
[版本号]  产品对齐校准页面增强
日期: 2026-06-25
========================================
1. 新增相机屏蔽功能：屏蔽相机后可手动输入特征点坐标
2. 新增数据接收模式切换：手动触发拍照 / 自动接收TCP推送数据
3. 新增基准特征点+基准角度设置：支持手动输入或"设为基准"按钮从当前计算结果填入
4. 新增Halcon算子计算：使用 angle_ll 计算角度、distance_pp 计算距离，角度归一化到[-180,180]
5. 新增偏差取反开关：ΔX/ΔY/ΔAngle 每项独立取反
6. 新增偏差与基准对比：计算当前特征点中心与基准点的偏差
7. 重写UI布局：参考双龙门标定左右分栏风格（左配置+右操作）
8. 多语言：新增23个ProductCalib_* Key（zh-CN/en-US同步）
9. 配置持久化：新增字段完整保存/加载

影响文件:
- Core/Models/StageCalibrationData.cs (扩展模型)
- Core/Abstraction/IStageCalibrationService.cs (扩展接口)
- Module/Services/StageCalibrationService.cs (实现Halcon计算+TCP订阅)
- Module/Controls/Loading/ProductCalibrationViewModel.cs (扩展属性/命令/逻辑)
- Module/Controls/Loading/ProductCalibrationView.xaml (重写布局)
- MainApp/Languages/Strings.zh-CN.xaml (多语言)
- MainApp/Languages/Strings.en-US.xaml (多语言)
```

- [ ] **Step 2: 提交**

```bash
git add MainApp/bin/Debug/net9.0-windows7.0/版本修改记录.txt
git commit -m "docs: 追加产品对齐校准页面增强版本记录"
```

---

### Task 8: 整体编译验证与集成测试

- [ ] **Step 1: 整体编译**

Run: `dotnet build GZQL_MACHINE.sln`
Expected: BUILD SUCCEEDED，无错误

- [ ] **Step 2: 检查多语言Key完整性**

验证 zh-CN 和 en-US 的 ProductCalib_* Key 集合完全一致（0重复、0缺失）。

- [ ] **Step 3: 运行时验证清单**

启动应用后从 LoadUnload 页面点击"载台对齐"按钮打开 ProductCalibrationView，验证：
1. 左右分栏布局正确渲染，无空白
2. 相机屏蔽开关切换时，拍照按钮启用/禁用正确
3. 模式切换开关切换时，状态栏提示正确
4. 两次拍照后自动计算中心点/角度/距离
5. "设为基准"按钮将当前计算结果填入基准输入框
6. 取反开关切换时偏差符号正确翻转
7. 全局变量链接控件正常显示
8. 保存/加载配置后新字段完整恢复
9. 中英文切换所有文本更新

- [ ] **Step 4: 最终提交（如有修复）**

```bash
git add -A
git commit -m "fix: 整体编译验证与集成测试修复"
```

---

## 自检清单

### Spec 覆盖验证
- [x] 相机屏蔽 → Task 5 Step 2 + Task 6 相机屏蔽区域
- [x] 模式切换（手动触发/自动接收）→ Task 5 Step 14 + Task 6 模式切换区域
- [x] 手动输入基准值和角度 → Task 6 基准输入区（TextBox 直接绑定）
- [x] 自动接收解析数据 → Task 3 Step 4 + Task 5 Step 14
- [x] Halcon算子计算中心点和角度 → Task 3 Step 3
- [x] 角度归一化 → Task 3 Step 3 NormalizeAngle + Task 5 Step 13
- [x] 设置基准和角度 → Task 5 Step 11 ExecuteSetAsReference
- [x] 计算当前特征点与基准点的偏差 → Task 5 Step 13 CalculateDeviationFromReference
- [x] 取反开关（每项独立）→ Task 5 Step 5 + Task 6 ΔX/ΔY/ΔAngle ToggleButton
- [x] 结果可保存 → Task 5 Step 15 BuildCurrentConfig/LoadConfigFromFileAsync
- [x] 参考维护页面双龙门标定 → Task 6 左右分栏布局
- [x] 偏差可链接全局变量 → Task 6 GlobalVariableLinkControl
- [x] 此页面放在loadunload页面打开 → 已有 OpenStageAlignCommand（无需改动）

### 类型一致性验证
- [x] `CalculateCenterAndAngleWithHalcon` 方法名在接口、实现、ViewModel 调用一致
- [x] `ProductAlignResult` 类名在模型、服务、ViewModel 一致
- [x] `DataReceiveMode` 枚举名在模型、ViewModel 一致
- [x] `SubscribeCameraData`/`UnsubscribeCameraData` 方法名一致
- [x] `SetAutoReceiveFeatureIndex` 方法名在服务和 ViewModel 一致
