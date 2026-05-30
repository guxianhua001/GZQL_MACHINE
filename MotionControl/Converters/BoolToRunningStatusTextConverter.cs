using System;
using System.Globalization;
using System.Windows.Data;
using Core.Abstraction;
using Prism.Ioc;

namespace MotionControl.Converters
{
    /// <summary>
    /// 布尔值到运行状态文本的转换器
    /// True = "运动中"，False = "停止"
    /// 缓存 ILocalizationService 引用，避免每次 Convert 都解析容器
    /// </summary>
    public class BoolToRunningStatusTextConverter : IValueConverter
    {
        private static ILocalizationService _cachedService;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isJogging && isJogging)
                return GetLocalizedText("JogStatus_Running", "运动中");

            return GetLocalizedText("JogStatus_Stopped", "已停止");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static string GetLocalizedText(string key, string fallback)
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
