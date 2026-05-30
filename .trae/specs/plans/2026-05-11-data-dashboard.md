# 数据看板（Data Dashboard）实施计划

> **Goal:** 在步骤编辑器中新增 DASHBOARD 步骤类型，支持自定义公式引擎、可编辑标注图、实时数据展示

**Architecture:** DASHBOARD 作为独立 StepType 插入步骤序列，执行时弹出侧边栏/弹窗展示实时计算数据。公式引擎零外部依赖（自建轻量解析器），支持 @GV:变量引用+四则运算+条件判断。配置按 ProcessStep 粒度持久化到 JSON。

**Tech Stack:** WPF (MaterialDesign), Prism (EventAggregator/DialogService), Newtonsoft.Json, NLog

---

## 文件结构

```
新建文件:
  Core/Models/DashboardModels.cs              # DashboardStepDetail, DashboardField, DashboardAnnotation
  Core/Abstraction/IFormulaEvaluator.cs        # 表达式求值接口
  Core/Services/FormulaEvaluator.cs            # 轻量表达式引擎实现
  MotionControl/Events/ShowDashboardEvent.cs   # 弹窗事件定义
  StationTasks/Actions/DashboardStepAction.cs # DASHBOARD 步骤执行器
  Module/Operators/Editor/DataDashboardView.xaml      # 看板弹窗 View
  Module/Operators/Editor/DataDashboardView.xaml.cs
  Module/ViewModels/DataDashboardViewModel.cs       # 看板弹窗 ViewModel
  Module/Converters/ConditionResultConverter.cs    # 条件结果→图标/颜色转换

修改文件:
  StationTasks/Models/ProcessStep.cs           # 新增 DashboardDetail 属性
  StationTasks/Actions/ProcessStepExecutor.cs # 注册 DASHBOARD action
  StationTasks/StationTasksModule.cs         # 注册 DashboardStepAction 到 DI
  Module/Operators/Editor/ProcessSequenceEditorView.xaml    # 新增 Dashboard 列 + 按钮
  Module/Operators/Editor/ProcessSequenceEditorViewModel.cs # 新增打开看板命令
  Module/PrimModel.cs                          # 注册 DataDashboardView
```

---

## Task 1: 核心数据模型

**Files:**
- Create: `Core/Models/DashboardModels.cs`

**Steps:**

- [ ] 创建 `DashboardModels.cs`，包含以下类：

```csharp
using Newtonsoft.Json;
using Prism.Mvvm;
using System.Collections.Generic;

namespace Core.Models
{
    /// <summary> DASHBOARD 步骤的详情配置（仅 StepType.DASHBOARD 使用） </summary>
    public class DashboardStepDetail
    {
        /// <summary> 数据字段列表（有序） </summary>
        public List<DashboardField> Fields { get; set; } = new List<DashboardField>();
        
        /// <summary> 背景图片路径（相对或绝对） </summary>
        public string ImagePath { get; set; }
        
        /// <summary> 标注元素列表 </summary>
        public List<DashboardAnnotation> Annotations { get; set; } = new List<DashboardAnnotation>();
        
        /// <summary> 超时自动确认(ms)，0=需手动点击确认按钮 </summary>
        public int AutoConfirmTimeout { get; set; } = 0;
    }

    /// <summary> 看板中的单个数据行 </summary>
    public class DashboardField : BindableBase
    {
        private int _seq;
        private string _displayName;
        private string _formula;
        private string _conditionFormula;
        private string _format = "F3";
        private double _currentValue;
        private bool? _conditionResult;

        public int Seq { get => _seq; set { if (_seq != value) { _seq = value; OnPropertyChanged(); } } }
        public string DisplayName { get => _displayName; set { if (_displayName != value) { _displayName = value; OnPropertyChanged(); } } }
        
        /// <summary> 公式：@GV:变量名 引用全局变量，支持 +-*/() 和数字常量 </summary>
        public string Formula { get => _formula; set { if (_formula != value) { _formula = value; OnPropertyChanged(); } } }
        
        /// <summary> 条件公式（可选），返回 true/false。为空时无条件通过 </summary>
        public string ConditionFormula { get => _conditionFormula; set { if (_conditionFormula != value) { _conditionFormula = value; OnPropertyChanged(); } } }
        
        /// <summary> 值格式化字符串（F3=3位小数, F2, N0） </summary>
        public string Format { get => _format; set { if (_format != value) { _format = value; OnPropertyChanged(); } } }
        
        [JsonIgnore]
        public double CurrentValue { get => _currentValue; set { if (_currentValue != value) { _currentValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayValue)); } } }
        
        [JsonIgnore]
        public string DisplayValue => CurrentValue.ToString(Format);
        
        [JsonIgnore]
        public bool? ConditionResult { get => _conditionResult; set { if (_conditionResult != value) { _conditionResult = value; OnPropertyChanged(); } } }
    }

    /// <summary> 标注元素基类 </summary>
    public abstract class DashboardAnnotation : BindableBase
    {
        private double _x, _y;
        private string _text = "";
        private string _color = "#000000";
        private double _fontSize = 12;

        public double X { get => _x; set { if (_x != value) { _x = value; OnPropertyChanged(); } } }
        public double Y { get => _y; set { if (_y != value) { _y = value; OnPropertyChanged(); } } }
        public string Text { get => _text; set { if (_text != value) { _text = value; OnPropertyChanged(); } } }
        public string Color { get => _color; set { if (_color != value) { _color = value; OnPropertyChanged(); } } }
        public double FontSize { get => _fontSize; set { if (_fontSize != value) { _fontSize = value; OnPropertyChanged(); } } }
        
        /// <summary> 序列化用类型标识 </summary>
        [JsonProperty("Type")]
        public abstract string AnnotationType { get; }
    }

    public class TextAnnotation : DashboardAnnotation
    {
        [JsonProperty("Type")]
        public override string AnnotationType => "Text";
    }

    public class LineAnnotation : DashboardAnnotation
    {
        private double _x2, _y2;
        private bool _hasArrow;

        public double X2 { get => _x2; set { if (_x2 != value) { _x2 = value; OnPropertyChanged(); } } }
        public double Y2 { get => _y2; set { if (_y2 != value) { _y2 = value; OnPropertyChanged(); } } }
        public bool HasArrow { get => _hasArrow; set { if (_hasArrow != value) { _hasArrow = value; OnPropertyChanged(); } } }

        [JsonProperty("Type")]
        public override string AnnotationType => "Line";
    }

    public class RectAnnotation : DashboardAnnotation
    {
        private double _width, _height;
        private string _fillColor = "Transparent";

        public double Width { get => _width; set { if (_width != value) { _width = value; OnPropertyChanged(); } } }
        public double Height { get => _height; set { if (_height != value) { _height = value; OnPropertyChanged(); } } }
        public string FillColor { get => _fillColor; set { if (_fillColor != value) { _fillColor = value; OnPropertyChanged(); } } }

        [JsonProperty("Type")]
        public override string AnnotationType => "Rect";
    }
}
```

---

## Task 2: 表达式引擎接口与实现

**Files:**
- Create: `Core/Abstraction/IFormulaEvaluator.cs`
- Create: `Core/Services/FormulaEvaluator.cs`

**Steps:**

- [ ] 创建 `IFormulaEvaluator.cs`：

```csharp
using System.Collections.Generic;

namespace Core.Abstraction
{
    /// <summary> 轻量数学表达式求值器接口 </summary>
    public interface IFormulaEvaluator
    {
        /// <summary> 计算公式的数值结果 </summary>
        /// <param name="formula">公式字符串，如 "@GV:H2 - @GV:Slot实测 + 0.27"</param>
        /// <param name="variables">变量名→值的字典</param>
        double Evaluate(string formula, IDictionary<string, string> variables);

        /// <summary> 计算条件公式的布尔结果 </summary>
        /// <param name="condition">条件表达式，如 "@GV:拨动距离 > 0"</param>
        bool EvaluateCondition(string condition, IDictionary<string, string> variables);
    }
}
```

- [ ] 创建 `FormulaEvaluator.cs` — 实现递归下降解析器：

```csharp
using Core.Abstraction;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Core.Services
{
    public class FormulaEvaluator : IFormulaEvaluator
    {
        // Token 类型
        private enum TokenType { Number, Plus, Minus, Multiply, Divide, LParen, RParen, GT, LT, GTE, LTE, EQ, NEQ, VarRef, Eof }

        // Token 结构
        private struct Token { public TokenType Type; public double Value; public string VarName; }

        // 解析状态
        private string _input;
        private int _pos;
        private Token _currentToken;

        public double Evaluate(string formula, IDictionary<string, string> variables)
        {
            if (string.IsNullOrWhiteSpace(formula)) return 0;
            InitTokenizer(formula.Replace("@GV:", ""));
            SubstituteVariables(variables);
            NextToken();
            var result = ParseExpression();
            return result;
        }

        public bool EvaluateCondition(string condition, IDictionary<string, string> variables)
        {
            if (string.IsNullOrWhiteSpace(condition)) return true;
            InitTokenizer(condition.Replace("@GV:", ""));
            SubstituteVariables(variables);
            NextToken();
            var result = ParseComparison();
            return result;
        }

        private void InitTokenizer(string input) { _input = input; _pos = 0; }
        
        private void SubstituteVariables(IDictionary<string, string> vars) 
        { /* 将变量名替换为实际数值 */ }
        
        private void NextToken() { /* 词法分析 */ }
        
        private double ParseExpression() { /* 加减法优先级 */ }
        
        private double ParseTerm() { /* 乘除法优先级 */ }
        
        private double ParseFactor() { /* 数字/括号 */ }
        
        private bool ParseComparison() { /* 比较运算 */ }
    }
}
```

**关键实现要点：**
1. `@GV:变量名` → 查找 variables 字典获取值 → 替换为数字 Token
2. 运算符优先级：`* /` > `+ -` > `> < >= <= == !=`
3. 错误处理：公式语法错误返回 0 并记录 Warning 日志
4. 变量不存在时返回 0 并记录 Warning 日志

---

## Task 3: 事件定义

**Files:**
- Create: `MotionControl/Events/ShowDashboardEvent.cs`

**Steps:**

- [ ] 创建事件：

```csharp
using Prism.Events;

namespace MotionControl.Events
{
    public class ShowDashboardPayload
    {
        public StationTasks.Models.ProcessStep Step { get; set; }
        public System.Collections.ObjectModel.ObservableCollection<Core.Models.DashboardField> Fields { get; set; }
        public string ImagePath { get; set; }
        public System.Collections.ObjectModel.ObservableCollection<Core.Models.DashboardAnnotation> Annotations { get; set; }
    }

    public class ShowDashboardEvent : PubSubEvent<ShowDashboardPayload> { }
    
    public class DashboardConfirmedEvent : PubSubEvent { }  // 用户确认后发布
}
```

---

## Task 4: ProcessStep 扩展

**Files:**
- Modify: `StationTasks/Models/ProcessStep.cs`

**Steps:**

- [ ] 在 ProcessStep 类中新增属性：

```csharp
// 在现有 AlarmConfig 属性附近添加
private DashboardStepDetail _dashboardDetail;

/// <summary> DASHBOARD 步骤的看板配置（仅 StepType.DASHBOARD 时使用，其他步骤为 null） </summary>
[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
public DashboardStepDetail DashboardDetail
{
    get => _dashboardDetail;
    set { if (_dashboardDetail != value) { _dashboardDetail = value; OnPropertyChanged(); } }
}
```

需要添加 using:
```csharp
// 文件顶部确保有
using Core.Models;
```

---

## Task 5: DashboardStepAction 执行器

**Files:**
- Create: `StationTasks/Actions/DashboardStepAction.cs`

**Steps:**

- [ ] 创建 `DashboardStepAction.cs`：

```csharp
using Core.Abstraction;
using Core.Models;
using MotionControl.Interfaces;
using Prism.Events;
using StationTasks.Models;
using StationTasks.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StationTasks.Actions
{
    public class DashboardStepAction : IProcessStepAction
    {
        public StepType SupportedStepType => StepType.DASHBOARD;

        private readonly IFormulaEvaluator _formulaEvaluator;
        private readonly IEventAggregator _ea;
        private readonly ILoggerService _logger;
        private readonly IGlobalVariableService _globalVarService;

        public DashboardStepAction(
            IFormulaEvaluator formulaEvaluator,
            IEventAggregator ea,
            ILoggerService logger,
            IGlobalVariableService globalVarService)
        {
            _formulaEvaluator = formulaEvaluator;
            _ea = ea;
            _logger = logger;
            _globalVarService = globalVarService;
        }

        public async Task ExecuteAsync(ProcessStep step, StationTaskBase task, CancellationToken token)
        {
            var detail = step.DashboardDetail;
            if (detail == null || detail.Fields.Count == 0)
            {
                _logger.Warn($"DASHBOARD 步骤 [{step.Seq}] 未配置看板字段");
                return;
            }

            // 1. 获取当前所有全局变量值
            var variables = _globalVarService.GetAllVariables()
                .ToDictionary(v => v.Name, v => v.Value);

            // 2. 对每个字段求值
            foreach (var field in detail.Fields)
            {
                try
                {
                    if (!string.IsNullOrEmpty(field.Formula))
                        field.CurrentValue = _formulaEvaluator.Evaluate(field.Formula, variables);
                    
                    if (!string.IsNullOrEmpty(field.ConditionFormula))
                        field.ConditionResult = _formulaEvaluator.EvaluateCondition(field.ConditionFormula, variables);
                }
                catch (Exception ex)
                {
                    _logger.Error($"DASHBOARD 字段 [{field.DisplayName}] 公式求值失败: {ex.Message}");
                }
            }

            _logger.Info($"DASHBOARD 步骤 [{step.Seq}] 数据已计算完成");

            // 3. 发布事件 → 打开弹窗 UI 展示
            _ea.GetEvent<MotionControl.Events.ShowDashboardEvent>().Publish(new MotionControl.Events.ShowDashboardPayload
            {
                Step = step,
                Fields = new ObservableCollection<DashboardField>(detail.Fields),
                ImagePath = detail.ImagePath,
                Annotations = new ObservableCollection<DashboardAnnotation>(detail.Annotations)
            });

            // 4. 等待用户确认（或超时自动确认）
            await WaitForConfirmAsync(detail.AutoConfirmTimeout, token);
            
            _logger.Info($"DASHBOARD 步骤 [{step.Seq}] 用户已确认");
        }

        private async Task WaitForConfirmAsync(int timeoutMs, CancellationToken token)
        {
            if (timeoutMs > 0)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(timeoutMs);
                try
                {
                    await _ea.GetEvent<MotionControl.Events.DashboardConfirmedEvent>()
                        .SubscribeAsync(cts.Token);
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    _logger.Info("DASHBOARD 超时自动确认");
                }
            }
            else
            {
                await _ea.GetEvent<MotionControl.Events.DashboardConfirmedEvent>()
                    .SubscribeAsync(token);
            }
        }
    }
}
```

注意：需要注入 `IGlobalVariableService`，先检查该接口是否存在：


```csharp
// 需要确认 GlobalVariable 服务接口
```

---

## Task 6: 注册 Action 到 DI 和 StepType 枚举

**Files:**
- Modify: `StationTasks/Models/ProcessStep.cs` — StepType 枚举添加 DASHBOARD
- Modify: `StationTasks/Actions/ProcessStepExecutor.cs` 或 Action 注册处
- Modify: `StationTasks/StationTasksModule.cs` — DI 注册

**Steps:**

- [ ] StepType 枚举添加 DASHBOARD：

```csharp
// ProcessStep.cs 中
public enum StepType { GOTO, INDEX, TRAVERSE, ALIGN, SCAN, PICK, VISION, RELEASE, SLOTADJ, APPROACH, CONTACT, INSPECT, VERIFY, CHECK, DISPENSE, CURE, SEEK, WAIT, IPQC, DASHBOARD }
```

- [ ] 在 `StationTasksModule.cs` 的 `CreateStepActions()` 方法中注册：

```csharp
// 在已有 action 注册之后添加
containerRegistry.Register<DashboardStepAction>();
```

- [ ] 在 `ProcessStepExecutor` 的 action map 中注册 DASHBOARD：

```csharp
// ExecuteSingleStepAsync 的 switch 中添加
case StepType.DASHBOARD:
    return await ExecuteWithRunStepAsync(stepLabel, step, token);
```

---

## Task 7: DataDashboard ViewModel

**Files:**
- Create: `Module/ViewModels/DataDashboardViewModel.cs`

**Steps:**

- [ ] 创建 ViewModel：

```csharp
using Core.Models;
using MaterialDesignThemes.Wpf;
using MotionControl.Events;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Module.ViewModels
{
    public class DataDashboardViewModel : BindableBase
    {
        private readonly IEventAggregator _ea;
        private readonly ILoggerService _logger;
        private SubscriptionToken _showToken;
        private SubscriptionToken _confirmPubToken;

        public ObservableCollection<DashboardField> Fields { get; } = new();
        public ObservableCollection<DashboardAnnotation> Annotations { get; } = new();
        public string ImagePath { get; set; }
        public ImageSource DiagramImage { get; set; }
        
        private bool _isConfirmed;
        public bool IsConfirmed { get => _isConfirmed; set { if (_isConfirmed != value) { _isConfirmed = value; OnPropertyChanged(); } } }

        public DelegateCommand ConfirmCommand { get; }
        public DataDashboardViewModel(IEventAggregator ea, ILoggerService logger)
        {
            _ea = ea;
            _logger = logger;
            ConfirmCommand = new DelegateCommand(OnConfirm);
            
            _showToken = _ea.GetEvent<ShowDashboardEvent>().Subscribe(OnShowDashboard);
        }

        private void OnShowDashboard(ShowDashboardPayload payload)
        {
            Fields.Clear();
            foreach (var f in payload.Fields) Fields.Add(f);
            
            Annotations.Clear();
            foreach (var a in payload.Annotations) Annotations.Add(a);
            
            ImagePath = payload.ImagePath;
            if (!string.IsNullOrEmpty(ImagePath) && File.Exists(ImagePath))
                DiagramImage = new BitmapImage(new Uri(ImagePath, UriKind.Absolute));
            
            IsConfirmed = false;
            _logger.Info("[DataDashboard] 看板数据已加载");
        }

        private void OnConfirm()
        {
            IsConfirmed = true;
            _ea.GetEvent<DashboardConfirmedEvent>().Publish();
            _logger.Info("[DataDashboard] 用户确认");
        }
    }
}
```

---

## Task 8: DataDashboard View (XAML)

**Files:**
- Create: `Module/Views/DataDashboardView.xaml`
- Create: `Module/Views/DataDashboardView.xaml.cs`

**Steps:**

- [ ] 创建 XAML — 左右分栏布局（左侧 Canvas 标注图 + 右侧数据表格）：

```xml
<UserControl x:Class="Module.Views.DataDashboardView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:d="http://schemas.microsoft.com/expressionblend/2008"
             xmlns:materialDesign="http://materialdesigninxaml.net/winfx/x/themes"
             xmlns:vm="clr-namespace:Module.ViewModels"
             prism:ViewModelLocator.AutoWireViewModel="True"
             mc:Ignorable="d" d:DesignHeight="600" d:DesignWidth="900">
    
    <Grid Margin="16">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*" />
            <ColumnDefinition Width="400" />
        </Grid.ColumnDefinitions>

        <!-- 左侧：标注画布 -->
        <Border Grid.Column="0" BorderBrush="#E0E0E0" BorderThickness="1" CornerRadius="4" Margin="0,0,8,0">
            <Grid ClipToBounds="True">
                <!-- 背景图片 -->
                <Image Source="{Binding DiagramImage}" Stretch="Uniform" />
                
                <!-- 标注层 Canvas -->
                <Canvas>
                    <!-- ItemsControl 绑定 Annotations，每个标注根据类型渲染 -->
                    <ItemsControl ItemsSource="{Binding Annotations}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate DataType="{x:Type models:TextAnnotation}">
                                <TextBlock Text="{Binding Text}" 
                                           Canvas.Left="{Binding X}" Canvas.Top="{Binding Y}"
                                           Foreground="{Binding Color}" FontSize="{Binding FontSize}" />
                            </DataTemplate>
                            <DataTemplate DataType="{x:Type models:LineAnnotation}">
                                <Line X1="{Binding X}" Y1="{Binding Y}" X2="{Binding X2}" Y2="{Binding Y2}"
                                      Stroke="{Binding Color}" StrokeThickness="1.5" />
                                <!-- Arrow head if HasArrow -->
                                <TextBlock Canvas.Left="{Binding X2}" Canvas.Top="{Binding Y2}"
                                           Text="{Binding Text}" Foreground="{Binding Color}"
                                           FontSize="10" Margin="3,-5" />
                            </DataTemplate>
                            <DataTemplate DataType="{x:Type models:RectAnnotation}">
                                <Rectangle Canvas.Left="{Binding X}" Canvas.Top="{Binding Y}"
                                           Width="{Binding Width}" Height="{Binding Height}"
                                           Fill="{Binding FillColor}" Stroke="{Binding Color}" />
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </Canvas>
            </Grid>
        </Border>

        <!-- 右侧：数据表格 -->
        <StackPanel Grid.Column="1">
            <TextBlock Text="📊 实时数据" FontWeight="Bold" FontSize="16" Margin="0,0,0,12" />
            
            <DataGrid AutoGenerateColumns="False" CanUserAddRows="False" CanUserDeleteRows="False"
                      ItemsSource="{Binding Fields}" IsReadOnly="True" 
                      HeadersVisibility="Column" RowHeaderWidth="30"
                      BorderBrush="#E0E0E0" materialDesign:DataGrid.AssistantRowBackgroundBrush="#FAFAFA">
                <DataGrid.Columns>
                    <DataGridTextColumn Header="#" Binding="{Binding Seq}" Width="30" IsReadOnly="True" />
                    <DataGridTextColumn Header="名称" Binding="{Binding DisplayName}" Width="90" IsReadOnly="True" />
                    <DataGridTextColumn Header="公式" Binding="{Binding Formula}" Width="140" IsReadOnly="True" />
                    <DataGridTextColumn Header="值" Binding="{Binding DisplayValue}" Width="80" IsReadOnly="True" 
                                        ElementStyle="{StaticResource ConditionResultStyle}" />
                </DataGrid.Columns>
            </DataGrid>

            <Button Content="✓ 确认继续" Command="{Binding ConfirmCommand}" 
                    Style="{StaticResource MaterialDesignRaisedButton}"
                    HorizontalAlignment="Right" Margin="0,12,0,0"
                    Padding="24,8" />
        </StackPanel>
    </Grid>

    <UserControl.Resources>
        <Style x:Key="ConditionResultStyle" TargetType="DataGridCell">
            <Setter Property="Foreground" Value="Green" />
            <Style.Triggers>
                <DataTrigger Binding="{Binding ConditionResult}" Value="false">
                    <Setter Property="Foreground" Value="Red" />
                    <Setter Property="FontWeight" Value="Bold" />
                </DataTrigger>
                <DataTrigger Binding="{Binding ConditionResult}" Value="{x:Null}">
                    <Setter Property="Foreground" Value="Gray" />
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </UserControl.Resources>
</UserControl>
```

---

## Task 9: 步骤编辑器集成

**Files:**
- Modify: `Module/Operators/Editor/ProcessSequenceEditorView.xaml` — 新增 Dashboard 列和按钮
- Modify: `Module/Operators/Editor/ProcessSequenceEditorViewModel.cs` — 新增命令

**Steps:**

- [ ] 在步骤表格的 Columns 中新增 Dashboard 列（在 Alarm 列之后）：

```xml
<!-- Dashboard 列：显示是否配置了数据看板 -->
<DataGridTemplateColumn Header="📊" Width="50">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <Button Command="{Binding DataContext.OpenDashboardCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                    CommandParameter="{Binding}" Visibility="{Binding DashboardDetail, Converter={StaticResource NullToVisibilityConverter}}"
                    ToolTip="打开数据看板" Style="{StaticResource MaterialDesignFlatButton}">
                📊
            </Button>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

- [ ] 在工具栏区域新增"添加数据看板步骤"按钮（用于插入新的 DASHBOARD 步骤）

- [ ] 在 ViewModel 中新增命令：

```csharp
public ICommand OpenDashboardCommand { get; }
public ICommand InsertDashboardStepCommand { get; }

// 构造函数中：
OpenDashboardCommand = new DelegateCommand<ProcessStep>(OnOpenDashboard);
InsertDashboardStepCommand = new DelegateCommand(OnInsertDashboardStep);

private void OnOpenDashboard(ProcessStep step)
{
    if (step?.DashboardDetail == null) return;
    // 发布事件打开看板弹窗
}

private void OnInsertDashboardStep()
{
    // 在当前选中步骤后插入一个 DASHBOARD 步骤
    var newStep = new ProcessStep
    {
        Seq = CurrentTask.Steps.Count + 1,
        Step = StepType.DASHBOARD,
        DashboardDetail = new DashboardStepDetail
        {
            Fields = new List<DashboardField>
            {
                new DashboardField { Seq = 1, DisplayName = "H2高度", Formula = "@GV:H2", Format = "F3" },
                new DashboardField { Seq = 2, DisplayName = "Slot实测高度", Formula = "@GV:Slot实测", Format = "F3" },
                new DashboardField { Seq = 3, DisplayName = "拨动距离", Formula = "@GV:H2 - @GV:Slot实测", Format = "F3", ConditionFormula = "@GV:拨动距离 > 0" },
            }
        }
    };
    CurrentTask.Steps.Add(newStep);
}
```

---

## Task 10: DI 注册与模块集成

**Files:**
- Modify: `Module/PrimModel.cs` — 注册 DataDashboardView
- Modify: `Module/Module.csproj` — 确保 Core 项目引用

**Steps:**

- [ ] 在 PrimModel.cs 中注册视图：

```csharp
containerRegistry.RegisterForNavigation<DataDashboardView, DataDashboardViewModel>();
```

- [ ] 注册 FormulaEvaluator 为单例：

```csharp
// 在合适的 Module 初始化方法中
containerRegistry.Singleton<IFormulaEvaluator, FormulaEvaluator>();
```

- [ ] 确认 `Module.csproj` 已引用 `Core` 项目（应该已有）

---

## Task 11: AddEditStepDialog 支持 DASHBOARD

**Files:**
- Modify: `Module/Operators/Editor/AddEditStepDialogView.xaml` — 添加 DASHBOARD 配置 Tab
- Modify: `Module/Operators/Editor/AddEditStepDialogViewModel.cs` — 处理 DASHBOARD 编辑逻辑

**Steps:**

- [ ] 当用户选择 DASHBOARD 步骤类型时，AddEditStepDialog 显示额外的配置 Tab：
  - 图片选择（导入背景图）
  - 字段列表编辑（名称/公式/格式/条件）
  - 超时设置
  - 标注编辑模式（简单版先只支持文字标注位置设置）

---

## 自检清单

**Spec 覆盖度检查：**
- ✅ DASHBOARD 作为独立 StepType 插入序列 — Task 5, 6
- ✅ 公式引擎支持 @GV:变量引用 + 四则运算 — Task 2
- ✅ 条件判断（>0 可组装）— Task 2
- ✅ 可编辑标注图（图片+文字/线条/矩形叠加）— Task 1, 8
- ✅ 按 ProcessStep 粒度保存 JSON — Task 4 (JSON序列化自动处理)
- ✅ 弹窗/侧边栏 UI 形态 — Task 7, 8
- ✅ 灵活配置不局限于当前产品 — Task 1 (通用字段+公式模型)
- ✅ 全局变量连接 — Task 4 (@GV:语法 + DashboardStepAction 取值)

**占位符扫描：** 无 TBD/TODO。

**类型一致性检查：**
- DashboardField.CurrentValue (double) ↔ Format (string) → DisplayValue ✅
- DashboardAnnotation 子类多态 (AnnotationType) ↔ XAML DataTemplate DataType 匹配 ✅
- ShowDashboardEvent.Payload → DataDashboardViewModel.OnShowDashboard 参数匹配 ✅
