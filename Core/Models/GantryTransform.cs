using System;

namespace Core.Models
{
    /// <summary>
    /// 跨龙门变换参数模型——描述龙门1坐标系到龙门2坐标系的刚体变换
    /// （含平移、旋转、缩放），用于双龙门协同运动时的坐标统一
    /// </summary>
    public class GantryTransform
    {
        /// <summary>X方向偏移（mm）</summary>
        public double OffsetX { get; set; }

        /// <summary>Y方向偏移（mm）</summary>
        public double OffsetY { get; set; }

        /// <summary>旋转角度（度）</summary>
        public double RotationDeg { get; set; }

        /// <summary>缩放因子（默认1.0）</summary>
        public double Scale { get; set; } = 1.0;

        /// <summary>对齐残差（mm），用于评估跨龙门对齐精度</summary>
        public double Residual { get; set; }

        /// <summary>是否已完成对齐</summary>
        public bool IsAligned { get; set; }

        /// <summary>
        /// 将龙门1坐标变换为龙门2等效坐标
        /// 注意：y1 应为龙门1的绝对Y（即 shared_Y + Dy）
        /// 返回的 Y2 为龙门2的共用Y轴坐标
        /// X2 = OffsetX + X1·cos(θ) - Y1·sin(θ)
        /// Y2 = OffsetY + X1·sin(θ) + Y1·cos(θ)
        /// </summary>
        /// <param name="x1">龙门1 X坐标（Dx轴位置）</param>
        /// <param name="y1">龙门1绝对Y坐标（shared_Y + Dy）</param>
        /// <returns>龙门2等效坐标 (X2, 共用Y轴坐标)</returns>
        public (double X2, double Y2) TransformGantry1ToGantry2(double x1, double y1)
        {
            double rad = RotationDeg * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            double x2 = OffsetX + x1 * cos - y1 * sin;
            double y2 = OffsetY + x1 * sin + y1 * cos;
            return (x2, y2);
        }

        /// <summary>
        /// 仅对差分移动量施加旋转+缩放（偏移量在差分中抵消，不需要）
        /// 用于已知龙门1移动量求龙门2等效移动量的场景（如夹爪定位跨龙门补偿）
        /// Δx2 = (Δx1·cos(θ) - Δy1·sin(θ)) · Scale
        /// Δy2 = (Δx1·sin(θ) + Δy1·cos(θ)) · Scale
        /// </summary>
        /// <param name="deltaX1">龙门1 X方向移动量</param>
        /// <param name="deltaY1">龙门1 Y方向移动量</param>
        /// <returns>龙门2等效移动量 (Δx2, Δy2)</returns>
        public (double DeltaX2, double DeltaY2) TransformDelta(double deltaX1, double deltaY1)
        {
            double rad = RotationDeg * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            double dx2 = (deltaX1 * cos - deltaY1 * sin) * Scale;
            double dy2 = (deltaX1 * sin + deltaY1 * cos) * Scale;
            return (dx2, dy2);
        }
    }
}
