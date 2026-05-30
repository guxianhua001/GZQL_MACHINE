using Prism.Mvvm;
using Prism.Events;
using System.Linq;
using System.Reflection;
using Core.Attributes;
using Core.Events;
using Core.Abstraction;

namespace Core.ViewModels
{
    /// <summary>
    /// 支持本地化的 ViewModel 基类（简化版）
    /// 提供 L() 快捷翻译方法，语言切换时自动刷新 [Localized] 标记的属性
    /// </summary>
    public abstract class LocalizedViewModelBase : BindableBase
    {
        protected readonly ILocalizationService LocalizationService;
        protected readonly IEventAggregator EventAggregator;

        protected LocalizedViewModelBase(
            ILocalizationService localizationService,
            IEventAggregator eventAggregator)
        {
            LocalizationService = localizationService;
            EventAggregator = eventAggregator;

            // 订阅语言变更事件
            EventAggregator.GetEvent<LanguageChangedEvent>()
                .Subscribe(OnLanguageChangedInternal, ThreadOption.UIThread);
            LocalizationService.LanguageChanged += OnLanguageChangedEventHandler;
        }

        /// <summary>快捷翻译方法</summary>
        protected string L(string key) => LocalizationService.GetResource(key);

        /// <summary>带参数的快捷翻译方法</summary>
        protected string L(string key, params object[] args) => LocalizationService.GetResource(key, args);

        /// <summary>语言变更时刷新所有 [Localized] 标记的属性</summary>
        private void OnLanguageChangedInternal(string cultureCode)
        {
            RefreshLocalizedProperties();
            OnLanguageChanged();
        }

        private void OnLanguageChangedEventHandler(object sender, LanguageChangedEventArgs e)
        {
            RefreshLocalizedProperties();
            OnLanguageChanged();
        }

        /// <summary>刷新所有标记了 [Localized] 特性的属性</summary>
        protected void RefreshLocalizedProperties()
        {
            var properties = GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                .Where(p => p.GetCustomAttribute<LocalizedAttribute>() != null);

            foreach (var property in properties)
            {
                RaisePropertyChanged(property.Name);
            }
        }

        /// <summary>语言变更时的自定义处理（子类可重写）</summary>
        protected virtual void OnLanguageChanged() { }
    }
}
