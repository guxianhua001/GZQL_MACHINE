using System.Collections.Generic;

namespace StationTasks.Services
{
    /// <summary>
    /// 视觉数据解析器接口：将原始字符串数据解析为键值对
    /// </summary>
    public interface IVisionDataParser
    {
        /// <summary>
        /// 解析原始数据字符串
        /// </summary>
        /// <param name="rawData">原始返回数据</param>
        /// <returns>解析出的键值对（如 offsetX=1.5, offsetY=-0.3）</returns>
        Dictionary<string, double> Parse(string rawData);
    }
}
