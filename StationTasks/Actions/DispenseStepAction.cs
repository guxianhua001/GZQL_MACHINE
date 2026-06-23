using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using MotionControl.Interfaces;
using StationTasks.Models;
using StationTasks.Params;
using StationTasks.Tasks;
using Recipe.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StationTasks.Actions
{
    /// <summary>
    /// DISPENSE 步骤动作：从配方段数据导入线段/圆弧引用，按点胶模式执行点胶工艺
    /// </summary>
    public class DispenseStepAction : IProcessStepAction
    {
        private readonly IRecipePoolService _recipePoolService;
        private readonly ILoggerService _logger;
        private readonly IStationRegistry _stationRegistry;
        private readonly IMotionService _motionService;
        private readonly IDispenseSegmentSourceService _segmentSourceService;

        /// <summary>直线插补坐标系 ID（MoveLineAbsAsync）</summary>
        private const int CoordIdLinear = 0;

        /// <summary>连续插补坐标系 ID（InitializeContinuousInterpolation / 走轨迹）</summary>
        private const int CoordIdContinuous = 1;

        /// <summary>针头1（Dz₂）出胶 IO 端口编号——与 DispenseExecuteService 一致</summary>
        private const int GlueIoPort1 = 12;

        /// <summary>针头2（Dz₃）出胶 IO 端口编号——与 DispenseExecuteService 一致</summary>
        private const int GlueIoPort2 = 13;

        private const double DefaultAcc = 0.05;
        private const double DefaultDec = 0.05;

        /// <summary>当前步骤是否启用 XY 补偿</summary>
        private bool _enableComp;

        /// <summary>当前步骤解析后的 X/Y 补偿量（mm）</summary>
        private double _xCompensation;
        private double _yCompensation;

        public StepType SupportedStepType => StepType.DISPENSE;

        public DispenseStepAction(
            IRecipePoolService recipePoolService,
            ILoggerService logger,
            IStationRegistry stationRegistry,
            IMotionService motionService,
            IDispenseSegmentSourceService segmentSourceService)
        {
            _recipePoolService = recipePoolService;
            _logger = logger;
            _stationRegistry = stationRegistry;
            _motionService = motionService;
            _segmentSourceService = segmentSourceService;
        }

        /// <summary>
        /// 执行 DISPENSE 步骤：Z校准 → 空跑(可选) → 按模式走胶
        /// </summary>
        public async Task ExecuteAsync(ProcessStep step, StationTaskBase task, CancellationToken token)
        {
            var detail = step.DispenseDetail;
            if (detail == null)
            {
                _logger.Warn($"DISPENSE 步骤 [{step.Seq}] 没有 DispenseDetail，跳过执行");
                return;
            }

            var sourceSegments = _segmentSourceService.GetSourceSegments();
            var segDict = sourceSegments.Where(s => !string.IsNullOrEmpty(s.SegmentId))
                .ToDictionary(s => s.SegmentId, s => s);
            _logger.Info($"DISPENSE 步骤 [{step.Seq}] 源轨迹段 {segDict.Count} 条");

            int dxAxisId = ResolveDispenseAxisId("Dx");
            int dyAxisId = ResolveDispenseAxisId("Dy");
            // 根据针头索引选择点胶Z轴：针头1→Dz₂, 针头2→Dz₃
            // Dz₁轴为相机/3D扫描轴，不作为点胶轴使用
            int needleIndex = detail.NeedleIndex;
            string dzAxisName = needleIndex == 0 ? "Dz₂" : "Dz₃";
            int dzAxisId = ResolveDispenseAxisId(dzAxisName);
            int glueIoPort = GetGlueIoPort(needleIndex);
            _logger.Info($"DISPENSE 步骤 [{step.Seq}] 使用针头{needleIndex + 1}/{dzAxisName}(逻辑轴ID={dzAxisId}), Dx={dxAxisId}, Dy={dyAxisId}, 出胶IO={glueIoPort}");

            // 解析 XY 补偿（启用时叠加到所有运动目标 MachineX/MachineY）
            _enableComp = detail.EnableComp;
            if (_enableComp)
            {
                _xCompensation = ResolveLinkedValue(detail.XCompensation, detail.XCompensationLinkedVar);
                _yCompensation = ResolveLinkedValue(detail.YCompensation, detail.YCompensationLinkedVar);
                _logger.Info($"DISPENSE 步骤 [{step.Seq}] XY补偿已启用: dX={_xCompensation:F4}mm, dY={_yCompensation:F4}mm");
            }
            else
            {
                _xCompensation = 0;
                _yCompensation = 0;
            }

            try
            {
                if (detail.IsDryRunMode)
                    await ExecuteDryRunAsync(detail, segDict, dxAxisId, dyAxisId, dzAxisId, token);

                if (detail.IsRealDispenseMode)
                {
                    switch (detail.DispenseMode)
                    {
                        case DispenseStepMode.Dot:
                            await ExecuteDotModeAsync(detail, segDict, dxAxisId, dyAxisId, dzAxisId, needleIndex, token);
                            break;
                        case DispenseStepMode.Arc:
                            await ExecuteArcModeAsync(detail, segDict, dxAxisId, dyAxisId, dzAxisId, needleIndex, token);
                            break;
                        default:
                            _logger.Warn($"DISPENSE 步骤 [{step.Seq}] 未知点胶模式: {detail.DispenseMode}");
                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                SafeGlueOff(needleIndex);
                _logger.Warn($"DISPENSE 步骤 [{step.Seq}] 已取消，已安全关胶");
                throw;
            }
            catch (Exception ex)
            {
                SafeGlueOff(needleIndex);
                _logger.Error(ex, $"DISPENSE 步骤 [{step.Seq}] 执行异常，已安全关胶");
                throw;
            }
        }

        /// <summary>
        /// 空跑：按工艺流程运动但不出胶，Z轴保持在安全高度
        /// </summary>
        private async Task ExecuteDryRunAsync(
            DispenseDetail detail,
            Dictionary<string, DispenseSegment> segDict,
            int dxAxisId, int dyAxisId, int dzAxisId,
            CancellationToken token)
        {
            _logger.Info("DISPENSE 开始空跑");

            var enabledRefs = detail.SegmentRefs.Where(r => r.IsEnabled).ToList();
            DispenseSegment lastSeg = null;

            foreach (var segRef in enabledRefs)
            {
                token.ThrowIfCancellationRequested();

                if (!segDict.TryGetValue(segRef.SourceSegmentId, out var source))
                {
                    _logger.Warn($"DISPENSE 空跑: 源段 '{segRef.SourceSegmentId}' 未找到，跳过");
                    continue;
                }

                if (source.Points == null || source.Points.Count == 0) continue;

                var seg = CreateSegmentWithParams(source, segRef, detail);
                lastSeg = seg;
                double moveSpeed = seg.MoveSpeed;
                double safeHeight = seg.SafeHeight;

                await _motionService.MoveAbsAsync(dzAxisId, safeHeight, moveSpeed, token);

                var startPt = seg.Points.First();
                var (startX, startY) = GetMachineXY(startPt);

                await _motionService.MoveLineAbsAsync(CoordIdLinear, new[] { dxAxisId, dyAxisId },
                    new[] { startX, startY }, moveSpeed, token);

                foreach (var pt in seg.Points.Skip(1))
                {
                    token.ThrowIfCancellationRequested();
                    var (px, py) = GetMachineXY(pt);
                    await _motionService.MoveLineAbsAsync(CoordIdLinear, new[] { dxAxisId, dyAxisId },
                        new[] { px, py }, moveSpeed, token);
                }

                await _motionService.MoveAbsAsync(dzAxisId, safeHeight, moveSpeed, token);
            }

            if (lastSeg != null)
                await _motionService.MoveAbsAsync(dzAxisId, lastSeg.SafeHeight, lastSeg.MoveSpeed, token);
            else
                await _motionService.MoveAbsAsync(dzAxisId, detail.DefaultSafeHeight, detail.DefaultMoveSpeed, token);

            _logger.Info("DISPENSE 空跑完成");
        }

        /// <summary>
        /// 单点模式：逐点点胶，遵循行业标准工艺流程
        /// 流程：Z抬升→XY定位→Z两段式下降→位置触发开胶→出胶→关胶→抬升
        /// </summary>
        private async Task ExecuteDotModeAsync(
            DispenseDetail detail,
            Dictionary<string, DispenseSegment> segDict,
            int dxAxisId, int dyAxisId, int dzAxisId,
            int needleIndex,
            CancellationToken token)
        {
            _logger.Info($"DISPENSE 单点模式开始");

            var enabledRefs = detail.SegmentRefs.Where(r => r.IsEnabled).ToList();
            int totalRefs = enabledRefs.Count;
            int currentRef = 0;
            DispenseSegment lastSeg = null;

            foreach (var segRef in enabledRefs)
            {
                token.ThrowIfCancellationRequested();
                currentRef++;

                if (!segDict.TryGetValue(segRef.SourceSegmentId, out var source))
                {
                    _logger.Warn($"DISPENSE 单点: 源段 '{segRef.SourceSegmentId}' 未找到，跳过");
                    continue;
                }

                if (source.Points == null || source.Points.Count == 0) continue;

                var seg = CreateSegmentWithParams(source, segRef, detail);
                lastSeg = seg;

                // 与 DISPENSE 工具页面 Effective* 一致：使用段级工艺参数
                double moveSpeed = seg.MoveSpeed;
                double safeHeight = seg.SafeHeight;
                double targetZ = seg.EffectiveZHeight;
                double approachOffset = seg.ApproachHeight;
                double slowVel = seg.MoveSpeed * seg.CornerDecel;
                double glueTriggerOffset = seg.GlueTriggerOffsetMm;

                _logger.Info($"DISPENSE 单点: 段[{seg.SegmentId}] ({currentRef}/{totalRefs})，{seg.Points.Count} 点，" +
                             $"MoveSpeed={moveSpeed:F1}, SafeHeight={safeHeight:F1}, DispenseTime={seg.DispenseTime:F0}ms");

                foreach (var (point, ptIndex) in seg.Points.Select((p, i) => (p, i)))
                {
                    token.ThrowIfCancellationRequested();

                    var (px, py) = GetMachineXY(point);

                    await _motionService.MoveAbsAsync(dzAxisId, safeHeight, moveSpeed, token);

                    await _motionService.MoveLineAbsAsync(CoordIdLinear, new[] { dxAxisId, dyAxisId },
                        new[] { px, py }, moveSpeed, token);

                    double approachZ = targetZ + approachOffset;
                    await _motionService.MoveAbsAsync(dzAxisId, approachZ, moveSpeed, token);

                    // 位置触发开胶：计算触发点Z，慢速移到触发位开胶，再继续到目标位
                    double triggerDistance = Math.Abs(glueTriggerOffset);
                    int motionDir = Math.Sign(approachZ - targetZ);
                    double triggerZ = targetZ + motionDir * triggerDistance;

                    await _motionService.MoveAbsAsync(dzAxisId, triggerZ, slowVel, token);
                    WriteGlueIo(true, needleIndex);
                    _logger.Debug($"DISPENSE 单点: 段[{seg.SegmentId}]点{ptIndex + 1} 位置触发开胶，triggerZ={triggerZ:F3}, targetZ={targetZ:F3}, offset={glueTriggerOffset:F3}mm");

                    await _motionService.MoveAbsAsync(dzAxisId, targetZ, slowVel, token);

                    if (seg.PreDelay > 0)
                        await Task.Delay((int)seg.PreDelay, token);

                    await Task.Delay((int)seg.DispenseTime, token);

                    WriteGlueIo(false, needleIndex);

                    if (seg.PostDelay > 0)
                        await Task.Delay((int)seg.PostDelay, token);

                    await _motionService.MoveAbsAsync(dzAxisId, safeHeight, moveSpeed, token);
                }
            }

            if (lastSeg != null)
                await _motionService.MoveAbsAsync(dzAxisId, lastSeg.SafeHeight, lastSeg.MoveSpeed, token);
            else
                await _motionService.MoveAbsAsync(dzAxisId, detail.DefaultSafeHeight, detail.DefaultMoveSpeed, token);

            _logger.Info("DISPENSE 单点模式完成");
        }

        /// <summary>
        /// 弧线模式：连续插补走胶，遵循行业标准工艺流程
        /// 流程：Z抬升→XY定位→Z两段式下降→位置触发开胶→连续插补走轨迹→关胶→抬升
        /// </summary>
        private async Task ExecuteArcModeAsync(
            DispenseDetail detail,
            Dictionary<string, DispenseSegment> segDict,
            int dxAxisId, int dyAxisId, int dzAxisId,
            int needleIndex,
            CancellationToken token)
        {
            _logger.Info($"DISPENSE 弧线模式开始");

            var enabledRefs = detail.SegmentRefs
                .Where(r => r.IsEnabled)
                .Where(r => segDict.TryGetValue(r.SourceSegmentId, out var src)
                            && DispenseSegmentClassification.IsArcCompatibleRef(r, src))
                .ToList();

            if (enabledRefs.Count == 0)
            {
                _logger.Warn("DISPENSE 弧线模式: 无已启用的圆弧类分段（请导入 Arc/Circle/Ellipse 或含弧段的多段线）");
                return;
            }
            int totalRefs = enabledRefs.Count;
            int currentRef = 0;
            DispenseSegment lastSeg = null;

            foreach (var segRef in enabledRefs)
            {
                token.ThrowIfCancellationRequested();
                currentRef++;

                if (!segDict.TryGetValue(segRef.SourceSegmentId, out var source))
                {
                    _logger.Warn($"DISPENSE 弧线: 源段 '{segRef.SourceSegmentId}' 未找到，跳过");
                    continue;
                }

                if (source.Points == null || source.Points.Count == 0) continue;

                var seg = CreateSegmentWithParams(source, segRef, detail);
                lastSeg = seg;

                // 与 DISPENSE 工具页面 Effective* 一致：使用段级工艺参数
                double moveSpeed = seg.MoveSpeed;
                double safeHeight = seg.SafeHeight;
                double targetZ = seg.EffectiveZHeight;
                double approachOffset = seg.ApproachHeight;
                double slowVel = seg.MoveSpeed * seg.CornerDecel;
                double glueTriggerOffset = seg.GlueTriggerOffsetMm;

                _logger.Info($"DISPENSE 弧线: 段[{seg.SegmentId}] ({currentRef}/{totalRefs})，{seg.Points.Count} 点，" +
                             $"MoveSpeed={moveSpeed:F1}, InterpSpeed={seg.InterpSpeed:F1}, SafeHeight={safeHeight:F1}");

                await _motionService.MoveAbsAsync(dzAxisId, safeHeight, moveSpeed, token);

                var startPt = seg.Points.First();
                var (startX, startY) = GetMachineXY(startPt);
                await _motionService.MoveLineAbsAsync(CoordIdLinear, new[] { dxAxisId, dyAxisId },
                    new[] { startX, startY }, moveSpeed, token);

                double approachZ = targetZ + approachOffset;
                await _motionService.MoveAbsAsync(dzAxisId, approachZ, moveSpeed, token);

                // 位置触发开胶：计算触发点Z，慢速移到触发位开胶，再继续到目标位
                double triggerDistance = Math.Abs(glueTriggerOffset);
                int motionDir = Math.Sign(approachZ - targetZ);
                double triggerZ = targetZ + motionDir * triggerDistance;

                await _motionService.MoveAbsAsync(dzAxisId, triggerZ, slowVel, token);
                WriteGlueIo(true, needleIndex);
                _logger.Debug($"DISPENSE 弧线: 段[{seg.SegmentId}] 位置触发开胶，triggerZ={triggerZ:F3}, targetZ={targetZ:F3}, offset={glueTriggerOffset:F3}mm");

                await _motionService.MoveAbsAsync(dzAxisId, targetZ, slowVel, token);

                if (seg.PreDelay > 0)
                    await Task.Delay((int)seg.PreDelay, token);

                double currentZPos = _motionService.GetAxisPosition(dzAxisId);
                if (Math.Abs(currentZPos - targetZ) > 0.5)
                {
                    _logger.Warn($"DISPENSE 弧线: 段[{seg.SegmentId}] Z轴未到位: 当前={currentZPos:F3}, 目标={targetZ:F3}，重新下降");
                    await _motionService.MoveAbsAsync(dzAxisId, targetZ, slowVel, token);
                }

                _motionService.InitializeContinuousInterpolation(
                    CoordIdContinuous, new[] { dxAxisId, dyAxisId },
                    startVel: 5, maxVel: seg.InterpSpeed, acc: DefaultAcc, dec: DefaultDec, endVel: 0);

                foreach (var pt in seg.Points)
                {
                    var (px, py) = GetMachineXY(pt);
                    _motionService.AddLineSegment(CoordIdContinuous, new[] { px, py });
                }

                _motionService.ExecuteContinuousInterpolation(CoordIdContinuous);

                bool completed = await _motionService.WaitForCoordMotionCompletionAsync(
                    CoordIdContinuous, TimeSpan.FromMinutes(5), token);

                if (!completed)
                    throw new TimeoutException($"DISPENSE 弧线: 段[{seg.SegmentId}] 运动超时");

                WriteGlueIo(false, needleIndex);

                if (seg.PostDelay > 0)
                    await Task.Delay((int)seg.PostDelay, token);

                await _motionService.MoveAbsAsync(dzAxisId, safeHeight, moveSpeed, token);
            }

            if (lastSeg != null)
                await _motionService.MoveAbsAsync(dzAxisId, lastSeg.SafeHeight, lastSeg.MoveSpeed, token);
            else
                await _motionService.MoveAbsAsync(dzAxisId, detail.DefaultSafeHeight, detail.DefaultMoveSpeed, token);

            _logger.Info("DISPENSE 弧线模式完成");
        }

        /// <summary>
        /// 根据源段和引用参数创建带工艺参数的临时段。
        /// 参数解析与 DISPENSE 工具页面 Effective* 一致：
        /// UseDefaultParams=true 取 DispenseDetail.Default*，否则取 SegmentRef.Override*。
        /// </summary>
        private DispenseSegment CreateSegmentWithParams(DispenseSegment source, DispenseSegmentRef segRef, DispenseDetail detail)
        {
            var seg = new DispenseSegment(source.SegmentId, source.EntityType, source.LayerName)
            {
                Points = source.Points,
                IsEnabled = true
            };

            if (segRef.UseDefaultParams)
            {
                seg.MoveSpeed = detail.DefaultMoveSpeed;
                seg.JumpSpeed = detail.DefaultMoveSpeed;
                seg.InterpSpeed = detail.DefaultInterpSpeed;
                seg.SafeHeight = detail.DefaultSafeHeight;
                seg.ApproachHeight = detail.DefaultApproachHeight;
                seg.DispenseAmount = detail.DefaultDispenseAmount;
                seg.DispenseTime = detail.DefaultDispenseTime;
                seg.PreDelay = detail.DefaultPreDelay;
                seg.PostDelay = detail.DefaultPostDelay;
                seg.DispensingPressure = detail.DefaultDispensingPressure;
                seg.SuckBackTime = detail.DefaultSuckBackTime;
                seg.GlueTriggerOffsetMm = detail.DefaultGlueTriggerOffsetMm;
                seg.CornerDecel = detail.DefaultCornerDecel;
                seg.TeachHeight = detail.DefaultTeachHeight;
                seg.HeightCompensation = detail.DefaultHeightCompensation;
            }
            else
            {
                seg.MoveSpeed = segRef.OverrideMoveSpeed;
                seg.JumpSpeed = segRef.OverrideMoveSpeed;
                seg.InterpSpeed = segRef.OverrideInterpSpeed;
                seg.SafeHeight = segRef.OverrideSafeHeight;
                seg.ApproachHeight = segRef.OverrideApproachHeight;
                seg.DispenseAmount = segRef.OverrideDispenseAmount;
                seg.DispenseTime = segRef.OverrideDispenseTime;
                seg.PreDelay = segRef.OverridePreDelay;
                seg.PostDelay = segRef.OverridePostDelay;
                seg.DispensingPressure = segRef.OverrideDispensingPressure;
                seg.SuckBackTime = segRef.OverrideSuckBackTime;
                seg.GlueTriggerOffsetMm = segRef.OverrideGlueTriggerOffsetMm;
                seg.CornerDecel = segRef.OverrideCornerDecel;
                seg.TeachHeight = segRef.OverrideTeachHeight;
                seg.HeightCompensation = segRef.OverrideHeightCompensation;
            }

            seg.HeightCompensation += ResolveZCompensation(detail);

            return seg;
        }

        /// <summary>
        /// 解析链接全局变量的值：若链接变量名非空则从配方池读取，否则返回手动值
        /// </summary>
        private double ResolveLinkedValue(double manualValue, string linkedVarName)
        {
            if (string.IsNullOrEmpty(linkedVarName)) return manualValue;

            try
            {
                var poolId = _recipePoolService?.CurrentPoolName;
                if (!string.IsNullOrEmpty(poolId))
                {
                    var variables = _recipePoolService.LoadGlobalVariablesAsync(poolId).Result;
                    var gv = variables.FirstOrDefault(v => v.Name == linkedVarName);
                    if (gv != null && double.TryParse(gv.Value, out double gvValue))
                        return gvValue;
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"解析链接全局变量 '{linkedVarName}' 失败: {ex.Message}");
            }

            return manualValue;
        }

        /// <summary>
        /// 解析Z向补偿总值（3D相机 + 校准器 + 手动，三来源叠加）
        /// </summary>
        private double ResolveZCompensation(DispenseDetail detail)
        {
            double compensation = 0.0;

            compensation += ResolveLinkedValue(detail.ZCompensation3D, detail.ZCompensation3DLinkedVar);
            compensation += ResolveLinkedValue(detail.ZCompensationCalibrator, detail.ZCompensationCalibratorLinkedVar);
            compensation += detail.ManualZCompensation;

            return compensation;
        }

        /// <summary>
        /// 从全局 hwcfg 轴配置解析点胶工站轴逻辑ID。
        /// DISPENSE 步骤可能在装配站等其他工站的工艺序列中执行，
        /// 不能依赖传入的 task.FindAxisIdByName（仅搜索当前工站已发现的轴）。
        /// </summary>
        private int ResolveDispenseAxisId(string axisName)
        {
            if (string.IsNullOrEmpty(axisName)) return -1;

            var configs = _motionService.GetAxisConfigurations();
            foreach (var candidate in GetAxisNameCandidates(axisName))
            {
                var cfg = configs.FirstOrDefault(a =>
                    string.Equals(a.Name, candidate, StringComparison.Ordinal));
                if (cfg != null)
                    return cfg.LogicalId;
            }

            _logger.Warn($"DISPENSE 无法从全局轴配置解析轴 '{axisName}'");
            return -1;
        }

        /// <summary>
        /// 轴名称候选列表——兼容 hwcfg 中 Dz₂/Dz2、Dz₃/Dz3 等不同写法
        /// </summary>
        private static IEnumerable<string> GetAxisNameCandidates(string axisName)
        {
            yield return axisName;
            switch (axisName)
            {
                case "Dz₂": yield return "Dz2"; break;
                case "Dz2": yield return "Dz₂"; break;
                case "Dz₃": yield return "Dz3"; break;
                case "Dz3": yield return "Dz₃"; break;
            }
        }

        /// <summary>根据针头索引选择出胶 IO 端口（0=针头1/Dz₂, 1=针头2/Dz₃）</summary>
        private static int GetGlueIoPort(int needleIndex) =>
            needleIndex == 0 ? GlueIoPort1 : GlueIoPort2;

        /// <summary>写出胶 IO——按所选针头使用对应端口</summary>
        private void WriteGlueIo(bool value, int needleIndex)
        {
            int port = GetGlueIoPort(needleIndex);
            try { _motionService.WriteDo(port, value); }
            catch (Exception ex) { _logger.Error(ex, $"DISPENSE 写出胶IO失败 port={port} value={value}"); }
        }

        /// <summary>安全关胶——按所选针头关闭对应 IO</summary>
        private void SafeGlueOff(int needleIndex)
        {
            try { _motionService.WriteDo(GetGlueIoPort(needleIndex), false); }
            catch { }
        }

        /// <summary>
        /// 安全获取点的机器坐标——严禁使用 OffsetX/X 等未转换坐标作为运动目标，
        /// MachineX/MachineY 为空时立即抛出异常中止运动，防止设备撞机。
        /// EnableComp 启用时在 MachineX/MachineY 上叠加 XY 补偿。
        /// </summary>
        private (double X, double Y) GetMachineXY(CadPoint pt)
        {
            if (pt.MachineX == null || pt.MachineY == null)
                throw new InvalidOperationException(
                    $"DISPENSE 致命错误: 点[Id={pt.Id}] MachineX/MachineY 为空，禁止使用未转换坐标执行运动");

            double x = pt.MachineX.Value;
            double y = pt.MachineY.Value;
            if (_enableComp)
            {
                x += _xCompensation;
                y += _yCompensation;
            }
            return (x, y);
        }
    }
}
