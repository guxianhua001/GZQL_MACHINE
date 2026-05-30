# VisionCaptureView 五项缺陷修复 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复 VisionCaptureView 的5项缺陷：Photo Position不跟随站点刷新、Needle Offset未链接全局变量时应为0、恢复被误删的Needle Compensation、新增相机胶针固定距离参数、修正点胶最终位置计算公式。

**Architecture:** 在现有 WPF+Prism+MaterialDesign 架构上，修正坐标变换公式为 `FinalPos = PhotoPos + VisionDelta + CameraNeedleDistance + NeedleOffset(Row) + NeedleCompensation(Row)`。ViewModel 级别管理全局参数（NeedleOffset链接全局变量、CameraNeedleDistance链接全局变量），Row 级别管理逐行参数（NeedleOffset、NeedleCompensation）。BezierArcDispenseService 接受完整偏移参数而非仅从全局变量读取。

**Tech Stack:** WPF, Prism, MaterialDesignInXAML, C# .NET 9

---

## 缺陷根因分析

### Issue 1: Photo Position不跟随站点刷新
**根因:** `OnSelectedGroupChanged`、`OnPositionsUpdated`、`OnRecipeChanged`、`OnStationPositionSaved` 中调用了 `RefreshAvailablePositions()` 刷新下拉列表，但未调用 `RefreshPhotoPosition(SelectedRow)` 刷新当前选中行的坐标显示值（PhotoDx/PhotoDy/PhotoDz1）。

### Issue 2: Needle offset未链接全局变量时应等于0
**根因:** `SelectedRow` setter 中 `NeedleOffsetX = value.CalculatedOffsetX` 将 Row 的计算偏移覆盖了 ViewModel 的全局 NeedleOffset。当 ViewModel 的 NeedleOffset 未链接全局变量时，应保持为0，不应被 Row 的值覆盖。

### Issue 3: Needle Compensation被误删
**根因:** 之前的重构计划将 CompX/CompY 删除并替换为 OffsetA，但 Needle Compensation（针头补偿）和 Needle Offset（针头偏移）是两个独立概念。Needle Compensation 是逐行补偿值，需要恢复。

### Issue 4: 缺少相机和胶针固定距离参数
**根因:** 坐标变换公式中缺少相机到胶针的固定物理距离参数。该参数是设备固有的固定值，应可链接全局变量（如来自针头校准页面的 NeedleAligner_CompX/Y）。

### Issue 5: 点胶最终位置计算不完整
**根因:** 当前公式 `Mech = PhotoPos + VisionDelta + NeedleOffset(全局)` 缺少 CameraNeedleDistance、Row.NeedleOffset、Row.NeedleCompensation 三个分量。BezierArcDispenseService 仅从全局变量读取偏移，未接受完整的偏移参数。

---

## 坐标变换公式（修正后）

```
FinalX = PhotoDx + (VisionPointX - VisionCenterX) + CameraNeedleDistanceX + Row.CalculatedOffsetX + Row.CalculatedCompensationX
FinalY = PhotoDy + (VisionPointY - VisionCenterY) + CameraNeedleDistanceY + Row.CalculatedOffsetY + Row.CalculatedCompensationY
```

其中：
- **PhotoPos**: 拍照位置（来自位置提供器）
- **VisionDelta**: 视觉偏移 = 视觉点 - 视觉中心
- **CameraNeedleDistance**: 相机与胶针固定距离（ViewModel级别，可链接全局变量）
- **Row.NeedleOffset**: 逐行针头偏移（Row级别，带表达式）
- **Row.NeedleCompensation**: 逐行针头补偿（Row级别，带表达式，需恢复）

---

## File Structure

| File | Action | Responsibility |
|------|--------|---------------|
| `Module\Controls\Dispense\PhotoPositionRow.cs` | Modify | 恢复 NeedleCompensationX/Y 属性及表达式计算 |
| `Module\Controls\Dispense\VisionCaptureViewModel.cs` | Modify | 修复Issue1/2，新增CameraNeedleDistance，修正坐标变换，更新保存/加载 |
| `StationTasks\Services\BezierArcDispenseService.cs` | Modify | ComputeMachineCoordinate 接受完整偏移参数，实例方法接受偏移参数而非读全局变量 |
| `Module\Controls\Dispense\VisionCaptureView.xaml` | Modify | 新增 NeedleCompensation UI、CameraNeedleDistance UI |
| `MainApp\Languages\Strings.zh-CN.xaml` | Modify | 新增语言键 |
| `MainApp\Languages\Strings.en-US.xaml` | Modify | 新增语言键 |

---

### Task 1: PhotoPositionRow - 恢复 NeedleCompensation 属性

**Files:**
- Modify: `Module\Controls\Dispense\PhotoPositionRow.cs`

- [ ] **Step 1: 在 NeedleOffsetY 属性之后、OffsetXExpression 属性之前，新增 NeedleCompensationX/Y 属性**

在 `NeedleOffsetY` 属性之后添加：

```csharp
private double _needleCompensationX;
/// <summary>
/// 针头X补偿基础值
/// </summary>
public double NeedleCompensationX
{
    get => _needleCompensationX;
    set
    {
        if (SetProperty(ref _needleCompensationX, value))
            RaisePropertyChanged(nameof(CalculatedCompensationX));
    }
}

private double _needleCompensationY;
/// <summary>
/// 针头Y补偿基础值
/// </summary>
public double NeedleCompensationY
{
    get => _needleCompensationY;
    set
    {
        if (SetProperty(ref _needleCompensationY, value))
            RaisePropertyChanged(nameof(CalculatedCompensationY));
    }
}
```

- [ ] **Step 2: 在 OffsetYExpression 属性之后，新增 CompensationX/Y 表达式属性**

```csharp
private string _compensationXExpression;
/// <summary>
/// CompensationX计算表达式，最终值 = NeedleCompensationX + 表达式结果
/// </summary>
public string CompensationXExpression
{
    get => _compensationXExpression;
    set
    {
        if (SetProperty(ref _compensationXExpression, value))
            RaisePropertyChanged(nameof(CalculatedCompensationX));
    }
}

private string _compensationYExpression;
/// <summary>
/// CompensationY计算表达式
/// </summary>
public string CompensationYExpression
{
    get => _compensationYExpression;
    set
    {
        if (SetProperty(ref _compensationYExpression, value))
            RaisePropertyChanged(nameof(CalculatedCompensationY));
    }
}
```

- [ ] **Step 3: 在 CalculatedOffsetY 属性之后，新增 CalculatedCompensationX/Y 计算属性**

```csharp
/// <summary>
/// 计算后的CompensationX = NeedleCompensationX + 表达式结果
/// </summary>
public double CalculatedCompensationX => NeedleCompensationX + EvaluateExpression(CompensationXExpression);

/// <summary>
/// 计算后的CompensationY = NeedleCompensationY + 表达式结果
/// </summary>
public double CalculatedCompensationY => NeedleCompensationY + EvaluateExpression(CompensationYExpression);
```

---

### Task 2: VisionCaptureViewModel - 修复 Issue 1 (Photo Position不跟随站点刷新)

**Files:**
- Modify: `Module\Controls\Dispense\VisionCaptureViewModel.cs`

- [ ] **Step 1: 在 OnSelectedGroupChanged 方法中，RefreshAvailablePositions() 之后添加 RefreshPhotoPosition 调用**

在 `OnSelectedGroupChanged` 方法中，找到 `RefreshAvailablePositions();` 行，在其后添加：

```csharp
RefreshAvailablePositions();
RefreshSafePositionDisplay();
RefreshPhotoPosition(SelectedRow);
```

- [ ] **Step 2: 在 OnPositionsUpdated 方法中，RefreshAvailablePositions() 之后添加 RefreshPhotoPosition 调用**

在 `OnPositionsUpdated` 方法中，找到 `RefreshAvailablePositions();` 行，在其后添加：

```csharp
RefreshAvailablePositions();
RefreshSafePositionDisplay();
RefreshPhotoPosition(SelectedRow);
```

- [ ] **Step 3: 在 OnRecipeChanged 方法中，RefreshAvailablePositions() 之后添加 RefreshPhotoPosition 调用**

在 `OnRecipeChanged` 方法中，找到 `RefreshAvailablePositions();` 行，在其后添加：

```csharp
RefreshAvailablePositions();
RefreshSafePositionDisplay();
RefreshPhotoPosition(SelectedRow);
```

- [ ] **Step 4: 在 OnStationPositionSaved 方法中，RefreshAvailablePositions() 之后添加 RefreshPhotoPosition 调用**

在 `OnStationPositionSaved` 方法中，找到 `RefreshAvailablePositions();` 行，在其后添加：

```csharp
RefreshAvailablePositions();
RefreshSafePositionDisplay();
RefreshPhotoPosition(SelectedRow);
```

---

### Task 3: VisionCaptureViewModel - 修复 Issue 2 (Needle offset未链接全局变量时应等于0)

**Files:**
- Modify: `Module\Controls\Dispense\VisionCaptureViewModel.cs`

- [ ] **Step 1: 修改 SelectedRow setter，不再用 Row 的 CalculatedOffset 覆盖 ViewModel 的 NeedleOffset**

将 SelectedRow setter 中的：
```csharp
NeedleOffsetX = value.CalculatedOffsetX;
NeedleOffsetY = value.CalculatedOffsetY;
```

改为：
```csharp
if (IsNeedleOffsetXLinked)
    RaisePropertyChanged(nameof(NeedleOffsetX));
else
    NeedleOffsetX = 0;

if (IsNeedleOffsetYLinked)
    RaisePropertyChanged(nameof(NeedleOffsetY));
else
    NeedleOffsetY = 0;
```

逻辑说明：如果 NeedleOffset 已链接全局变量，则保持全局变量值不变（仅通知UI刷新）；如果未链接，则设为0。

---

### Task 4: VisionCaptureViewModel - 新增 CameraNeedleDistance 参数 (Issue 4)

**Files:**
- Modify: `Module\Controls\Dispense\VisionCaptureViewModel.cs`

- [ ] **Step 1: 在 NeedleOffsetYLinkedVar 属性之后，新增 CameraNeedleDistanceX/Y 属性及链接变量**

```csharp
private double _cameraNeedleDistanceX;
/// <summary>
/// 相机与胶针固定距离X（可链接全局变量）
/// </summary>
public double CameraNeedleDistanceX
{
    get => _cameraNeedleDistanceX;
    set => SetProperty(ref _cameraNeedleDistanceX, value);
}

private double _cameraNeedleDistanceY;
/// <summary>
/// 相机与胶针固定距离Y（可链接全局变量）
/// </summary>
public double CameraNeedleDistanceY
{
    get => _cameraNeedleDistanceY;
    set => SetProperty(ref _cameraNeedleDistanceY, value);
}

private string _cameraNeedleDistanceXLinkedVar;
/// <summary>
/// 相机胶针距离X链接的全局变量名
/// </summary>
public string CameraNeedleDistanceXLinkedVar
{
    get => _cameraNeedleDistanceXLinkedVar;
    set
    {
        if (SetProperty(ref _cameraNeedleDistanceXLinkedVar, value))
        {
            RaisePropertyChanged(nameof(IsCameraNeedleDistanceXLinked));
            if (!string.IsNullOrEmpty(value))
            {
                var gv = AvailableGlobalVariables.FirstOrDefault(v => v.Name == value);
                if (gv != null && double.TryParse(gv.Value, out var val))
                    CameraNeedleDistanceX = val;
            }
            else
            {
                CameraNeedleDistanceX = 0;
            }
        }
    }
}

private string _cameraNeedleDistanceYLinkedVar;
/// <summary>
/// 相机胶针距离Y链接的全局变量名
/// </summary>
public string CameraNeedleDistanceYLinkedVar
{
    get => _cameraNeedleDistanceYLinkedVar;
    set
    {
        if (SetProperty(ref _cameraNeedleDistanceYLinkedVar, value))
        {
            RaisePropertyChanged(nameof(IsCameraNeedleDistanceYLinked));
            if (!string.IsNullOrEmpty(value))
            {
                var gv = AvailableGlobalVariables.FirstOrDefault(v => v.Name == value);
                if (gv != null && double.TryParse(gv.Value, out var val))
                    CameraNeedleDistanceY = val;
            }
            else
            {
                CameraNeedleDistanceY = 0;
            }
        }
    }
}

public bool IsCameraNeedleDistanceXLinked => !string.IsNullOrEmpty(CameraNeedleDistanceXLinkedVar);
public bool IsCameraNeedleDistanceYLinked => !string.IsNullOrEmpty(CameraNeedleDistanceYLinkedVar);
```

- [ ] **Step 2: 新增 UnlinkNeedleDistance 命令**

在构造函数的命令初始化区域添加：

```csharp
UnlinkNeedleDistanceXCommand = new DelegateCommand(() => CameraNeedleDistanceXLinkedVar = null);
UnlinkNeedleDistanceYCommand = new DelegateCommand(() => CameraNeedleDistanceYLinkedVar = null);
```

在命令属性声明区域添加：

```csharp
public DelegateCommand UnlinkNeedleDistanceXCommand { get; }
public DelegateCommand UnlinkNeedleDistanceYCommand { get; }
```

- [ ] **Step 3: 在 SaveTransformParamsAsync 中保存 CameraNeedleDistance 参数**

在 `SaveTransformParamsAsync` 方法中，`UpdateOrAddGlobalVariable(variableList, "NeedleOffsetY_LinkedVar", ...)` 之后添加：

```csharp
UpdateOrAddGlobalVariable(variableList, "CameraNeedleDistanceX", CameraNeedleDistanceX.ToString("F6"), "相机胶针固定距离X");
UpdateOrAddGlobalVariable(variableList, "CameraNeedleDistanceY", CameraNeedleDistanceY.ToString("F6"), "相机胶针固定距离Y");
UpdateOrAddGlobalVariable(variableList, "CameraNeedleDistanceX_LinkedVar", CameraNeedleDistanceXLinkedVar ?? "", "相机胶针距离X链接的全局变量名");
UpdateOrAddGlobalVariable(variableList, "CameraNeedleDistanceY_LinkedVar", CameraNeedleDistanceYLinkedVar ?? "", "相机胶针距离Y链接的全局变量名");
```

- [ ] **Step 4: 在 LoadTransformParamsAsync 中加载 CameraNeedleDistance 参数**

在 `LoadTransformParamsAsync` 方法中，`NeedleOffsetYLinkedVar = noyLink?.Value;` 之后添加：

```csharp
var cndXVar = variables.FirstOrDefault(v => v.Name == "CameraNeedleDistanceX");
var cndYVar = variables.FirstOrDefault(v => v.Name == "CameraNeedleDistanceY");
if (cndXVar != null && double.TryParse(cndXVar.Value, out var cndx))
    CameraNeedleDistanceX = cndx;
if (cndYVar != null && double.TryParse(cndYVar.Value, out var cndy))
    CameraNeedleDistanceY = cndy;

var cndxLink = variables.FirstOrDefault(v => v.Name == "CameraNeedleDistanceX_LinkedVar");
var cndyLink = variables.FirstOrDefault(v => v.Name == "CameraNeedleDistanceY_LinkedVar");
CameraNeedleDistanceXLinkedVar = cndxLink?.Value;
CameraNeedleDistanceYLinkedVar = cndyLink?.Value;

RaisePropertyChanged(nameof(IsCameraNeedleDistanceXLinked));
RaisePropertyChanged(nameof(IsCameraNeedleDistanceYLinked));
```

---

### Task 5: VisionCaptureViewModel - 更新配置持久化支持 NeedleCompensation 和 CameraNeedleDistance

**Files:**
- Modify: `Module\Controls\Dispense\VisionCaptureViewModel.cs`

- [ ] **Step 1: 在 PhotoPositionRowConfig 中新增 NeedleCompensation 字段**

在 `PhotoPositionRowConfig` 类中，`OffsetYExpression` 属性之后添加：

```csharp
public double NeedleCompensationX { get; set; }
public double NeedleCompensationY { get; set; }
public string CompensationXExpression { get; set; }
public string CompensationYExpression { get; set; }
```

- [ ] **Step 2: 在 VisionCaptureConfig 中新增 CameraNeedleDistance 字段**

在 `VisionCaptureConfig` 类中，`CameraCenterY` 属性之后添加：

```csharp
public double CameraNeedleDistanceX { get; set; }
public double CameraNeedleDistanceY { get; set; }
```

- [ ] **Step 3: 在 BuildCurrentConfig 中保存新字段**

在 `BuildCurrentConfig` 方法中：
1. 在 `config` 对象初始化中添加：
```csharp
CameraNeedleDistanceX = CameraNeedleDistanceX,
CameraNeedleDistanceY = CameraNeedleDistanceY,
```

2. 在 Row 的 Select 中添加：
```csharp
NeedleCompensationX = r.NeedleCompensationX,
NeedleCompensationY = r.NeedleCompensationY,
CompensationXExpression = r.CompensationXExpression,
CompensationYExpression = r.CompensationYExpression,
```

- [ ] **Step 4: 在 ApplyConfig 中加载新字段**

在 `ApplyConfig` 方法中：
1. 在 ViewModel 属性赋值区域添加：
```csharp
CameraNeedleDistanceX = config.CameraNeedleDistanceX;
CameraNeedleDistanceY = config.CameraNeedleDistanceY;
```

2. 在 Row 属性赋值区域（`row.OffsetYExpression = rowConfig.OffsetYExpression;` 之后）添加：
```csharp
row.NeedleCompensationX = rowConfig.NeedleCompensationX;
row.NeedleCompensationY = rowConfig.NeedleCompensationY;
row.CompensationXExpression = rowConfig.CompensationXExpression;
row.CompensationYExpression = rowConfig.CompensationYExpression;
```

---

### Task 6: BezierArcDispenseService - 修正坐标变换公式 (Issue 5)

**Files:**
- Modify: `StationTasks\Services\BezierArcDispenseService.cs`

- [ ] **Step 1: 修改 ComputeMachineCoordinate 静态方法，接受完整偏移参数**

将现有的：
```csharp
public static (double X, double Y) ComputeMachineCoordinate(
    (double Dx, double Dy) photoPosition,
    (double X, double Y) visionPoint,
    (double X, double Y) visionCenter,
    (double X, double Y) needleOffset)
{
    double visionDeltaX = visionPoint.X - visionCenter.X;
    double visionDeltaY = visionPoint.Y - visionCenter.Y;

    double mechX = photoPosition.Dx + visionDeltaX + needleOffset.X;
    double mechY = photoPosition.Dy + visionDeltaY + needleOffset.Y;

    return (mechX, mechY);
}
```

改为：
```csharp
/// <summary>
/// 计算单点的机械坐标
/// 公式: Mech = PhotoPos + VisionDelta + CameraNeedleDistance + NeedleOffset + NeedleCompensation
/// </summary>
public static (double X, double Y) ComputeMachineCoordinate(
    (double Dx, double Dy) photoPosition,
    (double X, double Y) visionPoint,
    (double X, double Y) visionCenter,
    (double X, double Y) cameraNeedleDistance,
    (double X, double Y) needleOffset,
    (double X, double Y) needleCompensation)
{
    double visionDeltaX = visionPoint.X - visionCenter.X;
    double visionDeltaY = visionPoint.Y - visionCenter.Y;

    double mechX = photoPosition.Dx + visionDeltaX + cameraNeedleDistance.X + needleOffset.X + needleCompensation.X;
    double mechY = photoPosition.Dy + visionDeltaY + cameraNeedleDistance.Y + needleOffset.Y + needleCompensation.Y;

    return (mechX, mechY);
}
```

- [ ] **Step 2: 保留旧签名的兼容方法（向后兼容）**

在新的 `ComputeMachineCoordinate` 之后添加兼容方法：

```csharp
/// <summary>
/// 兼容旧调用：needleOffset 包含所有偏移分量之和
/// </summary>
public static (double X, double Y) ComputeMachineCoordinate(
    (double Dx, double Dy) photoPosition,
    (double X, double Y) visionPoint,
    (double X, double Y) visionCenter,
    (double X, double Y) needleOffset)
{
    return ComputeMachineCoordinate(photoPosition, visionPoint, visionCenter, (0, 0), needleOffset, (0, 0));
}
```

- [ ] **Step 3: 修改 GenerateArcMachinePoints 静态方法**

将现有的：
```csharp
public static List<(double X, double Y)> GenerateArcMachinePoints(
    (double Dx, double Dy) photoPosition,
    (double X, double Y) p1, (double X, double Y) p2, (double X, double Y) p3,
    (double X, double Y) center,
    (double X, double Y) needleOffset,
    int segmentCount)
```

改为：
```csharp
/// <summary>
/// 生成Arc模式的贝塞尔离散机械坐标点
/// </summary>
public static List<(double X, double Y)> GenerateArcMachinePoints(
    (double Dx, double Dy) photoPosition,
    (double X, double Y) p1, (double X, double Y) p2, (double X, double Y) p3,
    (double X, double Y) center,
    (double X, double Y) cameraNeedleDistance,
    (double X, double Y) needleOffset,
    (double X, double Y) needleCompensation,
    int segmentCount)
{
    var mechP1 = ComputeMachineCoordinate(photoPosition, p1, center, cameraNeedleDistance, needleOffset, needleCompensation);
    var mechP2 = ComputeMachineCoordinate(photoPosition, p2, center, cameraNeedleDistance, needleOffset, needleCompensation);
    var mechP3 = ComputeMachineCoordinate(photoPosition, p3, center, cameraNeedleDistance, needleOffset, needleCompensation);

    return DiscretizeQuadraticBezier(mechP1, mechP2, mechP3, segmentCount);
}
```

- [ ] **Step 4: 修改 TransformVisionToMachine 实例方法**

将现有的 `TransformVisionToMachine` 方法签名改为接受完整参数：

```csharp
public CoordinateTransformDetail TransformVisionToMachine(
    double photoDx, double photoDy,
    double pointX, double pointY,
    double centerX, double centerY,
    double cameraNeedleDistanceX, double cameraNeedleDistanceY,
    double needleOffsetX, double needleOffsetY,
    double needleCompensationX, double needleCompensationY)
{
    var (finalX, finalY) = ComputeMachineCoordinate(
        (photoDx, photoDy),
        (pointX, pointY),
        (centerX, centerY),
        (cameraNeedleDistanceX, cameraNeedleDistanceY),
        (needleOffsetX, needleOffsetY),
        (needleCompensationX, needleCompensationY));

    return new CoordinateTransformDetail
    {
        PhotoDx = photoDx, PhotoDy = photoDy,
        DeltaToCenterX = pointX - centerX, DeltaToCenterY = pointY - centerY,
        NeedleOffsetX = needleOffsetX, NeedleOffsetY = needleOffsetY,
        FinalX = finalX, FinalY = finalY
    };
}
```

- [ ] **Step 5: 修改 ComputeMachinePointsAsync 实例方法**

修改方法签名，接受完整偏移参数而非从全局变量读取：

```csharp
public async Task<List<CoordinateTransformDetail>> ComputeMachinePointsAsync(
    Dictionary<string, double> visionData,
    double photoDx, double photoDy,
    bool isArc,
    int arcSegments,
    double cameraNeedleDistanceX, double cameraNeedleDistanceY,
    double needleOffsetX, double needleOffsetY,
    double needleCompensationX, double needleCompensationY)
{
    (double X, double Y) cameraNeedleDistance = (cameraNeedleDistanceX, cameraNeedleDistanceY);
    (double X, double Y) needleOffset = (needleOffsetX, needleOffsetY);
    (double X, double Y) needleCompensation = (needleCompensationX, needleCompensationY);

    if (!isArc)
    {
        double centerX = GetVisionValue(visionData, "centerX", 0.0);
        double centerY = GetVisionValue(visionData, "centerY", 0.0);
        double needleX = GetVisionValue(visionData, "needleX", 0.0);
        double needleY = GetVisionValue(visionData, "needleY", 0.0);

        var (finalX, finalY) = ComputeMachineCoordinate(
            (photoDx, photoDy), (needleX, needleY), (centerX, centerY),
            cameraNeedleDistance, needleOffset, needleCompensation);

        return new List<CoordinateTransformDetail>
        {
            new CoordinateTransformDetail
            {
                PhotoDx = photoDx, PhotoDy = photoDy,
                DeltaToCenterX = needleX - centerX, DeltaToCenterY = needleY - centerY,
                NeedleOffsetX = needleOffset.X, NeedleOffsetY = needleOffset.Y,
                FinalX = finalX, FinalY = finalY
            }
        };
    }
    else
    {
        var (centerX, centerY, p1x, p1y, p2x, p2y, p3x, p3y) = ExtractArcPoints(visionData);

        var bezierPoints = GenerateArcMachinePoints(
            (photoDx, photoDy),
            (p1x, p1y), (p2x, p2y), (p3x, p3y),
            (centerX, centerY),
            cameraNeedleDistance, needleOffset, needleCompensation,
            arcSegments);

        var result = new List<CoordinateTransformDetail>();
        foreach (var pt in bezierPoints)
        {
            result.Add(new CoordinateTransformDetail
            {
                PhotoDx = photoDx,
                PhotoDy = photoDy,
                DeltaToCenterX = 0,
                DeltaToCenterY = 0,
                NeedleOffsetX = needleOffset.X,
                NeedleOffsetY = needleOffset.Y,
                FinalX = pt.X,
                FinalY = pt.Y
            });
        }
        return result;
    }
}
```

- [ ] **Step 6: 修改 ExecuteDotDispenseAsync 实例方法**

修改方法签名，接受完整偏移参数：

```csharp
public async Task ExecuteDotDispenseAsync(
    Dictionary<string, double> visionData,
    double photoDx, double photoDy,
    int dxAxisId, int dyAxisId, int dz1AxisId,
    int coordId,
    double speed, double dzSafePos, double dzDispensePos,
    bool dryRun, bool needleDescend,
    double cameraNeedleDistanceX, double cameraNeedleDistanceY,
    double needleOffsetX, double needleOffsetY,
    double needleCompensationX, double needleCompensationY,
    CancellationToken token)
{
    double centerX = GetVisionValue(visionData, "centerX", 0.0);
    double centerY = GetVisionValue(visionData, "centerY", 0.0);
    double needleX = GetVisionValue(visionData, "needleX", 0.0);
    double needleY = GetVisionValue(visionData, "needleY", 0.0);

    var (mechX, mechY) = ComputeMachineCoordinate(
        (photoDx, photoDy), (needleX, needleY), (centerX, centerY),
        (cameraNeedleDistanceX, cameraNeedleDistanceY),
        (needleOffsetX, needleOffsetY),
        (needleCompensationX, needleCompensationY));

    _logger.Info($"[BezierArcDispense] Dot坐标转换: photo({photoDx:F3},{photoDy:F3}) " +
        $"delta({needleX - centerX:F3},{needleY - centerY:F3}) " +
        $"camNeedleDist({cameraNeedleDistanceX:F3},{cameraNeedleDistanceY:F3}) " +
        $"offset({needleOffsetX:F3},{needleOffsetY:F3}) " +
        $"comp({needleCompensationX:F3},{needleCompensationY:F3}) " +
        $"needleDescend={needleDescend} " +
        $"→ 机械({mechX:F3},{mechY:F3})");

    await _motionService.MoveAbsAsync(dz1AxisId, dzSafePos, speed, token);
    await _motionService.MoveLineAbsAsync(coordId, new[] { dxAxisId, dyAxisId }, new[] { mechX, mechY }, speed, token);

    if (needleDescend)
        await _motionService.MoveAbsAsync(dz1AxisId, dzDispensePos, speed, token);

    if (!dryRun)
        _logger.Info($"[BezierArcDispense] Dot点胶执行于 ({mechX:F3}, {mechY:F3})");
    else
        _logger.Info($"[BezierArcDispense] Dot空跑模式，跳过出胶，位置 ({mechX:F3}, {mechY:F3})");

    if (needleDescend)
        await _motionService.MoveAbsAsync(dz1AxisId, dzSafePos, speed, token);
}
```

- [ ] **Step 7: 修改 ExecuteArcDispenseAsync 实例方法**

修改方法签名，接受完整偏移参数：

```csharp
public async Task ExecuteArcDispenseAsync(
    Dictionary<string, double> visionData,
    double photoDx, double photoDy,
    int dxAxisId, int dyAxisId, int dz1AxisId,
    int coordId,
    double speed, double dzSafePos, double dzDispensePos,
    int arcSegments, bool dryRun, bool needleDescend,
    double cameraNeedleDistanceX, double cameraNeedleDistanceY,
    double needleOffsetX, double needleOffsetY,
    double needleCompensationX, double needleCompensationY,
    ManualResetEventSlim pauseEvent, CancellationToken token)
{
    var (centerX, centerY, p1x, p1y, p2x, p2y, p3x, p3y) = ExtractArcPoints(visionData);

    var bezierPoints = GenerateArcMachinePoints(
        (photoDx, photoDy),
        (p1x, p1y), (p2x, p2y), (p3x, p3y),
        (centerX, centerY),
        (cameraNeedleDistanceX, cameraNeedleDistanceY),
        (needleOffsetX, needleOffsetY),
        (needleCompensationX, needleCompensationY),
        arcSegments);

    _logger.Info($"[BezierArcDispense] Arc坐标转换: 起点({bezierPoints[0].X:F3},{bezierPoints[0].Y:F3}) " +
        $"中点({bezierPoints[bezierPoints.Count / 2].X:F3},{bezierPoints[bezierPoints.Count / 2].Y:F3}) " +
        $"终点({bezierPoints[bezierPoints.Count - 1].X:F3},{bezierPoints[bezierPoints.Count - 1].Y:F3}) " +
        $"needleDescend={needleDescend}");

    await _motionService.MoveAbsAsync(dz1AxisId, dzSafePos, speed, token);
    await _motionService.MoveLineAbsAsync(coordId, new[] { dxAxisId, dyAxisId },
        new[] { bezierPoints[0].X, bezierPoints[0].Y }, speed, token);

    if (needleDescend)
        await _motionService.MoveAbsAsync(dz1AxisId, dzDispensePos, speed, token);

    for (int i = 1; i < bezierPoints.Count; i++)
    {
        token.ThrowIfCancellationRequested();
        pauseEvent.Wait(token);
        await _motionService.MoveLineAbsAsync(coordId, new[] { dxAxisId, dyAxisId },
            new[] { bezierPoints[i].X, bezierPoints[i].Y }, speed, token);
    }

    if (!dryRun)
        _logger.Info($"[BezierArcDispense] Arc点胶完成，{arcSegments}段插补");
    else
        _logger.Info($"[BezierArcDispense] Arc空跑模式，跳过出胶，{arcSegments}段插补运动完成");

    if (needleDescend)
        await _motionService.MoveAbsAsync(dz1AxisId, dzSafePos, speed, token);
}
```

---

### Task 7: VisionCaptureViewModel - 更新调用 BezierArcDispenseService 的代码

**Files:**
- Modify: `Module\Controls\Dispense\VisionCaptureViewModel.cs`

- [ ] **Step 1: 修改 PreviewMachinePointsAsync 方法**

将 `_bezierArcDispenseService.ComputeMachinePointsAsync` 调用改为传递完整偏移参数：

```csharp
var points = await _bezierArcDispenseService.ComputeMachinePointsAsync(
    visionData, photoDx, photoDy, isArc, SelectedRow.ArcSegments,
    CameraNeedleDistanceX, CameraNeedleDistanceY,
    SelectedRow.CalculatedOffsetX, SelectedRow.CalculatedOffsetY,
    SelectedRow.CalculatedCompensationX, SelectedRow.CalculatedCompensationY);
```

- [ ] **Step 2: 修改 ExecuteDispenseAsync 方法中的 Dot 点胶调用**

将 `_bezierArcDispenseService.ExecuteDotDispenseAsync` 调用改为传递完整偏移参数：

```csharp
await _bezierArcDispenseService.ExecuteDotDispenseAsync(
    visionData, photoDx, photoDy,
    axisIdMap["Dx"], axisIdMap["Dy"], axisIdMap["Dz₁"],
    coordId,
    SelectedRow.Speed, dzSafePos, dzDispensePos,
    dryRun, NeedleDescend,
    CameraNeedleDistanceX, CameraNeedleDistanceY,
    SelectedRow.CalculatedOffsetX, SelectedRow.CalculatedOffsetY,
    SelectedRow.CalculatedCompensationX, SelectedRow.CalculatedCompensationY,
    token);
```

- [ ] **Step 3: 修改 ExecuteDispenseAsync 方法中的 Arc 点胶调用**

将 `_bezierArcDispenseService.ExecuteArcDispenseAsync` 调用改为传递完整偏移参数：

```csharp
await _bezierArcDispenseService.ExecuteArcDispenseAsync(
    visionData, photoDx, photoDy,
    axisIdMap["Dx"], axisIdMap["Dy"], axisIdMap["Dz₁"],
    coordId,
    SelectedRow.Speed, dzSafePos, dzDispensePos,
    SelectedRow.ArcSegments, dryRun, NeedleDescend,
    CameraNeedleDistanceX, CameraNeedleDistanceY,
    SelectedRow.CalculatedOffsetX, SelectedRow.CalculatedOffsetY,
    SelectedRow.CalculatedCompensationX, SelectedRow.CalculatedCompensationY,
    _pauseEvent, token);
```

---

### Task 8: VisionCaptureView.xaml - 新增 NeedleCompensation 和 CameraNeedleDistance UI

**Files:**
- Modify: `Module\Controls\Dispense\VisionCaptureView.xaml`

- [ ] **Step 1: 在 Step1 的 Offset Compensation 区域新增 NeedleCompensation 行**

在 Step1 偏移补偿 Grid（DataContext="{Binding SelectedRow}"）中，将 RowDefinitions 从 2 行改为 4 行，新增 CompensationX/Y 行：

找到现有的 2 行 Grid（OffsetX 和 OffsetY），在其后添加 Row2 (CompensationX) 和 Row3 (CompensationY)：

```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto" />
    <RowDefinition Height="Auto" />
    <RowDefinition Height="Auto" />
    <RowDefinition Height="Auto" />
</Grid.RowDefinitions>

<!-- Row 0: OffsetX (已有) -->
<!-- Row 1: OffsetY (已有) -->

<!-- Row 2: CompensationX -->
<TextBlock Grid.Row="2" Grid.Column="0" Text="{lang:Lang VisionCapture_Label_CompensationX}" Style="{StaticResource ParamLabel}" Margin="0,0,6,4" />
<TextBox Grid.Row="2" Grid.Column="1" Text="{Binding NeedleCompensationX}" FontSize="11"
         Style="{StaticResource MaterialDesignOutlinedTextBox}" Padding="4,2" Margin="0,0,4,4" />
<TextBox Grid.Row="2" Grid.Column="2" Text="{Binding CompensationXExpression}" FontSize="11"
         Style="{StaticResource MaterialDesignOutlinedTextBox}" Padding="4,2" Margin="0,0,4,4"
         materialDesign:HintAssist.Hint="{lang:Lang VisionCapture_ExpressionHint}" />
<TextBlock Grid.Row="2" Grid.Column="3" Text="{Binding CalculatedCompensationX, StringFormat='= {0:F3}'}"
           FontSize="10" Foreground="{StaticResource PrimaryBlue}" VerticalAlignment="Center" Margin="2,0,0,4" />

<!-- Row 3: CompensationY -->
<TextBlock Grid.Row="3" Grid.Column="0" Text="{lang:Lang VisionCapture_Label_CompensationY}" Style="{StaticResource ParamLabel}" Margin="0,0,6,0" />
<TextBox Grid.Row="3" Grid.Column="1" Text="{Binding NeedleCompensationY}" FontSize="11"
         Style="{StaticResource MaterialDesignOutlinedTextBox}" Padding="4,2" Margin="0,0,4,0" />
<TextBox Grid.Row="3" Grid.Column="2" Text="{Binding CompensationYExpression}" FontSize="11"
         Style="{StaticResource MaterialDesignOutlinedTextBox}" Padding="4,2" Margin="0,0,4,0"
         materialDesign:HintAssist.Hint="{lang:Lang VisionCapture_ExpressionHint}" />
<TextBlock Grid.Row="3" Grid.Column="3" Text="{Binding CalculatedCompensationY, StringFormat='= {0:F3}'}"
           FontSize="10" Foreground="{StaticResource PrimaryBlue}" VerticalAlignment="Center" Margin="2,0,0,0" />
```

- [ ] **Step 2: 在 Step2 坐标变换详情 Dot 模式中，在 NeedleOffset 区域之后新增 CameraNeedleDistance 区域**

在 Step2 Dot 模式的 NeedleOffset Border 之后、FinalDispensePos Border 之前，新增 CameraNeedleDistance Border：

```xml
<!-- ④ 相机胶针固定距离 -->
<Border Style="{StaticResource CoordCard}" Margin="0,0,0,4">
    <StackPanel>
        <StackPanel Orientation="Horizontal" Margin="0,0,0,2">
            <Border Width="18" Height="18" CornerRadius="9" Background="{StaticResource PrimaryBlue}" Margin="0,0,4,0">
                <TextBlock Text="④" Foreground="White" FontSize="9" FontWeight="Bold"
                           HorizontalAlignment="Center" VerticalAlignment="Center" />
            </Border>
            <TextBlock Text="{lang:Lang VisionCapture_CameraNeedleDistance}" FontWeight="SemiBold" FontSize="11" Foreground="{StaticResource TextPrimary}" />
        </StackPanel>
        <Grid Margin="22,0,0,0">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto" />
                <RowDefinition Height="Auto" />
            </Grid.RowDefinitions>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>
            <materialDesign:PackIcon Grid.Row="0" Grid.Column="0" Kind="AxisXArrow" Width="14" Height="14"
                                     Foreground="#E53935" VerticalAlignment="Center" Margin="0,0,4,2" />
            <TextBlock Grid.Row="0" Grid.Column="1" Text="{Binding CameraNeedleDistanceX, StringFormat=F3}"
                       FontWeight="Bold" Foreground="#E53935" FontSize="11" VerticalAlignment="Center" Margin="0,0,2,2" />
            <Button Grid.Row="0" Grid.Column="2" Command="{Binding UnlinkNeedleDistanceXCommand}"
                    Style="{StaticResource MaterialDesignIconButton}" Padding="0" Width="20" Height="20"
                    ToolTip="{lang:Lang VisionCapture_UnlinkGlobalVariable}">
                <materialDesign:PackIcon Kind="LinkOff" Width="12" Height="12"
                                         Foreground="{Binding IsCameraNeedleDistanceXLinked, Converter={StaticResource LinkedToBrushConverter}}"
                                         VerticalAlignment="Center" />
            </Button>
            <ComboBox Grid.Row="0" Grid.Column="3" ItemsSource="{Binding AvailableGlobalVariables}"
                      SelectedValuePath="Name" DisplayMemberPath="Name"
                      SelectedValue="{Binding CameraNeedleDistanceXLinkedVar, UpdateSourceTrigger=PropertyChanged}"
                      Width="100" FontSize="9"
                      materialDesign:HintAssist.Hint="{lang:Lang VisionCapture_LinkGlobalVariable}" />

            <materialDesign:PackIcon Grid.Row="1" Grid.Column="0" Kind="AxisYArrow" Width="14" Height="14"
                                     Foreground="#43A047" VerticalAlignment="Center" Margin="0,0,4,0" />
            <TextBlock Grid.Row="1" Grid.Column="1" Text="{Binding CameraNeedleDistanceY, StringFormat=F3}"
                       FontWeight="Bold" Foreground="#43A047" FontSize="11" VerticalAlignment="Center" Margin="0,0,2,0" />
            <Button Grid.Row="1" Grid.Column="2" Command="{Binding UnlinkNeedleDistanceYCommand}"
                    Style="{StaticResource MaterialDesignIconButton}" Padding="0" Width="20" Height="20"
                    ToolTip="{lang:Lang VisionCapture_UnlinkGlobalVariable}">
                <materialDesign:PackIcon Kind="LinkOff" Width="12" Height="12"
                                         Foreground="{Binding IsCameraNeedleDistanceYLinked, Converter={StaticResource LinkedToBrushConverter}}"
                                         VerticalAlignment="Center" />
            </Button>
            <ComboBox Grid.Row="1" Grid.Column="3" ItemsSource="{Binding AvailableGlobalVariables}"
                      SelectedValuePath="Name" DisplayMemberPath="Name"
                      SelectedValue="{Binding CameraNeedleDistanceYLinkedVar, UpdateSourceTrigger=PropertyChanged}"
                      Width="100" FontSize="9"
                      materialDesign:HintAssist.Hint="{lang:Lang VisionCapture_LinkGlobalVariable}" />
        </Grid>
    </StackPanel>
</Border>
```

注意：需要将原有的 NeedleOffset 编号从③调整为③，CameraNeedleDistance 为④，FinalDispensePos 为⑤。同时 Arc 模式中也需要同步添加 CameraNeedleDistance 区域。

---

### Task 9: 多语言支持

**Files:**
- Modify: `MainApp\Languages\Strings.zh-CN.xaml`
- Modify: `MainApp\Languages\Strings.en-US.xaml`

- [ ] **Step 1: 在 zh-CN 文件中新增语言键**

在 `VisionCapture_Label_OffsetY` 之后添加：

```xml
<sys:String x:Key="VisionCapture_Label_CompensationX">补偿X：</sys:String>
<sys:String x:Key="VisionCapture_Label_CompensationY">补偿Y：</sys:String>
<sys:String x:Key="VisionCapture_CameraNeedleDistance">相机胶针距离</sys:String>
```

- [ ] **Step 2: 在 en-US 文件中新增语言键**

在 `VisionCapture_Label_OffsetY` 之后添加：

```xml
<sys:String x:Key="VisionCapture_Label_CompensationX">Comp X:</sys:String>
<sys:String x:Key="VisionCapture_Label_CompensationY">Comp Y:</sys:String>
<sys:String x:Key="VisionCapture_CameraNeedleDistance">Camera-Needle Dist</sys:String>
```

---

### Task 10: 构建验证

- [ ] **Step 1: 运行构建，修复编译错误**

Run: `dotnet build`
Expected: BUILD SUCCEEDED

- [ ] **Step 2: 检查所有引用已更新**

确保所有对旧签名的调用已更新为新的完整参数签名。
