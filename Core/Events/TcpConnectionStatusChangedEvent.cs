using Prism.Events;

namespace Core.Events
{
    /// <summary>
    /// TCP 连接状态变更事件（TCPIPModule 发布，MainWindow 状态栏订阅）
    /// </summary>
    public class TcpConnectionStatusChangedEvent : PubSubEvent<TcpConnectionStatusPayload> { }

    /// <summary>
    /// TCP 连接状态载荷
    /// </summary>
    public class TcpConnectionStatusPayload
    {
        /// <summary>已连接数</summary>
        public int ConnectedCount { get; set; }

        /// <summary>总配置数（仅计算已启用的）</summary>
        public int TotalCount { get; set; }
    }
}
