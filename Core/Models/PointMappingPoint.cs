using Prism.Mvvm;

namespace Core.Models
{
    /// <summary>
    /// 逐点映射点位模型——用于逐点映射模式，存储每个CAD点对应的机械坐标
    /// 每个针头独立维护一套映射点数据，切换针头时整体切换
    /// </summary>
    public class PointMappingPoint : BindableBase
    {
        /// <summary>行索引</summary>
        public int Index { get; set; }

        private string _name = "";
        /// <summary>点名标识（如 P1, P2）</summary>
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        private double _cadX;
        /// <summary>CAD图纸X坐标（画布选取）</summary>
        public double CadX { get => _cadX; set => SetProperty(ref _cadX, value); }

        private double _cadY;
        /// <summary>CAD图纸Y坐标（画布选取）</summary>
        public double CadY { get => _cadY; set => SetProperty(ref _cadY, value); }

        private double _machineDx;
        /// <summary>机械示教Dx轴坐标</summary>
        public double MachineDx { get => _machineDx; set => SetProperty(ref _machineDx, value); }

        private double _machineDy;
        /// <summary>机械示教Dy轴坐标</summary>
        public double MachineDy { get => _machineDy; set => SetProperty(ref _machineDy, value); }

        private double _machineDz;
        /// <summary>机械示教Dz轴坐标（当前针头）</summary>
        public double MachineDz { get => _machineDz; set => SetProperty(ref _machineDz, value); }
    }
}
