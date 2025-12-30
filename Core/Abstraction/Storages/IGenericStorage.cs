
namespace Core.Abstractions.Storages
{
    public interface IGenericStorage
    {
        Task<T> LoadAsync<T>(string identifier) where T : class, new();
        Task SaveAsync<T>(string identifier, T data) where T : class;
        Task<bool> ExistsAsync<T>(string identifier);
        Task DeleteAsync<T>(string identifier);
    }
}
