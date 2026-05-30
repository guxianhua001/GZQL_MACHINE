using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Language.Converters
{
    /// <summary>
    /// 空值到可见性转换器
    /// </summary>
    [ValueConversion(typeof(object), typeof(Visibility))]
    public class NullToVisibilityConverter : IValueConverter
    {
        public Visibility NullValue { get; set; } = Visibility.Collapsed;
        public Visibility NotNullValue { get; set; } = Visibility.Visible;
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNullOrEmpty = value == null ||
                                (value is string str && string.IsNullOrWhiteSpace(str)) ||
                                (value is System.Collections.ICollection collection && collection.Count == 0);

            if (Invert)
            {
                isNullOrEmpty = !isNullOrEmpty;
            }

            return isNullOrEmpty ? NullValue : NotNullValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}