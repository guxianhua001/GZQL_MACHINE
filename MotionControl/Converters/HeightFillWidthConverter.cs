using System;
using System.Globalization;
using System.Windows.Data;

namespace MotionControl.Converters
{
    /// <summary>
    /// 多值转换器：根据高度轴当前位置和进度条实际宽度，计算填充条宽度
    /// 用于显示任意高度轴的实时进度指示（不限定具体是哪个轴）
    /// 绑定参数：[0]CurrentPosition, [1]SafeHeight(未使用), [2]ActualWidth
    /// </summary>
    public class HeightFillWidthConverter : IMultiValueConverter
    {
        private const double MaxHeight = 500.0;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 3 || values[0] is not double currentPosition || values[2] is not double actualWidth)
                return 0.0;

            if (actualWidth <= 0)
                return 0.0;

            double ratio = Math.Min(Math.Max(currentPosition / MaxHeight, 0), 1);
            return ratio * actualWidth;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
