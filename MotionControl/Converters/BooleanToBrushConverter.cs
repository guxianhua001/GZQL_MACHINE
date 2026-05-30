using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MotionControl.Converters
{
    /// <summary>
    /// 布尔值到画刷的转换器
    /// True = 绿色（正常/ON），False = 灰色（关闭/OFF）
    /// 用于状态指示灯
    /// </summary>
    public class BooleanToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveBrush = new(Color.FromRgb(76, 175, 80));
        private static readonly SolidColorBrush InactiveBrush = new(Color.FromRgb(158, 158, 158));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isTrue && isTrue)
                return ActiveBrush;

            return InactiveBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
