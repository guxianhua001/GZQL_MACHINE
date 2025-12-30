using Prism.Events;

namespace Recipe.Events
{
    public class RecipeChangedEvent : PubSubEvent<string> { }
    public class SaveParametersEvent : PubSubEvent<string> { }
    public class SaveParametersCompletedEvent : PubSubEvent<string> { }
    public class SaveParametersProgressEvent : PubSubEvent<SaveProgressInfo>
    {
    }

    public class SaveProgressInfo
    {
        public int Progress { get; set; }
        public string StationName { get; set; }
        public string Operation { get; set; }
    }
}
