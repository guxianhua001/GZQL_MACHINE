using Core.Abstraction;
using Core.Utilities;
using StationTasks.Models;
using StationTasks.Tasks;
using System.Threading;
using System.Threading.Tasks;

namespace StationTasks.Actions
{
    /// <summary>
    /// BRANCH步骤动作：纯逻辑步骤，不执行物理操作
    /// 核心条件评估和跳转逻辑在ProcessStepExecutor.ExecuteSingleStepAsync中处理
    /// 因为条件跳转需要返回下一步索引，而IProcessStepAction接口不支持返回索引
    /// </summary>
    public class BranchStepAction : IProcessStepAction
    {
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localization;

        public StepType SupportedStepType => StepType.BRANCH;

        public BranchStepAction(ILoggerService logger, ILocalizationService localization)
        {
            _logger = logger;
            _localization = localization;
        }

        public Task ExecuteAsync(ProcessStep step, StationTaskBase task, CancellationToken token)
        {
            _logger.Info(string.Format(
                _localization.GetResourceOrDefault("Branch_Log_ConditionHandledByExecutor", "BRANCH 步骤 [{0}] 条件评估由 ProcessStepExecutor 处理"),
                step.Seq));
            return Task.CompletedTask;
        }
    }
}
