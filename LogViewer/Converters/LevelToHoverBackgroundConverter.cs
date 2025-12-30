using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace Modules.LogViewer.Converters
{
    public class LevelToHoverBackgroundConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || !(values[0] is string level) || !(values[1] is bool isSelected))
                return Brushes.Transparent;

            // 如果已选中，不应用悬停效果
            if (isSelected)
                return Brushes.Transparent;

            // 根据日志级别返回对应的悬停背景色
            switch (level.ToUpper())
            {
                case "WARN":
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFADFF2F")); // GreenYellow
                case "ERROR":
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCD5C5C")); // IndianRed
                default:
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD3D3D3")); // LightGray
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
