using System.Collections.Generic;

namespace Core.Abstraction
{
    /// <summary>
    /// 受保护文件路径提供者接口。
    /// 提供不应被配置文件清理机制删除的文件路径列表
    /// （如被配方池 ExtensionData 引用的配置文件）。
    /// </summary>
    /// <remarks>
    /// 设计原则：Core 层定义接口（依赖倒置），上层（如 Recipe）实现具体扫描逻辑，
    /// 避免底层反向依赖上层模块。ConfigFileRetentionService 注入此接口（可选），
    /// 清理时跳过受保护文件，防止删除配方池引用的配置文件导致切换池后数据丢失。
    /// </remarks>
    public interface IProtectedFileProvider
    {
        /// <summary>
        /// 获取所有受保护的配置文件完整路径集合。
        /// 路径比较不区分大小写（使用 OrdinalIgnoreCase）。
        /// </summary>
        /// <returns>受保护文件路径集合；无受保护文件时返回空集合</returns>
        HashSet<string> GetProtectedFilePaths();
    }
}
