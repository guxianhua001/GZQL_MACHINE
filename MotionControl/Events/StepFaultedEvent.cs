using Prism.Events;

namespace MotionControl.Events
{
    /// <summary>
    /// 步骤触发报警事件：RunStep 捕获 RecoverableException 时立即发布
    /// 参数为步骤名（stepLabel），订阅者（ProcessStepExecutor）据此标记对应步骤行背景色为红色
    /// </summary>
    public class StepFaultedEvent : PubSubEvent<string> { }
}
