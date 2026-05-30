using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MotionControl.Converters
{
    public class BrushToColorConverter : IValueConverter
    {
        public static BrushToColorConverter Instance { get; } = new BrushToColorConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
            {
                return brush.Color;
            }
            return Colors.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
