using System;

namespace MotionControl.Events
{
    /// <summary>
    /// 轴状态变更事件数据
    /// 用于事件驱动的轴状态监控，替代定时器轮询
    /// </summary>
    public class AxisStateChangedEvent : EventArgs
    {
        /// <summary> 逻辑轴号 </summary>
        public int AxisId { get; set; }

        /// <summary> 轴名称 </summary>
        public string Name { get; set; }

        /// <summary> 实时位置（单位：mm）</summary>
        public double Position { get; set; }

        /// <summary> 是否正在运动 </summary>
        public bool IsMoving { get; set; }

        /// <summary> 是否报警 </summary>
        public bool IsAlarmed { get; set; }

        /// <summary> 伺服是否开启 </summary>
        public bool IsServoOn { get; set; }

        /// <summary> 是否到达负极限（MEL）</summary>
        public bool IsMEL { get; set; }

        /// <summary> 是否到达原点（ORG）</summary>
        public bool IsORG { get; set; }

        /// <summary> 是否到达正极限（PEL）</summary>
        public bool IsPEL { get; set; }

        /// <summary> 急停信号状态（ASTP）</summary>
        public bool IsASTP { get; set; }

        /// <summary>回零完成标志（CheckHomeDone/dmc_get_home_result == 1，非 ORG 传感器）</summary>
        public bool IsHomeOk { get; set; }

        /// <summary> 状态字（原始IO状态）</summary>
        public int StatusWord { get; set; }

        /// <summary> 事件时间戳 </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
