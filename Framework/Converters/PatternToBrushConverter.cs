using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Framework.Converters
{
    public class PatternToBrushConverter : IMultiValueConverter, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string pattern && parameter is string indexStr)
            {
                return ConvertPatternPosition(pattern, int.Parse(indexStr));
            }
            return Brushes.Transparent;
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 &&
                values[0] is string pattern &&
                values[1] is int index)
            {
                return ConvertPatternPosition(pattern, index);
            }
            return Brushes.Transparent;
        }
        private Brush ConvertPatternPosition(string pattern, int index)
        {
            // 解析 "1 0 0" 这样的模式
            var parts = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (index < parts.Length)
            {
                var status = parts[index] == "1" || parts[index] == "激活";
                return status ? Brushes.LimeGreen : Brushes.LightGray;
            }

            return Brushes.Transparent;
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
