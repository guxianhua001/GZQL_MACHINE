using Prism.Events;

namespace Core.Events
{
    public class MessageEvent : PubSubEvent<Message>
    {
    }

    public struct Message
    {
        /// <summary>
        /// 消息目标
        /// </summary>
        public string Target { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Content { get; set; }
    }
}
