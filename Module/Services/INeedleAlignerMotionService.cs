using Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Module.Services
{
    /// <summary>
    /// 针头对针运动服务：基于 IMotionService 执行寻针流程（非位置编辑器专用接口）
    /// </summary>
    public interface INeedleAlignerMotionService
    {
        /// <summary>读取当前 Dx/Dy/针尖Z 位置（系统1=Dz₂，系统2=Dz₃）</summary>
        IReadOnlyDictionary<string, double> ReadCurrentPositions(int systemNumber);

        /// <summary>安全移动到对针位置：先抬安全高度 → XY → 下降到对针高度</summary>
        Task MoveToAlignPositionAsync(NeedleCalibrationParams parameters, int systemNumber, CancellationToken token);

        /// <summary>抬升到参数中的安全高度</summary>
        Task MoveToSafeHeightAsync(NeedleCalibrationParams parameters, int systemNumber, CancellationToken token);

        /// <summary>安全移动到搜索点 XY（保持当前 Z 或在安全高度）</summary>
        Task MoveToSearchPointXYAsync(NeedleCalibrationParams parameters, int systemNumber, double x, double y, CancellationToken token);

        /// <summary>下降到寻针高度（对针位置 Z）</summary>
        Task MoveToSearchNeedleHeightAsync(NeedleCalibrationParams parameters, int systemNumber, CancellationToken token);

        /// <summary>执行完整四点寻针校准（参考 ExecuteNeedleCalibrationAsync）</summary>
        Task<NeedleCalibrationResult> ExecuteNeedleCalibrationAsync(
            NeedleCalibrationParams parameters,
            int systemNumber,
            IProgress<(string Status, double Progress)> progress,
            CancellationToken token);

        /// <summary>停止当前系统相关轴运动</summary>
        void StopMotion(int systemNumber);
    }

    /// <summary>寻针校准结果</summary>
    public class NeedleCalibrationResult
    {
        public bool Success { get; set; }
        public PointF MeasuredCenter { get; set; }
        public double MeasuredHeight { get; set; }
        public PointF Compensation { get; set; }
        public string ErrorMessage { get; set; }
    }
}
