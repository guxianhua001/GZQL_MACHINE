using Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Configuration
{
    /// <summary>
    /// 应用程序设置
    /// </summary>
    public class AppSettings
    {
        public string RecipeName { get; set; } = "Default";
        public string LastRecipeName { get; set; } = "Default";
        public string LastSelectedRecipePath { get; set; } = string.Empty;

        public string Language { get; set; } = "zh-CN"; // 默认语言
        public bool AutoDetectLanguage { get; set; } = true;
        public string Theme { get; set; } = "Dark";
        public int MaxLogFiles { get; set; } = 10;
        public int SaveLogsDays { get; set; } = 30;
        public string HardwareConfigPath { get; set; } = "D:\\Config\\hwcfg.xml";

        public ServerConfiguration Server { get; set; } = new ServerConfiguration();
        public List<ClientConfiguration> Clients { get; set; } = new List<ClientConfiguration>();

        public bool EnableSafetyGate { get; set; } = true;
        public bool EnableBuzzer { get; set; } = false;
        public bool EnableGrating { get; set; } = true;
        public bool EnableSafetyEventLog { get; set; } = true;

        /// <summary>
        /// 配置文件保留策略：按文件夹最大文件数量清理旧文件。
        /// 默认根目录为 &lt;应用基目录&gt;\Config，可通过 ConfigFileRetention.BasePath 自定义。
        /// </summary>
        public ConfigFileRetentionSettings ConfigFileRetention { get; set; } = new();

        /// <summary>
        /// 捕获 JSON 中未显式映射的其他配置项
        /// 例如："HardwareConfigPath": "D:\\Config\\hwcfg.xml"
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; set; } = new();
    }
}
