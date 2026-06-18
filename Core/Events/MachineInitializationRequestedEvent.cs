using Prism.Events;

namespace Core.Events
{
    /// <summary>
    /// 整机初始化请求事件：由复位按钮长按5秒触发，
    /// MachineInitializationService 订阅后执行整机初始化序列。
    /// </summary>
    public class MachineInitializationRequestedEvent : PubSubEvent { }
}
