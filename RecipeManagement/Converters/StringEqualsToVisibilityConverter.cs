using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Recipe.Converters
{
    public class StringEqualsToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str && parameter is string target)
                return str.Equals(target, StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}