// Core/Models/CoordinateTransform.cs
using System;

namespace Core.Models
{
    /// <summary>
    /// 坐标变换模型——封装仿射变换参数（平移+旋转+缩放），
    /// 用于 CAD 坐标系与机械坐标系之间的双向转换
    /// </summary>
    public class CoordinateTransform
    {
        #region 变换参数

        /// <summary>X方向平移量（mm）</summary>
        public double Tx { get; set; }

        /// <summary>Y方向平移量（mm）</summary>
        public double Ty { get; set; }

        /// <summary>Z方向平移量（mm）</summary>
        public double Tz { get; set; }

        /// <summary>旋转角度（度数，绕Z轴旋转）</summary>
        public double RotationAngle { get; set; }

        /// <summary>缩放因子（默认 1.0 表示不缩放）</summary>
        public double Scale { get; set; } = 1.0;

        #endregion

        #region 构造函数

        /// <summary>无参构造：初始化为单位变换（全零平移、零旋转、缩放1）</summary>
        public CoordinateTransform() { }

        /// <summary>含参构造：一次性设置所有变换参数</summary>
        public CoordinateTransform(double tx, double ty, double tz, double rotationAngleDeg, double scale = 1.0)
        {
            Tx = tx;
            Ty = ty;
            Tz = tz;
            RotationAngle = rotationAngleDeg;
            Scale = scale;
        }

        /// <summary>
        /// 从偏移量创建纯平移变换（无旋转、无缩放）的静态工厂方法
        /// </summary>
        /// <param name="tx">X偏移</param>
        /// <param name="ty">Y偏移</param>
        /// <param name="tz">Z偏移</param>
        /// <returns>仅包含平移分量的坐标变换实例</returns>
        public static CoordinateTransform FromTranslation(double tx, double ty, double tz = 0)
        {
            return new CoordinateTransform(tx, ty, tz, 0, 1.0);
        }

        #endregion

        #region 正变换：CAD → 机械坐标

        /// <summary>
        /// 对输入的 CAD 坐标点应用仿射变换，输出机械坐标点
        /// 变换顺序：缩放 → 旋转 → 平移
        /// </summary>
        /// <param name="input">CAD坐标系下的输入点</param>
        /// <returns>变换后的机械坐标点（MachineX/Y/Z 已填充）</summary>
        public CadPoint Transform(CadPoint input)
        {
            // 将角度转换为弧度
            double rad = RotationAngle * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            // 第一步：缩放
            double sx = input.X * Scale;
            double sy = input.Y * Scale;
            double sz = input.Z * Scale;

            // 第二步：绕Z轴旋转（2D旋转，Z不变）
            double rx = sx * cos - sy * sin;
            double ry = sx * sin + sy * cos;
            double rz = sz;

            // 第三步：平移
            double mx = rx + Tx;
            double my = ry + Ty;
            double mz = rz + Tz;

            // 构造结果点，保留原始CAD坐标并填充机械坐标
            var result = new CadPoint(input.X, input.Y, input.Z, input.Id, input.AssySite, input.Name)
            {
                MachineX = mx,
                MachineY = my,
                MachineZ = mz
            };
            return result;
        }

        #endregion

        #region 逆变换：机械坐标 → CAD

        /// <summary>
        /// 对机械坐标点执行逆变换，还原为 CAD 坐标点
        /// 逆变换顺序：逆平移 → 逆旋转 → 逆缩放
        /// </summary>
        /// <param name="machinePt">机械坐标系下的输入点（需有 MachineX/Y/Z 值）</param>
        /// <returns>逆变换还原后的CAD坐标点</summary>
        public CadPoint InverseTransform(CadPoint machinePt)
        {
            // 取机械坐标作为输入（优先使用Machine*属性，回退到XYZ）
            double mx = machinePt.MachineX ?? machinePt.X;
            double my = machinePt.MachineY ?? machinePt.Y;
            double mz = machinePt.MachineZ ?? machinePt.Z;

            // 第一步：逆平移
            double ix = mx - Tx;
            double iy = my - Ty;
            double iz = mz - Tz;

            // 第二步：逆旋转（绕Z轴逆向旋转，即取负角度）
            double rad = -RotationAngle * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            double rx = ix * cos - iy * sin;
            double ry = ix * sin + iy * cos;
            double rz = iz;

            // 第三步：逆缩放
            double cx = rx / Scale;
            double cy = ry / Scale;
            double cz = rz / Scale;

            return new CadPoint(cx, cy, cz, machinePt.Id, machinePt.AssySite, machinePt.Name);
        }

        #endregion

        #region 变换矩阵构建

        /// <summary>
        /// 构建 3×3 仿射变换矩阵（齐次坐标形式，用于组合多个变换）
        /// 矩阵布局（行主序）：
        /// [ cosθ·S  -sinθ·S   Tx ]
        /// [ sinθ·S   cosθ·S   Ty ]
        /// [   0        0      Tz/S ]
        /// 其中 S=Scale, θ=RotationAngle
        /// 注：Z方向仅做平移和缩放，不参与2D旋转
        /// </summary>
        /// <returns>3x3变换矩阵</returns>
        public Matrix3x3 BuildTransformMatrix()
        {
            double rad = RotationAngle * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            double s = Scale;

            return new Matrix3x3(
                cos * s, -sin * s, Tx,
                sin * s,  cos * s, Ty,
                0,       0,       Tz + (1 - s) * 0  // Z方向平移+缩放基准补偿
            );
        }

        /// <summary>
        /// 构建逆变换矩阵（用于从机械坐标还原CAD坐标）
        /// </summary>
        /// <returns>3x3逆变换矩阵</returns>
        public Matrix3x3 BuildInverseMatrix()
        {
            double rad = -RotationAngle * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            double invS = 1.0 / Scale;

            return new Matrix3x3(
                cos * invS, -sin * invS, -Tx * invS,
                sin * invS,  cos * invS, -Ty * invS,
                0,          0,           (-Tz) * invS + 1
            );
        }

        #endregion
    }
}
