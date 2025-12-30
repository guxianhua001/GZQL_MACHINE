using Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Framework.Views
{
    // 状态到颜色转换器
    public class StateToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is RaySourceState state))
                return Brushes.Gray;

            return state switch
            {
                RaySourceState.Standby => Brushes.Gray,
                RaySourceState.WarmingUp => Brushes.Orange,
                RaySourceState.Ready => Brushes.LightGreen,
                RaySourceState.Active => Brushes.Green,
                RaySourceState.Overloaded => Brushes.Red,
                RaySourceState.Error => Brushes.DarkRed,
                RaySourceState.Testing => Brushes.Blue,
                _ => Brushes.Gray
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // 状态到布尔值转换器（用于ToggleButton）
    public class StateToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RaySourceState state)
            {
                return state == RaySourceState.Active ||
                       state == RaySourceState.Ready ||
                       state == RaySourceState.WarmingUp;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool boolValue) && boolValue ?
                RaySourceState.Active : RaySourceState.Standby;
        }
    }

    // Toggle状态到命令参数转换器
    public class ToggleToCommandConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked)
            {
                return isChecked ? "ON" : "OFF";
            }
            return "OFF";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
