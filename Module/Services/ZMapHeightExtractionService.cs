#if HAS_HALCON
using Core.Abstraction;
using Core.Models;
using Core.Services;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using VisionTools.Tools.GrabImage;

namespace Module.Services
{
    /// <summary>
    /// ZMAP高度图Z值提取服务——在主进程(.NET 9)内直接读取单通道高度图（灰度值即高度mm），
    /// 用"像素↔机械"标定点求解仿射矩阵，并按机械坐标反查像素位置、双线性采样得到Z高度。
    ///
    /// 读取策略：所有支持格式均由 HALCON 原生 HImage.ReadImage 在主进程读取。
    ///   ① .tif/.tiff：必须为单通道 32 位 real 浮点图，像素值即高度；
    ///   ② .png/.jpg/.bmp：转为单通道 real，灰度值(0~255)作为测试高度。
    /// 读取得到行优先 float[] 高度数组用于采样，并据此构建 real HImage 供
    /// VMHWindowControl 在悬浮窗内直接显示（详见 <see cref="IZMapHeightExtractionService"/>）。
    /// </summary>
    public class ZMapHeightExtractionService : IZMapHeightExtractionService
    {
        private readonly IGrabImageReader _grabImageReader;
        private float[] _heightData;
        private int _width;
        private int _height;
        // 供窗口显示的高度图（real单通道，进程内构建，随Unload释放）
        private HImage _displayImage;
        private List<ZMapCalibrationPoint> _lastCalibrationPoints = new List<ZMapCalibrationPoint>();

        public bool IsHeightMapLoaded => _heightData != null;
        public int HeightMapWidth => _width;
        public int HeightMapHeight => _height;
        public string LoadedFilePath { get; private set; } = string.Empty;
        public double InvalidHeightValue { get; set; } = -1.0;
        public double ZOffset { get; set; }
        public AffineCalibrationResult CurrentCalibration { get; private set; }

        /// <summary>复用 GrabImage 的文件读图服务，保证 ZMap 与未来视觉流程使用相同原生读图入口。</summary>
        public ZMapHeightExtractionService(IGrabImageReader grabImageReader)
        {
            _grabImageReader = grabImageReader ?? throw new ArgumentNullException(nameof(grabImageReader));
        }

        /// <summary>
        /// 加载ZMAP高度图（要求单通道，灰度值即高度）。TIFF使用 HALCON 原生读取，
        /// 再复制为托管数组供标定与快速采样使用。
        /// </summary>
        public bool LoadHeightMap(string filePath, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                error = "ZMap_LoadError_FileNotFound";
                return false;
            }

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            bool isTiff = ext == ".tif" || ext == ".tiff";
            bool isCommon = ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp";
            if (!isTiff && !isCommon)
            {
                error = "ZMap_LoadError_UnsupportedFormat";
                return false;
            }

            try
            {
                float[] pixels;
                int width;
                int height;

                var readResult = _grabImageReader.ReadFile(filePath);
                if (!readResult.IsSuccess)
                {
                    error = MapReadError(readResult.ErrorCode);
                    return false;
                }

                using (readResult.Image)
                {
                    if (isTiff)
                    {
                        // 与后续视觉工具统一使用 HALCON 原生读图；仅接受ZMap定义的单通道real高度图。
                        if (!TryReadNativeTiffHeightMap(readResult.Image.Image, out width, out height, out pixels, out error))
                            return false;
                    }
                    else
                    {
                        if (!TryReadCommonImage(readResult.Image.Image, out width, out height, out pixels, out error))
                            return false;
                    }
                }

                if (pixels == null || width <= 0 || height <= 0 || (long)width * height != pixels.Length)
                {
                    error = "ZMap_LoadError_InvalidSize";
                    return false;
                }

                // 先释放旧状态，再写入新数据（Unload会清空_heightData/LoadedFilePath并释放旧显示图）
                Unload();
                _heightData = pixels;
                _width = width;
                _height = height;
                LoadedFilePath = filePath;
                _displayImage = BuildDisplayImage(pixels, width, height);
                return true;
            }
            catch (Exception)
            {
                error = "ZMap_LoadError_ReadFailed";
                return false;
            }
        }

        /// <summary>
        /// 对 GrabImageReader 已读取的 HALCON 图像校验 ZMap 浮点 TIFF，并复制为托管数组。
        /// 严格校验单通道 real，避免将普通 TIFF 的灰度值误用作真实高度。
        /// </summary>
        private static bool TryReadNativeTiffHeightMap(
            HImage image, out int width, out int height, out float[] pixels, out string error)
        {
            width = 0;
            height = 0;
            pixels = null;
            error = null;

            try
            {
                image.GetImageSize(out width, out height);

                if (image.CountChannels().I != 1)
                {
                    error = "ZMap_LoadError_RequiresSingleChannel";
                    return false;
                }

                string pixelType = image.GetImageType().ToString().Trim().Trim('"');
                if (!string.Equals(pixelType, "real", StringComparison.OrdinalIgnoreCase))
                {
                    error = "ZMap_LoadError_RequiresReal";
                    return false;
                }

                pixels = ExtractRealPixels(image, width, height);
                return true;
            }
            catch (Exception)
            {
                error = "ZMap_LoadError_ReadFailed";
                return false;
            }
        }

        /// <summary>
        /// 将 GrabImageReader 已读取的普通图片转为行优先real高度数组（多通道自动转灰度）。
        /// </summary>
        private static bool TryReadCommonImage(
            HImage raw, out int width, out int height, out float[] pixels, out string error)
        {
            width = 0;
            height = 0;
            pixels = null;
            error = null;

            HImage gray = null;
            HImage real = null;
            try
            {
                int channels = raw.CountChannels().I;
                gray = channels >= 3 ? raw.Rgb1ToGray() : raw.CopyImage();
                real = gray.ConvertImageType("real");
                real.GetImageSize(out width, out height);
                pixels = ExtractRealPixels(real, width, height);
                return true;
            }
            catch (Exception)
            {
                error = "ZMap_LoadError_ReadFailed";
                return false;
            }
            finally
            {
                try { gray?.Dispose(); } catch { }
                try { real?.Dispose(); } catch { }
            }
        }

        /// <summary>把读图服务错误转换为界面资源键，避免将 HALCON 异常文本直接展示给操作员。</summary>
        private static string MapReadError(VisionImageReadErrorCode errorCode)
        {
            switch (errorCode)
            {
                case VisionImageReadErrorCode.FileNotFound:
                    return "ZMap_LoadError_FileNotFound";
                case VisionImageReadErrorCode.UnsupportedFileType:
                    return "ZMap_LoadError_UnsupportedFormat";
                default:
                    return "ZMap_LoadError_ReadFailed";
            }
        }

        /// <summary>从real单通道HImage中拷出行优先float像素数组。</summary>
        private static float[] ExtractRealPixels(HImage real, int width, int height)
        {
            IntPtr ptr = real.GetImagePointer1(out HTuple _, out HTuple _, out HTuple _);
            int count = width * height;
            float[] data = new float[count];
            Marshal.Copy(ptr, data, 0, count);
            return data;
        }

        /// <summary>由行优先float高度数组构建real单通道HImage（gen_image1内部拷贝，构建后可释放托管数组固定）。</summary>
        private static HImage BuildDisplayImage(float[] pixels, int width, int height)
        {
            GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                return new HImage("real", width, height, handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>供窗口显示的real高度图（装箱HImage，未加载时null）。</summary>
        public object GetDisplayImage() => _displayImage;

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
            if (_heightData == null)
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

            double g00 = _heightData[r0 * _width + c0];
            double g01 = _heightData[r0 * _width + c1];
            double g10 = _heightData[r1 * _width + c0];
            double g11 = _heightData[r1 * _width + c1];

            // 双线性插值的四个邻域像素中任一为"无效值"标记，则整体判定为无效，
            // 避免无效标记值(如-1)混入插值污染真实高度数据
            if (IsInvalidRaw(g00) || IsInvalidRaw(g01) || IsInvalidRaw(g10) || IsInvalidRaw(g11))
                return false;

            double top = g00 * (1 - fc) + g01 * fc;
            double bottom = g10 * (1 - fc) + g11 * fc;
            rawZ = top * (1 - fr) + bottom * fr;
            return true;
        }

        private bool IsInvalidRaw(double grayVal) =>
            double.IsNaN(grayVal) ||
            double.IsInfinity(grayVal) ||
            Math.Abs(grayVal - InvalidHeightValue) < 1e-6;

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

        /// <summary>
        /// 对齐参考Plugin.DispensePath：先按ROI像素轨迹采样Z，再将同一像素点正向转换为机械XY。
        /// 单点失败不会中断整段提取，保证批量预览可明确显示每个无效点。
        /// </summary>
        public List<ZMapHeightSampleResult> SamplePixelHeights(IEnumerable<ZMapPixelPoint> pixelPoints)
        {
            var results = new List<ZMapHeightSampleResult>();
            if (pixelPoints == null)
                return results;

            int index = 0;
            foreach (var point in pixelPoints)
            {
                index++;
                var item = new ZMapHeightSampleResult
                {
                    Index = index,
                    PixelCol = Math.Round(point.Col, 2),
                    PixelRow = Math.Round(point.Row, 2)
                };

                if (CurrentCalibration == null)
                {
                    item.ErrorMessage = "未完成像素到机械坐标标定";
                    results.Add(item);
                    continue;
                }

                if (!TrySampleRawHeightAtPixel(point.Col, point.Row, out double rawZ))
                {
                    item.ErrorMessage = "ROI点超出图像范围或高度无效";
                    results.Add(item);
                    continue;
                }

                try
                {
                    var (machineX, machineY) =
                        AffineCalibrationService.Transform(CurrentCalibration, point.Col, point.Row);
                    item.MachineX = Math.Round(machineX, 3);
                    item.MachineY = Math.Round(machineY, 3);
                    item.RawZ = Math.Round(rawZ, 3);
                    item.CorrectedZ = Math.Round(rawZ + ZOffset, 3);
                    item.IsValid = true;
                }
                catch (Exception ex)
                {
                    item.ErrorMessage = $"像素到机械坐标转换失败: {ex.Message}";
                }
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
            _heightData = null;
            _width = 0;
            _height = 0;
            LoadedFilePath = string.Empty;

            if (_displayImage != null)
            {
                try { if (_displayImage.IsInitialized()) _displayImage.Dispose(); } catch { }
                _displayImage = null;
            }
        }
    }
}
#endif
