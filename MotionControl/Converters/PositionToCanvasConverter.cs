using System;
using System.Globalization;
using System.Windows.Data;

namespace MotionControl.Converters
{
    /// <summary>
    /// 将物理坐标映射到Canvas画布像素坐标
    /// 支持动态坐标范围，根据行程自动缩放
    /// 绑定参数：[0]Position, [1]RangeMin, [2]RangeMax, [3]CanvasSize
    /// </summary>
    public class PositionToCanvasConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 4)
                return 0.0;

            // position, rangeMin, rangeMax, canvasSize
            if (!(values[0] is double position) ||
                !(values[1] is double rangeMin) ||
                !(values[2] is double rangeMax) ||
                !(values[3] is double canvasSize))
                return 0.0;

            string param = parameter?.ToString() ?? "";

            double range = rangeMax - rangeMin;
            if (range <= 0) range = 1;

            // 留10%边距
            double margin = canvasSize * 0.05;
            double drawSize = canvasSize - margin * 2;

            double result = margin + ((position - rangeMin) / range) * drawSize;

            // 标签偏移
            if (param == "LabelX" || param == "LabelY")
                result += 15;

            return result;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
