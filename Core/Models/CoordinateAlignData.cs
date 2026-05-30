using System.Text.Json.Serialization;

namespace Core.Models
{
    /// <summary>
    /// 坐标对齐数据——保存/加载时序列化对齐参数
    /// 包含对齐模式、图纸基准点、机械基准点、方向距离等
    /// </summary>
    public class CoordinateAlignData
    {
        /// <summary>对齐模式：FirstPoint / AllPoints / Affine</summary>
        public string AlignMode { get; set; } = "Affine";

        /// <summary>图纸基准点 X</summary>
        public double MapFiducialX { get; set; }

        /// <summary>图纸基准点 Y</summary>
        public double MapFiducialY { get; set; }

        /// <summary>图纸基准点 Z</summary>
        public double MapFiducialZ { get; set; }

        /// <summary>机械基准点 X</summary>
        public double MachineFidX { get; set; }

        /// <summary>机械基准点 Y</summary>
        public double MachineFidY { get; set; }

        /// <summary>机械基准点 Z</summary>
        public double MachineFidZ { get; set; }

        /// <summary>机械基准点 Rx</summary>
        public double MachineFidRx { get; set; }

        /// <summary>机械基准点 Rz</summary>
        public double MachineFidRz { get; set; }

        /// <summary>方向点距离（仿射模式）</summary>
        public double DirectionLength { get; set; } = 100.0;
    }
}
