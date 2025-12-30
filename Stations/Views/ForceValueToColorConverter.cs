using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Stations.Views
{
    /// <summary>
    /// 力值到颜色转换器
    /// </summary>
    public class ForceValueToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double forceValue)
            {
                // 负值显示红色，正值显示绿色
                return forceValue < 0 ?
                    new SolidColorBrush(Color.FromRgb(244, 67, 54)) : // 红色 #F44336
                    new SolidColorBrush(Color.FromRgb(76, 175, 80));  // 绿色 #4CAF50
            }

            // 默认返回绿色
            return new SolidColorBrush(Color.FromRgb(76, 175, 80));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}