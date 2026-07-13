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
    /// 用HALCON读取单通道32位浮点real TIFF，把原始float高度数组和归一化PNG预览
    /// 写到指定输出文件，供主控程序（.NET 9）解析后做ROI采样和坐标变换。
    /// 本工具不承担标定、坐标或运动逻辑，保持视觉与控制分层。
    /// </summary>
    public sealed class ZMapReadTool : IVisionTool
    {
        private const int DataMagic = 0x5A4D4150; // ASCII: ZMAP
        private const int DataVersion = 1;

        public string Name => "zmap-read";

        public string Usage => "zmap-read <input.tif> <output.bin> <preview.png>";

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
                if (image.CountChannels().I != 1)
                    throw new InvalidDataException("ZMAP高度图必须为单通道图像");

                string imageType;
                int width;
                int height;
                IntPtr pointer = image.GetImagePointer1(out imageType, out width, out height);
                if (!string.Equals(imageType, "real", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("ZMAP高度图必须为32位浮点real类型，当前类型=" + imageType);
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
                    "OK width={0} height={1} min={2:R} max={3:R}",
                    width, height, min, max));
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
