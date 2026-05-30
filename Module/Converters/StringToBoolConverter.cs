using System;
using System.Globalization;
using System.Windows.Data;

namespace Module.Converters
{
    public class StringToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string current && parameter is string expected)
                return current == expected;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter is string expected)
                return expected;
            return Binding.DoNothing;
        }
    }
}