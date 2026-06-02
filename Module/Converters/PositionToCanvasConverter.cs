using System;
using System.Globalization;
using System.Windows.Data;

namespace Module.Converters
{
    /// <summary>
    /// 将物理坐标转换为Canvas画布像素坐标
    /// 支持X/Y轴位置映射，以及标签偏移量计算
    /// </summary>
    public class PositionToCanvasConverter : IValueConverter
    {
        private const double CanvasWidth = 400;
        private const double CanvasHeight = 300;
        private const double CoordinateRange = 200;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is double position))
                return 0.0;

            string param = parameter?.ToString() ?? "Position";

            return param switch
            {
                "XMin" => MapToCanvasX(position),
                "YMin" => MapToCanvasY(position),
                "LabelX" => MapToCanvasX(position) + 15,
                "LabelY" => MapToCanvasY(position) - 20,
                _ => MapToCanvasX(position)
            };
        }

        /// <summary>
        /// 将X轴物理坐标映射到Canvas坐标（原点在中心）
        /// </summary>
        private static double MapToCanvasX(double x)
        {
            return CanvasWidth / 2 + (x / CoordinateRange) * (CanvasWidth / 2);
        }

        /// <summary>
        /// 将Y轴物理坐标映射到Canvas坐标（Y轴翻转，原点在中心）
        /// </summary>
        private static double MapToCanvasY(double y)
        {
            return CanvasHeight / 2 - (y / CoordinateRange) * (CanvasHeight / 2);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
