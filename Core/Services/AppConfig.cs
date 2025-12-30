// Infrastructure/Configuration/AppConfig.cs
using System.Collections.Generic;
using System.Linq;
using Core.Abstractions.IConfiguration;
using Core.Models;

namespace Core.Services
{
    public class AppConfig : IAppConfig
    {
        private readonly IConfigurationProvider _configProvider;
        private AppConfiguration _configuration;

        public string Name
        {
            get => _configuration.Name;
            set => _configuration.Name = value;
        }

        public string RecipeName
        {
            get => _configuration.RecipeName;
            set => _configuration.RecipeName = value;
        }

        public string LastRecipeName
        {
            get => _configuration.LastRecipeName;
            set => _configuration.LastRecipeName = value;
        }

        public string LastSelectedRecipePath
        {
            get => _configuration.LastSelectedRecipePath;
            set => _configuration.LastSelectedRecipePath = value;
        }

        public ServerConfiguration ServerConfig => _configuration.Server;
        public IReadOnlyList<ClientConfiguration> Clients => _configuration.Clients.AsReadOnly();

        public AppConfig(IConfigurationProvider configProvider)
        {
            _configProvider = configProvider;
            _configuration = new AppConfiguration();
        }

        public void Load()
        {
            if (!_configProvider.ConfigurationExists)
            {
                _configProvider.CreateDefaultConfiguration();
            }

            _configuration = _configProvider.GetConfiguration<AppConfiguration>();
        }

        public void Save()
        {
            _configProvider.SaveConfiguration(_configuration);
        }

        public bool TryUpdateRecipeName(string newName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(newName))
                    return false;

                LastRecipeName = RecipeName;
                RecipeName = newName.Trim();
                Save();
                return true;
            }
            catch
            {
                return false;
            }
        }

        //public void SyncWithRecipePool(Recipe pool)
        //{
        //    LastSelectedRecipePath = pool.FilePath;
        //    Name = pool.Name;
        //    Save();
        //}

        public void AddClient(ClientConfiguration clientConfig)
        {
            if (_configuration.Clients.Any(c => c.ClientName == clientConfig.ClientName))
            {
                throw new System.ArgumentException($"客户端名称 '{clientConfig.ClientName}' 已存在");
            }

            _configuration.Clients.Add(clientConfig);
            Save();
        }

        public void RemoveClient(string clientName)
        {
            var client = _configuration.Clients.FirstOrDefault(c => c.ClientName == clientName);
            if (client != null)
            {
                _configuration.Clients.Remove(client);
                Save();
            }
        }

        public ClientConfiguration GetClient(string clientName)
        {
            return _configuration.Clients.FirstOrDefault(c => c.ClientName == clientName);
        }
    }
}