using System;
using System.Globalization;
using System.Windows.Data;

namespace Module.Converters
{
    public class RedIfLargeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                // 获取参数中设定的阈值，默认0.3
                double threshold = parameter != null && double.TryParse(parameter.ToString(), out threshold)
                    ? threshold : 0.3;

                return doubleValue > threshold ? System.Windows.Media.Brushes.Red : System.Windows.Media.Brushes.Black;
            }
            return System.Windows.Media.Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
