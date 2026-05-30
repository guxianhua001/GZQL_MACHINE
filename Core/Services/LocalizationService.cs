using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Markup;
using Core.Abstraction;
using Core.Events;
using Core.Markup;
using Core.Models;
using Prism.Events;

namespace Core.Services
{
    /// <summary>
    /// 本地化服务实现
    /// </summary>
    public class LocalizationService : ILocalizationService
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IAppSettingService _configService;
        private readonly Dictionary<string, LanguageItem> _languages = new();

        private LanguageItem _currentLanguage;
        private string _currentCultureCode = "zh-CN";

        /// <summary>
        /// 当前语言
        /// </summary>
        public LanguageItem CurrentLanguage => _currentLanguage;

        /// <summary>
        /// 当前区域性代码
        /// </summary>
        public string CurrentCultureCode => _currentCultureCode;

        /// <summary>
        /// 支持的语言列表
        /// </summary>
        public IReadOnlyList<LanguageItem> SupportedLanguages => _languages.Values
            .OrderBy(l => l.SortIndex)
            .ThenBy(l => l.DisplayName)
            .ToList();

        /// <summary>
        /// 语言变更事件
        /// </summary>
        public event EventHandler<LanguageChangedEventArgs> LanguageChanged;

        /// <summary>
        /// 构造函数：初始化语言列表 + 加载配置中的语言
        /// </summary>
        public LocalizationService(
            IEventAggregator eventAggregator,
            IAppSettingService configService)
        {
            _eventAggregator = eventAggregator;
            _configService = configService;

            InitializeLanguages();
            LoadLanguageFromConfiguration();
            PublishLanguageChanged();
        }

        /// <summary>
        /// 初始化支持的语言列表
        /// </summary>
        private void InitializeLanguages()
        {
            _languages.Clear();

            var languages = new List<LanguageItem>
            {
                new("中文（简体）", "zh-CN", "/Assets/Flags/china.png", 1, true),
                new("English", "en-US", "/Assets/Flags/usa.png", 2),
            };

            foreach (var language in languages)
            {
                _languages[language.CultureCode] = language;
            }
        }

        /// <summary>
        /// 从配置服务加载上次选择的语言
        /// </summary>
        private void LoadLanguageFromConfiguration()
        {
            try
            {
                var savedLanguage = _configService.Settings?.Language ?? "zh-CN";

                if (!string.IsNullOrEmpty(savedLanguage) && _languages.ContainsKey(savedLanguage))
                {
                    _currentCultureCode = savedLanguage;
                }
                else
                {
                    // 使用系统语言或默认语言
                    var systemLanguage = CultureInfo.CurrentUICulture.Name;
                    if (_languages.ContainsKey(systemLanguage))
                    {
                        _currentCultureCode = systemLanguage;
                    }
                    else if (systemLanguage.StartsWith("zh-"))
                    {
                        _currentCultureCode = "zh-CN";
                    }
                    else
                    {
                        _currentCultureCode = "en-US";
                    }
                }

                _currentLanguage = _languages[_currentCultureCode];
                ApplyLanguageSettings(_currentCultureCode);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载语言设置失败: {ex.Message}");
                _currentCultureCode = "zh-CN";
                _currentLanguage = _languages["zh-CN"];
            }
        }

        /// <summary>
        /// 切换语言：更新线程 Culture + 替换 XAML 资源字典 + 保存配置 + 触发事件 + 刷新 LangExtension
        /// </summary>
        public bool SetLanguage(string cultureCode)
        {
            if (!_languages.ContainsKey(cultureCode))
                return false;

            if (_currentCultureCode == cultureCode)
                return true;

            var oldCultureCode = _currentCultureCode;
            _currentCultureCode = cultureCode;
            _currentLanguage = _languages[cultureCode];

            try
            {
                ApplyLanguageSettings(cultureCode);
                SaveLanguageToConfiguration(cultureCode);

                // 通知所有 LangExtension 实例刷新
                LangExtension.InvalidateAll();

                // 触发语言变更事件
                OnLanguageChanged(oldCultureCode, cultureCode);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"切换语言失败: {ex.Message}");
                _currentCultureCode = oldCultureCode;
                _currentLanguage = _languages[oldCultureCode];
                return false;
            }
        }

        /// <summary>
        /// 应用语言设置：更新线程 Culture + 替换 XAML 资源字典 + 发布 Prism 事件
        /// </summary>
        private void ApplyLanguageSettings(string cultureCode)
        {
            // 设置线程区域性
            var culture = new CultureInfo(cultureCode);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = culture;

            // 在 UI 线程上设置应用程序语言和资源字典
            Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    var xmlLanguage = XmlLanguage.GetLanguage(culture.IetfLanguageTag);
                    Application.Current.Resources[FrameworkElement.LanguageProperty] = xmlLanguage;

                    if (Application.Current.MainWindow != null)
                    {
                        Application.Current.MainWindow.Language = xmlLanguage;
                    }

                    // 替换 MergedDictionaries 中的语言资源字典（保持 DynamicResource 向后兼容）
                    UpdateResourceDictionaries(cultureCode);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"更新应用程序语言失败: {ex.Message}");
                }
            });

            PublishLanguageChanged();
        }

        /// <summary>
        /// 替换 MergedDictionaries 中的语言资源字典
        /// </summary>
        private void UpdateResourceDictionaries(string cultureCode)
        {
            // 移除现有的语言资源字典
            var dictionariesToRemove = Application.Current.Resources.MergedDictionaries
                .Where(d => d.Source?.OriginalString?.Contains("Languages/") == true)
                .ToList();

            foreach (var dictionary in dictionariesToRemove)
            {
                Application.Current.Resources.MergedDictionaries.Remove(dictionary);
            }

            try
            {
                // 添加新的语言资源字典
                var newDictionary = new ResourceDictionary
                {
                    Source = new Uri($"/MainApp;component/Languages/Strings.{cultureCode}.xaml",
                                   UriKind.RelativeOrAbsolute)
                };

                Application.Current.Resources.MergedDictionaries.Add(newDictionary);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载语言资源失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存语言设置到配置文件
        /// </summary>
        private void SaveLanguageToConfiguration(string cultureCode)
        {
            try
            {
                if (_configService?.Settings != null)
                {
                    _configService.Settings.Language = cultureCode;
                    _configService.Save();

                    System.Diagnostics.Debug.WriteLine($"语言设置已保存: {cultureCode}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存语言设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取资源字符串：从 Application.Current.Resources 查找，找不到返回 [key]
        /// </summary>
        public string GetResource(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            try
            {
                var resource = Application.Current.TryFindResource(key);
                return resource?.ToString() ?? $"[{key}]";
            }
            catch
            {
                return $"[{key}]";
            }
        }

        /// <summary>
        /// 获取带格式的资源字符串
        /// </summary>
        public string GetResource(string key, params object[] args)
        {
            var format = GetResource(key);

            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        /// <summary>
        /// 获取资源字符串（支持默认值）
        /// </summary>
        public string GetResourceOrDefault(string key, string defaultValue = null)
        {
            if (TryGetResource(key, out var value))
                return value;

            return defaultValue ?? $"[{key}]";
        }

        /// <summary>
        /// 尝试获取资源字符串
        /// </summary>
        public bool TryGetResource(string key, out string value)
        {
            value = null;

            if (string.IsNullOrEmpty(key))
                return false;

            try
            {
                var resource = Application.Current.TryFindResource(key);
                if (resource != null)
                {
                    value = resource.ToString();
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        /// <summary>
        /// 触发语言变更事件
        /// </summary>
        protected virtual void OnLanguageChanged(string oldCultureCode, string newCultureCode)
        {
            LanguageChanged?.Invoke(this,
                new LanguageChangedEventArgs(oldCultureCode, newCultureCode, isUserInitiated: true));
        }

        /// <summary>
        /// 发布 Prism 语言变更事件
        /// </summary>
        private void PublishLanguageChanged()
        {
            _eventAggregator.GetEvent<LanguageChangedEvent>().Publish(_currentCultureCode);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _languages.Clear();
            LanguageChanged = null;
        }
    }
}
