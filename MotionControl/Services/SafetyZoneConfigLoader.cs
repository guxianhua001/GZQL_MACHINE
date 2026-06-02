using Core.Abstraction;
using Core.Utilities;
using MotionControl.Interfaces;
using MotionControl.Models;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;

namespace MotionControl.Services
{
    /// <summary>
    /// 安全互锁 JSON 配置加载器，支持旧版扁平字段自动迁移
    /// </summary>
    public class SafetyZoneConfigLoader : ISafetyZoneConfigLoader
    {
        private readonly ILoggerService _logger;
        private readonly IAppSettingService _appSettings;

        public SafetyZoneConfigLoader(ILoggerService logger, IAppSettingService appSettings = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _appSettings = appSettings;
        }

        public string ConfigFilePath => _appSettings?.GetValue("SafetyZoneConfigPath")
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "SafetyZoneConfig.json");

        public SafetyZoneConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    _logger.Info("[安全互锁] 未找到配置文件，使用默认机型规则");
                    return SafetyZoneConfig.CreateDefaultForCurrentMachine();
                }

                var json = File.ReadAllText(ConfigFilePath);
                var config = JsonConvert.DeserializeObject<SafetyZoneConfig>(json);
                if (config == null)
                    return SafetyZoneConfig.CreateDefaultForCurrentMachine();

                MigrateIfNeeded(config);
                return config;
            }
            catch (Exception ex)
            {
                _logger.Warn($"[安全互锁] 加载配置失败: {ex.Message}，使用默认规则");
                return SafetyZoneConfig.CreateDefaultForCurrentMachine();
            }
        }

        public void Save(SafetyZoneConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            EnsureConfigDirectory();
            config.SchemaVersion = 2;
            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(ConfigFilePath, json);
            _logger.Info("[安全互锁] 配置已保存");
        }

        public SafetyZoneConfig CreateDefault() => SafetyZoneConfig.CreateDefaultForCurrentMachine();

        /// <summary>
        /// 确保配置为 v2 规则结构（供 Monitor 热更新时调用）
        /// </summary>
        public static void EnsureMigrated(SafetyZoneConfig config) => MigrateIfNeeded(config);

        /// <summary>
        /// 将 v1 扁平字段迁移为 Rules + DangerZones 结构
        /// </summary>
        private static void MigrateIfNeeded(SafetyZoneConfig config)
        {
            if (config.SchemaVersion >= 2 && config.Rules != null && config.Rules.Count > 0)
                return;

            var migrated = SafetyZoneConfig.CreateDefaultForCurrentMachine();
            migrated.Enabled = config.Enabled;
            migrated.FailClosedOnMissingAxis = config.FailClosedOnMissingAxis;
            migrated.JogEstimateOffset = config.JogEstimateOffset > 0 ? config.JogEstimateOffset : 10.0;

            var rule = migrated.GetOrCreateHeightLockPlaneRule();
            double z1 = config.SafeHeightZ1 ?? 50.0;
            foreach (var ha in rule.HeightAxes)
                ha.SafeHeight = z1;

            if (config.DangerZoneXMin.HasValue || config.DangerZoneXMax.HasValue)
            {
                var dx = migrated.DangerZones.FirstOrDefault(z => z.AxisName == "Dx");
                if (dx != null)
                {
                    if (config.DangerZoneXMin.HasValue) dx.Min = config.DangerZoneXMin.Value;
                    if (config.DangerZoneXMax.HasValue) dx.Max = config.DangerZoneXMax.Value;
                }
            }

            if (config.DangerZoneYMin.HasValue || config.DangerZoneYMax.HasValue)
            {
                var dy = migrated.DangerZones.FirstOrDefault(z => z.AxisName == "Dy");
                if (dy != null)
                {
                    if (config.DangerZoneYMin.HasValue) dy.Min = config.DangerZoneYMin.Value;
                    if (config.DangerZoneYMax.HasValue) dy.Max = config.DangerZoneYMax.Value;
                }
            }

            config.SchemaVersion = 2;
            config.Rules = migrated.Rules;
            config.DangerZones = migrated.DangerZones;
        }

        private void EnsureConfigDirectory()
        {
            var dir = Path.GetDirectoryName(ConfigFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
