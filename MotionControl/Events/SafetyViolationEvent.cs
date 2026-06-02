using Prism.Events;
using System;

namespace MotionControl.Events
{
    public class SafetyViolationEvent : PubSubEvent<SafetyViolationEvent>
    {
        /// <summary> 触发违规的轴号 </summary>
        public int AxisId { get; set; }

        /// <summary> 触发违规的轴名称 </summary>
        public string AxisName { get; set; } = string.Empty;

        /// <summary> 目标位置（试图到达的位置）</summary>
        public double TargetPosition { get; set; }

        /// <summary> 当前实际位置 </summary>
        public double CurrentPosition { get; set; }

        /// <summary> 违规原因描述 </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary> 事件发生时间戳 </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary> 触发此违规的安全规则名称 </summary>
        public string RuleName { get; set; } = string.Empty;
    }
}
