using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using MotionControl.Interfaces;
using StationTasks.Models;
using Recipe.Interfaces;
using StationTasks.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StationTasks.Actions
{
    /// <summary>
    /// GOTO 步骤动作：遍历 SubMoves，逐轴移动到配方位置，支持偏移量
    /// 支持跨工站路由：根据 SubMove.StationId 查找目标工站任务执行移动
    /// </summary>
    public class GotoStepAction : IProcessStepAction
    {
        private readonly IRecipePoolService _recipePoolService;
        private readonly ILoggerService _logger;
        private readonly IStationRegistry _stationRegistry;
        private readonly IPositionProvider _positionProvider;

        public StepType SupportedStepType => StepType.GOTO;

        public GotoStepAction(
            IRecipePoolService recipePoolService,
            ILoggerService logger,
            IStationRegistry stationRegistry,
            IPositionProvider positionProvider)
        {
            _recipePoolService = recipePoolService;
            _logger = logger;
            _stationRegistry = stationRegistry;
            _positionProvider = positionProvider;
        }

        /// <summary>
        /// 执行 GOTO 步骤：遍历 SubMoves，解析轴ID和位置，逐轴移动
        /// 每个 SubMove 可指定不同的 StationId，路由到对应工站任务执行
        /// </summary>
        public async Task ExecuteAsync(ProcessStep step, StationTaskBase task, CancellationToken token)
        {
            if (step.SubMoves == null || step.SubMoves.Count == 0)
            {
                _logger.Warn($"GOTO 步骤 [{step.Seq}] 没有 SubMove，跳过执行");
                return;
            }

            bool isHome = step.GotoMode == StationTasks.Models.GotoModeEnum.Home;

            // 绝对定位前强制刷新位置缓存，避免位置编辑器已保存但 GOTO 仍读到旧快照
            if (!isHome)
            {
                await _positionProvider.RefreshCacheAsync();
            }

            // 配方池键：优先 Name，与持久化层一致
            string poolKey = !string.IsNullOrEmpty(_recipePoolService.CurrentPoolName)
                ? _recipePoolService.CurrentPoolName
                : _recipePoolService.CurrentPoolId;

            foreach (var subMove in step.SubMoves)
            {
                token.ThrowIfCancellationRequested();

                // 每个 SubMove 前刷新全局变量（方法4 IF/ELSE 循环中 VISION 可能已更新偏移量）
                List<GlobalVariable> globalVars = null;
                if (!string.IsNullOrEmpty(poolKey))
                {
                    try
                    {
                        globalVars = await _recipePoolService.LoadGlobalVariablesAsync(poolKey);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"SubMove [{subMove.SubSeq}] 刷新全局变量失败: {ex.Message}");
                    }
                }

                StationTaskBase targetTask = ResolveTargetTask(subMove.StationId, task);
                int axisId = ResolveAxisId(subMove, targetTask);
                string axisName = targetTask.GetAxisNameById(axisId);

                TaskState? overrideState = targetTask != task ? task.State : null;

                if (isHome)
                {
                    // 回零模式：检查轴是否已回零，已回零则跳过
                    bool isHomed = await targetTask.IsAxisHomedAsync(axisId);
                    if (isHomed)
                    {
                        _logger.Info($"HOME SubMove [{subMove.SubSeq}]: 轴 {axisName}({axisId}) 已回零，跳过");
                        continue;
                    }

                    if (subMove.HomeMode == 0)
                    {
                        // HomeMode=0: 使用控制卡已配置的回零参数（HomeAxisAsync）
                        _logger.Info($"HOME SubMove [{subMove.SubSeq}]: 工站={targetTask.StationIdentifierValue}, 轴{axisId}({axisName}), 使用卡内配置回零");
                        string moveLabel = $"[{step.Seq}] {axisName} → Home (card config)";
                        targetTask.PublishStepStatus(moveLabel, overrideState);
                        await targetTask.ExecuteHomeAxisAsync(axisId);
                    }
                    else
                    {
                        // HomeMode!=0: 使用自定义回零模式/速度参数（HomeAsync）
                        _logger.Info($"HOME SubMove [{subMove.SubSeq}]: 工站={targetTask.StationIdentifierValue}, 轴{axisId}({axisName}), 模式={subMove.HomeMode}, 低速={subMove.HomeMinVel}, 高速={subMove.HomeMaxVel}");
                        string moveLabel = $"[{step.Seq}] {axisName} → Home (mode={subMove.HomeMode}, vel={subMove.HomeMinVel}/{subMove.HomeMaxVel})";
                        targetTask.PublishStepStatus(moveLabel, overrideState);
                        await targetTask.ExecuteHomeAsync(axisId, subMove.HomeMode, subMove.HomeMinVel, subMove.HomeMaxVel);
                    }
                    //await Task.Delay(1800);
                    targetTask.CompleteStepStatus(overrideState);
                }
                else
                {
                    double totalOffset = 0;

                    if (subMove.Offset != 0)
                    {
                        totalOffset += subMove.Offset;
                        _logger.Info($"SubMove [{subMove.SubSeq}] 固定偏移 = {subMove.Offset}");
                    }

                    if (!string.IsNullOrEmpty(subMove.OffsetVariableName) && globalVars != null)
                    {
                        double varOffset = ResolveVariableOffset(subMove.OffsetVariableName, globalVars);
                        totalOffset += varOffset;
                        _logger.Info($"SubMove [{subMove.SubSeq}] 偏移变量 '{subMove.OffsetVariableName}' = {varOffset}");
                    }

                    double speed = subMove.Speed > 0 ? subMove.Speed : 10.0;
                    double posValue = await targetTask.GetPositionValueAsync(subMove.PositionName, axisName);
                    double targetPos = posValue + totalOffset;
                    // 记录配方位置值、偏移量和最终目标位置
                    _logger.Info($"GOTO SubMove [{subMove.SubSeq}]: 工站={targetTask.StationIdentifierValue}, 轴{axisId}({axisName}) -> 位置名'{subMove.PositionName}'={posValue:F3}, 偏移{totalOffset:F3}, 目标位置={targetPos:F3}, 速度{speed}");

                    string moveLabel = $"[{step.Seq}] {axisName} → {subMove.PositionName} ({posValue:F3})+{totalOffset:F3}={targetPos:F3}";
                    targetTask.PublishStepStatus(moveLabel, overrideState);

                    await targetTask.ExecuteMoveAsync(axisId, subMove.PositionName, speed, totalOffset);
                    //await Task.Delay(1800);
                    targetTask.CompleteStepStatus(overrideState);
                }
            }
        }

        /// <summary>
        /// 根据 StationId 查找目标工站任务，未指定时使用默认任务
        /// </summary>
        private StationTaskBase ResolveTargetTask(string stationId, StationTaskBase defaultTask)
        {
            if (string.IsNullOrEmpty(stationId))
                return defaultTask;  // 返回默认工站是否合适？

            var station = _stationRegistry.GetAllStations()
                .FirstOrDefault(s => s.StationIdentifier == stationId);
            if (station is StationTaskBase task)
                return task;

            _logger.Warn($"SubMove 指定的工站 '{stationId}' 未找到，使用默认工站 '{defaultTask.TaskName}'");
            return defaultTask;
        }

        /// <summary>
        /// 解析轴ID：优先使用 AxisId，否则通过目标工站任务查找轴名映射
        /// </summary>
        private int ResolveAxisId(SubMove subMove, StationTaskBase targetTask)
        {
            if (subMove.AxisId > 0)
            {
                return subMove.AxisId;
            }

            // 通过轴名称在目标工站任务中查找逻辑轴ID
            if (!string.IsNullOrEmpty(subMove.Axis))
            {
                int resolvedId = targetTask.FindAxisIdByName(subMove.Axis);
                if (resolvedId >= 0)
                {
                    return resolvedId;
                }
            }

            _logger.Warn($"SubMove [{subMove.SubSeq}] 无法解析轴ID，AxisId={subMove.AxisId}, Axis={subMove.Axis}，使用原始值");
            return subMove.AxisId;
        }

        /// <summary>到配方位置，支持偏移量
        /// 从全局变量列表中解析偏移变量值
        /// </summary>
        private double ResolveVariableOffset(string variableName, List<GlobalVariable> globalVars)
        {
            var gv = globalVars.FirstOrDefault(v => v.Name == variableName);
            if (gv == null)
            {
                _logger.Warn($"全局变量 '{variableName}' 未找到，偏移量按 0 处理");
                return 0;
            }

            if (double.TryParse(gv.Value, out var result))
            {
                return result;
            }

            _logger.Warn($"全局变量 '{variableName}' 的值 '{gv.Value}' 无法解析为数值，偏移量按 0 处理");
            return 0;
        }
    }
}