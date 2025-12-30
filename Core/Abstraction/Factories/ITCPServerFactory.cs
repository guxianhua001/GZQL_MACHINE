

using Core.Models;

namespace Core.Abstraction.Factories
{
    public interface ITCPServerFactory
    {
        ITCPServer CreateServer(ServerConfiguration config);
        ITCPServer CreateServer(string ip, int port, int maxClients, string encodingMethod);
    }
}
