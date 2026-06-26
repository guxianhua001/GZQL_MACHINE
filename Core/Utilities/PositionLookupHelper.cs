using System.Collections.Generic;

namespace Core.Utilities
{
    /// <summary>
    /// 配方位置键查找辅助：支持轴名称别名（Dz₂/Dz2 等）与「位置名.轴名」键解析。
    /// </summary>
    public static class PositionLookupHelper
    {
        /// <summary>轴名称候选列表，兼容 hwcfg 与位置编辑器中的不同写法</summary>
        public static IEnumerable<string> GetAxisNameCandidates(string axisName)
        {
            if (string.IsNullOrEmpty(axisName))
                yield break;

            yield return axisName;
            switch (axisName)
            {
                case "Dz₁": yield return "Dz1"; break;
                case "Dz1": yield return "Dz₁"; break;
                case "Dz₂": yield return "Dz2"; break;
                case "Dz2": yield return "Dz₂"; break;
                case "Dz₃": yield return "Dz3"; break;
                case "Dz3": yield return "Dz₃"; break;
            }
        }

        /// <summary>在位置字典中查找指定位置名+轴名的坐标值</summary>
        public static bool TryGetPositionValue(
            IReadOnlyDictionary<string, double> positions,
            string positionName,
            string axisName,
            out double value)
        {
            value = 0;
            if (positions == null || string.IsNullOrEmpty(positionName) || string.IsNullOrEmpty(axisName))
                return false;

            foreach (var candidate in GetAxisNameCandidates(axisName))
            {
                if (positions.TryGetValue($"{positionName}.{candidate}", out value))
                    return true;
            }

            return false;
        }

        /// <summary>位置字典中是否存在指定位置名+轴名的键</summary>
        public static bool HasPositionAxisKey(
            IReadOnlyDictionary<string, double> positions,
            string positionName,
            string axisName)
            => TryGetPositionValue(positions, positionName, axisName, out _);
    }
}
