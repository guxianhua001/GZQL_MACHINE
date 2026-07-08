using Prism.Mvvm;

namespace Core.Models
{
    /// <summary>
    /// 仿射标定点模型——用于N点仿射标定，存储CAD坐标与对应的机械示教坐标
    /// 仿射变换公式: Mx = A·Cx + B·Cy + Tx,  My = C·Cx + D·Cy + Ty
    /// 每个针头独立维护一套标定点数据，切换针头时整体切换
    /// </summary>
    public class AffineCalibrationPoint : BindableBase
    {
        /// <summary>行索引</summary>
        public int Index { get; set; }

        private string _name = "";
        /// <summary>点名标识（如 P1, P2, P3）</summary>
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        private double _cadX;
        /// <summary>CAD图纸X坐标</summary>
        public double CadX { get => _cadX; set => SetProperty(ref _cadX, value); }

        private double _cadY;
        /// <summary>CAD图纸Y坐标</summary>
        public double CadY { get => _cadY; set => SetProperty(ref _cadY, value); }

        private double _machineX;
        /// <summary>机械示教X坐标</summary>
        public double MachineX { get => _machineX; set => SetProperty(ref _machineX, value); }

        private double _machineY;
        /// <summary>机械示教Y坐标</summary>
        public double MachineY { get => _machineY; set => SetProperty(ref _machineY, value); }

        private double _machineDz;
        /// <summary>机械示教Z轴坐标（当前针头）</summary>
        public double MachineDz { get => _machineDz; set => SetProperty(ref _machineDz, value); }

        // 相机示教模式专用：移动相机至目标点时读取的相机机械坐标。
        // 针头机械坐标 = 相机机械坐标 + 相机针头固定距离 + NeedleTCP偏差（NeedleAlignComp）。
        // 默认针头示教模式下这两个字段为 0，不参与仿射求解（Solve 仍使用 MachineX/Y）。
        private double _cameraMachineX;
        /// <summary>相机示教模式：相机机械坐标X（读取 Dx 轴），仅用于显示与追溯</summary>
        public double CameraMachineX { get => _cameraMachineX; set => SetProperty(ref _cameraMachineX, value); }

        private double _cameraMachineY;
        /// <summary>相机示教模式：相机机械坐标Y（读取 Dy 轴），仅用于显示与追溯</summary>
        public double CameraMachineY { get => _cameraMachineY; set => SetProperty(ref _cameraMachineY, value); }

        private double _residual;
        /// <summary>该点的标定残差(mm)，标定计算后填充</summary>
        public double Residual { get => _residual; set => SetProperty(ref _residual, value); }
    }
}
