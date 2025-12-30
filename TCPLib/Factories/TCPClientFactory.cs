using Core.Abstraction;
using Core.Abstraction.Factories;
using Core.Models;
using Core.Utilities;
using TCPLib.Adapters;

namespace TCPLib.Factories
{
    // Infrastructure/Factories/TCPClientFactory.cs
    public class TCPClientFactory : ITCPClientFactory
    {
        private readonly ILoggerService _logger;

        public TCPClientFactory(ILoggerService logger)
        {
            _logger = logger;
        }

        public ITCPClient CreateClient(string clientName, ClientConfiguration config)
        {
            try
            {
                var client = new TCPClientAdapter(clientName, config);
                _logger.Debug($"创建TCP客户端 '{clientName}': {config.IP}:{config.Port}");
                return client;
            }
            catch (Exception ex)
            {
                _logger.Error($"创建TCP客户端 '{clientName}' 失败: {ex.Message}");
                throw;
            }
        }

        public ITCPClient CreateClient(string clientName, string ip, int port)
        {
            var config = new ClientConfiguration
            {
                ClientName = clientName,
                IP = ip,
                Port = port
            };
            return CreateClient(clientName, config);
        }
    }

    // Infrastructure/Factories/TCPServerFactory.cs
    public class TCPServerFactory : ITCPServerFactory
    {
        private readonly ILoggerService _logger;

        public TCPServerFactory(ILoggerService logger)
        {
            _logger = logger;
        }

        public ITCPServer CreateServer(ServerConfiguration config)
        {
            try
            {
                var server = new TCPServerAdapter(config, _logger);
                _logger.Debug($"创建TCP服务器: {config.ServerIP}:{config.Port}");
                return server;
            }
            catch (Exception ex)
            {
                _logger.Error($"创建TCP服务器失败: {ex.Message}");
                throw;
            }
        }

        public ITCPServer CreateServer(string ip, int port, int maxClients, string encodingMethod)
        {
            var config = new ServerConfiguration
            {
                ServerIP = ip,
                Port = port,
                MaxClients = maxClients,
                EncodingMethod = encodingMethod
            };
            return CreateServer(config);
        }
    }
}
