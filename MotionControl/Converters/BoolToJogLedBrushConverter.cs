using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MotionControl.Converters
{
    /// <summary>
    /// Jog 状态指示灯颜色转换器
    /// True = 绿色（正在运动），False = 灰色（已停止）
    /// </summary>
    public class BoolToJogLedBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveBrush = new(Color.FromRgb(0, 200, 83));
        private static readonly SolidColorBrush InactiveBrush = new(Color.FromRgb(158, 158, 158));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isJogging && isJogging)
                return ActiveBrush;

            return InactiveBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
