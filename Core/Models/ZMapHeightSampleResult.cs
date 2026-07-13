using Prism.Mvvm;

namespace Core.Models
{
    /// <summary>
    /// ZMAP高度提取——单个轨迹采样点的提取结果，用于悬浮窗口内预览表格展示，
    /// 用户确认后才由调用方（Step3面板）写回 CadPoint.Z。
    /// </summary>
    public class ZMapHeightSampleResult : BindableBase
    {
        private int _index;
        /// <summary>点序号（对应轨迹段内采样点顺序，从1开始）</summary>
        public int Index { get => _index; set => SetProperty(ref _index, value); }

        private double _machineX;
        /// <summary>该点的机械坐标X（用于反查ZMAP像素位置的输入）</summary>
        public double MachineX { get => _machineX; set => SetProperty(ref _machineX, value); }

        private double _machineY;
        /// <summary>该点的机械坐标Y（用于反查ZMAP像素位置的输入）</summary>
        public double MachineY { get => _machineY; set => SetProperty(ref _machineY, value); }

        private double _pixelCol;
        /// <summary>反查得到的ZMAP像素列坐标</summary>
        public double PixelCol { get => _pixelCol; set => SetProperty(ref _pixelCol, value); }

        private double _pixelRow;
        /// <summary>反查得到的ZMAP像素行坐标</summary>
        public double PixelRow { get => _pixelRow; set => SetProperty(ref _pixelRow, value); }

        private double _rawZ;
        /// <summary>ZMAP双线性采样得到的原始高度值（未叠加ZOffset基准修正）</summary>
        public double RawZ { get => _rawZ; set => SetProperty(ref _rawZ, value); }

        private double _correctedZ;
        /// <summary>叠加ZOffset基准修正后的最终高度值——即将写入CadPoint.Z的值</summary>
        public double CorrectedZ { get => _correctedZ; set => SetProperty(ref _correctedZ, value); }

        private bool _isValid;
        /// <summary>是否为有效高度（像素落在图像范围内，且灰度值不等于无效值）</summary>
        public bool IsValid { get => _isValid; set => SetProperty(ref _isValid, value); }

        private string _errorMessage = string.Empty;
        /// <summary>无效时的原因说明（如"超出图像范围""机械坐标未知"）</summary>
        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }
    }
}
