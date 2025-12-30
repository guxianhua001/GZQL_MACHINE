using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Framework.Converters
{
    // ColorConverter.cs - 支持多类型输入的通用颜色转换器
    [ValueConversion(typeof(bool), typeof(Brush))]
    [ValueConversion(typeof(string), typeof(Brush))]
    [ValueConversion(typeof(int), typeof(Brush))]
    public class ColorConverter : IValueConverter
    {
        // 默认颜色配置
        public Brush TrueColor { get; set; } = Brushes.LimeGreen;
        public Brush FalseColor { get; set; } = Brushes.Gray;
        public Brush DefaultColor { get; set; } = Brushes.Transparent;

        // 数值范围颜色配置
        public Dictionary<double, Brush> RangeColors { get; } = new();

        // 枚举颜色映射
        public Dictionary<object, Brush> EnumColors { get; } = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                // 布尔类型处理
                bool b => b ? TrueColor : FalseColor,

                // 字符串匹配处理
                string s when parameter is string format =>
                    ParseStringValue(s, format),

                // 数值类型处理
                IConvertible num when RangeColors.Any() =>
                    GetRangeColor(System.Convert.ToDouble(num)),

                // 枚举类型处理
                Enum e when EnumColors.ContainsKey(e) =>
                    EnumColors[e],

                // 默认处理
                _ => DefaultColor
            };
        }

        private Brush ParseStringValue(string value, string format)
        {
            // 示例格式："OK:Green,Error:Red"
            var mappings = format.Split(',')
                .Select(p => p.Split(':'))
                .ToDictionary(p => p[0], p => new BrushConverter().ConvertFromString(p[1]) as Brush);

            return mappings.TryGetValue(value, out var color) ? color : DefaultColor;
        }

        private Brush GetRangeColor(double value)
        {
            var orderedRanges = RangeColors.Keys.OrderBy(k => k).ToList();
            foreach (var threshold in orderedRanges)
            {
                if (value <= threshold)
                    return RangeColors[threshold];
            }
            return RangeColors.Last().Value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToColorConverter : IValueConverter
    {
        public Brush TrueColor { get; set; } = Brushes.LimeGreen;
        public Brush FalseColor { get; set; } = Brushes.LightGray;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? TrueColor : FalseColor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ArrayColorConverter : IValueConverter
    {
        private static readonly Brush[] _palette =
        {
            Brushes.RoyalBlue,
            Brushes.OrangeRed,
            Brushes.ForestGreen,
            Brushes.Gold,
            Brushes.Purple,
            Brushes.Teal
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index && index >= 0)
                return _palette[index % _palette.Length];
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // 平滑度转换器
    public class BoolToSmoothnessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            // 当勾选时返回 1（完全平滑），否则返回 0（不平滑）
            return (value is bool isChecked && isChecked) ? 1 : 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DateTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateTime)
            {
                return dateTime.ToString("HH:mm:ss.fff"); // 格式化为时:分:秒.毫秒
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(bool?), typeof(Brush))]
    [ValueConversion(typeof(bool?), typeof(string))] // 支持ToolTip转换
    public class MapStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                true => new SolidColorBrush(Color.FromRgb(76, 175, 80)),   // 合格-绿色
                false => new SolidColorBrush(Color.FromRgb(244, 67, 54)),  // 不合格-红色（Material Design Red500）
                _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))     // 未检测-灰色
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // 值转换器
    public class CountToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

}


