﻿using Prism.Events;
using Recipe.Models;

namespace Recipe.Events
{
    public class RecipeChangedEvent : PubSubEvent<string> { }
    public class SaveParametersEvent : PubSubEvent<string> { }
    public class SaveParametersCompletedEvent : PubSubEvent<string> { }
    public class SaveParametersProgressEvent : PubSubEvent<SaveProgressInfo>{ }
    public class RecipePoolChangedEvent : PubSubEvent<string> { }
    /// <summary>
    /// 配方池保存前同步全局变量事件。
    /// 由全局变量页面将当前编辑集合写入待保存的 RecipePool，避免保存后再加载旧数据覆盖编辑结果。
    /// </summary>
    public class SaveGlobalVariablesEvent : PubSubEvent<RecipePool> { }

    /// <summary>
    /// 全局变量被外部更新事件（如SCAN数据解析后自动写入全局变量）
    /// 参数为配方池ID，订阅者应重新从存储加载最新数据
    /// </summary>
    public class GlobalVariablesChangedEvent : PubSubEvent<string> { }

    public class SaveProgressInfo
    {
        public int Progress { get; set; }
        public string StationName { get; set; }
        public string Operation { get; set; }
    }
}
