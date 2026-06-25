# 产品对齐校准页面（Product Align）设计文档

## Why

现有 `ProductCalibrationView` 已实现基础两次拍照流程，但缺少以下关键能力：
- 相机屏蔽（禁用相机后手动输入特征点）
- 数据接收模式切换（手动触发 vs 自动接收 TCP 推送）
- 基准特征点+基准角度设置（当前仅计算两次拍照间偏差，无基准对比）
- Halcon 算子计算中心点与角度（当前使用简单 atan2）
- 角度归一化
- 偏差取反开关（每项独立）
- 偏差与基准点对比

本次设计在现有视图上增强，重新设计 UI 布局（参考维护页双龙门标定），补齐上述能力，使页面满足产品对齐校准的完整工艺需求。

## What Changes

- 重写 `Module/Controls/Loading/ProductCalibrationView.xaml`：左右分栏布局（左配置 + 右操作）
- 扩展 `Module/Controls/Loading/ProductCalibrationViewModel.cs`：新增相机屏蔽、模式切换、基准特征点、取反开关、Halcon 计算结果等属性与命令
- 扩展 `Core/Abstraction/IStageCalibrationService.cs`：新增 `CalculateCenterAndAngleWithHalcon` + `SubscribeCameraData`/`UnsubscribeCameraData`
- 实现 `Module/Services/StageCalibrationService.cs`：Halcon 计算（`angle_ll`/`distance_pp`）+ TCP 事件订阅
- 扩展 `Core/Models/StageCalibrationData.cs`：`StageCalibrationConfig` 新增字段 + 新增 `ProductAlignResult` 模型
- 扩展 `MainApp/Languages/Strings.zh-CN.xaml` 和 `Strings.en-US.xaml`：新增 `ProductCalib_*` 多语言 Key
- 追加 `MainApp/bin/Debug/net9.0-windows7.0/版本修改记录.txt`

## Impact

- Affected code:
  - `Module/Controls/Loading/ProductCalibrationView.xaml` (重写)
  - `Module/Controls/Loading/ProductCalibrationViewModel.cs` (扩展)
  - `Core/Abstraction/IStageCalibrationService.cs` (扩展接口)
  - `Module/Services/StageCalibrationService.cs` (实现新方法)
  - `Core/Models/StageCalibrationData.cs` (扩展模型)
  - `MainApp/Languages/Strings.zh-CN.xaml` (多语言)
  - `MainApp/Languages/Strings.en-US.xaml` (多语言)
  - `MainApp/bin/Debug/net9.0-windows7.0/版本修改记录.txt` (版本记录)

---

## 架构设计

### 方案选择：最小化增强（Approach 1）

在现有 `ProductCalibrationView`/`ViewModel` 上扩展，Halcon 计算作为 `IStageCalibrationService` 新方法，保持领域服务统一。复用现有 `GlobalVariableLinkControl` 和 TCP 基础设施。

### 依赖关系（无倒置依赖）

```
Module (ViewModel/View)
  → Core (IStageCalibrationService 接口, Models)
  → HalconWrapper (Halcon 算子)
  → TCPIPModule (ITCPEventService)
  → ModuleCore (GlobalVariableLinkControl)
```

---

## 数据模型扩展

### Core/Models/StageCalibrationData.cs

```csharp
/// <summary>载台校准完整配置数据（扩展）</summary>
public class StageCalibrationConfig
{
    // === 现有字段保留 ===
    public StageReferencePosition ReferencePosition { get; set; } = new();
    public StagePhotoPosition PhotoPosition1 { get; set; } = new() { Name = "Photo1" };
    public StagePhotoPosition PhotoPosition2 { get; set; } = new() { Name = "Photo2" };
    public int CaptureTimeoutMs { get; set; } = 5000;
    public string TcpConnectionName { get; set; } = string.Empty;
    public string TriggerCommand { get; set; } = string.Empty;
    public string DeltaXLinkedVar { get; set; } = string.Empty;
    public string DeltaYLinkedVar { get; set; } = string.Empty;
    public string DeltaAngleLinkedVar { get; set; } = string.Empty;
    public string LastFileName { get; set; } = string.Empty;

    // === 新增字段 ===
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
}

/// <summary>Halcon计算结果：中心点+角度+距离</summary>
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
```

### 数据接收模式枚举

```csharp
namespace Core.Models
{
    /// <summary>数据接收模式</summary>
    public enum DataReceiveMode
    {
        /// <summary>手动触发拍照</summary>
        ManualTrigger,
        /// <summary>自动接收TCP推送</summary>
        AutoReceive
    }
}
```

---

## 服务接口扩展

### Core/Abstraction/IStageCalibrationService.cs

```csharp
public interface IStageCalibrationService
{
    // === 现有方法保留 ===
    Task MoveToReferencePositionAsync(double rx, double rz);
    Task MoveCameraToPhotoPositionAsync(double dx, double dy, double dz);
    Task<FiducialCaptureResult> TriggerCaptureAsync(string tcpConnectionName, string triggerCommand, int timeoutMs);
    Task RotateToReferenceAngleAsync(double currentRz, double deltaAngle);
    Task<CurrentPositionResult> ReadCurrentPositionsAsync();

    // === 新增：Halcon 计算 ===
    /// <summary>使用 Halcon 算子计算两个特征点的中心点、角度（归一化[-180,180]）、距离</summary>
    /// <param name="p1X">特征点1机械坐标X</param>
    /// <param name="p1Y">特征点1机械坐标Y</param>
    /// <param name="p2X">特征点2机械坐标X</param>
    /// <param name="p2Y">特征点2机械坐标Y</param>
    ProductAlignResult CalculateCenterAndAngleWithHalcon(double p1X, double p1Y, double p2X, double p2Y);

    // === 新增：自动接收模式 TCP 订阅 ===
    /// <summary>订阅相机数据自动接收（解析 TCP 推送的视觉坐标）</summary>
    /// <param name="tcpConnectionName">TCP连接名</param>
    /// <param name="onDataReceived">数据回调（featureIndex 1或2, X, Y）</param>
    void SubscribeCameraData(string tcpConnectionName, Action<int, double, double> onDataReceived);

    /// <summary>取消订阅相机数据</summary>
    void UnsubscribeCameraData();
}
```

### Halcon 计算实现要点

```csharp
public ProductAlignResult CalculateCenterAndAngleWithHalcon(double p1X, double p1Y, double p2X, double p2Y)
{
    // 1. 中心点：两点的中点
    double centerX = (p1X + p2X) / 2.0;
    double centerY = (p1Y + p2Y) / 2.0;

    // 2. 距离：Halcon distance_pp（注意 Halcon 坐标系 Row=Y, Col=X）
    HOperatorSet.DistancePp(p1Y, p1X, p2Y, p2X, out HTuple distance);

    // 3. 角度：Halcon angle_ll（P1→P2 与 X 轴正方向夹角）
    //    第二条线为 X 轴正方向 (0,0)→(0,1)
    HOperatorSet.AngleLl(p1Y, p1X, p2Y, p2X, 0, 0, 0, 1, out HTuple angle);
    double angleDeg = angle.D * 180.0 / Math.PI;

    return new ProductAlignResult
    {
        CenterX = centerX,
        CenterY = centerY,
        AngleDeg = NormalizeAngle(angleDeg),
        Distance = distance.D
    };
}

/// <summary>角度归一化到 [-180, 180]</summary>
private static double NormalizeAngle(double angleDeg)
{
    while (angleDeg > 180) angleDeg -= 360;
    while (angleDeg <= -180) angleDeg += 360;
    return angleDeg;
}
```

### 自动接收订阅实现要点

- 通过 `ITCPEventService` 订阅指定连接的相机消息事件
- 解析消息格式（与 `TriggerCaptureAsync` 返回的 `FiducialCaptureResult` 一致：X, Y, 可选 Angle）
- 回调 `onDataReceived(featureIndex, X, Y)`，featureIndex 由调用方上下文决定（1 或 2）
- `UnsubscribeCameraData` 取消订阅，避免内存泄漏

---

## UI 布局设计

### 整体结构（参考 DualGantryCalibrationView 风格）

```
┌─────────────────────────────────────────────────────────────────────┐
│ Header: [PackIcon] 产品对齐校准  |  状态指示 Pill                    │
├───────────────────┬─────────────────────────────────────────────────┤
│ LEFT (配置栏)      │ RIGHT (操作区，ScrollViewer)                    │
│ Width: 320         │                                                 │
│                   │ ┌─────────────────────────────────────────────┐ │
│ ┌───────────────┐ │ │ ① 拍照位1 + 拍照位2 (左右双栏)               │ │
│ │ 机构配置       │ │ │   拍照基准位 Rx/Rz 示教/移动                 │ │
│ │ - 拍照基准位   │ │ │   拍照位1: Dx/Dy/Dz 示教/移动/视觉X/Y/拍照   │ │
│ │   Rx/Rz        │ │ │   拍照位2: Dx/Dy/Dz 示教/移动/视觉X/Y/拍照   │ │
│ └───────────────┘ │ └─────────────────────────────────────────────┘ │
│                   │                                                 │
│ ┌───────────────┐ │ ┌─────────────────────────────────────────────┐ │
│ │ 相机配置       │ │ │ ② 特征点计算结果 (Halcon)                    │ │
│ │ - TCP连接      │ │ │   中心点 X / Y | 角度(归一化) | 距离         │ │
│ │ - 触发命令     │ │ │   [重新计算]                                 │ │
│ │ - 超时         │ │ └─────────────────────────────────────────────┘ │
│ │ - 模式切换     │ │                                                 │
│ │   [手动|自动]  │ │ ┌─────────────────────────────────────────────┐ │
│ │ - 相机屏蔽☐    │ │ │ ③ 基准与偏差                                 │ │
│ └───────────────┘ │ │   基准中心 X/Y | 基准角度 [设为基准][手动输入] │ │
│                   │ │   偏差 ΔX [取反☐] ΔY [取反☐] ΔAngle [取反☐]  │ │
│ ┌───────────────┐ │ │   全局变量链接: ΔX/ΔY/ΔAngle                 │ │
│ │ 文件操作       │ │ └─────────────────────────────────────────────┘ │
│ │ - 保存/加载    │ │                                                 │
│ │ - 导入/导出    │ │ ┌─────────────────────────────────────────────┐ │
│ │ - 当前文件名   │ │ │ ④ 旋转校正 (根据ΔAngle旋转Rz轴)              │ │
│ └───────────────┘ │ └─────────────────────────────────────────────┘ │
├───────────────────┴─────────────────────────────────────────────────┤
│ Status Bar: 状态文本 (颜色区分：成功绿/警告橙/错误红/信息蓝)          │
└─────────────────────────────────────────────────────────────────────┘
```

### 配色规范

| 区域 | 颜色 | 用途 |
|------|------|------|
| 拍照位1 | `#1565C0`（蓝） | 标题、强调 |
| 拍照位2 | `#6A1B9A`（紫） | 标题、强调 |
| 特征点计算 | `#00897B`（青） | 标题、强调 |
| 基准与偏差 | `#FF6F00`（橙） | 标题、强调 |
| 旋转校正 | `#5E35B1`（深紫） | 标题、强调 |
| 已拍照指示 | `#43A047`（绿） | 状态灯 |
| 未拍照指示 | `#BDBDBD`（灰） | 状态灯 |
| 错误状态 | `#E53935`（红） | 状态栏 |

### 关键交互

#### ① 拍照位区域
- 拍照基准位（Rx/Rz）：示教按钮读取当前轴位置，移动按钮定位
- 拍照位1/2（Dx/Dy/Dz）：示教/移动/拍照
- 视觉X/Y：相机未屏蔽时为只读显示，屏蔽时为可输入文本框
- 拍照状态指示灯（绿色=已拍照）
- 相机屏蔽开启时：拍照按钮禁用，视觉X/Y 变为可输入

#### ② 特征点计算结果
- 两次拍照完成后自动计算
- 中心点 = (P1+P2)/2
- 角度使用 Halcon `angle_ll`，归一化到 [-180, 180]
- 距离使用 Halcon `distance_pp`
- "重新计算"按钮手动触发

#### ③ 基准与偏差
- 基准中心 X/Y + 基准角度：可手动输入，或"设为基准"按钮从当前计算结果填入
- 偏差计算：`ΔX = (CenterX - RefCenterX) × (InvertDeltaX ? -1 : 1)`
- 每个偏差项有独立取反 CheckBox
- 偏差值通过 `GlobalVariableLinkControl` 链接全局变量
- 取反开关变更时重新计算偏差并刷新全局变量

#### 模式切换
- ToggleButton 组：手动触发 / 自动接收
- 自动接收模式：订阅 TCP `CameraMessageReceived`，解析后自动填充当前选中拍照位视觉结果，两次都收到后自动计算
- 手动触发模式：点击拍照按钮发送触发命令

---

## ViewModel 设计

### 新增属性

```csharp
// === 相机屏蔽与模式 ===
public bool CameraShielded { get; set; }
public DataReceiveMode DataReceiveMode { get; set; }

// === 特征点计算结果 ===
public double CenterX { get; set; }
public double CenterY { get; set; }
public double ResultAngle { get; set; }      // 归一化角度
public double PointDistance { get; set; }

// === 基准特征点 ===
public double ReferenceCenterX { get; set; }
public double ReferenceCenterY { get; set; }
public double ReferenceAngle { get; set; }

// === 偏差结果（已应用取反） ===
public double DeltaX { get; set; }
public double DeltaY { get; set; }
public double DeltaAngle { get; set; }

// === 取反开关（每项独立） ===
public bool InvertDeltaX { get; set; }
public bool InvertDeltaY { get; set; }
public bool InvertDeltaAngle { get; set; }
```

### 新增命令

```csharp
public DelegateCommand SetAsReferenceCommand { get; }        // 设为基准（当前计算结果→基准）
public DelegateCommand CalculateCommand { get; }              // 重新计算（Halcon）
public DelegateCommand ToggleDataReceiveModeCommand { get; }  // 切换接收模式
```

### 偏差计算逻辑

```csharp
/// <summary>计算当前特征点与基准点的偏差（应用取反开关）</summary>
private void CalculateDeviationFromReference()
{
    double rawDeltaX = CenterX - ReferenceCenterX;
    double rawDeltaY = CenterY - ReferenceCenterY;
    double rawDeltaAngle = NormalizeAngle(ResultAngle - ReferenceAngle);

    DeltaX = InvertDeltaX ? -rawDeltaX : rawDeltaX;
    DeltaY = InvertDeltaY ? -rawDeltaY : rawDeltaY;
    DeltaAngle = InvertDeltaAngle ? -rawDeltaAngle : rawDeltaAngle;
}
```

### 自动接收模式数据流

```
TCP 推送视觉数据
  → ITCPEventService 事件
  → StageCalibrationService.SubscribeCameraData 解析
  → onDataReceived(featureIndex, X, Y) 回调
  → ViewModel 更新 Photo{featureIndex}VisionX/Y
  → 两次都收到后自动触发 CalculateCenterAndAngleWithHalcon
  → 自动触发 CalculateDeviationFromReference
  → 写入链接的全局变量
```

### 全局变量链接（遵循进化记录 v3 标准模式）

每个偏差项（ΔX/ΔY/ΔAngle）包含三件套：
1. 值属性（DeltaX/DeltaY/DeltaAngle）
2. 链接变量名属性（DeltaXLinkedVar/DeltaYLinkedVar/DeltaAngleLinkedVar）
3. 链接状态属性（IsDeltaXLinked/IsDeltaYLinked/IsDeltaAngleLinked）

- `NormalizeLinkedVarName` 规范化变量名
- `ReadLinkedVariableValue` 从全局变量读值
- 取消链接命令 `UnlinkDeltaXCommand` 等
- 切换行/站点时按链接状态读取
- 视觉拍照后先 `UpdateGlobalVariableValueAsync` 更新全局变量，再回读

---

## 多语言 Key 清单（新增）

前缀 `ProductCalib_`，同步添加到 `Strings.zh-CN.xaml` 和 `Strings.en-US.xaml`：

| Key | 中文 | English |
|-----|------|---------|
| ProductCalib_CameraShielded | 相机屏蔽 | Camera Shielded |
| ProductCalib_DataReceiveMode | 数据接收模式 | Data Receive Mode |
| ProductCalib_ManualTrigger | 手动触发 | Manual Trigger |
| ProductCalib_AutoReceive | 自动接收 | Auto Receive |
| ProductCalib_FeatureResult | 特征点计算结果 | Feature Calculation Result |
| ProductCalib_CenterPoint | 中心点 | Center Point |
| ProductCalib_Angle | 角度 | Angle |
| ProductCalib_Distance | 距离 | Distance |
| ProductCalib_Recalculate | 重新计算 | Recalculate |
| ProductCalib_ReferenceFeature | 基准特征点 | Reference Feature |
| ProductCalib_ReferenceCenter | 基准中心 | Reference Center |
| ProductCalib_ReferenceAngle | 基准角度 | Reference Angle |
| ProductCalib_SetAsReference | 设为基准 | Set As Reference |
| ProductCalib_ManualInput | 手动输入 | Manual Input |
| ProductCalib_Deviation | 偏差 | Deviation |
| ProductCalib_Invert | 取反 | Invert |
| ProductCalib_Calculating | 计算中... | Calculating... |
| ProductCalib_CalcDone | 计算完成 | Calculation Done |
| ProductCalib_SetRefDone | 基准设置完成 | Reference Set |
| ProductCalib_AutoReceiveOn | 自动接收已开启 | Auto Receive Enabled |
| ProductCalib_AutoReceiveOff | 自动接收已关闭 | Auto Receive Disabled |
| ProductCalib_CameraShieldedOn | 相机已屏蔽，可手动输入 | Camera Shielded, Manual Input |
| ProductCalib_Photo1AutoReceived | 拍照位1自动接收数据 | Photo1 Auto Received |
| ProductCalib_Photo2AutoReceived | 拍照位2自动接收数据 | Photo2 Auto Received |

---

## 运动控制安全性

- 所有运动 API 接受 CancellationToken，支持急停快速打断
- 移动前检查 `_controller.CanExecuteMotion()`，设备运行中禁止手动操作
- 自动接收模式下不自动移动轴，仅填充视觉数据
- 旋转校正前确认 ΔAngle 在合理范围（如 ±180°）
- 异常通过 `RecoverableException` 上报，携带 suggestedAction

---

## 测试要点

- Halcon 计算正确性：已知两点验证中心点、角度、距离
- 角度归一化：边界值 ±180°、±360°、±540° 测试
- 取反开关：开启/关闭时偏差符号正确翻转
- 相机屏蔽：屏蔽时拍照按钮禁用、视觉X/Y 可输入
- 模式切换：手动→自动→手动，订阅正确创建/取消
- 配置保存/加载：新字段完整持久化
- 多语言：中英文切换所有文本更新
