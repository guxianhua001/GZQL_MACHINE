using System;

namespace MotionControl.Exceptions
{
    /// <summary>
    /// 运动暂停异常：当轴运动因暂停信号中断时抛出
    /// 继承 RecoverableException，确保 RunStep 的 while(true) 循环捕获后进入暂停等待
    /// 恢复后重试当前步骤（重新发起运动到目标位置）
    /// </summary>
    public class MotionPausedException : RecoverableException
    {
        /// <summary> 被暂停中断运动的轴ID </summary>
        public int AxisId { get; }

        public MotionPausedException(int axisId, double targetPosition, double actualPosition)
            : base(
                message: $"轴{axisId}运动因暂停中断。目标: {targetPosition:F3}, 实际: {actualPosition:F3}",
                suggestedAction: "已自动暂停，恢复后将重新运动到目标位置。"
            )
        {
            AxisId = axisId;
        }
    }
}