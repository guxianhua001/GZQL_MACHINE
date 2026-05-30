using Prism.Events;

namespace MotionControl.Events
{
    public enum RecoverableFaultAction
    {
        None,
        Resume,
        Pause,
        Stop
    }

    public class RecoverableFaultEvent : PubSubEvent<RecoverableFaultPayload> { }

    public class RecoverableFaultPayload
    {
        public int TaskId { get; set; }
        public string TaskName { get; set; }
        public string StepName { get; set; }
        public string ErrorMessage { get; set; }
        public string SuggestedAction { get; set; }
        public RecoverableFaultAction Action { get; set; } = RecoverableFaultAction.None;
    }
}
