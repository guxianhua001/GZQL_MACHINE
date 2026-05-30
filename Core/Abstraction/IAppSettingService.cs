using Core.Configuration;
using Core.Models;
using System.Threading.Tasks;

namespace Core.Abstraction
{
    /// <summary>
    /// 配置服务接口
    /// </summary>
    public interface IAppSettingService
    {
        /// <summary>
        /// 获取应用程序设置
        /// </summary>
        AppSettings Settings { get; }

        /// <summary>
        /// 保存设置
        /// </summary>
        Task SaveAsync();

        /// <summary>
        /// 重新加载设置
        /// </summary>
        Task ReloadAsync();

        /// <summary>
        /// 重置为默认设置
        /// </summary>
        Task ResetToDefaultAsync();

        string RecipeName { get; set; }
        string LastRecipeName { get; set; }
        string LastSelectedRecipePath { get; set; }
        ServerConfiguration ServerConfig { get; }
        IReadOnlyList<ClientConfiguration> Clients { get; }
        void Load();                                 // 同步加载（可选）
        void Save();                                 // 同步保存（可选）
        bool TryUpdateRecipeName(string newName);

        // 客户端管理
        void AddClient(ClientConfiguration clientConfig);
        void RemoveClient(string clientName);
        ClientConfiguration GetClient(string clientName);
        /// <summary>
        /// 从配置中读取指定键的值，若无则返回默认值
        /// </summary>
        string GetValue(string key, string defaultValue = null);
    }

}