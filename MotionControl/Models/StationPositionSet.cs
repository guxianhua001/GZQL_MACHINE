using System.Collections.Generic;

namespace MotionControl.Models
{
    public class StationPositionSet
    {
        public string StationId { get; set; }
        public Dictionary<string, double> Positions { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, string> Comments { get; set; } = new Dictionary<string, string>();
    }
}