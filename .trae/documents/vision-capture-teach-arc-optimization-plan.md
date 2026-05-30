# VisionCaptureView 示教功能 & Arc 弧线优化设计方案

## 一、需求概述

### 1.1 表格功能增强
- 在 `PhotoPositionRows` DataGrid 的 **Spd 列后** 新增【示教】按钮
- 示教按钮功能：读取当前 Dx/Dy/Dz1/Y 轴的实际物理坐标
- 示教后新增【移动】列：按安全流程运动到示教位置
- 自动调整 DataGrid 列宽显示

### 1.2 移动动作流程（安全性要求）
```
Z轴抬起(到安全高度) → XY轴移动到目标位置 → Z轴下降(到拍照高度) → 触发拍照
```

### 1.3 Arc 模式功能优化
- 视觉返回起点(P1)、中间点(P2)、结束点(P3) + 相机中心(C) 共4个坐标
- 收到坐标后**自动生成贝塞尔弧线**
- 弧线段数可配置（复用现有 `ArcSegments` 属性）
- 所有离散点坐标在 MachinePoints 表格中可见
- **新增弧线图形可视化**（Canvas 绘制）

### 1.4 计算方法简化
- 参考历史成功项目 `DispensingPathViewModel.cs` 的计算逻辑
- 简化代码结构，但保证计算结果完全一致

---

## 二、技术方案设计

### 2.1 DataGrid 列结构调整

#### 当前列顺序：
```
Site | Dx | Dy | Dz₁ | Y | Spd | Trigger | Connection | Type | Capture
```

#### 调整后列顺序：
```
Site | Dx | Dy | Dz₁ | Y | Spd | 示教 | 移动 | Trigger | Connection | Type | Capture
```

**列定义说明：**
| 列名 | 宽度 | 类型 | 说明 |
|------|------|------|------|
| 示教 | 50px | Button | PackIcon=CrosshairsGps，读取当前轴坐标填入Dx/Dy/Dz1/Y |
| 移动 | 50px | Button | PackIcon=Navigation，执行安全移动流程 |

**自动列宽策略：**
- 固定宽度列：Site(70), Spd(40), 示教(50), 移动(50), Trigger(55), Connection(60), Type(48), Capture(52)
- 自适应列：Dx, Dy, Dz₁, Y 使用 `Width="*"` 均分剩余空间

### 2.2 参数配置区增强

在现有的「偏移/补偿参数」Card 中新增 **运动参数配置** 区域：

```xml
<!-- 运动参数配置 -->
<Border Style="{StaticResource SectionHeader}">
    <StackPanel Orientation="Horizontal">
        <materialDesign:PackIcon Kind="Robot" ... />
        <TextBlock Text="运动参数" />
    </StackPanel>
</Border>
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="100" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>
    <!-- SafePositionName -->
    <TextBlock Text="安全位" ... />
    <TextBox Text="{Binding SafePositionName}" ... />
    <!-- PhotoHeightName -->
    <TextBlock Text="拍照高度" ... />
    <TextBox Text="{Binding PhotoHeightName}" ... />
</Grid>
```

**新增 ViewModel 属性：**
```csharp
private string _photoHeightName = "PhotoHeight";
public string PhotoHeightName { get; set; }  // 拍照高度位置名称
```

### 2.3 示教命令实现

```csharp
// 新增命令
public DelegateCommand<PhotoPositionRow> TeachPositionCommand { get; }

// 实现：读取当前轴坐标并更新行数据
private async Task TeachPositionAsync(PhotoPositionRow row)
{
    var axisIdMap = ResolveAxisIdMap();

    // 读取各轴当前位置
    double dxPos = await _motionService.GetActualPositionAsync(axisIdMap["Dx"]);
    double dyPos = await _motionService.GetActualPositionAsync(axisIdMap["Dy"]);
    double dz1Pos = await _motionService.GetActualPositionAsync(axisIdMap["Dz1"]);
    double yPos = await _motionService.GetActualPositionAsync(axisIdMap["Y"]);

    // 更新到位置提供者（持久化）
    // 同时更新界面显示的 AvailablePositions 关联值
}
```

### 2.4 移动命令实现（安全流程）

```csharp
// 新增命令
public DelegateCommand<PhotoPositionRow> MoveToTeachPositionCommand { get; }

// 实现安全移动流程
private async Task MoveToTeachPositionAsync(PhotoPositionRow row)
{
    var axisIdMap = ResolveAxisIdMap();
    var coordId = ResolveCoordId();

    // 1. 从全局变量/配方获取安全高度和拍照高度
    double safeZ = GetPositionValue(SafePositionName + ".Dz₁");
    double photoZ = GetPositionValue(PhotoHeightName + ".Dz₁");

    // 2. Z轴抬起
    await _motionService.MoveAbsAsync(axisIdMap["Dz1"], safeZ, row.Speed, token);

    // 3. XY轴直线插补移动
    double targetX = GetPositionValue(row.DxPositionName + ".Dx");
    double targetY = GetPositionValue(row.DyPositionName + ".Dy");
    await _motionService.MoveLineAbsAsync(coordId,
        new[] { axisIdMap["Dx"], axisIdMap["Dy"] },
        new[] { targetX, targetY }, row.Speed, token);

    // 4. Z轴下降到拍照高度
    await _motionService.MoveAbsAsync(axisIdMap["Dz1"], photoZ, row.Speed, token);

    // 5. 可选：自动触发拍照
}
```

### 2.5 Arc 弧线可视化组件

在 Step2 的右侧面板或底部新增 **Canvas 弧线预览**：

```xml
<!-- 弧线可视化区域 -->
<materialDesign:Card Visibility="{Binding IsArcMode, Converter={StaticResource BoolToVis}}">
    <StackPanel>
        <TextBlock Text="弧线预览" FontWeight="SemiBold" />
        <Canvas x:Name="ArcPreviewCanvas" Height="200"
                Background="#FFF5F5F5"
                ClipToBounds="True">
            <!-- 坐标系网格 -->
            <!-- 贝塞尔曲线 -->
            <!-- 控制点标记 -->
            <!-- 离散点标记 -->
        </Canvas>
    </StackPanel>
</materialDesign:Card>
```

**绘制逻辑：**
1. 计算 MachinePoints 的边界框 (BoundingBox)
2. 自动缩放和平移以适应 Canvas 尺寸
3. 绘制元素：
   - 浅灰色坐标系网格
   - 蓝色贝塞尔曲线路径 (`Path` 或 `Polyline`)
   - 绿色圆点：P1(起点)、P2(中点)、P3(终点)
   - 灰色小圆点：离散插值点
   - 标注文字

### 2.6 计算方法简化设计

#### 当前计算逻辑（BezierArcDispenseService.cs）

```
机械坐标 = 拍照位置(photoDx/photoDy)
         + 点相对中心的偏移(pointX - centerX, pointY - centerY)
         + 针头偏移(NeedleOffsetX/Y)
         + 针头补偿(NeedleCompX/Y)
```

#### 简化后的方法封装

将分散的计算逻辑整合为清晰的步骤方法：

```csharp
/// <summary>
/// 计算单点的机械坐标（简化版）
/// 公式: Mech = PhotoPos + (VisionPoint - VisionCenter) + NeedleOffset + NeedleComp
/// </summary>
public static (double X, double Y) ComputeMachineCoordinate(
    (double Dx, double Dy) photoPosition,      // 拍照时的轴坐标
    (double X, double Y) visionPoint,           // 视觉检测到的目标点
    (double X, double Y) visionCenter,          // 视觉中心（相机中心）
    (double X, double Y) needleOffset,          // 针头固定偏移
    (double X, double Y) needleCompensation)    // 针头补偿量
{
    // 步骤1: 计算视觉偏移（目标点相对于相机中心的偏移）
    double visionDeltaX = visionPoint.X - visionCenter.X;
    double visionDeltaY = visionPoint.Y - visionCenter.Y;

    // 步骤2: 累加所有偏移量得到最终机械坐标
    double mechX = photoPosition.Dx + visionDeltaX + needleOffset.X + needleCompensation.X;
    double mechY = photoPosition.Dy + visionDeltaY + needleOffset.Y + needleCompensation.Y;

    return (mechX, mechY);
}

/// <summary>
/// 生成Arc模式的贝塞尔离散点（简化版）
/// </summary>
public static List<(double X, double Y)> GenerateArcMachinePoints(
    (double Dx, double Dy) photoPosition,
    (double X, double Y) p1, (double X, double Y) p2, (double X, double Y) p3,  // 三控制点
    (double X, double Y) center,
    (double X, double Y) needleOffset,
    (double X, double Y) needleCompensation,
    int segmentCount)
{
    // 步骤1: 将三个视觉坐标转换为机械坐标
    var mechP1 = ComputeMachineCoordinate(photoPosition, p1, center, needleOffset, needleCompensation);
    var mechP2 = ComputeMachineCoordinate(photoPosition, p2, center, needleOffset, needleCompensation);
    var mechP3 = ComputeMachineCoordinate(photoPosition, p3, center, needleOffset, needleCompensation);

    // 步骤2: 对三个机械坐标点进行二阶贝塞尔离散化
    return DiscretizeQuadraticBezier(mechP1, mechP2, mechP3, segmentCount);
}

/// <summary>
/// 二阶贝塞尔曲线离散化: B(t) = (1-t)²P0 + 2(1-t)t·P1 + t²P2
/// </summary>
public static List<(double X, double Y)> DiscretizeQuadraticBezier(
    (double X, double Y) p0, (double X, double Y) p1, (double X, double Y) p2,
    int segments)
{
    var points = new List<(double X, double Y)>();
    for (int i = 0; i <= segments; i++)
    {
        double t = (double)i / segments;
        double mt = 1.0 - t;

        double x = mt * mt * p0.X + 2 * mt * t * p1.X + t * t * p2.X;
        double y = mt * mt * p0.Y + 2 * mt * t * p1.Y + t * t * p2.Y;

        points.Add((x, y));
    }
    return points;
}
```

**关键改进点：**
1. 使用命名元组 `(double X, double Y)` 提高可读性
2. 将计算分解为语义清晰的单步方法
3. 公式注释明确，便于后续维护
4. 结果与原实现完全一致（数学等价）

---

## 三、文件修改清单

| 文件 | 修改内容 |
|------|----------|
| `VisionCaptureView.xaml` | DataGrid新增示教/移动列；新增运动参数配置区；新增弧线Canvas |
| `VisionCaptureViewModel.cs` | 新增 TeachPositionCommand、MoveToTeachPositionCommand；新增 PhotoHeightName 属性；新增弧线绘制属性 |
| `PhotoPositionRow.cs` | 无需修改（复用现有属性） |
| `BezierArcDispenseService.cs` | 重构计算方法为简化版（保持接口兼容） |

---

## 四、多语言支持

新增/修改的语言键：

| 键名 | 中文 | English |
|------|------|---------|
| `VisionCapture_ColumnTeach` | 示教 | Teach |
| `VisionCapture_ColumnMove` | 移动 | Move |
| `VisionCapture_MotionParams` | 运动参数 | Motion Params |
| `VisionCapture_SafePosition` | 安全位 | Safe Pos |
| `VisionCapture_PhotoHeight` | 拍照高度 | Photo Height |
| `VisionCapture_ArcPreview` | 弧线预览 | Arc Preview |
| `VisionCapture_TeachSuccess` | 示教完成 | Teach Complete |
| `VisionCapture_MoveComplete` | 移动完成 | Move Complete |

---

## 五、实施步骤

### Phase 1: UI 层修改
1. [ ] VisionCaptureView.xaml - DataGrid 新增示教/移动按钮列
2. [ ] VisionCaptureView.xaml - 新增运动参数配置区
3. [ ] VisionCaptureView.xaml - 新增弧线可视化 Canvas

### Phase 2: ViewModel 逻辑
4. [ ] VisionCaptureViewModel.cs - 新增 TeachPositionCommand 实现
5. [ ] VisionCaptureViewModel.cs - 新增 MoveToTeachPositionCommand 实现（含安全流程）
6. [ ] VisionCaptureViewModel.cs - 新增弧线绘制相关属性和方法

### Phase 3: 服务层重构
7. [ ] BezierArcDispenseService.cs - 简化计算方法（保持向后兼容）

### Phase 4: 多语言 & 测试
8. [ ] 更新多语言资源文件
9. [ ] 功能测试验证
