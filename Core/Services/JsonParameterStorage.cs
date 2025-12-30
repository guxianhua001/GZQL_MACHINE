// Core/Services/JsonParameterStorage.cs
using Core.Abstraction;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;

namespace Core.Services
{
    public class JsonParameterStorage : IParameterStorage
    {
        private readonly string _defaultConfigDirectory;

        public JsonParameterStorage()
        {
            _defaultConfigDirectory = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Config",
                "Parameters");
            Directory.CreateDirectory(_defaultConfigDirectory);
        }

        public TParams Load<TParams>(string identifier) where TParams : TaskParametersBase, new()
        {
            return Load<TParams>(identifier, _defaultConfigDirectory);
        }

        public void Save<TParams>(string identifier, TParams parameters) where TParams : TaskParametersBase
        {
            Save(identifier, parameters, _defaultConfigDirectory);
        }

        /// <summary>
        /// 加载数据（支持自定义路径）
        /// </summary>
        public T Load<T>(string identifier, string customDirectory) where T : class, new()
        {
            var filePath = GetFilePath(identifier, customDirectory);

            try
            {
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    return JsonConvert.DeserializeObject<T>(json) ?? new T();
                }
            }
            catch (Exception ex)
            {
                // 记录错误
                return new T();
            }
            return new T();
        }

        /// <summary>
        /// 保存数据（支持自定义路径）
        /// </summary>
        public void Save<T>(string identifier, T data, string customDirectory) where T : class
        {
            if (data == null) return;

            var filePath = GetFilePath(identifier, customDirectory);
            var directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore,
                TypeNameHandling = TypeNameHandling.Auto
            };

            File.WriteAllText(filePath, JsonConvert.SerializeObject(data, settings));
        }

        private string GetFilePath(string identifier, string directory)
        {
            var safeIdentifier = string.Join("_", identifier.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(directory, $"{safeIdentifier}.json");
        }
    }
}