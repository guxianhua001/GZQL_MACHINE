using System;
using System.Collections.Generic;
using Core.Services;

namespace Core.Models
{
    /// <summary>
    /// ZMAP标定配置——持久化"ZMAP像素坐标↔机械坐标"标定点、求解出的仿射矩阵、
    /// Z基准偏移量(ZOffset)以及高度图的无效值约定。JSON序列化保存于 Config/ZMap/ 目录，
    /// 风格与 ZScanConfigService 保持一致，便于设备重启/换线后复用已标定结果。
    /// </summary>
    public class ZMapCalibrationConfig
    {
        /// <summary>标定点列表（ZMAP像素坐标↔机械坐标），至少需3个不共线点</summary>
        public List<ZMapCalibrationPoint> CalibrationPoints { get; set; } = new List<ZMapCalibrationPoint>();

        /// <summary>求解得到的仿射标定结果（像素→机械）；未标定时为 null</summary>
        public AffineCalibrationResult Calibration { get; set; }

        /// <summary>
        /// Z基准偏移量：CorrectedZ = RawZ(ZMAP灰度值) + ZOffset。
        /// 用于修正ZMAP采集零点与机械坐标Z=0基准不一致的问题。
        /// </summary>
        public double ZOffset { get; set; }

        /// <summary>ZMAP图像中代表"无效/未测量"的高度值（默认-1，与灰度值近似相等即视为无效点）</summary>
        public double InvalidHeightValue { get; set; } = -1.0;

        /// <summary>最近一次加载的ZMAP高度图文件路径（仅记录，便于回溯，不代表当前一定已加载）</summary>
        public string LastHeightMapFilePath { get; set; } = string.Empty;

        /// <summary>最近一次更新（标定/ZOffset标定）的时间</summary>
        public DateTime LastUpdatedTime { get; set; }

        /// <summary>操作人（可选，便于追溯标定责任）</summary>
        public string Operator { get; set; } = string.Empty;
    }
}
