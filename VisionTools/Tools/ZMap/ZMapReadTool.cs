using HalconDotNet;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using VisionTools.Tools;

namespace VisionTools.Tools.ZMap
{
    /// <summary>
    /// ZMAP高度图读取工具（命令：zmap-read）。
    /// 读取高度图后，把原始float高度数组和归一化PNG预览写到指定输出文件，
    /// 供主控程序（.NET 9）解析后做ROI采样和坐标变换。
    /// 读取策略（按优先级）：
    ///   1. HALCON读取：标准ZMAP（单通道real TIFF）原样输出；普通图片
    ///      （PNG/JPG/BMP等，测试用途）彩色转灰度、再转real，灰度值即高度；
    ///   2. 托管兜底解码：部分仪器导出的浮点TIFF会让HALCON原生解码器
    ///      访问冲突崩溃（AccessViolationException），此时改用托管代码
    ///      直接解析无压缩浮点TIFF，保证数据仍能读出；
    ///   3. 两者都失败时，错误信息附带TIFF结构诊断，便于定位格式原因。
    /// 本工具不承担标定、坐标或运动逻辑，保持视觉与控制分层。
    /// </summary>
    public sealed class ZMapReadTool : IVisionTool
    {
        private const int DataMagic = 0x5A4D4150; // ASCII: ZMAP
        private const int DataVersion = 1;

        public string Name => "zmap-read";

        public string Usage => "zmap-read <input.tif|input.png|...> <output.bin> <preview.png>";

        public int Execute(string[] args)
        {
            if (args == null || args.Length != 3)
            {
                Console.Error.WriteLine("Usage: " + Usage);
                return 2;
            }

            string inputPath = args[0];
            string outputPath = args[1];
            string previewPath = args[2];

            try
            {
                if (!File.Exists(inputPath))
                    throw new FileNotFoundException("ZMAP文件不存在", inputPath);

                float[] pixels;
                int width;
                int height;
                bool converted;
                string source = "halcon";

                if (!TryReadWithHalcon(inputPath, out pixels, out width, out height, out converted, out string halconError))
                {
                    // HALCON解码失败/崩溃 → 托管兜底解码（仅限无压缩浮点TIFF）
                    string ext = Path.GetExtension(inputPath).ToLowerInvariant();
                    bool isTiff = ext == ".tif" || ext == ".tiff";
                    string fallbackError = isTiff ? null : "托管兜底解码仅支持TIFF文件";
                    if (isTiff && TiffFloatReader.TryReadHeightData(inputPath, out width, out height, out pixels, out fallbackError))
                    {
                        converted = false;
                        source = "managed";
                        Console.Out.WriteLine("WARN HALCON解码失败已改用托管解码: " + halconError);
                    }
                    else
                    {
                        string diagnostics = isTiff ? TiffFloatReader.DescribeFile(inputPath) : null;
                        throw new InvalidDataException(
                            "HALCON读取失败: " + halconError +
                            "；托管兜底解码失败: " + fallbackError +
                            (diagnostics == null ? string.Empty : "；TIFF结构: " + diagnostics));
                    }
                }

                GetFiniteRange(pixels, out double min, out double max);
                WriteData(outputPath, width, height, min, max, pixels);
                WritePreview(pixels, width, height, min, max, previewPath);

                Console.Out.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "OK width={0} height={1} min={2:R} max={3:R} converted={4} source={5}",
                    width, height, min, max, converted ? 1 : 0, source));
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.GetType().FullName + ": " + ex.Message);
                return 1;
            }
        }

        /// <summary>
        /// 用HALCON读取并归一化为单通道real高度数组。
        /// HALCON原生解码器碰到异常TIFF可能抛AccessViolationException（损坏状态异常），
        /// 通过HandleProcessCorruptedStateExceptions + App.config的
        /// legacyCorruptedStateExceptionsPolicy在此捕获，转为普通失败返回，
        /// 让上层走托管兜底解码而不是整个进程崩溃。
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        private static bool TryReadWithHalcon(
            string inputPath,
            out float[] pixels,
            out int width,
            out int height,
            out bool converted,
            out string error)
        {
            pixels = null;
            width = 0;
            height = 0;
            converted = false;
            error = null;
            HImage image = null;

            try
            {
                // 与参考Plugin.GrabImage一致的两步读图方式
                image = new HImage();
                image.ReadImage(inputPath);

                if (!image.IsInitialized())
                    throw new InvalidDataException("HALCON未能初始化高度图");

                // 非标准ZMAP图像统一归一化为单通道real：彩色转灰度、byte等类型转real，
                // 灰度值直接作为高度，便于用普通图片测试整条提取链路
                image = NormalizeToSingleChannelReal(image, out converted);

                IntPtr pointer = image.GetImagePointer1(out string imageType, out width, out height);
                if (!string.Equals(imageType, "real", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("图像类型转换失败，当前类型=" + imageType);
                if (width <= 0 || height <= 0)
                    throw new InvalidDataException("ZMAP高度图尺寸无效");

                long pixelCount64 = (long)width * height;
                if (pixelCount64 > int.MaxValue)
                    throw new InvalidDataException("ZMAP高度图像素数量超过处理上限");

                pixels = new float[(int)pixelCount64];
                Marshal.Copy(pointer, pixels, 0, pixels.Length);
                return true;
            }
            catch (Exception ex)
            {
                pixels = null;
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                try { image?.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// 把任意可读图像归一化为单通道real高度图：
        /// 3通道彩色先Rgb1ToGray转灰度，非real类型（byte/uint2等）再转real，
        /// 灰度值直接作为高度。标准ZMAP（单通道real）原样返回不做转换。
        /// converted=true表示发生过转换（普通图片测试模式）。
        /// 转换过程中产生的中间图像会被及时释放，原图被替换时也会释放。
        /// </summary>
        private static HImage NormalizeToSingleChannelReal(HImage image, out bool converted)
        {
            converted = false;
            HImage current = image;

            int channels = current.CountChannels().I;
            if (channels == 3)
            {
                HImage gray = current.Rgb1ToGray();
                current.Dispose();
                current = gray;
                converted = true;
            }
            else if (channels != 1)
            {
                throw new InvalidDataException("不支持的图像通道数: " + channels + "（仅支持单通道或RGB三通道）");
            }

            string imageType = current.GetImageType();
            if (!string.Equals(imageType, "real", StringComparison.OrdinalIgnoreCase))
            {
                HImage real = current.ConvertImageType("real");
                current.Dispose();
                current = real;
                converted = true;
            }

            return current;
        }

        /// <summary>计算有限高度范围，忽略NaN和Infinity，供预览归一化使用。</summary>
        private static void GetFiniteRange(float[] pixels, out double min, out double max)
        {
            min = double.MaxValue;
            max = double.MinValue;
            for (int i = 0; i < pixels.Length; i++)
            {
                float value = pixels[i];
                if (float.IsNaN(value) || float.IsInfinity(value))
                    continue;
                if (value < min) min = value;
                if (value > max) max = value;
            }

            if (min == double.MaxValue || max == double.MinValue)
                throw new InvalidDataException("ZMAP高度图不包含有效有限数值");
        }

        /// <summary>写入带固定头的高度数组，主控程序会校验magic、版本和尺寸后再加载。</summary>
        private static void WriteData(
            string outputPath,
            int width,
            int height,
            double min,
            double max,
            float[] pixels)
        {
            using (var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(DataMagic);
                writer.Write(DataVersion);
                writer.Write(width);
                writer.Write(height);
                writer.Write(min);
                writer.Write(max);
                for (int i = 0; i < pixels.Length; i++)
                    writer.Write(pixels[i]);
            }
        }

        /// <summary>
        /// 用托管代码（System.Drawing）从float数组生成8位灰度PNG预览：
        /// 高度归一化到0~255，NaN/Infinity显示为黑色。
        /// 不依赖HALCON，即使HALCON解码崩溃后走托管兜底路径也能生成预览。
        /// 预览失败不影响高度数据输出。
        /// </summary>
        private static void WritePreview(
            float[] pixels, int width, int height, double min, double max, string previewPath)
        {
            Bitmap bitmap = null;
            try
            {
                double range = max - min;
                double scale = range < 1e-12 ? 0 : 255.0 / range;

                bitmap = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
                ColorPalette palette = bitmap.Palette;
                for (int i = 0; i < 256; i++)
                    palette.Entries[i] = Color.FromArgb(i, i, i);
                bitmap.Palette = palette;

                BitmapData data = bitmap.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format8bppIndexed);
                try
                {
                    var rowBytes = new byte[data.Stride];
                    for (int y = 0; y < height; y++)
                    {
                        int rowStart = y * width;
                        for (int x = 0; x < width; x++)
                        {
                            float value = pixels[rowStart + x];
                            if (float.IsNaN(value) || float.IsInfinity(value))
                            {
                                rowBytes[x] = 0;
                                continue;
                            }
                            double normalized = (value - min) * scale;
                            rowBytes[x] = normalized <= 0 ? (byte)0
                                : normalized >= 255 ? (byte)255
                                : (byte)normalized;
                        }
                        Marshal.Copy(rowBytes, 0, data.Scan0 + y * data.Stride, data.Stride);
                    }
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }

                bitmap.Save(previewPath, ImageFormat.Png);
            }
            catch
            {
                try { if (File.Exists(previewPath)) File.Delete(previewPath); } catch { }
            }
            finally
            {
                bitmap?.Dispose();
            }
        }
    }
}
