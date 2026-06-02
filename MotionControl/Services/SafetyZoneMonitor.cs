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

        private SafetyZoneConfig _config = SafetyZoneConfig.CreateDefaultForCurrentMachine();

        private IMotionService Motion => _motionLazy.Value;

        public SafetyZoneMonitor(
            Lazy<IMotionService> motionService,
            ILoggerService logger,
            IEventAggregator eventAggregator,
            ILocalizationService localization = null)
        {
            _motionLazy = motionService ?? throw new ArgumentNullException(nameof(motionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
            _localization = localization;
        }

        /// <inheritdoc/>
        public double JogEstimateOffset => _config?.JogEstimateOffset > 0 ? _config.JogEstimateOffset : 10.0;

        /// <summary>
        /// 检查单轴移动是否被安全策略允许（配置驱动规则求值）
        /// </summary>
        public (bool allowed, string reason) CheckMoveAllowed(int axisId, double targetPosition)
        {
            if (!_config.Enabled)
                return (true, null);

            var axisName = TryGetAxisName(axisId);
            var getPos = BuildPositionResolver();

            var (allowed, reasonKey, reasonArgs, ruleId) = SafetyInterlockEvaluator.EvaluateMove(
                _config, axisName, getPos, _localization);

            if (!allowed)
            {
                string reason = SafetyInterlockEvaluator.FormatReason(_localization, reasonKey, reasonArgs);
                double current = axisName != null
                    ? Motion.GetAxisPosition(axisId)
                    : 0;
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

            double position = Motion.GetAxisPosition(axisId);
            return SafetyInterlockEvaluator.IsInDangerZone(_config, axisName, position);
        }

        /// <inheritdoc/>
        public SafetyStatus GetSafetyStatus()
        {
            var status = new SafetyStatus();
            var getPos = BuildPositionResolver();

            foreach (var axis in Motion.GetAxisConfigurations())
            {
                double pos = Motion.GetAxisPosition(axis.LogicalId);
                status.CurrentPositions[axis.Name] = pos;
                status.DangerZoneFlags[axis.Name] = SafetyInterlockEvaluator.IsInDangerZone(_config, axis.Name, pos);
            }

            status.LowHeightAxisNames = SafetyInterlockEvaluator.GetLowHeightAxisNames(_config, getPos);
            status.IsPlaneMovementLocked = SafetyInterlockEvaluator.IsPlaneMovementLocked(_config, getPos);
            status.IsZ1BelowSafeHeight = status.LowHeightAxisNames.Contains("Dz₁");
            status.ActiveRules = SafetyInterlockEvaluator.GetActiveRuleIds(_config, getPos);

            return status;
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

            _config = config;
            _logger.Info("[安全互锁] 配置已更新");
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
                    if (_config.FailClosedOnMissingAxis)
                        _logger.Warn($"[安全互锁] 配置引用的轴 '{axisName}' 未在硬件配置中找到");
                    return null;
                }

                return Motion.GetAxisPosition(match.LogicalId);
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
