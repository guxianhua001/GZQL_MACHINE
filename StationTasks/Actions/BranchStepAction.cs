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

        public StepType SupportedStepType => StepType.BRANCH;

        public BranchStepAction(ILoggerService logger)
        {
            _logger = logger;
        }

        public Task ExecuteAsync(ProcessStep step, StationTaskBase task, CancellationToken token)
        {
            _logger.Info($"BRANCH 步骤 [{step.Seq}] 条件评估由 ProcessStepExecutor 处理");
            return Task.CompletedTask;
        }
    }
}
