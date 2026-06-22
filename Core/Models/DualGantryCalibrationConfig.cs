namespace Core.Models
{
    /// <summary>
    /// 双龙门标定机构配置模型——存储工站标识、公共基准轴、双龙门独立轴、
    /// 双相机TCP通讯配置及自动标定参数等
    /// </summary>
    public class DualGantryCalibrationConfig
    {
        /// <summary>工站标识（如 "DispenserStation"）</summary>
        public string StationIdentifier { get; set; } = "DispenserStation";

        /// <summary>公共基准轴名（下层共用 Y 轴名，如 "GantryY"）</summary>
        public string CommonAxisY { get; set; } = "GantryY";

        /// <summary>龙门1独立X轴名（如 "Dx"）</summary>
        public string Gantry1AxisX { get; set; } = "Dx";

        /// <summary>龙门1独立Y轴名（如 "Dy"）</summary>
        public string Gantry1AxisY { get; set; } = "Dy";

        /// <summary>龙门2独立X轴名（如 "X2"）</summary>
        public string Gantry2AxisX { get; set; } = "X2";

        /// <summary>Cam1 TCP连接名（龙门1视觉相机）</summary>
        public string Gantry1TcpConnection { get; set; } = string.Empty;

        /// <summary>Cam2 TCP连接名（龙门2视觉相机）</summary>
        public string Gantry2TcpConnection { get; set; } = string.Empty;

        /// <summary>Cam1 触发拍照命令</summary>
        public string Gantry1TriggerCommand { get; set; } = string.Empty;

        /// <summary>Cam2 触发拍照命令</summary>
        public string Gantry2TriggerCommand { get; set; } = string.Empty;

        /// <summary>是否启用视觉数据接收（false时手动输入）</summary>
        public bool EnableVisionData { get; set; } = true;

        /// <summary>标定点数</summary>
        public int PointCount { get; set; } = 9;

        /// <summary>自动标定每点间延时（毫秒）</summary>
        public int AutoCalibDelayMs { get; set; } = 500;

        /// <summary>上次使用的配置文件名（仅文件名，不含路径）</summary>
        public string LastFileName { get; set; } = string.Empty;
    }
}
