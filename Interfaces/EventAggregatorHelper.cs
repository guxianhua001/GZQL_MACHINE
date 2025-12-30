using Interfaces.Events;
using Prism.Events;

namespace Interfaces
{
    // 事件聚合器辅助类 (单例模式)
    public static class EventAggregatorHelper
    {
        private static readonly IEventAggregator _eventAggregator = new EventAggregator();
        //public static TaskStepChangedEvent GetTaskStepChangedEvent()
        //{
        //    return _eventAggregator.GetEvent<TaskStepChangedEvent>();
        //}
        public static IEventAggregator GetEventAggregator()
        {
            return _eventAggregator;
        }
    }
}
