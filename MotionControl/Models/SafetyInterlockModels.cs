using System.Collections.Generic;

namespace MotionControl.Models
{
    /// <summary>
    /// 安全互锁规则类型（配置驱动，不同设备可选用不同规则组合）
    /// </summary>
    public enum SafetyInterlockRuleType
    {
        /// <summary>
        /// 当任一高度轴低于安全高度时，禁止指定平面轴（Dx/Dy 等）移动
        /// </summary>
        HeightLockPlane = 0
    }

    /// <summary>
    /// 单条高度轴安全高度配置
    /// </summary>
    public class HeightAxisSafeConfig
    {
        /// <summary>轴名称，与 hwcfg 中 AxisConfig.Name 一致，如 Dz₁、Dz₂、Dz₃</summary>
        public string AxisName { get; set; } = string.Empty;

        /// <summary>安全高度阈值（mm），当前位置低于此值视为未在安全区域</summary>
        public double SafeHeight { get; set; } = 50.0;

        /// <summary>
        /// Z轴方向模式：
        /// false（默认）= Z越往上值越大，安全=高位=大值，判断 pos &lt; SafeHeight 为不安全
        /// true = Z越往下值越大，安全=高位=小值，判断 pos &gt; SafeHeight 为不安全
        /// </summary>
        public bool InvertedDirection { get; set; } = false;
    }

    /// <summary>
    /// 单轴危险区边界（用于状态显示与可视化，可选）
    /// </summary>
    public class AxisDangerZoneConfig
    {
        public string AxisName { get; set; } = string.Empty;
        public double Min { get; set; }
        public double Max { get; set; } = 200.0;
    }

    /// <summary>
    /// 可配置的安全互锁规则
    /// </summary>
    public class SafetyInterlockRuleConfig
    {
        /// <summary>规则唯一标识，用于日志与事件</summary>
        public string Id { get; set; } = string.Empty;

        public SafetyInterlockRuleType Type { get; set; } = SafetyInterlockRuleType.HeightLockPlane;

        public bool Enabled { get; set; } = true;

        /// <summary>多语言资源键，用于违规原因描述</summary>
        public string MessageKey { get; set; } = "SafetyRule_HeightLockPlane";

        /// <summary>HeightLockPlane：参与判定的垂直轴及各自安全高度</summary>
        public List<HeightAxisSafeConfig> HeightAxes { get; set; } = new();

        /// <summary>HeightLockPlane：互锁激活时禁止移动的平面轴名称列表</summary>
        public List<string> LockedAxes { get; set; } = new();
    }
}
