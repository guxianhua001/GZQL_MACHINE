# CadAlignmentView 第3/4/5/1步功能增强 Spec

## Why
当前 CadAlignmentView 的 5 步流程已实现基础计算功能，但存在以下操作效率问题：
1. **步骤3（旋转角度）**：基准线段和目标线段的点坐标只能从 ComboBox 手动选择，无法直接从 CAD 图形窗口上点击选取，操作不直观
2. **步骤4（坐标变换）**：目标点位只能从全局列表选择，不能自动继承步骤3中选中的目标点对结果
3. **步骤5（夹爪定位）**：缺少基准点示教功能（X/Y/Ry/Z），只有固定偏移量 OffX/OffY 输入，无法显示"计算偏移量 vs 固定偏移量"的对比
4. **步骤1（回转中心）**：FitPoints 表格的 X(FitX)/Y(FitY) 列只能手动输入，缺少示教按钮从运动控制器或视觉系统获取实际测量值

## What Changes
- **步骤3 增强**：引入 CadPointEditorView 的图形选取能力，基准线段和目标线段可从 CAD 图形窗口上拾取两点确定
- **步骤4 增强**：新增「使用步骤3目标点」快捷选项，自动填充 TransformSelectedIndex
- **步骤5 增强**：新增夹爪基准点示教区（TeachX/TeachY/TeachRy/TeachZ），显示计算偏移量（= 当前位置 - 目标变换后位置），保留固定偏移量 OffX/OffY
- **步骤1 增强**：FitPoints DataGrid 的 X/Y 列追加示教按钮（CrosshairsGps 图标），触发运动轴定位→读取实际坐标回填

## Impact
- Affected specs: `cad-alignment-5step-redesign` (增强，非破坏性)
- Affected code:
  - `Module/Controls/Assembly/CadAlignmentViewModel.cs` — 新增属性、命令、方法
  - `Module/Controls/Assembly/CadAlignmentView.xaml` — Tab1/3/4/5 UI 增强
  - 可能需要与 `Module/Controls/Cad/CadPointEditorControl` 或其 ViewModel 交互

---

## ADDED Requirements

### Requirement: 步骤3 — 从CAD图形窗口选取线段端点

系统 SHALL 在步骤3的基准线段/目标线段选择器旁提供「从图形选取」按钮：

#### Scenario: 从图形窗口选取基准线段
- **WHEN** 用户在步骤3点击基准线段旁的「📐 从CAD图选取」按钮
- **THEN** 系统打开/激活一个 CAD 图形浏览窗口，用户可在图上依次点击两个点确定 P1 和 P2
- **AND** 点击完成后，P1/P2 的 CAD 坐标自动填入对应 CorrespondencePoint 的 CadX/CadY 字段
- **AND** BasePairIndex 自动选中对应的点对
- **AND** 角度 α_base 立即更新显示

#### Scenario: 从图形窗口选取目标线段
- **WHEN** 用户在步骤3点击目标线段旁的「📐 从CAD图选取」按钮
- **THEN** 同上逻辑，用户选取两个点作为 P3/P4（或其他目标线段）
- **AND** TargetPairIndex 自动更新
- **AND** 角度 α_target 立即更新显示

**UI 变更**: 在 Tab3 SectionCard1 的两个 ComboBox 旁各增加一个 SecondaryActionButton（图标 CrosshairsGps / VectorIntersection），文字「从CAD选取」

**前置条件**: 已加载 DXF/DWG 文件到 CAD 编辑器（通过 CadPointEditorControl 或独立导入）

### Requirement: 步骤4 — 继承步骤3的目标点位

系统 SHALL 在步骤4的点位选择器中提供「使用步骤3目标点」快捷入口：

#### Scenario: 一键继承步骤3结果
- **WHEN** 用户在步骤4点击「↓ 继承步骤3目标点」按钮
- **THEN** TransformSelectedIndex 自动设为步骤3 TargetPairIndex 对应的第一个点（如 P3）
- **AND** 该点的 CAD 坐标立即显示在只读框中
- **AND** 如果步骤3尚未完成（ThetaDeg == 0），按钮禁用并提示「请先完成步骤3」

**UI 变更**: 在 Tab4 SectionCard1 的 ComboBox 上方或旁侧添加一个 SecondaryActionButton（图标 ArrowDownBoldHexagonOutline），文字「↓ 用步骤3目标」

### Requirement: 步骤5 — 夹爪基准点示教 + 双模式偏移

系统 SHALL 在步骤5新增夹爪基准点示教功能和双偏移量对比显示：

#### Scenario: 示教夹爪基准点
- **WHEN** 用户在步骤5点击「🎯 示教夹爪当前位置」按钮
- **THEN** 系统读取当前夹爪/末端执行器的实际机械坐标 (TeachX, TeachY, TeachRy, TeachZ)
- **AND** 坐标显示在示教结果区域（ResultBorder 卡片）
- **AND** 计算并显示「计算偏移量」:
  - CalcOffX = TeachX - FinalTargetX（FinalTargetX 来自步骤4的 X_new）
  - CalcOffY = TeachY - FinalTargetY
- **AND** 用户可选择「应用计算偏移」（将 CalcOffX/Y → OffX/OffY）或保留手动输入的固定偏移

#### Scenario: 固定偏移模式（保留原有行为）
- **WHEN** 用户手动修改 OffX/OffY 输入框
- **THEN** 系统使用用户指定的固定偏移量计算最终 Gripper_X/Y
- **AND** 「计算偏移量」区域以灰色/虚线标记为未采用状态

**UI 变更**: Tab5 SectionCard1 重构：
```
┌─ 夹爪最终组装定位 ─────────────────────┐
│                                            │
│  目标点坐标(只读): [X_new] [Y_new]        │
│                                            │
│  ─── 示教区 ────────────────────────────   │
│  [🎯 示教当前位置]                         │
│  TeachX: [___]  TeachY: [___]             │
│  TeachRy:[___]  TeachZ:[___]              │
│                                            │
│  ─── 偏移量 ────────────────────────────   │
│  ○ 计算偏移: ΔX=[calc] ΔY=[calc]          │
│    [应用计算偏移]                          │
│  ● 固定偏移:                              │
│    OffX: [___]  OffY: [___]               │
│                                            │
│  [⑤ 计算夹爪位置]                          │
└────────────────────────────────────────────┘
```

**新增属性**:
- `TeachX/TeachY/TeachRy/TeachZ` (double, BindableBase)
- `CalcOffX/CalcOffY` (double, readonly computed)
- `UseCalculatedOffset` (bool, default false)
- `TeachGripperPositionCommand` (ICommand)

### Requirement: 步骤1 — FitPoints 表格追加示教按钮

系统 SHALL 在 FitPoints DataGrid 的 X(FitX)/Y(FitY) 列追加示教按钮：

#### Scenario: 单行示教拟合点坐标
- **WHEN** 用户点击某一行 FitPoint 的「📍 示教」按钮
- **THEN** 系统提示用户将 Rz 轴旋转到该行对应的角度（AngleLabel），确认后读取当前机械坐标
- **AND** 将读取到的 (Mx, My) 回填到该行的 FitX/FitY 字段
- **AND** 状态栏提示「已获取角度 XX° 的实测坐标」

**UI 变更**: Tab1 DataGrid 列定义从 3 列扩展为 4 列:
| 角度 | X(FitX) | Y(FitY) | 操作 |
|------|---------|---------|------|
| 0° | [文本框] | [文本框] | [📍示教] |
| 90° | ... | ... | [📍示教] |

每行操作列放一个紧凑的 SecondaryActionButton（图标 CrosshairsGps，小尺寸）

**新增命令**: `TeachFitPointCommand` (参数: int rowIndex)

---

## MODIFIED Requirements

### Requirement: CadAlignmentViewModel 属性集
**变更**: 新增以下属性:
```csharp
// 步骤1 示教
public ICommand TeachFitPointCommand { get; }

// 步骤3 图形选取
public ICommand PickBaselineFromCadCommand { get; }
public ICommand PickTargetFromCadCommand { get; }
public bool HasCadDrawingLoaded { get; } // 是否有可用的CAD图形

// 步骤4 继承
public ICommand InheritTargetFromStep3Command { get; }
public bool CanInheritFromStep3 => Step3Done && ThetaDeg != 0;

// 步骤5 示教+双偏移
public double TeachX / TeachY / TeachRy / TeachZ { get; set; }
public double CalcOffX => TeachX - FinalTargetX;
public double CalcOffY => TeachY - FinalTargetY;
public bool UseCalculatedOffset { get; set; }
public ICommand TeachGripperPositionCommand { get; }
public ICommand ApplyCalcOffsetCommand { get; }
```

### Requirement: ComputeGripperPosition() 方法
**变更**: 支持 UseCalculatedOffset 分支:
```
if (UseCalculatedOffset):
    offX = CalcOffX; offY = CalcOffY;
else:
    offX = GripperOffX; offY = GripperOffY;
Gripper_X = X_new + offX
Gripper_Y = Y_new + offY
```
