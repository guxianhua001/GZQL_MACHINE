using Core.Abstraction;
using Core.Utilities;
using Prism.Commands;
using Prism.Mvvm;
using Recipe.Interfaces;
using StationTasks.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using Module.Services;

namespace Module.ViewModels
{
    /// <summary>
    /// IF 步骤条件配置对话框 ViewModel。
    /// 支持条件表达式编辑、全局变量/步骤输出变量插入、实时语法校验。
    /// 表达式求值由 FormulaEvaluator 处理，支持 @GV: 和 @Output: 变量引用。
    /// </summary>
    public class IfDetailViewModel : BindableBase, IDialogCloseable
    {
        private readonly IRecipePoolService _recipePoolService;
        private readonly IFormulaEvaluator _formulaEvaluator;
        private readonly ILoggerService _logger;
        private readonly IProcessSequenceService _sequenceService;
        private ProcessStep _step;

        /// <summary>请求关闭对话框时触发</summary>
        public event Action<object> RequestClose;

        /// <summary>是否可以关闭对话框</summary>
        public bool CanCloseDialog() => true;

        /// <summary> 当前编辑的 IF 步骤，设置时自动加载现有配置 </summary>
        public ProcessStep Step
        {
            get => _step;
            set
            {
                if (SetProperty(ref _step, value))
                {
                    InitializeFromStep();
                    RaisePropertyChanged(nameof(StepDescription));
                }
            }
        }

        /// <summary> 步骤描述文本（用于标题栏显示） </summary>
        public string StepDescription => _step == null ? "—" : $"Seq{_step.Seq} → IF";

        private string _conditionExpression = "";
        /// <summary> 条件表达式（支持 @GV: 和 @Output: 变量引用） </summary>
        public string ConditionExpression
        {
            get => _conditionExpression;
            set
            {
                if (SetProperty(ref _conditionExpression, value))
                {
                    ScheduleValidation();
                }
            }
        }

        private string _description;
        /// <summary> IF 步骤说明 </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private bool _isValidationValid = true;
        /// <summary> 表达式语法校验是否通过 </summary>
        public bool IsValidationValid
        {
            get => _isValidationValid;
            set => SetProperty(ref _isValidationValid, value);
        }

        private string _validationMessage;
        /// <summary> 表达式校验消息 </summary>
        public string ValidationMessage
        {
            get => _validationMessage;
            set => SetProperty(ref _validationMessage, value);
        }

        /// <summary> 全局变量名列表（含 @GV: 前缀，用于下拉插入） </summary>
        public ObservableCollection<string> GlobalVariableNames { get; } = new ObservableCollection<string>();

        /// <summary> 前序步骤输出参数名列表（含 @Output: 前缀，用于下拉插入和校验） </summary>
        public ObservableCollection<string> PreviousStepOutputNames { get; } = new ObservableCollection<string>();

        /// <summary>
        /// 合并的变量列表（@GV: + @Output:），供下拉框统一显示。
        /// 在 GlobalVariableNames 和 PreviousStepOutputNames 加载完成后刷新。
        /// </summary>
        public ObservableCollection<string> AllVariableNames { get; } = new ObservableCollection<string>();

        /// <summary> 常用表达式模板（用于快速插入） </summary>
        public ObservableCollection<string> ExpressionTemplates { get; } = new ObservableCollection<string>
        {
            "@GV:变量名 == true",
            "@GV:变量名 == false",
            "@GV:变量名 > 0",
            "@GV:变量名1 > @GV:变量名2",
            "@Output:步骤1_GOTO结果 == true",
            "@Output:PassFlag == 1",
            "@GV:变量名1 + @GV:变量名2 > 10"
        };

        public ICommand SaveCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand InsertVariableCommand { get; }
        public ICommand InsertTemplateCommand { get; }

        private DispatcherTimer _validateTimer;

        /// <summary>
        /// 构造函数：注入配方池服务（加载全局变量）、表达式求值器（语法校验）、
        /// 日志服务、工序序列服务（收集前序步骤输出参数）
        /// </summary>
        public IfDetailViewModel(
            IRecipePoolService recipePoolService,
            IFormulaEvaluator formulaEvaluator,
            ILoggerService logger,
            IProcessSequenceService sequenceService)
        {
            _recipePoolService = recipePoolService;
            _formulaEvaluator = formulaEvaluator;
            _logger = logger;
            _sequenceService = sequenceService;

            SaveCommand = new DelegateCommand(OnSave);
            CloseCommand = new DelegateCommand(OnClose);
            InsertVariableCommand = new DelegateCommand<string>(OnInsertVariable);
            InsertTemplateCommand = new DelegateCommand<string>(OnInsertTemplate);

            _validateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _validateTimer.Tick += (s, e) =>
            {
                _validateTimer.Stop();
                ValidateExpression();
            };
        }

        /// <summary>
        /// 从 Step.IfDetail 加载配置，为空则创建默认值。
        /// 同时异步加载全局变量列表和前序步骤输出参数供下拉插入。
        /// </summary>
        private void InitializeFromStep()
        {
            if (_step == null) return;

            // 确保 IF 步骤已初始化
            if (_step.IfDetail == null)
            {
                _step.IfDetail = new IfDetail
                {
                    ConditionExpression = "",
                    Description = ""
                };
            }

            ConditionExpression = _step.IfDetail.ConditionExpression ?? "";
            Description = _step.IfDetail.Description;

            // 同步收集前序步骤输出参数（@Output: 变量）
            LoadPreviousStepOutputs();

            // 异步加载全局变量列表（@GV: 变量）
            _ = LoadGlobalVariablesAsync();
        }

        /// <summary>
        /// 异步加载全局变量列表，供下拉插入使用。
        /// 加载完成后合并到 AllVariableNames 供下拉框统一显示。
        /// </summary>
        private async Task LoadGlobalVariablesAsync()
        {
            try
            {
                GlobalVariableNames.Clear();

                var poolId = _recipePoolService?.CurrentPoolId;
                if (string.IsNullOrEmpty(poolId)) return;

                var globalVars = await _recipePoolService!.LoadGlobalVariablesAsync(poolId);
                foreach (var gv in globalVars)
                {
                    if (!string.IsNullOrEmpty(gv.Name))
                        GlobalVariableNames.Add($"@GV:{gv.Name}");
                }

                _logger.Info($"[IfDetail] 已加载 {GlobalVariableNames.Count} 个全局变量");
                RefreshAllVariableNames();
            }
            catch (Exception ex)
            {
                _logger.Warn($"[IfDetail] 加载全局变量失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 收集当前 IF 步骤之前所有步骤的输出参数，供条件表达式引用。
        /// 参考 ConditionBranchViewModel.LoadPreviousStepOutputs 的实现：
        /// - 自动为每个前序步骤生成布尔型"整体结果"输出（@Output:步骤{Seq}_{Step}结果）
        /// - 收集 BranchConfig.OutputParameters 中已配置的输出参数
        /// - 收集 VisionDetail.VariableMappings 中的全局变量映射（作为 @GV: 引用）
        /// </summary>
        private void LoadPreviousStepOutputs()
        {
            PreviousStepOutputNames.Clear();

            if (_sequenceService?.CurrentTask?.Steps == null || _step == null) return;

            var seenNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var step in _sequenceService.CurrentTask.Steps)
            {
                // 遇到当前 IF 步骤即停止（只收集前序步骤）
                if (step == _step || step.Seq >= _step.Seq) break;

                // 1. 为每个前序步骤自动添加布尔型"整体结果"输出
                string stepResultName = $"@Output:步骤{step.Seq}_{step.Step}结果";
                if (seenNames.Add(stepResultName))
                    PreviousStepOutputNames.Add(stepResultName);

                // 2. 收集 BranchConfig.OutputParameters 中已配置的输出参数
                if (step.BranchConfig?.OutputParameters != null)
                {
                    foreach (var param in step.BranchConfig.OutputParameters)
                    {
                        if (!string.IsNullOrEmpty(param.Name))
                        {
                            string refName = $"@Output:{param.Name}";
                            if (seenNames.Add(refName))
                                PreviousStepOutputNames.Add(refName);
                        }
                    }
                }

                // 3. 收集 VISION 步骤的变量映射输出（作为 @GV: 引用）
                if (step.VisionDetail?.VariableMappings != null)
                {
                    foreach (var mapping in step.VisionDetail.VariableMappings)
                    {
                        if (!string.IsNullOrEmpty(mapping.GlobalVariableName))
                        {
                            string refName = $"@GV:{mapping.GlobalVariableName}";
                            if (seenNames.Add(refName))
                                PreviousStepOutputNames.Add(refName);
                        }
                    }
                }
            }

            _logger.Info($"[IfDetail] 已收集 {PreviousStepOutputNames.Count} 个前序步骤输出参数");
            RefreshAllVariableNames();
        }

        /// <summary>
        /// 刷新合并的变量列表（@Output: 优先 + @GV: 全局变量），供下拉框统一显示。
        /// </summary>
        private void RefreshAllVariableNames()
        {
            AllVariableNames.Clear();
            // 先添加 @Output: 变量（前序步骤输出），再添加 @GV: 变量（全局变量）
            foreach (var name in PreviousStepOutputNames)
                AllVariableNames.Add(name);
            foreach (var name in GlobalVariableNames)
                AllVariableNames.Add(name);
        }

        /// <summary>
        /// 调度延迟校验：避免每次按键都触发校验，提升输入体验
        /// </summary>
        private void ScheduleValidation()
        {
            _validateTimer.Stop();
            _validateTimer.Start();
        }

        /// <summary>
        /// 校验条件表达式语法。
        /// 空表达式视为有效（按 false 处理）。
        /// 使用 ExpressionValidator 校验括号匹配、运算符合法性、变量引用格式。
        /// </summary>
        private void ValidateExpression()
        {
            if (string.IsNullOrWhiteSpace(ConditionExpression))
            {
                IsValidationValid = true;
                ValidationMessage = "";
                return;
            }

            try
            {
                // 合并全局变量和步骤输出作为可用变量列表
                var availableVars = GlobalVariableNames
                    .Concat(PreviousStepOutputNames)
                    .ToList();

                // 使用 ExpressionValidator 校验语法
                string error = ExpressionValidator.Validate(ConditionExpression, availableVars);

                if (string.IsNullOrEmpty(error))
                {
                    IsValidationValid = true;
                    ValidationMessage = "";
                }
                else
                {
                    // 忽略"未知变量"错误中针对 @Output: 前缀的（前序步骤输出可能未加载）
                    if (error.StartsWith("未知变量:") && error.Contains("@Output:"))
                    {
                        IsValidationValid = true;
                        ValidationMessage = "";
                    }
                    else
                    {
                        IsValidationValid = false;
                        ValidationMessage = error;
                    }
                }
            }
            catch (Exception ex)
            {
                IsValidationValid = false;
                ValidationMessage = $"校验失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 插入变量到表达式编辑框光标位置（由 View 调用，实际光标处理在 View 中）
        /// </summary>
        private void OnInsertVariable(string variable)
        {
            if (string.IsNullOrEmpty(variable)) return;
            // 实际插入由 View 处理（需要光标位置），这里仅触发事件
            // View 通过 InsertVariableCommand 参数获取变量名并插入到 TextBox
        }

        /// <summary>
        /// 插入表达式模板（替换当前表达式内容）
        /// </summary>
        private void OnInsertTemplate(string template)
        {
            if (string.IsNullOrEmpty(template)) return;
            ConditionExpression = template;
        }

        /// <summary>
        /// 保存当前配置到 Step.IfDetail 并关闭弹窗
        /// </summary>
        private void OnSave()
        {
            if (_step == null) return;

            if (_step.IfDetail == null)
                _step.IfDetail = new IfDetail();

            _step.IfDetail.ConditionExpression = ConditionExpression ?? "";
            _step.IfDetail.Description = Description;

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
}
