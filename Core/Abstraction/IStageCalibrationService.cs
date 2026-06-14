using Core.Models;
using System.Threading.Tasks;

namespace Core.Abstraction
{
    public interface IStageCalibrationService
    {
        // ===== 旧接口（保留兼容） =====
        Task GoToPhotoPositionAsync(double x, double y, double z, double rx, double rz);
        Task<FiducialCaptureResult> CaptureFiducialAsync(int fiducialIndex);
        Task ApplyCorrectionAsync(double dx, double dy, double dAngle);
        Task<CurrentPositionResult> TeachCurrentPositionAsync();
        Task SaveCalibrationDataAsync();
        Task LoadCalibrationDataAsync();
        StageCalibrationData GetCurrentCalibrationData();
        void ApplyCalibrationData(StageCalibrationData data);

        // ===== 新增接口（载台校准重构） =====

        /// <summary>移动载台Rx/Rz轴到拍照基准位</summary>
        Task MoveToReferencePositionAsync(double rx, double rz);

        /// <summary>移动相机(Dx/Dy/Dz轴)到指定拍照位</summary>
        Task MoveCameraToPhotoPositionAsync(double dx, double dy, double dz);

        /// <summary>触发视觉拍照并返回结果（通过TCP通讯）</summary>
        /// <param name="tcpConnectionName">TCP连接名</param>
        /// <param name="triggerCommand">触发命令</param>
        /// <param name="timeoutMs">超时毫秒</param>
        Task<FiducialCaptureResult> TriggerCaptureAsync(string tcpConnectionName, string triggerCommand, int timeoutMs);

        /// <summary>旋转载台Rz轴到基准角度（当前角度+偏差角度）</summary>
        Task RotateToReferenceAngleAsync(double currentRz, double deltaAngle);

        /// <summary>读取当前所有轴位置</summary>
        Task<CurrentPositionResult> ReadCurrentPositionsAsync();
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
        public double Dx { get; set; }
        public double Dy { get; set; }
        public double Dz { get; set; }
    }
}
