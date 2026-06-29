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

        /// <summary>
        /// 连续插补走轨迹：开胶后调用；在预估结束前 earlyCloseGlueDelayMs 关阀，走完剩余路径后 PostDelay 泄压。
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
            // 本地化辅助：localization 为 null 时回退到中文默认值（保证日志可读）
            string L(string key, string fallback) => localization != null ? localization.GetResourceOrDefault(key, fallback) : fallback;

            if (motion == null) throw new ArgumentNullException(nameof(motion));
            if (pathPoints == null || pathPoints.Count == 0)
                throw new ArgumentException(L("ACD_Msg_PathPointsEmpty", "路径点为空"), nameof(pathPoints));

            motion.InitializeContinuousInterpolation(coordId, axisIds, startVel, interpSpeed, acc, dec, endVel);
            foreach (var (x, y) in pathPoints)
                motion.AddLineSegment(coordId, new[] { x, y });
            motion.ExecuteContinuousInterpolation(coordId);

            double pathLen = ComputePathLengthMm(pathPoints);
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
                        string.Format(L("ACD_Log_EarlyGlueOff", "[{0}] 提前关胶: 路径 {1:F2}mm, 插补 {2:F1}mm/s, 提前 {3}ms"),
                            logContext, pathLen, interpSpeed, earlyCloseMs));
                }
            }

            bool completed = await motionTask;
            if (!completed)
                throw new TimeoutException(string.Format(L("ACD_Msg_InterpTimeout", "{0} 连续插补运动超时"), logContext));

            if (writeGlueIo != null && !glueClosed)
            {
                writeGlueIo(false);
                if (earlyCloseMs > 0)
                    logger?.Debug(string.Format(L("ACD_Log_ShortPathGlueOff", "[{0}] 路径较短，运动结束时关胶"), logContext));
            }

            if (postDelayMs > 0)
                await Task.Delay(postDelayMs, token);
        }
    }
}
