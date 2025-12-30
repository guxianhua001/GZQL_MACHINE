// Infrastructure/Services/TCPClientManagerService.cs
using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using System.Collections.Concurrent;
using TCPLib.Adapters;

namespace TCPLib.Services
{
    public class TCPClientManagerService : ITCPClientManagerService, IDisposable
    {
        private readonly ConcurrentDictionary<string, ITCPClient> _clients = new();
        private readonly ILoggerService _logger;
        private bool _disposed = false;

        public IReadOnlyDictionary<string, ITCPClient> Clients => _clients;
        public bool IsInitialized { get; private set; }

        public event Action<string, ITCPClient> ClientAdded;
        public event Action<string> ClientRemoved;

        public TCPClientManagerService(ILoggerService logger)
        {
            _logger = logger;
        }

        public async Task InitializeAsync(IEnumerable<ClientConfiguration> clientConfigs)
        {
            if (IsInitialized)
            {
                _logger.Warn("TCPClientManagerService 已经初始化");
                return;
            }

            try
            {
                _logger.Info("开始初始化 TCPClientManagerService");

                foreach (var config in clientConfigs)
                {
                    await AddClientAsync(config.ClientName, config);
                }

                IsInitialized = true;
                _logger.Info("TCPClientManagerService 初始化完成");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "TCPClientManagerService 初始化失败");
                throw;
            }
        }

        // 同步获取客户端方法
        public ITCPClient GetClient(string clientName)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TCPClientManagerService));

            _clients.TryGetValue(clientName, out var client);
            return client;
        }

        public async Task<ITCPClient> GetClientAsync(string clientName)
        {
            return await Task.Run(() => GetClient(clientName));
        }

        public async Task<bool> AddClientAsync(string clientName, ClientConfiguration config)
        {
            try
            {
                if (_clients.ContainsKey(clientName))
                {
                    _logger.Warn($"客户端 '{clientName}' 已存在");
                    return false;
                }

                // 创建新的 TCP 客户端
                var client = CreateTCPClient(clientName, config);

                _clients[clientName] = client;

                _logger.Info($"客户端 '{clientName}' 添加成功: {config.IP}:{config.Port}");
                ClientAdded?.Invoke(clientName, client);

                // 不等待连接，避免阻塞 - 启动后台连接任务
                //_ = Task.Run(async () =>
                //{
                //    try
                //    {
                //        await client.ConnectAsync(config.IP, config.Port);
                //        _logger.Info($"客户端 '{clientName}' 连接成功");
                //    }
                //    catch (Exception ex)
                //    {
                //        _logger.Error(ex, $"客户端 '{clientName}' 连接失败");
                //        // 连接失败不会移除客户端，允许后续重连
                //    }
                //});

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"添加客户端 '{clientName}' 失败");
                return false;
            }
        }

        public async Task<bool> RemoveClientAsync(string clientName)
        {
            try
            {
                if (_clients.TryRemove(clientName, out var client))
                {
                    await client.DisconnectAsync();
                    client.Dispose();

                    _logger.Info($"客户端 '{clientName}' 移除成功");
                    ClientRemoved?.Invoke(clientName);
                    return true;
                }

                _logger.Warn($"客户端 '{clientName}' 不存在");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"移除客户端 '{clientName}' 失败");
                return false;
            }
        }

        public async Task BroadcastAsync(byte[] data)
        {
            var tasks = _clients.Values
                .Where(client => client.IsConnected)
                .Select(client => client.SendAsync(data));

            await Task.WhenAll(tasks);
            _logger.Info($"广播消息到 {tasks.Count()} 个客户端");
        }

        private ITCPClient CreateTCPClient(string clientName, ClientConfiguration config)
        {
            // 使用 TCPClientAdapter 创建真实的 TCP 客户端
            return new TCPClientAdapter(clientName, config);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                foreach (var client in _clients.Values)
                {
                    client.Dispose();
                }
                _clients.Clear();

                _logger.Info("TCPClientManagerService 已释放");
            }
        }
    }
}