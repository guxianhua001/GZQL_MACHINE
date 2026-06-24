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

        private double _rotatedX;
        /// <summary>旋转后机械 X 坐标（相机中心坐标）</summary>
        public double RotatedX
        {
            get => _rotatedX;
            set => SetProperty(ref _rotatedX, value);
        }

        private double _rotatedY;
        /// <summary>旋转后机械 Y 坐标（相机中心坐标）</summary>
        public double RotatedY
        {
            get => _rotatedY;
            set => SetProperty(ref _rotatedY, value);
        }

        private double _finalX;
        /// <summary>最终点胶针头 X 坐标 = RotatedX + 针头偏移补偿X</summary>
        public double FinalX
        {
            get => _finalX;
            set => SetProperty(ref _finalX, value);
        }

        private double _finalY;
        /// <summary>最终点胶针头 Y 坐标 = RotatedY + 针头偏移补偿Y</summary>
        public double FinalY
        {
            get => _finalY;
            set => SetProperty(ref _finalY, value);
        }
    }
}
