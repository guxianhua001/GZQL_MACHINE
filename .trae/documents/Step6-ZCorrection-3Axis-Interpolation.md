# Step6 Z向校准 (Z-Correction) — 3轴 XYZ 连续插补

## Context（背景）

`Step6ExecutePanel` 的 `ZCorrectionEnabled`（Z向校准）复选框目前只是一个 UI 存根：它在 `CadPointEditorViewModel` 中被持久化到 `CadPointPanelOptions`，但 `ExecutePath()` 执行时**从未消费**该标志，实际点胶走的是 XY 双轴连续插补，Z 轴在插补过程中保持静止于 `EffectiveZHeight`。

需求：启用 Z向校准时，根据所选针头（needleIndex 0→Dz₂，1→Dz₃），将该 Z 轴与 Dx、Dy 一起纳入连续插补（3轴连续插补），使针头在走胶过程中跟随 CAD 表面 Z 轮廓。点胶高度计算：以段内第 1 个点的 Z 为基准 0，每点 Z 目标 = 基准高度 + deltaZ_i，其中 deltaZ_i = `CadPoint.Z_i - CadPoint.Z_1`（Z 轴越往下数值越大）。

预期结果：3D 表面跟随点胶能力，同时不破坏生产路径（`DispenseStepAction`）和现有 XY 双轴行为。

## Design Decisions（已与用户确认）

| 项 | 决定 |
|---|---|
| 基准高度 | `EffectiveZHeight` (= TeachHeight + HeightCompensation)，保留换针/手动补偿，deltaZ 叠加其上 |
| deltaZ 数据源 | `CadPoint.Z`（CAD 坐标，非 MachineZ） |
| 实现范围 | 仅 `DispenseExecuteService`（Step6 手动/测试执行路径）；生产 `DispenseStepAction` 不变 |
| 默认值 | `ZCorrectionEnabled` 默认 `false`（首版更安全，操作员显式开启） |
| 公式 | 点 i 的 Z 目标 = `EffectiveZHeight + (CadPoint.Z_i - CadPoint.Z_1)`；第 1 点 deltaZ=0，Z=EffectiveZHeight（与现有预下降目标一致，无起点跳变） |

## Implementation Steps

### 1. `MotionControl\Services\ArcContinuousDispenseHelper.cs` — 加 3D 重载（非破坏）
- 抽取共享私有核心 `RunCoreAsync(... IReadOnlyList<double[]> pathPoints, Func<double> pathLenForGlueTiming ...)`。
- 现有 2D 公开方法委托核心（每点 `new[]{x,y}`，XY-only `ComputePathLengthMm`）。**签名不变** → `DispenseStepAction` 零改动。
- 新增 3D 公开重载（每点 `new[]{x,y,z}`，新增 `ComputePathLengthMm3D` 用 `sqrt(dx²+dy²+dz²)`）。
- **正确性要点**：3D 必须用 3D 路径长度算提前关胶时序，否则低估时长→末端缺胶。

### 2. `MotionControl\Interfaces\IMotionService.cs` + `MotionService.cs` — 同卡校验
- `IMotionService` 新增 `bool AreAxesOnSameCard(int[] axisIds)`。
- `MotionService` 用私有 `GetCardForAxis` 比较。`DispenseExecuteService` 3 轴插补前调用，跨卡即抛异常。

### 3. `Module\Services\IDispenseExecuteService.cs` — 接口加参数
- `DryRunAsync`、`ExecutePathAsync` 末尾加 `bool zCorrectionEnabled = false`。修正 XML 注释 Dz1/Dz2→Dz₂/Dz₃。

### 4. `Module\Services\DispenseExecuteService.cs` — 核心实现
- 透传 `zCorrectionEnabled` 到 `ExecuteSegmentsAsync`。
- 常量 `ZCorrectionMaxDeltaMm = 10.0`。
- `BuildZCorrectedPath(seg, baseZ)`：firstZ=seg.Points[0].Z；每点 deltaZ=pt.Z-firstZ，z=baseZ+deltaZ；校验 MachineX/Y、IsFinite、|deltaZ|<=阈值，超阈值抛异常；记录 min/max deltaZ 与校正后 Z。
- 插补段分支：`zCorrectionEnabled && descendToWorkHeight` → 3D 重载 + `new[]{AxisDx,AxisDy,axisDz}` + 同卡校验；else 现有 XY-only。
- 预下降序列不变（targetZ=EffectiveZHeight，第1点Z=base无跳变）。

### 5. `Module\Controls\Cad\CadPointEditorViewModel.cs` — 透传 + 默认 false
- `_zCorrectionEnabled = true` → `false`。
- `ExecuteRun` 的 DryRunAsync(3126)/ExecutePathAsync(3144) 传 `ZCorrectionEnabled`。

### 6. `Module\Controls\Cad\Step6ExecutePanel.xaml` — 加说明文字
- 第 36 行 CheckBox 后加 `<TextBlock Text="{lang:Lang Step6_Desc_ZCorrection}" FontSize="10" Foreground="#9E9E9E" TextWrapping="Wrap" Margin="24,2,0,4"/>`。

### 7. 本地化 `Strings.zh-CN.xaml` + `Strings.en-US.xaml`
- `Step6_Desc_ZCorrection`、`DispenseExec_ZCorrectionEnabled`、`DispenseExec_ZCorrectionDeltaExceeded`、`DispenseExec_ZCorrectionCrossCard`（中英）。

### 8. `MainApp\bin\Debug\net9.0-windows7.0\版本修改记录.txt` — 追加

### 9. 构建验证 `dotnet build Module` 0 error。

## Safety
1. deltaZ 阈值(默认10mm)+有限性校验→抛异常不夹紧。
2. 同卡校验 AreAxesOnSameCard。
3. 起点连续性（第1点Z=base，无跳变）。
4. 日志输出 deltaZ 范围供方向核对。
5. 预下降与开胶时序不变；3D 用 3D 长度算提前关胶。
6. 已知遗留(非本次范围)：连续插补未接 SafetyZoneMonitor（2/3轴共有既有缺口），建议独立任务。

## Critical Files
- `MotionControl\Services\ArcContinuousDispenseHelper.cs`
- `Module\Services\DispenseExecuteService.cs`
- `Module\Services\IDispenseExecuteService.cs`
- `MotionControl\Interfaces\IMotionService.cs` + `MotionControl\Services\MotionService.cs`
- `Module\Controls\Cad\CadPointEditorViewModel.cs`
- `Module\Controls\Cad\Step6ExecutePanel.xaml`
- `MainApp\Languages\Strings.zh-CN.xaml` + `Strings.en-US.xaml`
- `MainApp\bin\Debug\net9.0-windows7.0\版本修改记录.txt`
