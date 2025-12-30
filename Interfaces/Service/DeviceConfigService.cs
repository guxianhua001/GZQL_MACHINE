using System;
using System.Data;
using System.IO;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Xml.Serialization;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace Interfaces.Services
{
    public static class DeviceConfigService
    {
        private static DeviceConfig _currentConfig;
        public static DeviceConfig CurrentConfig
        {
            get => _currentConfig ??= GetDefaultConfig();
            private set => _currentConfig = value;
        }

        // 获取数据保存路径（通过静态属性）
        public static string CurrentDataSavePath => CurrentConfig.DataSavePath;
        // 默认配置路径
        private static readonly string DefaultConfigDirectory = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Config");

        public static string ConfigDirectory { get; private set; } = DefaultConfigDirectory;

        private static string DeviceConfigFile => Path.Combine(ConfigDirectory, "DeviceConfig.json");

        // 确保目录存在
        static DeviceConfigService()
        {
            if (!Directory.Exists(ConfigDirectory))
                Directory.CreateDirectory(ConfigDirectory);
            // 初始化时加载配置
            CurrentConfig = LoadDeviceConfig();
        }

        public static void ChangeConfigDirectory(string newDirectory)
        {
            if (!Directory.Exists(newDirectory))
                Directory.CreateDirectory(newDirectory);

            ConfigDirectory = newDirectory;
        }
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,  // 美化输出，方便阅读
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static DeviceConfig LoadDeviceConfig()
        {
            try
            {
                if (!File.Exists(DeviceConfigFile))
                {
                    CurrentConfig = GetDefaultConfig();
                    return CurrentConfig;
                }

                var serializer = new XmlSerializer(typeof(DeviceConfig));
                using var reader = new StreamReader(DeviceConfigFile);
                var config = (DeviceConfig)serializer.Deserialize(reader);

                // 更新静态配置
                CurrentConfig = config ?? GetDefaultConfig();
                return CurrentConfig;
            }
            catch (Exception ex)
            {
                // 可以记录日志
                System.Diagnostics.Debug.WriteLine($"加载配置失败: {ex.Message}");
                CurrentConfig = GetDefaultConfig();
                return CurrentConfig;
            }
        }
        public static void SaveDeviceConfig(DeviceConfig config)
        {
            try
            {
                if (config == null) return;
                // 更新静态配置
                CurrentConfig = config;

                var dir = Path.GetDirectoryName(DeviceConfigFile);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var serializer = new XmlSerializer(typeof(DeviceConfig));
                using var writer = new StreamWriter(DeviceConfigFile);
                serializer.Serialize(writer, config);
                // 触发配置更改事件
                ConfigChanged?.Invoke(null, new ConfigChangedEventArgs
                {
                    ConfigFile = DeviceConfigFile,
                    ConfigDirectory = ConfigDirectory
                });
            }
            catch (Exception ex)
            {
                // 可以记录日志
                System.Diagnostics.Debug.WriteLine($"保存配置失败: {ex.Message}");
                throw;
            }
        }
        public static DeviceConfig GetDefaultConfig()
        {
            return new DeviceConfig
            {
                EnableSafetyGate = true,
                EnableBuzzer = false,
                EnableSnCode = true,    
                EnableSecsGem = false,
                IsModule1Enabled = true,
                IsModule2Enabled = true,
                IsModule3Enabled = true,
                IsModule4Enabled = true,
                SecsGemIP = "127.0.0.1",
                SecsGemPort = "5000",
                SecsGemDeviceId = "EQP001",
                DataSavePath = GetDefaultDataPath(),
                LastUpdated = DateTime.Now
            };
        }

        public static string GetDefaultDataPath()
        {
            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "DeviceData");
        }
        /// <summary>
        /// 清理过期数据（应在后台线程调用）
        /// </summary>
        public static void CleanupExpiredData()
        {
            try
            {
                var config = CurrentConfig;
                if (!config.AutoCleanOldData || config.DataRetentionDays <= 0)
                    return;

                var dataPath = config.DataSavePath;
                if (!Directory.Exists(dataPath))
                    return;

                // 计算清理截止日期
                var cutoffDate = DateTime.Now.AddDays(-config.DataRetentionDays);
                IMessage.Logger.Info($"开始清理过期数据，路径: {dataPath}, 时间点: {cutoffDate:yyyy-MM-dd}");

                // 清理文件和文件夹
                CleanDirectory(dataPath, cutoffDate);
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"清理旧数据失败: {ex.Message}");
            }
        }

        // 添加异步清理方法，支持取消操作
        public static async Task CleanupExpiredDataAsync(
            int maxDegreeOfParallelism = 2,
            int throttleMs = 100,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var config = CurrentConfig;
                if (!config.AutoCleanOldData || config.DataRetentionDays <= 0)
                    return;
                var dataPath = config.DataSavePath;
                if (!Directory.Exists(dataPath))
                    return;
                var cutoffDate = DateTime.Now.AddDays(-config.DataRetentionDays);
                IMessage.Logger.Info($"开始异步清理过期数据，路径: {dataPath}, 时间点: {cutoffDate:yyyy-MM-dd}");
                // 异步清理主目录（允许并行操作但控制并发数）
                await CleanDirectoryAsync(dataPath, cutoffDate, maxDegreeOfParallelism, throttleMs, cancellationToken);
                IMessage.Logger.Info($"异步清理过期数据完成.");
            }
            catch (OperationCanceledException)
            {
                IMessage.Logger.Info("数据清理已取消");
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"清理旧数据失败: {ex.Message} \n {ex.StackTrace}");
            }
        }
        private static async Task CleanDirectoryAsync(
            string directoryPath,
            DateTime cutoffDate,
            int maxDegreeOfParallelism,
            int throttleMs,
            CancellationToken ct)
        {
            // 优先清理文件（更快腾出空间）
            await foreach (var file in GetFilesAsync(directoryPath).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime <= cutoffDate)
                    {
                        File.Delete(file);
                        IMessage.Logger.Debug($"删除过期文件: {file}");

                        // 定期暂停释放资源
                        await Task.Delay(throttleMs, ct).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    IMessage.Logger.Debug($"文件删除失败 {file}: {ex.Message}");
                }
            }
            // 并行清理子目录（控制并行度）
            var childDirTasks = new List<Task>();
            foreach (var dir in Directory.GetDirectories(directoryPath))
            {
                if (childDirTasks.Count >= maxDegreeOfParallelism)
                {
                    var completed = await Task.WhenAny(childDirTasks);
                    childDirTasks.Remove(completed);
                }
                childDirTasks.Add(CleanDirectoryAsync(dir, cutoffDate, maxDegreeOfParallelism, throttleMs, ct));
            }
            await Task.WhenAll(childDirTasks).ConfigureAwait(false);
            // 延迟删除空目录（避免干扰主任务）
            try
            {
                if (!Directory.EnumerateFileSystemEntries(directoryPath).Any())
                {
                    Directory.Delete(directoryPath, false);
                    IMessage.Logger.Debug($"删除空目录: {directoryPath}");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                IMessage.Logger.Debug($"目录删除失败 {directoryPath}: {ex.Message}");
            }
        }
        // 异步枚举文件（减少内存占用）
        private static async IAsyncEnumerable<string> GetFilesAsync(string path)
        {
            var queue = new Queue<string>();
            queue.Enqueue(path);
            while (queue.Count > 0)
            {
                var dir = queue.Dequeue();
                foreach (var subDir in Directory.GetDirectories(dir))
                {
                    queue.Enqueue(subDir);
                }
                foreach (var file in Directory.GetFiles(dir))
                {
                    await Task.Yield(); // 每次yield让出控制权
                    yield return file;
                }
            }
        }
        private static void CleanDirectory(string directoryPath, DateTime cutoffDate)
        {
            // 清理文件
            foreach (var file in Directory.GetFiles(directoryPath))
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime < cutoffDate)
                    {
                        File.Delete(file);
                        IMessage.Logger.Info($"删除过期文件: {file}");
                    }
                }
                catch { /* 忽略错误 */ }
            }

            // 递归清理子目录
            foreach (var dir in Directory.GetDirectories(directoryPath))
            {
                CleanDirectory(dir, cutoffDate);

                // 删除空目录
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    {
                        Directory.Delete(dir, false);
                        IMessage.Logger.Debug($"删除空目录: {dir}");
                    }
                }
                catch { /* 忽略错误 */ }
            }
        }

        public static event EventHandler<ConfigChangedEventArgs> ConfigChanged;
    }

    public class ConfigChangedEventArgs : EventArgs
    {
        public string ConfigFile { get; set; }
        public string ConfigDirectory { get; set; }
        public DateTime ChangeTime { get; } = DateTime.Now;
    }

    [Serializable]
    public class DeviceConfig
    {
        public bool EnableSafetyGate { get; set; }
        public bool EnableBuzzer { get; set; }
        public bool EnableSnCode { get; set; }
        public bool EnableSecsGem { get; set; }
        public string SecsGemPort { get; set; }
        public string SecsGemDeviceId { get; set; }
        public string DataSavePath { get; set; }
        [Description("数据保留天数 (0=永久保存)")]
        public int DataRetentionDays { get; set; } = 30;  // 默认保留30天

        [Description("自动清理旧数据")]
        public bool AutoCleanOldData { get; set; } = true;
        public string SecsGemIP { get; set; }
        public bool IsModule1Enabled { get; set; }
        public bool IsModule2Enabled { get; set; }
        public bool IsModule3Enabled { get; set; }
        public bool IsModule4Enabled { get; set; }

        [JsonIgnore]  // 不序列化到JSON
        public DateTime LastUpdated { get; set; }

        // 可以添加更多辅助方法
        public bool IsValidPort() =>
            int.TryParse(SecsGemPort, out int port) && port > 0 && port <= 65535;
    }
}
