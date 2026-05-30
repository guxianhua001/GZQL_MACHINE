using System;

namespace Core.Abstraction
{
    /// <summary>
    /// 本地化配置
    /// </summary>
    [Obsolete("此接口已不再使用，将在未来版本中移除")]
    public interface ILocalizationConfiguration
    {
        /// <summary>
        /// 默认文化代码
        /// </summary>
        string DefaultCulture { get; }

        /// <summary>
        /// 资源字典基路径
        /// </summary>
        string ResourceDictionaryBasePath { get; }

        /// <summary>
        /// 资源字典命名模式
        /// </summary>
        string ResourceDictionaryPattern { get; }

        /// <summary>
        /// 获取资源字典URI
        /// </summary>
        Uri GetResourceDictionaryUri(string cultureCode);
    }
}