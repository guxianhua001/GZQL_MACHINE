using System;

namespace Core.Models
{
    public struct Vector3
    {
        public double X, Y, Z;
        public Vector3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3 operator *(Matrix3x3 m, Vector3 v) => new Vector3(
            m.M11 * v.X + m.M12 * v.Y + m.M13 * v.Z,
            m.M21 * v.X + m.M22 * v.Y + m.M23 * v.Z,
            m.M31 * v.X + m.M32 * v.Y + m.M33 * v.Z
        );
        public override string ToString() => $"({X:F3}, {Y:F3}, {Z:F3})";
    }
}