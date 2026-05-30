using Core.Utilities;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;

namespace LogViewer
{
    public class LogViewerModule : IModule
    {
        private readonly IRegionManager _regionManager;

        public LogViewerModule(IRegionManager regionManager)
        {
            _regionManager = regionManager;
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            // 注册视图到区域（如果需要）
            // _regionManager.RegisterViewWithRegion("LogRegion", typeof(LogViewer));
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 注册LogViewerViewModel为Singleton，避免重复订阅LoggerService.LogEvent事件
            containerRegistry.RegisterSingleton<ViewModels.LogViewerViewModel>();
            containerRegistry.RegisterForNavigation<Views.LogViewer>();
        }
    }
}
