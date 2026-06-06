// Module/Converters/RoleVisibilityConverter.cs
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Module.Converters
{
    /// <summary>
    /// 根据AssySite角色值和转换器参数判断是否显示（Visibility）
    /// 用法：Visibility="{Binding AssySite, Converter={StaticResource RoleVisibilityConverter}, ConverterParameter=Base_Start}"
    /// 当AssySite值与参数对应的角色名称匹配时返回Visible，否则Collapsed
    /// </summary>
    public class RoleVisibilityConverter : IValueConverter
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
                
                // 匹配中文或英文
                bool match = assySite == expectedValue || assySite == expectedValueEn;
                return match ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
