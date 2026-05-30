using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Module.Converters
{
    /// <summary>
    /// int 等值比较转 Visibility——当绑定值等于 ConverterParameter 时返回 Visible
    /// 用于 Step Panel 切换：CurrentStep == N 时显示对应面板
    /// </summary>
    public class IntEqualityToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue && parameter != null && int.TryParse(parameter.ToString(), out int paramValue))
                return intValue == paramValue ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
