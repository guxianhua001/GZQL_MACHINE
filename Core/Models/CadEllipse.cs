// Core/Models/CadEllipse.cs
namespace Core.Models
{
    /// <summary>
    /// 椭圆图元，表示由中心点、长短轴长度、旋转角和起止角度定义的椭圆弧或完整椭圆
    /// </summary>
    public class CadEllipse : CadEntity
    {
        private double _centerX;
        private double _centerY;
        private double _centerZ;
        private double _majorAxisLength;
        private double _minorAxisLength;
        private double _rotationAngle;
        private double _startAngle;
        private double _endAngle;

        /// <summary>
        /// 椭圆中心X坐标
        /// </summary>
        public double CenterX
        {
            get => _centerX;
            set => SetProperty(ref _centerX, value);
        }

        /// <summary>
        /// 椭圆中心Y坐标
        /// </summary>
        public double CenterY
        {
            get => _centerY;
            set => SetProperty(ref _centerY, value);
        }

        /// <summary>
        /// 椭圆中心Z坐标
        /// </summary>
        public double CenterZ
        {
            get => _centerZ;
            set => SetProperty(ref _centerZ, value);
        }

        /// <summary>
        /// 长半轴长度（椭圆的长轴半径）
        /// </summary>
        public double MajorAxisLength
        {
            get => _majorAxisLength;
            set => SetProperty(ref _majorAxisLength, value);
        }

        /// <summary>
        /// 短半轴长度（椭圆的短轴半径）
        /// </summary>
        public double MinorAxisLength
        {
            get => _minorAxisLength;
            set => SetProperty(ref _minorAxisLength, value);
        }

        /// <summary>
        /// 旋转角度（单位：度，长轴相对于X轴正方向的旋转角度，逆时针为正）
        /// </summary>
        public double RotationAngle
        {
            get => _rotationAngle;
            set => SetProperty(ref _rotationAngle, value);
        }

        /// <summary>
        /// 起始角度（单位：度，相对于长轴方向的参数角，用于定义椭圆弧的起点）
        /// </summary>
        public double StartAngle
        {
            get => _startAngle;
            set => SetProperty(ref _startAngle, value);
        }

        /// <summary>
        /// 终止角度（单位：度，相对于长轴方向的参数角，用于定义椭圆弧的终点）
        /// </summary>
        public double EndAngle
        {
            get => _endAngle;
            set => SetProperty(ref _endAngle, value);
        }

        /// <summary>
        /// 无参构造函数，初始化默认值并设置图元类型为Ellipse
        /// </summary>
        public CadEllipse()
        {
            EntityType = CadEntityType.Ellipse;
        }

        /// <summary>
        /// 带参数构造函数，指定椭圆的所有几何参数
        /// </summary>
        /// <param name="centerX">中心X坐标</param>
        /// <param name="centerY">中心Y坐标</param>
        /// <param name="majorAxisLength">长半轴长度</param>
        /// <param name="minorAxisLength">短半轴长度</param>
        /// <param name="rotationAngle">旋转角度（度）</param>
        /// <param name="startAngle">起始角度（度）</param>
        /// <param name="endAngle">终止角度（度）</param>
        /// <param name="centerZ">中心Z坐标，默认为0</param>
        public CadEllipse(double centerX, double centerY, double majorAxisLength, double minorAxisLength,
            double rotationAngle, double startAngle, double endAngle, double centerZ = 0)
        {
            EntityType = CadEntityType.Ellipse;
            CenterX = centerX;
            CenterY = centerY;
            CenterZ = centerZ;
            MajorAxisLength = majorAxisLength;
            MinorAxisLength = minorAxisLength;
            RotationAngle = rotationAngle;
            StartAngle = startAngle;
            EndAngle = endAngle;
        }

        /// <summary>
        /// 计算椭圆的轴对齐包围盒
        /// 使用参数方程采样关键点来估算边界范围
        /// </summary>
        /// <returns>椭圆的包围盒</returns>
        public override BoundingBox GetBoundingBox()
        {
            var bbox = new BoundingBox();

            // 将旋转角度转换为弧度
            double rotRad = RotationAngle * Math.PI / 180.0;
            double cosRot = Math.Cos(rotRad);
            double sinRot = Math.Sin(rotRad);

            // 参数方程计算椭圆上的点并扩展包围盒
            // 采样关键角度：0°、90°、180°、270°以及起止角度
            double[] sampleAngles = { StartAngle, EndAngle, 0, 90, 180, 270 };

            foreach (double angleDeg in sampleAngles)
            {
                double angleRad = angleDeg * Math.PI / 180.0;

                // 椭圆参数方程（相对于旋转后的坐标系）
                double localX = MajorAxisLength * Math.Cos(angleRad);
                double localY = MinorAxisLength * Math.Sin(angleRad);

                // 应用旋转变换得到世界坐标
                double worldX = CenterX + localX * cosRot - localY * sinRot;
                double worldY = CenterY + localX * sinRot + localY * cosRot;

                bbox.ExpandToInclude(worldX, worldY);
            }

            return bbox;
        }
    }
}
