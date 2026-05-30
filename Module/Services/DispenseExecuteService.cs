using Core.Models;
using Core.Utilities;
using MotionControl.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Module.Services
{
    /// <summary>
    /// 点胶执行服务实现 — 将 DispenseSegment 轨迹转换为运动控制指令
    /// 空跑和走胶共享同一工艺流程，仅工作高度和出胶行为不同
    /// </summary>
    public class DispenseExecuteService : IDispenseExecuteService
    {
        private readonly IMotionService _motionService;
        private readonly ILoggerService? _logger;

        private const int CoordId = 0;
        private const int AxisDx = 9;
        private const int AxisDy = 7;
        private const int AxisDz1 = 3;
        private const int GlueIoPort = 0;
        private const double DefaultVelocity = 10.0;
        private const double DefaultAcc = 0.05;
        private const double DefaultDec = 0.05;
        private const int DefaultGlueDurationMs = 200;
        private const int DefaultPostDelayMs = 30;

        private int _isRunning;

        public event Action<string, int, int>? ProgressChanged;
        public event Action<string>? StatusChanged;

        public bool IsRunning => Interlocked.CompareExchange(ref _isRunning, 0, 0) == 1;

        public DispenseExecuteService(IMotionService motionService, ILoggerService? logger = null)
        {
            _motionService = motionService ?? throw new ArgumentNullException(nameof(motionService));
            _logger = logger;
        }

        /// <summary>
        /// 空跑仿真：按行业标准工艺流程执行，可选是否下降到工作高度，不出胶
        /// </summary>
        public async Task DryRunAsync(IEnumerable<DispenseSegment> segments, bool descendToWorkHeight = false, CancellationToken token = default)
        {
            await ExecuteSegmentsAsync(segments, descendToWorkHeight: descendToWorkHeight, dispenseGlue: false, modeLabel: "空跑", token: token);
        }

        /// <summary>
        /// 执行走胶路径：按行业标准工艺流程执行，下降到工作高度并出胶
        /// </summary>
        public async Task ExecutePathAsync(IEnumerable<DispenseSegment> segments, string site, CancellationToken token = default)
        {
            await ExecuteSegmentsAsync(segments, descendToWorkHeight: true, dispenseGlue: true, modeLabel: $"走胶[{site}]", token: token);
        }

        /// <summary>
        /// 统一执行入口——空跑和走胶共享同一工艺流程
        /// 【工业标准工艺】
        /// 流程：安全抬升 → XY定位 → Z下降(可选) → 走轨迹 → 关胶 → 抬升
        /// 
        /// 参数组合：
        /// - 空跑: descendToWorkHeight=false, dispenseGlue=false → 保持在安全高度，不出胶
        /// - 走胶: descendToWorkHeight=true,  dispenseGlue=true  → 下降到工作高度，出胶
        /// </summary>
        /// <param name="segments">轨迹段集合</param>
        /// <param name="descendToWorkHeight">是否下降到工作高度（false=保持在安全高度）</param>
        /// <param name="dispenseGlue">是否出胶</param>
        /// <param name="modeLabel">模式标签（用于日志和进度显示）</param>
        /// <param name="token">取消令牌</param>
        private async Task ExecuteSegmentsAsync(
            IEnumerable<DispenseSegment> segments,
            bool descendToWorkHeight,
            bool dispenseGlue,
            string modeLabel,
            CancellationToken token)
        {
            SetRunning(true);
            PublishStatus("Running");

            try
            {
                var segmentList = segments.Where(s => s.IsEnabled).ToList();
                int total = segmentList.Count;
                _logger?.Info($"[DispenseExecute] 开始{modeLabel}，共 {total} 段");

                foreach (var (seg, index) in segmentList.Select((s, i) => (s, i)))
                {
                    token.ThrowIfCancellationRequested();
                    if (seg.Points == null || seg.Points.Count == 0) continue;

                    PublishProgress($"{modeLabel} - 段 [{seg.SegmentId}] ({seg.EntityType})", index + 1, total);
                    _logger?.Debug($"[DispenseExecute] {modeLabel}段 [{seg.SegmentId}]");

                    // 1. Z 抬升到安全高度
                    await _motionService.MoveAbsAsync(AxisDz1, seg.SafeHeight, DefaultVelocity, token);

                    // 2. XY 移动到段起点上方
                    var startPt = seg.Points.First();
                    double startX = startPt.MachineX ?? startPt.X;
                    double startY = startPt.MachineY ?? startPt.Y;
                    await _motionService.MoveLineAbsAsync(CoordId, new[] { AxisDx, AxisDy },
                        new[] { startX, startY }, DefaultVelocity, token);

                    // 3. Z 下降到工作高度（根据 descendToWorkHeight 标志决定是否下降）
                    double targetZ = descendToWorkHeight ? seg.EffectiveZHeight : seg.SafeHeight;

                    if (descendToWorkHeight)
                    {
                        // 两段式下降：快速接近 + 慢速到位
                        double approachOffset = 3.0;
                        double approachZ = targetZ + approachOffset;
                        double slowVel = DefaultVelocity * seg.CornerDecel;

                        // 3a. 快速下降到距目标位 3mm 处
                        await _motionService.MoveAbsAsync(AxisDz1, approachZ, DefaultVelocity, token);

                        // 3b. 慢速接近目标高度（使用减速系数）
                        var moveZTask = _motionService.MoveAbsAsync(AxisDz1, targetZ, slowVel, token);

                        // 3c. 出胶模式：慢速下降过程中位置触发开胶
                        if (dispenseGlue)
                        {
                            bool glueOpened = false;
                            double triggerOffset = seg.GlueTriggerOffsetMm;
                            while (!moveZTask.IsCompleted && !token.IsCancellationRequested)
                            {
                                double currentZ = _motionService.GetAxisPosition(AxisDz1);
                                if (Math.Abs(currentZ - targetZ) <= triggerOffset)
                                {
                                    WriteGlueIo(true);
                                    _logger?.Debug($"[DispenseExecute] 段 [{seg.SegmentId}] 位置触发开胶");
                                    glueOpened = true;
                                    break;
                                }
                                await Task.Delay(1, token);
                            }

                            if (!glueOpened)
                            {
                                WriteGlueIo(true);
                                _logger?.Warn($"[DispenseExecute] 段 [{seg.SegmentId}] 兜底开胶");
                            }
                        }

                        await moveZTask;
                    }

                    // 3b. 开胶稳定延时
                    if (dispenseGlue && seg.PreDelay > 0)
                        await Task.Delay((int)seg.PreDelay, token);

                    // 3d. Z轴安全防护：确认Z轴已到达工作高度再开始插补运动
                    if (descendToWorkHeight)
                    {
                        double currentZPos = _motionService.GetAxisPosition(AxisDz1);
                        if (Math.Abs(currentZPos - targetZ) > 0.5)
                        {
                            _logger?.Warn($"[DispenseExecute] 段 [{seg.SegmentId}] Z轴未到位: 当前={currentZPos:F3}, 目标={targetZ:F3}，重新下降");
                            double slowVel = DefaultVelocity * seg.CornerDecel;
                            await _motionService.MoveAbsAsync(AxisDz1, targetZ, slowVel, token);
                        }
                    }

                    // 4. 连续插补走轨迹
                    _motionService.InitializeContinuousInterpolation(
                        CoordId, new[] { AxisDx, AxisDy },
                        startVel: 5, maxVel: seg.MoveSpeed, acc: DefaultAcc, dec: DefaultDec, endVel: 0);

                    foreach (var pt in seg.Points)
                    {
                        double px = pt.MachineX ?? pt.X;
                        double py = pt.MachineY ?? pt.Y;
                        _motionService.AddLineSegment(CoordId, new[] { px, py });
                    }

                    _motionService.ExecuteContinuousInterpolation(CoordId);

                    // 5. 等待运动完成
                    bool completed = await _motionService.WaitForCoordMotionCompletionAsync(
                        CoordId, TimeSpan.FromMinutes(5), token);

                    if (!completed)
                        throw new TimeoutException($"段 [{seg.SegmentId}] {modeLabel}运动超时");

                    // 6. 关胶 + 尾端延时
                    if (dispenseGlue)
                    {
                        WriteGlueIo(false);
                        _logger?.Debug($"[DispenseExecute] 段 [{seg.SegmentId}] 关胶");

                        if (seg.PostDelay > 0)
                            await Task.Delay((int)seg.PostDelay, token);
                    }

                    // 7. Z 抬升到安全高度
                    await _motionService.MoveAbsAsync(AxisDz1, seg.SafeHeight, DefaultVelocity, token);
                }

                PublishStatus("Completed");
                _logger?.Info($"[DispenseExecute] {modeLabel}完成");
            }
            catch (OperationCanceledException)
            {
                if (dispenseGlue) SafeGlueOff();
                PublishStatus("Canceled");
                _logger?.Warn($"[DispenseExecute] {modeLabel}已取消");
                throw;
            }
            catch (Exception ex)
            {
                if (dispenseGlue) SafeGlueOff();
                PublishStatus("Error");
                _logger?.Error(ex, $"[DispenseExecute] {modeLabel}异常");
                throw;
            }
            finally
            {
                SetRunning(false);
            }
        }

        /// <summary>
        /// 执行单点点胶：定点下降 → 开胶 → 延时 → 关胶 → 上升
        /// </summary>
        public async Task ExecuteSinglePointAsync(CadPoint point, CancellationToken token = default)
        {
            SetRunning(true);
            PublishStatus("Running");
            try
            {
                double mx = point.MachineX ?? point.X;
                double my = point.MachineY ?? point.Y;
                double mz = point.MachineZ ?? point.Z;

                _logger?.Info($"[DispenseExecute] 单点点胶 → ({mx:F3}, {my:F3}, {mz:F3})");

                PublishProgress("单点点胶 - 移动到安全高度", 1, 1);
                await _motionService.MoveAbsAsync(AxisDz1, 5.0, DefaultVelocity, token);

                PublishProgress("单点点胶 - XY 定位", 1, 1);
                await _motionService.MoveLineAbsAsync(
                    CoordId, new[] { AxisDx, AxisDy }, new[] { mx, my },
                    DefaultVelocity, token);

                PublishProgress("单点点胶 - Z 轴下降", 1, 1);
                await _motionService.MoveAbsAsync(AxisDz1, mz, DefaultVelocity, token);

                PublishProgress("单点点胶 - 开胶", 1, 1);
                WriteGlueIo(true);
                await Task.Delay(DefaultGlueDurationMs, token);
                WriteGlueIo(false);
                await Task.Delay(DefaultPostDelayMs, token);

                PublishProgress("单点点胶 - Z 轴回升", 1, 1);
                await _motionService.MoveAbsAsync(AxisDz1, 5.0, DefaultVelocity, token);

                PublishStatus("Completed");
                _logger?.Info("[DispenseExecute] 单点点胶完成");
            }
            catch (OperationCanceledException)
            {
                SafeGlueOff();
                PublishStatus("Error");
                _logger?.Warn("[DispenseExecute] 单点点胶被取消");
                throw;
            }
            catch (Exception ex)
            {
                SafeGlueOff();
                PublishStatus("Error");
                _logger?.Error(ex, "[DispenseExecute] 单点点胶异常");
                throw;
            }
            finally
            {
                SetRunning(false);
            }
        }

        /// <summary>
        /// 单点模式执行线条走胶——逐点执行，遵循行业标准工艺流程
        /// 流程：单点→Z抬升→XY定位→Z两段式下降(同步检测开胶距离)→出胶(起点延时)→
        /// 关胶(收胶延时)→抬升至安全高度→循环→结束后Z抬升至待机位
        /// </summary>
        public async Task ExecuteSinglePointLineAsync(
            IEnumerable<DispenseSegment> segments,
            DotProcessParams processParams,
            double standbyHeight,
            CancellationToken token = default)
        {
            SetRunning(true);
            PublishStatus("Running");

            try
            {
                var segmentList = segments.Where(s => s.IsEnabled).ToList();
                int total = segmentList.Count;
                _logger?.Info($"[DispenseExecute] 开始单点线条走胶，共 {total} 段");

                double moveSpeed = processParams.MoveSpeed;
                double safeHeight = processParams.SafeHeight;
                double approachOffset = processParams.ApproachHeight;
                double slowVel = moveSpeed * processParams.CornerDecel;
                double glueTriggerOffset = processParams.DotGlueTriggerOffsetMm;

                await _motionService.MoveAbsAsync(AxisDz1, safeHeight, moveSpeed, token);

                foreach (var (seg, index) in segmentList.Select((s, i) => (s, i)))
                {
                    token.ThrowIfCancellationRequested();
                    if (seg.Points == null || seg.Points.Count == 0) continue;

                    PublishProgress($"单点走胶 - 段 [{seg.SegmentId}] ({index + 1}/{total})", index + 1, total);
                    _logger?.Debug($"[DispenseExecute] 单点走胶段 [{seg.SegmentId}]，共 {seg.Points.Count} 点");

                    double targetZ = processParams.EffectiveZHeight;

                    foreach (var (point, ptIndex) in seg.Points.Select((p, i) => (p, i)))
                    {
                        token.ThrowIfCancellationRequested();

                        double px = point.MachineX ?? point.OffsetX ?? point.X;
                        double py = point.MachineY ?? point.OffsetY ?? point.Y;

                        await _motionService.MoveAbsAsync(AxisDz1, safeHeight, moveSpeed, token);

                        await _motionService.MoveLineAbsAsync(CoordId, new[] { AxisDx, AxisDy },
                            new[] { px, py }, moveSpeed, token);

                        double approachZ = targetZ + approachOffset;
                        await _motionService.MoveAbsAsync(AxisDz1, approachZ, moveSpeed, token);

                        var moveZTask = _motionService.MoveAbsAsync(AxisDz1, targetZ, slowVel, token);

                        bool glueOpened = false;
                        while (!moveZTask.IsCompleted && !token.IsCancellationRequested)
                        {
                            double currentZ = _motionService.GetAxisPosition(AxisDz1);
                            if (Math.Abs(currentZ - targetZ) <= glueTriggerOffset)
                            {
                                WriteGlueIo(true);
                                _logger?.Debug($"[DispenseExecute] 段[{seg.SegmentId}]点{ptIndex + 1} 位置触发开胶");
                                glueOpened = true;
                                break;
                            }
                            await Task.Delay(1, token);
                        }

                        if (!glueOpened)
                        {
                            WriteGlueIo(true);
                            _logger?.Warn($"[DispenseExecute] 段[{seg.SegmentId}]点{ptIndex + 1} 兜底开胶");
                        }

                        await moveZTask;

                        if (processParams.PreDispenseDelay > 0)
                            await Task.Delay((int)processParams.PreDispenseDelay, token);

                        await Task.Delay((int)processParams.DispenseTime, token);

                        WriteGlueIo(false);

                        if (processParams.PostDelay > 0)
                            await Task.Delay((int)processParams.PostDelay, token);

                        await _motionService.MoveAbsAsync(AxisDz1, safeHeight, moveSpeed, token);
                    }
                }

                await _motionService.MoveAbsAsync(AxisDz1, standbyHeight, moveSpeed, token);

                PublishStatus("Completed");
                _logger?.Info("[DispenseExecute] 单点线条走胶完成");
            }
            catch (OperationCanceledException)
            {
                SafeGlueOff();
                PublishStatus("Canceled");
                _logger?.Warn("[DispenseExecute] 单点线条走胶已取消");
                throw;
            }
            catch (Exception ex)
            {
                SafeGlueOff();
                PublishStatus("Error");
                _logger?.Error(ex, "[DispenseExecute] 单点线条走胶异常");
                throw;
            }
            finally
            {
                SetRunning(false);
            }
        }

        private void WriteGlueIo(bool value)
        {
            try { _motionService.WriteDo(GlueIoPort, value); }
            catch (Exception ex) { _logger?.Error(ex, $"[DispenseExecute] 写出胶IO失败 port={GlueIoPort} value={value}"); }
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
