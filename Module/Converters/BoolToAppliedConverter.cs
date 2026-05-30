using System;
using System.Globalization;
using System.Windows.Data;

namespace Module.Converters
{
    public class BoolToAppliedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b && b) ? "✔ Applied" : "❌ Not Applied";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}