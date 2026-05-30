using Core.Abstraction;
using Core.Events;
using Core.Utilities;
using Prism.Events;
using System.Collections.Concurrent;

namespace Module.Services
{
    /// <summary>
    /// 针头服务实现，提供线程安全的针头使用计数管理
    /// </summary>
    public class NeedleService : INeedleService
    {
        private readonly ConcurrentDictionary<int, int> _usageCounts = new();
        private readonly ILoggerService _logger;
        private readonly IEventAggregator _eventAggregator;

        /// <summary>
        /// 针头默认最大使用次数
        /// </summary>
        private const int DefaultMaxCount = 10000;

        /// <summary>
        /// 寿命预警阈值（使用次数占最大次数的百分比）
        /// </summary>
        private const double WarningThreshold = 0.8;

        public NeedleService(ILoggerService logger, IEventAggregator eventAggregator)
        {
            _logger = logger;
            _eventAggregator = eventAggregator;
        }

        /// <summary>
        /// 获取指定针头的已使用次数
        /// </summary>
        public int GetNeedleUsageCount(int needleId)
        {
            return _usageCounts.TryGetValue(needleId, out var count) ? count : 0;
        }

        /// <summary>
        /// 获取指定针头的最大使用次数
        /// </summary>
        public int GetNeedleMaxCount(int needleId)
        {
            return DefaultMaxCount;
        }

        /// <summary>
        /// 递增指定针头的使用计数，达到预警阈值时发布寿命警告事件
        /// </summary>
        public void IncrementNeedleCount(int needleId)
        {
            var newCount = _usageCounts.AddOrUpdate(needleId, 1, (_, current) => current + 1);
            _logger?.Info($"NeedleService: 针头 {needleId} 使用次数递增至 {newCount}");

            var maxCount = GetNeedleMaxCount(needleId);
            var ratio = (double)newCount / maxCount;

            if (ratio >= WarningThreshold)
            {
                _logger?.Warn($"NeedleService: 针头 {needleId} 使用次数已达预警阈值 ({ratio:P0})，当前 {newCount}/{maxCount}");

                _eventAggregator.GetEvent<NeedleLifeWarningEvent>().Publish(new NeedleLifeWarningEventArgs
                {
                    NeedleId = needleId,
                    UsageCount = newCount,
                    MaxCount = maxCount,
                    UsageRatio = ratio
                });
            }
        }

        /// <summary>
        /// 重置指定针头的使用计数
        /// </summary>
        public void ResetNeedle(int needleId)
        {
            _usageCounts.AddOrUpdate(needleId, 0, (_, _) => 0);
            _logger?.Info($"NeedleService: 针头 {needleId} 使用计数已重置");
        }
    }
}
