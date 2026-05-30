using System;
using System.Globalization;
using System.Windows.Data;

namespace Module.Converters
{
    public class RunningStatusTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isRunning)
            {
                return isRunning ? "In operation" : "Discontinued";
            }
            return "Unknown status";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}