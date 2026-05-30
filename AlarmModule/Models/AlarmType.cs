namespace AlarmModule.Models
{
    /// <summary>
    /// 报警类型
    /// </summary>
    public enum AlarmType
    {
        /// <summary>硬件故障</summary>
        HardwareFault = 1,
        /// <summary>参数超限</summary>
        ParameterOutOfLimit = 2,
        /// <summary>通讯错误</summary>
        CommunicationError = 3,
        /// <summary>工艺错误</summary>
        ProcessError = 4
    }
}
