using Prism.Events;
using System;

namespace Stations.Event
{
    public class H2HeightUpdatedEvent : PubSubEvent<H2HeightData>
    {

    }
    public class H2HeightData
    {
        public int TabIndex { get; set; }
        public double H2Height { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
