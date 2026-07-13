using System;
using System.Collections.Generic;
using System.Diagnostics;
using VisionTools.Runtime;
using VisionTools.Tools;
using VisionTools.Tools.ZMap;

namespace VisionTools
{
    /// <summary>
    /// 视觉工具调度器——类库对外的唯一执行入口，按子命令名分发到对应的视觉工具。
    /// 启动壳（VisionTools.Host）只负责把命令行参数转发到 <see cref="Run"/>，
    /// 全部业务逻辑（含工具注册）都留在类库中，便于扩展与断点调试。
    /// 当前命令：
    ///   zmap-read &lt;input.tif&gt; &lt;output.bin&gt; &lt;preview.png&gt;
    /// 未来从VM 02Plugins迁移的视觉工具在 <see cref="CreateTools"/> 中追加注册即可。
    /// </summary>
    public static class ToolRunner
    {
        /// <summary>
        /// 执行入口。返回进程退出码：0成功，1工具执行失败，2参数/命令错误。
        /// </summary>
        public static int Run(string[] args)
        {
            // 必须先解析HALCON原生DLL搜索路径，再执行任何会触碰HALCON API的代码，
            // 否则halcondotnet加载原生halcon.dll时会抛DllNotFoundException
            HalconEnvironment.EnsureNativeDllResolvable();
            MaybeLaunchDebugger();

            var tools = CreateTools();
            if (args == null || args.Length == 0)
            {
                PrintUsage(tools);
                return 2;
            }

            string commandName = args[0].Trim().ToLowerInvariant();
            if (!tools.TryGetValue(commandName, out IVisionTool tool))
            {
                Console.Error.WriteLine("未知命令: " + commandName);
                PrintUsage(tools);
                return 2;
            }

            var toolArgs = new string[args.Length - 1];
            Array.Copy(args, 1, toolArgs, 0, toolArgs.Length);
            return tool.Execute(toolArgs);
        }

        /// <summary>
        /// 子进程断点调试支持：本工具由主控程序以独立进程启动，VS调试器不会自动附加，
        /// 断点默认不会命中。设置环境变量 VISIONTOOLS_DEBUG=1 后，
        /// 进程启动时会弹出"选择调试器"对话框（Debugger.Launch），
        /// 选择当前VS实例即可命中类库中的断点。
        /// </summary>
        private static void MaybeLaunchDebugger()
        {
            if (Environment.GetEnvironmentVariable("VISIONTOOLS_DEBUG") == "1" && !Debugger.IsAttached)
                Debugger.Launch();
        }

        /// <summary>注册全部可用视觉工具（命令名 → 工具实例）。</summary>
        private static Dictionary<string, IVisionTool> CreateTools()
        {
            var tools = new Dictionary<string, IVisionTool>(StringComparer.OrdinalIgnoreCase);
            Register(tools, new ZMapReadTool());
            return tools;
        }

        private static void Register(Dictionary<string, IVisionTool> tools, IVisionTool tool)
        {
            tools[tool.Name] = tool;
        }

        private static void PrintUsage(Dictionary<string, IVisionTool> tools)
        {
            Console.Error.WriteLine("Usage: VisionTools.Host.exe <command> [args...]");
            Console.Error.WriteLine("Commands:");
            foreach (var tool in tools.Values)
                Console.Error.WriteLine("  " + tool.Usage);
        }
    }
}
