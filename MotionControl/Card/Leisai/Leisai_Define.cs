using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MotionControl.Card
{
    /// <summary>
    /// 雷赛运动控制 IO 信号及状态位定义
    /// </summary>
    public static class Leisai_Define
    {
        // 轴 IO 信号掩码 (dmc_axis_io_status)
        public const int MIO_ALM = 1 << 0;   // 伺服报警
        public const int MIO_PEL = 1 << 1;   // 正硬限位
        public const int MIO_MEL = 1 << 2;   // 负硬限位
        public const int MIO_EMG = 1 << 3;   // 急停信号
        public const int MIO_ORG = 1 << 4;   // 原点信号
        public const int MIO_SPEL = 1 << 6;   // 正软限位
        public const int MIO_SMEL = 1 << 7;   // 负软限位
        public const int MIO_INP = 1 << 8;   // EtherCAT 保留
        public const int MIO_EZ = 1 << 9;   // EZ 信号
        public const int MIO_DSTP = 1 << 11;  // 减速停止
        public const int MIO_SVON = 1 << 12;  // 伺服使能状态
        public const int MIO_ASTP = 1 << 13;  // 急停有效状态

        // 轴停止原因掩码 (dmc_get_stop_reason)
        public const int MTS_MDN = 1 << 0;  // 正常停止
        public const int MTS_ALM = 1 << 1;  // 报警立即停止
        public const int MTS_EMG = 1 << 4;  // 急停立即停止
        public const int MTS_OTHER = 1 << 15; // 其它轴引起的立即停止
        public const int MTS_PEL = 1 << 5;  // 正硬限位停止
        public const int MTS_MEL = 1 << 6;  // 负硬限位停止
        public const int MTS_SVON = 1 << 10; // 伺服使能

        // 总线轴状态机 (nmc_get_axis_state_machine)，返回值 0~7
        public const int AXIS_SM_NOT_STARTED = 0;           // 未启动
        public const int AXIS_SM_SWITCH_ON_DISABLED = 1;    // 启动禁止
        public const int AXIS_SM_READY_TO_SWITCH_ON = 2;    // 准备启动
        public const int AXIS_SM_SWITCHED_ON = 3;           // 已启动
        /// <summary> 操作使能（旧项目 IsSVON 判断 sts==4） </summary>
        public const int AXIS_SM_OPERATION_ENABLED = 4;
        public const int AXIS_SM_QUICK_STOP = 5;           // 停止
        public const int AXIS_SM_FAULT_REACTION = 6;        // 错误触发
        public const int AXIS_SM_FAULT = 7;                 // 错误
        public const int NOT_READY = 1 << 0;
        public const int DISABLE = 1 << 1;
        public const int READY = 1 << 2;
        public const int ON = 1 << 3;
        public const int ENABLE = 1 << 4;
        public const int QUICK_STOP = 1 << 5;
        public const int FAULT_ACTIVE = 1 << 6;
        public const int FAULT = 1 << 7;
    }
}
