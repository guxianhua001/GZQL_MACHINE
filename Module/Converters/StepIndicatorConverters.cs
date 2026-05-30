using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Module.Converters
{
    /// <summary>
    /// 步骤指示器背景色转换器——当前步骤用主题色，已完成用绿色，未到用灰色
    /// </summary>
    public class StepBgConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isCurrent && isCurrent)
                return new SolidColorBrush(Color.FromRgb(0x19, 0x76, 0xD2));
            return new SolidColorBrush(Color.FromRgb(0xBD, 0xBD, 0xBD));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// 步骤完成状态边框转换器——已完成的步骤显示绿色边框
    /// </summary>
    public class StepCompletedBorderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isCompleted && isCompleted)
                return new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// 步骤文字前景色转换器——当前步骤用深蓝，其他用灰色
    /// </summary>
    public class StepTextForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isCurrent && isCurrent)
                return new SolidColorBrush(Color.FromRgb(0x19, 0x76, 0xD2));
            return new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// 步骤文字画笔转换器（别名）——与 StepTextForegroundConverter 功能相同，用于步骤标题颜色绑定
    /// </summary>
    public class StepTextBrushConv : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isCurrent && isCurrent)
                return new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0));
            return new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x90));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// 布尔值转阴影透明度——当前步骤发光效果（true=0.6, false=0）
    /// </summary>
    public class BoolToShadowOpacity : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b) return 0.6;
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// 整数减1转换器——将1-based步骤号转为0-based索引（TabControl SelectedIndex绑定用）
    /// </summary>
    public class IntMinusOneConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int i) return i - 1;
            return -1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int i) return i + 1;
            return 1;
        }
    }

    /// <summary>
    /// 底部状态栏第1个圆点颜色——步骤1时为青色(#80CBC4)，否则灰色
    /// </summary>
    public class StepDotColor1 : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int step && step >= 1)
                return new SolidColorBrush(Color.FromRgb(0x80, 0xCB, 0xC4));
            return new SolidColorBrush(Color.FromRgb(0x60, 0x7D, 0x8B));
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>
    /// 底部状态栏第2个圆点颜色——步骤2或3时为青色，否则灰色
    /// </summary>
    public class StepDotColor2 : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int step && step >= 2)
                return new SolidColorBrush(Color.FromRgb(0x80, 0xCB, 0xC4));
            return new SolidColorBrush(Color.FromRgb(0x60, 0x7D, 0x8B));
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>
    /// 底部状态栏第3个圆点颜色——仅步骤3时为青色，否则灰色
    /// </summary>
    public class StepDotColor3 : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int step && step >= 3)
                return new SolidColorBrush(Color.FromRgb(0x80, 0xCB, 0xC4));
            return new SolidColorBrush(Color.FromRgb(0x60, 0x7D, 0x8B));
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>
    /// 底部状态栏第4个圆点颜色——步骤4或5时为青色(#80CBC4)，否则灰色
    /// </summary>
    public class StepDotColor4 : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int step && step >= 4)
                return new SolidColorBrush(Color.FromRgb(0x80, 0xCB, 0xC4));
            return new SolidColorBrush(Color.FromRgb(0x60, 0x7D, 0x8B));
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>
    /// 底部状态栏第5个圆点颜色——仅步骤5时为青色(#80CBC4)，否则灰色
    /// </summary>
    public class StepDotColor5 : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int step && step >= 5)
                return new SolidColorBrush(Color.FromRgb(0x80, 0xCB, 0xC4));
            return new SolidColorBrush(Color.FromRgb(0x60, 0x7D, 0x8B));
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}