using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MotionControl.Converters
{
    /// <summary>
    /// 多值转换器：根据安全高度阈值和进度条实际宽度，计算安全线标记的左边距
    /// 用于在高度轴进度条上标记安全/危险分界线位置（不限定具体是哪个轴）
    /// 绑定参数：[0]SafeHeight, [1]ActualWidth
    /// </summary>
    public class SafeLineMarginConverter : IMultiValueConverter
    {
        private const double MaxHeight = 500.0;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] is not double safeHeight || values[1] is not double actualWidth)
                return new Thickness(0);

            if (actualWidth <= 0)
                return new Thickness(0);

            double ratio = Math.Min(Math.Max(safeHeight / MaxHeight, 0), 1);
            return new Thickness(ratio * actualWidth, 0, 0, 0);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
