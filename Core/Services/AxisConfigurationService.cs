using Core.Models;
using System.Collections.Generic;

namespace Core.Services
{
    /// <summary>
    /// 轴配置服务接口：根据工站标识获取轴定义列表
    /// 实现类已迁移至 MotionControl 模块
    /// </summary>
    public interface IAxisConfigurationService
    {
        IReadOnlyList<AxisDefinition> GetAxesForStation(string stationIdentifier);
    }
}
