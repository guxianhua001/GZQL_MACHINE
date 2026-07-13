using System;
using System.Text;

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
            // 子进程stdout/stderr统一UTF-8输出，主控程序按UTF-8读取，
            // 避免中文错误信息因控制台默认GBK编码而乱码
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
            }
            catch
            {
                // 无控制台句柄等场景下设置失败可忽略，不影响工具执行
            }

            return ToolRunner.Run(args);
        }
    }
}
