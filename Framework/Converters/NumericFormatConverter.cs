using System;
using System.Globalization;
using System.Windows.Data;

namespace Framework.Converters
{
    public class NumericFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                if (parameter is string format)
                {
                    return doubleValue.ToString(format, culture);
                }
                return doubleValue.ToString("F2", culture);
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                if (double.TryParse(stringValue, out double result))
                {
                    return result;
                }
            }
            return 0.0;
        }
    }
}
