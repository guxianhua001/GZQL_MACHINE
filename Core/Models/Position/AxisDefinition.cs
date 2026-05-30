

namespace Core.Models
{
    // Core/Models/AxisDefinition.cs
    public class AxisDefinition
    {
        public string Name { get; set; }      // 轴标识，如 "X", "Y", "Rz"
        public string DisplayName { get; set; } // 显示名称，如 "X (mm)"
        public string Unit { get; set; }       // 单位，如 "mm", "°"
        public double DefaultValue { get; set; }
        public bool IsRequired { get; set; }
    }
}
