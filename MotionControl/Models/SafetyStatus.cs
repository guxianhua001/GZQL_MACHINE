using System.Collections.Generic;

namespace MotionControl.Models
{
    public class SafetyStatus
    {
        /// <summary>
        /// 各轴当前实时位置映射（键：轴名称，值：物理位置/mm）
        /// </summary>
        public Dictionary<string, double> CurrentPositions { get; set; } = new();

        /// <summary>
        /// 各轴危险区状态标志（键：轴名称，值：是否处于危险区内）
        /// </summary>
        public Dictionary<string, bool> DangerZoneFlags { get; set; } = new();

        /// <summary>
        /// 当前处于激活状态的互锁规则名称列表
        /// 用于向操作员展示哪些安全规则正在生效
        /// </summary>
        public List<string> ActiveRules { get; set; } = new();

        /// <summary>
        /// Z1轴是否低于安全高度阈值
        /// 核心安全指标，直接影响设备运行权限判断
        /// </summary>
        public bool IsZ1BelowSafeHeight { get; set; }
    }
}
