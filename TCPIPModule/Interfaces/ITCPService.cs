using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Models;

namespace TCPIPModule.Interfaces
{
    /// <summary>
    /// TCP客户端接口：封装异步连接、发送、接收操作
    /// 支持原始字节收发和长度前缀帧协议
    /// </summary>
    public interface ITCPClient : IDisposable
    {
        /// <summary> 客户端名称 </summary>
        string ClientName { get; }

        /// <summary> 是否已连接 </summary>
        bool IsConnected { get; }

        /// <summary> 远端IP地址 </summary>
        string RemoteIP { get; }

        /// <summary> 远端端口号 </summary>
        int RemotePort { get; }

        /// <summary> 连接状态变更事件 </summary>
        event Action<ITCPClient, bool> ConnectionStateChanged;

        /// <summary> 数据接收事件 </summary>
        event Action<ITCPClient, byte[]> DataReceived;

        /// <summary> 错误事件 </summary>
        event Action<ITCPClient, Exception> ErrorOccurred;

        /// <summary>
        /// 异步连接到指定IP和端口
        /// </summary>
        Task ConnectAsync(string ip, int port);

        /// <summary>
        /// 异步断开连接
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// 发送原始字节数据（无帧封装）
        /// </summary>
        Task SendAsync(byte[] data);

        /// <summary>
        /// 发送原始字节数据（带超时）
        /// </summary>
        Task<bool> SendAsync(byte[] data, int timeout);

        /// <summary>
        /// 发送长度前缀帧格式的消息
        /// 帧格式：[4字节长度][消息体]
        /// </summary>
        Task SendFrameAsync(string message);

        /// <summary>
        /// 发送长度前缀帧格式的消息（带超时）
        /// </summary>
        Task<bool> SendFrameAsync(string message, int timeout);

        /// <summary>
        /// 从接收队列中等待并获取一条完整消息（带超时）
        /// </summary>
        Task<byte[]> ReceiveAsync(int timeout);

        /// <summary>
        /// 发送字符串消息并等待字符串响应（使用帧协议）
        /// </summary>
        Task<string> SendAndReceiveAsync(string message, int timeout = 5000);
    }

    /// <summary>
    /// TCP服务器接口：管理客户端连接、广播和定向发送
    /// </summary>
    public interface ITCPServer : IDisposable
    {
        /// <summary> 服务器是否正在运行 </summary>
        bool IsRunning { get; }

        /// <summary> 当前已连接的客户端数量 </summary>
        int ConnectedClientsCount { get; }

        /// <summary> 客户端连接事件 </summary>
        event Action<ITCPClient> ClientConnected;

        /// <summary> 客户端断开事件 </summary>
        event Action<ITCPClient> ClientDisconnected;

        /// <summary> 服务器错误事件 </summary>
        event Action<Exception> ServerError;

        /// <summary> 收到数据事件：(clientIdentifier, message) </summary>
        event Action<string, string> DataReceived;

        /// <summary>
        /// 向所有已连接客户端广播消息
        /// </summary>
        Task<bool> BroadcastAsync(string message);

        /// <summary>
        /// 向指定客户端发送消息
        /// </summary>
        Task<bool> SendToClientAsync(string clientIdentifier, string message);

        /// <summary>
        /// 启动TCP服务器
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// 停止TCP服务器
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// 获取所有已连接客户端
        /// </summary>
        IEnumerable<ITCPClient> GetConnectedClients();
    }

    /// <summary>
    /// TCP事件服务接口：高层命令发送/接收协调器
    /// 管理TCP服务器和客户端的生命周期，提供命令发送和响应等待功能
    /// </summary>
    public interface ITCPEventService
    {
        /// <summary> 客户端连接事件：(clientName, ip, port) </summary>
        event Action<string, string, int> ClientConnected;

        /// <summary> 客户端断开事件：(clientName, ip, port) </summary>
        event Action<string, string, int> ClientDisconnected;

        /// <summary> 客户端错误事件：(clientName, ip, port, errorMessage) </summary>
        event Action<string, string, int, string> ClientError;

        /// <summary> 服务端客户端连接事件：(clientId, port) </summary>
        event Action<string, int> ServerClientConnected;

        /// <summary> 服务端客户端断开事件：(clientId, port) </summary>
        event Action<string, int> ServerClientDisconnected;

        /// <summary> 相机消息接收事件：(cameraName, message) </summary>
        event Action<string, string> CameraMessageReceived;

        /// <summary> 相机命令完成事件：(cameraName, success) </summary>
        event Action<string, bool> CameraCommandCompleted;

        /// <summary> 是否已初始化 </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 获取所有运行中的TCP服务器名称列表（Server模式）
        /// 用于UI下拉框填充，与Client模式的客户端名称合并后形成完整的TCP连接选项
        /// </summary>
        IEnumerable<string> GetServerNames();

        /// <summary>
        /// 初始化事件服务
        /// </summary>
        void Initialize();

        /// <summary>
        /// 回放当前所有已连接客户端的上线状态到事件订阅者
        /// 解决ViewModel订阅事件前客户端已连接导致的首次上线日志丢失问题
        /// 应在SubscribeTcpEvents()之后调用
        /// </summary>
        void ReplayConnectedClients();

        /// <summary>
        /// 启动TCP服务器
        /// serverName: 服务器配置名称，用于CameraMessageReceived事件的cameraName参数
        /// </summary>
        void StartServer(ServerConfiguration serverConfig, string serverName = "");

        /// <summary>
        /// 停止TCP服务器
        /// serverName: 服务器配置名称，为空时停止所有服务器
        /// </summary>
        void StopServer(string serverName = "");

        /// <summary>
        /// 添加TCP客户端
        /// </summary>
        void AddClient(string clientName, ClientConfiguration config);

        /// <summary>
        /// 异步添加TCP客户端（推荐，避免同步阻塞）
        /// </summary>
        Task AddClientAsync(string clientName, ClientConfiguration config);

        /// <summary>
        /// 移除TCP客户端
        /// </summary>
        void RemoveClient(string clientName);

        /// <summary>
        /// 向所有已连接客户端广播命令
        /// </summary>
        Task<bool> BroadcastCommandAsync(string command, int timeout = 5000);

        /// <summary>
        /// 向指定客户端发送命令
        /// </summary>
        Task<bool> SendCommandAsync(string cameraName, string command, int timeout = 5000);

        /// <summary>
        /// 向指定客户端发送命令并等待响应
        /// </summary>
        Task<string> SendCommandWithResponseAsync(string cameraName, string command, int timeout = 5000);

        /// <summary>
        /// 注册客户端（快捷方式）
        /// </summary>
        void RegisterClient(string cameraName, string ip, int port);

        /// <summary>
        /// 注销客户端（快捷方式）
        /// </summary>
        void UnregisterClient(string cameraName);
    }

    /// <summary>
    /// TCP客户端管理服务接口：管理多个命名的TCP客户端
    /// </summary>
    public interface ITCPClientManagerService
    {
        /// <summary> 已注册的客户端字典 </summary>
        IReadOnlyDictionary<string, ITCPClient> Clients { get; }

        /// <summary> 是否已初始化 </summary>
        bool IsInitialized { get; }

        /// <summary> 客户端添加事件 </summary>
        event Action<string, ITCPClient> ClientAdded;

        /// <summary> 客户端移除事件 </summary>
        event Action<string> ClientRemoved;

        /// <summary>
        /// 从配置列表批量初始化客户端
        /// </summary>
        Task InitializeAsync(IEnumerable<ClientConfiguration> clientConfigs);

        /// <summary>
        /// 获取指定名称的客户端
        /// </summary>
        ITCPClient GetClient(string clientName);

        /// <summary>
        /// 异步获取指定名称的客户端
        /// </summary>
        Task<ITCPClient> GetClientAsync(string clientName);

        /// <summary>
        /// 添加新客户端
        /// </summary>
        Task<bool> AddClientAsync(string clientName, ClientConfiguration config);

        /// <summary>
        /// 移除客户端
        /// </summary>
        Task<bool> RemoveClientAsync(string clientName);

        /// <summary>
        /// 向所有已连接客户端广播数据
        /// </summary>
        Task BroadcastAsync(byte[] data);
    }
}
