using Core.Abstractions.Plugins;
using System;

using System.IO;
using System.Text.Json;

namespace Core.Services
{
    public class JsonPluginConfiguration : IPluginConfiguration
    {
        private readonly string _pluginConfigPath;
        private Dictionary<string, object> _settings;
        private readonly object _lockObject = new object();

        public string PluginName { get; }

        public JsonPluginConfiguration(string pluginName)
        {
            PluginName = pluginName ?? throw new ArgumentNullException(nameof(pluginName));

            // 配置文件路径：Plugins/{PluginName}/config.json
            _pluginConfigPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Plugins",
                pluginName,
                "config.json");

            LoadSettings();
        }

        private void LoadSettings()
        {
            lock (_lockObject)
            {
                try
                {
                    if (File.Exists(_pluginConfigPath))
                    {
                        var json = File.ReadAllText(_pluginConfigPath);
                        _settings = JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                                    ?? new Dictionary<string, object>();
                    }
                    else
                    {
                        _settings = new Dictionary<string, object>();
                        // 创建配置目录
                        Directory.CreateDirectory(Path.GetDirectoryName(_pluginConfigPath));
                        // 创建默认配置文件
                        SaveSettings();
                    }
                }
                catch (Exception ex)
                {
                    _settings = new Dictionary<string, object>();
                    Console.WriteLine($"加载插件 '{PluginName}' 配置失败: {ex.Message}");
                }
            }
        }

        private void SaveSettings()
        {
            lock (_lockObject)
            {
                try
                {
                    var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    File.WriteAllText(_pluginConfigPath, json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"保存插件 '{PluginName}' 配置失败: {ex.Message}");
                }
            }
        }

        public async Task<T> GetSettingAsync<T>(string key)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_settings.TryGetValue(key, out var value))
                    {
                        try
                        {
                            if (value is JsonElement jsonElement)
                            {
                                return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
                            }
                            return (T)Convert.ChangeType(value, typeof(T));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"获取配置 '{key}' 失败: {ex.Message}");
                            return default;
                        }
                    }
                    return default;
                }
            });
        }

        public async Task SaveSettingAsync<T>(string key, T value)
        {
            await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    _settings[key] = value;
                    SaveSettings();
                }
            });
        }

        public async Task<Dictionary<string, object>> GetAllSettingsAsync()
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    return new Dictionary<string, object>(_settings);
                }
            });
        }

        public bool HasSetting(string key)
        {
            lock (_lockObject)
            {
                return _settings.ContainsKey(key);
            }
        }

        public async Task<bool> RemoveSettingAsync(string key)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    bool removed = _settings.Remove(key);
                    if (removed)
                    {
                        SaveSettings();
                    }
                    return removed;
                }
            });
        }
    }
}
