using System;

namespace Core.Extensions
{
    /// <summary>
    /// XY坐标点数据
    /// </summary>
    [Serializable]
    public class Point
    {
        public Point() { }
        public Point(double px, double py)
        {
            this.pX = px;
            this.pY = py;
        }
        private double _px;

        public double pX
        {
            get
            {
                return Math.Round(_px, 3);
            }
            set { _px = value; }
        }
        private double _py;

        public double pY
        {
            get
            {
                return Math.Round(_py, 3);
            }
            set { _py = value; }
        }
    }
    /// <summary>
    /// XYZ坐标点数据
    /// </summary>
    [Serializable]
    public class Point3D
    {
        public Point3D() { }
        public Point3D(double x, double y, double z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }
        private double _x;

        public double X
        {
            get
            {
                return Math.Round(_x, 3);
            }
            set { _x = value; }
        }
        private double _y;

        public double Y
        {
            get
            {
                return Math.Round(_y, 3);
            }
            set { _y = value; }
        }
        private double _z;

        public double Z
        {
            get
            {
                return Math.Round(_z, 3);
            }
            set { _z = value; }
        }
    }
}
