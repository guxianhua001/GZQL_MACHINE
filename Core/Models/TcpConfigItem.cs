namespace Core.Models
{
    /// <summary>
    /// TCPIP连接配置项，用于持久化到配方池
    /// </summary>
    public class TcpConfigItem
    {
        /// <summary> 连接名称（唯一标识） </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary> 连接模式：Server 或 Client </summary>
        public string Mode { get; set; } = "Client";
        /// <summary> IP地址 </summary>
        public string IP { get; set; } = "127.0.0.1";
        /// <summary> 端口号 </summary>
        public int Port { get; set; } = 8080;
        /// <summary> 超时时间（毫秒） </summary>
        public int Timeout { get; set; } = 5000;
        /// <summary> 编码方式 </summary>
        public string Encoding { get; set; } = "UTF-8";
        /// <summary> 是否启用 </summary>
        public bool IsEnabled { get; set; } = true;
        /// <summary> 描述 </summary>
        public string Description { get; set; } = string.Empty;
    }
}
