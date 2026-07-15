#if !HAS_HALCON
using System.Collections.Generic;
using Core.Abstraction;
using Core.Models;
using Core.Services;

namespace Module.Services
{
    /// <summary>
    /// ZMAP高度提取服务空实现——Halcon SDK未安装时代替 ZMapHeightExtractionService，
    /// 避免DI容器解析失败。所有方法为空操作或返回失败/默认值，不影响其它现有功能。
    /// </summary>
    public class StubZMapHeightExtractionService : IZMapHeightExtractionService
    {
        public bool IsHeightMapLoaded => false;
        public int HeightMapWidth => 0;
        public int HeightMapHeight => 0;
        public string LoadedFilePath => string.Empty;
        public double InvalidHeightValue { get; set; } = -1.0;
        public double ZOffset { get; set; }
        public AffineCalibrationResult CurrentCalibration => null;

        public bool LoadHeightMap(string filePath, out string error)
        {
            error = "当前环境未安装Halcon SDK，无法使用ZMAP高度提取功能";
            return false;
        }

        public AffineCalibrationResult ComputeCalibration(IList<ZMapCalibrationPoint> calibrationPoints, out string error)
        {
            error = "当前环境未安装Halcon SDK，无法使用ZMAP高度提取功能";
            return null;
        }

        public void SetCalibration(AffineCalibrationResult calibration) { }

        public bool TryGetPixelForMachinePoint(double machineX, double machineY, out double pixelCol, out double pixelRow)
        {
            pixelCol = 0;
            pixelRow = 0;
            return false;
        }

        public bool TrySampleRawHeightAtPixel(double pixelCol, double pixelRow, out double rawZ)
        {
            rawZ = 0;
            return false;
        }

        public bool TrySampleHeightAtMachinePoint(double machineX, double machineY, out double correctedZ)
        {
            correctedZ = 0;
            return false;
        }

        public List<ZMapHeightSampleResult> SampleHeights(IEnumerable<(double MachineX, double MachineY)> machinePoints)
            => new List<ZMapHeightSampleResult>();

        public List<ZMapHeightSampleResult> SamplePixelHeights(IEnumerable<ZMapPixelPoint> pixelPoints)
            => new List<ZMapHeightSampleResult>();

        public void CalibrateZOffset(double referenceMachineZ, double rawZAtReference) { }

        public ZMapCalibrationConfig ExportConfig() => new ZMapCalibrationConfig();

        public void ImportConfig(ZMapCalibrationConfig config) { }

        public object GetDisplayImage() => null;

        public void Unload() { }
    }
}
#endif
