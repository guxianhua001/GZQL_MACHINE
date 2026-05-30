using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Module.Converters
{
    /// <summary>
    /// 字符串非空转 Visibility——字符串非空且非空白时返回 Visible
    /// 用于状态消息区域的条件显示
    /// </summary>
    public class StringNotEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str && !string.IsNullOrWhiteSpace(str))
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
