using Core.Abstraction;
using Core.Utilities;
using MotionControl.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MotionControl.Services
{
    /// <summary>
    /// 连续插补走胶辅助：在预估轨迹结束前提前关胶，运动完成后 PostDelay 泄压。
    /// 用于补偿胶阀关断延迟、针嘴残胶及流体惯性，避免线段末端拖尾溢胶。
    /// 支持 2D(XY) 与 3D(XYZ) 两种路径：2D 重载供现有 XY 双轴插补使用（不变签名），
    /// 3D 重载供 Z 向校准启用时的三轴连续插补使用，提前关胶时序按 3D 矢量路径长度计算。
    /// </summary>
    public static class ArcContinuousDispenseHelper
    {
        /// <summary>计算 XY 路径总长度（mm）</summary>
        public static double ComputePathLengthMm(IReadOnlyList<(double X, double Y)> points)
        {
            if (points == null || points.Count < 2) return 0;
            double total = 0;
            for (int i = 1; i < points.Count; i++)
            {
                double dx = points[i].X - points[i - 1].X;
                double dy = points[i].Y - points[i - 1].Y;
                total += Math.Sqrt(dx * dx + dy * dy);
            }
            return total;
        }

        /// <summary>计算 XYZ 空间路径总长度（mm）——3D 连续插补时用于提前关胶时序，避免低估时长导致末端缺胶</summary>
        public static double ComputePathLengthMm3D(IReadOnlyList<(double X, double Y, double Z)> points)
        {
            if (points == null || points.Count < 2) return 0;
            double total = 0;
            for (int i = 1; i < points.Count; i++)
            {
                double dx = points[i].X - points[i - 1].X;
                double dy = points[i].Y - points[i - 1].Y;
                double dz = points[i].Z - points[i - 1].Z;
                total += Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }
            return total;
        }

        /// <summary>
        /// 连续插补走轨迹（XY 双轴）：开胶后调用；在预估结束前 earlyCloseGlueDelayMs 关阀，走完剩余路径后 PostDelay 泄压。
        /// 签名保持不变，供 DispenseStepAction 生产路径与既有 XY 双轴调用方继续使用。
        /// </summary>
        /// <param name="writeGlueIo">写出胶 IO；null 表示空跑不出胶</param>
        /// <param name="earlyCloseGlueDelayMs">提前关胶延时 ms，0 表示运动结束时关胶（兼容旧行为）</param>
        /// <param name="postDelayMs">关胶后泄压延时 ms，在整段运动完成后执行</param>
        public static async Task RunContinuousInterpolationWithEarlyGlueOffAsync(
            IMotionService motion,
            int coordId,
            int[] axisIds,
            IReadOnlyList<(double X, double Y)> pathPoints,
            double interpSpeed,
            double startVel,
            double acc,
            double dec,
            double endVel,
            int earlyCloseGlueDelayMs,
            int postDelayMs,
            Action<bool>? writeGlueIo,
            ILoggerService? logger,
            string logContext,
            CancellationToken token,
            TimeSpan motionTimeout,
            ILocalizationService? localization = null)
        {
            if (motion == null) throw new ArgumentNullException(nameof(motion));
            if (pathPoints == null || pathPoints.Count == 0)
                throw new ArgumentException(Localize(localization, "ACD_Msg_PathPointsEmpty", "路径点为空"), nameof(pathPoints));

            // 转为逐点 double[]（XY 两轴），委托共享核心
            var pts = new List<double[]>(pathPoints.Count);
            foreach (var (x, y) in pathPoints)
                pts.Add(new[] { x, y });
            double pathLen = ComputePathLengthMm(pathPoints);

            await RunCoreAsync(motion, coordId, axisIds, pts, pathLen,
                interpSpeed, startVel, acc, dec, endVel,
                earlyCloseGlueDelayMs, postDelayMs,
                writeGlueIo, logger, logContext, token, motionTimeout, localization);
        }

        /// <summary>
        /// 连续插补走轨迹（XYZ 三轴）：用于 Z 向校准启用时，Dx/Dy/所选 Dz 同步连续插补，针头跟随 CAD 表面 Z 轮廓。
        /// 提前关胶时序按 3D 矢量路径长度计算（interpSpeed 为矢量速度，3D 路径比 XY 更长，须用 3D 长度避免提前关胶过早导致末端缺胶）。
        /// </summary>
        /// <param name="writeGlueIo">写出胶 IO；null 表示空跑不出胶</param>
        /// <param name="earlyCloseGlueDelayMs">提前关胶延时 ms，0 表示运动结束时关胶</param>
        /// <param name="postDelayMs">关胶后泄压延时 ms，在整段运动完成后执行</param>
        public static async Task RunContinuousInterpolationWithEarlyGlueOffAsync(
            IMotionService motion,
            int coordId,
            int[] axisIds,
            IReadOnlyList<(double X, double Y, double Z)> pathPoints3D,
            double interpSpeed,
            double startVel,
            double acc,
            double dec,
            double endVel,
            int earlyCloseGlueDelayMs,
            int postDelayMs,
            Action<bool>? writeGlueIo,
            ILoggerService? logger,
            string logContext,
            CancellationToken token,
            TimeSpan motionTimeout,
            ILocalizationService? localization = null)
        {
            if (motion == null) throw new ArgumentNullException(nameof(motion));
            if (pathPoints3D == null || pathPoints3D.Count == 0)
                throw new ArgumentException(Localize(localization, "ACD_Msg_PathPointsEmpty", "路径点为空"), nameof(pathPoints3D));

            // 转为逐点 double[]（XYZ 三轴），委托共享核心
            var pts = new List<double[]>(pathPoints3D.Count);
            foreach (var (x, y, z) in pathPoints3D)
                pts.Add(new[] { x, y, z });
            double pathLen = ComputePathLengthMm3D(pathPoints3D);

            await RunCoreAsync(motion, coordId, axisIds, pts, pathLen,
                interpSpeed, startVel, acc, dec, endVel,
                earlyCloseGlueDelayMs, postDelayMs,
                writeGlueIo, logger, logContext, token, motionTimeout, localization);
        }

        /// <summary>
        /// 共享核心：初始化插补 → 逐点 AddLineSegment → 执行 → 提前关胶 → 等待完成 → PostDelay 泄压。
        /// pathPoints 每项 double[] 长度须与 axisIds 长度一致（2D=2，3D=3）。
        /// pathLengthMm 由调用方按维度正确计算后传入，用于提前关胶时序估算。
        /// </summary>
        private static async Task RunCoreAsync(
            IMotionService motion,
            int coordId,
            int[] axisIds,
            IReadOnlyList<double[]> pathPoints,
            double pathLengthMm,
            double interpSpeed,
            double startVel,
            double acc,
            double dec,
            double endVel,
            int earlyCloseGlueDelayMs,
            int postDelayMs,
            Action<bool>? writeGlueIo,
            ILoggerService? logger,
            string logContext,
            CancellationToken token,
            TimeSpan motionTimeout,
            ILocalizationService? localization)
        {
            motion.InitializeContinuousInterpolation(coordId, axisIds, startVel, interpSpeed, acc, dec, endVel);
            foreach (var pt in pathPoints)
                motion.AddLineSegment(coordId, pt);
            motion.ExecuteContinuousInterpolation(coordId);

            double pathLen = pathLengthMm;
            int earlyCloseMs = Math.Clamp(earlyCloseGlueDelayMs, 0, 5000);
            double estDurationMs = interpSpeed > 0.01 ? pathLen / interpSpeed * 1000.0 : 0;
            int glueOffAfterMs = earlyCloseMs > 0 && estDurationMs > earlyCloseMs
                ? (int)(estDurationMs - earlyCloseMs)
                : 0;

            bool glueClosed = false;
            var motionTask = motion.WaitForCoordMotionCompletionAsync(coordId, motionTimeout, token);

            // 提前关胶：在预估剩余 earlyCloseMs 时关阀，运动继续
            if (writeGlueIo != null && earlyCloseMs > 0 && glueOffAfterMs > 0)
            {
                await Task.WhenAny(Task.Delay(glueOffAfterMs, token), motionTask);
                if (!motionTask.IsCompleted)
                {
                    writeGlueIo(false);
                    glueClosed = true;
                    logger?.Info(
                        string.Format(Localize(localization, "ACD_Log_EarlyGlueOff", "[{0}] 提前关胶: 路径 {1:F2}mm, 插补 {2:F1}mm/s, 提前 {3}ms"),
                            logContext, pathLen, interpSpeed, earlyCloseMs));
                }
            }

            bool completed = await motionTask;
            if (!completed)
                throw new TimeoutException(string.Format(Localize(localization, "ACD_Msg_InterpTimeout", "{0} 连续插补运动超时"), logContext));

            if (writeGlueIo != null && !glueClosed)
            {
                writeGlueIo(false);
                if (earlyCloseMs > 0)
                    logger?.Debug(string.Format(Localize(localization, "ACD_Log_ShortPathGlueOff", "[{0}] 路径较短，运动结束时关胶"), logContext));
            }

            if (postDelayMs > 0)
                await Task.Delay(postDelayMs, token);
        }

        /// <summary>本地化辅助：localization 为 null 时回退到中文默认值（保证日志可读）</summary>
        private static string Localize(ILocalizationService? localization, string key, string fallback)
            => localization != null ? localization.GetResourceOrDefault(key, fallback) : fallback;
    }
}
