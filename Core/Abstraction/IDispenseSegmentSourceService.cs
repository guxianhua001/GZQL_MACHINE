using Core.Models;
using System.Collections.Generic;

namespace Core.Abstraction
{
    /// <summary>
    /// 点胶轨迹段数据源服务——统一从共享存储、工站参数、JSON 配置文件读取线段
    /// 保证不打开 CadPointEditorView 时 DISPENSE 步骤与导入功能仍可获取轨迹段
    /// </summary>
    public interface IDispenseSegmentSourceService
    {
        /// <summary>
        /// 获取当前可用的源轨迹段列表（优先级：共享存储 → 工站参数 → LastSegmentConfigPath JSON）
        /// </summary>
        IReadOnlyList<DispenseSegment> GetSourceSegments();

        /// <summary>
        /// 从与轨迹段相同的数据源加载坐标对齐数据（含双针头仿射矩阵）
        /// </summary>
        CoordinateAlignData TryLoadAlignData();
    }
}
