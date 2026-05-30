// Core/Models/BoundingBox.cs
namespace Core.Models
{
    /// <summary>
    /// 轴对齐包围盒（AABB），用于表示二维几何图形的边界范围
    /// </summary>
    public class BoundingBox
    {
        private double _minX = double.MaxValue;
        private double _maxX = double.MinValue;
        private double _minY = double.MaxValue;
        private double _maxY = double.MinValue;

        /// <summary>
        /// 包围盒左边界X坐标（最小X值）
        /// </summary>
        public double MinX
        {
            get => _minX;
            set => _minX = value;
        }

        /// <summary>
        /// 包围盒右边界X坐标（最大X值）
        /// </summary>
        public double MaxX
        {
            get => _maxX;
            set => _maxX = value;
        }

        /// <summary>
        /// 包围盒下边界Y坐标（最小Y值）
        /// </summary>
        public double MinY
        {
            get => _minY;
            set => _minY = value;
        }

        /// <summary>
        /// 包围盒上边界Y坐标（最大Y值）
        /// </summary>
        public double MaxY
        {
            get => _maxY;
            set => _maxY = value;
        }

        /// <summary>
        /// 包围盒宽度（只读计算属性，MaxX - MinX）
        /// </summary>
        public double Width => IsEmpty ? 0 : MaxX - MinX;

        /// <summary>
        /// 包围盒高度（只读计算属性，MaxY - MinY）
        /// </summary>
        public double Height => IsEmpty ? 0 : MaxY - MinY;

        /// <summary>
        /// 判断包围盒是否为空（未包含任何点）
        /// </summary>
        public bool IsEmpty => _minX > _maxX || _minY > _maxY;

        /// <summary>
        /// 无参构造函数，初始化为空包围盒
        /// </summary>
        public BoundingBox()
        {
        }

        /// <summary>
        /// 带参数构造函数，直接指定包围盒的四个边界值
        /// </summary>
        /// <param name="minX">最小X坐标</param>
        /// <param name="maxX">最大X坐标</param>
        /// <param name="minY">最小Y坐标</param>
        /// <param name="maxY">最大Y坐标</param>
        public BoundingBox(double minX, double maxX, double minY, double maxY)
        {
            _minX = minX;
            _maxX = maxX;
            _minY = minY;
            _maxY = maxY;
        }

        /// <summary>
        /// 判断指定坐标点是否在包围盒内部或边界上
        /// </summary>
        /// <param name="x">待检测点的X坐标</param>
        /// <param name="y">待检测点的Y坐标</param>
        /// <returns>如果点在包围盒内返回true，否则返回false</returns>
        public bool Contains(double x, double y)
        {
            if (IsEmpty)
                return false;

            return x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
        }

        /// <summary>
        /// 计算当前包围盒与另一个包围盒的并集，返回新的包围盒
        /// </summary>
        /// <param name="other">要合并的另一个包围盒</param>
        /// <returns>包含两个包围盒范围的新包围盒</returns>
        public BoundingBox Union(BoundingBox other)
        {
            if (other == null || other.IsEmpty)
                return new BoundingBox(MinX, MaxX, MinY, MaxY);

            if (IsEmpty)
                return new BoundingBox(other.MinX, other.MaxX, other.MinY, other.MaxY);

            return new BoundingBox(
                Math.Min(MinX, other.MinX),
                Math.Max(MaxX, other.MaxX),
                Math.Min(MinY, other.MinY),
                Math.Max(MaxY, other.MaxY)
            );
        }

        /// <summary>
        /// 扩展当前包围盒以包含指定的坐标点
        /// </summary>
        /// <param name="x">要包含的点的X坐标</param>
        /// <param name="y">要包含的点的Y坐标</param>
        public void ExpandToInclude(double x, double y)
        {
            if (IsEmpty)
            {
                _minX = _maxX = x;
                _minY = _maxY = y;
            }
            else
            {
                if (x < _minX) _minX = x;
                if (x > _maxX) _maxX = x;
                if (y < _minY) _minY = y;
                if (y > _maxY) _maxY = y;
            }
        }
    }
}
