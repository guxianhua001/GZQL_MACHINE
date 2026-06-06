// Module/Converters/RoleTextConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace Module.Converters
{
    /// <summary>
    /// 根据AssySite角色值返回显示文本
    /// 用法：Text="{Binding AssySite, Converter={StaticResource RoleTextConverter}, ConverterParameter=Base_Start}"
    /// 当AssySite匹配指定角色时返回AssySite值，否则返回空字符串
    /// </summary>
    public class RoleTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string assySite && !string.IsNullOrEmpty(assySite) && parameter is string roleKey)
            {
                // 参数到期望值的映射（中文）
                string expectedValue = roleKey switch
                {
                    "Base_Start" => "基准起点",
                    "Base_End" => "基准终点",
                    "Target_Start" => "目标起点",
                    "Target_End" => "目标终点",
                    _ => ""
                };

                // 同时支持英文匹配
                string expectedValueEn = roleKey switch
                {
                    "Base_Start" => "Base Start",
                    "Base_End" => "Base End",
                    "Target_Start" => "Target Start",
                    "Target_End" => "Target End",
                    _ => ""
                };

                // 匹配中文或英文时返回AssySite值，否则返回空字符串
                bool match = assySite == expectedValue || assySite == expectedValueEn;
                return match ? assySite : string.Empty;
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
