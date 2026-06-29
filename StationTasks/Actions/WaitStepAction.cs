using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using StationTasks.Models;
using StationTasks.Tasks;
using System.Threading;
using System.Threading.Tasks;

namespace StationTasks.Actions
{
    /// <summary>
    /// WAIT/DELAY步骤动作：按配置的延时时长等待，支持急停/停止打断
    /// </summary>
    public class WaitStepAction : IProcessStepAction
    {
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localization;

        public StepType SupportedStepType => StepType.WAIT;

        public WaitStepAction(ILoggerService logger, ILocalizationService localization)
        {
            _logger = logger;
            _localization = localization;
        }

        /// <summary>
        /// 执行WAIT步骤：读取延时配置，异步等待指定时长，支持取消打断
        /// </summary>
        public async Task ExecuteAsync(ProcessStep step, StationTaskBase task, CancellationToken token)
        {
            double delayMs = step.WaitDetail?.ActualDelayMs ?? 1000;

            _logger.Info(string.Format(
                _localization.GetResourceOrDefault("Wait_Log_StartDelay", "WAIT 步骤 [{0}] 开始延时: {1} ms"),
                step.Seq, delayMs));

            await Task.Delay((int)delayMs, token);

            _logger.Info(string.Format(
                _localization.GetResourceOrDefault("Wait_Log_DelayCompleted", "WAIT 步骤 [{0}] 延时完成: {1} ms"),
                step.Seq, delayMs));
        }
    }
}
