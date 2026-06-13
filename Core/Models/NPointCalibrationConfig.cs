namespace Core.Models
{
    /// <summary>
    /// N点标定配置模型——存储轴配置、TCP通讯配置、自动标定参数等
    /// </summary>
    public class NPointCalibrationConfig
    {
        /// <summary>启用X轴</summary>
        public bool EnableAxisX { get; set; } = true;

        /// <summary>启用Y轴</summary>
        public bool EnableAxisY { get; set; } = true;

        /// <summary>标定点数</summary>
        public int PointCount { get; set; } = 9;

        /// <summary>是否接收视觉数据（false时手动输入）</summary>
        public bool EnableVisionData { get; set; } = true;

        /// <summary>TCP连接名称</summary>
        public string TcpConnectionName { get; set; } = string.Empty;

        /// <summary>触发视觉拍照命令</summary>
        public string TriggerCommand { get; set; } = string.Empty;

        /// <summary>自动标定每点间延时（毫秒）</summary>
        public int AutoCalibDelayMs { get; set; } = 500;

        /// <summary>上次使用的配置文件名（仅文件名，不含路径）</summary>
        public string LastFileName { get; set; } = string.Empty;
    }
}
