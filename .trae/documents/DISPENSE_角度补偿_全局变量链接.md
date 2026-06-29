# DISPENSE 工具 — Enable Rotation Comp 组新增「角度补偿」

## Context（背景）

DISPENSE 工具的「Enable Rotation Comp」旋转补偿组目前只有一个 `RotationAngle`（旋转角度）参数，已支持链接全局变量。但在工业现场，产品基础旋转角度（来自 CAD/配方）与运行时调整的「角度补偿」往往需要分离：操作员希望在不修改配方基础角度的前提下，通过全局变量动态微调一个补偿角度，使最终生效角度 = 旋转角度 + 角度补偿。

本次变更在旋转补偿组中新增「角度补偿（Angle Compensation）」字段，复用现有 `RotationAngle` 的全局变量链接模式，实现：

* 可链接全局变量（与 RotationAngle 一致的双值模式：字面量 + LinkedVar）

* 实时更新链接数值（复用现有 `GlobalVariablesChangedEvent` → `RefreshCalibrationDisplayValues` 链路）

* 计算时使用 `RotationAngle + AngleCompensation`（预览 + 执行两条路径）

* 可持久化（随 `DispenseDetail` 模型序列化）

## 设计要点

* **遵循现有模式**：完全复用 `RotationAngle` / `RotationAngleLinkedVar` / `RotationAngleDisplayValue` / `IsRotationAngleLinked` / `UnlinkRotationAngleCommand` 的五元组成果，新增对应 `AngleCompensation*` 系列。不引入新依赖、新服务、新事件。

* **架构无倒置依赖**：Model（`DispenseDetail`）只持有数据；ViewModel 负责链接解析与实时刷新；执行路径（`DispenseStepAction`）通过 `ResolveLinkedValue` 在步骤解析时读取最新值。计算逻辑集中在 `CadAlignTransformSnapshot.Transform`，不修改。

* **安全性**：角度补偿仅在 `EnableRotationComp == true` 且 CAD 对齐快照有效时参与计算；补偿失败/未链接时回退为 0，不影响原有运动逻辑。

* **多语言**：所有用户可见文本（标签、提示、日志）走 `ILocalizationService.GetResourceOrDefault` + `Strings.zh-CN.xaml` / `Strings.en-US.xaml`。

## 实施步骤

### 1. Model — `Core\Models\DispenseDetail.cs`（持久化）

在 `RotationAngleLinkedVar`（约 line 186）之后、`#endregion` 之前新增两个属性，沿用 `SetProperty` + XML 注释模式：

* `double AngleCompensation`（默认 0.0，注释：角度补偿，与旋转角度相加后参与坐标变换）

* `string AngleCompensationLinkedVar`（注释：角度补偿链接的全局变量名）

随 `DispenseDetail` JSON 序列化自动持久化，无需额外存储改造。

### 2. ViewModel — `Module\Controls\StepDetails\DispenseDetailViewModel.cs`

**a) 属性区**（约 line 327 `RotationAngleDisplayValue` 之后）新增：

* `AngleCompensation`（委托 `_step.DispenseDetail.AngleCompensation`）

* `AngleCompensationLinkedVar`（setter 内触发 `IsAngleCompensationLinked` + `RefreshCalibrationDisplayValues()`，与 `RotationAngleLinkedVar` 一致）

* `bool IsAngleCompensationLinked`（`!string.IsNullOrEmpty(...)`）

* `double AngleCompensationDisplayValue`（private set）

* `UnlinkAngleCompensationCommand`（声明于 line 451 附近，实例化于构造函数 line 1389 附近：`new DelegateCommand(() => AngleCompensationLinkedVar = null)`）

**b)** **`RefreshCalibrationDisplayValues()`**（line 2226 旋转角度块之后）新增角度补偿解析块：

```csharp
// 角度补偿
if (!string.IsNullOrEmpty(_step?.DispenseDetail?.AngleCompensationLinkedVar))
{
    var gv = AvailableGlobalVariables.FirstOrDefault(v => v.Name == _step.DispenseDetail.AngleCompensationLinkedVar);
    AngleCompensationDisplayValue = gv != null && double.TryParse(gv.Value, out var val) ? val : 0.0;
}
else
{
    AngleCompensationDisplayValue = AngleCompensation;
}
RaisePropertyChanged(nameof(AngleCompensationDisplayValue));
```

实时刷新链路已存在：`OnGlobalVariablesChanged` → `ReplaceAvailableGlobalVariables` → `RefreshCalibrationDisplayValues`（line 2124），无需新增订阅。

**c)** **`OnViewRotatedCoordsAsync()`**（line 2365）将有效角度改为两者之和：

```csharp
double rotationAngle = (IsRotationAngleLinked ? RotationAngleDisplayValue : RotationAngle)
                     + (IsAngleCompensationLinked ? AngleCompensationDisplayValue : AngleCompensation);
```

并更新 line 2366 日志，追加显示 `角度补偿` 与 `有效角度`。

### 3. View — `Module\Controls\StepDetails\DispenseDetailView.xaml`

在旋转角度 `Grid`（line 238-265）之后、Coord Transform 状态提示 `TextBlock`（line 267）之前，插入一个新的 `Grid` 行，结构完全对齐旋转角度行：

* Column 0：`TextBlock Text="{lang:Lang DispenseDetail_AngleCompensation}"`

* Column 1：`common:GlobalVariableLinkControl`

  * `DisplayValue="{Binding AngleCompensationDisplayValue, Mode=OneWay}"`

  * `IsLinked="{Binding IsAngleCompensationLinked, Mode=OneWay}"`

  * `LinkedVariableName="{Binding AngleCompensationLinkedVar, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"`

  * `LinkableGlobalVariables="{Binding AvailableGlobalVariables}"`

  * `UnlinkCommand="{Binding UnlinkAngleCompensationCommand}"`

  * `ComboBoxWidth="180"`

* Column 2：留空（保持与上方对齐）或放一个 `PackIcon Kind="InformationOutline"` 带 ToolTip 说明「计算时与旋转角度相加」。

* 在状态提示 TextBlock 下方追加一行小字说明：`有效旋转角度 = 旋转角度 + 角度补偿`。

### 4. 执行路径 — `StationTasks\Actions\DispenseStepAction.cs`

**a) 字段**（line 73-74 附近）新增 `private double _angleCompensation;`

**b) 解析块**（line 198 之后，`_enableRotationComp` 块内）新增：

```csharp
_angleCompensation = ResolveLinkedValue(detail.AngleCompensation, detail.AngleCompensationLinkedVar);
```

并在 `Disp_Log_RotationCompEnabled` 日志（line 209-211）中追加 `角度补偿` 与 `有效角度={_rotationAngle + _angleCompensation:F3}°` 字段。

**c)** **`ResolveMachineXYBreakdown`**（line 999）改为使用有效角度：

```csharp
double effectiveAngle = _rotationAngle + _angleCompensation;
(tx, ty) = _cadAlignSnapshot.Transform(cadX, cadY, effectiveAngle);
```

安全逻辑（快照无效回退、无仿射/无 MachineXY 抛 `InvalidOperationException`）保持不变。

### 5. 多语言资源 — `MainApp\Languages\Strings.zh-CN.xaml` + `Strings.en-US.xaml`

新增键（中英对照，中文为兜底默认值）：

| Key                                    | zh-CN                                                      | en-US                                                                       |
| -------------------------------------- | ---------------------------------------------------------- | --------------------------------------------------------------------------- |
| `DispenseDetail_AngleCompensation`     | 角度补偿                                                       | Angle Compensation                                                          |
| `DispenseDetail_AngleCompensationHint` | 计算时与旋转角度相加（有效角度=旋转角度+角度补偿）                                 | Added to Rotation Angle (Effective = Rotation + Compensation)               |
| `Disp_Log_AngleCompensationApplied`    | DISPENSE 步骤 \[{0}] 角度补偿={1:F3}°, 有效旋转角度={2:F3}°            | DISPENSE step \[{0}] angle compensation={1:F3}°, effective rotation={2:F3}° |
| `DD_Log_EffectiveRotationAngle`        | \[DispenseDetail] 旋转角度={0:F3}°, 角度补偿={1:F3}°, 有效角度={2:F3}° | \[DispenseDetail] rotation={0:F3}°, compensation={1:F3}°, effective={2:F3}° |

按现有文件中键的字母序/分组位置插入，保持两语言文件键完全一致。

### 6. 版本修改记录 — `net9.0-windows7.0\版本修改记录.txt`

追加 v2026.06.30 条目，说明本次变更内容（新增角度补偿字段、链接全局变量、实时刷新、计算相加、持久化）。

## 关键文件清单

| 关注点                     | 文件                                                          |
| ----------------------- | ----------------------------------------------------------- |
| Model（持久化属性）            | `Core\Models\DispenseDetail.cs`                             |
| ViewModel（链接解析+实时刷新+预览） | `Module\Controls\StepDetails\DispenseDetailViewModel.cs`    |
| View（UI 行）              | `Module\Controls\StepDetails\DispenseDetailView.xaml`       |
| 执行路径（运动目标计算）            | `StationTasks\Actions\DispenseStepAction.cs`                |
| 多语言                     | `MainApp\Languages\Strings.zh-CN.xaml`、`Strings.en-US.xaml` |
| 版本记录                    | `net9.0-windows7.0\版本修改记录.txt`                              |

## 复用的既有机制（不重写）

* `GlobalVariableLinkControl`（`ModuleCore\UserControls\GlobalVariableLinkControl.xaml(.cs)`）— 链接 UI 控件

* `GlobalVariablesChangedEvent` → `OnGlobalVariablesChanged` → `ReplaceAvailableGlobalVariables` → `RefreshCalibrationDisplayValues`（line 2134/2121/2124）— 实时刷新链路

* `ResolveLinkedValue`（`DispenseStepAction`）— 步骤解析时读取链接值

* `CadAlignTransformSnapshot.Transform`（`Core\Models\CadAlignTransformSnapshot.cs`）— 旋转计算，不改

* `ILocalizationService.GetResourceOrDefault(key, 中文默认值) + string.Format` — 日志多语言模式

## 验证

1. **构建**：`dotnet build c:\WorkFiles\GZQL_MACHINE\GZQL_MACHINE.sln`（期望 0 error；既有 warnings 不相关）
2. **UI 验证**：打开 DISPENSE 步骤详情 → 勾选「启用旋转补偿」→ 确认新增「角度补偿」行显示，可链接全局变量、可解链、解链后回退字面量。
3. **实时刷新**：在「全局变量」页修改已链接变量的 Value 并保存 → 返回 DISPENSE 详情，确认角度补偿显示值实时更新（无需重开页面）。
4. **预览**：点击「查看旋转坐标」→ 弹窗中旋转后坐标应等于 `Transform(cadX, cadY, 旋转角度 + 角度补偿)`；日志含 `有效角度`。
5. **持久化**：设置角度补偿值/链接 → 保存配方 → 重启应用重开步骤 → 值与链接名均保留。
6. **执行**：运行含 DISPENSE 步骤的序列 → 日志 `Disp_Log_RotationCompEnabled` / `Disp_Log_AngleCompensationApplied` 显示有效角度；运动目标坐标与预览一致。
7. **安全回退**：清空角度补偿链接、设字面量 0 → 行为等价于变更前；CAD 快照无效时仍回退原始坐标。

