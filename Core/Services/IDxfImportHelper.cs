using Core.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Core.Services
{
    /// <summary>
    /// DXF 导入选项配置类
    /// 控制统一导入方法的行为参数
    /// </summary>
    public class DxfImportOptions
    {
        /// <summary> 是否包含 ARC 圆弧实体（默认 true） </summary>
        public bool IncludeArcs { get; set; } = true;

        /// <summary> 是否包含 CIRCLE 圆形实体（默认 true） </summary>
        public bool IncludeCircles { get; set; } = true;

        /// <summary> 是否包含 SPLINE 样条曲线实体（默认 true） </summary>
        public bool IncludeSplines { get; set; } = true;

        /// <summary> 离散化间距（毫米），0 表示不进行离散化（默认 1.0） </summary>
        public double DiscretizePitchMM { get; set; } = 1.0;

        /// <summary> 是否提取原始点位数据用于 DataGrid 显示（默认 false） </summary>
        public bool ExtractPoints { get; set; } = false;

        /// <summary> 点位提取时的图层过滤，null 或空表示提取所有图层 </summary>
        public string PointLayerFilter { get; set; } = null;

        /// <summary>
        /// 创建点胶轨迹编辑器使用的标准选项
        /// 包含所有实体类型 + 1mm 间距离散化
        /// </summary>
        public static DxfImportOptions ForDispenseEditor => new DxfImportOptions
        {
            IncludeArcs = true,
            IncludeCircles = true,
            IncludeSplines = true,
            DiscretizePitchMM = 1.0,
            ExtractPoints = false
        };

        /// <summary>
        /// 创建坐标对齐模块使用的选项
        /// ✅ 已统一：与 ForDispenseEditor 使用相同的实体过滤和离散化参数
        /// 额外提取点位用于 DataGrid 显示和线段选取
        /// </summary>
        public static DxfImportOptions ForAlignment => new DxfImportOptions
        {
            IncludeArcs = true,         // ✅ 修改：包含圆弧（保证显示一致性）
            IncludeCircles = true,
            IncludeSplines = true,
            DiscretizePitchMM = 1.0,    // ✅ 修改：启用离散化（保证渲染一致）
            ExtractPoints = true         // 保持：提取点位用于 DataGrid
        };
    }

    /// <summary>
    /// DXF 统一导入结果类
    /// 封装导入操作的所有输出数据
    /// </summary>
    public class DxfImportResult
    {
        /// <summary> 原始解析结果（包含图层信息、警告等） </summary>
        public DxfParseResult ParseResult { get; set; }

        /// <summary> 用于 HalconCanvas 渲染的图元集合（已根据选项过滤） </summary>
        public ObservableCollection<CadEntity> DisplayEntities { get; set; } = new ObservableCollection<CadEntity>();

        /// <summary> 提取的原始点位列表（用于 DataGrid 显示） </summary>
        public List<CadPoint> ExtractedPoints { get; set; } = new List<CadPoint>();

        /// <summary> 图层名称列表 </summary>
        public List<string> LayerNames { get; set; } = new List<string>();

        /// <summary> 导入是否成功 </summary>
        public bool IsSuccess => ParseResult?.IsSuccess == true && (DisplayEntities.Count > 0 || ExtractedPoints.Count > 0);

        /// <summary> 总图元数量（过滤后） </summary>
        public int TotalEntityCount => DisplayEntities?.Count ?? 0;

        /// <summary> 总点数 </summary>
        public int TotalPointCount => ExtractedPoints?.Count ?? 0;
    }

    /// <summary>
    /// DXF 统一导入服务接口
    /// 提供 DXF 文件导入、图元过滤、离散化和点位提取的一站式功能
    /// 保证 CadPointEditorViewModel 和 CadAlignmentViewModel 使用完全相同的导入逻辑
    /// </summary>
    public interface IDxfImportHelper
    {
        /// <summary>
        /// 统一导入 DXF 文件并返回标准化结果
        /// 内部调用 IDxfParserService.Parse() 进行解析，然后根据选项进行过滤和处理
        /// </summary>
        /// <param name="filePath">DXF 文件的完整路径</param>
        /// <param name="options">导入选项配置</param>
        /// <returns>标准化导入结果</returns>
        DxfImportResult Import(string filePath, DxfImportOptions options);
    }
}
