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
        private readonly ILocalizationService _localization;

        /// <summary>获取多语言格式化字符串</summary>
        private string L(string key, string fallback, params object[] args)
        {
            var format = _localization?.GetResourceOrDefault(key, fallback) ?? fallback;
            return args.Length > 0 ? string.Format(format, args) : format;
        }

        /// <summary>
        /// 针头默认最大使用次数
        /// </summary>
        private const int DefaultMaxCount = 10000;

        /// <summary>
        /// 寿命预警阈值（使用次数占最大次数的百分比）
        /// </summary>
        private const double WarningThreshold = 0.8;

        public NeedleService(ILoggerService logger, IEventAggregator eventAggregator, ILocalizationService localization)
        {
            _logger = logger;
            _eventAggregator = eventAggregator;
            _localization = localization;
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
            _logger?.Info(L("Needle_Log_CountIncremented", "NeedleService: 针头 {0} 使用次数递增至 {1}", needleId, newCount));

            var maxCount = GetNeedleMaxCount(needleId);
            var ratio = (double)newCount / maxCount;

            if (ratio >= WarningThreshold)
            {
                _logger?.Warn(L("Needle_Log_LifeWarningThreshold", "NeedleService: 针头 {0} 使用次数已达预警阈值 ({1:P0})，当前 {2}/{3}", needleId, ratio, newCount, maxCount));

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
            _logger?.Info(L("Needle_Log_CountReset", "NeedleService: 针头 {0} 使用计数已重置", needleId));
        }
    }
}
