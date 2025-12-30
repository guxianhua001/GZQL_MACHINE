using System;
using System.IO;
using Core.Abstraction;
using Newtonsoft.Json;

namespace Core.Services
{
    public class JsonConfigurationService : IConfigurationService
    {
        public JsonConfigurationService()
        {
            // 确保配置目录存在
            var configPath = GetDefaultConfigPath();
            if (!Directory.Exists(configPath))
            {
                Directory.CreateDirectory(configPath);
            }
        }

        public void SaveConfiguration(string sectionName, string format, object config)
        {
            var filePath = Path.Combine(GetDefaultConfigPath(), $"{sectionName}.json");
            try
            {
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"保存配置失败: {filePath}", ex);
            }
        }

        public T LoadConfiguration<T>(string sectionName) where T : new()
        {
            var filePath = Path.Combine(GetDefaultConfigPath(), $"{sectionName}.json");
            if (!File.Exists(filePath))
                return new T();

            try
            {
                var json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<T>(json) ?? new T();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"加载配置失败: {filePath}", ex);
            }
        }

        private static string GetDefaultConfigPath()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory();
            return Path.Combine(baseDir, "Config", "Position");
        }

        // 可选：添加一个公共属性来获取配置路径，便于调试和测试
        public string ConfigDirectory => GetDefaultConfigPath();
    }
}