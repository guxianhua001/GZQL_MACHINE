using AlarmModule.Models;
using Core.Abstraction;
using System;
using System.Globalization;
using System.Windows.Data;

namespace AlarmModule.Converters
{
    public class AlarmStatusToTextConverter : IValueConverter
    {
        private static ILocalizationService _localizationService;

        public static void Initialize(ILocalizationService localizationService)
        {
            _localizationService = localizationService;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AlarmStatus status)
            {
                return status switch
                {
                    AlarmStatus.Unconfirmed => _localizationService?.GetResourceOrDefault("AlarmStatus_Unconfirmed", "未确认") ?? "未确认",
                    AlarmStatus.Confirmed => _localizationService?.GetResourceOrDefault("AlarmStatus_Confirmed", "已确认") ?? "已确认",
                    AlarmStatus.Reset => _localizationService?.GetResourceOrDefault("AlarmStatus_Reset", "已复位") ?? "已复位",
                    AlarmStatus.Eliminated => _localizationService?.GetResourceOrDefault("AlarmStatus_Eliminated", "已消除") ?? "已消除",
                    _ => status.ToString()
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
