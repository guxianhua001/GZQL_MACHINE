using System.Collections.Generic;

namespace Core.Models
{
    /// <summary>
    /// 轨迹段保存文件的数据结构——同时包含轨迹段列表和坐标对齐参数
    /// 保存时序列化此对象，加载时反序列化恢复完整状态
    /// </summary>
    public class SegmentSaveData
    {
        /// <summary>轨迹段列表</summary>
        public List<DispenseSegment> Segments { get; set; } = new List<DispenseSegment>();

        /// <summary>坐标对齐数据（可为 null，兼容旧版本文件）</summary>
        public CoordinateAlignData AlignData { get; set; }

        /// <summary>Step5/Step6 面板操作选项（可为 null，兼容旧版本文件）</summary>
        public CadPointPanelOptions PanelOptions { get; set; }
    }
}
