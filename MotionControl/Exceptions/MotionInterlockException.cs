using System;

namespace MotionControl.Exceptions
{
    /// <summary>
    /// 整机状态不允许手动运动时抛出（如急停、未初始化、自动运行中等）
    /// </summary>
    public class MotionInterlockException : InvalidOperationException
    {
        public MotionInterlockException(string message) : base(message) { }
    }
}
