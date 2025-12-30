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
            // 注册日志查看器视图
            //containerRegistry.RegisterSingleton<ILoggerService, LoggerService>();
            containerRegistry.RegisterForNavigation<Modules.LogViewer.Views.LogViewer, Modules.LogViewer.ViewModels.LogViewerViewModel>();
        }
    }
}
