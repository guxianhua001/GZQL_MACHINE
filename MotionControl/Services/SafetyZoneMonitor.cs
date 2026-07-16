using Core.Abstraction;
using Core.Utilities;
using MotionControl.Events;
using MotionControl.Interfaces;
using MotionControl.Models;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MotionControl.Services
{
    /// <summary>
    /// 安全区域监控：按 JSON 配置的规则集求值，无硬编码轴名与规则逻辑
    /// </summary>
    public class SafetyZoneMonitor : ISafetyZoneMonitor
    {
        /// <summary>
        /// 延迟解析 IMotionService，打破与 MotionService 的构造期循环依赖
        /// </summary>
        private readonly Lazy<IMotionService> _motionLazy;
        private readonly ILoggerService _logger;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILocalizationService _localization;
        private readonly AlarmModule.Interfaces.IAlarmService _alarmService;

        private SafetyZoneConfig _config = SafetyZoneConfig.CreateDefaultForCurrentMachine();
        private readonly object _configLock = new();

        private IMotionService Motion => _motionLazy.Value;

        /// <summary>当前互锁总开关是否启用（线程安全读取快照）</summary>
        public bool IsInterlockEnabled
        {
            get
            {
                lock (_configLock)
                    return _config?.Enabled ?? false;
            }
        }

        public SafetyZoneMonitor(
            Lazy<IMotionService> motionService,
            ILoggerService logger,
            IEventAggregator eventAggregator,
            ILocalizationService localization = null,
            AlarmModule.Interfaces.IAlarmService alarmService = null)
        {
            _motionLazy = motionService ?? throw new ArgumentNullException(nameof(motionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
            _localization = localization;
            _alarmService = alarmService;
        }

        /// <inheritdoc/>
        public double JogEstimateOffset
        {
            get
            {
                lock (_configLock)
                    return _config?.JogEstimateOffset > 0 ? _config.JogEstimateOffset : 10.0;
            }
        }

        /// <summary>
        /// 检查单轴移动是否被安全策略允许（配置驱动规则求值）
        /// </summary>
        public (bool allowed, string reason) CheckMoveAllowed(int axisId, double targetPosition)
        {
            SafetyZoneConfig configSnapshot;
            lock (_configLock)
                configSnapshot = _config;

            // 每次 Jog/运动前读取最新 Enabled 与规则（不缓存互锁条件）
            if (configSnapshot == null || !configSnapshot.Enabled)
                return (true, null);

            var axisName = TryGetAxisName(axisId);
            var getPos = BuildPositionResolver();
            var isKnown = BuildAxisKnownPredicate();
            var targetPositions = axisName == null
                ? null
                : new Dictionary<string, double>(StringComparer.Ordinal) { [axisName] = targetPosition };

            var (allowed, reasonKey, reasonArgs, ruleId) = SafetyInterlockEvaluator.EvaluateMove(
                configSnapshot, axisName == null ? Array.Empty<string>() : new[] { axisName },
                getPos, targetPositions, isKnown, _localization);

            if (!allowed)
            {
                string reason = SafetyInterlockEvaluator.FormatReason(_localization, reasonKey, reasonArgs);
                // 使用缓存位置，避免 UI 线程硬件读卡
                var state = Motion.GetAxisState(axisId);
                double current = state?.ActualPosition ?? 0;
                PublishViolation(axisId, axisName ?? axisId.ToString(), targetPosition, current, reason, ruleId ?? "Unknown");
                return (false, reason);
            }

            return (true, null);
        }

        /// <inheritdoc/>
        public (bool allowed, string reason) CheckInterpolationMoveAllowed(int[] axisIds, double[] targetPositions)
        {
            if (axisIds == null || targetPositions == null || axisIds.Length != targetPositions.Length)
                return (false, SafetyInterlockEvaluator.FormatReason(_localization, "SafetyRule_InvalidInterpolation", null));

            SafetyZoneConfig configSnapshot;
            lock (_configLock)
                configSnapshot = _config;

            if (configSnapshot == null || !configSnapshot.Enabled)
                return (true, null);

            var movingAxisNames = new List<string>(axisIds.Length);
            var targetsByName = new Dictionary<string, double>(StringComparer.Ordinal);
            for (int i = 0; i < axisIds.Length; i++)
            {
                var axisName = TryGetAxisName(axisIds[i]);
                if (string.IsNullOrWhiteSpace(axisName))
                    return (false, SafetyInterlockEvaluator.FormatReason(_localization, "SafetyRule_MissingAxis", null));

                movingAxisNames.Add(axisName);
                targetsByName[axisName] = targetPositions[i];
            }

            var getPos = BuildPositionResolver();
            var isKnown = BuildAxisKnownPredicate();
            var (allowed, reasonKey, reasonArgs, ruleId) = SafetyInterlockEvaluator.EvaluateMove(
                configSnapshot, movingAxisNames, getPos, targetsByName, isKnown, _localization);
            if (!allowed)
            {
                string reason = SafetyInterlockEvaluator.FormatReason(_localization, reasonKey, reasonArgs);
                int violationIndex = Array.FindIndex(axisIds, axisId =>
                    string.Equals(TryGetAxisName(axisId), movingAxisNames.FirstOrDefault(), StringComparison.Ordinal));
                int violationAxisId = violationIndex >= 0 ? axisIds[violationIndex] : axisIds[0];
                var state = Motion.GetAxisState(violationAxisId);
                PublishViolation(violationAxisId, TryGetAxisName(violationAxisId) ?? violationAxisId.ToString(),
                    targetPositions[violationIndex >= 0 ? violationIndex : 0], state?.ActualPosition ?? 0,
                    reason, ruleId ?? "Unknown");
                return (false, reason);
            }

            return (true, null);
        }

        /// <inheritdoc/>
        public bool IsInDangerZone(int axisId)
        {
            var axisName = TryGetAxisName(axisId);
            if (axisName == null)
                return false;

            // 使用缓存位置，避免硬件读卡阻塞 UI 线程
            var state = Motion.GetAxisState(axisId);
            if (state == null)
                return false;

            lock (_configLock)
                return SafetyInterlockEvaluator.IsInDangerZone(_config, axisName, state.ActualPosition);
        }

        /// <inheritdoc/>
        public SafetyStatus GetSafetyStatus()
        {
            var status = new SafetyStatus();
            SafetyZoneConfig configSnapshot;
            lock (_configLock)
                configSnapshot = _config;

            var getPos = BuildPositionResolver();
            var isKnown = BuildAxisKnownPredicate();

            foreach (var axis in Motion.GetAxisConfigurations())
            {
                // 使用缓存位置，避免硬件读卡阻塞 UI 线程
                var state = Motion.GetAxisState(axis.LogicalId);
                double pos = state?.ActualPosition ?? 0;
                status.CurrentPositions[axis.Name] = pos;
                status.DangerZoneFlags[axis.Name] = SafetyInterlockEvaluator.IsInDangerZone(configSnapshot, axis.Name, pos);
            }

            status.LowHeightAxisNames = SafetyInterlockEvaluator.GetLowHeightAxisNames(configSnapshot, getPos, isKnown);
            status.IsPlaneMovementLocked = SafetyInterlockEvaluator.IsPlaneMovementLocked(configSnapshot, getPos, isKnown);
            status.IsZ1BelowSafeHeight = status.LowHeightAxisNames.Contains("Dz₁");
            status.ActiveRules = SafetyInterlockEvaluator.GetActiveRuleIds(configSnapshot, getPos, isKnown);

            // 注意：GetSafetyStatus 是只读状态查询（Setting 页 500ms 轮询），绝不能在此触发报警。
            // 报警仅在 CheckMoveAllowed 实际拒绝运动时由 PublishViolation 触发。

            return status;
        }

        /// <inheritdoc/>
        public void UpdateConfig(SafetyZoneConfig config)
        {
            if (config == null)
            {
                _logger.Warn(_localization.GetResourceOrDefault("SZM_Log_NullConfigIgnored", "[安全互锁] 收到空配置，忽略更新"));
                return;
            }

            if (config.Rules == null || config.Rules.Count == 0)
                SafetyZoneConfigLoader.EnsureMigrated(config);

            var snapshot = config.Clone();
            lock (_configLock)
                _config = snapshot;

            _logger.Info(string.Format(_localization.GetResourceOrDefault("SZM_Log_ConfigHotUpdated", "[安全互锁] 配置已热更新 | Enabled={0} | 规则={1} | FailClosed={2}"), snapshot.Enabled, snapshot.Rules?.Count ?? 0, snapshot.FailClosedOnMissingAxis));
        }

        #region 私有辅助

        private string TryGetAxisName(int axisId)
        {
            var match = Motion.GetAxisConfigurations()
                .FirstOrDefault(a => a.LogicalId == axisId);
            return match?.Name;
        }

        /// <summary>
        /// 判断轴名是否存在于当前机型硬件配置（hwcfg）
        /// </summary>
        private Func<string, bool> BuildAxisKnownPredicate()
        {
            return axisName =>
            {
                if (string.IsNullOrWhiteSpace(axisName))
                    return false;
                return Motion.GetAxisConfigurations()
                    .Any(a => string.Equals(a.Name, axisName, StringComparison.Ordinal));
            };
        }

        /// <summary>
        /// 构建轴名→位置的解析器；缺失轴在 FailClosed 时返回 null 触发互锁
        /// 注意：使用轮询线程推送的缓存位置（AxisState.ActualPosition），
        /// 绝不在 UI 线程调用 GetAxisPosition（硬件读卡），避免与轮询线程争锁导致 Jog 卡顿。
        /// 缓存不可用时返回 null，FailClosed 模式下将触发互锁拒绝（安全优先）。
        /// 硬件配置中根本不存在的轴：同样返回 null，但求值器通过 isAxisKnown 跳过，避免误锁。
        /// </summary>
        private Func<string, double?> BuildPositionResolver()
        {
            return axisName =>
            {
                if (string.IsNullOrWhiteSpace(axisName))
                    return null;

                var match = Motion.GetAxisConfigurations()
                    .FirstOrDefault(a => string.Equals(a.Name, axisName, StringComparison.Ordinal));
                if (match == null)
                {
                    lock (_configLock)
                    {
                        if (_config.FailClosedOnMissingAxis)
                            _logger.Warn(string.Format(_localization.GetResourceOrDefault("SZM_Log_AxisNotFoundInHwConfig", "[安全互锁] 配置引用的轴 '{0}' 未在硬件配置中找到（已跳过，不参与高度互锁）"), axisName));
                    }
                    return null;
                }

                // 使用轮询缓存位置，避免 UI 线程同步读卡
                var state = Motion.GetAxisState(match.LogicalId);
                return state?.ActualPosition;
            };
        }

        /// <summary>
        /// 发布安全违规事件，并触发左下角报警（仅在真实运动被拒绝时调用）
        /// </summary>
        private void PublishViolation(int axisId, string axisName, double targetPosition, double currentPosition, string reason, string ruleName)
        {
            _eventAggregator.GetEvent<SafetyViolationEvent>().Publish(new SafetyViolationEvent
            {
                AxisId = axisId,
                AxisName = axisName,
                TargetPosition = targetPosition,
                CurrentPosition = currentPosition,
                Reason = reason,
                RuleName = ruleName
            });
            _logger.Warn(string.Format(_localization.GetResourceOrDefault("SZM_Log_Violation", "[安全互锁] 违规 | 规则:{0} | 轴:{1}(#{2}) | {3}"), ruleName, axisName, axisId, reason));

            // 仅在真实拦截运动时触发报警；Setting 页轮询 GetSafetyStatus 不会走到这里
            if (_alarmService != null)
            {
                _ = _alarmService.TriggerAlarmAsync(
                    $"SAFETY_INTERLOCK_{axisName}",
                    AlarmModule.Models.AlarmLevel.General,
                    reason,
                    source: axisName,
                    type: AlarmModule.Models.AlarmType.ParameterOutOfLimit,
                    triggerValue: currentPosition);
            }
        }

        #endregion
    }
}
