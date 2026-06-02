using Core.Utilities;
using MotionControl.Events;
using MotionControl.Interfaces;
using MotionControl.Models;
using Prism.Events;
using System.Collections.Generic;
using System.Linq;

namespace MotionControl.Services
{
    public class SafetyZoneMonitor : ISafetyZoneMonitor
    {
        private readonly IMotionService _motionService;
        private readonly ILoggerService _logger;
        private readonly IEventAggregator _eventAggregator;

        private SafetyZoneConfig _config;

        private const string Rule_Z1_X_Negative_Lock = "Z1_X_Negative_Lock";
        private const string Rule_Z1_X_Danger_Y_Lock = "Z1_X_Danger_Y_Lock";

        public SafetyZoneMonitor(
            IMotionService motionService,
            ILoggerService logger,
            IEventAggregator eventAggregator)
        {
            _motionService = motionService ?? throw new System.ArgumentNullException(nameof(motionService));
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
            _eventAggregator = eventAggregator ?? throw new System.ArgumentNullException(nameof(eventAggregator));
            _config = new SafetyZoneConfig();
        }

        /// <summary>
        /// 检查单轴移动是否被安全策略允许
        /// 依次执行Z₁高度互锁规则，任一规则触发即拒绝移动并发布违规事件
        /// </summary>
        public (bool allowed, string reason) CheckMoveAllowed(int axisId, double targetPosition)
        {
            if (!_config.Enabled)
                return (true, null);

            var axisName = TryGetAxisName(axisId);
            if (axisName == null)
            {
                _logger.Warn($"[安全互锁] 无法查找轴号 {axisId} 的名称，跳过安全检查，允许移动");
                return (true, null);
            }

            double z1CurrentPosition = GetZ1CurrentPosition();

            // 规则：Z₁低于安全高度时，禁止X轴向负方向进入危险区域
            if (z1CurrentPosition < _config.SafeHeightZ1 && axisName == "Dx" && targetPosition < _config.DangerZoneXMin)
            {
                string reason = "Z₁低于安全高度，禁止X轴向负方向进入危险区域";
                PublishViolation(axisId, axisName, targetPosition, _motionService.GetAxisPosition(axisId), reason, Rule_Z1_X_Negative_Lock);
                return (false, reason);
            }

            // 规则：Z₁低于安全高度且X已在危险区域时，禁止Y轴移动
            if (z1CurrentPosition < _config.SafeHeightZ1 && axisName == "Dy")
            {
                double dxCurrent = GetAxisCurrentPositionByName("Dx");
                if (dxCurrent < _config.DangerZoneXMin)
                {
                    string reason = "Z₁低于安全高度且X已在危险区域，禁止Y轴移动";
                    PublishViolation(axisId, axisName, targetPosition, _motionService.GetAxisPosition(axisId), reason, Rule_Z1_X_Danger_Y_Lock);
                    return (false, reason);
                }
            }

            return (true, null);
        }

        /// <summary>
        /// 检查多轴插补移动是否被安全策略允许
        /// 遍历每对轴号/目标位置，任一轴不通过则整体拒绝（快速失败）
        /// </summary>
        public (bool allowed, string reason) CheckInterpolationMoveAllowed(int[] axisIds, double[] targetPositions)
        {
            if (axisIds == null || targetPositions == null || axisIds.Length != targetPositions.Length)
                return (false, "插补参数无效：轴号数组与目标位置数组长度不一致");

            for (int i = 0; i < axisIds.Length; i++)
            {
                var (allowed, reason) = CheckMoveAllowed(axisIds[i], targetPositions[i]);
                if (!allowed)
                    return (false, reason);
            }

            return (true, null);
        }

        /// <summary>
        /// 判断指定轴当前位置是否处于该轴的危险区域内
        /// 根据轴名称匹配对应的危险区边界进行判定
        /// </summary>
        public bool IsInDangerZone(int axisId)
        {
            var axisName = TryGetAxisName(axisId);
            if (axisName == null)
                return false;

            double position = _motionService.GetAxisPosition(axisId);

            return axisName switch
            {
                "Dx" => position < _config.DangerZoneXMin || position > _config.DangerZoneXMax,
                "Dy" => position < _config.DangerZoneYMin || position > _config.DangerZoneYMax,
                _ => false
            };
        }

        /// <summary>
        /// 获取当前完整的安全状态快照
        /// 包含各轴实时位置、危险区标志、Z₁高度状态、活跃互锁规则列表
        /// 用于UI监控面板显示和运行状态诊断
        /// </summary>
        public SafetyStatus GetSafetyStatus()
        {
            var status = new SafetyStatus();
            var axisConfigs = _motionService.GetAxisConfigurations();
            double z1Position = GetZ1CurrentPosition();

            foreach (var axis in axisConfigs)
            {
                double pos = _motionService.GetAxisPosition(axis.LogicalId);
                status.CurrentPositions[axis.Name] = pos;
                status.DangerZoneFlags[axis.Name] = IsInDangerZone(axis.LogicalId);
            }

            status.IsZ1BelowSafeHeight = z1Position < _config.SafeHeightZ1;
            status.ActiveRules = EvaluateActiveRules(z1Position);

            return status;
        }

        /// <summary>
        /// 动态更新安全区域配置参数
        /// 支持运行时调整阈值而不重启系统（如从设置界面修改后热更新）
        /// </summary>
        public void UpdateConfig(SafetyZoneConfig config)
        {
            if (config == null)
            {
                _logger.Warn("[安全互锁] 收到空配置，忽略更新");
                return;
            }
            _config = config;
            _logger.Info("[安全互锁] 配置已更新");
        }

        #region 私有辅助方法

        /// <summary>
        /// 通过轴号查找轴名称，查找失败返回null（调用方负责降级处理）
        /// </summary>
        private string TryGetAxisName(int axisId)
        {
            var axisConfigs = _motionService.GetAxisConfigurations();
            var match = axisConfigs.FirstOrDefault(a => a.LogicalId == axisId);
            return match?.Name;
        }

        /// <summary>
        /// 获取Z₁轴（Dz₁/Dz3）的当前位置
        /// 优先匹配"Dz₁"，其次尝试"Dz3"，均未找到则返回double.MaxValue（视为安全高度以上）
        /// </summary>
        private double GetZ1CurrentPosition()
        {
            var axisConfigs = _motionService.GetAxisConfigurations();

            var z1Config = axisConfigs.FirstOrDefault(a => a.Name == "Dz₁")
                        ?? axisConfigs.FirstOrDefault(a => a.Name == "Dz3");

            if (z1Config == null)
            {
                _logger.Warn("[安全互锁] 未找到Z₁轴（Dz₁/Dz3）配置，默认按安全高度处理");
                return double.MaxValue;
            }

            return _motionService.GetAxisPosition(z1Config.LogicalId);
        }

        /// <summary>
        /// 通过轴名称获取该轴的当前位置
        /// 查找失败时返回double.MaxValue（避免误触发危险区判断）
        /// </summary>
        private double GetAxisCurrentPositionByName(string name)
        {
            var axisConfigs = _motionService.GetAxisConfigurations();
            var match = axisConfigs.FirstOrDefault(a => a.Name == name);
            if (match == null)
            {
                _logger.Warn($"[安全互锁] 未找到名为 {name} 的轴配置");
                return double.MaxValue;
            }
            return _motionService.GetAxisPosition(match.LogicalId);
        }

        /// <summary>
        /// 发布安全违规事件到EventAggregator
        /// 供UI层、报警服务、日志记录等订阅者响应
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
            _logger.Warn($"[安全互锁] 违规触发 | 规则:{ruleName} | 轴:{axisName}(#{axisId}) | 原因:{reason}");
        }

        /// <summary>
        /// 根据当前各轴实际位置评估哪些互锁规则处于激活状态
        /// 用于GetSafetyStatus构建活跃规则列表，帮助操作员了解当前受限原因
        /// </summary>
        private List<string> EvaluateActiveRules(double z1Position)
        {
            var activeRules = new List<string>();

            bool z1Low = z1Position < _config.SafeHeightZ1;
            if (!z1Low)
                return activeRules;

            double dxPos = GetAxisCurrentPositionByName("Dx");

            if (dxPos < _config.DangerZoneXMin)
                activeRules.Add(Rule_Z1_X_Negative_Lock);

            if (dxPos < _config.DangerZoneXMin)
                activeRules.Add(Rule_Z1_X_Danger_Y_Lock);

            return activeRules;
        }

        #endregion
    }
}
