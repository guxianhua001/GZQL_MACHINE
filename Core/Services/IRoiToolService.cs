// Core/Services/IRoiToolService.cs
using System.Collections.Generic;
using Core.Models;

namespace Core.Services
{
    /// <summary>
    /// ROI工具服务接口——提供ROI区域的创建与采样功能，
    /// 支持直线、折线、圆弧、自由手绘四种几何形态
    /// </summary>
    public interface IRoiToolService
    {
        /// <summary>创建直线型ROI区域</summary>
        /// <param name="start">起点坐标</param>
        /// <param name="end">终点坐标</param>
        /// <returns>配置好的直线RoiRegion实例</returns>
        RoiRegion CreateLineRoi(PointF start, PointF end);

        /// <summary>创建折线型ROI区域</summary>
        /// <param name="vertices">折线顶点序列（至少2个点）</param>
        /// <returns>配置好的折线RoiRegion实例</returns>
        RoiRegion CreatePolylineRoi(List<PointF> vertices);

        /// <summary>创建圆弧型ROI区域</summary>
        /// <param name="center">圆弧圆心</param>
        /// <param name="radius">圆弧半径</param>
        /// <param name="startAngleDeg">起始角度（度数）</param>
        /// <param name="endAngleDeg">终止角度（度数）</param>
        /// <returns>配置好的圆弧RoiRegion实例</returns>
        RoiRegion CreateArcRoi(PointF center, double radius, double startAngleDeg, double endAngleDeg);

        /// <summary>创建自由手绘型ROI区域</summary>
        /// <param name="rawPoints">密集笔迹原始点序列</param>
        /// <returns>配置好的自由手绘RoiRegion实例</returns>
        RoiRegion CreateFreehandRoi(List<PointF> rawPoints);

        /// <summary>
        /// 对指定ROI区域进行等间距采样，生成离散化CadPoint序列
        /// </summary>
        /// <param name="roi">待采样的ROI区域</param>
        /// <param name="pitchMM">采样间距（mm）</param>
        /// <returns>采样后的CadPoint列表</returns>
        List<CadPoint> SamplePoints(RoiRegion roi, double pitchMM);
    }
}
