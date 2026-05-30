using AlarmModule.Models;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AlarmModule.Converters
{
    /// <summary>
    /// 报警等级到画刷转换器：将AlarmLevel枚举转换为对应的SolidColorBrush
    /// 支持ConverterParameter设置透明度（0-1），用于DataGrid行背景等场景
    /// Emergency→红色(#FF1744), Serious→橙色(#FF9100), General→黄色(#FFD600), Prompt→蓝色(#2979FF)
    /// </summary>
    public class AlarmLevelToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AlarmLevel level)
            {
                var colorHex = level switch
                {
                    AlarmLevel.Emergency => "#FF1744",
                    AlarmLevel.Serious => "#FF9100",
                    AlarmLevel.General => "#FFD600",
                    AlarmLevel.Prompt => "#2979FF",
                    _ => "#808080"
                };
                var color = (Color)ColorConverter.ConvertFromString(colorHex);

                // 支持通过ConverterParameter设置透明度（如"0.15"表示15%不透明度）
                double opacity = 1.0;
                if (parameter is string paramStr && double.TryParse(paramStr, out var parsedOpacity))
                    opacity = parsedOpacity;

                return new SolidColorBrush(Color.FromArgb(
                    (byte)(color.A * opacity), color.R, color.G, color.B));
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
