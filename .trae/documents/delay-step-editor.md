# DELAY 步骤编辑器实施计划

## 概述

在自定义步骤编辑器中增加 DELAY（延时等待）模块，复用现有 `StepType.WAIT` 枚举值（已存在但未实现），按照 SEEK 模块的实现模式补充完整的数据模型、ViewModel、View、执行器和注册链路。

> **设计决策**：使用已有的 `StepType.WAIT` 而非新增 `DELAY` 枚举值，因为 WAIT 和 DELAY 语义等价，避免枚举膨胀。UI 显示名称为 "DELAY"。

---

## 实施步骤

### 步骤 1：扩展 ProcessStep 数据模型

**文件**：`c:\WorkFiles\GZQL_MACHINE\StationTasks\Models\ProcessStep.cs`

1. 新增 `WaitDetail` 类（在文件末尾，`SeekDetail` 类之后）：

```csharp
/// <summary>
/// WAIT/DELAY 步骤的延时配置
/// </summary>
public class WaitDetail : BindableBase
{
    private double _delayMs = 1000;
    /// <summary> 延时时长（毫秒） </summary>
    public double DelayMs
    {
        get => _delayMs;
        set => SetProperty(ref _delayMs, value);
    }

    private string _timeUnit = "ms";
    /// <summary> 时间单位：ms / s / min </summary>
    public string TimeUnit
    {
        get => _timeUnit;
        set => SetProperty(ref _timeUnit, value);
    }

    private string _description;
    /// <summary> 延时说明 </summary>
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary> 转换为实际毫秒数（根据 TimeUnit 换算） </summary>
    [JsonIgnore]
    public double ActualDelayMs => TimeUnit switch
    {
        "s" => DelayMs * 1000,
        "min" => DelayMs * 60000,
        _ => DelayMs
    };
}
```

2. 在 `ProcessStep` 类中新增 `WaitDetail` 属性（在 `SeekDetail` 属性之后）：

```csharp
private WaitDetail _waitDetail;

/// <summary> WAIT/DELAY 步骤的延时配置（仅 StepType.WAIT 时使用） </summary>
[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
public WaitDetail WaitDetail
{
    get => _waitDetail;
    set { if (_waitDetail != value) { _waitDetail = value; OnPropertyChanged(); } }
}
```

---

### 步骤 2：创建 WaitStepAction 执行器

**文件**：`c:\WorkFiles\GZQL_MACHINE\StationTasks\Actions\WaitStepAction.cs`（新建）

- 实现 `IProcessStepAction` 接口
- `SupportedStepType => StepType.WAIT`
- 构造函数注入 `ILoggerService`
- `ExecuteAsync` 逻辑：
  1. 读取 `step.WaitDetail`，若为空则默认 1000ms
  2. 调用 `WaitDetail.ActualDelayMs` 获取实际毫秒数
  3. 使用 `Task.Delay((int)actualMs, token)` 执行延时
  4. 日志记录延时开始/完成
  5. 支持 CancellationToken 取消（急停/停止打断）

---

### 步骤 3：注册 WaitStepAction 到 DI 和 Executor

**文件 1**：`c:\WorkFiles\GZQL_MACHINE\StationTasks\StationTasksModule.cs`
- 在 `RegisterMany` 的类型数组中添加 `typeof(WaitStepAction)`

**文件 2**：`c:\WorkFiles\GZQL_MACHINE\StationTasks\Actions\ProcessStepExecutor.cs`
- 在 `ExecuteSingleStepAsync` 的 switch 中添加 `case StepType.WAIT:`，与 SEEK 等并列

---

### 步骤 4：创建 WaitDetailViewModel

**文件**：`c:\WorkFiles\GZQL_MACHINE\Module\Editor\WaitDetailViewModel.cs`（新建）

- 继承 `BindableBase`
- 属性：
  - `ProcessStep Step`（设置时调用 InitializeFromStep）
  - `double DelayValue`（延时数值，双向绑定）
  - `string SelectedTimeUnit`（时间单位，ms/s/min）
  - `ObservableCollection<string> TimeUnitOptions`（单位选项列表）
  - `string Description`（延时说明）
  - `string EstimatedDisplay`（估算显示，如 "≈ 1.0 s"）
- 命令：
  - `SaveCommand` → 保存 WaitDetail 到 Step 并关闭弹窗
  - `CloseCommand` → 关闭弹窗不保存
- `InitializeFromStep()`：从 Step.WaitDetail 加载数据，为空则创建默认值
- `OnSave()`：将当前配置写入 Step.WaitDetail，调用 DialogHost.Close

---

### 步骤 5：创建 WaitDetailView.xaml

**文件**：`c:\WorkFiles\GZQL_MACHINE\Module\Editor\WaitDetailView.xaml`（新建）

- 遵循三段式布局（标题栏 → 内容区 → 底部操作栏），与 SeekDetailView 风格统一
- **标题栏**：深色渐变 `#2D3748 → #3A475A`，PackIcon `Kind="Timer"`，标题文字 "DELAY"
- **内容区**：
  - 延时数值输入：`NumericUpDown` 控件，Minimum=0
  - 时间单位选择：`ComboBox`（ms / s / min）
  - 估算显示：实时换算显示（如输入 2 + min → "≈ 120,000 ms"）
  - 延时说明：`TextBox` 多行输入
  - 可选：进度条模拟预览（静态，仅视觉提示）
- **底部操作栏**：
  - 确认按钮：PackIcon `Kind="CheckCircle"`，文字 "确认继续"
  - 关闭按钮：PackIcon `Kind="Close"`

**文件**：`c:\WorkFiles\GZQL_MACHINE\Module\Editor\WaitDetailView.xaml.cs`（新建）
- 标准 UserControl 代码后置

---

### 步骤 6：注册 ViewModel 到 DI

**文件**：`c:\WorkFiles\GZQL_MACHINE\Module\PrimModel.cs`
- 在 `RegisterTypes` 中添加 `containerRegistry.Register<WaitDetailViewModel>();`

---

### 步骤 7：添加导航分支

**文件**：`c:\WorkFiles\GZQL_MACHINE\Module\Editor\ProcessSequenceEditorViewModel.cs`
- 在 `NavigateToDetailView` 方法中添加 WAIT 分支：
  ```csharp
  else if (step.Step == StepType.WAIT)
  {
      ShowWaitDetailDialog(step);
  }
  ```
- 新增 `ShowWaitDetailDialog` 方法，遵循现有模式：
  ```csharp
  private async void ShowWaitDetailDialog(ProcessStep step)
  {
      var vm = _containerProvider.Resolve<WaitDetailViewModel>();
      var view = new WaitDetailView();
      view.DataContext = vm;
      vm.Step = step;
      await ShowDialogSafely(view);
  }
  ```

---

## 文件变更清单

| 操作 | 文件路径 | 说明 |
|------|---------|------|
| 修改 | `StationTasks\Models\ProcessStep.cs` | 新增 WaitDetail 类 + ProcessStep.WaitDetail 属性 |
| 新建 | `StationTasks\Actions\WaitStepAction.cs` | WAIT 步骤执行器 |
| 修改 | `StationTasks\StationTasksModule.cs` | DI 注册 WaitStepAction |
| 修改 | `StationTasks\Actions\ProcessStepExecutor.cs` | switch 添加 WAIT 分支 |
| 新建 | `Module\Editor\WaitDetailViewModel.cs` | 延时配置 ViewModel |
| 新建 | `Module\Editor\WaitDetailView.xaml` | 延时配置 View |
| 新建 | `Module\Editor\WaitDetailView.xaml.cs` | View 代码后置 |
| 修改 | `Module\PrimModel.cs` | 注册 WaitDetailViewModel |
| 修改 | `Module\Editor\ProcessSequenceEditorViewModel.cs` | 添加 WAIT 导航分支 |

---

## 关键设计考量

1. **安全性**：WaitStepAction 使用 CancellationToken 支持急停/停止打断，延时期间可随时取消
2. **快速响应性**：Task.Delay 本身是异步非阻塞的，不会占用线程资源
3. **架构一致性**：完全遵循 SEEK 模块的实现模式（模型→执行器→ViewModel→View→注册→导航）
4. **扩展性**：WaitDetail 的 TimeUnit 设计支持 ms/s/min 三种单位，未来可轻松扩展
5. **UI 一致性**：三段式布局 + 深色渐变标题栏 + PackIcon 图标，与现有 DetailView 风格统一
