# ConditionBranchView 功能优化设计文档

## 1. 背景与目标

### 1.1 当前问题

ConditionBranchView（条件分支配置对话框）存在以下功能缺陷：

| # | 问题 | 严重程度 | 说明 |
|---|------|----------|------|
| 1 | **参数名无法链接前序步骤输出** | 🔴 高 | XAML 绑定 `AvailableParamNames` 但 ViewModel 未定义该属性，导致 ComboBox 无数据源 |
| 2 | **条件表达式无变量插入功能** | 🔴 高 | 纯文本 DataGridTextColumn，用户需手动敲入 `@Output:xxx` 格式，易出错 |
| 3 | **列宽固定不适应内容** | 🟡 中 | 列宽硬编码，过长内容被截断或过短列浪费空间 |
| 4 | **无表达式语法校验** | 🟡 中 | 非法表达式可在保存时写入，运行时才报错 |
| 5 | **条件规则无法调整优先级** | 🟡 中 | 条件按列表顺序匹配，但 UI 不支持拖拽排序 |
| 6 | **值列无常用选项** | 🟢 低 | 输出参数的值列为纯文本，未提供 true/false/0/1 快捷选项 |

### 1.2 优化目标

- ✅ 输出参数名可直接选择前序步骤的输出参数
- ✅ 条件表达式支持可视化变量插入和语法校验
- ✅ 列宽自适应内容且允许手动调整
- ✅ 条件规则支持拖拽/按钮调整优先级顺序
- ✅ 值列提供常用值下拉选项
- ✅ 符合工业控制软件快速响应性原则（内联编辑，无多余弹窗）

---

## 2. 设计方案：轻量内联构建器

### 2.1 方案选型

经对比三种方案后选定 **方案 A（轻量内联构建器）**：

- **核心思路**：在现有 DataGrid 内嵌增强编辑能力，不创建独立对话框
- **选型理由**：
  - 符合工业控制快速响应原则（减少交互层级）
  - 改动集中在现有文件，可控性好
  - 表达式格式简单（`@GV:xxx > 10`），无需完整 IDE 级别构建器
  - 复用已有代码基础（`OnConditionExprInsertBox_SelectionChanged` 方法框架已存在）

---

## 3. 详细设计

### 3.1 输出参数区域改造

#### 3.1.1 参数名列 — 链接前序步骤输出

**当前状态**：
```xml
<ComboBox ItemsSource="{Binding DataContext.AvailableParamNames, ...}" IsEditable="True" />
```
❌ `AvailableParamNames` 属性不存在于 ViewModel

**改造方案**：
```xml
<DataGridTemplateColumn Header="{lang:Lang BranchDetail_Column_ParamName}"
                        Width="Auto" MinWidth="180"
                        CanUserResize="True">
    <DataGridTemplateColumn.CellEditingTemplate>
        <DataTemplate>
            <ComboBox ItemsSource="{Binding DataContext.PreviousStepOutputNames,
                                RelativeSource={RelativeSource AncestorType=UserControl}}"
                      SelectedItem="{Binding Name, UpdateSourceTrigger=PropertyChanged}"
                      IsEditable="False"
                      materialDesign:HintAssist.Hint="{lang:Lang BranchDetail_SelectPrevOutput}" />
        </DataTemplate>
    </DataGridTemplateColumn.CellEditingTemplate>
</DataGridTemplateColumn>
```

**关键决策**：
- 直接绑定 `PreviousStepOutputNames`（不复用新属性）
- `IsEditable="False"` 强制从列表选择，避免拼写错误
- 数据源格式示例：`@Output:步骤3_检测结果`, `@GV:H2`, `@Output:步骤5_CheckResult`

#### 3.1.2 值列 — 下拉优化

**当前状态**：
```xml
<DataGridTextColumn Binding="{Binding Value}" Width="120" />
```

**改造方案**：
```xml
<DataGridTemplateColumn Header="{lang:Lang BranchDetail_Column_Value}"
                        Width="100" MinWidth="80"
                        CanUserResize="True">
    <DataGridTemplateColumn.CellEditingTemplate>
        <DataTemplate>
            <ComboBox ItemsSource="{Binding DataContext.CommonParameterValues,
                                RelativeSource={RelativeSource AncestorType=UserControl}}"
                      Text="{Binding Value, UpdateSourceTrigger=PropertyChanged}"
                      IsEditable="True"
                      IsReadOnly="False" />
        </DataTemplate>
    </DataGridTemplateColumn.CellEditingTemplate>
</DataGridTemplateColumn>
```

**预设常用值**（ViewModel 新增属性）：
```csharp
public List<string> CommonParameterValues { get; } = new() { "true", "false", "0", "1" };
```

#### 3.1.3 目标全局变量列 — 保持不变

当前实现已满足需求，仅调整列宽为 `Width="*"` 自适应剩余空间。

#### 3.1.4 输出参数区域列宽配置

| 列 | 宽度模式 | 最小宽度 | 可调整 |
|----|----------|----------|--------|
| 参数名 | Auto | 180px | ✅ |
| 值 | 固定 100px | 80px | ✅ |
| 目标全局变量 | * (占比) | 150px | ✅ |

---

### 3.2 条件规则区域改造

#### 3.2.1 条件表达式列 — 内联编辑器 + 变量插入

**当前状态**：
```xml
<DataGridTextColumn Binding="{Binding ConditionExpression}" Width="*" />
```
❌ 纯文本输入，无变量引用能力

**改造方案**：单元格水平布局为 TextBox + 变量插入按钮

```
┌──────────────────────────────────────────────────────────┬────┐
│ [TextBox: 表达式输入区 (Width="*")]                     │[▼] │
│                                                          │插入│
│ @Output:步骤3_检测结果 == true && @GV:H2 > 10           │变量│
└──────────────────────────────────────────────────────────┴────┘
```

**XAML 结构**：
```xml
<DataGridTemplateColumn Header="{lang:Lang BranchDetail_Column_ConditionExpr}"
                        Width="*" MinWidth="250"
                        CanUserResize="True">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <DockPanel>
                <!-- 左侧：表达式只读显示 -->
                <TextBlock Text="{Binding ConditionExpression}"
                           VerticalAlignment="Center"
                           DockPanel.Dock="Left" />
                <!-- 右侧：错误图标 -->
                <materialDesign:PackIcon Kind="AlertCircle"
                                         Foreground="#E53935"
                                         Visibility="{Binding DataContext.ConditionErrors[${this}], 
                                                     Converter={StaticResource NullToVisConverter}, ...}"
                                         ToolTip="{Binding DataContext.ConditionErrors[${this}], ...}"
                                         DockPanel.Dock="Right" />
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

                <!-- TextBox: 表达式输入 -->
                <TextBox Grid.Column="0"
                         Text="{Binding ConditionExpression, UpdateSourceTrigger=PropertyChanged}"
                         AcceptsReturn="False"
                         materialDesign:HintAssist.Hint="@Output:xxx > 10" />

                <!-- 变量插入按钮 -->
                <Button Grid.Column="1"
                        Margin="4,0,0,0"
                        Padding="4"
                        Click="OnInsertVarButtonClick"
                        Tag="{Binding RelativeSource={RelativeSource Self}}">
                    <StackPanel Orientation="Horizontal">
                        <materialDesign:PackIcon Kind="Variable" Width="14" Height="14" />
                        <TextBlock Text="{lang:Lang BranchDetail_InsertVar}" Margin="4,0,0,0" FontSize="11" />
                    </StackPanel>
                </Button>

                <!-- Popup: 变量列表 -->
                <Popup PlacementTarget="{Binding ElementName=...}"
                       Placement="Bottom"
                       StaysOpen="False"
                       x:Name="VarInsertPopup">
                    <ListBox ItemsSource="{Binding DataContext.PreviousStepOutputNames, ...}"
                             SelectionChanged="OnVariableSelected" />
                </Popup>
            </Grid>
        </DataTemplate>
    </DataGridTemplateColumn.CellEditingTemplate>
</DataGridTemplateColumn>
```

**交互流程**：
1. 用户双击表达式单元格 → 进入编辑模式
2. 用户直接在 TextBox 输入/修改表达式
3. 用户点击 [▼插入变量] 按钮 → Popup 显示 `PreviousStepOutputNames` 列表
4. 用户选择变量 → 变量名插入到 TextBox 光标位置
5. TextBox LostFocus 或 300ms 防抖 → 触发语法校验 → 更新错误图标显示

#### 3.2.2 语法校验机制

**新增工具类**：`Core/Utilities/ExpressionValidator.cs`

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
        public static string Validate(string expression, IEnumerable<string> availableVariables)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return "表达式不能为空";

            var varSet = availableVariables?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

            // 1. 提取所有变量引用
            var referencedVars = VariablePattern.Matches(expression)
                .Select(m => m.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 2. 检查变量是否存在
            foreach (var v in referencedVars)
            {
                if (!varSet.Contains(v))
                    return $"未知变量: {v}";
            }

            // 3. 检查括号匹配
            int depth = 0;
            for (int i = 0; i < expression.Length; i++)
            {
                if (expression[i] == '(') depth++;
                else if (expression[i] == ')') depth--;
                if (depth < 0) return $"位置 {i}: 多余的右括号";
            }
            if (depth > 0) return "括号不匹配";

            // 4. 检查非法字符（基本过滤）
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

**校验触发时机**：

| 时机 | 触发方式 | 用途 |
|------|----------|------|
| 实时校验 | TextBox LostFocus + 300ms 防抖 | 即时反馈错误 |
| 保存前校验 | OnOk() 方法中调用 ValidateAllConditions() | 阻止非法数据保存 |

**ViewModel 新增校验相关成员**：

```csharp
/// <summary> 条件表达式的校验错误字典 Key=条件对象, Value=错误信息(空=通过)</summary>
public Dictionary<BranchCondition, string> ConditionErrors { get; } = new();

/// <summary> 是否存在校验错误（用于禁用确定按钮）</summary>
public bool HasValidationErrors => ConditionErrors.Values.Any(v => !string.IsNullOrEmpty(v));

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

**UI 错误展示**：
- 行首或行尾显示红色警告图标 (`materialDesign:PackIcon Kind="AlertCircle"`)
- 图标 Tooltip 显示具体错误信息
- 确定（Ok）按钮绑定 `IsEnabled="{Binding !HasValidationErrors}"` 或点击时拦截提示

#### 3.2.3 拖拽排序 / 上下移动按钮

**方案 A：拖拽排序（优先）**

使用 PreviewMouseMove + DragDrop 实现：

```csharp
// ConditionBranchView.xaml.cs 新增
private bool _isDragging;
private BranchCondition _draggedItem;

private void OnConditionsGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    if (sender is not DataGrid dg || dg.SelectedItem is not BranchCondition item) return;
    _draggedItem = item;
    _isDragging = false;
}

private void OnConditionsGrid_PreviewMouseMove(object sender, MouseEventArgs e)
{
    if (_draggedItem == null || e.LeftButton != MouseButtonState.Pressed) return;
    
    if (!_isDragging)
    {
        _isDragging = true;
        DragDrop.DoDragDrop(dg, _draggedItem, DragDropEffects.Move);
    }
}

private void OnConditionsGrid_Drop(object sender, DragEventArgs e)
{
    if (_draggedItem == null || e.Data.GetData(typeof(BranchCondition)) is not BranchCondition targetItem) return;
    if (targetItem == _draggedItem) return;

    if (DataContext is ConditionBranchViewModel vm)
    {
        vm.MoveCondition(_draggedItem, targetItem);
    }
    
    _draggedItem = null;
    _isDragging = false;
}
```

**方案 B：上下移动按钮（备选/补充）**

如果拖拽体验不佳，每行添加操作按钮：

```xml
<!-- 在条件规则 DataGrid 中增加操作列 -->
<DataGridTemplateColumn Header="{lang:Lang BranchDetail_Column_Actions}" Width="Auto">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal">
                <Button Command="{Binding DataContext.MoveUpCommand, ...}"
                        CommandParameter="{Binding}"
                        ToolTip="{lang:Lang BranchDetail_MoveUp}">
                    <materialDesign:PackIcon Kind="ArrowUp" />
                </Button>
                <Button Command="{Binding DataContext.MoveDownCommand, ...}"
                        CommandParameter="{Binding}"
                        ToolTip="{lang:Lang BranchDetail_MoveDown}">
                    <materialDesign:PackIcon Kind="ArrowDown" />
                </Button>
            </StackPanel>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

**ViewModel 移动逻辑**：

```csharp
public ICommand MoveUpCommand { get; }
public ICommand MoveDownCommand { get; }

private void OnMoveUp(BranchCondition condition)
{
    int idx = Conditions.IndexOf(condition);
    if (idx <= 0) return;
    Conditions.Move(idx, idx - 1);
}

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

#### 3.2.4 条件规则区域列宽配置

| 列 | 宽度模式 | 最小宽度 | 可调整 |
|----|----------|----------|--------|
| 条件表达式 | * (占比) | 250px | ✅ |
| 跳转目标 | 固定 90px | 70px | ✅ |
| 描述 | 固定 120px | 100px | ✅ |
| 操作（可选） | Auto | 60px | ❌ |

---

### 3.3 默认动作区域

保持不变，无需修改。

---

## 4. 文件变更清单

| 文件路径 | 变更类型 | 变更说明 |
|----------|----------|----------|
| `Module/Controls/StepDetails/ConditionBranchView.xaml` | **修改** | 参数名列绑定修正、值列改ComboBox、表达式列改内联编辑器+变量插入Popup+错误图标、列宽调整为Auto+MinWidth、可选操作列（↑↓按钮） |
| `Module/Controls/StepDetails/ConditionBranchView.xaml.cs` | **修改** | 变量插入按钮Click事件处理、Popup变量选择事件处理、拖拽排序事件处理（PreviewMouseLeftButtonDown/PreviewMouseMove/Drop）、语法校验触发方法 |
| `Module/Controls/StepDetails/ConditionBranchViewModel.cs` | **修改** | 新增 CommonParameterValues 属性、新增 ConditionErrors 字典及 HasValidationErrors 属性、新增 MoveUpCommand/MoveDownCommand、新增 ValidateCondition/ValidateAllConditions 方法、新增 MoveCondition 方法、OnOk() 增加全量校验拦截 |
| `Core/Utilities/ExpressionValidator.cs` | **新建** | 静态校验工具类：Validate() 方法，包含非空检查、变量存在性、括号匹配、操作符合法性四项校验 |
| `Core/Models/BranchConfig.cs` | **无变更** | 模型层保持稳定 |

---

## 5. 数据流时序

```
用户打开 ConditionBranchView
 │
 ▼
ViewModel.LoadFromStep(step)
 ├── RefreshAvailableSteps()
 ├── LoadGlobalVariableNamesAsync()
 └── LoadPreviousStepOutputs(step) → 填充 PreviousStepOutputNames
 │
 ▼
【输出参数区域】
 │
 ├── 点击参数名单元格 → ComboBox 显示 PreviousStepOutputNames
 ├── 选择参数 → Name 属性更新
 ├── 点击值单元格 → ComboBox 显示 true/false/0/1 + 可自定义输入
 └── 选择目标全局变量 → GlobalVariableNames 列表
 │
 ▼
【条件规则区域】
 │
 ├── 双击表达式单元格 → 进入编辑模式
 │   ├── TextBox 直接输入/编辑
 │   └── 点击 [▼插入变量] → Popup 显示变量列表 → 选择后插入光标位置
 │       │
 │       ▼ (LostFocus 或 300ms 防抖)
 │   └── ViewModel.ValidateCondition(cond) → 更新 ConditionErrors → UI显示/隐藏错误图标
 │
 ├── 调整优先级
 │   ├── 方式A: 拖拽行 → Drop 事件 → ViewModel.MoveCondition()
 │   └── 方式B: 点击 ↑/↓ 按钮 → MoveUpCommand / MoveDownCommand
 │
 └── 选择跳转目标 → AvailableStepSeqs 列表
 │
 ▼
【用户点击确定】
 │
 ├── SyncAllComboBoxTextEdits()      ← 已有方法
 ├── CommitAllDataGridEdits()         ← 已有方法
 ├── ValidateAllConditions()          ← 新增：全量校验
 │   ├── HasErrors → MessageBox 提示 → 中止保存
 │   └── No Errors → 继续
 │
 └── OnOk() → 写回 BranchConfig → 关闭 DialogHost
```

---

## 6. 多语言资源键清单

以下资源键需添加到语言文件中：

| 资源键 | 中文 (zh-CN) | English (en-US) |
|--------|-------------|-----------------|
| `BranchDetail_InsertVar` | 插入变量 | Insert Var |
| `BranchDetail_SelectPrevOutput` | 选择前序输出参数 | Select Prev Output |
| `BranchDetail_MoveUp` | 上移 | Move Up |
| `BranchDetail_MoveDown` | 下移 | Move Down |
| `BranchDetail_Column_Actions` | 操作 | Actions |
| `BranchDetail_ExprError_Format` | 表达式错误: {0} | Expression Error: {0} |
| `BranchDetail_ValidateError_Msg` | 第 {0} 行条件表达式存在错误，请修正后保存 | Row {0} has expression errors, please fix before saving |

**涉及文件**：
- `MainApp/Languages/Strings.zh-CN.xaml`
- `MainApp/Languages/Strings.en-US.xaml`

---

## 7. 设计约束与原则

### 7.1 必须遵循

- ✅ **多语言支持**：所有用户可见文本必须使用 `{lang:Lang XxxKey}` 绑定
- ✅ **WPF + PRISM + MaterialDesign**：使用 PackIcon 图标、DelegateCommand、BindableBase、DialogHost
- ✅ **快速响应性**：内联编辑无额外弹窗，校验采用防抖避免卡顿
- ✅ **安全性**：保存前强制校验，拦截非法表达式；参数名强制选择禁止手输
- ✅ **清晰架构**：校验逻辑独立为静态工具类，不污染 ViewModel；职责单一
- ✅ **FallbackValue 兼容**：避免 Binding.FallbackValue 使用嵌套标记扩展

### 7.2 性能考虑

- **防抖机制**：表达式输入时的实时校验需加 300ms 防抖，避免频繁正则匹配
- **虚拟化**：DataGrid 保持默认虚拟化开启，大量条件时不影响性能
- **字典查找**：ConditionErrors 使用 Dictionary 保证 O(1) 查找效率

### 7.3 向后兼容

- **模型层不变**：BranchConfig / BranchOutputParameter / BranchCondition 结构不变
- **JSON 持久化格式兼容**：保存逻辑不变，历史配置可正常加载
- **API 接口不变**：对外暴露的 Step.BranchConfig 属性类型不变

---

## 8. 验收标准

### 功能验收

- [ ] 输出参数名 ComboBox 能正确显示前序步骤的所有输出参数列表
- [ ] 选择参数名后能正确写入 BranchOutputParameter.Name
- [ ] 值列 ComboBox 提供 true/false/0/1 选项且支持自定义输入
- [ ] 条件表达式编辑框能正常输入文本
- [ ] 点击「插入变量」按钮能弹出变量列表
- [ ] 选择变量后能正确插入到光标位置
- [ ] 输入非法表达式时显示红色错误图标和 Tooltip
- [ ] 存在错误时确定按钮被禁用或点击时弹出提示
- [ ] 条件规则可通过拖拽或 ↑↓ 按钮调整顺序
- [ ] 所有列宽自适应内容且可手动拖拽调整
- [ ] 中英文界面切换正常

### 代码质量

- [ ] ExpressionValidator 单元测试覆盖主要场景
- [ ] 无编译警告
- [ ] 遵循项目现有命名规范和代码风格
- [ ] 关键方法和类有 XML 注释
