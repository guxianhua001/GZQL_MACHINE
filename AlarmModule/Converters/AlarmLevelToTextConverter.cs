using AlarmModule.Models;
using Core.Abstraction;
using System;
using System.Globalization;
using System.Windows.Data;

namespace AlarmModule.Converters
{
    public class AlarmLevelToTextConverter : IValueConverter
    {
        private static ILocalizationService _localizationService;

        public static void Initialize(ILocalizationService localizationService)
        {
            _localizationService = localizationService;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AlarmLevel level)
            {
                return level switch
                {
                    AlarmLevel.Emergency => _localizationService?.GetResourceOrDefault("AlarmLevel_Emergency", "紧急") ?? "紧急",
                    AlarmLevel.Serious => _localizationService?.GetResourceOrDefault("AlarmLevel_Serious", "严重") ?? "严重",
                    AlarmLevel.General => _localizationService?.GetResourceOrDefault("AlarmLevel_General", "一般") ?? "一般",
                    AlarmLevel.Prompt => _localizationService?.GetResourceOrDefault("AlarmLevel_Prompt", "提示") ?? "提示",
                    _ => level.ToString()
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
