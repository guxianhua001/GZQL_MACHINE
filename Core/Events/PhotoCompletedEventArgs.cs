using System;
namespace Core.Events
{
    // 相机拍照完成事件参数
    public class PhotoCompletedEventArgs : EventArgs
    {
        public string CameraName { get; set; }
        public bool Success { get; set; }
        public string Data { get; set; }
        public string ErrorMessage { get; set; }
    }
}
