using AlarmModule.Interfaces;
using AlarmModule.Models;
using Core.Models;
using Core.Utilities;
using MotionControl.Exceptions;
using MotionControl.Interfaces;
using Recipe.Interfaces;
using StationTasks.Models;
using StationTasks.Tasks;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StationTasks.Actions
{
    /// <summary>
    /// SEEK步骤动作：遍历通道行读取力值→判定范围→写入全局变量→超限报警
    /// </summary>
    public class SeekStepAction : IProcessStepAction
    {
        private readonly IMotionService _motionService;
        private readonly IRecipePoolService _recipePoolService;
        private readonly IAlarmService _alarmService;
        private readonly ILoggerService _logger;

        public StepType SupportedStepType => StepType.SEEK;

        public SeekStepAction(
            IMotionService motionService,
            IRecipePoolService recipePoolService,
            IAlarmService alarmService,
            ILoggerService logger)
        {
            _motionService = motionService;
            _recipePoolService = recipePoolService;
            _alarmService = alarmService;
            _logger = logger;
        }

        /// <summary>
        /// 执行SEEK步骤：逐通道读取模拟量力值，判定范围并写入全局变量，超限时发布报警
        /// </summary>
        public async Task ExecuteAsync(ProcessStep step, StationTaskBase task, CancellationToken token)
        {
            if (step.SeekDetail?.ChannelRows == null || step.SeekDetail.ChannelRows.Count == 0)
            {
                _logger.Warn($"SEEK 步骤 [{step.Seq}] 无通道配置，跳过");
                return;
            }

            var poolId = _recipePoolService.CurrentPoolId;
            var variables = string.IsNullOrEmpty(poolId)
                ? new System.Collections.Generic.List<GlobalVariable>()
                : await _recipePoolService.LoadGlobalVariablesAsync(poolId);

            bool hasOutOfRange = false;
            var outOfRangeDetails = new List<string>();

            foreach (var row in step.SeekDetail.ChannelRows)
            {
                token.ThrowIfCancellationRequested();

                // 通道号范围校验（dmc_get_ad_input: channel 取值 0~7）
                if (row.LinkedChannel < 0 || row.LinkedChannel > 7)
                {
                    _logger.Warn($"SEEK 步骤 [{step.Seq}] 通道 {row.LinkedChannel} 超出有效范围(0-7)，跳过");
                    row.CurrentForce = 0;
                    row.IsForceInRange = false;
                    continue;
                }

                // 读取AD模拟量，失败时仅记录日志不弹窗
                double force;
                try
                {
                    force = await _motionService.ReadAnalogChannelAsync(0, row.LinkedChannel);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"SEEK 步骤 [{step.Seq}] 通道 {row.LinkedChannel} 读取失败: {ex.Message}，跳过");
                    row.CurrentForce = 0;
                    row.IsForceInRange = false;
                    continue;
                }

                _logger.Info($"SEEK 步骤 [{step.Seq}] 通道 {row.LinkedChannel}: 力值={force:F3}N");

                row.CurrentForce = force;
                row.IsForceInRange = force >= row.ForceMin && force <= row.ForceMax;

                if (!row.IsForceInRange)
                {
                    hasOutOfRange = true;
                    string detail = $"通道{row.LinkedChannel}: {force:F3}N (范围[{row.ForceMin}, {row.ForceMax}])";
                    outOfRangeDetails.Add(detail);
                    _logger.Warn($"SEEK 步骤 [{step.Seq}] 通道 {row.LinkedChannel} 力值超限: {force:F3}N, 范围=[{row.ForceMin}, {row.ForceMax}]");
                }

                if (!string.IsNullOrEmpty(row.LinkedVariableName))
                {
                    var targetVar = variables.FirstOrDefault(v => v.Name == row.LinkedVariableName);
                    if (targetVar != null)
                    {
                        targetVar.Value = force.ToString("F3");
                        _logger.Info($"SEEK 步骤 [{step.Seq}] 全局变量 [{row.LinkedVariableName}] = {force:F3}");
                    }
                }
            }

            // 批量保存全局变量
            if (!string.IsNullOrEmpty(poolId) && variables.Count > 0)
            {
                await _recipePoolService.SaveGlobalVariablesAsync(poolId, variables);
                _logger.Info($"SEEK 步骤 [{step.Seq}] 全局变量已保存");
            }

            // 力值超限时发布步骤故障事件，并根据 AlarmConfig 决定是否触发报警
            if (hasOutOfRange)
            {
                string stepLabel = $"[{step.Seq}] {step.Step}";
                string errorDetail = string.Join("; ", outOfRangeDetails);
                string errorMessage = $"SEEK 步骤 [{step.Seq}] 力值超限: {errorDetail}";

                if (step.AlarmConfig?.IsEnabled == true)
                {
                    step.ErrorMessage = errorMessage;
                    task.Ea.GetEvent<MotionControl.Events.StepFaultedEvent>().Publish(stepLabel);
                    task.Ea.GetEvent<MotionControl.Events.StepErrorEvent>().Publish(new MotionControl.Events.StepErrorPayload
                    {
                        StepName = stepLabel,
                        ErrorMessage = errorMessage,
                        ErrorCode = "SEEK_FORCE_OUT_OF_RANGE"
                    });

                    var alarmLevel = step.AlarmConfig.AlarmLevel switch
                    {
                        1 => AlarmLevel.Emergency,
                        2 => AlarmLevel.Serious,
                        3 => AlarmLevel.General,
                        _ => AlarmLevel.General
                    };
                    string alarmCode = string.IsNullOrEmpty(step.AlarmConfig.AlarmCode)
                        ? "SEEK_FORCE_OUT_OF_RANGE"
                        : step.AlarmConfig.AlarmCode;
                    await _alarmService.TriggerAlarmAsync(
                        alarmCode,
                        alarmLevel,
                        $"SEEK步骤 [{step.Seq}] 力值超限",
                        source: $"{task.TaskName}.Step{step.Seq}",
                        type: AlarmType.ParameterOutOfLimit);
                }
            }
        }
    }
}
