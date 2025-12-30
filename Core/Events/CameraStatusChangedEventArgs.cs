using System;

namespace Core.Events
{
    // 相机状态改变事件参数
    public class CameraStatusChangedEventArgs : EventArgs
    {
        public string CameraName { get; set; }
        public bool IsConnected { get; set; }
        public string Status { get; set; }
    }
}
