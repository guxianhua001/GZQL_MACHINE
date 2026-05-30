using Core.Attributes;
using System;

namespace Core.Logging
{
    [GenerateLogMessages]
    public static class LogMessageDefinitions
    {
        #region 运动控制相关 (Motion Control)

        [LogMessage("zh-CN", "轴 {0} 已移动到目标位置 ({1:F3}, {2:F3})")]
        [LogMessage("en-US", "Axis {0} moved to target position ({1:F3}, {2:F3})")]
        public const string AxisMovedToTarget = "AxisMovedToTarget";

        [LogMessage("zh-CN", "轴 {0} 回零完成")]
        [LogMessage("en-US", "Axis {0} homing completed")]
        public const string AxisHomingDone = "AxisHomingDone";

        [LogMessage("zh-CN", "收到急停信号，正在停止轴 {0}")]
        [LogMessage("en-US", "Emergency stop received, stopping axis {0}")]
        public const string EmergencyStopTriggered = "EmergencyStopTriggered";

        #endregion

        #region IO 控制相关 (I/O Control)

        [LogMessage("zh-CN", "DI [{0}] 状态: {1}")]
        [LogMessage("en-US", "DI [{0}] status: {1}")]
        public const string DiStatusChanged = "DiStatusChanged";

        [LogMessage("zh-CN", "DO [{0}] 已切换为 {1}")]
        [LogMessage("en-US", "DO [{0}] toggled to {1}")]
        public const string DoToggled = "DoToggled";

        [LogMessage("zh-CN", "DO [{0}] 切换失败: {1}")]
        [LogMessage("en-US", "DO [{0}] toggle failed: {1}")]
        public const string DoToggleFailed = "DoToggleFailed";

        #endregion

        #region 流程控制相关 (Process Control)

        [LogMessage("zh-CN", "流程开始: {0}")]
        [LogMessage("en-US", "Process started: {0}")]
        public const string ProcessStarted = "ProcessStarted";

        [LogMessage("zh-CN", "步骤 [{0}] {1} 完成")]
        [LogMessage("en-US", "Step [{0}] {1} completed")]
        public const string StepCompleted = "StepCompleted";

        [LogMessage("zh-CN", "流程 {0} 执行完毕，耗时 {1}")]
        [LogMessage("en-US", "Process {0} completed, duration {1}")]
        public const string ProcessCompleted = "ProcessCompleted";

        #endregion

        #region 视觉检测相关 (Vision)

        [LogMessage("zh-CN", "触发相机 {0} 拍照")]
        [LogMessage("en-US", "Trigger camera {0} capture")]
        public const string CameraTriggered = "CameraTriggered";

        [LogMessage("zh-CN", "收到相机 {0} 数据: {1} 个测量值")]
        [LogMessage("en-US", "Received camera {0} data: {1} measurements")]
        public const string VisionDataReceived = "VisionDataReceived";

        #endregion

        #region 报警系统相关 (Alarm)

        [LogMessage("zh-CN", "报警触发: [{0}] {1}")]
        [LogMessage("en-US", "Alarm triggered: [{0}] {1}")]
        public const string AlarmTriggered = "AlarmTriggered";

        [LogMessage("zh-CN", "报警已确认: {0}")]
        [LogMessage("en-US", "Alarm acknowledged: {0}")]
        public const string AlarmAcknowledged = "AlarmAcknowledged";

        #endregion
    }
}
