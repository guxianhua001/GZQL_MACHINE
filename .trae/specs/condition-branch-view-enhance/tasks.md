# ConditionBranchView 功能优化实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 优化 ConditionBranchView 的输出参数链接、条件表达式编辑、语法校验、拖拽排序和列宽自适应功能

**Architecture:** 采用轻量内联构建器方案，在现有 DataGrid 内嵌增强编辑能力（变量插入 Popup、语法校验错误图标、拖拽排序），校验逻辑独立为 ExpressionValidator 静态工具类，ViewModel 新增校验状态管理和移动命令

**Tech Stack:** WPF + PRISM 9 + MaterialDesignInXAML + .NET 9.0

---

## Task 1: 新增多语言资源键

**Files:**
- Modify: `MainApp/Languages/Strings.zh-CN.xaml` (在 BranchDetail_Ok 键之后追加)
- Modify: `MainApp/Languages/Strings.en-US.xaml` (在 BranchDetail_Ok 键之后追加)

- [ ] **Step 1: 在 Strings.zh-CN.xaml 末尾（</ResourceDictionary> 前）添加新资源键**

在 `BranchDetail_Ok` 行（约第2137行）之后、`</ResourceDictionary>` 标签之前插入：

```xml
    <sys:String x:Key="BranchDetail_InsertVar">插入变量</sys:String>
    <sys:String x:Key="BranchDetail_SelectPrevOutput">选择前序输出参数</sys:String>
    <sys:String x:Key="BranchDetail_MoveUp">上移</sys:String>
    <sys:String x:Key="BranchDetail_MoveDown">下移</sys:String>
    <sys:String x:Key="BranchDetail_Column_Actions">操作</sys:String>
    <sys:String x:Key="BranchDetail_ExprError_Format">表达式错误: {0}</sys:String>
    <sys:String x:Key="BranchDetail_ValidateError_Msg">第 {0} 行条件表达式存在错误，请修正后保存</sys:String>
```

- [ ] **Step 2: 在 Strings.en-US.xaml 末尾添加对应的英文资源键**

在 `BranchDetail_Ok` 行（约第1752行）之后插入：

```xml
    <sys:String x:Key="BranchDetail_InsertVar">Insert Var</sys:String>
    <sys:String x:Key="BranchDetail_SelectPrevOutput">Select Prev Output</sys:String>
    <sys:String x:Key="BranchDetail_MoveUp">Move Up</sys:String>
    <sys:String x:Key="BranchDetail_MoveDown">Move Down</sys:String>
    <sys:String x:Key="BranchDetail_Column_Actions">Actions</sys:String>
    <sys:String x:Key="BranchDetail_ExprError_Format">Expression Error: {0}</sys:String>
    <sys:String x:Key="BranchDetail_ValidateError_Msg">Row {0} has expression errors, please fix before saving</sys:String>
```

- [ ] **Step 3: 验证编译通过**

Run: `dotnet build GZQL_MACHINE.sln --configuration Debug`
Expected: Build succeeded, 无 XAML 解析错误

---

## Task 2: 创建 ExpressionValidator 校验工具类

**Files:**
- Create: `Core/Utilities/ExpressionValidator.cs`

- [ ] **Step 1: 创建 ExpressionValidator.cs 文件并实现基础校验逻辑**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Core.Utilities
{
    /// <summary>
    /// 条件表达式语法校验器
    /// 支持变量引用 (@Output:xxx, @GV:xxx)、比较运算符、逻辑运算符的合法性检查
    /// 用于 ConditionBranchView 的条件规则编辑时实时校验
    /// </summary>
    public static class ExpressionValidator
    {
        private static readonly HashSet<string> ValidOperators = new(StringComparer.OrdinalIgnoreCase)
        { "==", "!=", ">", "<", ">=", "<=", "&&", "||", "+", "-", "*", "/", "!", "(", ")" };

        private static readonly Regex VariablePattern = new(@"@(Output|GV):[\w\u4e00-\u9fa5]+",
            RegexOptions.Compiled);

        /// <summary>
        /// 校验表达式合法性
        /// 返回空字符串表示校验通过，否则返回错误信息
        /// </summary>
        /// <param name="expression">待校验的条件表达式</param>
        /// <param name="availableVariables">可用的变量名列表（来自 PreviousStepOutputNames）</param>
        /// <returns>空字符串表示通过，非空为错误描述</returns>
        public static string Validate(string expression, IEnumerable<string> availableVariables)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return "表达式不能为空";

            var varSet = availableVariables?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

            // 1. 提取所有变量引用并检查是否存在
            var referencedVars = VariablePattern.Matches(expression)
                .Select(m => m.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var v in referencedVars)
            {
                if (!varSet.Contains(v))
                    return $"未知变量: {v}";
            }

            // 2. 检查括号匹配
            int depth = 0;
            for (int i = 0; i < expression.Length; i++)
            {
                if (expression[i] == '(') depth++;
                else if (expression[i] == ')')
                {
                    depth--;
                    if (depth < 0) return $"位置 {i}: 多余的右括号";
                }
            }
            if (depth > 0) return "括号不匹配";

            // 3. 检查非法字符（替换变量后检查剩余操作符）
            string sanitized = VariablePattern.Replace(expression, "VAR");
            foreach (char c in sanitized.Where(ch => !Char.IsLetterOrDigit(ch) && !Char.IsWhiteSpace(ch)))
            {
                string token = c.ToString();
                if (!ValidOperators.Contains(token))
                    return $"非法字符: '{c}'";
            }

            return string.Empty;
        }
    }
}
```

- [ ] **Step 2: 验证编译通过**

Run: `dotnet build Core/Core.csproj --configuration Debug`
Expected: Build succeeded

---

## Task 3: ViewModel 基础增强 — 属性与命令

**Files:**
- Modify: `Module/Controls/StepDetails/ConditionBranchViewModel.cs`

- [ ] **Step 1: 添加 using 语句**

在文件顶部 using 区域添加：

```csharp
using Core.Utilities;
using System.Windows.Threading;
```

- [ ] **Step 2: 新增 CommonParameterValues 属性**

在 `PreviousStepOutputNames` 属性声明之后（约第154行后）添加：

```csharp
/// <summary> 值列预设常用值选项（true/false/0/1）</summary>
public List<string> CommonParameterValues { get; } = new() { "true", "false", "0", "1" };
```

- [ ] **Step 3: 新增 ConditionErrors 字典和 HasValidationErrors 属性**

在 CommonParameterValues 之后添加：

```csharp
/// <summary> 条件表达式的校验错误字典 Key=条件对象, Value=错误信息(空=通过)</summary>
public Dictionary<BranchCondition, string> ConditionErrors { get; } = new();

/// <summary> 是否存在校验错误（用于禁用确定按钮和保存拦截）</summary>
public bool HasValidationErrors => ConditionErrors.Values.Any(v => !string.IsNullOrEmpty(v));
```

- [ ] **Step 4: 新增移动命令属性**

在现有命令声明区域（约第157-162行）添加：

```csharp
public ICommand MoveUpCommand { get; }
public ICommand MoveDownCommand { get; }
```

- [ ] **Step 5: 在构造函数中初始化新增命令**

在构造函数中现有命令初始化之后（约第55行后）添加：

```csharp
MoveUpCommand = new DelegateCommand<BranchCondition>(OnMoveUp);
MoveDownCommand = new DelegateCommand<BranchCondition>(OnMoveDown);
```

- [ ] **Step 6: 新增校验方法**

在 `LoadPreviousStepOutputs` 方法之后（约第286行后）添加：

```csharp
/// <summary> 校验单个条件表达式并更新错误字典</summary>
public void ValidateCondition(BranchCondition condition)
{
    if (condition == null) return;
    string error = ExpressionValidator.Validate(condition.ConditionExpression, PreviousStepOutputNames);
    ConditionErrors[condition] = error;
    RaisePropertyChanged(nameof(HasValidationErrors));
}

/// <summary> 校验所有条件表达式</summary>
public void ValidateAllConditions()
{
    foreach (var cond in Conditions)
        ValidateCondition(cond);
}
```

- [ ] **Step 7: 新增移动方法**

在 ValidateAllConditions 方法之后添加：

```csharp
/// <summary> 条件规则上移一位</summary>
private void OnMoveUp(BranchCondition condition)
{
    int idx = Conditions.IndexOf(condition);
    if (idx <= 0) return;
    Conditions.Move(idx, idx - 1);
}

/// <summary> 条件规则下移一位</summary>
private void OnMoveDown(BranchCondition condition)
{
    int idx = Conditions.IndexOf(condition);
    if (idx < 0 || idx >= Conditions.Count - 1) return;
    Conditions.Move(idx, idx + 1);
}

/// <summary> 拖拽排序：将 draggedItem 移动到 targetItem 的位置</summary>
public void MoveCondition(BranchCondition draggedItem, BranchCondition targetItem)
{
    int fromIdx = Conditions.IndexOf(draggedItem);
    int toIdx = Conditions.IndexOf(targetItem);
    if (fromIdx < 0 || toIdx < 0 || fromIdx == toIdx) return;
    Conditions.RemoveAt(fromIdx);
    int insertIdx = Conditions.IndexOf(targetItem);
    Conditions.Insert(insertIdx, draggedItem);
}
```

- [ ] **Step 8: 修改 OnOk 方法增加全量校验拦截**

将 OnOk 方法（约第323行开始）修改为：

```csharp
/// <summary> 确认保存：先校验所有条件表达式，通过后才写回配置</summary>
private void OnOk()
{
    if (_step == null) return;

    // 全量校验条件表达式
    ValidateAllConditions();
    if (HasValidationErrors)
    {
        var errorRows = ConditionErrors
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .Select((kv, i) => Conditions.IndexOf(kv.Key) + 1);
        _logger?.Warning($"[ConditionBranch] 存在表达式错误，无法保存: 行 [{string.Join(", ", errorRows)}]");
        return;
    }

    // 深拷贝条件规则列表，避免对象引用导致的数据绑定未提交问题
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

    try
    {
        var session = MaterialDesignThemes.Wpf.DialogHost.GetDialogSession(DialogIdentifier);
        session?.Close(true);
    }
    catch (InvalidOperationException) { }
}
```

- [ ] **Step 9: 验证编译通过**

Run: `dotnet build Module/Module.csproj --configuration Debug`
Expected: Build succeeded, 无编译错误

---

## Task 4: 输出参数区域 XAML 改造

**Files:**
- Modify: `Module/Controls/StepDetails/ConditionBranchView.xaml`

- [ ] **Step 1: 修改输出参数 DataGrid 列宽和参数名列绑定**

将第78-119行的 `<DataGrid ...>` 及其列定义整体替换为：

```xml
                            <DataGrid ItemsSource="{Binding OutputParameters}"
                                      AutoGenerateColumns="False"
                                      CanUserAddRows="False"
                                      CanUserDeleteRows="False"
                                      CanUserResizeColumns="True"
                                      materialDesign:DataGridAssist.CellPadding="4"
                                      MaxHeight="200">
                                <DataGrid.Columns>
                                    <DataGridTemplateColumn Header="{lang:Lang BranchDetail_Column_ParamName}"
                                                            Width="Auto" MinWidth="180">
                                        <DataGridTemplateColumn.CellTemplate>
                                            <DataTemplate>
                                                <TextBlock Text="{Binding Name}" VerticalAlignment="Center" />
                                            </DataTemplate>
                                        </DataGridTemplateColumn.CellTemplate>
                                        <DataGridTemplateColumn.CellEditingTemplate>
                                            <DataTemplate>
                                                <ComboBox ItemsSource="{Binding DataContext.PreviousStepOutputNames, RelativeSource={RelativeSource AncestorType=UserControl}}"
                                                          SelectedItem="{Binding Name, UpdateSourceTrigger=PropertyChanged}"
                                                          IsEditable="False"
                                                          materialDesign:HintAssist.Hint="{lang:Lang BranchDetail_SelectPrevOutput}" />
                                            </DataTemplate>
                                        </DataGridTemplateColumn.CellEditingTemplate>
                                    </DataGridTemplateColumn>

                                    <DataGridTemplateColumn Header="{lang:Lang BranchDetail_Column_Value}"
                                                            Width="100" MinWidth="80">
                                        <DataGridTemplateColumn.CellTemplate>
                                            <DataTemplate>
                                                <TextBlock Text="{Binding Value}" VerticalAlignment="Center" />
                                            </DataTemplate>
                                        </DataGridTemplateColumn.CellTemplate>
                                        <DataGridTemplateColumn.CellEditingTemplate>
                                            <DataTemplate>
                                                <ComboBox ItemsSource="{Binding DataContext.CommonParameterValues, RelativeSource={RelativeSource AncestorType=UserControl}}"
                                                          Text="{Binding Value, UpdateSourceTrigger=PropertyChanged}"
                                                          IsEditable="True" />
                                            </DataTemplate>
                                        </DataGridTemplateColumn.CellEditingTemplate>
                                    </DataGridTemplateColumn>

                                    <DataGridTemplateColumn Header="{lang:Lang BranchDetail_Column_TargetGlobalVar}"
                                                            Width="*" MinWidth="150">
                                        <DataGridTemplateColumn.CellTemplate>
                                            <DataTemplate>
                                                <TextBlock Text="{Binding TargetGlobalVariable}" VerticalAlignment="Center" />
                                            </DataTemplate>
                                        </DataGridTemplateColumn.CellTemplate>
                                        <DataGridTemplateColumn.CellEditingTemplate>
                                            <DataTemplate>
                                                <ComboBox ItemsSource="{Binding DataContext.GlobalVariableNames, RelativeSource={RelativeSource AncestorType=UserControl}}"
                                                          SelectedItem="{Binding TargetGlobalVariable, UpdateSourceTrigger=PropertyChanged}"
                                                          IsEditable="True" />
                                            </DataTemplate>
                                        </DataGridTemplateColumn.CellEditingTemplate>
                                    </DataGridTemplateColumn>
                                </DataGrid.Columns>
                            </DataGrid>
```

关键变更点：
- 参数名列：`ItemsSource` 从 `AvailableParamNames` 改为 `PreviousStepOutputNames`，`IsEditable` 从 `True` 改为 `False`
- 值列：从 `DataGridTextColumn` 改为 `DataGridTemplateColumn` + ComboBox 绑定 `CommonParameterValues`
- 目标全局变量列：宽度从固定 `180` 改为 `*` MinWidth=`150`
- 所有列均设置合理的 MinWidth 和 Width 模式
- DataGrid 增加 `CanUserResizeColumns="True"`

- [ ] **Step 2: 验证 XAML 编译通过**

Run: `dotnet build Module/Module.csproj --configuration Debug`
Expected: Build succeeded, 无 XAML 解析错误

---

## Task 5: 条件规则区域 XAML 改造 — 表达式内联编辑器

**Files:**
- Modify: `Module/Controls/StepDetails/ConditionBranchView.xaml`

- [ ] **Step 1: 替换条件规则 DataGrid 的列定义**

将第145-172行的条件规则 `<DataGrid ...>` 及其列定义整体替换为：

```xml
                            <DataGrid x:Name="ConditionsGrid"
                                      ItemsSource="{Binding Conditions}"
                                      AutoGenerateColumns="False"
                                      CanUserAddRows="False"
                                      CanUserDeleteRows="False"
                                      CanUserResizeColumns="True"
                                      materialDesign:DataGridAssist.CellPadding="4"
                                      MaxHeight="250"
                                      PreviewMouseLeftButtonDown="OnConditionsGrid_PreviewMouseLeftButtonDown"
                                      PreviewMouseMove="OnConditionsGrid_PreviewMouseMove"
                                      Drop="OnConditionsGrid_Drop"
                                      AllowDrop="True">
                                <DataGrid.Columns>
                                    <DataGridTemplateColumn Header="{lang:Lang BranchDetail_Column_ConditionExpr}"
                                                            Width="*" MinWidth="250">
                                        <DataGridTemplateColumn.CellTemplate>
                                            <DataTemplate>
                                                <DockPanel>
                                                    <TextBlock Text="{Binding ConditionExpression}"
                                                               VerticalAlignment="Center"
                                                               TextTrimming="CharacterEllipsis"
                                                               DockPanel.Dock="Left" />
                                                    <materialDesign:PackIcon Kind="AlertCircle"
                                                                             Foreground="#E53935"
                                                                             Width="16" Height="16"
                                                                             Visibility="{Binding DataContext.ConditionErrors[(sys:CurrentItem)], Converter={StaticResource NullToVisConverter}, FallbackValue=Collapsed}"
                                                                             ToolTip="{Binding DataContext.ConditionErrors[(sys:CurrentItem)], FallbackValue=''}"
                                                                             DockPanel.Dock="Right"
                                                                             Margin="4,0,0,0" />
                                                </DockPanel>
                                            </DataTemplate>
                                        </DataGridTemplateColumn.CellTemplate>
                                        <DataGridTemplateColumn.CellEditingTemplate>
                                            <DataTemplate>
                                                <Grid>
                                                    <Grid.ColumnDefinitions>
                                                        <ColumnDefinition Width="*" />
                                                        <ColumnDefinition Width="Auto" />
                                                    </Grid.ColumnDefinitions>

                                                    <TextBox Grid.Column="0"
                                                             x:Name="ExprTextBox"
                                                             Text="{Binding ConditionExpression, UpdateSourceTrigger=PropertyChanged}"
                                                             LostFocus="OnExprTextBox_LostFocus"
                                                             AcceptsReturn="False"
                                                             materialDesign:HintAssist.Hint="@Output:xxx > 10" />

                                                    <Button Grid.Column="1"
                                                            Margin="4,0,0,0"
                                                            Padding="6,2"
                                                            Click="OnInsertVarButtonClick"
                                                            VerticalAlignment="Center"
                                                            ToolTip="{lang:Lang BranchDetail_InsertVar}">
                                                        <StackPanel Orientation="Horizontal">
                                                            <materialDesign:PackIcon Kind="CodeTags" Width="14" Height="14" />
                                                            <TextBlock Text="{lang:Lang BranchDetail_InsertVar}" Margin="3,0,0,0" FontSize="11" />
                                                        </StackPanel>
                                                    </Button>

                                                    <Popup PlacementTarget="{Binding ElementName=ExprTextBox}"
                                                           Placement="Bottom"
                                                           StaysOpen="False"
                                                           x:Name="VarInsertPopup"
                                                           MaxHeight="200">
                                                        <ListBox ItemsSource="{Binding DataContext.PreviousStepOutputNames, RelativeSource={RelativeSource AncestorType=UserControl}}"
                                                                 SelectionChanged="OnVariableSelected"
                                                                 MinWidth="150" />
                                                    </Popup>
                                                </Grid>
                                            </DataTemplate>
                                        </DataGridTemplateColumn.CellEditingTemplate>
                                    </DataGridTemplateColumn>

                                    <DataGridTemplateColumn Header="{lang:Lang BranchDetail_Column_JumpTo}"
                                                            Width="90" MinWidth="70">
                                        <DataGridTemplateColumn.CellTemplate>
                                            <DataTemplate>
                                                <TextBlock Text="{Binding TargetStepSeq}" VerticalAlignment="Center" />
                                            </DataTemplate>
                                        </DataGridTemplateColumn.CellTemplate>
                                        <DataGridTemplateColumn.CellEditingTemplate>
                                            <DataTemplate>
                                                <ComboBox ItemsSource="{Binding DataContext.AvailableStepSeqs, RelativeSource={RelativeSource AncestorType=UserControl}}"
                                                          SelectedValue="{Binding TargetStepSeq, UpdateSourceTrigger=PropertyChanged}" />
                                            </DataTemplate>
                                        </DataGridTemplateColumn.CellEditingTemplate>
                                    </DataGridTemplateColumn>

                                    <DataGridTextColumn Header="{lang:Lang BranchDetail_Column_Description}"
                                                        Binding="{Binding Description}"
                                                        Width="120" MinWidth="100" />

                                    <DataGridTemplateColumn Header="{lang:Lang BranchDetail_Column_Actions}"
                                                            Width="Auto" MinWidth="60">
                                        <DataGridTemplateColumn.CellTemplate>
                                            <DataTemplate>
                                                <StackPanel Orientation="Horizontal">
                                                    <Button Command="{Binding DataContext.MoveUpCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"
                                                            CommandParameter="{Binding}"
                                                            ToolTip="{lang:Lang BranchDetail_MoveUp}"
                                                            Style="{StaticResource MaterialDesignIconButton}"
                                                            Width="24" Height="24"
                                                            Margin="0,0,2,0">
                                                        <materialDesign:PackIcon Kind="ArrowUp" Width="14" Height="14" />
                                                    </Button>
                                                    <Button Command="{Binding DataContext.MoveDownCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"
                                                            CommandParameter="{Binding}"
                                                            ToolTip="{lang:Lang BranchDetail_MoveDown}"
                                                            Style="{StaticResource MaterialDesignIconButton}"
                                                            Width="24" Height="24">
                                                        <materialDesign:PackIcon Kind="ArrowDown" Width="14" Height="14" />
                                                    </Button>
                                                </StackPanel>
                                            </DataTemplate>
                                        </DataGridTemplateColumn.CellTemplate>
                                    </DataGridTemplateColumn>
                                </DataGrid.Columns>
                            </DataGrid>
```

关键变更点：
- 条件表达式列：从 `DataGridTextColumn` 改为完整的内联编辑器（TextBox + 变量插入按钮 + Popup + 错误图标）
- 跳转目标列：宽度调整为 90px MinWidth 70px
- 描述列：宽度调整为 120px MinWidth 100px
- **新增操作列**：包含上移(↑)/下移(↓)按钮
- DataGrid 增加拖拽事件绑定和 `AllowDrop="True"`
- DataGrid 命名为 `ConditionsGrid` 以便 code-behind 引用

注意：CellTemplate 中使用 `{x:Null}` 作为 fallback 或使用 converter 处理 ConditionErrors 字典查找。如果 `NullToVisConverter` 不存在于项目中，需改用 DataTrigger 方式或创建简单 converter。

- [ ] **Step 2: 确认 NullToVisConverter 是否存在**

搜索项目中是否已有 NullToVisibilityConverter：
- 如果已存在则无需额外处理
- 如果不存在，需要在 Task 5 的 Step 3 中补充创建或改用 DataTrigger 替代方案

- [ ] **Step 3: 验证 XAML 编译通过**

Run: `dotnet build Module/Module.csproj --configuration Debug`
Expected: Build succeeded

---

## Task 6: Code-Behind 事件处理实现

**Files:**
- Modify: `Module/Controls/StepDetails/ConditionBranchView.xaml.cs`

- [ ] **Step 1: 添加必要的 using 语句**

在文件顶部添加：

```csharp
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
```

- [ ] **Step 2: 添加拖拽相关私有字段**

在类中（构造函数之前）添加：

```csharp
private bool _isDragging;
private BranchCondition _draggedItem;
private DispatcherTimer _validateTimer;
```

- [ ] **Step 3: 在构造函数中初始化防抖定时器**

在 `InitializeComponent();` 之后添加：

```csharp
_validateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
_validateTimer.Tick += (s, e) =>
{
    _validateTimer.Stop();
    if (DataContext is ViewModels.ConditionBranchViewModel vm)
    {
        // 找到当前焦点所在的 TextBox 所在的行数据
        var focused = FocusManager.GetFocusedElement(this) as TextBox;
        if (focused != null && focused.DataContext is BranchCondition cond)
            vm.ValidateCondition(cond);
    }
};
```

- [ ] **Step 4: 实现变量插入按钮点击事件**

在 `OnOkClick` 方法之前添加：

```csharp
/// <summary> 变量插入按钮点击：打开 Popup 显示可用变量列表</summary>
private void OnInsertVarButtonClick(object sender, RoutedEventArgs e)
{
    if (sender is not Button btn) return;

    // 找到同单元格中的 Popup
    var parent = VisualTreeHelper.GetParent(btn);
    while (parent != null && parent is not Grid)
        parent = VisualTreeHelper.GetParent(parent);

    if (parent is Grid grid)
    {
        var popup = grid.Children.OfType<Popup>().FirstOrDefault();
        popup?.IsOpen = true;
    }
}
```

- [ ] **Step 5: 实现 Popup 变量选择事件**

在 OnInsertVarButtonClick 之后添加：

```csharp
/// <summary> Popup 变量列表选中：将变量名插入到 TextBox 光标位置</summary>
private void OnVariableSelected(object sender, SelectionChangedEventArgs e)
{
    if (sender is not ListBox listBox || listBox.SelectedItem is not string selectedVar)
        return;
    if (string.IsNullOrEmpty(selectedVar)) return;

    // 找到同一 Grid 中的 TextBox
    var parentGrid = VisualTreeHelper.GetParent(listBox);
    while (parentGrid != null && parentGrid is not Grid)
        parentGrid = VisualTreeHelper.GetParent(parentGrid);

    if (parentGrid is Grid grid && grid.Children[0] is TextBox textBox)
    {
        int caretIndex = textBox.CaretIndex;
        string current = textBox.Text ?? "";
        textBox.Text = current.Insert(caretIndex, selectedVar);
        textBox.CaretIndex = caretIndex + selectedVar.Length;
        textBox.Focus();
    }

    listBox.SelectedItem = null;

    // 关闭 Popup
    var popup = VisualTreeHelper.GetParent(listBox) as Popup;
    if (popup != null) popup.IsOpen = false;
}
```

- [ ] **Step 6: 实现表达式 TextBox 失焦校验事件**

在 OnVariableSelected 之后添加：

```csharp
/// <summary> 表达式 TextBox 失焦时触发防抖校验</summary>
private void OnExprTextBox_LostFocus(object sender, RoutedEventArgs e)
{
    if (sender is not TextBox textBox || textBox.DataContext is not BranchCondition cond) return;
    _validateTimer.Stop();
    _validateTimer.Start();

    if (DataContext is ViewModels.ConditionBranchViewModel vm)
    {
        vm.ValidateCondition(cond);
    }
}
```

- [ ] **Step 7: 实现拖拽排序事件处理组**

在 OnExprTextBox_LostFocus 之后添加三个拖拽方法：

```csharp
/// <summary> 条件规则 DataGrid 鼠标按下：记录拖拽起始项</summary>
private void OnConditionsGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    if (sender is not DataGrid dg || dg.SelectedItem is not BranchCondition item) return;
    _draggedItem = item;
    _isDragging = false;
}

/// <summary> 条件规则 DataGrid 鼠标移动：启动拖拽操作</summary>
private void OnConditionsGrid_PreviewMouseMove(object sender, MouseEventArgs e)
{
    if (_draggedItem == null || e.LeftButton != MouseButtonState.Pressed) return;

    if (!_isDragging)
    {
        _isDragging = true;
        DragDrop.DoDragDrop((DataGrid)sender, _draggedItem, DragDropEffects.Move);
    }
}

/// <summary> 条件规则 DataGrid 放置：执行排序</summary>
private void OnConditionsGrid_Drop(object sender, DragEventArgs e)
{
    if (_draggedItem == null) return;

    // 获取放置目标位置的元素
    var dropPos = e.GetPosition((UIElement)sender);
    var targetElement = (UIElement)sender.InputHitTest(dropPos);

    // 向上查找 DataGridRow
    var targetRow = FindParent<DataGridRow>(targetElement);
    if (targetRow?.DataContext is not BranchCondition targetItem || targetItem == _draggedItem)
    {
        _draggedItem = null;
        _isDragging = false;
        return;
    }

    if (DataContext is ViewModels.ConditionBranchViewModel vm)
    {
        vm.MoveCondition(_draggedItem, targetItem);
    }

    _draggedItem = null;
    _isDragging = false;
}

/// <summary> 可视化树向上查找指定类型的父元素</summary>
private static T FindParent<T>(DependencyObject child) where T : DependencyObject
{
    while (child != null)
    {
        if (child is T result) return result;
        child = VisualTreeHelper.GetParent(child);
    }
    return null;
}
```

- [ ] **Step 8: 验证编译通过**

Run: `dotnet build Module/Module.csproj --configuration Debug`
Expected: Build succeeded, 无编译错误

---

## Task 7: 集成测试与验证

**Files:**
- No new files (manual verification)

- [ ] **Step 1: 全量编译验证**

Run: `dotnet build GZQL_MACHINE.sln --configuration Debug`
Expected: Build succeeded, 0 errors, 0 warnings related to changed files

- [ ] **Step 2: 运行应用程序手动验证功能清单**

启动应用后进入步骤序列器，打开任意步骤的条件分支配置对话框，逐项验证：

| # | 验证项 | 操作步骤 | 预期结果 |
|---|--------|----------|----------|
| 1 | 输出参数名下拉 | 点击参数名列进入编辑模式 | ComboBox 显示前序步骤输出参数列表（@Output:xxx / @GV:xxx 格式） |
| 2 | 选择参数名 | 从下拉列表选择一项 | 参数名正确写入，不可手输 |
| 3 | 值列下拉 | 点击值列进入编辑模式 | ComboBox 显示 true/false/0/1，可选择也可自定义输入 |
| 4 | 目标全局变量列宽 | 观察目标全局变量列 | 列宽自动占据剩余空间 |
| 5 | 条件表达式输入 | 双击表达式单元格进入编辑 | TextBox 可正常输入文本 |
| 6 | 变量插入功能 | 点击「插入变量」按钮 | Popup 弹出显示变量列表 |
| 7 | 变量插入到位 | 在 Popup 中选择一个变量 | 变量名插入到光标位置 |
| 8 | 语法校验-非法变量 | 输入 `@GV:不存在的变量 > 10` 并离开焦点 | 行尾显示红色警告图标，Tooltip 显示「未知变量」 |
| 9 | 语法校验-空表达式 | 清空表达式内容 | 显示错误提示 |
| 10 | 语法校验-合法表达式 | 输入 `@Output:步骤3_检测结果 == true` | 无错误图标显示 |
| 11 | 上移/下移按钮 | 点击操作列的 ↑ 或 ↓ 按钮 | 对应条件行向上/向下移动一位 |
| 12 | 拖拽排序 | 按住某行拖动到另一行位置 | 被拖拽行移动到目标位置 |
| 13 | 列宽调整 | 拖拽列头分隔线 | 列宽跟随鼠标调整 |
| 14 | 保存拦截-有错 | 输入非法表达式后点击确定 | 保存被拦截，不关闭对话框 |
| 15 | 保存成功-无错 | 修正所有错误后点击确定 | 配置保存成功，对话框关闭 |
| 16 | 中英文切换 | 切换界面语言 | 所有新增文本正确切换 |

- [ ] **Step 3: 检查版本修改记录**

确认 `版本修改记录.txt` 已更新（如项目要求）

---

## 实施顺序依赖关系

```
Task 1 (多语言资源)
   ↓
Task 2 (ExpressionValidator) ──┬──→ Task 3 (ViewModel 增强)
                               │          ↓
                               └──→ Task 4 (输出参数 XAML) ──→ Task 5 (条件规则 XAML) ──→ Task 6 (Code-Behind) ──→ Task 7 (集成测试)
```

Task 1 和 Task 2 无依赖关系，可并行执行。
Task 4 和 Task 5 可并行（但建议按顺序以便逐步验证）。

---

## 风险与注意事项

1. **NullToVisConverter 依赖**: 如果项目中不存在该转换器，Task 5 Step 1 中的错误图标可见性绑定需要改用 DataTrigger 或新建简单 converter
2. **DataGrid 编辑模式冲突**: 变量插入 Popup 可能与 DataGrid 的行编辑模式冲突，需确保 Popup 的 StaysOpen=False 且点击外部区域时正确关闭
3. **拖拽与按钮排序共存**: 同时实现了拖拽排序和按钮排序，两者功能重复但互补（拖拽适合批量调整，按钮适合微调），保留两者不影响功能
4. **防抖定时器生命周期**: DispatcherTimer 需确保在 UserControl 卸载时 Stop，避免内存泄漏（当前实现较简单，若后续发现问题可在 Unloaded 事件中清理）
