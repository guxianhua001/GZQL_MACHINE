using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Abstraction
{
    /// <summary>
    /// 配置文件保留策略服务接口。
    /// 负责按文件夹最大文件数量清理旧文件，并提供统一的文件夹路径解析。
    /// </summary>
    public interface IConfigFileRetentionService
    {
        /// <summary>
        /// 获取受管理的文件夹标识列表（按字母序）。
        /// 包含：ProcessSequences、ZScan、VisionCapture、Dot、CalibrationSystem1、CalibrationSystem2、CadAlignment。
        /// </summary>
        IReadOnlyList<string> ManagedFolderKeys { get; }

        /// <summary>
        /// 获取文件夹显示名称（多语言资源 Key，由调用方通过 ILocalizationService 解析）。
        /// </summary>
        /// <param name="folderKey">文件夹标识</param>
        /// <returns>多语言资源 Key</returns>
        string GetFolderDisplayNameKey(string folderKey);

        /// <summary>
        /// 获取文件夹相对路径（相对于根目录，如 "ProcessSequences"、"Calibration\System1"）。
        /// </summary>
        /// <param name="folderKey">文件夹标识</param>
        /// <returns>相对路径字符串</returns>
        string GetFolderRelativePath(string folderKey);

        /// <summary>
        /// 获取文件夹完整路径。若 <see cref="Configuration.ConfigFileRetentionSettings.BasePath"/> 为空，
        /// 则使用默认根目录 &lt;应用基目录&gt;\Config。
        /// </summary>
        /// <param name="folderKey">文件夹标识</param>
        /// <returns>文件夹完整路径</returns>
        string GetFolderFullPath(string folderKey);

        /// <summary>
        /// 获取指定文件夹的最大文件数量。
        /// 优先读取 FolderMaxCounts 中的显式配置，未配置则返回 DefaultMaxFileCount。
        /// </summary>
        /// <param name="folderKey">文件夹标识</param>
        /// <returns>最大文件数量</returns>
        int GetMaxFileCount(string folderKey);

        /// <summary>
        /// 设置指定文件夹的最大文件数量。
        /// </summary>
        /// <param name="folderKey">文件夹标识</param>
        /// <param name="maxCount">最大文件数量</param>
        void SetMaxFileCount(string folderKey, int maxCount);

        /// <summary>
        /// 清理指定文件夹中超出最大数量的旧文件。
        /// 按 <paramref name="filePattern"/> 匹配文件，按最后写入时间升序排序，删除最旧的文件直至数量不超过最大值。
        /// 跳过 <paramref name="currentFilePath"/> 指定的当前文件。
        /// </summary>
        /// <param name="folderKey">文件夹标识</param>
        /// <param name="filePattern">文件匹配模式（如 "ProcessSequences_*.json"）</param>
        /// <param name="currentFilePath">当前刚保存的文件完整路径，不会被删除；可为 null</param>
        void CleanupFolderByCount(string folderKey, string filePattern, string currentFilePath = null);

        /// <summary>
        /// 异步清理指定文件夹中超出最大数量的旧文件。
        /// </summary>
        /// <param name="folderKey">文件夹标识</param>
        /// <param name="filePattern">文件匹配模式</param>
        /// <param name="currentFilePath">当前刚保存的文件完整路径；可为 null</param>
        /// <returns>表示异步操作的任务</returns>
        Task CleanupFolderByCountAsync(string folderKey, string filePattern, string currentFilePath = null);

        /// <summary>
        /// 从 <see cref="IAppSettingService"/> 重新加载保留策略设置。
        /// 在 DeviceConfigView 保存配置后调用，确保清理逻辑使用最新配置。
        /// </summary>
        void RefreshSettings();
    }
}
