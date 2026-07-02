using Core.Abstraction;
using Core.Abstractions.Plugins;
using Core.Services;
using Core.Abstractions.Storages;
using Prism.Ioc;
using Prism.Modularity;
using Recipe.Extensions;
using Recipe.Interfaces;
using Recipe.Plugin;
using Recipe.Services;
using Recipe.ViewModels;
using Recipe.Views;
using Framework.Services;
using Framework.Views;
using Framework.ViewModels;

namespace Recipe
{
    /// <summary>
    /// 配方管理Prism模块，负责注册配方相关的服务、视图和对话框
    /// </summary>
    public class RecipeModule : IModule
    {
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 插件管理
            containerRegistry.RegisterSingleton<IPluginManager, PluginManager>();
            containerRegistry.RegisterInstance<IPlugin>(new RecipeManagementPlugin());
            containerRegistry.RegisterSingleton<IPluginConfiguration, JsonPluginConfiguration>();

            // 配方核心服务
            containerRegistry.RegisterSingleton<IGenericStorage, JsonRecipeFileStorage>();
            containerRegistry.RegisterSingleton<IRecipeStorage, RecipeStorage>();
            containerRegistry.RegisterSingleton<IRecipePoolService, RecipePoolService>();
            containerRegistry.RegisterSingleton<IRecipeDialogService, RecipeDialogService>();

            // 受保护文件提供者：扫描配方池 ExtensionData，供 ConfigFileRetentionService
            // 清理时跳过被配方池引用的配置文件，防止切换池后配置丢失
            containerRegistry.RegisterSingleton<IProtectedFileProvider, RecipePoolProtectedFileProvider>();

            // 导航视图注册
            containerRegistry.RegisterForNavigation<RecipeManagerView, RecipeManagerViewModel>();
            containerRegistry.RegisterForNavigation<MultiStationPositionEditorView, MultiStationPositionEditorViewModel>();

            // 对话框注册
            containerRegistry.RegisterDialog<RecipeEditorDialog, RecipeEditorDialogViewModel>("RecipeEditorDialog");
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
        }
    }
}
