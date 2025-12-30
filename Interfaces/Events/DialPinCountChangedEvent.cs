using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces.Events
{
    public class DialPinCountChangedEvent : PubSubEvent<DialPinCountChangedEventArgs>
    {
    }

    public class DialPinCountChangedEventArgs
    {
        public int TaskNumber { get; set; } // 3-6
        public int NewCount { get; set; }
    }
}
