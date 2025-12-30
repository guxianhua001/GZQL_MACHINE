using Core.Abstraction;
using Core.Models;
using System.Net.Sockets;
using TCPLib.TCPHelper;

namespace TCPLib.Adapters
{
    public class TCPClientAdapter : ITCPClient
    {
        private readonly TCPClientHelper _wrappedClient;
        private readonly string _clientName;
        private readonly Queue<byte[]> _receivedDataQueue = new Queue<byte[]>();
        private readonly object _queueLock = new object();
        private bool _isReceiving = false;

        public string ClientName => _clientName;
        public bool IsConnected => _wrappedClient.IsConnected;
        public string RemoteIP => _wrappedClient.RemoteIP;
        public int RemotePort => _wrappedClient.RemotePort;

        public event Action<ITCPClient, bool> ConnectionStateChanged;
        public event Action<ITCPClient, byte[]> DataReceived;
        public event Action<ITCPClient, Exception> ErrorOccurred;

        public TCPClientAdapter(string clientName, ClientConfiguration config)
        {
            _clientName = clientName;
            _wrappedClient = new TCPClientHelper();

            if (clientName.StartsWith("127.0.0.1"))
            {
                var ipPortPart = clientName.Split(':')[0..2];          // 取前 2 段
                var ip = ipPortPart[0];                                // "127.0.0.1"
                var port = int.Parse(ipPortPart[1]);                   // 55792
                _wrappedClient.RemoteIP = ip;
                _wrappedClient.RemotePort = port;
            }

            // 包装事件 - 根据实际的 TCPClientHelper 事件进行调整
            _wrappedClient.ConnectedServer += OnWrappedConnected;
            _wrappedClient.ErrorEvent += OnWrappedError;

            // 尝试订阅数据接收事件，如果存在的话
            SubscribeToDataReceivedEvent();
        }

        private void SubscribeToDataReceivedEvent()
        {
            // 尝试通过反射查找数据接收事件，或者使用其他方法
            // 这里假设可能有名为 "DataReceived", "MessageReceived", "ReceiveData" 等事件
            var eventInfo = _wrappedClient.GetType().GetEvent("DataReceived")
                         ?? _wrappedClient.GetType().GetEvent("MessageReceived")
                         ?? _wrappedClient.GetType().GetEvent("ReceiveData");

            if (eventInfo != null)
            {
                // 动态订阅事件
                // 注意：这需要根据实际的事件委托类型进行调整
            }
        }

        public async Task ConnectAsync(string ip, int port)
        {
            try
            {
                // 根据实际的 TCPClientHelper API 进行调整
                // 假设有 Connect 方法
                _wrappedClient.Connect(ip, port);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            _wrappedClient.Close();
            await Task.CompletedTask;
        }

        public async Task SendAsync(byte[] data)
        {
            try
            {
                // 根据实际的 TCPClientHelper API 进行调整
                _wrappedClient.SendBytes(data);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                throw;
            }
        }

        public async Task<bool> SendAsync(byte[] data, int timeout)
        {
            try
            {
                using var cts = new CancellationTokenSource(timeout);

                await Task.Run(() =>
                {
                    cts.Token.ThrowIfCancellationRequested();
                    _wrappedClient.SendBytes(data);
                }, cts.Token);

                return true;
            }
            catch (OperationCanceledException)
            {
                var timeoutEx = new TimeoutException($"发送数据到 {_clientName} 超时 ({timeout}ms)");
                ErrorOccurred?.Invoke(this, timeoutEx);
                return false;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                return false;
            }
        }

        public async Task<byte[]> ReceiveAsync(int timeout)
        {
            try
            {
                var tcs = new TaskCompletionSource<byte[]>();
                using var cts = new CancellationTokenSource(timeout);

                // 设置超时
                cts.Token.Register(() =>
                    tcs.TrySetException(new TimeoutException($"从 {_clientName} 接收数据超时 ({timeout}ms)")));

                // 检查队列中是否有已接收的数据
                lock (_queueLock)
                {
                    if (_receivedDataQueue.Count > 0)
                    {
                        var data = _receivedDataQueue.Dequeue();
                        return data;
                    }
                }

                // 如果没有数据，开始接收模式
                _isReceiving = true;

                // 临时事件处理程序
                void DataReceivedHandler(ITCPClient client, byte[] receivedData)
                {
                    if (_isReceiving)
                    {
                        tcs.TrySetResult(receivedData);
                        _isReceiving = false;
                    }
                    else
                    {
                        // 存储到队列供后续使用
                        lock (_queueLock)
                        {
                            _receivedDataQueue.Enqueue(receivedData);
                        }
                    }
                }

                void ErrorHandler(ITCPClient client, Exception ex)
                {
                    if (_isReceiving)
                    {
                        tcs.TrySetException(ex);
                        _isReceiving = false;
                    }
                }

                DataReceived += DataReceivedHandler;
                ErrorOccurred += ErrorHandler;

                try
                {
                    var result = await tcs.Task;
                    return result;
                }
                finally
                {
                    DataReceived -= DataReceivedHandler;
                    ErrorOccurred -= ErrorHandler;
                }
            }
            catch (Exception ex)
            {
                _isReceiving = false;
                throw;
            }
        }

        private void OnWrappedConnected(object sender, NetEventArgs e)
        {
            ConnectionStateChanged?.Invoke(this, true);
        }

        private void OnWrappedError(object sender, NetEventArgs e, SocketError errorCode)
        {
            var exception = new SocketException((int)errorCode);
            ErrorOccurred?.Invoke(this, exception);
        }

        // 手动触发数据接收的方法（如果 TCPClientHelper 没有数据接收事件）
        public void OnDataReceived(byte[] data)
        {
            DataReceived?.Invoke(this, data);
        }

        public void Dispose()
        {
            // 清理事件订阅
            if (_wrappedClient != null)
            {
                _wrappedClient.ConnectedServer -= OnWrappedConnected;
                _wrappedClient.ErrorEvent -= OnWrappedError;
                _wrappedClient.Close();
            }
        }
    }
}