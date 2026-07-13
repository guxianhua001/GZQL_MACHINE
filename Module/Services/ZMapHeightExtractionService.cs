#if HAS_HALCON
using Core.Abstraction;
using Core.Models;
using Core.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Module.Services
{
    /// <summary>
    /// ZMAP高度图Z值提取服务——通过HALCON 21.11隔离进程加载单通道高度图（灰度值即高度mm），
    /// 用"像素↔机械"标定点求解仿射矩阵，并按机械坐标反查像素位置、双线性采样得到Z高度。
    /// 详细对齐原理见 <see cref="IZMapHeightExtractionService"/> 接口注释。
    /// </summary>
    public class ZMapHeightExtractionService : IZMapHeightExtractionService
    {
        private const int DataMagic = 0x5A4D4150;
        private const int DataVersion = 1;
        private const int ReaderTimeoutMilliseconds = 30000;
        private float[] _heightData;
        private int _width;
        private int _height;
        private string _previewImagePath;
        private List<ZMapCalibrationPoint> _lastCalibrationPoints = new List<ZMapCalibrationPoint>();

        public bool IsHeightMapLoaded => _heightData != null;
        public int HeightMapWidth => _width;
        public int HeightMapHeight => _height;
        public string LoadedFilePath { get; private set; } = string.Empty;
        public string PreviewImagePath => _previewImagePath ?? string.Empty;
        public double InvalidHeightValue { get; set; } = -1.0;
        public double ZOffset { get; set; }
        public AffineCalibrationResult CurrentCalibration { get; private set; }

        /// <summary>
        /// 加载ZMAP高度图（要求单通道32位浮点real图像，灰度值即高度）。
        /// HALCON 21.11仅提供dotnet20/dotnet35托管接口，不能把其TIFF原生解码风险直接放在
        /// .NET 9主控进程中。因此调用.NET Framework 4.7.2隔离读取器（与参考
        /// Plugin.GrabImage运行环境一致），即使原生解码器崩溃也不会终止主控进程。
        /// </summary>
        public bool LoadHeightMap(string filePath, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                error = "文件不存在";
                return false;
            }

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext != ".tif" && ext != ".tiff")
            {
                error = "ZMAP高度图仅支持 .tif/.tiff 格式";
                return false;
            }

            string readerPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "ZMapHalconReader",
                "ZMapHalconReader.exe");
            if (!File.Exists(readerPath))
            {
                error = "未找到HALCON高度图读取器，请重新生成并部署ZMapHalconReader";
                return false;
            }

            string token = Guid.NewGuid().ToString("N");
            string dataPath = Path.Combine(Path.GetTempPath(), $"zmap_data_{token}.bin");
            string previewPath = Path.Combine(Path.GetTempPath(), $"zmap_preview_{token}.png");
            try
            {
                if (!RunReader(readerPath, filePath, dataPath, previewPath, out error))
                    return false;
                if (!TryReadHeightData(dataPath, out float[] pixels, out int width, out int height, out error))
                    return false;

                Unload();
                _heightData = pixels;
                _width = width;
                _height = height;
                LoadedFilePath = filePath;
                _previewImagePath = File.Exists(previewPath) ? previewPath : null;
                return true;
            }
            catch (Exception ex)
            {
                error = $"加载ZMAP高度图失败: {ex.Message}";
                return false;
            }
            finally
            {
                try { if (File.Exists(dataPath)) File.Delete(dataPath); } catch { }
                if (_previewImagePath != previewPath)
                {
                    try { if (File.Exists(previewPath)) File.Delete(previewPath); } catch { }
                }
            }
        }

        /// <summary>启动HALCON隔离读取器并限制最长执行时间，防止异常文件阻塞UI。</summary>
        private static bool RunReader(
            string readerPath,
            string inputPath,
            string dataPath,
            string previewPath,
            out string error)
        {
            error = null;
            var startInfo = new ProcessStartInfo
            {
                FileName = readerPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add(dataPath);
            startInfo.ArgumentList.Add(previewPath);

            using (var process = new Process { StartInfo = startInfo })
            {
                if (!process.Start())
                {
                    error = "无法启动HALCON高度图读取器";
                    return false;
                }

                if (!process.WaitForExit(ReaderTimeoutMilliseconds))
                {
                    try { process.Kill(true); } catch { }
                    error = "HALCON读取ZMAP超时，已终止隔离读取进程";
                    return false;
                }
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                if (process.ExitCode != 0)
                {
                    error = string.IsNullOrWhiteSpace(stderr)
                        ? $"HALCON读取进程异常退出（代码 {process.ExitCode}）"
                        : stderr.Trim();
                    return false;
                }
                if (!File.Exists(dataPath))
                {
                    error = string.IsNullOrWhiteSpace(stdout)
                        ? "HALCON读取器未生成高度数据"
                        : stdout.Trim();
                    return false;
                }
            }

            return true;
        }

        /// <summary>读取并校验隔离进程输出，避免损坏或伪造尺寸导致主进程内存异常。</summary>
        private static bool TryReadHeightData(
            string dataPath,
            out float[] pixels,
            out int width,
            out int height,
            out string error)
        {
            pixels = null;
            width = 0;
            height = 0;
            error = null;
            try
            {
                using (var stream = new FileStream(dataPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(stream))
                {
                    if (reader.ReadInt32() != DataMagic || reader.ReadInt32() != DataVersion)
                        throw new InvalidDataException("高度数据头或版本无效");
                    width = reader.ReadInt32();
                    height = reader.ReadInt32();
                    reader.ReadDouble(); // min，仅供协议诊断保留
                    reader.ReadDouble(); // max，仅供协议诊断保留

                    long count64 = (long)width * height;
                    if (width <= 0 || height <= 0 || count64 > int.MaxValue)
                        throw new InvalidDataException("高度图尺寸无效或超过处理上限");
                    long expectedLength = 32L + count64 * sizeof(float);
                    if (stream.Length != expectedLength)
                        throw new InvalidDataException("高度数据长度与图像尺寸不一致");

                    pixels = new float[(int)count64];
                    for (int i = 0; i < pixels.Length; i++)
                        pixels[i] = reader.ReadSingle();
                }
                return true;
            }
            catch (Exception ex)
            {
                error = $"解析HALCON高度数据失败: {ex.Message}";
                pixels = null;
                width = 0;
                height = 0;
                return false;
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

            // 高度数组由HALCON隔离进程一次性导出；主进程采样不再跨越旧版原生接口边界。
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

            if (!string.IsNullOrEmpty(_previewImagePath) && File.Exists(_previewImagePath))
            {
                try { File.Delete(_previewImagePath); } catch { /* 临时文件删除失败不影响主流程 */ }
            }
            _previewImagePath = null;
        }
    }
}
#endif
