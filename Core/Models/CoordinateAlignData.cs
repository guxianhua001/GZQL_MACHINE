using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Core.Models
{
    /// <summary>
    /// 坐标对齐数据——保存/加载时序列化对齐参数
    /// 包含对齐模式、双针头仿射标定点、逐点映射点、双针头仿射结果等
    /// </summary>
    public class CoordinateAlignData
    {
        /// <summary>对齐模式：Affine / PointMapping</summary>
        public string AlignMode { get; set; } = "Affine";

        /// <summary>针头1仿射标定点集合</summary>
        public List<AffineCalibrationPoint> AffineCalibrationPointsNeedle1 { get; set; } = new();

        /// <summary>针头2仿射标定点集合</summary>
        public List<AffineCalibrationPoint> AffineCalibrationPointsNeedle2 { get; set; } = new();

        /// <summary>仿射标定点集合（旧版兼容，加载时自动迁移到 Needle1）</summary>
        public List<AffineCalibrationPoint> AffineCalibrationPoints { get; set; } = new();

        /// <summary>逐点映射点集合（旧版兼容，加载时自动迁移到 Needle1）</summary>
        public List<PointMappingPoint> PointMappingPoints { get; set; } = new();

        /// <summary>针头1逐点映射点集合</summary>
        public List<PointMappingPoint> PointMappingPointsNeedle1 { get; set; } = new();

        /// <summary>针头2逐点映射点集合</summary>
        public List<PointMappingPoint> PointMappingPointsNeedle2 { get; set; } = new();

        /// <summary>针头1仿射标定结果参数</summary>
        public AffineResultData AffineResultDataNeedle1 { get; set; }

        /// <summary>针头2仿射标定结果参数</summary>
        public AffineResultData AffineResultDataNeedle2 { get; set; }

        /// <summary>仿射标定结果参数（旧版兼容，加载时自动迁移到 Needle1）</summary>
        public AffineResultData AffineResultData { get; set; }

        /// <summary>当前针头索引（0=Dz1, 1=Dz2）</summary>
        public int CurrentNeedleIndex { get; set; }
    }

    /// <summary>
    /// 仿射标定结果序列化数据——用于保存/加载仿射变换6个参数和RMS
    /// </summary>
    public class AffineResultData
    {
        public double A { get; set; }
        public double B { get; set; }
        public double C { get; set; }
        public double D { get; set; }
        public double Tx { get; set; }
        public double Ty { get; set; }
        public double RmsError { get; set; }
        public int PointCount { get; set; }
    }
}
