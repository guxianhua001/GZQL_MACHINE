using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Recipe.ViewModels;

namespace Recipe.Converters
{
    public class NodeTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is NodeType type && parameter is string targetTypeStr)
            {
                return type.ToString() == targetTypeStr ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}