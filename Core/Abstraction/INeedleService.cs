namespace Core.Abstraction
{
    /// <summary>
    /// 针头服务接口，提供针头使用计数、最大次数查询及重置等操作
    /// </summary>
    public interface INeedleService
    {
        /// <summary>
        /// 获取指定针头的已使用次数
        /// </summary>
        int GetNeedleUsageCount(int needleId);

        /// <summary>
        /// 获取指定针头的最大使用次数
        /// </summary>
        int GetNeedleMaxCount(int needleId);

        /// <summary>
        /// 递增指定针头的使用计数
        /// </summary>
        void IncrementNeedleCount(int needleId);

        /// <summary>
        /// 重置指定针头的使用计数
        /// </summary>
        void ResetNeedle(int needleId);
    }
}
