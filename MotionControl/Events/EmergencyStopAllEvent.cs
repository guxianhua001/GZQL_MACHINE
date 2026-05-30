using Prism.Events;

namespace MotionControl.Events
{
    // 全局急停事件（无参数，通知所有轴和任务立即停止）
    public class EmergencyStopAllEvent : PubSubEvent { }
}