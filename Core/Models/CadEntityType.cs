// Core/Models/CadEntityType.cs
namespace Core.Models
{
    /// <summary>
    /// CAD图元类型枚举，用于标识不同类型的几何图形元素
    /// </summary>
    public enum CadEntityType
    {
        /// <summary>直线段</summary>
        Line,

        /// <summary>圆弧</summary>
        Arc,

        /// <summary>整圆</summary>
        Circle,

        /// <summary>轻量多段线（LWPOLYLINE）</summary>
        LwPolyline,

        /// <summary>通用多段线</summary>
        Polyline,

        /// <summary>椭圆</summary>
        Ellipse,

        /// <summary>样条曲线</summary>
        Spline,

        /// <summary>未知类型</summary>
        Unknown
    }
}
