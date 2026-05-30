using System;
using System.Globalization;
using System.Windows.Data;

namespace Module.Converters
{
    /// <summary>
    /// 整数到布尔值转换器
    /// 用于将整数值与参数比较，转换为布尔值
    /// </summary>
    public class IntToBoolConverter : IValueConverter
    {
        /// <summary>
        /// 将整数值转换为布尔值
        /// </summary>
        /// <param name="value">绑定的整数值</param>
        /// <param name="targetType">目标类型（bool）</param>
        /// <param name="parameter">比较的参数（整数）</param>
        /// <param name="culture">区域信息</param>
        /// <returns>如果值等于参数则返回true，否则返回false</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value == null || parameter == null)
                    return false;

                // 解析当前值
                int intValue;
                if (value is int)
                    intValue = (int)value;
                else if (value is string strValue && int.TryParse(strValue, out int parsedValue))
                    intValue = parsedValue;
                else
                    return false;

                // 解析参数值
                int paramValue;
                if (parameter is int)
                    paramValue = (int)parameter;
                else if (parameter is string paramStr && int.TryParse(paramStr, out int parsedParam))
                    paramValue = parsedParam;
                else
                    return false;

                return intValue == paramValue;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 将布尔值转换为整数值
        /// </summary>
        /// <param name="value">布尔值</param>
        /// <param name="targetType">目标类型（int）</param>
        /// <param name="parameter">比较的参数（整数）</param>
        /// <param name="culture">区域信息</param>
        /// <returns>如果为true则返回参数值，否则返回Binding.DoNothing</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value == null || parameter == null)
                    return Binding.DoNothing;

                bool boolValue;
                if (value is bool)
                    boolValue = (bool)value;
                else if (value is string strValue && bool.TryParse(strValue, out bool parsedValue))
                    boolValue = parsedValue;
                else
                    return Binding.DoNothing;

                // 如果值为true，则返回参数值
                if (boolValue)
                {
                    // 解析参数值
                    if (parameter is int paramInt)
                        return paramInt;
                    else if (parameter is string paramStr && int.TryParse(paramStr, out int parsedParam))
                        return parsedParam;
                }

                return Binding.DoNothing;
            }
            catch
            {
                return Binding.DoNothing;
            }
        }
    }
}
