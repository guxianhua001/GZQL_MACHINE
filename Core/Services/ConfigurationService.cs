// Core/Services/ConfigurationService.cs
using Core.Abstraction;
using Core.Configuration;
using Core.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Core.Services
{
    public class ConfigurationService : IAppSettingService
    {
        private readonly string _configFilePath;
        private readonly object _lock = new object();
        private AppSettings _settings;

        public string RecipeName
        {
            get => Settings.RecipeName;
            set => Settings.RecipeName = value;
        }
        public string LastRecipeName
        {
            get => Settings.LastRecipeName;
            set => Settings.LastRecipeName = value;
        }
        public string LastSelectedRecipePath
        {
            get => Settings.LastSelectedRecipePath;
            set => Settings.LastSelectedRecipePath = value;
        }
        public ServerConfiguration ServerConfig => _settings.Server;
        public IReadOnlyList<ClientConfiguration> Clients => _settings.Clients.AsReadOnly();

        public AppSettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    LoadSettings();
                }
                return _settings;
            }
            private set => _settings = value;
        }

        public ConfigurationService()
        {
            // 获取启动路径
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var configDirectory = Path.Combine(baseDirectory, "Config");

            // 确保目录存在
            Directory.CreateDirectory(configDirectory);

            // 配置文件路径
            _configFilePath = Path.Combine(configDirectory, "appsettings.JSON");

            // 如果配置文件不存在，创建默认配置
            if (!File.Exists(_configFilePath))
            {
                CreateDefaultSettings();
            }
        }

        // 同步加载
        public void Load()
        {
            LoadSettings();
        }

        // 同步保存
        public void Save()
        {
            SaveSettings();
        }

        // 异步保存
        public async Task SaveAsync()
        {
            await Task.Run(() => SaveSettings());
        }

        // 异步重新加载
        public async Task ReloadAsync()
        {
            await Task.Run(() => LoadSettings());
        }

        // 异步重置
        public async Task ResetToDefaultAsync()
        {
            await Task.Run(() =>
            {
                _settings = new AppSettings();
                SaveSettings();
            });
        }

        // 更新配方名称
        public bool TryUpdateRecipeName(string newName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(newName))
                    return false;

                Settings.LastRecipeName = Settings.RecipeName;
                Settings.RecipeName = newName.Trim();
                //Save();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // 客户端管理
        public void AddClient(ClientConfiguration clientConfig)
        {
            if (clientConfig == null)
                throw new ArgumentNullException(nameof(clientConfig));

            if (Settings.Clients.Any(c => c.ClientName == clientConfig.ClientName))
                throw new ArgumentException($"客户端名称 '{clientConfig.ClientName}' 已存在");

            Settings.Clients.Add(clientConfig);
            Save();
        }

        public void RemoveClient(string clientName)
        {
            var client = Settings.Clients.FirstOrDefault(c => c.ClientName == clientName);
            if (client != null)
            {
                Settings.Clients.Remove(client);
                Save();
            }
        }

        public ClientConfiguration GetClient(string clientName)
        {
            return Settings.Clients.FirstOrDefault(c => c.ClientName == clientName);
        }

        // 私有辅助方法
        private void LoadSettings()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_configFilePath))
                    {
                        var json = File.ReadAllText(_configFilePath);
                        _settings = JsonSerializer.Deserialize<AppSettings>(json);
                    }
                    else
                    {
                        CreateDefaultSettings();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"加载配置失败: {ex.Message}");
                    CreateDefaultSettings();
                }
            }
        }

        private void CreateDefaultSettings()
        {
            _settings = new AppSettings();
            SaveSettings();
        }

        private void SaveSettings()
        {
            lock (_lock)
            {
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNameCaseInsensitive = true, // 解决属性名大小写不敏感问题      
                    };

                    var json = JsonSerializer.Serialize(_settings, options);
                    File.WriteAllText(_configFilePath, json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"保存配置失败: {ex.Message}");
                }
            }
        }
        public string GetValue(string key, string defaultValue = null)
        {
            // 确保设置已加载
            if (_settings == null)
                LoadSettings();

            if (_settings.ExtensionData.TryGetValue(key, out JsonElement element))
            {
                // 尝试获取字符串值，如果是其他类型可能会比较麻烦，这里假设存储的是字符串
                if (element.ValueKind == JsonValueKind.String)
                    return element.GetString();
            }
            return defaultValue;
        }

    }
}