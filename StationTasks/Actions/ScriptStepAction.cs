using Core.Utilities;
using MotionControl.Exceptions;
using Natasha.CSharp;
using Prism.Events;
using Recipe.Events;
using Recipe.Interfaces;
using StationTasks.Models;
using StationTasks.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace StationTasks.Actions
{
    /// <summary>
    /// SCRIPT步骤动作：基于 Natasha 动态编译执行 C# 脚本
    /// 脚本约定：类名 ScriptAction，方法 public static bool Execute(ScriptContext ctx)
    /// </summary>
    public class ScriptStepAction : IProcessStepAction
    {
        private readonly IRecipePoolService _recipePoolService;
        private readonly ILoggerService _logger;
        private readonly IEventAggregator _eventAggregator;
        private readonly object _compileLock = new object();
        private Func<ScriptContext, bool> _compiledDelegate;
        private string _compiledScript;
        private static bool _natashaInitialized;
        private static readonly object _initLock = new object();

        /// <summary> 步骤输出参数字典，由 ProcessStepExecutor 注入 </summary>
        public Dictionary<string, string> StepOutputs { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public StepType SupportedStepType => StepType.SCRIPT;

        public ScriptStepAction(IRecipePoolService recipePoolService, ILoggerService logger, IEventAggregator eventAggregator)
        {
            _recipePoolService = recipePoolService;
            _logger = logger;
            _eventAggregator = eventAggregator;
        }

        /// <summary>
        /// 执行SCRIPT步骤：加载全局变量→编译脚本→执行→写入全局变量→发布事件通知UI刷新
        /// </summary>
        public async Task ExecuteAsync(ProcessStep step, StationTaskBase task, CancellationToken token)
        {
            if (step.ScriptDetail == null || string.IsNullOrWhiteSpace(step.ScriptDetail.ScriptCode))
            {
                _logger.Warn($"SCRIPT 步骤 [{step.Seq}] 无脚本代码，跳过");
                return;
            }

            var poolId = _recipePoolService.CurrentPoolName;
            var globalVars = string.IsNullOrEmpty(poolId)
                ? new List<Core.Models.GlobalVariable>()
                : await _recipePoolService.LoadGlobalVariablesAsync(poolId);

            var globalVariables = globalVars.ToDictionary(
                gv => gv.Name,
                gv => gv.Value ?? "0",
                StringComparer.OrdinalIgnoreCase);

            _logger.Info($"SCRIPT 步骤 [{step.Seq}] 开始执行，全局变量数: {globalVariables.Count}，步骤输出数: {StepOutputs.Count}");

            var ctx = new ScriptContext(globalVariables, StepOutputs);
            bool success;
            try
            {
                EnsureCompiled(step.ScriptDetail.ScriptCode);
                success = _compiledDelegate(ctx);
            }
            catch (InvalidOperationException ex)
            {
                throw new RecoverableException(
                    message: $"SCRIPT 步骤 [{step.Seq}] 脚本错误: {ex.Message}",
                    suggestedAction: "请检查脚本语法是否正确，类名是否为 ScriptAction，方法签名是否匹配。");
            }
            catch (Exception ex)
            {
                throw new RecoverableException(
                    message: $"SCRIPT 步骤 [{step.Seq}] 运行时异常: {ex.Message}",
                    suggestedAction: "请检查脚本逻辑，确认变量名和类型转换是否正确。");
            }

            if (!success)
            {
                throw new RecoverableException(
                    message: $"SCRIPT 步骤 [{step.Seq}] 脚本返回 false，执行失败",
                    suggestedAction: "请检查脚本逻辑，确认执行条件是否满足。");
            }

            var changes = ctx.GetChanges();
            if (changes.Count == 0)
            {
                _logger.Info($"SCRIPT 步骤 [{step.Seq}] 执行完成，无输出");
                return;
            }

            foreach (var kv in changes)
            {
                var targetVar = globalVars.FirstOrDefault(v => string.Equals(v.Name, kv.Key, StringComparison.OrdinalIgnoreCase));
                if (targetVar != null)
                {
                    targetVar.Value = kv.Value;
                    _logger.Info($"SCRIPT 步骤 [{step.Seq}] 全局变量 [{kv.Key}] = {kv.Value}");
                }
                else
                {
                    _logger.Info($"SCRIPT 步骤 [{step.Seq}] 输出参数 [{kv.Key}] 未匹配到全局变量，跳过回写");
                }
            }

            if (!string.IsNullOrEmpty(poolId) && globalVars.Count > 0)
            {
                await _recipePoolService.SaveGlobalVariablesAsync(poolId, globalVars);

                // 通知全局变量窗口重新加载最新数据（使用 CurrentPoolName 与加载/保存一致）
                if (!string.IsNullOrEmpty(poolId))
                    _eventAggregator.GetEvent<GlobalVariablesChangedEvent>().Publish(poolId);
            }

            foreach (var kv in changes)
            {
                StepOutputs[kv.Key] = kv.Value;
            }

            _logger.Info($"SCRIPT 步骤 [{step.Seq}] 执行完成，输出 {changes.Count} 个参数");
        }

        /// <summary>
        /// 确保 Natasha 已初始化且脚本已编译，仅当脚本内容变化时重新编译
        /// </summary>
        private void EnsureCompiled(string scriptCode)
        {
            lock (_compileLock)
            {
                if (_compiledDelegate != null && scriptCode == _compiledScript)
                    return;

                EnsureNatashaInitialized();

                try
                {
                    var builder = new AssemblyCSharpBuilder();
                    builder.Compiler.Domain = DomainManagement.Random;
                    builder.UseStreamCompile();
                    builder.ThrowAndLogCompilerError();
                    builder.ThrowAndLogSyntaxError();
                    builder.Add(scriptCode);

                    var assembly = builder.GetAssembly();

                    var targetType = assembly.GetType("ScriptAction");
                    if (targetType == null)
                        throw new InvalidOperationException("脚本编译成功，但未找到约定的类 'ScriptAction'");

                    var executeMethod = targetType.GetMethod("Execute",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new Type[] { typeof(ScriptContext) },
                        null);

                    if (executeMethod == null)
                        throw new InvalidOperationException("未找到约定的方法 'public static bool Execute(ScriptContext ctx)'");

                    if (executeMethod.ReturnType != typeof(bool))
                        throw new InvalidOperationException("方法 'Execute' 的返回类型必须是 'bool'");

                    _compiledDelegate = (Func<ScriptContext, bool>)
                        Delegate.CreateDelegate(typeof(Func<ScriptContext, bool>), executeMethod);

                    _compiledScript = scriptCode;
                    _logger.Info("SCRIPT 脚本编译并绑定成功");
                }
                catch (InvalidOperationException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.Error($"SCRIPT 脚本编译失败: {ex.Message}");
                    _compiledDelegate = null;
                    _compiledScript = null;
                    throw new InvalidOperationException($"脚本编译失败: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 全局一次性初始化 Natasha 编译引擎
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
