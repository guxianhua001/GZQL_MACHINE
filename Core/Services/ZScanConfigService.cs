using Core.Abstraction;
using Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;

namespace Core.Services
{
    public class ZScanConfigService : IZScanConfigService
    {
        private readonly string _configDirectory;
        private readonly JsonSerializerSettings _serializerSettings;
        private readonly IConfigFileRetentionService _configRetentionService;

        public ZScanConfigService() : this(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "ZScan"))
        {
        }

        public ZScanConfigService(string configDirectory) : this(configDirectory, null)
        {
        }

        /// <summary>
        /// 构造函数：指定配置目录和可选的保留策略服务。
        /// </summary>
        /// <param name="configDirectory">配置文件目录</param>
        /// <param name="configRetentionService">配置文件保留策略服务（可选，为 null 时不执行按数量清理）</param>
        public ZScanConfigService(string configDirectory, IConfigFileRetentionService configRetentionService)
        {
            _configDirectory = configDirectory;
            _configRetentionService = configRetentionService;
            _serializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore,
                TypeNameHandling = TypeNameHandling.Auto
            };
        }

        public ZScanConfigFile Load(string fileName = "ZScanConfig.json")
        {
            var filePath = Path.Combine(_configDirectory, fileName);

            if (!File.Exists(filePath))
                return new ZScanConfigFile();

            try
            {
                var json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<ZScanConfigFile>(json, _serializerSettings)
                       ?? new ZScanConfigFile();
            }
            catch
            {
                return new ZScanConfigFile();
            }
        }

        public void Save(ZScanConfigFile config, string fileName = "ZScanConfig.json")
        {
            if (config == null) return;

            if (!Directory.Exists(_configDirectory))
                Directory.CreateDirectory(_configDirectory);

            var filePath = Path.Combine(_configDirectory, fileName);
            var json = JsonConvert.SerializeObject(config, _serializerSettings);
            File.WriteAllText(filePath, json);
        }

        public string GetConfigPath()
        {
            return _configDirectory;
        }

        private string _lastSavedFilePath;

        public string LastSavedFilePath => _lastSavedFilePath;

        public string SaveWithTimestamp(ZScanConfigFile config)
        {
            if (config == null) return null;

            if (!Directory.Exists(_configDirectory))
                Directory.CreateDirectory(_configDirectory);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"ZScan_{timestamp}.json";
            var filePath = Path.Combine(_configDirectory, fileName);
            var json = JsonConvert.SerializeObject(config, _serializerSettings);
            File.WriteAllText(filePath, json);
            _lastSavedFilePath = filePath;

            // 后台按数量清理旧文件，避免阻塞UI
            _configRetentionService?.CleanupFolderByCountAsync("ZScan", "ZScan_*.json", filePath);

            return filePath;
        }

        public ZScanConfigFile LoadLastFromRecipePool()
        {
            if (string.IsNullOrEmpty(_lastSavedFilePath) || !File.Exists(_lastSavedFilePath))
                return new ZScanConfigFile();

            try
            {
                var json = File.ReadAllText(_lastSavedFilePath);
                return JsonConvert.DeserializeObject<ZScanConfigFile>(json, _serializerSettings)
                       ?? new ZScanConfigFile();
            }
            catch
            {
                return new ZScanConfigFile();
            }
        }

        public void SaveToRecipePool(ZScanConfigFile config, string recipeName)
        {
            if (config == null || string.IsNullOrEmpty(recipeName)) return;

            var recipeDir = Path.Combine(_configDirectory, "RecipePool");
            if (!Directory.Exists(recipeDir))
                Directory.CreateDirectory(recipeDir);

            var filePath = Path.Combine(recipeDir, $"{recipeName}_ZScan.json");
            var json = JsonConvert.SerializeObject(config, _serializerSettings);
            File.WriteAllText(filePath, json);
        }

        public ZScanConfigFile LoadFromFile(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
                return new ZScanConfigFile();

            try
            {
                var json = File.ReadAllText(fullPath);
                _lastSavedFilePath = fullPath;
                return JsonConvert.DeserializeObject<ZScanConfigFile>(json, _serializerSettings)
                       ?? new ZScanConfigFile();
            }
            catch
            {
                return new ZScanConfigFile();
            }
        }
    }
}
