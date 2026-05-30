using Prism.Events;

namespace MotionControl.Events
{
    public class SystemResetResultEvent : PubSubEvent<bool> { }
    // true 表示全部回零成功，false 表示至少有一个失败
}
