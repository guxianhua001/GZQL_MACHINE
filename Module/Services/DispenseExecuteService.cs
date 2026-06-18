using Core.Extensions;
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
    /// 支持双针头：needleIndex=0 使用针头1/Dz₂轴，needleIndex=1 使用针头2/Dz₃轴
    /// Dz₁轴为相机/3D扫描轴，不作为点胶轴使用
    /// </summary>
    public class DispenseExecuteService : IDispenseExecuteService
    {
        private readonly IMotionService _motionService;
        private readonly ILoggerService? _logger;

        private const int CoordIdLinear = 0;     // 直线插补使用坐标系0
        private const int CoordIdContinuous = 1;  // 多段连续插补走轨迹使用坐标系1
        private const int AxisDx = 8;
        private const int AxisDy = 6;
        /// <summary>针头1对应的Z轴编号（Dz₂, LogicalId=3）</summary>
        private const int AxisDzNeedle1 = 3;
        /// <summary>针头2对应的Z轴编号（Dz₃, LogicalId=4）</summary>
        private const int AxisDzNeedle2 = 4;
        private const int GlueIoPort1 = 12;   // 1/Dz₂出胶IO端口编号（LogicalId=12）
        private const int GlueIoPort2 = 13;   // 2/Dz₃出胶IO端口编号（LogicalId=13）

        private const double DefaultAcc = 0.05;
        private const double DefaultDec = 0.05;

        private int _isRunning;

        public event Action<string, int, int>? ProgressChanged;
        public event Action<string>? StatusChanged;

        public bool IsRunning => Interlocked.CompareExchange(ref _isRunning, 0, 0) == 1;

        public DispenseExecuteService(IMotionService motionService, ILoggerService? logger = null)
        {
            _motionService = motionService ?? throw new ArgumentNullException(nameof(motionService));
            _logger = logger;
        }

        /// <summary>根据针头索引获取对应的Z轴编号（针头1→Dz₂, 针头2→Dz₃）</summary>
        private static int GetAxisDz(int needleIndex) => needleIndex == 0 ? AxisDzNeedle1 : AxisDzNeedle2;

        /// <summary>根据针头索引获取对应的出胶IO端口（针头1→LogicalId=12, 针头2→LogicalId=13）</summary>
        private static int GetGlueIoPort(int needleIndex) => needleIndex == 0 ? GlueIoPort1 : GlueIoPort2;

        /// <summary>获取针头显示文本（针头1/Dz₂ 或 针头2/Dz₃）</summary>
        private static string NeedleText(int needleIndex) =>
            ResourceHelper.GetString("DispenseExec_NeedleFormat", needleIndex == 0 ? "1/Dz₂" : "2/Dz₃");

        /// <summary>暂停检查——pauseEvent 未置位时阻塞，支持取消</summary>
        private static void WaitIfPaused(ManualResetEventSlim? pauseEvent, CancellationToken token)
        {
            if (pauseEvent == null) return;
            while (!pauseEvent.IsSet)
            {
                token.ThrowIfCancellationRequested();
                pauseEvent.Wait(100, token);
            }
        }

        /// <summary>
        /// 空跑仿真：按行业标准工艺流程执行，可选是否下降到工作高度，不出胶
        /// </summary>
        public async Task DryRunAsync(IEnumerable<DispenseSegment> segments, bool descendToWorkHeight = false, int needleIndex = 0, CancellationToken token = default, ManualResetEventSlim? pauseEvent = null)
        {
            var modeLabel = ResourceHelper.GetString("DispenseExec_DryRun");
            await ExecuteSegmentsAsync(segments, descendToWorkHeight: descendToWorkHeight, dispenseGlue: false, modeLabel: modeLabel, needleIndex: needleIndex, token: token, pauseEvent: pauseEvent);
        }

        /// <summary>
        /// 执行走胶路径：按行业标准工艺流程执行，下降到工作高度并出胶
        /// </summary>
        public async Task ExecutePathAsync(IEnumerable<DispenseSegment> segments, string site, int needleIndex = 0, CancellationToken token = default, ManualResetEventSlim? pauseEvent = null)
        {
            var modeLabel = ResourceHelper.GetString("DispenseExec_Dispense", site);
            await ExecuteSegmentsAsync(segments, descendToWorkHeight: true, dispenseGlue: true, modeLabel: modeLabel, needleIndex: needleIndex, token: token, pauseEvent: pauseEvent);
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
            CancellationToken token,
            ManualResetEventSlim? pauseEvent = null)
        {
            SetRunning(true);
            PublishStatus("Running");
            int axisDz = GetAxisDz(needleIndex);

            try
            {
                var segmentList = segments.Where(s => s.IsEnabled).ToList();
                int total = segmentList.Count;
                _logger?.Info($"[DispenseExecute] {ResourceHelper.GetString("DispenseExec_StartLog", modeLabel, total, NeedleText(needleIndex))}");

                foreach (var (seg, index) in segmentList.Select((s, i) => (s, i)))
                {
                    token.ThrowIfCancellationRequested();
                    WaitIfPaused(pauseEvent, token);
                    if (seg.Points == null || seg.Points.Count == 0) continue;

                    PublishProgress(ResourceHelper.GetString("DispenseExec_SegmentProgress", modeLabel, seg.SegmentId, seg.EntityType), index + 1, total);
                    _logger?.Info($"[DispenseExecute] {modeLabel}段 [{seg.SegmentId}]");

                    // 1. Z 抬升到安全高度（使用 Step3 段参数 MoveSpeed）
                    double moveSpeed = seg.MoveSpeed;
                    await _motionService.MoveAbsAsync(axisDz, seg.SafeHeight, moveSpeed, token);

                    // 2. XY 移动到段起点上方（必须使用对齐后的机械坐标，CAD坐标不可用于运动）
                    var startPt = seg.Points.First();
                    if (!startPt.MachineX.HasValue || !startPt.MachineY.HasValue)
                        throw new InvalidOperationException(
                            ResourceHelper.GetString("DispenseExec_MissingMachineCoord", seg.SegmentId, ResourceHelper.GetString("DispenseExec_StartPoint")));
                    double startX = startPt.MachineX.Value;
                    double startY = startPt.MachineY.Value;
                    await _motionService.MoveLineAbsAsync(CoordIdLinear, new[] { AxisDx, AxisDy },
                        new[] { startX, startY }, moveSpeed, token);

                    // 3. Z 下降到工作高度（根据 descendToWorkHeight 标志决定是否下降）
                    double targetZ = descendToWorkHeight ? seg.EffectiveZHeight : seg.SafeHeight;

                    if (descendToWorkHeight)
                    {
                        // 两段式下降：快速接近 + 慢速到位
                        double approachOffset = seg.ApproachHeight;
                        double approachZ = targetZ + approachOffset;
                        double slowVel = moveSpeed * seg.CornerDecel;

                        // 3a. 快速下降到接近高度
                        await _motionService.MoveAbsAsync(axisDz, approachZ, moveSpeed, token);

                        if (dispenseGlue)
                        {
                            // 3b. 计算位置触发点：根据运动方向确定触发位在目标上方（提前开胶）
                            double triggerDistance = Math.Abs(seg.GlueTriggerOffsetMm);
                            int motionDir = Math.Sign(approachZ - targetZ);
                            double triggerZ = targetZ + motionDir * triggerDistance;

                            // 3c. 慢速移到触发位开胶
                            await _motionService.MoveAbsAsync(axisDz, triggerZ, slowVel, token);
                            WriteGlueIo(true, needleIndex);
                            _logger?.Info($"[DispenseExecute] {ResourceHelper.GetString("DispenseExec_TriggerGlueOn", seg.SegmentId, triggerZ, targetZ, seg.GlueTriggerOffsetMm)}");

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
                            _logger?.Warn($"[DispenseExecute] {ResourceHelper.GetString("DispenseExec_ZNotReached", seg.SegmentId, currentZPos, targetZ)}");
                            double retrySlowVel = moveSpeed * seg.CornerDecel;
                            await _motionService.MoveAbsAsync(axisDz, targetZ, retrySlowVel, token);
                        }
                    }

                    // 4. 连续插补走轨迹
                    _motionService.InitializeContinuousInterpolation(
                        CoordIdContinuous, new[] { AxisDx, AxisDy },
                        startVel: 0, maxVel: seg.InterpSpeed, acc: DefaultAcc, dec: DefaultDec, endVel: 0);

                    foreach (var pt in seg.Points)
                    {
                        if (!pt.MachineX.HasValue || !pt.MachineY.HasValue)
                            throw new InvalidOperationException(
                                ResourceHelper.GetString("DispenseExec_MissingMachineCoord", seg.SegmentId, ""));
                        double px = pt.MachineX.Value;
                        double py = pt.MachineY.Value;
                        _motionService.AddLineSegment(CoordIdContinuous, new[] { px, py });
                    }

                    _motionService.ExecuteContinuousInterpolation(CoordIdContinuous);

                    // 5. 等待运动完成
                    bool completed = await _motionService.WaitForCoordMotionCompletionAsync(
                        CoordIdContinuous, TimeSpan.FromMinutes(5), token);

                    if (!completed)
                        throw new TimeoutException(ResourceHelper.GetString("DispenseExec_MotionTimeout", seg.SegmentId, modeLabel));

                    // 6. 关胶 + 尾端延时
                    if (dispenseGlue)
                    {
                        WriteGlueIo(false, needleIndex);
                        _logger?.Info($"[DispenseExecute] {ResourceHelper.GetString("DispenseExec_GlueOff", seg.SegmentId)}");

                        if (seg.PostDelay > 0)
                            await Task.Delay((int)seg.PostDelay, token);
                    }

                    // 7. Z 抬升到安全高度
                    await _motionService.MoveAbsAsync(axisDz, seg.SafeHeight, moveSpeed, token);
                }

                PublishStatus("Completed");
                _logger?.Info($"[DispenseExecute] {ResourceHelper.GetString("DispenseExec_Completed", modeLabel)}");
            }
            catch (OperationCanceledException)
            {
                if (dispenseGlue) SafeGlueOff(needleIndex);
                PublishStatus("Canceled");
                _logger?.Warn($"[DispenseExecute] {ResourceHelper.GetString("DispenseExec_Canceled", modeLabel)}");
                throw;
            }
            catch (Exception ex)
            {
                if (dispenseGlue) SafeGlueOff(needleIndex);
                PublishStatus("Error");
                _logger?.Error(ex, $"[DispenseExecute] {ResourceHelper.GetString("DispenseExec_Error", modeLabel)}");
                throw;
            }
            finally
            {
                SetRunning(false);
            }
        }

        /// <summary>
        /// 执行单点点胶：定点下降 → 开胶 → 延时 → 关胶 → 上升
        /// 工艺参数来自 Step3EditParamsPanel 单点模式全局参数（DotProcessParams）
        /// </summary>
        public async Task ExecuteSinglePointAsync(
            CadPoint point,
            DotProcessParams processParams,
            int needleIndex = 0,
            CancellationToken token = default)
        {
            if (processParams == null)
                throw new ArgumentNullException(nameof(processParams));

            SetRunning(true);
            PublishStatus("Running");
            int axisDz = GetAxisDz(needleIndex);

            // Step3 单点工艺参数：MoveSpeed / DispenseTime / PostDelay
            double moveSpeed = processParams.MoveSpeed;
            int dispenseTimeMs = (int)processParams.DispenseTime;
            int postDelayMs = (int)processParams.PostDelay;
            double safeHeight = processParams.SafeHeight;

            try
            {
                if (!point.MachineX.HasValue || !point.MachineY.HasValue)
                    throw new InvalidOperationException(ResourceHelper.GetString("DispenseExec_SinglePointMissingCoord"));
                double mx = point.MachineX.Value;
                double my = point.MachineY.Value;
                double mz = point.MachineZ ?? point.Z;

                _logger?.Info($"[DispenseExecute] {ResourceHelper.GetString("DispenseExec_SinglePointStart", NeedleText(needleIndex), mx, my, mz)}");

                PublishProgress(ResourceHelper.GetString("DispenseExec_SinglePointDispense") + " - " + ResourceHelper.GetString("DispenseExec_MoveToSafeHeight"), 1, 1);
                await _motionService.MoveAbsAsync(axisDz, safeHeight, moveSpeed, token);

                PublishProgress(ResourceHelper.GetString("DispenseExec_SinglePointDispense") + " - " + ResourceHelper.GetString("DispenseExec_XYPositioning"), 1, 1);
                await _motionService.MoveLineAbsAsync(
                    CoordIdLinear, new[] { AxisDx, AxisDy }, new[] { mx, my },
                    moveSpeed, token);

                PublishProgress(ResourceHelper.GetString("DispenseExec_SinglePointDispense") + " - " + ResourceHelper.GetString("DispenseExec_ZDescending"), 1, 1);
                await _motionService.MoveAbsAsync(axisDz, mz, moveSpeed, token);

                PublishProgress(ResourceHelper.GetString("DispenseExec_SinglePointDispense") + " - " + ResourceHelper.GetString("DispenseExec_GlueOn"), 1, 1);
                WriteGlueIo(true, needleIndex);
                await Task.Delay(dispenseTimeMs, token);
                WriteGlueIo(false, needleIndex);
                if (postDelayMs > 0)
                    await Task.Delay(postDelayMs, token);

                PublishProgress(ResourceHelper.GetString("DispenseExec_SinglePointDispense") + " - " + ResourceHelper.GetString("DispenseExec_ZAscending"), 1, 1);
                await _motionService.MoveAbsAsync(axisDz, safeHeight, moveSpeed, token);

                PublishStatus("Completed");
                _logger?.Info($"[DispenseExecute] {ResourceHelper.GetString("DispenseExec_SinglePointCompleted")}");
            }
            catch (OperationCanceledException)
            {
                SafeGlueOff(needleIndex);
                PublishStatus("Error");
                _logger?.Warn($"[DispenseExecute] {ResourceHelper.GetString("DispenseExec_SinglePointCanceled")}");
                throw;
            }
            catch (Exception ex)
            {
                SafeGlueOff(needleIndex);
                PublishStatus("Error");
                _logger?.Error(ex, $"[DispenseExecute] {ResourceHelper.GetString("DispenseExec_SinglePointError")}");
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
            int needleIndex = 0,
            CancellationToken token = default,
            bool dryRun = false,
            ManualResetEventSlim? pauseEvent = null)
        {
            SetRunning(true);
            PublishStatus("Running");
            int axisDz = GetAxisDz(needleIndex);
            string modeLabel = dryRun
                ? ResourceHelper.GetString("DispenseExec_SinglePointDryRun")
                : ResourceHelper.GetString("DispenseExec_SinglePointDispense");

            try
            {
                var segmentList = segments.Where(s => s.IsEnabled).ToList();
                int total = segmentList.Count;
                _logger?.Info($"[DispenseExecute] {ResourceHelper.GetString("DispenseExec_StartLog", modeLabel, total, NeedleText(needleIndex))}");

                double moveSpeed = processParams.MoveSpeed;
                double safeHeight = processParams.SafeHeight;
                double approachOffset = processParams.ApproachHeight;
                double slowVel = moveSpeed * processParams.CornerDecel;
                double glueTriggerOffset = processParams.DotGlueTriggerOffsetMm;

                await _motionService.MoveAbsAsync(axisDz, safeHeight, moveSpeed, token);

                foreach (var (seg, index) in segmentList.Select((s, i) => (s, i)))
                {
                    token.ThrowIfCancellationRequested();
                    WaitIfPaused(pauseEvent, token);
                    if (seg.Points == null || seg.Points.Count == 0) continue;

                    PublishProgress(ResourceHelper.GetString("DispenseExec_SegmentProgressWithIndex", modeLabel, seg.SegmentId, index + 1, total), index + 1, total);
                    _logger?.Debug($"[DispenseExecute] {modeLabel}段 [{seg.SegmentId}]，共 {seg.Points.Count} 点");

                    double targetZ = dryRun ? safeHeight : processParams.EffectiveZHeight;

                    foreach (var (point, ptIndex) in seg.Points.Select((p, i) => (p, i)))
                    {
                        token.ThrowIfCancellationRequested();
                        WaitIfPaused(pauseEvent, token);

                        double px = point.MachineX ?? throw new InvalidOperationException(
                            ResourceHelper.GetString("DispenseExec_MissingMachineCoord", seg.SegmentId,
                                ResourceHelper.GetString("DispenseExec_PointLabel", ptIndex + 1) + "MachineX "));
                        double py = point.MachineY ?? throw new InvalidOperationException(
                            ResourceHelper.GetString("DispenseExec_MissingMachineCoord", seg.SegmentId,
                                ResourceHelper.GetString("DispenseExec_PointLabel", ptIndex + 1) + "MachineY "));

                        await _motionService.MoveAbsAsync(axisDz, safeHeight, moveSpeed, token);

                        await _motionService.MoveLineAbsAsync(CoordIdLinear, new[] { AxisDx, AxisDy },
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
                            WriteGlueIo(true, needleIndex);
                            _logger?.Debug($"[DispenseExecute] {ResourceHelper.GetString("DispenseExec_PointTriggerGlueOn", seg.SegmentId, ptIndex + 1, triggerZ, targetZ, glueTriggerOffset)}");

                            // 继续慢速移到目标位
                            await _motionService.MoveAbsAsync(axisDz, targetZ, slowVel, token);

                            if (processParams.PreDispenseDelay > 0)
                                await Task.Delay((int)processParams.PreDispenseDelay, token);

                            await Task.Delay((int)processParams.DispenseTime, token);

                            WriteGlueIo(false, needleIndex);

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

                await _motionService.MoveAbsAsync(axisDz, safeHeight, moveSpeed, token);

                PublishStatus("Completed");
                _logger?.Info($"[DispenseExecute] {ResourceHelper.GetString("DispenseExec_Completed", modeLabel)}");
            }
            catch (OperationCanceledException)
            {
                SafeGlueOff(needleIndex);
                PublishStatus("Canceled");
                _logger?.Warn($"[DispenseExecute] {ResourceHelper.GetString("DispenseExec_Canceled", modeLabel)}");
                throw;
            }
            catch (Exception ex)
            {
                SafeGlueOff(needleIndex);
                PublishStatus("Error");
                _logger?.Error(ex, $"[DispenseExecute] {ResourceHelper.GetString("DispenseExec_Error", modeLabel)}");
                throw;
            }
            finally
            {
                SetRunning(false);
            }
        }

        /// <summary>写入指定针头的出胶IO</summary>
        private void WriteGlueIo(bool value, int needleIndex)
        {
            int port = GetGlueIoPort(needleIndex);
            try { _motionService.WriteDo(port, value); }
            catch (Exception ex) { _logger?.Error(ex, $"[DispenseExecute] {ResourceHelper.GetString("DispenseExec_WriteIoFailed", port, value)}"); }
        }

        /// <summary>安全关胶——仅关闭当前针头对应的出胶IO</summary>
        private void SafeGlueOff(int needleIndex)
        {
            try { _motionService.WriteDo(GetGlueIoPort(needleIndex), false); }
            catch { }
        }

        private void SetRunning(bool running) => Interlocked.Exchange(ref _isRunning, running ? 1 : 0);

        private void PublishProgress(string message, int current, int total) => ProgressChanged?.Invoke(message, current, total);

        private void PublishStatus(string status) => StatusChanged?.Invoke(status);
    }
}
