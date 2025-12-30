using NLog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace Framework.Converters
{
    // LogLevelToBrushConverter.cs
    public class LogLevelToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogEntryLevel level)
            {
                return level switch
                {
                    LogEntryLevel.Info => Brushes.Black,
                    LogEntryLevel.Warning => Brushes.Orange,
                    LogEntryLevel.Error => Brushes.Red,
                    LogEntryLevel.Success => Brushes.Green,
                    LogEntryLevel.Exception => Brushes.Purple,
                    LogEntryLevel.CriticalAlert => Brushes.DarkRed,
                    _ => Brushes.Black
                };
            }
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

    }
}
