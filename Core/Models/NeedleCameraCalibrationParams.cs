using System;

namespace Core.Models
{
    public class NeedleCameraCalibrationParams
    {
        public int SystemNumber { get; set; }
        public double CameraCenterX { get; set; }
        public double CameraCenterY { get; set; }
        public double NeedleTipX { get; set; }
        public double NeedleTipY { get; set; }
        public double NeedleTipZ { get; set; }
        public double CalibrationDeltaX { get; set; }
        public double CalibrationDeltaY { get; set; }
        public double CompensationX { get; set; }
        public double CompensationY { get; set; }
        public string CompensationXExpression { get; set; }
        public string CompensationYExpression { get; set; }
        public string CompXLinkedVar { get; set; }
        public string CompYLinkedVar { get; set; }
        public DateTime LastCalibrated { get; set; }

        /// <summary>深拷贝，用于系统切换时缓存独立参数快照</summary>
        public NeedleCameraCalibrationParams Clone() => new()
        {
            SystemNumber = SystemNumber,
            CameraCenterX = CameraCenterX,
            CameraCenterY = CameraCenterY,
            NeedleTipX = NeedleTipX,
            NeedleTipY = NeedleTipY,
            NeedleTipZ = NeedleTipZ,
            CalibrationDeltaX = CalibrationDeltaX,
            CalibrationDeltaY = CalibrationDeltaY,
            CompensationX = CompensationX,
            CompensationY = CompensationY,
            CompensationXExpression = CompensationXExpression,
            CompensationYExpression = CompensationYExpression,
            CompXLinkedVar = CompXLinkedVar,
            CompYLinkedVar = CompYLinkedVar,
            LastCalibrated = LastCalibrated
        };
    }
}
