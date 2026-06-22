using Core.Utilities;
using StationTasks.Models;
using StationTasks.Tasks;
using System.Threading;
using System.Threading.Tasks;

namespace StationTasks.Actions
{
    /// <summary>
    /// IF 步骤动作：纯逻辑步骤，不执行物理操作。
    /// 核心条件评估和分支递归执行逻辑在 ProcessStepExecutor.ExecuteIfStepAsync 中处理，
    /// 因为 IF 需要返回下一步索引并递归执行子步骤集合，而 IProcessStepAction 接口不支持返回索引。
    /// 此处仅作为占位 Action 注册到 _actionMap，保证 ExecuteSingleStepAsync 能识别 IF 类型。
    /// </summary>
    public class IfStepAction : IProcessStepAction
    {
        private readonly ILoggerService _logger;

        /// <summary> 该 Action 支持的步骤类型：IF </summary>
        public StepType SupportedStepType => StepType.IF;

        public IfStepAction(ILoggerService logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 执行 IF 步骤（占位实现）。
        /// 实际条件评估和分支执行由 ProcessStepExecutor.ExecuteIfStepAsync 处理。
        /// </summary>
        public Task ExecuteAsync(ProcessStep step, StationTaskBase task, CancellationToken token)
        {
            _logger.Info($"IF 步骤 [{step.Seq}] 条件评估由 ProcessStepExecutor 处理");
            return Task.CompletedTask;
        }
    }
}
