// Core/Services/JsonParameterStorage.cs
using Core.Abstraction;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
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
                    return JsonConvert.DeserializeObject<T>(json, GetSerializerSettings()) ?? new T();
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

            File.WriteAllText(filePath, JsonConvert.SerializeObject(data, GetSerializerSettings()));
        }

        /// <summary>
        /// 获取 JSON 序列化设置，统一使用本地时间格式（无时区后缀），
        /// 与 JsonRecipeFileStorage 的 LocalDateTimeConverter 保持一致。
        /// </summary>
        private JsonSerializerSettings GetSerializerSettings()
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore,
                TypeNameHandling = TypeNameHandling.Auto
            };
            // 注册本地时间转换器，序列化时输出无时区后缀的本地时间
            settings.Converters.Add(new LocalNewtonsoftDateTimeConverter());
            return settings;
        }

        private string GetFilePath(string identifier, string directory)
        {
            var safeIdentifier = string.Join("_", identifier.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(directory, $"{safeIdentifier}.json");
        }
    }

    /// <summary>
    /// Newtonsoft.Json 的 DateTime 自定义转换器。
    /// 序列化时统一输出无时区后缀的本地时间格式（如 2026-06-18T14:59:06.8346289），
    /// 与 JsonRecipeFileStorage.LocalDateTimeConverter 行为一致。
    /// 反序列化时兼容 ISO 8601 各种格式（Z、+08:00、无后缀），并转换为 Local Kind。
    /// </summary>
    public class LocalNewtonsoftDateTimeConverter : Newtonsoft.Json.JsonConverter<DateTime>
    {
        private const string LocalFormat = "yyyy-MM-ddTHH:mm:ss.fffffff";

        public override DateTime ReadJson(JsonReader reader, Type objectType, DateTime existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Date && reader.Value is DateTime dt)
            {
                // 统一转换为 Local Kind，确保内存中时间与计算机时区一致
                return dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt;
            }
            if (reader.TokenType == JsonToken.String && reader.Value is string s && DateTime.TryParse(s, out var parsed))
            {
                return parsed.Kind == DateTimeKind.Utc ? parsed.ToLocalTime() : parsed;
            }
            return DateTime.Now;
        }

        public override void WriteJson(JsonWriter writer, DateTime value, JsonSerializer serializer)
        {
            // 统一转换为本地时间后输出无时区后缀格式
            var local = value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
            writer.WriteValue(local.ToString(LocalFormat));
        }
    }
}