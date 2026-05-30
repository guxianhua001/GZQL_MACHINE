using System;
using System.Collections.Generic;
using Core.Models;

namespace Core.Abstraction
{
    /// <summary>
    /// 本地化服务接口
    /// </summary>
    public interface ILocalizationService : IDisposable
    {
        /// <summary>
        /// 当前语言
        /// </summary>
        LanguageItem CurrentLanguage { get; }

        /// <summary>
        /// 当前区域性代码
        /// </summary>
        string CurrentCultureCode { get; }

        /// <summary>
        /// 支持的语言列表
        /// </summary>
        IReadOnlyList<LanguageItem> SupportedLanguages { get; }

        /// <summary>
        /// 设置语言
        /// </summary>
        /// <param name="cultureCode">区域性代码</param>
        /// <returns>是否成功</returns>
        bool SetLanguage(string cultureCode);

        /// <summary>
        /// 获取资源字符串
        /// </summary>
        /// <param name="key">资源键</param>
        /// <returns>本地化字符串</returns>
        string GetResource(string key);

        /// <summary>
        /// 获取带格式的资源字符串
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="args">格式化参数</param>
        /// <returns>格式化后的字符串</returns>
        string GetResource(string key, params object[] args);

        /// <summary>
        /// 获取资源字符串（支持默认值）
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>本地化字符串或默认值</returns>
        string GetResourceOrDefault(string key, string defaultValue = null);

        /// <summary>
        /// 尝试获取资源字符串
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="value">获取到的资源值</param>
        /// <returns>是否成功获取</returns>
        bool TryGetResource(string key, out string value);

        /// <summary>
        /// 语言变更事件
        /// </summary>
        event EventHandler<LanguageChangedEventArgs> LanguageChanged;
    }

    /// <summary>
    /// 语言变更事件参数
    /// </summary>
    public class LanguageChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 旧的文化代码
        /// </summary>
        public string OldCultureCode { get; }

        /// <summary>
        /// 新的文化代码
        /// </summary>
        public string NewCultureCode { get; }

        /// <summary>
        /// 变更是否由用户触发
        /// </summary>
        public bool IsUserInitiated { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="oldCultureCode">旧的文化代码</param>
        /// <param name="newCultureCode">新的文化代码</param>
        /// <param name="isUserInitiated">是否由用户触发</param>
        public LanguageChangedEventArgs(
            string oldCultureCode,
            string newCultureCode,
            bool isUserInitiated = false)
        {
            OldCultureCode = oldCultureCode ?? throw new ArgumentNullException(nameof(oldCultureCode));
            NewCultureCode = newCultureCode ?? throw new ArgumentNullException(nameof(newCultureCode));
            IsUserInitiated = isUserInitiated;
        }
    }
}
