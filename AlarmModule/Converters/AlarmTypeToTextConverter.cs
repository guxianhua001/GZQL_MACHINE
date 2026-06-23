using AlarmModule.Models;
using Core.Abstraction;
using System;
using System.Globalization;
using System.Windows.Data;

namespace AlarmModule.Converters
{
    public class AlarmTypeToTextConverter : IValueConverter
    {
        private static ILocalizationService _localizationService;

        public static void Initialize(ILocalizationService localizationService)
        {
            _localizationService = localizationService;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AlarmType type)
            {
                return type switch
                {
                    AlarmType.HardwareFault => _localizationService?.GetResourceOrDefault("AlarmType_HardwareFault", "硬件故障") ?? "硬件故障",
                    AlarmType.ParameterOutOfLimit => _localizationService?.GetResourceOrDefault("AlarmType_ParameterOutOfLimit", "参数超限") ?? "参数超限",
                    AlarmType.CommunicationError => _localizationService?.GetResourceOrDefault("AlarmType_CommunicationError", "通信错误") ?? "通信错误",
                    AlarmType.ProcessError => _localizationService?.GetResourceOrDefault("AlarmType_ProcessError", "工艺错误") ?? "工艺错误",
                    _ => type.ToString()
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
