# VisionCaptureView 优化实施计划

> **目标:** 优化 VisionCaptureView 页面与动作流程，新增点胶工艺参数，实现完整相机引导点胶流程，与标准服务对齐。
> **架构:** VisionCaptureViewModel 构建 `DotPoint`/`DispenseSegment` 数据 → 传入 `DotDispenseService`/`DispenseExecuteService`（不改服务）。BezierArcDispenseService 保留弧线生成 + 新增直线生成。
> **技术栈:** WPF + Prism + MaterialDesignInXaml + C# (.NET 9)
> **约束:** 不修改 `DispenseExecuteService.ExecutePathAsync`/`DotDispenseService.ExecuteDotDispenseAsync` 现有实现；多语言；不硬编码硬件点位；icon 用 PackIcon；不用 Binding.FallbackValue。

---

## Phase 1: 数据模型改造（PhotoPositionRow + 持久化）

### Task 1.1: PhotoPositionRow 改造

**Files:**
- Modify: `Module/Controls/Dispense/PhotoPositionRow.cs`

- [ ] **Step 1:** 新增 `ArcTrackType` 枚举（`Arc`/`Line`），放在 `DispenseType` 枚举旁。
- [ ] **Step 2:** 将 `SiteFeatureName`（只读）改为 `PositionName`（可编辑 `string`，`SetProperty` 支持双向绑定），保留构造函数参数名兼容（旧代码传 siteFeatureName 赋给 PositionName）。
- [ ] **Step 3:** 新增只读解析属性 `Dx`/`Dy`/`Dz1`/`Y`/`Rx`/`Rz`（double，私有 set），由 ViewModel 调用 `UpdateParsedCoordinates(Dictionary<string,double>)` 填充。
- [ ] **Step 4:** 新增 `DotParams`（`DotProcessParams`，构造时 new）和 `ArcParams`（`DispenseSegment`，构造时 new 空 Points）子对象属性。
- [ ] **Step 5:** 新增 `ArcTrackType` 属性（默认 `Arc`）。
- [ ] **Step 6:** 保留原 `DxPositionName`/`DyPositionName`/`Dz1PositionName`/`YPositionName` 字段为兼容旧配置加载（标记 `[Obsolete]`，加载时迁移到 PositionName），避免破坏旧 JSON。实际新逻辑用 PositionName。
- [ ] **Step 7:** 编译验证。

### Task 1.2: 配置持久化模型扩展

**Files:**
- Modify: `Module/Controls/Dispense/VisionCaptureViewModel.cs`（末尾 `PhotoPositionRowConfig`/`VisionCaptureConfig`）

- [ ] **Step 1:** `PhotoPositionRowConfig` 新增 `PositionName`/`DotParams`/`ArcParams`/`ArcTrackType` 字段；保留旧字段（DxPositionName 等）用于迁移。
- [ ] **Step 2:** `VisionCaptureConfig` 的 `SafePositionName` 固定默认 `"SafePosition"`（移除下拉相关逻辑，字段保留以兼容）。
- [ ] **Step 3:** 编译验证。

---

## Phase 2: BezierArcDispenseService 新增直线生成

### Task 2.1: 新增 GenerateLineMachinePoints

**Files:**
- Modify: `StationTasks/Services/BezierArcDispenseService.cs`

- [ ] **Step 1:** 在 `GenerateArcMachinePoints`（line 107）旁新增静态方法：
```csharp
/// <summary>生成直线插补机械坐标点：P1→P3 等距采样</summary>
public static List<(double X, double Y)> GenerateLineMachinePoints(
    (double X, double Y) p1, (double X, double Y) p3, int samplePoints)
{
    var pts = new List<(double X, double Y)>(samplePoints);
    if (samplePoints <= 1) { pts.Add(p1); pts.Add(p3); return pts; }
    for (int i = 0; i <= samplePoints; i++)
    {
        double t = (double)i / samplePoints;
        pts.Add((p1.X + (p3.X - p1.X) * t, p1.Y + (p3.Y - p1.Y) * t));
    }
    return pts;
}
```
- [ ] **Step 2:** 编译验证。

---

## Phase 3: ViewModel 改造

### Task 3.1: 注入标准点胶服务

**Files:**
- Modify: `Module/Controls/Dispense/VisionCaptureViewModel.cs`（构造函数 + 字段）
- Modify: `Module/PrimModel.cs`（注册服务，若未注册）

- [ ] **Step 1:** ViewModel 新增只读字段 `DotDispenseService _dotDispenseService`、`DispenseExecuteService _dispenseExecuteService`，构造函数注入。
- [ ] **Step 2:** 确认 `PrimModel.cs` 已注册这两个服务（若未注册则 `containerRegistry.RegisterSingleton<...>()`）。
- [ ] **Step 3:** 编译验证。

### Task 3.2: 坐标解析与同步

**Files:**
- Modify: `Module/Controls/Dispense/VisionCaptureViewModel.cs`

- [ ] **Step 1:** 新增 `UpdateRowParsedCoordinatesAsync(PhotoPositionRow)`：通过 `ResolvePositionKey(PositionName, axis)` + `_allPositions` 解析 Dx/Dy/Dz1/Y/Rx/Rz 填入 row。
- [ ] **Step 2:** `PositionName` 变更（监听 row.PropertyChanged）或 `StationParameterSavedEvent` 收到时调用上述方法刷新所有行坐标。
- [ ] **Step 3:** `SafePositionName` 固定 `"SafePosition"`，`RefreshSafePositionDisplay()` 从位置编辑器解析 SafePosition 的 Dx/Dy/Dz1 显示。
- [ ] **Step 4:** 移除 `AvailableSafePositions` 下拉数据源相关逻辑（属性可保留为空或删除，XAML 不再绑定）。
- [ ] **Step 5:** 编译验证。

### Task 3.3: 拍照运动安全抬轴

**Files:**
- Modify: `Module/Controls/Dispense/VisionCaptureViewModel.cs`

- [ ] **Step 1:** 新增 `RaiseZAxesToSafeAsync(CancellationToken)`：
```csharp
// 从 SafePosition 解析 Dz1 作为安全高度（Dz2/Dz3 复用同值或按需）
double safeZ = ResolveAxisValue("SafePosition", "Dz₁");
const int AxisDz1=2, AxisDz2=3, AxisDz3=4;
await _motionService.MoveAbsAsync(AxisDz1, safeZ, speed, token);
await _motionService.MoveAbsAsync(AxisDz2, safeZ, speed, token);
await _motionService.MoveAbsAsync(AxisDz3, safeZ, speed, token);
```
- [ ] **Step 2:** `ExecuteCaptureAsync`/`MoveToTeachPositionAsync` 入口调用 `await RaiseZAxesToSafeAsync(token)`，再执行 XY 移动/拍照。
- [ ] **Step 3:** 编译验证。

### Task 3.4: 添加/删除拍照位命令

**Files:**
- Modify: `Module/Controls/Dispense/VisionCaptureViewModel.cs`

- [ ] **Step 1:** 新增 `AddPhotoPositionCommand`（创建新 `PhotoPositionRow("NewPos"+n)`，加入 `PhotoPositionRows`，设为 SelectedRow）。
- [ ] **Step 2:** 新增 `DeletePhotoPositionCommand`（参数 PhotoPositionRow，从 `PhotoPositionRows` 移除 SelectedRow 或传入行）。
- [ ] **Step 3:** 编译验证。

### Task 3.5: Dot 执行流程改造

**Files:**
- Modify: `Module/Controls/Dispense/VisionCaptureViewModel.cs`（`ExecuteDispenseAsync` Dot 分支）

- [ ] **Step 1:** Dot 分支：计算最终点胶机械坐标 (FinalX/FinalY)（沿用现有 CalculateFinalX/Y 逻辑）。
- [ ] **Step 2:** 构建 `DotPoint`（设 Dx=FinalX, Dy=FinalY, Dz2/Dz3=row.DotParams.TeachHeight+HeightCompensation, Y=row.Y, Rx=row.Rx, Rz=row.Rz, IsSelected=true, IsEnabled=true）。
- [ ] **Step 3:** 调用 `_dotDispenseService.ExecuteDotDispenseAsync(new[]{dotPoint}, row.DotParams, needleIndex, token)`（needleIndex 由当前选中针头决定，默认0）。
- [ ] **Step 4:** DryRun 模式调用 `_dotDispenseService.DryRunAsync(...)`。
- [ ] **Step 5:** 移除原 `_bezierArcDispenseService.ExecuteDotDispenseAsync` 调用。
- [ ] **Step 6:** 编译验证。

### Task 3.6: Arc 执行流程改造

**Files:**
- Modify: `Module/Controls/Dispense/VisionCaptureViewModel.cs`（`ExecuteDispenseAsync` Arc 分支）

- [ ] **Step 1:** Arc 分支：计算 P1/P2/P3 + 相机中心机械坐标（沿用现有逻辑）。
- [ ] **Step 2:** 根据 `row.ArcTrackType`：
  - `Arc` → `BezierArcDispenseService.GenerateArcMachinePoints(P1,P2,P3,arcHeight,arcDirection,arcSegments)`
  - `Line` → `BezierArcDispenseService.GenerateLineMachinePoints(P1,P3,arcSegments)`
- [ ] **Step 3:** 将机械坐标点列表包装为 `DispenseSegment`：复制 `row.ArcParams` 工艺参数，`Points` 设为 `CadPoint{MachineX,MachineY}` 列表。
- [ ] **Step 4:** DryRun 调用 `_dispenseExecuteService.DryRunAsync(segments,...)`；走胶调用 `_dispenseExecuteService.ExecutePathAsync(segments, site, needleIndex, token, _pauseEvent)`。
- [ ] **Step 5:** 移除原 `_bezierArcDispenseService.ExecuteArcDispenseAsync` 调用。
- [ ] **Step 6:** 编译验证。

### Task 3.7: 配置加载/保存适配

**Files:**
- Modify: `Module/Controls/Dispense/VisionCaptureViewModel.cs`（`SaveConfigToFileAsync`/`LoadConfigFromFileAsync`）

- [ ] **Step 1:** 保存时映射 row → rowConfig（含 PositionName/DotParams/ArcParams/ArcTrackType）。
- [ ] **Step 2:** 加载时映射 rowConfig → row，兼容旧字段（DxPositionName 等迁移到 PositionName）。
- [ ] **Step 3:** 编译验证。

---

## Phase 4: UI 改造（XAML）

### Task 4.1: Motion Params 区删除下拉框

**Files:**
- Modify: `Module/Controls/Dispense/VisionCaptureView.xaml`（line 244-269）

- [ ] **Step 1:** 删除 Safe Position `ComboBox`（line 258-260）。
- [ ] **Step 2:** 保留 Safe Position 标签 + Dx/Dy/Dz₁ 只读 TextBlock 显示。
- [ ] **Step 3:** 编译验证。

### Task 4.2: Config Station 表格改造

**Files:**
- Modify: `Module/Controls/Dispense/VisionCaptureView.xaml`（line 303-445）

- [ ] **Step 1:** 表格上方新增"添加拍照位"/"删除拍照位"按钮（绑定 AddPhotoPositionCommand/DeletePhotoPositionCommand）。
- [ ] **Step 2:** 重构 DataGrid 列：拍照位名称（TextColumn TwoWay PositionName）| Dx | Dy | Dz₁ | Y | Rx | Rz（TextColumn OneWay 只读 F3）| Speed | Move | Trigger | Connection | 超时 | Type（保留下拉）| Capture | Return | Safe。
- [ ] **Step 3:** 删除 Dx/Dy/Dz₁/Y 的 `CellEditingTemplate` ComboBox。
- [ ] **Step 4:** 编译验证。

### Task 4.3: 底部工具栏

**Files:**
- Modify: `Module/Controls/Dispense/VisionCaptureView.xaml`（Grid 结构 + line 206-242 文件管理区）

- [ ] **Step 1:** 主 Grid 新增 RowDefinition（Auto）作为底部工具栏，原 StatusBar 下移一行。
- [ ] **Step 2:** 新增底部工具栏 Border：文件名 TextBox(只读) + 加载按钮 + 保存按钮（从原文件管理 Card 移出）。
- [ ] **Step 3:** 删除原 Step1 顶部"文件管理"Card（line 207-242）。
- [ ] **Step 4:** 编译验证。

### Task 4.4: Step2 工艺参数面板

**Files:**
- Modify: `Module/Controls/Dispense/VisionCaptureView.xaml`（line 1054-1143 右侧面板）

- [ ] **Step 1:** "点胶操作"Card 下方新增"工艺参数"Card。
- [ ] **Step 2:** Dot 模式面板（Visibility 绑定 IsDotMode）：DecimalUpDown 绑定 SelectedRow.DotParams.* 各字段。
- [ ] **Step 3:** Arc 模式面板（Visibility 绑定 IsArcMode）：轨迹子类型选择(弧线/Line) + DecimalUpDown 绑定 SelectedRow.ArcParams.* 各字段。
- [ ] **Step 4:** 设计时机中文用 `d:Text` 占位，icon 用 PackIcon。
- [ ] **Step 5:** 编译验证。

---

## Phase 5: 多语言资源

### Task 5.1: 新增资源键

**Files:**
- Modify: `Languages/Strings.zh-CN.xaml`
- Modify: `Languages/Strings.en-US.xaml`（若存在）

- [ ] **Step 1:** 新增资源键：`VisionCapture_AddPhotoPos`/`VisionCapture_DeletePhotoPos`/`VisionCapture_ProcessParams`/`VisionCapture_TrackType`/`VisionCapture_TrackType_Arc`/`VisionCapture_TrackType_Line`/`VisionCapture_DotParams_*`（MoveSpeed/SafeHeight/ApproachHeight/CornerDecel/DispenseTime/PreDispenseDelay/PostDelay/DotGlueTriggerOffset/DispensingPressure/SuckBackTime/TeachHeight/HeightCompensation）/`VisionCapture_ArcParams_*`（JumpSpeed/InterpSpeed/MoveSpeed/DispenseAmount/PreDelay/PostDelay/EarlyCloseGlueDelayMs/CornerDecel/TeachHeight/HeightCompensation/SafeHeight/GlueTriggerOffsetMm/DispenseTime/ApproachHeight/DispensingPressure/SuckBackTime）。
- [ ] **Step 2:** 中英文对照填写。
- [ ] **Step 3:** 编译验证。

---

## Phase 6: 验证与收尾

### Task 6.1: 全量编译验证

- [ ] **Step 1:** `dotnet build` 全解决方案，0 error。
- [ ] **Step 2:** 修复编译警告（与本次改动相关）。

### Task 6.2: 版本修改记录

**Files:**
- Modify: `net9.0-windows7.0/版本修改记录.txt`

- [ ] **Step 1:** 追加本次变更记录（日期、版本号、变更摘要）。

### Task 6.3: 逻辑审查

- [ ] **Step 1:** 确认 `DispenseExecuteService.ExecutePathAsync`/`DotDispenseService.ExecuteDotDispenseAsync` 未被修改。
- [ ] **Step 2:** 确认抬轴逻辑在所有拍照/移动入口生效。
- [ ] **Step 3:** 确认旧配置文件可兼容加载（迁移逻辑）。

---

## 自审清单

- [x] Spec 覆盖：6 节设计均有对应 Task
- [x] 类型一致：PositionName/DotParams/ArcParams/ArcTrackType 命名跨 Task 一致
- [x] 无占位符
- [x] 约束：不改标准服务（Task 3.5/3.6 仅构建数据传入）
