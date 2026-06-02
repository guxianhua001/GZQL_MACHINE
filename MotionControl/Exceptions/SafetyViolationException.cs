using System;

namespace MotionControl.Exceptions
{
    public class SafetyViolationException : InvalidOperationException
    {
        /// <summary> 触发违规的轴号 </summary>
        public int AxisId { get; }

        /// <summary> 安全违规的具体原因说明 </summary>
        public string Reason { get; }

        public SafetyViolationException(string message, int axisId, string reason)
            : base(message)
        {
            AxisId = axisId;
            Reason = reason;
        }
    }
}
