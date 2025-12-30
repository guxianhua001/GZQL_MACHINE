
namespace Core.Models
{
    // 针头校准参数数据模型
    public class NeedleCalibrationParameters
    {
        public double CameraCenterX { get; set; }
        public double CameraCenterY { get; set; }
        public double NeedleTipX { get; set; }
        public double NeedleTipY { get; set; }
        public double BasePlaneZ { get; set; }
        public double NeedleTipZ { get; set; }
        public double CalibrationDeltaX { get; set; }
        public double CalibrationDeltaY { get; set; }
        public double CompensationX { get; set; }
        public double CompensationY { get; set; }
        public double CompensationZ { get; set; }
        public DateTime LastCalibrated { get; set; }
    }
}
