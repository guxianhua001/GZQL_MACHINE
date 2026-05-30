// Core/Models/CadEntity.cs
using Prism.Mvvm;

namespace Core.Models
{
    /// <summary>
    /// CAD图元基类，所有具体几何图形元素的抽象基类
    /// 提供通用属性如ID、图层、类型、颜色、选择状态和可见性
    /// </summary>
    public class CadEntity : BindableBase
    {
        private string _id = string.Empty;
        private string _layerName = "0";
        private CadEntityType _entityType;
        private string _color = "#FF000000";
        private bool _isSelected;
        private bool _isVisible = true;

        /// <summary>
        /// 图元唯一标识符
        /// </summary>
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// 图元所属图层名称
        /// </summary>
        public string LayerName
        {
            get => _layerName;
            set => SetProperty(ref _layerName, value);
        }

        /// <summary>
        /// 图元类型（直线、圆弧、圆形等）
        /// </summary>
        public CadEntityType EntityType
        {
            get => _entityType;
            set => SetProperty(ref _entityType, value);
        }

        /// <summary>
        /// 图元渲染颜色（ARGB十六进制格式，如 "#FF2196F3"）
        /// </summary>
        public string Color
        {
            get => _color;
            set => SetProperty(ref _color, value);
        }

        /// <summary>
        /// 是否被选中（用于交互式选择操作）
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>
        /// 是否可见（控制图元的显示/隐藏）
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        /// <summary>
        /// 自定义数据标签（用于存储预计算的渲染对象等扩展信息）
        /// 例如：拟合椭圆的预计算XLD轮廓、用户自定义元数据等
        /// </summary>
        public object Tag { get; set; }

        /// <summary>
        /// 无参构造函数，初始化默认值
        /// </summary>
        public CadEntity()
        {
        }

        /// <summary>
        /// 获取图元的轴对齐包围盒（AABB）
        /// 子类必须重写此方法以提供具体的边界计算逻辑
        /// </summary>
        /// <returns>包含该图元所有几何点的最小包围盒</returns>
        public virtual BoundingBox GetBoundingBox()
        {
            return new BoundingBox();
        }
    }
}
