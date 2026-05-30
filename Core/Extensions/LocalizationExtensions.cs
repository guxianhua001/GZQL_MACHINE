using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Core.Abstraction;
using Core.Services;

namespace Core.Extensions
{
    /// <summary>
    /// 资源辅助类，提供在XAML中使用的MarkupExtension
    /// </summary>
    public class LocalizeExtension : System.Windows.Markup.MarkupExtension
    {
        public string Key { get; set; }

        public LocalizeExtension() { }

        public LocalizeExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key))
                return null;

            // 返回一个绑定，实现动态更新
            var binding = new Binding
            {
                Source = Application.Current,
                Path = new PropertyPath($"Resources[{Key}]"),
                Mode = BindingMode.OneWay,
                FallbackValue = $"[{Key}]"
            };

            return binding.ProvideValue(serviceProvider);
        }
    }

    /// <summary>
    /// 静态资源访问器
    /// </summary>
    public static class ResourceHelper
    {
        public static string GetString(string key)
        {
            var resource = Application.Current.TryFindResource(key);
            return resource?.ToString() ?? $"[{key}]";
        }

        public static string GetString(string key, params object[] args)
        {
            var format = GetString(key);
            try
            {
                return string.Format(format, args);
            }
            catch
            {
                return format;
            }
        }
    }
}