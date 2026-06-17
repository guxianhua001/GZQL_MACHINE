﻿
namespace MotionControl.Interfaces
{
    public interface IAxis
    {
        string Name { get; }
        int LogicalId { get; }
        double ActualPosition { get; }
        double CommandPosition { get; }
        bool IsMoving { get; }
        bool IsAlarmed { get; }
        bool IsEnabled { get; }
        bool IsHomeOk { get; }
        int AxisStatusWord { get; }
    }
}