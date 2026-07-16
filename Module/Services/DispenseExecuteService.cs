using Core.Extensions;
using Core.Models;
using Core.Utilities;
using MotionControl.Interfaces;
using MotionControl.Services;
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
        private const int GlueIoPort1 = 13;   // 1/Dz₂出胶IO端口编号（LogicalId=12）
        private const int GlueIoPort2 = 12;   // 2/Dz₃出胶IO端口编号（LogicalId=13）

        private const double DefaultAcc = 0.05;
        private const double DefaultDec = 0.05;
        /// <summary>Z 向校准单点 deltaZ 安全阈值(mm)：|deltaZ| 超此值视为可疑 CAD 数据，抛异常中止防碰撞。
        /// 操作员应按工件最大表面起伏调整（默认 10mm 兼顾安全与常见 3D 表面跟随）。</summary>
        private const double ZCorrectionMaxDeltaMm = 10.0;

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
        public async Task DryRunAsync(IEnumerable<DispenseSegment> segments, bool descendToWorkHeight = false, int needleIndex = 0, CancellationToken token = default, ManualResetEventSlim? pauseEvent = null, bool zCorrectionEnabled = false)
        {
            var modeLabel = ResourceHelper.GetString("DispenseExec_DryRun");
            await ExecuteSegmentsAsync(segments, descendToWorkHeight: descendToWorkHeight, dispenseGlue: false, modeLabel: modeLabel, needleIndex: needleIndex, token: token, pauseEvent: pauseEvent, zCorrectionEnabled: zCorrectionEnabled);
        }

        /// <summary>
        /// 执行走胶路径：按行业标准工艺流程执行，下降到工作高度并出胶
        /// </summary>
        public async Task ExecutePathAsync(IEnumerable<DispenseSegment> segments, string site, int needleIndex = 0, CancellationToken token = default, ManualResetEventSlim? pauseEvent = null, bool zCorrectionEnabled = false)
        {
            var modeLabel = ResourceHelper.GetString("DispenseExec_Dispense", site);
            await ExecuteSegmentsAsync(segments, descendToWorkHeight: true, dispenseGlue: true, modeLabel: modeLabel, needleIndex: needleIndex, token: token, pauseEvent: pauseEvent, zCorrectionEnabled: zCorrectionEnabled);
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
            ManualResetEventSlim? pauseEvent = null,
            bool zCorrectionEnabled = false)
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
                    _logger?.Info($"[DispenseExecute] {ResourceHelper.GetString("DispExec_Log_SegmentStart", modeLabel, seg.SegmentId)}");

                    // 1. Z 抬升到安全高度（使用 Step3 段参数 MoveSpeed）；SafeHeight=0 视为未配置，安全兜底为 -20
                    double moveSpeed = seg.MoveSpeed;
                    await _motionService.MoveAbsAsync(axisDz, seg.EffectiveSafeHeight, moveSpeed, token);

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
                    double targetZ = descendToWorkHeight ? seg.EffectiveZHeight : seg.EffectiveSafeHeight;
                    var endPt = seg.Points.Last();
                    if (!endPt.MachineX.HasValue || !endPt.MachineY.HasValue)
                        throw new InvalidOperationException(
                            ResourceHelper.GetString("DispenseExec_MissingMachineCoord", seg.SegmentId,
                                ResourceHelper.GetString("DispenseExec_PointLabel", seg.Points.Count)));
                    double endX = endPt.MachineX.Value;
                    double endY = endPt.MachineY.Value;

                    // 记录轨迹计划坐标；仅在段开始写一次，避免高频轨迹采样影响运动响应。
                    _logger?.Info($"[DispenseExecute] {ResourceHelper.GetString(
                        "DispenseExec_SegmentMotionPlan", seg.SegmentId, seg.Points.Count,
                        startX, startY, endX, endY, targetZ, seg.EffectiveSafeHeight,
                        seg.InterpSpeed, axisDz)}");

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

                    // 4. 连续插补走轨迹（走胶：提前关胶 + 走完剩余路径 + PostDelay 泄压）
                    Action<bool>? glueWriter = dispenseGlue ? on => WriteGlueIo(on, needleIndex) : null;
                    int earlyCloseMs = dispenseGlue ? (int)seg.EarlyCloseGlueDelayMs : 0;
                    int postDelayMs = dispenseGlue ? (int)seg.PostDelay : 0;

                    // 插补启动前读取编码器位置，作为实际起点审计基准。
                    LogInterpolationActualPosition("DispenseExec_InterpolationStartActual", seg.SegmentId, axisDz);

                    if (zCorrectionEnabled && descendToWorkHeight)
                    {
                        // Z 向校准：3 轴 XYZ 连续插补，针头跟随 CAD 表面 Z 轮廓
                        // 同卡校验：连续插补 ContiOpenList 只在单卡打开，跨卡轴会被静默错配到首卡物理轴号
                        int[] zCorrectAxes = { AxisDx, AxisDy, axisDz };
                        if (!_motionService.AreAxesOnSameCard(zCorrectAxes))
                            throw new InvalidOperationException(
                                ResourceHelper.GetString("DispenseExec_ZCorrectionCrossCard"));

                        // 基准高度 = EffectiveZHeight（保留换针/手动补偿）；ZMap 值越小表示表面越低，
                        // 因机械 Z 数值越大越向下，deltaZ 需按反向高度差计算。
                        var pathPoints3D = BuildZCorrectedPath(seg, seg.EffectiveZHeight);
                        await ArcContinuousDispenseHelper.RunContinuousInterpolationWithEarlyGlueOffAsync(
                            _motionService,
                            CoordIdContinuous,
                            zCorrectAxes,
                            pathPoints3D,
                            seg.InterpSpeed,
                            startVel: 0,
                            DefaultAcc,
                            DefaultDec,
                            endVel: 0,
                            earlyCloseMs,
                            postDelayMs,
                            glueWriter,
                            _logger,
                            $"[DispenseExecute] 段[{seg.SegmentId}]",
                            token,
                            TimeSpan.FromMinutes(5));
                    }
                    else
                    {
                        // XY 双轴连续插补（Z 保持 EffectiveZHeight 静止）
                        var pathPoints = new List<(double X, double Y)>(seg.Points.Count);
                        foreach (var pt in seg.Points)
                        {
                            if (!pt.MachineX.HasValue || !pt.MachineY.HasValue)
                                throw new InvalidOperationException(
                                    ResourceHelper.GetString("DispenseExec_MissingMachineCoord", seg.SegmentId, ""));
                            pathPoints.Add((pt.MachineX.Value, pt.MachineY.Value));
                        }

                        await ArcContinuousDispenseHelper.RunContinuousInterpolationWithEarlyGlueOffAsync(
                            _motionService,
                            CoordIdContinuous,
                            new[] { AxisDx, AxisDy },
                            pathPoints,
                            seg.InterpSpeed,
                            startVel: 0,
                            DefaultAcc,
                            DefaultDec,
                            endVel: 0,
                            earlyCloseMs,
                            postDelayMs,
                            glueWriter,
                            _logger,
                            $"[DispenseExecute] 段[{seg.SegmentId}]",
                            token,
                            TimeSpan.FromMinutes(5));
                    }

                    // 连续插补完成并经过 PostDelay 后读取终点实际位置，便于定位跟随误差。
                    LogInterpolationActualPosition("DispenseExec_InterpolationCompletedActual", seg.SegmentId, axisDz);

                    if (dispenseGlue)
                        _logger?.Info($"[DispenseExecute] {ResourceHelper.GetString("DispenseExec_GlueOff", seg.SegmentId)}");

                    // 5. Z 抬升到安全高度；SafeHeight=0 视为未配置，安全兜底为 -20
                    await _motionService.MoveAbsAsync(axisDz, seg.EffectiveSafeHeight, moveSpeed, token);
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
        /// 构建 Z 向校准后的 3D 插补路径：以段内第 1 个点的 ZMap 高度为基准 0。
        /// ZMap 值越小表示表面越低；机械 Z 数值越大越向下，
        /// 故每点 Z 目标 = baseZ + (firstZ - pt.Z)，使针头向下跟随低洼面。
        /// 安全校验：MachineX/Y 缺失、Z 非有限、|deltaZ| 超阈值均抛 InvalidOperationException 中止防碰撞。
        /// 第 1 点 deltaZ=0，Z=baseZ，与预下降目标(EffectiveZHeight)一致，无起点跳变。
        /// </summary>
        /// <param name="seg">点胶段（Points 已含 CAD Z 数据）</param>
        /// <param name="baseZ">基准高度 = EffectiveZHeight（保留换针/手动补偿）</param>
        /// <returns>3D 插补路径点列表 (X=MachineX, Y=MachineY, Z=baseZ+deltaZ)</returns>
        private List<(double X, double Y, double Z)> BuildZCorrectedPath(DispenseSegment seg, double baseZ)
        {
            var pts = seg.Points;
            if (pts == null || pts.Count == 0)
                throw new InvalidOperationException(
                    ResourceHelper.GetString("DispenseExec_MissingMachineCoord", seg.SegmentId, ""));

            double firstZ = pts[0].Z;
            if (!double.IsFinite(firstZ))
                throw new InvalidOperationException(
                    ResourceHelper.GetString("Step6_ZCorrection_InvalidZPoints", seg.SegmentId, 1, pts.Count, "1"));

            var path = new List<(double X, double Y, double Z)>(pts.Count);
            double minDelta = double.PositiveInfinity, maxDelta = double.NegativeInfinity;
            double minZ = double.PositiveInfinity, maxZ = double.NegativeInfinity;

            for (int i = 0; i < pts.Count; i++)
            {
                var pt = pts[i];
                if (!pt.MachineX.HasValue || !pt.MachineY.HasValue)
                    throw new InvalidOperationException(
                        ResourceHelper.GetString("DispenseExec_MissingMachineCoord", seg.SegmentId,
                            ResourceHelper.GetString("DispenseExec_PointLabel", i + 1)));

                // 逐点校验Z有效性，避免无效高度进入3轴插补导致撞针。
                if (!double.IsFinite(pt.Z))
                    throw new InvalidOperationException(
                        ResourceHelper.GetString("Step6_ZCorrection_InvalidZPoints",
                            seg.SegmentId, 1, pts.Count, (i + 1).ToString()));

                // ZMap 值越小表示表面越低；机械 Z 数值越大越向下。
                // 因此 deltaZ = 第1点 ZMap 值 - 当前点 ZMap 值，deltaZ>0 表示当前点更低、针头需下移。
                double deltaZ = firstZ - pt.Z;
                if (!double.IsFinite(deltaZ) || Math.Abs(deltaZ) > ZCorrectionMaxDeltaMm)
                    throw new InvalidOperationException(
                        ResourceHelper.GetString("DispenseExec_ZCorrectionDeltaExceeded", seg.SegmentId, deltaZ, ZCorrectionMaxDeltaMm));

                double z = baseZ + deltaZ;
                path.Add((pt.MachineX.Value, pt.MachineY.Value, z));

                if (deltaZ < minDelta) minDelta = deltaZ;
                if (deltaZ > maxDelta) maxDelta = deltaZ;
                if (z < minZ) minZ = z;
                if (z > maxZ) maxZ = z;
            }

            // 记录基准 Z、deltaZ 范围、校正后 Z 范围，便于操作员核对 CAD Z 方向是否与机器一致
            _logger?.Info($"[DispenseExecute] {ResourceHelper.GetString("DispenseExec_ZCorrectionEnabled", seg.SegmentId, baseZ, pts.Count, minDelta, maxDelta, minZ, maxZ)}");
            return path;
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
            // SafeHeight=0 视为未配置，安全兜底为 -20，避免直接抬升到 0 造成撞针
            double safeHeight = processParams.EffectiveSafeHeight;

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
                // SafeHeight=0 视为未配置，安全兜底为 -20，避免直接抬升到 0 造成撞针
                double safeHeight = processParams.EffectiveSafeHeight;
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
                    _logger?.Debug($"[DispenseExecute] {ResourceHelper.GetString("DispExec_Log_SegmentDebug", modeLabel, seg.SegmentId, seg.Points.Count)}");

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

        /// <summary>
        /// 记录连续插补阶段的实际编码器坐标。
        /// 仅在起点和完成点读取，不以高频采样方式访问运动卡，保证运动控制响应。
        /// </summary>
        private void LogInterpolationActualPosition(string resourceKey, string segmentId, int axisDz)
        {
            var dx = _motionService.GetAxisPosition(AxisDx);
            var dy = _motionService.GetAxisPosition(AxisDy);
            var dz = _motionService.GetAxisPosition(axisDz);
            _logger?.Info($"[DispenseExecute] {ResourceHelper.GetString(resourceKey, segmentId, dx, dy, dz)}");
        }

        private void SetRunning(bool running) => Interlocked.Exchange(ref _isRunning, running ? 1 : 0);

        private void PublishProgress(string message, int current, int total) => ProgressChanged?.Invoke(message, current, total);

        private void PublishStatus(string status) => StatusChanged?.Invoke(status);
    }
}
