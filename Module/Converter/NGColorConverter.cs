// NGColorConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Framework.Converters
{
    public class NGColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2 || targetType != typeof(Brush))
                return Brushes.Black;

            // 第一个值是偏差值
            if (values[0] is not double deviation)
            {
                // 尝试解析字符串或处理无效值
                if (!double.TryParse(values[0]?.ToString(), out deviation))
                    return Brushes.Gray;
            }

            // 第二个值是点位的状态 (IsOk) - 使用 as 操作符处理 nullable bool
            bool? status = values[1] as bool?;

            // 如果状态是NG (false)，直接返回红色
            if (status == false)
                return Brushes.Red;

            // 如果是未操作状态 (null)，返回灰色
            if (status == null)
                return Brushes.Gray;

            // 如果是OK状态，检查偏差值是否超过阈值
            if (!double.TryParse(parameter?.ToString(), out double threshold))
                threshold = 0.2;

            return Math.Abs(deviation) > threshold ? Brushes.Red : Brushes.Black;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
