// Core/Models/RoiRegion.cs
using Prism.Mvvm;
using System;
using System.Collections.Generic;

namespace Core.Models
{
    /// <summary>
    /// ROI区域类型枚举
    /// </summary>
    public enum RoiType
    {
        Line,       // 直线ROI
        Polyline,   // 折线ROI
        Arc,        // 圆弧ROI
        Freehand    // 自由手绘ROI
    }

    /// <summary>
    /// ROI感兴趣区域模型——定义用户在视觉界面上框选或绘制的点胶区域，
    /// 支持多种几何形态，可通过采样算法生成离散化的 CadPoint 序列
    /// </summary>
    public class RoiRegion : BindableBase
    {
        #region 公共属性

        private RoiType _type;
        /// <summary>ROI区域类型（线段/折线/圆弧/手绘）</summary>
        public RoiType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        private double _samplingPitchMM = 1.0;
        /// <summary>采样间距 mm（控制离散化密度）</summary>
        public double SamplingPitchMM
        {
            get => _samplingPitchMM;
            set => SetProperty(ref _samplingPitchMM, value);
        }

        #endregion

        #region LineRoi 特有属性

        private PointF _lineStartPoint;
        /// <summary>直线ROI起点</summary>
        public PointF LineStartPoint
        {
            get => _lineStartPoint;
            set => SetProperty(ref _lineStartPoint, value);
        }

        private PointF _lineEndPoint;
        /// <summary>直线ROI终点</summary>
        public PointF LineEndPoint
        {
            get => _lineEndPoint;
            set => SetProperty(ref _lineEndPoint, value);
        }

        #endregion

        #region PolylineRoi 特有属性

        private List<PointF> _polylineVertices = new List<PointF>();
        /// <summary>折线ROI顶点序列</summary>
        public List<PointF> PolylineVertices
        {
            get => _polylineVertices;
            set => SetProperty(ref _polylineVertices, value);
        }

        #endregion

        #region ArcRoi 特有属性

        private PointF _arcCenter;
        /// <summary>圆弧ROI圆心</summary>
        public PointF ArcCenter
        {
            get => _arcCenter;
            set => SetProperty(ref _arcCenter, value);
        }

        private double _arcRadius;
        /// <summary>圆弧ROI半径（mm）</summary>
        public double ArcRadius
        {
            get => _arcRadius;
            set => SetProperty(ref _arcRadius, value);
        }

        private double _arcStartAngle;
        /// <summary>圆弧起始角度（度数）</summary>
        public double ArcStartAngle
        {
            get => _arcStartAngle;
            set => SetProperty(ref _arcStartAngle, value);
        }

        private double _arcEndAngle;
        /// <summary>圆弧终止角度（度数）</summary>
        public double ArcEndAngle
        {
            get => _arcEndAngle;
            set => SetProperty(ref _arcEndAngle, value);
        }

        #endregion

        #region FreehandRoi 特有属性

        private List<PointF> _freehandRawPoints = new List<PointF>();
        /// <summary>自由手绘ROI的密集笔迹原始点序列</summary>
        public List<PointF> FreehandRawPoints
        {
            get => _freehandRawPoints;
            set => SetProperty(ref _freehandRawPoints, value);
        }

        #endregion

        #region 采样方法

        /// <summary>
        /// 根据ROI类型和采样间距生成离散化点列
        /// 具体采样算法由 RoiToolService 实现，此处提供各类型的骨架实现
        /// </summary>
        /// <returns>采样后的CadPoint列表（空列表表示尚未实现具体采样逻辑）</returns>
        public List<CadPoint> SamplePoints()
        {
            var result = new List<CadPoint>();

            switch (Type)
            {
                case RoiType.Line:
                    result = SampleLine();
                    break;
                case RoiType.Polyline:
                    result = SamplePolyline();
                    break;
                case RoiType.Arc:
                    result = SampleArc();
                    break;
                case RoiType.Freehand:
                    result = SampleFreehand();
                    break;
                default:
                    break;
            }

            return result;
        }

        /// <summary>
        /// 直线ROI采样：在起终点之间按等间距插值
        /// </summary>
        private List<CadPoint> SampleLine()
        {
            var points = new List<CadPoint>();
            if (LineStartPoint == null || LineEndPoint == null)
                return points;

            double dx = LineEndPoint.X - LineStartPoint.X;
            double dy = LineEndPoint.Y - LineStartPoint.Y;
            double dz = LineEndPoint.Z - LineStartPoint.Z;
            double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            if (dist < 1e-6)
            {
                points.Add(new CadPoint(LineStartPoint.X, LineStartPoint.Y, LineStartPoint.Z));
                return points;
            }

            int stepCount = Math.Max(1, (int)Math.Ceiling(dist / SamplingPitchMM));
            for (int i = 0; i <= stepCount; i++)
            {
                double t = (double)i / stepCount;
                points.Add(new CadPoint(
                    LineStartPoint.X + dx * t,
                    LineStartPoint.Y + dy * t,
                    LineStartPoint.Z + dz * t
                ));
            }
            return points;
        }

        /// <summary>
        /// 折线ROI采样：逐段按等间距插值后拼接
        /// </summary>
        private List<CadPoint> SamplePolyline()
        {
            var points = new List<CadPoint>();
            if (PolylineVertices == null || PolylineVertices.Count < 2)
                return points;

            for (int seg = 0; seg < PolylineVertices.Count - 1; seg++)
            {
                var p1 = PolylineVertices[seg];
                var p2 = PolylineVertices[seg + 1];
                double dx = p2.X - p1.X;
                double dy = p2.Y - p1.Y;
                double dz = p2.Z - p1.Z;
                double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);

                int stepCount = Math.Max(1, (int)Math.Ceiling(dist / SamplingPitchMM));
                // 避免重复端点：非首段跳过起始点
                int startIdx = (seg > 0) ? 1 : 0;
                for (int i = startIdx; i <= stepCount; i++)
                {
                    double t = (double)i / stepCount;
                    points.Add(new CadPoint(
                        p1.X + dx * t,
                        p1.Y + dy * t,
                        p1.Z + dz * t
                    ));
                }
            }
            return points;
        }

        /// <summary>
        /// 圆弧ROI采样：按角度等间距在圆弧上插值
        /// </summary>
        private List<CadPoint> SampleArc()
        {
            var points = new List<CadPoint>();
            if (ArcCenter == null || ArcRadius <= 0)
                return points;

            double startRad = ArcStartAngle * Math.PI / 180.0;
            double endRad = ArcEndAngle * Math.PI / 180.0;
            double arcLen = Math.Abs(endRad - startRad) * ArcRadius;

            if (arcLen < 1e-6)
            {
                double x = ArcCenter.X + ArcRadius * Math.Cos(startRad);
                double y = ArcCenter.Y + ArcRadius * Math.Sin(startRad);
                points.Add(new CadPoint(x, y, ArcCenter.Z));
                return points;
            }

            int stepCount = Math.Max(1, (int)Math.Ceiling(arcLen / SamplingPitchMM));
            for (int i = 0; i <= stepCount; i++)
            {
                double t = (double)i / stepCount;
                double angle = startRad + (endRad - startRad) * t;
                double x = ArcCenter.X + ArcRadius * Math.Cos(angle);
                double y = ArcCenter.Y + ArcRadius * Math.Sin(angle);
                points.Add(new CadPoint(x, y, ArcCenter.Z));
            }
            return points;
        }

        /// <summary>
        /// 自由手绘ROI采样：对密集笔迹点进行降采样（道格拉斯-普克简化或等间距抽稀）
        /// 此处使用简单的等间距抽稀策略
        /// </summary>
        private List<CadPoint> SampleFreehand()
        {
            var points = new List<CadPoint>();
            if (FreehandRawPoints == null || FreehandRawPoints.Count == 0)
                return points;

            if (FreehandRawPoints.Count == 1)
            {
                var p = FreehandRawPoints[0];
                points.Add(new CadPoint(p.X, p.Y, p.Z));
                return points;
            }

            // 等间距抽稀：累计距离超过SamplingPitchMM时取点
            points.Add(new CadPoint(FreehandRawPoints[0].X, FreehandRawPoints[0].Y, FreehandRawPoints[0].Z));
            double accumulatedDist = 0;
            PointF lastSampled = FreehandRawPoints[0];

            for (int i = 1; i < FreehandRawPoints.Count; i++)
            {
                var curr = FreehandRawPoints[i];
                double dx = curr.X - lastSampled.X;
                double dy = curr.Y - lastSampled.Y;
                double dz = curr.Z - lastSampled.Z;
                double d = Math.Sqrt(dx * dx + dy * dy + dz * dz);

                accumulatedDist += d;
                if (accumulatedDist >= SamplingPitchMM)
                {
                    points.Add(new CadPoint(curr.X, curr.Y, curr.Z));
                    lastSampled = curr;
                    accumulatedDist = 0;
                }
            }

            // 确保末尾点被包含
            var lastRaw = FreehandRawPoints[FreehandRawPoints.Count - 1];
            if (points.Count == 0 ||
                Math.Abs(points[points.Count - 1].X - lastRaw.X) > 1e-6 ||
                Math.Abs(points[points.Count - 1].Y - lastRaw.Y) > 1e-6)
            {
                points.Add(new CadPoint(lastRaw.X, lastRaw.Y, lastRaw.Z));
            }

            return points;
        }

        #endregion

        /// <summary>无参构造函数</summary>
        public RoiRegion()
        {
            PolylineVertices = new List<PointF>();
            FreehandRawPoints = new List<PointF>();
        }

        /// <summary>带类型参数的构造函数</summary>
        public RoiRegion(RoiType type)
        {
            Type = type;
            PolylineVertices = new List<PointF>();
            FreehandRawPoints = new List<PointF>();
        }
    }
}
