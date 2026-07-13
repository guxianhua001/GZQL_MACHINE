using Prism.Mvvm;

namespace Core.Models
{
    /// <summary>
    /// ZMAP标定点——建立"ZMAP高度图像素坐标(PixelCol,PixelRow)"与"机械坐标(MachineX,MachineY)"的对应关系。
    /// 多个标定点(≥3个,不共线)通过 AffineCalibrationService.Solve 求解出仿射矩阵，
    /// 用于后续按机械坐标反查ZMAP像素位置、提取该处的Z高度。
    /// </summary>
    public class ZMapCalibrationPoint : BindableBase
    {
        private int _id;
        /// <summary>标定点序号</summary>
        public int Id { get => _id; set => SetProperty(ref _id, value); }

        private double _pixelCol;
        /// <summary>ZMAP图像像素列坐标（对应Halcon图像坐标的Col/X）</summary>
        public double PixelCol { get => _pixelCol; set => SetProperty(ref _pixelCol, value); }

        private double _pixelRow;
        /// <summary>ZMAP图像像素行坐标（对应Halcon图像坐标的Row/Y）</summary>
        public double PixelRow { get => _pixelRow; set => SetProperty(ref _pixelRow, value); }

        private double _machineX;
        /// <summary>该标定点对应的机械坐标X（mm）</summary>
        public double MachineX { get => _machineX; set => SetProperty(ref _machineX, value); }

        private double _machineY;
        /// <summary>该标定点对应的机械坐标Y（mm）</summary>
        public double MachineY { get => _machineY; set => SetProperty(ref _machineY, value); }

        private string _note = string.Empty;
        /// <summary>备注（如Mark点编号、治具基准点说明等）</summary>
        public string Note { get => _note; set => SetProperty(ref _note, value); }
    }
}
