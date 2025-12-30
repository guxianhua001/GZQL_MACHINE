using System;

namespace Core.Models
{
    // 标定状态枚举
    public enum CalibrationState
    {
        Idle,
        MovingToPoint,
        WaitingForConfirmation,
        RecordingPoint,
        Completed,
        Error
    }

    // 标定配置
    public class CalibrationConfig
    {
        public bool Is9PointCalibration { get; set; } = true;
        public bool IsSideCamera { get; set; } = true;
        public double StartX { get; set; } = 100;
        public double StartY { get; set; } = 100;
        public double Spacing { get; set; } = 50;
        public double RotationRadius { get; set; } = 75; // 新增旋转半径
    }
}
