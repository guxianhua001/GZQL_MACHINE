using MotionControl.Models;

namespace MotionControl.Interfaces
{
    public interface ISafetyZoneMonitor
    {
        /// <summary>Jog 安全检查使用的估算位移（mm），来自配置</summary>
        double JogEstimateOffset { get; }

        /// <summary>当前互锁总开关是否启用（读取监控器内最新配置快照）</summary>
        bool IsInterlockEnabled { get; }

        /// <summary>
        /// 检查单轴移动是否被安全策略允许
        /// 返回元组：(是否允许, 拒绝原因)
        /// </summary>
        (bool allowed, string reason) CheckMoveAllowed(int axisId, double targetPosition);

        /// <summary>
        /// 检查多轴插补移动是否被安全策略允许
        /// 需同时验证所有目标轴的位置约束，任一轴违规即拒绝整个插补指令
        /// </summary>
        (bool allowed, string reason) CheckInterpolationMoveAllowed(int[] axisIds, double[] targetPositions);

        /// <summary>
        /// 判断指定轴当前位置是否处于危险区域内
        /// 用于实时监控和UI告警显示
        /// </summary>
        bool IsInDangerZone(int axisId);

        /// <summary>
        /// 获取当前完整的安全状态快照
        /// 包含各轴位置、危险区标志、活跃规则列表等信息
        /// </summary>
        SafetyStatus GetSafetyStatus();

        /// <summary>
        /// 动态更新安全区域配置参数
        /// 支持运行时调整阈值而不重启系统
        /// </summary>
        void UpdateConfig(SafetyZoneConfig config);
    }
}
