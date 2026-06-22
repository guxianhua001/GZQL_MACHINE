using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using MotionControl.Interfaces;
using Natasha.CSharp;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Recipe.Events;
using Recipe.Interfaces;
using StationTasks.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace Module.ViewModels
{
    /// <summary>
    /// SCRIPT 步骤编辑器 ViewModel，支持脚本编辑、编译检查、变量引用插入、执行预览
    /// </summary>
    public class ScriptDetailViewModel : BindableBase, IDialogCloseable
    {
        private readonly IRecipePoolService _recipePoolService;
        private readonly ILoggerService _logger;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILocalizationService _localizationService;
        private readonly IMotionService _motionService;
        private ProcessStep _step;
        private IList<ProcessStep> _allSteps;
        private readonly object _compileLock = new object();
        private Func<ScriptContext, bool> _compiledDelegate;
        private string _compiledScript;
        private static bool _natashaInitialized;
        private static readonly object _initLock = new object();

        /// <summary>请求关闭对话框时触发</summary>
        public event Action<object> RequestClose;

        /// <summary>是否可以关闭对话框</summary>
        public bool CanCloseDialog() => true;

        /// <summary> 当前编辑的工艺步骤，设置时自动初始化脚本配置 </summary>
        public ProcessStep Step
        {
            get => _step;
            set
            {
                if (SetProperty(ref _step, value))
                    InitializeFromStep();
            }
        }

        /// <summary> 步骤序列（含当前步骤的所有兄弟步骤），用于收集前序步骤输出参数 </summary>
        public IList<ProcessStep> AllSteps
        {
            get => _allSteps;
            set 
            { 
                if (SetProperty(ref _allSteps, value))
                    CollectStepOutputParameters();
            }
        }

        private string _scriptCode;
        /// <summary> C# 脚本代码 </summary>
        public string ScriptCode
        {
            get => _scriptCode;
            set => SetProperty(ref _scriptCode, value);
        }

        private string _description;
        /// <summary> 脚本说明 </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private ObservableCollection<GlobalVariableItem> _globalVariables = new ObservableCollection<GlobalVariableItem>();
        /// <summary> 全局变量列表，用于右侧面板显示和插入引用 </summary>
        public ObservableCollection<GlobalVariableItem> GlobalVariables
        {
            get => _globalVariables;
            set => SetProperty(ref _globalVariables, value);
        }

        private ObservableCollection<StepOutputItem> _stepOutputParameters = new ObservableCollection<StepOutputItem>();
        /// <summary> 前序步骤输出参数列表 </summary>
        public ObservableCollection<StepOutputItem> StepOutputParameters
        {
            get => _stepOutputParameters;
            set => SetProperty(ref _stepOutputParameters, value);
        }

        private string _compileResult;
        /// <summary> 编译结果消息 </summary>
        public string CompileResult
        {
            get => _compileResult;
            set => SetProperty(ref _compileResult, value);
        }

        private bool? _isCompileSuccess;
        /// <summary> 编译是否成功（null=未编译, true=成功, false=失败） </summary>
        public bool? IsCompileSuccess
        {
            get => _isCompileSuccess;
            set => SetProperty(ref _isCompileSuccess, value);
        }

        private string _executeResult;
        /// <summary> 执行结果消息 </summary>
        public string ExecuteResult
        {
            get => _executeResult;
            set => SetProperty(ref _executeResult, value);
        }

        private bool _isExecuting;
        /// <summary> 是否正在执行 </summary>
        public bool IsExecuting
        {
            get => _isExecuting;
            set => SetProperty(ref _isExecuting, value);
        }

        /// <summary> 插入文本到代码编辑器的回调（由 View 设置） </summary>
        public Action<string> InsertTextCallback { get; set; }

        public ICommand CompileCommand { get; }
        public ICommand ExecuteCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CloseCommand { get; }

        /// <summary> Default script template (English, no multilingual) </summary>
        public string DefaultScriptTemplate => @"using System;
using System.Collections.Generic;
using StationTasks.Models;

// ScriptAction - Custom script for SCRIPT step
// Convention: class ScriptAction with public static bool Execute(ScriptContext ctx)
// Use // comments only (no /// XML doc comments) to avoid Natasha CS1569 error
public class ScriptAction
{
    public static bool Execute(ScriptContext ctx)
    {
        // Read global variables
        // double val = ctx.GetDouble(""VariableName"");

        // Write to global variables
        // ctx.Set(""VariableName"", 1.23);

        // Read step output parameters
        // string output = ctx.GetStepOutput(""StepSeq_ParamName"");

        // Digital Output (DO) - write by port name from hwcfg.xml
        // ctx.WriteDO(""PortName"", true);

        // Digital Input (DI) - read by port name from hwcfg.xml
        // bool sensor = ctx.ReadDI(""SensorName"");

        // DO/DI by logical ID
        // ctx.WriteDO(0, true);
        // bool di0 = ctx.ReadDI(0);

        return true; // true=pass, false=fail
    }
}";

        public ScriptDetailViewModel(IRecipePoolService recipePoolService, ILoggerService logger, IEventAggregator eventAggregator, ILocalizationService localizationService, IMotionService motionService)
        {
            _recipePoolService = recipePoolService;
            _logger = logger;
            _eventAggregator = eventAggregator;
            _localizationService = localizationService;
            _motionService = motionService;

            CompileCommand = new DelegateCommand(OnCompile);
            ExecuteCommand = new DelegateCommand(OnExecute);
            SaveCommand = new DelegateCommand(OnSave);
            CloseCommand = new DelegateCommand(OnClose);
        }

        /// <summary>
        /// 获取多语言文本（便捷方法）
        /// </summary>
        private string L(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            if (_localizationService != null)
                return _localizationService.GetResource(key);

            var resource = Application.Current?.TryFindResource(key);
            return resource?.ToString() ?? $"[{key}]";
        }

        /// <summary>
        /// 从 Step.ScriptDetail 加载配置，为空则生成默认模板
        /// </summary>
        private void InitializeFromStep()
        {
            if (_step == null) return;

            if (_step.ScriptDetail == null)
            {
                _step.ScriptDetail = new ScriptDetail { ScriptCode = DefaultScriptTemplate };
            }

            var detail = _step.ScriptDetail;
            ScriptCode = detail.ScriptCode;
            Description = detail.Description;
            CompileResult = null;
            IsCompileSuccess = null;
            ExecuteResult = null;

            LoadGlobalVariables();
            CollectStepOutputParameters();
        }

        /// <summary>
        /// 从配方池加载全局变量列表
        /// </summary>
        private async void LoadGlobalVariables()
        {
            try
            {
                var poolId = _recipePoolService.CurrentPoolName;
                if (string.IsNullOrEmpty(poolId)) return;

                var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);
                GlobalVariables = new ObservableCollection<GlobalVariableItem>(
                    variables.Select(v => new GlobalVariableItem { Name = v.Name, Value = v.Value, Type = v.Type.ToString() }));
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(L("ScriptDetail_Log_VarLoadFail"), ex.Message));
            }
        }

        /// <summary>
        /// 遍历当前步骤之前的所有步骤，收集前序步骤的输出参数和自动生成的步骤结果键
        /// 模拟 ProcessStepExecutor 运行时的 _stepOutputs 累积逻辑，供脚本编写时引用预览
        /// </summary>
        private void CollectStepOutputParameters()
        {
            var outputs = new ObservableCollection<StepOutputItem>();

            if (_step == null || _allSteps == null)
            {
                StepOutputParameters = outputs;
                return;
            }

            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var prevStep in _allSteps)
            {
                if (prevStep == _step) break;

                if (prevStep.BranchConfig?.OutputParameters != null)
                {
                    foreach (var op in prevStep.BranchConfig.OutputParameters)
                    {
                        if (!string.IsNullOrEmpty(op.Name) && seenNames.Add(op.Name))
                            outputs.Add(new StepOutputItem { Name = op.Name, Value = op.Value ?? "" });
                    }
                }

                string stepResultKey = $"Step{prevStep.Seq}_{prevStep.Step}Result";
                if (seenNames.Add(stepResultKey))
                    outputs.Add(new StepOutputItem { Name = stepResultKey, Value = "true" });

                if (prevStep.Step == StepType.DASHBOARD && prevStep.DashboardDetail != null)
                {
                    string confirmKey = $"Step{prevStep.Seq}_DASHBOARDConfirmResult";
                    if (seenNames.Add(confirmKey))
                        outputs.Add(new StepOutputItem { Name = confirmKey, Value = "true" });

                    foreach (var field in prevStep.DashboardDetail.Fields ?? Enumerable.Empty<DashboardField>())
                    {
                        if (!string.IsNullOrEmpty(field.DisplayName))
                        {
                            string fieldKey = $"Step{prevStep.Seq}_DASHBOARD_{field.DisplayName}";
                            if (seenNames.Add(fieldKey))
                                outputs.Add(new StepOutputItem { Name = fieldKey, Value = "true" });
                        }
                    }
                }
            }

            StepOutputParameters = outputs;
        }

        /// <summary>
        /// 编译当前脚本代码，仅检查不执行
        /// </summary>
        private void OnCompile()
        {
            if (string.IsNullOrWhiteSpace(ScriptCode))
            {
                CompileResult = L("ScriptDetail_EmptyCode");
                IsCompileSuccess = false;
                return;
            }

            try
            {
                CompileScript(ScriptCode);
                CompileResult = L("ScriptDetail_CompileSuccess");
                IsCompileSuccess = true;
            }
            catch (Exception ex)
            {
                CompileResult = string.Format(L("ScriptDetail_CompileFailed"), ex.Message);
                IsCompileSuccess = false;
            }
        }

        /// <summary>
        /// 编译并执行脚本（编辑器预览模式），执行后回写全局变量并通知 UI 刷新
        /// </summary>
        private async void OnExecute()
        {
            if (string.IsNullOrWhiteSpace(ScriptCode))
            {
                ExecuteResult = L("ScriptDetail_EmptyCode");
                return;
            }

            IsExecuting = true;
            ExecuteResult = L("ScriptDetail_Executing");

            try
            {
                var poolName = _recipePoolService.CurrentPoolName;
                List<GlobalVariable> globalVars = null;
                Dictionary<string, string> changes = null;

                await System.Threading.Tasks.Task.Run(() =>
                {
                    CompileScript(ScriptCode);

                    globalVars = string.IsNullOrEmpty(poolName)
                        ? new List<GlobalVariable>()
                        : _recipePoolService.LoadGlobalVariablesAsync(poolName).GetAwaiter().GetResult();

                    var globalVariables = globalVars.ToDictionary(
                        gv => gv.Name,
                        gv => gv.Value ?? "0",
                        StringComparer.OrdinalIgnoreCase);

                    var stepOutputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var op in StepOutputParameters)
                        stepOutputs[op.Name] = op.Value ?? "0";

                    var ctx = new ScriptContext(globalVariables, stepOutputs, _motionService);
                    bool success = _compiledDelegate(ctx);

                    if (!success)
                    {
                        ExecuteResult = L("ScriptDetail_ExecReturnFalse");
                        return;
                    }

                    changes = ctx.GetChanges();

                    if (changes.Count == 0)
                    {
                        ExecuteResult = L("ScriptDetail_ExecNoOutput");
                    }
                    else
                    {
                        ExecuteResult = L("ScriptDetail_ExecResultHeader") + string.Join("\n", changes.Select(kv => $"  {kv.Key} = {kv.Value}"));

                        // 回写全局变量到内存模型
                        foreach (var kv in changes)
                        {
                            var targetVar = globalVars.FirstOrDefault(v => string.Equals(v.Name, kv.Key, StringComparison.OrdinalIgnoreCase));
                            if (targetVar != null)
                                targetVar.Value = kv.Value;
                        }
                    }
                });

                // 预览执行后：更新右侧面板 + 持久化 + 通知配方页面刷新
                if (changes != null && changes.Count > 0 && globalVars != null && !string.IsNullOrEmpty(poolName))
                {
                    // 更新右侧全局变量列表中的值
                    foreach (var kv in changes)
                    {
                        var item = GlobalVariables.FirstOrDefault(v => string.Equals(v.Name, kv.Key, StringComparison.OrdinalIgnoreCase));
                        if (item != null)
                            item.Value = kv.Value;
                    }

                    // 持久化保存到存储
                    await _recipePoolService.SaveGlobalVariablesAsync(poolName, globalVars);

                    // 通知配方页面全局变量窗口重新加载
                    _eventAggregator.GetEvent<GlobalVariablesChangedEvent>().Publish(poolName);
                }
            }
            catch (Exception ex)
            {
                ExecuteResult = string.Format(L("ScriptDetail_ExecFailed"), ex.Message);
            }
            finally
            {
                IsExecuting = false;
            }
        }

        /// <summary>
        /// 使用 Natasha 编译脚本
        /// </summary>
        private void CompileScript(string scriptCode)
        {
            lock (_compileLock)
            {
                if (_compiledDelegate != null && scriptCode == _compiledScript)
                    return;

                EnsureNatashaInitialized();

                var builder = new AssemblyCSharpBuilder();
                builder.Compiler.Domain = DomainManagement.Random;
                builder.UseStreamCompile();
                builder.ThrowAndLogCompilerError();
                builder.ThrowAndLogSyntaxError();
                builder.Add(scriptCode);

                var assembly = builder.GetAssembly();

                var targetType = assembly.GetType("ScriptAction");
                if (targetType == null)
                    throw new InvalidOperationException(L("ScriptDetail_ErrClassNotFound"));

                var executeMethod = targetType.GetMethod("Execute",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(ScriptContext) },
                    null);

                if (executeMethod == null)
                    throw new InvalidOperationException(L("ScriptDetail_ErrMethodNotFound"));

                if (executeMethod.ReturnType != typeof(bool))
                    throw new InvalidOperationException(L("ScriptDetail_ErrReturnType"));

                _compiledDelegate = (Func<ScriptContext, bool>)
                    Delegate.CreateDelegate(typeof(Func<ScriptContext, bool>), executeMethod);

                _compiledScript = scriptCode;
            }
        }

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

        /// <summary>
        /// 保存当前配置到 Step.ScriptDetail 并关闭弹窗
        /// </summary>
        private void OnSave()
        {
            if (_step == null) return;

            if (_step.ScriptDetail == null)
                _step.ScriptDetail = new ScriptDetail();

            _step.ScriptDetail.ScriptCode = ScriptCode;
            _step.ScriptDetail.Description = Description;

            RequestClose?.Invoke(true);
        }

        /// <summary>
        /// 关闭弹窗不保存
        /// </summary>
        private void OnClose()
        {
            RequestClose?.Invoke(false);
        }
    }

    /// <summary>
    /// 全局变量列表项，用于右侧面板显示，Value 变更时通知 UI 刷新
    /// </summary>
    public class GlobalVariableItem : BindableBase
    {
        public string Name { get; set; }
        public string Type { get; set; }

        private string _value;
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
    }

    /// <summary>
    /// 步骤输出参数列表项，用于右侧面板显示
    /// </summary>
    public class StepOutputItem
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
