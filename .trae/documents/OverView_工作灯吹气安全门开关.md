# OverView 工作灯/吹气/安全门锁 开关实现计划

## 概述（Summary）

为 OverView 总览页顶部快捷操作栏的「工作灯」「吹气」按钮补充 DO 输出 toggle 开关逻辑，
并新增「安全门锁」按钮。三个按钮均为手动 toggle，外观反映通断/锁定状态：

* 工作灯 → 输出点 `Q2.7CabinetLighting`（柜内照明）

* 吹气 → 输出点 `Q3.7MasterAirPressureControl`（总气压控制）

* 安全门锁 → 输出点 `Q1.2SafetyDoorLock`（安全门锁定，true=锁定）

本任务为前序会话的延续：OverViewModel 的 DO 开关逻辑（注入 `IMotionService`、命令、
属性、`ResolveOutputDoIds`、`RefreshOutputStates`、`ToggleDo` 通用方法）已实现完成，
仅剩 `OnNavigatedTo` 末尾回读状态、XAML 接线、多语言、版本记录与构建验证。

## 当前状态分析（Current State Analysis）

### 已完成（前序会话）

**`Module/ViewModels/OverViewModel.cs`**：

* 字段/常量（line 35-45）：`_motionService`、三个 DO 点名常量、三个 `_xxxDoId = -1`

* 绑定属性（line 120-138）：`IsWorkLightOn`/`IsAirBlowOn`/`IsSafetyDoorLocked` + 三个 `IsXxxAvailable`

* 命令声明（line 151-156）：`ToggleWorkLightCommand`/`ToggleAirBlowCommand`/`ToggleSafetyDoorLockCommand`

* 构造函数（line 167-211）：注入 `IMotionService`、命令实例化、`ResolveOutputDoIds()` 调用

* `#region DO 输出开关`（line 381-472）：`ResolveOutputDoIds`/`ResolveDoId`/`RefreshOutputStates`/
  `TryReadDo`/`OnToggleXxx`/`ToggleDo`（取反写入 + 乐观更新 + 失败回滚）

### 待完成

1. `OverViewModel.cs` `OnNavigatedTo`（line 474-501）末尾未调用 `RefreshOutputStates()`
   → 页面导航进入时不会回读硬件状态，按钮颜色不反映真实通断。
2. `OverView.xaml` 三个按钮尚未接 Command/IsEnabled/DataTrigger，且无安全门锁按钮。
3. 多语言缺 `OverView_Btn_SafetyDoorLock` 键。
4. 版本修改记录未追加本任务条目；未构建验证。

### 关键约定（已在前序会话确认，本次沿用）

* **DO 极性**：遵循 `SystemStateService.WriteLight` 权威约定 —— `WriteDo(true)` = 开/锁定，不反转。
  `IODisplayViewModel` 的 `IsActive = !state` 反转疑似 bug，不复制。

* **LogicalId 解析**：运行时按 hwcfg.xml `name` 属性精确匹配（已核对：
  `Q1.2SafetyDoorLock`/`Q2.7CabinetLighting`/`Q3.7MasterAirPressureControl`），
  不硬编码 actDoId。未配置返回 -1，按钮自动禁用。

* **安全性**：DI 安全层（`SystemStateService` 20ms 轮询 SafetyGates）独立于 DO 锁，
  本按钮仅控制 DO 锁定输出，不替代安全回路。`ToggleDo` 失败回滚避免 UI 与硬件不一致。

## 提议变更（Proposed Changes）

### 变更 1：`Module/ViewModels/OverViewModel.cs` — OnNavigatedTo 回读状态

**位置**：`OnNavigatedTo` 方法末尾（line 500，三个 region 处理之后、方法闭合 `}` 之前）
**改法**：追加一行 `RefreshOutputStates();`
**理由**：导航进入页面时 best-effort 回读三个 DO 点硬件状态，使按钮初始颜色反映真实通断，
避免显示与硬件脱节（工业 HMI 安全性要求）。`RefreshOutputStates` 内部已用 `TryReadDo`
包 catch，运动卡未就绪时保持默认 false，不抛异常。

```csharp
                    speedRegion.RequestNavigate(nameof(SpeedControlView));
                }
            }
            // 回读 DO 输出硬件状态，刷新按钮通断/锁定显示（best-effort，失败保持默认）
            RefreshOutputStates();
        }
```

### 变更 2：`Module/Views/OverView.xaml` — 三按钮接 Command + DataTrigger 变色 + 新增安全门锁

**位置**：顶部快捷操作栏 StackPanel（line 45-104）

**设计模式**：复用本文件 `ToggleSingleStep` 按钮（line 151-189）既有的
`Button.Style` + `Style.Triggers` + `DataTrigger` 切换 `Template` 的模式。
不通断时灰色 `#757575`（hover `#616161`），通/锁定时切换为语义色。

**2.a 工作灯按钮**（line 45-64）改写：

* 加 `Command="{Binding ToggleWorkLightCommand}"`

* 加 `IsEnabled="{Binding IsWorkLightAvailable}"`

* 将 `<Button.Template>` 改为 `<Button.Style>` + `Style.Triggers`：

  * 默认 Template：背景 `#757575`（hover `#616161`）— OFF

  * DataTrigger `IsWorkLightOn=True`：切换 Template 背景 `#FF9800`（橙，灯亮）hover `#F57C00`

* 内容 StackPanel 保持（Lightbulb 图标 + 文本）

**2.b 吹气按钮**（line 65-84）改写：

* 加 `Command="{Binding ToggleAirBlowCommand}"`

* 加 `IsEnabled="{Binding IsAirBlowAvailable}"`

* 同样改为 `Button.Style` + `Style.Triggers`：

  * 默认 `#757575`（hover `#616161`）— OFF

  * DataTrigger `IsAirBlowOn=True`：背景 `#1565C0`（蓝）hover `#0D47A1`

**2.c 新增安全门锁按钮**（插在吹气按钮后、日志按钮前，line 84 与 85 之间）：

* `Command="{Binding ToggleSafetyDoorLockCommand}"`

* `IsEnabled="{Binding IsSafetyDoorLockAvailable}"`

* `ToolTip="{lang:Lang OverView_Btn_SafetyDoorLock}"`

* `Button.Style` + `Style.Triggers`：

  * 默认 `#757575`（hover `#616161`）— 未锁定

  * DataTrigger `IsSafetyDoorLocked=True`：背景 `#C62828`（红，危险/锁定）hover `#B71C1C`

* 内容：PackIcon Kind 用 Style + DataTrigger 在 `LockOpen`(未锁) / `Lock`(已锁) 间切换

  * 文本 `{lang:Lang OverView_Btn_SafetyDoorLock}`

* PackIcon 沿用本文件既有内联 `xmlns:materialDesign` 模式

**注意**：用户规则要求「按钮 icon 使用 `<materialDesign:PackIcon>` 不要 emoji」—— 已遵循。
用户规则要求「Binding.FallbackValue 不支持嵌套标记扩展」—— 本次绑定均为简单
`{Binding BoolProp}` 与 `{lang:Lang Key}`，未使用 FallbackValue，无冲突。

### 变更 3：多语言 — 新增 `OverView_Btn_SafetyDoorLock`

**文件**：

* `MainApp/Languages/Strings.zh-CN.xaml`（line 2994 后，按字母序插在 AirBlow 与 EStop 之间）

* `MainApp/Languages/Strings.en-US.xaml`（同位置）

**键值**：

| Key                           | zh-CN | en-US            |
| ----------------------------- | ----- | ---------------- |
| `OverView_Btn_SafetyDoorLock` | 安全门锁  | Safety Door Lock |

ToolTip 复用同一键（与既有 WorkLight/AirBlow/Log 按钮的 `ToolTip="{lang:Lang 同键}"` 模式一致，
不额外造状态 ToolTip 键，保持 proportional）。通断/锁定状态由按钮颜色 + 图标视觉传达。

### 变更 4：版本修改记录

**文件**：

* `版本修改记录.txt`（源）

* `MainApp/bin/Debug/net9.0-windows7.0/版本修改记录.txt`（运行时副本，用户规则要求生成于此）

**条目**：在 `v2026.06.30b` 之后追加 `v2026.06.30c — OverView 工作灯/吹气/安全门锁 DO 开关`，
含需求、实现（OverViewModel 注入 IMotionService + 三命令 + ResolveOutputDoIds + RefreshOutputStates +
ToggleDo 通用方法、OverView\.xaml DataTrigger 变色 + 安全门锁按钮、多语言）、
安全性（DO 极性遵循 WriteLight、失败回滚、DI 安全层独立）、修改文件清单。

## 假设与决策（Assumptions & Decisions）

1. **DO 极性**：`WriteDo(true)=开/锁定`，与 `SystemStateService.WriteLight` 一致。
   安全门锁 hwcfg 标注 LowActive，但现有代码未应用极性反转，本次沿用标准约定；
   调机时核对硬件，若实际反转则统一在 `MotionService.WriteDo` 层处理，不在 ViewModel 散落反转。
2. **状态来源**：进入页面时 `RefreshOutputStates` 回读；toggle 后乐观更新 UI。
   不做定时轮询（OverView 非监控页，避免与 IODisplayViewModel 重复轮询增负担）；
   若需实时性可后续加事件订阅，本次不引入。
3. **按钮颜色语义**：工作灯 ON=橙(#FF9800)、吹气 ON=蓝(#1565C0)、门锁 LOCKED=红(#C62828)。
   红色仅用于锁定态以示危险/受限，符合工业 HMI 习惯。
4. **不改动既有 ToggleSingleStep/日志按钮**，仅动工作灯/吹气两按钮并新增门锁按钮。
5. **多语言键最小集**：仅加 `OverView_Btn_SafetyDoorLock`，不造状态 ToolTip 键（颜色+图标已传达状态）。

## 验证步骤（Verification）

1. `dotnet build` 解决方案（cwd `c:\WorkFiles\GZQL_MACHINE`），确认 0 error。
   输出截断用 `Select-Object -Last 60`（Windows 无 tail）。
2. 确认 OverView 顶部按钮：工作灯/吹气/安全门锁/日志 依次排列。
3. 确认未配置 DO 时（DoId=-1）对应按钮 IsEnabled=False（灰禁用）。
4. 确认 toggle 后颜色切换：工作灯橙、吹气蓝、门锁红；失败回滚颜色。
5. 确认导航离开再回到 OverView，按钮颜色随硬件真实状态刷新。
6. 切换语言（zh-CN/en-US），安全门锁按钮文本/ToolTip正确。
7. 版本修改记录两份（源 + net9.0-windows7.0 副本）均含 `v2026.06.30c` 条目。

