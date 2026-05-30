using Core.Models;
using System.Globalization;
using System.IO;

namespace Core.Services
{
    /// <summary>
    /// DXF文件解析服务实现，提供DXF文本解析、图元构建和离散化功能
    /// 采用逐行组码/值对模式解析，不依赖第三方DXF库
    /// </summary>
    public class DxfParserService : IDxfParserService
    {
        // 浮点数解析使用的区域性设置（确保小数点为"."而非","）
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        // ======================== 公共接口方法 ========================

        /// <summary>
        /// 解析DXF文件，按图层分组返回所有图元
        /// 定位ENTITIES段，逐行读取组码/值对，根据实体类型构建对应的CadEntity对象
        /// </summary>
        public DxfParseResult Parse(string filePath)
        {
            var layers = new Dictionary<string, List<CadEntity>>();
            var warnings = new List<string>();
            var allEntities = new List<CadEntity>();

            try
            {
                if (!File.Exists(filePath))
                {
                    warnings.Add($"文件不存在: {filePath}");
                    return new DxfParseResult(layers, new BoundingBox(), warnings);
                }

                var lines = File.ReadAllLines(filePath);
                ParseEntitiesSection(lines, layers, allEntities, warnings);
            }
            catch (Exception ex)
            {
                warnings.Add($"文件读取异常: {ex.Message}");
            }

            // 遍历所有图元计算整体Extents（并集包围盒）
            var extents = CalculateExtents(allEntities);

            return new DxfParseResult(layers, extents, warnings);
        }

        /// <summary>
        /// 将单个CAD图元离散化为等间距点序列
        /// 根据图元类型分派到对应的离散化算法
        /// </summary>
        public List<CadPoint> Discretize(CadEntity entity, double pitchMM)
        {
            if (entity == null || pitchMM <= 0)
                return new List<CadPoint>();

            return entity switch
            {
                CadLine line => DiscretizeLine(line, pitchMM),
                CadArc arc => DiscretizeArc(arc, pitchMM),
                CadCircle circle => DiscretizeCircle(circle, pitchMM),
                CadLwPolyline polyline => DiscretizePolyline(polyline, pitchMM),
                CadEllipse ellipse => DiscretizeEllipse(ellipse, pitchMM),
                CadSpline spline => DiscretizeSpline(spline, pitchMM),
                _ => new List<CadPoint>()
            };
        }

        /// <summary>
        /// 批量离散化多个CAD图元，按顺序合并所有离散点
        /// </summary>
        public List<CadPoint> DiscretizeAll(IEnumerable<CadEntity> entities, double pitchMM)
        {
            var result = new List<CadPoint>();
            if (entities == null || pitchMM <= 0)
                return result;

            foreach (var entity in entities)
            {
                result.AddRange(Discretize(entity, pitchMM));
            }
            return result;
        }

        /// <summary>
        /// 按指定点数对CAD图元进行离散化采样（等间距均匀采样）
        /// </summary>
        public List<CadPoint> DiscretizeByCount(CadEntity entity, int pointCount)
        {
            if (entity == null || pointCount < 2)
                return new List<CadPoint>();

            return entity switch
            {
                CadLine line => DiscretizeLineByCount(line, pointCount),
                CadArc arc => DiscretizeArcByCount(arc, pointCount),
                CadCircle circle => DiscretizeCircleByCount(circle, pointCount),
                CadLwPolyline polyline => DiscretizePolylineByCount(polyline, pointCount),
                CadEllipse ellipse => DiscretizeEllipseByCount(ellipse, pointCount),
                CadSpline spline => DiscretizeSplineByCount(spline, pointCount),
                _ => new List<CadPoint>()
            };
        }

        // ======================== DXF 解析核心逻辑 ========================

        /// <summary>
        /// 从全部文本行中定位ENTITIES段并解析其中的实体
        /// 使用状态机模式：追踪是否在ENTITIES段内、当前实体类型、当前图层名
        /// </summary>
        private void ParseEntitiesSection(string[] lines,
            Dictionary<string, List<CadEntity>> layers,
            List<CadEntity> allEntities,
            List<string> warnings)
        {
            bool inEntities = false;
            int i = 0;

            while (i < lines.Length)
            {
                string trimmed = lines[i].Trim();

                // 进入ENTITIES段
                if (trimmed == "ENTITIES")
                {
                    inEntities = true;
                    i++;
                    continue;
                }

                // 离开ENTITIES段（遇到ENDSEC或其他SECTION）
                if (trimmed == "ENDSEC" || trimmed == "SECTION")
                {
                    if (inEntities && trimmed == "ENDSEC")
                        inEntities = false;
                    i++;
                    continue;
                }

                // 只在ENTITIES段内进行实体解析
                if (!inEntities)
                {
                    i++;
                    continue;
                }

                // 组码"0"表示接下来的一行是实体类型名称
                if (trimmed == "0")
                {
                    i++; // 移动到实体类型名行
                    if (i >= lines.Length) break;

                    string entityType = lines[i].Trim();
                    i++; // 跳过实体类型名，移动到第一个组码行

                    CadEntity? entity = null;

                    try
                    {
                        switch (entityType)
                        {
                            case "LINE":
                                entity = ParseLine(lines, ref i, warnings);
                                break;
                            case "ARC":
                                entity = ParseArc(lines, ref i, warnings);
                                break;
                            case "CIRCLE":
                                entity = ParseCircle(lines, ref i, warnings);
                                break;
                            case "LWPOLYLINE":
                                entity = ParseLwPolyline(lines, ref i, warnings);
                                break;
                            case "POLYLINE":
                                // ✅ 新增支持：老式POLYLINE格式（含VERTEX子实体）
                                entity = ParsePolyline(lines, ref i, warnings);
                                break;
                            case "ELLIPSE":
                                entity = ParseEllipse(lines, ref i, warnings);
                                break;
                            case "SPLINE":
                                // ✅ 新增支持：NURBS样条曲线（AutoCAD 2018格式）
                                entity = ParseSpline(lines, ref i, warnings);
                                break;
                            default:
                                if (entityType != "ENDSEC" && entityType != "SECTION"
                                    && !string.IsNullOrEmpty(entityType))
                                {
                                    warnings.Add($"跳过不支持的实体类型: {entityType}");
                                }
                                // 跳过未识别实体的所有组码/值对，直到下一个组码"0"
                                while (i + 1 < lines.Length)
                                {
                                    if (lines[i].Trim() == "0") break;
                                    i++;
                                }
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"解析{entityType}实体时发生异常: {ex.Message}");
                    }

                    if (entity != null)
                    {
                        string layerName = entity.LayerName ?? "0";
                        if (!layers.ContainsKey(layerName))
                            layers[layerName] = new List<CadEntity>();
                        layers[layerName].Add(entity);
                        allEntities.Add(entity);
                    }

                    // ParseXxx 方法返回时 index 指向组码"0"的前一行，
                    // i++ 让 i 指向组码"0"行，外层 while 下一轮即可识别新实体
                    i++;
                    continue;
                }

                i++;
            }
        }

        /// <summary>
        /// 解析LINE实体：读取起点(10/20/30)和终点(11/21/31)坐标，以及图层名(8)
        /// </summary>
        private CadEntity? ParseLine(string[] lines, ref int index, List<string> warnings)
        {
            double startX = 0, startY = 0, startZ = 0;
            double endX = 0, endY = 0, endZ = 0;
            string layerName = "0";

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
                        case "10":
                            startX = ParseDouble(value, warnings, "LINE起点X");
                            break;
                        case "20":
                            startY = ParseDouble(value, warnings, "LINE起点Y");
                            break;
                        case "30":
                            startZ = ParseDouble(value, warnings, "LINE起点Z");
                            break;
                        case "11":
                            endX = ParseDouble(value, warnings, "LINE终点X");
                            break;
                        case "21":
                            endY = ParseDouble(value, warnings, "LINE终点Y");
                            break;
                        case "31":
                            endZ = ParseDouble(value, warnings, "LINE终点Z");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"LINE组码{groupCode}解析失败: {ex.Message}");
                }

                index++;
            }
            index--;

            var line = new CadLine(startX, startY, endX, endY, startZ, endZ);
            line.LayerName = layerName;
            return line;
        }

        /// <summary>
        /// 解析ARC实体：读取圆心(10/20/30)、半径(40)、起止角(50/51)、图层名(8)
        /// </summary>
        private CadEntity? ParseArc(string[] lines, ref int index, List<string> warnings)
        {
            double centerX = 0, centerY = 0, centerZ = 0;
            double radius = 0;
            double startAngle = 0, endAngle = 0;
            string layerName = "0";

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
                        case "10":
                            centerX = ParseDouble(value, warnings, "ARC圆心X");
                            break;
                        case "20":
                            centerY = ParseDouble(value, warnings, "ARC圆心Y");
                            break;
                        case "30":
                            centerZ = ParseDouble(value, warnings, "ARC圆心Z");
                            break;
                        case "40":
                            radius = ParseDouble(value, warnings, "ARC半径");
                            break;
                        case "50":
                            startAngle = ParseDouble(value, warnings, "ARC起始角");
                            break;
                        case "51":
                            endAngle = ParseDouble(value, warnings, "ARC终止角");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"ARC组码{groupCode}解析失败: {ex.Message}");
                }

                index++;
            }
            index--;

            var arc = new CadArc(centerX, centerY, radius, startAngle, endAngle, centerZ);
            arc.LayerName = layerName;
            return arc;
        }

        /// <summary>
        /// 解析CIRCLE实体：读取圆心(10/20/30)、半径(40)、图层名(8)
        /// </summary>
        private CadEntity? ParseCircle(string[] lines, ref int index, List<string> warnings)
        {
            double centerX = 0, centerY = 0, centerZ = 0;
            double radius = 0;
            string layerName = "0";

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
                        case "10":
                            centerX = ParseDouble(value, warnings, "CIRCLE圆心X");
                            break;
                        case "20":
                            centerY = ParseDouble(value, warnings, "CIRCLE圆心Y");
                            break;
                        case "30":
                            centerZ = ParseDouble(value, warnings, "CIRCLE圆心Z");
                            break;
                        case "40":
                            radius = ParseDouble(value, warnings, "CIRCLE半径");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"CIRCLE组码{groupCode}解析失败: {ex.Message}");
                }

                index++;
            }
            index--;

            var circle = new CadCircle(centerX, centerY, radius, centerZ);
            circle.LayerName = layerName;
            return circle;
        }

        /// <summary>
        /// 解析LWPOLYLINE实体：收集多个顶点坐标(10/20序列)、凸度值(42)、闭合标志(70)、线宽(43)、图层名(8)
        /// 注意：LWPOLYLINE的顶点不以单独的VERTEX子实体形式出现，而是直接在主实体内连续排列
        /// ⭐ 新增支持：解析组码42（Bulge凸度），用于识别圆弧段
        /// DXF中bulge定义：b = tan(θ/4)，θ为圆弧圆心角。b=0为直线，|b|=1为半圆
        /// </summary>
        private CadEntity? ParseLwPolyline(string[] lines, ref int index, List<string> warnings)
        {
            var vertices = new List<PointF>();
            var bulges = new List<double>();  // bulges[i] 表示从vertices[i]到vertices[i+1]的段
            bool isClosed = false;
            double width = 0;
            string layerName = "0";
            
            double tempX = 0, tempY = 0;
            bool hasX = false, hasY = false;

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
                            
                        case "42":  // ⭐ Bulge（凸度）值 - 出现在当前顶点坐标之后
                            {
                                // DXF规范：bulge出现在(10,20)之后，表示从该顶点到下一顶点的段类型
                                // 因此遇到42时，应该更新最后已添加顶点的bulge值
                                double bulgeVal = ParseDouble(value, warnings, "LWPOLYLINE凸度");
                                
                                if (bulges.Count > 0 && bulges.Count <= vertices.Count)
                                {
                                    // 更新最后一个顶点的bulge（该顶点刚在之前的10/20中被添加）
                                    bulges[bulges.Count - 1] = bulgeVal;
                                }
                                // 如果bulges列表还没元素（理论上不应该发生），忽略此bulge
                            }
                            break;
                            
                        case "10":  // 顶点X坐标
                            tempX = ParseDouble(value, warnings, "LWPOLYLINE顶点X");
                            hasX = true;
                            if (hasX && hasY)
                            {
                                vertices.Add(new PointF((float)tempX, (float)tempY));
                                bulges.Add(0);  // 先用0占位，稍后遇到42时会更新为实际值
                                hasX = hasY = false;
                            }
                            break;
                            
                        case "20":  // 顶点Y坐标
                            tempY = ParseDouble(value, warnings, "LWPOLYLINE顶点Y");
                            hasY = true;
                            if (hasX && hasY)
                            {
                                vertices.Add(new PointF((float)tempX, (float)tempY));
                                bulges.Add(0);  // 先用0占位，稍后遇到42时会更新为实际值
                                hasX = hasY = false;
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
            
            polyline.Bulges = bulges;
            polyline.BuildSegments();

            return polyline;
        }

        /// <summary>
        /// 解析老式POLYLINE实体：读取POLYLINE头部的属性，然后遍历后续的VERTEX子实体收集顶点坐标
        /// DXF结构：POLYLINE → [VERTEX × N] → SEQEND
        /// 与LWPOLYLINE不同，老式POLYLINE的顶点是独立的子实体，需要跨实体边界收集
        /// </summary>
        private CadEntity? ParsePolyline(string[] lines, ref int index, List<string> warnings)
        {
            var vertices = new List<PointF>();
            bool isClosed = false;
            double width = 0;
            string layerName = "0";

            // 阶段1：解析POLYLINE头部属性（图层、标志、线宽等）
            while (index + 1 < lines.Length)
            {
                string groupCode = lines[index].Trim();
                if (groupCode == "0") break; // 遇到下一个实体（应该是VERTEX）

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
                        case "70":  // 多段线标志位（bit0=1表示闭合）
                            int flags = (int)ParseDouble(value, warnings, "POLYLINE标志");
                            isClosed = (flags & 1) != 0;
                            break;
                        case "43":  // 默认线宽
                            width = ParseDouble(value, warnings, "POLYLINE线宽");
                            break;
                        default:
                            // 跳过其他组码（如66、10/20/30等头部属性）
                            break;
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"POLYLINE头部组码{groupCode}解析失败: {ex.Message}");
                }

                index++;
            }

            // 阶段2：遍历VERTEX子实体，收集所有顶点坐标
            while (index + 1 < lines.Length)
            {
                string entityType = lines[index].Trim();
                
                if (entityType == "SEQEND")
                {
                    // POLYLINE结束标记，跳出循环
                    index++; // 跳过SEQEND
                    break;
                }
                
                if (entityType != "VERTEX")
                {
                    // 意外的实体类型（可能是其他图元），停止解析VERTEX
                    warnings.Add($"POLYLINE解析遇到非VERTEX实体: {entityType}，停止收集顶点");
                    break;
                }

                // 解析单个VERTEX实体
                double vertexX = 0, vertexY = 0, vertexZ = 0;
                index++; // 跳过"0\nVERTEX"，指向VERTEX的第一个组码

                while (index + 1 < lines.Length)
                {
                    string vertexGroupCode = lines[index].Trim();
                    if (vertexGroupCode == "0") break; // 下一个实体开始

                    index++;
                    if (index >= lines.Length) break;
                    string vertexValue = lines[index].Trim();

                    try
                    {
                        switch (vertexGroupCode)
                        {
                            case "10":  // 顶点X坐标
                                vertexX = ParseDouble(vertexValue, warnings, "VERTEX X");
                                break;
                            case "20":  // 顶点Y坐标
                                vertexY = ParseDouble(vertexValue, warnings, "VERTEX Y");
                                break;
                            case "30":  // 顶点Z坐标（可选）
                                vertexZ = ParseDouble(vertexValue, warnings, "VERTEX Z");
                                break;
                            // 忽略VERTEX的其他组码（70-标志、62-颜色等）
                        }
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"VERTEX组码{vertexGroupCode}解析失败: {ex.Message}");
                    }

                    index++;
                }

                // 将收集到的顶点加入列表
                vertices.Add(new PointF((float)vertexX, (float)vertexY));
            }

            index--; // 回退一个位置，让主循环正确处理

            // 创建CadLwPolyline对象（复用现有数据结构）
            if (vertices.Count == 0)
            {
                warnings.Add("POLYLINE未包含任何顶点");
                return null;
            }

            var polyline = new CadLwPolyline(vertices, isClosed, width);
            polyline.LayerName = layerName;
            
            return polyline;
        }

        /// <summary>
        /// 解析ELLIPSE实体：读取中心(10/20/30)、长轴端点(11/21/31)、长短轴比(40)、起止角(50/51)、图层名(8)
        /// 长轴长度由长轴端点向量模计算得出，旋转角度由该向量与X轴夹角确定
        /// </summary>
        private CadEntity? ParseEllipse(string[] lines, ref int index, List<string> warnings)
        {
            double centerX = 0, centerY = 0, centerZ = 0;
            // 长轴端点相对于中心的偏移量
            double majorEndPointX = 0, majorEndPointY = 0, majorEndPointZ = 0;
            double ratio = 0;          // 长短轴比例（短轴/长轴）
            double startAngle = 0, endAngle = 0;
            string layerName = "0";

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
                        case "10":
                            centerX = ParseDouble(value, warnings, "ELLIPSE中心X");
                            break;
                        case "20":
                            centerY = ParseDouble(value, warnings, "ELLIPSE中心Y");
                            break;
                        case "30":
                            centerZ = ParseDouble(value, warnings, "ELLIPSE中心Z");
                            break;
                        case "11":  // 长轴端点X（相对于中心点的偏移）
                            majorEndPointX = ParseDouble(value, warnings, "ELLIPSE长轴端点X");
                            break;
                        case "21":  // 长轴端点Y
                            majorEndPointY = ParseDouble(value, warnings, "ELLIPSE长轴端点Y");
                            break;
                        case "31":  // 长轴端点Z
                            majorEndPointZ = ParseDouble(value, warnings, "ELLIPSE长轴端点Z");
                            break;
                        case "40":  // 短轴与长轴的比值（Minor/Major）
                            ratio = ParseDouble(value, warnings, "ELLIPSE长短轴比");
                            break;
                        case "50":
                            startAngle = ParseDouble(value, warnings, "ELLIPSE起始角");
                            break;
                        case "51":
                            endAngle = ParseDouble(value, warnings, "ELLIPSE终止角");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"ELLIPSE组码{groupCode}解析失败: {ex.Message}");
                }

                index++;
            }
            index--;

            // 从长轴端点向量计算长半轴长度和旋转角度
            double majorLength = Math.Sqrt(majorEndPointX * majorEndPointX + majorEndPointY * majorEndPointY);
            double minorLength = majorLength * ratio;
            // 旋转角度：长轴方向与X轴正方向的夹角（弧度→度数）
            double rotationRad = Math.Atan2(majorEndPointY, majorEndPointX);
            double rotationDeg = rotationRad * 180.0 / Math.PI;

            var ellipse = new CadEllipse(centerX, centerY, majorLength, minorLength,
                rotationDeg, startAngle, endAngle, centerZ);
            ellipse.LayerName = layerName;
            return ellipse;
        }

        /// <summary>
        /// 解析SPLINE实体（NURBS样条曲线）：读取控制点、节点向量、权重、度数等参数
        /// DXF结构：
        ///   - 70: 标志位（bit0=闭合, bit1=周期性, bit2=有理）
        ///   - 71: 度数（Degree，通常为3）
        ///   - 72: 控制点数量
        ///   - 73: 节点数量
        ///   - 40: 节点向量值（重复出现）
        ///   - 10/20/30: 控制点坐标（重复出现）
        ///   - 41: 权重值（可选，仅IsRational时出现）
        ///   - 210/220/230: 法向量
        /// </summary>
        private CadEntity? ParseSpline(string[] lines, ref int index, List<string> warnings)
        {
            int degree = 3;
            int flags = 0;
            string layerName = "0";
            var controlPoints = new List<PointF>();
            var knots = new List<double>();
            var weights = new List<double>();
            double normalX = 0, normalY = 0, normalZ = 1;
            double knotTolerance = 1e-7;
            double fitTolerance = 1e-10;

            // 临时变量用于累积当前正在读取的控制点坐标
            double tempX = 0, tempY = 0, tempZ = 0;
            bool hasX = false, hasY = false;

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
                        case "70":  // 标志位
                            flags = (int)ParseDouble(value, warnings, "SPLINE标志");
                            break;
                        case "71":  // 度数
                            degree = (int)ParseDouble(value, warnings, "SPLINE度数");
                            if (degree < 1) degree = 3; // 默认三次样条
                            break;
                        case "72":  // 控制点数量（用于预分配内存，实际以读取到的为准）
                            // 可选：预先分配控制点列表容量
                            break;
                        case "73":  // 节点数量
                            // 可选：预先分配节点向量容量
                            break;
                        case "40":  // 节点向量值
                            knots.Add(ParseDouble(value, warnings, "SPLINE节点"));
                            break;
                        case "41":  // 权重值
                            weights.Add(ParseDouble(value, warnings, "SPLINE权重"));
                            break;
                        case "42":  // 节点公差
                            knotTolerance = ParseDouble(value, warnings, "SPLINE节点公差");
                            break;
                        case "43":  // 拟合公差
                            fitTolerance = ParseDouble(value, warnings, "SPLINE拟合公差");
                            break;
                        case "10":  // 控制点X坐标
                            tempX = ParseDouble(value, warnings, "SPLINE控制点X");
                            hasX = true;
                            if (hasX && hasY)
                            {
                                controlPoints.Add(new PointF((float)tempX, (float)tempY));
                                hasX = hasY = false;
                            }
                            break;
                        case "20":  // 控制点Y坐标
                            tempY = ParseDouble(value, warnings, "SPLINE控制点Y");
                            hasY = true;
                            if (hasX && hasY)
                            {
                                controlPoints.Add(new PointF((float)tempX, (float)tempY));
                                hasX = hasY = false;
                            }
                            break;
                        case "30":  // 控制点Z坐标（可选，暂不使用）
                            tempZ = ParseDouble(value, warnings, "SPLINE控制点Z");
                            break;
                        case "210":  // 法向量X
                            normalX = ParseDouble(value, warnings, "SPLINE法向量X");
                            break;
                        case "220":  // 法向量Y
                            normalY = ParseDouble(value, warnings, "SPLINE法向量Y");
                            break;
                        case "230":  // 法向量Z
                            normalZ = ParseDouble(value, warnings, "SPLINE法向量Z");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"SPLINE组码{groupCode}解析失败: {ex.Message}");
                }

                index++;
            }
            index--;

            // 验证数据完整性
            if (controlPoints.Count < 2)
            {
                warnings.Add("SPLINE控制点不足2个，跳过该实体");
                return null;
            }

            if (knots.Count == 0)
            {
                // 如果没有节点向量，生成均匀节点向量
                knots = GenerateUniformKnots(degree, controlPoints.Count);
                warnings.Add("SPLINE缺少节点向量，已自动生成均匀节点");
            }

            // 解析标志位
            bool isClosed = (flags & 1) != 0;
            bool isPeriodic = (flags & 2) != 0;
            bool isRational = (flags & 4) != 0 || weights.Count > 0;

            var spline = new CadSpline(degree, controlPoints, knots,
                weights.Count > 0 ? weights : null, isClosed, isPeriodic,
                normalX, normalY, normalZ);
            spline.LayerName = layerName;
            spline.KnotTolerance = knotTolerance;
            spline.FitTolerance = fitTolerance;

            return spline;
        }

        /// <summary>
        /// 生成均匀节点向量（当DXF文件中缺失节点数据时的备用方案）
        /// 对于非周期样条：长度 = degree + numControlPoints + 1，首尾重复degree+1次
        /// 对于周期样条：长度 = degree + numControlPoints
        /// </summary>
        private static List<double> GenerateUniformKnots(int degree, int numControlPoints)
        {
            var knots = new List<double>();

            if (numControlPoints <= degree)
            {
                // 退化情况：返回简单线性分布
                for (int i = 0; i <= numControlPoints; i++)
                    knots.Add((double)i / numControlPoints);
                return knots;
            }

            // 标准非周期B样条节点向量
            int n = numControlPoints - 1; // 最后一个控制点索引
            int m = n + degree + 1;       // 最后一个节点索引

            for (int i = 0; i <= m; i++)
            {
                if (i <= degree)
                    knots.Add(0.0);  // 前端重复
                else if (i > n)
                    knots.Add(1.0);  // 后端重复
                else
                    knots.Add((double)(i - degree) / (n - degree + 1));  // 中间均匀分布
            }

            return knots;
        }

        // ======================== 离散化方法 ========================

        /// <summary>
        /// 直线段离散化：在起点和终点之间按pitchMM间距做线性插值
        /// 点数 = ceil(线段长度 / 间距) + 1，确保覆盖整条线段且不超过最大间距
        /// </summary>
        private List<CadPoint> DiscretizeLine(CadLine line, double pitchMM)
        {
            var points = new List<CadPoint>();
            double dx = line.EndX - line.StartX;
            double dy = line.EndY - line.StartY;
            double dz = line.EndZ - line.StartZ;
            double length = Math.Sqrt(dx * dx + dy * dy);

            // 线段长度为零或极短时直接返回起点点
            if (length < 1e-9)
            {
                points.Add(new CadPoint(line.StartX, line.StartY, line.StartZ));
                return points;
            }

            // 计算插值点数量（向上取整确保间距不超过pitchMM）
            int count = (int)Math.Ceiling(length / pitchMM) + 1;
            for (int i = 0; i < count; i++)
            {
                double t = (count > 1) ? (double)i / (count - 1) : 0;
                double x = line.StartX + dx * t;
                double y = line.StartY + dy * t;
                double z = line.StartZ + dz * t;
                points.Add(new CadPoint(x, y, z));
            }
            return points;
        }

        /// <summary>
        /// 样条曲线离散化：使用de Boor算法在有效参数域内按pitchMM间距采样
        /// 采样策略：
        ///   1. 计算参数域范围 [tStart, tEnd]
        ///   2. 估算样条曲线总长度（通过控制点多边形近似）
        ///   3. 按间距计算采样点数，在参数域内均匀分布
        ///   4. 使用de Boor算法计算每个参数值对应的曲线点坐标
        /// </summary>
        private List<CadPoint> DiscretizeSpline(CadSpline spline, double pitchMM)
        {
            var points = new List<CadPoint>();

            if (spline == null || spline.ControlPoints == null || spline.ControlPoints.Count < 2
                || spline.Knots == null || spline.Knots.Count == 0)
                return points;

            // 获取有效参数域
            var (tStart, tEnd) = spline.GetParameterRange();
            if (Math.Abs(tEnd - tStart) < 1e-10)
                return points;

            // 估算样条曲线长度（使用控制点多边形的总长度作为近似）
            double estimatedLength = EstimateSplineLength(spline);
            if (estimatedLength < 1e-9)
                estimatedLength = pitchMM; // 防止除零

            // 计算采样点数量（基于估算长度和间距）
            int count = (int)Math.Ceiling(estimatedLength / pitchMM) + 1;
            count = Math.Max(count, spline.Degree * 2 + 1); // 至少保证基本形状

            // 在参数域内均匀采样并计算曲线点
            for (int i = 0; i < count; i++)
            {
                double t = (count > 1) ? tStart + (tEnd - tStart) * i / (count - 1) : tStart;

                // 使用de Boor算法计算曲线上的点
                var point = DeBoorEvaluate(spline, t);
                if (point != null)
                    points.Add(new CadPoint(point.X, point.Y, 0));
            }

            if (points.Count > 0)
            {
                var firstCtrl = spline.ControlPoints[0];
                var lastCtrl = spline.ControlPoints[spline.ControlPoints.Count - 1];
                points[0] = new CadPoint(firstCtrl.X, firstCtrl.Y, 0);
                if (points.Count > 1)
                    points[points.Count - 1] = new CadPoint(lastCtrl.X, lastCtrl.Y, 0);
            }

            return points;
        }

        /// <summary>
        /// 样条曲线按指定点数均匀离散化：在参数域内均匀分布采样点
        /// </summary>
        private List<CadPoint> DiscretizeSplineByCount(CadSpline spline, int pointCount)
        {
            var points = new List<CadPoint>();

            if (spline == null || spline.ControlPoints == null || spline.ControlPoints.Count < 2
                || spline.Knots == null || spline.Knots.Count == 0)
                return points;

            if (pointCount < 2)
                pointCount = 2;

            // 获取有效参数域
            var (tStart, tEnd) = spline.GetParameterRange();

            // 在参数域内均匀采样
            for (int i = 0; i < pointCount; i++)
            {
                double t = (pointCount > 1) ? tStart + (tEnd - tStart) * i / (pointCount - 1) : tStart;

                var point = DeBoorEvaluate(spline, t);
                if (point != null)
                    points.Add(new CadPoint(point.X, point.Y, 0));
            }

            if (points.Count > 0)
            {
                var firstCtrl = spline.ControlPoints[0];
                var lastCtrl = spline.ControlPoints[spline.ControlPoints.Count - 1];
                points[0] = new CadPoint(firstCtrl.X, firstCtrl.Y, 0);
                if (points.Count > 1)
                    points[points.Count - 1] = new CadPoint(lastCtrl.X, lastCtrl.Y, 0);
            }

            return points;
        }

        /// <summary>
        /// 估算样条曲线的总长度（使用控制点多边形的线段长度之和）
        /// 这是一个保守估计，实际曲线长度可能略大或略小
        /// </summary>
        private static double EstimateSplineLength(CadSpline spline)
        {
            double length = 0;
            var pts = spline.ControlPoints;

            for (int i = 0; i < pts.Count - 1; i++)
            {
                double dx = pts[i + 1].X - pts[i].X;
                double dy = pts[i + 1].Y - pts[i].Y;
                length += Math.Sqrt(dx * dx + dy * dy);
            }

            // 如果是闭合样条，加上最后一条回连到起点的边
            if (spline.IsClosed && pts.Count >= 2)
            {
                double dx = pts[0].X - pts[pts.Count - 1].X;
                double dy = pts[0].Y - pts[pts.Count - 1].Y;
                length += Math.Sqrt(dx * dx + dy * dy);
            }

            return length;
        }

        /// <summary>
        /// de Boor算法：计算NURBS/B-Spline曲线上给定参数t处的点坐标
        ///
        /// 算法原理：
        ///   对于非有理B样条（所有权重=1），直接使用标准de Boor递推公式：
        ///     d[j][i] = (1-α) * d[j-1][i-1] + α * d[j-1][i]
        ///     其中 α = (t - knots[i+j]) / (knots[i+degree+1-j] - knots[i+j])
        ///
        ///   对于有理NURBS（权重不全为1），先在齐次坐标系下计算，
        ///   再投影回笛卡尔坐标：
        ///     x_cart = x_homog / w
        ///     y_cart = y_homog / w
        ///
        /// 参数：
        ///   spline - 样条曲线对象（包含控制点、节点向量、度数、权重等）
        ///   t - 曲线参数值（必须在有效参数域 [Knots[degree], Knots[KnotCount-degree-1] 内）
        ///
        /// 返回：
        ///   曲线上的点坐标，如果参数t超出有效域则返回null
        /// </summary>
        private static PointF DeBoorEvaluate(CadSpline spline, double t)
        {
            int degree = spline.Degree;
            var controlPts = spline.ControlPoints;
            var knots = spline.Knots;
            var weights = spline.Weights;

            // 有效参数域检查
            double tMin = knots[degree];
            double tMax = knots[knots.Count - degree - 1];

            // 允许轻微超出边界（数值误差容忍）
            const double eps = 1e-7;
            if (t < tMin - eps || t > tMax + eps)
                return null;

            // 将t限制到有效域内（防止越界访问数组）
            t = Math.Max(tMin, Math.Min(tMax, t));

            // 找到包含参数t的节点区间 [knots[k], knots[k+1])
            // 使得 knots[k] <= t < knots[k+1]
            int k = degree;
            while (k < knots.Count - degree - 1 && knots[k + 1] <= t)
                k++;

            // 初始化控制点数组（考虑权重）
            int n = degree + 1; // 当前层需要计算的点数
            var d = new PointF[n];
            var w = new double[n]; // 权重数组

            for (int j = 0; j < n; j++)
            {
                d[j] = new PointF(); // 初始化每个元素
                int idx = k - degree + j; // 控制点索引

                // 边界检查
                if (idx >= 0 && idx < controlPts.Count)
                {
                    d[j] = controlPts[idx];

                    // 获取权重（如果有理样条且有权重数据）
                    if (weights != null && idx < weights.Count)
                        w[j] = weights[idx];
                    else
                        w[j] = 1.0;
                }
                else
                {
                    d[j] = new PointF(0, 0);
                    w[j] = 1.0;
                }
            }

            // de Boor递推计算（从最高阶到最低阶）
            for (int r = 1; r <= degree; r++)
            {
                for (int j = degree; j >= r; j--)
                {
                    int i = k - degree + j;

                    // 计算插值系数α
                    double denom = knots[i + degree + 1 - r] - knots[i];
                    double alpha;

                    if (Math.Abs(denom) < 1e-12)
                    {
                        // 节点重复时（通常出现在首尾），α取0或1
                        alpha = 0.5; // 中间值，避免极端情况
                    }
                    else
                    {
                        alpha = (t - knots[i]) / denom;
                    }

                    // 在齐次坐标系下进行线性插值
                    // P_new = (1-α) * P_left + α * P_right（带权重）
                    double wNew = (1 - alpha) * w[j - 1] + alpha * w[j];
                    
                    if (Math.Abs(wNew) > 1e-15)
                    {
                        float x = (float)(((1 - alpha) * w[j - 1] * d[j - 1].X + alpha * w[j] * d[j].X) / wNew);
                        float y = (float)(((1 - alpha) * w[j - 1] * d[j - 1].Y + alpha * w[j] * d[j].Y) / wNew);
                        d[j] = new PointF(x, y);
                        w[j] = wNew;
                    }
                    else
                    {
                        // 权重接近零时的退化处理
                        d[j] = new PointF(
                            (float)((d[j - 1].X + d[j].X) / 2),
                            (float)((d[j - 1].Y + d[j].Y) / 2));
                        w[j] = 1e-15;
                    }
                }
            }

            // 最终结果在 d[degree]
            return d[degree];
        }

        /// <summary>
        /// 圆弧离散化：将起止角度范围按弧长近似等分为若干段
        /// 每段对应的圆心角 ≈ pitchMM / radius（弧度），沿逆时针方向采样
        /// 特殊处理跨360°边界的情况（如从300°到60°）
        /// </summary>
        private List<CadPoint> DiscretizeArc(CadArc arc, double pitchMM)
        {
            var points = new List<CadPoint>();
            if (arc.Radius <= 0) return points;

            // 计算圆弧跨越的角度范围（处理跨越0°的情况）
            double sweep = NormalizeSweep(arc.EndAngle - arc.StartAngle);

            // 弧长 = 半径 × 角度（弧度）
            double arcLength = Math.Abs(sweep) * Math.PI / 180.0 * arc.Radius;
            if (arcLength < 1e-9)
            {
                // 极短弧或零长弧，只输出起点
                double startRad = arc.StartAngle * Math.PI / 180.0;
                points.Add(new CadPoint(
                    arc.CenterX + arc.Radius * Math.Cos(startRad),
                    arc.CenterY + arc.Radius * Math.Sin(startRad),
                    arc.CenterZ));
                return points;
            }

            // 按弧长计算分段数
            int count = (int)Math.Ceiling(arcLength / pitchMM) + 1;
            for (int i = 0; i < count; i++)
            {
                double t = (count > 1) ? (double)i / (count - 1) : 0;
                double angleDeg = arc.StartAngle + sweep * t;
                double angleRad = angleDeg * Math.PI / 180.0;
                points.Add(new CadPoint(
                    arc.CenterX + arc.Radius * Math.Cos(angleRad),
                    arc.CenterY + arc.Radius * Math.Sin(angleRad),
                    arc.CenterZ));
            }
            return points;
        }

        /// <summary>
        /// 圆形离散化：完整0°~360°等间距采样
        /// 周长 = 2πr，分段数由周长/pitchMM决定
        /// </summary>
        private List<CadPoint> DiscretizeCircle(CadCircle circle, double pitchMM)
        {
            var points = new List<CadPoint>();
            if (circle.Radius <= 0) return points;

            double circumference = 2 * Math.PI * circle.Radius;
            int count = (int)Math.Ceiling(circumference / pitchMM);

            // 至少生成4个点保证圆形基本形状
            count = Math.Max(count, 4);
            // 末尾点与首点重合（闭合），所以循环到count（包含count个点，第count个点即回到0°）
            for (int i = 0; i < count; i++)
            {
                double angleRad = 2.0 * Math.PI * i / count;
                points.Add(new CadPoint(
                    circle.CenterX + circle.Radius * Math.Cos(angleRad),
                    circle.CenterY + circle.Radius * Math.Sin(angleRad),
                    circle.CenterZ));
            }
            return points;
        }

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

        /// <summary>
        /// 椭圆离散化：使用参数方程在t∈[StartAngle, EndAngle]范围内等分采样
        /// 参数方程：
        ///   x = cx + a·cos(t)·cos(φ) - b·sin(t)·sin(φ)
        ///   y = cy + a·cos(t)·sin(φ) + b·sin(t)·cos(φ)
        /// 其中a=长半轴, b=短半轴, φ=旋转角(弧度), t=参数角(弧度)
        /// </summary>
        private List<CadPoint> DiscretizeEllipse(CadEllipse ellipse, double pitchMM)
        {
            var points = new List<CadPoint>();
            if (ellipse.MajorAxisLength <= 0) return points;

            // 估算椭圆弧长（用平均半径近似）：≈ π · (a+b) · |Δθ| / 180
            double avgRadius = (ellipse.MajorAxisLength + ellipse.MinorAxisLength) / 2.0;
            double sweep = NormalizeSweep(ellipse.EndAngle - ellipse.StartAngle);
            double approxLength = Math.Abs(sweep) * Math.PI / 180.0 * avgRadius;

            if (approxLength < 1e-9)
            {
                double tRad = ellipse.StartAngle * Math.PI / 180.0;
                double rotRadInner = ellipse.RotationAngle * Math.PI / 180.0;
                double localX = ellipse.MajorAxisLength * Math.Cos(tRad);
                double localY = ellipse.MinorAxisLength * Math.Sin(tRad);
                points.Add(new CadPoint(
                    ellipse.CenterX + localX * Math.Cos(rotRadInner) - localY * Math.Sin(rotRadInner),
                    ellipse.CenterY + localX * Math.Sin(rotRadInner) + localY * Math.Cos(rotRadInner),
                    ellipse.CenterZ));
                return points;
            }

            int count = (int)Math.Ceiling(approxLength / pitchMM) + 1;
            double rotRad = ellipse.RotationAngle * Math.PI / 180.0;
            double cosRot = Math.Cos(rotRad);
            double sinRot = Math.Sin(rotRad);

            for (int i = 0; i < count; i++)
            {
                double t = (count > 1) ? (double)i / (count - 1) : 0;
                // 参数角（度→弧度），在起止角范围内线性插值
                double paramAngleDeg = ellipse.StartAngle + sweep * t;
                double paramAngleRad = paramAngleDeg * Math.PI / 180.0;

                // 椭圆局部坐标系下的坐标
                double localX = ellipse.MajorAxisLength * Math.Cos(paramAngleRad);
                double localY = ellipse.MinorAxisLength * Math.Sin(paramAngleRad);

                // 经旋转变换后的世界坐标
                double worldX = ellipse.CenterX + localX * cosRot - localY * sinRot;
                double worldY = ellipse.CenterY + localX * sinRot + localY * cosRot;

                points.Add(new CadPoint(worldX, worldY, ellipse.CenterZ));
            }
            return points;
        }

        // ======================== 按点数离散化 ========================

        /// <summary>直线按指定点数均匀采样</summary>
        private List<CadPoint> DiscretizeLineByCount(CadLine line, int pointCount)
        {
            var points = new List<CadPoint>();
            double dx = line.EndX - line.StartX;
            double dy = line.EndY - line.StartY;
            double dz = line.EndZ - line.StartZ;

            for (int i = 0; i < pointCount; i++)
            {
                double t = (pointCount > 1) ? (double)i / (pointCount - 1) : 0;
                points.Add(new CadPoint(
                    line.StartX + dx * t,
                    line.StartY + dy * t,
                    line.StartZ + dz * t));
            }
            return points;
        }

        /// <summary>圆弧按指定点数均匀采样</summary>
        private List<CadPoint> DiscretizeArcByCount(CadArc arc, int pointCount)
        {
            var points = new List<CadPoint>();
            if (arc.Radius <= 0) return points;

            double sweep = NormalizeSweep(arc.EndAngle - arc.StartAngle);

            for (int i = 0; i < pointCount; i++)
            {
                double t = (pointCount > 1) ? (double)i / (pointCount - 1) : 0;
                double angleDeg = arc.StartAngle + sweep * t;
                double angleRad = angleDeg * Math.PI / 180.0;
                points.Add(new CadPoint(
                    arc.CenterX + arc.Radius * Math.Cos(angleRad),
                    arc.CenterY + arc.Radius * Math.Sin(angleRad),
                    arc.CenterZ));
            }
            return points;
        }

        /// <summary>圆形按指定点数均匀采样</summary>
        private List<CadPoint> DiscretizeCircleByCount(CadCircle circle, int pointCount)
        {
            var points = new List<CadPoint>();
            if (circle.Radius <= 0) return points;

            pointCount = Math.Max(pointCount, 4);
            for (int i = 0; i < pointCount; i++)
            {
                double angleRad = 2.0 * Math.PI * i / pointCount;
                points.Add(new CadPoint(
                    circle.CenterX + circle.Radius * Math.Cos(angleRad),
                    circle.CenterY + circle.Radius * Math.Sin(angleRad),
                    circle.CenterZ));
            }
            return points;
        }

        /// <summary>多段线按指定点数均匀采样</summary>
        private List<CadPoint> DiscretizePolylineByCount(CadLwPolyline polyline, int pointCount)
        {
            var vertices = polyline.Vertices;
            if (vertices == null || vertices.Count < 2)
                return new List<CadPoint>();

            // 先计算每段子线段的长度，按长度比例分配点数
            int segmentCount = polyline.IsClosed ? vertices.Count : vertices.Count - 1;
            var segLengths = new double[segmentCount];
            double totalLength = 0;

            for (int seg = 0; seg < segmentCount; seg++)
            {
                int fromIdx = seg % vertices.Count;
                int toIdx = (seg + 1) % vertices.Count;
                double dx = vertices[toIdx].X - vertices[fromIdx].X;
                double dy = vertices[toIdx].Y - vertices[fromIdx].Y;
                segLengths[seg] = Math.Sqrt(dx * dx + dy * dy);
                totalLength += segLengths[seg];
            }

            if (totalLength < 1e-9)
                return new List<CadPoint> { new CadPoint(vertices[0].X, vertices[0].Y, 0) };

            var points = new List<CadPoint>();
            for (int seg = 0; seg < segmentCount; seg++)
            {
                int fromIdx = seg % vertices.Count;
                int toIdx = (seg + 1) % vertices.Count;
                double x0 = vertices[fromIdx].X, y0 = vertices[fromIdx].Y;
                double dx = vertices[toIdx].X - x0;
                double dy = vertices[toIdx].Y - y0;

                // 按长度比例分配点数
                int segPoints = (int)Math.Round(pointCount * segLengths[seg] / totalLength);
                segPoints = Math.Max(segPoints, 2);
                int startI = (seg == 0) ? 0 : 1;

                for (int i = startI; i < segPoints; i++)
                {
                    double t = (segPoints > 1) ? (double)i / (segPoints - 1) : 0;
                    points.Add(new CadPoint(x0 + dx * t, y0 + dy * t, 0));
                }
            }
            return points;
        }

        /// <summary>椭圆按指定点数均匀采样</summary>
        private List<CadPoint> DiscretizeEllipseByCount(CadEllipse ellipse, int pointCount)
        {
            var points = new List<CadPoint>();
            if (ellipse.MajorAxisLength <= 0) return points;

            double sweep = NormalizeSweep(ellipse.EndAngle - ellipse.StartAngle);
            double rotRad = ellipse.RotationAngle * Math.PI / 180.0;
            double cosRot = Math.Cos(rotRad);
            double sinRot = Math.Sin(rotRad);

            for (int i = 0; i < pointCount; i++)
            {
                double t = (pointCount > 1) ? (double)i / (pointCount - 1) : 0;
                double paramAngleDeg = ellipse.StartAngle + sweep * t;
                double paramAngleRad = paramAngleDeg * Math.PI / 180.0;

                double localX = ellipse.MajorAxisLength * Math.Cos(paramAngleRad);
                double localY = ellipse.MinorAxisLength * Math.Sin(paramAngleRad);

                double worldX = ellipse.CenterX + localX * cosRot - localY * sinRot;
                double worldY = ellipse.CenterY + localX * sinRot + localY * cosRot;

                points.Add(new CadPoint(worldX, worldY, ellipse.CenterZ));
            }
            return points;
        }

        // ======================== 辅助方法 ========================

        /// <summary>
        /// 安全解析浮点数字符串，使用InvariantCulture确保小数点格式正确
        /// 解析失败时向warnings列表追加提示信息并返回默认值0
        /// </summary>
        private static double ParseDouble(string value, List<string> warnings, string fieldName)
        {
            if (double.TryParse(value, NumberStyles.Float, InvariantCulture, out double result))
                return result;
            warnings.Add($"无法解析{fieldName}的值: '{value}'，已使用默认值0");
            return 0;
        }

        /// <summary>
        /// 将角度差值归一化为有效的扫掠角度
        /// 处理DXF中常见的跨360°情况（如起始300°、终止60°，实际扫掠120°而非-240°）
        /// </summary>
        private static double NormalizeSweep(double deltaDegrees)
        {
            // DXF规范：ARC始终逆时针(CCW)，负差值需加360走长路径
            if (deltaDegrees < 0)
                deltaDegrees += 360;
            return deltaDegrees;
        }

        /// <summary>
        /// 遍历所有图元，通过GetBoundingBox()的Union操作计算整体范围
        /// </summary>
        private static BoundingBox CalculateExtents(List<CadEntity> allEntities)
        {
            var extents = new BoundingBox();
            foreach (var entity in allEntities)
            {
                if (entity != null)
                {
                    var bbox = entity.GetBoundingBox();
                    if (bbox != null && !bbox.IsEmpty)
                        extents = extents.Union(bbox);
                }
            }
            return extents;
        }
    }
}
