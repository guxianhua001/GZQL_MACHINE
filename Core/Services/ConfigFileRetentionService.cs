using Core.Abstraction;
using Core.Configuration;
using Core.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Services
{
    /// <summary>
    /// 配置文件保留策略服务实现。
    /// 按文件夹最大文件数量清理旧文件，并提供统一的文件夹路径解析。
    /// 设置来源于 <see cref="IAppSettingService.Settings.ConfigFileRetention"/>。
    /// </summary>
    public class ConfigFileRetentionService : IConfigFileRetentionService
    {
        /// <summary>受管理的文件夹标识与其相对路径（相对于根目录）的映射</summary>
        private static readonly Dictionary<string, string> FolderRelativePaths = new()
        {
            { "ProcessSequences", "ProcessSequences" },
            { "ZScan", "ZScan" },
            { "VisionCapture", "VisionCapture" },
            { "Dot", "Dot" },
            { "CalibrationSystem1", Path.Combine("Calibration", "System1") },
            { "CalibrationSystem2", Path.Combine("Calibration", "System2") },
            { "CadAlignment", "CadAlignment" }
        };

        /// <summary>文件夹标识对应的多语言资源 Key</summary>
        private static readonly Dictionary<string, string> FolderDisplayNameKeys = new()
        {
            { "ProcessSequences", "DeviceConfig_Folder_ProcessSequences" },
            { "ZScan", "DeviceConfig_Folder_ZScan" },
            { "VisionCapture", "DeviceConfig_Folder_VisionCapture" },
            { "Dot", "DeviceConfig_Folder_Dot" },
            { "CalibrationSystem1", "DeviceConfig_Folder_CalibrationSystem1" },
            { "CalibrationSystem2", "DeviceConfig_Folder_CalibrationSystem2" },
            { "CadAlignment", "DeviceConfig_Folder_CadAlignment" }
        };

        private readonly IAppSettingService _appSettingService;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localization;
        private readonly object _settingsLock = new object();
        private ConfigFileRetentionSettings _settings;

        /// <summary>
        /// 构造函数：注入应用配置服务和日志服务，并加载当前保留策略设置。
        /// </summary>
        /// <param name="appSettingService">应用配置服务</param>
        /// <param name="logger">日志服务</param>
        /// <param name="localization">本地化服务</param>
        public ConfigFileRetentionService(IAppSettingService appSettingService, ILoggerService logger, ILocalizationService localization)
        {
            _appSettingService = appSettingService ?? throw new ArgumentNullException(nameof(appSettingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            LoadSettings();
        }

        /// <inheritdoc />
        public IReadOnlyList<string> ManagedFolderKeys => FolderRelativePaths.Keys.OrderBy(k => k).ToList().AsReadOnly();

        /// <inheritdoc />
        public string GetFolderDisplayNameKey(string folderKey)
        {
            return FolderDisplayNameKeys.TryGetValue(folderKey, out var key) ? key : folderKey;
        }

        /// <inheritdoc />
        public string GetFolderRelativePath(string folderKey)
        {
            return FolderRelativePaths.TryGetValue(folderKey, out var path) ? path : folderKey;
        }

        /// <inheritdoc />
        public string GetFolderFullPath(string folderKey)
        {
            var relativePath = GetFolderRelativePath(folderKey);
            var basePath = ResolveBasePath();
            var fullPath = Path.Combine(basePath, relativePath);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }
            return fullPath;
        }

        /// <inheritdoc />
        public int GetMaxFileCount(string folderKey)
        {
            var settings = GetCurrentSettings();
            if (settings.FolderMaxCounts != null
                && settings.FolderMaxCounts.TryGetValue(folderKey, out var count))
            {
                return count > 0 ? count : 0;
            }
            return settings.DefaultMaxFileCount;
        }

        /// <inheritdoc />
        public void SetMaxFileCount(string folderKey, int maxCount)
        {
            var settings = GetCurrentSettings();
            settings.FolderMaxCounts ??= new Dictionary<string, int>();
            settings.FolderMaxCounts[folderKey] = maxCount;
        }

        /// <inheritdoc />
        public void CleanupFolderByCount(string folderKey, string filePattern, string currentFilePath = null)
        {
            var settings = GetCurrentSettings();
            // 全局开关关闭时不执行清理
            if (!settings.Enabled)
            {
                return;
            }

            var maxCount = GetMaxFileCount(folderKey);
            // maxCount <= 0 表示不限制
            if (maxCount <= 0)
            {
                return;
            }

            try
            {
                var folderPath = GetFolderFullPath(folderKey);
                if (!Directory.Exists(folderPath))
                {
                    return;
                }

                // 枚举匹配文件，按最后写入时间升序排序（最旧在前）
                var files = Directory.EnumerateFiles(folderPath, filePattern)
                    .Select(f => new FileInfo(f))
                    .OrderBy(f => f.LastWriteTime)
                    .ToList();

                if (files.Count <= maxCount)
                {
                    return;
                }

                // 需要删除的文件数量
                var deleteCount = files.Count - maxCount;
                var cleanedCount = 0;

                foreach (var file in files)
                {
                    if (deleteCount <= 0)
                    {
                        break;
                    }

                    // 跳过当前刚保存的文件
                    if (!string.IsNullOrEmpty(currentFilePath)
                        && string.Equals(file.FullName, currentFilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        file.Delete();
                        cleanedCount++;
                        deleteCount--;
                        _logger.Info(string.Format(_localization.GetResourceOrDefault("CfgRet_Log_CleanedOldFile", "[ConfigRetention] 已清理旧配置文件: {0} (超出最大数量{1})"), file.FullName, maxCount));
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn(string.Format(_localization.GetResourceOrDefault("CfgRet_Log_CleanFileFailed", "[ConfigRetention] 清理旧配置文件失败: {0}, {1}"), file.FullName, ex.Message));
                    }
                }

                if (cleanedCount > 0)
                {
                    _logger.Info(string.Format(_localization.GetResourceOrDefault("CfgRet_Log_FolderCleanupSummary", "[ConfigRetention] 文件夹 {0} 本次清理了 {1} 个旧文件 (保留最大{2}个)"), folderKey, cleanedCount, maxCount));
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(_localization.GetResourceOrDefault("CfgRet_Log_CleanupFolderException", "[ConfigRetention] 清理文件夹 {0} 旧文件异常: {1}"), folderKey, ex.Message));
            }
        }

        /// <inheritdoc />
        public Task CleanupFolderByCountAsync(string folderKey, string filePattern, string currentFilePath = null)
        {
            return Task.Run(() => CleanupFolderByCount(folderKey, filePattern, currentFilePath));
        }

        /// <inheritdoc />
        public void RefreshSettings()
        {
            lock (_settingsLock)
            {
                LoadSettings();
            }
        }

        /// <summary>从 IAppSettingService 加载保留策略设置</summary>
        private void LoadSettings()
        {
            lock (_settingsLock)
            {
                try
                {
                    _settings = _appSettingService.Settings.ConfigFileRetention ?? new ConfigFileRetentionSettings();
                }
                catch (Exception ex)
                {
                    _logger.Warn(string.Format(_localization.GetResourceOrDefault("CfgRet_Log_LoadSettingsFailed", "[ConfigRetention] 加载保留策略设置失败，使用默认值: {0}"), ex.Message));
                    _settings = new ConfigFileRetentionSettings();
                }
            }
        }

        /// <summary>获取当前设置快照（线程安全）</summary>
        private ConfigFileRetentionSettings GetCurrentSettings()
        {
            lock (_settingsLock)
            {
                return _settings ?? (_settings = new ConfigFileRetentionSettings());
            }
        }

        /// <summary>
        /// 解析根目录：若 BasePath 为空或无效，使用默认值 &lt;应用基目录&gt;\Config。
        /// </summary>
        private string ResolveBasePath()
        {
            var settings = GetCurrentSettings();
            if (!string.IsNullOrWhiteSpace(settings.BasePath))
            {
                try
                {
                    var path = settings.BasePath.Trim();
                    if (Path.IsPathRooted(path))
                    {
                        if (!Directory.Exists(path))
                        {
                            Directory.CreateDirectory(path);
                        }
                        return path;
                    }
                    // 相对路径：基于应用基目录
                    var combined = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
                    if (!Directory.Exists(combined))
                    {
                        Directory.CreateDirectory(combined);
                    }
                    return combined;
                }
                catch (Exception ex)
                {
                    _logger.Warn(string.Format(_localization.GetResourceOrDefault("CfgRet_Log_BasePathInvalid", "[ConfigRetention] BasePath 无效，回退到默认 Config 目录: {0}"), ex.Message));
                }
            }
            // 默认根目录
            var defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
            if (!Directory.Exists(defaultPath))
            {
                Directory.CreateDirectory(defaultPath);
            }
            return defaultPath;
        }
    }
}
