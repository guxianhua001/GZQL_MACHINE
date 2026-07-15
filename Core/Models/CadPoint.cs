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

        // 相机示教模式显示用：相机机械坐标（针头机械坐标 - 相机针头固定距离 - NeedleTCP偏差）。
        // 仅在 Step4 启用「使用相机示教」时由 RefreshSegmentMachineCoordinates 计算填充，
        // 供 Step3 采样点表格展示相机坐标列。其它模式下为 null，不影响现有数据。
        private double? _cameraMachineX;
        /// <summary>相机示教模式显示用：相机机械坐标X（= 针头机械X - 相机针头偏移 - 对针补偿X）</summary>
        public double? CameraMachineX { get => _cameraMachineX; set => SetProperty(ref _cameraMachineX, value); }

        private double? _cameraMachineY;
        /// <summary>相机示教模式显示用：相机机械坐标Y（= 针头机械Y - 相机针头偏移 - 对针补偿Y）</summary>
        public double? CameraMachineY { get => _cameraMachineY; set => SetProperty(ref _cameraMachineY, value); }

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

        private bool? _isZMapHeightValid;
        /// <summary>
        /// 最近一次ZMAP提取对该点是否有效：true=已写入有效高度；false=提取失败未覆盖；
        /// null=尚未参与ZMAP提取。Step6 Z向校准据此拦截无效点，防止撞针。
        /// </summary>
        public bool? IsZMapHeightValid
        {
            get => _isZMapHeightValid;
            set => SetProperty(ref _isZMapHeightValid, value);
        }

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