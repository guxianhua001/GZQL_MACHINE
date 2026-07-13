#if HAS_HALCON
using Core.Abstraction;
using Core.Models;
using Core.Services;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Module.Services
{
    /// <summary>
    /// ZMAP高度图Z值提取服务（Halcon实现）——负责加载单通道ZMAP高度图（灰度值即高度mm），
    /// 用"像素↔机械"标定点求解仿射矩阵，并按机械坐标反查像素位置、双线性采样得到Z高度。
    /// 详细对齐原理见 <see cref="IZMapHeightExtractionService"/> 接口注释。
    /// </summary>
    public class ZMapHeightExtractionService : IZMapHeightExtractionService
    {
        private HImage _heightMap;
        private int _width;
        private int _height;
        private string _previewImagePath;
        private List<ZMapCalibrationPoint> _lastCalibrationPoints = new List<ZMapCalibrationPoint>();

        public bool IsHeightMapLoaded => _heightMap != null;
        public int HeightMapWidth => _width;
        public int HeightMapHeight => _height;
        public string LoadedFilePath { get; private set; } = string.Empty;
        public string PreviewImagePath => _previewImagePath ?? string.Empty;
        public double InvalidHeightValue { get; set; } = -1.0;
        public double ZOffset { get; set; }
        public AffineCalibrationResult CurrentCalibration { get; private set; }

        /// <summary>
        /// 加载ZMAP高度图（要求单通道图像，灰度值即高度）。
        /// 加载成功后额外生成一张归一化灰度PNG供悬浮窗口预览显示（不影响原始浮点数据的采样精度）。
        /// </summary>
        public bool LoadHeightMap(string filePath, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                error = "文件不存在";
                return false;
            }

            try
            {
                var image = new HImage(filePath);
                int channels = image.CountChannels().I;
                if (channels != 1)
                {
                    error = $"ZMAP高度图须为单通道图像，当前通道数={channels}";
                    return false;
                }

                image.GetImageSize(out HTuple w, out HTuple h);

                Unload();

                _heightMap = image;
                _width = w.I;
                _height = h.I;
                LoadedFilePath = filePath;
                GeneratePreviewImage();
                return true;
            }
            catch (Exception ex)
            {
                error = $"加载ZMAP高度图失败: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// 生成归一化灰度PNG预览图（min-max拉伸至0-255），仅用于UI显示，不参与实际Z值计算。
        /// </summary>
        private void GeneratePreviewImage()
        {
            try
            {
                var scaled = _heightMap.ScaleImageMax();
                var tempPath = Path.Combine(Path.GetTempPath(), $"zmap_preview_{Guid.NewGuid():N}.png");
                scaled.WriteImage("png", 0, tempPath);
                _previewImagePath = tempPath;
            }
            catch
            {
                // 预览图生成失败不影响主流程（仅UI展示受限），采样功能仍可用
                _previewImagePath = null;
            }
        }

        public AffineCalibrationResult ComputeCalibration(IList<ZMapCalibrationPoint> calibrationPoints, out string error)
        {
            error = null;
            try
            {
                if (calibrationPoints == null || calibrationPoints.Count < 3)
                {
                    error = "标定至少需要3个不共线的标定点";
                    return null;
                }

                var pixelPoints = calibrationPoints.Select(p => (p.PixelCol, p.PixelRow)).ToList();
                var machinePoints = calibrationPoints.Select(p => (p.MachineX, p.MachineY)).ToList();
                var result = AffineCalibrationService.Solve(pixelPoints, machinePoints);

                CurrentCalibration = result;
                _lastCalibrationPoints = calibrationPoints.ToList();
                return result;
            }
            catch (Exception ex)
            {
                error = $"标定求解失败: {ex.Message}";
                return null;
            }
        }

        public void SetCalibration(AffineCalibrationResult calibration)
        {
            CurrentCalibration = calibration;
        }

        public bool TryGetPixelForMachinePoint(double machineX, double machineY, out double pixelCol, out double pixelRow)
        {
            pixelCol = 0;
            pixelRow = 0;
            if (CurrentCalibration == null)
                return false;

            try
            {
                var (col, row) = AffineCalibrationService.InverseTransform(CurrentCalibration, machineX, machineY);
                pixelCol = col;
                pixelRow = row;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TrySampleRawHeightAtPixel(double pixelCol, double pixelRow, out double rawZ)
        {
            rawZ = 0;
            if (_heightMap == null)
                return false;

            // 越界（含亚像素边缘）直接判定无效，避免外插产生错误高度
            if (pixelCol < 0 || pixelRow < 0 || pixelCol > _width - 1 || pixelRow > _height - 1)
                return false;

            int c0 = (int)Math.Floor(pixelCol);
            int r0 = (int)Math.Floor(pixelRow);
            int c1 = Math.Min(c0 + 1, _width - 1);
            int r1 = Math.Min(r0 + 1, _height - 1);
            double fc = pixelCol - c0;
            double fr = pixelRow - r0;

            double g00 = _heightMap.GetGrayval(r0, c0).D;
            double g01 = _heightMap.GetGrayval(r0, c1).D;
            double g10 = _heightMap.GetGrayval(r1, c0).D;
            double g11 = _heightMap.GetGrayval(r1, c1).D;

            // 双线性插值的四个邻域像素中任一为"无效值"标记，则整体判定为无效，
            // 避免无效标记值(如-1)混入插值污染真实高度数据
            if (IsInvalidRaw(g00) || IsInvalidRaw(g01) || IsInvalidRaw(g10) || IsInvalidRaw(g11))
                return false;

            double top = g00 * (1 - fc) + g01 * fc;
            double bottom = g10 * (1 - fc) + g11 * fc;
            rawZ = top * (1 - fr) + bottom * fr;
            return true;
        }

        private bool IsInvalidRaw(double grayVal) => Math.Abs(grayVal - InvalidHeightValue) < 1e-6;

        public bool TrySampleHeightAtMachinePoint(double machineX, double machineY, out double correctedZ)
        {
            correctedZ = 0;
            if (!TryGetPixelForMachinePoint(machineX, machineY, out double col, out double row))
                return false;
            if (!TrySampleRawHeightAtPixel(col, row, out double rawZ))
                return false;

            correctedZ = rawZ + ZOffset;
            return true;
        }

        public List<ZMapHeightSampleResult> SampleHeights(IEnumerable<(double MachineX, double MachineY)> machinePoints)
        {
            var results = new List<ZMapHeightSampleResult>();
            if (machinePoints == null) return results;

            int index = 0;
            foreach (var pt in machinePoints)
            {
                index++;
                var item = new ZMapHeightSampleResult
                {
                    Index = index,
                    MachineX = pt.MachineX,
                    MachineY = pt.MachineY
                };

                if (!TryGetPixelForMachinePoint(pt.MachineX, pt.MachineY, out double col, out double row))
                {
                    item.IsValid = false;
                    item.ErrorMessage = "未标定或坐标反查失败";
                    results.Add(item);
                    continue;
                }

                item.PixelCol = Math.Round(col, 2);
                item.PixelRow = Math.Round(row, 2);

                if (!TrySampleRawHeightAtPixel(col, row, out double rawZ))
                {
                    item.IsValid = false;
                    item.ErrorMessage = "超出图像范围或高度无效";
                    results.Add(item);
                    continue;
                }

                item.RawZ = Math.Round(rawZ, 3);
                item.CorrectedZ = Math.Round(rawZ + ZOffset, 3);
                item.IsValid = true;
                results.Add(item);
            }

            return results;
        }

        public void CalibrateZOffset(double referenceMachineZ, double rawZAtReference)
        {
            ZOffset = referenceMachineZ - rawZAtReference;
        }

        public ZMapCalibrationConfig ExportConfig()
        {
            return new ZMapCalibrationConfig
            {
                CalibrationPoints = _lastCalibrationPoints.Select(p => new ZMapCalibrationPoint
                {
                    Id = p.Id,
                    PixelCol = p.PixelCol,
                    PixelRow = p.PixelRow,
                    MachineX = p.MachineX,
                    MachineY = p.MachineY,
                    Note = p.Note
                }).ToList(),
                Calibration = CurrentCalibration,
                ZOffset = ZOffset,
                InvalidHeightValue = InvalidHeightValue,
                LastHeightMapFilePath = LoadedFilePath
            };
        }

        public void ImportConfig(ZMapCalibrationConfig config)
        {
            if (config == null) return;

            _lastCalibrationPoints = config.CalibrationPoints ?? new List<ZMapCalibrationPoint>();
            CurrentCalibration = config.Calibration;
            ZOffset = config.ZOffset;
            InvalidHeightValue = config.InvalidHeightValue;
        }

        public void Unload()
        {
            try
            {
                _heightMap?.Dispose();
            }
            catch
            {
                // 忽略释放异常，避免影响窗口关闭流程
            }
            _heightMap = null;
            _width = 0;
            _height = 0;
            LoadedFilePath = string.Empty;

            if (!string.IsNullOrEmpty(_previewImagePath) && File.Exists(_previewImagePath))
            {
                try { File.Delete(_previewImagePath); } catch { /* 临时文件删除失败不影响主流程 */ }
            }
            _previewImagePath = null;
        }
    }
}
#endif
