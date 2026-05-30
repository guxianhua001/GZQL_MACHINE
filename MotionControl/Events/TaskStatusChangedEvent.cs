
using MotionControl.Interfaces;
using Prism.Events;

namespace MotionControl.Events
{
    /// <summary>
    /// 任务状态改变事件
    /// </summary>
    public class TaskStatusChangedEvent : PubSubEvent<TaskStatusPayload> { }
    public class TaskStatusPayload
    {
        public int TaskId { get; set; }
        public string TaskName { get; set; }
        public TaskState State { get; set; }
        public string CurrentStepName { get; set; }
        /// <summary>
        /// 标记当前步骤已完成（用于跨工站步骤执行完毕后通知监控栏）
        /// </summary>
        public bool IsStepCompleted { get; set; }
    }
}