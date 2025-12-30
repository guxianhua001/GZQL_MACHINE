
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace Framework.Converters
{
    // CriticalToBrushConverter.cs
    public class CriticalToBrushConverter : IValueConverter
    {
        public Brush TrueBrush { get; set; } = Brushes.Red;
        public Brush FalseBrush { get; set; } = Brushes.DarkOrange;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isCritical)
            {
                return isCritical ? TrueBrush : FalseBrush;
            }
            return FalseBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // CriticalToIconConverter.cs
    public class CriticalToIconConverter : IValueConverter
    {
        public PackIconKind TrueValue { get; set; } = PackIconKind.AlertCircleOutline;
        public PackIconKind FalseValue { get; set; } = PackIconKind.AlertOutline;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isCritical)
            {
                return isCritical ? TrueValue : FalseValue;
            }
            return FalseValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
