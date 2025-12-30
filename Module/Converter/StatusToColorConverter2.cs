using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Markup;

namespace Framework.Converters
{
    public class StatusToColorConverter2 : MarkupExtension, IValueConverter
    {
        private static StatusToColorConverter2 _instance;

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return _instance ?? (_instance = new StatusToColorConverter2());
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || !(value is bool))
                return Brushes.Gray;

            return (bool)value ? Brushes.Green : Brushes.Red;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
