# 修复：闭合样条曲线图形选取不到、未生成点位

## 问题根因

### 原因 1（主要）：`IsHit` 方法缺少 Spline 命中检测
**文件**：`Module/Controls/Cad/HalconCanvasControl.xaml.cs:1251-1264`

当前 `IsHit` 方法的 switch 表达式只处理了 Line、Arc、Circle、LwPolyline、Ellipse 五种类型，`CadEntityType.Spline` 走默认分支返回 `false`，导致所有 SPLINE 实体在画布上永远无法被点击选中。

### 原因 2（次要）：SPLINE 包围盒估算偏低
**文件**：`Core/Models/CadSpline.cs:205-238`

`GetBoundingBox()` 仅基于控制点多边形 + 5% 扩展估算，NURBS 曲线可能显著超出控制点多边形（尤其闭合高曲率样条），但不影响主功能。

---

## 修复步骤

### 步骤 1：添加 `IsHitSpline` 命中检测方法
**文件**：`Module/Controls/Cad/HalconCanvasControl.xaml.cs`

在 `IsHitEllipse` 方法之后添加 `IsHitSpline` 方法：

```csharp
/// <summary>
/// 样条曲线命中检测——将曲线离散化为折线段后逐段检测最短距离
/// 复用已缓存的 DxfParserService 进行离散化采样
/// </summary>
private bool IsHitSpline(CadSpline spline, double px, double py, double tolerance)
{
    // 通过 Core 项目暴露的静态服务获取离散化点
    var points = CadEntityHalconExtensions.DiscretizeSplineForHitTest(spline);
    if (points == null || points.Count < 2)
        return false;
    
    int segCount = spline.IsClosed ? points.Count : points.Count - 1;
    for (int i = 0; i < segCount; i++)
    {
        int j = (i + 1) % points.Count;
        var p1 = points[i];
        var p2 = points[j];
        
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-10) continue;
        
        double t = Math.Clamp(((px - p1.X) * dx + (py - p1.Y) * dy) / lenSq, 0, 1);
        double nearX = p1.X + t * dx;
        double nearY = p1.Y + t * dy;
        double dist = Math.Sqrt(Math.Pow(px - nearX, 2) + Math.Pow(py - nearY, 2));
        
        if (dist <= tolerance)
            return true;
    }
    return false;
}
```

### 步骤 2：在 `IsHit` switch 中添加 Spline 分支
**文件**：`Module/Controls/Cad/HalconCanvasControl.xaml.cs:1255-1263`

在 `CadEntityType.Ellipse` 之后添加：
```csharp
CadEntityType.Spline => IsHitSpline((CadSpline)entity, cadX, cadY, toleranceCad),
```

### 步骤 3：添加 `DiscretizeSplineForHitTest` 静态辅助方法
**文件**：`Core/Models/CadEntityHalconExtensions.cs`

为了避免 HalconCanvasControl 直接依赖 DxfParserService，在 CadEntityHalconExtensions 中添加一个静态辅助方法：

```csharp
/// <summary>
/// 为命中检测提供样条曲线离散化点列表
/// 复用 DxfParserService 的 de Boor 算法
/// </summary>
public static List<Core.Models.PointF> DiscretizeSplineForHitTest(CadSpline spline)
{
    if (DxfParserService == null || spline == null)
        return null;
    
    try
    {
        // 调用 DxfParserService 的离散化方法获取采样点
        var cadPoints = DxfParserService.Discretize(spline, pitchMM: 0.5);
        if (cadPoints == null || cadPoints.Count < 2)
            return null;
        
        return cadPoints.Select(p => new Core.Models.PointF((float)p.X, (float)p.Y)).ToList();
    }
    catch
    {
        return null;
    }
}
```

**注意**：需要确认 `IDxfParserService` 接口是否暴露了 `Discretize` 方法（接受 CadEntity 和 pitchMM 参数）。如果方法签名为 `Discretize(CadEntity entity, double pitchMM)`，则直接调用即可。

### 步骤 4：编译验证
运行 `dotnet build` 确保无编译错误。

---

## 受影响的文件
1. `Module/Controls/Cad/HalconCanvasControl.xaml.cs` — 添加 `IsHitSpline` + switch 分支
2. `Core/Models/CadEntityHalconExtensions.cs` — 添加 `DiscretizeSplineForHitTest` 辅助方法

## 预期结果
- 闭合/开口 SPLINE 实体在画布上可正常点击选中
- 选中后在 Step3 面板点位表格中显示离散化采样点（含起点/终点）
- 对非 SPLINE 图元无影响