using Prism.Events;

namespace MotionControl.Events
{
    /// <summary>
    /// EtherCAT 总线状态变更事件（轮询 nmc_get_errcode 后发布，供 MainWindow 状态栏展示）
    /// </summary>
    public class EtherCatBusStatusChangedEvent : PubSubEvent<EtherCatBusStatusPayload> { }

    public class EtherCatBusStatusPayload
    {
        /// <summary>总线错误码，0 表示正常</summary>
        public int ErrorCode { get; set; }

        /// <summary>是否为模拟模式（无真实 EtherCAT 硬件）</summary>
        public bool IsSimulation { get; set; }

        public bool IsHealthy => !IsSimulation && ErrorCode == 0;
    }
}
