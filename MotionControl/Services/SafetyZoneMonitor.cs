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

        /// <summary>记录已报警的轴，避免重复触发</summary>
        private readonly HashSet<string> _alarmedAxes = new();

        private IMotionService Motion => _motionLazy.Value;

        /// <summary>当前是否启用互锁（线程安全读取快照）</summary>
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

            var (allowed, reasonKey, reasonArgs, ruleId) = SafetyInterlockEvaluator.EvaluateMove(
                configSnapshot, axisName, getPos, _localization);

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

            for (int i = 0; i < axisIds.Length; i++)
            {
                var (allowed, reason) = CheckMoveAllowed(axisIds[i], targetPositions[i]);
                if (!allowed)
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

            foreach (var axis in Motion.GetAxisConfigurations())
            {
                // 使用缓存位置，避免硬件读卡阻塞 UI 线程
                var state = Motion.GetAxisState(axis.LogicalId);
                double pos = state?.ActualPosition ?? 0;
                status.CurrentPositions[axis.Name] = pos;
                status.DangerZoneFlags[axis.Name] = SafetyInterlockEvaluator.IsInDangerZone(configSnapshot, axis.Name, pos);
            }

            status.LowHeightAxisNames = SafetyInterlockEvaluator.GetLowHeightAxisNames(configSnapshot, getPos);
            status.IsPlaneMovementLocked = SafetyInterlockEvaluator.IsPlaneMovementLocked(configSnapshot, getPos);
            status.IsZ1BelowSafeHeight = status.LowHeightAxisNames.Contains("Dz₁");
            status.ActiveRules = SafetyInterlockEvaluator.GetActiveRuleIds(configSnapshot, getPos);

            // 检测危险区状态并触发报警
            CheckDangerZoneAlarms(status);

            return status;
        }

        /// <summary>
        /// 检测平面轴是否进入危险区，触发/消除报警
        /// 进入危险区：触发 General 级报警（左下角 Toast 弹窗）
        /// 离开危险区：消除对应报警
        /// </summary>
        private void CheckDangerZoneAlarms(SafetyStatus status)
        {
            if (_alarmService == null) return;

            // 检查平面轴（Dx/Dy）是否在危险区内
            var dangerAxes = new List<string>();
            foreach (var kvp in status.DangerZoneFlags)
            {
                if (kvp.Value && !_alarmedAxes.Contains(kvp.Key))
                {
                    dangerAxes.Add(kvp.Key);
                    _alarmedAxes.Add(kvp.Key);
                }
                else if (!kvp.Value && _alarmedAxes.Contains(kvp.Key))
                {
                    // 离开危险区，从已报警集合移除
                    _alarmedAxes.Remove(kvp.Key);
                }
            }

            // 触发报警
            foreach (var axisName in dangerAxes)
            {
                status.CurrentPositions.TryGetValue(axisName, out var pos);
                _ = _alarmService.TriggerAlarmAsync(
                    $"SAFETY_DANGER_ZONE_{axisName}",
                    AlarmModule.Models.AlarmLevel.General,
                    $"轴 {axisName} 进入危险区域 (位置: {pos:F1}mm)，平面移动已被安全互锁锁定",
                    source: axisName,
                    type: AlarmModule.Models.AlarmType.ParameterOutOfLimit,
                    triggerValue: pos);
            }
        }

        /// <inheritdoc/>
        public void UpdateConfig(SafetyZoneConfig config)
        {
            if (config == null)
            {
                _logger.Warn("[安全互锁] 收到空配置，忽略更新");
                return;
            }

            if (config.Rules == null || config.Rules.Count == 0)
                SafetyZoneConfigLoader.EnsureMigrated(config);

            var snapshot = config.Clone();
            lock (_configLock)
                _config = snapshot;

            _logger.Info($"[安全互锁] 配置已热更新 | Enabled={snapshot.Enabled} | 规则={snapshot.Rules?.Count ?? 0} | FailClosed={snapshot.FailClosedOnMissingAxis}");
        }

        #region 私有辅助

        private string TryGetAxisName(int axisId)
        {
            var match = Motion.GetAxisConfigurations()
                .FirstOrDefault(a => a.LogicalId == axisId);
            return match?.Name;
        }

        /// <summary>
        /// 构建轴名→位置的解析器；缺失轴在 FailClosed 时返回 null 触发互锁
        /// 关键：使用轮询线程推送的缓存位置（AxisState.ActualPosition），
        /// 绝不在 UI 线程调用 GetAxisPosition（硬件读卡），避免与轮询线程争锁导致 Jog 卡顿。
        /// 缓存不可用时返回 null，FailClosed 模式下将触发互锁拒绝（安全优先）。
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
                            _logger.Warn($"[安全互锁] 配置引用的轴 '{axisName}' 未在硬件配置中找到");
                    }
                    return null;
                }

                // 使用轮询缓存位置，避免 UI 线程同步读卡
                var state = Motion.GetAxisState(match.LogicalId);
                return state?.ActualPosition;
            };
        }

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
            _logger.Warn($"[安全互锁] 违规 | 规则:{ruleName} | 轴:{axisName}(#{axisId}) | {reason}");
        }

        #endregion
    }
}
