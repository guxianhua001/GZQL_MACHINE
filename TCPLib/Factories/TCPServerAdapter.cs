using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TCPLib.Adapters;
using TCPLib.TCPHelper;

namespace TCPLib.Factories
{
    public class TCPServerAdapter : ITCPServer
    {
        private readonly TCPServerHelper _wrappedServer;
        private readonly ServerConfiguration _config;
        private readonly ILoggerService _logger;
        private readonly Dictionary<string, ITCPClient> _connectedClients;

        public bool IsRunning => _wrappedServer?.IsRun ?? false;
        public int ConnectedClientsCount => _connectedClients.Count;

        public event Action<ITCPClient> ClientConnected;
        public event Action<ITCPClient> ClientDisconnected;
        public event Action<Exception> ServerError;
        public event Action<string, string> DataReceived;

        public TCPServerAdapter(ServerConfiguration config, ILoggerService logger)
        {
            _config = config;
            _logger = logger;
            _connectedClients = new Dictionary<string, ITCPClient>();

            // 参数类型转换
            var ipAddress = System.Net.IPAddress.Parse(config.ServerIP);
            var encodingMethod = ParseEncodingMethod(config.EncodingMethod);
            var port = (ushort)config.Port;
            var maxClients = (ushort)config.MaxClients;

            var coder = new Coder(encodingMethod);
            _wrappedServer = new TCPServerHelper(ipAddress, port, maxClients, coder);

            SubscribeToWrappedServerEvents();
            _logger.Info($"TCP服务器适配器创建完成: {config.ServerIP}:{config.Port}");
        }

        private Coder.EncodingMothord ParseEncodingMethod(string encodingMethod)
        {
            if (string.IsNullOrEmpty(encodingMethod))
                return Coder.EncodingMothord.UTF8;

            return encodingMethod.ToUpper() switch
            {
                "UTF8" or "UTF-8" => Coder.EncodingMothord.UTF8,
                "ASCII" => Coder.EncodingMothord.ASCII,
                "UNICODE" => Coder.EncodingMothord.Unicode,
                "Default" => Coder.EncodingMothord.Default,
                _ => Coder.EncodingMothord.UTF8
            };
        }

        private void SubscribeToWrappedServerEvents()
        {
            if (_wrappedServer == null) return;

            _wrappedServer.ClientConn += OnWrappedClientConnected;
            _wrappedServer.ClientClose += OnWrappedClientDisconnected;
            _wrappedServer.RecvData += OnServerHelperDataReceived;
            _wrappedServer.ServerFull += OnWrappedServerFull;
        }

        private void UnsubscribeFromWrappedServerEvents()
        {
            if (_wrappedServer == null) return;

            _wrappedServer.ClientConn -= OnWrappedClientConnected;
            _wrappedServer.ClientClose -= OnWrappedClientDisconnected;
            _wrappedServer.RecvData -= OnServerHelperDataReceived;
            _wrappedServer.ServerFull -= OnWrappedServerFull;
        }

        public async Task StartAsync()
        {
            try
            {
                _wrappedServer.Start();
                _logger.Info($"TCP服务器已启动: {_config.ServerIP}:{_config.Port}");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Error($"启动TCP服务器失败: {ex.Message}");
                ServerError?.Invoke(ex);
                throw;
            }
        }

        public async Task StopAsync()
        {
            try
            {
                _wrappedServer.Stop();

                // 断开所有客户端
                foreach (var client in _connectedClients.Values)
                {
                    await client.DisconnectAsync();
                }
                _connectedClients.Clear();

                _logger.Info("TCP服务器已停止");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Error($"停止TCP服务器失败: {ex.Message}");
                throw;
            }
        }

        public IEnumerable<ITCPClient> GetConnectedClients()
        {
            return _connectedClients.Values.ToList();
        }

        private void OnWrappedClientConnected(object sender, NetEventArgs e)
        {
            try
            {
                var session = e.Client;
                var clientKey = $"{session.RemoteIP}:{session.RemotePort}";

                // 创建客户端适配器
                var clientConfig = new ClientConfiguration
                {
                    ClientName = clientKey,
                    IP = session.RemoteIP,
                    Port = session.RemotePort
                };

                var clientAdapter = new TCPClientAdapter(clientKey, clientConfig);
                _connectedClients[clientKey] = clientAdapter;

                _logger.Info($"客户端连接: {clientKey}");
                ClientConnected?.Invoke(clientAdapter);
            }
            catch (Exception ex)
            {
                _logger.Error($"处理客户端连接事件时出错: {ex.Message}");
            }
        }

        private void OnWrappedClientDisconnected(object sender, NetEventArgs e)
        {
            try
            {
                var session = e.Client;
                var clientKey = $"{session.RemoteIP}:{session.RemotePort}";

                if (_connectedClients.TryGetValue(clientKey, out var client))
                {
                    _connectedClients.Remove(clientKey);
                    client.Dispose();

                    _logger.Info($"客户端断开: {clientKey}");
                    ClientDisconnected?.Invoke(client);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"处理客户端断开事件时出错: {ex.Message}");
            }
        }

        private void OnServerHelperDataReceived(object sender, NetEventArgs e)
        {
            try
            {
                var session = e.Client;
                string clientName = $"Client_{session.RemoteIP.Replace('.', '_')}";
                string message = Encoding.UTF8.GetString(session.Datagram);

                // 转发到ITCPServer格式的事件
                DataReceived?.Invoke(clientName, message);
            }
            catch (Exception ex)
            {
                _logger.Error($"处理数据接收事件时出错: {ex.Message}");
            }
        }

        private void OnWrappedServerFull(object sender, NetEventArgs e)
        {
            var exception = new Exception("TCP服务器已达到最大客户端连接数");
            _logger.Warn("TCP服务器已满，无法接受新连接");
            ServerError?.Invoke(exception);
        }

        /// <summary>
        /// 向所有连接的客户端广播消息
        /// </summary>
        public async Task<bool> BroadcastAsync(string message)
        {
            try
            {
                if (_wrappedServer != null && _wrappedServer.IsRun)
                {
                    await Task.Run(() => _wrappedServer.Broadcast(message));
                    _logger.Info($"广播消息成功: {message}");
                    return true;
                }
                else
                {
                    _logger.Warn("服务器未运行，无法广播消息");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"广播消息失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 向特定客户端发送消息
        /// </summary>
        public async Task<bool> SendToClientAsync(string clientIdentifier, string message)
        {
            try
            {
                if (_wrappedServer != null && _wrappedServer.IsRun)
                {
                    var session = FindClientSession(clientIdentifier);
                    if (session != null)
                    {
                        await Task.Run(() => _wrappedServer.SendText(session, message));
                        _logger.Info($"向客户端 {clientIdentifier} 发送消息成功: {message}");
                        return true;
                    }
                    else
                    {
                        _logger.Warn($"未找到客户端: {clientIdentifier}");
                        return false;
                    }
                }
                else
                {
                    _logger.Warn("服务器未运行，无法发送消息");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"向客户端 {clientIdentifier} 发送消息失败: {ex.Message}");
                return false;
            }
        }

        private Session FindClientSession(string clientIdentifier)
        {
            try
            {
                // 根据客户端标识符查找会话
                foreach (DictionaryEntry entry in _wrappedServer.SessionTable)
                {
                    var session = entry.Value as Session;
                    if (session != null)
                    {
                        return session;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error($"查找客户端会话失败: {ex.Message}");
                return null;
            }
        }

        private string GetClientNameFromSession(Session session)
        {
            return $"Client_{session.RemoteIP.Replace('.', '_')}";
        }

        public void Dispose()
        {
            try
            {
                UnsubscribeFromWrappedServerEvents();
                _wrappedServer?.Stop();

                foreach (var client in _connectedClients.Values)
                {
                    client.Dispose();
                }
                _connectedClients.Clear();

                _logger.Info("TCP服务器适配器已释放");
            }
            catch (Exception ex)
            {
                _logger.Warn($"释放TCP服务器适配器时出错: {ex.Message}");
            }
        }
    }
}