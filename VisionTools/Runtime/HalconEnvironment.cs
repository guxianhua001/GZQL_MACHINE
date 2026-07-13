using System;
using System.IO;

namespace VisionTools.Runtime
{
    /// <summary>
    /// HALCON运行环境初始化——负责在调用任何HALCON API之前，
    /// 把原生halcon.dll所在目录加入本进程的DLL搜索路径。
    ///
    /// 背景：halcondotnet.dll（托管封装）在首次调用时通过P/Invoke加载原生halcon.dll；
    /// 若本进程PATH中没有 %HALCONROOT%\bin\%HALCONARCH%，会抛
    /// System.DllNotFoundException: 无法加载 DLL "halcon"（HRESULT: 0x8007007E）。
    /// 主控程序可正常运行是因为其输出目录直接放了一份halcon.dll，
    /// 而子进程工作目录/所在目录不同，必须自行解析。
    /// </summary>
    internal static class HalconEnvironment
    {
        /// <summary>
        /// 按优先级把可能包含原生halcon.dll的目录前置到本进程PATH：
        /// ① %HALCONROOT%\bin\%HALCONARCH%（标准安装位置）
        /// ② 本exe上级目录（主控程序输出目录，其中已部署halcon.dll）
        /// 该方法只修改当前进程环境变量，不影响系统配置。
        /// </summary>
        public static void EnsureNativeDllResolvable()
        {
            var candidates = new[]
            {
                GetHalconInstallBinDir(),
                GetParentDirOfExe()
            };

            string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var dir in candidates)
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                    continue;
                if (path.IndexOf(dir, StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                path = dir + Path.PathSeparator + path;
            }
            Environment.SetEnvironmentVariable("PATH", path);
        }

        private static string GetHalconInstallBinDir()
        {
            string root = Environment.GetEnvironmentVariable("HALCONROOT");
            string arch = Environment.GetEnvironmentVariable("HALCONARCH");
            if (string.IsNullOrEmpty(root))
                return null;
            return Path.Combine(root, "bin", string.IsNullOrEmpty(arch) ? "x64-win64" : arch);
        }

        private static string GetParentDirOfExe()
        {
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
                return Path.GetDirectoryName(exeDir);
            }
            catch
            {
                return null;
            }
        }
    }
}
