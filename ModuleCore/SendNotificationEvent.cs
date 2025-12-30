using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuleCore
{
    // NotificationEvent.cs（事件定义）
    public class SendNotificationEvent : PubSubEvent<string> { }

    public class NotificationMessage
    {
        public string Content { get; set; }
        public NotificationType Type { get; set; } = NotificationType.Info;
        public int DurationSeconds { get; set; } = 3;
    }

    public enum NotificationType
    {
        Info,
        Warning,
        Error,
        Success
    }

    public class EnhancedNotificationEvent : PubSubEvent<NotificationMessage> { }

}
