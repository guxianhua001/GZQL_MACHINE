using System;
using System.Collections.Generic;

namespace Core.Services
{
    /// <summary>
    /// 仿射标定结果模型——存储6个仿射参数和残差统计信息
    /// 仿射变换公式: Mx = A·Cx + B·Cy + Tx,  My = C·Cx + D·Cy + Ty
    /// </summary>
    public class AffineCalibrationResult
    {
        /// <summary>仿射参数 A (X输出对CAD_X的系数)</summary>
        public double A { get; set; }

        /// <summary>仿射参数 B (X输出对CAD_Y的系数)</summary>
        public double B { get; set; }

        /// <summary>仿射参数 C (Y输出对CAD_X的系数)</summary>
        public double C { get; set; }

        /// <summary>仿射参数 D (Y输出对CAD_Y的系数)</summary>
        public double D { get; set; }

        /// <summary>X方向平移量 Tx</summary>
        public double Tx { get; set; }

        /// <summary>Y方向平移量 Ty</summary>
        public double Ty { get; set; }

        /// <summary>均方根误差 RMS (mm)</summary>
        public double RmsError { get; set; }

        /// <summary>各标定点残差中的最大值 (mm)，用于评估最坏情况拟合偏差</summary>
        public double MaxResidual { get; set; }

        /// <summary>每个标定点的残差 (计算机械坐标 - 实际机械坐标 的欧氏距离)</summary>
        public List<double> Residuals { get; set; } = new List<double>();

        /// <summary>标定点数量</summary>
        public int PointCount { get; set; }

        /// <summary>
        /// 从仿射参数中提取等效旋转角度 (度)
        /// θ = atan2(C, A) —— 近似旋转角
        /// </summary>
        public double EquivalentRotationDeg
        {
            get
            {
                double rad = Math.Atan2(C, A);
                return rad * 180.0 / Math.PI;
            }
        }

        /// <summary>
        /// 从仿射参数中提取等效缩放因子
        /// scale = sqrt(A² + C²)  (X方向)
        /// </summary>
        public double EquivalentScaleX => Math.Sqrt(A * A + C * C);

        /// <summary>
        /// 从仿射参数中提取等效缩放因子
        /// scale = sqrt(B² + D²)  (Y方向)
        /// </summary>
        public double EquivalentScaleY => Math.Sqrt(B * B + D * D);

        /// <summary>质量评级文本</summary>
        public string QualityGrade
        {
            get
            {
                if (RmsError < 0.05) return "Good";
                if (RmsError < 0.10) return "Acceptable";
                return "Deviation too large";
            }
        }
    }

    /// <summary>
    /// N点仿射标定服务——使用最小二乘法求解 CAD坐标系 → 机械坐标系 的仿射变换参数
    /// 
    /// 仿射变换模型:
    ///   Mx = A·Cx + B·Cy + Tx
    ///   My = C·Cx + D·Cy + Ty
    /// 
    /// 其中 (Cx,Cy) 为CAD坐标, (Mx,My) 为机械坐标
    /// 6个参数 (A,B,C,D,Tx,Ty) 至少需要3对不共线的对应点求解
    /// 当点数>3时使用最小二乘法求最优解
    /// 
    /// 行业标准方法: SMT贴片机、CNC加工、AOI检测均使用此类标定方法
    /// </summary>
    public static class AffineCalibrationService
    {
        /// <summary>
        /// N点最小二乘仿射标定 (>=3点)
        /// </summary>
        /// <param name="cadPoints">CAD坐标列表 (Cx, Cy)</param>
        /// <param name="machinePoints">机械坐标列表 (Mx, My)，与cadPoints一一对应</param>
        /// <returns>仿射标定结果，包含6个参数和RMS误差</returns>
        /// <exception cref="ArgumentException">点数不足3个或数量不匹配时抛出</exception>
        public static AffineCalibrationResult Solve(
            List<(double Cx, double Cy)> cadPoints,
            List<(double Mx, double My)> machinePoints)
        {
            if (cadPoints == null || machinePoints == null)
                throw new ArgumentException("Input point lists cannot be null.");

            if (cadPoints.Count != machinePoints.Count)
                throw new ArgumentException("CAD and Machine point lists must have the same count.");

            int n = cadPoints.Count;
            if (n < 3)
                throw new ArgumentException("At least 3 point pairs are required for affine calibration.");

            // 分离 X 和 Y 方向的最小二乘问题:
            // X方向: Mx_i = A·Cx_i + B·Cy_i + Tx  →  矩阵方程: A_mat · [A, B, Tx]^T = b_x
            // Y方向: My_i = C·Cx_i + D·Cy_i + Ty  →  矩阵方程: A_mat · [C, D, Ty]^T = b_y
            //
            // A_mat (N×3): 每行 [Cx_i, Cy_i, 1]
            // 正规方程: (A_mat^T · A_mat) · params = A_mat^T · b

            // 构建 A^T·A (3×3 对称矩阵) 和 A^T·b_x, A^T·b_y
            double sCx = 0, sCy = 0, sCxCx = 0, sCyCy = 0, sCxCy = 0;
            double sMx = 0, sMy = 0, sCxMx = 0, sCyMx = 0, sCxMy = 0, sCyMy = 0;

            for (int i = 0; i < n; i++)
            {
                double cx = cadPoints[i].Cx;
                double cy = cadPoints[i].Cy;
                double mx = machinePoints[i].Mx;
                double my = machinePoints[i].My;

                sCx += cx;
                sCy += cy;
                sCxCx += cx * cx;
                sCyCy += cy * cy;
                sCxCy += cx * cy;
                sMx += mx;
                sMy += my;
                sCxMx += cx * mx;
                sCyMx += cy * mx;
                sCxMy += cx * my;
                sCyMy += cy * my;
            }

            // ATA = A^T · A (3×3 对称矩阵):
            // [ sCxCx, sCxCy, sCx ]
            // [ sCxCy, sCyCy, sCy ]
            // [ sCx,   sCy,   n   ]
            double[,] ATA = new double[3, 3]
            {
                { sCxCx, sCxCy, sCx },
                { sCxCy, sCyCy, sCy },
                { sCx,   sCy,   n   }
            };

            // ATbx = A^T · b_x (3×1)
            double[] ATbx = { sCxMx, sCyMx, sMx };

            // ATby = A^T · b_y (3×1)
            double[] ATby = { sCxMy, sCyMy, sMy };

            // 求解 3×3 线性方程组: ATA · x = ATbx 和  ATA · y = ATby
            double[] solX = SolveLinear3x3(ATA, ATbx);
            double[] solY = SolveLinear3x3(ATA, ATby);

            // 提取仿射参数
            var result = new AffineCalibrationResult
            {
                A = solX[0],   // Mx 对 Cx 的系数
                B = solX[1],   // Mx 对 Cy 的系数
                Tx = solX[2],  // X方向平移
                C = solY[0],   // My 对 Cx 的系数
                D = solY[1],   // My 对 Cy 的系数
                Ty = solY[2],  // Y方向平移
                PointCount = n
            };

            // 计算每个点的残差、最大残差和 RMS
            // 注意：恰好 3 点时方程组恰定（6 未知数 / 6 方程），残差恒为 0，RMS=0 属正常现象；
            // 需 >=4 个标定点才有冗余约束，RMS 才能反映拟合误差。
            double sumResidualSq = 0;
            double maxResidual = 0;
            for (int i = 0; i < n; i++)
            {
                var (calcMx, calcMy) = Transform(result, cadPoints[i].Cx, cadPoints[i].Cy);
                double resX = calcMx - machinePoints[i].Mx;
                double resY = calcMy - machinePoints[i].My;
                double residual = Math.Sqrt(resX * resX + resY * resY);
                result.Residuals.Add(Math.Round(residual, 6));
                sumResidualSq += resX * resX + resY * resY;
                if (residual > maxResidual) maxResidual = residual;
            }

            // RMS = sqrt( Σ(Δx²+Δy²) / N )，即各点欧氏残差的均方根
            result.RmsError = Math.Round(Math.Sqrt(sumResidualSq / n), 6);
            result.MaxResidual = Math.Round(maxResidual, 6);

            return result;
        }

        /// <summary>
        /// 用仿射参数变换单个CAD点 → 机械坐标
        /// Mx = A·Cx + B·Cy + Tx
        /// My = C·Cx + D·Cy + Ty
        /// </summary>
        /// <param name="calib">仿射标定结果</param>
        /// <param name="cadX">CAD X坐标</param>
        /// <param name="cadY">CAD Y坐标</param>
        /// <returns>机械坐标 (Mx, My)</returns>
        public static (double Mx, double My) Transform(AffineCalibrationResult calib, double cadX, double cadY)
        {
            if (calib == null) throw new ArgumentNullException(nameof(calib));

            double mx = calib.A * cadX + calib.B * cadY + calib.Tx;
            double my = calib.C * cadX + calib.D * cadY + calib.Ty;
            return (mx, my);
        }

        /// <summary>
        /// 仿射逆变换——由机械坐标反算CAD/像素坐标 (Mx,My) → (Cx,Cy)。
        /// 用于"机械坐标系→图像像素坐标系"场景（例如ZMAP高度图标定后，按机械XY反查对应像素位置）。
        /// 正变换: Mx=A·Cx+B·Cy+Tx, My=C·Cx+D·Cy+Ty；
        /// 逆变换通过对2×2线性部分矩阵[[A,B],[C,D]]求逆，再减去平移量得到。
        /// </summary>
        /// <param name="calib">仿射标定结果（正变换：Cx,Cy → Mx,My）</param>
        /// <param name="mx">机械坐标X</param>
        /// <param name="my">机械坐标Y</param>
        /// <returns>反算得到的 (Cx, Cy)</returns>
        /// <exception cref="InvalidOperationException">仿射矩阵奇异（不可逆）时抛出</exception>
        public static (double Cx, double Cy) InverseTransform(AffineCalibrationResult calib, double mx, double my)
        {
            if (calib == null) throw new ArgumentNullException(nameof(calib));

            double det = calib.A * calib.D - calib.B * calib.C;
            if (Math.Abs(det) < 1e-12)
                throw new InvalidOperationException("Affine calibration is singular and cannot be inverted.");

            double dx = mx - calib.Tx;
            double dy = my - calib.Ty;
            double cx = (calib.D * dx - calib.B * dy) / det;
            double cy = (calib.A * dy - calib.C * dx) / det;
            return (cx, cy);
        }

        /// <summary>
        /// 求解3×3对称线性方程组 M·x = b (使用Cramer法则)
        /// M 为 3×3 矩阵, b 为 3×1 向量
        /// 返回解向量 x
        /// </summary>
        private static double[] SolveLinear3x3(double[,] M, double[] b)
        {
            // 计算行列式 det(M)
            double det = Determinant3x3(M);

            if (Math.Abs(det) < 1e-12)
                throw new InvalidOperationException(
                    "Affine calibration failed: singular matrix. Check that points are not collinear.");

            double invDet = 1.0 / det;

            // Cramer法则: x_i = det(M_i) / det(M)
            // M_0: 用 b 替换第0列
            double det0 = b[0] * (M[1, 1] * M[2, 2] - M[1, 2] * M[2, 1])
                        - M[0, 1] * (b[1] * M[2, 2] - M[1, 2] * b[2])
                        + M[0, 2] * (b[1] * M[2, 1] - M[1, 1] * b[2]);

            // M_1: 用 b 替换第1列
            double det1 = M[0, 0] * (b[1] * M[2, 2] - M[1, 2] * b[2])
                        - b[0] * (M[1, 0] * M[2, 2] - M[1, 2] * M[2, 0])
                        + M[0, 2] * (M[1, 0] * b[2] - b[1] * M[2, 0]);

            // M_2: 用 b 替换第2列
            double det2 = M[0, 0] * (M[1, 1] * b[2] - b[1] * M[2, 1])
                        - M[0, 1] * (M[1, 0] * b[2] - b[1] * M[2, 0])
                        + b[0] * (M[1, 0] * M[2, 1] - M[1, 1] * M[2, 0]);

            return new double[] { det0 * invDet, det1 * invDet, det2 * invDet };
        }

        /// <summary>
        /// 计算3×3矩阵的行列式
        /// </summary>
        private static double Determinant3x3(double[,] M)
        {
            return M[0, 0] * (M[1, 1] * M[2, 2] - M[1, 2] * M[2, 1])
                 - M[0, 1] * (M[1, 0] * M[2, 2] - M[1, 2] * M[2, 0])
                 + M[0, 2] * (M[1, 0] * M[2, 1] - M[1, 1] * M[2, 0]);
        }
    }
}
