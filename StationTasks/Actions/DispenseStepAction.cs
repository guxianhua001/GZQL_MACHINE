using Core.Abstraction;
using Core.Models;
using Core.Services;
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
        private readonly ICadAlignTransformService _cadAlignTransformService;
        private readonly INeedleCameraCalibrationProvider _needleCameraCalibrationProvider;

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

        /// <summary>当前步骤是否启用针头偏移补偿（相机中心坐标→实际针头坐标）</summary>
        private bool _enableNeedleOffsetComp;

        /// <summary>当前步骤解析后的针头偏移补偿量（mm）：相机与针头固定距离 + 对针补偿</summary>
        private double _needleOffsetX;
        private double _needleOffsetY;

        /// <summary>当前步骤 X/Y Comp（校准器）补偿量（mm）</summary>
        private double _xCompCalibrator;
        private double _yCompCalibrator;

        /// <summary>当前步骤是否启用校准（X/Y/Z Comp 校准器 + Z Comp 3D Camera）</summary>
        private bool _enableCalibration;

        /// <summary>当前步骤是否启用旋转补偿（产品旋转后按 Coord Transform 换算坐标）</summary>
        private bool _enableRotationComp;

        /// <summary>当前步骤解析后的产品旋转角度（度数）</summary>
        private double _rotationAngle;

        /// <summary>CAD 对齐坐标变换快照（启用旋转补偿时使用）</summary>
        private CadAlignTransformSnapshot _cadAlignSnapshot;

        /// <summary>执行时按 NeedleIndex 加载的仿射矩阵（优先于点内缓存 MachineX/Y）</summary>
        private AffineCalibrationResult _runtimeAffine;

        public StepType SupportedStepType => StepType.DISPENSE;

        public DispenseStepAction(
            IRecipePoolService recipePoolService,
            ILoggerService logger,
            IStationRegistry stationRegistry,
            IMotionService motionService,
            IDispenseSegmentSourceService segmentSourceService,
            ICadAlignTransformService cadAlignTransformService,
            INeedleCameraCalibrationProvider needleCameraCalibrationProvider)
        {
            _recipePoolService = recipePoolService;
            _logger = logger;
            _stationRegistry = stationRegistry;
            _motionService = motionService;
            _segmentSourceService = segmentSourceService;
            _cadAlignTransformService = cadAlignTransformService;
            _needleCameraCalibrationProvider = needleCameraCalibrationProvider;
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

            // 运动/工艺等待须合并暂停信号，避免暂停后 WaitForDone 误报运动超时
            var motionToken = task.MotionCancellationToken;

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

            // 解析针头偏移补偿（启用时叠加：相机与针头固定距离 + 对针补偿）
            _enableNeedleOffsetComp = detail.EnableNeedleOffsetComp;
            if (_enableNeedleOffsetComp)
            {
                (_needleOffsetX, _needleOffsetY) = ResolveNeedleOffset(detail, needleIndex);
                _logger.Info($"DISPENSE 步骤 [{step.Seq}] 针头偏移补偿已启用: dX={_needleOffsetX:F4}mm, dY={_needleOffsetY:F4}mm");
            }
            else
            {
                _needleOffsetX = 0;
                _needleOffsetY = 0;
            }

            // 解析校准补偿（Enable Calibration 启用时叠加 X/Y Comp 校准器）
            _enableCalibration = detail.EnableZCalibration;
            if (_enableCalibration)
            {
                _xCompCalibrator = ResolveLinkedValue(detail.XCompensationCalibrator, detail.XCompensationCalibratorLinkedVar);
                _yCompCalibrator = ResolveLinkedValue(detail.YCompensationCalibrator, detail.YCompensationCalibratorLinkedVar);
                _logger.Info($"DISPENSE 步骤 [{step.Seq}] 校准补偿已启用: X Comp={_xCompCalibrator:F4}mm, Y Comp={_yCompCalibrator:F4}mm");
            }
            else
            {
                _xCompCalibrator = 0;
                _yCompCalibrator = 0;
            }

            // 解析旋转补偿（启用时按 CAD 对齐 Coord Transform 换算旋转后坐标）
            _enableRotationComp = detail.EnableRotationComp;
            if (_enableRotationComp)
            {
                _rotationAngle = ResolveLinkedValue(detail.RotationAngle, detail.RotationAngleLinkedVar);
                _cadAlignSnapshot = _cadAlignTransformService?.CurrentSnapshot;
                if (_cadAlignSnapshot == null || !_cadAlignSnapshot.IsValid)
                {
                    _logger.Warn($"DISPENSE 步骤 [{step.Seq}] 旋转补偿已启用但 CAD 对齐变换不可用，回退使用原始坐标");
                    _enableRotationComp = false;
                }
                else
                {
                    _logger.Info($"DISPENSE 步骤 [{step.Seq}] 旋转补偿已启用: 旋转角度={_rotationAngle:F3}°, 回转中心=({_cadAlignSnapshot.Mox:F3}, {_cadAlignSnapshot.Moy:F3})");
                }
            }

            // 按配方针头加载仿射矩阵，执行时实时换算 MachineX/Y，避免与 Step3 所选针头不一致
            _runtimeAffine = LoadAffineForNeedle(detail.NeedleIndex);
            if (_runtimeAffine != null)
                _logger.Info($"DISPENSE 步骤 [{step.Seq}] 使用针头{needleIndex + 1}仿射矩阵实时换算坐标 (RMS={_runtimeAffine.RmsError:F4}mm)");
            else
                _logger.Warn($"DISPENSE 步骤 [{step.Seq}] 未找到针头{needleIndex + 1}仿射矩阵，回退使用点内 MachineX/MachineY");

            try
            {
                if (detail.IsDryRunMode)
                    await ExecuteDryRunAsync(detail, segDict, dxAxisId, dyAxisId, dzAxisId, motionToken);

                if (detail.IsRealDispenseMode)
                {
                    switch (detail.DispenseMode)
                    {
                        case DispenseStepMode.Dot:
                            await ExecuteDotModeAsync(detail, segDict, dxAxisId, dyAxisId, dzAxisId, needleIndex, motionToken);
                            break;
                        case DispenseStepMode.Arc:
                            await ExecuteArcModeAsync(detail, segDict, dxAxisId, dyAxisId, dzAxisId, needleIndex, motionToken);
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
            CancellationToken motionToken)
        {
            _logger.Info("DISPENSE 开始空跑");

            var enabledRefs = detail.SegmentRefs.Where(r => r.IsEnabled).ToList();
            DispenseSegment lastSeg = null;

            foreach (var segRef in enabledRefs)
            {
                motionToken.ThrowIfCancellationRequested();

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

                await _motionService.MoveAbsAsync(dzAxisId, safeHeight, moveSpeed, motionToken);

                var startPt = seg.Points.First();
                var (startX, startY) = GetMachineXY(startPt, seg);

                await _motionService.MoveLineAbsAsync(CoordIdLinear, new[] { dxAxisId, dyAxisId },
                    new[] { startX, startY }, moveSpeed, motionToken);

                foreach (var pt in seg.Points.Skip(1))
                {
                    motionToken.ThrowIfCancellationRequested();
                    var (px, py) = GetMachineXY(pt, seg);
                    await _motionService.MoveLineAbsAsync(CoordIdLinear, new[] { dxAxisId, dyAxisId },
                        new[] { px, py }, moveSpeed, motionToken);
                }

                await _motionService.MoveAbsAsync(dzAxisId, safeHeight, moveSpeed, motionToken);
            }

            if (lastSeg != null)
                await _motionService.MoveAbsAsync(dzAxisId, lastSeg.SafeHeight, lastSeg.MoveSpeed, motionToken);
            else
                await _motionService.MoveAbsAsync(dzAxisId, detail.DefaultSafeHeight, detail.DefaultMoveSpeed, motionToken);

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
            CancellationToken motionToken)
        {
            _logger.Info($"DISPENSE 单点模式开始");

            var enabledRefs = detail.SegmentRefs.Where(r => r.IsEnabled).ToList();
            int totalRefs = enabledRefs.Count;
            int currentRef = 0;
            DispenseSegment lastSeg = null;

            foreach (var segRef in enabledRefs)
            {
                motionToken.ThrowIfCancellationRequested();
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
                    motionToken.ThrowIfCancellationRequested();

                    var (px, py) = GetMachineXY(point, seg);

                    await _motionService.MoveAbsAsync(dzAxisId, safeHeight, moveSpeed, motionToken);

                    await _motionService.MoveLineAbsAsync(CoordIdLinear, new[] { dxAxisId, dyAxisId },
                        new[] { px, py }, moveSpeed, motionToken);

                    double approachZ = targetZ + approachOffset;
                    await _motionService.MoveAbsAsync(dzAxisId, approachZ, moveSpeed, motionToken);

                    // 位置触发开胶：计算触发点Z，慢速移到触发位开胶，再继续到目标位
                    double triggerDistance = Math.Abs(glueTriggerOffset);
                    int motionDir = Math.Sign(approachZ - targetZ);
                    double triggerZ = targetZ + motionDir * triggerDistance;

                    await _motionService.MoveAbsAsync(dzAxisId, triggerZ, slowVel, motionToken);
                    WriteGlueIo(true, needleIndex);
                    _logger.Debug($"DISPENSE 单点: 段[{seg.SegmentId}]点{ptIndex + 1} 位置触发开胶，triggerZ={triggerZ:F3}, targetZ={targetZ:F3}, offset={glueTriggerOffset:F3}mm");

                    await _motionService.MoveAbsAsync(dzAxisId, targetZ, slowVel, motionToken);

                    if (seg.PreDelay > 0)
                        await Task.Delay((int)seg.PreDelay, motionToken);

                    await Task.Delay((int)seg.DispenseTime, motionToken);

                    WriteGlueIo(false, needleIndex);

                    if (seg.PostDelay > 0)
                        await Task.Delay((int)seg.PostDelay, motionToken);

                    await _motionService.MoveAbsAsync(dzAxisId, safeHeight, moveSpeed, motionToken);
                }
            }

            if (lastSeg != null)
                await _motionService.MoveAbsAsync(dzAxisId, lastSeg.SafeHeight, lastSeg.MoveSpeed, motionToken);
            else
                await _motionService.MoveAbsAsync(dzAxisId, detail.DefaultSafeHeight, detail.DefaultMoveSpeed, motionToken);

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
            CancellationToken motionToken)
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
                motionToken.ThrowIfCancellationRequested();
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

                await _motionService.MoveAbsAsync(dzAxisId, safeHeight, moveSpeed, motionToken);

                var startPt = seg.Points.First();
                var (startX, startY) = GetMachineXY(startPt, seg);
                await _motionService.MoveLineAbsAsync(CoordIdLinear, new[] { dxAxisId, dyAxisId },
                    new[] { startX, startY }, moveSpeed, motionToken);

                double approachZ = targetZ + approachOffset;
                await _motionService.MoveAbsAsync(dzAxisId, approachZ, moveSpeed, motionToken);

                // 位置触发开胶：计算触发点Z，慢速移到触发位开胶，再继续到目标位
                double triggerDistance = Math.Abs(glueTriggerOffset);
                int motionDir = Math.Sign(approachZ - targetZ);
                double triggerZ = targetZ + motionDir * triggerDistance;

                await _motionService.MoveAbsAsync(dzAxisId, triggerZ, slowVel, motionToken);
                WriteGlueIo(true, needleIndex);
                _logger.Debug($"DISPENSE 弧线: 段[{seg.SegmentId}] 位置触发开胶，triggerZ={triggerZ:F3}, targetZ={targetZ:F3}, offset={glueTriggerOffset:F3}mm");

                await _motionService.MoveAbsAsync(dzAxisId, targetZ, slowVel, motionToken);

                if (seg.PreDelay > 0)
                    await Task.Delay((int)seg.PreDelay, motionToken);

                double currentZPos = _motionService.GetAxisPosition(dzAxisId);
                if (Math.Abs(currentZPos - targetZ) > 0.5)
                {
                    _logger.Warn($"DISPENSE 弧线: 段[{seg.SegmentId}] Z轴未到位: 当前={currentZPos:F3}, 目标={targetZ:F3}，重新下降");
                    await _motionService.MoveAbsAsync(dzAxisId, targetZ, slowVel, motionToken);
                }

                _motionService.InitializeContinuousInterpolation(
                    CoordIdContinuous, new[] { dxAxisId, dyAxisId },
                    startVel: 5, maxVel: seg.InterpSpeed, acc: DefaultAcc, dec: DefaultDec, endVel: 0);

                foreach (var pt in seg.Points)
                {
                    var (px, py) = GetMachineXY(pt, seg);
                    _motionService.AddLineSegment(CoordIdContinuous, new[] { px, py });
                }

                _motionService.ExecuteContinuousInterpolation(CoordIdContinuous);

                bool completed = await _motionService.WaitForCoordMotionCompletionAsync(
                    CoordIdContinuous, TimeSpan.FromMinutes(5), motionToken);

                if (!completed)
                    throw new TimeoutException($"DISPENSE 弧线: 段[{seg.SegmentId}] 运动超时");

                WriteGlueIo(false, needleIndex);

                if (seg.PostDelay > 0)
                    await Task.Delay((int)seg.PostDelay, motionToken);

                await _motionService.MoveAbsAsync(dzAxisId, safeHeight, moveSpeed, motionToken);
            }

            if (lastSeg != null)
                await _motionService.MoveAbsAsync(dzAxisId, lastSeg.SafeHeight, lastSeg.MoveSpeed, motionToken);
            else
                await _motionService.MoveAbsAsync(dzAxisId, detail.DefaultSafeHeight, detail.DefaultMoveSpeed, motionToken);

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

            if (detail.EnableZCalibration)
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
        /// 解析针头偏移补偿（X/Y）：相机与针头固定距离 + 对针补偿，两来源叠加。
        /// 相机与针头固定距离：Link 勾选时取链接全局变量值，否则为 0。对针补偿支持链接全局变量。
        /// X/Y Comp（校准器）由 XCompensationCalibrator/YCompensationCalibrator 单独叠加，见 GetMachineXY。
        /// </summary>
        private (double X, double Y) ResolveNeedleOffset(DispenseDetail detail, int needleIndex)
        {
            // 相机与针头固定距离：Link 勾选时取全局变量，否则为 0
            double cameraNeedleX = 0;
            double cameraNeedleY = 0;
            if (detail.LinkCameraNeedleOffsetToCalibration)
            {
                cameraNeedleX = ResolveLinkedValue(detail.CameraNeedleOffsetX, detail.CameraNeedleOffsetXLinkedVar);
                cameraNeedleY = ResolveLinkedValue(detail.CameraNeedleOffsetY, detail.CameraNeedleOffsetYLinkedVar);
            }

            // 对针补偿
            double alignX = ResolveLinkedValue(detail.NeedleAlignCompX, detail.NeedleAlignCompXLinkedVar);
            double alignY = ResolveLinkedValue(detail.NeedleAlignCompY, detail.NeedleAlignCompYLinkedVar);

            return (cameraNeedleX + alignX, cameraNeedleY + alignY);
        }

        /// <summary>
        /// 解析 Z 向校准补偿总值（Z Comp 3D Camera + Z Comp 校准器）
        /// </summary>
        private double ResolveZCompensation(DispenseDetail detail)
        {
            double compensation = 0.0;

            compensation += ResolveLinkedValue(detail.ZCompensation3D, detail.ZCompensation3DLinkedVar);
            compensation += ResolveLinkedValue(detail.ZCompensationCalibrator, detail.ZCompensationCalibratorLinkedVar);

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

        /// <summary>从轨迹 JSON 对齐数据加载指定针头的仿射矩阵</summary>
        private AffineCalibrationResult LoadAffineForNeedle(int needleIndex)
        {
            var alignData = _segmentSourceService.TryLoadAlignData();
            if (alignData == null)
                return null;

            var data = needleIndex == 0
                ? alignData.AffineResultDataNeedle1 ?? alignData.AffineResultData
                : alignData.AffineResultDataNeedle2;

            if (data == null || data.PointCount < 3)
                return null;

            return new AffineCalibrationResult
            {
                A = data.A,
                B = data.B,
                C = data.C,
                D = data.D,
                Tx = data.Tx,
                Ty = data.Ty,
                RmsError = data.RmsError,
                PointCount = data.PointCount
            };
        }

        /// <summary>
        /// 安全获取点的机器坐标——优先按当前针头仿射矩阵从 CAD 坐标实时换算；
        /// 无仿射时回退点内 MachineX/MachineY。
        /// 最终坐标 = 变换后坐标 + 针头偏移(可选) + 校准补偿(可选) + X/Y Compensation(可选)。
        /// EnableRotationComp 启用时使用 CAD 对齐 Coord Transform 换算旋转后坐标。
        /// </summary>
        private (double X, double Y) GetMachineXY(CadPoint pt, DispenseSegment seg = null)
        {
            double x;
            double y;

            // 旋转补偿优先：使用 CAD 对齐变换快照按旋转角度换算坐标
            if (_enableRotationComp && _cadAlignSnapshot != null && _cadAlignSnapshot.IsValid)
            {
                (x, y) = _cadAlignSnapshot.Transform(pt.X, pt.Y, _rotationAngle);
                if (seg != null)
                {
                    x += seg.XyCompensationX;
                    y += seg.XyCompensationY;
                }
            }
            else if (_runtimeAffine != null)
            {
                (x, y) = AffineCalibrationService.Transform(_runtimeAffine, pt.X, pt.Y);
                if (seg != null)
                {
                    x += seg.XyCompensationX;
                    y += seg.XyCompensationY;
                }
            }
            else if (pt.MachineX != null && pt.MachineY != null)
            {
                x = pt.MachineX.Value;
                y = pt.MachineY.Value;
            }
            else
            {
                throw new InvalidOperationException(
                    $"DISPENSE 致命错误: 点[Id={pt.Id}] 无仿射矩阵且 MachineX/MachineY 为空，禁止运动");
            }

            // 针头偏移补偿：将相机中心坐标换算为实际点胶针头坐标
            if (_enableNeedleOffsetComp)
            {
                x += _needleOffsetX;
                y += _needleOffsetY;
            }

            // 校准补偿：X/Y Comp（校准器）
            if (_enableCalibration)
            {
                x += _xCompCalibrator;
                y += _yCompCalibrator;
            }

            if (_enableComp)
            {
                x += _xCompensation;
                y += _yCompensation;
            }

            return (x, y);
        }
    }
}
