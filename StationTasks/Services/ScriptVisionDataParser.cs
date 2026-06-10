using Core.Utilities;
using Natasha.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace StationTasks.Services
{
    /// <summary>
    /// 基于 Natasha 动态编译的视觉数据脚本解析器
    /// 脚本约定：用户编写完整的 C# 类，类名必须为 VisionParseScript，
    /// 包含 public static Dictionary&lt;string, double&gt; Parse(string data) 方法。
    /// </summary>
    public class ScriptVisionDataParser : IVisionDataParser
    {
        private readonly ILoggerService _logger;
        private readonly DefaultVisionDataParser _defaultParser;
        private readonly object _compileLock = new object();
        private string _script;
        private Func<string, Dictionary<string, double>> _compiledDelegate;
        private string _compiledScript;
        /// <summary>Natasha 全局初始化标志（只需初始化一次）</summary>
        private static bool _natashaInitialized;
        private static readonly object _initLock = new object();
        public ScriptVisionDataParser(ILoggerService logger)
        {
            _logger = logger;
            _defaultParser = new DefaultVisionDataParser(logger);
        }
        /// <summary>
        /// 用户自定义解析脚本（完整 C# 类代码）
        /// </summary>
        public string Script
        {
            get => _script;
            set => _script = value;
        }
        /// <summary>
        /// 解析原始视觉数据：若脚本为空则委托给默认解析器，否则使用动态编译的脚本解析
        /// </summary>
        public Dictionary<string, double> Parse(string rawData)
        {
            if (string.IsNullOrWhiteSpace(_script))
                return _defaultParser.Parse(rawData);
            try
            {
                EnsureCompiled();
                return _compiledDelegate(rawData);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error($"视觉脚本运行时错误: {ex.Message}");
                throw new InvalidOperationException($"视觉脚本运行时错误: {ex.Message}", ex);
            }
        }
        /// <summary>
        /// 仅编译脚本，不执行。用于验证脚本语法和约定是否正确
        /// </summary>
        /// <exception cref="InvalidOperationException">编译失败或约定不满足时抛出</exception>
        public void CompileScript()
        {
            if (string.IsNullOrWhiteSpace(_script))
                throw new InvalidOperationException("脚本内容为空");

            // 清除缓存以强制重新编译
            _compiledDelegate = null;
            _compiledScript = null;
            EnsureCompiled();
        }

        /// <summary>
        /// 确保 Natasha 已初始化且脚本已编译，仅当脚本内容变化时重新编译
        /// </summary>
        private void EnsureCompiled()
        {
            lock (_compileLock)
            {
                if (_compiledDelegate != null && _script == _compiledScript)
                    return;
                EnsureNatashaInitialized();
                try
                {
                    var builder = new AssemblyCSharpBuilder();
                    builder.Compiler.Domain = DomainManagement.Random;
                    builder.UseStreamCompile();
                    builder.ThrowAndLogCompilerError();
                    builder.ThrowAndLogSyntaxError();
                    builder.Add(_script);
                    var assembly = builder.GetAssembly();
                    // 优化：直接通过反射从编译后的程序集中获取目标方法并创建委托
                    var targetType = assembly.GetType("VisionParseScript");
                    if (targetType == null)
                    {
                        throw new InvalidOperationException("脚本编译成功，但未找到约定的类 'VisionParseScript'");
                    }
                    var parseMethod = targetType.GetMethod("Parse",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new Type[] { typeof(string) },
                        null);
                    if (parseMethod == null)
                    {
                        throw new InvalidOperationException("在 'VisionParseScript' 类中未找到约定的方法 'public static Dictionary<string, double> Parse(string data)'");
                    }
                    // 验证返回类型是否匹配
                    if (parseMethod.ReturnType != typeof(Dictionary<string, double>))
                    {
                        throw new InvalidOperationException("方法 'Parse' 的返回类型必须是 'Dictionary<string, double>'");
                    }
                    // 创建强类型委托，性能远优于动态调用
                    _compiledDelegate = (Func<string, Dictionary<string, double>>)Delegate.CreateDelegate(
                        typeof(Func<string, Dictionary<string, double>>), parseMethod);
                    _compiledScript = _script;
                    _logger.Info("视觉解析脚本编译并绑定成功");
                }
                catch (InvalidOperationException)
                {
                    // 抛出已格式化的约定异常，直接向上抛
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.Error($"视觉脚本编译失败: {ex.Message}");
                    _compiledDelegate = null;
                    _compiledScript = null;
                    throw new InvalidOperationException($"视觉脚本编译失败: {ex.Message}", ex);
                }
            }
        }
        /// <summary>
        /// 全局一次性初始化 Natasha 编译引擎（注册+预热）
        /// </summary>
        private static void EnsureNatashaInitialized()
        {
            if (_natashaInitialized) return;
            lock (_initLock)
            {
                if (_natashaInitialized) return;
                NatashaInitializer.Initialize().GetAwaiter().GetResult();
                _natashaInitialized = true;
            }
        }
    }
}