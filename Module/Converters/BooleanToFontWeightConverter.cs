using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace Module.Converters
{
    /// <summary>
    /// 布尔值到字体粗细转换器
    /// true -> 粗体, false -> 正常
    /// </summary>
    public class BooleanToFontWeightConverter : IValueConverter
    {
        /// <summary>
        /// True状态的字体粗细（默认粗体）
        /// </summary>
        public FontWeight TrueWeight { get; set; } = FontWeights.Bold;

        /// <summary>
        /// False状态的字体粗细（默认正常）
        /// </summary>
        public FontWeight FalseWeight { get; set; } = FontWeights.Normal;

        /// <summary>
        /// 空值或非布尔值的字体粗细（默认正常）
        /// </summary>
        public FontWeight NullWeight { get; set; } = FontWeights.Normal;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueWeight : FalseWeight;
            }

            return NullWeight;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 反转的布尔值到字体粗细转换器
    /// true -> 正常, false -> 粗体
    /// </summary>
    public class InverseBooleanToFontWeightConverter : IValueConverter
    {
        public FontWeight TrueWeight { get; set; } = FontWeights.Normal;
        public FontWeight FalseWeight { get; set; } = FontWeights.Bold;
        public FontWeight NullWeight { get; set; } = FontWeights.Normal;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueWeight : FalseWeight;
            }

            return NullWeight;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}