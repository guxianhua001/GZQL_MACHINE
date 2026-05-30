using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Module.Converters
{
    public class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value as string;
            if (status == "PASS")
                return new SolidColorBrush(Colors.Green);
            if (status == "FAIL")
                return new SolidColorBrush(Colors.Red);
            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}