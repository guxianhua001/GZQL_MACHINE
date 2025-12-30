using System;

namespace Core.Abstraction
{
    /// <summary>
    /// AD值转换器接口
    /// </summary>
    public interface IADValueConverter
    {
        /// <summary>
        /// 转换单个AD值到物理量
        /// </summary>
        double Convert(int channel, double adValue);

        /// <summary>
        /// 批量转换AD值到物理量
        /// </summary>
        Dictionary<int, double> ConvertBatch(Dictionary<int, double> channelADValues);

        /// <summary>
        /// 获取通道配置
        /// </summary>
        ADChannelConfig GetChannelConfig(int channel);

        /// <summary>
        /// 更新通道配置
        /// </summary>
        void UpdateChannelConfig(ADChannelConfig config);

        /// <summary>
        /// 获取所有通道配置
        /// </summary>
        IReadOnlyDictionary<int, ADChannelConfig> GetAllChannelConfigs();
    }
    /// <summary>
    /// AD通道配置
    /// </summary>
    public class ADChannelConfig
    {
        /// <summary>
        /// 通道号
        /// </summary>
        public int Channel { get; set; }

        /// <summary>
        /// 通道名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// AD最小值
        /// </summary>
        public double MinADValue { get; set; } = -32767;

        /// <summary>
        /// AD最大值
        /// </summary>
        public double MaxADValue { get; set; } = 32767;

        /// <summary>
        /// 物理量最小值
        /// </summary>
        public double MinPhysicalValue { get; set; }

        /// <summary>
        /// 物理量最大值
        /// </summary>
        public double MaxPhysicalValue { get; set; }

        /// <summary>
        /// 物理量单位
        /// </summary>
        public string Unit { get; set; } = "N";

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 校准系数
        /// </summary>
        public double CalibrationFactor { get; set; } = 1.0;

        /// <summary>
        /// 零点偏移
        /// </summary>
        public double ZeroOffset { get; set; } = 0.0;
    }
}
