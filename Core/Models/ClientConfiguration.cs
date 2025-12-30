// Core/Models/ConfigurationModels.cs
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
        public string IP { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 8080;
        public string Description { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
    }

    // 配置文件根对象
    public class AppConfiguration
    {
        public string Name { get; set; } = "Default";
        public string RecipeName { get; set; } = "Default";
        public string LastRecipeName { get; set; } = "Default";
        public string LastSelectedRecipePath { get; set; } = string.Empty;

        public ServerConfiguration Server { get; set; } = new ServerConfiguration();
        public List<ClientConfiguration> Clients { get; set; } = new List<ClientConfiguration>();
    }
}