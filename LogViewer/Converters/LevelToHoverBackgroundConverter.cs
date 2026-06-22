using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace LogViewer.Converters
{
    /// <summary>
    /// 日志级别到悬停背景色的转换器
    /// 从 Application.Resources 获取动态主题颜色
    /// </summary>
    public class LevelToHoverBackgroundConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || !(values[0] is string level) || !(values[1] is bool isSelected))
                return Brushes.Transparent;

            // 如果已选中，不应用悬停效果
            if (isSelected)
                return Brushes.Transparent;

            // 从 Application.Resources 获取动态主题颜色
            var resources = Application.Current.Resources;

            // 根据日志级别返回对应的悬停背景色
            switch (level.ToUpper())
            {
                case "WARN":
                    return resources["LogWarnHoverBackground"] as SolidColorBrush ?? Brushes.GreenYellow;
                case "ERROR":
                    return resources["LogErrorHoverBackground"] as SolidColorBrush ?? Brushes.IndianRed;
                default:
                    return resources["LogInfoHoverBackground"] as SolidColorBrush ?? Brushes.LightGray;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
