using Core.Abstraction;
using Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;

namespace Core.Services
{
    /// <summary>
    /// ZMAP标定配置持久化服务实现——JSON读写风格与 ZScanConfigService 保持一致
    /// （Newtonsoft.Json + camelCase + 缩进格式化），默认存储目录 Config/ZMap。
    /// </summary>
    public class ZMapConfigService : IZMapConfigService
    {
        private readonly string _configDirectory;
        private readonly JsonSerializerSettings _serializerSettings;

        public ZMapConfigService() : this(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "ZMap"))
        {
        }

        public ZMapConfigService(string configDirectory)
        {
            _configDirectory = configDirectory;
            _serializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore,
                TypeNameHandling = TypeNameHandling.Auto
            };
        }

        public ZMapCalibrationConfig Load(string fileName = "ZMapCalibration.json")
        {
            var filePath = Path.Combine(_configDirectory, fileName);

            if (!File.Exists(filePath))
                return new ZMapCalibrationConfig();

            try
            {
                var json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<ZMapCalibrationConfig>(json, _serializerSettings)
                       ?? new ZMapCalibrationConfig();
            }
            catch
            {
                // 配置文件损坏时不影响主流程，返回空配置，等待用户重新标定
                return new ZMapCalibrationConfig();
            }
        }

        public void Save(ZMapCalibrationConfig config, string fileName = "ZMapCalibration.json")
        {
            if (config == null) return;

            if (!Directory.Exists(_configDirectory))
                Directory.CreateDirectory(_configDirectory);

            config.LastUpdatedTime = DateTime.Now;
            var filePath = Path.Combine(_configDirectory, fileName);
            var json = JsonConvert.SerializeObject(config, _serializerSettings);
            File.WriteAllText(filePath, json);
        }

        public string GetConfigPath() => _configDirectory;
    }
}
