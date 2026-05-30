using Core.Models;

namespace Core.Services
{
    /// <summary>
    /// DXF文件解析服务接口，提供DXF文件解析和图元离散化功能
    /// </summary>
    public interface IDxfParserService
    {
        /// <summary>
        /// 解析DXF文件，按图层分组返回所有图元
        /// </summary>
        /// <param name="filePath">DXF文件的完整路径</param>
        /// <returns>解析结果，包含按图层分组的图元、整体范围和解析警告</returns>
        DxfParseResult Parse(string filePath);

        /// <summary>
        /// 将单个CAD图元离散化为等间距点序列（用于运动轨迹生成）
        /// </summary>
        /// <param name="entity">要离散化的CAD图元</param>
        /// <param name="pitchMM">离散化间距（单位：毫米）</param>
        /// <returns>离散化后的点序列列表</returns>
        List<CadPoint> Discretize(CadEntity entity, double pitchMM);

        /// <summary>
        /// 批量离散化多个CAD图元，将所有图元的离散点合并为一个序列
        /// </summary>
        /// <param name="entities">要离散化的CAD图元集合</param>
        /// <param name="pitchMM">离散化间距（单位：毫米）</param>
        /// <returns>所有图元离散化后的合并点序列列表</returns>
        List<CadPoint> DiscretizeAll(IEnumerable<CadEntity> entities, double pitchMM);

        /// <summary>
        /// 按指定点数对CAD图元进行离散化采样（等间距均匀采样）
        /// </summary>
        /// <param name="entity">要离散化的CAD图元</param>
        /// <param name="pointCount">目标采样点数（最小2个点）</param>
        /// <returns>离散化后的点序列列表</returns>
        List<CadPoint> DiscretizeByCount(CadEntity entity, int pointCount);
    }
}
