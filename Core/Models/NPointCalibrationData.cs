using Core.Services;
using System.Collections.Generic;

namespace Core.Models
{
    /// <summary>
    /// N点标定完整数据模型——用于序列化/反序列化标定配置和点位数据
    /// </summary>
    public class NPointCalibrationData
    {
        /// <summary>标定配置</summary>
        public NPointCalibrationConfig Config { get; set; } = new NPointCalibrationConfig();

        /// <summary>标定点列表</summary>
        public List<NPointCalibrationPoint> Points { get; set; } = new List<NPointCalibrationPoint>();

        /// <summary>仿射标定结果（6参数 + RMS误差）</summary>
        public AffineCalibrationResult? CalibrationResult { get; set; }
    }
}
