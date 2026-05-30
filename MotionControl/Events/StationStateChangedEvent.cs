using MotionControl.Models;
using Prism.Events;

namespace MotionControl.Events
{
    public class StationStateChangedEvent : PubSubEvent<StationStatePayload> { }

    public class StationStatePayload
    {
        public StationState State { get; set; }
        public string Description { get; set; }
        public bool GreenLight { get; set; }
        public bool RedLight { get; set; }
        public bool OrangeLight { get; set; }
        public bool Buzzer { get; set; }
    }
}
