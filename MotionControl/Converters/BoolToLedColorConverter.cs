using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Core.Abstraction;
using Prism.Ioc;

namespace MotionControl.Converters
{
    /// <summary>
    /// 布尔值转 LED 颜色（绿=激活/ON，灰=未激活/OFF）
    /// 用于 DI/DO 状态指示灯
    /// </summary>
    public class BoolToLedColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveBrush = new(Color.FromRgb(0, 255, 0));
        private static readonly SolidColorBrush InactiveBrush = new(Color.FromRgb(100, 100, 100));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive && isActive)
                return ActiveBrush;

            return InactiveBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 布尔值转按钮背景色（绿=ON，灰色=OFF）
    /// 用于 DO 切换按钮的背景色
    /// </summary>
    public class BoolToButtonColorConverter : IValueConverter
    {
        private static readonly Color ActiveColor = Color.FromRgb(16, 124, 16);
        private static readonly Color InactiveColor = Color.FromRgb(80, 80, 80);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive && isActive)
                return ActiveColor;

            return InactiveColor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 布尔值转状态文本（Active/Inactive）
    /// 缓存 ILocalizationService 引用，避免每次 Convert 都解析容器
    /// </summary>
    public class BoolToTextConverter : IValueConverter
    {
        private static ILocalizationService _cachedService;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive && isActive)
                return GetLocalized("IoStatus_Active", "Active");

            return GetLocalized("IoStatus_Inactive", "Inactive");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static string GetLocalized(string key, string fallback)
        {
            try
            {
                _cachedService ??= ContainerLocator.Container?.Resolve<ILocalizationService>();
                return _cachedService?.GetResourceOrDefault(key, fallback) ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }
    }

    /// <summary>
    /// 布尔值转切换按钮文本（Turn Off / Turn On）
    /// 缓存 ILocalizationService 引用，避免每次 Convert 都解析容器
    /// </summary>
    public class BoolToToggleTextConverter : IValueConverter
    {
        private static ILocalizationService _cachedService;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive && isActive)
                return GetLocalized("IoAction_TurnOff", "Turn Off");

            return GetLocalized("IoAction_TurnOn", "Turn On");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static string GetLocalized(string key, string fallback)
        {
            try
            {
                _cachedService ??= ContainerLocator.Container?.Resolve<ILocalizationService>();
                return _cachedService?.GetResourceOrDefault(key, fallback) ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }
    }
}
