using Prism.Events;

namespace Core.Events
{
    public class OperationProgressEvent : PubSubEvent<OperationProgressData> { }

    public class OperationProgressData
    {
        public double Progress { get; set; }
        public string Status { get; set; }
        public string OperationId { get; set; }
        public bool IsCompleted { get; set; }
        public bool Success { get; set; }
    }
    public class OperationCompletedData
    {
        public string OperationId { get; set; }
        public bool Success { get; set; }
    }
    public class OperationCompletedEvent : PubSubEvent<OperationCompletedData> { }
}
