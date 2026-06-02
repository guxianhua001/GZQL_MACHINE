using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Module.Converters
{
    /// <summary>
    /// 多值转换器：根据安全高度阈值和进度条实际宽度，计算安全线标记的左边距
    /// 用于在Z₁高度进度条上标记安全/危险分界线位置
    /// 绑定参数：[0]SafeHeightZ1, [1]ActualWidth
    /// </summary>
    public class SafeLineMarginConverter : IMultiValueConverter
    {
        private const double MaxZ1Height = 500.0;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] is not double safeHeight || values[1] is not double actualWidth)
                return new Thickness(0);

            if (actualWidth <= 0)
                return new Thickness(0);

            double ratio = Math.Min(Math.Max(safeHeight / MaxZ1Height, 0), 1);
            return new Thickness(ratio * actualWidth, 0, 0, 0);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
