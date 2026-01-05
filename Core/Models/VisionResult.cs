namespace Core.Models
{
    /// <summary>
    /// 视觉数据结果结构体
    /// </summary>
    public struct VisionResult
    {
        public string Camera { get; set; }
        public bool Success { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public double OffsetX2 { get; set; }
        public double OffsetY2 { get; set; }
        public double OffsetU { get; set; }
        public double OffsetH { get; set; }
        public double OffsetU2 { get; set; }
        public double OffsetH2 { get; set; }
        public string RawData { get; set; }
        public string Message { get; set; }
        public double CenterX { get; set; }
        public double CenterY { get; set; }

        /// <summary>
        /// 检测到的点列表（新格式支持多点）
        /// </summary>
        public List<PointResult> Points { get; set; }

        /// <summary>
        /// 获取第一个点（便捷方法）
        /// </summary>
        public PointResult FirstPoint => Points?.FirstOrDefault();

        /// <summary>
        /// 获取指定索引的点（从1开始）
        /// </summary>
        public PointResult GetPoint(int pointIndex)
        {
            return Points?.FirstOrDefault(p => p.PointIndex == pointIndex);
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public VisionResult()
        {
            Points = new List<PointResult>();
        }

        /// <summary>
        /// 获取所有点的坐标字符串表示
        /// </summary>
        private string GetPointsString()
        {
            if (Points == null || Points.Count == 0)
                return "无点数据";

            var pointStrings = Points.Select(p =>
                $"Point{p.PointIndex}=({p.X:F3},{p.Y:F3})");

            return string.Join(", ", pointStrings);
        }

        /// <summary>
        /// 获取简化的点信息（用于调试显示）
        /// </summary>
        private string GetPointsSummary()
        {
            if (Points == null || Points.Count == 0)
                return "0个点";

            return $"{Points.Count}个点";
        }

        public override string ToString()
        {
            var pointsInfo = GetPointsSummary();

            return $"Camera={Camera}, Success={Success}, " +
                   $"CenterX={CenterX:F3}, CenterY={CenterY:F3}, " +
                   $"OffsetX={OffsetX:F3}, OffsetY={OffsetY:F3}, " +
                   $"OffsetX2={OffsetX2:F3}, OffsetY2={OffsetY2:F3}, " +
                   $"OffsetU={OffsetU:F3}, OffsetH={OffsetH:F3}, " +
                   $"OffsetU2={OffsetU:F3}, OffsetH2={OffsetH:F3}, " +
                   $"Points={pointsInfo}, Message={Message}";
        }

        /// <summary>
        /// 获取详细字符串表示（包含所有点坐标）
        /// </summary>
        public string ToDetailedString()
        {
            var pointsInfo = GetPointsString();

            return $"Camera={Camera}, Success={Success}, " +
                   $"CenterX={CenterX:F3}, CenterY={CenterY:F3}, " +
                   $"OffsetX={OffsetX:F3}, OffsetY={OffsetY:F3}, " +
                   $"OffsetX2={OffsetX2:F3}, OffsetY2={OffsetY2:F3}, " +
                   $"OffsetU={OffsetU:F3}, OffsetH={OffsetH:F3}, " +
                   $"Points=[{pointsInfo}], Message={Message}";
        }

        /// <summary>
        /// 获取简短摘要（用于日志）
        /// </summary>
        public string ToShortString()
        {
            var pointsInfo = GetPointsSummary();

            return $"Camera:{Camera}, Success:{Success}, " +
                   $"Center:({CenterX:F3},{CenterY:F3}), " +
                   $"Points:{pointsInfo}";
        }
    }

    /// <summary>
    /// 点检测结果
    /// </summary>
    public class PointResult
    {
        /// <summary>
        /// 点索引（从1开始）
        /// </summary>
        public int PointIndex { get; set; }

        /// <summary>
        /// X坐标
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Y坐标
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// 点的标签/名称
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// 点的置信度/分数
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// 点的角度
        /// </summary>
        public double Angle { get; set; }

        /// <summary>
        /// 转换为字符串表示
        /// </summary>
        public override string ToString()
        {
            var labelInfo = !string.IsNullOrEmpty(Label) ? $", Label={Label}" : "";
            var scoreInfo = Score > 0 ? $", Score={Score:F3}" : "";
            var angleInfo = Math.Abs(Angle) > 0.001 ? $", Angle={Angle:F3}" : "";

            return $"Point{PointIndex}=({X:F3},{Y:F3}{labelInfo}{scoreInfo}{angleInfo})";
        }

        /// <summary>
        /// 获取简短表示
        /// </summary>
        public string ToShortString()
        {
            return $"({X:F3},{Y:F3})";
        }
    }

    /// <summary>
    /// 视觉系统结果类
    /// </summary>
    public class VisionSystemResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public double XPosition { get; set; }
        public double YPosition { get; set; }
    }
}