using System.Collections.Generic;

namespace Core.Configuration
{
    /// <summary>
    /// 配置文件保留策略设置：按文件夹控制最大文件数量，超出则删除最旧文件。
    /// 所有文件夹默认位于应用程序 Config 目录下，可通过 <see cref="BasePath"/> 自定义根目录。
    /// </summary>
    public class ConfigFileRetentionSettings
    {
        /// <summary>是否启用按数量清理（全局开关）</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 配置文件根目录。为空时使用默认值：&lt;应用基目录&gt;\Config。
        /// 设置后所有子文件夹均位于此根目录下。
        /// </summary>
        public string BasePath { get; set; } = string.Empty;

        /// <summary>默认每个文件夹保留的最大文件数量（未在 FolderMaxCounts 中显式配置时使用）</summary>
        public int DefaultMaxFileCount { get; set; } = 100;

        /// <summary>
        /// 各文件夹最大文件数量映射。
        /// Key 为文件夹标识（如 ProcessSequences、ZScan、VisionCapture、Dot、CalibrationSystem1、CalibrationSystem2、CadAlignment），
        /// Value 为该文件夹允许保留的最大文件数。
        /// </summary>
        public Dictionary<string, int> FolderMaxCounts { get; set; } = new();
    }
}
