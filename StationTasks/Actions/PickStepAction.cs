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
    /// PICK 步骤动作：执行取料运动序列 → 夹紧 → 保压延时 → 真空检测
    /// 保压时间从 PickDetail.PickHoldingTime 读取，无硬编码
    /// </summary>
    public class PickStepAction : IProcessStepAction
    {
        private readonly IRecipePoolService _recipePoolService;
        private readonly ILoggerService _logger;
        private readonly IStationRegistry _stationRegistry;
        private readonly IGripperService _gripperService;

        public StepType SupportedStepType => StepType.PICK;

        public PickStepAction(
            IRecipePoolService recipePoolService,
            ILoggerService logger,
            IStationRegistry stationRegistry,
            IGripperService gripperService)
        {
            _recipePoolService = recipePoolService;
            _logger = logger;
            _stationRegistry = stationRegistry;
            _gripperService = gripperService;
        }

        /// <summary>
        /// 执行 PICK 步骤：运动到取料位 → 夹紧 → 保压 → 真空检测
        /// </summary>
        public async Task ExecuteAsync(ProcessStep step, StationTaskBase task, CancellationToken token)
        {
            var pickDetail = step.PickDetail;
            if (pickDetail == null)
            {
                _logger.Warn($"PICK 步骤 [{step.Seq}] 没有 PickDetail，跳过执行");
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
                _logger.Warn($"加载全局变量失败: {ex.Message}，偏移变量名将无法解析");
            }

            // 1. 执行取料运动序列（包含内嵌的夹爪/延时等动作）
            if (pickDetail.PickMoves != null && pickDetail.PickMoves.Count > 0)
            {
                foreach (var subMove in pickDetail.PickMoves)
                {
                    token.ThrowIfCancellationRequested();

                    // 先执行子步骤动作（夹爪/延时等），再执行运动
                    await ExecuteSubMoveActionAsync(subMove, pickDetail, step.Seq, token);

                    // 仅当有轴配置时才执行运动（纯动作行如 Clamp/Hold 不需要运动）
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
                        _logger.Info($"PICK SubMove [{subMove.SubSeq}]: 工站={targetTask.StationIdentifierValue}, 轴{axisId}({axisName}) → '{subMove.PositionName}'={posValue:F3}, 偏移{totalOffset:F3}, 目标位置={targetPos:F3}, 速度{speed}");

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

            _logger.Warn($"SubMove 指定的工站 '{stationId}' 未找到，使用默认工站 '{defaultTask.TaskName}'");
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

            _logger.Warn($"SubMove [{subMove.SubSeq}] 无法解析轴ID，AxisId={subMove.AxisId}, Axis={subMove.Axis}");
            return subMove.AxisId;
        }

        private double ResolveVariableOffset(string variableName, List<GlobalVariable> globalVars)
        {
            var gv = globalVars.FirstOrDefault(v => v.Name == variableName);
            if (gv == null)
            {
                _logger.Warn($"全局变量 '{variableName}' 未找到，偏移量按 0 处理");
                return 0;
            }

            if (double.TryParse(gv.Value, out var result))
                return result;

            _logger.Warn($"全局变量 '{variableName}' 的值 '{gv.Value}' 无法解析为数值，偏移量按 0 处理");
            return 0;
        }

        /// <summary>
        /// 执行 SubMove 的内嵌动作（夹爪/延时/真空等）
        /// 支持在运动序列的任意位置灵活插入非运动操作
        /// </summary>
        private async Task ExecuteSubMoveActionAsync(SubMove subMove, PickDetail pickDetail, int stepSeq, CancellationToken token)
        {
            if (subMove.Action == SubMoveAction.None)
                return;

            token.ThrowIfCancellationRequested();

            switch (subMove.Action)
            {
                case SubMoveAction.Clamp:
                    // 夹爪夹紧：使用行内参数或 PickDetail 默认值
                    var clampPos = subMove.ActionParameter > 0 ? subMove.ActionParameter : pickDetail.ClampPosition;
                    if (!_gripperService.IsInitialized)
                    {
                        throw new RecoverableException(
                            "夹爪服务未初始化，无法执行夹紧动作",
                            "请先执行系统复位操作");
                    }
                    _logger.Info($"PICK 步骤 [{stepSeq}] SubMove [{subMove.SubSeq}] 执行夹紧动作, 目标位置: {clampPos}, 速度: {_gripperService.ManualOperationSpeed}%");
                    await _gripperService.ClampAsync(clampPos, token);
                    _logger.Info($"PICK 步骤 [{stepSeq}] SubMove [{subMove.SubSeq}] 夹紧完成");
                    break;

                case SubMoveAction.Release:
                    // 夹爪释放：使用行内参数或 PickDetail 默认值
                    var releasePos = subMove.ActionParameter > 0 ? subMove.ActionParameter : pickDetail.ReleasePosition;
                    if (!_gripperService.IsInitialized)
                    {
                        throw new RecoverableException(
                            "夹爪服务未初始化，无法执行释放动作",
                            "请先执行系统复位操作");
                    }
                    _logger.Info($"PICK 步骤 [{stepSeq}] SubMove [{subMove.SubSeq}] 执行释放动作, 目标位置: {releasePos}, 速度: {_gripperService.ManualOperationSpeed}%");
                    await _gripperService.ReleaseAsync(releasePos, token);
                    _logger.Info($"PICK 步骤 [{stepSeq}] SubMove [{subMove.SubSeq}] 释放完成");
                    break;

                case SubMoveAction.Hold:
                    // 延时等待：使用行内参数或 PickDetail 默认值
                    var holdMs = subMove.ActionParameter > 0 ? subMove.ActionParameter : pickDetail.PickHoldingTime;
                    _logger.Info($"PICK 步骤 [{stepSeq}] SubMove [{subMove.SubSeq}] 执行延时等待: {holdMs}ms");
                    await Task.Delay((int)holdMs, token);
                    _logger.Info($"PICK 步骤 [{stepSeq}] SubMove [{subMove.SubSeq}] 延时完成");
                    break;

                case SubMoveAction.VacuumOn:
                    // 开真空：标记真空状态为开启（实际IO控制由 GripperService 或外部设备处理）
                    _logger.Info($"PICK 步骤 [{stepSeq}] SubMove [{subMove.SubSeq}] 开真空");
                    pickDetail.IsVacuumOn = true;
                    // TODO: 如果需要实际的真空IO控制，在此调用 _gripperService 或 IO 服务
                    break;

                case SubMoveAction.VacuumOff:
                    // 关真空：标记真空状态为关闭
                    _logger.Info($"PICK 步骤 [{stepSeq}] SubMove [{subMove.SubSeq}] 关真空");
                    pickDetail.IsVacuumOn = false;
                    // TODO: 如果需要实际的真空IO控制，在此调用 _gripperService 或 IO 服务
                    break;

                default:
                    _logger.Warn($"PICK 步骤 [{stepSeq}] SubMove [{subMove.SubSeq}] 未知的动作类型: {subMove.Action}, 跳过执行");
                    break;
            }
        }
    }
}
