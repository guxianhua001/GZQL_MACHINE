# Dispense Operation 重构计划

## 需求分析

### 需求1：Dispense Operation 下拉选择 SiteFeature
当前点胶操作基于 DataGrid 选中行（SelectedRow），不够直观。改为下拉选择 SiteFeature 名称。

### 需求2：Dot 模式坐标转换公式修正
**当前（错误）**：`机械坐标 = 视觉坐标 + 针尖偏移`
**正确公式**：`最终点胶位 = 当前拍照位坐标 + 目标点距离相机中心的距离 + 相机距离针头的固定距离 + 针头补偿值`

展开：
- 目标点距离相机中心的距离 = `(pointX - centerX, pointY - centerY)`
- 当前拍照位坐标 = 从 Positions 字典读取的 Dx/Dy 轴位置
- 相机距离针头的固定距离 = NeedleOffset（从全局变量读取）
- 针头补偿值 = NeedleCompensation（新增全局变量）

即：`mechX = photoDx + (pointX - centerX) + needleOffsetX + needleCompX`
    `mechY = photoDy + (pointY - centerY) + needleOffsetY + needleCompY`

**机械坐标显示每个中间结果**：
1. 当前拍照位坐标 (photoDx, photoDy)
2. 目标点距离相机中心的距离 (pointX-centerX, pointY-centerY)
3. 相机距离针头的固定距离 (needleOffsetX, needleOffsetY)
4. 针头补偿值 (needleCompX, needleCompY)
5. 最终点胶位坐标

### 需求3：Arc 模式坐标转换公式修正
**视觉返回数据格式**：
```
centerX=...,centerY=...,point1X=...,point1Y=...,point2X=...,point2Y=...,point3X=...,point3Y=...
```
其中 centerX/Y 是相机中心物理坐标，point1/2/3 是起始/中间/终点的物理坐标。

**转换步骤**：
1. 起始点坐标 = 当前拍照位坐标(Dx,Dy) + 起始点到相机中心的距离(point1X-centerX, point1Y-centerY) + 相机距离针头的固定距离
2. 同理计算中间点和终点在机械坐标系中的位置
3. 用转换后的三点构建贝塞尔弧线，离散化生成坐标集合
4. 可预览坐标

### 需求4：数据显示经转换后的最终坐标集合
MachinePoints DataGrid 中显示每个坐标点，Dot 模式只有1个点，Arc 模式有 N+1 个点。

## 实施步骤

### Step 1: 新增 NeedleCompensation 全局变量支持

**文件**: `VisionCaptureViewModel.cs`

- 新增 `NeedleCompX` / `NeedleCompY` 属性
- `SaveTransformParamsAsync` / `LoadTransformParamsAsync` 增加这两个变量的读写
- XAML 坐标转换区域增加2个 TextBox

### Step 2: 重构 BezierArcDispenseService 坐标转换

**文件**: `BezierArcDispenseService.cs`

**2.1 Dot 模式新公式**：
```
mechX = photoDx + (pointX - centerX) + needleOffsetX + needleCompX
mechY = photoDy + (pointY - centerY) + needleOffsetY + needleCompY
```
- `photoDx/photoDy`：当前拍照位坐标，由调用方传入
- `pointX/pointY`：视觉数据中的目标点坐标（key: needleX/needleY）
- `centerX/centerY`：视觉数据中的相机中心坐标（key: centerX/centerY）
- `needleOffsetX/Y`：相机到针头固定距离
- `needleCompX/Y`：针头补偿值

**2.2 Arc 模式新公式**：
视觉数据包含 centerX/Y, point1X/Y, point2X/Y, point3X/Y
- 起始点机械坐标 = photoDx + (point1X - centerX) + needleOffsetX + needleCompX
- 中间点机械坐标 = photoDx + (point2X - centerX) + needleOffsetX + needleCompX
- 终点机械坐标 = photoDx + (point3X - centerX) + needleOffsetX + needleCompX
- 用转换后的三点构建贝塞尔弧线，离散化

**2.3 新增 ComputeDetailResult 类**：
返回坐标转换的每个中间步骤，供 UI 显示：
```csharp
public class CoordinateTransformDetail
{
    public double PhotoDx, PhotoDy;           // 当前拍照位
    public double DeltaToCenterX, DeltaToCenterY; // 到相机中心距离
    public double NeedleOffsetX, NeedleOffsetY;   // 相机到针头距离
    public double NeedleCompX, NeedleCompY;       // 针头补偿
    public double FinalX, FinalY;                 // 最终坐标
}
```

**2.4 方法签名变更**：
- `ExecuteDotDispenseAsync` 新增 `double photoDx, double photoDy` 参数
- `ExecuteArcDispenseAsync` 新增 `double photoDx, double photoDy` 参数
- `ComputeMachinePointsAsync` 新增 `double photoDx, double photoDy` 参数，返回 `List<CoordinateTransformDetail>`
- `TransformVisionToMachine` 改为接收 photoDx/photoDy + 视觉坐标 + 相机中心 + needleOffset + needleComp

### Step 3: 重构 VisionCaptureViewModel

**3.1 SiteFeature 下拉选择**：
- 新增 `ObservableCollection<string> SiteFeatureNames` 属性（从 PhotoPositionRows 提取）
- 新增 `string SelectedSiteFeatureName` 属性
- 选中后自动匹配 PhotoPositionRow

**3.2 传入拍照位坐标**：
- `ExecuteDispenseAsync` 和 `PreviewMachinePointsAsync` 中从 `_allPositions` 读取当前选中行的 Dx/Dy 位置值作为 photoDx/photoDy

**3.3 显示转换详情**：
- 新增 `ObservableCollection<CoordinateTransformDetail> TransformDetails` 属性
- 预览时填充此集合，UI 显示每个中间步骤

### Step 4: 重构 VisionCaptureView.xaml

**4.1 Dispense Operation 区域**：
- 新增 SiteFeature 下拉 ComboBox
- 保留 RunMode 切换

**4.2 坐标转换详情显示**：
- 在 Machine Coordinates 上方新增 DataGrid 显示 TransformDetails
- 列：拍照位 | 到中心距离 | 针头偏移 | 针头补偿 | 最终坐标

**4.3 坐标转换参数区域**：
- 新增 NeedleCompX / NeedleCompY 两个 TextBox

### Step 5: 编译验证

## 依赖关系

```
Step 1 (ViewModel属性) ──┐
Step 2 (Service重构) ────┤──→ Step 3 (ViewModel重构) ──→ Step 4 (View重构) ──→ Step 5 (编译)
```

Step 1 和 Step 2 可并行。
