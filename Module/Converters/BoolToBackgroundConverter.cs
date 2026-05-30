using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Module.Converters
{
    public class BoolToBackgroundConverter : IValueConverter
    {
        private static readonly SolidColorBrush TrueBrush = new SolidColorBrush(Color.FromRgb(0xE3, 0xF2, 0xFD));
        private static readonly SolidColorBrush FalseBrush = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? TrueBrush : FalseBrush;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
