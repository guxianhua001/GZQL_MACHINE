using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AlarmModule.Interfaces;
using AlarmModule.Models;
using Core.Models;
using Core.Utilities;
using TCPIPModule.Interfaces;

namespace TCPIPModule.Services
{
    /// <summary>
    /// TCP事件服务实现：高层命令发送/接收协调器
    /// 管理多个TCP服务器实例和客户端的生命周期，提供命令发送和响应等待功能
    /// 默认使用Raw模式兼容标准TCP设备，可选Frame模式防粘包
    /// 修复：使用ConcurrentDictionary支持多服务器实例并行运行（如TCP_1/TCP_2）
    /// 修复：维护连接状态快照，支持迟到订阅者回放，解决首次上线日志丢失问题
    /// </summary>
    public class TcpEventServiceImpl : ITCPEventService
    {
        private readonly ITCPClientManagerService _clientManager;
        private readonly ILoggerService _logger;
        private readonly IAlarmService? _alarmService;
        private readonly ConcurrentDictionary<string, ITCPServer> _servers = new();

        /// <summary>
        /// 连接状态快照：记录每个服务器名称下已连接的客户端列表
        /// 用于解决ViewModel订阅事件前客户端已连接导致的首次上线日志丢失问题
        /// Key=服务器名称(TCP_1), Value=已连接客户端列表(ip, port)
        /// </summary>
        private readonly ConcurrentDictionary<string, List<(string ip, int port)>> _connectedSnapshot = new();

        /// <summary> 客户端连接事件：(clientName, ip, port) </summary>
        public event Action<string, string, int>? ClientConnected;

        /// <summary> 客户端断开事件：(clientName, ip, port) </summary>
        public event Action<string, string, int>? ClientDisconnected;

        /// <summary> 客户端错误事件：(clientName, ip, port, errorMessage) </summary>
        public event Action<string, string, int, string>? ClientError;

        /// <summary> 服务端客户端连接事件：(clientId, port) </summary>
        public event Action<string, int>? ServerClientConnected;

        /// <summary> 服务端客户端断开事件：(clientId, port) </summary>
        public event Action<string, int>? ServerClientDisconnected;

        /// <summary> 相机消息接收事件：(cameraName, message) </summary>
        public event Action<string, string>? CameraMessageReceived;

        /// <summary> 相机命令完成事件：(cameraName, success) </summary>
        public event Action<string, bool>? CameraCommandCompleted;

        /// <summary> 是否已初始化 </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 获取所有运行中的TCP服务器名称列表（Server模式）
        /// </summary>
        public IEnumerable<string> GetServerNames()
        {
            return _servers.Keys.ToList();
        }

        public TcpEventServiceImpl(ITCPClientManagerService clientManager, ILoggerService logger, IAlarmService? alarmService = null)
        {
            _clientManager = clientManager;
            _logger = logger;
            _alarmService = alarmService;
        }

        /// <summary>
        /// 初始化事件服务，订阅客户端管理器事件
        /// </summary>
        public void Initialize()
        {
            _clientManager.ClientAdded += OnClientAdded;
            _clientManager.ClientRemoved += OnClientRemoved;
            IsInitialized = true;
            _logger.Info("TCP事件服务初始化完成");
        }

        /// <summary>
        /// 启动TCP服务器，监听指定配置的IP和端口
        /// serverName: 服务器配置名称（如 TCP_1、TCP_2），用于CameraMessageReceived事件的cameraName参数
        /// 支持多服务器实例并行运行，每个服务器的DataReceived事件独立绑定对应的serverName
        /// </summary>
        public void StartServer(ServerConfiguration serverConfig, string serverName = "")
        {
            try
            {
                if (_servers.ContainsKey(serverName))
                {
                    _logger.Warn($"TCP服务器 '{serverName}' 已存在，先停止旧实例");
                    StopServer(serverName);
                }

                var server = new TcpServerImpl
                {
                    ListenIP = serverConfig.ServerIP,
                    ListenPort = serverConfig.Port,
                    DataMode = DataMode.Raw
                };

                // 使用闭包捕获serverName，确保每个服务器的DataReceived事件使用自己的名称
                var capturedName = serverName;

                server.ClientConnected += serverClient =>
                {
                    ServerClientConnected?.Invoke(serverClient.ClientName, serverClient.RemotePort);
                    _logger.Info($"TCP服务器[{capturedName}]接受客户端连接: {serverClient.ClientName} ({serverClient.RemoteIP}:{serverClient.RemotePort})");
                    ClientConnected?.Invoke(capturedName, serverClient.RemoteIP, serverClient.RemotePort);

                    // 更新连接状态快照，支持迟到订阅者回放
                    _connectedSnapshot.AddOrUpdate(capturedName,
                        new List<(string, int)> { (serverClient.RemoteIP, serverClient.RemotePort) },
                        (_, list) => { lock (list) { list.Add((serverClient.RemoteIP, serverClient.RemotePort)); } return list; });
                };

                server.ClientDisconnected += serverClient =>
                {
                    ServerClientDisconnected?.Invoke(serverClient.ClientName, serverClient.RemotePort);
                    _logger.Info($"TCP服务器[{capturedName}]客户端断开: {serverClient.ClientName}");
                    ClientDisconnected?.Invoke(capturedName, serverClient.RemoteIP, serverClient.RemotePort);

                    // 更新连接状态快照
                    if (_connectedSnapshot.TryGetValue(capturedName, out var list))
                    {
                        lock (list) { list.RemoveAll(x => x.port == serverClient.RemotePort); }
                    }

                    // 触发掉线报警（上传到服务器）
                    TriggerDisconnectAlarm(capturedName, serverClient.RemoteIP, serverClient.RemotePort);
                };

                // 关键修复：每个服务器的DataReceived事件使用闭包捕获的serverName
                // 解决多服务器场景下日志都显示为最后一个服务器名称的问题
                server.DataReceived += (clientId, message) =>
                {
                    var sourceName = string.IsNullOrEmpty(capturedName) ? clientId : capturedName;
                    _logger.Info($"TCP服务器收到数据: 服务器={sourceName}, 客户端={clientId}, 消息={message}");
                    CameraMessageReceived?.Invoke(sourceName, message);
                };

                server.ServerError += ex =>
                {
                    _logger.Error($"TCP服务器[{capturedName}]错误: {ex.Message}");
                    ClientError?.Invoke(capturedName, "", 0, $"服务器异常: {ex.Message}");
                    TriggerErrorAlarm(capturedName, ex.Message);
                };

                server.StartAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                _servers[serverName] = server;
                _logger.Info($"TCP服务器[{serverName}]启动成功: {serverConfig.ServerIP}:{serverConfig.Port} (当前共{_servers.Count}个服务器)");
            }
            catch (Exception ex)
            {
                _logger.Error($"TCP服务器[{serverName}]启动失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止TCP服务器
        /// serverName: 服务器配置名称，为空时停止所有服务器
        /// </summary>
        public void StopServer(string serverName = "")
        {
            if (string.IsNullOrEmpty(serverName))
            {
                // 停止所有服务器
                foreach (var kvp in _servers.ToList())
                {
                    try
                    {
                        kvp.Value.StopAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                        kvp.Value.Dispose();
                        _logger.Info($"TCP服务器[{kvp.Key}]已停止");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"停止TCP服务器[{kvp.Key}]失败: {ex.Message}");
                    }
                }
                _servers.Clear();
            }
            else
            {
                // 停止指定服务器
                if (_servers.TryRemove(serverName, out var server))
                {
                    try
                    {
                        server.StopAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                        server.Dispose();
                        _logger.Info($"TCP服务器[{serverName}]已停止");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"停止TCP服务器[{serverName}]失败: {ex.Message}");
                    }
                }
                else
                {
                    _logger.Warn($"TCP服务器[{serverName}]不存在或已停止");
                }
            }
        }

        /// <summary>
        /// 添加TCP客户端并订阅其事件（异步版本，避免同步阻塞）
        /// </summary>
        public async Task AddClientAsync(string clientName, ClientConfiguration config)
        {
            await _clientManager.AddClientAsync(clientName, config).ConfigureAwait(false);
            _logger.Info($"TCP客户端 [{clientName}] 已添加: {config.IP}:{config.Port}");
        }

        /// <summary>
        /// 添加TCP客户端并订阅其事件（同步包装，仅用于兼容旧调用）
        /// 注意：内部使用Task.Run避免同步阻塞导致死锁
        /// </summary>
        public void AddClient(string clientName, ClientConfiguration config)
        {
            Task.Run(() => AddClientAsync(clientName, config)).Wait();
            _logger.Info($"TCP客户端 [{clientName}] 已添加: {config.IP}:{config.Port}");
        }

        /// <summary>
        /// 移除TCP客户端
        /// </summary>
        public void RemoveClient(string clientName)
        {
            _clientManager.RemoveClientAsync(clientName).ConfigureAwait(false).GetAwaiter().GetResult();
            _logger.Info($"TCP客户端 [{clientName}] 已移除");
        }

        /// <summary>
        /// 向所有已连接客户端广播命令
        /// 支持多服务器场景：遍历所有运行中的服务器进行广播
        /// </summary>
        public async Task<bool> BroadcastCommandAsync(string command, int timeout = 5000)
        {
            try
            {
                var tasks = new List<Task<bool>>();

                // 向所有运行中的服务器广播
                foreach (var server in _servers.Values.Where(s => s.IsRunning))
                {
                    tasks.Add(server.BroadcastAsync(command));
                }

                // 向客户端管理器中的所有客户端广播
                var message = Encoding.UTF8.GetBytes(command);
                tasks.Add(Task.Run(async () =>
                {
                    await _clientManager.BroadcastAsync(message);
                    return true;
                }));

                if (tasks.Count == 0)
                {
                    _logger.Warn("没有可用的TCP连接进行广播");
                    return false;
                }

                var results = await Task.WhenAll(tasks);
                return results.All(r => r);
            }
            catch (Exception ex)
            {
                _logger.Error($"广播命令失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 向指定客户端发送命令
        /// 路由逻辑：
        /// 1. 优先检查客户端管理器中的已注册客户端（Client模式）
        /// 2. 若未找到，检查是否为Server模式的服务器名称，向该服务器的所有客户端广播
        /// 3. 若服务器名称也不匹配，尝试在所有服务器的已连接客户端中查找
        /// </summary>
        public async Task<bool> SendCommandAsync(string cameraName, string command, int timeout = 5000)
        {
            if (cameraName.Equals("broadcast", StringComparison.OrdinalIgnoreCase) ||
                cameraName.Equals("all", StringComparison.OrdinalIgnoreCase))
                return await BroadcastCommandAsync(command, timeout);

            // 优先检查客户端管理器（Client模式）
            var client = _clientManager.GetClient(cameraName);
            if (client != null)
            {
                if (!client.IsConnected)
                {
                    _logger.Warn($"TCP客户端 [{cameraName}] 未连接");
                    return false;
                }

                try
                {
                    return await client.SendFrameAsync(command, timeout);
                }
                catch (Exception ex)
                {
                    _logger.Error($"发送命令到 [{cameraName}] 失败: {ex.Message}");
                    ClientError?.Invoke(cameraName, client.RemoteIP, client.RemotePort, ex.Message);
                    return false;
                }
            }

            // 客户端管理器中未找到，检查是否为Server模式的服务器名称
            if (_servers.TryGetValue(cameraName, out var targetServer) && targetServer.IsRunning)
            {
                // cameraName是服务器配置名（如TCP_1），向该服务器的所有已连接客户端广播
                _logger.Debug($"通过服务器[{cameraName}]广播消息到所有已连接客户端");
                return await targetServer.BroadcastAsync(command);
            }

            // 不是已知的服务器名称，尝试在所有服务器的已连接客户端中精确匹配
            foreach (var server in _servers.Values.Where(s => s.IsRunning))
            {
                var sent = await server.SendToClientAsync(cameraName, command);
                if (sent) return true;
            }

            _logger.Warn($"TCP [{cameraName}] 不存在：既不是Client模式的客户端，也不是Server模式的服务器");
            return false;
        }

        /// <summary>
        /// 向指定客户端发送命令并等待响应
        /// Client模式：通过ITCPClient.SendAndReceiveAsync等待帧协议响应
        /// Server模式：广播命令后通过CameraMessageReceived事件等待客户端返回数据
        /// </summary>
        public async Task<string> SendCommandWithResponseAsync(string cameraName, string command, int timeout = 5000)
        {
            // 优先检查客户端管理器（Client模式）
            var client = _clientManager.GetClient(cameraName);
            if (client != null)
            {
                if (!client.IsConnected)
                    throw new InvalidOperationException($"TCP客户端 [{cameraName}] 未连接");

                try
                {
                    var response = await client.SendAndReceiveAsync(command, timeout);

                    CameraMessageReceived?.Invoke(cameraName, response);
                    CameraCommandCompleted?.Invoke(cameraName, true);

                    return response;
                }
                catch (TimeoutException)
                {
                    CameraCommandCompleted?.Invoke(cameraName, false);
                    ClientError?.Invoke(cameraName, client.RemoteIP, client.RemotePort, "响应超时");
                    throw;
                }
                catch (Exception ex)
                {
                    CameraCommandCompleted?.Invoke(cameraName, false);
                    ClientError?.Invoke(cameraName, client.RemoteIP, client.RemotePort, ex.Message);
                    throw;
                }
            }

            // 客户端管理器中未找到，检查是否为Server模式的服务器名称
            if (_servers.TryGetValue(cameraName, out var targetServer) && targetServer.IsRunning)
            {
                var sent = await targetServer.BroadcastAsync(command);
                if (!sent)
                    throw new InvalidOperationException($"服务器[{cameraName}]无法发送消息：无已连接客户端");

                // Server模式：广播后等待客户端通过CameraMessageReceived事件返回数据
                return await WaitForServerResponseAsync(cameraName, timeout);
            }

            throw new InvalidOperationException($"TCP [{cameraName}] 不存在且无对应服务器运行");
        }

        /// <summary>
        /// Server模式下等待客户端返回数据的实现
        /// 通过订阅CameraMessageReceived事件，在超时时间内等待匹配cameraName的响应
        /// </summary>
        private async Task<string> WaitForServerResponseAsync(string cameraName, int timeout)
        {
            var tcs = new TaskCompletionSource<string>();
            var cts = new CancellationTokenSource(timeout);

            // 注册超时取消回调
            cts.Token.Register(() =>
            {
                tcs.TrySetResult(string.Empty);
                _logger.Warn($"Server模式等待[{cameraName}]响应超时({timeout}ms)");
            });

            Action<string, string> handler = (sourceName, message) =>
            {
                if (sourceName == cameraName)
                {
                    tcs.TrySetResult(message);
                }
            };

            try
            {
                CameraMessageReceived += handler;
                var response = await tcs.Task;

                if (string.IsNullOrEmpty(response))
                {
                    CameraCommandCompleted?.Invoke(cameraName, false);
                    ClientError?.Invoke(cameraName, "", 0, "Server模式等待响应超时");
                    throw new TimeoutException($"Server模式等待[{cameraName}]响应超时({timeout}ms)");
                }

                CameraCommandCompleted?.Invoke(cameraName, true);
                _logger.Info($"Server模式[{cameraName}]收到响应: {response}");
                return response;
            }
            finally
            {
                CameraMessageReceived -= handler;
                cts.Dispose();
            }
        }

        /// <summary>
        /// 注册客户端（快捷方式）
        /// </summary>
        public void RegisterClient(string cameraName, string ip, int port)
        {
            AddClient(cameraName, new ClientConfiguration { ClientName = cameraName, IP = ip, Port = port });
        }

        /// <summary>
        /// 注销客户端（快捷方式）
        /// </summary>
        public void UnregisterClient(string cameraName)
        {
            RemoveClient(cameraName);
        }

        /// <summary>
        /// 客户端添加事件处理：订阅连接状态和数据接收事件
        /// </summary>
        private void OnClientAdded(string clientName, ITCPClient client)
        {
            client.ConnectionStateChanged += (c, connected) =>
            {
                if (connected)
                {
                    ClientConnected?.Invoke(c.ClientName, c.RemoteIP, c.RemotePort);
                    _logger.Info($"TCP客户端 [{c.ClientName}] 已连接: {c.RemoteIP}:{c.RemotePort}");
                }
                else
                {
                    ClientDisconnected?.Invoke(c.ClientName, c.RemoteIP, c.RemotePort);
                    _logger.Warn($"TCP客户端 [{c.ClientName}] 已断开: {c.RemoteIP}:{c.RemotePort}");
                }
            };

            client.ErrorOccurred += (c, ex) =>
            {
                ClientError?.Invoke(c.ClientName, c.RemoteIP, c.RemotePort, ex.Message);
                _logger.Error($"TCP客户端 [{c.ClientName}] 错误: {ex.Message}");
            };

            client.DataReceived += (c, data) =>
            {
                var message = Encoding.UTF8.GetString(data);
                CameraMessageReceived?.Invoke(c.ClientName, message);
            };
        }

        /// <summary>
        /// 客户端移除事件处理
        /// </summary>
        private void OnClientRemoved(string clientName)
        {
            _logger.Info($"TCP客户端 [{clientName}] 已从管理器移除");
        }

        /// <summary>
        /// 回放当前所有已连接客户端的上线状态到事件订阅者
        /// 解决ViewModel订阅事件前客户端已连接导致的首次上线日志丢失问题
        /// 应在ViewModel SubscribeTcpEvents() 之后调用
        /// </summary>
        public void ReplayConnectedClients()
        {
            foreach (var kvp in _connectedSnapshot)
            {
                var serverName = kvp.Key;
                List<(string ip, int port)> snapshot;
                lock (kvp.Value) { snapshot = kvp.Value.ToList(); }

                foreach (var (ip, port) in snapshot)
                {
                    _logger.Info($"回放连接状态: 服务器[{serverName}] 客户端已连接 ({ip}:{port})");
                    ClientConnected?.Invoke(serverName, ip, port);
                }
            }
        }

        /// <summary>
        /// 触发TCP客户端掉线报警，通过AlarmModule上传到服务器
        /// 使用fire-and-forget模式，不阻塞事件处理链路
        /// </summary>
        private void TriggerDisconnectAlarm(string serverName, string clientIp, int clientPort)
        {
            if (_alarmService == null) return;

            _ = _alarmService.TriggerAlarmAsync(
                alarmCode: "TCP_CLIENT_DISCONNECT",
                level: AlarmLevel.General,
                description: $"TCP服务器[{serverName}]客户端断开连接 ({clientIp}:{clientPort})",
                source: $"TCPIP.{serverName}",
                type: AlarmType.CommunicationError);
        }

        /// <summary>
        /// 触发TCP通讯错误报警，通过AlarmModule上传到服务器
        /// 使用fire-and-forget模式，不阻塞事件处理链路
        /// </summary>
        private void TriggerErrorAlarm(string serverName, string errorMessage)
        {
            if (_alarmService == null) return;

            _ = _alarmService.TriggerAlarmAsync(
                alarmCode: "TCP_COMM_ERROR",
                level: AlarmLevel.Serious,
                description: $"TCP服务器[{serverName}]通讯异常: {errorMessage}",
                source: $"TCPIP.{serverName}",
                type: AlarmType.CommunicationError);
        }
    }
}
