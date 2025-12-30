using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;

namespace Interfaces.Converters
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BooleanToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 将布尔值转换为Visibility类型
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool isInverse = false;
            if (parameter != null)
                isInverse = parameter.ToString().Equals("Inverse", StringComparison.OrdinalIgnoreCase);

            bool boolValue = false;
            if (value is bool)
                boolValue = (bool)value;
            else if (value is bool?)
                boolValue = (value as bool?) ?? false;

            if (isInverse)
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            else
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 将Visibility转换回布尔值（可选实现）
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            Visibility visibility = (Visibility)value;
            bool isInverse = parameter?.ToString().Equals("Inverse", StringComparison.OrdinalIgnoreCase) ?? false;

            if (isInverse)
                return visibility != Visibility.Visible;
            else
                return visibility == Visibility.Visible;
        }
    }
}
