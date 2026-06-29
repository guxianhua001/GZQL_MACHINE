using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using MotionControl.Exceptions;
using MotionControl.Interfaces;
using StationTasks.Models;
using Recipe.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StationTasks.Tasks;

namespace StationTasks.Actions
{
    /// <summary>
    /// RELEASE 步骤动作：执行释放运动序列 → 夹爪释放 → 延时 → 真空控制
    /// 释放延时从 ReleaseDetail.ReleaseDelayTime 读取，无硬编码
    /// </summary>
    public class ReleaseStepAction : IProcessStepAction
    {
        private readonly IRecipePoolService _recipePoolService;
        private readonly ILoggerService _logger;
        private readonly IStationRegistry _stationRegistry;
        private readonly IGripperService _gripperService;
        private readonly ILocalizationService _localization;

        public StepType SupportedStepType => StepType.RELEASE;

        public ReleaseStepAction(
            IRecipePoolService recipePoolService,
            ILoggerService logger,
            IStationRegistry stationRegistry,
            IGripperService gripperService,
            ILocalizationService localization)
        {
            _recipePoolService = recipePoolService;
            _logger = logger;
            _stationRegistry = stationRegistry;
            _gripperService = gripperService;
            _localization = localization;
        }

        /// <summary>
        /// 执行 RELEASE 步骤：运动到释放位 → 释放 → 延时 → 真空控制
        /// </summary>
        public async Task ExecuteAsync(ProcessStep step, StationTaskBase task, CancellationToken token)
        {
            var releaseDetail = step.ReleaseDetail;
            if (releaseDetail == null)
            {
                _logger.Warn(string.Format(
                    _localization.GetResourceOrDefault("Rel_Log_NoReleaseDetail", "RELEASE 步骤 [{0}] 没有 ReleaseDetail，跳过执行"),
                    step.Seq));
                return;
            }

            List<GlobalVariable> globalVars = null;
            try
            {
                var poolId = _recipePoolService.CurrentPoolName;
                if (!string.IsNullOrEmpty(poolId))
                    globalVars = await _recipePoolService.LoadGlobalVariablesAsync(poolId);
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(
                    _localization.GetResourceOrDefault("Rel_Log_LoadGlobalVarsFailed", "加载全局变量失败: {0}，偏移变量名将无法解析"),
                    ex.Message));
            }

            // 执行释放运动序列（包含内嵌的夹爪/延时等动作）
            if (releaseDetail.ReleaseMoves != null && releaseDetail.ReleaseMoves.Count > 0)
            {
                foreach (var subMove in releaseDetail.ReleaseMoves)
                {
                    token.ThrowIfCancellationRequested();

                    // 先执行子步骤动作（夹爪/延时等），再执行运动
                    await ExecuteSubMoveActionAsync(subMove, releaseDetail, step.Seq, token);

                    // 仅当有轴配置时才执行运动（纯动作行如 Release/Hold 不需要运动）
                    if (!string.IsNullOrEmpty(subMove.Axis) || subMove.AxisId > 0)
                    {
                        StationTaskBase targetTask = ResolveTargetTask(subMove.StationId, task);
                        int axisId = ResolveAxisId(subMove, targetTask);
                        string axisName = targetTask.GetAxisNameById(axisId);

                        double totalOffset = 0;
                        if (subMove.Offset != 0)
                            totalOffset += subMove.Offset;
                        if (!string.IsNullOrEmpty(subMove.OffsetVariableName) && globalVars != null)
                            totalOffset += ResolveVariableOffset(subMove.OffsetVariableName, globalVars);

                        double speed = subMove.Speed > 0 ? subMove.Speed : 10.0;
                        double posValue = await targetTask.GetPositionValueAsync(subMove.PositionName, axisName);
                        double targetPos = posValue + totalOffset;
                        // 记录配方位置值、偏移量和最终目标位置
                        _logger.Info(string.Format(
                            _localization.GetResourceOrDefault("Rel_Log_ReleaseMove", "RELEASE SubMove [{0}]: 工站={1}, 轴{2}({3}) → '{4}'={5:F3}, 偏移{6:F3}, 目标位置={7:F3}, 速度{8}"),
                            subMove.SubSeq, targetTask.StationIdentifierValue, axisId, axisName, subMove.PositionName, posValue, totalOffset, targetPos, speed));

                        TaskState? overrideState = targetTask != task ? task.State : null;
                        string moveLabel = $"[{step.Seq}] {axisName} → {subMove.PositionName} ({posValue:F3})+{totalOffset:F3}={targetPos:F3}";
                        targetTask.PublishStepStatus(moveLabel, overrideState);

                        await targetTask.ExecuteMoveAsync(axisId, subMove.PositionName, speed, totalOffset);
                        targetTask.CompleteStepStatus(overrideState);
                    }
                }
            }
        }

        private StationTaskBase ResolveTargetTask(string stationId, StationTaskBase defaultTask)
        {
            if (string.IsNullOrEmpty(stationId))
                return defaultTask;

            var station = _stationRegistry.GetAllStations()
                .FirstOrDefault(s => s.StationIdentifier == stationId);
            if (station is StationTaskBase task)
                return task;

            _logger.Warn(string.Format(
                _localization.GetResourceOrDefault("Rel_Log_StationNotFound", "SubMove 指定的工站 '{0}' 未找到，使用默认工站 '{1}'"),
                stationId, defaultTask.TaskName));
            return defaultTask;
        }

        private int ResolveAxisId(SubMove subMove, StationTaskBase targetTask)
        {
            if (subMove.AxisId > 0)
                return subMove.AxisId;

            if (!string.IsNullOrEmpty(subMove.Axis))
            {
                int resolvedId = targetTask.FindAxisIdByName(subMove.Axis);
                if (resolvedId >= 0)
                    return resolvedId;
            }

            _logger.Warn(string.Format(
                _localization.GetResourceOrDefault("Rel_Log_AxisIdUnresolved", "SubMove [{0}] 无法解析轴ID，AxisId={1}, Axis={2}"),
                subMove.SubSeq, subMove.AxisId, subMove.Axis));
            return subMove.AxisId;
        }

        private double ResolveVariableOffset(string variableName, List<GlobalVariable> globalVars)
        {
            var gv = globalVars.FirstOrDefault(v => v.Name == variableName);
            if (gv == null)
            {
                _logger.Warn(string.Format(
                    _localization.GetResourceOrDefault("Rel_Log_VariableNotFound", "全局变量 '{0}' 未找到，偏移量按 0 处理"),
                    variableName));
                return 0;
            }

            if (double.TryParse(gv.Value, out var result))
                return result;

            _logger.Warn(string.Format(
                _localization.GetResourceOrDefault("Rel_Log_VariableParseFailed", "全局变量 '{0}' 的值 '{1}' 无法解析为数值，偏移量按 0 处理"),
                variableName, gv.Value));
            return 0;
        }

        /// <summary>
        /// 执行 SubMove 的内嵌动作（夹爪释放/延时/真空等）
        /// 支持在运动序列的任意位置灵活插入非运动操作
        /// </summary>
        private async Task ExecuteSubMoveActionAsync(SubMove subMove, ReleaseDetail releaseDetail, int stepSeq, CancellationToken token)
        {
            if (subMove.Action == SubMoveAction.None)
                return;

            token.ThrowIfCancellationRequested();

            switch (subMove.Action)
            {
                case SubMoveAction.Clamp:
                    // 夹爪夹紧：使用行内参数或默认值
                    var clampPos = subMove.ActionParameter > 0 ? subMove.ActionParameter : 0;
                    if (!_gripperService.IsInitialized)
                    {
                        throw new RecoverableException(
                            "夹爪服务未初始化，无法执行夹紧动作",
                            "请先执行系统复位操作");
                    }
                    _logger.Info(string.Format(
                        _localization.GetResourceOrDefault("Rel_Log_ClampStart", "RELEASE 步骤 [{0}] SubMove [{1}] 执行夹紧动作, 目标位置: {2}, 速度: {3}%"),
                        stepSeq, subMove.SubSeq, clampPos, _gripperService.ManualOperationSpeed));
                    await _gripperService.ClampAsync(clampPos, token);
                    _logger.Info(string.Format(
                        _localization.GetResourceOrDefault("Rel_Log_ClampDone", "RELEASE 步骤 [{0}] SubMove [{1}] 夹紧完成"),
                        stepSeq, subMove.SubSeq));
                    break;

                case SubMoveAction.Release:
                    // 夹爪释放：使用行内参数或 ReleaseDetail 默认值
                    var releasePos = subMove.ActionParameter > 0 ? subMove.ActionParameter : releaseDetail.ReleasePosition;
                    if (!_gripperService.IsInitialized)
                    {
                        throw new RecoverableException(
                            "夹爪服务未初始化，无法执行释放动作",
                            "请先执行系统复位操作");
                    }
                    _logger.Info(string.Format(
                        _localization.GetResourceOrDefault("Rel_Log_ReleaseStart", "RELEASE 步骤 [{0}] SubMove [{1}] 执行释放动作, 目标位置: {2}, 速度: {3}%"),
                        stepSeq, subMove.SubSeq, releasePos, _gripperService.ManualOperationSpeed));
                    await _gripperService.ReleaseAsync(releasePos, token);
                    _logger.Info(string.Format(
                        _localization.GetResourceOrDefault("Rel_Log_ReleaseDone", "RELEASE 步骤 [{0}] SubMove [{1}] 释放完成"),
                        stepSeq, subMove.SubSeq));
                    break;

                case SubMoveAction.Hold:
                    // 延时等待：使用行内参数或 ReleaseDetail 默认值
                    var holdMs = subMove.ActionParameter > 0 ? subMove.ActionParameter : releaseDetail.ReleaseDelayTime;
                    _logger.Info(string.Format(
                        _localization.GetResourceOrDefault("Rel_Log_HoldStart", "RELEASE 步骤 [{0}] SubMove [{1}] 执行延时等待: {2}ms"),
                        stepSeq, subMove.SubSeq, holdMs));
                    await Task.Delay((int)holdMs, token);
                    _logger.Info(string.Format(
                        _localization.GetResourceOrDefault("Rel_Log_HoldDone", "RELEASE 步骤 [{0}] SubMove [{1}] 延时完成"),
                        stepSeq, subMove.SubSeq));
                    break;

                case SubMoveAction.VacuumOn:
                    // 开真空：标记真空状态为开启
                    _logger.Info(string.Format(
                        _localization.GetResourceOrDefault("Rel_Log_VacuumOn", "RELEASE 步骤 [{0}] SubMove [{1}] 开真空"),
                        stepSeq, subMove.SubSeq));
                    releaseDetail.IsVacuumOn = true;
                    break;

                case SubMoveAction.VacuumOff:
                    // 关真空：标记真空状态为关闭
                    _logger.Info(string.Format(
                        _localization.GetResourceOrDefault("Rel_Log_VacuumOff", "RELEASE 步骤 [{0}] SubMove [{1}] 关真空"),
                        stepSeq, subMove.SubSeq));
                    releaseDetail.IsVacuumOn = false;
                    break;

                default:
                    _logger.Warn(string.Format(
                        _localization.GetResourceOrDefault("Rel_Log_UnknownAction", "RELEASE 步骤 [{0}] SubMove [{1}] 未知的动作类型: {2}, 跳过执行"),
                        stepSeq, subMove.SubSeq, subMove.Action));
                    break;
            }
        }
    }
}
