#if HAS_HALCON
using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using HalconDotNet;
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
    /// 针头对针运动实现：使用 IMotionService 直接控制 Dx/Dy/Dz₂/Dz₃
    /// 五阶段流程：①Z 安全高度 → ②四点 X 向寻边 → ③Halcon 直线交点拟合中心 → ④Z 零点检测 → ⑤增量补偿
    /// </summary>
    public class NeedleAlignerMotionService : INeedleAlignerMotionService
    {
        private const string StationIdentifier = "DispenserStation";
        private const int SensorPollMs = 20;
        /// <summary>边界入光沿扫描轮询周期（ms），比通用轮询更短以降低越位</summary>
        private const int BoundarySensorPollMs = 5;
        private const int SensorTimeoutMs = 60000;
        /// <summary>Z 接近目标前减速段长度（mm）</summary>
        private const double ZApproachGapMm = 5.0;

        /// <summary>边界扫描停轴后位置稳定判定次数</summary>
        private const int AxisSettleStableReads = 3;
        /// <summary>边界扫描停轴后额外静置时间（ms），避免控制器 CheckDone 滞后</summary>
        private const int AxisSettleDelayMs = 100;
        /// <summary>双激光预清分步长度（mm）</summary>
        private const double DualLaserPreClearStepMm = 1.0;
        /// <summary>束内扫描启动后无位移判定超时（ms），防止相对运动未下发却空等传感器</summary>
        private const int BoundaryScanMotionStartTimeoutMs = 800;
        /// <summary>双激光预清超时（ms）</summary>
        private const int DualLaserPreClearTimeoutMs = 8000;
        /// <summary>粗拟合中心到位后局部精搜半宽（mm）：双激光未同时亮时仅在此范围内扫描，防止大跨度撞针</summary>
        private const double CenterRefineHalfSpanMm = 2.0;
        /// <summary>Z 高度搜索起始偏移：寻针高度上方（物理上抬）5mm</summary>
        private const double ZDescendStartOffsetAboveSearchMm = 5.0;
        /// <summary>下探触发前 Z 最小有效行程（mm），防止起点误触发</summary>
        private const double ZDescendMinTravelBeforeTriggerMm = 0.05;

        private readonly IMotionService _motion;
        private readonly IAxisParameterService _axisParameterService;
        private readonly ISpeedOverrideService _speedOverride;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localization;
        private readonly ISafetyZoneConfigLoader _safetyZoneConfigLoader;
        private Dictionary<string, int> _axisIdCache;
        private int? _coordIdCache;

        public NeedleAlignerMotionService(
            IMotionService motion,
            IAxisParameterService axisParameterService,
            ISpeedOverrideService speedOverride,
            ILoggerService logger,
            ILocalizationService localization,
            ISafetyZoneConfigLoader safetyZoneConfigLoader = null)
        {
            _motion = motion ?? throw new ArgumentNullException(nameof(motion));
            _axisParameterService = axisParameterService ?? throw new ArgumentNullException(nameof(axisParameterService));
            _speedOverride = speedOverride ?? throw new ArgumentNullException(nameof(speedOverride));
            _logger = logger;
            _localization = localization;
            _safetyZoneConfigLoader = safetyZoneConfigLoader;
        }

        /// <summary>获取多语言格式化字符串</summary>
        private string L(string key, string fallback, params object[] args)
        {
            var format = _localization?.GetResourceOrDefault(key, fallback) ?? fallback;
            return args.Length > 0 ? string.Format(format, args) : format;
        }

        /// <summary>上报进度（Status 可空表示仅写日志）</summary>
        private void ReportProgress(
            IProgress<NeedleAlignerProgressReport> progress,
            string status,
            double percent,
            string detailLog = null)
        {
            progress?.Report(new NeedleAlignerProgressReport(status, percent, detailLog));
            // 文件日志由 ViewModel.AddLog 统一写入，此处不再重复记录
        }

        /// <summary>仅写详细日志到 UI/文件，不更新状态栏文案</summary>
        private void ReportDetail(
            IProgress<NeedleAlignerProgressReport> progress,
            double percent,
            string detailLog)
        {
            ReportProgress(progress, null, percent, detailLog);
        }

        /// <summary>读取当前 Dx/Dy 位置</summary>
        private (double X, double Y) ReadCurrentXY(int dxId, int dyId) =>
            (_motion.GetAxisPosition(dxId), _motion.GetAxisPosition(dyId));

        public IReadOnlyDictionary<string, double> ReadCurrentPositions(int systemNumber)
        {
            var map = ResolveAxisMap();
            var result = new Dictionary<string, double>();

            if (TryGetAxisId(map, "Dx", out int dxId))
                result["Dx"] = _motion.GetAxisPosition(dxId);
            if (TryGetAxisId(map, "Dy", out int dyId))
                result["Dy"] = _motion.GetAxisPosition(dyId);

            foreach (var zName in GetNeedleZAxisNames(systemNumber))
            {
                if (TryGetAxisId(map, zName, out int zId))
                {
                    result[zName] = _motion.GetAxisPosition(zId);
                    break;
                }
            }

            return result;
        }

        public async Task MoveToSafeHeightAsync(NeedleCalibrationParams parameters, int systemNumber, CancellationToken token)
        {
            var zId = ResolveZAxisId(systemNumber);
            var fastSpeed = GetAxisMotionSpeed(zId);
            await MoveZAbsWithApproachAsync(zId, parameters.SafeHeight, fastSpeed, parameters.FineSearchSpeed, token);
        }

        public async Task MoveToAlignPositionAsync(NeedleCalibrationParams parameters, int systemNumber, CancellationToken token)
        {
            var align = GetAlignPosition(parameters, systemNumber);
            await MoveToSafeHeightAsync(parameters, systemNumber, token);

            var map = ResolveAxisMap();
            var dxId = ResolveAxisId(map, "Dx");
            var dyId = ResolveAxisId(map, "Dy");
            var zId = ResolveZAxisId(systemNumber);

            await MoveXYLineAsync(dxId, dyId, align.X, align.Y, GetXYInterpSpeed(dxId, dyId), token);
            await MoveZAbsWithApproachAsync(zId, align.Z, GetAxisMotionSpeed(zId), parameters.FineSearchSpeed, token);
        }

        public async Task MoveToSearchPointXYAsync(NeedleCalibrationParams parameters, int systemNumber, double x, double y, CancellationToken token)
        {
            await MoveToSafeHeightAsync(parameters, systemNumber, token);

            var map = ResolveAxisMap();
            var dxId = ResolveAxisId(map, "Dx");
            var dyId = ResolveAxisId(map, "Dy");
            await MoveXYLineAsync(dxId, dyId, x, y, parameters.SearchSpeed, token);
        }

        public async Task MoveToSearchNeedleHeightAsync(NeedleCalibrationParams parameters, int systemNumber, CancellationToken token)
        {
            var searchZ = GetSearchNeedleHeight(parameters, systemNumber);
            var zId = ResolveZAxisId(systemNumber);
            await MoveZAbsWithApproachAsync(zId, searchZ, GetAxisMotionSpeed(zId), parameters.FineSearchSpeed, token);
        }

        public async Task<NeedleCalibrationResult> ExecuteNeedleCalibrationAsync(
            NeedleCalibrationParams parameters,
            int systemNumber,
            IProgress<NeedleAlignerProgressReport> progress,
            CancellationToken token)
        {
            bool succeeded = false;
            try
            {
                // 阶段 1：Z 抬升至安全高度，水平移动前防碰撞
                ReportProgress(progress,
                    L("NeedleAligner_Status_RaiseSafeHeight", "抬升到安全高度"), 5,
                    L("NeedleAligner_Log_RaiseSafeHeight", "阶段1: 抬升至安全高度 Z={0:F3}", parameters.SafeHeight));
                await MoveToSafeHeightAsync(parameters, systemNumber, token);

                // 阶段 2+3：四点边界扫描 → 拟合中心 → 移至 (X0,Y0)
                ReportProgress(progress, L("NeedleAligner_Status_SearchCenterXY", "搜索中心点XY"), 10);
                var boundaryCenter = await SearchCenterPointAsync(parameters, systemNumber, progress, token);
                if (boundaryCenter == null)
                    return Fail(L("NeedleAligner_Error_SearchCenterFailed", "搜索中心点失败"));

                // 阶段 4：Z 向高度零点检测（双激光同时遮挡触发，并采集实测 XY）
                ReportProgress(progress, L("NeedleAligner_Status_SearchNeedleHeight", "搜索针尖高度"), 65,
                    L("NeedleAligner_Log_ZSearchStart", "阶段4: 边界拟合中心 X0={0:F3} Y0={1:F3}，开始 Z 高度搜索",
                        boundaryCenter.X, boundaryCenter.Y));
                var zOutcome = await SearchNeedleHeightAsync(
                    boundaryCenter, parameters, systemNumber, progress, token);
                if (double.IsNaN(zOutcome.Height))
                    return Fail(L("NeedleAligner_Error_SearchHeightFailed", "搜索针尖高度失败"));

                // 实测中心：优先双激光触发 XY（比边界拟合更准确），否则回退边界拟合
                var measuredCenter = zOutcome.HasDualLaserCenter
                    ? zOutcome.DualLaserCenter
                    : boundaryCenter;
                if (zOutcome.HasDualLaserCenter)
                {
                    double dx = measuredCenter.X - boundaryCenter.X;
                    double dy = measuredCenter.Y - boundaryCenter.Y;
                    ReportDetail(progress, 88,
                        L("NeedleAligner_Log_FitVsDualLaser",
                            "边界拟合 vs 双激光: 拟合({0:F3},{1:F3}) 实测({2:F3},{3:F3}) ΔX={4:F3} ΔY={5:F3}",
                            boundaryCenter.X, boundaryCenter.Y,
                            measuredCenter.X, measuredCenter.Y, dx, dy));
                }

                // 阶段 5：增量法计算本次补偿偏移
                ReportProgress(progress, L("NeedleAligner_Status_CalcCompensation", "计算补偿值"), 90,
                    L("NeedleAligner_Log_FinalMeasured",
                        "最终实测: X={0:F3} Y={1:F3} Z={2:F3}",
                        measuredCenter.X, measuredCenter.Y, zOutcome.Height));
                var compensation = CalculateCompensation(measuredCenter, zOutcome.Height, parameters);

                ReportProgress(progress, L("NeedleAligner_Status_CalibrationDoneMotion", "针头校准完成"), 100);
                await MoveToSafeHeightAsync(parameters, systemNumber, token);

                succeeded = true;
                return new NeedleCalibrationResult
                {
                    Success = true,
                    MeasuredCenter = measuredCenter,
                    MeasuredHeight = zOutcome.Height,
                    Compensation = compensation
                };
            }
            catch (OperationCanceledException)
            {
                return Fail(L("NeedleAligner_Error_CalibrationCancelled", "校准已取消"));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[NeedleAligner] 寻针校准异常");
                return Fail(ex.Message);
            }
            finally
            {
                // 寻针失败（非用户取消）时自动抬升至安全高度
                if (!succeeded && !token.IsCancellationRequested)
                    await TryRaiseToSafeHeightOnFailureAsync(parameters, systemNumber, token);
            }
        }

        /// <summary>寻针失败后安全抬 Z（两段到位），忽略二次异常避免掩盖原始错误</summary>
        private async Task TryRaiseToSafeHeightOnFailureAsync(
            NeedleCalibrationParams parameters,
            int systemNumber,
            CancellationToken token)
        {
            try
            {
                _logger.Warn("[NeedleAligner] 寻针失败，自动抬升至安全高度");
                await MoveToSafeHeightAsync(parameters, systemNumber, token);
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleAligner] 失败后抬升安全高度异常: {ex.Message}");
            }
        }

        public void StopMotion(int systemNumber)
        {
            var map = ResolveAxisMap();
            var ids = new HashSet<int>();
            if (TryGetAxisId(map, "Dx", out int dx)) ids.Add(dx);
            if (TryGetAxisId(map, "Dy", out int dy)) ids.Add(dy);
            ids.Add(ResolveZAxisId(systemNumber));

            foreach (var id in ids)
            {
                try { _motion.StopAxis(id); }
                catch (Exception ex) { _logger.Warn($"[NeedleAligner] 停止轴{id}失败: {ex.Message}"); }
            }
        }

        #region 寻针核心逻辑

        /// <summary>
        /// 阶段 2+3：按 UI 配置的 Sensor 类型逐点双向寻边，Halcon 直线交点拟合中心并移至 (X0,Y0)。
        /// </summary>
        private async Task<PointF?> SearchCenterPointAsync(
            NeedleCalibrationParams parameters,
            int systemNumber,
            IProgress<NeedleAlignerProgressReport> progress,
            CancellationToken token)
        {
            var searchPoints = new[]
            {
                parameters.SearchPoint1,
                parameters.SearchPoint2,
                parameters.SearchPoint3,
                parameters.SearchPoint4
            };

            // 按传感器类型分组：X 传感器边缘点构成 X 线，Y 传感器边缘点构成 Y 线（顺序随 UI 配置，不要求 P1/P2 必先 X）
            var xEdgePoints = new List<PointF>(2);
            var yEdgePoints = new List<PointF>(2);
            var xEdgePointIndices = new List<int>(2);
            var yEdgePointIndices = new List<int>(2);

            var map = ResolveAxisMap();
            var dxId = ResolveAxisId(map, "Dx");
            var dyId = ResolveAxisId(map, "Dy");

            for (int i = 0; i < searchPoints.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                double pointProgress = 10 + i * 12;
                var sensorKind = parameters.GetSearchPointSensorKind(i);
                bool useXSensor = sensorKind == NeedleSearchSensorKind.SensorX;
                int sensorPort = useXSensor ? parameters.SensorDiX : parameters.SensorDiY;

                ReportProgress(progress,
                    useXSensor
                        ? L("NeedleAligner_Status_SearchPointX", "在点{0}进行X方向搜索", i + 1)
                        : L("NeedleAligner_Status_SearchPointY", "在点{0}进行Y方向搜索", i + 1),
                    pointProgress,
                    L("NeedleAligner_Log_SearchPointStart",
                        "── 搜索点{0} 预设({1:F3},{2:F3}) ──",
                        i + 1, searchPoints[i].X, searchPoints[i].Y));

                if (i == 0)
                {
                    await MoveToSearchPointXYAsync(
                        parameters, systemNumber,
                        searchPoints[i].X, searchPoints[i].Y, token);
                    await MoveToSearchNeedleHeightAsync(parameters, systemNumber, token);
                }
                else
                {
                    await MoveXYLineAsync(
                        dxId, dyId,
                        searchPoints[i].X, searchPoints[i].Y,
                        parameters.SearchSpeed, token);
                }

                var (actualX, actualY) = ReadCurrentXY(dxId, dyId);
                ReportDetail(progress, pointProgress + 0.5,
                    L("NeedleAligner_Log_SearchPointArrival",
                        "点{0} 到位: X={1:F3} Y={2:F3} Z寻针高",
                        i + 1, actualX, actualY));

                PointF? edgePoint = useXSensor
                    ? await SearchEdgeAlongXAsync(
                        searchPoints[i], sensorPort, parameters, progress, i, pointProgress, dxId, dyId, token)
                    : await SearchEdgeAlongYAsync(
                        searchPoints[i], sensorPort, parameters, progress, i, pointProgress, dxId, dyId, token);
                if (edgePoint == null)
                {
                    _logger.Error($"[NeedleAligner] 搜索点{i + 1}边缘搜索失败");
                    return null;
                }

                if (useXSensor)
                {
                    xEdgePoints.Add(edgePoint);
                    xEdgePointIndices.Add(i + 1);
                    ReportDetail(progress, pointProgress + 4,
                        L("NeedleAligner_Log_PointMidX",
                            "点{0} X边缘=({1:F3},{2:F3})",
                            i + 1, edgePoint.X, edgePoint.Y));
                }
                else
                {
                    yEdgePoints.Add(edgePoint);
                    yEdgePointIndices.Add(i + 1);
                    ReportDetail(progress, pointProgress + 4,
                        L("NeedleAligner_Log_PointMidY",
                            "点{0} Y边缘=({1:F3},{2:F3})",
                            i + 1, edgePoint.X, edgePoint.Y));
                }
            }

            if (xEdgePoints.Count < 2 || yEdgePoints.Count < 2)
            {
                _logger.Error(
                    $"[NeedleAligner] Halcon 拟合需 X/Y 传感器各至少 2 个边缘点，当前 X={xEdgePoints.Count}(P{string.Join(",", xEdgePointIndices)}) Y={yEdgePoints.Count}(P{string.Join(",", yEdgePointIndices)})");
                return null;
            }

            // Halcon 取各组前两个边缘点（默认配置 P1/P2=X、P3/P4=Y 时与旧项目一致）
            var xLinePoints = new List<PointF> { xEdgePoints[0], xEdgePoints[1] };
            var yLinePoints = new List<PointF> { yEdgePoints[0], yEdgePoints[1] };
            if (xEdgePoints.Count > 2 || yEdgePoints.Count > 2)
            {
                _logger.Warn(
                    $"[NeedleAligner] X/Y 传感器边缘点超过 2 个，Halcon 拟合使用前两个: X=P{xEdgePointIndices[0]}/P{xEdgePointIndices[1]} Y=P{yEdgePointIndices[0]}/P{yEdgePointIndices[1]}");
            }

            var xDetail = string.Join(", ", xLinePoints.Select((p, idx) => $"P{xEdgePointIndices[idx]}=({p.X:F3},{p.Y:F3})"));
            var yDetail = string.Join(", ", yLinePoints.Select((p, idx) => $"P{yEdgePointIndices[idx]}=({p.X:F3},{p.Y:F3})"));
            ReportDetail(progress, 56,
                L("NeedleAligner_Log_HalconEdgeSamples",
                    "Halcon 边缘样本 X线:[{0}] Y线:[{1}]",
                    xDetail, yDetail));

            // Halcon 两直线交点拟合中心
            var halconCenter = CalculateCenterPointWithHalcon(xLinePoints, yLinePoints);
            float x0;
            float y0Coarse;
            if (halconCenter != null)
            {
                x0 = halconCenter.X;
                y0Coarse = halconCenter.Y;
                ReportDetail(progress, 57,
                    L("NeedleAligner_Log_HalconCenterOk",
                        "Halcon 交点中心: X0={0:F3}, Y0={1:F3}", x0, y0Coarse));
            }
            else
            {
                x0 = xLinePoints.Average(p => p.X);
                y0Coarse = yLinePoints.Average(p => p.Y);
                ReportDetail(progress, 57,
                    L("NeedleAligner_Log_HalconCenterFallback",
                        "Halcon 交点失败，回退均值: X0={0:F3}, Y0={1:F3}", x0, y0Coarse));
            }

            // 先移至 Halcon 粗拟合中心，再始终执行 ±2mm 双激光精搜（单路传感器拟合仅作粗定位）
            ReportProgress(progress, L("NeedleAligner_Status_MoveToCoarseCenter", "移动到粗拟合中心点"), 58);
            await MoveXYLineAsync(dxId, dyId, x0, y0Coarse, parameters.SearchSpeed, token);
            await WaitForAxesSettledAsync(new[] { dxId, dyId }, token);
            var (atX, atY) = ReadCurrentXY(dxId, dyId);
            ReportDetail(progress, 58.2,
                L("NeedleAligner_Log_MoveToCoarseCenter",
                    "粗拟合中心到位: X={0:F3} Y={1:F3}", atX, atY));

            await Task.Delay(AxisSettleDelayMs, token);
            LogDualLaserSensorState(
                L("NeedleAligner_Log_CenterSensorCheck", "粗拟合中心传感器"), parameters);

            float y0 = y0Coarse;
            float x0Final = x0;

            // 不因粗中心 DI 读数跳过精搜：单路寻边中心与双激光十字中心存在偏差
            ReportDetail(progress, 58.5,
                L("NeedleAligner_Log_CenterRefineStart",
                    "Halcon 粗中心后 ±{0:F1}mm 双激光精搜",
                    CenterRefineHalfSpanMm));

            ReportProgress(progress, L("NeedleAligner_Status_RefineYAtX0", "X0处Y向精扫"), 58.7);
            var yRefined = await RefineYCenterAtX0Async(
                dxId, dyId, x0, y0Coarse, CenterRefineHalfSpanMm, parameters, progress, token);
            if (yRefined != null)
            {
                float dy = yRefined.Value - y0Coarse;
                y0 = yRefined.Value;
                ReportDetail(progress, 59,
                    L("NeedleAligner_Log_YRefineAtX0",
                        "X0={0:F3} 双激光Y精扫: 粗={1:F3} 精={2:F3} ΔY={3:F3}",
                        x0, y0Coarse, y0, dy));
            }

            ReportProgress(progress, L("NeedleAligner_Status_RefineXAtY0", "Y0处X向精扫"), 59.3);
            var xRefined = await RefineXCenterAtY0Async(
                dxId, dyId, x0, y0, CenterRefineHalfSpanMm, parameters, progress, token);
            if (xRefined != null)
            {
                float dx = xRefined.Value - x0;
                x0Final = xRefined.Value;
                ReportDetail(progress, 59.5,
                    L("NeedleAligner_Log_XRefineAtY0",
                        "Y0={0:F3} 双激光X精扫: 粗={1:F3} 精={2:F3} ΔX={3:F3}",
                        y0, x0, x0Final, dx));
            }

            if (Math.Abs(x0Final - atX) > 0.001 || Math.Abs(y0 - atY) > 0.001)
            {
                await MoveXYLineAsync(dxId, dyId, x0Final, y0, parameters.FineSearchSpeed, token);
                await WaitForAxesSettledAsync(new[] { dxId, dyId }, token);
                (atX, atY) = ReadCurrentXY(dxId, dyId);
                ReportDetail(progress, 59.8,
                    L("NeedleAligner_Log_MoveToFitCenter",
                        "精搜中心到位: X={0:F3} Y={1:F3}", atX, atY));
            }

            LogDualLaserSensorState(
                L("NeedleAligner_Log_RefineCenterSensor", "精搜中心传感器"), parameters);

            ReportDetail(progress, 60,
                L("NeedleAligner_Log_FitCenter", "边界拟合中心: X0={0:F3}, Y0={1:F3}", x0Final, y0));

            return new PointF(x0Final, y0);
        }

        /// <summary>
        /// 在 X0 处用双激光同时触发判据精扫 Y，与手动对中心一致（单路 Y 传感器中值会系统性偏约 1mm）。
        /// </summary>
        private async Task<float?> RefineYCenterAtX0Async(
            int dxId,
            int dyId,
            float x0,
            float yCoarse,
            double halfSpanMm,
            NeedleCalibrationParams parameters,
            IProgress<NeedleAlignerProgressReport> progress,
            CancellationToken token)
        {
            ReportDetail(progress, 57.6,
                L("NeedleAligner_Log_YRefineStart",
                    "── X0={0:F3} 双激光Y精扫 粗Y={1:F3} 半宽±{2:F1}mm ──",
                    x0, yCoarse, halfSpanMm));

            double? yMid = await ScanDualLaserBoundaryMidAsync(
                dxId, dyId, dyId, "Y",
                x0, yCoarse, halfSpanMm, parameters, progress, token);

            return yMid == null ? null : (float?)yMid.Value;
        }

        /// <summary>
        /// 在 Y0 处用双激光同时触发判据精扫 X，修正单路 X 传感器偏心采样的系统性偏差。
        /// </summary>
        private async Task<float?> RefineXCenterAtY0Async(
            int dxId,
            int dyId,
            float xCoarse,
            float y0,
            double halfSpanMm,
            NeedleCalibrationParams parameters,
            IProgress<NeedleAlignerProgressReport> progress,
            CancellationToken token)
        {
            ReportDetail(progress, 58.1,
                L("NeedleAligner_Log_XRefineStart",
                    "── Y0={0:F3} 双激光X精扫 粗X={1:F3} 半宽±{2:F1}mm ──",
                    y0, xCoarse, halfSpanMm));

            // 在 Y0 处沿 X 轴双向扫描，双激光同时触发取中点
            double? xMid = await ScanDualLaserBoundaryMidAsync(
                dxId, dyId, dxId, "X",
                y0, xCoarse, halfSpanMm, parameters, progress, token);

            return xMid == null ? null : (float?)xMid.Value;
        }

        /// <summary>
        /// 使用 Halcon IntersectionLines 计算 X/Y 两条边缘直线的交点（Row=Y, Column=X）。
        /// </summary>
        private PointF? CalculateCenterPointWithHalcon(List<PointF> xPoints, List<PointF> yPoints)
        {
            try
            {
                HTuple intersectionRow, intersectionColumn, isOverlapping;

                HOperatorSet.IntersectionLines(
                    new HTuple(xPoints[0].Y),
                    new HTuple(xPoints[0].X),
                    new HTuple(xPoints[1].Y),
                    new HTuple(xPoints[1].X),
                    new HTuple(yPoints[0].Y),
                    new HTuple(yPoints[0].X),
                    new HTuple(yPoints[1].Y),
                    new HTuple(yPoints[1].X),
                    out intersectionRow,
                    out intersectionColumn,
                    out isOverlapping);

                if (intersectionRow.TupleLength() > 0 && intersectionColumn.TupleLength() > 0 &&
                    !isOverlapping.TupleEqual(1))
                {
                    float centerX = (float)intersectionColumn.D;
                    float centerY = (float)intersectionRow.D;
                    _logger.Info($"[NeedleAligner] Halcon 交点: X={centerX:F3}, Y={centerY:F3}");
                    return new PointF(centerX, centerY);
                }

                _logger.Warn("[NeedleAligner] Halcon 交点失败，直线可能平行或重叠");
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error($"[NeedleAligner] Halcon 直线交点异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 单点 X 向双向寻边：预设点 ±SearchRange 两侧各扫一次入光沿，取中点（与旧项目正反寻边一致）。
        /// </summary>
        private async Task<PointF?> SearchEdgeAlongXAsync(
            PointF searchPoint,
            int sensorPort,
            NeedleCalibrationParams parameters,
            IProgress<NeedleAlignerProgressReport> progress,
            int pointIndex,
            double pointProgress,
            int dxId,
            int dyId,
            CancellationToken token)
        {
            double range = parameters.SearchRange;
            double fineSpeed = parameters.FineSearchSpeed;
            double scanSpan = range * 2;
            double negStart = searchPoint.X - range;
            double posStart = searchPoint.X + range;

            ReportDetail(progress, pointProgress + 1,
                L("NeedleAligner_Log_BoundaryScanStart",
                    "点{0} {1}向扫描: 中心={2:F3} ±{3:F3} DI={4}",
                    pointIndex + 1, "X", searchPoint.X, range, sensorPort));

            // Pass1：负侧起扫 → 正向跨越 2×Range
            ReportDetail(progress, pointProgress + 1.2,
                L("NeedleAligner_Log_BoundaryScanPass",
                    "点{0} {1}向第{2}次: 起={3:F3} 方向{4} 跨度={5:F3}",
                    pointIndex + 1, "X", 1, negStart, "+", scanSpan));
            await MoveScanAxesAsync(dxId, dyId, negStart, searchPoint.Y, fineSpeed, dxId, token);
            await WaitForAxesSettledAsync(new[] { dxId, dyId }, token);

            double forwardEdge = await SearchSingleEdgeRisingAsync(
                dxId, dyId, dxId, scanSpan, fineSpeed, sensorPort,
                progress, pointProgress, pointIndex, "X", 1, token);
            if (double.IsNaN(forwardEdge))
                return null;

            // Pass2：正侧起扫 → 负向跨越 2×Range（必须先移到 +Range，避免仍在针内重复触发）
            ReportDetail(progress, pointProgress + 1.4,
                L("NeedleAligner_Log_BoundaryScanPass",
                    "点{0} {1}向第{2}次: 起={3:F3} 方向{4} 跨度={5:F3}",
                    pointIndex + 1, "X", 2, posStart, "-", scanSpan));
            await MoveScanAxesAsync(dxId, dyId, posStart, searchPoint.Y, fineSpeed, dxId, token);
            await WaitForAxesSettledAsync(new[] { dxId, dyId }, token);

            double backwardEdge = await SearchSingleEdgeRisingAsync(
                dxId, dyId, dxId, -scanSpan, fineSpeed, sensorPort,
                progress, pointProgress, pointIndex, "X", 2, token);
            if (double.IsNaN(backwardEdge))
                return null;

            double centerX = (forwardEdge + backwardEdge) / 2.0;
            ReportDetail(progress, pointProgress + 3,
                L("NeedleAligner_Log_BoundaryMid",
                    "点{0} {1}向边界: Enter1={2:F3} Enter2={3:F3} Mid={4:F3}",
                    pointIndex + 1, "X", forwardEdge, backwardEdge, centerX));

            return new PointF((float)centerX, searchPoint.Y);
        }

        /// <summary>
        /// 单点 Y 向双向寻边：预设点 ±SearchRange 两侧各扫一次入光沿，取中点。
        /// </summary>
        private async Task<PointF?> SearchEdgeAlongYAsync(
            PointF searchPoint,
            int sensorPort,
            NeedleCalibrationParams parameters,
            IProgress<NeedleAlignerProgressReport> progress,
            int pointIndex,
            double pointProgress,
            int dxId,
            int dyId,
            CancellationToken token)
        {
            double range = parameters.SearchRange;
            double fineSpeed = parameters.FineSearchSpeed;
            double scanSpan = range * 2;
            double negStart = searchPoint.Y - range;
            double posStart = searchPoint.Y + range;

            ReportDetail(progress, pointProgress + 1,
                L("NeedleAligner_Log_BoundaryScanStart",
                    "点{0} {1}向扫描: 中心={2:F3} ±{3:F3} DI={4}",
                    pointIndex + 1, "Y", searchPoint.Y, range, sensorPort));

            ReportDetail(progress, pointProgress + 1.2,
                L("NeedleAligner_Log_BoundaryScanPass",
                    "点{0} {1}向第{2}次: 起={3:F3} 方向{4} 跨度={5:F3}",
                    pointIndex + 1, "Y", 1, negStart, "+", scanSpan));
            await MoveScanAxesAsync(dxId, dyId, searchPoint.X, negStart, fineSpeed, dyId, token);
            await WaitForAxesSettledAsync(new[] { dxId, dyId }, token);

            double forwardEdge = await SearchSingleEdgeRisingAsync(
                dxId, dyId, dyId, scanSpan, fineSpeed, sensorPort,
                progress, pointProgress, pointIndex, "Y", 1, token);
            if (double.IsNaN(forwardEdge))
                return null;

            ReportDetail(progress, pointProgress + 1.4,
                L("NeedleAligner_Log_BoundaryScanPass",
                    "点{0} {1}向第{2}次: 起={3:F3} 方向{4} 跨度={5:F3}",
                    pointIndex + 1, "Y", 2, posStart, "-", scanSpan));
            await MoveScanAxesAsync(dxId, dyId, searchPoint.X, posStart, fineSpeed, dyId, token);
            await WaitForAxesSettledAsync(new[] { dxId, dyId }, token);

            double backwardEdge = await SearchSingleEdgeRisingAsync(
                dxId, dyId, dyId, -scanSpan, fineSpeed, sensorPort,
                progress, pointProgress, pointIndex, "Y", 2, token);
            if (double.IsNaN(backwardEdge))
                return null;

            double centerY = (forwardEdge + backwardEdge) / 2.0;
            ReportDetail(progress, pointProgress + 3,
                L("NeedleAligner_Log_BoundaryMid",
                    "点{0} {1}向边界: Enter1={2:F3} Enter2={3:F3} Mid={4:F3}",
                    pointIndex + 1, "Y", forwardEdge, backwardEdge, centerY));

            return new PointF(searchPoint.X, (float)centerY);
        }

        /// <summary>单路传感器上升沿寻边，检测到触发后立即停轴并返回扫描轴坐标</summary>
        private async Task<double> SearchSingleEdgeRisingAsync(
            int dxId,
            int dyId,
            int axisId,
            double searchDistance,
            double speed,
            int sensorPort,
            IProgress<NeedleAlignerProgressReport> progress,
            double pointProgress,
            int pointIndex,
            string axisLabel,
            int passIndex,
            CancellationToken token)
        {
            if (!await StartBoundaryRelativeScanAsync(dxId, dyId, axisId, searchDistance, speed, sensorPort, token))
                return double.NaN;

            bool prevTriggered = false;
            var deadline = DateTime.UtcNow.AddMilliseconds(SensorTimeoutMs);
            while (!token.IsCancellationRequested)
            {
                if (DateTime.UtcNow > deadline)
                {
                    await StopBoundaryScanAxesAsync(dxId, dyId, axisId, token);
                    _logger.Warn($"[NeedleAligner] 单路入光扫描超时 DI={sensorPort}");
                    return double.NaN;
                }

                bool triggered = IsSensorTriggered(sensorPort);
                if (!prevTriggered && triggered)
                {
                    double scanPos = _motion.GetAxisPosition(axisId);
                    StopAxisSafe(axisId);
                    var (x, y) = ReadCurrentXY(dxId, dyId);
                    await StopBoundaryScanAxesAsync(dxId, dyId, axisId, token);
                    ReportDetail(progress, pointProgress + 2.5,
                        L("NeedleAligner_Log_EdgeCapture",
                            "点{0} {1}向第{2}次入光: 扫描轴={3:F3} 位置X={4:F3} Y={5:F3} DI={6}",
                            pointIndex + 1, axisLabel, passIndex, scanPos, x, y, sensorPort));
                    return scanPos;
                }

                prevTriggered = triggered;
                await Task.Delay(BoundarySensorPollMs, token);
            }

            token.ThrowIfCancellationRequested();
            return double.NaN;
        }

        /// <summary>精扫日志点号（与搜索点1~4区分，日志显示为点5）</summary>
        private const int RefineYScanLogPointIndex = 4;

        /// <summary>
        /// 双激光精扫：在固定坐标处沿指定轴双向扫描，以双激光同时触发上升沿取边界中点。
        /// </summary>
        /// <param name="moveAxisId">扫描运动轴 ID（dxId 扫 X / dyId 扫 Y）</param>
        /// <param name="axisLabel">扫描轴标签（"X" 或 "Y"），用于日志</param>
        /// <param name="fixedCoord">固定轴坐标（扫 Y 时为 X0，扫 X 时为 Y0）</param>
        /// <param name="centerCoord">扫描轴中心值（局部精搜起点）</param>
        /// <param name="halfSpanMm">扫描半宽（mm），中心附近 ±halfSpanMm</param>
        private async Task<double?> ScanDualLaserBoundaryMidAsync(
            int dxId,
            int dyId,
            int moveAxisId,
            string axisLabel,
            double fixedCoord,
            double centerCoord,
            double halfSpanMm,
            NeedleCalibrationParams parameters,
            IProgress<NeedleAlignerProgressReport> progress,
            CancellationToken token)
        {
            double fineSpeed = parameters.FineSearchSpeed;

            double halfSpan = halfSpanMm;
            double negStart = centerCoord - halfSpan;
            double posStart = centerCoord + halfSpan;
            double scanSpan = halfSpan * 2;

            int pointIndex = RefineYScanLogPointIndex;
            const double pointProgress = 57.7;

            ReportDetail(progress, pointProgress,
                L("NeedleAligner_Log_DualLaserScanStart",
                    "点{0} 双激光{1}扫描: 固定={2:F3} 中心{1}={3:F3} 负起={4:F3} 正起={5:F3} 跨度={6:F3}",
                    pointIndex + 1, axisLabel, fixedCoord, centerCoord, negStart, posStart, scanSpan));

            // Pass 1：从负侧起扫，预清向负方向，搜索向正方向
            if (moveAxisId == dxId)
                await MoveScanAxesAsync(dxId, dyId, negStart, fixedCoord, fineSpeed, moveAxisId, token);
            else
                await MoveScanAxesAsync(dxId, dyId, fixedCoord, negStart, fineSpeed, moveAxisId, token);

            await PreClearDualLaserZoneAsync(
                moveAxisId, -DualLaserPreClearStepMm, scanSpan, fineSpeed,
                parameters, progress, pointProgress, pointIndex, token);

            double enterPos1 = await SearchDualLaserEdgeAsync(
                dxId, dyId, moveAxisId, scanSpan, fineSpeed, parameters,
                progress, pointProgress, pointIndex, axisLabel, 1, token);
            if (double.IsNaN(enterPos1)) return null;

            // Pass 2：从正侧起扫，预清向正方向，搜索向负方向
            if (moveAxisId == dxId)
                await MoveScanAxesAsync(dxId, dyId, posStart, fixedCoord, fineSpeed, moveAxisId, token);
            else
                await MoveScanAxesAsync(dxId, dyId, fixedCoord, posStart, fineSpeed, moveAxisId, token);

            await PreClearDualLaserZoneAsync(
                moveAxisId, DualLaserPreClearStepMm, scanSpan, fineSpeed,
                parameters, progress, pointProgress, pointIndex, token);

            double enterPos2 = await SearchDualLaserEdgeAsync(
                dxId, dyId, moveAxisId, -scanSpan, fineSpeed, parameters,
                progress, pointProgress, pointIndex, axisLabel, 2, token);
            if (double.IsNaN(enterPos2)) return null;

            double mid = (enterPos1 + enterPos2) / 2.0;
            ReportDetail(progress, pointProgress + 3,
                L("NeedleAligner_Log_DualLaserBoundaryMid",
                    "点{0} 双激光{1}边界: Enter1={2:F3} Enter2={3:F3} Mid={4:F3}",
                    pointIndex + 1, axisLabel, enterPos1, enterPos2, mid));
            return mid;
        }

        /// <summary>双路激光均已灭光（脱离激光区）</summary>
        private bool IsDualLaserZoneClear(NeedleCalibrationParams parameters) =>
            !IsSensorTriggered(parameters.SensorDiX) &&
            !IsSensorTriggered(parameters.SensorDiY);

        /// <summary>
        /// 分步移出激光区直至双路均灭；单向后失败则反方向再试，保证后续上升沿扫描。
        /// </summary>
        private async Task PreClearDualLaserZoneAsync(
            int axisId,
            double stepSignedMm,
            double maxDistance,
            double speed,
            NeedleCalibrationParams parameters,
            IProgress<NeedleAlignerProgressReport> progress,
            double pointProgress,
            int pointIndex,
            CancellationToken token)
        {
            if (IsDualLaserZoneClear(parameters))
                return;

            ReportDetail(progress, pointProgress + 0.8,
                L("NeedleAligner_Log_DualLaserPreclearStart",
                    "点{0} 双激光预清: 任一路入光，分步移出",
                    pointIndex + 1));

            double moved = await PreClearDualLaserOneDirectionAsync(
                axisId, stepSignedMm, maxDistance, speed, parameters, token);
            if (!IsDualLaserZoneClear(parameters))
            {
                ReportDetail(progress, pointProgress + 0.85,
                    L("NeedleAligner_Log_DualLaserPreclearReverse",
                        "点{0} 双激光预清反向再试",
                        pointIndex + 1));
                moved += await PreClearDualLaserOneDirectionAsync(
                    axisId, -stepSignedMm, maxDistance, speed, parameters, token);
            }

            bool cleared = IsDualLaserZoneClear(parameters);
            ReportDetail(progress, pointProgress + 0.9,
                L("NeedleAligner_Log_DualLaserPreclearDone",
                    "点{0} 双激光预清{1}: 移离{2:F3}mm",
                    pointIndex + 1,
                    cleared
                        ? L("NeedleAligner_SensorPreclearOk", "成功")
                        : L("NeedleAligner_SensorPreclearFail", "失败仍入光"),
                    moved));

            if (!cleared)
                _logger.Warn("[NeedleAligner] 双激光预清未完全灭光，继续束内双沿回退");
        }

        /// <summary>单方向分步预清，返回累计移离距离（mm）</summary>
        private async Task<double> PreClearDualLaserOneDirectionAsync(
            int axisId,
            double stepSignedMm,
            double maxDistance,
            double speed,
            NeedleCalibrationParams parameters,
            CancellationToken token)
        {
            double moved = 0;
            var deadline = DateTime.UtcNow.AddMilliseconds(DualLaserPreClearTimeoutMs);
            while (!token.IsCancellationRequested && moved < maxDistance && DateTime.UtcNow <= deadline)
            {
                if (IsDualLaserZoneClear(parameters))
                    break;

                await _motion.MoveRelAsync(axisId, stepSignedMm, speed, token);
                moved += Math.Abs(stepSignedMm);
                await WaitForAxesSettledAsync(new[] { axisId }, token, 1500);
            }

            return moved;
        }

        /// <summary>双激光同时入光沿：起点双灭用上升沿，起点已入光用束内双灭→双入光沿。</summary>
        private async Task<double> SearchDualLaserEdgeAsync(
            int dxId,
            int dyId,
            int axisId,
            double searchDistance,
            double speed,
            NeedleCalibrationParams parameters,
            IProgress<NeedleAlignerProgressReport> progress,
            double pointProgress,
            int pointIndex,
            string axisLabel,
            int passIndex,
            CancellationToken token)
        {
            if (!IsDualLaserZoneClear(parameters))
            {
                return await SearchDualLaserEdgeFromInsideBeamAsync(
                    dxId, dyId, axisId, searchDistance, speed, parameters,
                    progress, pointProgress, pointIndex, axisLabel, passIndex, token);
            }

            ReportDetail(progress, pointProgress + 1.2,
                L("NeedleAligner_Log_DualLaserEdgeOffStart",
                    "点{0} {1}向第{2}次: 双激光均灭，上升沿扫描",
                    pointIndex + 1, axisLabel, passIndex));

            bool prevBothOn = false;
            if (!await StartBoundaryRelativeScanAsync(dxId, dyId, axisId, searchDistance, speed, parameters.SensorDiX, token))
                return double.NaN;

            var deadline = DateTime.UtcNow.AddMilliseconds(SensorTimeoutMs);
            while (!token.IsCancellationRequested)
            {
                if (DateTime.UtcNow > deadline)
                {
                    await StopBoundaryScanAxesAsync(dxId, dyId, axisId, token);
                    _logger.Warn("[NeedleAligner] 双激光入光扫描超时");
                    return double.NaN;
                }

                bool bothOn = AreBothSensorsTriggered(parameters);
                if (!prevBothOn && bothOn)
                {
                    double scanPos = _motion.GetAxisPosition(axisId);
                    StopAxisSafe(axisId);
                    var (x, y) = ReadCurrentXY(dxId, dyId);
                    await StopBoundaryScanAxesAsync(dxId, dyId, axisId, token);
                    ReportDetail(progress, pointProgress + 2.5,
                        L("NeedleAligner_Log_DualLaserEdgeCapture",
                            "点{0} {1}向第{2}次双激光入光: 扫描轴={3:F3} X={4:F3} Y={5:F3}",
                            pointIndex + 1, axisLabel, passIndex, scanPos, x, y));
                    return scanPos;
                }

                prevBothOn = bothOn;
                await Task.Delay(BoundarySensorPollMs, token);
            }

            token.ThrowIfCancellationRequested();
            return double.NaN;
        }

        /// <summary>起点已在激光区内：先等双路均灭，再捕获双激光同时入光沿。</summary>
        private async Task<double> SearchDualLaserEdgeFromInsideBeamAsync(
            int dxId,
            int dyId,
            int axisId,
            double searchDistance,
            double speed,
            NeedleCalibrationParams parameters,
            IProgress<NeedleAlignerProgressReport> progress,
            double pointProgress,
            int pointIndex,
            string axisLabel,
            int passIndex,
            CancellationToken token)
        {
            ReportDetail(progress, pointProgress + 1.2,
                L("NeedleAligner_Log_DualLaserInsideBeamScan",
                    "点{0} {1}向第{2}次: 起点已入光，束内双灭→双入光扫描",
                    pointIndex + 1, axisLabel, passIndex));

            bool prevBothOn = AreBothSensorsTriggered(parameters);
            bool exitedZone = IsDualLaserZoneClear(parameters);

            if (!await StartBoundaryRelativeScanAsync(dxId, dyId, axisId, searchDistance, speed, parameters.SensorDiY, token))
                return double.NaN;

            var deadline = DateTime.UtcNow.AddMilliseconds(SensorTimeoutMs);
            while (!token.IsCancellationRequested)
            {
                if (DateTime.UtcNow > deadline)
                {
                    await StopBoundaryScanAxesAsync(dxId, dyId, axisId, token);
                    return double.NaN;
                }

                bool bothOn = AreBothSensorsTriggered(parameters);
                if (prevBothOn && !bothOn)
                    exitedZone = true;

                if (exitedZone && !prevBothOn && bothOn)
                {
                    double scanPos = _motion.GetAxisPosition(axisId);
                    StopAxisSafe(axisId);
                    var (x, y) = ReadCurrentXY(dxId, dyId);
                    await StopBoundaryScanAxesAsync(dxId, dyId, axisId, token);
                    ReportDetail(progress, pointProgress + 2.5,
                        L("NeedleAligner_Log_DualLaserEdgeInside",
                            "点{0} {1}向第{2}次双激光入光(束内): 扫描轴={3:F3} X={4:F3} Y={5:F3}",
                            pointIndex + 1, axisLabel, passIndex, scanPos, x, y));
                    return scanPos;
                }

                prevBothOn = bothOn;
                await Task.Delay(BoundarySensorPollMs, token);
            }

            token.ThrowIfCancellationRequested();
            return double.NaN;
        }

        /// <summary>下发相对扫描并确认轴已开始位移，避免控制器忙/插补占用时空等传感器</summary>
        private async Task<bool> StartBoundaryRelativeScanAsync(
            int dxId,
            int dyId,
            int axisId,
            double searchDistance,
            double speed,
            int sensorPort,
            CancellationToken token)
        {
            double startPos = _motion.GetAxisPosition(axisId);
            try
            {
                await _motion.MoveRelStartAsync(axisId, searchDistance, speed);
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleAligner] 相对扫描启动失败 DI={sensorPort}: {ex.Message}");
                return false;
            }

            var deadline = DateTime.UtcNow.AddMilliseconds(BoundaryScanMotionStartTimeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                token.ThrowIfCancellationRequested();
                if (Math.Abs(_motion.GetAxisPosition(axisId) - startPos) > 0.01)
                    return true;

                await Task.Delay(BoundarySensorPollMs, token);
            }

            _logger.Warn($"[NeedleAligner] 相对扫描未启动 DI={sensorPort} 轴{axisId} 距离={searchDistance:F3}");
            await StopBoundaryScanAxesAsync(dxId, dyId, axisId, token);
            return false;
        }

        /// <summary>
        /// 边界扫描段 XY 定位：仅移动需位移的轴，双轴均需动时用多轴同步；到位后等待稳定。
        /// </summary>
        private async Task MoveScanAxesAsync(
            int dxId,
            int dyId,
            double x,
            double y,
            double speed,
            int moveAxisId,
            CancellationToken token)
        {
            const double posTolerance = 0.002;
            double curX = _motion.GetAxisPosition(dxId);
            double curY = _motion.GetAxisPosition(dyId);
            bool xNeedsMove = Math.Abs(curX - x) > posTolerance;
            bool yNeedsMove = Math.Abs(curY - y) > posTolerance;

            if (xNeedsMove && yNeedsMove)
            {
                await _motion.MoveAbsMultiAxisAsync(new[]
                {
                    (dxId, x, speed),
                    (dyId, y, speed)
                }, token);
                return;
            }

            // 仅扫描轴位移，避免对已在位的轴重复 MoveAbs 导致控制器拒动
            if (moveAxisId == dxId && xNeedsMove)
                await _motion.MoveAbsAsync(dxId, x, speed, token);
            else if (moveAxisId == dyId && yNeedsMove)
                await _motion.MoveAbsAsync(dyId, y, speed, token);
        }

        /// <summary>
        /// 传感器触发后停扫描轴并同步停另一轴，等待位置稳定后再发下一段绝对运动。
        /// </summary>
        private async Task StopBoundaryScanAxesAsync(int dxId, int dyId, int activeAxisId, CancellationToken token)
        {
            StopAxisSafe(activeAxisId);
            if (activeAxisId != dxId) StopAxisSafe(dxId);
            if (activeAxisId != dyId) StopAxisSafe(dyId);
            await WaitForAxesSettledAsync(new[] { dxId, dyId }, token);
        }

        /// <summary>
        /// 停轴后等待编码器位置连续稳定（直接读 GetAxisPosition，不依赖 UI 轮询的 IsMoving）。
        /// </summary>
        private async Task WaitForAxesSettledAsync(IEnumerable<int> axisIds, CancellationToken token, int timeoutMs = 5000)
        {
            var ids = axisIds.Distinct().ToArray();
            var lastPositions = ids.ToDictionary(id => id, id => _motion.GetAxisPosition(id));
            int stableCount = 0;
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (DateTime.UtcNow < deadline)
            {
                token.ThrowIfCancellationRequested();
                bool moved = false;

                foreach (var id in ids)
                {
                    double pos = _motion.GetAxisPosition(id);
                    if (Math.Abs(pos - lastPositions[id]) > 0.002)
                    {
                        lastPositions[id] = pos;
                        moved = true;
                    }
                }

                if (!moved)
                {
                    stableCount++;
                    if (stableCount >= AxisSettleStableReads)
                    {
                        await Task.Delay(AxisSettleDelayMs, token);
                        return;
                    }
                }
                else
                {
                    stableCount = 0;
                }

                await Task.Delay(SensorPollMs, token);
            }

            _logger.Warn($"[NeedleAligner] 停轴后等待位置稳定超时({timeoutMs}ms)");
        }

        /// <summary>等待轴空闲（CheckDone），短超时避免阻塞过久</summary>
        private async Task WaitForAxesIdleAsync(IEnumerable<int> axisIds, CancellationToken token, int timeoutMs = 2000)
        {
            await WaitForAxesSettledAsync(axisIds, token, timeoutMs);
        }

        /// <summary>Z 高度搜索结果：双激光触发高度与实测 XY</summary>
        private sealed class ZHeightSearchOutcome
        {
            public double Height { get; init; } = double.NaN;
            public PointF DualLaserCenter { get; init; }
            public bool HasDualLaserCenter { get; init; }
        }

        /// <summary>
        /// 阶段 4：在拟合中心 (X0,Y0) 多次 Z 下探，双激光同时遮挡时记录 XYZ，取中值。
        /// </summary>
        private async Task<ZHeightSearchOutcome> SearchNeedleHeightAsync(
            PointF boundaryCenter,
            NeedleCalibrationParams parameters,
            int systemNumber,
            IProgress<NeedleAlignerProgressReport> progress,
            CancellationToken token)
        {
            var map = ResolveAxisMap();
            var dxId = ResolveAxisId(map, "Dx");
            var dyId = ResolveAxisId(map, "Dy");
            var zId = ResolveZAxisId(systemNumber);
            bool downCoordIncrease = IsZDownCoordIncrease(parameters, systemNumber);
            double searchZ = GetSearchNeedleHeight(parameters, systemNumber);
            double descStartZ = GetZDescendStartHeight(searchZ, downCoordIncrease);
            double fineTargetZ = GetZFineDescendTarget(
                parameters, systemNumber, downCoordIncrease, out double rawFineTargetZ);
            int count = Math.Max(1, parameters.ZSearchCount);
            var zSamples = new List<double>(count);
            var xySamples = new List<PointF>(count);

            var alignPos = GetAlignPosition(parameters, systemNumber);
            ReportDetail(progress, 65.5,
                L("NeedleAligner_Log_ZSearchContext",
                    "Z高度搜索: 寻针Z={0:F3} 对针位Z={1:F3} 安全Z={2:F3} 下探量={3:F3} 最低Z={4:F3}",
                    searchZ, alignPos.Z, parameters.SafeHeight, parameters.ZProbeDescentHeight, parameters.ZMinHeight));

            ReportDetail(progress, 65.8,
                L("NeedleAligner_Log_ZDirInferred",
                    "Z向推断: 安全Z{0}寻针Z → 物理下探=坐标{1}",
                    parameters.SafeHeight < searchZ
                        ? L("NeedleAligner_ZDir_SafeBelowSearch", "低于")
                        : L("NeedleAligner_ZDir_SafeAboveSearch", "高于"),
                    downCoordIncrease
                        ? L("NeedleAligner_ZDir_CoordIncrease", "增大")
                        : L("NeedleAligner_ZDir_CoordDecrease", "减小")));

            ReportDetail(progress, 66,
                L("NeedleAligner_Log_ZSearchParams",
                    "Z搜索参数: 寻针Z={0:F3} 起始Z={1:F3}(上方{2:F0}mm) 下探极限Z={3:F3} 次数={4} 向下{5}",
                    searchZ, descStartZ, ZDescendStartOffsetAboveSearchMm, fineTargetZ, count,
                    downCoordIncrease
                        ? L("NeedleAligner_ZDir_CoordIncrease", "坐标增大")
                        : L("NeedleAligner_ZDir_CoordDecrease", "坐标减小")));

            if (Math.Abs(fineTargetZ - rawFineTargetZ) > 0.0001)
            {
                ReportDetail(progress, 66.1,
                    L("NeedleAligner_Log_ZFineTargetClamped",
                        "寻针Z+下探Z={0:F3} 超过最低高度 {1:F3}，下探极限取最低高度",
                        rawFineTargetZ, parameters.ZMinHeight));
            }

            for (int i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested();
                ReportProgress(progress,
                    L("NeedleAligner_Status_ZHeightSearch", "第 {0}/{1} 次高度定位", i + 1, count),
                    65 + i * (20.0 / count));

                // 每次采样前回到「寻针高度上方 5mm」起始高度
                double zBeforeReturn = _motion.GetAxisPosition(zId);
                if (Math.Abs(zBeforeReturn - descStartZ) > 0.01)
                {
                    ReportDetail(progress, 65.5 + i * 0.2,
                        L("NeedleAligner_Log_ZSampleReturnStart",
                            "第{0}次采样: 当前Z={1:F3} → 回到起始Z={2:F3}(寻针上方{3:F0}mm)",
                            i + 1, zBeforeReturn, descStartZ, ZDescendStartOffsetAboveSearchMm));
                    await MoveZToDescendStartAsync(zId, descStartZ, parameters.SearchSpeed, token);
                }

                var sample = await SearchSingleZHeightSampleAsync(
                    zId, dxId, dyId, searchZ, descStartZ, fineTargetZ, parameters, downCoordIncrease, progress, i, token);
                if (sample == null)
                {
                    _logger.Warn($"[NeedleAligner] 第{i + 1}次 Z 高度采样失败");
                    continue;
                }

                zSamples.Add(sample.Value.Z);
                xySamples.Add(new PointF((float)sample.Value.X, (float)sample.Value.Y));
                ReportDetail(progress, 65 + (i + 1) * (20.0 / count),
                    L("NeedleAligner_Log_DualLaserTrigger",
                        "第{0}次双激光同时亮: X={1:F3} Y={2:F3} Z={3:F3}",
                        i + 1, sample.Value.X, sample.Value.Y, sample.Value.Z));
            }

            if (zSamples.Count == 0)
                return new ZHeightSearchOutcome();

            // 多次采样取算术平均
            double z0 = zSamples.Average();
            var dualCenter = AggregateXYSamples(xySamples);
            await MoveToSafeHeightAsync(parameters, systemNumber, token);

            ReportDetail(progress, 84,
                L("NeedleAligner_Log_ZSearchResult",
                    "Z搜索汇总: Z0={0:F3}(均值) 寻针Z={1:F3} ΔZ={2:F3} 双激光XY X={3:F3} Y={4:F3} (有效{5}/{6})",
                    z0, searchZ, z0 - searchZ, dualCenter.X, dualCenter.Y, zSamples.Count, count));

            return new ZHeightSearchOutcome
            {
                Height = z0,
                DualLaserCenter = dualCenter,
                HasDualLaserCenter = true
            };
        }

        /// <summary>双激光触发瞬间采样的 XYZ</summary>
        private readonly struct DualLaserSample
        {
            public double X { get; init; }
            public double Y { get; init; }
            public double Z { get; init; }
        }

        /// <summary>
        /// 单次 Z 高度采样：上抬至寻针上方 5mm → 双灭则连续下探至绝对极限(寻针+下探高度) → 双亮停轴记录。
        /// </summary>
        private async Task<DualLaserSample?> SearchSingleZHeightSampleAsync(
            int zId,
            int dxId,
            int dyId,
            double searchZ,
            double descStartZ,
            double fineTargetZ,
            NeedleCalibrationParams parameters,
            bool downCoordIncrease,
            IProgress<NeedleAlignerProgressReport> progress,
            int sampleIndex,
            CancellationToken token)
        {
            double currentZ = _motion.GetAxisPosition(zId);

            // 1. 连续上抬至寻针高度上方 5mm（绝对运动，非寸动）
            if (Math.Abs(currentZ - descStartZ) > 0.01)
            {
                ReportDetail(progress, 66 + sampleIndex * 0.3,
                    L("NeedleAligner_Log_ZSampleMoveToStart",
                        "第{0}次Z采样: 当前Z={1:F3} → 上抬至起始Z={2:F3}(寻针Z={3:F3}上方{4:F0}mm)",
                        sampleIndex + 1, currentZ, descStartZ, searchZ, ZDescendStartOffsetAboveSearchMm));
                await MoveZToDescendStartAsync(zId, descStartZ, parameters.SearchSpeed, token);
            }
            else
            {
                ReportDetail(progress, 66 + sampleIndex * 0.3,
                    L("NeedleAligner_Log_ZSampleAtStart",
                        "第{0}次Z采样: 已在起始Z={1:F3}(寻针Z={2:F3}上方{3:F0}mm)",
                        sampleIndex + 1, descStartZ, searchZ, ZDescendStartOffsetAboveSearchMm));
            }

            var sensorState = ReadDualLaserSensorState(parameters);
            LogDualLaserSensorState(
                L("NeedleAligner_Log_ZSampleStartSensor", "Z下探起点传感器"), parameters);
            string stateSummary = sensorState.BothOn
                ? L("NeedleAligner_DualLaser_BothOn", "双亮")
                : sensorState.BothOff
                    ? L("NeedleAligner_DualLaser_BothOff", "双灭")
                    : L("NeedleAligner_DualLaser_SingleOn", "仅单路亮");
            string xLabel = sensorState.XOn
                ? L("NeedleAligner_SensorOn", "亮")
                : L("NeedleAligner_SensorOff", "灭");
            string yLabel = sensorState.YOn
                ? L("NeedleAligner_SensorOn", "亮")
                : L("NeedleAligner_SensorOff", "灭");
            ReportDetail(progress, 66.1 + sampleIndex * 0.3,
                L("NeedleAligner_Log_ZSampleStartSensorDiag",
                    "第{0}次起点激光状态: {1} (X={2} Y={3})",
                    sampleIndex + 1, stateSummary, xLabel, yLabel));

            // 双亮(非双灭)时仍继续下探，用上升沿捕获，不因单路常亮误跳过
            if (sensorState.BothOn)
            {
                ReportDetail(progress, 66.12 + sampleIndex * 0.3,
                    L("NeedleAligner_Log_ZSampleStartBothOn",
                        "第{0}次起点双激光已同时亮，连续下探等待上升沿触发",
                        sampleIndex + 1));
            }
            else if (!sensorState.BothOff)
            {
                ReportDetail(progress, 66.12 + sampleIndex * 0.3,
                    L("NeedleAligner_Log_ZSampleStartSingleOn",
                        "第{0}次起点仅单路亮(非双亮)，连续下探等待双亮",
                        sampleIndex + 1));
            }

            double zBeforeDescend = _motion.GetAxisPosition(zId);
            double descendDistance = fineTargetZ - zBeforeDescend;
            if (Math.Abs(descendDistance) < 0.001)
            {
                _logger.Warn($"[NeedleAligner] 第{sampleIndex + 1}次 Z 已在下探极限({fineTargetZ:F3})，跳过");
                return null;
            }

            if (downCoordIncrease != (descendDistance > 0))
            {
                _logger.Error(
                    $"[NeedleAligner] Z下探方向异常: 起始Z={zBeforeDescend:F3} 极限Z={fineTargetZ:F3} ΔZ={descendDistance:F3} downCoordIncrease={downCoordIncrease}");
                return null;
            }

            ReportDetail(progress, 66.2 + sampleIndex * 0.3,
                L("NeedleAligner_Log_ZSampleDescendStart",
                    "第{0}次Z连续下探(SearchSpeed): 起Z={1:F3} 绝对极限Z={2:F3}(寻针+下探) ΔZ={3:F3}",
                    sampleIndex + 1, zBeforeDescend, fineTargetZ, descendDistance));

            // 2. 连续下探至绝对极限，同步检测双激光同时亮
            await _motion.MoveRelStartAsync(zId, descendDistance, parameters.FineSearchSpeed);

            // 起点双灭：可直接捕获双亮；起点双亮：须先见双灭再双亮（上升沿）
            bool armedForTrigger = sensorState.BothOff;
            var deadline = DateTime.UtcNow.AddMilliseconds(SensorTimeoutMs);
            while (!token.IsCancellationRequested)
            {
                if (DateTime.UtcNow > deadline)
                {
                    StopAxisSafe(zId);
                    await WaitForAxesSettledAsync(new[] { zId }, token);
                    LogDualLaserSensorState(
                        L("NeedleAligner_Log_ZSampleTimeout", "Z采样超时"), parameters);
                    _logger.Warn($"[NeedleAligner] 第{sampleIndex + 1}次 Z 高度搜索超时");
                    return null;
                }

                bool bothOn = AreBothSensorsTriggered(parameters);
                double zNow = _motion.GetAxisPosition(zId);
                double traveledMm = downCoordIncrease
                    ? zNow - zBeforeDescend
                    : zBeforeDescend - zNow;

                if (!bothOn)
                    armedForTrigger = true;

                if (armedForTrigger && bothOn && traveledMm >= ZDescendMinTravelBeforeTriggerMm)
                {
                    var (x, y) = ReadCurrentXY(dxId, dyId);
                    StopAxisSafe(zId);
                    await WaitForAxesSettledAsync(new[] { zId }, token);
                    ReportDetail(progress, 66.5 + sampleIndex * 0.3,
                        L("NeedleAligner_Log_ZSampleTrigger",
                            "第{0}次双激光同时亮停轴: Z={1:F3} 相对寻针Z Δ={2:F3} 下探行程={3:F3}mm",
                            sampleIndex + 1, zNow, zNow - searchZ, traveledMm));
                    return new DualLaserSample { X = x, Y = y, Z = zNow };
                }

                await Task.Delay(SensorPollMs, token);
            }

            token.ThrowIfCancellationRequested();
            return null;
        }

        /// <summary>双激光 DI 状态快照（低电平=入光=亮）</summary>
        private readonly struct DualLaserSensorSnapshot
        {
            public bool XOn { get; init; }
            public bool YOn { get; init; }
            public bool BothOn => XOn && YOn;
            public bool BothOff => !XOn && !YOn;
        }

        private DualLaserSensorSnapshot ReadDualLaserSensorState(NeedleCalibrationParams parameters) =>
            new DualLaserSensorSnapshot
            {
                XOn = IsSensorTriggered(parameters.SensorDiX),
                YOn = IsSensorTriggered(parameters.SensorDiY)
            };

        /// <summary>连续移动到 Z 下探起始高度（SearchSpeed 绝对运动）</summary>
        private async Task MoveZToDescendStartAsync(
            int zId, double descStartZ, double speed, CancellationToken token)
        {
            await _motion.MoveAbsAsync(zId, descStartZ, speed, token);
            await WaitForAxesSettledAsync(new[] { zId }, token);
        }

        /// <summary>记录 X/Y 传感器 DI 状态（低电平=入光触发）</summary>
        private void LogDualLaserSensorState(string context, NeedleCalibrationParams parameters)
        {
            bool xOn = IsSensorTriggered(parameters.SensorDiX);
            bool yOn = IsSensorTriggered(parameters.SensorDiY);
            _logger.Info(
                $"[NeedleAligner] {context}: DI{parameters.SensorDiX}(X)={(xOn ? "ON" : "OFF")} DI{parameters.SensorDiY}(Y)={(yOn ? "ON" : "OFF")}");
        }

        /// <summary>对双激光 XY 采样取平均</summary>
        private static PointF AggregateXYSamples(IReadOnlyList<PointF> samples)
        {
            if (samples == null || samples.Count == 0)
                return new PointF(0, 0);
            return new PointF(
                (float)samples.Average(p => p.X),
                (float)samples.Average(p => p.Y));
        }

        /// <summary>寻针高度上方（物理上抬）5mm 的起始 Z 坐标</summary>
        private static double GetZDescendStartHeight(double searchZ, bool downCoordIncrease) =>
            downCoordIncrease
                ? searchZ - ZDescendStartOffsetAboveSearchMm
                : searchZ + ZDescendStartOffsetAboveSearchMm;

        /// <summary>
        /// 物理下探极限 Z：寻针高度 + 下探高度；超过 ZMinHeight 时取 ZMinHeight。
        /// </summary>
        private static double GetZFineDescendTarget(
            NeedleCalibrationParams parameters,
            int systemNumber,
            bool downCoordIncrease,
            out double unclampedZ)
        {
            double search = GetSearchNeedleHeight(parameters, systemNumber);
            double probe = parameters.ZProbeDescentHeight;
            unclampedZ = downCoordIncrease ? search + probe : search - probe;
            double minHeight = parameters.ZMinHeight;

            if (downCoordIncrease)
            {
                return unclampedZ > minHeight ? minHeight : unclampedZ;
            }

            return unclampedZ < minHeight ? minHeight : unclampedZ;
        }

        /// <summary>
        /// 物理下探是否使 Z 坐标增大：由安全高度与寻针高度相对位置推断（与 MoveToSafeHeight 一致，不读 InvertedDirection）。
        /// </summary>
        private static bool IsZDownCoordIncrease(NeedleCalibrationParams parameters, int systemNumber)
        {
            double searchZ = GetSearchNeedleHeight(parameters, systemNumber);
            return parameters.SafeHeight < searchZ;
        }

        /// <summary>双路激光同时被针头遮挡（X+Y 传感器均触发）</summary>
        private bool AreBothSensorsTriggered(NeedleCalibrationParams parameters) =>
            IsSensorTriggered(parameters.SensorDiX) &&
            IsSensorTriggered(parameters.SensorDiY);

        /// <summary>当前系统 Z 向寻探高度（针头进入十字激光检测平面）</summary>
        private static double GetSearchNeedleHeight(NeedleCalibrationParams parameters, int systemNumber) =>
            systemNumber == 1 ? parameters.SearchNeedleHeightSystem1 : parameters.SearchNeedleHeightSystem2;

        /// <summary>增量法：本次测量值相对固定基准 ReferenceXYZ 的偏移，取反作为补偿增量</summary>
        private static PointF CalculateCompensation(PointF measured, double measuredHeight, NeedleCalibrationParams parameters)
        {
            float deltaX = measured.X - parameters.ReferenceXYZ.X;
            float deltaY = measured.Y - parameters.ReferenceXYZ.Y;
            float deltaZ = (float)measuredHeight - parameters.ReferenceXYZ.Z;
            return new PointF(-deltaX, -deltaY, -deltaZ);
        }

        /// <summary>安全停轴，忽略单次停轴异常以保证急停路径畅通</summary>
        private void StopAxisSafe(int axisId)
        {
            try { _motion.StopAxis(axisId); }
            catch (Exception ex) { _logger.Warn($"[NeedleAligner] 停止轴{axisId}失败: {ex.Message}"); }
        }

        #endregion

        #region 速度与插补运动

        /// <summary>
        /// 轴设置界面 Motion.MaxSpeed，并乘以全局速度百分比（与 StationTaskBase 一致）。
        /// </summary>
        private double GetAxisMotionSpeed(int logicalAxisId)
        {
            var cfg = _motion.GetAxisConfigurations().FirstOrDefault(a => a.LogicalId == logicalAxisId);
            if (cfg == null)
                return 10.0 * (_speedOverride.SpeedPercent / 100.0);

            var baseSpeed = _axisParameterService.GetAxisSpeed(cfg.CardId, cfg.AxisId);
            return baseSpeed * (_speedOverride.SpeedPercent / 100.0);
        }

        /// <summary>XY 插补速度：取 Dx/Dy 轴设置速度的较小值（均已含全局百分比）。</summary>
        private double GetXYInterpSpeed(int dxId, int dyId) =>
            Math.Min(GetAxisMotionSpeed(dxId), GetAxisMotionSpeed(dyId));

        /// <summary>解析 Dx 所在插补系 CoordId（与 VisionCapture 一致）。</summary>
        private int ResolveCoordId()
        {
            if (_coordIdCache.HasValue)
                return _coordIdCache.Value;

            var axisConfigs = _motion.GetAxisConfigurations();
            var dxConfig = axisConfigs.FirstOrDefault(a => a.Name == "Dx");
            if (dxConfig == null)
            {
                _logger.Warn("[NeedleAligner] 未找到 Dx 轴配置，CoordId 回退 0");
                _coordIdCache = 0;
                return 0;
            }

            foreach (var sys in _axisParameterService.LoadInterpolationSystems())
            {
                foreach (var axisEntry in sys.Axes)
                {
                    var parts = axisEntry.Split('-');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int actAxisId) && actAxisId == dxConfig.AxisId)
                    {
                        _coordIdCache = sys.CoordId;
                        return sys.CoordId;
                    }
                }
            }

            _logger.Warn($"[NeedleAligner] Dx 不在插补系中，CoordId 回退 0");
            _coordIdCache = 0;
            return 0;
        }

        /// <summary>XY 同步绝对运动（插补）；MoveLineAbsAsync 内部已 WaitForCoordDone。</summary>
        private Task MoveXYLineAsync(int dxId, int dyId, double x, double y, double velocity, CancellationToken token)
        {
            int coordId = ResolveCoordId();
            return _motion.MoveLineAbsAsync(coordId, new[] { dxId, dyId }, new[] { x, y }, velocity, token);
        }

        /// <summary>
        /// Z 两段到位：距目标 ZApproachGapMm 以内用慢速，此前用快速（安全高度/寻针高度）。
        /// </summary>
        private async Task MoveZAbsWithApproachAsync(
            int zId,
            double targetZ,
            double fastSpeed,
            double slowSpeed,
            CancellationToken token)
        {
            double current = _motion.GetAxisPosition(zId);
            double delta = targetZ - current;
            if (Math.Abs(delta) <= ZApproachGapMm)
            {
                await _motion.MoveAbsAsync(zId, targetZ, slowSpeed, token);
                return;
            }

            double via = delta > 0 ? targetZ - ZApproachGapMm : targetZ + ZApproachGapMm;
            await _motion.MoveAbsAsync(zId, via, fastSpeed, token);
            await _motion.MoveAbsAsync(zId, targetZ, slowSpeed, token);
        }

        #endregion

        #region 传感器与轴解析

        /// <summary>
        /// 检查指定 DI 是否入光触发。
        /// 信号链：dmc_read_inbit raw=0→有信号，raw=1→无信号；
        /// Leisai GetDi 已归一化为 1=有信号；MotionService.ReadDi 返回 true=有信号。此处不再取反。
        /// </summary>
        private bool IsSensorTriggered(int port)
        {
            if (port < 0)
            {
                _logger.Warn($"[NeedleAligner] 传感器 DI 端口号无效: {port}");
                return false;
            }

            try
            {
                return _motion.ReadDi(port);
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleAligner] 读取 DI{port} 失败: {ex.Message}");
                return false;
            }
        }

        private Dictionary<string, int> ResolveAxisMap()
        {
            if (_axisIdCache != null) return _axisIdCache;

            _axisIdCache = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var cfg in _motion.GetAxisConfigurations())
            {
                if (!_axisIdCache.ContainsKey(cfg.Name))
                    _axisIdCache[cfg.Name] = cfg.LogicalId;
            }

            return _axisIdCache;
        }

        private static int ResolveAxisId(Dictionary<string, int> map, string name)
        {
            if (TryGetAxisId(map, name, out int id)) return id;
            throw new InvalidOperationException($"未找到轴配置: {name} ({StationIdentifier})");
        }

        private int ResolveZAxisId(int systemNumber)
        {
            var map = ResolveAxisMap();
            foreach (var name in GetNeedleZAxisNames(systemNumber))
            {
                if (TryGetAxisId(map, name, out int id))
                    return id;
            }

            throw new InvalidOperationException($"未找到系统{systemNumber}针尖Z轴 (Dz₂/Dz₃)");
        }

        private static bool TryGetAxisId(Dictionary<string, int> map, string name, out int axisId)
        {
            if (map.TryGetValue(name, out axisId))
                return true;

            axisId = -1;
            return false;
        }

        private static string[] GetNeedleZAxisNames(int systemNumber) =>
            systemNumber == 1
                ? new[] { "Dz₂", "Dz2" }
                : new[] { "Dz₃", "Dz3" };

        private static PointF GetAlignPosition(NeedleCalibrationParams parameters, int systemNumber) =>
            systemNumber == 1 ? parameters.AlignPositionSystem1 : parameters.AlignPositionSystem2;

        private static NeedleCalibrationResult Fail(string message) =>
            new() { Success = false, ErrorMessage = message };

        #endregion
    }
}
#else
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Models;

namespace Module.Services
{
    /// <summary>
    /// Halcon SDK 未安装时的占位实现，确保 INeedleAlignerMotionService 可正常解析
    /// </summary>
    public class NeedleAlignerMotionService : INeedleAlignerMotionService
    {
        public IReadOnlyDictionary<string, double> ReadCurrentPositions(int systemNumber)
            => new Dictionary<string, double>();

        public Task MoveToAlignPositionAsync(NeedleCalibrationParams parameters, int systemNumber, CancellationToken token)
            => Task.CompletedTask;

        public Task MoveToSafeHeightAsync(NeedleCalibrationParams parameters, int systemNumber, CancellationToken token)
            => Task.CompletedTask;

        public Task MoveToSearchPointXYAsync(NeedleCalibrationParams parameters, int systemNumber, double x, double y, CancellationToken token)
            => Task.CompletedTask;

        public Task MoveToSearchNeedleHeightAsync(NeedleCalibrationParams parameters, int systemNumber, CancellationToken token)
            => Task.CompletedTask;

        public Task<NeedleCalibrationResult> ExecuteNeedleCalibrationAsync(
            NeedleCalibrationParams parameters, int systemNumber,
            IProgress<NeedleAlignerProgressReport> progress, CancellationToken token)
            => Task.FromResult(new NeedleCalibrationResult { Success = false, ErrorMessage = "Halcon SDK not available" });

        public void StopMotion(int systemNumber) { }
    }
}
#endif
