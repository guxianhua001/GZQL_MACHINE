# 修复 GOTO 位置引用安全防护（#2 运行时防护 + #3 编辑器校验）

## Context

**问题背景**：GOTO 步骤通过 `StationId + PositionName`（字符串）间接引用配方位置。位置编辑器修改/重命名/删除位置后，GOTO 引用不会自动更新，产生悬空引用。当前实现存在两个安全缺陷：

1. **运行时缺陷**：`StationTaskBase.GetPositionAsync` 找不到位置时静默返回 0（[StationTaskBase.cs:246](file:///c:/WorkFiles/GZQL_MACHINE/MotionControl/Services/StationTaskBase.cs#L246)），导致 `GotoStepAction` / `ExecuteMoveAsync` 驱动轴移动到机械 0 位 + offset，存在撞机风险。
2. **编辑器缺陷**：GOTO 详情视图打开时，若 `SubMove.PositionName` 在当前位置列表中已失效，UI 无任何警告（[GotoDetailView.xaml:91-104](file:///c:/WorkFiles/GZQL_MACHINE/Module/Controls/StepDetails/GotoDetailView.xaml#L91-L104)），用户无感知。

**预期结果**：
- 运行时找不到位置 → 抛 `PositionNotFoundException` → RunStep 致命分支中止流程 + Serious 报警
- 编辑器打开时位置已失效 → Position 列显示红色 AlertCircle 图标 + ToolTip 提示

## 实现步骤

### 1. 新建 `PositionNotFoundException.cs`

**路径**：`c:\WorkFiles\GZQL_MACHINE\MotionControl\Exceptions\PositionNotFoundException.cs`

- 命名空间 `MotionControl.Exceptions`，继承 `Exception`（非 `RecoverableException` — 位置缺失是配置错误，重试无意义且后续步骤可能同样失配，致命中止更安全）
- 属性：`PositionName`、`AxisName`、`StationId`
- 多语言 message：通过 `ContainerLocator.Container.Resolve<ILocalizationService>()` 获取 `Exception_PositionNotFound` 模板，`string.Format` 填充三个参数；容错回退中文硬编码（遵循 `LogMessages.g.cs:30-33` 与 `BoolToLedColorConverter.cs:81` 先例）

### 2. 修改 `StationTaskBase.cs`

**路径**：`c:\WorkFiles\GZQL_MACHINE\MotionControl\Services\StationTaskBase.cs`

- 改造 `GetPositionAsync`（行 242-247）：找不到时 `throw new PositionNotFoundException(positionName, axisName, _stationId)`
- 新增 `TryGetPositionAsync(string, string) -> Task<(bool found, double value)>`：返回元组，不抛异常，供"可选轴"场景使用
- `ExecuteMoveAsync`（行 743）、`MoveToAsync`（行 649）、`GetPositionValueAsync`（行 254）无需改动，自动获得抛异常行为
- RunStep 的 `catch(Exception)`（行 504-526）会走致命路径：`StepErrorEvent`(STEP_FATAL_ERROR) + `AlarmLevel.Serious` + `StepFailureException` 中止流程

### 3. 修改 `DispensingTask.cs`

**路径**：`c:\WorkFiles\GZQL_MACHINE\StationTasks\Tasks\DispensingTask.cs`（行 105-113）

- `MoveToPosition` 方法第 111 行 `var z2 = await GetPositionAsync(posName, "Dz₂"); if (!double.IsNaN(z2)) ...` 改为：
  ```csharp
  var (hasZ2, z2) = await TryGetPositionAsync(posName, "Dz₂");
  if (hasZ2) await _motion.MoveAbsAsync(AxisDz2, z2, 20);
  ```
- 同时修复原 `IsNaN` 永真 latent bug（GetPositionAsync 从不返回 NaN，原检查永远为 true）

### 4. 修改 `SubMoveRowViewModel.cs`

**路径**：`c:\WorkFiles\GZQL_MACHINE\Module\Controls\StepEditor\SubMoveRowViewModel.cs`

- **行 45 `PositionName` setter**：补充 `RaisePropertyChanged`（当前无通知），变更时同时通知 `IsPositionInvalid`
- **新增 `IsPositionInvalid` 计算属性**（放在 `AvailablePositions` 后）：
  ```csharp
  public bool IsPositionInvalid =>
      !string.IsNullOrEmpty(PositionName) &&
      AvailablePositions != null &&
      AvailablePositions.Count > 0 &&
      !AvailablePositions.Contains(PositionName);
  ```
- **`AvailablePositions` setter（行 183）**：`SetProperty` 后追加 `RaisePropertyChanged(nameof(IsPositionInvalid))`
- `StationId` setter（行 29-42）切换工站后 `LoadAxesAndPositionsAsync` 刷新 `AvailablePositions`，自动联动 `IsPositionInvalid`

### 5. 修改 `GotoDetailView.xaml`

**路径**：`c:\WorkFiles\GZQL_MACHINE\Module\Controls\StepDetails\GotoDetailView.xaml`（行 91-104）

仅改 Position 列 `CellTemplate`（回零模式 DataGrid 无 Position 列，无需改）：

```xaml
<DataGridTemplateColumn.CellTemplate>
    <DataTemplate>
        <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
            <materialDesign:PackIcon Kind="AlertCircle"
                                     Width="14" Height="14"
                                     VerticalAlignment="Center"
                                     Foreground="#F44336"
                                     Margin="0,0,2,0"
                                     ToolTip="{lang:Lang GotoDetail_PositionInvalidTip}"
                                     Visibility="{Binding IsPositionInvalid, Converter={StaticResource BoolToVisibilityConverter}}" />
            <TextBlock Text="{Binding PositionName}" VerticalAlignment="Center" />
        </StackPanel>
    </DataTemplate>
</DataGridTemplateColumn.CellTemplate>
```

`CellEditingTemplate` 保持不变。`BoolToVisibilityConverter` 已在行 14 声明。风格遵循 [ProcessSequenceEditorView.xaml:412-429](file:///c:/WorkFiles/GZQL_MACHINE/Module/Controls/StepEditor/ProcessSequenceEditorView.xaml#L412-L429) 范例。

### 6. 多语言资源

**路径**：`c:\WorkFiles\GZQL_MACHINE\MainApp\Languages\Strings.zh-CN.xaml` 和 `Strings.en-US.xaml`

在 `GotoDetail_` 区域（line 3098 后）追加：

| Key | zh-CN | en-US |
|---|---|---|
| `GotoDetail_PositionInvalidTip` | 位置名已失效（被重命名或删除），请重新选择 | Position name is invalid (renamed or deleted), please reselect |
| `Exception_PositionNotFound` | 位置 [{0}] 的轴 [{1}] 在工站 [{2}] 中未找到，请检查配方配置 | Position [{0}] axis [{1}] not found in station [{2}], please check recipe config |

## 关键文件清单

- 新建：`MotionControl\Exceptions\PositionNotFoundException.cs`
- 修改：`MotionControl\Services\StationTaskBase.cs`（GetPositionAsync + TryGetPositionAsync）
- 修改：`StationTasks\Tasks\DispensingTask.cs`（可选轴语义）
- 修改：`Module\Controls\StepEditor\SubMoveRowViewModel.cs`（IsPositionInvalid + setter 通知）
- 修改：`Module\Controls\StepDetails\GotoDetailView.xaml`（Position 列警告图标）
- 修改：`MainApp\Languages\Strings.zh-CN.xaml`、`Strings.en-US.xaml`（2 个新 key）

## 验证步骤

1. 启动应用 → 打开配方编辑器 → 进入 GOTO 步骤详情（绝对定位模式）
2. 选择工站 A，某行 Position 选有效位置名 `P1` → 保存配方
3. 在位置管理器中重命名 `P1` 为 `P1_New`（或删除）
4. 重新打开该 GOTO 详情 → Position 列应显示红色 `AlertCircle` 图标，悬停显示"位置名已失效"提示
5. 运行该 GOTO 步骤 → 应触发 `STEP_FATAL_ERROR` + Serious 报警 + 流程中止，日志含 `PositionNotFoundException` 及位置/轴/工站信息
6. 测试 DispensingTask：配置含 `Dz₂` 的位置 → Z 轴正常移动；移除 `Dz₂` → 跳过 Z 轴移动不报错
7. 切换中/英文 → 警告 ToolTip 与异常 message 随语言切换
8. 编译验证：`dotnet build` 无错误无警告
