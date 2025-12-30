using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace Framework.Converters
{
    public class BooleanToBrushConverter : IValueConverter
    {
        public Brush TrueBrush { get; set; } = Brushes.Green;
        public Brush FalseBrush { get; set; } = Brushes.Red;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string param && param.Contains("|"))
            {
                var colors = param.Split('|');
                TrueBrush = (Brush)new BrushConverter().ConvertFromString(colors[0].Trim());
                FalseBrush = (Brush)new BrushConverter().ConvertFromString(colors[1].Trim());
            }

            return (value is bool boolValue && boolValue) ? TrueBrush : FalseBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BooleanToIconConverter : IValueConverter
    {
        public PackIconKind TrueValue { get; set; } = PackIconKind.CheckCircle;
        public PackIconKind FalseValue { get; set; } = PackIconKind.AlertCircle;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string param && param.Contains("|"))
            {
                var values = param.Split('|');
                if (Enum.TryParse(values[0].Trim(), out PackIconKind trueKind))
                    TrueValue = trueKind;
                if (Enum.TryParse(values[1].Trim(), out PackIconKind falseKind))
                    FalseValue = falseKind;
            }

            return (value is bool boolValue && boolValue) ? TrueValue : FalseValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BooleanToTextConverter : IValueConverter
    {
        public string TrueText { get; set; } = "正常";
        public string FalseText { get; set; } = "异常";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string param && param.Contains("|"))
            {
                var texts = param.Split('|');
                TrueText = texts[0].Trim();
                FalseText = texts[1].Trim();
            }

            return (value is bool boolValue && boolValue) ? TrueText : FalseText;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
