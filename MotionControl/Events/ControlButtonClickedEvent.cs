using Prism.Events;

namespace MotionControl.Events
{
    /// <summary>
    /// 主窗口控制按钮点击事件 - 用于MainWindow与OverViewModel之间的解耦通信
    /// </summary>
    public class ControlButtonClickedEvent : PubSubEvent<string> { }
}
