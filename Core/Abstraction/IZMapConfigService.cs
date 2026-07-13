using Core.Models;

namespace Core.Abstraction
{
    /// <summary>
    /// ZMAP标定配置持久化服务——负责将"像素↔机械"标定点、仿射矩阵、ZOffset基准
    /// 读写为JSON文件，纯逻辑实现（不依赖Halcon），风格对齐 IZScanConfigService。
    /// </summary>
    public interface IZMapConfigService
    {
        /// <summary>从默认（或指定）配置文件加载标定配置；不存在时返回空配置（不抛异常）</summary>
        ZMapCalibrationConfig Load(string fileName = "ZMapCalibration.json");

        /// <summary>保存标定配置到默认（或指定）配置文件，覆盖写入</summary>
        void Save(ZMapCalibrationConfig config, string fileName = "ZMapCalibration.json");

        /// <summary>配置文件所在目录（Config/ZMap）</summary>
        string GetConfigPath();
    }
}
