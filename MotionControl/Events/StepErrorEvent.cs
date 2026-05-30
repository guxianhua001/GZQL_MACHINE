using Prism.Events;

namespace MotionControl.Events
{
    /// <summary>
    /// 步骤错误事件：RunStep 捕获异常时发布，携带步骤名和错误详情
    /// ProcessStepExecutor 订阅后设置对应 ProcessStep 的 ErrorMessage 属性
    /// </summary>
    public class StepErrorEvent : PubSubEvent<StepErrorPayload> { }

    /// <summary>
    /// 步骤错误信息载体
    /// </summary>
    public class StepErrorPayload
    {
        /// <summary> 步骤名（格式: "[Seq] StepType"，如 "[5] SEEK"） </summary>
        public string StepName { get; set; }

        /// <summary> 错误信息（包含异常消息和建议操作） </summary>
        public string ErrorMessage { get; set; }

        /// <summary> 错误代码（如 "RECOVERABLE_FAULT"、"STEP_FATAL_ERROR"） </summary>
        public string ErrorCode { get; set; }
    }
}
