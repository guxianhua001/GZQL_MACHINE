namespace AlarmModule.Models
{
    /// <summary>
    /// 报警状态生命周期：Unconfirmed→Confirmed→Reset→Eliminated
    /// </summary>
    public enum AlarmStatus
    {
        /// <summary>未确认</summary>
        Unconfirmed = 1,
        /// <summary>已确认</summary>
        Confirmed = 2,
        /// <summary>已复位</summary>
        Reset = 3,
        /// <summary>已消除</summary>
        Eliminated = 4
    }
}
