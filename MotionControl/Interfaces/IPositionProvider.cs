using System.Collections.Generic;
using System.Threading.Tasks;

namespace MotionControl.Interfaces
{
    public interface IPositionProvider
    {
        Task<Dictionary<string, double>> GetPositionsAsync(string stationId);

        Task PreloadAsync();

        /// <summary>
        /// 使缓存失效，下次查询时将从存储重新加载
        /// </summary>
        Task InvalidateCacheAsync();

        /// <summary>
        /// 立即从配方文件重新加载位置缓存（保存位置参数后调用）
        /// </summary>
        Task RefreshCacheAsync();
    }
}
