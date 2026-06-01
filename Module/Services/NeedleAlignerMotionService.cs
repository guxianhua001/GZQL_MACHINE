using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using HalconDotNet;
using MotionControl.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Module.Services
{
    /// <summary>
    /// 针头对针运动实现：使用 IMotionService 直接控制 Dx/Dy/Dz₂/Dz₃
    /// 流程：抬安全高度 → 对针位 XY → 对针高度 → 四点寻边 → Z 寻高 → 计算补偿
    /// </summary>
    public class NeedleAlignerMotionService : INeedleAlignerMotionService
    {
        private const string StationIdentifier = "DispenserStation";
        private const int MoveDonePollMs = 10;
        private const int SensorPollMs = 20;
        private const int SensorTimeoutMs = 60000;

        private readonly IMotionService _motion;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localization;
        private Dictionary<string, int> _axisIdCache;

        public NeedleAlignerMotionService(IMotionService motion, ILoggerService logger, ILocalizationService localization)
        {
            _motion = motion ?? throw new ArgumentNullException(nameof(motion));
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
            await _motion.MoveAbsAsync(zId, parameters.SafeHeight, parameters.SearchSpeed, token);
            await WaitAxisDoneAsync(zId, token);
        }

        public async Task MoveToAlignPositionAsync(NeedleCalibrationParams parameters, int systemNumber, CancellationToken token)
        {
            var align = GetAlignPosition(parameters, systemNumber);
            await MoveToSafeHeightAsync(parameters, systemNumber, token);

            var map = ResolveAxisMap();
            var dxId = ResolveAxisId(map, "Dx");
            var dyId = ResolveAxisId(map, "Dy");
            var zId = ResolveZAxisId(systemNumber);

            await _motion.MoveAbsAsync(dxId, align.X, parameters.SearchSpeed, token);
            await _motion.MoveAbsAsync(dyId, align.Y, parameters.SearchSpeed, token);
            await Task.WhenAll(WaitAxisDoneAsync(dxId, token), WaitAxisDoneAsync(dyId, token));

            await _motion.MoveAbsAsync(zId, align.Z, parameters.SearchSpeed * 0.5, token);
            await WaitAxisDoneAsync(zId, token);
        }

        public async Task MoveToSearchPointXYAsync(NeedleCalibrationParams parameters, int systemNumber, double x, double y, CancellationToken token)
        {
            await MoveToSafeHeightAsync(parameters, systemNumber, token);

            var map = ResolveAxisMap();
            var dxId = ResolveAxisId(map, "Dx");
            var dyId = ResolveAxisId(map, "Dy");

            await _motion.MoveAbsAsync(dxId, x, parameters.SearchSpeed, token);
            await _motion.MoveAbsAsync(dyId, y, parameters.SearchSpeed, token);
            await Task.WhenAll(WaitAxisDoneAsync(dxId, token), WaitAxisDoneAsync(dyId, token));
        }

        public async Task MoveToSearchNeedleHeightAsync(NeedleCalibrationParams parameters, int systemNumber, CancellationToken token)
        {
            var align = GetAlignPosition(parameters, systemNumber);
            var zId = ResolveZAxisId(systemNumber);
            await _motion.MoveAbsAsync(zId, align.Z, parameters.SearchSpeed * 0.5, token);
            await WaitAxisDoneAsync(zId, token);
        }

        public async Task<NeedleCalibrationResult> ExecuteNeedleCalibrationAsync(
            NeedleCalibrationParams parameters,
            int systemNumber,
            IProgress<(string Status, double Progress)> progress,
            CancellationToken token)
        {
            try
            {
                progress?.Report((L("NeedleAligner_Status_RaiseSafeHeight", "抬升到安全高度"), 10));
                await MoveToSafeHeightAsync(parameters, systemNumber, token);

                progress?.Report((L("NeedleAligner_Status_SearchCenterXY", "搜索中心点XY"), 20));
                var center = await SearchCenterPointAsync(parameters, systemNumber, progress, token);
                if (center == null)
                    return Fail(L("NeedleAligner_Error_SearchCenterFailed", "搜索中心点失败"));

                progress?.Report((L("NeedleAligner_Status_SearchNeedleHeight", "搜索针尖高度"), 60));
                var needleHeight = await SearchNeedleHeightAsync(center, parameters, systemNumber, progress, token);
                if (double.IsNaN(needleHeight))
                    return Fail(L("NeedleAligner_Error_SearchHeightFailed", "搜索针尖高度失败"));

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

        private async Task<PointF> SearchCenterPointAsync(
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
            // 移到第一个搜索点
            await MoveToSearchPointXYAsync(parameters, systemNumber, searchPoints[0].X, searchPoints[0].Y, token);
            await MoveToSearchNeedleHeightAsync(parameters, systemNumber, token);

            var xEdgePoints = new List<PointF>();
            var yEdgePoints = new List<PointF>();

            for (int i = 0; i < 2; i++)
            {
                token.ThrowIfCancellationRequested();
                progress?.Report((L("NeedleAligner_Status_SearchPointX", "在点{0}进行X方向搜索", i + 1), 20 + i * 10));
                var xEdge = await SearchEdgeInDirectionAsync(searchPoints[i], SearchDirection.XPositive, SearchDirection.X, parameters, systemNumber, token);
                if (xEdge == null) return null;
                xEdgePoints.Add(xEdge);
            }

            for (int i = 2; i < 4; i++)
            {
                token.ThrowIfCancellationRequested();
                progress?.Report((L("NeedleAligner_Status_SearchPointY", "在点{0}进行Y方向搜索", i + 1), 40 + (i - 2) * 10));
                var yEdge = await SearchEdgeInDirectionAsync(searchPoints[i], SearchDirection.XPositive, SearchDirection.Y, parameters, systemNumber, token);
                if (yEdge == null) return null;
                yEdgePoints.Add(yEdge);
            }

            if (xEdgePoints.Count < 2 || yEdgePoints.Count < 2)
            {
                _logger.Error($"[NeedleAligner] 有效边缘点不足: X={xEdgePoints.Count}, Y={yEdgePoints.Count}");
                return null;
            }

            var center = CalculateCenterPointWithHalcon(xEdgePoints, yEdgePoints);
            if (center != null)
            {
                _logger.Info($"[NeedleAligner] Halcon中心点: X={center.X:F3}, Y={center.Y:F3}");
                return center;
            }

            float centerX = xEdgePoints.Average(p => p.X);
            float centerY = yEdgePoints.Average(p => p.Y);
            _logger.Warn("[NeedleAligner] Halcon交点失败，使用平均值");
            return new PointF(centerX, centerY);
        }

        private async Task<PointF> SearchEdgeInDirectionAsync(
            PointF startPoint,
            SearchDirection moveDirection,
            SearchDirection sensorDirection,
            NeedleCalibrationParams parameters,
            int systemNumber,
            CancellationToken token)
        {
            var map = ResolveAxisMap();
            int axisId = GetAxisIdForDirection(map, moveDirection);

            double startX = startPoint.X - parameters.SearchRange;
            var dxId = ResolveAxisId(map, "Dx");
            var dyId = ResolveAxisId(map, "Dy");

            await _motion.MoveAbsAsync(dxId, startX, parameters.SearchSpeed, token);
            await _motion.MoveAbsAsync(dyId, startPoint.Y, parameters.SearchSpeed, token);
            await Task.WhenAll(WaitAxisDoneAsync(dxId, token), WaitAxisDoneAsync(dyId, token));

            double forwardEdge = await SearchSingleEdgeAsync(moveDirection, sensorDirection, parameters.SearchRange * 2, parameters.FineSearchSpeed, axisId, parameters, token);
            if (double.IsNaN(forwardEdge)) return null;

            await WaitAxisDoneAsync(axisId, token);

            double backwardEdge = await SearchSingleEdgeAsync(GetOppositeDirection(moveDirection), sensorDirection, parameters.SearchRange * 2, parameters.FineSearchSpeed, axisId, parameters, token);
            if (double.IsNaN(backwardEdge)) return null;

            await WaitAxisDoneAsync(axisId, token);

            double center = (forwardEdge + backwardEdge) / 2;
            var result = new PointF(startPoint.X, startPoint.Y);
            if (moveDirection is SearchDirection.XPositive or SearchDirection.XNegative)
                result.X = (float)center;
            else
                result.Y = (float)center;

            return result;
        }

        private async Task<double> SearchSingleEdgeAsync(
            SearchDirection direction,
            SearchDirection sensorDirection,
            double searchRange,
            double speed,
            int axisId,
            NeedleCalibrationParams parameters,
            CancellationToken token)
        {
            double searchDistance = direction is SearchDirection.XPositive or SearchDirection.YPositive
                ? searchRange
                : -searchRange;

            _logger.Info($"[NeedleAligner] {direction}边缘搜索: 轴={axisId}, 距离={searchDistance:F3}");
            await _motion.MoveRelAsync(axisId, searchDistance, speed, token);

            var edgePos = await WaitForSensorTriggerAsync(sensorDirection, axisId, parameters, token);
            if (!double.IsNaN(edgePos))
                _logger.Info($"[NeedleAligner] {direction}边缘位置: {edgePos:F3}");
            else
                _logger.Warn($"[NeedleAligner] {direction}边缘搜索超时");

            return edgePos;
        }

        private async Task<double> WaitForSensorTriggerAsync(
            SearchDirection sensorDirection,
            int axisId,
            NeedleCalibrationParams parameters,
            CancellationToken token)
        {
            var startTime = DateTime.UtcNow;
            while (!token.IsCancellationRequested)
            {
                if ((DateTime.UtcNow - startTime).TotalMilliseconds > SensorTimeoutMs)
                    return double.NaN;

                if (IsNeedleSensorTriggered(sensorDirection, parameters))
                    return _motion.GetAxisPosition(axisId);

                await Task.Delay(SensorPollMs, token);
            }

            token.ThrowIfCancellationRequested();
            return double.NaN;
        }

        /// <summary>
        /// 针尖 Z：仅使用对针位置高度（无 Z 向 DI），移至中心后下降到对针位 Z 并读取编码器。
        /// </summary>
        private async Task<double> SearchNeedleHeightAsync(
            PointF centerPoint,
            NeedleCalibrationParams parameters,
            int systemNumber,
            IProgress<(string Status, double Progress)> progress,
            CancellationToken token)
        {
            var map = ResolveAxisMap();
            var dxId = ResolveAxisId(map, "Dx");
            var dyId = ResolveAxisId(map, "Dy");
            var zId = ResolveZAxisId(systemNumber);
            var alignZ = GetAlignPosition(parameters, systemNumber).Z;

            await _motion.MoveAbsAsync(dxId, centerPoint.X, parameters.SearchSpeed, token);
            await _motion.MoveAbsAsync(dyId, centerPoint.Y, parameters.SearchSpeed, token);
            await Task.WhenAll(WaitAxisDoneAsync(dxId, token), WaitAxisDoneAsync(dyId, token));

            double totalHeight = 0;
            int count = Math.Max(1, parameters.ZSearchCount);

            for (int i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested();
                progress?.Report((L("NeedleAligner_Status_ZHeightSearch", "第 {0}/{1} 次高度定位", i + 1, count), 60 + i * 10));

                await MoveToSafeHeightAsync(parameters, systemNumber, token);
                await _motion.MoveAbsAsync(dxId, centerPoint.X, parameters.SearchSpeed, token);
                await _motion.MoveAbsAsync(dyId, centerPoint.Y, parameters.SearchSpeed, token);
                await Task.WhenAll(WaitAxisDoneAsync(dxId, token), WaitAxisDoneAsync(dyId, token));

                await _motion.MoveAbsAsync(zId, alignZ, parameters.FineSearchSpeed, token);
                await WaitAxisDoneAsync(zId, token);

                totalHeight += _motion.GetAxisPosition(zId);
            }

            double average = totalHeight / count;
            _logger.Info($"[NeedleAligner] 针尖 Z(对针高度): {average:F3}mm, 次数={count}");
            return average;
        }

        private static PointF CalculateCompensation(PointF measured, double measuredHeight, NeedleCalibrationParams parameters)
        {
            float deltaX = measured.X - parameters.ReferenceXYZ.X;
            float deltaY = measured.Y - parameters.ReferenceXYZ.Y;
            float deltaZ = (float)measuredHeight - parameters.ReferenceXYZ.Z;
            return new PointF(-deltaX, -deltaY, -deltaZ);
        }

        private PointF? CalculateCenterPointWithHalcon(List<PointF> xPoints, List<PointF> yPoints)
        {
            try
            {
                HTuple intersectionRow, intersectionColumn, isOverlapping;
                HOperatorSet.IntersectionLines(
                    new HTuple(xPoints[0].Y), new HTuple(xPoints[0].X),
                    new HTuple(xPoints[1].Y), new HTuple(xPoints[1].X),
                    new HTuple(yPoints[0].Y), new HTuple(yPoints[0].X),
                    new HTuple(yPoints[1].Y), new HTuple(yPoints[1].X),
                    out intersectionRow, out intersectionColumn, out isOverlapping);

                if (intersectionRow.TupleLength() > 0 && intersectionColumn.TupleLength() > 0 &&
                    !isOverlapping.TupleEqual(1))
                {
                    return new PointF((float)intersectionColumn.D, (float)intersectionRow.D);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[NeedleAligner] Halcon交点计算异常: {ex.Message}");
            }

            return null;
        }

        #endregion

        #region 传感器与轴解析

        /// <summary>
        /// 检查 XY 寻针传感器是否触发。
        /// 硬件约定：DI 读数为 0（低电平）表示触发，与参考 NeedleCalibrating.CheckNeedleSensor 一致。
        /// </summary>
        private bool IsNeedleSensorTriggered(SearchDirection sensorAxis, NeedleCalibrationParams parameters)
        {
            int port = sensorAxis is SearchDirection.X or SearchDirection.XPositive or SearchDirection.XNegative
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

        private static int GetAxisIdForDirection(Dictionary<string, int> map, SearchDirection direction) =>
            direction is SearchDirection.XPositive or SearchDirection.XNegative or SearchDirection.X
                ? ResolveAxisId(map, "Dx")
                : ResolveAxisId(map, "Dy");

        private static PointF GetAlignPosition(NeedleCalibrationParams parameters, int systemNumber) =>
            systemNumber == 1 ? parameters.AlignPositionSystem1 : parameters.AlignPositionSystem2;

        private async Task WaitAxisDoneAsync(int axisId, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (!_motion.GetAxisState(axisId).IsMoving)
                    return;
                await Task.Delay(MoveDonePollMs, token);
            }

            token.ThrowIfCancellationRequested();
        }

        private static NeedleCalibrationResult Fail(string message) =>
            new() { Success = false, ErrorMessage = message };

        private static SearchDirection GetOppositeDirection(SearchDirection direction) =>
            direction switch
            {
                SearchDirection.XPositive => SearchDirection.XNegative,
                SearchDirection.XNegative => SearchDirection.XPositive,
                SearchDirection.YPositive => SearchDirection.YNegative,
                SearchDirection.YNegative => SearchDirection.YPositive,
                _ => direction
            };

        private enum SearchDirection { XPositive, XNegative, YPositive, YNegative, X, Y }

        #endregion
    }
}
