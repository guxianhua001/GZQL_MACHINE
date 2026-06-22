using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Module.Converters
{
    /// <summary>
    /// 类型过滤转换器：当值与指定类型匹配时返回原值，否则返回 null。
    /// 用于多个 ContentControl 重叠场景，避免 ContentTemplate 切换时的动画崩溃。
    /// </summary>
    public class TypeFilterConverter : IValueConverter
    {
        /// <summary>
        /// 如果 value 是 ConverterParameter 指定的类型（或其子类），返回原值；否则返回 null
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter is not Type targetType2)
                return null;
            return targetType2.IsAssignableFrom(value.GetType()) ? value : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// 类型转可见性转换器：当值与指定类型匹配时返回 Visible，否则返回 Collapsed。
    /// </summary>
    public class TypeToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 如果 value 是 ConverterParameter 指定的类型（或其子类），返回 Visible；否则返回 Collapsed
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter is not Type targetType2)
                return Visibility.Collapsed;
            return targetType2.IsAssignableFrom(value.GetType()) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
