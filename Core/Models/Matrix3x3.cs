using System;

namespace Core.Models
{
    public struct Matrix3x3
    {
        public double M11, M12, M13;
        public double M21, M22, M23;
        public double M31, M32, M33;

        public Matrix3x3(
            double m11, double m12, double m13,
            double m21, double m22, double m23,
            double m31, double m32, double m33)
        {
            M11 = m11; M12 = m12; M13 = m13;
            M21 = m21; M22 = m22; M23 = m23;
            M31 = m31; M32 = m32; M33 = m33;
        }

        // 单位矩阵
        public static Matrix3x3 Identity => new Matrix3x3(1, 0, 0, 0, 1, 0, 0, 0, 1);

        // 矩阵乘法
        public static Matrix3x3 operator *(Matrix3x3 a, Matrix3x3 b)
        {
            return new Matrix3x3(
                a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31,
                a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32,
                a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33,
                a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31,
                a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32,
                a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33,
                a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31,
                a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32,
                a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33
            );
        }

        // 转置
        public Matrix3x3 Transpose() => new Matrix3x3(
            M11, M21, M31,
            M12, M22, M32,
            M13, M23, M33
        );

        // 从旋转角度（绕Z轴）创建旋转矩阵（用于2D旋转）
        public static Matrix3x3 RotateZ(double angleDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            double cos = Math.Cos(rad), sin = Math.Sin(rad);
            return new Matrix3x3(
                cos, -sin, 0,
                sin, cos, 0,
                0, 0, 1
            );
        }
    }
}