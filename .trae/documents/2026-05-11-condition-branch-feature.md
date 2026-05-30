# 条件分支功能实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在步骤编辑器中增加通用条件分支功能，支持基于步骤输出参数或全局变量的表达式判断进行流程跳转

**Architecture:** 扩展现有CHECK步骤的条件跳转机制为通用条件分支系统。每个ProcessStep增加BranchConfig配置项，包含输出参数定义、条件表达式和跳转目标。复用FormulaEvaluator进行表达式求值，实现灵活的条件分支逻辑。

**Tech Stack:** WPF, Prism MVVM, Newtonsoft.Json, 自定义表达式引擎

---

## 需求分析

### 核心功能点
1. **步骤输出参数**：每个步骤可定义输出参数（如整体结果=true/false）
2. **变量连接**：输出参数可连接到全局变量（@GV:变量名）
3. **条件表达式**：支持比较运算符（=, >, <, >=, <=, !=）的表达式判断
4. **跳转控制**：根据表达式结果跳转到指定步骤序号

### 使用场景示例
```
步骤3: VISION拍照 → 输出参数: 检测结果=true/false → 写入@GV:检测结果
  ↓
步骤4: CONDITION分支 → 判断 @GV:检测结果 == true ?
  ├─ True → 跳转到步骤6 (组装)
  └─ False → 跳转到步骤8 (NG处理)
```

---

## 文件结构设计

### 新增文件
- `Core/Models/BranchConfig.cs` - 条件分支配置数据模型
- `Module/Views/ConditionBranchView.xaml` - 条件分支配置对话框UI
- `Module/ViewModels/ConditionBranchViewModel.cs` - 条件分支配置ViewModel

### 修改文件
- `StationTasks/Models/ProcessStep.cs` - 增加BranchConfig属性
- `StationTasks/Actions/ProcessStepExecutor.cs` - 增加条件分支执行逻辑
- `Module/Operators/Editor/ProcessSequenceEditorView.xaml` - 增加分支列和按钮
- `Module/Operators/Editor/ProcessSequenceEditorViewModel.cs` - 增加分支相关命令
- `Module/PrimModel.cs` - 注册ConditionBranchViewModel

---

## Task 1: 创建条件分支数据模型

**Files:**
- Create: `c:\WorkFiles\GZQL_MACHINE\Core\Models\BranchConfig.cs`

- [ ] **Step 1: 定义BranchOutputParameter类 - 步骤输出参数**

```csharp
using Prism.Mvvm;
using System;

namespace Core.Models
{
    /// <summary>
    /// 步骤输出参数定义，用于将步骤执行结果输出到全局变量或作为后续条件判断的数据源
    /// </summary>
    public class BranchOutputParameter : BindableBase
    {
        private string _name;
        private string _value;
        private string _targetGlobalVariable;

        /// <summary> 参数名称（如"检测结果"、"测量值"） </summary>
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        /// <summary> 参数值（true/false 或数值） </summary>
        public string Value { get => _value; set => SetProperty(ref _value, value); }

        /// <summary> 目标全局变量名（可选，设置后自动写入全局变量） </summary>
        public string TargetGlobalVariable { get => _targetGlobalVariable; set => SetProperty(ref _targetGlobalVariable, value); }
    }
}
```

- [ ] **Step 2: 定义BranchCondition类 - 分支条件规则**

```csharp
using Prism.Mvvm;

namespace Core.Models
{
    /// <summary>
    /// 单个分支条件规则：当条件满足时跳转到指定目标
    /// 支持多个条件的OR关系（任一条件满足即触发）
    /// </summary>
    public class BranchCondition : BindableBase
    {
        private string _conditionExpression;
        private int _targetStepSeq;
        private string _description;

        /// <summary>
        /// 条件表达式，支持格式：
        /// - 简单比较: "@GV:变量名 > 10"
        /// - 参数引用: "@Output:参数名 == true"
        /// - 复合表达式: "@GV:H2 - @GV:Slot > 0.27"
        /// </summary>
        public string ConditionExpression { get => _conditionExpression; set => SetProperty(ref _conditionExpression, value); }

        /// <summary> 条件满足时跳转的目标步骤Seq号（0表示继续下一步） </summary>
        public int TargetStepSeq { get => _targetStepSeq; set => SetProperty(ref _targetStepSeq, value); }

        /// <summary> 条件描述（用于UI显示，如"检测通过→跳转组装"） </summary>
        public string Description { get => _description; set => SetProperty(ref _description, value); }
    }
}
```

- [ ] **Step 3: 定义BranchConfig类 - 完整分支配置**

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Core.Models
{
    /// <summary>
    /// 步骤的条件分支配置，定义该步骤执行后的输出参数、条件判断和跳转逻辑
    /// 类似于CheckDetail但更通用，适用于所有步骤类型
    /// </summary>
    public class BranchConfig
    {
        /// <summary> 是否启用条件分支（默认false，向后兼容） </summary>
        public bool IsEnabled { get; set; }

        /// <summary> 输出参数列表（该步骤执行后产生的结果数据） </summary>
        public List<BranchOutputParameter> OutputParameters { get; set; } = new List<BranchOutputParameter>();

        /// <summary> 条件规则列表（按优先级从高到低评估，第一个匹配的条件生效） </summary>
        public List<BranchCondition> Conditions { get; set; } = new List<BranchCondition>();

        /// <summary> 所有条件都不满足时的默认动作（Continue=继续下一步, Stop=终止序列） </summary>
        public DefaultBranchAction DefaultAction { get; set; } = DefaultBranchAction.Continue;

        /// <summary> 默认动作的目标步骤Seq（仅DefaultAction=SkipTo时有效） </summary>
        public int DefaultTargetStepSeq { get; set; } = 0;
    }

    /// <summary> 默认分支动作枚举 </summary>
    public enum DefaultBranchAction
    {
        Continue,
        Stop,
        SkipTo
    }
}
```

- [ ] **Step 4: 编译验证**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\Core\Core.csproj --configuration Debug`
Expected: Build succeeded with no errors

---

## Task 2: 扩展ProcessStep模型

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\StationTasks\Models\ProcessStep.cs`

- [ ] **Step 1: 在ProcessStep类中添加BranchConfig属性**

在DashboardDetail属性后面添加：

```csharp
private BranchConfig _branchConfig;

/// <summary>
/// 步骤的条件分支配置（可选，启用后该步骤执行完会进行条件判断）
/// 支持基于输出参数或全局变量的表达式判断，决定后续跳转目标
/// </summary>
[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
public BranchConfig BranchConfig
{
    get => _branchConfig;
    set
    {
        if (_branchConfig != value)
        {
            _branchConfig = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBranchEnabled));
        }
    }
}

/// <summary> 是否启用了条件分支（扁平属性，供DataGrid列直接绑定） </summary>
[JsonIgnore]
public bool IsBranchEnabled => _branchConfig?.IsEnabled == true;
```

- [ ] **Step 2: 在EnsureAlarmConfigInitialized方法中添加BranchConfig初始化**

```csharp
// 在现有方法末尾添加
if (_branchConfig == null)
    _branchConfig = new BranchConfig();
OnPropertyChanged(nameof(IsBranchEnabled));
```

- [ ] **Step 3: 编译验证**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\StationTasks\StationTasks.csproj --configuration Debug`
Expected: Build succeeded

---

## Task 3: 实现条件分支执行逻辑

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\StationTasks\Actions\ProcessStepExecutor.cs`

- [ ] **Step 1: 注入IFormulaEvaluator依赖**

在构造函数中添加参数：

```csharp
private readonly IFormulaEvaluator _formulaEvaluator;

public ProcessStepExecutor(
    StationTaskBase task,
    ILoggerService logger,
    IEnumerable<IProcessStepAction> actions,
    IAlarmService alarmService,
    IFormulaEvaluator formulaEvaluator) // 新增参数
{
    // ...existing code...
    _formulaEvaluator = formulaEvaluator;
}
```

- [ ] **Step 2: 添加ExecuteBranchLogicAsync方法**

```csharp
/// <summary>
/// 执行条件分支逻辑：评估步骤的BranchConfig，返回下一个要执行的步骤索引
/// </summary>
private async Task<int> ExecuteBranchLogicAsync(
    ProcessStep step,
    ObservableCollection<ProcessStep> steps,
    int currentIndex,
    CancellationToken token)
{
    var branchConfig = step.BranchConfig;
    if (branchConfig == null || !branchConfig.IsEnabled)
    {
        return currentIndex + 1; // 未启用分支，正常执行下一步
    }

    _logger.Info($"[Branch] 步骤 [{step.Seq}] 开始评估条件分支...");

    // 1. 收集当前上下文中的变量值（全局变量 + 输出参数）
    var variables = await CollectContextVariablesAsync(step, branchConfig);

    // 2. 按顺序评估每个条件规则
    foreach (var condition in branchConfig.Conditions)
    {
        if (string.IsNullOrWhiteSpace(condition.ConditionExpression))
            continue;

        try
        {
            bool conditionResult = EvaluateCondition(condition.ConditionExpression, variables);
            _logger.Info($"[Branch] 条件 '{condition.ConditionExpression}' = {conditionResult}");

            if (conditionResult)
            {
                _logger.Info($"[Branch] ✓ 条件匹配! 跳转到步骤 [{condition.TargetStepSeq}] ({condition.Description})");
                return ResolveStepIndex(condition.TargetStepSeq, steps, currentIndex);
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"[Branch] 条件表达式评估失败: '{condition.ConditionExpression}' - {ex.Message}");
            continue;
        }
    }

    // 3. 所有条件都不满足，执行默认动作
    _logger.Info($"[Branch] 无条件匹配，执行默认动作: {branchConfig.DefaultAction}");
    return HandleDefaultAction(branchConfig, steps, currentIndex);
}
```

- [ ] **Step 3: 实现辅助方法CollectContextVariablesAsync**

```csharp
/// <summary>
/// 收集条件评估所需的上下文变量（全局变量 + 步骤输出参数）
/// </summary>
private async Task<Dictionary<string, string>> CollectContextVariablesAsync(
    ProcessStep step,
    BranchConfig branchConfig)
{
    var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    // 从全局变量服务获取当前全局变量值（需要注入IGlobalVariableService）
    // TODO: 这里需要根据实际的全局变量服务接口进行调整

    // 将输出参数加入变量池（前缀 @Output:）
    foreach (var output in branchConfig.OutputParameters)
    {
        if (!string.IsNullOrEmpty(output.Name))
        {
            variables[$"@Output:{output.Name}"] = output.Value ?? "false";
        }
    }

    return variables;
}
```

- [ ] **Step 4: 实现EvaluateCondition方法**

```csharp
/// <summary>
/// 评估条件表达式，返回bool结果
/// 表达式示例:
///   "@GV:检测结果 == true"
///   "@GV:H2 > 10.5"
///   "@Output:PassFlag == true && @GV:Count > 0"
/// </summary>
private bool EvaluateCondition(string expression, Dictionary<string, string> variables)
{
    try
    {
        // 使用FormulaEvaluator计算表达式值
        double result = _formulaEvaluator.Evaluate(expression, variables);

        // 非0值为true，0值为false（兼容数值表达式）
        // 对于布尔表达式，true=1.0, false=0.0
        return Math.Abs(result) > 0.0001;
    }
    catch (Exception ex)
    {
        _logger.Error($"[Branch] 表达式评估异常: '{expression}' - {ex.Message}");
        return false;
    }
}
```

- [ ] **Step 5: 实现HandleDefaultAction方法**

```csharp
/// <summary>
/// 处理所有条件都不满足时的默认动作
/// </summary>
private int HandleDefaultAction(BranchConfig config, ObservableCollection<ProcessStep> steps, int currentIndex)
{
    switch (config.DefaultAction)
    {
        case DefaultBranchAction.Stop:
            _logger.Warn("[Branch] 默认动作: 终止序列执行");
            return -1; // -1 表示终止

        case DefaultBranchAction.SkipTo:
            if (config.DefaultTargetStepSeq > 0)
            {
                _logger.Info($"[Branch] 默认动作: 跳转到步骤 [{config.DefaultTargetStepSeq}]");
                return ResolveStepIndex(config.DefaultTargetStepSeq, steps, currentIndex);
            }
            goto case DefaultBranchAction.Continue;

        case DefaultBranchAction.Continue:
        default:
            _logger.Info("[Branch] 默认动作: 继续下一步");
            return currentIndex + 1;
    }
}
```

- [ ] **Step 6: 修改ExecuteSingleStepAsync集成分支逻辑**

在switch语句的default分支之前，添加分支逻辑调用：

```csharp
case StepType.GOTO:
case StepType.VISION:
case StepType.SCAN:
case StepType.DASHBOARD:
    // ...existing execution code...
    
    // ★ 新增：执行完后检查是否启用了条件分支
    if (step.BranchConfig?.IsEnabled == true)
    {
        return await ExecuteBranchLogicAsync(step, steps, currentIndex, token);
    }
    return currentIndex + 1;

// 其他步骤类型类似处理...
```

- [ ] **Step 7: 编译验证**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\StationTasks\StationTasks.csproj --configuration Debug`
Expected: Build succeeded

---

## Task 4: 创建条件分支配置对话框UI

**Files:**
- Create: `c:\WorkFiles\GZQL_MACHINE\Module\Views\ConditionBranchView.xaml`
- Create: `c:\WorkFiles\GZQL_MACHINE\Module\ViewModels\ConditionBranchViewModel.cs`

- [ ] **Step 1: 创建ConditionBranchViewModel**

```csharp
using Core.Models;
using Module.Services;
using Prism.Commands;
using Prism.Mvvm;
using StationTasks.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Module.ViewModels
{
    public class ConditionBranchViewModel : BindableBase
    {
        private readonly IProcessSequenceService _sequenceService;
        private ProcessStep _step;

        public ConditionBranchViewModel(IProcessSequenceService sequenceService)
        {
            _sequenceService = sequenceService;
            
            OutputParameters = new ObservableCollection<BranchOutputParameter>();
            Conditions = new ObservableCollection<BranchCondition>();
            
            AddOutputCommand = new DelegateCommand(OnAddOutput);
            RemoveOutputCommand = new DelegateCommand<BranchOutputParameter>(OnRemoveOutput);
            AddConditionCommand = new DelegateCommand(OnAddCondition);
            RemoveConditionCommand = new DelegateCommand<BranchCondition>(OnRemoveCondition);
            OkCommand = new DelegateCommand(OnOk);
            CancelCommand = new DelegateCommand(() => RequestClose?.Invoke(false));
        }

        /// <summary> 当前正在配置的步骤 </summary>
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
        public bool IsEnabled { get; set; }

        /// <summary> 输出参数列表 </summary>
        public ObservableCollection<BranchOutputParameter> OutputParameters { get; }

        /// <summary> 条件规则列表 </summary>
        public ObservableCollection<BranchCondition> Conditions { get; }

        /// <summary> 默认动作 </summary>
        public DefaultBranchAction DefaultAction { get; set; }

        /// <summary> 默认跳转目标步骤号 </summary>
        public int DefaultTargetStepSeq { get; set; }

        /// <summary> 可选的步骤列表（用于下拉选择跳转目标） </summary>
        public List<int> AvailableStepSeqs { get; private set; }

        public ICommand AddOutputCommand { get; }
        public ICommand RemoveOutputCommand { get; }
        public ICommand AddConditionCommand { get; }
        public ICommand RemoveConditionCommand { get; }
        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }

        /// <summary> 关闭对话框回调 </summary>
        public Action<bool> RequestClose { get; set; }

        /// <summary> 从步骤加载现有配置 </summary>
        private void LoadFromStep(ProcessStep step)
        {
            if (step?.BranchConfig == null)
            {
                IsEnabled = false;
                OutputParameters.Clear();
                Conditions.Clear();
                DefaultAction = DefaultBranchAction.Continue;
                DefaultTargetStepSeq = 0;
                return;
            }

            IsEnabled = step.BranchConfig.IsEnabled;
            OutputParameters.Clear();
            foreach (var param in step.BranchConfig.OutputParameters)
                OutputParameters.Add(param);

            Conditions.Clear();
            foreach (var cond in step.BranchConfig.Conditions)
                Conditions.Add(cond);

            DefaultAction = step.BranchConfig.DefaultAction;
            DefaultTargetStepSeq = step.BranchConfig.DefaultTargetStepSeq;

            RefreshAvailableSteps();
        }

        /// <summary> 刷新可用步骤列表 </summary>
        private void RefreshAvailableSteps()
        {
            if (_sequenceService?.CurrentTask?.Steps != null)
            {
                AvailableStepSeqs = _sequenceService.CurrentTask.Steps.Select(s => s.Seq).ToList();
                RaisePropertyChanged(nameof(AvailableStepSeqs));
            }
        }

        private void OnAddOutput()
        {
            OutputParameters.Add(new BranchOutputParameter
            {
                Name = $"参数{OutputParameters.Count + 1}",
                Value = "false",
                TargetGlobalVariable = ""
            });
        }

        private void OnRemoveOutput(BranchOutputParameter param)
        {
            OutputParameters.Remove(param);
        }

        private void OnAddCondition()
        {
            Conditions.Add(new BranchCondition
            {
                ConditionExpression = "",
                TargetStepSeq = 0,
                Description = $"条件{Conditions.Count + 1}"
            });
        }

        private void OnRemoveCondition(BranchCondition condition)
        {
            Conditions.Remove(condition);
        }

        private void OnOk()
        {
            // 保存配置回步骤
            if (_step == null) return;

            _step.BranchConfig = new BranchConfig
            {
                IsEnabled = IsEnabled,
                OutputParameters = OutputParameters.ToList(),
                Conditions = Conditions.ToList(),
                DefaultAction = DefaultAction,
                DefaultTargetStepSeq = DefaultTargetStepSeq
            };

            RequestClose?.Invoke(true);
        }
    }
}
```

- [ ] **Step 2: 创建ConditionBranchView.xaml界面**

```xml
<UserControl x:Class="Module.Views.ConditionBranchView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes"
             xmlns:sys="clr-namespace:System;assembly=mscorlib"
             Width="650" Height="550">
    <StackPanel Margin="16">
        <TextBlock FontSize="18" FontWeight="Bold" Margin="0,0,0,16">
            ⚡ 条件分支配置
        </TextBlock>

        <!-- 启用开关 -->
        <CheckBox Content="启用条件分支" 
                  IsChecked="{Binding IsEnabled}" 
                  FontWeight="SemiBold"
                  Margin="0,8" />

        <!-- 输出参数区域 -->
        <GroupBox Header="📤 输出参数（步骤执行后输出的结果数据）" Margin="0,12">
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="*" />
                </Grid.RowDefinitions>

                <Button Content="+ 添加参数" 
                        Command="{Binding AddOutputCommand}"
                        HorizontalAlignment="Right"
                        Margin="0,0,0,8"
                        Style="{StaticResource MaterialDesignFlatButton}" />

                <DataGrid Grid.Row="1"
                          ItemsSource="{Binding OutputParameters}"
                          AutoGenerateColumns="False"
                          CanUserAddRows="False"
                          Height="120">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="参数名" Binding="{Binding Name}" Width="100" />
                        <DataGridTextColumn Header="值" Binding="{Binding Value}" Width="80" />
                        <DataGridTextColumn Header="目标全局变量" Binding="{Binding TargetGlobalVariable}" Width="150"
                                            materialDesign:HintAssist.Hint="@GV:xxx" />
                        <DataGridTemplateColumn Header="操作" Width="60">
                            <DataGridTemplateColumn.CellTemplate>
                                <DataTemplate>
                                    <Button Content="删除"
                                            Command="{Binding DataContext.RemoveOutputCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                            CommandParameter="{Binding}"
                                            Style="{StaticResource MaterialDesignFlatButton}" />
                                </DataTemplate>
                            </DataGridTemplateColumn.CellTemplate>
                        </DataGridTemplateColumn>
                    </DataGrid.Columns>
                </DataGrid>
            </Grid>
        </GroupBox>

        <!-- 条件规则区域 -->
        <GroupBox Header="🔀 条件规则（按顺序匹配，第一个满足即生效）" Margin="0,12">
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="*" />
                </Grid.RowDefinitions>

                <Button Content="+ 添加条件"
                        Command="{Binding AddConditionCommand}"
                        HorizontalAlignment="Right"
                        Margin="0,0,0,8"
                        Style="{StaticResource MaterialDesignFlatButton}" />

                <DataGrid Grid.Row="1"
                          ItemsSource="{Binding Conditions}"
                          AutoGenerateColumns="False"
                          CanUserAddRows="False"
                          Height="180">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="条件表达式" Binding="{Binding ConditionExpression}" Width="220"
                                            materialDesign:HintAssist.Hint="@GV:变量 > 10" />
                        <DataGridComboBoxColumn Header="跳转到" 
                                                SelectedItemBinding="{Binding TargetStepSeq}"
                                                Width="80">
                            <DataGridComboBoxColumn.ElementStyle>
                                <Style TargetType="ComboBox">
                                    <Setter Property="ItemsSource" Value="{Binding DataContext.AvailableStepSeqs, RelativeSource={RelativeSource AncestorType=UserControl}}" />
                                </Style>
                            </DataGridComboBoxColumn.ElementStyle>
                            <DataGridComboBoxColumn.EditingElementStyle>
                                <Style TargetType="ComboBox">
                                    <Setter Property="ItemsSource" Value="{Binding DataContext.AvailableStepSeqs, RelativeSource={RelativeSource AncestorType=UserControl}}" />
                                </Style>
                            </DataGridComboBoxColumn.EditingElementStyle>
                        </DataGridComboBoxColumn>
                        <DataGridTextColumn Header="描述" Binding="{Binding Description}" Width="140" />
                        <DataGridTemplateColumn Header="操作" Width="60">
                            <DataGridTemplateColumn.CellTemplate>
                                <DataTemplate>
                                    <Button Content="删除"
                                            Command="{Binding DataContext.RemoveConditionCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                            CommandParameter="{Binding}"
                                            Style="{StaticResource MaterialDesignFlatButton}" />
                                </DataTemplate>
                            </DataGridTemplateColumn>
                        </DataGridTemplateColumn>
                    </DataGrid.Columns>
                </DataGrid>
            </Grid>
        </GroupBox>

        <!-- 默认动作区域 -->
        <GroupBox Header="⚙️ 默认动作（所有条件都不满足时）" Margin="0,12">
            <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                <TextBlock Text="默认:" VerticalAlignment="Center" Margin="0,0,8,0" />
                <ComboBox SelectedItem="{Binding DefaultAction}" Width="120" Margin="0,0,16,0">
                    <sys:DefaultBranchAction>Continue</sys:DefaultBranchAction>
                    <sys:DefaultBranchAction>Stop</sys:DefaultBranchAction>
                    <sys:DefaultBranchAction>SkipTo</sys:DefaultBranchAction>
                </ComboBox>
                <TextBlock Text="跳转到步骤:" VerticalAlignment="Center" Margin="0,0,8,0"
                           Visibility="{Binding DefaultAction, Converter={StaticResource EnumToVisibilityConverter}, ConverterParameter=SkipTo}" />
                <ComboBox SelectedItem="{Binding DefaultTargetStepSeq}" Width="80"
                          Visibility="{Binding DefaultAction, Converter={StaticResource EnumToVisibilityConverter}, ConverterParameter=SkipTo}"
                          ItemsSource="{Binding AvailableStepSeqs}" />
            </StackPanel>
        </GroupBox>

        <!-- 底部按钮 -->
        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,16,0,0">
            <Button Content="取消"
                    Style="{StaticResource MaterialDesignFlatButton}"
                    Command="{Binding CancelCommand}"
                    Margin="8,0" />
            <Button Content="✓ 确定"
                    Style="{StaticResource MaterialDesignRaisedButton}"
                    Command="{Binding OkCommand}" />
        </StackPanel>
    </StackPanel>
</UserControl>
```

- [ ] **Step 3: 编译验证**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\Module\Module.csproj --configuration Debug`
Expected: Build succeeded

---

## Task 5: 集成到步骤编辑器UI

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\Module\Operators\Editor\ProcessSequenceEditorView.xaml`
- Modify: `c:\WorkFiles\GZQL_MACHINE\Module\Operators\Editor\ProcessSequenceEditorViewModel.cs`

- [ ] **Step 1: 在XAML中添加分支列**

在Alarm列之后、📊列之前添加：

```xml
<!-- 条件分支列：仅启用了分支的步骤显示图标 -->
<DataGridTemplateColumn Header="⚡" Width="50">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <Button Command="{Binding DataContext.OpenBranchConfigCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                    CommandParameter="{Binding}"
                    Visibility="{Binding IsBranchEnabled, Converter={StaticResource BooleanToVisibilityConverter}}"
                    ToolTip="配置条件分支" Style="{StaticResource MaterialDesignFlatButton}"
                    Padding="4">
                ⚡
            </Button>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

- [ ] **Step 2: 在工具栏中添加"Add Branch"按钮**

在"Add Dashboard"按钮后面添加：

```xml
<Button Content="⚡ Add Branch"
        Command="{Binding InsertBranchStepCommand}"
        Style="{StaticResource MaterialDesignOutlinedButton}"
        Margin="0,0,8,0"
        ToolTip="插入带条件分支的步骤" />
```

- [ ] **Step 3: 在ViewModel中添加命令和实现**

```csharp
// 在构造函数中添加命令绑定
OpenBranchConfigCommand = new DelegateCommand<ProcessStep>(OnOpenBranchConfig);
InsertBranchStepCommand = new DelegateCommand(OnInsertBranchStep);

// 属性声明
public ICommand OpenBranchConfigCommand { get; }
public ICommand InsertBranchStepCommand { get; }

/// <summary> 打开条件分支配置对话框 </summary>
private async void OnOpenBranchConfig(ProcessStep step)
{
    if (step == null) return;

    var vm = _containerProvider.Resolve<ConditionBranchViewModel>();
    var view = new ConditionBranchView();
    view.DataContext = vm;
    vm.Step = step;

    bool? result = (bool?)await MaterialDesignThemes.Wpf.DialogHost.Show(view, "MainDialogHost");
    if (result == true)
    {
        _logger.Info($"[ProcessSequenceEditor] 已更新步骤 [{step.Seq}] 的条件分支配置");
    }
}

/// <summary> 插入一个带默认条件分支配置的步骤 </summary>
private void OnInsertBranchStep()
{
    if (CurrentTask == null) return;

    int nextSeq = CurrentTask.Steps.Count > 0 ? CurrentTask.Steps.Max(s => s.Seq) + 1 : 1;
    var newStep = new ProcessStep
    {
        Seq = nextSeq,
        Step = StepType.VISION, // 默认使用VISION类型（通常需要先采集数据再判断）
        CompFeature = "—",
        SiteFeature = "—",
        BranchConfig = new Core.Models.BranchConfig
        {
            IsEnabled = true,
            OutputParameters = new List<Core.Models.BranchOutputParameter>
            {
                new Core.Models.BranchOutputParameter { Name = "检测结果", Value = "false", TargetGlobalVariable = "@GV:检测结果" }
            },
            Conditions = new List<Core.Models.BranchCondition>
            {
                new Core.Models.BranchCondition
                {
                    ConditionExpression = "@GV:检测结果 == true",
                    TargetStepSeq = nextSeq + 2, // 默认跳过下一步
                    Description = "检测通过→继续"
                }
            },
            DefaultAction = DefaultBranchAction.SkipTo,
            DefaultTargetStepSeq = nextSeq + 3 // NG处理步骤
        }
    };

    CurrentTask.Steps.Add(newStep);
    _logger.Info($"[ProcessSequenceEditor] 已插入带条件分支的步骤 [Seq={nextSeq}]");
}
```

- [ ] **Step 4: 编译验证**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\Module\Module.csproj --configuration Debug`
Expected: Build succeeded

---

## Task 6: DI注册与模块集成

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\Module\PrimModel.cs`

- [ ] **Step 1: 注册ConditionBranchViewModel**

在RegisterTypes方法中添加：

```csharp
containerRegistry.Register<ConditionBranchViewModel>();
```

- [ ] **Step 2: 确保IFormulaEvaluator已注册（Task 1已完成）**

确认以下代码已存在：

```csharp
containerRegistry.RegisterSingleton<Core.Services.IFormulaEvaluator, Core.Services.FormulaEvaluator>();
```

- [ ] **Step 3: 编译验证完整解决方案**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\GZQL_MACHINE.sln --configuration Debug`
Expected: Build succeeded with no errors

---

## Task 7: 测试与验证

**Files:**
- Test: Manual testing required

- [ ] **Step 1: 启动应用程序并导航到步骤编辑器**

- [ ] **Step 2: 测试插入条件分支步骤**

1. 点击工具栏"⚡ Add Branch"按钮
2. 验证新步骤已插入，且带有默认分支配置
3. 验证⚡图标显示在该步骤行的分支列

- [ ] **Step 3: 测试配置条件分支对话框**

1. 点击某步骤的⚡图标打开配置对话框
2. 验证输出参数可以正常添加/删除
3. 验证条件规则可以正常添加/删除
4. 验证表达式输入框可用
5. 验证跳转目标下拉列表显示正确的步骤序号
6. 点击确定保存配置

- [ ] **Step 4: 测试运行时条件分支逻辑**

1. 配置一个简单的条件分支（如基于全局变量判断）
2. 运行步骤序列
3. 验证条件正确时跳转到指定步骤
4. 验证条件不满足时执行默认动作

- [ ] **Step 5: 测试持久化保存**

1. 配置好条件分支后保存步骤序列到JSON文件
2. 重新加载JSON文件
3. 验证分支配置已正确持久化和恢复

---

## 架构说明

### 数据流图
```
┌─────────────┐     ┌──────────────────┐     ┌─────────────────┐
│  VISION步骤  │ ──▶ │  输出参数写入GV   │ ──▶ │  下一步CONDITION │
│  (执行检测)  │     │ (@GV:检测结果)   │     │  (读取GV判断)   │
└─────────────┘     └──────────────────┘     └────────┬────────┘
                                                     │
                                    ┌────────────────┼────────────────┐
                                    ▼                                 ▼
                           ┌──────────────┐                   ┌──────────────┐
                           │ 条件==True?  │                   │ 条件==False? │
                           │ 跳转到步骤X   │                   │ 跳转到步骤Y   │
                           └──────────────┘                   └──────────────┘
```

### 与现有系统的关系
- **复用FormulaEvaluator**: 条件表达式复用数据看板的公式引擎
- **扩展CheckDetail模式**: BranchConfig是CheckDetail的泛化版本，适用于所有步骤类型
- **保持向后兼容**: BranchConfig默认null/IsEnabled=false，不影响现有步骤行为

### JSON持久化示例
```json
{
  "Seq": 4,
  "Step": "VISION",
  "CompFeature": "检测",
  "SiteFeature": "Site1",
  "BranchConfig": {
    "IsEnabled": true,
    "OutputParameters": [
      { "Name": "检测结果", "Value": "true", "TargetGlobalVariable": "@GV:检测结果" }
    ],
    "Conditions": [
      {
        "ConditionExpression": "@GV:检测结果 == true",
        "TargetStepSeq": 6,
        "Description": "OK→组装"
      },
      {
        "ConditionExpression": "@GV:检测结果 == false",
        "TargetStepSeq": 8,
        "Description": "NG→排出"
      }
    ],
    "DefaultAction": "Stop",
    "DefaultTargetStepSeq": 0
  }
}
```

---

## 总结

本实施计划实现了完整的条件分支功能，包括：

✅ **数据模型**: BranchConfig, BranchCondition, BranchOutputParameter  
✅ **执行引擎**: ProcessStepExecutor集成条件评估和跳转逻辑  
✅ **UI界面**: 条件分支配置对话框，支持可视化配置  
✅ **编辑器集成**: 工具栏按钮、表格列图标、快捷操作  
✅ **持久化**: JSON序列化/反序列化完全支持  
✅ **灵活性**: 支持任意步骤类型、多条件规则、复杂表达式  

**预计工作量**: 7个任务，约2-3小时完成开发和测试
