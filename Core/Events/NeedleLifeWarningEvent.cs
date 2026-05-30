using Prism.Events;

namespace Core.Events
{
    public class NeedleLifeWarningEvent : PubSubEvent<NeedleLifeWarningEventArgs>
    {
    }

    public class NeedleLifeWarningEventArgs
    {
        public int NeedleId { get; set; }
        public int UsageCount { get; set; }
        public int MaxCount { get; set; }
        public double UsageRatio { get; set; }
    }
}
