// Core/Abstractions/IConfiguration/IAppConfig.cs
using Core.Models;

namespace Core.Abstractions.IConfiguration
{
    public interface IAppConfig
    {
        string Name { get; set; }
        string RecipeName { get; set; }
        string LastRecipeName { get; set; }
        string LastSelectedRecipePath { get; set; }

        ServerConfiguration ServerConfig { get; }
        IReadOnlyList<ClientConfiguration> Clients { get; }

        void Load();
        void Save();
        bool TryUpdateRecipeName(string newName);
        //void SyncWithRecipePool(Recipe pool);

        // 动态客户端管理
        void AddClient(ClientConfiguration clientConfig);
        void RemoveClient(string clientName);
        ClientConfiguration GetClient(string clientName);
    }
}

// Core/Abstractions/IConfiguration/IConfigurationProvider.cs
public interface IConfigurationProvider
{
    T GetConfiguration<T>() where T : class, new();
    void SaveConfiguration<T>(T config) where T : class;
    bool ConfigurationExists { get; }
    void CreateDefaultConfiguration();
}
