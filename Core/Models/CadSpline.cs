// Core/Models/CadSpline.cs
using System;
using System.Collections.Generic;

namespace Core.Models
{
    /// <summary>
    /// 样条曲线图元（NURBS: Non-Uniform Rational B-Spline）
    /// 支持AutoCAD 2018 DXF格式(AC1032)的SPLINE实体解析和离散化
    /// 
    /// DXF组码说明：
    ///   70 - 标志位（bit0=闭合, bit1=周期性, bit2=有理, bit3=平面, bit4=线性）
    ///   71 - 度数（Degree，通常为3表示三次样条）
    ///   72 - 控制点数量
    ///   73 - 节点数量 = degree + numControlPoints + 1（非周期）或 degree + numControlPoints（周期）
    ///   74 - 拟合数据标志
    ///   42 - 节点公差
    ///   43 - 拟合公差
    ///   40 - 节点向量值（重复出现多次）
    ///   10/20/30 - 控制点坐标（重复出现多次）
    ///   41 - 权重值（可选，仅在有理样条时出现，默认1.0）
    ///   210/220/230 - 法向量（平面法线方向）
    /// </summary>
    public class CadSpline : CadEntity
    {
        private int _degree;
        private bool _isClosed;
        private bool _isPeriodic;
        private bool _isRational;
        private List<PointF> _controlPoints;
        private List<double> _knots;
        private List<double> _weights;
        private double _normalX, _normalY, _normalZ;
        private double _knotTolerance;
        private double _fitTolerance;

        /// <summary>
        /// 样条曲线度数（阶数-1），通常为3（三次样条）
        /// </summary>
        public int Degree
        {
            get => _degree;
            set => SetProperty(ref _degree, value);
        }

        /// <summary>
        /// 是否闭合样条（起点与终点相连）
        /// </summary>
        public bool IsClosed
        {
            get => _isClosed;
            set => SetProperty(ref _isClosed, value);
        }

        /// <summary>
        /// 是否周期性样条（控制点和节点首尾衔接）
        /// </summary>
        public bool IsPeriodic
        {
            get => _isPeriodic;
            set => SetProperty(ref _isPeriodic, value);
        }

        /// <summary>
        /// 是否有理样条（权重不全为1.0）
        /// </summary>
        public bool IsRational
        {
            get => _isRational;
            set => SetProperty(ref _isRational, value);
        }

        /// <summary>
        /// 控制点列表（定义样条形状的关键点）
        /// </summary>
        public List<PointF> ControlPoints
        {
            get => _controlPoints;
            set => SetProperty(ref _controlPoints, value);
        }

        /// <summary>
        /// 节点向量（非递减序列，定义参数域划分）
        /// 长度 = Degree + ControlPoints.Count + 1（非周期）或 Degree + ControlPoints.Count（周期）
        /// </summary>
        public List<double> Knots
        {
            get => _knots;
            set => SetProperty(ref _knots, value);
        }

        /// <summary>
        /// 权重列表（每个控制点的权重，仅IsRational=true时有意义）
        /// 为空或null时所有权重视为1.0
        /// </summary>
        public List<double> Weights
        {
            get => _weights;
            set => SetProperty(ref _weights, value);
        }

        /// <summary>
        /// 平面法向量X分量（用于平面样条的朝向判定）
        /// </summary>
        public double NormalX
        {
            get => _normalX;
            set => SetProperty(ref _normalX, value);
        }

        /// <summary>
        /// 平面法向量Y分量
        /// </summary>
        public double NormalY
        {
            get => _normalY;
            set => SetProperty(ref _normalY, value);
        }

        /// <summary>
        /// 平面法向量Z分量
        /// </summary>
        public double NormalZ
        {
            get => _normalZ;
            set => SetProperty(ref _normalZ, value);
        }

        /// <summary>
        /// 节点公差（用于节点向量的数值精度）
        /// </summary>
        public double KnotTolerance
        {
            get => _knotTolerance;
            set => SetProperty(ref _knotTolerance, value);
        }

        /// <summary>
        /// 拟合公差（用于拟合点的容差范围）
        /// </summary>
        public double FitTolerance
        {
            get => _fitTolerance;
            set => SetProperty(ref _fitTolerance, value);
        }

        /// <summary>
        /// 无参构造函数，初始化集合并设置图元类型为Spline
        /// </summary>
        public CadSpline()
        {
            EntityType = CadEntityType.Spline;
            ControlPoints = new List<PointF>();
            Knots = new List<double>();
            Weights = new List<double>();
        }

        /// <summary>
        /// 带参数构造函数，指定样条的所有几何参数
        /// </summary>
        public CadSpline(int degree, List<PointF> controlPoints, List<double> knots,
            List<double> weights = null, bool isClosed = false, bool isPeriodic = false,
            double normalX = 0, double normalY = 0, double normalZ = 1)
        {
            EntityType = CadEntityType.Spline;
            Degree = degree;
            ControlPoints = controlPoints ?? new List<PointF>();
            Knots = knots ?? new List<double>();
            Weights = weights ?? new List<double>();
            IsClosed = isClosed;
            IsPeriodic = isPeriodic;
            IsRational = weights != null && weights.Count > 0;
            NormalX = normalX;
            NormalY = normalY;
            NormalZ = normalZ;
        }

        /// <summary>
        /// 获取控制点的数量
        /// </summary>
        public int ControlPointCount => ControlPoints?.Count ?? 0;

        /// <summary>
        /// 获取节点向量的长度
        /// </summary>
        public int KnotCount => Knots?.Count ?? 0;

        /// <summary>
        /// 获取有效参数域范围 [Knots[Degree], Knots[KnotCount-Degree-1]]
        /// 用于离散化时的参数t取值范围
        /// </summary>
        public (double Start, double End) GetParameterRange()
        {
            if (Knots == null || Knots.Count <= Degree + 1)
                return (0.0, 1.0);

            return (Knots[Degree], Knots[Knots.Count - Degree - 1]);
        }

        /// <summary>
        /// 计算样条曲线的轴对齐包围盒
        /// 通过采样控制点来估算边界范围
        /// </summary>
        /// <returns>包围盒</returns>
        public override BoundingBox GetBoundingBox()
        {
            var bbox = new BoundingBox();

            if (ControlPoints == null || ControlPoints.Count == 0)
                return bbox;

            // 使用控制点估算边界（保守估计，实际曲线可能超出控制点多边形）
            foreach (var pt in ControlPoints)
            {
                bbox.ExpandToInclude(pt.X, pt.Y);
            }

            // 扩展包围盒以容纳可能的曲线外凸部分（按控制点多边形边长的10%扩展）
            if (ControlPoints.Count >= 2)
            {
                double maxExtent = 0;
                for (int i = 0; i < ControlPoints.Count; i++)
                {
                    for (int j = i + 1; j < ControlPoints.Count; j++)
                    {
                        double dx = ControlPoints[j].X - ControlPoints[i].X;
                        double dy = ControlPoints[j].Y - ControlPoints[i].Y;
                        double dist = Math.Sqrt(dx * dx + dy * dy);
                        if (dist > maxExtent) maxExtent = dist;
                    }
                }
                double expansion = maxExtent * 0.05; // 5% 扩展
                bbox.ExpandToInclude(bbox.MinX - expansion, bbox.MinY - expansion);
                bbox.ExpandToInclude(bbox.MaxX + expansion, bbox.MaxY + expansion);
            }

            return bbox;
        }
    }
}
