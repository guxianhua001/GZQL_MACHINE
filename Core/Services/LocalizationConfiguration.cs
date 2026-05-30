using Core.Abstraction;
using System;

namespace Core.Services
{
    /// <summary>
    /// 默认本地化配置
    /// </summary>
    [Obsolete("此类已不再使用，将在未来版本中移除")]
    public class LocalizationConfiguration : ILocalizationConfiguration
    {
        private readonly string _assemblyName;

        public string DefaultCulture { get; set; } = "zh-CN";
        public string ResourceDictionaryBasePath { get; set; } = "/MainApp;component/Localization/";
        public string ResourceDictionaryPattern { get; set; } = "Strings.{0}.xaml";

        public LocalizationConfiguration(string assemblyName = null)
        {
            _assemblyName = assemblyName ?? "MainApp";
        }

        public Uri GetResourceDictionaryUri(string cultureCode)
        {
            var fileName = string.Format(ResourceDictionaryPattern, cultureCode);
            var path = $"{ResourceDictionaryBasePath}{fileName}";
            return new Uri($"/{_assemblyName};component{path}", UriKind.RelativeOrAbsolute);
        }
    }
}