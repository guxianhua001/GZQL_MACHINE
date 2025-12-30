using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Framework.Converters
{
    public class BrushToMaterialCardBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
            {
                return new SolidColorBrush(ApplyAlpha(brush.Color, 0.15));
            }
            return new SolidColorBrush(ApplyAlpha(Colors.LightGray, 0.15));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static Color ApplyAlpha(Color color, double opacity)
        {
            return Color.FromArgb((byte)(255 * opacity), color.R, color.G, color.B);
        }
    }
}

