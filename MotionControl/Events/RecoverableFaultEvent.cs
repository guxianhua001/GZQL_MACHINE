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
        /// <summary> 是否为手动操作触发的故障（而非自动运行）。手动操作时弹窗只显示"确定"按钮 </summary>
        public bool IsManualOperation { get; set; }
    }
}
