using System;
using System.Globalization;
using System.Windows.Data;
using Core.Abstraction;
using Prism.Ioc;

namespace Module.Converters
{
    /// <summary>
    /// 将报警等级数字(1-4)转换为可读的本地化文本
    /// 1=紧急 2=严重 3=一般 4=提示
    /// </summary>
    public class AlarmLevelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int level)
            {
                string key = level switch
                {
                    1 => "AlarmLevel_Emergency",
                    2 => "AlarmLevel_Serious",
                    3 => "AlarmLevel_General",
                    4 => "AlarmLevel_Prompt",
                    _ => "AlarmLevel_General"
                };

                try
                {
                    var locService = ContainerLocator.Container.Resolve<ILocalizationService>();
                    string result = locService.GetResource(key);
                    if (!string.IsNullOrEmpty(result) && result != $"[{key}]")
                        return result;
                }
                catch { }

                return level switch
                {
                    1 => "紧急",
                    2 => "严重",
                    3 => "一般",
                    4 => "提示",
                    _ => level.ToString()
                };
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
