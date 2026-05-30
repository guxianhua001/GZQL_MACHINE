using Prism.Events;

namespace MotionControl.Events
{
    public class AxisAlarmEvent : PubSubEvent<AxisAlarmPayload> { }

    public class AxisAlarmPayload
    {
        public int AxisId { get; set; }
        public bool IsAlarm { get; set; }
    }
}