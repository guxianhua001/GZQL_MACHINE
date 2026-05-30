// Core/Models/DxfParseResult.cs
namespace Core.Models
{
    /// <summary>
    /// DXF文件解析结果，包含按图层分组的图元数据、整体范围和解析警告信息
    /// </summary>
    public class DxfParseResult
    {
        private Dictionary<string, List<CadEntity>> _layers = new();
        private BoundingBox _extents = new();
        private List<string> _parseWarnings = new();

        /// <summary>
        /// 按图层名称分组的图元字典（Key为图层名，Value为该图层下的所有图元列表）
        /// </summary>
        public Dictionary<string, List<CadEntity>> Layers
        {
            get => _layers;
            set => _layers = value;
        }

        /// <summary>
        /// 所有图元的整体范围包围盒（用于计算视口缩放和居中显示）
        /// </summary>
        public BoundingBox Extents
        {
            get => _extents;
            set => _extents = value;
        }

        /// <summary>
        /// 解析过程中产生的警告信息列表（如不支持的实体类型、数据异常等）
        /// </summary>
        public List<string> ParseWarnings
        {
            get => _parseWarnings;
            set => _parseWarnings = value;
        }

        /// <summary>
        /// 无参构造函数，初始化空的数据集合
        /// </summary>
        public DxfParseResult()
        {
        }

        /// <summary>
        /// 带参数构造函数，直接指定解析结果的各个组成部分
        /// </summary>
        /// <param name="layers">按图层分组的图元字典</param>
        /// <param name="extents">整体范围包围盒</param>
        /// <param name="parseWarnings">解析警告信息</param>
        public DxfParseResult(Dictionary<string, List<CadEntity>> layers, BoundingBox extents, List<string> parseWarnings)
        {
            _layers = layers ?? new Dictionary<string, List<CadEntity>>();
            _extents = extents ?? new BoundingBox();
            _parseWarnings = parseWarnings ?? new List<string>();
        }

        /// <summary>
        /// 获取所有图层中的图元总数
        /// </summary>
        public int TotalEntityCount => _layers?.Values.Sum(list => list?.Count ?? 0) ?? 0;

        /// <summary>
        /// 获取所有图层名称的列表
        /// </summary>
        public List<string> LayerNames => _layers?.Keys.ToList() ?? new List<string>();

        /// <summary>
        /// 判断解析是否成功完成（无严重错误）
        /// </summary>
        public bool IsSuccess => _layers != null && _layers.Count > 0;
    }
}
