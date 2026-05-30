# CAD对位工具 5步标准流程重设计 Spec

## Why
现有 CadAlignmentView 的 3 步（参数配置/标定计算/变换工具）是通用学术型设计，与现场实际操作流程不匹配。用户提供了完整的 **5 步工业标准流程**（求回转中心→全局偏移→CAD角度→坐标变换→夹爪定位），需要将 ViewModel 的数据模型、计算逻辑、UI 步骤全部重构为与实际工艺一致的 5 步导航模式，并提供可验证的默认测试数据。

## What Changes
- **步骤从 3 步扩展为 5 步**，严格对应现场操作顺序
- **ViewModel 数据模型重构**：新增 Rz 四点拟合集合、全局偏移 ΔX/ΔY、CAD 向量角度 θ、夹爪偏移 OffX/OffY 等属性；清理不再需要的旧属性（CorrectedCenterX/Y/Z、CalibrationStatus 等）
- **5 个核心计算方法重写**：四点圆拟合求中心、偏移量计算、向量方向角计算、先平移后旋转变换、夹爪定位
- **CorrespondencePoint 模型扩展**：新增 RotatedX/RotatedY/RotatedZ 字段存储变换后坐标
- **XAML Tab 从 3 个改为 5 个**，每步一个 Tab 页面，保持现有 SectionCard / RefCard / ResultBorder 样式体系
- **默认验证数据**：提供一组 P1~P6 的 CAD 坐标和四点拟合坐标，用户点击即可验证全流程结果
- **底部步骤指示器同步更新为 5 个圆点**

## Impact
- Affected code:
  - `Module/Controls/Assembly/CadAlignmentViewModel.cs` — 主要重写对象
  - `Module/Controls/Assembly/CadAlignmentView.xaml` — UI 重构为 5 Tab
  - `Core/Models/CorrespondencePoint.cs` — 新增 RotatedX/Y/Z 属性
  - `Module/Converters/StepIndicatorConverters.cs` — StepDotColor 扩展到 5 个
- 不影响其他 View 或 Module

---

## ADDED Requirements

### Requirement: 5步导航系统

系统 SHALL 提供 5 步顺序导航，步骤不可跳过，必须按 1→2→3→4→5 顺序执行：

| 步骤 | 标题 | 提示 | 图标 |
|------|------|------|------|
| 1 | 回转中心 | 四点拟合求 Rz 中心 | `TargetVariant` |
| 2 | 全局偏移 | 计算 ΔX/ΔY 偏移量 | `ArrowExpandHorizontal` |
| 3 | 旋转角度 | CAD 向量方向角计算 | `AngleAcute` |
| 4 | 坐标变换 | 先平移后旋转 | `SwapHorizontal` |
| 5 | 夹爪定位 | 最终组装位置计算 | `RobotIndustrial` |

#### Scenario: 步骤导航
- **WHEN** 用户在任意步骤点击"下一步"
- **THEN** 当前步骤标记完成(绿色)，下一步激活(蓝色)，Tab 自动切换
- **WHEN** 用户在第 1 步未完成时点击"下一步"
- **THEN** 按钮禁用或提示需先完成当前步骤计算

### Requirement: 步骤1 — 四点拟合求 Rz 回转中心

系统 SHALL 提供四点圆拟合功能：

**输入**: Rz 轴分别在 0°/90°/180°/270° 时相机拍摄同一特征点的 4 组机械坐标 (Mx, My)

**输出**: Rz 回转中心机械坐标 O (Mox, Moy)

**算法**:
```
最小二乘法拟合圆：(x-a)² + (y-b)² = r²
其中 (a,b) = 圆心 = 回转中心 Mox, Moy
```

**UI 元素**:
- DataGrid 输入 4 行数据（角度列+X列+Y列）
- "① 拟合回转中心"按钮
- 结果展示：Mox, Moy, 半径 R
- 公式说明卡片

**默认验证数据**:
```
0°   → (100.000, 200.000)
90°  → (150.500, 198.200)
180° → (102.300, 150.800)
270° → ( 48.700, 153.500)
预期结果: Mox ≈ 100.0, Moy ≈ 175.0
```

### Requirement: 步骤2 — 计算全局偏移量 ΔX/ΔY

系统 SHALL 在 Rz 回归 0° 后计算 CAD 与机械坐标系的全局固定偏移：

**输入**:
- P1 实物机械坐标 P1(Mx, My) — 来自相机实拍
- P1 CAD 设计坐标 P1(Cx, Cy) — 来自图纸

**输出**: ΔX = Mx - Cx, ΔY = My - Cy

**前置条件**: 步骤1已完成（有回转中心）

**UI 元素**:
- P1 机械坐标输入框 (Mx, My)
- P1 CAD 坐标输入框 (Cx, Cy) — 可从 CorrespondencePoints 自动读取
- "② 计算偏移"按钮
- 结果展示：ΔX, ΔY（ResultBorder 卡片）
- 说明文字："此偏移量为全局固定值，后续所有点位共用"

**默认验证数据**:
```
P1机械: (70.320, 213.260)
P1 CAD: (100.000, 200.000)
预期: ΔX = -29.680, ΔY = 13.260
```

### Requirement: 步骤3 — 纯CAD坐标计算旋转角度 θ

系统 SHALL 仅使用 CAD 两点坐标向量计算所需旋转角度（无需视觉拍照）：

**输入**:
- 基准线段 P1P2 的 CAD 坐标
- 目标线段 P3P4（或 P5P6）的 CAD 坐标

**输出**: θ = α_基准 - α_目标（度）

**算法**:
```
α = atan2(Y2-Y1, X2-X1) × 180/π    （向量方向角，范围 -180° ~ 180°）
θ = α_P1P2 - α_P3P4
正值为逆时针旋转，负值为顺时针
```

**UI 元素**:
- 基准点对选择（P1/P2 下拉框）
- 目标点对选择（P3/P4 下拉框）
- 各线段方向角显示
- "③ 计算旋转角度"按钮
- 结果：θ 角度值（大字高亮显示）
- 可视化示意图文字描述

**默认验证数据**:
```
P1(100,200), P2(150,250) → α基准 = atan2(50,50) = 45.00°
P3(120,180), P4(130,220) → α目标 = atan2(40,10) = 75.96°
预期: θ = 45.00 - 75.96 = -30.96° （顺时针约31°）
```

### Requirement: 步骤4 — 坐标转换（先平移后旋转）

系统 SHALL 对所有目标点位执行"先平移后绕中心旋转"的核心变换：

**输入**: 任意 CAD 点位 (Cx, Cy)

**中间变量**（自动计算）:
```
未旋转机械坐标: Xm = Cx + ΔX, Ym = Cy + ΔY
相对中心偏移: dx = Xm - Mox, dy = Ym - Moy
```

**输出**:
```
X_new = dx×cosθ - dy×sinθ + Mox
Y_new = dx×sinθ + dy×cosθ + Moy
```

**UI 元素**:
- 点位选择下拉框（P3/P4/P5/P6）
- 该点位 CAD 坐标显示（只读，来自数据表）
- 中间过程展示：未旋转机械(Xm,Ym)、相对中心(dx,dy)
- "④ 执行变换"按钮
- 最终结果 X_new, Y_new（绿色高亮 ResultBorder）
- 变换公式参考卡片
- **批量变换按钮**：一键对所有非基准点执行变换，结果写入 CorrespondencePoint.RotatedX/Y/Z

**前置条件**: 步骤1~3 均已完成（Mox/Moy, ΔX/ΔY, θ 已知）

**默认验证数据**（以 P3 为例）:
```
P3 CAD: (120, 180)
Mox=100, Moy=175, ΔX=-29.68, ΔY=13.26, θ=-30.96°
Xm = 120 + (-29.68) = 90.32
Ym = 180 + 13.26 = 193.26
dx = 90.32 - 100 = -9.68
dy = 193.26 - 175 = 18.26
cos(-30.96°) = 0.857, sin(-30.96°) = -0.515
X_new = (-9.68)(0.857) - (18.26)(-0.515) + 100 = 108.14
Y_new = (-9.68)(-0.515) + (18.26)(0.857) + 175 = 195.27
```

### Requirement: 步骤5 — 夹爪最终组装定位

系统 SHALL 基于变换后的目标点坐标和预设夹爪偏移量计算最终定位坐标：

**输入**:
- 目标点变换后坐标 (X_new, Y_new) — 来自步骤4
- 夹爪相对该点的固定偏移 (OffX, OffY)

**输出**:
```
Gripper_X = X_new + OffX
Gripper_Y = Y_new + OffY
```

**UI 元素**:
- 目标点选择（继承步骤4的结果）
- 夹爪偏移量输入 (OffX, OffY)
- "⑤ 计算夹爪位置"按钮
- 最终结果：Gripper_X, Gripper_Y（大字绿色加粗卡片）
- 易错要点提示面板（5条规则）

**前置条件**: 步骤4已完成（有变换后的坐标）

**默认验证数据**:
```
P3 变换后: (108.14, 195.27)
夹爪偏移: OffX = 15.0, OffY = -10.0
预期: Gripper_X = 123.14, Gripper_Y = 185.27
```

### Requirement: 默认验证数据完整性

系统 SHALL 启动时就加载一套完整的默认数据集，使用户无需手动输入即可点击各步骤按钮验证全流程：

**CorrespondencePoints 默认值（P1~P6）**:
| Name | CadX | CadY | CadZ |
|------|------|------|------|
| P1 | 100.0 | 200.0 | 50.0 |
| P2 | 150.0 | 250.0 | 55.0 |
| P3 | 120.0 | 180.0 | 52.0 |
| P4 | 130.0 | 220.0 | 53.0 |
| P5 | 140.0 | 210.0 | 54.0 |
| P6 | 110.0 | 190.0 | 51.0 |

**Actual 坐标（模拟相机实拍值）**:
| Name | ActualX | ActualY | ActualZ |
|------|---------|---------|---------|
| P1 | 70.32 | 213.26 | 0 |
| P2 | 100.20 | 277.28 | 0 |
| P3 | 95.95 | 201.28 | 0 |
| P4 | 91.67 | 242.28 | 0 |
| P5 | 104.47 | 236.30 | 0 |
| P6 | 83.14 | 207.26 | 0 |

**四点拟合默认值**:
| 角度 | FitX | FitY |
|------|------|------|
| 0° | 100.000 | 200.000 |
| 90° | 150.500 | 198.200 |
| 180° | 102.300 | 150.800 |
| 270° | 48.700 | 153.500 |

**夹爪偏移默认值**: OffX = 15.0, OffY = -10.0

---

## MODIFIED Requirements

### Requirement: AlignmentStepInfo.ShowConnector
**变更**: `ShowConnector => Number < 5`（原为 Number < 3）

### Requirement: CadAlignmentViewModel.CurrentStepTitle
**变更**: 返回值扩展为 5 个 case：
```csharp
1 => "① 回转中心",
2 => "② 全局偏移",
3 => "③ 旋转角度",
4 => "④ 坐标变换",
5 => "⑤ 夹爪定位"
```

### Requirement: CadAlignmentViewModel.CanGoNext / CanGoPrev
**变更**: 上限改为 5，下限仍为 1

### Requirement: 底部状态栏进度圆点
**变更**: StepDotColor 扩展到 StepDotColor1~5

### Requirement: CorrespondencePoint 模型
**变更**: 新增属性
```csharp
private double _rotatedX, _rotatedY, _rotatedZ;
public double RotatedX { get; set; }
public double RotatedY { get; set; }
public double RotatedZ { get; set; }
```

---

## REMOVED Requirements

### Requirement: 旧标定计算流程
**原因**: 原 ComputeCalibration / CorrectedCenterX/Y/Z / CorrectedAngleDeg 是通用的最小二乘拟合方法，与新 5 步标准流程的计算逻辑不兼容
**迁移**: 替换为新的 5 个独立计算方法（FitRotationCenter / ComputeGlobalOffset / ComputeCadRotationAngle / ExecuteTransform / ComputeGripperPosition），旧方法代码删除

### Requirement: SVD 坐标系标定
**原因**: CalibrateCoordinateSystemCommand / ComputeCameraToGripperTransform 属于高级可选功能，不在 5 步标准流程范围内
**迁移**: 暂时移除，可在后续版本作为独立扩展功能添加

### Requirement: EstimateRotationCenterCommand
**原因**: 旧的估算方法基于理论点反推，与新的四点实测拟合方法冲突
**迁移**: 替换为 FitRotationCenterCommand（四点圆拟合）
