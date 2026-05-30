using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TCPIPModule.Interfaces;

namespace TCPIPModule.Services
{
    /// <summary>
    /// 基于System.Net.Sockets的TCP客户端实现
    /// 支持两种数据模式：
    /// - Raw模式（默认）：直接收发原始字节，兼容标准TCP设备（如NetAssist、视觉系统）
    /// - Frame模式：使用长度前缀帧协议 [4字节长度][消息体]，防止粘包/拆包
    /// 支持异步连接、自动重连、超时收发
    /// </summary>
    public class TcpClientImpl : ITCPClient
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private CancellationTokenSource? _readCts;
        private CancellationTokenSource? _reconnectCts;
        private readonly object _lock = new();

        /// <summary> 客户端名称 </summary>
        public string ClientName { get; }

        /// <summary> 是否已连接 </summary>
        public bool IsConnected { get; private set; }

        /// <summary> 远端IP地址 </summary>
        public string RemoteIP { get; private set; } = "";

        /// <summary> 远端端口号 </summary>
        public int RemotePort { get; private set; }

        /// <summary> 是否启用自动重连 </summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary> 自动重连间隔（毫秒） </summary>
        public int ReconnectInterval { get; set; } = 3000;

        /// <summary>
        /// 数据模式：Raw=直接收发原始字节（默认，兼容标准TCP设备）；
        /// Frame=使用长度前缀帧协议[4字节长度][消息体]
        /// </summary>
        public DataMode DataMode { get; set; } = DataMode.Raw;

        /// <summary> 连接状态变更事件 </summary>
        public event Action<ITCPClient, bool>? ConnectionStateChanged;

        /// <summary> 数据接收事件 </summary>
        public event Action<ITCPClient, byte[]>? DataReceived;

        /// <summary> 错误事件 </summary>
        public event Action<ITCPClient, Exception>? ErrorOccurred;

        /// <summary> 接收队列：存储已解析的消息帧数据 </summary>
        private readonly ConcurrentQueue<byte[]> _receiveQueue = new();

        /// <summary> 接收信号量：通知等待接收的操作有数据可用 </summary>
        private SemaphoreSlim? _receiveSignal;

        /// <summary> 消息帧读取缓冲区（仅Frame模式使用） </summary>
        private readonly MemoryStream _frameBuffer = new();

        public TcpClientImpl(string clientName)
        {
            ClientName = clientName;
            _receiveSignal = new SemaphoreSlim(0);
        }

        /// <summary>
        /// 异步连接到指定IP和端口，启动读取循环
        /// 使用5秒连接超时，避免目标不可达时长时间阻塞
        /// </summary>
        public async Task ConnectAsync(string ip, int port)
        {
            RemoteIP = ip;
            RemotePort = port;

            try
            {
                var client = new TcpClient();
                using var connectCts = new CancellationTokenSource(5000);
                await client.ConnectAsync(ip, port, connectCts.Token);
                InitializeFromAcceptedClient(client);
            }
            catch (Exception ex)
            {
                IsConnected = false;
                ConnectionStateChanged?.Invoke(this, false);
                ErrorOccurred?.Invoke(this, ex);

                if (AutoReconnect)
                    _ = StartReconnectLoopAsync();

                throw;
            }
        }

        /// <summary>
        /// 从已接受的TcpClient初始化（Server模式使用）
        /// 直接使用已连接的socket，启动读取循环
        /// </summary>
        public void InitializeFromAcceptedClient(TcpClient acceptedClient)
        {
            lock (_lock)
            {
                _tcpClient = acceptedClient;
                _stream = acceptedClient.GetStream();
                _readCts = new CancellationTokenSource();
            }

            var remoteEndPoint = acceptedClient.Client.RemoteEndPoint as System.Net.IPEndPoint;
            if (remoteEndPoint != null)
            {
                RemoteIP = remoteEndPoint.Address.ToString();
                RemotePort = remoteEndPoint.Port;
            }

            IsConnected = true;
            ConnectionStateChanged?.Invoke(this, true);

            _frameBuffer.SetLength(0);
            _ = ReadLoopAsync(_readCts!.Token);
        }

        /// <summary>
        /// 断开连接，停止读取循环和自动重连
        /// </summary>
        public async Task DisconnectAsync()
        {
            AutoReconnect = false;
            _reconnectCts?.Cancel();
            _readCts?.Cancel();

            lock (_lock)
            {
                _stream?.Close();
                _stream?.Dispose();
                _tcpClient?.Close();
                _tcpClient?.Dispose();
                _tcpClient = null;
                _stream = null;
            }

            IsConnected = false;
            ConnectionStateChanged?.Invoke(this, false);
            await Task.CompletedTask;
        }

        /// <summary>
        /// 发送原始字节数据（无帧封装）
        /// </summary>
        public async Task SendAsync(byte[] data)
        {
            NetworkStream? stream;
            lock (_lock) { stream = _stream; }

            if (stream == null) throw new InvalidOperationException($"客户端 [{ClientName}] 未连接");
            await stream.WriteAsync(data, 0, data.Length);
        }

        /// <summary>
        /// 发送原始字节数据（带超时）
        /// </summary>
        public async Task<bool> SendAsync(byte[] data, int timeout)
        {
            try
            {
                using var cts = new CancellationTokenSource(timeout);
                NetworkStream? stream;
                lock (_lock) { stream = _stream; }

                if (stream == null) return false;
                await stream.WriteAsync(data, 0, data.Length, cts.Token);
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// 发送字符串消息：根据DataMode选择Raw或Frame方式
        /// </summary>
        public async Task SendFrameAsync(string message)
        {
            if (DataMode == DataMode.Frame)
            {
                var frame = BuildFrame(message);
                await SendAsync(frame);
            }
            else
            {
                var data = Encoding.UTF8.GetBytes(message);
                await SendAsync(data);
            }
        }

        /// <summary>
        /// 发送字符串消息（带超时）：根据DataMode选择Raw或Frame方式
        /// </summary>
        public async Task<bool> SendFrameAsync(string message, int timeout)
        {
            if (DataMode == DataMode.Frame)
            {
                var frame = BuildFrame(message);
                return await SendAsync(frame, timeout);
            }
            else
            {
                var data = Encoding.UTF8.GetBytes(message);
                return await SendAsync(data, timeout);
            }
        }

        /// <summary>
        /// 从接收队列中等待并获取一条完整消息（带超时）
        /// </summary>
        public async Task<byte[]> ReceiveAsync(int timeout)
        {
            if (_receiveSignal == null) throw new InvalidOperationException("客户端未初始化");

            var got = await _receiveSignal.WaitAsync(timeout);
            if (!got) throw new TimeoutException($"客户端 [{ClientName}] 接收超时（{timeout}ms）");

            _receiveQueue.TryDequeue(out var data);
            return data ?? Array.Empty<byte>();
        }

        /// <summary>
        /// 发送字符串消息并等待字符串响应
        /// </summary>
        public async Task<string> SendAndReceiveAsync(string message, int timeout = 5000)
        {
            await SendFrameAsync(message);
            var response = await ReceiveAsync(timeout);
            return Encoding.UTF8.GetString(response);
        }

        /// <summary>
        /// 读取循环：持续从网络流读取数据
        /// Raw模式：直接将收到的数据放入接收队列并触发事件
        /// Frame模式：解析长度前缀帧后放入接收队列
        /// </summary>
        private async Task ReadLoopAsync(CancellationToken token)
        {
            var buffer = new byte[8192];
            try
            {
                while (!token.IsCancellationRequested)
                {
                    NetworkStream? stream;
                    lock (_lock) { stream = _stream; }

                    if (stream == null || !stream.CanRead) break;

                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (bytesRead == 0) break;

                    var receivedData = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, receivedData, 0, bytesRead);

                    if (DataMode == DataMode.Raw)
                    {
                        _receiveQueue.Enqueue(receivedData);
                        _receiveSignal?.Release();
                        DataReceived?.Invoke(this, receivedData);
                    }
                    else
                    {
                        _frameBuffer.Write(receivedData, 0, bytesRead);
                        ProcessFrameBuffer();
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
            finally
            {
                bool wasConnected = IsConnected;
                IsConnected = false;

                if (wasConnected)
                    ConnectionStateChanged?.Invoke(this, false);

                if (AutoReconnect && !token.IsCancellationRequested)
                    _ = StartReconnectLoopAsync();
            }
        }

        /// <summary>
        /// 处理帧缓冲区：解析长度前缀帧，提取完整消息（仅Frame模式使用）
        /// 帧格式：[4字节长度（小端序）][消息体]
        /// </summary>
        private void ProcessFrameBuffer()
        {
            while (_frameBuffer.Length >= 4)
            {
                _frameBuffer.Position = 0;
                var lengthBytes = new byte[4];
                _frameBuffer.Read(lengthBytes, 0, 4);
                int bodyLength = BitConverter.ToInt32(lengthBytes, 0);

                if (bodyLength <= 0 || bodyLength > 10 * 1024 * 1024)
                {
                    _frameBuffer.SetLength(0);
                    ErrorOccurred?.Invoke(this, new InvalidDataException($"无效的消息帧长度: {bodyLength}"));
                    return;
                }

                if (_frameBuffer.Length - 4 < bodyLength)
                    break;

                var body = new byte[bodyLength];
                _frameBuffer.Read(body, 0, bodyLength);

                var remaining = (int)(_frameBuffer.Length - _frameBuffer.Position);
                if (remaining > 0)
                {
                    var leftover = new byte[remaining];
                    _frameBuffer.Read(leftover, 0, remaining);
                    _frameBuffer.SetLength(0);
                    _frameBuffer.Write(leftover, 0, leftover.Length);
                }
                else
                {
                    _frameBuffer.SetLength(0);
                }

                _receiveQueue.Enqueue(body);
                _receiveSignal?.Release();
                DataReceived?.Invoke(this, body);
            }
        }

        /// <summary>
        /// 构建长度前缀帧：[4字节长度][消息体]
        /// </summary>
        private static byte[] BuildFrame(string message)
        {
            var body = Encoding.UTF8.GetBytes(message);
            var lengthPrefix = BitConverter.GetBytes(body.Length);
            var frame = new byte[4 + body.Length];
            Buffer.BlockCopy(lengthPrefix, 0, frame, 0, 4);
            Buffer.BlockCopy(body, 0, frame, 4, body.Length);
            return frame;
        }

        /// <summary>
        /// 自动重连循环：在连接断开后按间隔尝试重新连接
        /// 使用5秒连接超时，避免目标不可达时长时间阻塞
        /// </summary>
        private async Task StartReconnectLoopAsync()
        {
            _reconnectCts = new CancellationTokenSource();
            var token = _reconnectCts.Token;

            while (AutoReconnect && !token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(ReconnectInterval, token);

                    var client = new TcpClient();
                    using var connectCts = new CancellationTokenSource(5000);
                    await client.ConnectAsync(RemoteIP, RemotePort, connectCts.Token);

                    lock (_lock)
                    {
                        _tcpClient = client;
                        _stream = client.GetStream();
                        _readCts = new CancellationTokenSource();
                    }

                    IsConnected = true;
                    ConnectionStateChanged?.Invoke(this, true);
                    _frameBuffer.SetLength(0);
                    _ = ReadLoopAsync(_readCts.Token);

                    break;
                }
                catch (OperationCanceledException) { break; }
                catch
                {
                }
            }
        }

        public void Dispose()
        {
            AutoReconnect = false;
            _reconnectCts?.Cancel();
            _reconnectCts?.Dispose();
            _readCts?.Cancel();
            _readCts?.Dispose();

            lock (_lock)
            {
                _stream?.Close();
                _stream?.Dispose();
                _tcpClient?.Close();
                _tcpClient?.Dispose();
            }

            _receiveSignal?.Dispose();
            _frameBuffer?.Dispose();
        }
    }

    /// <summary>
    /// 数据收发模式
    /// Raw=直接收发原始字节（默认，兼容标准TCP设备如NetAssist、视觉系统）
    /// Frame=使用长度前缀帧协议[4字节长度][消息体]（防粘包/拆包）
    /// </summary>
    public enum DataMode
    {
        Raw = 0,
        Frame = 1
    }
}
