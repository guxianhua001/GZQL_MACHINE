using Prism.Events;

namespace Core.Events
{
    /// <summary>
    /// 工站初始化进度事件：在整机初始化过程中发布，
    /// TaskMonitorViewModel 订阅后更新各工站回零进度显示。
    /// </summary>
    public class StationInitProgressEvent : PubSubEvent<StationInitProgressPayload> { }

    /// <summary>
    /// 工站初始化进度数据载体
    /// </summary>
    public class StationInitProgressPayload
    {
        /// <summary>工站标识（与 StationIdentifierValue 一致）</summary>
        public string StationId { get; set; }

        /// <summary>进度百分比 0-100</summary>
        public double Progress { get; set; }

        /// <summary>当前操作描述（如"回零 Dz₁"、"回到待机位"）</summary>
        public string Message { get; set; }

        /// <summary>是否已完成该工站初始化</summary>
        public bool IsCompleted { get; set; }

        /// <summary>是否发生错误</summary>
        public bool HasError { get; set; }
    }
}
