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
    /// 支持双针头：needleIndex=0 使用 AxisDz1，needleIndex=1 使用 AxisDz2
    /// </summary>
    public class DispenseExecuteService : IDispenseExecuteService
    {
        private readonly IMotionService _motionService;
        private readonly ILoggerService? _logger;

        private const int CoordId = 0;
        private const int AxisDx = 8;
        private const int AxisDy = 6;
        private const int AxisDz1 = 3;
        private const int AxisDz2 = 4;
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

        /// <summary>根据针头索引获取对应的Z轴编号</summary>
        private static int GetAxisDz(int needleIndex) => needleIndex == 0 ? AxisDz1 : AxisDz2;

        /// <summary>
        /// 空跑仿真：按行业标准工艺流程执行，可选是否下降到工作高度，不出胶
        /// </summary>
        public async Task DryRunAsync(IEnumerable<DispenseSegment> segments, bool descendToWorkHeight = false, int needleIndex = 0, CancellationToken token = default)
        {
            await ExecuteSegmentsAsync(segments, descendToWorkHeight: descendToWorkHeight, dispenseGlue: false, modeLabel: "空跑", needleIndex: needleIndex, token: token);
        }

        /// <summary>
        /// 执行走胶路径：按行业标准工艺流程执行，下降到工作高度并出胶
        /// </summary>
        public async Task ExecutePathAsync(IEnumerable<DispenseSegment> segments, string site, int needleIndex = 0, CancellationToken token = default)
        {
            await ExecuteSegmentsAsync(segments, descendToWorkHeight: true, dispenseGlue: true, modeLabel: $"走胶[{site}]", needleIndex: needleIndex, token: token);
        }

        /// <summary>
        /// 统一执行入口——空跑和走胶共享同一工艺流程
        /// 【工业标准工艺】
        /// 流程：安全抬升 → XY定位 → Z下降(可选) → 走轨迹 → 关胶 → 抬升
        /// </summary>
        private async Task ExecuteSegmentsAsync(
            IEnumerable<DispenseSegment> segments,
            bool descendToWorkHeight,
            bool dispenseGlue,
            string modeLabel,
            int needleIndex,
            CancellationToken token)
        {
            SetRunning(true);
            PublishStatus("Running");
            int axisDz = GetAxisDz(needleIndex);

            try
            {
                var segmentList = segments.Where(s => s.IsEnabled).ToList();
                int total = segmentList.Count;
                _logger?.Info($"[DispenseExecute] 开始{modeLabel}，共 {total} 段，针头{(needleIndex == 0 ? "1/Dz1" : "2/Dz2")}");

                foreach (var (seg, index) in segmentList.Select((s, i) => (s, i)))
                {
                    token.ThrowIfCancellationRequested();
                    if (seg.Points == null || seg.Points.Count == 0) continue;

                    PublishProgress($"{modeLabel} - 段 [{seg.SegmentId}] ({seg.EntityType})", index + 1, total);
                    _logger?.Debug($"[DispenseExecute] {modeLabel}段 [{seg.SegmentId}]");

                    // 1. Z 抬升到安全高度
                    await _motionService.MoveAbsAsync(axisDz, seg.SafeHeight, DefaultVelocity, token);

                    // 2. XY 移动到段起点上方（必须使用对齐后的机械坐标，CAD坐标不可用于运动）
                    var startPt = seg.Points.First();
                    if (!startPt.MachineX.HasValue || !startPt.MachineY.HasValue)
                        throw new InvalidOperationException($"段 [{seg.SegmentId}] 起点缺少机械坐标（未执行坐标对齐），拒绝执行以防撞机");
                    double startX = startPt.MachineX.Value;
                    double startY = startPt.MachineY.Value;
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
                        await _motionService.MoveAbsAsync(axisDz, approachZ, DefaultVelocity, token);

                        if (dispenseGlue)
                        {
                            // 3b. 计算位置触发点：根据运动方向确定触发位在目标上方（提前开胶）
                            double triggerDistance = Math.Abs(seg.GlueTriggerOffsetMm);
                            int motionDir = Math.Sign(approachZ - targetZ);
                            double triggerZ = targetZ + motionDir * triggerDistance;

                            // 3c. 慢速移到触发位开胶
                            await _motionService.MoveAbsAsync(axisDz, triggerZ, slowVel, token);
                            WriteGlueIo(true);
                            _logger?.Debug($"[DispenseExecute] 段 [{seg.SegmentId}] 位置触发开胶，triggerZ={triggerZ:F3}, targetZ={targetZ:F3}, offset={seg.GlueTriggerOffsetMm:F3}mm");

                            // 3d. 继续慢速移到目标位
                            await _motionService.MoveAbsAsync(axisDz, targetZ, slowVel, token);
                        }
                        else
                        {
                            // 空跑：直接慢速移到目标位
                            await _motionService.MoveAbsAsync(axisDz, targetZ, slowVel, token);
                        }
                    }

                    // 开胶稳定延时
                    if (dispenseGlue && seg.PreDelay > 0)
                        await Task.Delay((int)seg.PreDelay, token);

                    // Z轴安全防护：确认Z轴已到达工作高度再开始插补运动
                    if (descendToWorkHeight)
                    {
                        double currentZPos = _motionService.GetAxisPosition(axisDz);
                        if (Math.Abs(currentZPos - targetZ) > 0.5)
                        {
                            _logger?.Warn($"[DispenseExecute] 段 [{seg.SegmentId}] Z轴未到位: 当前={currentZPos:F3}, 目标={targetZ:F3}，重新下降");
                            double slowVel = DefaultVelocity * seg.CornerDecel;
                            await _motionService.MoveAbsAsync(axisDz, targetZ, slowVel, token);
                        }
                    }

                    // 4. 连续插补走轨迹
                    _motionService.InitializeContinuousInterpolation(
                        CoordId, new[] { AxisDx, AxisDy },
                        startVel: 5, maxVel: seg.MoveSpeed, acc: DefaultAcc, dec: DefaultDec, endVel: 0);

                    foreach (var pt in seg.Points)
                    {
                        if (!pt.MachineX.HasValue || !pt.MachineY.HasValue)
                            throw new InvalidOperationException($"段 [{seg.SegmentId}] 点缺少机械坐标（未执行坐标对齐），拒绝执行以防撞机");
                        double px = pt.MachineX.Value;
                        double py = pt.MachineY.Value;
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
                    await _motionService.MoveAbsAsync(axisDz, seg.SafeHeight, DefaultVelocity, token);
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
        public async Task ExecuteSinglePointAsync(CadPoint point, int needleIndex = 0, CancellationToken token = default)
        {
            SetRunning(true);
            PublishStatus("Running");
            int axisDz = GetAxisDz(needleIndex);

            try
            {
                if (!point.MachineX.HasValue || !point.MachineY.HasValue)
                    throw new InvalidOperationException("单点点胶：点缺少机械坐标（未执行坐标对齐），拒绝执行以防撞机");
                double mx = point.MachineX.Value;
                double my = point.MachineY.Value;
                double mz = point.MachineZ ?? point.Z;

                _logger?.Info($"[DispenseExecute] 单点点胶(针头{(needleIndex == 0 ? "1" : "2")}) → ({mx:F3}, {my:F3}, {mz:F3})");

                PublishProgress("单点点胶 - 移动到安全高度", 1, 1);
                await _motionService.MoveAbsAsync(axisDz, 5.0, DefaultVelocity, token);

                PublishProgress("单点点胶 - XY 定位", 1, 1);
                await _motionService.MoveLineAbsAsync(
                    CoordId, new[] { AxisDx, AxisDy }, new[] { mx, my },
                    DefaultVelocity, token);

                PublishProgress("单点点胶 - Z 轴下降", 1, 1);
                await _motionService.MoveAbsAsync(axisDz, mz, DefaultVelocity, token);

                PublishProgress("单点点胶 - 开胶", 1, 1);
                WriteGlueIo(true);
                await Task.Delay(DefaultGlueDurationMs, token);
                WriteGlueIo(false);
                await Task.Delay(DefaultPostDelayMs, token);

                PublishProgress("单点点胶 - Z 轴回升", 1, 1);
                await _motionService.MoveAbsAsync(axisDz, 5.0, DefaultVelocity, token);

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
        /// </summary>
        public async Task ExecuteSinglePointLineAsync(
            IEnumerable<DispenseSegment> segments,
            DotProcessParams processParams,
            double standbyHeight,
            int needleIndex = 0,
            CancellationToken token = default,
            bool dryRun = false)
        {
            SetRunning(true);
            PublishStatus("Running");
            int axisDz = GetAxisDz(needleIndex);
            string modeLabel = dryRun ? "单点空跑" : "单点走胶";

            try
            {
                var segmentList = segments.Where(s => s.IsEnabled).ToList();
                int total = segmentList.Count;
                _logger?.Info($"[DispenseExecute] 开始{modeLabel}，共 {total} 段，针头{(needleIndex == 0 ? "1/Dz1" : "2/Dz2")}");

                double moveSpeed = processParams.MoveSpeed;
                double safeHeight = processParams.SafeHeight;
                double approachOffset = processParams.ApproachHeight;
                double slowVel = moveSpeed * processParams.CornerDecel;
                double glueTriggerOffset = processParams.DotGlueTriggerOffsetMm;

                await _motionService.MoveAbsAsync(axisDz, safeHeight, moveSpeed, token);

                foreach (var (seg, index) in segmentList.Select((s, i) => (s, i)))
                {
                    token.ThrowIfCancellationRequested();
                    if (seg.Points == null || seg.Points.Count == 0) continue;

                    PublishProgress($"{modeLabel} - 段 [{seg.SegmentId}] ({index + 1}/{total})", index + 1, total);
                    _logger?.Debug($"[DispenseExecute] {modeLabel}段 [{seg.SegmentId}]，共 {seg.Points.Count} 点");

                    double targetZ = dryRun ? safeHeight : processParams.EffectiveZHeight;

                    foreach (var (point, ptIndex) in seg.Points.Select((p, i) => (p, i)))
                    {
                        token.ThrowIfCancellationRequested();

                        double px = point.MachineX ?? throw new InvalidOperationException($"段 [{seg.SegmentId}] 点{ptIndex + 1}缺少MachineX（未执行坐标对齐），拒绝执行以防撞机");
                        double py = point.MachineY ?? throw new InvalidOperationException($"段 [{seg.SegmentId}] 点{ptIndex + 1}缺少MachineY（未执行坐标对齐），拒绝执行以防撞机");

                        await _motionService.MoveAbsAsync(axisDz, safeHeight, moveSpeed, token);

                        await _motionService.MoveLineAbsAsync(CoordId, new[] { AxisDx, AxisDy },
                            new[] { px, py }, moveSpeed, token);

                        if (!dryRun)
                        {
                            // 正常走胶：两段式下降 + 位置触发开胶
                            double approachZ = targetZ + approachOffset;
                            await _motionService.MoveAbsAsync(axisDz, approachZ, moveSpeed, token);

                            // 计算位置触发点：根据运动方向确定触发位在目标上方（提前开胶）
                            double triggerDistance = Math.Abs(glueTriggerOffset);
                            int motionDir = Math.Sign(approachZ - targetZ);
                            double triggerZ = targetZ + motionDir * triggerDistance;

                            // 慢速移到触发位开胶
                            await _motionService.MoveAbsAsync(axisDz, triggerZ, slowVel, token);
                            WriteGlueIo(true);
                            _logger?.Debug($"[DispenseExecute] 段[{seg.SegmentId}]点{ptIndex + 1} 位置触发开胶，triggerZ={triggerZ:F3}, targetZ={targetZ:F3}, offset={glueTriggerOffset:F3}mm");

                            // 继续慢速移到目标位
                            await _motionService.MoveAbsAsync(axisDz, targetZ, slowVel, token);

                            if (processParams.PreDispenseDelay > 0)
                                await Task.Delay((int)processParams.PreDispenseDelay, token);

                            await Task.Delay((int)processParams.DispenseTime, token);

                            WriteGlueIo(false);

                            if (processParams.PostDelay > 0)
                                await Task.Delay((int)processParams.PostDelay, token);
                        }
                        else
                        {
                            // 空跑模式：仅在安全高度定位，不出胶，短暂延时模拟
                            await Task.Delay(50, token);
                        }

                        await _motionService.MoveAbsAsync(axisDz, safeHeight, moveSpeed, token);
                    }
                }

                await _motionService.MoveAbsAsync(axisDz, standbyHeight, moveSpeed, token);

                PublishStatus("Completed");
                _logger?.Info($"[DispenseExecute] {modeLabel}完成");
            }
            catch (OperationCanceledException)
            {
                SafeGlueOff();
                PublishStatus("Canceled");
                _logger?.Warn($"[DispenseExecute] {modeLabel}已取消");
                throw;
            }
            catch (Exception ex)
            {
                SafeGlueOff();
                PublishStatus("Error");
                _logger?.Error(ex, $"[DispenseExecute] {modeLabel}异常");
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
