namespace VisionTools.Host
{
    /// <summary>
    /// 视觉工具进程入口（瘦启动壳）——不含任何业务逻辑，
    /// 直接把命令行参数转发给VisionTools类库的调度器执行。
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            return ToolRunner.Run(args);
        }
    }
}
