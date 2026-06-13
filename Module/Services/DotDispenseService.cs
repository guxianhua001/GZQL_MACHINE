using Core.Models;
using Core.Utilities;
using MotionControl.Interfaces;
using Module.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Module.Services
{
    /// <summary>
    /// 点胶单点执行服务实现——点涂模式下的空跑、真实点胶和示教操作
    /// 遵循行业标准工艺流程：安全抬升 → XY定位 → Z两段式下降 → 开胶 → 出胶 → 关胶 → 抬升
    /// </summary>
    public class DotDispenseService : IDotDispenseService
    {
        private readonly IMotionService _motionService;
        private readonly ILoggerService? _logger;

        private const int CoordId = 0;
        private const int AxisDx = 8;
        private const int AxisDy = 6;
        private const int AxisDz1 = 2;
        private const int AxisDz2 = 3;
        private const int AxisDz3 = 4;
        private const int AxisRx = 10;
        private const int AxisRz = 11;
        private const int AxisY = 9;
        private const int GlueIoPort = 13;

        private int _isRunning;

        public event Action<string, int, int> ProgressChanged;
        public event Action<string> StatusChanged;

        public bool IsRunning => Interlocked.CompareExchange(ref _isRunning, 0, 0) == 1;

        public DotDispenseService(IMotionService motionService, ILoggerService? logger = null)
        {
            _motionService = motionService ?? throw new ArgumentNullException(nameof(motionService));
            _logger = logger;
        }

        /// <summary>
        /// 空跑试运行：按工艺流程运动但不出胶，Z轴保持在安全高度
        /// </summary>
        public async Task DryRunAsync(IEnumerable<DotPoint> points, DotProcessParams processParams, CancellationToken token = default)
        {
            SetRunning(true);
            PublishStatus("Running");

            try
            {
                var pointList = points.Where(p => p.IsSelected && p.IsEnabled).ToList();
                int total = pointList.Count;
                _logger?.Info($"[DotDispense] 开始空跑，共 {total} 点");

                double moveSpeed = processParams.MoveSpeed;
                double safeHeight = processParams.SafeHeight;
                double approachOffset = processParams.ApproachHeight;
                double slowVel = moveSpeed * processParams.CornerDecel;
                double dotGlueTriggerOffset = processParams.DotGlueTriggerOffsetMm;

                foreach (var (point, index) in pointList.Select((p, i) => (p, i)))
                {
                    token.ThrowIfCancellationRequested();

                    PublishProgress($"空跑 - 点 [{point.PointId}] ({index + 1}/{total})", index + 1, total);
                    _logger?.Debug($"[DotDispense] 空跑点 [{point.PointId}]");

                    await _motionService.MoveAbsAsync(AxisDz2, safeHeight, moveSpeed, token);

                    // Y轴定位
                    await _motionService.MoveAbsAsync(AxisY, point.Y, moveSpeed, token);

                    await _motionService.MoveLineAbsAsync(CoordId, new[] { AxisDx, AxisDy },
                        new[] { point.Dx, point.Dy }, moveSpeed, token);

                    double targetZ = point.EffectiveDz2 != 0
                                   ? point.EffectiveDz2
                                   : processParams.TeachHeight + processParams.HeightCompensation;

                    // Z 两段式下降：快速接近 + 慢速到位
                    double approachZ = targetZ + approachOffset;
                    await _motionService.MoveAbsAsync(AxisDz2, approachZ, moveSpeed, token);

                    // 计算位置触发点：根据运动方向确定触发位在目标上方（提前开胶）
                    // Z轴坐标系：向下位置增大。dotGlueTriggerOffset 为负数，|offset| 为提前距离
                    // 向上运动(approachZ < targetZ): triggerZ = targetZ - |offset|（更小的位置值）
                    // 向下运动(approachZ > targetZ): triggerZ = targetZ + |offset|（更大的位置值）
                    double triggerDistance = Math.Abs(dotGlueTriggerOffset);
                    int motionDir = Math.Sign(approachZ - targetZ); // +1=向上, -1=向下
                    double triggerZ = targetZ + motionDir * triggerDistance;

                    // 两段式慢速下降：先移到触发位开胶，再移到目标位
                    await _motionService.MoveAbsAsync(AxisDz2, triggerZ, slowVel, token);
                    _logger?.Debug($"[DotDispense] 空跑 点 [{point.PointId}] 到达触发位，triggerZ={triggerZ:F3}, targetZ={targetZ:F3}, offset={dotGlueTriggerOffset:F3}mm");

                    // 继续慢速移到目标位
                    await _motionService.MoveAbsAsync(AxisDz2, targetZ, slowVel, token);
                }

                // 空跑完成后返回安全高度
                await _motionService.MoveAbsAsync(AxisDz2, safeHeight, moveSpeed, token);

                PublishStatus("Completed");
                _logger?.Info("[DotDispense] 空跑完成");
            }
            catch (OperationCanceledException)
            {
                await StopAsync();
                PublishStatus("Canceled");
                _logger?.Warn("[DotDispense] 空跑已取消");
                throw;
            }
            catch (Exception ex)
            {
                await StopAsync();
                PublishStatus("Error");
                _logger?.Error(ex, "[DotDispense] 空跑异常");
                throw;
            }
            finally
            {
                SetRunning(false);
            }
        }

        /// <summary>
        /// 真实点胶执行：按行业标准流程逐点点胶
        /// 工艺流程：安全抬升 → XY定位 → Z两段式下降 → 位置触发开胶 → 出胶 → 关胶 → 抬升
        /// 全选模式：统一抬升后逐点执行（减少重复抬升）；部分选中模式：逐点完整执行
        /// </summary>
        public async Task ExecuteDotDispenseAsync(IEnumerable<DotPoint> points, DotProcessParams processParams, CancellationToken token = default)
        {
            SetRunning(true);
            PublishStatus("Running");

            try
            {
                var allPoints = points.ToList();
                var selectedPoints = allPoints.Where(p => p.IsSelected && p.IsEnabled).ToList();
                int total = selectedPoints.Count;
                bool allPointsSelected = allPoints.All(p => p.IsSelected && p.IsEnabled);

                _logger?.Info($"[DotDispense] 开始点胶，共 {total} 点，全选={allPointsSelected}");

                double moveSpeed = processParams.MoveSpeed;
                double safeHeight = processParams.SafeHeight;
                double approachOffset = processParams.ApproachHeight;
                double slowVel = moveSpeed * processParams.CornerDecel;
                double dotGlueTriggerOffset = processParams.DotGlueTriggerOffsetMm;

                if (allPointsSelected)
                {
                    await _motionService.MoveAbsAsync(AxisDz2, safeHeight, moveSpeed, token);
                }

                foreach (var (point, index) in selectedPoints.Select((p, i) => (p, i)))
                {
                    token.ThrowIfCancellationRequested();

                    PublishProgress($"点胶 - 点 [{point.PointId}] ({index + 1}/{total})", index + 1, total);
                    _logger?.Debug($"[DotDispense] 点胶点 [{point.PointId}]");

                    double targetZ = point.EffectiveDz2 != 0
                        ? point.EffectiveDz2
                        : processParams.TeachHeight + processParams.HeightCompensation;

                    if (!allPointsSelected)
                    {
                        await _motionService.MoveAbsAsync(AxisDz2, safeHeight, moveSpeed, token);
                    }
                    
                    // Y轴定位到点位
                    await _motionService.MoveAbsAsync(AxisY, point.Y, moveSpeed, token);

                    // XY 定位到点位上方
                    await _motionService.MoveLineAbsAsync(CoordId, new[] { AxisDx, AxisDy },
                        new[] { point.Dx, point.Dy }, moveSpeed, token);

                    // Z 两段式下降：快速接近 + 慢速到位
                    double approachZ = targetZ + approachOffset;
                    await _motionService.MoveAbsAsync(AxisDz2, approachZ, moveSpeed, token);

                    // 计算位置触发点：根据运动方向确定触发位在目标上方（提前开胶）
                    // Z轴坐标系：向下位置增大。dotGlueTriggerOffset 为负数，|offset| 为提前距离
                    // 向上运动(approachZ < targetZ): triggerZ = targetZ - |offset|（更小的位置值）
                    // 向下运动(approachZ > targetZ): triggerZ = targetZ + |offset|（更大的位置值）
                    double triggerDistance = Math.Abs(dotGlueTriggerOffset);
                    int motionDir = Math.Sign(approachZ - targetZ); // +1=向上, -1=向下
                    double triggerZ = targetZ + motionDir * triggerDistance;

                    // 两段式慢速下降：先移到触发位开胶，再移到目标位
                    await _motionService.MoveAbsAsync(AxisDz2, triggerZ, slowVel, token);
                    WriteGlueIo(true);
                    _logger?.Debug($"[DotDispense] 点 [{point.PointId}] 位置触发开胶，triggerZ={triggerZ:F3}, targetZ={targetZ:F3}, offset={dotGlueTriggerOffset:F3}mm");

                    // 继续慢速移到目标位
                    await _motionService.MoveAbsAsync(AxisDz2, targetZ, slowVel, token);

                    // 出胶延时
                    await Task.Delay((int)processParams.DispenseTime, token);

                    // 关胶
                    WriteGlueIo(false);

                    // 关胶后延时
                    if (processParams.PostDelay > 0)
                        await Task.Delay((int)processParams.PostDelay, token);

                    // Z 抬升到安全高度
                    await _motionService.MoveAbsAsync(AxisDz2, safeHeight, moveSpeed, token);
                }

                PublishStatus("Completed");
                _logger?.Info("[DotDispense] 点胶完成");
            }
            catch (OperationCanceledException)
            {
                await StopAsync();
                PublishStatus("Canceled");
                _logger?.Warn("[DotDispense] 点胶已取消");
                throw;
            }
            catch (Exception ex)
            {
                await StopAsync();
                PublishStatus("Error");
                _logger?.Error(ex, "[DotDispense] 点胶异常");
                throw;
            }
            finally
            {
                SetRunning(false);
            }
        }

        /// <summary>
        /// 示教单点：读取当前运动轴位置填入点位坐标
        /// </summary>
        public Task TeachPointAsync(DotPoint point, CancellationToken token = default)
        {
            point.Dx = _motionService.GetAxisPosition(AxisDx);
            point.Dy = _motionService.GetAxisPosition(AxisDy);
            point.Dz2 = _motionService.GetAxisPosition(AxisDz2);
            point.Dz3 = _motionService.GetAxisPosition(AxisDz3);
            point.Rx = _motionService.GetAxisPosition(AxisRx);
            point.Rz = _motionService.GetAxisPosition(AxisRz);
            point.Y = _motionService.GetAxisPosition(AxisY);

            _logger?.Info($"[DotDispense] 示教点位 [{point.PointId}] → ({point.Dx:F3}, {point.Dy:F3}, {point.Dz2:F3}, {point.Dz3:F3}, {point.Rx:F3}, {point.Rz:F3}, {point.Y:F3})");

            return Task.CompletedTask;
        }

        /// <summary>
        /// 安全停止：停止所有相关轴运动并关胶，轮询等待轴完全停止
        /// </summary>
        public async Task StopAsync()
        {
            SafeGlueOff();
            _motionService.StopAxis(AxisDx);
            _motionService.StopAxis(AxisDy);
            _motionService.StopAxis(AxisDz1);
            _motionService.StopAxis(AxisDz2);
            _motionService.StopAxis(AxisDz3);
            _motionService.StopAxis(AxisRx);
            _motionService.StopAxis(AxisRz);
            _motionService.StopAxis(AxisY);
            _logger?.Info("[DotDispense] 执行安全停止");

            // 等待所有轴停止运动（轮询位置变化）
            await WaitForAxesStoppedAsync();
        }

        /// <summary>
        /// 等待所有相关轴停止运动：连续检测位置不再变化即认为已停止
        /// </summary>
        private async Task WaitForAxesStoppedAsync(int stableCount = 3, int intervalMs = 10)
        {
            int[] axisIds = { AxisDz1, AxisDx, AxisDy, AxisDz2, AxisDz3, AxisRx, AxisRz, AxisY };
            double[] lastPositions = new double[axisIds.Length];
            int[] stableCounters = new int[axisIds.Length];
            bool allStopped = false;

            while (!allStopped)
            {
                allStopped = true;
                for (int i = 0; i < axisIds.Length; i++)
                {
                    double currentPos = _motionService.GetAxisPosition(axisIds[i]);
                    if (Math.Abs(currentPos - lastPositions[i]) > 0.001)
                    {
                        lastPositions[i] = currentPos;
                        stableCounters[i] = 0;
                        allStopped = false;
                    }
                    else
                    {
                        stableCounters[i]++;
                        if (stableCounters[i] < stableCount)
                            allStopped = false;
                    }
                }
                if (!allStopped)
                    await Task.Delay(intervalMs);
            }
        }

        private void WriteGlueIo(bool value)
        {
            try { _motionService.WriteDo(GlueIoPort, value); }
            catch (Exception ex) { _logger?.Error(ex, $"[DotDispense] 写出胶IO失败 port={GlueIoPort} value={value}"); }
        }

        private void SafeGlueOff()
        {
            try { _motionService.WriteDo(GlueIoPort, false); }
            catch { }
        }

        private void SetRunning(bool running) => Interlocked.Exchange(ref _isRunning, running ? 1 : 0);

        private void PublishProgress(string message, int current, int total) => ProgressChanged?.Invoke(message, current, total);

        private void PublishStatus(string status) => StatusChanged?.Invoke(status);
    }
}
