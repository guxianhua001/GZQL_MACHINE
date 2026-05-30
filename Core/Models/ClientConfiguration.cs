﻿﻿﻿// Core/Models/ConfigurationModels.cs
namespace Core.Models
{
    public class ServerConfiguration
    {
        public string ServerIP { get; set; } = "0.0.0.0";
        public int Port { get; set; } = 8080;
        public int MaxClients { get; set; } = 100;
        public string EncodingMethod { get; set; } = "UTF-8";
    }

    public class ClientConfiguration
    {
        public string ClientName { get; set; } = string.Empty;
        /// <summary> 连接模式：Client(客户端) 或 Server(服务端) </summary>
        public string Mode { get; set; } = "Client";
        public string IP { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 8080;
        public string Description { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
    }

}