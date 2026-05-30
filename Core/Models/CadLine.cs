// Core/Models/CadLine.cs
namespace Core.Models
{
    /// <summary>
    /// 直线段图元，表示二维或三维空间中的一条直线
    /// </summary>
    public class CadLine : CadEntity
    {
        private double _startX;
        private double _startY;
        private double _startZ;
        private double _endX;
        private double _endY;
        private double _endZ;

        /// <summary>
        /// 起点X坐标
        /// </summary>
        public double StartX
        {
            get => _startX;
            set => SetProperty(ref _startX, value);
        }

        /// <summary>
        /// 起点Y坐标
        /// </summary>
        public double StartY
        {
            get => _startY;
            set => SetProperty(ref _startY, value);
        }

        /// <summary>
        /// 起点Z坐标（三维空间中的高度信息）
        /// </summary>
        public double StartZ
        {
            get => _startZ;
            set => SetProperty(ref _startZ, value);
        }

        /// <summary>
        /// 终点X坐标
        /// </summary>
        public double EndX
        {
            get => _endX;
            set => SetProperty(ref _endX, value);
        }

        /// <summary>
        /// 终点Y坐标
        /// </summary>
        public double EndY
        {
            get => _endY;
            set => SetProperty(ref _endY, value);
        }

        /// <summary>
        /// 终点Z坐标（三维空间中的高度信息）
        /// </summary>
        public double EndZ
        {
            get => _endZ;
            set => SetProperty(ref _endZ, value);
        }

        /// <summary>
        /// 无参构造函数，初始化默认值并设置图元类型为Line
        /// </summary>
        public CadLine()
        {
            EntityType = CadEntityType.Line;
        }

        /// <summary>
        /// 带参数构造函数，直接指定起点和终点坐标
        /// </summary>
        /// <param name="startX">起点X坐标</param>
        /// <param name="startY">起点Y坐标</param>
        /// <param name="endX">终点X坐标</param>
        /// <param name="endY">终点Y坐标</param>
        /// <param name="startZ">起点Z坐标，默认为0</param>
        /// <param name="endZ">终点Z坐标，默认为0</param>
        public CadLine(double startX, double startY, double endX, double endY, double startZ = 0, double endZ = 0)
        {
            EntityType = CadEntityType.Line;
            StartX = startX;
            StartY = startY;
            StartZ = startZ;
            EndX = endX;
            EndY = endY;
            EndZ = endZ;
        }

        /// <summary>
        /// 计算直线的轴对齐包围盒（包含起点和终点的最小矩形）
        /// </summary>
        /// <returns>直线的包围盒</returns>
        public override BoundingBox GetBoundingBox()
        {
            var bbox = new BoundingBox();
            bbox.ExpandToInclude(StartX, StartY);
            bbox.ExpandToInclude(EndX, EndY);
            return bbox;
        }
    }
}
