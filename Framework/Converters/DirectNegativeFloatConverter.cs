using System;
using System.Globalization;
using System.Windows.Data;

namespace Framework.ViewModels
{
    public class DirectNegativeFloatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is float floatValue)
            {
                // 确保在绑定源是负值时，显示时也保持负号
                return floatValue.ToString(culture); // 直接返回带符号的值
            }
            if (value is double doubleValue)
            {
                return doubleValue.ToString(culture); // 处理 double 类型的情况
            }
            return string.Empty;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                string normalizedValue = stringValue
                    .Replace(" ", "")
                    .Replace("mm", "")
                    .Trim();

                // 尝试解析为浮点数
                if (float.TryParse(normalizedValue, NumberStyles.Float, culture, out float floatResult))
                {
                    // 强制转换为负值
                    if (floatResult >= 0)
                    {
                        return -Math.Abs(floatResult);
                    }
                    return floatResult; // 如果本来就是负数，直接返回
                }

                // 处理逗号等特殊分隔符
                if (float.TryParse(normalizedValue.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out floatResult))
                {
                    if (floatResult >= 0)
                    {
                        return -Math.Abs(floatResult);
                    }
                    return floatResult;
                }
            }

            // 如果无法解析，默认返回原值或抛出异常
            return Binding.DoNothing; // 更好的做法是不更新源
        }
    }
}

