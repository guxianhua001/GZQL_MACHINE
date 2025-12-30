using Core.Abstraction.Factories;
using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using System.Text;

namespace TCPLib.Services
{
    public class TCPEventService : ITCPEventService, IDisposable
    {
        private readonly ITCPClientManagerService _clientManager;
        private readonly ITCPServerFactory _serverFactory;
        private readonly ILoggerService _logger;
        private ITCPServer _tcpServer;
        private bool _disposed = false;

        public event Action<string, string, int> ClientConnected;
        public event Action<string, string, int> ClientDisconnected;
        public event Action<string, string, int, string> ClientError;
        public event Action<string, int> ServerClientConnected;
        public event Action<string, int> ServerClientDisconnected;
        public event Action<string, bool> CameraCommandCompleted;
        public event Action<string, string> CameraMessageReceived;

        public bool IsInitialized { get; private set; }

        public TCPEventService(
            ITCPClientManagerService clientManager,
            ITCPServerFactory serverFactory,
            ILoggerService logger)
        {
            _clientManager = clientManager;
            _serverFactory = serverFactory;
            _logger = logger;

            // 订阅客户端管理器的事件
            _clientManager.ClientAdded += OnClientAdded;
            _clientManager.ClientRemoved += OnClientRemoved;
        }

        event Action<string, bool> ITCPEventService.CameraCommandCompleted
        {
            add
            {
                CameraCommandCompleted += value;
            }
            remove
            {
                CameraCommandCompleted -= value;
            }
        }

        public void Initialize()
        {
            if (IsInitialized)
            {
                _logger.Warn("TCPEventService 已经初始化");
                return;
            }

            try
            {
                _logger.Info("开始初始化 TCPEventService");

                // 这里可以添加其他初始化逻辑
                // 比如从配置加载客户端等

                IsInitialized = true;
                _logger.Info("TCPEventService 初始化完成");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "TCPEventService 初始化失败");
                throw;
            }
        }

        public void StartServer(ServerConfiguration serverConfig)
        {
            if (_tcpServer != null && _tcpServer.IsRunning)
            {
                _logger.Warn("TCP 服务器已经在运行");
                return;
            }

            try
            {
                _logger.Info($"启动 TCP 服务器: {serverConfig.ServerIP}:{serverConfig.Port}");

                _tcpServer = _serverFactory.CreateServer(serverConfig);

                // 订阅服务器事件
                _tcpServer.ClientConnected += OnServerClientConnected;
                _tcpServer.ClientDisconnected += OnServerClientDisconnected;
                _tcpServer.ServerError += OnServerError;
                _tcpServer.DataReceived += OnServerDataReceived;
                _tcpServer.StartAsync().Wait();

                _logger.Info("TCP 服务器启动成功");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "启动 TCP 服务器失败");
                throw;
            }
        }

        public void StopServer()
        {
            try
            {
                if (_tcpServer != null && _tcpServer.IsRunning)
                {
                    _logger.Info("停止 TCP 服务器");

                    _tcpServer.ClientConnected -= OnServerClientConnected;
                    _tcpServer.ClientDisconnected -= OnServerClientDisconnected;
                    _tcpServer.ServerError -= OnServerError;
                    _tcpServer.DataReceived -= OnServerDataReceived;
                    _tcpServer.StopAsync().Wait();
                    _tcpServer.Dispose();
                    _tcpServer = null;

                    _logger.Info("TCP 服务器已停止");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "停止 TCP 服务器时出错");
            }
        }

        public void AddClient(string clientName, ClientConfiguration config)
        {
            try
            {
                _logger.Info($"添加 TCP 客户端: {clientName} - {config.IP}:{config.Port}");
                _clientManager.AddClientAsync(clientName, config).Wait();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"添加 TCP 客户端 '{clientName}' 失败");
                throw;
            }
        }

        public void RemoveClient(string clientName)
        {
            try
            {
                _logger.Info($"移除 TCP 客户端: {clientName}");
                _clientManager.RemoveClientAsync(clientName).Wait();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"移除 TCP 客户端 '{clientName}' 失败");
                throw;
            }
        }

        private void OnClientAdded(string clientName, ITCPClient client)
        {
            _logger.Info($"客户端 '{clientName}' 已添加到管理器");

            // 订阅客户端事件
            client.ConnectionStateChanged += (c, connected) => OnClientConnectionStateChanged(clientName, c, connected);
            client.ErrorOccurred += (c, ex) => OnClientError(clientName, c, ex);

            // 如果客户端已经连接，触发连接事件
            if (client.IsConnected)
            {
                ClientConnected?.Invoke(clientName, client.RemoteIP, client.RemotePort);
            }
        }

        private void OnClientRemoved(string clientName)
        {
            _logger.Info($"客户端 '{clientName}' 已从管理器移除");
        }

        private void OnClientConnectionStateChanged(string clientName, ITCPClient client, bool connected)
        {
            if (connected)
            {
                _logger.Info($"客户端 '{clientName}' 已连接: {client.RemoteIP}:{client.RemotePort}");
                ClientConnected?.Invoke(clientName, client.RemoteIP, client.RemotePort);
            }
            else
            {
                _logger.Info($"客户端 '{clientName}' 已断开连接");
                ClientDisconnected?.Invoke(clientName, client.RemoteIP, client.RemotePort);
            }
        }

        private void OnClientError(string clientName, ITCPClient client, Exception exception)
        {
            _logger.Error(exception, $"客户端 '{clientName}' 发生错误");
            ClientError?.Invoke(clientName, client.RemoteIP, client.RemotePort, exception.Message);
        }

        private void OnServerClientConnected(ITCPClient client)
        {
            _logger.Info($"服务器客户端连接: {client.RemoteIP}:{client.RemotePort}");
            ServerClientConnected?.Invoke(client.RemoteIP, client.RemotePort);
        }

        private void OnServerClientDisconnected(ITCPClient client)
        {
            _logger.Info($"服务器客户端断开: {client.RemoteIP}:{client.RemotePort}");
            ServerClientDisconnected?.Invoke(client.RemoteIP, client.RemotePort);
        }

        private void OnServerError(Exception exception)
        {
            _logger.Error(exception, "TCP 服务器发生错误");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    StopServer();

                    if (_clientManager is IDisposable disposableManager)
                    {
                        disposableManager.Dispose();
                    }

                    _disposed = true;
                    _logger.Info("TCPEventService 已释放");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "释放 TCPEventService 时出错");
                }
            }
        }

        /// <summary>
        /// 向所有连接的客户端广播命令
        /// </summary>
        public async Task<bool> BroadcastCommandAsync(string command, int timeout = 5000)
        {
            try
            {
                _logger.Info($"广播命令: {command}");

                if (_tcpServer == null || !_tcpServer.IsRunning)
                {
                    _logger.Warn("TCP服务器未运行，无法广播命令");
                    return false;
                }

                bool success = await _tcpServer.BroadcastAsync(command);

                if (success)
                {
                    _logger.Info($"命令广播成功: {command}");
                    // 可以为每个客户端触发完成事件，或者只触发一次广播完成事件
                    CameraCommandCompleted?.Invoke("BROADCAST", true);
                }
                else
                {
                    _logger.Error($"命令广播失败: {command}");
                    CameraCommandCompleted?.Invoke("BROADCAST", false);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"广播命令异常: {command}");
                CameraCommandCompleted?.Invoke("BROADCAST", false);
                return false;
            }
        }

        /// <summary>
        /// 支持广播标识
        /// </summary>
        public async Task<bool> SendCommandAsync(string clientName, string command, int timeout = 5000)
        {
            // 如果 clientName 是 "broadcast" 或 "all"，则使用广播
            if (clientName.Equals("broadcast", StringComparison.OrdinalIgnoreCase) ||
                clientName.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return await BroadcastCommandAsync(command, timeout);
            }

            try
            {
                _logger.Info($"发送命令到 {clientName}: {command}");

                // 首先尝试使用服务器发送（针对被动连接的客户端）
                if (_tcpServer != null && _tcpServer.IsRunning)
                {
                    bool serverSendSuccess = await _tcpServer.BroadcastAsync(command);
                    if (serverSendSuccess)
                    {
                        _logger.Info($"通过服务器发送命令成功: {clientName} - {command}");
                        CameraCommandCompleted?.Invoke(clientName, true);
                        return true;
                    }
                }

                // 如果服务器发送失败，回退到使用客户端管理器（针对主动连接的客户端）
                var client = _clientManager.GetClient(clientName);
                if (client == null)
                {
                    _logger.Warn($"客户端 {clientName} 不存在");
                    return false;
                }

                if (!client.IsConnected)
                {
                    _logger.Warn($"客户端 {clientName} 未连接");
                    return false;
                }

                // 发送命令
                byte[] commandBytes = Encoding.UTF8.GetBytes(command);
                bool success = await client.SendAsync(commandBytes, timeout);

                if (success)
                {
                    _logger.Info($"命令发送成功: {clientName} - {command}");
                    CameraCommandCompleted?.Invoke(clientName, true);
                }
                else
                {
                    _logger.Error($"命令发送失败: {clientName} - {command}");
                    CameraCommandCompleted?.Invoke(clientName, false);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"发送命令异常: {clientName} - {command}");
                CameraCommandCompleted?.Invoke(clientName, false);
                return false;
            }
        }

        /// <summary>
        /// 异步发送命令并等待响应
        /// </summary>
        public async Task<string> SendCommandWithResponseAsync(string clientName, string command, int timeout = 5000)
        {
            try
            {
                _logger.Info($"发送命令到 {clientName} 并等待响应: {command}");

                // 使用同步 GetClient 方法
                var client = _clientManager.GetClient(clientName);
                if (client == null)
                {
                    _logger.Warn($"客户端 {clientName} 不存在");
                    return "ERROR: Client not found";
                }

                if (!client.IsConnected)
                {
                    _logger.Warn($"客户端 {clientName} 未连接");
                    return "ERROR: Client not connected";
                }

                // 发送命令
                byte[] commandBytes = Encoding.UTF8.GetBytes(command);
                bool sendSuccess = await client.SendAsync(commandBytes, timeout);

                if (!sendSuccess)
                {
                    _logger.Error($"命令发送失败: {clientName} - {command}");
                    return "ERROR: Send failed";
                }

                _logger.Info($"命令发送成功，等待响应: {clientName}");

                // 等待响应
                byte[] responseBytes = await client.ReceiveAsync(timeout);
                if (responseBytes == null || responseBytes.Length == 0)
                {
                    _logger.Warn($"客户端 {clientName} 无响应");
                    return "ERROR: No response";
                }

                string response = Encoding.UTF8.GetString(responseBytes).Trim();

                _logger.Info($"收到响应: {clientName} - {response}");

                // 触发消息接收事件
                CameraMessageReceived?.Invoke(clientName, response);
                CameraCommandCompleted?.Invoke(clientName, true);

                return response;
            }
            catch (TimeoutException)
            {
                _logger.Warn($"等待 {clientName} 响应超时");
                CameraCommandCompleted?.Invoke(clientName, false);
                return "ERROR: Timeout";
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"发送命令等待响应异常: {clientName}");
                CameraCommandCompleted?.Invoke(clientName, false);
                return $"ERROR: {ex.Message}";
            }
        }

        public void RegisterClient(string cameraName, string ip, int port)
        {
            //try
            //{
            //    var config = new ClientConfiguration
            //    {
            //        IP = ip,
            //        Port = port,
            //        BufferSize = 4096,
            //        ConnectionTimeout = 5000,
            //        ReceiveTimeout = 5000,
            //        SendTimeout = 5000
            //    };

            //    AddClient(clientName, config);
            //    _logger.Info($"通过RegisterClient注册客户端: {clientName} - {ip}:{port}");
            //}
            //catch (Exception ex)
            //{
            //    _logger.Error(ex, $"RegisterClient失败: {clientName}");
            //    throw;
            //}
        }

        public void UnregisterClient(string cameraName)
        {
            //RemoveClient(clientName);
            //_logger.Info($"通过UnregisterClient注销客户端: {clientName}");
        }

        private void OnServerDataReceived(string clientName, string message)
        {
            try
            {
                _logger.Info($"服务器收到来自 {clientName} 的消息: {message}");

                // 触发相机消息接收事件
                CameraMessageReceived?.Invoke(clientName, message);

                // 根据消息类型触发相应事件
                if (message.StartsWith("PHOTO_RESULT:"))
                {
                    var resultData = message.Substring("PHOTO_RESULT:".Length);
                    var result = ParseCameraResponse(resultData);
                    CameraCommandCompleted?.Invoke(clientName, result.Success);
                }
                else if (message.StartsWith("STATUS:"))
                {
                    // 处理状态消息
                    CameraCommandCompleted?.Invoke(clientName, true);
                }
                else if (message.StartsWith("ERROR:"))
                {
                    CameraCommandCompleted?.Invoke(clientName, false);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"处理服务器数据接收事件失败: {clientName}");
            }
        }

        // 解析相机响应
        private VisionResult ParseCameraResponse(string response)
        {
            if (response.StartsWith("SUCCESS:"))
            {
                return new VisionResult
                {
                    Success = true,
                    Message = response.Substring("SUCCESS:".Length)
                };
            }
            else if (response.StartsWith("ERROR:"))
            {
                return new VisionResult
                {
                    Success = false,
                    Message = response.Substring("ERROR:".Length)
                };
            }

            return new VisionResult { Success = false, Message = "响应格式错误" };
        }

        #region 事件触发方法

        /// <summary>
        /// 触发相机命令完成事件
        /// </summary>
        protected virtual void OnCameraCommandCompleted(string clientName, bool result)
        {
            CameraCommandCompleted?.Invoke(clientName, result);
        }

        /// <summary>
        /// 触发相机消息接收事件
        /// </summary>
        protected virtual void OnCameraMessageReceived(string clientName, string message)
        {
            CameraMessageReceived?.Invoke(clientName, message);
        }

        #endregion
    }
}