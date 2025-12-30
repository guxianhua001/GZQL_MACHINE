using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSMS
{
    public class SecsCommandEvent : PubSubEvent<SecsCommandParameter>
    {
        public static SecsCommandEvent Instance => new SecsCommandEvent();
    }
    public enum SecsCommandType { Hold, Release, Stop, Start }

    public struct SecsCommandParameter
    {
        public SecsCommandType CommandType;
        public string LogMessage;
    }

}
