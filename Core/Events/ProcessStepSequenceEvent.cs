using Prism.Events;

namespace Core.Events
{
    /// <summary>
    /// 工艺步骤序列启动事件，UI 编辑器发布此事件通知工站任务开始执行步骤
    /// </summary>
    public class ProcessStepSequenceEvent : PubSubEvent<ProcessStepSequencePayload> { }

    /// <summary>
    /// 事件载荷，携带目标工站ID和步骤列表
    /// Steps 运行时类型为 ObservableCollection&lt;ProcessStep&gt;，使用 object 避免跨项目类型依赖
    /// </summary>
    public class ProcessStepSequencePayload
    {
        /// <summary> 目标工站标识，为空则广播给所有工站 </summary>
        public string StationId { get; set; }

        /// <summary> 待执行的工艺步骤列表（运行时为 ObservableCollection&lt;ProcessStep&gt;） </summary>
        public object Steps { get; set; }
    }

    /// <summary>
    /// 步骤序列控制动作枚举
    /// </summary>
    public enum SequenceControlAction
    {
        /// <summary> 暂停执行 </summary>
        Pause,
        /// <summary> 恢复执行 </summary>
        Resume,
        /// <summary> 停止执行 </summary>
        Stop
    }

    /// <summary>
    /// 工艺步骤序列控制事件，UI 编辑器通过此事件控制工站任务的暂停/恢复/停止
    /// </summary>
    public class ProcessStepSequenceControlEvent : PubSubEvent<ProcessStepSequenceControlPayload> { }

    /// <summary>
    /// 控制事件载荷
    /// </summary>
    public class ProcessStepSequenceControlPayload
    {
        /// <summary> 控制动作类型 </summary>
        public SequenceControlAction Action { get; set; }

        /// <summary> 目标工站标识，为空则广播给所有工站 </summary>
        public string StationId { get; set; }
    }
}
