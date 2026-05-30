using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using Core.Abstractions.Storages;

namespace Core.Services
{
    /// <summary>
    /// 配方的JSON文件存储服务，用于加载、保存和删除配方数据。
    /// 使用文件级异步锁确保同一文件的并发读写安全
    /// </summary>
    public class JsonRecipeFileStorage : IGenericStorage
    {
        private readonly string _basePath;

        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        /// <summary>
        /// 文件级异步锁字典，确保同一文件的并发读写互斥
        /// </summary>
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();

        private static SemaphoreSlim GetFileLock(string filePath)
            => _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));

        public JsonRecipeFileStorage(string basePath = null)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory();
            _basePath = basePath ?? Path.Combine(baseDir, "Recipes");
            Directory.CreateDirectory(_basePath);
        }

        public async Task<T> LoadAsync<T>(string identifier) where T : class, new()
        {
            var filePath = GetFilePath<T>(identifier);
            if (!File.Exists(filePath)) return new T();

            var fileLock = GetFileLock(filePath);
            await fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
                return JsonSerializer.Deserialize<T>(json, _jsonOptions) ?? new T();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading {typeof(T).Name} from {filePath}: {ex.Message}");
                return new T();
            }
            finally
            {
                fileLock.Release();
            }
        }

        public async Task SaveAsync<T>(string identifier, T data) where T : class
        {
            var filePath = GetFilePath<T>(identifier);
            var directory = Path.GetDirectoryName(filePath);
            Directory.CreateDirectory(directory);

            var fileLock = GetFileLock(filePath);
            await fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);
            }
            finally
            {
                fileLock.Release();
            }
        }

        public Task<bool> ExistsAsync<T>(string identifier)
        {
            var filePath = GetFilePath<T>(identifier);
            return Task.FromResult(File.Exists(filePath));
        }

        public Task DeleteAsync<T>(string identifier)
        {
            var filePath = GetFilePath<T>(identifier);
            if (File.Exists(filePath)) File.Delete(filePath);
            return Task.CompletedTask;
        }

        public Task<List<string>> GetAllRecipePoolFilesAsync()
        {
            var recipePoolDir = Path.Combine(_basePath, "recipepool");
            if (!Directory.Exists(recipePoolDir)) return Task.FromResult(new List<string>());

            var files = Directory.GetFiles(recipePoolDir, "*.json")
                .Select(Path.GetFileName)
                .ToList();

            return Task.FromResult(files);
        }

        public string ExtractPoolIdFromFileName(string fileName)
        {
            if (fileName.StartsWith("recipe_pool_") && fileName.EndsWith(".json"))
            {
                return fileName.Substring(12, fileName.Length - 17);
            }
            return fileName;
        }

        private string GetFilePath<T>(string identifier)
        {
            var typeName = typeof(T).Name.ToLower();
            var safeIdentifier = Path.GetFileName(identifier);
            return Path.Combine(_basePath, typeName, $"{safeIdentifier}.json");
        }
    }
}
