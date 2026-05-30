// Core/Services/RoiToolService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;

namespace Core.Services
{
    /// <summary>
    /// ROI工具服务实现——负责ROI区域的创建与等间距采样，
    /// 支持直线、折线、圆弧、自由手绘四种几何形态的离散化
    /// </summary>
    public class RoiToolService : IRoiToolService
    {
        #region ROI 创建方法

        /// <summary>
        /// 创建直线型ROI区域——设置起终点坐标，类型为Line
        /// </summary>
        public RoiRegion CreateLineRoi(PointF start, PointF end)
        {
            return new RoiRegion(RoiType.Line)
            {
                LineStartPoint = start,
                LineEndPoint = end
            };
        }

        /// <summary>
        /// 创建折线型ROI区域——将顶点序列拷贝到PolylineVertices属性中
        /// </summary>
        public RoiRegion CreatePolylineRoi(List<PointF> vertices)
        {
            if (vertices == null || vertices.Count < 2)
                throw new ArgumentException("折线ROI至少需要2个顶点", nameof(vertices));

            return new RoiRegion(RoiType.Polyline)
            {
                PolylineVertices = new List<PointF>(vertices)
            };
        }

        /// <summary>
        /// 创建圆弧型ROI区域——设置圆心、半径和起止角度
        /// </summary>
        public RoiRegion CreateArcRoi(PointF center, double radius, double startAngleDeg, double endAngleDeg)
        {
            if (radius <= 0)
                throw new ArgumentException("圆弧半径必须大于0", nameof(radius));

            return new RoiRegion(RoiType.Arc)
            {
                ArcCenter = center,
                ArcRadius = radius,
                ArcStartAngle = startAngleDeg,
                ArcEndAngle = endAngleDeg
            };
        }

        /// <summary>
        /// 创建自由手绘型ROI区域——将密集笔迹点序列拷贝到FreehandRawPoints属性中
        /// </summary>
        public RoiRegion CreateFreehandRoi(List<PointF> rawPoints)
        {
            var roi = new RoiRegion(RoiType.Freehand);
            roi.FreehandRawPoints = rawPoints != null ? new List<PointF>(rawPoints) : new List<PointF>();
            return roi;
        }

        #endregion

        #region 采样方法

        /// <summary>
        /// 根据ROI类型分发到对应的采样算法，生成等间距CadPoint序列
        /// </summary>
        public List<CadPoint> SamplePoints(RoiRegion roi, double pitchMM)
        {
            if (roi == null)
                throw new ArgumentNullException(nameof(roi));
            if (pitchMM <= 0)
                throw new ArgumentException("采样间距必须大于0", nameof(pitchMM));

            switch (roi.Type)
            {
                case RoiType.Line:
                    return SampleLine(roi, pitchMM);
                case RoiType.Polyline:
                    return SamplePolyline(roi, pitchMM);
                case RoiType.Arc:
                    return SampleArc(roi, pitchMM);
                case RoiType.Freehand:
                    return SampleFreehand(roi, pitchMM);
                default:
                    return new List<CadPoint>();
            }
        }

        /// <summary>
        /// 直线采样算法——在起终点之间按pitchMM等间距线性插值
        /// </summary>
        private List<CadPoint> SampleLine(RoiRegion roi, double pitchMM)
        {
            var result = new List<CadPoint>();
            var start = roi.LineStartPoint;
            var end = roi.LineEndPoint;

            // 空值或零距离保护
            if (start == null || end == null)
                return result;

            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double dz = end.Z - start.Z;
            double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            if (dist < 1e-6)
            {
                result.Add(new CadPoint(Math.Round(start.X, 4), Math.Round(start.Y, 4), Math.Round(start.Z, 4)));
                return result;
            }

            // 按间距计算插值步数，确保覆盖整条线段
            int stepCount = Math.Max(1, (int)Math.Ceiling(dist / pitchMM));
            for (int i = 0; i <= stepCount; i++)
            {
                double t = (double)i / stepCount;
                result.Add(new CadPoint(
                    Math.Round(start.X + dx * t, 4),
                    Math.Round(start.Y + dy * t, 4),
                    Math.Round(start.Z + dz * t, 4)
                ));
            }
            return result;
        }

        /// <summary>
        /// 折线采样算法——逐段线性插值后拼接，相邻段连接点去重避免重复
        /// </summary>
        private List<CadPoint> SamplePolyline(RoiRegion roi, double pitchMM)
        {
            var result = new List<CadPoint>();
            var vertices = roi.PolylineVertices;

            if (vertices == null || vertices.Count < 2)
                return result;

            for (int seg = 0; seg < vertices.Count - 1; seg++)
            {
                var p1 = vertices[seg];
                var p2 = vertices[seg + 1];
                double dx = p2.X - p1.X;
                double dy = p2.Y - p1.Y;
                double dz = p2.Z - p1.Z;
                double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);

                int stepCount = Math.Max(1, (int)Math.Ceiling(dist / pitchMM));
                // 非首段跳过起始点（与前一段的终点重复）
                int startIdx = (seg > 0) ? 1 : 0;
                for (int i = startIdx; i <= stepCount; i++)
                {
                    double t = (double)i / stepCount;
                    result.Add(new CadPoint(
                        Math.Round(p1.X + dx * t, 4),
                        Math.Round(p1.Y + dy * t, 4),
                        Math.Round(p1.Z + dz * t, 4)
                    ));
                }
            }
            return result;
        }

        /// <summary>
        /// 圆弧采样算法——按角度等分，使用参数方程 x=cx+r·cosθ, y=cy+r·sinθ 插值
        /// </summary>
        private List<CadPoint> SampleArc(RoiRegion roi, double pitchMM)
        {
            var result = new List<CadPoint>();
            var center = roi.ArcCenter;
            double radius = roi.ArcRadius;

            if (center == null || radius <= 0)
                return result;

            // 将度数转为弧度
            double startRad = roi.ArcStartAngle * Math.PI / 180.0;
            double endRad = roi.ArcEndAngle * Math.PI / 180.0;
            double arcLen = Math.Abs(endRad - startRad) * radius;

            if (arcLen < 1e-6)
            {
                result.Add(new CadPoint(
                    Math.Round(center.X + radius * Math.Cos(startRad), 4),
                    Math.Round(center.Y + radius * Math.Sin(startRad), 4),
                    Math.Round(center.Z, 4)
                ));
                return result;
            }

            // 按弧长计算等分步数
            int stepCount = Math.Max(1, (int)Math.Ceiling(arcLen / pitchMM));
            for (int i = 0; i <= stepCount; i++)
            {
                double t = (double)i / stepCount;
                double angle = startRad + (endRad - startRad) * t;
                result.Add(new CadPoint(
                    Math.Round(center.X + radius * Math.Cos(angle), 4),
                    Math.Round(center.Y + radius * Math.Sin(angle), 4),
                    Math.Round(center.Z, 4)
                ));
            }
            return result;
        }

        /// <summary>
        /// 自由手绘采样算法——对密集笔迹进行降采样处理：
        /// 1) 计算累积弦长（相邻点欧氏距离累加）
        /// 2) 沿累积弦长按pitchMM等间距重采样（线性插值）
        /// 3) 移动平均平滑（窗口大小3）去除高频抖动
        /// </summary>
        private List<CadPoint> SampleFreehand(RoiRegion roi, double pitchMM)
        {
            var rawPoints = roi.FreehandRawPoints;
            if (rawPoints == null || rawPoints.Count == 0)
                return new List<CadPoint>();

            // 单点直接返回
            if (rawPoints.Count == 1)
            {
                var p = rawPoints[0];
                return new List<CadPoint> { new CadPoint(Math.Round(p.X, 4), Math.Round(p.Y, 4), Math.Round(p.Z, 4)) };
            }

            // ===== 第一步：计算累积弦长 =====
            var chordLengths = new double[rawPoints.Count]; // 每个点到首点的累积弦长
            chordLengths[0] = 0.0;
            for (int i = 1; i < rawPoints.Count; i++)
            {
                double dx = rawPoints[i].X - rawPoints[i - 1].X;
                double dy = rawPoints[i].Y - rawPoints[i - 1].Y;
                double dz = rawPoints[i].Z - rawPoints[i - 1].Z;
                chordLengths[i] = chordLengths[i - 1] + Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }

            double totalLength = chordLengths[chordLengths.Length - 1];

            // 零长度保护
            if (totalLength < 1e-6)
            {
                var p0 = rawPoints[0];
                return new List<CadPoint> { new CadPoint(Math.Round(p0.X, 4), Math.Round(p0.Y, 4), Math.Round(p0.Z, 4)) };
            }

            // ===== 第二步：沿累积弦长按pitchMM等间距重采样（线性插值） =====
            var sampled = new List<CadPoint>();
            int sampleCount = Math.Max(1, (int)Math.Ceiling(totalLength / pitchMM));

            for (int i = 0; i <= sampleCount; i++)
            {
                double targetDist = totalLength * i / sampleCount; // 目标累积距离

                // 在chordLengths数组中二分查找定位所在段
                int idx = Array.BinarySearch(chordLengths, targetDist);
                if (idx >= 0)
                {
                    // 精确命中某个原始点
                    var pt = rawPoints[idx];
                    sampled.Add(new CadPoint(pt.X, pt.Y, pt.Z));
                }
                else
                {
                    // 未精确命中，取插入位置的前后两点做线性插值
                    int insertIdx = ~idx; // BinarySearch返回的位补码即为插入位置
                    if (insertIdx == 0)
                    {
                        var pt = rawPoints[0];
                        sampled.Add(new CadPoint(pt.X, pt.Y, pt.Z));
                    }
                    else if (insertIdx >= rawPoints.Count)
                    {
                        var pt = rawPoints[rawPoints.Count - 1];
                        sampled.Add(new CadPoint(pt.X, pt.Y, pt.Z));
                    }
                    else
                    {
                        // 在 [insertIdx-1, insertIdx] 段内线性插值
                        var p0 = rawPoints[insertIdx - 1];
                        var p1 = rawPoints[insertIdx];
                        double segLen = chordLengths[insertIdx] - chordLengths[insertIdx - 1];
                        double t = (segLen > 1e-9) ? (targetDist - chordLengths[insertIdx - 1]) / segLen : 0;
                        sampled.Add(new CadPoint(
                            p0.X + (p1.X - p0.X) * t,
                            p0.Y + (p1.Y - p0.Y) * t,
                            p0.Z + (p1.Z - p0.Z) * t
                        ));
                    }
                }
            }

            // ===== 第三步：移动平均平滑（窗口大小3），消除高频抖动 =====
            var smoothed = MovingAverageSmooth(sampled, 3);

            // 四舍五入到合理精度
            for (int i = 0; i < smoothed.Count; i++)
            {
                smoothed[i] = new CadPoint(
                    Math.Round(smoothed[i].X, 4),
                    Math.Round(smoothed[i].Y, 4),
                    Math.Round(smoothed[i].Z, 4)
                );
            }

            return smoothed;
        }

        /// <summary>
        /// 简单移动平均平滑——对每个点取其前后共windowSize个点的均值，
        /// 边界处自动缩小窗口以避免越界
        /// </summary>
        /// <param name="points">输入点列</param>
        /// <param name="windowSize">平滑窗口大小（奇数效果最佳，默认3）</param>
        /// <returns>平滑后的新点列</returns>
        private static List<CadPoint> MovingAverageSmooth(List<CadPoint> points, int windowSize)
        {
            if (points == null || points.Count <= 2 || windowSize < 2)
                return new List<CadPoint>(points);

            int halfWin = windowSize / 2;
            var result = new List<CadPoint>(points.Count);

            for (int i = 0; i < points.Count; i++)
            {
                // 计算实际可用窗口范围（边界处缩小）
                int start = Math.Max(0, i - halfWin);
                int end = Math.Min(points.Count - 1, i + halfWin);
                int count = end - start + 1;

                double sumX = 0, sumY = 0, sumZ = 0;
                for (int j = start; j <= end; j++)
                {
                    sumX += points[j].X;
                    sumY += points[j].Y;
                    sumZ += points[j].Z;
                }

                result.Add(new CadPoint(sumX / count, sumY / count, sumZ / count));
            }
            return result;
        }

        #endregion
    }
}
