using Prism.Events;
using System.Collections.Generic;

namespace Core.Events
{
    /// <summary>
    /// TCP 连接状态变更事件（TCPIPModule 发布，MainWindow 状态栏订阅）
    /// </summary>
    public class TcpConnectionStatusChangedEvent : PubSubEvent<TcpConnectionStatusPayload> { }

    /// <summary>
    /// 单个 TCP 连接的状态信息
    /// </summary>
    public class TcpConnectionDetail
    {
        /// <summary>连接名称（如 TCP_1、TCP_2）</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>是否已连接</summary>
        public bool IsConnected { get; set; }
    }

    /// <summary>
    /// TCP 连接状态载荷
    /// </summary>
    public class TcpConnectionStatusPayload
    {
        /// <summary>已连接数</summary>
        public int ConnectedCount { get; set; }

        /// <summary>总配置数（仅计算已启用的）</summary>
        public int TotalCount { get; set; }

        /// <summary>每个连接的详细状态</summary>
        public List<TcpConnectionDetail> Details { get; set; } = new();
    }
}
