// Core/Models/CadAlignTransformSnapshot.cs
using System;
using Core.Services;

namespace Core.Models
{
    /// <summary>
    /// CAD 对齐坐标变换快照——封装回转中心、偏移/仿射参数及方向取反开关，
    /// 用于在产品旋转时将 CAD 坐标点换算为旋转后的机械坐标。
    /// 变换流程：① CAD→机械坐标（仿射或平移） ② 绕回转中心(Mox,Moy)旋转指定角度
    /// </summary>
    public class CadAlignTransformSnapshot
    {
        #region 变换参数

        /// <summary>快照是否有效（Step1 回转中心 + Step2 偏移/仿射 均已完成）</summary>
        public bool IsValid { get; set; }

        /// <summary>回转中心 X 坐标（mm）</summary>
        public double Mox { get; set; }

        /// <summary>回转中心 Y 坐标（mm）</summary>
        public double Moy { get; set; }

        /// <summary>X 方向平移量（1点平移模式，mm）</summary>
        public double DeltaX { get; set; }

        /// <summary>Y 方向平移量（1点平移模式，mm）</summary>
        public double DeltaY { get; set; }

        /// <summary>是否使用仿射标定（true 时使用 AffineResult，false 时使用 DeltaX/DeltaY 平移）</summary>
        public bool UseAffineCalibration { get; set; }

        /// <summary>仿射标定结果（UseAffineCalibration=true 时生效）</summary>
        public AffineCalibrationResult AffineResult { get; set; }

        /// <summary>X 方向角度取反开关（硬件轴镜像，启用后旋转时 dx 取反）</summary>
        public bool InvertXAngle { get; set; }

        /// <summary>Y 方向角度取反开关（硬件轴镜像，启用后旋转时 dy 取反）</summary>
        public bool InvertYAngle { get; set; }

        /// <summary>旋转角度 θ 取反开关（硬件轴镜像，启用后旋转角度取负）</summary>
        public bool InvertThetaAngle { get; set; }

        #endregion

        #region 坐标变换核心算法

        /// <summary>
        /// 将 CAD 坐标点按当前快照变换为旋转后的机械坐标。
        /// 步骤：
        /// 1. CAD→机械坐标：仿射模式用 AffineResult，否则用 DeltaX/DeltaY 平移
        /// 2. 相对回转中心偏移：dx = mx - Mox, dy = my - Moy
        /// 3. 应用方向取反开关：edx = InvertXAngle ? -dx : dx
        /// 4. 绕回转中心旋转 rotationAngleDeg 度：
        ///    newX = edx·cosθ - edy·sinθ + Mox
        ///    newY = edx·sinθ + edy·cosθ + Moy
        /// </summary>
        /// <param name="cadX">CAD 坐标 X</param>
        /// <param name="cadY">CAD 坐标 Y</param>
        /// <param name="rotationAngleDeg">产品旋转角度（度数，正值逆时针）</param>
        /// <returns>旋转后的机械坐标 (X, Y)</returns>
        public (double X, double Y) Transform(double cadX, double cadY, double rotationAngleDeg)
        {
            // ① CAD → 机械坐标（仿射或平移）
            double mx, my;
            if (UseAffineCalibration && AffineResult != null)
            {
                (mx, my) = AffineCalibrationService.Transform(AffineResult, cadX, cadY);
            }
            else
            {
                mx = cadX + DeltaX;
                my = cadY + DeltaY;
            }

            // ② 相对回转中心偏移
            double dx = mx - Mox;
            double dy = my - Moy;

            // ③ 应用方向取反开关（硬件轴镜像）
            double edx = InvertXAngle ? -dx : dx;
            double edy = InvertYAngle ? -dy : dy;

            // ④ 绕回转中心旋转（考虑 θ 取反开关）
            double effectiveAngle = InvertThetaAngle ? -rotationAngleDeg : rotationAngleDeg;
            double rad = effectiveAngle * Math.PI / 180.0;
            double cosT = Math.Cos(rad);
            double sinT = Math.Sin(rad);

            double newX = edx * cosT - edy * sinT + Mox;
            double newY = edx * sinT + edy * cosT + Moy;

            return (newX, newY);
        }

        #endregion
    }
}
