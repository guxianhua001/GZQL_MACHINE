using System.Collections.Generic;

namespace Core.Models
{
    /// <summary>
    /// 载台校准拍照位数据——存储单个拍照位的机械坐标
    /// </summary>
    public class StagePhotoPosition
    {
        /// <summary>拍照位名称</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>相机Dx坐标</summary>
        public double Dx { get; set; }

        /// <summary>相机Dy坐标</summary>
        public double Dy { get; set; }

        /// <summary>相机Dz坐标</summary>
        public double Dz { get; set; }
    }

    /// <summary>
    /// 载台校准基准位数据——存储Rx/Rz轴的拍照基准位
    /// </summary>
    public class StageReferencePosition
    {
        /// <summary>Rx轴基准角度</summary>
        public double Rx { get; set; }

        /// <summary>Rz轴基准角度</summary>
        public double Rz { get; set; }
    }

    /// <summary>
    /// 载台校准完整配置数据——用于序列化/反序列化
    /// 包含基准位、两个拍照位、全局变量链接、超时配置等
    /// </summary>
    public class StageCalibrationConfig
    {
        /// <summary>基准位（Rx/Rz轴移动目标）</summary>
        public StageReferencePosition ReferencePosition { get; set; } = new StageReferencePosition();

        /// <summary>拍照位1（相机移动到载台第一个特征点）</summary>
        public StagePhotoPosition PhotoPosition1 { get; set; } = new StagePhotoPosition { Name = "Photo1" };

        /// <summary>拍照位2（相机移动到载台第二个特征点）</summary>
        public StagePhotoPosition PhotoPosition2 { get; set; } = new StagePhotoPosition { Name = "Photo2" };

        /// <summary>拍照超时时间（毫秒）</summary>
        public int CaptureTimeoutMs { get; set; } = 5000;

        /// <summary>TCP连接名称（用于触发视觉拍照）</summary>
        public string TcpConnectionName { get; set; } = string.Empty;

        /// <summary>触发拍照命令</summary>
        public string TriggerCommand { get; set; } = string.Empty;

        /// <summary>偏差X链接的全局变量名</summary>
        public string DeltaXLinkedVar { get; set; } = string.Empty;

        /// <summary>偏差Y链接的全局变量名</summary>
        public string DeltaYLinkedVar { get; set; } = string.Empty;

        /// <summary>偏差角度链接的全局变量名</summary>
        public string DeltaAngleLinkedVar { get; set; } = string.Empty;

        /// <summary>上次使用的配置文件名（仅文件名，不含路径）</summary>
        public string LastFileName { get; set; } = string.Empty;
    }

    // 保留旧模型以兼容现有代码
    public class StageCalibrationFiducialData
    {
        public double PhotoX { get; set; }
        public double PhotoY { get; set; }
        public double PhotoZ { get; set; }
        public double PhotoRx { get; set; }
        public double PhotoRz { get; set; }
        public double RefX { get; set; }
        public double RefY { get; set; }
        public double RefAngle { get; set; }
        public double MeasuredX { get; set; }
        public double MeasuredY { get; set; }
        public double MeasuredAngle { get; set; }
    }

    public class StageCalibrationData
    {
        public StageCalibrationFiducialData Fiducial1 { get; set; } = new StageCalibrationFiducialData();
        public StageCalibrationFiducialData Fiducial2 { get; set; } = new StageCalibrationFiducialData();
    }
}
