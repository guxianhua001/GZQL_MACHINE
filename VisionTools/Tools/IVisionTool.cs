namespace VisionTools.Tools
{
    /// <summary>
    /// 视觉工具统一接口——每个工具对应一条命令行子命令。
    /// 未来从VM 02Plugins迁移视觉工具时，按此接口逐个实现并在Program中注册即可，
    /// 主控程序通过 "VisionTools.exe &lt;命令名&gt; [参数...]" 调用，无需改动调度框架。
    /// </summary>
    public interface IVisionTool
    {
        /// <summary>命令名（小写短横线风格，如 zmap-read）</summary>
        string Name { get; }

        /// <summary>命令用法说明，参数错误时输出到标准错误流</summary>
        string Usage { get; }

        /// <summary>
        /// 执行工具逻辑。
        /// </summary>
        /// <param name="args">去掉命令名后的剩余参数</param>
        /// <returns>进程退出码：0成功，非0失败（错误详情写入标准错误流）</returns>
        int Execute(string[] args);
    }
}
