using Core.Abstraction;
using Core.Utilities;
using MotionControl.Exceptions;
using StationTasks.Models;
using StationTasks.Services;
using StationTasks.Tasks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StationTasks.Actions
{
    /// <summary>
    /// 发送信号步骤动作（SIGNAL_SEND）。
    /// 用于 Task 间信号交互：置位指定名称的信号，唤醒正在等待该信号的 Task。
    /// 信号语义为一次性消费：发送后保持置位，被等待方消费后自动复位。
    /// 发送操作本身不阻塞，立即返回。
    /// </summary>
    public class SignalSendStepAction : IProcessStepAction
    {
        private readonly ILoggerService _logger;
        private readonly ITaskSignalService _signalService;
        private readonly ILocalizationService _localization;

        /// <summary> 该 Action 支持的步骤类型：SIGNAL_SEND </summary>
        public StepType SupportedStepType => StepType.SIGNAL_SEND;

        /// <summary>
        /// 构造函数：注入日志服务和任务信号服务
        /// </summary>
        /// <param name="logger">日志服务</param>
        /// <param name="signalService">任务信号交互服务</param>
        /// <param name="localization">本地化服务</param>
        public SignalSendStepAction(ILoggerService logger, ITaskSignalService signalService, ILocalizationService localization)
        {
            _logger = logger;
            _signalService = signalService;
            _localization = localization;
        }

        /// <summary>
        /// 执行发送信号步骤：
        /// 1. 读取 SignalDetail 配置，获取信号名称
        /// 2. 调用 ITaskSignalService.SendSignal 置位信号
        /// 3. 记录日志，立即返回（不阻塞）
        /// </summary>
        public Task ExecuteAsync(ProcessStep step, StationTaskBase task, CancellationToken token)
        {
            var detail = step.SignalDetail;
            if (detail == null || string.IsNullOrEmpty(detail.SignalName))
            {
                _logger.Warn(string.Format(
                    _localization.GetResourceOrDefault("Sig_Log_Send_NoSignalName", "[SignalSend] 步骤 [{0}] 未配置信号名称，跳过发送"),
                    step.Seq));
                return Task.CompletedTask;
            }

            // 取消令牌检查：急停/停止时快速响应
            token.ThrowIfCancellationRequested();

            _signalService.SendSignal(detail.SignalName);
            _logger.Info(string.Format(
                _localization.GetResourceOrDefault("Sig_Log_Send_SignalSent", "[SignalSend] 步骤 [{0}] 已发送信号: {1}"),
                step.Seq, detail.SignalName));

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 等待信号步骤动作（SIGNAL_WAIT）。
    /// 用于 Task 间信号交互：阻塞等待指定名称的信号被置位，收到后自动复位（消费）。
    /// 支持无限等待或超时等待，超时触发可恢复异常，便于操作员介入处理。
    /// 取消令牌（急停/停止）触发时立即响应并抛出 OperationCanceledException。
    /// </summary>
    public class SignalWaitStepAction : IProcessStepAction
    {
        private readonly ILoggerService _logger;
        private readonly ITaskSignalService _signalService;
        private readonly ILocalizationService _localization;

        /// <summary> 该 Action 支持的步骤类型：SIGNAL_WAIT </summary>
        public StepType SupportedStepType => StepType.SIGNAL_WAIT;

        /// <summary>
        /// 构造函数：注入日志服务和任务信号服务
        /// </summary>
        /// <param name="logger">日志服务</param>
        /// <param name="signalService">任务信号交互服务</param>
        /// <param name="localization">本地化服务</param>
        public SignalWaitStepAction(ILoggerService logger, ITaskSignalService signalService, ILocalizationService localization)
        {
            _logger = logger;
            _signalService = signalService;
            _localization = localization;
        }

        /// <summary>
        /// 执行等待信号步骤：
        /// 1. 读取 SignalDetail 配置，获取信号名称和超时时间
        /// 2. 调用 ITaskSignalService.WaitForSignalAsync 异步等待信号
        /// 3. 收到信号后自动复位（消费），继续执行下一步
        /// 4. 超时未收到信号时抛出可恢复异常，提示操作员检查
        /// 5. 取消令牌触发时立即抛出 OperationCanceledException
        /// </summary>
        public async Task ExecuteAsync(ProcessStep step, StationTaskBase task, CancellationToken token)
        {
            var detail = step.SignalDetail;
            if (detail == null || string.IsNullOrEmpty(detail.SignalName))
            {
                _logger.Warn(string.Format(
                    _localization.GetResourceOrDefault("Sig_Log_Wait_NoSignalName", "[SignalWait] 步骤 [{0}] 未配置信号名称，跳过等待"),
                    step.Seq));
                return;
            }

            int timeoutMs = detail.TimeoutMs;
            string signalName = detail.SignalName;

            // 超时显示文本：<=0 表示无限等待
            string timeoutDisplay = timeoutMs <= 0
                ? _localization.GetResourceOrDefault("Sig_Log_Wait_Infinite", "无限")
                : $"{timeoutMs}ms";
            _logger.Info(string.Format(
                _localization.GetResourceOrDefault("Sig_Log_Wait_StartWait", "[SignalWait] 步骤 [{0}] 开始等待信号: {1}, 超时: {2}"),
                step.Seq, signalName, timeoutDisplay));

            // 执行等待：支持取消令牌即时响应急停/停止
            bool received = await _signalService.WaitForSignalAsync(signalName, timeoutMs, token).ConfigureAwait(false);

            if (received)
            {
                _logger.Info(string.Format(
                    _localization.GetResourceOrDefault("Sig_Log_Wait_SignalReceived", "[SignalWait] 步骤 [{0}] 收到信号: {1}，已自动复位（消费）"),
                    step.Seq, signalName));
            }
            else
            {
                // 超时未收到信号：抛出可恢复异常，触发报警并暂停等待处理
                string errorMsg = $"等待信号 [{signalName}] 超时 ({timeoutMs}ms)，未收到发送方信号";
                _logger.Error(string.Format(
                    _localization.GetResourceOrDefault("Sig_Log_Wait_TimeoutError", "[SignalWait] 步骤 [{0}] {1}"),
                    step.Seq, errorMsg));
                throw new RecoverableException(
                    message: errorMsg,
                    suggestedAction: $"请检查发送信号 [{signalName}] 的任务是否正常运行，或延长超时时间后重试。"
                );
            }
        }
    }
}
