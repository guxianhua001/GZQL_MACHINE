# NeedleAligner 重构实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 重构 NeedleAlignerViewModel，将运动接口从 IPositionMotionController 切换为 IMotionService，从 motion parameters 的 SafeHeight 参数获取安全高度，增加系统1/系统2对针位置示教功能，寻针动作参考 ExecuteNeedleCalibrationAsync 实现，更新运动参数卡片。

**Architecture:** NeedleAlignerViewModel 直接使用 IMotionService 进行底层运动控制（MoveAbsAsync/MoveRelAsync/GetAxisPosition/StopAxis），安全高度从 `Parameters.SafeHeight` 获取。对针轴按系统划分：系统1使用 Dx/Dy/Dz₂，系统2使用 Dx/Dy/Dz₃。寻针流程：抬升安全高度→移动到对针位置XY→下降到寻针高度→四点边缘搜索→Z轴高度搜索→计算补偿。IPositionMotionController 专属于位置编辑器，不再被 NeedleAligner 使用。

**Tech Stack:** WPF + Prism + MaterialDesignInXAML, .NET 9, IMotionService (底层运动控制)

---

## 文件结构

| 操作 | 文件路径 | 职责 |
|------|---------|------|
| 修改 | `Core\Models\NeedleCalibrationParams.cs` | 删除 NeedleBaseHeight，增加 SafeHeight、System1AlignPosition、System2AlignPosition |
| 修改 | `Module\Controls\Maintenance\NeedleAlignerViewModel.cs` | 替换 IPositionMotionController→IMotionService，增加轴解析、对针位置示教、重写寻针流程 |
| 修改 | `Module\Controls\Maintenance\NeedleAlignerView.xaml` | 增加系统1/系统2对针位置卡片，替换 NeedleBaseHeight→SafeHeight |
| 修改 | `MainApp\Languages\Strings.zh-CN.xaml` | 增加/修改中文语言键 |
| 修改 | `MainApp\Languages\Strings.en-US.xaml` | 增加/修改英文语言键 |
| 修改 | `版本修改记录.txt` | 记录版本变更 |

---

### Task 1: 更新 NeedleCalibrationParams 模型

**Files:**
- Modify: `Core\Models\NeedleCalibrationParams.cs`

- [ ] **Step 1: 删除 NeedleBaseHeight 属性，增加 SafeHeight 和对针位置属性**

在 `NeedleCalibrationParams.cs` 中：

删除以下代码（约 L175-L182）：
```csharp
[Category("针头参数")]
[DisplayName("针头基准高度 (mm)")]
[Description("针头在零位时的基准高度")]
public double NeedleBaseHeight
{
    get => _needleBaseHeight;
    set => SetProperty(ref _needleBaseHeight, value);
}
```

删除对应字段（约 L63）：
```csharp
private double _needleBaseHeight = 0.1;
```

在搜索参数 region 之后增加：
```csharp
#region 安全高度
[Category("安全参数")]
[DisplayName("安全高度 (mm)")]
[Description("对针运动中的安全Z高度，运动前先抬升到此高度")]
[Range(0.0, 500.0)]
public double SafeHeight
{
    get => _safeHeight;
    set => SetProperty(ref _safeHeight, value);
}
private double _safeHeight = 50.0;
#endregion

#region 对针位置
[Category("对针位置")]
[DisplayName("系统1对针位置")]
[Description("系统1对针位置 (Dx, Dy, Dz₂)")]
public PointF System1AlignPosition
{
    get => _system1AlignPosition;
    set => SetProperty(ref _system1AlignPosition, value);
}
private PointF _system1AlignPosition = new PointF(0, 0, 0);

[Category("对针位置")]
[DisplayName("系统2对针位置")]
[Description("系统2对针位置 (Dx, Dy, Dz₃)")]
public PointF System2AlignPosition
{
    get => _system2AlignPosition;
    set => SetProperty(ref _system2AlignPosition, value);
}
private PointF _system2AlignPosition = new PointF(0, 0, 0);
#endregion
```

- [ ] **Step 2: 更新 Clone 方法**

在 `Clone()` 方法中：
- 删除 `NeedleBaseHeight = this.NeedleBaseHeight,`
- 增加：
```csharp
SafeHeight = this.SafeHeight,
System1AlignPosition = this.System1AlignPosition != null ? new PointF(this.System1AlignPosition.X, this.System1AlignPosition.Y, this.System1AlignPosition.Z) : new PointF(),
System2AlignPosition = this.System2AlignPosition != null ? new PointF(this.System2AlignPosition.X, this.System2AlignPosition.Y, this.System2AlignPosition.Z) : new PointF(),
```

- [ ] **Step 3: 编译验证**

Run: `dotnet build Core\Core.csproj --no-restore`
Expected: 编译成功，无错误

---

### Task 2: 重构 NeedleAlignerViewModel - 替换运动接口和重写寻针流程

**Files:**
- Modify: `Module\Controls\Maintenance\NeedleAlignerViewModel.cs`

- [ ] **Step 1: 替换构造函数依赖**

将字段声明（约 L29）：
```csharp
private readonly IPositionMotionController _motionController;
```
替换为：
```csharp
private readonly IMotionService _motionService;
```

在 using 区域增加：
```csharp
using MotionControl.Interfaces;
```

删除 using（不再需要）：
```csharp
using Core.Abstraction; // 仅当此文件无其他引用时删除
```

将构造函数参数（约 L314-L322）：
```csharp
public NeedleAlignerViewModel(
    IPositionMotionController motionController,
    IParameterStorage parameterStorage,
    ILoggerService logger,
    ILocalizationService localization,
    IDialogService dialogService,
    IEventAggregator eventAggregator,
    NeedleCompensationManager compensationManager,
    IRecipePoolService recipePoolService)
```
替换为：
```csharp
public NeedleAlignerViewModel(
    IMotionService motionService,
    IParameterStorage parameterStorage,
    ILoggerService logger,
    ILocalizationService localization,
    IDialogService dialogService,
    IEventAggregator eventAggregator,
    NeedleCompensationManager compensationManager,
    IRecipePoolService recipePoolService)
```

在构造函数体中替换赋值：
```csharp
_motionService = motionService;
```

- [ ] **Step 2: 增加轴ID解析辅助方法**

删除常量 `SafeHeightOffset`（约 L41）：
```csharp
private const double SafeHeightOffset = 50.0;
```

增加轴ID缓存和解析方法：
```csharp
private int? _axisDxId;
private int? _axisDyId;
private int? _axisDz2Id;
private int? _axisDz3Id;

/// <summary>根据轴名解析轴ID（缓存结果）</summary>
private int? ResolveAxisId(string axisName)
{
    var axisConfigs = _motionService.GetAxisConfigurations();
    var axis = axisConfigs.FirstOrDefault(a => a.Name == axisName);
    return axis?.AxisId;
}

/// <summary>获取Dx轴ID</summary>
private int AxisDxId => _axisDxId ??= ResolveAxisId("Dx") ?? throw new InvalidOperationException("未找到Dx轴配置");
/// <summary>获取Dy轴ID</summary>
private int AxisDyId => _axisDyId ??= ResolveAxisId("Dy") ?? throw new InvalidOperationException("未找到Dy轴配置");
/// <summary>获取Dz₂轴ID（系统1 Z轴）</summary>
private int AxisDz2Id => _axisDz2Id ??= ResolveAxisId("Dz₂") ?? throw new InvalidOperationException("未找到Dz₂轴配置");
/// <summary>获取Dz₃轴ID（系统2 Z轴）</summary>
private int AxisDz3Id => _axisDz3Id ??= ResolveAxisId("Dz₃") ?? throw new InvalidOperationException("未找到Dz₃轴配置");

/// <summary>根据系统编号获取Z轴ID</summary>
private int GetZAxisId(int systemNumber) => systemNumber == 1 ? AxisDz2Id : AxisDz3Id;
```

- [ ] **Step 3: 重写 MoveToPositionSafelyAsync 使用 IMotionService**

将现有 `MoveToPositionSafelyAsync`（约 L468-L478）替换为：
```csharp
/// <summary>
/// 安全移动到目标位置：先抬升Z轴到安全高度，再水平移动，最后下降Z轴
/// 防止针头在水平移动过程中碰撞工件或夹具
/// 安全高度从 Parameters.SafeHeight 获取
/// </summary>
private async Task MoveToPositionSafelyAsync(int systemNumber, double targetX, double targetY, double targetZ, double velocity, CancellationToken token = default)
{
    var zAxisId = GetZAxisId(systemNumber);
    var safeHeight = Parameters.SafeHeight;

    // 1. 抬升Z轴到安全高度
    await _motionService.MoveAbsAsync(zAxisId, safeHeight, velocity, token);

    // 2. 水平移动到目标XY
    await _motionService.MoveLineAbsAsync(
        0,
        new[] { AxisDxId, AxisDyId },
        new[] { targetX, targetY },
        velocity, token);

    // 3. 下降Z轴到目标高度（半速，确保安全）
    await _motionService.MoveAbsAsync(zAxisId, targetZ, velocity * 0.5, token);
}
```

- [ ] **Step 4: 重写 StartCalibrationAsync - 参考 ExecuteNeedleCalibrationAsync 流程**

将 `StartCalibrationAsync`（约 L395-L462）整体替换为：
```csharp
/// <summary>
/// 执行针头校准流程（参考 ExecuteNeedleCalibrationAsync）
/// 流程：抬升安全高度 → 移动到对针位置XY → 下降到寻针高度 → 四点边缘搜索 → Z轴高度搜索 → 计算补偿
/// </summary>
private async Task StartCalibrationAsync()
{
    try
    {
        IsCalibrating = true;
        CalibrationStatus = _localization.GetResourceOrDefault("NeedleAligner_Status_Starting", "开始校准...");
        CalibrationProgress = 0;
        _calibrationCts = new CancellationTokenSource();
        var token = _calibrationCts.Token;
        var zAxisId = GetZAxisId(SystemNumber);

        // 1. 抬升到安全高度
        token.ThrowIfCancellationRequested();
        CalibrationStatus = _localization.GetResourceOrDefault("NeedleAligner_Status_MovingToSafeHeight", "抬升到安全高度...");
        CalibrationProgress = 5;
        await _motionService.MoveAbsAsync(zAxisId, Parameters.SafeHeight, Parameters.SearchSpeed, token);

        // 2. 移动到对针位置XY，然后下降到寻针高度
        token.ThrowIfCancellationRequested();
        CalibrationStatus = _localization.GetResourceOrDefault("NeedleAligner_Status_MovingToAlignPosition", "移动到对针位置...");
        CalibrationProgress = 10;
        var alignPosition = SystemNumber == 1 ? Parameters.System1AlignPosition : Parameters.System2AlignPosition;
        await MoveToPositionSafelyAsync(SystemNumber, alignPosition.X, alignPosition.Y, alignPosition.Z, Parameters.SearchSpeed, token);

        // 3. 搜索中心点XY（四点边缘搜索）
        token.ThrowIfCancellationRequested();
        CalibrationStatus = _localization.GetResourceOrDefault("NeedleAligner_Status_SearchingCenter", "搜索中心点XY...");
        CalibrationProgress = 20;
        var centerPoint = await SearchCenterPointAsync(token);
        if (centerPoint == null)
        {
            CalibrationStatus = _localization.GetResourceOrDefault("NeedleAligner_Status_SearchCenterFailed", "搜索中心点失败");
            return;
        }

        // 4. 搜索针尖高度
        token.ThrowIfCancellationRequested();
        CalibrationStatus = _localization.GetResourceOrDefault("NeedleAligner_Status_SearchingHeight", "搜索针尖高度...");
        CalibrationProgress = 60;
        var needleHeight = await SearchNeedleHeightAsync(centerPoint, token);
        if (double.IsNaN(needleHeight))
        {
            CalibrationStatus = _localization.GetResourceOrDefault("NeedleAligner_Status_SearchHeightFailed", "搜索针尖高度失败");
            return;
        }

        // 5. 计算补偿值
        token.ThrowIfCancellationRequested();
        CalibrationStatus = _localization.GetResourceOrDefault("NeedleAligner_Status_Calculating", "计算补偿值...");
        CalibrationProgress = 90;
        Parameters.CurrentXYZ = new PointF(centerPoint.X, centerPoint.Y, (float)needleHeight);

        CalibrationProgress = 100;
        OnCalibrationCompleted();

        // 6. 抬升回安全高度
        await _motionService.MoveAbsAsync(zAxisId, Parameters.SafeHeight, Parameters.SearchSpeed, token);

        // 自动保存
        await SaveParametersAsync(syncGlobalVariables: false);

        CalibrationStatus = _localization.GetResourceOrDefault("NeedleAligner_Status_Completed", "校准完成");
        AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_CalibrationSuccess", "针头校准成功完成"));
    }
    catch (OperationCanceledException)
    {
        CalibrationStatus = _localization.GetResourceOrDefault("NeedleAligner_Status_Cancelled", "校准已取消");
        AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_CalibrationCancelled", "针头校准已取消"));
    }
    catch (Exception ex)
    {
        CalibrationStatus = string.Format(
            _localization.GetResourceOrDefault("NeedleAligner_Status_Error", "校准异常: {0}"),
            ex.Message);
        AddLog(string.Format(
            _localization.GetResourceOrDefault("NeedleAligner_Log_CalibrationError", "校准异常: {0}"),
            ex.Message));
        _logger.Error(ex, "针头校准异常");
    }
    finally
    {
        IsCalibrating = false;
        _calibrationCts?.Dispose();
        _calibrationCts = null;
    }
}
```

- [ ] **Step 5: 增加 SearchCenterPointAsync 方法（参考 NeedleCalibrating.cs）**

```csharp
/// <summary>
/// 搜索中心点XY：四点边缘搜索，X方向两点+Y方向两点，取边缘中点计算中心
/// 参考 NeedleCalibrating.cs SearchCenterPointAsync
/// </summary>
private async Task<PointF> SearchCenterPointAsync(CancellationToken token)
{
    var searchPoints = new[]
    {
        Parameters.SearchPoint1,
        Parameters.SearchPoint2,
        Parameters.SearchPoint3,
        Parameters.SearchPoint4
    };

    var xEdgePoints = new List<PointF>();
    var yEdgePoints = new List<PointF>();

    // 移动到第一个搜索点后，下降到寻针高度
    await MoveToXYPositionAsync(searchPoints[0].X, searchPoints[0].Y, Parameters.SearchSpeed, token);
    var zAxisId = GetZAxisId(SystemNumber);
    var alignPosition = SystemNumber == 1 ? Parameters.System1AlignPosition : Parameters.System2AlignPosition;
    await _motionService.MoveAbsAsync(zAxisId, alignPosition.Z, Parameters.SearchSpeed * 0.5, token);

    // 前两个点进行X方向搜索
    for (int i = 0; i < 2; i++)
    {
        token.ThrowIfCancellationRequested();
        CalibrationProgress = 20 + i * 10;
        CalibrationStatus = string.Format(
            _localization.GetResourceOrDefault("NeedleAligner_Status_SearchingXEdge", "在点{0}进行X方向搜索..."),
            i + 1);

        var xEdge = await SearchEdgeInDirectionAsync(searchPoints[i], AxisDxId, token);
        if (xEdge != null)
            xEdgePoints.Add(xEdge.Value);
        else
        {
            AddLog(string.Format(
                _localization.GetResourceOrDefault("NeedleAligner_Log_SearchEdgeFailed", "点{0} X方向边缘搜索失败"),
                i + 1));
            return null;
        }
    }

    // 后两个点进行Y方向搜索
    for (int i = 2; i < 4; i++)
    {
        token.ThrowIfCancellationRequested();
        CalibrationProgress = 40 + (i - 2) * 10;
        CalibrationStatus = string.Format(
            _localization.GetResourceOrDefault("NeedleAligner_Status_SearchingYEdge", "在点{0}进行Y方向搜索..."),
            i + 1);

        var yEdge = await SearchEdgeInDirectionAsync(searchPoints[i], AxisDyId, token);
        if (yEdge != null)
            yEdgePoints.Add(yEdge.Value);
        else
        {
            AddLog(string.Format(
                _localization.GetResourceOrDefault("NeedleAligner_Log_SearchEdgeFailed", "点{0} Y方向边缘搜索失败"),
                i + 1));
            return null;
        }
    }

    if (xEdgePoints.Count < 2 || yEdgePoints.Count < 2)
    {
        _logger.Error($"[NeedleAligner] 有效边缘点不足: X方向={xEdgePoints.Count}, Y方向={yEdgePoints.Count}");
        return null;
    }

    // 计算中心点：X方向取中点，Y方向取中点
    float centerX = (xEdgePoints[0].X + xEdgePoints[1].X) / 2f;
    float centerY = (yEdgePoints[0].Y + yEdgePoints[1].Y) / 2f;

    AddLog(string.Format(
        _localization.GetResourceOrDefault("NeedleAligner_Log_CenterPointFound", "中心点: X={0:F3}, Y={1:F3}"),
        centerX, centerY));

    return new PointF(centerX, centerY);
}
```

- [ ] **Step 6: 增加 SearchEdgeInDirectionAsync 方法**

```csharp
/// <summary>
/// 在指定方向搜索边缘：正向搜索+反向搜索，取中点
/// 参考 NeedleCalibrating.cs SearchEdgeInDirectionAsync
/// </summary>
private async Task<PointF?> SearchEdgeInDirectionAsync(PointF startPoint, int axisId, CancellationToken token)
{
    try
    {
        var currentX = _motionService.GetAxisPosition(AxisDxId);
        var currentY = _motionService.GetAxisPosition(AxisDyId);

        // 移动到搜索起始点（偏移 -SearchRange）
        if (axisId == AxisDxId)
        {
            await MoveToXYPositionAsync(startPoint.X - Parameters.SearchRange, currentY, Parameters.SearchSpeed, token);
        }
        else
        {
            await MoveToXYPositionAsync(currentX, startPoint.Y - Parameters.SearchRange, Parameters.SearchSpeed, token);
        }

        // 正向搜索
        double forwardEdge = await SearchSingleEdgeAsync(axisId, Parameters.SearchRange * 2, Parameters.FineSearchSpeed, token);
        if (double.IsNaN(forwardEdge))
            return null;

        // 反向搜索
        double backwardEdge = await SearchSingleEdgeAsync(axisId, -Parameters.SearchRange * 2, Parameters.FineSearchSpeed, token);
        if (double.IsNaN(backwardEdge))
            return null;

        // 计算中心位置
        double center = (forwardEdge + backwardEdge) / 2.0;

        var result = new PointF((float)currentX, (float)currentY);
        if (axisId == AxisDxId)
            result = new PointF((float)center, (float)currentY);
        else
            result = new PointF((float)currentX, (float)center);

        return result;
    }
    catch (OperationCanceledException)
    {
        return null;
    }
    catch (Exception ex)
    {
        _logger.Error(ex, "[NeedleAligner] 边缘搜索失败");
        return null;
    }
}
```

- [ ] **Step 7: 增加 SearchSingleEdgeAsync 方法**

```csharp
/// <summary>
/// 搜索单个边缘：沿指定轴相对移动，监测传感器触发位置
/// 参考 NeedleCalibrating.cs SearchSingleEdgeAsync
/// </summary>
private async Task<double> SearchSingleEdgeAsync(int axisId, double searchDistance, double speed, CancellationToken token)
{
    return await Task.Run(async () =>
    {
        double startPos = _motionService.GetAxisPosition(axisId);

        _logger.Info($"[NeedleAligner] 开始边缘搜索: 轴ID={axisId}, 起始={startPos:F3}, 距离={searchDistance:F3}, 速度={speed:F1}");

        // 开始搜索移动（相对运动）
        await _motionService.MoveRelAsync(axisId, searchDistance, speed, token);

        // 等待传感器触发（使用DI端口检测）
        var edgePos = await WaitForSensorTriggerAsync(axisId, token);

        if (!double.IsNaN(edgePos))
        {
            _logger.Info($"[NeedleAligner] 边缘位置: {edgePos:F3}");
            // 停止轴运动
            _motionService.StopAxis(axisId);
        }
        else
        {
            _logger.Warn("[NeedleAligner] 边缘搜索超时");
        }

        return edgePos;
    }, token);
}
```

- [ ] **Step 8: 增加 WaitForSensorTriggerAsync 方法**

```csharp
/// <summary>
/// 等待传感器触发：轮询DI端口，返回触发时的轴位置
/// 参考 NeedleCalibrating.cs WaitForSensorTriggerAsync
/// </summary>
private async Task<double> WaitForSensorTriggerAsync(int axisId, CancellationToken token)
{
    try
    {
        var timeoutMs = 60000;
        var checkIntervalMs = 20;
        var startTime = DateTime.Now;

        while (!token.IsCancellationRequested)
        {
            if ((DateTime.Now - startTime).TotalMilliseconds > timeoutMs)
                return double.NaN;

            // 检查针头传感器信号（DI端口37和38）
            var sensor1 = _motionService.ReadDi(37);
            var sensor2 = _motionService.ReadDi(38);
            bool sensorTriggered = sensor1 && sensor2;

            if (sensorTriggered)
            {
                var currentPos = _motionService.GetAxisPosition(axisId);
                return currentPos;
            }

            await Task.Delay(checkIntervalMs, token);
        }

        return double.NaN;
    }
    catch (OperationCanceledException)
    {
        _logger.Info("[NeedleAligner] 传感器等待被取消");
        return double.NaN;
    }
}
```

- [ ] **Step 9: 增加 SearchNeedleHeightAsync 方法**

```csharp
/// <summary>
/// 搜索针尖高度：移动到中心点XY，多次Z方向搜索取平均
/// 参考 NeedleCalibrating.cs SearchNeedleHeightAsync
/// </summary>
private async Task<double> SearchNeedleHeightAsync(PointF centerPoint, CancellationToken token)
{
    try
    {
        var zAxisId = GetZAxisId(SystemNumber);

        // 移动到中心点XY
        await MoveToXYPositionAsync(centerPoint.X, centerPoint.Y, Parameters.SearchSpeed, token);

        double totalHeight = 0;
        int successCount = 0;

        for (int i = 0; i < Parameters.ZSearchCount; i++)
        {
            token.ThrowIfCancellationRequested();
            CalibrationProgress = 60 + i * 10;
            CalibrationStatus = string.Format(
                _localization.GetResourceOrDefault("NeedleAligner_Status_ZSearch", "第 {0}/{1} 次高度搜索..."),
                i + 1, Parameters.ZSearchCount);

            double height = await SearchSingleNeedleHeightAsync(zAxisId, token);
            if (!double.IsNaN(height))
            {
                totalHeight += height;
                successCount++;
            }
        }

        if (successCount == 0)
            return double.NaN;

        double averageHeight = totalHeight / successCount;
        _logger.Info($"[NeedleAligner] 针尖高度搜索结果: 平均高度={averageHeight:F3}mm, 成功次数={successCount}");

        return averageHeight;
    }
    catch (OperationCanceledException)
    {
        return double.NaN;
    }
    catch (Exception ex)
    {
        _logger.Error(ex, "[NeedleAligner] 搜索针尖高度异常");
        return double.NaN;
    }
}
```

- [ ] **Step 10: 增加 SearchSingleNeedleHeightAsync 方法**

```csharp
/// <summary>
/// 单次搜索针尖高度：Z轴先上抬5mm，再缓慢下降监测传感器
/// 参考 NeedleCalibrating.cs SearchSingleNeedleHeightAsync
/// </summary>
private async Task<double> SearchSingleNeedleHeightAsync(int zAxisId, CancellationToken token)
{
    try
    {
        double startHeight = _motionService.GetAxisPosition(zAxisId);
        double liftDistance = 5.0;

        _logger.Info($"[NeedleAligner] 开始Z方向高度搜索: 起始高度={startHeight:F3}");

        // 先上抬5mm
        await _motionService.MoveRelAsync(zAxisId, liftDistance, Parameters.SearchSpeed, token);
        await Task.Delay(100, token);

        // 缓慢下降搜索
        await _motionService.MoveRelAsync(zAxisId, -liftDistance, Parameters.FineSearchSpeed, token);

        // 等待Z轴传感器触发
        var startTime = DateTime.Now;
        while ((DateTime.Now - startTime).TotalSeconds < 60)
        {
            token.ThrowIfCancellationRequested();

            // 检查Z方向传感器（DI端口37和38同时为低电平表示触发）
            var sensor1 = _motionService.ReadDi(37);
            var sensor2 = _motionService.ReadDi(38);
            bool sensorTriggered = sensor1 && sensor2;

            if (sensorTriggered)
            {
                double needleHeight = _motionService.GetAxisPosition(zAxisId);
                _motionService.StopAxis(zAxisId);
                _logger.Info($"[NeedleAligner] 针尖高度: {needleHeight:F3}");
                return needleHeight;
            }

            await Task.Delay(10, token);
        }

        _logger.Warn("[NeedleAligner] 针尖高度搜索超时");
        return double.NaN;
    }
    catch (OperationCanceledException)
    {
        return double.NaN;
    }
    catch (Exception ex)
    {
        _logger.Error(ex, "[NeedleAligner] 单次高度搜索异常");
        return double.NaN;
    }
}
```

- [ ] **Step 11: 增加 MoveToXYPositionAsync 辅助方法**

```csharp
/// <summary>
/// 移动到指定XY位置（插补运动）
/// </summary>
private async Task MoveToXYPositionAsync(double targetX, double targetY, double velocity, CancellationToken token = default)
{
    await _motionService.MoveLineAbsAsync(
        0,
        new[] { AxisDxId, AxisDyId },
        new[] { targetX, targetY },
        velocity, token);
}
```

- [ ] **Step 12: 重写 StopCalibration 使用 IMotionService**

将 `StopCalibration`（约 L498-L514）替换为：
```csharp
/// <summary>
/// 停止校准运动
/// </summary>
private void StopCalibration()
{
    try
    {
        _calibrationCts?.Cancel();
        _motionService.StopAxis(AxisDxId);
        _motionService.StopAxis(AxisDyId);
        _motionService.StopAxis(GetZAxisId(SystemNumber));
        CalibrationStatus = _localization.GetResourceOrDefault("NeedleAligner_Status_Stopped", "校准已停止");
        AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_CalibrationStopped", "针头校准已手动停止"));
    }
    catch (Exception ex)
    {
        AddLog(string.Format(
            _localization.GetResourceOrDefault("NeedleAligner_Log_StopError", "停止校准失败: {0}"),
            ex.Message));
    }
}
```

- [ ] **Step 13: 重写 TeachSearchPointAsync 使用 IMotionService**

将 `TeachSearchPointAsync`（约 L904-L946）替换为：
```csharp
/// <summary>
/// 示教搜索点：读取当前运动位置并写入对应搜索点
/// </summary>
private async Task TeachSearchPointAsync(int step)
{
    try
    {
        double x = _motionService.GetAxisPosition(AxisDxId);
        double y = _motionService.GetAxisPosition(AxisDyId);

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
    catch (Exception ex)
    {
        AddLog(string.Format(
            _localization.GetResourceOrDefault("NeedleAligner_Log_TeachSearchPointError", "搜索点示教失败: {0}"),
            ex.Message));
    }
    await Task.CompletedTask;
}
```

- [ ] **Step 14: 删除 stationId 变量**

在 `StartCalibrationAsync` 中删除 `var stationId = $"NeedleCalibration_System{SystemNumber}";`（约 L405），因为不再需要。

- [ ] **Step 15: 编译验证**

Run: `dotnet build Module\Module.csproj --no-restore`
Expected: 编译成功

---

### Task 3: 增加对针位置示教属性和命令

**Files:**
- Modify: `Module\Controls\Maintenance\NeedleAlignerViewModel.cs`

- [ ] **Step 1: 增加对针位置属性和示教命令**

在属性区域增加：
```csharp
/// <summary>系统1对针位置Dx</summary>
public double System1AlignX
{
    get => Parameters?.System1AlignPosition.X ?? 0;
    set
    {
        if (Parameters != null)
        {
            Parameters.System1AlignPosition = new PointF((float)value, Parameters.System1AlignPosition.Y, Parameters.System1AlignPosition.Z);
            RaisePropertyChanged();
        }
    }
}

/// <summary>系统1对针位置Dy</summary>
public double System1AlignY
{
    get => Parameters?.System1AlignPosition.Y ?? 0;
    set
    {
        if (Parameters != null)
        {
            Parameters.System1AlignPosition = new PointF(Parameters.System1AlignPosition.X, (float)value, Parameters.System1AlignPosition.Z);
            RaisePropertyChanged();
        }
    }
}

/// <summary>系统1对针位置Dz₂</summary>
public double System1AlignZ
{
    get => Parameters?.System1AlignPosition.Z ?? 0;
    set
    {
        if (Parameters != null)
        {
            Parameters.System1AlignPosition = new PointF(Parameters.System1AlignPosition.X, Parameters.System1AlignPosition.Y, (float)value);
            RaisePropertyChanged();
        }
    }
}

/// <summary>系统2对针位置Dx</summary>
public double System2AlignX
{
    get => Parameters?.System2AlignPosition.X ?? 0;
    set
    {
        if (Parameters != null)
        {
            Parameters.System2AlignPosition = new PointF((float)value, Parameters.System2AlignPosition.Y, Parameters.System2AlignPosition.Z);
            RaisePropertyChanged();
        }
    }
}

/// <summary>系统2对针位置Dy</summary>
public double System2AlignY
{
    get => Parameters?.System2AlignPosition.Y ?? 0;
    set
    {
        if (Parameters != null)
        {
            Parameters.System2AlignPosition = new PointF(Parameters.System2AlignPosition.X, (float)value, Parameters.System2AlignPosition.Z);
            RaisePropertyChanged();
        }
    }
}

/// <summary>系统2对针位置Dz₃</summary>
public double System2AlignZ
{
    get => Parameters?.System2AlignPosition.Z ?? 0;
    set
    {
        if (Parameters != null)
        {
            Parameters.System2AlignPosition = new PointF(Parameters.System2AlignPosition.X, Parameters.System2AlignPosition.Y, (float)value);
            RaisePropertyChanged();
        }
    }
}
```

增加命令声明：
```csharp
public DelegateCommand TeachSystem1AlignCommand { get; }
public DelegateCommand TeachSystem2AlignCommand { get; }
```

在构造函数中初始化命令：
```csharp
TeachSystem1AlignCommand = new DelegateCommand(
    async () => await TeachAlignPositionAsync(1),
    () => !IsCalibrating)
    .ObservesProperty(() => IsCalibrating);

TeachSystem2AlignCommand = new DelegateCommand(
    async () => await TeachAlignPositionAsync(2),
    () => !IsCalibrating)
    .ObservesProperty(() => IsCalibrating);
```

- [ ] **Step 2: 增加对针位置示教方法**

```csharp
/// <summary>
/// 示教对针位置：读取当前轴位置并写入对应系统的对针位置
/// 系统1读取 Dx/Dy/Dz₂，系统2读取 Dx/Dy/Dz₃
/// </summary>
private async Task TeachAlignPositionAsync(int systemNumber)
{
    try
    {
        double dx = _motionService.GetAxisPosition(AxisDxId);
        double dy = _motionService.GetAxisPosition(AxisDyId);
        double dz = _motionService.GetAxisPosition(GetZAxisId(systemNumber));

        if (systemNumber == 1)
        {
            Parameters.System1AlignPosition = new PointF((float)dx, (float)dy, (float)dz);
            RaisePropertyChanged(nameof(System1AlignX));
            RaisePropertyChanged(nameof(System1AlignY));
            RaisePropertyChanged(nameof(System1AlignZ));
        }
        else
        {
            Parameters.System2AlignPosition = new PointF((float)dx, (float)dy, (float)dz);
            RaisePropertyChanged(nameof(System2AlignX));
            RaisePropertyChanged(nameof(System2AlignY));
            RaisePropertyChanged(nameof(System2AlignZ));
        }

        var zAxisLabel = systemNumber == 1 ? "Dz₂" : "Dz₃";
        AddLog(string.Format(
            _localization.GetResourceOrDefault("NeedleAligner_Log_TeachAlignPosition", "系统{0}对针位置示教完成: Dx={1:F3}, Dy={2:F3}, {3}={4:F3}"),
            systemNumber, dx, dy, zAxisLabel, dz));
    }
    catch (Exception ex)
    {
        AddLog(string.Format(
            _localization.GetResourceOrDefault("NeedleAligner_Log_TeachAlignPositionError", "对针位置示教失败: {0}"),
            ex.Message));
    }
    await Task.CompletedTask;
}
```

- [ ] **Step 3: 更新 OnParametersPropertyChanged 监听对针位置变更**

在 `OnParametersPropertyChanged` 中增加对 `System1AlignPosition` 和 `System2AlignPosition` 的监听：
```csharp
private void OnParametersPropertyChanged(object sender, PropertyChangedEventArgs e)
{
    if (e.PropertyName is nameof(NeedleCalibrationParams.ReferenceXYZ)
        or nameof(NeedleCalibrationParams.CurrentXYZ))
    {
        RaiseCalibrationDeltaAndCalculatedChanged();
    }
    else if (e.PropertyName == nameof(NeedleCalibrationParams.System1AlignPosition))
    {
        RaisePropertyChanged(nameof(System1AlignX));
        RaisePropertyChanged(nameof(System1AlignY));
        RaisePropertyChanged(nameof(System1AlignZ));
    }
    else if (e.PropertyName == nameof(NeedleCalibrationParams.System2AlignPosition))
    {
        RaisePropertyChanged(nameof(System2AlignX));
        RaisePropertyChanged(nameof(System2AlignY));
        RaisePropertyChanged(nameof(System2AlignZ));
    }
}
```

- [ ] **Step 4: 编译验证**

Run: `dotnet build Module\Module.csproj --no-restore`
Expected: 编译成功

---

### Task 4: 更新 NeedleAlignerView.xaml

**Files:**
- Modify: `Module\Controls\Maintenance\NeedleAlignerView.xaml`

- [ ] **Step 1: 在左侧列搜索点卡片之后增加对针位置卡片**

在搜索点设置卡片（`</materialDesign:Card>` 约 L220）之后、参考坐标卡片之前，增加对针位置卡片：
```xml
<!-- 对针位置卡片 -->
<materialDesign:Card UniformCornerRadius="8" Padding="12" Margin="0,0,0,6"
                     Background="{DynamicResource MaterialDesignCardBackground}">
    <StackPanel>
        <Grid Margin="0,0,0,8">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>
            <materialDesign:PackIcon Kind="Needle" Width="18" Height="18"
                                     Foreground="{DynamicResource PrimaryHueMidBrush}"
                                     Margin="0,0,8,0" VerticalAlignment="Top" />
            <TextBlock Grid.Column="1"
                       Text="{lang:Lang NeedleAligner_AlignPosition}"
                       Style="{StaticResource SideCardHeaderTextStyle}"
                       Foreground="{DynamicResource PrimaryHueMidBrush}" />
        </Grid>

        <!-- 系统1对针位置 (Dx, Dy, Dz₂) -->
        <TextBlock Text="{lang:Lang NeedleAligner_System1AlignPosition}"
                   FontSize="11" FontWeight="SemiBold" Foreground="#5E35B1" Margin="0,0,0,4" />
        <Grid Style="{StaticResource ParamRowStyle}">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>
            <materialDesign:PackIcon Grid.Column="0" Kind="AxisXArrow" Width="14" Height="14"
                                     Foreground="#E53935" VerticalAlignment="Center" Margin="0,0,2,0" />
            <TextBox Grid.Column="1" Text="{Binding System1AlignX, StringFormat=F3}"
                     Style="{StaticResource ParamTextBoxStyle}" VerticalContentAlignment="Center" MinWidth="0" />
            <TextBox Grid.Column="2" Text="{Binding System1AlignY, StringFormat=F3}"
                     Style="{StaticResource ParamTextBoxStyle}" VerticalContentAlignment="Center" MinWidth="0" />
            <TextBox Grid.Column="3" Text="{Binding System1AlignZ, StringFormat=F3}"
                     Style="{StaticResource ParamTextBoxStyle}" VerticalContentAlignment="Center" MinWidth="0" />
            <Button Grid.Column="4" Command="{Binding TeachSystem1AlignCommand}"
                    Style="{StaticResource MaterialDesignRaisedButton}"
                    Padding="6,2" Margin="4,0,0,0"
                    materialDesign:ButtonAssist.CornerRadius="4"
                    ToolTip="{lang:Lang NeedleAligner_TeachSystem1Align}">
                <materialDesign:PackIcon Kind="CrosshairsGps" Width="14" Height="14" />
            </Button>
        </Grid>

        <!-- 系统2对针位置 (Dx, Dy, Dz₃) -->
        <TextBlock Text="{lang:Lang NeedleAligner_System2AlignPosition}"
                   FontSize="11" FontWeight="SemiBold" Foreground="#1E88E5" Margin="0,6,0,4" />
        <Grid Style="{StaticResource ParamRowStyle}">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>
            <materialDesign:PackIcon Grid.Column="0" Kind="AxisXArrow" Width="14" Height="14"
                                     Foreground="#E53935" VerticalAlignment="Center" Margin="0,0,2,0" />
            <TextBox Grid.Column="1" Text="{Binding System2AlignX, StringFormat=F3}"
                     Style="{StaticResource ParamTextBoxStyle}" VerticalContentAlignment="Center" MinWidth="0" />
            <TextBox Grid.Column="2" Text="{Binding System2AlignY, StringFormat=F3}"
                     Style="{StaticResource ParamTextBoxStyle}" VerticalContentAlignment="Center" MinWidth="0" />
            <TextBox Grid.Column="3" Text="{Binding System2AlignZ, StringFormat=F3}"
                     Style="{StaticResource ParamTextBoxStyle}" VerticalContentAlignment="Center" MinWidth="0" />
            <Button Grid.Column="4" Command="{Binding TeachSystem2AlignCommand}"
                    Style="{StaticResource MaterialDesignRaisedButton}"
                    Padding="6,2" Margin="4,0,0,0"
                    materialDesign:ButtonAssist.CornerRadius="4"
                    ToolTip="{lang:Lang NeedleAligner_TeachSystem2Align}">
                <materialDesign:PackIcon Kind="CrosshairsGps" Width="14" Height="14" />
            </Button>
        </Grid>
    </StackPanel>
</materialDesign:Card>
```

- [ ] **Step 2: 替换运动参数卡片中的 NeedleBaseHeight 为 SafeHeight**

将 NeedleBaseHeight 的 Grid（约 L367-L385）替换为：
```xml
<Grid Style="{StaticResource ParamRowStyle}">
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />
        <RowDefinition Height="Auto" />
    </Grid.RowDefinitions>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>
    <materialDesign:PackIcon Grid.Row="0" Grid.Column="0" Kind="ArrowUpBold" Width="16" Height="16"
                             Foreground="#9E9E9E" VerticalAlignment="Top" Margin="0,2,4,0" />
    <TextBlock Grid.Row="0" Grid.Column="1"
               Text="{lang:Lang NeedleAligner_SafeHeight}"
               Style="{StaticResource ParamLabelStyle}"
               ToolTip="{lang:Lang NeedleAligner_SafeHeightTip}" />
    <TextBox Grid.Row="1" Grid.Column="0" Grid.ColumnSpan="2"
             Text="{Binding Parameters.SafeHeight, StringFormat=F3}"
             Style="{StaticResource ParamTextBoxStyle}" />
</Grid>
```

- [ ] **Step 3: 编译验证**

Run: `dotnet build Module\Module.csproj --no-restore`
Expected: 编译成功

---

### Task 5: 更新语言文件

**Files:**
- Modify: `MainApp\Languages\Strings.zh-CN.xaml`
- Modify: `MainApp\Languages\Strings.en-US.xaml`

- [ ] **Step 1: 在 zh-CN 语言文件中增加/修改键**

在 NeedleAligner 相关键区域（约 L1738 之后）增加：
```xml
<sys:String x:Key="NeedleAligner_SafeHeight">安全高度 (mm)</sys:String>
<sys:String x:Key="NeedleAligner_SafeHeightTip">对针运动中的安全Z高度，运动前先抬升到此高度</sys:String>
<sys:String x:Key="NeedleAligner_AlignPosition">对针位置</sys:String>
<sys:String x:Key="NeedleAligner_System1AlignPosition">系统1 (Dx, Dy, Dz₂)</sys:String>
<sys:String x:Key="NeedleAligner_System2AlignPosition">系统2 (Dx, Dy, Dz₃)</sys:String>
<sys:String x:Key="NeedleAligner_TeachSystem1Align">示教系统1对针位置</sys:String>
<sys:String x:Key="NeedleAligner_TeachSystem2Align">示教系统2对针位置</sys:String>
<sys:String x:Key="NeedleAligner_Log_TeachAlignPosition">系统{0}对针位置示教完成: Dx={1:F3}, Dy={2:F3}, {3}={4:F3}</sys:String>
<sys:String x:Key="NeedleAligner_Log_TeachAlignPositionError">对针位置示教失败: {0}</sys:String>
<sys:String x:Key="NeedleAligner_Status_MovingToSafeHeight">抬升到安全高度...</sys:String>
<sys:String x:Key="NeedleAligner_Status_MovingToAlignPosition">移动到对针位置...</sys:String>
<sys:String x:Key="NeedleAligner_Status_SearchingCenter">搜索中心点XY...</sys:String>
<sys:String x:Key="NeedleAligner_Status_SearchCenterFailed">搜索中心点失败</sys:String>
<sys:String x:Key="NeedleAligner_Status_SearchingHeight">搜索针尖高度...</sys:String>
<sys:String x:Key="NeedleAligner_Status_SearchHeightFailed">搜索针尖高度失败</sys:String>
<sys:String x:Key="NeedleAligner_Status_Calculating">计算补偿值...</sys:String>
<sys:String x:Key="NeedleAligner_Status_SearchingXEdge">在点{0}进行X方向搜索...</sys:String>
<sys:String x:Key="NeedleAligner_Status_SearchingYEdge">在点{0}进行Y方向搜索...</sys:String>
<sys:String x:Key="NeedleAligner_Status_ZSearch">第 {0}/{1} 次高度搜索...</sys:String>
<sys:String x:Key="NeedleAligner_Log_SearchEdgeFailed">点{0}边缘搜索失败</sys:String>
<sys:String x:Key="NeedleAligner_Log_CenterPointFound">中心点: X={0:F3}, Y={1:F3}</sys:String>
```

删除（不再需要）：
```xml
<sys:String x:Key="NeedleAligner_NeedleBaseHeight">针头基准高度 (mm)</sys:String>
<sys:String x:Key="NeedleAligner_NeedleBaseHeightTip">针头在零位时的基准高度，单位mm</sys:String>
```

- [ ] **Step 2: 在 en-US 语言文件中增加/修改键**

在 NeedleAligner 相关键区域增加：
```xml
<sys:String x:Key="NeedleAligner_SafeHeight">Safe Height (mm)</sys:String>
<sys:String x:Key="NeedleAligner_SafeHeightTip">Safe Z height for needle alignment, axis lifts to this height before horizontal move</sys:String>
<sys:String x:Key="NeedleAligner_AlignPosition">Alignment Position</sys:String>
<sys:String x:Key="NeedleAligner_System1AlignPosition">System 1 (Dx, Dy, Dz₂)</sys:String>
<sys:String x:Key="NeedleAligner_System2AlignPosition">System 2 (Dx, Dy, Dz₃)</sys:String>
<sys:String x:Key="NeedleAligner_TeachSystem1Align">Teach System 1 Alignment</sys:String>
<sys:String x:Key="NeedleAligner_TeachSystem2Align">Teach System 2 Alignment</sys:String>
<sys:String x:Key="NeedleAligner_Log_TeachAlignPosition">System {0} alignment position taught: Dx={1:F3}, Dy={2:F3}, {3}={4:F3}</sys:String>
<sys:String x:Key="NeedleAligner_Log_TeachAlignPositionError">Alignment position teach failed: {0}</sys:String>
<sys:String x:Key="NeedleAligner_Status_MovingToSafeHeight">Moving to safe height...</sys:String>
<sys:String x:Key="NeedleAligner_Status_MovingToAlignPosition">Moving to alignment position...</sys:String>
<sys:String x:Key="NeedleAligner_Status_SearchingCenter">Searching center point XY...</sys:String>
<sys:String x:Key="NeedleAligner_Status_SearchCenterFailed">Center point search failed</sys:String>
<sys:String x:Key="NeedleAligner_Status_SearchingHeight">Searching needle tip height...</sys:String>
<sys:String x:Key="NeedleAligner_Status_SearchHeightFailed">Needle tip height search failed</sys:String>
<sys:String x:Key="NeedleAligner_Status_Calculating">Calculating compensation...</sys:String>
<sys:String x:Key="NeedleAligner_Status_SearchingXEdge">Searching X edge at point {0}...</sys:String>
<sys:String x:Key="NeedleAligner_Status_SearchingYEdge">Searching Y edge at point {0}...</sys:String>
<sys:String x:Key="NeedleAligner_Status_ZSearch">Z search {0}/{1}...</sys:String>
<sys:String x:Key="NeedleAligner_Log_SearchEdgeFailed">Edge search failed at point {0}</sys:String>
<sys:String x:Key="NeedleAligner_Log_CenterPointFound">Center point: X={0:F3}, Y={1:F3}</sys:String>
```

删除：
```xml
<sys:String x:Key="NeedleAligner_NeedleBaseHeight">Needle Base Height (mm)</sys:String>
<sys:String x:Key="NeedleAligner_NeedleBaseHeightTip">Needle base height at zero position in mm</sys:String>
```

- [ ] **Step 3: 编译验证**

Run: `dotnet build MainApp\MainApp.csproj --no-restore`
Expected: 编译成功

---

### Task 6: 更新版本修改记录

**Files:**
- Modify: `版本修改记录.txt`

- [ ] **Step 1: 在版本修改记录末尾追加新版本条目**

追加以下内容：
```
========================================
v2026.06.01 - NeedleAligner重构
========================================
[NeedleAligner] 运动接口从IPositionMotionController切换为IMotionService
[NeedleAligner] 安全高度从motion parameters的SafeHeight参数获取，替代硬编码SafeHeightOffset
[NeedleAligner] 寻针流程参考ExecuteNeedleCalibrationAsync：抬升安全高度→移动对针位置→下降寻针高度→四点边缘搜索→Z高度搜索→计算补偿
[NeedleAligner] 增加系统1(Dx,Dy,Dz₂)和系统2(Dx,Dy,Dz₃)对针位置示教功能
[NeedleAligner] 运动参数：增加安全高度(SafeHeight)，删除NeedleBaseHeight
[NeedleAligner] UI增加对针位置卡片，显示系统1/系统2对针位置及示教按钮
[NeedleCalibrationParams] 删除NeedleBaseHeight，增加SafeHeight、System1AlignPosition、System2AlignPosition
[多语言] 新增SafeHeight/AlignPosition/校准状态相关中英文语言键
```

---

## 自检清单

### 1. 需求覆盖

| 需求 | 对应任务 |
|------|---------|
| NeedleAlignerViewModel 运动接口使用 IMotionService | Task 2 Step 1 |
| IPositionMotionController 专属于位置编辑器 | Task 2 Step 1 (删除依赖) |
| 安全高度从 motion parameters 的 SafeHeight 获取 | Task 1 Step 1, Task 2 Step 3 |
| 对针轴: 系统1(Dx Dy Dz₂) 系统2(Dx Dy Dz₃) | Task 2 Step 2, Task 3 |
| 寻针动作: 先抬起到安全高度→移动到对针位置→下降到对针高度→开始寻针 | Task 2 Step 4-10 |
| 寻针动作参考 ExecuteNeedleCalibrationAsync | Task 2 Step 4-10 |
| UI增加系统1和系统2对针位置示教按钮 | Task 4 Step 1 |
| motion parameters增加安全高度 | Task 1 Step 1, Task 4 Step 2 |
| 删除 Needle Base Height(mm) | Task 1 Step 1, Task 4 Step 2, Task 5 |

### 2. 占位符扫描

无 TBD、TODO、implement later 等占位符。所有步骤包含完整代码。

### 3. 类型一致性

- `NeedleCalibrationParams.SafeHeight` (double) → XAML 绑定 `Parameters.SafeHeight` ✓
- `NeedleCalibrationParams.System1AlignPosition` (PointF) → ViewModel 属性 `System1AlignX/Y/Z` (double) ✓
- `IMotionService.MoveAbsAsync(int axisId, ...)` → `GetZAxisId(systemNumber)` 返回 int ✓
- `IMotionService.MoveRelAsync(int axisId, double distance, ...)` → SearchSingleEdgeAsync/SearchSingleNeedleHeightAsync ✓
- `IMotionService.ReadDi(int port)` → WaitForSensorTriggerAsync ✓
- 语言键 `NeedleAligner_SafeHeight` 在 zh-CN 和 en-US 中一致 ✓
