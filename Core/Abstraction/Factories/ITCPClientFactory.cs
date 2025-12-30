

using Core.Models;

namespace Core.Abstraction.Factories
{
    public interface ITCPClientFactory
    {
        ITCPClient CreateClient(string clientName, ClientConfiguration config);
        ITCPClient CreateClient(string clientName, string ip, int port);
    }
}
