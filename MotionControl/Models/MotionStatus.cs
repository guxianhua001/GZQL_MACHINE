﻿using MotionControl.Interfaces;
using System.Collections.Generic;

namespace MotionControl.Models
{
    public class MotionStatus
    {
        public List<AxisState> AxisStates { get; set; } = new List<AxisState>();
        public List<IoState> IoStates { get; set; } = new List<IoState>();
    }

    public class AxisState : IAxis
    {
        public int AxisId { get; set; }
        public string Name { get; set; }
        public double ActualPosition { get; set; }
        public double CommandPosition { get; set; }
        public bool IsMoving { get; set; }
        public bool IsAlarmed { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsHomeOk { get; set; }
        public int AxisStatusWord { get; set; }

        // IAxis 显式实现
        int IAxis.LogicalId => AxisId;
    }

    public class IoState : IIoPoint
    {
        public int Port { get; set; }
        public string Name { get; set; }
        public bool IsInput { get; set; }
        public bool Value { get; set; }

        int IIoPoint.LogicalId => Port;
    }
}
