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
    /// CURE 步骤动作：执行固化运动序列 → UV灯控制 → 延时等待 → 关闭UV灯
    /// </summary>
    public class CureStepAction : IProcessStepAction
    {
        private readonly IRecipePoolService _recipePoolService;
        private readonly ILoggerService _logger;
        private readonly IStationRegistry _stationRegistry;
        private readonly IMotionService _motionService;

        public StepType SupportedStepType => StepType.CURE;

        public CureStepAction(
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
        /// 执行 CURE 步骤：运动到固化位 → UV灯打开 → 延时 → UV灯关闭
        /// </summary>
        public async Task ExecuteAsync(ProcessStep step, StationTaskBase task, CancellationToken token)
        {
            var cureDetail = step.CureDetail;
            if (cureDetail == null)
            {
                _logger.Warn($"CURE 步骤 [{step.Seq}] 没有 CureDetail，跳过执行");
                return;
            }

            List<GlobalVariable> globalVars = null;
            try
            {
                var poolId = _recipePoolService.CurrentPoolId;
                if (!string.IsNullOrEmpty(poolId))
                    globalVars = await _recipePoolService.LoadGlobalVariablesAsync(poolId);
            }
            catch (Exception ex)
            {
                _logger.Warn($"加载全局变量失败: {ex.Message}，偏移变量名将无法解析");
            }

            // 执行固化运动序列（包含内嵌的UV灯控制等动作）
            if (cureDetail.CureMoves != null && cureDetail.CureMoves.Count > 0)
            {
                foreach (var subMove in cureDetail.CureMoves)
                {
                    token.ThrowIfCancellationRequested();

                    // 先执行子步骤动作（UV灯/延时等），再执行运动
                    await ExecuteSubMoveActionAsync(subMove, cureDetail, step.Seq, token);

                    // 仅当有轴配置时才执行运动（纯动作行不需要运动）
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
                        _logger.Info($"CURE SubMove [{subMove.SubSeq}]: 工站={targetTask.StationIdentifierValue}, 轴{axisId}({axisName}) → '{subMove.PositionName}'={posValue:F3}, 偏移{totalOffset:F3}, 目标位置={targetPos:F3}, 速度{speed}");

                        TaskState? overrideState = targetTask != task ? task.State : null;
                        string moveLabel = $"[{step.Seq}] {axisName} → {subMove.PositionName} ({posValue:F3})+{totalOffset:F3}={targetPos:F3}";
                        targetTask.PublishStepStatus(moveLabel, overrideState);

                        await targetTask.ExecuteMoveAsync(axisId, subMove.PositionName, speed, totalOffset);
                        await Task.Delay(1800);
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
        /// 执行 SubMove 的内嵌动作（UV灯开关、延时等待等）
        /// 支持在运动序列的任意位置灵活插入非运动操作
        /// </summary>
        private async Task ExecuteSubMoveActionAsync(SubMove subMove, CureDetail cureDetail, int stepSeq, CancellationToken token)
        {
            if (subMove.Action == SubMoveAction.None)
                return;

            token.ThrowIfCancellationRequested();

            switch (subMove.Action)
            {
                case SubMoveAction.UvOn:
                    // 打开UV灯：根据固化头选择对应的DO端口
                    int uvDoPort = cureDetail.UvHeadIndex == 1 ? cureDetail.UvHead1DoPort : cureDetail.UvHead2DoPort;
                    _logger.Info($"CURE 步骤 [{stepSeq}] SubMove [{subMove.SubSeq}] 打开UV灯，DO端口: {uvDoPort}");
                    _motionService.WriteDo(uvDoPort, true);
                    break;

                case SubMoveAction.UvOff:
                    // 关闭UV灯
                    int uvDoPortOff = cureDetail.UvHeadIndex == 1 ? cureDetail.UvHead1DoPort : cureDetail.UvHead2DoPort;
                    _logger.Info($"CURE 步骤 [{stepSeq}] SubMove [{subMove.SubSeq}] 关闭UV灯，DO端口: {uvDoPortOff}");
                    _motionService.WriteDo(uvDoPortOff, false);
                    break;

                case SubMoveAction.Hold:
                case SubMoveAction.UvDelay:
                    // 延时等待：优先使用行内参数，若无则使用 CureDetail.CureTimeMs（固化时间）
                    int holdMs = subMove.ActionParameter > 0 ? (int)subMove.ActionParameter : cureDetail.CureTimeMs;
                    _logger.Info($"CURE 步骤 [{stepSeq}] SubMove [{subMove.SubSeq}] 固化延时: {holdMs}ms");
                    await Task.Delay(holdMs, token);
                    _logger.Info($"CURE 步骤 [{stepSeq}] SubMove [{subMove.SubSeq}] 固化延时完成");
                    break;

                default:
                    _logger.Warn($"CURE 步骤 [{stepSeq}] SubMove [{subMove.SubSeq}] 未知的动作类型: {subMove.Action}, 跳过执行");
                    break;
            }
        }
    }
}
