namespace MotionControl.Models
{
    public enum StationState
    {
        ESTOP,      // 急停
        ALARM,      // 报警
        PAUSE,      // 暂停
        RESETING,   // 复位中
        RUNNING,    // 运行中
        STOP,       // 停止
        WAITRESET,  // 等待复位
        CLEAR,      // 正在清料
        TIP,        // 发现报警（提示）
        WAITRUN     // 等待运行
    }
}