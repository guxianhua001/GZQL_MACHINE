using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Models;
using Core.Utilities;
using TCPIPModule.Interfaces;

namespace TCPIPModule.Services
{
    /// <summary>
    /// TCP客户端管理服务：管理多个命名的TCP客户端连接
    /// 支持自动重连、帧协议通信
    /// </summary>
    public class TcpClientManagerServiceImpl : ITCPClientManagerService
    {
        private readonly ConcurrentDictionary<string, ITCPClient> _clients = new();
        private readonly ILoggerService _logger;

        /// <summary> 已注册的客户端字典 </summary>
        public IReadOnlyDictionary<string, ITCPClient> Clients => _clients;

        /// <summary> 是否已初始化 </summary>
        public bool IsInitialized { get; private set; }

        /// <summary> 客户端添加事件 </summary>
        public event Action<string, ITCPClient>? ClientAdded;

        /// <summary> 客户端移除事件 </summary>
        public event Action<string>? ClientRemoved;

        public TcpClientManagerServiceImpl(ILoggerService logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 从配置列表批量初始化客户端，仅连接已启用的客户端
        /// </summary>
        public async Task InitializeAsync(IEnumerable<ClientConfiguration> clientConfigs)
        {
            foreach (var config in clientConfigs.Where(c => c.IsEnabled))
            {
                await AddClientAsync(config.ClientName, config);
            }
            IsInitialized = true;
            _logger.Info($"TCP客户端管理器初始化完成，已加载 {_clients.Count} 个客户端");
        }

        /// <summary>
        /// 获取指定名称的客户端
        /// </summary>
        public ITCPClient? GetClient(string clientName)
        {
            _clients.TryGetValue(clientName, out var client);
            return client;
        }

        /// <summary>
        /// 异步获取指定名称的客户端
        /// </summary>
        public async Task<ITCPClient> GetClientAsync(string clientName)
        {
            if (_clients.TryGetValue(clientName, out var client))
                return client;

            return await Task.FromResult<ITCPClient>(null!);
        }

        /// <summary>
        /// 添加新客户端：创建TcpClientImpl实例，设置自动重连，尝试连接
        /// </summary>
        public async Task<bool> AddClientAsync(string clientName, ClientConfiguration config)
        {
            if (_clients.ContainsKey(clientName))
            {
                _logger.Warn($"TCP客户端 [{clientName}] 已存在，跳过添加");
                return false;
            }

            var client = new TcpClientImpl(clientName)
            {
                AutoReconnect = true,
                ReconnectInterval = 3000,
                DataMode = DataMode.Raw
            };

            _clients[clientName] = client;
            ClientAdded?.Invoke(clientName, client);

            if (config.IsEnabled)
            {
                try
                {
                    await client.ConnectAsync(config.IP, config.Port);
                    _logger.Info($"TCP客户端 [{clientName}] 已连接到 {config.IP}:{config.Port}");
                }
                catch (Exception ex)
                {
                    _logger.Warn($"TCP客户端 [{clientName}] 连接 {config.IP}:{config.Port} 失败: {ex.Message}，自动重连已启用");
                }
            }

            return true;
        }

        /// <summary>
        /// 移除客户端：断开连接并从字典中删除
        /// </summary>
        public async Task<bool> RemoveClientAsync(string clientName)
        {
            if (_clients.TryRemove(clientName, out var client))
            {
                await client.DisconnectAsync();
                client.Dispose();
                ClientRemoved?.Invoke(clientName);
                _logger.Info($"TCP客户端 [{clientName}] 已移除");
                return true;
            }
            return await Task.FromResult(false);
        }

        /// <summary>
        /// 向所有已连接客户端广播数据（使用帧协议）
        /// </summary>
        public async Task BroadcastAsync(byte[] data)
        {
            var message = Encoding.UTF8.GetString(data);
            var tasks = _clients.Values
                .Where(c => c.IsConnected)
                .Select(c => c.SendFrameAsync(message));
            await Task.WhenAll(tasks);
        }
    }
}
