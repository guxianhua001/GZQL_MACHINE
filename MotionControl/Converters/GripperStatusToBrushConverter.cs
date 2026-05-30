using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MotionControl.Models;

namespace MotionControl.Converters
{
    public class GripperStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is GripperStatus status)
            {
                return status switch
                {
                    GripperStatus.Unknown => Brushes.Gray,
                    GripperStatus.Idle => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                    GripperStatus.Moving => new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                    GripperStatus.Clamping => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                    GripperStatus.Clamped => new SolidColorBrush(Color.FromRgb(230, 81, 0)),
                    GripperStatus.Releasing => new SolidColorBrush(Color.FromRgb(156, 39, 176)),
                    GripperStatus.Error => Brushes.Red,
                    GripperStatus.Homing => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                    _ => Brushes.Gray
                };
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
