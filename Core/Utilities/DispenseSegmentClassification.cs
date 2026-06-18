using Core.Models;
using System;

namespace Core.Utilities
{
    /// <summary>
    /// DISPENSE 工具段类型分类——区分点模式(Line)与圆弧模式(Arc)可导入/可执行的源段
    /// </summary>
    public static class DispenseSegmentClassification
    {
        /// <summary>
        /// 是否适合圆弧(Arc)模式——Arc/Circle/Ellipse/Spline，及含弧段的多段线（含 OriginalEntityData / SegmentId 回退）
        /// </summary>
        public static bool IsArcCompatible(DispenseSegment segment)
        {
            if (segment == null) return false;

            switch (segment.EntityType)
            {
                case CadEntityType.Arc:
                case CadEntityType.Circle:
                case CadEntityType.Ellipse:
                case CadEntityType.Spline:
                    return true;
                case CadEntityType.LwPolyline:
                case CadEntityType.Polyline:
                    return HasCurvedOriginalEntity(segment);
                default:
                    return false;
            }
        }

        /// <summary>
        /// 是否适合点(Dot)模式线段导入——直线段，以及非圆弧类的折线/多段线
        /// </summary>
        public static bool IsLineCompatible(DispenseSegment segment)
        {
            if (segment == null) return false;
            if (IsArcCompatible(segment)) return false;

            return segment.EntityType is CadEntityType.Line
                or CadEntityType.LwPolyline
                or CadEntityType.Polyline;
        }

        /// <summary>
        /// SegmentRef 是否可在 Arc 模式执行——优先按源段数据判定，回退 SourceEntityType
        /// </summary>
        public static bool IsArcCompatibleRef(DispenseSegmentRef segRef, DispenseSegment source)
        {
            if (source != null)
                return IsArcCompatible(source);

            if (segRef == null) return false;

            return segRef.SourceEntityType is CadEntityType.Arc
                or CadEntityType.Circle
                or CadEntityType.Ellipse
                or CadEntityType.Spline;
        }

        /// <summary>从 OriginalEntityData 或 SegmentId 前缀判断是否为圆弧类图元</summary>
        private static bool HasCurvedOriginalEntity(DispenseSegment segment)
        {
            var originalType = segment.OriginalEntityData?.EntityType;
            if (!string.IsNullOrEmpty(originalType))
            {
                return originalType is "Arc" or "Circle" or "Ellipse" or "Spline";
            }

            var id = segment.SegmentId ?? string.Empty;
            return id.StartsWith("ARC_", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith("CIRC_", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith("ELLIP_", StringComparison.OrdinalIgnoreCase);
        }
    }
}
