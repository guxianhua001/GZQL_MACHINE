using Prism.Events;

namespace Core.Events
{
    /// <summary>
    /// 语言变更事件（用于Prism事件聚合器）
    /// </summary>
    public class LanguageChangedEvent : PubSubEvent<string>
    {
        /// <summary>
        /// 事件数据包装类
        /// </summary>
        public class Data
        {
            /// <summary>
            /// 旧的文化代码
            /// </summary>
            public string OldCultureCode { get; set; }

            /// <summary>
            /// 新的文化代码
            /// </summary>
            public string NewCultureCode { get; set; }

            /// <summary>
            /// 是否用户触发
            /// </summary>
            public bool IsUserInitiated { get; set; }

            /// <summary>
            /// 变更时间戳
            /// </summary>
            public System.DateTime Timestamp { get; } = System.DateTime.UtcNow;

            /// <summary>
            /// 构造函数
            /// </summary>
            public Data(string oldCultureCode, string newCultureCode, bool isUserInitiated = false)
            {
                OldCultureCode = oldCultureCode;
                NewCultureCode = newCultureCode;
                IsUserInitiated = isUserInitiated;
            }
        }

        /// <summary>
        /// 发布语言变更事件（带详细数据）
        /// </summary>
        public void Publish(string oldCultureCode, string newCultureCode, bool isUserInitiated = false)
        {
            base.Publish(newCultureCode);

            var detailedData = new Data(oldCultureCode, newCultureCode, isUserInitiated);
        }
    }

    /// <summary>
    /// 详细语言变更事件
    /// </summary>
    public class DetailedLanguageChangedEvent : PubSubEvent<LanguageChangedEvent.Data>
    {
    }
}
