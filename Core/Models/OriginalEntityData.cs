using System.Collections.Generic;

namespace Core.Models
{
    /// <summary>
    /// 原始图元序列化数据——保存原始CAD图元的类型和几何参数
    /// 用于保存/加载轨迹段时恢复原始图元形状（如弧线、圆等）
    /// </summary>
    public class OriginalEntityData
    {
        /// <summary>图元类型名称（"Line"/"Arc"/"Circle"/"Ellipse"/"LwPolyline"）</summary>
        public string EntityType { get; set; }

        /// <summary>图层名称</summary>
        public string LayerName { get; set; }

        /// <summary>颜色（ARGB十六进制格式）</summary>
        public string Color { get; set; }

        /// <summary>几何参数键值对（如 CenterX, CenterY, Radius, StartAngle, EndAngle 等）</summary>
        public Dictionary<string, double> Parameters { get; set; } = new();

        /// <summary>
        /// 从 CadEntity 提取序列化数据
        /// </summary>
        public static OriginalEntityData FromEntity(CadEntity entity)
        {
            if (entity == null) return null;

            var data = new OriginalEntityData
            {
                EntityType = entity.EntityType.ToString(),
                LayerName = entity.LayerName,
                Color = entity.Color
            };

            switch (entity)
            {
                case CadLine line:
                    data.Parameters["StartX"] = line.StartX;
                    data.Parameters["StartY"] = line.StartY;
                    data.Parameters["StartZ"] = line.StartZ;
                    data.Parameters["EndX"] = line.EndX;
                    data.Parameters["EndY"] = line.EndY;
                    data.Parameters["EndZ"] = line.EndZ;
                    break;

                case CadArc arc:
                    data.Parameters["CenterX"] = arc.CenterX;
                    data.Parameters["CenterY"] = arc.CenterY;
                    data.Parameters["CenterZ"] = arc.CenterZ;
                    data.Parameters["Radius"] = arc.Radius;
                    data.Parameters["StartAngle"] = arc.StartAngle;
                    data.Parameters["EndAngle"] = arc.EndAngle;
                    break;

                case CadCircle circle:
                    data.Parameters["CenterX"] = circle.CenterX;
                    data.Parameters["CenterY"] = circle.CenterY;
                    data.Parameters["CenterZ"] = circle.CenterZ;
                    data.Parameters["Radius"] = circle.Radius;
                    break;

                case CadEllipse ellipse:
                    data.Parameters["CenterX"] = ellipse.CenterX;
                    data.Parameters["CenterY"] = ellipse.CenterY;
                    data.Parameters["CenterZ"] = ellipse.CenterZ;
                    data.Parameters["MajorAxisLength"] = ellipse.MajorAxisLength;
                    data.Parameters["MinorAxisLength"] = ellipse.MinorAxisLength;
                    data.Parameters["RotationAngle"] = ellipse.RotationAngle;
                    data.Parameters["StartAngle"] = ellipse.StartAngle;
                    data.Parameters["EndAngle"] = ellipse.EndAngle;
                    break;
            }

            return data;
        }

        /// <summary>
        /// 从序列化数据重建 CadEntity
        /// </summary>
        public CadEntity ToEntity()
        {
            CadEntity entity = EntityType switch
            {
                "Line" => new CadLine(
                    Parameters.GetValueOrDefault("StartX"),
                    Parameters.GetValueOrDefault("StartY"),
                    Parameters.GetValueOrDefault("EndX"),
                    Parameters.GetValueOrDefault("EndY"),
                    Parameters.GetValueOrDefault("StartZ"),
                    Parameters.GetValueOrDefault("EndZ")),

                "Arc" => new CadArc(
                    Parameters.GetValueOrDefault("CenterX"),
                    Parameters.GetValueOrDefault("CenterY"),
                    Parameters.GetValueOrDefault("Radius"),
                    Parameters.GetValueOrDefault("StartAngle"),
                    Parameters.GetValueOrDefault("EndAngle"),
                    Parameters.GetValueOrDefault("CenterZ")),

                "Circle" => new CadCircle(
                    Parameters.GetValueOrDefault("CenterX"),
                    Parameters.GetValueOrDefault("CenterY"),
                    Parameters.GetValueOrDefault("Radius"),
                    Parameters.GetValueOrDefault("CenterZ")),

                "Ellipse" => new CadEllipse(
                    Parameters.GetValueOrDefault("CenterX"),
                    Parameters.GetValueOrDefault("CenterY"),
                    Parameters.GetValueOrDefault("MajorAxisLength"),
                    Parameters.GetValueOrDefault("MinorAxisLength"),
                    Parameters.GetValueOrDefault("RotationAngle"),
                    Parameters.GetValueOrDefault("StartAngle"),
                    Parameters.GetValueOrDefault("EndAngle"),
                    Parameters.GetValueOrDefault("CenterZ")),

                _ => null
            };

            if (entity != null)
            {
                entity.LayerName = LayerName ?? "0";
                entity.Color = Color ?? "#FF000000";
            }

            return entity;
        }
    }
}
