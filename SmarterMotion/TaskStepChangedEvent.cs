using Prism.Events;
using System;

namespace SmarterMotion.Events
{
    // Prism 事件用于全局通信
    public class TaskStepChangedEvent : PubSubEvent<TaskStepChangedEventArgs> { }
}
