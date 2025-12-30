using System;
using System.Globalization;
using System.Windows.Data;

namespace Framework.Converters
{
    public class ArcDirectionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double direction && parameter is string paramString)
            {
                double paramValue = double.Parse(paramString);
                return Math.Abs(direction - paramValue) < 0.001;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter is string paramString)
            {
                return double.Parse(paramString);
            }
            return 1.0; // 默认向外
        }
    }
}
