namespace AlarmModule.Models
{
    /// <summary>
    /// 报警等级：工业4级分类系统
    /// </summary>
    public enum AlarmLevel
    {
        /// <summary>紧急停机：危及人身/设备安全，立即停机</summary>
        Emergency = 1,
        /// <summary>严重故障：无法生产，需立刻处理</summary>
        Serious = 2,
        /// <summary>一般报警：可维持生产，需当班处理</summary>
        General = 3,
        /// <summary>提示预警：保养提醒、临近阈值，无需紧急处理</summary>
        Prompt = 4
    }
}
