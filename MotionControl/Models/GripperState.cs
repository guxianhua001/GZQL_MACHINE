using System;

namespace MotionControl.Models
{
    public enum GripperStatus
    {
        Unknown = 0,
        Idle = 1,
        Moving = 2,
        Clamping = 3,
        Clamped = 4,
        Releasing = 5,
        Error = 6,
        Homing = 7
    }

    public class GripperState
    {
        public string GripperId { get; set; } = "Gripper1";
        
        public GripperStatus Status { get; set; } = GripperStatus.Unknown;
        
        public double CurrentPosition { get; set; }
        
        public double TargetPosition { get; set; }
        
        public double CurrentTorque { get; set; }
        
        public double TargetTorque { get; set; }
        
        public bool IsAlarmActive { get; set; }
        
        public bool IsAtHome { get; set; }
        
        public DateTime LastUpdateTime { get; set; } = DateTime.Now;
        
        public string ErrorMessage { get; set; } = "";
    }
}
