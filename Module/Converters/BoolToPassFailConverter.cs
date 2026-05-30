using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Module.Converters
{
    public class BoolToPassFailConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool result)
                return result ? "Pass" : "Fail";
            return "Unknown";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    public class BoolToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool result)
                return result ? "Check" : "Close";
            return "Help";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

}