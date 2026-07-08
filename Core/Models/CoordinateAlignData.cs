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

        /// <summary>针头1 - 针头示教模式仿射标定点（与相机示教模式独立持久化）</summary>
        public List<AffineCalibrationPoint> AffineCalibrationPointsNeedle1_NeedleTeach { get; set; } = new();

        /// <summary>针头1 - 相机示教模式仿射标定点</summary>
        public List<AffineCalibrationPoint> AffineCalibrationPointsNeedle1_CameraTeach { get; set; } = new();

        /// <summary>针头2 - 针头示教模式仿射标定点</summary>
        public List<AffineCalibrationPoint> AffineCalibrationPointsNeedle2_NeedleTeach { get; set; } = new();

        /// <summary>针头2 - 相机示教模式仿射标定点</summary>
        public List<AffineCalibrationPoint> AffineCalibrationPointsNeedle2_CameraTeach { get; set; } = new();

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

        /// <summary>针头1 - 针头示教模式仿射标定结果</summary>
        public AffineResultData AffineResultDataNeedle1_NeedleTeach { get; set; }

        /// <summary>针头1 - 相机示教模式仿射标定结果</summary>
        public AffineResultData AffineResultDataNeedle1_CameraTeach { get; set; }

        /// <summary>针头2 - 针头示教模式仿射标定结果</summary>
        public AffineResultData AffineResultDataNeedle2_NeedleTeach { get; set; }

        /// <summary>针头2 - 相机示教模式仿射标定结果</summary>
        public AffineResultData AffineResultDataNeedle2_CameraTeach { get; set; }

        /// <summary>仿射标定结果参数（旧版兼容，加载时自动迁移到 Needle1）</summary>
        public AffineResultData AffineResultData { get; set; }

        /// <summary>当前针头索引（0=Dz1, 1=Dz2）</summary>
        public int CurrentNeedleIndex { get; set; }

        /// <summary>
        /// N点仿射模式下是否启用「使用相机示教」子模式：
        /// 勾选时移动相机至目标点读取相机机械坐标，再叠加相机-针头固定距离 + NeedleTCP偏差换算针头机械坐标；
        /// 不勾选时沿用移动针头直接示教针头机械坐标。默认 false 保持旧行为。
        /// </summary>
        public bool UseCameraTeachForAffine { get; set; }
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
        /// <summary>各标定点残差最大值 (mm)</summary>
        public double MaxResidual { get; set; }
        public int PointCount { get; set; }
    }
}
