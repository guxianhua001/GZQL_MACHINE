using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;
using System.Collections.Generic;

namespace Interfaces
{
    public class WindowLevelProcessor
    {
        // 处理单个WriteableBitmap
        public static unsafe void ApplyWindowLevel(WriteableBitmap bitmap, int windowLevel, int windowWidth)
        {
            if (bitmap == null || bitmap.Format != PixelFormats.Gray16)
                return;

            bitmap.Lock();
            try
            {
                int width = bitmap.PixelWidth;
                int height = bitmap.PixelHeight;
                int stride = bitmap.BackBufferStride;
                byte* buffer = (byte*)bitmap.BackBuffer;

                // 计算窗位窗宽的范围
                double min = windowLevel - windowWidth / 2.0;
                double max = windowLevel + windowWidth / 2.0;
                double scale = 255.0 / (max - min);

                // 使用并行处理获得最佳性能
                Parallel.For(0, height, y =>
                {
                    ushort* line = (ushort*)(buffer + y * stride);
                    for (int x = 0; x < width; x++)
                    {
                        ushort original = line[x];

                        // 应用窗位窗宽转换
                        double converted;
                        if (original < min)
                            converted = 0;
                        else if (original > max)
                            converted = 255;
                        else
                            converted = (original - min) * scale;

                        // 转换为8位灰度
                        line[x] = (byte)converted;
                    }
                });

                // 标记整个图像为已更新
                bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            }
            finally
            {
                bitmap.Unlock();
            }
        }

        // 批量处理多个图像
        public static void ApplyWindowLevelBatch(IEnumerable<WriteableBitmap> images, int windowLevel, int windowWidth)
        {
            Parallel.ForEach(images, image =>
            {
                ApplyWindowLevel(image, windowLevel, windowWidth);
            });
        }
    }
}
