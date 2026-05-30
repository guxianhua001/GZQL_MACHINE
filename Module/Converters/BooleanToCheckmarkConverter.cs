using System;
using System.Globalization;
using System.Windows.Data;

namespace Module.Converters
{
    public class BooleanToCheckmarkConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return "✔"; // MaterialDesign 图标字体可用 "\u2714"
            else
                return "✘"; // "\u2718"
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}