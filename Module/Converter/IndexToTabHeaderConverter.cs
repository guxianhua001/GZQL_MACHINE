
using System;
using System.Globalization;
using System.Windows.Data;

namespace Module.Converters
{
    public class IndexToTabHeaderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Windows.Controls.GroupBox groupBox)
            {
                var itemsControl = System.Windows.Media.VisualTreeHelper.GetParent(groupBox) as System.Windows.Controls.ItemsControl;
                if (itemsControl != null)
                {
                    var index = itemsControl.Items.IndexOf(groupBox.DataContext);
                    return $"Tab{index + 1}高度";
                }
            }
            return "Tab高度";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class IndexToPositionHeaderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Windows.Controls.GroupBox groupBox)
            {
                var itemsControl = System.Windows.Media.VisualTreeHelper.GetParent(groupBox) as System.Windows.Controls.ItemsControl;
                if (itemsControl != null)
                {
                    var index = itemsControl.Items.IndexOf(groupBox.DataContext);
                    return $"基准位{index + 1}";
                }
            }
            return "基准位";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}