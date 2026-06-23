using AlarmModule.ViewModels;
using Core.Abstraction;
using System;
using System.Globalization;
using System.Windows.Data;

namespace AlarmModule.Converters
{
    /// <summary>
    /// 启用状态筛选选项到本地化文本的转换器
    /// </summary>
    public class EnabledFilterToTextConverter : IValueConverter
    {
        private static ILocalizationService _localizationService;

        /// <summary>
        /// 初始化本地化服务（模块启动时调用）
        /// </summary>
        public static void Initialize(ILocalizationService localizationService)
        {
            _localizationService = localizationService;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is EnabledFilterOption option)
            {
                return option switch
                {
                    EnabledFilterOption.All => _localizationService?.GetResourceOrDefault("EnabledAll", "全部") ?? "全部",
                    EnabledFilterOption.EnabledOnly => _localizationService?.GetResourceOrDefault("EnabledOnly", "仅启用") ?? "仅启用",
                    EnabledFilterOption.DisabledOnly => _localizationService?.GetResourceOrDefault("DisabledOnly", "仅禁用") ?? "仅禁用",
                    _ => option.ToString()
                };
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
