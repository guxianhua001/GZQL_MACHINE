using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace Module.Converters
{
    public class ValueToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                // parameter 格式: "默认值|默认颜色|非默认颜色"
                var paramStr = parameter as string;
                if (!string.IsNullOrEmpty(paramStr))
                {
                    var parts = paramStr.Split('|');
                    if (parts.Length >= 3)
                    {
                        double defaultValue;
                        if (double.TryParse(parts[0], out defaultValue))
                        {
                            // 如果值是默认值（0），使用灰色
                            if (Math.Abs(doubleValue - defaultValue) < 0.001)
                            {
                                var colorStr = parts[1];
                                return GetBrushFromString(colorStr);
                            }
                            else
                            {
                                var colorStr = parts[2];
                                return GetBrushFromString(colorStr);
                            }
                        }
                    }
                }
            }

            return Brushes.Black;
        }

        private Brush GetBrushFromString(string colorStr)
        {
            switch (colorStr.ToLower())
            {
                case "gray": return Brushes.Gray;
                case "red": return Brushes.Red;
                case "green": return Brushes.Green;
                case "blue": return Brushes.Blue;
                case "orange": return Brushes.Orange;
                case "black": return Brushes.Black;
                case "white": return Brushes.White;
                default:
                    try
                    {
                        return new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr));
                    }
                    catch
                    {
                        return Brushes.Black;
                    }
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

