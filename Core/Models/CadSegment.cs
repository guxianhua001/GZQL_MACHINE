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
                segment.SegmentType = CadSegmentType.Line;
                return segment;
            }

            double midX = (x1 + x2) / 2.0;
            double midY = (y1 + y2) / 2.0;

            // 步骤2：计算圆心角 θ = 4 × arctan(|bulge|)
            double absBulge = Math.Abs(bulge);
            double theta = 4.0 * Math.Atan(absBulge);

            // 步骤3：计算半径 r = chord × (1 + b²) / (4 × |b|)
            double radius = chord * (1.0 + bulge * bulge) / (4.0 * absBulge);
            segment.Radius = radius;

            // 步骤4：计算矢高 sagitta = |b| × chord / 2
            double sagitta = absBulge * chord / 2.0;

            // 步骤5：计算圆心位置
            double alpha = Math.Atan2(dy, dx);
            
            double sign = bulge > 0 ? 1.0 : -1.0;
            double beta = alpha + Math.PI / 2.0 * sign;

            double halfChord = chord / 2.0;
            double apothemSquared = radius * radius - halfChord * halfChord;
            
            double apothem;
            if (apothemSquared < 0)
            {
                apothem = Math.Max(0, sagitta);
            }
            else
            {
                apothem = Math.Sqrt(apothemSquared);
                
                if (theta > Math.PI)
                {
                    apothem = -apothem;
                }
            }

            segment.CenterX = midX + apothem * Math.Cos(beta);
            segment.CenterY = midY + apothem * Math.Sin(beta);

            // 步骤6：计算起止角度（度数）
            segment.StartAngle = Math.Atan2(y1 - segment.CenterY, x1 - segment.CenterX) * 180.0 / Math.PI;
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
            
            if (Bulge > 0)
            {
                if (sweep < 0) sweep += 360.0;
            }
            else
            {
                if (sweep > 0) sweep -= 360.0;
            }
            
            return sweep;
        }
    }
}
