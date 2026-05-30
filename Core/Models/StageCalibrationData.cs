using System.Collections.Generic;

namespace Core.Models
{
    public class StageCalibrationFiducialData
    {
        public double PhotoX { get; set; }
        public double PhotoY { get; set; }
        public double PhotoZ { get; set; }
        public double PhotoRx { get; set; }
        public double PhotoRz { get; set; }
        public double RefX { get; set; }
        public double RefY { get; set; }
        public double RefAngle { get; set; }
        public double MeasuredX { get; set; }
        public double MeasuredY { get; set; }
        public double MeasuredAngle { get; set; }
    }

    public class StageCalibrationData
    {
        public StageCalibrationFiducialData Fiducial1 { get; set; } = new StageCalibrationFiducialData();
        public StageCalibrationFiducialData Fiducial2 { get; set; } = new StageCalibrationFiducialData();
    }
}
