using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Module.ViewModels;

namespace Module.Converters
{
    /// <summary>
    /// 将StepState枚举转换为对应的画刷颜色
    /// Active=蓝色, Done=绿色, Pending=灰色
    /// </summary>
    public class StepStateToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveBrush = new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0));
        private static readonly SolidColorBrush DoneBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
        private static readonly SolidColorBrush PendingBrush = new SolidColorBrush(Color.FromRgb(0xBD, 0xBD, 0xBD));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is StepState state)
            {
                return state switch
                {
                    StepState.Active => ActiveBrush,
                    StepState.Done => DoneBrush,
                    StepState.Pending => PendingBrush,
                    _ => PendingBrush
                };
            }
            return PendingBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// 将StepState枚举转换为对应的PackIcon Kind名称
    /// Active=圆圈数字图标, Done=勾选图标, Pending=空心圆图标
    /// </summary>
    public class StepStateToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is StepState state)
            {
                return state switch
                {
                    StepState.Active => "Pencil",
                    StepState.Done => "CheckCircle",
                    StepState.Pending => "CircleOutline",
                    _ => "CircleOutline"
                };
            }
            return "CircleOutline";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// 将StepState枚举转换为步骤前景色（圆圈内文字颜色）
    /// Active/Done=白色, Pending=深灰色
    /// </summary>
    public class StepStateToForegroundConverter : IValueConverter
    {
        private static readonly SolidColorBrush WhiteBrush = Brushes.White;
        private static readonly SolidColorBrush DarkBrush = new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is StepState state)
            {
                return state switch
                {
                    StepState.Active => WhiteBrush,
                    StepState.Done => WhiteBrush,
                    StepState.Pending => DarkBrush,
                    _ => DarkBrush
                };
            }
            return DarkBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// 将StepState枚举转换为步骤描述文字颜色
    /// Active/Done=深色, Pending=灰色
    /// </summary>
    public class StepStateToTextBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveTextBrush = new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0));
        private static readonly SolidColorBrush DoneTextBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
        private static readonly SolidColorBrush PendingTextBrush = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is StepState state)
            {
                return state switch
                {
                    StepState.Active => ActiveTextBrush,
                    StepState.Done => DoneTextBrush,
                    StepState.Pending => PendingTextBrush,
                    _ => PendingTextBrush
                };
            }
            return PendingTextBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// 将StepState枚举转换为步骤连接线颜色
    /// Done步骤之间的连接线=绿色, 其他=灰色
    /// </summary>
    public class StepStateToConnectorBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush DoneConnectorBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
        private static readonly SolidColorBrush PendingConnectorBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is StepState state)
            {
                return state == StepState.Done ? DoneConnectorBrush : PendingConnectorBrush;
            }
            return PendingConnectorBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
