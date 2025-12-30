using Core.Models;

namespace Core.Abstraction
{
    public interface ITCPEventService
    {
        event Action<string, string, int> ClientConnected;
        event Action<string, string, int> ClientDisconnected;
        event Action<string, string, int, string> ClientError;
        event Action<string, int> ServerClientConnected;
        event Action<string, int> ServerClientDisconnected;
        event Action<string, string> CameraMessageReceived; // 相机消息接收事件
        event Action<string, bool> CameraCommandCompleted;  // 相机命令完成事件
        bool IsInitialized { get; }

        void Initialize();
        void StartServer(ServerConfiguration serverConfig);
        void StopServer();
        void AddClient(string clientName, ClientConfiguration config);
        void RemoveClient(string clientName);
        Task<bool> BroadcastCommandAsync(string command, int timeout = 5000);
        Task<bool> SendCommandAsync(string cameraName, string command, int timeout = 5000);
        Task<string> SendCommandWithResponseAsync(string cameraName, string command, int timeout = 5000);
        void RegisterClient(string cameraName, string ip, int port);
        void UnregisterClient(string cameraName);
    }
}
