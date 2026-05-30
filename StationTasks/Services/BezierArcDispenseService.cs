using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Utilities;
using MotionControl.Exceptions;
using MotionControl.Interfaces;
using Recipe.Interfaces;

namespace StationTasks.Services
{
    public class CoordinateTransformDetail
    {
        public double PhotoDx { get; set; }
        public double PhotoDy { get; set; }
        public double DeltaToCenterX { get; set; }
        public double DeltaToCenterY { get; set; }
        public double CameraNeedleDistanceX { get; set; }
        public double CameraNeedleDistanceY { get; set; }
        public double TargetOffsetX { get; set; }
        public double TargetOffsetY { get; set; }
        public double NeedleOffsetX { get; set; }
        public double NeedleOffsetY { get; set; }
        public double NeedleCompensationX { get; set; }
        public double NeedleCompensationY { get; set; }
        public double FinalX { get; set; }
        public double FinalY { get; set; }
    }

    public class BezierArcDispenseService
    {
        private readonly IMotionService _motionService;
        private readonly IRecipePoolService _recipePoolService;
        private readonly ILoggerService _logger;

        public BezierArcDispenseService(IMotionService motionService, IRecipePoolService recipePoolService, ILoggerService logger)
        {
            _motionService = motionService;
            _recipePoolService = recipePoolService;
            _logger = logger;
        }

        #region 静态计算方法

        /// <summary>
        /// 计算单点的机械坐标
        /// 公式: Mech = PhotoPos + VisionDelta + CameraNeedleDistance + NeedleOffset + NeedleCompensation
        /// </summary>
        public static (double X, double Y) ComputeMachineCoordinate(
            (double Dx, double Dy) photoPosition,
            (double X, double Y) visionPoint,
            (double X, double Y) visionCenter,
            (double X, double Y) cameraNeedleDistance,
            (double X, double Y) needleOffset,
            (double X, double Y) needleCompensation)
        {
            double visionDeltaX = visionPoint.X - visionCenter.X;
            double visionDeltaY = visionPoint.Y - visionCenter.Y;

            return ComputeMachineCoordinateFromOffset(
                photoPosition,
                (visionDeltaX, visionDeltaY),
                cameraNeedleDistance,
                needleOffset,
                needleCompensation);
        }

        /// <summary>
        /// 按已计算好的目标偏移计算机械坐标。
        /// 公式: 机械坐标 = 相机拍照位 + 目标偏移 + 相机针头固定距离 + 针头偏移 + 手动补偿
        /// </summary>
        public static (double X, double Y) ComputeMachineCoordinateFromOffset(
            (double Dx, double Dy) photoPosition,
            (double X, double Y) targetOffset,
            (double X, double Y) cameraNeedleDistance,
            (double X, double Y) needleOffset,
            (double X, double Y) needleCompensation)
        {
            double mechX = photoPosition.Dx + targetOffset.X + cameraNeedleDistance.X + needleOffset.X + needleCompensation.X;
            double mechY = photoPosition.Dy + targetOffset.Y + cameraNeedleDistance.Y + needleOffset.Y + needleCompensation.Y;

            return (mechX, mechY);
        }

        /// <summary>
        /// 兼容旧调用：needleOffset 包含所有偏移分量之和
        /// </summary>
        public static (double X, double Y) ComputeMachineCoordinate(
            (double Dx, double Dy) photoPosition,
            (double X, double Y) visionPoint,
            (double X, double Y) visionCenter,
            (double X, double Y) needleOffset)
        {
            return ComputeMachineCoordinate(photoPosition, visionPoint, visionCenter, (0, 0), needleOffset, (0, 0));
        }

        /// <summary>
        /// 生成Arc模式的贝塞尔离散机械坐标点
        /// 视觉系统返回的 P1/P2/P3 是9点标定后的机械坐标
        /// 公式：Mech_n = PhotoPos + ( Center - P_n) + CamToNeedle + NeedleOffset + NeedleComp）
        /// </summary>
        public static List<(double X, double Y)> GenerateArcMachinePoints(
            (double Dx, double Dy) photoPosition,
            (double X, double Y) center,
            (double X, double Y) p1, (double X, double Y) p2, (double X, double Y) p3,
            (double X, double Y) cameraNeedleDistance,
            (double X, double Y) needleOffset,
            (double X, double Y) needleCompensation,
            int segmentCount)
        {
            // P_n 是9点标定后的机械坐标，(拍照位 + P_n到相机中心偏移 叠加相机到针头距离 + 偏移 + 补偿
            static (double X, double Y) ApplyOffset(
                (double Dx, double Dy) photo,
                (double X, double Y) ctr,
                (double X, double Y) p,
                (double X, double Y) cam, (double X, double Y) off, (double X, double Y) comp)
                => (photo.Dx + (ctr.X - p.X) + cam.X + off.X + comp.X,
                    photo.Dy + (ctr.Y - p.Y) + cam.Y + off.Y + comp.Y);

            var mechP1 = ApplyOffset(photoPosition, center, p1, cameraNeedleDistance, needleOffset, needleCompensation);
            var mechP2 = ApplyOffset(photoPosition, center, p2, cameraNeedleDistance, needleOffset, needleCompensation);
            var mechP3 = ApplyOffset(photoPosition, center, p3, cameraNeedleDistance, needleOffset, needleCompensation);

            return DiscretizeQuadraticBezier(mechP1, mechP2, mechP3, segmentCount);
        }


        /// <summary>
        /// 二阶贝塞尔曲线离散化: B(t) = (1-t)²P0 + 2(1-t)t·P1 + t²P2
        /// </summary>
        public static List<(double X, double Y)> DiscretizeQuadraticBezier(
            (double X, double Y) p0, (double X, double Y) p1, (double X, double Y) p2,
            int segments)
        {
            var points = new List<(double X, double Y)>();
            for (int i = 0; i <= segments; i++)
            {
                double t = (double)i / segments;
                double mt = 1.0 - t;

                double x = mt * mt * p0.X + 2 * mt * t * p1.X + t * t * p2.X;
                double y = mt * mt * p0.Y + 2 * mt * t * p1.Y + t * t * p2.Y;

                points.Add((x, y));
            }
            return points;
        }

        #endregion

        #region 实例方法

        public List<(double X, double Y)> DiscretizeBezier(double p0x, double p0y, double p1x, double p1y, double p2x, double p2y, int segments)
        {
            return DiscretizeQuadraticBezier((p0x, p0y), (p1x, p1y), (p2x, p2y), segments);
        }

        public CoordinateTransformDetail TransformVisionToMachine(
            double photoDx, double photoDy,
            double pointX, double pointY,
            double centerX, double centerY,
            double cameraNeedleDistanceX, double cameraNeedleDistanceY,
            double needleOffsetX, double needleOffsetY,
            double needleCompensationX, double needleCompensationY)
        {
            var (finalX, finalY) = ComputeMachineCoordinate(
                (photoDx, photoDy),
                (pointX, pointY),
                (centerX, centerY),
                (cameraNeedleDistanceX, cameraNeedleDistanceY),
                (needleOffsetX, needleOffsetY),
                (needleCompensationX, needleCompensationY));

            return new CoordinateTransformDetail
            {
                PhotoDx = photoDx, PhotoDy = photoDy,
                DeltaToCenterX = pointX - centerX, DeltaToCenterY = pointY - centerY,
                CameraNeedleDistanceX = cameraNeedleDistanceX, CameraNeedleDistanceY = cameraNeedleDistanceY,
                NeedleOffsetX = needleOffsetX, NeedleOffsetY = needleOffsetY,
                NeedleCompensationX = needleCompensationX, NeedleCompensationY = needleCompensationY,
                FinalX = finalX, FinalY = finalY
            };
        }

        /// <summary>
        /// 计算机械坐标点列表（预览用），接受完整偏移参数
        /// </summary>
        public Task<List<CoordinateTransformDetail>> ComputeMachinePointsAsync(
            Dictionary<string, double> visionData,
            double photoDx, double photoDy,
            bool isArc,
            int arcSegments,
            double cameraNeedleDistanceX, double cameraNeedleDistanceY,
            double targetOffsetX, double targetOffsetY,
            double needleOffsetX, double needleOffsetY,
            double needleCompensationX, double needleCompensationY)
        {
            (double X, double Y) targetOffset = (targetOffsetX, targetOffsetY);
            (double X, double Y) cameraNeedleDistance = (cameraNeedleDistanceX, cameraNeedleDistanceY);
            (double X, double Y) needleOffset = (needleOffsetX, needleOffsetY);
            (double X, double Y) needleCompensation = (needleCompensationX, needleCompensationY);

            if (!isArc)
            {
                var (finalX, finalY) = ComputeMachineCoordinateFromOffset(
                    (photoDx, photoDy), targetOffset,
                    cameraNeedleDistance, needleOffset, needleCompensation);

                return Task.FromResult(new List<CoordinateTransformDetail>
                {
                    new CoordinateTransformDetail
                    {
                        PhotoDx = photoDx, PhotoDy = photoDy,
                        DeltaToCenterX = targetOffset.X, DeltaToCenterY = targetOffset.Y,
                        TargetOffsetX = targetOffset.X, TargetOffsetY = targetOffset.Y,
                        CameraNeedleDistanceX = cameraNeedleDistance.X, CameraNeedleDistanceY = cameraNeedleDistance.Y,
                        NeedleOffsetX = needleOffset.X, NeedleOffsetY = needleOffset.Y,
                        NeedleCompensationX = needleCompensation.X, NeedleCompensationY = needleCompensation.Y,
                        FinalX = finalX, FinalY = finalY
                    }
                });
            }
            else
            {
                var (centerX, centerY, p1x, p1y, p2x, p2y, p3x, p3y) = ExtractArcPoints(visionData);

                // 公式：Mech_n = PhotoPos + (Center - P_n) + CamToNeedle + NeedleOffset + NeedleComp
                var bezierPoints = GenerateArcMachinePoints(
                    (photoDx, photoDy), (centerX, centerY),
                    (p1x, p1y), (p2x, p2y), (p3x, p3y),
                    cameraNeedleDistance, needleOffset, needleCompensation,
                    arcSegments);

                var result = new List<CoordinateTransformDetail>();
                foreach (var pt in bezierPoints)
                {
                    result.Add(new CoordinateTransformDetail
                    {
                        PhotoDx = photoDx,
                        PhotoDy = photoDy,
                        DeltaToCenterX = 0,
                        DeltaToCenterY = 0,
                        TargetOffsetX = 0,
                        TargetOffsetY = 0,
                        CameraNeedleDistanceX = cameraNeedleDistance.X,
                        CameraNeedleDistanceY = cameraNeedleDistance.Y,
                        NeedleOffsetX = needleOffset.X,
                        NeedleOffsetY = needleOffset.Y,
                        NeedleCompensationX = needleCompensation.X,
                        NeedleCompensationY = needleCompensation.Y,
                        FinalX = pt.X,
                        FinalY = pt.Y
                    });
                }
                return Task.FromResult(result);
            }
        }

        /// <summary>
        /// 执行Dot模式点胶，接受完整偏移参数
        /// </summary>
        public async Task ExecuteDotDispenseAsync(
            Dictionary<string, double> visionData,
            double photoDx, double photoDy,
            int dxAxisId, int dyAxisId, int dz1AxisId,
            int coordId,
            double speed, double dzSafePos, double dzDispensePos,
            bool dryRun, bool needleDescend,
            double cameraNeedleDistanceX, double cameraNeedleDistanceY,
            double targetOffsetX, double targetOffsetY,
            double needleOffsetX, double needleOffsetY,
            double needleCompensationX, double needleCompensationY,
            CancellationToken token)
        {
            var (mechX, mechY) = ComputeMachineCoordinateFromOffset(
                (photoDx, photoDy), (targetOffsetX, targetOffsetY),
                (cameraNeedleDistanceX, cameraNeedleDistanceY),
                (needleOffsetX, needleOffsetY),
                (needleCompensationX, needleCompensationY));

            _logger.Info($"[BezierArcDispense] Dot坐标转换: photo({photoDx:F3},{photoDy:F3}) " +
                $"targetOffset({targetOffsetX:F3},{targetOffsetY:F3}) " +
                $"camNeedleDist({cameraNeedleDistanceX:F3},{cameraNeedleDistanceY:F3}) " +
                $"needleOffset({needleOffsetX:F3},{needleOffsetY:F3}) " +
                $"comp({needleCompensationX:F3},{needleCompensationY:F3}) " +
                $"needleDescend={needleDescend} " +
                $"→ 机械({mechX:F3},{mechY:F3})");

            await _motionService.MoveAbsAsync(dz1AxisId, dzSafePos, speed, token);
            await _motionService.MoveLineAbsAsync(coordId, new[] { dxAxisId, dyAxisId }, new[] { mechX, mechY }, speed, token);

            if (needleDescend)
                await _motionService.MoveAbsAsync(dz1AxisId, dzDispensePos, speed, token);

            if (!dryRun)
                _logger.Info($"[BezierArcDispense] Dot点胶执行于 ({mechX:F3}, {mechY:F3})");
            else
                _logger.Info($"[BezierArcDispense] Dot空跑模式，跳过出胶，位置 ({mechX:F3}, {mechY:F3})");

            if (needleDescend)
                await _motionService.MoveAbsAsync(dz1AxisId, dzSafePos, speed, token);
        }

        /// <summary>
        /// 执行Arc模式点胶。
        /// 公式：Mech_n = PhotoPos + (Center - P_n) + CamToNeedle + NeedleOffset + NeedleComp，经贝塞尔离散后逐段插补运动。
        /// </summary>
        public async Task ExecuteArcDispenseAsync(
            Dictionary<string, double> visionData,
            double photoDx, double photoDy,
            int dxAxisId, int dyAxisId, int dz1AxisId,
            int coordId,
            double speed, double dzSafePos, double dzDispensePos,
            int arcSegments, bool dryRun, bool needleDescend,
            double cameraNeedleDistanceX, double cameraNeedleDistanceY,
            double needleOffsetX, double needleOffsetY,
            double needleCompensationX, double needleCompensationY,
            ManualResetEventSlim pauseEvent, CancellationToken token)
        {
            var (centerX, centerY, p1x, p1y, p2x, p2y, p3x, p3y) = ExtractArcPoints(visionData);

            // 公式：Mech_n = PhotoPos + (Center - P_n) + CamToNeedle + NeedleOffset + NeedleComp
            var bezierPoints = GenerateArcMachinePoints(
                (photoDx, photoDy), (centerX, centerY),
                (p1x, p1y), (p2x, p2y), (p3x, p3y),
                (cameraNeedleDistanceX, cameraNeedleDistanceY),
                (needleOffsetX, needleOffsetY),
                (needleCompensationX, needleCompensationY),
                arcSegments);

            _logger.Info($"[BezierArcDispense] Arc坐标: " +
                $"photo({photoDx:F3},{photoDy:F3}) center({centerX:F3},{centerY:F3}) " +
                $"P1→机械({bezierPoints[0].X:F3},{bezierPoints[0].Y:F3}) " +
                $"P2→机械({bezierPoints[bezierPoints.Count / 2].X:F3},{bezierPoints[bezierPoints.Count / 2].Y:F3}) " +
                $"P3→机械({bezierPoints[bezierPoints.Count - 1].X:F3},{bezierPoints[bezierPoints.Count - 1].Y:F3}) " +
                $"camNeedle({cameraNeedleDistanceX:F3},{cameraNeedleDistanceY:F3}) " +
                $"needleOffset({needleOffsetX:F3},{needleOffsetY:F3}) " +
                $"comp({needleCompensationX:F3},{needleCompensationY:F3}) " +
                $"needleDescend={needleDescend} 插补点数={bezierPoints.Count}");

            await _motionService.MoveAbsAsync(dz1AxisId, dzSafePos, speed, token);
            await _motionService.MoveLineAbsAsync(coordId, new[] { dxAxisId, dyAxisId },
                new[] { bezierPoints[0].X, bezierPoints[0].Y }, speed, token);

            if (needleDescend)
                await _motionService.MoveAbsAsync(dz1AxisId, dzDispensePos, speed, token);

            for (int i = 1; i < bezierPoints.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                pauseEvent.Wait(token);
                await _motionService.MoveLineAbsAsync(coordId, new[] { dxAxisId, dyAxisId },
                    new[] { bezierPoints[i].X, bezierPoints[i].Y }, speed, token);
            }

            if (!dryRun)
                _logger.Info($"[BezierArcDispense] Arc点胶完成，{arcSegments}段插补");
            else
                _logger.Info($"[BezierArcDispense] Arc空跑模式，跳过出胶，{arcSegments}段插补运动完成");

            if (needleDescend)
                await _motionService.MoveAbsAsync(dz1AxisId, dzSafePos, speed, token);
        }

        #endregion

        #region 私有辅助方法

        private (double centerX, double centerY, double p1x, double p1y, double p2x, double p2y, double p3x, double p3y) ExtractArcPoints(Dictionary<string, double> visionData)
        {
            double centerX = GetVisionValue(visionData, "centerX", double.NaN);
            double centerY = GetVisionValue(visionData, "centerY", double.NaN);
            double p1x = GetVisionValue(visionData, "point1X", double.NaN);
            double p1y = GetVisionValue(visionData, "point1Y", double.NaN);
            double p2x = GetVisionValue(visionData, "point2X", double.NaN);
            double p2y = GetVisionValue(visionData, "point2Y", double.NaN);
            double p3x = GetVisionValue(visionData, "point3X", double.NaN);
            double p3y = GetVisionValue(visionData, "point3Y", double.NaN);

            if (double.IsNaN(centerX) || double.IsNaN(centerY) ||
                double.IsNaN(p1x) || double.IsNaN(p1y) ||
                double.IsNaN(p2x) || double.IsNaN(p2y) ||
                double.IsNaN(p3x) || double.IsNaN(p3y))
            {
                throw new RecoverableException(
                    "视觉数据不足以提取Arc三点及中心点",
                    "请检查视觉数据是否包含centerX/centerY/point1X/point1Y/point2X/point2Y/point3X/point3Y。");
            }

            return (centerX, centerY, p1x, p1y, p2x, p2y, p3x, p3y);
        }

        private double GetVisionValue(Dictionary<string, double> visionData, string key, double defaultValue)
        {
            if (visionData != null && visionData.TryGetValue(key, out double value))
                return value;
            return defaultValue;
        }

        #endregion
    }
}
