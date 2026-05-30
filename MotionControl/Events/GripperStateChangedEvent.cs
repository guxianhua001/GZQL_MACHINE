using MotionControl.Models;
using Prism.Events;

namespace MotionControl.Events
{
    public class GripperStateChangedEvent : PubSubEvent<GripperState> { }
}
