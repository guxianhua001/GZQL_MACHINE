using System;
using System.Globalization;
using System.Windows.Data;

namespace MotionControl.Converters
{
    /// <summary>
    /// 计算危险区矩形在Canvas上的位置和尺寸
    /// 绑定参数：[0]DangerMin, [1]DangerMax, [2]RangeMin, [3]RangeMax, [4]CanvasSize
    /// ConverterParameter: "Left"/"Top"/"Width"/"Height"
    /// </summary>
    public class DangerZoneRectConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 5)
                return 0.0;

            if (!(values[0] is double dangerMin) ||
                !(values[1] is double dangerMax) ||
                !(values[2] is double rangeMin) ||
                !(values[3] is double rangeMax) ||
                !(values[4] is double canvasSize))
                return 0.0;

            string param = parameter?.ToString() ?? "Left";

            double range = rangeMax - rangeMin;
            if (range <= 0) range = 1;

            // 留5%边距
            double margin = canvasSize * 0.05;
            double drawSize = canvasSize - margin * 2;

            double left = margin + ((dangerMin - rangeMin) / range) * drawSize;
            double right = margin + ((dangerMax - rangeMin) / range) * drawSize;
            double width = Math.Max(right - left, 2); // 最小2像素

            return param switch
            {
                "Left" or "Top" => left,
                "Width" or "Height" => width,
                _ => left
            };
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
