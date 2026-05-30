# DXF LWPOLYLINE Bulge支持修复 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复DXF导入后圆弧上多出直线的Bug，通过完整实现LWPOLYLINE的Bulge（凸度/组码42）解析支持

**Architecture:** 新建CadSegment模型表示多段线的子段类型（直线/圆弧），修改ParseLwPolyline解析bulge值并构建Segments列表，修改DiscretizePolyline和ToHObject按段类型分别处理

**Tech Stack:** C# / .NET 9.0, WPF + Prism, HalconDotNet, Math.NET Numerics (可选)

---

## 文件结构总览

| 操作 | 文件路径 | 职责 |
|------|---------|------|
| **Create** | `Core/Models/CadSegment.cs` | 多段子段模型（Line/Arc）+ Bulge→圆弧转换算法 |
| **Modify** | `Core/Models/CadLwPolyline.cs` | 新增Segments/Bulges属性和BuildSegments方法 |
| **Modify** | `Core/Services/DxfParserService.cs:408-473` | ParseLwPolyline增加组码42解析 |
| **Modify** | `Core/Services/DxfParserService.cs:1185-1228` | DiscretizePolyline支持混合段离散化 |
| **Modify** | `Core/Models/CadEntityHalconExtensions.cs:209-246` | ToHObject(LwPolyline)正确渲染混合轮廓 |

---

## Task 1: 创建 CadSegment 模型

**Files:**
- Create: `Core/Models/CadSegment.cs`

- [ ] **Step 1: 创建 CadSegment.cs 文件**

```csharp
// Core/Models/CadSegment.cs
using System;

namespace Core.Models
{
    /// <summary>
    /// 多段线的子段类型枚举
    /// </summary>
    public enum CadSegmentType
    {
        Line,   // 直线段
        Arc     // 圆弧段
    }

    /// <summary>
    /// 多段线的单个子段（直线或圆弧）
    /// 由LWPOLYLINE的bulge值解析得到
    /// Bulge定义：b = tan(θ/4)，其中θ为圆弧的圆心角（弧度）
    /// b=0表示直线段，b>0逆时针圆弧，b<0顺时针圆弧，|b|=1为半圆
    /// </summary>
    public class CadSegment
    {
        public CadSegmentType SegmentType { get; set; }
        
        // 起点和终点坐标（所有段共有）
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
        
        // 仅圆弧段使用
        public double CenterX { get; set; }      // 圆心X
        public double CenterY { get; set; }      // 圆心Y
        public double Radius { get; set; }       // 半径
        public double StartAngle { get; set; }   // 起始角度（度，从正X轴逆时针）
        public double EndAngle { get; set; }     // 终止角度（度）
        public double Bulge { get; set; }        // 原始bulge值（用于判断方向）
        
        public bool IsArc => SegmentType == CadSegmentType.Arc;
        
        private CadSegment() { }

        /// <summary>
        /// 工厂方法：从两点坐标和bulge值创建子段对象
        /// bulge=0时创建直线段，否则创建圆弧段并计算完整的圆弧参数
        /// </summary>
        public static CadSegment CreateFromBulge(
            double x1, double y1, 
            double x2, double y2, 
            double bulge)
        {
            if (Math.Abs(bulge) < 1e-10)
            {
                return new CadSegment
                {
                    SegmentType = CadSegmentType.Line,
                    StartX = x1, StartY = y1,
                    EndX = x2, EndY = y2,
                    Bulge = 0
                };
            }
            
            return CreateArcFromBulge(x1, y1, x2, y2, bulge);
        }

        /// <summary>
        /// 从bulge值计算完整的圆弧几何参数
        /// 
        /// 算法流程：
        ///   1. 计算弦长 chord = |P1P2|
        ///   2. 计算圆心角 θ = 4 × arctan(|bulge|)
        ///   3. 计算半径 r = chord × (1 + b²) / (4 × |b|)
        ///   4. 计算矢高 sagitta = |b| × chord / 2
        ///   5. 计算圆心位置（基于弦中点+垂直偏移）
        ///   6. 计算起止角度（从圆心到端点的方位角）
        /// </summary>
        private static CadSegment CreateArcFromBulge(
            double x1, double y1, 
            double x2, double y2, 
            double bulge)
        {
            var segment = new CadSegment
            {
                SegmentType = CadSegmentType.Arc,
                StartX = x1,
                StartY = y1,
                EndX = x2,
                EndY = y2,
                Bulge = bulge
            };

            // 步骤1：计算弦长和弦中点
            double dx = x2 - x1;
            double dy = y2 - y1;
            double chord = Math.Sqrt(dx * dx + dy * dy);
            
            if (chord < 1e-10)
            {
                // 退化情况：两点重合，返回直线段
                segment.SegmentType = CadSegmentType.Line;
                return segment;
            }

            double midX = (x1 + x2) / 2.0;
            double midY = (y1 + y2) / 2.0;

            // 步骤2：计算圆心角 θ = 4 × arctan(|bulge|)
            double absBulge = Math.Abs(bulge);
            double theta = 4.0 * Math.Atan(absBulge);  // 圆心角（弧度）

            // 步骤3：计算半径 r = chord × (1 + b²) / (4 × |b|)
            double radius = chord * (1.0 + bulge * bulge) / (4.0 * absBulge);
            segment.Radius = radius;

            // 步骤4：计算矢高 sagitta = |b| × chord / 2
            double sagitta = absBulge * chord / 2.0;

            // 步骤5：计算圆心位置
            // 弦的方向角 α = atan2(dy, dx)
            double alpha = Math.Atan2(dy, dx);
            
            // 垂直方向角 β = α + π/2 × sign(bulge)
            // bulge>0时圆心在弦左侧（逆时针），bulge<0时在右侧（顺时针）
            double sign = bulge > 0 ? 1.0 : -1.0;
            double beta = alpha + Math.PI / 2.0 * sign;

            // apothem = √(r² - (chord/2)²)：弦中点到圆心的距离
            double halfChord = chord / 2.0;
            double apothemSquared = radius * radius - halfChord * halfChord;
            
            double apothem;
            if (apothemSquared < 0)
            {
                // 数值误差导致负数（理论上不会发生），使用近似值
                apothem = Math.Max(0, sagitta);
            }
            else
            {
                apothem = Math.Sqrt(apothemSquared);
                
                // 优弧情况（θ > π）：圆心在弦的另一侧
                if (theta > Math.PI)
                {
                    apothem = -apothem;
                }
            }

            // 圆心坐标 = 弦中点 + apothem × (cosβ, sinβ)
            segment.CenterX = midX + apothem * Math.Cos(beta);
            segment.CenterY = midY + apothem * Math.Sin(beta);

            // 步骤6：计算起止角度（度数）
            // 起点相对于圆心的方位角
            segment.StartAngle = Math.Atan2(y1 - segment.CenterY, x1 - segment.CenterX) * 180.0 / Math.PI;
            // 终点相对于圆心的方位角
            segment.EndAngle = Math.Atan2(y2 - segment.CenterY, x2 - segment.CenterX) * 180.0 / Math.PI;

            return segment;
        }

        /// <summary>
        /// 获取圆弧的扫掠角度范围（用于离散化）
        /// 返回值单位：度数
        /// </summary>
        public double GetSweepAngleDegrees()
        {
            if (!IsArc) return 0;
            
            double sweep = EndAngle - StartAngle;
            
            // 根据bulge符号确定方向
            if (Bulge > 0)
            {
                // 逆时针：确保sweep为正
                if (sweep < 0) sweep += 360.0;
            }
            else
            {
                // 顺时针：确保sweep为负
                if (sweep > 0) sweep -= 360.0;
            }
            
            return sweep;
        }
    }
}
```

- [ ] **Step 2: 验证文件创建成功**

Run: `ls Core/Models/CadSegment.cs`
Expected: 文件存在且非空

- [ ] **Step 3: 编译验证**

Run: `dotnet build --no-restore`
Expected: Build succeeded (CadSegment类目前未被引用，但应无编译错误)

---

## Task 2: 扩展 CadLwPolyline 模型

**Files:**
- Modify: `Core/Models/CadLwPolyline.cs`

- [ ] **Step 1: 添加 Segments 和 Bulges 属性及 BuildSegments 方法**

在 `CadLwPolyline.cs` 中添加以下代码：

在字段声明区域（第12-14行之后）添加：
```csharp
private List<CadSegment> _segments = new();
private List<double> _bulges = new();
```

在 Width 属性（第37-41行）之后添加：
```csharp
/// <summary>
/// 解析后的子段列表（由bulge计算得到）
/// 每个元素代表从Vertices[i]到Vertices[i+1]的一段（直线或圆弧）
/// 如果Bulges数据可用，在ParseLwPolyline结束后调用BuildSegments()填充此列表
/// </summary>
public List<CadSegment> Segments
{
    get => _segments;
    set => SetProperty(ref _segments, value);
}

/// <summary>
/// 原始bulge值列表（与顶点一一对应）
/// Bulges[i] 表示从 Vertices[i] 到 Vertices[i+1] 的段的凸度
/// 最后一个顶点的bulge仅对闭合多段线有意义（表示闭合段）
/// </summary>
public List<double> Bulges
{
    get => _bulges;
    set => SetProperty(ref _bulges, value);
}

/// <summary>
/// 根据顶点坐标和Bulges列表构建所有子段
/// 必须在设置Vertices和Bulges后调用
/// 将每个bulge值转换为对应的CadSegment（直线或圆弧）对象
/// </summary>
public void BuildSegments()
{
    Segments.Clear();

    if (Vertices == null || Vertices.Count < 2)
        return;

    // 构建常规段（从顶点i到顶点i+1）
    int segmentCount = Math.Min(Vertices.Count - 1, Bulges.Count > 0 ? Bulges.Count : Vertices.Count - 1);

    for (int i = 0; i < segmentCount; i++)
    {
        double bulge = (i < Bulges.Count) ? Bulges[i] : 0;
        var p1 = Vertices[i];
        var p2 = Vertices[i + 1];

        var segment = CadSegment.CreateFromBulge(p1.X, p1.Y, p2.X, p2.Y, bulge);
        Segments.Add(segment);
    }

    // 处理闭合多段线的最后一段（从最后一个顶点回到第一个顶点）
    if (IsClosed && Vertices.Count >= 2)
    {
        int lastIdx = Vertices.Count - 1;
        double closingBulge = (Bulges.Count >= Vertices.Count) ? Bulges[lastIdx] : 0;

        var closingSegment = CadSegment.CreateFromBulge(
            Vertices[lastIdx].X, Vertices[lastIdx].Y,
            Vertices[0].X, Vertices[0].Y,
            closingBulge);

        Segments.Add(closingSegment);
    }

    System.Diagnostics.Debug.WriteLine(
        $"[CadLwPolyline.BuildSegments] vertices={Vertices.Count}, " +
        $"bulges={Bulges.Count}, segments={Segments.Count}, " +
        $"arcSegments={Segments.Count(s => s.IsArc)}");
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build --no-restore`
Expected: Build succeeded

---

## Task 3: 修改 ParseLwPolyline 解析 Bulge 值

**Files:**
- Modify: `Core/Services/DxfParserService.cs:408-473`

- [ ] **Step 1: 重写 ParseLwPolyline 方法以支持组码42**

将整个 `ParseLwPolyline` 方法（第408-473行）替换为以下实现：

```csharp
/// <summary>
/// 解析LWPOLYLINE实体：收集多个顶点坐标(10/20序列)、凸度值(42)、闭合标志(70)、线宽(43)、图层名(8)
/// 注意：LWPOLYLINE的顶点不以单独的VERTEX子实体形式出现，而是直接在主实体内连续排列
/// ⭐ 新增支持：解析组码42（Bulge凸度），用于识别圆弧段
/// DXF中bulge定义：b = tan(θ/4)，θ为圆弧圆心角。b=0为直线，|b|=1为半圆
/// </summary>
private CadEntity? ParseLwPolyline(string[] lines, ref int index, List<string> warnings)
{
    var vertices = new List<PointF>();
    var bulges = new List<double>();  // ⭐ 新增：收集每个顶点的bulge值
    bool isClosed = false;
    double width = 0;
    string layerName = "0";
    
    double tempX = 0, tempY = 0;
    bool hasX = false, hasY = false;
    double currentBulge = 0;  // 当前正在累积的bulge值（遇到新顶点前可能被设置）

    while (index + 1 < lines.Length)
    {
        string groupCode = lines[index].Trim();
        if (groupCode == "0") break;

        index++;
        if (index >= lines.Length) break;
        string value = lines[index].Trim();

        try
        {
            switch (groupCode)
            {
                case "8":
                    layerName = value;
                    break;
                    
                case "42":  // ⭐ 新增：Bulge（凸度）值
                    // bulge出现在顶点坐标之前，表示该顶点到下一顶点的段类型
                    currentBulge = ParseDouble(value, warnings, "LWPOLYLINE凸度");
                    break;
                    
                case "10":  // 顶点X坐标
                    tempX = ParseDouble(value, warnings, "LWPOLYLINE顶点X");
                    hasX = true;
                    // 当收到完整的(X,Y)对时，将顶点和当前bulge加入对应列表
                    if (hasX && hasY)
                    {
                        vertices.Add(new PointF((float)tempX, (float)tempY));
                        bulges.Add(currentBulge);  // ⭐ 关联bulge与当前顶点
                        hasX = hasY = false;
                        currentBulge = 0;  // 重置：下一个顶点默认为直线（bulge=0）
                    }
                    break;
                    
                case "20":  // 顶点Y坐标
                    tempY = ParseDouble(value, warnings, "LWPOLYLINE顶点Y");
                    hasY = true;
                    if (hasX && hasY)
                    {
                        vertices.Add(new PointF((float)tempX, (float)tempY));
                        bulges.Add(currentBulge);  // ⭐ 关联bulge与当前顶点
                        hasX = hasY = false;
                        currentBulge = 0;  // 重置默认值
                    }
                    break;
                    
                case "70":  // 多段线标志位（bit0=1表示闭合）
                    int flags = (int)ParseDouble(value, warnings, "LWPOLYLINE标志");
                    isClosed = (flags & 1) != 0;
                    break;
                    
                case "43":  // 默认线宽
                    width = ParseDouble(value, warnings, "LWPOLYLINE线宽");
                    break;
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"LWPOLYLINE组码{groupCode}解析失败: {ex.Message}");
        }

        index++;
    }
    index--;

    var polyline = new CadLwPolyline(vertices, isClosed, width);
    polyline.LayerName = layerName;
    
    // ⭐ 设置bulge列表并构建子段
    polyline.Bulges = bulges;
    polyline.BuildSegments();

    System.Diagnostics.Debug.WriteLine(
        $"[DxfParser] LWPOLYLINE: layer={layerName}, vertices={vertices.Count}, " +
        $"bulges={bulges.Count}, segments={polyline.Segments.Count}, " +
        $"arcSegments={polyline.Segments.Count(s => s.IsArc)}, closed={isClosed}");
    
    return polyline;
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build --no-restore`
Expected: Build succeeded

---

## Task 4: 修改 DiscretizePolyline 支持混合段

**Files:**
- Modify: `Core/Services/DxfParserService.cs:1185-1228`

- [ ] **Step 1: 重写 DiscretizePolyline 方法**

将整个 `DiscretizePolyline` 方法（第1185-1228行）替换为以下实现：

```csharp
/// <summary>
/// 轻量多段线离散化：优先按Segments列表中的段类型分别处理
/// 直线段使用线性插值，圆弧段使用角度采样
/// 如果没有Segments数据（旧版本兼容），回退到纯直线模式
/// </summary>
private List<CadPoint> DiscretizePolyline(CadLwPolyline polyline, double pitchMM)
{
    var points = new List<CadPoint>();

    // ⭐ 优先使用Segments列表进行精确离散化（支持混合直线+圆弧段）
    if (polyline.Segments != null && polyline.Segments.Count > 0)
    {
        foreach (var segment in polyline.Segments)
        {
            if (segment.IsArc)
            {
                // 圆弧段：创建临时CadArc对象，复用现有的DiscretizeArc逻辑
                var arc = new CadArc(
                    segment.CenterX, segment.CenterY, segment.Radius,
                    segment.StartAngle, segment.EndAngle);
                points.AddRange(Discretize(arc, pitchMM));
            }
            else
            {
                // 直线段：创建临时CadLine对象，复用DiscretizeLine逻辑
                var line = new CadLine(
                    segment.StartX, segment.StartY,
                    segment.EndX, segment.EndY);
                points.AddRange(Discretize(line, pitchMM));
            }
        }
        return points;
    }

    // ===== 回退逻辑：纯直线多段线（兼容旧数据或无bulge的情况）=====
    var vertices = polyline.Vertices;
    if (vertices == null || vertices.Count < 2) return points;

    int segmentCount = vertices.Count - 1;
    if (polyline.IsClosed)
        segmentCount = vertices.Count;

    for (int seg = 0; seg < segmentCount; seg++)
    {
        int fromIdx = seg % vertices.Count;
        int toIdx = (seg + 1) % vertices.Count;

        double x0 = vertices[fromIdx].X, y0 = vertices[fromIdx].Y;
        double x1 = vertices[toIdx].X, y1 = vertices[toIdx].Y;

        double dx = x1 - x0;
        double dy = y1 - y0;
        double segLength = Math.Sqrt(dx * dx + dy * dy);

        if (segLength < 1e-9)
        {
            if (points.Count == 0 || seg > 0)
                points.Add(new CadPoint(x0, y0, 0));
            continue;
        }

        int segPoints = (int)Math.Ceiling(segLength / pitchMM) + 1;
        int startI = (seg == 0) ? 0 : 1;

        for (int i = startI; i < segPoints; i++)
        {
            double t = (segPoints > 1) ? (double)i / (segPoints - 1) : 0;
            points.Add(new CadPoint(x0 + dx * t, y0 + dy * t, 0));
        }
    }
    return points;
}
```

- [ ] **Step 2: 同步修改 DiscretizePolylineByCount 方法（如果存在）**

检查是否还有 `DiscretizePolylineByCount` 方法需要同步更新。如果有，添加类似的Segments支持逻辑。

- [ ] **Step 3: 编译验证**

Run: `dotnet build --no-restore`
Expected: Build succeeded

---

## Task 5: 修改 ToHObject(LwPolyline) 渲染逻辑

**Files:**
- Modify: `Core/Models/CadEntityHalconExtensions.cs:209-246`

- [ ] **Step 1: 重写 ToHObject(CadLwPolyline) 扩展方法**

将整个 `ToHObject(this CadLwPolyline)` 方法（第209-246行）替换为以下实现：

```csharp
/// <summary>
/// 将轻量多段线图元转换为Halcon XLD轮廓对象
/// ⭐ 支持混合段渲染：直线段使用GenContourPolygonXld两点连线，
///   圆弧段通过采样后生成平滑轮廓
/// 如果有Segments数据则按段类型分别渲染，否则回退到纯顶点连接模式
/// </summary>
/// <param name="polyline">轻量多段线图元</param>
/// <returns>Halcon XLD轮廓对象；顶点列表为空时返回空HObject</returns>
public static HObject ToHObject(this CadLwPolyline polyline)
{
    if (polyline == null)
        throw new ArgumentNullException(nameof(polyline));

    if (polyline.Vertices == null || polyline.Vertices.Count == 0)
        return new HObject();

    // ⭐ 优先使用Segments进行精确渲染（支持混合直线+圆弧段）
    if (polyline.Segments != null && polyline.Segments.Count > 0)
    {
        var allContours = new List<HObject>();
        
        foreach (var segment in polyline.Segments)
        {
            HObject contour;
            
            if (segment.IsArc)
            {
                // 圆弧段：创建临时CadArc对象，复用现有的Arc.ToHObject()逻辑
                // 该方法内部会进行高质量的角度采样，保证圆弧平滑
                var arc = new CadArc(
                    segment.CenterX, segment.CenterY, segment.Radius,
                    segment.StartAngle, segment.EndAngle);
                contour = arc.ToHObject();
            }
            else
            {
                // 直线段：使用GenContourPolygonXld将起点终点连成线段
                double[] rows = { segment.StartY, segment.EndY };
                double[] cols = { segment.StartX, segment.EndX };
                HOperatorSet.GenContourPolygonXld(out contour, rows, cols);
            }
            
            if (contour != null && contour.IsInitialized())
                allContours.Add(contour);
        }
        
        // 合并所有子段为一个HObject
        if (allContours.Count == 0)
            return new HObject();
        if (allContours.Count == 1)
            return allContours[0];
        
        // 使用ConcatObj依次合并所有轮廓
        HOperatorSet.ConcatObj(allContours[0], allContours[1], out HObject result);
        for (int i = 2; i < allContours.Count; i++)
        {
            HOperatorSet.ConcatObj(result, allContours[i], out result);
        }
        return result;
    }

    // ===== 回退逻辑：原有纯直线多段线渲染（兼容旧数据）=====
    int count = polyline.Vertices.Count;
    bool needClose = polyline.IsClosed && count >= 2;

    int totalPoints = needClose &&
        (Math.Abs(polyline.Vertices[0].X - polyline.Vertices[count - 1].X) > 1e-6 ||
         Math.Abs(polyline.Vertices[0].Y - polyline.Vertices[count - 1].Y) > 1e-6)
        ? count + 1 : count;

    double[] rows = new double[totalPoints];
    double[] cols = new double[totalPoints];

    for (int i = 0; i < count; i++)
    {
        rows[i] = polyline.Vertices[i].Y;  // Halcon坐标系：Row=Y
        cols[i] = polyline.Vertices[i].X;  // Halcon坐标系：Col=X
    }

    if (totalPoints > count)
    {
        rows[count] = polyline.Vertices[0].Y;
        cols[count] = polyline.Vertices[0].X;
    }

    HOperatorSet.GenContourPolygonXld(out HObject fallbackContour, rows, cols);
    return fallbackContour;
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build --no-restore`
Expected: Build succeeded

---

## Task 6: 集成测试与回归验证

**Files:**
- No file creation/modification (testing only)

- [ ] **Step 1: 使用用户提供的DXF文件进行功能验证**

测试步骤：
1. 启动应用程序
2. 导入之前出现"多余直线"问题的DXF文件
3. 对比原始CAD截图，确认：
   - ✅ 圆弧处不再有多余直线
   - ✅ 圆弧形状与原始CAD一致（曲率、方向、大小）
   - ✅ 直线段保持不变
   - ✅ 整体轮廓连续性正确

- [ ] **Step 2: 验证运动控制集成（如适用）**

如果该DXF文件用于运动轨迹生成：
1. 选择导入的轮廓
2. 执行离散化操作（生成点位）
3. 检查点位分布：
   - ✅ 圆弧段点位沿圆弧均匀分布（而非直线插值）
   - ✅ 点位间距符合pitchMM要求
   - ✅ 无异常跳跃或重叠点位

- [ ] **Step 3: 边界条件快速检查**

使用Debug输出确认：
```
[DxfParser] LWPOLYLINE: vertices=N, bulges=M, segments=S, arcSegments=A
```
- N ≥ 2 （至少2个顶点）
- M 应该接近N（每个顶点对应一个bulge）
- S ≥ 1 （至少1个子段）
- A 可以是0（全直线）或≥1（包含圆弧）

- [ ] **Step 4: 性能基准对比（可选）**

记录修复前后同一文件的导入时间：
- 解析时间应在可接受范围内（<100ms对于普通工程图）
- 内存占用无明显增长

---

## 自检清单

### Spec覆盖率检查
- ✅ Task 1: CadSegment模型 - 覆盖设计文档§4.1
- ✅ Task 2: CadLwPolyline扩展 - 覆盖设计文档§4.2
- ✅ Task 3: Bulge解析 - 覆盖设计文档§4.3
- ✅ Task 4: 混合离散化 - 覆盖设计文档§4.4
- ✅ Task 5: 混合渲染 - 覆盖设计文档§4.5
- ✅ Task 6: 测试验证 - 覆盖设计文档§5

### 占位符扫描
- ❌ 无 TBD / TODO / 待实现 标记
- ❌ 无 "类似Task N" 引用
- ✅ 所有代码块包含完整实现

### 类型一致性
- ✅ CadSegment.CreateFromBulge() 在 Task 1 定义，Task 2/3/4/5 正确调用
- ✅ CadLwPolyline.Segments/Bulges/BuildSegments() 在 Task 2 定义，Task 3/4/5 正确使用
- ✅ 方法签名一致：参数类型、返回类型、命名规范

---

## 回滚策略

如果修复后出现新问题，可通过以下方式快速回滚：

**方式A（推荐）：功能开关**
在 `CadLwPolyline.BuildSegments()` 开头添加：
```csharp
if (!EnableBulgeSupport) return;  // 全局静态开关
```

**方式B：注释掉关键调用**
在 `DxfParserService.ParseLwPolyline()` 中注释掉：
```csharp
// polyline.Bulges = bulges;
// polyline.BuildSegments();
```

两种方式都不需要删除新增代码，便于后续重新启用。

---

**预计总工作量：** ~2小时（不含集成调试时间）
**风险等级：** 低（向后兼容，保留fallback逻辑）
