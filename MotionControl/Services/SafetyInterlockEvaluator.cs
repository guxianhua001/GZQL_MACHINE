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
        /// <param name="isAxisKnown">轴名是否存在于当前机型硬件配置；未配置的高度轴不参与 FailClosed 误判</param>
        public static (bool allowed, string reasonKey, object[] reasonArgs, string ruleId) EvaluateMove(
            SafetyZoneConfig config,
            IReadOnlyCollection<string> movingAxisNames,
            Func<string, double?> getPositionByName,
            IReadOnlyDictionary<string, double> targetPositionsByName,
            Func<string, bool> isAxisKnown,
            ILocalizationService localization)
        {
            if (config == null || !config.Enabled)
                return (true, null, null, null);

            if (movingAxisNames == null || movingAxisNames.Count == 0 || movingAxisNames.Any(string.IsNullOrWhiteSpace))
                return DenyMissingAxis(config, localization);

            foreach (var rule in config.Rules.Where(r => r.Enabled))
            {
                var deny = EvaluateRule(rule, movingAxisNames, getPositionByName, targetPositionsByName, isAxisKnown,
                    config.FailClosedOnMissingAxis, config.DangerZones);
                if (deny != null)
                {
                    return (false, deny.Value.messageKey, deny.Value.args, rule.Id);
                }
            }

            return (true, null, null, null);
        }

        /// <summary>
        /// 判断轴当前位置是否在配置的危险区内
        /// 危险区定义：位置在 [Min, Max] 范围内为危险（可能碰撞），范围外为安全
        /// </summary>
        public static bool IsInDangerZone(SafetyZoneConfig config, string axisName, double position)
        {
            if (config?.DangerZones == null)
                return false;

            var zone = config.DangerZones.FirstOrDefault(z =>
                string.Equals(z.AxisName, axisName, StringComparison.Ordinal));
            if (zone == null)
                return false;

            return position >= zone.Min && position <= zone.Max;
        }

        /// <summary>
        /// 判断指定轴当前位置是否在危险区内（内部重载）
        /// 危险区定义：位置在 [Min, Max] 范围内为危险，范围外为安全
        /// </summary>
        private static bool IsInDangerZone(List<AxisDangerZoneConfig> dangerZones, string axisName, double position)
        {
            if (dangerZones == null)
                return false;

            var zone = dangerZones.FirstOrDefault(z =>
                string.Equals(z.AxisName, axisName, StringComparison.Ordinal));
            if (zone == null)
                // 未配置危险区的轴，默认视为在危险区内（fail-closed）
                return true;

            return position >= zone.Min && position <= zone.Max;
        }

        /// <summary>
        /// 获取当前未达安全高度的高度轴名称列表
        /// </summary>
        public static List<string> GetLowHeightAxisNames(
            SafetyZoneConfig config,
            Func<string, double?> getPositionByName,
            Func<string, bool> isAxisKnown = null)
        {
            var result = new List<string>();
            if (config == null || !config.Enabled)
                return result;

            foreach (var rule in config.Rules.Where(r => r.Enabled && r.Type == SafetyInterlockRuleType.HeightLockPlane))
            {
                foreach (var ha in rule.HeightAxes)
                {
                    if (!IsHeightAxisUnsafe(ha, getPositionByName, isAxisKnown, config.FailClosedOnMissingAxis))
                        continue;

                    if (!result.Any(x => string.Equals(x, ha.AxisName, StringComparison.Ordinal)))
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
            Func<string, double?> getPositionByName,
            Func<string, bool> isAxisKnown = null)
        {
            var active = new List<string>();
            if (config == null || !config.Enabled)
                return active;

            foreach (var rule in config.Rules.Where(r => r.Enabled))
            {
                if (IsRuleActive(rule, getPositionByName, isAxisKnown, config.FailClosedOnMissingAxis))
                    active.Add(rule.Id);
            }

            return active;
        }

        /// <summary>
        /// 平面轴是否因高度互锁而被禁止移动
        /// </summary>
        public static bool IsPlaneMovementLocked(
            SafetyZoneConfig config,
            Func<string, double?> getPositionByName,
            Func<string, bool> isAxisKnown = null)
        {
            if (config == null || !config.Enabled)
                return false;

            return config.Rules
                .Where(r => r.Enabled && r.Type == SafetyInterlockRuleType.HeightLockPlane)
                .Any(r => IsRuleActive(r, getPositionByName, isAxisKnown, config.FailClosedOnMissingAxis)
                    && DoesPathIntersectDangerZone(r, getPositionByName, null, isAxisKnown,
                        config.FailClosedOnMissingAxis, config.DangerZones));
        }

        /// <summary>
        /// 获取当前"真正被互锁锁定"的平面轴名称集合。
        /// 判定条件与 EvaluateHeightLockPlane 完全一致：规则激活（高度轴未达安全高度）
        /// 且所有参与互锁的平面轴同时处于危险矩形内。
        /// </summary>
        public static HashSet<string> GetLockedPlaneAxisNames(
            SafetyZoneConfig config,
            Func<string, double?> getPositionByName,
            Func<string, bool> isAxisKnown = null)
        {
            var locked = new HashSet<string>(StringComparer.Ordinal);
            if (config == null || !config.Enabled)
                return locked;

            foreach (var rule in config.Rules.Where(r => r.Enabled && r.Type == SafetyInterlockRuleType.HeightLockPlane))
            {
                if (!IsRuleActive(rule, getPositionByName, isAxisKnown, config.FailClosedOnMissingAxis))
                    continue;

                if (!DoesPathIntersectDangerZone(rule, getPositionByName, null, isAxisKnown,
                        config.FailClosedOnMissingAxis, config.DangerZones))
                    continue;

                foreach (var axisName in rule.LockedAxes ?? Enumerable.Empty<string>())
                    if (!string.IsNullOrWhiteSpace(axisName)
                        && (isAxisKnown == null || isAxisKnown(axisName)))
                        locked.Add(axisName);
            }

            return locked;
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
            IReadOnlyCollection<string> movingAxisNames,
            Func<string, double?> getPositionByName,
            IReadOnlyDictionary<string, double> targetPositionsByName,
            Func<string, bool> isAxisKnown,
            bool failClosed,
            List<AxisDangerZoneConfig> dangerZones)
        {
            return rule.Type switch
            {
                SafetyInterlockRuleType.HeightLockPlane => EvaluateHeightLockPlane(
                    rule, movingAxisNames, getPositionByName, targetPositionsByName, isAxisKnown, failClosed, dangerZones),
                _ => null
            };
        }

        /// <summary>
        /// 高度锁平面：Z轴未达安全高度时，若平面运动路径进入危险矩形则禁止运动。
        /// 仅 Dx、Dy 等全部锁定轴同时落在各自危险范围内才视为进入危险区域。
        /// 对单轴移动，未移动的平面轴以当前位置参与计算；对插补，按线性轨迹完整检查。
        /// </summary>
        private static (string messageKey, object[] args)? EvaluateHeightLockPlane(
            SafetyInterlockRuleConfig rule,
            IReadOnlyCollection<string> movingAxisNames,
            Func<string, double?> getPositionByName,
            IReadOnlyDictionary<string, double> targetPositionsByName,
            Func<string, bool> isAxisKnown,
            bool failClosed,
            List<AxisDangerZoneConfig> dangerZones)
        {
            // 运动指令未涉及受互锁保护的平面轴，不拦截
            if (rule.LockedAxes == null || !movingAxisNames.Any(movingAxisName =>
                    rule.LockedAxes.Any(a => string.Equals(a, movingAxisName, StringComparison.Ordinal))))
                return null;

            // 检查是否有高度轴未达安全高度
            if (!IsRuleActive(rule, getPositionByName, isAxisKnown, failClosed))
                return null;

            // 起点、目标点及两点间插补线均不进入危险矩形时允许运动
            if (!DoesPathIntersectDangerZone(rule, getPositionByName, targetPositionsByName, isAxisKnown,
                    failClosed, dangerZones))
                return null;

            // 收集未达安全高度的高度轴名称，用于提示信息
            var lowAxes = new List<string>();
            foreach (var ha in rule.HeightAxes)
            {
                if (IsHeightAxisUnsafe(ha, getPositionByName, isAxisKnown, failClosed))
                    lowAxes.Add(ha.AxisName);
            }

            return ("SafetyRule_HeightLockPlanePath",
                new object[] { string.Join("/", movingAxisNames), string.Join(", ", lowAxes) });
        }

        /// <summary>
        /// 判断平面运动线段是否穿过危险超矩形。
        /// 每个锁定轴对应一个区间；仅所有轴在同一时刻同时位于各自区间内时返回 true。
        /// 使用参数 t∈[0,1] 的线段裁剪，避免按采样点判断而漏掉高速插补穿越。
        /// </summary>
        private static bool DoesPathIntersectDangerZone(
            SafetyInterlockRuleConfig rule,
            Func<string, double?> getPositionByName,
            IReadOnlyDictionary<string, double> targetPositionsByName,
            Func<string, bool> isAxisKnown,
            bool failClosed,
            List<AxisDangerZoneConfig> dangerZones)
        {
            double enterT = 0.0;
            double exitT = 1.0;
            bool hasParticipatingAxis = false;

            foreach (var axisName in rule.LockedAxes ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(axisName)
                    || (isAxisKnown != null && !isAxisKnown(axisName)))
                    continue;

                hasParticipatingAxis = true;
                var start = getPositionByName(axisName);
                var zone = dangerZones?.FirstOrDefault(z =>
                    string.Equals(z.AxisName, axisName, StringComparison.Ordinal));
                if (start == null || zone == null)
                    return failClosed;

                double target = targetPositionsByName != null
                    && targetPositionsByName.TryGetValue(axisName, out var configuredTarget)
                    ? configuredTarget
                    : start.Value;

                if (!TryClipLineToRange(start.Value, target, zone.Min, zone.Max, ref enterT, ref exitT))
                    return false;
            }

            return hasParticipatingAxis && enterT <= exitT;
        }

        /// <summary>将一维运动线段裁剪至闭区间，并累计其位于区间内的时间参数范围。</summary>
        private static bool TryClipLineToRange(
            double start, double target, double min, double max, ref double enterT, ref double exitT)
        {
            if (min > max)
                (min, max) = (max, min);

            double delta = target - start;
            if (Math.Abs(delta) < double.Epsilon)
            {
                if (start < min || start > max)
                    return false;
                return true;
            }

            double axisEnter = (min - start) / delta;
            double axisExit = (max - start) / delta;
            if (axisEnter > axisExit)
                (axisEnter, axisExit) = (axisExit, axisEnter);

            enterT = Math.Max(enterT, axisEnter);
            exitT = Math.Min(exitT, axisExit);
            return enterT <= exitT && exitT >= 0.0 && enterT <= 1.0;
        }

        private static bool IsRuleActive(
            SafetyInterlockRuleConfig rule,
            Func<string, double?> getPositionByName,
            Func<string, bool> isAxisKnown,
            bool failClosed)
        {
            return rule.Type switch
            {
                SafetyInterlockRuleType.HeightLockPlane => rule.HeightAxes.Any(ha =>
                    IsHeightAxisUnsafe(ha, getPositionByName, isAxisKnown, failClosed)),
                _ => false
            };
        }

        /// <summary>
        /// 判断单个高度轴是否处于不安全状态（未达安全高度）。
        /// 硬件配置中不存在的轴视为未安装，不参与互锁，避免 Dz₂/Dz₃ 缺轴时 FailClosed 永久锁死平面轴。
        /// </summary>
        private static bool IsHeightAxisUnsafe(
            HeightAxisSafeConfig ha,
            Func<string, double?> getPositionByName,
            Func<string, bool> isAxisKnown,
            bool failClosed)
        {
            if (ha == null || string.IsNullOrWhiteSpace(ha.AxisName) || !ha.Enabled)
                return false;

            // 硬件中无此轴：跳过，不按 FailClosed 视为不安全
            if (isAxisKnown != null && !isAxisKnown(ha.AxisName))
                return false;

            var pos = getPositionByName(ha.AxisName);
            if (pos == null)
                return failClosed;

            return IsBelowSafeHeight(pos.Value, ha);
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

        /// <summary>
        /// 判断当前位置是否低于安全高度（未在安全区域）
        /// 常规方向（InvertedDirection=false）：Z越往上值越大，pos &lt; SafeHeight 为不安全
        /// 反转方向（InvertedDirection=true）：Z越往下值越大，pos &gt; SafeHeight 为不安全
        /// </summary>
        private static bool IsBelowSafeHeight(double position, HeightAxisSafeConfig ha)
        {
            return ha.InvertedDirection
                ? position > ha.SafeHeight   // 反转：值越大越往下，超过安全高度=不安全
                : position < ha.SafeHeight;   // 常规：值越小越往下，低于安全高度=不安全
        }
    }
}
