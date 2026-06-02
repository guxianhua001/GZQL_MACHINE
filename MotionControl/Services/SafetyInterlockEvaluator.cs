using Core.Abstraction;
using MotionControl.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MotionControl.Services
{
    /// <summary>
    /// 安全互锁规则求值器（纯配置驱动，无硬编码轴名）
    /// </summary>
    internal static class SafetyInterlockEvaluator
    {
        /// <summary>
        /// 评估单轴移动是否允许
        /// </summary>
        public static (bool allowed, string reasonKey, object[] reasonArgs, string ruleId) EvaluateMove(
            SafetyZoneConfig config,
            string movingAxisName,
            Func<string, double?> getPositionByName,
            ILocalizationService localization)
        {
            if (config == null || !config.Enabled)
                return (true, null, null, null);

            if (string.IsNullOrEmpty(movingAxisName))
                return DenyMissingAxis(config, localization);

            foreach (var rule in config.Rules.Where(r => r.Enabled))
            {
                var deny = EvaluateRule(rule, movingAxisName, getPositionByName, config.FailClosedOnMissingAxis);
                if (deny != null)
                {
                    var reason = FormatReason(localization, deny.Value.messageKey, deny.Value.args);
                    return (false, deny.Value.messageKey, deny.Value.args, rule.Id);
                }
            }

            return (true, null, null, null);
        }

        /// <summary>
        /// 判断轴当前位置是否在配置的危险区内
        /// </summary>
        public static bool IsInDangerZone(SafetyZoneConfig config, string axisName, double position)
        {
            if (config?.DangerZones == null)
                return false;

            var zone = config.DangerZones.FirstOrDefault(z =>
                string.Equals(z.AxisName, axisName, StringComparison.Ordinal));
            if (zone == null)
                return false;

            return position < zone.Min || position > zone.Max;
        }

        /// <summary>
        /// 获取当前未达安全高度的高度轴名称列表
        /// </summary>
        public static List<string> GetLowHeightAxisNames(
            SafetyZoneConfig config,
            Func<string, double?> getPositionByName)
        {
            var result = new List<string>();
            if (config == null || !config.Enabled)
                return result;

            foreach (var rule in config.Rules.Where(r => r.Enabled && r.Type == SafetyInterlockRuleType.HeightLockPlane))
            {
                foreach (var ha in rule.HeightAxes)
                {
                    if (string.IsNullOrWhiteSpace(ha.AxisName))
                        continue;

                    var pos = getPositionByName(ha.AxisName);
                    bool isLow = pos == null
                        ? config.FailClosedOnMissingAxis
                        : pos.Value < ha.SafeHeight;

                    if (isLow && !result.Any(x => string.Equals(x, ha.AxisName, StringComparison.Ordinal)))
                        result.Add(ha.AxisName);
                }
            }

            return result;
        }

        /// <summary>
        /// 获取当前激活中的规则 Id 列表
        /// </summary>
        public static List<string> GetActiveRuleIds(
            SafetyZoneConfig config,
            Func<string, double?> getPositionByName)
        {
            var active = new List<string>();
            if (config == null || !config.Enabled)
                return active;

            foreach (var rule in config.Rules.Where(r => r.Enabled))
            {
                if (IsRuleActive(rule, getPositionByName, config.FailClosedOnMissingAxis))
                    active.Add(rule.Id);
            }

            return active;
        }

        /// <summary>
        /// 平面轴是否因高度互锁而被禁止移动
        /// </summary>
        public static bool IsPlaneMovementLocked(
            SafetyZoneConfig config,
            Func<string, double?> getPositionByName)
        {
            return GetLowHeightAxisNames(config, getPositionByName).Count > 0;
        }

        private static (bool allowed, string reasonKey, object[] reasonArgs, string ruleId) DenyMissingAxis(
            SafetyZoneConfig config,
            ILocalizationService localization)
        {
            if (!config.FailClosedOnMissingAxis)
                return (true, null, null, null);

            const string key = "SafetyRule_MissingAxis";
            return (false, key, Array.Empty<object>(), "MissingAxis");
        }

        private static (string messageKey, object[] args)? EvaluateRule(
            SafetyInterlockRuleConfig rule,
            string movingAxisName,
            Func<string, double?> getPositionByName,
            bool failClosed)
        {
            return rule.Type switch
            {
                SafetyInterlockRuleType.HeightLockPlane => EvaluateHeightLockPlane(
                    rule, movingAxisName, getPositionByName, failClosed),
                _ => null
            };
        }

        /// <summary>
        /// 高度锁平面：任一高度轴未达安全高度时，禁止 LockedAxes 中轴移动
        /// </summary>
        private static (string messageKey, object[] args)? EvaluateHeightLockPlane(
            SafetyInterlockRuleConfig rule,
            string movingAxisName,
            Func<string, double?> getPositionByName,
            bool failClosed)
        {
            if (rule.LockedAxes == null || !rule.LockedAxes.Any(a =>
                    string.Equals(a, movingAxisName, StringComparison.Ordinal)))
                return null;

            if (!IsRuleActive(rule, getPositionByName, failClosed))
                return null;

            var lowAxes = new List<string>();
            foreach (var ha in rule.HeightAxes)
            {
                if (string.IsNullOrWhiteSpace(ha.AxisName))
                    continue;

                var pos = getPositionByName(ha.AxisName);
                bool isLow = pos == null ? failClosed : pos.Value < ha.SafeHeight;
                if (isLow)
                    lowAxes.Add(ha.AxisName);
            }

            return (rule.MessageKey, new object[] { movingAxisName, string.Join(", ", lowAxes) });
        }

        private static bool IsRuleActive(
            SafetyInterlockRuleConfig rule,
            Func<string, double?> getPositionByName,
            bool failClosed)
        {
            return rule.Type switch
            {
                SafetyInterlockRuleType.HeightLockPlane => rule.HeightAxes.Any(ha =>
                {
                    if (string.IsNullOrWhiteSpace(ha.AxisName))
                        return false;
                    var pos = getPositionByName(ha.AxisName);
                    return pos == null ? failClosed : pos.Value < ha.SafeHeight;
                }),
                _ => false
            };
        }

        public static string FormatReason(ILocalizationService localization, string key, object[] args)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            if (localization != null)
            {
                if (args != null && args.Length > 0)
                    return localization.GetResource(key, args);
                return localization.GetResourceOrDefault(key, key);
            }

            return args != null && args.Length > 0 ? string.Format(key, args) : key;
        }
    }
}
