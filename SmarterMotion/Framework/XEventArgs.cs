using Interfaces;
using System;

namespace SmarterMotion
{
    public class XEventArgs : EventArgs
    {
        public DateTime DateTime { get; set; }
        public int StationId { get; set; }
        public int AlarmLevel { get; set; }
        public int AlarmId { get; set; }
        public int IntValue { get; set; }
        public string StringValue { get; set; }
        public bool BoolValue { get; set; }
        public double DoubleValue { get; set; }
        public XAlarmEventArgs AlarmEventArgs { get; set; }
    }
}
