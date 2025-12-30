using System;

namespace Core.Models
{
    /// <summary>
    /// 带Z坐标的点结构
    /// </summary>
    public class PointF
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public PointF() { }

        public PointF(float x, float y)
        {
            X = x;
            Y = y;
            Z = 0;
        }

        public PointF(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString()
        {
            return $"X:{X:F3}, Y:{Y:F3}, Z:{Z:F3}";
        }
    }
}
