using Core.Abstraction;

namespace Core.Services
{
    /// <summary>
    /// 通用AD值转换器
    /// </summary>
    public class UniversalADValueConverter : IADValueConverter
    {
        private readonly Dictionary<int, ADChannelConfig> _channelConfigs;

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public UniversalADValueConverter()
        {
            _channelConfigs = new Dictionary<int, ADChannelConfig>();
        }

        /// <summary>
        /// 使用预定义配置初始化
        /// </summary>
        public UniversalADValueConverter(IEnumerable<ADChannelConfig> channelConfigs)
        {
            _channelConfigs = channelConfigs.ToDictionary(c => c.Channel, c => c);
        }

        /// <summary>
        /// 转换单个AD值到物理量，通道未配置时返回原始AD值
        /// </summary>
        public double Convert(int channel, double adValue)
        {
            if (!_channelConfigs.TryGetValue(channel, out var config))
            {
                return adValue;
            }

            if (!config.IsEnabled)
            {
                return 0.0;
            }

            // 线性转换公式
            double physicalValue = ((adValue - config.MinADValue) / (config.MaxADValue - config.MinADValue))
                                 * (config.MaxPhysicalValue - config.MinPhysicalValue)
                                 + config.MinPhysicalValue;

            // 应用校准系数和零点偏移
            physicalValue = (physicalValue + config.ZeroOffset) * config.CalibrationFactor;

            return physicalValue;
        }

        /// <summary>
        /// 批量转换AD值到物理量
        /// </summary>
        public Dictionary<int, double> ConvertBatch(Dictionary<int, double> channelADValues)
        {
            var results = new Dictionary<int, double>();
            foreach (var kvp in channelADValues)
            {
                try
                {
                    results[kvp.Key] = Convert(kvp.Key, kvp.Value);
                }
                catch (Exception ex)
                {
                    // 记录错误，但继续处理其他通道
                    System.Diagnostics.Debug.WriteLine($"通道 {kvp.Key} 转换失败: {ex.Message}");
                    results[kvp.Key] = 0.0;
                }
            }
            return results;
        }

        /// <summary>
        /// 获取通道配置
        /// </summary>
        public ADChannelConfig GetChannelConfig(int channel)
        {
            if (_channelConfigs.TryGetValue(channel, out var config))
            {
                return config;
            }
            return null;
        }

        /// <summary>
        /// 更新通道配置
        /// </summary>
        public void UpdateChannelConfig(ADChannelConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _channelConfigs[config.Channel] = config;
        }

        /// <summary>
        /// 获取所有通道配置
        /// </summary>
        public IReadOnlyDictionary<int, ADChannelConfig> GetAllChannelConfigs()
        {
            return _channelConfigs;
        }

        /// <summary>
        /// 添加通道配置
        /// </summary>
        public void AddChannelConfig(ADChannelConfig config)
        {
            UpdateChannelConfig(config);
        }

        /// <summary>
        /// 移除通道配置
        /// </summary>
        public bool RemoveChannelConfig(int channel)
        {
            return _channelConfigs.Remove(channel);
        }

        /// <summary>
        /// 清空所有配置
        /// </summary>
        public void ClearAllConfigs()
        {
            _channelConfigs.Clear();
        }
    }
}
