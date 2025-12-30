using Core.Utilities;
using Prism.Ioc;
using Prism.Modularity;

namespace TCPLib
{
    // Infrastructure/InfrastructureModule.cs
    public class InfrastructureModule : IModule
    {
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 这里可以注册其他基础设施服务
            // 注意：TCP服务已经在App.xaml.cs中注册
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            // 模块初始化逻辑
            var logger = containerProvider.Resolve<ILoggerService>();
            logger.Info("基础设施模块已初始化");
        }
    }
}
