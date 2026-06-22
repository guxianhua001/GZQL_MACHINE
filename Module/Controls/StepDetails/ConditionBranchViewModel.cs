using Core.Models;
using Core.Utilities;
using Core.Abstraction;
using Module.Services;
using Prism.Commands;
using Prism.Mvvm;
using Recipe.Interfaces;
using StationTasks.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Module.ViewModels
{
    public class ConditionBranchViewModel : BindableBase, IDialogCloseable
    {
        private readonly IProcessSequenceService _sequenceService;
        private readonly IRecipePoolService _recipePoolService;
        private readonly ILoggerService _logger;
        private ProcessStep _step;

        /// <summary> DialogHost标识符，用于关闭对话框 </summary>
        private const string DialogIdentifier = "MainDialogHost";

        /// <summary>请求关闭对话框时触发</summary>
        public event Action<object> RequestClose;

        /// <summary>是否可以关闭对话框</summary>
        public bool CanCloseDialog() => true;

        /// <summary> 构造函数：注入步骤序列服务和配方池服务（用于加载全局变量列表） </summary>
        public ConditionBranchViewModel(
            IProcessSequenceService sequenceService,
            IRecipePoolService recipePoolService,
            ILoggerService logger)
        {
            _sequenceService = sequenceService;
            _recipePoolService = recipePoolService;
            _logger = logger;

            OutputParameters = new ObservableCollection<BranchOutputParameter>();
            Conditions = new ObservableCollection<BranchCondition>();
            GlobalVariables = new ObservableCollection<GlobalVariable>();
            GlobalVariableNames = new ObservableCollection<string>();
            PreviousStepOutputs = new ObservableCollection<StepOutputInfo>();
            PreviousStepOutputNames = new ObservableCollection<string>();

            // 初始化默认动作选项（多语言键，运行时由 LangExtension 解析）
            // 这里先用占位符，实际显示通过 XAML 的 lang:Lang 绑定
            DefaultActionTypes = new List<string>
            {
                "Continue",
                "Stop",
                "SkipTo"
            };

            AddOutputCommand = new DelegateCommand(OnAddOutput);
            RemoveOutputCommand = new DelegateCommand(OnRemoveOutput, () => SelectedOutputParameter != null);
            AddConditionCommand = new DelegateCommand(OnAddCondition);
            RemoveConditionCommand = new DelegateCommand(OnRemoveCondition, () => SelectedCondition != null);
            OkCommand = new DelegateCommand(OnOk);
            CancelCommand = new DelegateCommand(OnCancel);
            MoveUpCommand = new DelegateCommand<BranchCondition>(OnMoveUp);
            MoveDownCommand = new DelegateCommand<BranchCondition>(OnMoveDown);
        }

        /// <summary> 当前正在配置的步骤（设置时自动加载现有配置） </summary>
        public ProcessStep Step
        {
            get => _step;
            set
            {
                SetProperty(ref _step, value);
                LoadFromStep(value);
            }
        }

        /// <summary> 是否启用条件分支 </summary>
        private bool _isEnabled;
        public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }

        /// <summary> 默认动作是否为跳转模式（与 IsDefaultContinue 互斥，双向绑定用）</summary>
        private bool _isSkipTo = true;
        public bool IsSkipTo
        {
            get => _isSkipTo;
            set
            {
                if (SetProperty(ref _isSkipTo, value))
                {
                    DefaultAction = value ? DefaultBranchAction.SkipTo : DefaultBranchAction.Continue;
                    RaisePropertyChanged(nameof(IsDefaultContinue));
                }
            }
        }

        /// <summary> 默认动作是否为继续模式（与 IsSkipTo 互斥，双向绑定用）</summary>
        public bool IsDefaultContinue
        {
            get => !_isSkipTo;
            set
            {
                if (value != _isSkipTo)
                {
                    _isSkipTo = !value;
                    DefaultAction = _isSkipTo ? DefaultBranchAction.SkipTo : DefaultBranchAction.Continue;
                    RaisePropertyChanged(nameof(IsSkipTo));
                    RaisePropertyChanged(nameof(IsDefaultContinue));
                }
            }
        }

        /// <summary> 输出参数列表（该步骤执行后产生的结果数据） </summary>
        public ObservableCollection<BranchOutputParameter> OutputParameters { get; }

        /// <summary> 当前选中的输出参数（用于删除操作） </summary>
        private BranchOutputParameter _selectedOutputParameter;
        public BranchOutputParameter SelectedOutputParameter
        {
            get => _selectedOutputParameter;
            set
            {
                if (SetProperty(ref _selectedOutputParameter, value))
                    RemoveOutputCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary> 条件规则列表（按优先级从高到低评估） </summary>
        public ObservableCollection<BranchCondition> Conditions { get; }

        /// <summary> 当前选中的条件规则（用于删除操作） </summary>
        private BranchCondition _selectedCondition;
        public BranchCondition SelectedCondition
        {
            get => _selectedCondition;
            set
            {
                if (SetProperty(ref _selectedCondition, value))
                    RemoveConditionCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary> 默认动作（所有条件都不满足时） </summary>
        private DefaultBranchAction _defaultAction = DefaultBranchAction.SkipTo;
        public DefaultBranchAction DefaultAction
        {
            get => _defaultAction;
            set
            {
                if (SetProperty(ref _defaultAction, value))
                {
                    _isSkipTo = (value == DefaultBranchAction.SkipTo);
                    RaisePropertyChanged(nameof(IsSkipTo));
                    RaisePropertyChanged(nameof(IsDefaultContinue));
                    RaisePropertyChanged(nameof(DefaultActionText));
                }
            }
        }

        /// <summary> 默认动作文本（用于 ComboBox 绑定，与枚举值双向同步） </summary>
        public string DefaultActionText
        {
            get => _defaultAction.ToString();
            set
            {
                if (Enum.TryParse<DefaultBranchAction>(value, out var action) && action != _defaultAction)
                    DefaultAction = action;
            }
        }

        /// <summary> 默认跳转目标步骤号（仅DefaultAction=SkipTo时有效） </summary>
        private int _defaultTargetStepSeq;
        public int DefaultTargetStepSeq { get => _defaultTargetStepSeq; set => SetProperty(ref _defaultTargetStepSeq, value); }

        /// <summary> 可选的步骤列表（用于下拉选择跳转目标） </summary>
        private List<int> _availableStepSeqs = new List<int>();
        public List<int> AvailableStepSeqs { get => _availableStepSeqs; private set => SetProperty(ref _availableStepSeqs, value); }

        /// <summary> 默认动作选项列表（用于下拉菜单，多语言） </summary>
        private List<string> _defaultActionTypes = new List<string>();
        public List<string> DefaultActionTypes { get => _defaultActionTypes; private set => SetProperty(ref _defaultActionTypes, value); }

        /// <summary> 全局变量列表（含类型信息，用于按类型过滤下拉选项） </summary>
        public ObservableCollection<GlobalVariable> GlobalVariables { get; }

        /// <summary> 全局变量名列表（兼容旧绑定，从GlobalVariables同步） </summary>
        public ObservableCollection<string> GlobalVariableNames { get; }

        /// <summary> 上一步输出参数列表（携带类型信息，用于输出参数下拉选择和类型自动推断） </summary>
        public ObservableCollection<StepOutputInfo> PreviousStepOutputs { get; }

        /// <summary> 上一步输出参数名列表（兼容条件表达式校验） </summary>
        public ObservableCollection<string> PreviousStepOutputNames { get; }

        /// <summary> 常用参数值选项（用于条件表达式下拉提示） </summary>
        public List<string> CommonParameterValues { get; } = new() { "true", "false", "0", "1" };

        /// <summary> 输出参数类型选项（用于输出参数表格的类型下拉） </summary>
        public List<GlobalVariableType> OutputTypeOptions { get; } =
            Enum.GetValues(typeof(GlobalVariableType)).Cast<GlobalVariableType>().ToList();

        /// <summary> 条件校验错误字典（key=条件对象, value=错误信息或空） </summary>
        public Dictionary<BranchCondition, string> ConditionErrors { get; } = new();

        /// <summary> 是否存在校验错误 </summary>
        public bool HasValidationErrors => ConditionErrors.Values.Any(v => !string.IsNullOrEmpty(v));

        // 命令定义
        public DelegateCommand AddOutputCommand { get; }
        public DelegateCommand RemoveOutputCommand { get; }
        public DelegateCommand AddConditionCommand { get; }
        public DelegateCommand RemoveConditionCommand { get; }
        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }

        /// <summary> 从步骤加载现有的分支配置到UI </summary>
        private void LoadFromStep(ProcessStep step)
        {
            if (step?.BranchConfig == null || !step.BranchConfig.IsEnabled)
            {
                IsEnabled = false;
                OutputParameters.Clear();
                Conditions.Clear();
                DefaultAction = DefaultBranchAction.SkipTo;
                DefaultTargetStepSeq = 0;
            }
            else
            {
                IsEnabled = step.BranchConfig.IsEnabled;
                OutputParameters.Clear();
                if (step.BranchConfig.OutputParameters != null)
                {
                    foreach (var param in step.BranchConfig.OutputParameters)
                    {
                        param.FilteredGlobalVariableNamesChanged += OnOutputTypeChanged;
                        OutputParameters.Add(param);
                    }
                }

                Conditions.Clear();
                if (step.BranchConfig.Conditions != null)
                {
                    foreach (var cond in step.BranchConfig.Conditions)
                        Conditions.Add(cond);
                }

                DefaultAction = step.BranchConfig.DefaultAction;
                DefaultTargetStepSeq = step.BranchConfig.DefaultTargetStepSeq;
            }

            RefreshAvailableSteps();
            LoadGlobalVariableNamesAndStepOutputsAsync(step);
        }

        /// <summary> 刷新可用步骤列表（从当前任务中获取） </summary>
        private void RefreshAvailableSteps()
        {
            if (_sequenceService?.CurrentTask?.Steps != null)
            {
                AvailableStepSeqs = _sequenceService.CurrentTask.Steps.Select(s => s.Seq).ToList();
            }
        }

        /// <summary> 先加载全局变量列表，再加载步骤输出（确保全局变量类型信息可用） </summary>
        private async void LoadGlobalVariableNamesAndStepOutputsAsync(ProcessStep step)
        {
            await LoadGlobalVariableNamesAsync();
            LoadPreviousStepOutputs(step);
            foreach (var param in OutputParameters)
                RefreshFilteredGlobalVariables(param);
        }

        /// <summary> 从RecipePoolService加载全局变量列表，供下拉选择 </summary>
        private async Task LoadGlobalVariableNamesAsync()
        {
            GlobalVariables.Clear();
            GlobalVariableNames.Clear();
            try
            {
                var poolId = _recipePoolService?.CurrentPoolId;
                if (string.IsNullOrEmpty(poolId)) return;

                var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);
                foreach (var v in variables)
                {
                    GlobalVariables.Add(v);
                    GlobalVariableNames.Add(v.Name);
                }
            }
            catch (Exception)
            {
                // 全局变量加载失败时不阻塞UI
            }
        }

        /// <summary> 根据输出参数类型过滤可绑定的全局变量名列表 </summary>
        public IEnumerable<string> GetFilteredGlobalVariableNames(GlobalVariableType outputType)
        {
            return GlobalVariables
                .Where(gv => gv.Type == outputType)
                .Select(gv => gv.Name);
        }

        /// <summary> 输出参数的 OutputType 变化时刷新过滤列表并校验全局变量绑定 </summary>
        private void OnOutputTypeChanged(BranchOutputParameter param)
        {
            RefreshFilteredGlobalVariables(param);
        }

        /// <summary> 刷新单个输出参数的过滤后全局变量列表 </summary>
        private void RefreshFilteredGlobalVariables(BranchOutputParameter param)
        {
            param.FilteredGlobalVariableNames = GetFilteredGlobalVariableNames(param.OutputType).ToList();
        }

        /// <summary>
        /// 收集当前步骤之前所有步骤的输出参数，供条件表达式中引用和输出参数下拉选择
        /// 自动为每个前序步骤生成一个布尔型"整体结果"输出（如 @Output:步骤3_结果）
        /// 同时收集已配置的 BranchConfig.OutputParameters 和 VisionDetail.VariableMappings
        /// </summary>
        private void LoadPreviousStepOutputs(ProcessStep currentStep)
        {
            PreviousStepOutputs.Clear();
            PreviousStepOutputNames.Clear();

            if (_sequenceService?.CurrentTask?.Steps == null || currentStep == null) return;

            foreach (var step in _sequenceService.CurrentTask.Steps)
            {
                if (step.Seq >= currentStep.Seq) break;

                // 为每个前序步骤自动添加布尔型“整体结果”输出
                string stepResultName = $"@Output:Step{step.Seq}_{step.Step}Result";
                AddStepOutput(stepResultName, GlobalVariableType.Bool);

                // 收集已配置的 BranchConfig 输出参数（携带类型信息）
                if (step.BranchConfig?.OutputParameters != null)
                {
                    foreach (var param in step.BranchConfig.OutputParameters)
                    {
                        if (!string.IsNullOrEmpty(param.Name))
                        {
                            string refName = $"@Output:{param.Name}";
                            AddStepOutput(refName, param.OutputType);
                        }
                    }
                }

                // 收集 VISION 步骤的变量映射输出（根据全局变量类型推断）
                if (step.VisionDetail?.VariableMappings != null)
                {
                    foreach (var mapping in step.VisionDetail.VariableMappings)
                    {
                        if (!string.IsNullOrEmpty(mapping.GlobalVariableName))
                        {
                            string refName = $"@GV:{mapping.GlobalVariableName}";
                            var gvType = GlobalVariables.FirstOrDefault(g => g.Name == mapping.GlobalVariableName);
                            AddStepOutput(refName, gvType?.Type ?? GlobalVariableType.Double);
                        }
                    }
                }
            }
        }

        /// <summary> 添加步骤输出信息（去重） </summary>
        private void AddStepOutput(string name, GlobalVariableType type)
        {
            if (!PreviousStepOutputs.Any(o => o.Name == name))
            {
                PreviousStepOutputs.Add(new StepOutputInfo { Name = name, OutputType = type });
            }
            if (!PreviousStepOutputNames.Contains(name))
            {
                PreviousStepOutputNames.Add(name);
            }
        }

        /// <summary> 校验单个条件的表达式语法 </summary>
        public void ValidateCondition(BranchCondition condition)
        {
            if (condition == null) return;
            string error = ExpressionValidator.Validate(condition.ConditionExpression, PreviousStepOutputNames);
            ConditionErrors[condition] = error;
            RaisePropertyChanged(nameof(HasValidationErrors));
        }

        /// <summary> 校验所有条件表达式 </summary>
        public void ValidateAllConditions()
        {
            foreach (var cond in Conditions)
                ValidateCondition(cond);
        }

        /// <summary> 将指定条件向上移动一位（提高优先级） </summary>
        private void OnMoveUp(BranchCondition condition)
        {
            int idx = Conditions.IndexOf(condition);
            if (idx <= 0) return;
            Conditions.Move(idx, idx - 1);
        }

        /// <summary> 将指定条件向下移动一位（降低优先级） </summary>
        private void OnMoveDown(BranchCondition condition)
        {
            int idx = Conditions.IndexOf(condition);
            if (idx < 0 || idx >= Conditions.Count - 1) return;
            Conditions.Move(idx, idx + 1);
        }

        /// <summary> 拖拽排序：将 draggedItem 移动到 targetItem 的位置 </summary>
        public void MoveCondition(BranchCondition draggedItem, BranchCondition targetItem)
        {
            int fromIdx = Conditions.IndexOf(draggedItem);
            int toIdx = Conditions.IndexOf(targetItem);
            if (fromIdx < 0 || toIdx < 0 || fromIdx == toIdx) return;
            Conditions.RemoveAt(fromIdx);
            int insertIdx = Conditions.IndexOf(targetItem);
            Conditions.Insert(insertIdx, draggedItem);
        }

        /// <summary> 添加输出参数 </summary>
        private void OnAddOutput()
        {
            var param = new BranchOutputParameter
            {
                Name = $"参数{OutputParameters.Count + 1}",
                Value = "false",
                TargetGlobalVariable = ""
            };
            param.FilteredGlobalVariableNamesChanged += OnOutputTypeChanged;
            RefreshFilteredGlobalVariables(param);
            OutputParameters.Add(param);
        }

        /// <summary> 删除选中的输出参数 </summary>
        private void OnRemoveOutput()
        {
            if (SelectedOutputParameter != null)
            {
                OutputParameters.Remove(SelectedOutputParameter);
                SelectedOutputParameter = null;
            }
        }

        /// <summary> 添加条件规则 </summary>
        private void OnAddCondition()
        {
            Conditions.Add(new BranchCondition
            {
                ConditionExpression = "",
                TargetStepSeq = 0,
                Description = $"条件{Conditions.Count + 1}"
            });
        }

        /// <summary> 删除选中的条件规则 </summary>
        private void OnRemoveCondition()
        {
            if (SelectedCondition != null)
            {
                Conditions.Remove(SelectedCondition);
                SelectedCondition = null;
            }
        }

        /// <summary> 确认保存：校验所有条件表达式，通过后将UI配置写回步骤的BranchConfig属性，并关闭DialogHost </summary>
        private void OnOk()
        {
            if (_step == null) return;

            var nonEmptyConditions = Conditions.Where(c => !string.IsNullOrWhiteSpace(c.ConditionExpression)).ToList();
            foreach (var cond in nonEmptyConditions)
                ValidateCondition(cond);

            var errors = ConditionErrors
                .Where(kv => nonEmptyConditions.Contains(kv.Key) && !string.IsNullOrEmpty(kv.Value))
                .ToList();

            if (errors.Count > 0)
            {
                var errorRows = errors
                    .Select(kv => Conditions.IndexOf(kv.Key) + 1)
                    .ToList();
                string errorMsg = string.Join("\n", errors.Select(kv => $"行{Conditions.IndexOf(kv.Key) + 1}: {kv.Value}"));
                _logger?.Error($"[ConditionBranch] 存在表达式错误，无法保存: 行 [{string.Join(", ", errorRows)}]");
                System.Windows.MessageBox.Show(
                    $"条件表达式校验失败:\n{errorMsg}",
                    "校验错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            var conditionsCopy = Conditions.Select(c => new BranchCondition
            {
                ConditionExpression = c.ConditionExpression ?? "",
                TargetStepSeq = c.TargetStepSeq,
                Description = c.Description ?? ""
            }).ToList();

            _logger?.Info($"[ConditionBranch] 保存条件分支配置: IsEnabled={IsEnabled}, 条件数={conditionsCopy.Count}");
            foreach (var c in conditionsCopy)
                _logger?.Info($"[ConditionBranch]   条件: Expression='{c.ConditionExpression}', Target={c.TargetStepSeq}, Desc={c.Description}");

            _step.BranchConfig = new BranchConfig
            {
                IsEnabled = IsEnabled,
                OutputParameters = OutputParameters.ToList(),
                Conditions = conditionsCopy,
                DefaultAction = DefaultAction,
                DefaultTargetStepSeq = DefaultTargetStepSeq
            };

            RequestClose?.Invoke(true);
        }

        /// <summary> 取消：直接关闭DialogHost，不保存修改 </summary>
        private void OnCancel()
        {
            RequestClose?.Invoke(false);
        }
    }
}
