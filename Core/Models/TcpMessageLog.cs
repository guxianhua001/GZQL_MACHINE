using System;

namespace Core.Models
{
    /// <summary>
    /// TCP消息日志记录，用于UI显示收发消息
    /// </summary>
    public class TcpMessageLog
    {
        /// <summary> 时间戳 </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary> 消息方向：Send / Receive </summary>
        public string Direction { get; set; } = "Send";

        /// <summary> 客户端名称 </summary>
        public string ClientName { get; set; } = string.Empty;

        /// <summary> 消息内容 </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary> 格式化的时间戳字符串 </summary>
        public string TimestampStr => Timestamp.ToString("HH:mm:ss.fff");

        /// <summary> 方向显示文本 </summary>
        public string DirectionDisplay => Direction == "Send" ? "发送" : "接收";
    }
}
