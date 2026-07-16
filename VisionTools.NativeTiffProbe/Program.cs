using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using HalconDotNet;
using VisionTools.Tools.ZMap;

namespace VisionTools.NativeTiffProbe;

/// <summary>
/// HALCON 原生 TIFF 读取独立诊断入口。
/// 该程序故意以独立进程运行；若 HALCON 原生库发生访问冲突，只会终止本进程。
/// </summary>
internal static class Program
{
    private const int ExitSuccess = 0;
    private const int ExitInvalidArguments = 2;
    private const int ExitReadFailed = 3;
    private const int ExitValidationFailed = 4;
    private const int NativeReadRepeatCount = 20;
    private const double ComparisonTolerance = 0.00001;
    private const string DefaultFilePath = @"D:\LMI_Data\2026-07-16\HeightMap\08_55_53.tiff";

    /// <summary>
    /// 读取单个 TIFF 并输出 JSON 诊断结果，便于人工和自动脚本比对。
    /// </summary>
    private static int Main(string[] args)
    {
        // 未传入参数时使用当前 LMI 样本，便于快速重复执行；传入参数可测试其他批次文件。
        var filePath = @"D:\LMI_Data\2026-07-16\HeightMap\08_55_53.tiff";
        if (!File.Exists(filePath))
        {
            WriteResult(new ProbeResult
            {
                Success = false,
                FilePath = filePath,
                ErrorCode = "FileNotFound",
                Message = GetText("未找到指定的 TIFF 文件。", "The specified TIFF file was not found.")
            });
            return ExitInvalidArguments;
        }

        var stopwatch = Stopwatch.StartNew();
        HImage? image = null;
        try
        {
            long privateMemoryBefore = Process.GetCurrentProcess().PrivateMemorySize64;
            image = new HImage();
            image.ReadImage(filePath);
            image.GetImageSize(out int width, out int height);
            int channelCount = image.CountChannels().I;
            string pixelType = image.GetImageType().ToString();

            // 对多个代表性位置取值，强制 HALCON 实际访问像素，并与托管浮点数组逐点对照。
            var samplePoints = CreateSamplePoints(width, height);
            var samples = new List<PixelSample>(samplePoints.Count);
            bool managedReadSucceeded = TiffFloatReader.TryReadHeightData(
                filePath, out int managedWidth, out int managedHeight, out float[] managedPixels, out string managedError);

            foreach (var (row, column) in samplePoints)
            {
                double nativeValue = image.GetGrayval(row, column);
                float? managedValue = managedReadSucceeded
                    ? managedPixels[row * managedWidth + column]
                    : null;
                double? difference = managedValue.HasValue ? Math.Abs(nativeValue - managedValue.Value) : null;
                samples.Add(new PixelSample
                {
                    Row = row,
                    Column = column,
                    NativeValue = nativeValue,
                    ManagedValue = managedValue,
                    Difference = difference,
                    IsMatch = difference.HasValue && difference.Value <= ComparisonTolerance
                });
            }

            // 连续创建与释放图像对象，验证常见的重复读图稳定性并记录内存增量。
            for (int index = 1; index < NativeReadRepeatCount; index++)
            {
                using var repeatImage = new HImage();
                repeatImage.ReadImage(filePath);
                _ = repeatImage.GetGrayval(height / 2, width / 2);
            }

            long privateMemoryAfter = Process.GetCurrentProcess().PrivateMemorySize64;
            bool sizeMatches = managedReadSucceeded && managedWidth == width && managedHeight == height;
            bool samplesMatch = managedReadSucceeded && samples.All(sample => sample.IsMatch);
            bool isValidationPassed = channelCount == 1 &&
                                      IsRealPixelType(pixelType) &&
                                      sizeMatches &&
                                      samplesMatch;

            WriteResult(new ProbeResult
            {
                Success = isValidationPassed,
                FilePath = filePath,
                Width = width,
                Height = height,
                ChannelCount = channelCount,
                PixelType = pixelType,
                NativeReadRepeatCount = NativeReadRepeatCount,
                PrivateMemoryDeltaBytes = privateMemoryAfter - privateMemoryBefore,
                ManagedDecodeSucceeded = managedReadSucceeded,
                ManagedWidth = managedReadSucceeded ? managedWidth : null,
                ManagedHeight = managedReadSucceeded ? managedHeight : null,
                ManagedDecodeError = managedReadSucceeded ? null : managedError,
                Samples = samples,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                Message = isValidationPassed
                    ? GetText("HALCON 原生 TIFF 与托管解码验证通过。", "HALCON native TIFF and managed decoding validation passed.")
                    : GetText("HALCON 原生 TIFF 可读取，但与托管解码验证未通过。", "HALCON native TIFF read succeeded, but managed decoding validation failed.")
            });
            return isValidationPassed ? ExitSuccess : ExitValidationFailed;
        }
        catch (Exception exception)
        {
            // 只能记录可托管捕获的错误；AccessViolation 等进程级故障由调用端依据退出码识别。
            WriteResult(new ProbeResult
            {
                Success = false,
                FilePath = filePath,
                ErrorCode = exception.GetType().Name,
                Message = exception.Message,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            });
            return ExitReadFailed;
        }
        finally
        {
            image?.Dispose();
        }
    }

    /// <summary>固定输出 JSON，避免本地化文本影响测试脚本的字段解析。</summary>
    private static void WriteResult(ProbeResult result) =>
        Console.Out.WriteLine(JsonSerializer.Serialize(result));

    /// <summary>根据当前 UI 区域性选择中文或英文诊断文本，JSON 字段名始终保持稳定。</summary>
    private static string GetText(string chinese, string english) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase)
            ? chinese
            : english;

    /// <summary>生成角点、中心和四分点，兼顾边界及内部浮点高度值验证。</summary>
    private static List<(int Row, int Column)> CreateSamplePoints(int width, int height)
    {
        var points = new (int Row, int Column)[]
        {
            (0, 0),
            (0, width - 1),
            (height - 1, 0),
            (height - 1, width - 1),
            (height / 2, width / 2),
            (height / 4, width / 4),
            (height / 4, width * 3 / 4),
            (height * 3 / 4, width / 4),
            (height * 3 / 4, width * 3 / 4)
        };

        return points.Distinct().ToList();
    }

    /// <summary>HALCON HTuple 的字符串表示可能包含引号，比较前须规范化。</summary>
    private static bool IsRealPixelType(string pixelType) =>
        string.Equals(pixelType.Trim().Trim('"'), "real", StringComparison.OrdinalIgnoreCase);

    /// <summary>原生读取诊断结果；字段名保持稳定，消息内容面向中文操作人员。</summary>
    private sealed class ProbeResult
    {
        public bool Success { get; init; }
        public string? FilePath { get; init; }
        public int? Width { get; init; }
        public int? Height { get; init; }
        public int? ChannelCount { get; init; }
        public string? PixelType { get; init; }
        public int NativeReadRepeatCount { get; init; }
        public long PrivateMemoryDeltaBytes { get; init; }
        public bool ManagedDecodeSucceeded { get; init; }
        public int? ManagedWidth { get; init; }
        public int? ManagedHeight { get; init; }
        public string? ManagedDecodeError { get; init; }
        public IReadOnlyList<PixelSample>? Samples { get; init; }
        public long ElapsedMilliseconds { get; init; }
        public string? ErrorCode { get; init; }
        public string? Message { get; init; }
    }

    /// <summary>同一坐标下 HALCON 原生与托管解码的高度值比较结果。</summary>
    private sealed class PixelSample
    {
        public int Row { get; init; }
        public int Column { get; init; }
        public double NativeValue { get; init; }
        public float? ManagedValue { get; init; }
        public double? Difference { get; init; }
        public bool IsMatch { get; init; }
    }
}
