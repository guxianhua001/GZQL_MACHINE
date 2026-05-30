// Core/Models/CadCircle.cs
namespace Core.Models
{
    /// <summary>
    /// 圆形图元，表示由圆心和半径定义的完整圆形
    /// </summary>
    public class CadCircle : CadEntity
    {
        private double _centerX;
        private double _centerY;
        private double _centerZ;
        private double _radius;

        /// <summary>
        /// 圆心X坐标
        /// </summary>
        public double CenterX
        {
            get => _centerX;
            set => SetProperty(ref _centerX, value);
        }

        /// <summary>
        /// 圆心Y坐标
        /// </summary>
        public double CenterY
        {
            get => _centerY;
            set => SetProperty(ref _centerY, value);
        }

        /// <summary>
        /// 圆心Z坐标
        /// </summary>
        public double CenterZ
        {
            get => _centerZ;
            set => SetProperty(ref _centerZ, value);
        }

        /// <summary>
        /// 圆的半径（必须为正数）
        /// </summary>
        public double Radius
        {
            get => _radius;
            set => SetProperty(ref _radius, value);
        }

        /// <summary>
        /// 无参构造函数，初始化默认值并设置图元类型为Circle
        /// </summary>
        public CadCircle()
        {
            EntityType = CadEntityType.Circle;
        }

        /// <summary>
        /// 带参数构造函数，指定圆心和半径
        /// </summary>
        /// <param name="centerX">圆心X坐标</param>
        /// <param name="centerY">圆心Y坐标</param>
        /// <param name="radius">圆的半径</param>
        /// <param name="centerZ">圆心Z坐标，默认为0</param>
        public CadCircle(double centerX, double centerY, double radius, double centerZ = 0)
        {
            EntityType = CadEntityType.Circle;
            CenterX = centerX;
            CenterY = centerY;
            CenterZ = centerZ;
            Radius = radius;
        }

        /// <summary>
        /// 计算圆形的轴对齐包围盒（圆的外接正方形）
        /// </summary>
        /// <returns>圆形的包围盒</returns>
        public override BoundingBox GetBoundingBox()
        {
            return new BoundingBox(
                CenterX - Radius,
                CenterX + Radius,
                CenterY - Radius,
                CenterY + Radius
            );
        }
    }
}
