using System;
using System.Globalization;
using System.Windows.Data;

namespace Framework.Converters
{
    public class NumericFormatDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                if (parameter is string formatString && !string.IsNullOrEmpty(formatString))
                {
                    return doubleValue.ToString(formatString, culture);
                }
                return doubleValue.ToString("F2", culture);
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 不需要反向转换
            throw new NotImplementedException();
        }
    }
}
