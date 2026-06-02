using MotionControl.Models;

namespace MotionControl.Interfaces
{
    /// <summary>
    /// 安全区域互锁配置加载与持久化（运控层启动时加载，与维护界面共用同一 JSON）
    /// </summary>
    public interface ISafetyZoneConfigLoader
    {
        /// <summary>配置文件完整路径</summary>
        string ConfigFilePath { get; }

        /// <summary>从磁盘加载配置，不存在或解析失败时返回默认配置</summary>
        SafetyZoneConfig Load();

        /// <summary>保存配置到磁盘</summary>
        void Save(SafetyZoneConfig config);

        /// <summary>创建当前机型默认配置（含 Dz₁/Dz₂/Dz₃ 高度锁 Dx/Dy 规则）</summary>
        SafetyZoneConfig CreateDefault();
    }
}
