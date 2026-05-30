using Core.Abstraction;
using Prism.Events;

namespace Core.Events
{
    /// <summary>
    /// 工站注册事件，当新工站注册到IStationRegistry时发布
    /// </summary>
    public class StationRegisteredEvent : PubSubEvent<IStationParameterProvider> { }
}
