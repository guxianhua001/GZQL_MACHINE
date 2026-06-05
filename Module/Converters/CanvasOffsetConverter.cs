using System;
using System.Globalization;
using System.Windows.Data;

namespace Module.Converters
{
    /// <summary>
    /// 画布坐标偏移转换器：将绑定值加上 ConverterParameter 指定的偏移量
    /// 用途：回转中心可视化画布中，十字线/标签等相对于中心点的偏移定位
    /// </summary>
    public class CanvasOffsetConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double val = 0;
            if (value is double d) val = d;
            else if (value is float f) val = f;
            else if (value is int i) val = i;

            double offset = 0;
            if (parameter != null && double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double p))
                offset = p;

            return val + offset;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
