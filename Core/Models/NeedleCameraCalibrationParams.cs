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
        public double CompensationZ { get; set; }
        public string CompensationXExpression { get; set; }
        public string CompensationYExpression { get; set; }
        public string CompensationZExpression { get; set; }
        public string DeltaXLinkedVar { get; set; }
        public string DeltaYLinkedVar { get; set; }
        public DateTime LastCalibrated { get; set; }
    }
}
