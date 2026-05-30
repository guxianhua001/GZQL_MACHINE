using Module.Models;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Module.Converters
{
    public class ScanStatusToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ScanStatus status)
            {
                return status switch
                {
                    ScanStatus.NotScanned => "CircleOutline",      // ○
                    ScanStatus.ScannedOk => "CheckCircle",        // ✅
                    ScanStatus.HighDelta => "AlertCircle",        // ⚠
                    ScanStatus.Failed => "CloseCircle",           // ❌
                    _ => "HelpCircle"
                };
            }
            return "HelpCircle";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
    public class ScanStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ScanStatus status)
            {
                return status switch
                {
                    ScanStatus.NotScanned => Brushes.Gray,
                    ScanStatus.ScannedOk => Brushes.Green,
                    ScanStatus.HighDelta => Brushes.Orange,
                    ScanStatus.Failed => Brushes.Red,
                    _ => Brushes.Black
                };
            }
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}