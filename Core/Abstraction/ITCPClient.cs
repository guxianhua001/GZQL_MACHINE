
namespace Core.Abstraction
{
    // Core/Abstractions/ITCPClient.cs
    public interface ITCPClient : IDisposable
    {
        string ClientName { get; }
        bool IsConnected { get; }
        string RemoteIP { get; }
        int RemotePort { get; }

        event Action<ITCPClient, bool> ConnectionStateChanged;
        event Action<ITCPClient, byte[]> DataReceived;
        event Action<ITCPClient, Exception> ErrorOccurred;

        Task ConnectAsync(string ip, int port);
        Task DisconnectAsync();
        Task SendAsync(byte[] data);
        Task<bool> SendAsync(byte[] data, int timeout);
        Task<byte[]> ReceiveAsync(int timeout);
    }

    // Core/Abstractions/ITCPServer.cs
    public interface ITCPServer : IDisposable
    {
        bool IsRunning { get; }
        int ConnectedClientsCount { get; }

        event Action<ITCPClient> ClientConnected;
        event Action<ITCPClient> ClientDisconnected;
        event Action<Exception> ServerError;
        event Action<string, string> DataReceived;

        // 广播方法
        Task<bool> BroadcastAsync(string message);
        Task<bool> SendToClientAsync(string clientIdentifier, string message);

        Task StartAsync();
        Task StopAsync();
        IEnumerable<ITCPClient> GetConnectedClients();
    }
}
