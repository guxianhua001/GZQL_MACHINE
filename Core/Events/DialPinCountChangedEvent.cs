using Prism.Events;

namespace Core.Events
{
    public class DialPinCountChangedEvent : PubSubEvent<DialPinCountChangedEventArgs>
    {
    }

    public class DialPinCountChangedEventArgs
    {
        public int TaskNumber { get; set; }
        public int NewCount { get; set; }
    }
}
