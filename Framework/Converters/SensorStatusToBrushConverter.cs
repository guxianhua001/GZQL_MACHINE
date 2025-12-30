
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Framework.Converters
{
    public class SensorStatusToBrushConverter : IValueConverter
    {
        public Brush ActiveBrush { get; set; } = Brushes.Green;
        public Brush InactiveBrush { get; set; } = Brushes.LightGray;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                return isActive ? ActiveBrush : InactiveBrush;
            }
            return InactiveBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}


