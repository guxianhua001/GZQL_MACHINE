using Core.Models;
using System.Threading.Tasks;

namespace Core.Abstraction
{
    public interface IStageCalibrationService
    {
        Task GoToPhotoPositionAsync(double x, double y, double z, double rx, double rz);
        Task<FiducialCaptureResult> CaptureFiducialAsync(int fiducialIndex);
        Task ApplyCorrectionAsync(double dx, double dy, double dAngle);
        Task<CurrentPositionResult> TeachCurrentPositionAsync();
        Task SaveCalibrationDataAsync();
        Task LoadCalibrationDataAsync();
        StageCalibrationData GetCurrentCalibrationData();
        void ApplyCalibrationData(StageCalibrationData data);
    }

    public class FiducialCaptureResult
    {
        public bool Success { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Angle { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class CurrentPositionResult
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Rx { get; set; }
        public double Rz { get; set; }
    }
}
