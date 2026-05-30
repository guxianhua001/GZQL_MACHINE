using Core.Abstraction;
using Core.Utilities;
using Prism.Ioc;
using Prism.Modularity;
using TCPIPModule.Interfaces;
using TCPIPModule.Services;
using TCPIPModule.ViewModels;
using TCPIPModule.Views;

namespace TCPIPModule
{
    /// <summary>
    /// TCPIP独立模块 - 自包含TCP服务实现，不依赖外部TCPLib
    /// 使用System.Net.Sockets实现，配置持久化到appsettings.json
    /// </summary>
    public class TCPIPModule : IModule
    {
        /// <summary>
        /// 注册TCP相关服务和视图导航
        /// </summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 注册TCP客户端管理服务（单例）
            containerRegistry.RegisterSingleton<ITCPClientManagerService, TcpClientManagerServiceImpl>();

            // 注册TCP事件服务（单例）
            containerRegistry.RegisterSingleton<ITCPEventService, TcpEventServiceImpl>();

            // 注册TcpConfig视图导航
            containerRegistry.RegisterForNavigation<TcpConfigView, TcpConfigViewModel>();
        }

        /// <summary>
        /// 模块初始化：从appsettings.json读取TCP配置，根据Mode字段分别启动服务器或添加客户端
        /// 使用异步非阻塞方式初始化，避免ConnectAsync超时导致应用启动卡死
        /// </summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var appConfig = containerProvider.Resolve<IAppSettingService>();
            var tcpEventService = containerProvider.Resolve<ITCPEventService>();
            var logger = containerProvider.Resolve<ILoggerService>();

            // 初始化事件服务
            tcpEventService.Initialize();

            // 异步初始化TCP系统，不阻塞模块加载
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    // 根据每个配置项的Mode字段区分处理：Server模式启动监听，Client模式发起连接
                    foreach (var clientConfig in appConfig.Clients.Where(c => c.IsEnabled))
                    {
                        try
                        {
                            if (clientConfig.Mode == "Server")
                            {
                                // 服务端模式：为该配置项启动独立的TCP服务器监听
                                var serverConfig = new Core.Models.ServerConfiguration
                                {
                                    ServerIP = string.IsNullOrEmpty(clientConfig.IP) ? "0.0.0.0" : clientConfig.IP,
                                    Port = clientConfig.Port,
                                    EncodingMethod = "UTF-8"
                                };
                                tcpEventService.StartServer(serverConfig, clientConfig.ClientName);
                                logger.Info($"TCP服务器 '{clientConfig.ClientName}' 已启动监听 {serverConfig.ServerIP}:{serverConfig.Port}");
                            }
                            else
                            {
                                // 客户端模式：创建TCP客户端连接到远程服务端
                                await tcpEventService.AddClientAsync(clientConfig.ClientName, clientConfig);
                                logger.Info($"TCP客户端 '{clientConfig.ClientName}' 已连接 {clientConfig.IP}:{clientConfig.Port}");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            logger.Error($"初始化TCP配置项 '{clientConfig.ClientName}' 失败(Mode={clientConfig.Mode}): {ex.Message}");
                        }
                    }

                    logger.Info($"TCP系统初始化完成，共 {appConfig.Clients.Count(c => c.IsEnabled)} 项配置");
                }
                catch (System.Exception ex)
                {
                    logger.Error($"TCP系统初始化失败: {ex.Message}");
                }
            });
        }
    }
}
