# 步骤编辑器添加运行耗时列 - 实施计划

## 需求概述

在步骤编辑器（ProcessSequenceEditorView）的 DataGrid 最后一列添加"运行耗时"列，显示每个步骤最近一次执行的耗时（毫秒）。该数据在运行时实时记录，不参与序列化。

## 当前架构分析

### 数据流
```
ProcessStepExecutor.ExecuteAsync
  → step.IsCurrent = true
  → ExecuteSingleStepAsync (内部调用 _task.ExecuteStepSafeAsync → RunStep)
  → step.IsCurrent = false
```

### 计时现状
- `StationTaskBase.RunStep` 中已有 `Stopwatch` 计时，但结果仅写日志，未回写模型
- `ProcessStep` 模型中无耗时相关属性
- `ProcessSequenceEditorView.xaml` DataGrid 有7列：Seq/Step/CompFeature/SiteFeature/Camera/Purpose/Alarm

## 实施步骤

### 步骤1：ProcessStep 模型添加 LastElapsedMs 属性

**文件**: `StationTasks\Models\ProcessStep.cs`

在 `HasError` 属性之后添加：

```csharp
[JsonIgnore]
private long _lastElapsedMs;
/// <summary> 步骤最近一次执行的耗时（毫秒），运行时记录，不序列化 </summary>
public long LastElapsedMs
{
    get => _lastElapsedMs;
    set
    {
        if (_lastElapsedMs != value)
        {
            _lastElapsedMs = value;
            OnPropertyChanged();
        }
    }
}
```

- 标记 `[JsonIgnore]`，不参与配方序列化
- 使用 `long` 类型存储毫秒值
- 属性变更通知确保 UI 绑定自动刷新

### 步骤2：ProcessStepExecutor 中添加计时逻辑

**文件**: `StationTasks\Actions\ProcessStepExecutor.cs`

在 `ExecuteAsync` 方法的步骤执行循环中（第147-153行），用 `Stopwatch` 包裹步骤执行并回写 `LastElapsedMs`：

```csharp
// 标记当前步骤
step.IsCurrent = true;
_logger.Info($"=== 执行步骤 [{step.Seq}] {step.Step} ... ===");

try
{
    var sw = Stopwatch.StartNew();                           // ← 新增：开始计时
    int nextIndex = await ExecuteSingleStepAsync(step, steps, currentIndex, token);
    sw.Stop();                                                // ← 新增：停止计时
    step.LastElapsedMs = sw.ElapsedMilliseconds;             // ← 新增：回写耗时
    step.IsCurrent = false;
    currentIndex = nextIndex;
}
```

同时在 `catch` 块中也记录耗时（异常路径的耗时同样有价值）：

```csharp
catch (OperationCanceledException)
{
    step.IsCurrent = false;
    // 异常路径不记录耗时（步骤未正常完成）
    ...
}
catch (Exception ex)
{
    step.IsCurrent = false;
    // 异常路径不记录耗时（步骤未正常完成）
    ...
}
```

同样，在单独执行步骤的方法 `ExecuteSingleStepAsync(ProcessStep step, CancellationToken token)` 中也添加计时：

```csharp
var sw = Stopwatch.StartNew();
await _task.ExecuteStepSafeAsync(stepLabel, async () =>
{
    await action.ExecuteAsync(step, _task, token);
}, publishStatus, step.AlarmConfig);
sw.Stop();
step.LastElapsedMs = sw.ElapsedMilliseconds;
```

需要在文件顶部添加 `using System.Diagnostics;`。

### 步骤3：ProcessSequenceEditorView.xaml 添加耗时列

**文件**: `Module\Controls\StepEditor\ProcessSequenceEditorView.xaml`

在 Alarm 列之后、`</DataGrid.Columns>` 之前添加耗时列：

```xml
<DataGridTemplateColumn Header="{DynamicResource PSE_ElapsedTime}" Width="80" IsReadOnly="True">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding LastElapsedMs, StringFormat={}{0} ms}"
                       VerticalAlignment="Center"
                       HorizontalAlignment="Right"
                       FontSize="11"
                       Foreground="#666666">
                <TextBlock.Style>
                    <Style TargetType="TextBlock">
                        <Setter Property="Visibility" Value="Visible"/>
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding LastElapsedMs}" Value="0">
                                <Setter Property="Visibility" Value="Collapsed"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </TextBlock.Style>
            </TextBlock>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

设计要点：
- 列宽 80px，右对齐显示数值
- 格式为 "1234 ms"
- `LastElapsedMs` 为 0 时隐藏（未执行过的步骤不显示）
- `IsReadOnly="True"`，耗时不可编辑
- 使用灰色字体 `#666666` 区别于可编辑列
- 使用多语言键 `PSE_ElapsedTime` 作为列头

### 步骤4：添加多语言资源

**文件**: `MainApp\Languages\Strings.zh-CN.xaml`

```xml
<sys:String x:Key="PSE_ElapsedTime">耗时</sys:String>
```

**文件**: `MainApp\Languages\Strings.en-US.xaml`

```xml
<sys:String x:Key="PSE_ElapsedTime">Time</sys:String>
```

### 步骤5：编译验证

编译整个解决方案，确保无错误。

## 修改文件清单

| 文件 | 修改内容 |
|------|----------|
| `StationTasks\Models\ProcessStep.cs` | 添加 `LastElapsedMs` 属性（JsonIgnore） |
| `StationTasks\Actions\ProcessStepExecutor.cs` | 在 ExecuteAsync 和 ExecuteSingleStepAsync 中添加 Stopwatch 计时，回写 LastElapsedMs |
| `Module\Controls\StepEditor\ProcessSequenceEditorView.xaml` | DataGrid 添加耗时列 |
| `MainApp\Languages\Strings.zh-CN.xaml` | 添加 PSE_ElapsedTime 键 |
| `MainApp\Languages\Strings.en-US.xaml` | 添加 PSE_ElapsedTime 键 |

## 设计考量

1. **性能**: Stopwatch 是轻量级高精度计时器，对运动控制的快速响应性无影响
2. **序列化**: `LastElapsedMs` 标记 `[JsonIgnore]`，不影响配方存储
3. **UI 一致性**: 新列样式与现有列保持一致（DataGridTemplateColumn、Material Design 风格）
4. **数据准确性**: 计时点在 ProcessStepExecutor 层面，覆盖所有步骤类型（GOTO/PICK/CURE/RELEASE/VISION 等）
5. **异常处理**: 异常路径不记录耗时（步骤未正常完成时耗时无意义）
6. **显示策略**: 未执行的步骤（LastElapsedMs=0）不显示耗时，避免视觉干扰
