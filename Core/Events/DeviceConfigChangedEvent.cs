using Core.Configuration;
using Prism.Events;

namespace Core.Events
{
    /// <summary>
    /// 设备配置变更事件（用于Prism事件聚合器）
    /// </summary>
    public class DeviceConfigChangedEvent : PubSubEvent<AppSettings>
    {
    }
}
