using System;
using System.Collections.Generic;

namespace Core.Models
{
    /// <summary>
    /// 段级ZMAP提取配置：与 DispenseSegment 一起保存到 segments JSON。
    /// 标定、ROI和Z基准均只服务于所属轨迹段，避免多个产品/段共用全局配置产生误用。
    /// </summary>
    public class ZMapSegmentProfile
    {
        /// <summary>是否已有用户保存过的段级配置。</summary>
        public bool IsConfigured { get; set; }

        /// <summary>连续轨迹的实际离散点数；与所属段 SamplePointCount 保持一致。</summary>
        public int TrajectoryPointCount { get; set; }

        /// <summary>像素↔机械坐标标定、仿射结果与Z基准偏移。</summary>
        public ZMapCalibrationConfig CalibrationConfig { get; set; } = new ZMapCalibrationConfig();

        /// <summary>图像ROI定义，包含类型和控制点，窗口重开后恢复。</summary>
        public ZMapRoiDefinition RoiDefinition { get; set; } = new ZMapRoiDefinition();

        /// <summary>折线示教方向（Auto/Head/Tail）。</summary>
        public ZMapTeachDirection TeachDirection { get; set; } = ZMapTeachDirection.Auto;

        /// <summary>ROI采样方向是否反向；起始顶点由RoiDefinition.ControlPoints首点表达。</summary>
        public bool ReverseRoiDirection { get; set; }

        /// <summary>最近一次提取预览的有效点数。</summary>
        public int LastExtractValidCount { get; set; }

        /// <summary>最近一次提取预览的总点数。</summary>
        public int LastExtractTotalCount { get; set; }

        /// <summary>最近一次提取预览中的无效点序号（从1开始），供Step6防撞针预检。</summary>
        public List<int> LastInvalidZIndices { get; set; } = new List<int>();

        /// <summary>最近一次提取预览说明摘要（与窗口PreviewSummaryText一致）。</summary>
        public string LastExtractSummary { get; set; } = string.Empty;

        /// <summary>最近一次更新的本地时间，便于工艺追溯。</summary>
        public DateTime LastUpdatedTime { get; set; }

        /// <summary>最近一次提取是否存在无效Z点（说明区已有不合格提示时为true）。</summary>
        public bool HasUnresolvedInvalidZ =>
            LastExtractTotalCount > 0
            && (LastExtractValidCount < LastExtractTotalCount
                || (LastInvalidZIndices != null && LastInvalidZIndices.Count > 0));
    }
}