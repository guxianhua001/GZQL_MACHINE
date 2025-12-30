// Core.Abstractions/Plugins/IPluginConfiguration.cs
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Abstractions.Plugins
{
    public interface IPluginConfiguration
    {
        string PluginName { get; }
        Task<T> GetSettingAsync<T>(string key);
        Task SaveSettingAsync<T>(string key, T value);
        Task<Dictionary<string, object>> GetAllSettingsAsync();
    }
}