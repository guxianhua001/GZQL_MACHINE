using Core.Abstraction;
using Language.ViewModels;
using Language.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace Modules.Language
{
    /// <summary>
    /// 语言模块
    /// </summary>
    public class LanguageModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            // 资源管理器加载已移除，LocalizationService 直接从 Application.Resources 查找
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 注册语言选择器视图
            containerRegistry.RegisterForNavigation<LanguageSelectorView, LanguageSelectorViewModel>("LanguageSelector");
        }
    }
}
