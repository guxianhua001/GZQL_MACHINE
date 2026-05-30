// Core/Services/CoordinateAlignService.cs
using Core.Models;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Services
{
    /// <summary>
    /// 坐标对齐服务实现——管理CAD坐标系到机械坐标系的映射转换，
    /// 内部维护基准点信息、变换矩阵、注册点集及逐点映射表
    /// </summary>
    public class CoordinateAlignService : ICoordinateAlignService
    {
        #region 私有字段

        /// <summary>当前对齐模式</summary>
        private AlignMode _mode = AlignMode.FirstPoint;

        /// <summary>CAD图纸中的基准点（Mark/Fiducial）</summary>
        private CadPoint _mapFiducial = new CadPoint();

        /// <summary>机械坐标系下的基准点X</summary>
        private double _machineFiducialX;

        /// <summary>机械坐标系下的基准点Y</summary>
        private double _machineFiducialY;

        /// <summary>机械坐标系下的基准点Z</summary>
        private double _machineFiducialZ;

        /// <summary>机械基准点绕X轴旋转角度（度数）</summary>
        private double _machineFiducialRx;

        /// <summary>机械基准点绕Z轴旋转角度（度数）</summary>
        private double _machineFiducialRz;

        /// <summary>已注册的需要参与批量变换的点集引用列表</summary>
        private readonly List<CadPoint> _registeredPoints = new List<CadPoint>();

        /// <summary>Mode2使用的逐点映射字典：pointId → (mx, my, mz)</summary>
        private readonly Dictionary<string, (double mx, double my, double mz)> _pointMappings =
            new Dictionary<string, (double, double, double)>();

        /// <summary>当前生效的坐标变换对象</summary>
        private CoordinateTransform _transform = new CoordinateTransform();

        /// <summary>方向点距离（仿射模式下自动生成虚拟方向点B的偏移距离）</summary>
        private double _directionLength = 100.0;

        /// <summary>Halcon 仿射矩阵（仿射模式下的计算结果，延迟加载避免类级别Halcon依赖）</summary>
        private object _affineMatrix;

        #endregion

        #region 公共属性

        /// <summary>当前对齐模式（只读）</summary>
        public AlignMode CurrentMode => _mode;

        #endregion

        #region 模式与基准点配置

        /// <summary>
        /// 设置对齐模式（FirstPoint 或 AllPoints）
        /// 切换时不清除已有数据，允许动态切换
        /// </summary>
        public void SetMode(AlignMode mode)
        {
            _mode = mode;
        }

        /// <summary>设置CAD图纸中的基准点坐标</summary>
        public void SetMapFiducial(double x, double y, double z)
        {
            _mapFiducial = new CadPoint(x, y, z);
        }

        /// <summary>设置机械坐标系下的基准点坐标及旋转量</summary>
        public void SetMachineFiducial(double x, double y, double z, double rx, double rz)
        {
            _machineFiducialX = x;
            _machineFiducialY = y;
            _machineFiducialZ = z;
            _machineFiducialRx = rx;
            _machineFiducialRz = rz;
        }

        #endregion

        #region Mode1: 基准点偏移自动计算

        /// <summary>
        /// Mode1自动计算——基于CAD基准点与机械基准点的偏移量构建纯平移变换矩阵，
        /// 并对所有已注册的CadPoint执行坐标转换，结果写入各点的MachineX/Y/Z属性
        /// 
        /// 计算公式：
        ///   Tx = machineFiducial.X - mapFiducial.X
        ///   Ty = machineFiducial.Y - mapFiducial.Y
        ///   Tz = machineFiducial.Z - mapFiducial.Z
        /// </summary>
        public void AutoCalculate()
        {
            // 计算平移偏移量：机械基准点 - CAD基准点
            double tx = _machineFiducialX - _mapFiducial.X;
            double ty = _machineFiducialY - _mapFiducial.Y;
            double tz = _machineFiducialZ - _mapFiducial.Z;

            // 构建纯平移变换（无旋转、缩放因子为1）
            _transform = new CoordinateTransform(tx, ty, tz, 0, 1.0);

            // 遍历所有已注册点，逐一执行变换并回写Machine坐标
            foreach (var point in _registeredPoints)
            {
                var transformed = _transform.Transform(point);
                point.MachineX = Math.Round(transformed.MachineX ?? 0, 3);
                point.MachineY = Math.Round(transformed.MachineY ?? 0, 3);
                point.MachineZ = Math.Round(transformed.MachineZ ?? 0, 3);
            }
        }

        /// <summary>
        /// 注册需要参与Mode1批量坐标变换的点集
        /// 存储的是对象的直接引用，后续AutoCalculate()会就地修改其Machine*属性
        /// </summary>
        /// <param name="cadPoints">待注册的CAD点集合</param>
        public void RegisterPoints(IEnumerable<CadPoint> cadPoints)
        {
            if (cadPoints == null)
                return;

            // 先清空再重新填充，确保注册列表与传入数据一致
            _registeredPoints.Clear();
            _registeredPoints.AddRange(cadPoints);
        }

        #endregion

        #region Mode2: 逐点映射

        /// <summary>
        /// Mode2逐点映射——为指定ID的点手动设置其对应的机械坐标
        /// 后续TransformToMachine()会从此映射表中查找并返回结果
        /// </summary>
        /// <param name="pointId">点的唯一标识（对应CadPoint.Id）</param>
        /// <param name="mx">目标机械X坐标</param>
        /// <param name="my">目标机械Y坐标</param>
        /// <param name="mz">目标机械Z坐标</param>
        public void SetPointMapping(string pointId, double mx, double my, double mz)
        {
            _pointMappings[pointId] = (Math.Round(mx, 3), Math.Round(my, 3), Math.Round(mz, 3));
        }

        #endregion

        #region 坐标转换

        /// <summary>
        /// 将单个CAD坐标点转换为机械坐标点：
        /// - Mode1: 使用内部_transform变换矩阵执行仿射变换
        /// - Mode2: 在_pointMappings映射表中通过pointId查找，返回映射结果
        /// 
        /// 注意：此方法返回新的CadPoint副本，不修改原始输入对象
        /// </summary>
        /// <param name="cadPoint">输入的CAD坐标点</param>
        /// <returns>包含机械坐标的新CadPoint实例</returns>
        public CadPoint TransformToMachine(CadPoint cadPoint)
        {
            if (cadPoint == null)
                throw new ArgumentNullException(nameof(cadPoint));

            if (_mode == AlignMode.FirstPoint)
            {
                // Mode1: 使用内部变换矩阵计算
                var result = _transform.Transform(cadPoint);
                return new CadPoint(result.X, result.Y, result.Z, result.Id, result.AssySite, result.Name)
                {
                    MachineX = Math.Round(result.MachineX ?? 0, 4),
                    MachineY = Math.Round(result.MachineY ?? 0, 4),
                    MachineZ = Math.Round(result.MachineZ ?? 0, 4)
                };
            }
            else
            {
                // Mode2: 在映射表中按pointId查找
                string key = cadPoint.Id;
                if (_pointMappings.TryGetValue(key, out var mapping))
                {
                    return new CadPoint(cadPoint.X, cadPoint.Y, cadPoint.Z, cadPoint.Id, cadPoint.AssySite, cadPoint.Name)
                    {
                        MachineX = mapping.mx,
                        MachineY = mapping.my,
                        MachineZ = mapping.mz
                    };
                }

                // 映射表中未找到匹配项，尝试最近点匹配作为兜底策略
                var nearest = FindNearestMappedPoint(cadPoint);
                if (nearest.HasValue)
                {
                    return new CadPoint(cadPoint.X, cadPoint.Y, cadPoint.Z, cadPoint.Id, cadPoint.AssySite, cadPoint.Name)
                    {
                        MachineX = nearest.Value.mx,
                        MachineY = nearest.Value.my,
                        MachineZ = nearest.Value.mz
                    };
                }

                // 完全无匹配时返回原点（Machine坐标全为null表示未对齐）
                return new CadPoint(cadPoint.X, cadPoint.Y, cadPoint.Z, cadPoint.Id, cadPoint.AssySite, cadPoint.Name);
            }
        }

        /// <summary>
        /// 在Mode2映射表中寻找与目标CAD点欧氏距离最近的映射条目（兜底匹配策略）
        /// </summary>
        /// <param name="target">目标CAD点</param>
        /// <returns>最近的映射元组；若无任何映射则返回null</returns>
        private (double mx, double my, double mz)? FindNearestMappedPoint(CadPoint target)
        {
            if (_pointMappings.Count == 0)
                return null;

            string nearestKey = null;
            double minDist = double.MaxValue;

            // 遍历所有映射key，找到CAD坐标空间中距离target最近的那个
            foreach (var key in _pointMappings.Keys)
            {
                // 由于映射表只存了机械坐标，这里用key本身做近似匹配
                // 实际场景建议扩展映射结构同时存储原始CAD坐标用于距离计算
                // 此处采用简单的字符串相似度+顺序匹配策略
                double dist = StringDistance(target.Id ?? "", key ?? "");
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestKey = key;
                }
            }

            return nearestKey != null ? _pointMappings[nearestKey] : null;
        }

        /// <summary>
        /// 简单的字符串编辑距离（用于Mode2兜底匹配时的近似比较）
        /// </summary>
        private static double StringDistance(string a, string b)
        {
            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 0;
            if (string.IsNullOrEmpty(a)) return b.Length;
            if (string.IsNullOrEmpty(b)) return a.Length;

            int lenA = a.Length, lenB = b.Length;
            var dp = new int[lenA + 1, lenB + 1];
            for (int i = 0; i <= lenA; i++) dp[i, 0] = i;
            for (int j = 0; j <= lenB; j++) dp[0, j] = j;

            for (int i = 1; i <= lenA; i++)
            {
                for (int j = 1; j <= lenB; j++)
                {
                    int cost = (a[i - 1] == b[j - 1]) ? 0 : 1;
                    dp[i, j] = Math.Min(
                        Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                        dp[i - 1, j - 1] + cost
                    );
                }
            }
            return dp[lenA, lenB];
        }

        #endregion

        #region 变换查询

        /// <summary>获取当前生效的坐标变换对象（含Tx/Ty/Tz/Rotation/Scale参数）</summary>
        public CoordinateTransform GetTransform()
        {
            return _transform;
        }

        #endregion

        #region Mode3: 仿射对齐

        /// <summary>设置方向点距离（仿射模式下自动生成虚拟方向点B的偏移距离）</summary>
        public void SetDirectionLength(double length)
        {
            _directionLength = length > 0 ? length : 100.0;
        }

        /// <summary>
        /// 仿射模式自动计算——自动生成虚拟方向点B，使用Halcon VectorToHomMat2D计算仿射矩阵
        /// 图纸端：B = A + (DirectionLength, 0)
        /// 机械端：B = A + (DirectionLength, 0)
        /// 支持平移+旋转+缩放，特别适合圆弧轨迹
        /// </summary>
        public void AutoCalculateAffine()
        {
            // 图纸端基准点A
            double cadAx = _mapFiducial.X;
            double cadAy = _mapFiducial.Y;
            // 图纸端虚拟方向点B（沿X轴偏移）
            double cadBx = cadAx + _directionLength;
            double cadBy = cadAy;

            // 机械端基准点A
            double machineAx = _machineFiducialX;
            double machineAy = _machineFiducialY;
            // 机械端虚拟方向点B（沿X轴偏移）
            double machineBx = machineAx + _directionLength;
            double machineBy = machineAy;

            try
            {
                // 使用HHomMat2D.VectorToHomMat2d计算仿射矩阵
                // 注意：Halcon使用行列坐标系，row=Y, col=X
                HTuple px = new HTuple(cadAx, cadBx);   // 原始 X
                HTuple py = new HTuple(cadAy, cadBy);   // 原始 Y
                HTuple qx = new HTuple(machineAx, machineBx); // 目标 X
                HTuple qy = new HTuple(machineAy, machineBy); // 目标 Y

                var affineMat = new HalconDotNet.HHomMat2D();
                affineMat.VectorToRigid(px, py, qx, qy);
                _affineMatrix = affineMat;

                // 遍历所有注册点，使用仿射矩阵转换坐标
                foreach (var point in _registeredPoints)
                {
                    try
                    {
                        // AffineTransPoint2d(row, col, out colOut) 返回 rowOut
                        double xOut = affineMat.AffineTransPoint2d(point.X, point.Y, out double yOut);
                        point.MachineX = Math.Round(xOut, 3);
                        point.MachineY = Math.Round(yOut, 3);
                        point.MachineZ = Math.Round(point.Z + (_machineFiducialZ - _mapFiducial.Z), 3);
                    }
                    catch
                    {
                        point.MachineX = point.X;
                        point.MachineY = point.Y;
                        point.MachineZ = point.Z;
                    }
                }

                // 同时更新 CoordinateTransform（用于兼容性查询）
                HalconDotNet.HTuple matTuple = affineMat;
                double tx = matTuple[0].D;
                double ty = matTuple[3].D;
                _transform = new CoordinateTransform(ty, tx, _machineFiducialZ - _mapFiducial.Z, 0, 1.0);
            }
            catch (Exception ex)
            {
                // Halcon不可用时回退到纯平移
                System.Diagnostics.Debug.WriteLine($"[CoordinateAlignService] 仿射计算失败，回退到纯平移: {ex.Message}");
                double tx = _machineFiducialX - _mapFiducial.X;
                double ty = _machineFiducialY - _mapFiducial.Y;
                double tz = _machineFiducialZ - _mapFiducial.Z;
                _transform = new CoordinateTransform(tx, ty, tz, 0, 1.0);

                // 标记为纯平移回退，避免 GetAffineMatrixDisplay() 返回"未计算"
                _affineMatrix = $"回退纯平移: Tx={tx:F2} Ty={ty:F2}";

                foreach (var point in _registeredPoints)
                {
                    point.MachineX = Math.Round(point.X + tx, 3);
                    point.MachineY = Math.Round(point.Y + ty, 3);
                    point.MachineZ = Math.Round(point.Z + tz, 3);
                }
            }
        }

        /// <summary>获取仿射矩阵参数文本（用于UI显示）</summary>
        public string GetAffineMatrixDisplay()
        {
            if (_affineMatrix == null)
                return "未计算";

            // 回退纯平移的情况
            if (_affineMatrix is string fallbackText)
                return fallbackText;

            try
            {
                HHomMat2D mat = (HHomMat2D)_affineMatrix;

                // Halcon 正确获取矩阵值的方式：直接用索引
                double scaleX = mat[0];   // [0]
                double rot1 = mat[1];    // [1]
                double tx = mat[2];    // [2] → 真正的 X 偏移
                double rot2 = mat[3];    // [3]
                double scaleY = mat[4];    // [4]
                double ty = mat[5];    // [5] → 真正的 Y 偏移

                return $"Tx={tx:F2} Ty={ty:F2} (仿射矩阵)";
            }
            catch
            {
                return "仿射矩阵读取失败";
            }
        }
        #endregion
    }
}
