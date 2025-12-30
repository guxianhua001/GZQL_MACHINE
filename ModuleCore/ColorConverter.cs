using ModuleCore.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace ModuleCore.Converters
{
    public class ProgressColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is NeedleModel needle)
            {
                var ratio = (double)needle.CurrentCount / needle.MaxCount;
                return ratio switch
                {
                    >= 1 => Brushes.Red,
                    >= 0.9 => Brushes.Orange,
                    _ => Brushes.LimeGreen
                };
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

}
