# Step6 Z 向校准（3 轴 XYZ 连续插补）— 构建验证计划

## 摘要

本次会话上下文丢失后，经 Phase 1 探查确认：**Z 向校准功能的全部代码实现已完成且内在一致**，版本修改记录已追加。**唯一剩余步骤是构建验证**，确认 0 编译错误。

## 当前状态分析（已通过 Grep/Read 验证）

### 已完成且验证一致的实现

| # | 文件 | 状态 | 关键验证点 |
|---|------|------|-----------|
| 1 | `MotionControl/Services/ArcContinuousDispenseHelper.cs` | ✅ | 2D 重载（行 55，签名不变）+ 3D 重载（行 97，`IReadOnlyList<(double X,double Y,double Z)>`）+ `ComputePathLengthMm3D`（行 34）+ 共享 `RunCoreAsync` 核心 |
| 2 | `MotionControl/Interfaces/IMotionService.cs` | ✅ | `AreAxesOnSameCard(int[] axisIds)` 接口方法（行 111） |
| 3 | `MotionControl/Services/MotionService.cs` | ✅ | `AreAxesOnSameCard` 实现（行 1162，用 `_axisCardMap` + `ReferenceEquals`） |
| 4 | `Module/Services/IDispenseExecuteService.cs` | ✅ | `DryRunAsync`/`ExecutePathAsync` 末参 `bool zCorrectionEnabled = false`（行 23、34） |
| 5 | `Module/Services/DispenseExecuteService.cs` | ✅ | `ZCorrectionMaxDeltaMm=10.0`（行 40）+ 3 轴分支（行 198-226）+ 同卡校验 + `BuildZCorrectedPath`（行 297-339，deltaZ=pt.Z-firstZ, z=baseZ+deltaZ, 阈值校验抛异常） |
| 6 | `Module/Controls/Cad/CadPointEditorViewModel.cs` | ✅ | `_zCorrectionEnabled = false`（行 1027，默认关闭） |
| 7 | `Module/Controls/Cad/Step6ExecutePanel.xaml` | ✅ | CheckBox + 说明 TextBlock（行 36-38，`Step6_Desc_ZCorrection`） |
| 8 | `MainApp/Languages/Strings.zh-CN.xaml` | ✅ | `Step6_Desc_ZCorrection` + 3 个 `DispenseExec_ZCorrection*` 键 |
| 9 | `MainApp/Languages/Strings.en-US.xaml` | ✅ | 同上 4 键英文版 |
| 10 | `MainApp/bin/Debug/net9.0-windows7.0/版本修改记录.txt` | ✅ | 条目已追加（行 4187-4219） |

### 关键设计决策（已在代码中落实）

- **基准高度** = `EffectiveZHeight`（= TeachHeight + HeightCompensation，保留换针/手动补偿）
- **deltaZ 数据源** = `CadPoint.Z`（CAD 坐标），deltaZ = pt.Z - firstZ
- **第 1 点 deltaZ=0**，Z=baseZ，与预下降目标一致，无起点跳变
- **Z 轴越往下数值越大**：deltaZ>0 表示该点更低，z=baseZ+deltaZ 自动往下
- **针头选择**：needleIndex 0→Dz₂，1→Dz₃（沿用既有 axisDz 解析逻辑）
- **实现范围**：仅 Step6（DispenseExecuteService），生产路径 DispenseStepAction 零改动
- **默认关闭**：`ZCorrectionEnabled=false`（首版更安全，操作员显式开启）

### 安全防护（已在代码中落实）

1. **deltaZ 阈值校验**：`|deltaZ| > ZCorrectionMaxDeltaMm(10mm)` 或非有限值 → 抛 `InvalidOperationException` 中止不夹紧（防碰撞）
2. **同卡校验**：`AreAxesOnSameCard({Dx, Dy, axisDz})` 跨卡 → 抛异常（防 ContiOpenList 静默错配到首卡物理轴号）
3. **3D 路径长度**：提前关胶时序用 `ComputePathLengthMm3D`（3D 矢量长度 ≥ XY 长度，避免低估时长导致末端缺胶）
4. **Z 到位确认**：插补前校验 `|currentZ - targetZ| > 0.5` 则慢速重试（既有逻辑，zCorrection 分支前已执行）

### 已知遗留（非本次范围）

- 连续插补未接 `ISafetyZoneMonitor.CheckInterpolationMoveAllowed`（既有缺口，XY 双轴模式同样未接，非 Z 校准引入）

## 待执行步骤

### 步骤 1：构建验证

```bash
dotnet build Module
```

**预期**：0 error。若出现编译错误，根据错误信息修复（可能涉及命名空间缺失、参数顺序等）。

**修复原则**：
- 优先保持现有实现逻辑不变，仅修编译性错误
- 若错误涉及 `ResourceHelper.GetString` 重载不匹配，检查参数类型与 `Strings.zh-CN.xaml` 中格式串占位符
- 若错误涉及 `AreAxesOnSameCard` 调用，确认 `MotionService` 已实现且 `IMotionService` 接口已更新（已验证）

### 步骤 2：若构建失败则修复后重新构建，直到 0 error

### 步骤 3：构建通过后，无需再追加版本记录（已确认存在于行 4187-4219）

## 验证清单

- [ ] `dotnet build Module` 返回 0 error
- [ ] 若有修复，确认未破坏 2D XY 插补既有路径（签名未变）
- [ ] 若有修复，确认 `BuildZCorrectedPath` 的 deltaZ 计算与阈值校验逻辑不变

## 假设与决策

- **假设**：上下文总结中描述的所有代码修改已正确落盘（Phase 1 已通过 Grep/Read 逐项验证为真）
- **决策**：不再重复实现，仅执行构建验证这一收尾步骤
- **决策**：若构建意外失败，仅做最小编译性修复，不重新设计功能
