using System;
using System.Globalization;
using System.Windows.Data;

namespace Framework.Converters
{
    public class EqualityConverter : IValueConverter, IMultiValueConverter
    {
        // 实现 IValueConverter 接口
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 单值比较逻辑
            return value?.Equals(parameter) ?? false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        // 实现 IMultiValueConverter 接口
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // 多值比较逻辑
            if (values == null || values.Length < 2)
                return false;

            return values[0]?.Equals(values[1]) ?? false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

