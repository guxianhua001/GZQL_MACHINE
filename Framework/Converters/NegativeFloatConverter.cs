using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace Framework.Converters
{
    public class NegativeFloatConverter : IValueConverter
    {
        // 验证规则参数
        public double Min { get; set; } = -1000.0;
        public double Max { get; set; } = -0.0;

        // 转换方法 - 用于显示值
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is float floatValue)
            {
                return floatValue.ToString("0.0##", culture);
            }
            return value;
        }

        // 转换回方法 - 用于处理输入值
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                string normalizedValue = stringValue
                    .Replace(" ", "")
                    .Replace("mm", "")
                    .Trim();

                // 尝试解析数值
                if (float.TryParse(normalizedValue, NumberStyles.Any, culture, out var result))
                {
                    // 强制转换为负值
                    if (result >= 0)
                    {
                        result = -Math.Abs(result);
                    }

                    // 检查值范围
                    if (result < (float)Min || result > (float)Max)
                    {
                        // 返回ValidationResult会破坏绑定
                        throw new ValidationException($"值必须在 {Min} 和 {Max} 之间");
                    }

                    return result;
                }

                // 处理逗号等特殊分隔符
                if (float.TryParse(normalizedValue.Replace(',', '.'),
                                    NumberStyles.Float,
                                    CultureInfo.InvariantCulture,
                                    out result))
                {
                    if (result >= 0)
                    {
                        result = -Math.Abs(result);
                    }

                    if (result < (float)Min || result > (float)Max)
                    {
                        throw new ValidationException($"值必须在 {Min} 和 {Max} 之间");
                    }

                    return result;
                }
            }

            throw new ValidationException("请输入有效的数值");
        }
    }
}
