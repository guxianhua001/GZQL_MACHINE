# VisionCaptureView 优化设计

> 日期: 2026-07-08
> 范围: `Module/Controls/Dispense/VisionCaptureView` 页面与动作流程优化
> 状态: 已批准，待实施

## 1. 背景与目标

VisionCaptureView（相机引导点胶）当前仅实现点胶位置，缺少点胶工艺参数；Motion Params 的 Safe Position 下拉框冗余；Config Station 表格使用下拉框引用位置名，不够直观；加载/保存按钮位置不合理；点胶执行未走标准工艺流程。

目标：优化页面布局与动作流程，新增点胶工艺参数（Dot/连续点胶），实现完整的相机引导点胶流程，与 `CadPointEditorControl`/`DispenseExecuteService`/`DotDispenseService` 对齐。

## 2. 关键决策（用户确认）

| 决策点 | 选择 |
|--------|------|
| 点胶类型 | 保留 `Dot`/`Arc` 两种，**Arc = 连续点胶**（不废弃弧线逻辑） |
| 表格坐标列 | 拍照位名称**手动输入**，Dx/Dy/Dz₁/Y/Rx/Rz 从位置编辑器**按名只读解析**，与位置编辑器同步 |
| 执行服务 | Dot 走 `DotDispenseService.ExecuteDotDispenseAsync`；Arc 走 `DispenseExecuteService.ExecutePathAsync`；**保留 BezierArcDispenseService 弧线生成**，新增直线生成 |
| 视觉坐标 | 相机返回的是**9点转换后的机械坐标**，直接用于生成弧线/直线 |
| 工艺参数 | 每行持有 `DotProcessParams` + `DispenseSegment`(仅工艺字段) 两个子对象 |
| 直线拓展 | 视觉3点 → P1→P3 单段直线插补（P2 忽略/校验） |
| 底部工具栏 | 独立工具栏行（StatusBar 上方）放置文件名+加载/保存 |

## 3. 硬约束

- **不得影响** `DispenseExecuteService.ExecutePathAsync`、`DotDispenseService.ExecuteDotDispenseAsync` 已实现的功能：只构建 `DotPoint`/`DispenseSegment` 数据传入，不修改这两个服务的现有方法签名与实现。
- 硬件点位不硬编码，DO/DI 从 hwcfg.xml 读取（本任务涉及出胶IO，沿用服务内部既有端口常量）。
- XAML 不使用 `Binding.FallbackValue`；设计时中文用 `d:Text` 占位。
- icon 用 `<materialDesign:PackIcon>`，不用 emoji。
- 运动控制符合工业设备快速响应性与安全性（拍照前先抬轴）。
- 多语言支持（zh-CN + en-US）。
- 版本修改记录写入 `net9.0-windows7.0\版本修改记录.txt`。

## 4. 设计

### 4.1 数据模型（PhotoPositionRow + 持久化）

`PhotoPositionRow` 改造：
- `PositionName`（string，可编辑）替代 `SiteFeatureName`（只读），手动输入对应位置编辑器位置名。
- `Dx`/`Dy`/`Dz1`/`Y`/`Rx`/`Rz`（double，只读解析属性）：通过 `IPositionProvider` 按 `PositionName` 解析；`PositionName` 变更或收到 `StationParameterSavedEvent` 时刷新。
- `DotParams`（`DotProcessParams` 子对象）：Dot 模式工艺参数。
- `ArcParams`（`DispenseSegment` 子对象，仅用工艺字段，Points 留空）：Arc 模式工艺参数。
- `ArcTrackType`（枚举 `Arc`/`Line`）：Arc 模式轨迹子类型。
- 保留：`Speed`/`TriggerCommand`/`ConnectionName`/`Timeout`/`ReturnToSafeAfterCapture`/`DispenseType`/`ArcSegments`/`ArcHeight`/`ArcDirection`/`NeedleOffset*`/`NeedleCompensation*`。
- 新增命令：`AddPhotoPositionCommand`/`DeletePhotoPositionCommand`（ViewModel 级）。

`PhotoPositionRowConfig` 同步新增 `DotParams`/`ArcParams`/`ArcTrackType`/`PositionName` 字段。
`VisionCaptureConfig` 移除 `AvailableSafePositions` 相关；`SafePositionName` 固定 `"SafePosition"`。

### 4.2 UI（VisionCaptureView.xaml）

**Motion Params 区**：删除 Safe Position `ComboBox`，保留坐标只读显示（固定从 `"SafePosition"` 解析）。

**Config Station 表格**：列顺序 `拍照位名称 | Dx | Dy | Dz₁ | Y | Rx | Rz | Speed | Move | Trigger | Connection | 超时 | Type | Capture | Return | Safe`。
- 拍照位名称：可编辑 `DataGridTextColumn`（TwoWay）。
- Dx/Dy/Dz₁/Y/Rx/Rz：只读 `DataGridTextColumn`（OneWay，F3）。
- Type：保留下拉（Dot/Arc）。
- 取消位置列 `CellEditingTemplate` 下拉框。
- 表格上方新增"添加拍照位"/"删除拍照位"按钮。

**底部工具栏**（Grid.Row=2，StatusBar 之前插入独立 Border）：文件名 TextBox(只读) + 加载按钮 + 保存按钮。

**Step2 工艺参数面板**（右侧"点胶操作"Card 下方新增"工艺参数"Card）：
- `DispenseType==Dot` → DotParams 编辑面板。
- `DispenseType==Arc` → ArcParams 编辑面板 + 轨迹子类型(弧线/直线)选择。
- 控件用 `DecimalUpDown` + `GlobalVariableLinkControl`，布局参考 `Step3EditParamsPanel.xaml`。

### 4.3 拍照运动安全抬轴

`ExecuteCaptureAsync`/`MoveToTeachPositionAsync` 执行前新增 `RaiseZAxesToSafeAsync()`：
- 从位置编辑器 `"SafePosition"` 的 Dz₁ 解析安全高度。
- 依次抬起 Dz₁(axis 2)/Dz₂(axis 3)/Dz₃(axis 4) 到安全高度。
- 抬轴完成后再 XY 移动/拍照。

### 4.4 执行流程（核心，不改标准服务）

**Dot 模式**：
```
视觉中心机械坐标 + TargetOffset + NeedleOffset + NeedleCompensation
→ 最终点胶机械坐标 (X,Y)
→ 构建 DotPoint{Dx,Dy,Dz2/Dz3,Y,Rx,Rz}（单点）
→ DotDispenseService.ExecuteDotDispenseAsync(points, row.DotParams, needleIndex, token)
```

**Arc 模式（连续点胶）**：
```
视觉 P1/P2/P3 机械坐标 + 相机中心 + CamToNeedle + ArcNeedleOffset + ArcNeedleComp
→ BezierArcDispenseService 生成机械坐标点列表：
   弧线: GenerateArcMachinePoints(P1,P2,P3,arcHeight,arcDirection,segments) [保留]
   直线: GenerateLineMachinePoints(P1,P3) [新增，P1→P3 单段]
→ 包装为 DispenseSegment{Points=[CadPoint{MachineX,MachineY}], 工艺参数=row.ArcParams}
→ DispenseExecuteService.ExecutePathAsync(segments, site, needleIndex, token, pauseEvent)
```

**废弃**：`BezierArcDispenseService.ExecuteDotDispenseAsync`/`ExecuteArcDispenseAsync`（自带执行逻辑）在此页不再调用；保留并复用其生成方法。

**直线生成方法**：`BezierArcDispenseService` 新增 `static List<(double X,double Y)> GenerateLineMachinePoints((double X,double Y) p1, (double X,double Y) p3, int samplePoints)`，等距采样 P1→P3 直线。

### 4.5 加载/保存与文件管理

- 加载/保存/文件名移至底部工具栏。
- `PhotoPositionRowConfig` 新增工艺参数序列化。
- 沿用 `VisionCapture_CurrentFile` ExtensionData 持久化（受保护文件）。

### 4.6 多语言与架构

- 新增资源键到 `Languages/Strings.zh-CN.xaml` + `Strings.en-US.xaml`。
- 依赖方向：VisionCaptureViewModel → DotDispenseService/DispenseExecuteService/BezierArcDispenseService（Module/StationTasks 层，无倒置依赖）。

## 5. 影响文件清单

| 文件 | 变更类型 |
|------|----------|
| `Module/Controls/Dispense/PhotoPositionRow.cs` | 改造数据模型 |
| `Module/Controls/Dispense/VisionCaptureViewModel.cs` | 改造命令/执行流程/坐标解析 |
| `Module/Controls/Dispense/VisionCaptureView.xaml` | UI 改造 |
| `StationTasks/Services/BezierArcDispenseService.cs` | 新增 GenerateLineMachinePoints |
| `Languages/Strings.zh-CN.xaml` / `Strings.en-US.xaml` | 新增资源键 |
| `net9.0-windows7.0/版本修改记录.txt` | 记录变更 |
