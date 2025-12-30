// Core.Infrastructure/Storages/JsonFileStorage.cs
using System;
using System.IO;
using System.Text.Json;
using Core.Abstractions.Storages;

namespace Core.Services
{
    /// <summary>
    ///  配方的JSON文件存储服务，用于加载、保存和删除配方数据。
    /// </summary>
    public class JsonRecipeFileStorage : IGenericStorage
    {
        private readonly string _basePath;

        public JsonRecipeFileStorage(string basePath = null)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory();
            _basePath = basePath ?? Path.Combine(baseDir, "Recipes");
            Directory.CreateDirectory(_basePath);
        }

        public async Task<T> LoadAsync<T>(string identifier) where T : class, new()
        {
            var filePath = GetFilePath<T>(identifier);
            if (!File.Exists(filePath))
                return new T();

            try
            {
                var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
                return JsonSerializer.Deserialize<T>(json) ?? new T();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading {typeof(T).Name} from {filePath}: {ex.Message}");
                return new T();
            }
        }

        public async Task SaveAsync<T>(string identifier, T data) where T : class
        {
            var filePath = GetFilePath<T>(identifier);
            var directory = Path.GetDirectoryName(filePath);
            Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
        }

        public Task<bool> ExistsAsync<T>(string identifier)
        {
            var filePath = GetFilePath<T>(identifier);
            return Task.FromResult(File.Exists(filePath));
        }

        public Task DeleteAsync<T>(string identifier)
        {
            var filePath = GetFilePath<T>(identifier);
            if (File.Exists(filePath))
                File.Delete(filePath);

            return Task.CompletedTask;
        }
        // 获取所有配方池文件
        public Task<List<string>> GetAllRecipePoolFilesAsync()
        {
            var recipePoolDir = Path.Combine(_basePath, "recipepool");
            if (!Directory.Exists(recipePoolDir))
                return Task.FromResult(new List<string>());

            var files = Directory.GetFiles(recipePoolDir, "*.json")
                                .Select(Path.GetFileName)
                                .ToList();
            return Task.FromResult(files);
        }

        // 从文件名提取池ID
        public string ExtractPoolIdFromFileName(string fileName)
        {
            // 从 "recipe_pool_af7b5b18-511e-4b0a-9302-e51bdb26af10.json" 提取 "af7b5b18-511e-4b0a-9302-e51bdb26af10"
            if (fileName.StartsWith("recipe_pool_") && fileName.EndsWith(".json"))
            {
                return fileName.Substring(12, fileName.Length - 17);
            }
            return fileName;
        }
        private string GetFilePath<T>(string identifier)
        {
            var typeName = typeof(T).Name.ToLower();
            var safeIdentifier = Path.GetFileName(identifier); // 防止路径遍历
            return Path.Combine(_basePath, typeName, $"{safeIdentifier}.json");
        }
    }
}
