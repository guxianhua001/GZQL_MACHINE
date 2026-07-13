using System;
using System.Collections.Generic;
using VisionTools.Runtime;
using VisionTools.Tools;
using VisionTools.Tools.ZMap;

namespace VisionTools
{
    /// <summary>
    /// 视觉工具命令行入口——按子命令名分发到对应的视觉工具。
    /// 用法：VisionTools.exe &lt;命令名&gt; [参数...]
    /// 当前命令：
    ///   zmap-read &lt;input.tif&gt; &lt;output.bin&gt; &lt;preview.png&gt;
    /// 未来从VM 02Plugins迁移的视觉工具在 <see cref="CreateTools"/> 中追加注册即可。
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            // 必须先解析HALCON原生DLL搜索路径，再执行任何会触碰HALCON API的代码，
            // 否则halcondotnet加载原生halcon.dll时会抛DllNotFoundException
            HalconEnvironment.EnsureNativeDllResolvable();

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
            Console.Error.WriteLine("Usage: VisionTools.exe <command> [args...]");
            Console.Error.WriteLine("Commands:");
            foreach (var tool in tools.Values)
                Console.Error.WriteLine("  " + tool.Usage);
        }
    }
}
