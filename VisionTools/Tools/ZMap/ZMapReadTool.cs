using HalconDotNet;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using VisionTools.Tools;

namespace VisionTools.Tools.ZMap
{
    /// <summary>
    /// ZMAP高度图读取工具（命令：zmap-read）。
    /// 用HALCON读取高度图，把原始float高度数组和归一化PNG预览
    /// 写到指定输出文件，供主控程序（.NET 9）解析后做ROI采样和坐标变换。
    /// 图像兼容策略：
    ///   - 标准ZMAP：单通道32位浮点real图像（TIFF），灰度值即高度(mm)，原样输出；
    ///   - 普通图片（测试用途）：8位灰度或RGB彩色图（PNG/JPG/BMP等），
    ///     彩色先转灰度，再转为real类型，灰度值(0~255)直接作为高度值，
    ///     用于在没有真实ZMAP数据时验证整条提取链路。
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
            HImage image = null;

            try
            {
                if (!File.Exists(inputPath))
                    throw new FileNotFoundException("ZMAP文件不存在", inputPath);

                // 与参考Plugin.GrabImage一致的两步读图方式
                image = new HImage();
                image.ReadImage(inputPath);

                if (!image.IsInitialized())
                    throw new InvalidDataException("HALCON未能初始化高度图");

                // 非标准ZMAP图像统一归一化为单通道real：彩色转灰度、byte等类型转real，
                // 灰度值直接作为高度，便于用普通图片测试整条提取链路
                image = NormalizeToSingleChannelReal(image, out bool converted);

                string imageType;
                int width;
                int height;
                IntPtr pointer = image.GetImagePointer1(out imageType, out width, out height);
                if (!string.Equals(imageType, "real", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("图像类型转换失败，当前类型=" + imageType);
                if (width <= 0 || height <= 0)
                    throw new InvalidDataException("ZMAP高度图尺寸无效");

                long pixelCount64 = (long)width * height;
                if (pixelCount64 > int.MaxValue)
                    throw new InvalidDataException("ZMAP高度图像素数量超过处理上限");

                var pixels = new float[(int)pixelCount64];
                Marshal.Copy(pointer, pixels, 0, pixels.Length);
                GetFiniteRange(pixels, out double min, out double max);
                WriteData(outputPath, width, height, min, max, pixels);
                WritePreview(image, previewPath, min, max);

                Console.Out.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "OK width={0} height={1} min={2:R} max={3:R} converted={4}",
                    width, height, min, max, converted ? 1 : 0));
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.GetType().FullName + ": " + ex.Message);
                return 1;
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

        /// <summary>生成8位PNG预览；预览失败不影响高度数据输出。</summary>
        private static void WritePreview(HImage image, string previewPath, double min, double max)
        {
            HImage scaled = null;
            HImage byteImage = null;
            try
            {
                double range = max - min;
                if (range < 1e-12)
                {
                    byteImage = image.ConvertImageType("byte");
                }
                else
                {
                    scaled = image.ScaleImage(255.0 / range, -255.0 * min / range);
                    byteImage = scaled.ConvertImageType("byte");
                }
                byteImage.WriteImage("png", 0, previewPath);
            }
            catch
            {
                try { if (File.Exists(previewPath)) File.Delete(previewPath); } catch { }
            }
            finally
            {
                try { byteImage?.Dispose(); } catch { }
                try { scaled?.Dispose(); } catch { }
            }
        }
    }
}
