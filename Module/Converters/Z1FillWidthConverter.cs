using System;
using System.Globalization;
using System.Windows.Data;

namespace Module.Converters
{
    /// <summary>
    /// 多值转换器：根据Z₁当前高度和进度条实际宽度，计算填充条宽度
    /// 用于显示Z₁高度的实时进度指示
    /// 绑定参数：[0]CurrentZ1, [1]SafeHeightZ1(未使用), [2]ActualWidth
    /// </summary>
    public class Z1FillWidthConverter : IMultiValueConverter
    {
        private const double MaxZ1Height = 500.0;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 3 || values[0] is not double currentZ1 || values[2] is not double actualWidth)
                return 0.0;

            if (actualWidth <= 0)
                return 0.0;

            double ratio = Math.Min(Math.Max(currentZ1 / MaxZ1Height, 0), 1);
            return ratio * actualWidth;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
