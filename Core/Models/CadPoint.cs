// Core/Models/CadPoint.cs
using Prism.Mvvm;

namespace Core.Models
{
    public class CadPoint : BindableBase
    {
        private string _id;
        private double _x;
        private double _y;
        private double _z;
        private string _name;
        private string _assySite;

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public double X
        {
            get => _x;
            set => SetProperty(ref _x, value);
        }

        public double Y
        {
            get => _y;
            set => SetProperty(ref _y, value);
        }

        public double Z
        {
            get => _z;
            set => SetProperty(ref _z, value);
        }
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        public string AssySite
        {
            get => _assySite;
            set => SetProperty(ref _assySite, value);
        }

        // 机械坐标（坐标对齐后计算得出，未对齐时为 null）
        private double? _machineX;
        /// <summary>
        /// 机械坐标X（坐标对齐后计算得出，未对齐时为 null）
        /// </summary>
        public double? MachineX { get => _machineX; set => SetProperty(ref _machineX, value); }

        private double? _machineY;
        /// <summary>
        /// 机械坐标Y（坐标对齐后计算得出，未对齐时为 null）
        /// </summary>
        public double? MachineY { get => _machineY; set => SetProperty(ref _machineY, value); }

        private double? _machineZ;
        /// <summary>
        /// 机械坐标Z（坐标对齐后计算得出，未对齐时为 null）
        /// </summary>
        public double? MachineZ { get => _machineZ; set => SetProperty(ref _machineZ, value); }

        /// <summary>偏移后坐标X（CAD_X + 全局偏移ΔX，步骤2完成后有值）</summary>
        private double? _offsetX;
        public double? OffsetX { get => _offsetX; set => SetProperty(ref _offsetX, value); }

        /// <summary>偏移后坐标Y（CAD_Y + 全局偏移ΔY，步骤2完成后有值）</summary>
        private double? _offsetY;
        public double? OffsetY { get => _offsetY; set => SetProperty(ref _offsetY, value); }

        /// <summary>Halcon图像像素行号（由CadToImage转换，FitToAll后有值）</summary>
        private double? _imageRow;
        public double? ImageRow { get => _imageRow; set => SetProperty(ref _imageRow, value); }

        /// <summary>Halcon图像像素列号（由CadToImage转换，FitToAll后有值）</summary>
        private double? _imageCol;
        public double? ImageCol { get => _imageCol; set => SetProperty(ref _imageCol, value); }
        // 无参构造函数（供对象初始化器使用）
        public CadPoint()
        {
            X = 0;
            Y = 0;
            Z = 0;
            Id = string.Empty;
            AssySite = "ASSY_001";
            Name = string.Empty;
        }
        public CadPoint(double x, double y, double z, string id = null, string assySite = "ASSY_001", string name = null)
        {
            X = x;
            Y = y;
            Z = z;
            Id = id ?? string.Empty;
            AssySite = assySite;
            Name = name ?? string.Empty;
        }

    }
}