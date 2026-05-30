using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TCPIPModule.Interfaces;

namespace TCPIPModule.Services
{
    /// <summary>
    /// 基于System.Net.Sockets的TCP服务器实现
    /// 支持多客户端连接管理、Raw/Frame数据模式、广播和定向发送
    /// 修复：使用InitializeFromAcceptedClient正确初始化已连接客户端的读取循环
    /// </summary>
    public class TcpServerImpl : ITCPServer
    {
        private TcpListener? _listener;
        private CancellationTokenSource? _acceptCts;
        private readonly ConcurrentDictionary<string, ConnectedClient> _clients = new();
        private readonly object _lock = new();
        private int _clientIdCounter;

        /// <summary> 服务器是否正在运行 </summary>
        public bool IsRunning { get; private set; }

        /// <summary> 当前已连接的客户端数量 </summary>
        public int ConnectedClientsCount => _clients.Count;

        /// <summary> 客户端连接事件 </summary>
        public event Action<ITCPClient>? ClientConnected;

        /// <summary> 客户端断开事件 </summary>
        public event Action<ITCPClient>? ClientDisconnected;

        /// <summary> 服务器错误事件 </summary>
        public event Action<Exception>? ServerError;

        /// <summary> 收到数据事件：(clientIdentifier, message) </summary>
        public event Action<string, string>? DataReceived;

        /// <summary> 服务器监听配置 </summary>
        public string ListenIP { get; set; } = "0.0.0.0";
        public int ListenPort { get; set; } = 8080;

        /// <summary>
        /// 数据模式：默认Raw，兼容标准TCP客户端
        /// </summary>
        public DataMode DataMode { get; set; } = DataMode.Raw;

        /// <summary>
        /// 启动TCP服务器，开始监听并接受客户端连接
        /// </summary>
        public async Task StartAsync()
        {
            if (IsRunning) return;

            try
            {
                _acceptCts = new CancellationTokenSource();
                var endpoint = new IPEndPoint(IPAddress.Parse(ListenIP), ListenPort);
                _listener = new TcpListener(endpoint);
                _listener.Start();
                IsRunning = true;

                _ = AcceptLoopAsync(_acceptCts.Token);
            }
            catch (Exception ex)
            {
                IsRunning = false;
                ServerError?.Invoke(ex);
                throw;
            }
        }

        /// <summary>
        /// 停止TCP服务器，断开所有客户端连接
        /// </summary>
        public Task StopAsync()
        {
            if (!IsRunning) return Task.CompletedTask;

            IsRunning = false;
            _acceptCts?.Cancel();

            foreach (var kvp in _clients)
            {
                try { kvp.Value.Client.DisconnectAsync().ConfigureAwait(false).GetAwaiter().GetResult(); }
                catch { }
            }
            _clients.Clear();

            _listener?.Stop();
            _listener = null;
            return Task.CompletedTask;
        }

        /// <summary>
        /// 向所有已连接客户端广播消息（Raw模式直接发送字符串，Frame模式加帧头）
        /// </summary>
        public async Task<bool> BroadcastAsync(string message)
        {
            var tasks = _clients.Values
                .Where(c => c.Client.IsConnected)
                .Select(c => c.Client.SendFrameAsync(message));

            try
            {
                await Task.WhenAll(tasks);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 向指定客户端发送消息
        /// </summary>
        public async Task<bool> SendToClientAsync(string clientIdentifier, string message)
        {
            if (_clients.TryGetValue(clientIdentifier, out var connectedClient) &&
                connectedClient.Client.IsConnected)
            {
                await connectedClient.Client.SendFrameAsync(message);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取所有已连接客户端的ITCPClient接口
        /// </summary>
        public IEnumerable<ITCPClient> GetConnectedClients()
        {
            return _clients.Values.Select(c => c.Client);
        }

        /// <summary>
        /// 接受客户端连接的循环
        /// 修复：使用InitializeFromAcceptedClient正确初始化已连接客户端
        /// 使其读取循环启动，能够接收客户端发送的数据
        /// </summary>
        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && IsRunning)
            {
                try
                {
                    var tcpClient = await _listener!.AcceptTcpClientAsync(token);
                    var clientId = $"Client_{Interlocked.Increment(ref _clientIdCounter)}";

                    var client = new TcpClientImpl(clientId)
                    {
                        AutoReconnect = false,
                        DataMode = DataMode
                    };

                    client.ConnectionStateChanged += (c, connected) =>
                    {
                        if (!connected)
                        {
                            _clients.TryRemove(clientId, out _);
                            ClientDisconnected?.Invoke(c);
                        }
                    };

                    client.DataReceived += (c, data) =>
                    {
                        var message = Encoding.UTF8.GetString(data);
                        DataReceived?.Invoke(clientId, message);
                    };

                    client.ErrorOccurred += (c, ex) =>
                    {
                        ServerError?.Invoke(ex);
                    };

                    // 关键修复：使用InitializeFromAcceptedClient初始化已连接的socket
                    // 而不是调用ConnectAsync（那会尝试重新连接）
                    client.InitializeFromAcceptedClient(tcpClient);

                    var connectedClient = new ConnectedClient(clientId, client);
                    _clients[clientId] = connectedClient;

                    ClientConnected?.Invoke(client);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    ServerError?.Invoke(ex);
                }
            }
        }

        public void Dispose()
        {
            StopAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            _acceptCts?.Dispose();
        }

        /// <summary>
        /// 已连接客户端的包装类
        /// </summary>
        private class ConnectedClient
        {
            public string Id { get; }
            public ITCPClient Client { get; }

            public ConnectedClient(string id, ITCPClient client)
            {
                Id = id;
                Client = client;
            }
        }
    }
}
