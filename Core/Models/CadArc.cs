// Core/Models/CadArc.cs
namespace Core.Models
{
    /// <summary>
    /// 圆弧图元，表示由圆心、半径和起止角度定义的圆弧段
    /// 角度采用度数制，逆时针方向为正
    /// </summary>
    public class CadArc : CadEntity
    {
        private double _centerX;
        private double _centerY;
        private double _centerZ;
        private double _radius;
        private double _startAngle;
        private double _endAngle;

        /// <summary>
        /// 圆弧圆心X坐标
        /// </summary>
        public double CenterX
        {
            get => _centerX;
            set => SetProperty(ref _centerX, value);
        }

        /// <summary>
        /// 圆弧圆心Y坐标
        /// </summary>
        public double CenterY
        {
            get => _centerY;
            set => SetProperty(ref _centerY, value);
        }

        /// <summary>
        /// 圆弧圆心Z坐标
        /// </summary>
        public double CenterZ
        {
            get => _centerZ;
            set => SetProperty(ref _centerZ, value);
        }

        /// <summary>
        /// 圆弧半径（必须为正数）
        /// </summary>
        public double Radius
        {
            get => _radius;
            set => SetProperty(ref _radius, value);
        }

        /// <summary>
        /// 圆弧起始角度（单位：度，逆时针方向为正，从正X轴开始计算）
        /// </summary>
        public double StartAngle
        {
            get => _startAngle;
            set => SetProperty(ref _startAngle, value);
        }

        /// <summary>
        /// 圆弧终止角度（单位：度，逆时针方向为正）
        /// </summary>
        public double EndAngle
        {
            get => _endAngle;
            set => SetProperty(ref _endAngle, value);
        }

        /// <summary>
        /// 无参构造函数，初始化默认值并设置图元类型为Arc
        /// </summary>
        public CadArc()
        {
            EntityType = CadEntityType.Arc;
        }

        /// <summary>
        /// 带参数构造函数，指定圆心和半径及起止角度
        /// </summary>
        /// <param name="centerX">圆心X坐标</param>
        /// <param name="centerY">圆心Y坐标</param>
        /// <param name="radius">圆弧半径</param>
        /// <param name="startAngle">起始角度（度）</param>
        /// <param name="endAngle">终止角度（度）</param>
        /// <param name="centerZ">圆心Z坐标，默认为0</param>
        public CadArc(double centerX, double centerY, double radius, double startAngle, double endAngle, double centerZ = 0)
        {
            EntityType = CadEntityType.Arc;
            CenterX = centerX;
            CenterY = centerY;
            CenterZ = centerZ;
            Radius = radius;
            StartAngle = startAngle;
            EndAngle = endAngle;
        }

        /// <summary>
        /// 计算圆弧的轴对齐包围盒
        /// 通过采样圆弧上的关键点（包含四分点和起止点）来计算边界
        /// </summary>
        /// <returns>圆弧的包围盒</returns>
        public override BoundingBox GetBoundingBox()
        {
            var bbox = new BoundingBox();

            // 将角度转换为弧度进行计算
            double startRad = StartAngle * Math.PI / 180.0;
            double endRad = EndAngle * Math.PI / 180.0;

            // 确保包含起始点和终止点
            bbox.ExpandToInclude(CenterX + Radius * Math.Cos(startRad), CenterY + Radius * Math.Sin(startRad));
            bbox.ExpandToInclude(CenterX + Radius * Math.Cos(endRad), CenterY + Radius * Math.Sin(endRad));

            // 检查是否经过四个象限点（0°、90°、180°、270°），这些点是包围盒的潜在极值点
            double normalizedStart = NormalizeAngle(StartAngle);
            double normalizedEnd = NormalizeAngle(EndAngle);

            // 检查90°点（顶部）
            if (IsAngleInRange(90, normalizedStart, normalizedEnd))
                bbox.ExpandToInclude(CenterX, CenterY + Radius);

            // 检查180°点（左侧）
            if (IsAngleInRange(180, normalizedStart, normalizedEnd))
                bbox.ExpandToInclude(CenterX - Radius, CenterY);

            // 检查270°点（底部）
            if (IsAngleInRange(270, normalizedStart, normalizedEnd))
                bbox.ExpandToInclude(CenterX, CenterY - Radius);

            // 检查0°/360°点（右侧）
            if (IsAngleInRange(0, normalizedStart, normalizedEnd) || IsAngleInRange(360, normalizedStart, normalizedEnd))
                bbox.ExpandToInclude(CenterX + Radius, CenterY);

            return bbox;
        }

        // 将角度归一化到[0, 360)范围
        private static double NormalizeAngle(double angle)
        {
            angle = angle % 360;
            if (angle < 0) angle += 360;
            return angle;
        }

        // 判断目标角度是否在起始到终止的角度范围内（逆时针方向）
        private bool IsAngleInRange(double targetAngle, double startAngle, double endAngle)
        {
            if (endAngle >= startAngle)
                return targetAngle >= startAngle && targetAngle <= endAngle;

            // 处理跨越0度的情况（如从300°到60°）
            return targetAngle >= startAngle || targetAngle <= endAngle;
        }
    }
}
