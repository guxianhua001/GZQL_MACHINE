using Core.Abstraction;
using Prism.Events;

namespace Core.Events
{
    /// <summary>
    /// 工站注销事件，当工站从IStationRegistry注销时发布
    /// </summary>
    public class StationUnregisteredEvent : PubSubEvent<IStationParameterProvider> { }
}
