using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Module.Converters
{
    /// <summary>
    /// 布尔值到画刷转换器
    /// true -> 绿色, false -> 红色, 其他 -> 灰色
    /// </summary>
    public class BooleanToBrushConverter : IValueConverter
    {
        /// <summary>
        /// True状态的颜色（默认绿色）
        /// </summary>
        public Brush TrueBrush { get; set; } = Brushes.LimeGreen;

        /// <summary>
        /// False状态的颜色（默认红色）
        /// </summary>
        public Brush FalseBrush { get; set; } = Brushes.Red;

        /// <summary>
        /// 空值或非布尔值的颜色（默认灰色）
        /// </summary>
        public Brush NullBrush { get; set; } = Brushes.LightGray;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueBrush : FalseBrush;
            }

            return NullBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 布尔值到状态颜色转换器（专门用于状态指示灯）
    /// </summary>
    public class StatusIndicatorConverter : IValueConverter
    {
        public Brush TrueBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0, 200, 0)); // 绿色
        public Brush FalseBrush { get; set; } = new SolidColorBrush(Color.FromRgb(200, 200, 200)); // 灰色
        public Brush NullBrush { get; set; } = new SolidColorBrush(Color.FromRgb(150, 150, 150)); // 深灰色

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueBrush : FalseBrush;
            }

            return NullBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
