// Core/Models/DispenseRotatedCoordItem.cs
using Prism.Mvvm;

namespace Core.Models
{
    /// <summary>
    /// 旋转后坐标对照项——用于弹窗展示单个点的 CAD 原始坐标与旋转后机械坐标
    /// </summary>
    public class DispenseRotatedCoordItem : BindableBase
    {
        /// <summary>所属段名称</summary>
        public string SegmentName { get; set; }

        /// <summary>点序号（从1开始）</summary>
        public int PointIndex { get; set; }

        /// <summary>CAD 原始 X 坐标</summary>
        public double CadX { get; set; }

        /// <summary>CAD 原始 Y 坐标</summary>
        public double CadY { get; set; }

        /// <summary>旋转后机械 X 坐标</summary>
        public double RotatedX { get; set; }

        /// <summary>旋转后机械 Y 坐标</summary>
        public double RotatedY { get; set; }
    }
}
