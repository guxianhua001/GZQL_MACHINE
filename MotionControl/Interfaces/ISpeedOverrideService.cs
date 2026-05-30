using System;

namespace MotionControl.Interfaces
{
    public interface ISpeedOverrideService
    {
        double SpeedPercent { get; set; }
        event Action<double> SpeedChanged;
    }
}
