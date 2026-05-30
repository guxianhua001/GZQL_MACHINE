// Core/Models/CadLwPolyline.cs
using System.Drawing;

namespace Core.Models
{
    /// <summary>
    /// 轻量多段线图元（LWPOLYLINE），表示由多个顶点连接而成的折线或多边形
    /// 支持闭合状态和线宽设置
    /// </summary>
    public class CadLwPolyline : CadEntity
    {
        private List<PointF> _vertices = new();
        private bool _isClosed;
        private double _width;
        private List<CadSegment> _segments = new();
        private List<double> _bulges = new();

        /// <summary>
        /// 多段线的顶点列表（使用PointF存储二维坐标）
        /// </summary>
        public List<PointF> Vertices
        {
            get => _vertices;
            set => SetProperty(ref _vertices, value);
        }

        /// <summary>
        /// 是否闭合（true表示首尾相连形成多边形，false表示开放的折线）
        /// </summary>
        public bool IsClosed
        {
            get => _isClosed;
            set => SetProperty(ref _isClosed, value);
        }

        /// <summary>
        /// 多段线的线宽（用于渲染时的线条粗细）
        /// </summary>
        public double Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        /// <summary>
        /// 解析后的子段列表（由bulge计算得到）
        /// 每个元素代表从Vertices[i]到Vertices[i+1]的一段（直线或圆弧）
        /// 如果Bulges数据可用，在ParseLwPolyline结束后调用BuildSegments()填充此列表
        /// </summary>
        public List<CadSegment> Segments
        {
            get => _segments;
            set => SetProperty(ref _segments, value);
        }

        /// <summary>
        /// 原始bulge值列表（与顶点一一对应）
        /// Bulges[i] 表示从 Vertices[i] 到 Vertices[i+1] 的段的凸度
        /// 最后一个顶点的bulge仅对闭合多段线有意义（表示闭合段）
        /// </summary>
        public List<double> Bulges
        {
            get => _bulges;
            set => SetProperty(ref _bulges, value);
        }

        /// <summary>
        /// 根据顶点坐标和Bulges列表构建所有子段
        /// 必须在设置Vertices和Bulges后调用
        /// 将每个bulge值转换为对应的CadSegment（直线或圆弧）对象
        /// </summary>
        public void BuildSegments()
        {
            Segments.Clear();

            if (Vertices == null || Vertices.Count < 2)
                return;

            int segmentCount = Math.Min(Vertices.Count - 1, Bulges.Count > 0 ? Bulges.Count : Vertices.Count - 1);

            for (int i = 0; i < segmentCount; i++)
            {
                double bulge = (i < Bulges.Count) ? Bulges[i] : 0;
                var p1 = Vertices[i];
                var p2 = Vertices[i + 1];

                var segment = CadSegment.CreateFromBulge(p1.X, p1.Y, p2.X, p2.Y, bulge);
                Segments.Add(segment);
            }

            if (IsClosed && Vertices.Count >= 2)
            {
                int lastIdx = Vertices.Count - 1;
                double closingBulge = (Bulges.Count >= Vertices.Count) ? Bulges[lastIdx] : 0;

                var closingSegment = CadSegment.CreateFromBulge(
                    Vertices[lastIdx].X, Vertices[lastIdx].Y,
                    Vertices[0].X, Vertices[0].Y,
                    closingBulge);

                Segments.Add(closingSegment);
            }

            System.Diagnostics.Debug.WriteLine(
                $"[CadLwPolyline.BuildSegments] vertices={Vertices.Count}, " +
                $"bulges={Bulges.Count}, segments={Segments.Count}, " +
                $"arcSegments={Segments.Count(s => s.IsArc)}");
        }

        /// <summary>
        /// 无参构造函数，初始化默认值并设置图元类型为LwPolyline
        /// </summary>
        public CadLwPolyline()
        {
            EntityType = CadEntityType.LwPolyline;
        }

        /// <summary>
        /// 带参数构造函数，指定顶点列表和闭合状态
        /// </summary>
        /// <param name="vertices">顶点坐标列表</param>
        /// <param name="isClosed">是否闭合，默认为false</param>
        /// <param name="width">线宽，默认为0</param>
        public CadLwPolyline(List<PointF> vertices, bool isClosed = false, double width = 0)
        {
            EntityType = CadEntityType.LwPolyline;
            Vertices = vertices ?? new List<PointF>();
            IsClosed = isClosed;
            Width = width;
        }

        /// <summary>
        /// 计算多段线的轴对齐包围盒（包含所有顶点的最小矩形）
        /// </summary>
        /// <returns>多段线的包围盒</returns>
        public override BoundingBox GetBoundingBox()
        {
            var bbox = new BoundingBox();

            if (_vertices == null || _vertices.Count == 0)
                return bbox;

            foreach (var vertex in _vertices)
            {
                bbox.ExpandToInclude(vertex.X, vertex.Y);
            }

            return bbox;
        }
    }
}
