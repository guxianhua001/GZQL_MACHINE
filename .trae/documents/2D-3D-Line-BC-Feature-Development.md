# 2D/3D线条(B/C)功能开发与优化 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现2D/3D线条(B/C)编辑器的参数模式管理（单点/连续插补切换）和第六步执行走胶的双模式互斥执行功能

**Architecture:** 在现有CadPointEditorViewModel六步工作流基础上，扩展Step3参数面板支持模式切换UI，扩展Step6执行面板支持互斥执行模式选择。参数体系复用DotProcessParams，执行层扩展DispenseExecuteService支持单点逐点执行流程。

**Tech Stack:** WPF + Prism + MaterialDesignInXAML + MotionControl连续插补API

---

## 文件结构映射

| 操作 | 文件路径 | 职责 |
|------|----------|------|
| 新建 | `Core/Models/LineDispenseMode.cs` | 线条点胶模式枚举 |
| 修改 | `Core/Models/DispenseSegment.cs` | 扩展单点模式参数字段 |
| 修改 | `Module/Controls/Cad/CadPointEditorViewModel.cs` | 添加模式管理、参数绑定、执行分发 |
| 修改 | `Module/Controls/Cad/Step3EditParamsPanel.xaml` | 模式切换UI + 条件参数面板 |
| 修改 | `Module/Controls/Cad/Step6ExecutePanel.xaml` | 互斥执行模式选择UI |
| 修改 | `Module/Services/IDispenseExecuteService.cs` | 新增单点线条执行接口 |
| 修改 | `Module/Services/DispenseExecuteService.cs` | 实现单点线条执行 + 增强连续插补 |
| 修改 | `MainApp/Languages/Strings.zh-CN.xaml` | 中文语言资源 |
| 修改 | `MainApp/Languages/Strings.en-US.xaml` | 英文语言资源 |

---

### Task 1: 新建 LineDispenseMode 枚举

**Files:**
- Create: `Core/Models/LineDispenseMode.cs`

- [ ] **Step 1: 创建枚举文件**

```csharp
namespace Core.Models
{
    /// <summary>
    /// 线条点胶操作模式——决定参数显示和执行方式
    /// </summary>
    public enum LineDispenseMode
    {
        /// <summary>单点模式：逐点点胶，复用点涂(A)工艺参数体系</summary>
        SinglePoint,
        /// <summary>连续插补模式：连续插补走胶，使用线段工艺参数</summary>
        ContinuousInterpolation
    }
}
```

---

### Task 2: 扩展 DispenseSegment 模型 — 添加单点模式参数

**Files:**
- Modify: `Core/Models/DispenseSegment.cs`

- [ ] **Step 1: 在 DispenseSegment 的工艺参数区域添加单点模式所需字段**

在 `#region 工艺参数` 区域末尾（`GlueTriggerOffsetMm` 属性之后）添加：

```csharp
private double _dispenseTime = 180.0;
/// <summary>出胶时间 ms（范围 10~5000，单点模式下控制胶点大小）</summary>
public double DispenseTime
{
    get => _dispenseTime;
    set => SetProperty(ref _dispenseTime, Math.Clamp(value, 10.0, 5000.0));
}

private double _approachHeight = 3.0;
/// <summary>逼近高度 mm（范围 0~50，快速下降到此高度后转为慢速逼近）</summary>
public double ApproachHeight
{
    get => _approachHeight;
    set => SetProperty(ref _approachHeight, Math.Clamp(value, 0.0, 50.0));
}

private double _dispensingPressure = 0.30;
/// <summary>点胶气压 MPa（范围 0.1~1.0）</summary>
public double DispensingPressure
{
    get => _dispensingPressure;
    set => SetProperty(ref _dispensingPressure, Math.Clamp(value, 0.1, 1.0));
}

private double _suckBackTime = 100.0;
/// <summary>回吸时间 ms（范围 10~500，防止滴漏）</summary>
public double SuckBackTime
{
    get => _suckBackTime;
    set => SetProperty(ref _suckBackTime, Math.Clamp(value, 10.0, 500.0));
}
```

---

### Task 3: 更新 CadPointEditorViewModel — 模式管理与参数绑定

**Files:**
- Modify: `Module/Controls/Cad/CadPointEditorViewModel.cs`

- [ ] **Step 1: 添加 LineDispenseMode 绑定属性**

在 `#region 绑定属性 — Step5 & Step6: 执行` 区域中，替换现有的 `IsSinglePointMode` 属性为：

```csharp
private LineDispenseMode _lineDispenseMode = LineDispenseMode.ContinuousInterpolation;
/// <summary>线条点胶操作模式（单点/连续插补）</summary>
public LineDispenseMode LineDispenseMode
{
    get => _lineDispenseMode;
    set
    {
        if (SetProperty(ref _lineDispenseMode, value))
        {
            RaisePropertyChanged(nameof(IsSinglePointMode));
            RaisePropertyChanged(nameof(IsContinuousInterpolationMode));
            RaisePropertyChanged(nameof(CanExecute));
        }
    }
}

/// <summary>是否为单点模式（便捷属性，供UI绑定）</summary>
public bool IsSinglePointMode
{
    get => _lineDispenseMode == LineDispenseMode.SinglePoint;
    set { if (value) LineDispenseMode = LineDispenseMode.SinglePoint; }
}

/// <summary>是否为连续插补模式（便捷属性，供UI绑定）</summary>
public bool IsContinuousInterpolationMode
{
    get => _lineDispenseMode == LineDispenseMode.ContinuousInterpolation;
    set { if (value) LineDispenseMode = LineDispenseMode.ContinuousInterpolation; }
}
```

- [ ] **Step 2: 添加单点模式全局工艺参数属性**

在 `#region 绑定属性 — Step5 & Step6: 执行` 区域中添加：

```csharp
private DotProcessParams _singlePointProcessParams = new DotProcessParams();
/// <summary>单点模式全局工艺参数（复用点涂A参数体系）</summary>
public DotProcessParams SinglePointProcessParams
{
    get => _singlePointProcessParams;
    set => SetProperty(ref _singlePointProcessParams, value);
}
```

- [ ] **Step 3: 添加待机高度属性（单点模式循环结束后Z轴抬升目标）**

```csharp
private double _standbyHeight = 10.0;
/// <summary>待机高度 mm（单点模式循环结束后Z轴抬升目标，范围 0~200）</summary>
public double StandbyHeight
{
    get => _standbyHeight;
    set => SetProperty(ref _standbyHeight, Math.Clamp(value, 0.0, 200.0));
}
```

- [ ] **Step 4: 更新 ExecutePath 方法 — 基于模式分发执行**

替换现有的 `ExecutePath()` 方法：

```csharp
/// <summary>
/// 执行走胶——根据 LineDispenseMode 分发到不同的执行路径
/// 连续插补模式：调用 DispenseExecuteService.ExecutePathAsync
/// 单点模式：调用 DispenseExecuteService.ExecuteSinglePointLineAsync
/// </summary>
private void ExecutePath()
{
    var enabledSegments = Segments.Where(s => s.IsEnabled).ToList();
    if (enabledSegments.Count == 0)
    {
        GlobalStatus = L("CadPoint_Error_NoExecutableTrajectory");
        return;
    }

    string modeDesc = LineDispenseMode == LineDispenseMode.SinglePoint
        ? L("LineBC_Mode_SinglePoint")
        : L("LineBC_Mode_ContinuousInterpolation");

    GlobalStatus = string.Format(L("LineBC_Status_PrepareExec"), enabledSegments.Count, modeDesc);
    OnExecuteRequestRequested();
}
```

- [ ] **Step 5: 添加模式切换命令**

在 `#region 委托命令 — Step3: 参数编辑与批量操作` 区域添加：

```csharp
private DelegateCommand<LineDispenseMode> _switchDispenseModeCommand;
/// <summary>切换点胶模式命令</summary>
public DelegateCommand<LineDispenseMode> SwitchDispenseModeCommand =>
    _switchDispenseModeCommand ??= new DelegateCommand<LineDispenseMode>(ExecuteSwitchDispenseMode);

/// <summary>切换点胶模式——更新参数面板显示</summary>
private void ExecuteSwitchDispenseMode(LineDispenseMode mode)
{
    LineDispenseMode = mode;
    GlobalStatus = mode == LineDispenseMode.SinglePoint
        ? L("LineBC_Status_SwitchToSinglePoint")
        : L("LineBC_Status_SwitchToContinuousInterpolation");
}
```

---

### Task 4: 更新 Step3EditParamsPanel — 模式切换UI + 条件参数面板

**Files:**
- Modify: `Module/Controls/Cad/Step3EditParamsPanel.xaml`

- [ ] **Step 1: 在标题下方添加模式切换区域**

在 `<TextBlock Text="{lang:Lang Step3_Description}".../>` 之后、批量操作工具栏之前添加：

```xml
<!-- 参数模式切换 -->
<materialDesign:Card Padding="10" Margin="0,0,0,8" Background="#E3F2FD">
    <StackPanel>
        <TextBlock Text="{lang:Lang Step3_Section_DispenseMode}" FontWeight="Bold" FontSize="12" Margin="0,0,0,6"/>
        <StackPanel Orientation="Horizontal">
            <RadioButton Content="{lang:Lang Step3_Radio_ContinuousInterpolation}"
                         IsChecked="{Binding IsContinuousInterpolationMode}"
                         GroupName="DispenseMode" Margin="0,0,16,0"
                         FontSize="12"/>
            <RadioButton Content="{lang:Lang Step3_Radio_SinglePoint}"
                         IsChecked="{Binding IsSinglePointMode}"
                         GroupName="DispenseMode"
                         FontSize="12"/>
        </StackPanel>
        <TextBlock Text="{lang:Lang Step3_Desc_ModeSwitch}"
                   FontSize="10" Foreground="#9E9E9E" TextWrapping="Wrap" Margin="0,4,0,0"/>
    </StackPanel>
</materialDesign:Card>
```

- [ ] **Step 2: 添加单点模式参数面板**

在选中段参数编辑区 (`materialDesign:Card` with `HasSelectedSegment` visibility) 之后添加：

```xml
<!-- 单点模式全局工艺参数面板 -->
<materialDesign:Card Padding="10" Margin="0,0,0,8"
                      Visibility="{Binding IsSinglePointMode, Converter={StaticResource BoolToVisConv}, FallbackValue=Collapsed}">
    <StackPanel>
        <TextBlock Text="{lang:Lang Step3_Section_SinglePointParams}" FontWeight="Bold" FontSize="12" Margin="0,0,0,6"/>
        <Grid Margin="0,2">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>
            <!-- 运动参数 -->
            <TextBlock Grid.Row="0" Grid.Column="0" Text="{lang:Lang Step3_Label_MoveSpeed}" VerticalAlignment="Center" Margin="0,3,8,0"/>
            <TextBox Grid.Row="0" Grid.Column="1" Text="{Binding SinglePointProcessParams.MoveSpeed, StringFormat=F1, UpdateSourceTrigger=LostFocus}" Margin="0,2"/>
            <TextBlock Grid.Row="0" Grid.Column="2" Text="{lang:Lang Step3_Label_SafeHeight}" VerticalAlignment="Center" Margin="8,3,8,0"/>
            <TextBox Grid.Row="0" Grid.Column="3" Text="{Binding SinglePointProcessParams.SafeHeight, StringFormat=F1, UpdateSourceTrigger=LostFocus}" Margin="0,2"/>
            <TextBlock Grid.Row="0" Grid.Column="4" Text="{lang:Lang Step3_Label_ApproachHeight}" VerticalAlignment="Center" Margin="8,3,8,0"/>
            <TextBox Grid.Row="0" Grid.Column="5" Text="{Binding SinglePointProcessParams.ApproachHeight, StringFormat=F1, UpdateSourceTrigger=LostFocus}" Margin="0,2"/>
            <!-- 出胶参数 -->
            <TextBlock Grid.Row="1" Grid.Column="0" Text="{lang:Lang Step3_Label_DispenseTime}" VerticalAlignment="Center" Margin="0,3,8,0"/>
            <TextBox Grid.Row="1" Grid.Column="1" Text="{Binding SinglePointProcessParams.DispenseTime, StringFormat=F1, UpdateSourceTrigger=LostFocus}" Margin="0,2"/>
            <TextBlock Grid.Row="1" Grid.Column="2" Text="{lang:Lang Step3_Label_PreDispenseDelay}" VerticalAlignment="Center" Margin="8,3,8,0"/>
            <TextBox Grid.Row="1" Grid.Column="3" Text="{Binding SinglePointProcessParams.PreDispenseDelay, StringFormat=F1, UpdateSourceTrigger=LostFocus}" Margin="0,2"/>
            <TextBlock Grid.Row="1" Grid.Column="4" Text="{lang:Lang Step3_Label_PostDelay}" VerticalAlignment="Center" Margin="8,3,8,0"/>
            <TextBox Grid.Row="1" Grid.Column="5" Text="{Binding SinglePointProcessParams.PostDelay, StringFormat=F1, UpdateSourceTrigger=LostFocus}" Margin="0,2"/>
            <!-- 阀控参数 -->
            <TextBlock Grid.Row="2" Grid.Column="0" Text="{lang:Lang Step3_Label_DispensingPressure}" VerticalAlignment="Center" Margin="0,3,8,0"/>
            <TextBox Grid.Row="2" Grid.Column="1" Text="{Binding SinglePointProcessParams.DispensingPressure, StringFormat=F2, UpdateSourceTrigger=LostFocus}" Margin="0,2"/>
            <TextBlock Grid.Row="2" Grid.Column="2" Text="{lang:Lang Step3_Label_SuckBackTime}" VerticalAlignment="Center" Margin="8,3,8,0"/>
            <TextBox Grid.Row="2" Grid.Column="3" Text="{Binding SinglePointProcessParams.SuckBackTime, StringFormat=F1, UpdateSourceTrigger=LostFocus}" Margin="0,2"/>
            <TextBlock Grid.Row="2" Grid.Column="4" Text="{lang:Lang Step3_Label_GlueTriggerOffset}" VerticalAlignment="Center" Margin="8,3,8,0"/>
            <TextBox Grid.Row="2" Grid.Column="5" Text="{Binding SinglePointProcessParams.DotGlueTriggerOffsetMm, StringFormat=F1, UpdateSourceTrigger=LostFocus}" Margin="0,2"/>
            <!-- 高度参数 -->
            <TextBlock Grid.Row="3" Grid.Column="0" Text="{lang:Lang Step3_Label_TeachHeight}" VerticalAlignment="Center" Margin="0,3,8,0"/>
            <TextBox Grid.Row="3" Grid.Column="1" Text="{Binding SinglePointProcessParams.TeachHeight, StringFormat=F3, UpdateSourceTrigger=LostFocus}" Margin="0,2"/>
            <TextBlock Grid.Row="3" Grid.Column="2" Text="{lang:Lang Step3_Label_HeightCompensation}" VerticalAlignment="Center" Margin="8,3,8,0"/>
            <TextBox Grid.Row="3" Grid.Column="3" Text="{Binding SinglePointProcessParams.HeightCompensation, StringFormat=F3, UpdateSourceTrigger=LostFocus}" Margin="0,2"/>
            <TextBlock Grid.Row="3" Grid.Column="4" Text="{lang:Lang Step3_Label_EffectiveHeight}" VerticalAlignment="Center" Margin="8,3,8,0" Foreground="#757575"/>
            <TextBlock Grid.Row="3" Grid.Column="5" Text="{Binding SinglePointProcessParams.EffectiveZHeight, StringFormat=F3}" VerticalAlignment="Center" Margin="0,3,0,0" Foreground="#424242" FontWeight="Medium"/>
            <!-- 拐角减速 -->
            <TextBlock Grid.Row="4" Grid.Column="0" Text="{lang:Lang Step3_Label_CornerDecel}" VerticalAlignment="Center" Margin="0,3,8,0"/>
            <TextBox Grid.Row="4" Grid.Column="1" Text="{Binding SinglePointProcessParams.CornerDecel, StringFormat=F2, UpdateSourceTrigger=LostFocus}" Margin="0,2"/>
        </Grid>
    </StackPanel>
</materialDesign:Card>
```

- [ ] **Step 3: 修改连续插补模式下的 MoveSpeed 标签**

在现有选中段参数编辑区中，将 MoveSpeed 的标签替换为条件显示：

将原来的：
```xml
<TextBlock Grid.Row="0" Grid.Column="0" Text="{lang:Lang Step3_Label_MoveSpeed}" .../>
```
替换为：
```xml
<TextBlock Grid.Row="0" Grid.Column="0" VerticalAlignment="Center" Margin="0,3,8,0">
    <Run Text="{lang:Lang Step3_Label_DispenseSpeed}"/>
</TextBlock>
```

这里使用新的语言键 `Step3_Label_DispenseSpeed`（"出胶速度"），仅在连续插补模式下显示此面板。

- [ ] **Step 4: 为连续插补参数面板添加模式可见性控制**

在现有选中段参数编辑区的 `materialDesign:Card` 上添加可见性绑定，仅在连续插补模式下显示：

将 `Visibility="{Binding HasSelectedSegment, Converter={StaticResource BoolToVisConv}, FallbackValue=Collapsed}"` 
改为需要同时满足 HasSelectedSegment 和 IsContinuousInterpolationMode。

由于 WPF 不支持多条件 BooleanToVisibility 转换，需要添加一个 MultiBinding 或使用新的复合属性。在 ViewModel 中添加：

```csharp
/// <summary>是否显示连续插补段参数编辑区（有选中段 且 为连续插补模式）</summary>
public bool ShowContinuousInterpolationParams => HasSelectedSegment && IsContinuousInterpolationMode;
```

并在 HasSelectedSegment 和 IsContinuousInterpolationMode 变更时 RaisePropertyChanged。

XAML 中改为：
```xml
Visibility="{Binding ShowContinuousInterpolationParams, Converter={StaticResource BoolToVisConv}, FallbackValue=Collapsed}"
```

---

### Task 5: 更新 IDispenseExecuteService 接口 — 新增单点线条执行方法

**Files:**
- Modify: `Module/Services/IDispenseExecuteService.cs`

- [ ] **Step 1: 添加单点线条执行方法签名**

在 `ExecuteSinglePointAsync` 方法之后添加：

```csharp
/// <summary>
/// 单点模式执行线条走胶：逐点下降→开胶→出胶→关胶→抬升→循环
/// 工艺流程：单点→Z抬升→移动至接近高度→减速到示教高度+偏移(同步检测开胶距离)→
/// 执行点胶(起点延时)→点胶完成(收胶延时)→抬升至安全高度→循环→结束后Z抬升至待机位
/// </summary>
/// <param name="segments">轨迹段集合</param>
/// <param name="processParams">单点模式工艺参数（复用点涂A参数体系）</param>
/// <param name="standbyHeight">待机高度mm（循环结束后Z轴抬升目标）</param>
/// <param name="token">取消令牌</param>
Task ExecuteSinglePointLineAsync(IEnumerable<DispenseSegment> segments, DotProcessParams processParams, double standbyHeight, CancellationToken token = default);
```

---

### Task 6: 实现 DispenseExecuteService 单点线条执行 + 增强连续插补

**Files:**
- Modify: `Module/Services/DispenseExecuteService.cs`

- [ ] **Step 1: 实现 ExecuteSinglePointLineAsync 方法**

在 `ExecuteSinglePointAsync` 方法之后添加：

```csharp
/// <summary>
/// 单点模式执行线条走胶——逐点执行，遵循行业标准工艺流程
/// 流程：单点→Z抬升→XY定位→Z两段式下降(同步检测开胶距离)→出胶(起点延时)→
/// 关胶(收胶延时)→抬升至安全高度→循环→结束后Z抬升至待机位
/// </summary>
public async Task ExecuteSinglePointLineAsync(
    IEnumerable<DispenseSegment> segments,
    DotProcessParams processParams,
    double standbyHeight,
    CancellationToken token = default)
{
    SetRunning(true);
    PublishStatus("Running");

    try
    {
        var segmentList = segments.Where(s => s.IsEnabled).ToList();
        int total = segmentList.Count;
        _logger?.Info($"[DispenseExecute] 开始单点线条走胶，共 {total} 段");

        double moveSpeed = processParams.MoveSpeed;
        double safeHeight = processParams.SafeHeight;
        double approachOffset = processParams.ApproachHeight;
        double slowVel = moveSpeed * processParams.CornerDecel;
        double glueTriggerOffset = processParams.DotGlueTriggerOffsetMm;

        // 初始Z轴抬升至安全高度
        await _motionService.MoveAbsAsync(AxisDz1, safeHeight, moveSpeed, token);

        foreach (var (seg, index) in segmentList.Select((s, i) => (s, i)))
        {
            token.ThrowIfCancellationRequested();
            if (seg.Points == null || seg.Points.Count == 0) continue;

            PublishProgress($"单点走胶 - 段 [{seg.SegmentId}] ({index + 1}/{total})", index + 1, total);
            _logger?.Debug($"[DispenseExecute] 单点走胶段 [{seg.SegmentId}]，共 {seg.Points.Count} 点");

            double targetZ = processParams.EffectiveZHeight;

            foreach (var (point, ptIndex) in seg.Points.Select((p, i) => (p, i)))
            {
                token.ThrowIfCancellationRequested();

                double px = point.MachineX ?? point.OffsetX ?? point.X;
                double py = point.MachineY ?? point.OffsetY ?? point.Y;

                // 1. Z轴抬升至安全高度
                await _motionService.MoveAbsAsync(AxisDz1, safeHeight, moveSpeed, token);

                // 2. XY定位到点上方
                await _motionService.MoveLineAbsAsync(CoordId, new[] { AxisDx, AxisDy },
                    new[] { px, py }, moveSpeed, token);

                // 3. Z两段式下降：快速接近高度
                double approachZ = targetZ + approachOffset;
                await _motionService.MoveAbsAsync(AxisDz1, approachZ, moveSpeed, token);

                // 4. 慢速下降到示教高度+偏移量，同步检测开胶距离
                var moveZTask = _motionService.MoveAbsAsync(AxisDz1, targetZ, slowVel, token);

                bool glueOpened = false;
                while (!moveZTask.IsCompleted && !token.IsCancellationRequested)
                {
                    double currentZ = _motionService.GetAxisPosition(AxisDz1);
                    if (Math.Abs(currentZ - targetZ) <= glueTriggerOffset)
                    {
                        WriteGlueIo(true);
                        _logger?.Debug($"[DispenseExecute] 段[{seg.SegmentId}]点{ptIndex + 1} 位置触发开胶");
                        glueOpened = true;
                        break;
                    }
                    await Task.Delay(1, token);
                }

                if (!glueOpened)
                {
                    WriteGlueIo(true);
                    _logger?.Warn($"[DispenseExecute] 段[{seg.SegmentId}]点{ptIndex + 1} 兜底开胶");
                }

                await moveZTask;

                // 5. 起点延时（开胶稳定延时）
                if (processParams.PreDispenseDelay > 0)
                    await Task.Delay((int)processParams.PreDispenseDelay, token);

                // 6. 出胶时间
                await Task.Delay((int)processParams.DispenseTime, token);

                // 7. 关胶
                WriteGlueIo(false);

                // 8. 收胶延时
                if (processParams.PostDelay > 0)
                    await Task.Delay((int)processParams.PostDelay, token);

                // 9. Z抬升至安全高度
                await _motionService.MoveAbsAsync(AxisDz1, safeHeight, moveSpeed, token);
            }
        }

        // 循环结束后Z轴抬升至待机位
        await _motionService.MoveAbsAsync(AxisDz1, standbyHeight, moveSpeed, token);

        PublishStatus("Completed");
        _logger?.Info("[DispenseExecute] 单点线条走胶完成");
    }
    catch (OperationCanceledException)
    {
        SafeGlueOff();
        PublishStatus("Canceled");
        _logger?.Warn("[DispenseExecute] 单点线条走胶已取消");
        throw;
    }
    catch (Exception ex)
    {
        SafeGlueOff();
        PublishStatus("Error");
        _logger?.Error(ex, "[DispenseExecute] 单点线条走胶异常");
        throw;
    }
    finally
    {
        SetRunning(false);
    }
}
```

- [ ] **Step 2: 增强连续插补执行 — 添加Z轴安全防护**

修改 `ExecuteSegmentsAsync` 方法中连续插补走轨迹部分（步骤4前后），添加Z轴安全防护：

在步骤3（Z下降到工作高度）之后、步骤4（连续插补走轨迹）之前，添加Z轴到位确认：

```csharp
// 3d. Z轴安全防护：确认Z轴已到达工作高度再开始插补运动
double currentZPos = _motionService.GetAxisPosition(AxisDz1);
if (descendToWorkHeight && Math.Abs(currentZPos - targetZ) > 0.5)
{
    _logger?.Warn($"[DispenseExecute] 段 [{seg.SegmentId}] Z轴未到位: 当前={currentZPos:F3}, 目标={targetZ:F3}，重新下降");
    await _motionService.MoveAbsAsync(AxisDz1, targetZ, slowVel, token);
}
```

在步骤4（连续插补走轨迹）中，增强开胶距离实时检测逻辑（现有代码已包含位置触发开胶，确认逻辑正确即可）。

---

### Task 7: 更新 Step6ExecutePanel — 互斥执行模式选择UI

**Files:**
- Modify: `Module/Controls/Cad/Step6ExecutePanel.xaml`

- [ ] **Step 1: 替换执行选项区域**

将现有的执行选项 `materialDesign:Card`（包含Z校正和单点模式CheckBox）替换为：

```xml
<!-- 执行模式选择（互斥） -->
<materialDesign:Card Padding="10" Margin="0,0,0,8">
    <StackPanel>
        <TextBlock Text="{lang:Lang Step6_Section_ExecMode}" FontWeight="Bold" FontSize="12" Margin="0,0,0,6"/>
        <RadioButton Content="{lang:Lang Step6_Radio_ContinuousInterpolation}"
                     IsChecked="{Binding IsContinuousInterpolationMode}"
                     GroupName="Step6ExecMode" Margin="0,2"/>
        <TextBlock Text="{lang:Lang Step6_Desc_ContinuousInterpolation}"
                   FontSize="10" Foreground="#9E9E9E" TextWrapping="Wrap" Margin="24,2,0,4"/>
        <RadioButton Content="{lang:Lang Step6_Radio_SinglePoint}"
                     IsChecked="{Binding IsSinglePointMode}"
                     GroupName="Step6ExecMode" Margin="0,4,0,0"/>
        <TextBlock Text="{lang:Lang Step6_Desc_SinglePoint}"
                   FontSize="10" Foreground="#9E9E9E" TextWrapping="Wrap" Margin="24,2,0,4"/>
        <CheckBox Content="{lang:Lang Step6_CheckBox_ZCorrection}" IsChecked="{Binding ZCorrectionEnabled}" Margin="0,8,0,0"/>
    </StackPanel>
</materialDesign:Card>
```

- [ ] **Step 2: 更新执行按钮文本**

将执行主按钮的 Content 改为根据模式动态显示：

```xml
<!-- 执行主按钮 -->
<Button Command="{Binding ExecutePathCommand}"
        Style="{StaticResource MaterialDesignRaisedButton}"
        HorizontalAlignment="Stretch" Padding="0,6"
        FontSize="15" FontWeight="Bold"
        Background="#D32F2F" Foreground="White"
        BorderBrush="#B71C1C"
        IsEnabled="{Binding CanExecute, FallbackValue=False}"
        Margin="0,0,0,6">
    <StackPanel Orientation="Horizontal">
        <materialDesign:PackIcon Kind="Play" Margin="0,0,6,0" VerticalAlignment="Center"/>
        <TextBlock>
            <TextBlock.Style>
                <Style TargetType="TextBlock">
                    <Setter Property="Text" Value="{lang:Lang Step6_Btn_ExecutePath}"/>
                    <Style.Triggers>
                        <DataTrigger Binding="{Binding IsSinglePointMode}" Value="True">
                            <Setter Property="Text" Value="{lang:Lang Step6_Btn_ExecuteSinglePoint}"/>
                        </DataTrigger>
                    </Style.Triggers>
                </Style>
            </TextBlock.Style>
        </TextBlock>
    </StackPanel>
</Button>
```

注意：由于 `{lang:Lang}` 标记扩展不能在 Style Setter 中直接使用，需要改用绑定到 ViewModel 属性的方式。在 ViewModel 中添加：

```csharp
/// <summary>执行按钮文本（根据模式动态切换）</summary>
public string ExecuteButtonText => IsSinglePointMode
    ? L("Step6_Btn_ExecuteSinglePoint")
    : L("Step6_Btn_ExecutePath");
```

并在 `IsSinglePointMode` 变更时 `RaisePropertyChanged(nameof(ExecuteButtonText))`。

XAML 简化为：
```xml
<Button Content="{Binding ExecuteButtonText}"
        Command="{Binding ExecutePathCommand}"
        Style="{StaticResource MaterialDesignRaisedButton}"
        HorizontalAlignment="Stretch" Padding="0,6"
        FontSize="15" FontWeight="Bold"
        Background="#D32F2F" Foreground="White"
        BorderBrush="#B71C1C"
        IsEnabled="{Binding CanExecute, FallbackValue=False}"
        Margin="0,0,0,6"/>
```

---

### Task 8: 更新 CadPointEditorViewModel — 完善执行分发逻辑

**Files:**
- Modify: `Module/Controls/Cad/CadPointEditorViewModel.cs`

- [ ] **Step 1: 更新 ExecuteRun 方法 — 支持双模式执行**

在 `#region Step5: 预览仿真命令实现` 中修改 `ExecuteRun` 方法，增加单点模式真实执行路径：

在现有的 `if (IsRealDryRunMode)` 分支之后，添加单点模式真实执行分支：

```csharp
if (IsRealDryRunMode)
{
    GlobalStatus = DescendInDryRun
        ? L("CadPoint_Status_DryRunStart_Descend")
        : L("CadPoint_Status_DryRunStart_Safe");
    await _dispenseExecuteService.DryRunAsync(enabledSegments, DescendInDryRun, _simCts.Token);
    GlobalStatus = L("CadPoint_Status_DryRunCompleted");
}
else if (IsRealDispenseMode)
{
    if (LineDispenseMode == LineDispenseMode.SinglePoint)
    {
        GlobalStatus = L("LineBC_Status_SinglePointExecuting");
        await _dispenseExecuteService.ExecuteSinglePointLineAsync(
            enabledSegments, SinglePointProcessParams, StandbyHeight, _simCts.Token);
        GlobalStatus = L("LineBC_Status_SinglePointCompleted");
    }
    else
    {
        GlobalStatus = L("LineBC_Status_ContinuousInterpolationExecuting");
        await _dispenseExecuteService.ExecutePathAsync(enabledSegments, "B/C", _simCts.Token);
        GlobalStatus = L("LineBC_Status_ContinuousInterpolationCompleted");
    }
}
```

- [ ] **Step 2: 添加 ShowContinuousInterpolationParams 复合属性**

```csharp
/// <summary>是否显示连续插补段参数编辑区（有选中段 且 为连续插补模式）</summary>
public bool ShowContinuousInterpolationParams => HasSelectedSegment && IsContinuousInterpolationMode;
```

在 `HasSelectedSegment` 和 `LineDispenseMode` 的 setter 中添加：
```csharp
RaisePropertyChanged(nameof(ShowContinuousInterpolationParams));
```

- [ ] **Step 3: 添加 ExecuteButtonText 属性**

```csharp
/// <summary>执行按钮文本（根据模式动态切换）</summary>
public string ExecuteButtonText => IsSinglePointMode
    ? L("Step6_Btn_ExecuteSinglePoint")
    : L("Step6_Btn_ExecutePath");
```

在 `LineDispenseMode` setter 中添加：
```csharp
RaisePropertyChanged(nameof(ExecuteButtonText));
```

---

### Task 9: 添加多语言资源

**Files:**
- Modify: `MainApp/Languages/Strings.zh-CN.xaml`
- Modify: `MainApp/Languages/Strings.en-US.xaml`

- [ ] **Step 1: 在 Strings.zh-CN.xaml 中添加中文语言键**

在文件末尾 `</ResourceDictionary>` 之前添加：

```xml
<!-- 2D/3D线条(B/C) 模式管理 -->
<sys:String x:Key="LineBC_Mode_SinglePoint">单点模式</sys:String>
<sys:String x:Key="LineBC_Mode_ContinuousInterpolation">连续插补模式</sys:String>
<sys:String x:Key="LineBC_Status_SwitchToSinglePoint">已切换到单点模式</sys:String>
<sys:String x:Key="LineBC_Status_SwitchToContinuousInterpolation">已切换到连续插补模式</sys:String>
<sys:String x:Key="LineBC_Status_PrepareExec">准备执行: {0} 段, {1}</sys:String>
<sys:String x:Key="LineBC_Status_SinglePointExecuting">单点模式走胶执行中...</sys:String>
<sys:String x:Key="LineBC_Status_SinglePointCompleted">单点模式走胶完成</sys:String>
<sys:String x:Key="LineBC_Status_ContinuousInterpolationExecuting">连续插补模式走胶执行中...</sys:String>
<sys:String x:Key="LineBC_Status_ContinuousInterpolationCompleted">连续插补模式走胶完成</sys:String>

<!-- Step3 参数模式切换 -->
<sys:String x:Key="Step3_Section_DispenseMode">参数模式</sys:String>
<sys:String x:Key="Step3_Radio_ContinuousInterpolation">连续插补模式</sys:String>
<sys:String x:Key="Step3_Radio_SinglePoint">单点模式</sys:String>
<sys:String x:Key="Step3_Desc_ModeSwitch">切换模式将改变参数显示和执行方式。单点模式复用点涂(A)工艺参数体系。</sys:String>
<sys:String x:Key="Step3_Section_SinglePointParams">单点模式工艺参数</sys:String>
<sys:String x:Key="Step3_Label_DispenseSpeed">出胶速度 (mm/s)</sys:String>
<sys:String x:Key="Step3_Label_DispenseTime">出胶时间 (ms)</sys:String>
<sys:String x:Key="Step3_Label_PreDispenseDelay">起点延时 (ms)</sys:String>
<sys:String x:Key="Step3_Label_PostDelay">收胶延时 (ms)</sys:String>
<sys:String x:Key="Step3_Label_DispensingPressure">点胶气压 (MPa)</sys:String>
<sys:String x:Key="Step3_Label_SuckBackTime">回吸时间 (ms)</sys:String>
<sys:String x:Key="Step3_Label_SafeHeight">安全高度 (mm)</sys:String>
<sys:String x:Key="Step3_Label_ApproachHeight">逼近高度 (mm)</sys:String>
<sys:String x:Key="Step3_Label_TeachHeight">示教高度 (mm)</sys:String>
<sys:String x:Key="Step3_Label_HeightCompensation">高度补偿 (mm)</sys:String>
<sys:String x:Key="Step3_Label_EffectiveHeight">有效高度 (mm)</sys:String>
<sys:String x:Key="Step3_Label_CornerDecel">拐角减速系数</sys:String>
<sys:String x:Key="Step3_Label_GlueTriggerOffset">开胶触发距离 (mm)</sys:String>

<!-- Step6 执行模式 -->
<sys:String x:Key="Step6_Section_ExecMode">执行模式</sys:String>
<sys:String x:Key="Step6_Radio_ContinuousInterpolation">连续插补执行</sys:String>
<sys:String x:Key="Step6_Desc_ContinuousInterpolation">使用连续插补功能沿轨迹走胶，适用于连续线条点胶</sys:String>
<sys:String x:Key="Step6_Radio_SinglePoint">单点执行</sys:String>
<sys:String x:Key="Step6_Desc_SinglePoint">逐点下降出胶后抬升，适用于离散点位点胶</sys:String>
<sys:String x:Key="Step6_Btn_ExecuteSinglePoint">执行单点走胶</sys:String>
```

- [ ] **Step 2: 在 Strings.en-US.xaml 中添加英文语言键**

```xml
<!-- 2D/3D Line (B/C) Mode Management -->
<sys:String x:Key="LineBC_Mode_SinglePoint">Single Point Mode</sys:String>
<sys:String x:Key="LineBC_Mode_ContinuousInterpolation">Continuous Interpolation Mode</sys:String>
<sys:String x:Key="LineBC_Status_SwitchToSinglePoint">Switched to Single Point Mode</sys:String>
<sys:String x:Key="LineBC_Status_SwitchToContinuousInterpolation">Switched to Continuous Interpolation Mode</sys:String>
<sys:String x:Key="LineBC_Status_PrepareExec">Preparing: {0} segments, {1}</sys:String>
<sys:String x:Key="LineBC_Status_SinglePointExecuting">Single point dispensing in progress...</sys:String>
<sys:String x:Key="LineBC_Status_SinglePointCompleted">Single point dispensing completed</sys:String>
<sys:String x:Key="LineBC_Status_ContinuousInterpolationExecuting">Continuous interpolation dispensing in progress...</sys:String>
<sys:String x:Key="LineBC_Status_ContinuousInterpolationCompleted">Continuous interpolation dispensing completed</sys:String>

<!-- Step3 Parameter Mode Switch -->
<sys:String x:Key="Step3_Section_DispenseMode">Parameter Mode</sys:String>
<sys:String x:Key="Step3_Radio_ContinuousInterpolation">Continuous Interpolation</sys:String>
<sys:String x:Key="Step3_Radio_SinglePoint">Single Point</sys:String>
<sys:String x:Key="Step3_Desc_ModeSwitch">Switching mode changes parameter display and execution. Single point mode reuses Dot(A) process parameters.</sys:String>
<sys:String x:Key="Step3_Section_SinglePointParams">Single Point Process Parameters</sys:String>
<sys:String x:Key="Step3_Label_DispenseSpeed">Dispense Speed (mm/s)</sys:String>
<sys:String x:Key="Step3_Label_DispenseTime">Dispense Time (ms)</sys:String>
<sys:String x:Key="Step3_Label_PreDispenseDelay">Pre-Dispense Delay (ms)</sys:String>
<sys:String x:Key="Step3_Label_PostDelay">Post Delay (ms)</sys:String>
<sys:String x:Key="Step3_Label_DispensingPressure">Pressure (MPa)</sys:String>
<sys:String x:Key="Step3_Label_SuckBackTime">Suck-back Time (ms)</sys:String>
<sys:String x:Key="Step3_Label_SafeHeight">Safe Height (mm)</sys:String>
<sys:String x:Key="Step3_Label_ApproachHeight">Approach Height (mm)</sys:String>
<sys:String x:Key="Step3_Label_TeachHeight">Teach Height (mm)</sys:String>
<sys:String x:Key="Step3_Label_HeightCompensation">Height Compensation (mm)</sys:String>
<sys:String x:Key="Step3_Label_EffectiveHeight">Effective Height (mm)</sys:String>
<sys:String x:Key="Step3_Label_CornerDecel">Corner Decel Factor</sys:String>
<sys:String x:Key="Step3_Label_GlueTriggerOffset">Glue Trigger Offset (mm)</sys:String>

<!-- Step6 Execution Mode -->
<sys:String x:Key="Step6_Section_ExecMode">Execution Mode</sys:String>
<sys:String x:Key="Step6_Radio_ContinuousInterpolation">Continuous Interpolation</sys:String>
<sys:String x:Key="Step6_Desc_ContinuousInterpolation">Use continuous interpolation along trajectory, suitable for continuous line dispensing</sys:String>
<sys:String x:Key="Step6_Radio_SinglePoint">Single Point</sys:String>
<sys:String x:Key="Step6_Desc_SinglePoint">Dispense at each point with Z-axis descent and ascent, suitable for discrete point dispensing</sys:String>
<sys:String x:Key="Step6_Btn_ExecuteSinglePoint">Execute Single Point</sys:String>
```

---

### Task 10: 构建验证与修复

**Files:**
- All modified files

- [ ] **Step 1: 执行构建**

Run: `dotnet build GZQL_MACHINE.sln --no-restore`
Expected: 编译成功，无错误

- [ ] **Step 2: 修复编译错误（如有）**

检查并修复所有编译错误，确保：
- 所有新增的语言键在两个语言文件中都已定义
- 所有新增的绑定属性在 ViewModel 中都有对应的 RaisePropertyChanged
- DispenseSegment 新增字段不影响 JSON 序列化/反序列化兼容性

- [ ] **Step 3: 验证功能完整性**

检查清单：
- [ ] Step3 模式切换RadioButton正常工作
- [ ] 单点模式参数面板正确显示/隐藏
- [ ] 连续插补模式下 MoveSpeed 标签显示为"出胶速度"
- [ ] Step6 执行模式互斥选择正常
- [ ] 执行按钮文本根据模式动态切换
- [ ] 多语言切换后所有新增文本正确显示

---

## 自检清单

### 1. 需求覆盖

| 需求项 | 对应Task |
|--------|----------|
| 两种操作模式切换 | Task 3, 4 |
| 单点模式显示完整参数界面 | Task 4 (Step3单点参数面板) |
| 单点模式复用点涂(A)参数体系 | Task 3 (DotProcessParams), Task 6 |
| 连续插补模式显示当前参数 | Task 4 (现有面板+标签修改) |
| "运动速度"改为"出胶速度" | Task 4 (Step3_Label_DispenseSpeed) |
| Step6互斥选择机制 | Task 7 (RadioButton GroupName) |
| 连续插补执行模式 | Task 6 (增强现有ExecuteSegmentsAsync) |
| Z轴安全防护 | Task 6 Step2 |
| 实时检测开胶距离 | Task 6 (已有逻辑确认) |
| 连续插补应用工艺参数 | Task 6 (PreDelay/PostDelay/DispenseHeight) |
| 单点执行模式动作流程 | Task 6 Step1 |
| 单点应用工艺参数 | Task 6 Step1 (PreDispenseDelay/PostDelay/DispenseTime) |
| 多语言支持 | Task 9 |

### 2. 占位符扫描
无 TBD/TODO/占位符。

### 3. 类型一致性
- `LineDispenseMode` 枚举在 Core/Models 中定义，在 ViewModel 和 Service 中使用一致
- `DotProcessParams` 在 Core/Models 中定义，在 ViewModel 和 Service 接口中使用一致
- `DispenseSegment` 新增字段与 `DotProcessParams` 对应字段类型和范围一致
