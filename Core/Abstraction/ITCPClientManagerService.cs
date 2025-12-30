using Core.Models;

namespace Core.Abstraction
{
    public interface ITCPClientManagerService
    {
        IReadOnlyDictionary<string, ITCPClient> Clients { get; }
        bool IsInitialized { get; }

        event Action<string, ITCPClient> ClientAdded;
        event Action<string> ClientRemoved;

        Task InitializeAsync(IEnumerable<ClientConfiguration> clientConfigs);
        ITCPClient GetClient(string clientName);
        Task<ITCPClient> GetClientAsync(string clientName);
        Task<bool> AddClientAsync(string clientName, ClientConfiguration config);
        Task<bool> RemoveClientAsync(string clientName);
        Task BroadcastAsync(byte[] data);
    }
}
