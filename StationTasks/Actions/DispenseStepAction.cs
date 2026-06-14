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

        private const int CoordId = 0;
        private const int GlueIoPort = 0;
        private const double DefaultAcc = 0.05;
        private const double DefaultDec = 0.05;

        public StepType SupportedStepType => StepType.DISPENSE;

        public DispenseStepAction(
            IRecipePoolService recipePoolService,
            ILoggerService logger,
            IStationRegistry stationRegistry,
            IMotionService motionService)
        {
            _recipePoolService = recipePoolService;
            _logger = logger;
            _stationRegistry = stationRegistry;
            _motionService = motionService;
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

            var sourceSegments = GetSourceSegments();
            var segDict = sourceSegments.Where(s => !string.IsNullOrEmpty(s.SegmentId))
                .ToDictionary(s => s.SegmentId, s => s);

            int dxAxisId = ResolveAxisId("Dx", task);
            int dyAxisId = ResolveAxisId("Dy", task);
            int dzAxisId = ResolveAxisId("Dz₁", task);

            try
            {
                if (detail.IsDryRunMode)
                    await ExecuteDryRunAsync(detail, segDict, dxAxisId, dyAxisId, dzAxisId, token);

                if (detail.IsRealDispenseMode)
                {
                    switch (detail.DispenseMode)
                    {
                        case DispenseStepMode.Dot:
                            await ExecuteDotModeAsync(detail, segDict, dxAxisId, dyAxisId, dzAxisId, token);
                            break;
                        case DispenseStepMode.Arc:
                            await ExecuteArcModeAsync(detail, segDict, dxAxisId, dyAxisId, dzAxisId, token);
                            break;
                        default:
                            _logger.Warn($"DISPENSE 步骤 [{step.Seq}] 未知点胶模式: {detail.DispenseMode}");
                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                SafeGlueOff();
                _logger.Warn($"DISPENSE 步骤 [{step.Seq}] 已取消，已安全关胶");
                throw;
            }
            catch (Exception ex)
            {
                SafeGlueOff();
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
            double safeHeight = detail.DefaultSafeHeight;
            double moveSpeed = detail.DefaultMoveSpeed;

            await _motionService.MoveAbsAsync(dzAxisId, safeHeight, moveSpeed, token);

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

                var startPt = seg.Points.First();
                double startX = startPt.MachineX ?? startPt.X;
                double startY = startPt.MachineY ?? startPt.Y;

                await _motionService.MoveLineAbsAsync(CoordId, new[] { dxAxisId, dyAxisId },
                    new[] { startX, startY }, moveSpeed, token);

                foreach (var pt in seg.Points.Skip(1))
                {
                    token.ThrowIfCancellationRequested();
                    double px = pt.MachineX ?? pt.X;
                    double py = pt.MachineY ?? pt.Y;
                    await _motionService.MoveLineAbsAsync(CoordId, new[] { dxAxisId, dyAxisId },
                        new[] { px, py }, moveSpeed, token);
                }

                await _motionService.MoveAbsAsync(dzAxisId, safeHeight, moveSpeed, token);
            }

            await _motionService.MoveAbsAsync(dzAxisId, detail.DefaultSafeHeight, moveSpeed, token);
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
            CancellationToken token)
        {
            _logger.Info($"DISPENSE 单点模式开始");

            var enabledRefs = detail.SegmentRefs.Where(r => r.IsEnabled).ToList();
            int totalRefs = enabledRefs.Count;
            int currentRef = 0;

            double moveSpeed = detail.DefaultMoveSpeed;
            double safeHeight = detail.DefaultSafeHeight;

            await _motionService.MoveAbsAsync(dzAxisId, safeHeight, moveSpeed, token);

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
                double targetZ = seg.EffectiveZHeight;
                double approachOffset = seg.ApproachHeight;
                double slowVel = seg.MoveSpeed * seg.CornerDecel;
                double glueTriggerOffset = seg.GlueTriggerOffsetMm;

                _logger.Info($"DISPENSE 单点: 段[{seg.SegmentId}] ({currentRef}/{totalRefs})，{seg.Points.Count} 点");

                foreach (var (point, ptIndex) in seg.Points.Select((p, i) => (p, i)))
                {
                    token.ThrowIfCancellationRequested();

                    double px = point.MachineX ?? point.OffsetX ?? point.X;
                    double py = point.MachineY ?? point.OffsetY ?? point.Y;

                    await _motionService.MoveAbsAsync(dzAxisId, safeHeight, moveSpeed, token);

                    await _motionService.MoveLineAbsAsync(CoordId, new[] { dxAxisId, dyAxisId },
                        new[] { px, py }, moveSpeed, token);

                    double approachZ = targetZ + approachOffset;
                    await _motionService.MoveAbsAsync(dzAxisId, approachZ, moveSpeed, token);

                    // 位置触发开胶：计算触发点Z，慢速移到触发位开胶，再继续到目标位
                    double triggerDistance = Math.Abs(glueTriggerOffset);
                    int motionDir = Math.Sign(approachZ - targetZ);
                    double triggerZ = targetZ + motionDir * triggerDistance;

                    await _motionService.MoveAbsAsync(dzAxisId, triggerZ, slowVel, token);
                    WriteGlueIo(true);
                    _logger.Debug($"DISPENSE 单点: 段[{seg.SegmentId}]点{ptIndex + 1} 位置触发开胶，triggerZ={triggerZ:F3}, targetZ={targetZ:F3}, offset={glueTriggerOffset:F3}mm");

                    await _motionService.MoveAbsAsync(dzAxisId, targetZ, slowVel, token);

                    if (seg.PreDelay > 0)
                        await Task.Delay((int)seg.PreDelay, token);

                    await Task.Delay((int)seg.DispenseTime, token);

                    WriteGlueIo(false);

                    if (seg.PostDelay > 0)
                        await Task.Delay((int)seg.PostDelay, token);

                    await _motionService.MoveAbsAsync(dzAxisId, safeHeight, moveSpeed, token);
                }
            }

            await _motionService.MoveAbsAsync(dzAxisId, detail.DefaultSafeHeight, moveSpeed, token);
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
            CancellationToken token)
        {
            _logger.Info($"DISPENSE 弧线模式开始");

            var enabledRefs = detail.SegmentRefs
                .Where(r => r.IsEnabled &&
                       (r.SourceEntityType == CadEntityType.Arc || r.SourceEntityType == CadEntityType.Circle))
                .ToList();
            int totalRefs = enabledRefs.Count;
            int currentRef = 0;

            double moveSpeed = detail.DefaultMoveSpeed;
            double safeHeight = detail.DefaultSafeHeight;

            await _motionService.MoveAbsAsync(dzAxisId, safeHeight, moveSpeed, token);

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
                double targetZ = seg.EffectiveZHeight;
                double approachOffset = seg.ApproachHeight;
                double slowVel = seg.MoveSpeed * seg.CornerDecel;
                double glueTriggerOffset = seg.GlueTriggerOffsetMm;

                _logger.Info($"DISPENSE 弧线: 段[{seg.SegmentId}] ({currentRef}/{totalRefs})，{seg.Points.Count} 点");

                await _motionService.MoveAbsAsync(dzAxisId, safeHeight, moveSpeed, token);

                var startPt = seg.Points.First();
                double startX = startPt.MachineX ?? startPt.X;
                double startY = startPt.MachineY ?? startPt.Y;
                await _motionService.MoveLineAbsAsync(CoordId, new[] { dxAxisId, dyAxisId },
                    new[] { startX, startY }, moveSpeed, token);

                double approachZ = targetZ + approachOffset;
                await _motionService.MoveAbsAsync(dzAxisId, approachZ, moveSpeed, token);

                // 位置触发开胶：计算触发点Z，慢速移到触发位开胶，再继续到目标位
                double triggerDistance = Math.Abs(glueTriggerOffset);
                int motionDir = Math.Sign(approachZ - targetZ);
                double triggerZ = targetZ + motionDir * triggerDistance;

                await _motionService.MoveAbsAsync(dzAxisId, triggerZ, slowVel, token);
                WriteGlueIo(true);
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
                    CoordId, new[] { dxAxisId, dyAxisId },
                    startVel: 5, maxVel: seg.MoveSpeed, acc: DefaultAcc, dec: DefaultDec, endVel: 0);

                foreach (var pt in seg.Points)
                {
                    double px = pt.MachineX ?? pt.X;
                    double py = pt.MachineY ?? pt.Y;
                    _motionService.AddLineSegment(CoordId, new[] { px, py });
                }

                _motionService.ExecuteContinuousInterpolation(CoordId);

                bool completed = await _motionService.WaitForCoordMotionCompletionAsync(
                    CoordId, TimeSpan.FromMinutes(5), token);

                if (!completed)
                    throw new TimeoutException($"DISPENSE 弧线: 段[{seg.SegmentId}] 运动超时");

                WriteGlueIo(false);

                if (seg.PostDelay > 0)
                    await Task.Delay((int)seg.PostDelay, token);

                await _motionService.MoveAbsAsync(dzAxisId, safeHeight, moveSpeed, token);
            }

            await _motionService.MoveAbsAsync(dzAxisId, detail.DefaultSafeHeight, moveSpeed, token);
            _logger.Info("DISPENSE 弧线模式完成");
        }

        /// <summary>
        /// 从点胶工站参数获取源段数据
        /// </summary>
        private List<DispenseSegment> GetSourceSegments()
        {
            try
            {
                var station = _stationRegistry.GetStation("DispenserStation");
                if (station is IStationParameterProvider provider)
                {
                    if (provider.CurrentParameters is DispenserStationParams dispenserParams)
                        return dispenserParams.Segments ?? new List<DispenseSegment>();
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"获取点胶工站段数据失败: {ex.Message}");
            }
            return new List<DispenseSegment>();
        }

        /// <summary>
        /// 根据源段和引用参数创建带工艺参数的临时段
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
                seg.JumpSpeed = detail.DefaultJumpSpeed;
                seg.MoveSpeed = detail.DefaultMoveSpeed;
                seg.SafeHeight = detail.DefaultSafeHeight;
                seg.ApproachHeight = detail.DefaultApproachHeight;
                seg.DispenseAmount = detail.DefaultDispenseAmount;
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
                seg.JumpSpeed = segRef.OverrideJumpSpeed;
                seg.MoveSpeed = segRef.OverrideMoveSpeed;
                seg.SafeHeight = segRef.OverrideSafeHeight;
                seg.ApproachHeight = segRef.OverrideApproachHeight;
                seg.DispenseAmount = segRef.OverrideDispenseAmount;
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
        /// 根据轴名称解析逻辑轴ID
        /// </summary>
        private int ResolveAxisId(string axisName, StationTaskBase task)
        {
            int axisId = task.FindAxisIdByName(axisName);
            if (axisId < 0)
            {
                _logger.Warn($"无法解析轴'{axisName}'，使用默认值");
                return 0;
            }
            return axisId;
        }

        private void WriteGlueIo(bool value)
        {
            try { _motionService.WriteDo(GlueIoPort, value); }
            catch (Exception ex) { _logger.Error(ex, $"DISPENSE 写出胶IO失败 port={GlueIoPort} value={value}"); }
        }

        private void SafeGlueOff()
        {
            try { _motionService.WriteDo(GlueIoPort, false); }
            catch { }
        }
    }
}
