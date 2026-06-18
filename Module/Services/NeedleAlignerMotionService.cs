#if HAS_HALCON
using Core.Abstraction;
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
    /// 针头对针运动实现：使用 IMotionService 直接控制 Dx/Dy/Dz₂/Dz₃
    /// 五阶段流程：①Z 安全高度 → ②四点 XY 边界扫描 → ③拟合中心 → ④Z 零点检测 → ⑤增量补偿
    /// </summary>
    public class NeedleAlignerMotionService : INeedleAlignerMotionService
    {
        private const string StationIdentifier = "DispenserStation";
        private const int SensorPollMs = 20;
        private const int SensorTimeoutMs = 60000;
        /// <summary>Z 接近目标前减速段长度（mm）</summary>
        private const double ZApproachGapMm = 5.0;

        private readonly IMotionService _motion;
        private readonly IAxisParameterService _axisParameterService;
        private readonly ISpeedOverrideService _speedOverride;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localization;
        private Dictionary<string, int> _axisIdCache;
        private int? _coordIdCache;

        public NeedleAlignerMotionService(
            IMotionService motion,
            IAxisParameterService axisParameterService,
            ISpeedOverrideService speedOverride,
            ILoggerService logger,
            ILocalizationService localization)
        {
            _motion = motion ?? throw new ArgumentNullException(nameof(motion));
            _axisParameterService = axisParameterService ?? throw new ArgumentNullException(nameof(axisParameterService));
            _speedOverride = speedOverride ?? throw new ArgumentNullException(nameof(speedOverride));
            _logger = logger;
            _localization = localization;
        }

        /// <summary>获取多语言格式化字符串</summary>
        private string L(string key, string fallback, params object[] args)
        {
            var format = _localization?.GetResourceOrDefault(key, fallback) ?? fallback;
            return args.Length > 0 ? string.Format(format, args) : format;
        }

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
            await MoveToSearchPointXYAsync(parameters, systemNumber, x, y, useAxisMotionSpeed: false, token);
        }

        /// <summary>
        /// 安全移动到搜索点 XY：先抬安全高度，再 XY 插补。
        /// 第一个搜索点使用轴设置 MaxSpeed×全局速度百分比；其余使用参数 SearchSpeed。
        /// </summary>
        private async Task MoveToSearchPointXYAsync(
            NeedleCalibrationParams parameters,
            int systemNumber,
            double x,
            double y,
            bool useAxisMotionSpeed,
            CancellationToken token)
        {
            await MoveToSafeHeightAsync(parameters, systemNumber, token);

            var map = ResolveAxisMap();
            var dxId = ResolveAxisId(map, "Dx");
            var dyId = ResolveAxisId(map, "Dy");
            double velocity = useAxisMotionSpeed
                ? GetXYInterpSpeed(dxId, dyId)
                : parameters.SearchSpeed;

            await MoveXYLineAsync(dxId, dyId, x, y, velocity, token);
        }

        public async Task MoveToSearchNeedleHeightAsync(NeedleCalibrationParams parameters, int systemNumber, CancellationToken token)
        {
            var align = GetAlignPosition(parameters, systemNumber);
            var zId = ResolveZAxisId(systemNumber);
            await MoveZAbsWithApproachAsync(zId, align.Z, GetAxisMotionSpeed(zId), parameters.FineSearchSpeed, token);
        }

        public async Task<NeedleCalibrationResult> ExecuteNeedleCalibrationAsync(
            NeedleCalibrationParams parameters,
            int systemNumber,
            IProgress<(string Status, double Progress)> progress,
            CancellationToken token)
        {
            try
            {
                // 阶段 1：Z 抬升至安全高度，水平移动前防碰撞
                progress?.Report((L("NeedleAligner_Status_RaiseSafeHeight", "抬升到安全高度"), 5));
                await MoveToSafeHeightAsync(parameters, systemNumber, token);

                // 阶段 2+3：四点边界扫描 → 拟合中心 → 移至 (X0,Y0)
                progress?.Report((L("NeedleAligner_Status_SearchCenterXY", "搜索中心点XY"), 10));
                var center = await SearchCenterPointAsync(parameters, systemNumber, progress, token);
                if (center == null)
                    return Fail(L("NeedleAligner_Error_SearchCenterFailed", "搜索中心点失败"));

                // 阶段 4：Z 向高度零点检测（双激光同时遮挡触发）
                progress?.Report((L("NeedleAligner_Status_SearchNeedleHeight", "搜索针尖高度"), 65));
                var needleHeight = await SearchNeedleHeightAsync(center, parameters, systemNumber, progress, token);
                if (double.IsNaN(needleHeight))
                    return Fail(L("NeedleAligner_Error_SearchHeightFailed", "搜索针尖高度失败"));

                // 阶段 5：增量法计算本次补偿偏移
                progress?.Report((L("NeedleAligner_Status_CalcCompensation", "计算补偿值"), 90));
                var compensation = CalculateCompensation(center, needleHeight, parameters);

                progress?.Report((L("NeedleAligner_Status_CalibrationDoneMotion", "针头校准完成"), 100));
                await MoveToSafeHeightAsync(parameters, systemNumber, token);

                return new NeedleCalibrationResult
                {
                    Success = true,
                    MeasuredCenter = center,
                    MeasuredHeight = needleHeight,
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
        /// 阶段 2+3：遍历 4 个搜索点采集 XY 中值，拟合针尖中心并移至 (X0,Y0)。
        /// </summary>
        private async Task<PointF?> SearchCenterPointAsync(
            NeedleCalibrationParams parameters,
            int systemNumber,
            IProgress<(string Status, double Progress)> progress,
            CancellationToken token)
        {
            var searchPoints = new[]
            {
                parameters.SearchPoint1,
                parameters.SearchPoint2,
                parameters.SearchPoint3,
                parameters.SearchPoint4
            };

            var xMidSamples = new List<double>(4);
            var yMidSamples = new List<double>(4);

            for (int i = 0; i < searchPoints.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                double pointProgress = 10 + i * 12;
                progress?.Report((
                    L("NeedleAligner_Status_SearchPointScan", "在点{0}进行边界搜索", i + 1),
                    pointProgress));

                // 安全高度下移至搜索点 XY（首点用轴速度，其余用 SearchSpeed）
                await MoveToSearchPointXYAsync(
                    parameters, systemNumber,
                    searchPoints[i].X, searchPoints[i].Y,
                    useAxisMotionSpeed: i == 0, token);

                // 下降至 Z 向寻探高度，针头进入十字激光检测平面
                await MoveToSearchNeedleHeightAsync(parameters, systemNumber, token);

                var midpoint = await ScanSearchPointBoundariesAsync(
                    searchPoints[i], parameters, progress, i, token);
                if (midpoint == null)
                {
                    _logger.Error($"[NeedleAligner] 搜索点{i + 1}边界扫描失败");
                    return null;
                }

                xMidSamples.Add(midpoint.X);
                yMidSamples.Add(midpoint.Y);
                _logger.Info($"[NeedleAligner] 点{i + 1}中值: X_Mid={midpoint.X:F3}, Y_Mid={midpoint.Y:F3}");
            }

            if (xMidSamples.Count < 4 || yMidSamples.Count < 4)
            {
                _logger.Error($"[NeedleAligner] 有效中值不足: X={xMidSamples.Count}, Y={yMidSamples.Count}");
                return null;
            }

            // 阶段 3：四点拟合 — 4 组 X/Y 中值取平均得到针尖 XY 基准原点
            float x0 = (float)xMidSamples.Average();
            float y0 = (float)yMidSamples.Average();
            _logger.Info($"[NeedleAligner] 拟合中心: X0={x0:F3}, Y0={y0:F3}");

            progress?.Report((L("NeedleAligner_Status_MoveToFittedCenter", "移动到拟合中心点"), 58));
            await MoveToSafeHeightAsync(parameters, systemNumber, token);

            var map = ResolveAxisMap();
            var dxId = ResolveAxisId(map, "Dx");
            var dyId = ResolveAxisId(map, "Dy");
            await MoveXYLineAsync(dxId, dyId, x0, y0, parameters.SearchSpeed, token);

            return new PointF(x0, y0);
        }

        /// <summary>
        /// 单点位标准动作：X 边界双向精细扫描 → Y 边界双向精细扫描，返回该点 XY 中值。
        /// </summary>
        private async Task<PointF?> ScanSearchPointBoundariesAsync(
            PointF searchPoint,
            NeedleCalibrationParams parameters,
            IProgress<(string Status, double Progress)> progress,
            int pointIndex,
            CancellationToken token)
        {
            var map = ResolveAxisMap();
            var dxId = ResolveAxisId(map, "Dx");
            var dyId = ResolveAxisId(map, "Dy");
            double range = parameters.SearchRange;
            double fineSpeed = parameters.FineSearchSpeed;

            progress?.Report((
                L("NeedleAligner_Status_SearchPointX", "在点{0}进行X方向搜索", pointIndex + 1),
                10 + pointIndex * 12 + 2));

            // X 轴边界精细扫描：负向偏移起点 → 正向入光沿 → 对侧反向入光沿
            double? xMid = await ScanAxisBoundaryMidAsync(
                dxId, dyId,
                searchPoint.X - range, searchPoint.Y,
                range * 2, fineSpeed,
                SearchDirection.X, parameters, token);
            if (xMid == null) return null;

            progress?.Report((
                L("NeedleAligner_Status_SearchPointY", "在点{0}进行Y方向搜索", pointIndex + 1),
                10 + pointIndex * 12 + 5));

            // Y 轴边界精细扫描：在 X 中值处沿 Y 方向双向扫描
            double? yMid = await ScanAxisBoundaryMidAsync(
                dxId, dyId,
                xMid.Value, searchPoint.Y - range,
                range * 2, fineSpeed,
                SearchDirection.Y, parameters, token);
            if (yMid == null) return null;

            return new PointF((float)xMid.Value, (float)yMid.Value);
        }

        /// <summary>
        /// 单轴边界双向扫描：正向捕获入光上升沿 → 移至对侧 → 反向捕获入光上升沿，返回两次入光中点。
        /// 几何原理：正向入光 X1=L-w/2，反向入光 X2=L+w/2，中点 (X1+X2)/2=L，抵消针头半宽 w。
        /// </summary>
        private async Task<double?> ScanAxisBoundaryMidAsync(
            int dxId, int dyId,
            double scanStartX, double scanStartY,
            double scanSpan,
            double fineSpeed,
            SearchDirection sensorAxis,
            NeedleCalibrationParams parameters,
            CancellationToken token)
        {
            // 计算扫描终点（对侧起始位置）
            double scanEndX = sensorAxis == SearchDirection.X ? scanStartX + scanSpan : scanStartX;
            double scanEndY = sensorAxis == SearchDirection.Y ? scanStartY + scanSpan : scanStartY;

            // 移至扫描起点（激光左侧/下侧）
            await MoveXYLineAsync(dxId, dyId, scanStartX, scanStartY, fineSpeed, token);

            int moveAxisId = sensorAxis == SearchDirection.X ? dxId : dyId;

            // 正向慢移：针头从激光左侧切入，捕捉 SensorDi 上升沿（未触发→触发）
            double enterPos1 = await SearchBoundaryEdgeAsync(
                moveAxisId, scanSpan, fineSpeed, sensorAxis, parameters, token);
            if (double.IsNaN(enterPos1)) return null;

            // 移至对侧起点（激光右侧/上侧），穿过激光后传感器恢复未触发态
            await MoveXYLineAsync(dxId, dyId, scanEndX, scanEndY, fineSpeed, token);

            // 反向慢移：针头从激光右侧反向切入，再次捕捉上升沿（未触发→触发）
            double enterPos2 = await SearchBoundaryEdgeAsync(
                moveAxisId, -scanSpan, fineSpeed, sensorAxis, parameters, token);
            if (double.IsNaN(enterPos2)) return null;

            // 两次入光中点 = 激光中心，抵消针头半宽 w
            double mid = (enterPos1 + enterPos2) / 2.0;
            _logger.Info($"[NeedleAligner] {sensorAxis}边界: Enter1={enterPos1:F3}, Enter2={enterPos2:F3}, Mid={mid:F3}");
            return mid;
        }

        /// <summary>
        /// 边扫描边轮询传感器入光上升沿（未触发→触发）；检测到沿后立即停轴并记录当前坐标。
        /// </summary>
        private async Task<double> SearchBoundaryEdgeAsync(
            int axisId,
            double searchDistance,
            double speed,
            SearchDirection sensorDirection,
            NeedleCalibrationParams parameters,
            CancellationToken token)
        {
            bool prevTriggered = IsNeedleSensorTriggered(sensorDirection, parameters);
            _logger.Info($"[NeedleAligner] {sensorDirection} 入光扫描: 轴={axisId}, 距离={searchDistance:F3}, 速度={speed:F2}");

            await _motion.MoveRelStartAsync(axisId, searchDistance, speed);

            var deadline = DateTime.UtcNow.AddMilliseconds(SensorTimeoutMs);
            while (!token.IsCancellationRequested)
            {
                if (DateTime.UtcNow > deadline)
                {
                    StopAxisSafe(axisId);
                    _logger.Warn($"[NeedleAligner] {sensorDirection} 入光扫描超时");
                    return double.NaN;
                }

                bool triggered = IsNeedleSensorTriggered(sensorDirection, parameters);
                // 入光上升沿：针头切入激光，触发态由 false→true（DI 高→低）
                if (!prevTriggered && triggered)
                {
                    double pos = _motion.GetAxisPosition(axisId);
                    StopAxisSafe(axisId);
                    _logger.Info($"[NeedleAligner] {sensorDirection} 入光捕获: {pos:F3}");
                    return pos;
                }

                prevTriggered = triggered;
                await Task.Delay(SensorPollMs, token);
            }

            token.ThrowIfCancellationRequested();
            return double.NaN;
        }

        /// <summary>
        /// 阶段 4：在拟合中心 (X0,Y0) 多次 Z 下探，双激光同时遮挡时记录 Z 坐标，取中值。
        /// </summary>
        private async Task<double> SearchNeedleHeightAsync(
            PointF centerPoint,
            NeedleCalibrationParams parameters,
            int systemNumber,
            IProgress<(string Status, double Progress)> progress,
            CancellationToken token)
        {
            var zId = ResolveZAxisId(systemNumber);
            var probeZ = GetSearchNeedleTargetZ(parameters, systemNumber);
            int count = Math.Max(1, parameters.ZSearchCount);
            var zSamples = new List<double>(count);

            for (int i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested();
                progress?.Report((
                    L("NeedleAligner_Status_ZHeightSearch", "第 {0}/{1} 次高度定位", i + 1, count),
                    65 + i * (20.0 / count)));

                if (i > 0)
                    await MoveToSafeHeightAsync(parameters, systemNumber, token);

                double zTrigger = await SearchSingleZHeightSampleAsync(
                    zId, parameters.SafeHeight, probeZ, parameters, token);
                if (double.IsNaN(zTrigger))
                {
                    _logger.Warn($"[NeedleAligner] 第{i + 1}次 Z 高度采样失败");
                    continue;
                }

                zSamples.Add(zTrigger);
                _logger.Info($"[NeedleAligner] 第{i + 1}次 Z 触发高度: {zTrigger:F3}mm");
            }

            if (zSamples.Count == 0)
                return double.NaN;

            double z0 = AggregateZSamples(zSamples);
            await MoveToSafeHeightAsync(parameters, systemNumber, token);
            _logger.Info($"[NeedleAligner] 针尖 Z 基准 Z0={z0:F3}mm, 有效采样={zSamples.Count}/{count}");
            return z0;
        }

        /// <summary>
        /// 单次 Z 高度采样：从安全高度以精细速度下探，双传感器同时触发时记录 Z 并停轴。
        /// </summary>
        private async Task<double> SearchSingleZHeightSampleAsync(
            int zId,
            double safeHeight,
            double probeZ,
            NeedleCalibrationParams parameters,
            CancellationToken token)
        {
            double currentZ = _motion.GetAxisPosition(zId);
            if (Math.Abs(currentZ - safeHeight) > 0.01)
            {
                var zFastSpeed = GetAxisMotionSpeed(zId);
                await MoveZAbsWithApproachAsync(zId, safeHeight, zFastSpeed, parameters.FineSearchSpeed, token);
            }

            double descendDistance = probeZ - safeHeight;
            if (descendDistance >= 0)
            {
                _logger.Warn($"[NeedleAligner] Z 下探目标({probeZ:F3})不在安全高度({safeHeight:F3})下方，跳过本次采样");
                return double.NaN;
            }

            bool prevBothTriggered = AreBothSensorsTriggered(parameters);
            await _motion.MoveRelStartAsync(zId, descendDistance, parameters.FineSearchSpeed);

            var deadline = DateTime.UtcNow.AddMilliseconds(SensorTimeoutMs);
            while (!token.IsCancellationRequested)
            {
                if (DateTime.UtcNow > deadline)
                {
                    StopAxisSafe(zId);
                    _logger.Warn("[NeedleAligner] Z 高度搜索超时");
                    return double.NaN;
                }

                bool bothTriggered = AreBothSensorsTriggered(parameters);
                // 双激光同时被针头遮挡的临界上升沿
                if (!prevBothTriggered && bothTriggered)
                {
                    double zPos = _motion.GetAxisPosition(zId);
                    StopAxisSafe(zId);
                    return zPos;
                }

                prevBothTriggered = bothTriggered;
                await Task.Delay(SensorPollMs, token);
            }

            token.ThrowIfCancellationRequested();
            return double.NaN;
        }

        /// <summary>双路激光同时被针头遮挡（X+Y 传感器均触发）</summary>
        private bool AreBothSensorsTriggered(NeedleCalibrationParams parameters) =>
            IsNeedleSensorTriggered(SearchDirection.X, parameters) &&
            IsNeedleSensorTriggered(SearchDirection.Y, parameters);

        /// <summary>对 N 组 Z 采样取中值（偶数时取中间两值平均）</summary>
        private static double AggregateZSamples(IReadOnlyList<double> samples)
        {
            var sorted = samples.OrderBy(z => z).ToList();
            int n = sorted.Count;
            if (n == 0) return double.NaN;
            if (n % 2 == 1) return sorted[n / 2];
            return (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
        }

        /// <summary>寻针 Z 下探极限 = 对针位置 Z + Z 下探补偿高度</summary>
        private static double GetSearchNeedleTargetZ(NeedleCalibrationParams parameters, int systemNumber)
        {
            var alignZ = GetAlignPosition(parameters, systemNumber).Z;
            return alignZ + parameters.ZProbeDescentHeight;
        }

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
        /// 检查 XY 寻针传感器是否触发。
        /// 硬件约定：DI 读数为 0（低电平）表示触发，与参考 NeedleCalibrating.CheckNeedleSensor 一致。
        /// </summary>
        private bool IsNeedleSensorTriggered(SearchDirection sensorAxis, NeedleCalibrationParams parameters)
        {
            int port = sensorAxis == SearchDirection.X
                ? parameters.SensorDiX
                : parameters.SensorDiY;

            if (port < 0)
            {
                _logger.Warn($"[NeedleAligner] 传感器 DI 端口号无效: {port}");
                return false;
            }

            try
            {
                // ReadDi=true 为高电平；触发时为低电平 → ReadDi 为 false
                return !_motion.ReadDi(port);
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

        private enum SearchDirection { X, Y }

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
            IProgress<(string Status, double Progress)> progress, CancellationToken token)
            => Task.FromResult(new NeedleCalibrationResult { Success = false, ErrorMessage = "Halcon SDK not available" });

        public void StopMotion(int systemNumber) { }
    }
}
#endif
