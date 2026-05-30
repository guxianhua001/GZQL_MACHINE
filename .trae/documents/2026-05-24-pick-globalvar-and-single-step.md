# PICK工具全局变量绑定优化 + 步骤单独执行

## 需求概述

1. **PICK工具全局变量绑定优化**：Offset列改为VisionCapture风格的ComboBox绑定（`ItemsSource=AvailableGlobalVariables`, `SelectedValuePath="Name"`, `DisplayMemberPath="Name"`），Hint显示为链接Icon
2. **PICK步骤单独执行**：在步骤编辑器中支持选中某一步骤单独运行

---

## 一、PICK工具全局变量绑定优化

### 当前问题

Offset列使用 `IsEditable=True` 的ComboBox + `OffsetVariableOptions`（`List<string>`），用户需要手动输入变量名或从字符串列表中选择。对比VisionCapture的做法：
- VisionCapture使用 `ObservableCollection<GlobalVariable>` 作为ItemsSource
- `SelectedValuePath="Name"`, `DisplayMemberPath="Name"` 精确绑定
- 选择变量后自动回填数值

### 修改方案

#### 1. PickDetailViewModel 修改

- `OffsetVariableOptions`（`ObservableCollection<string>`）→ `AvailableGlobalVariables`（`ObservableCollection<GlobalVariable>`）
- `LoadGlobalVariablesAsync()` 方法改为直接填充 `AvailableGlobalVariables`，不再拼接字符串列表
- 新增 `SelectedStepOffsetLinkedVar` 属性（或由SubMoveRowViewModel自行管理）

#### 2. SubMoveRowViewModel 修改

- 新增 `AvailableGlobalVariables` 属性（从父级ViewModel传递或共享引用）
- `OffsetVariableName` setter中：选择全局变量后自动回填Offset数值（参考VisionCapture的NeedleOffsetXLinkedVar模式）
- 保留 `IsOffsetLinked` 和 `OffsetDisplayText` 逻辑不变

#### 3. PickDetailView.xaml 修改

Offset列编辑模板改为：
```xml
<DataGridTemplateColumn.CellEditingTemplate>
    <DataTemplate>
        <DockPanel>
            <materialDesign:PackIcon Kind="LinkVariant" ... DockPanel.Dock="Left"
                Visibility="{Binding IsOffsetLinked, Converter={StaticResource BoolToVisibilityConverter}}"/>
            <ComboBox ItemsSource="{Binding DataContext.AvailableGlobalVariables, RelativeSource={RelativeSource AncestorType=UserControl}}"
                      SelectedValuePath="Name" DisplayMemberPath="Name"
                      SelectedValue="{Binding OffsetVariableName, UpdateSourceTrigger=PropertyChanged}"
                      IsEditable="True"
                      IsEnabled="{Binding IsMotionEnabled}"
                      materialDesign:HintAssist.Hint="{lang:Lang VisionCapture_LinkVariable}" />
        </DockPanel>
    </DataTemplate>
</DataGridTemplateColumn.CellEditingTemplate>
```

关键变化：
- `ItemsSource` 从 `OffsetVariableOptions`（string列表）改为 `AvailableGlobalVariables`（GlobalVariable对象列表）
- 添加 `SelectedValuePath="Name"` + `DisplayMemberPath="Name"` 精确绑定
- `materialDesign:HintAssist.Hint` 使用已有的 `VisionCapture_LinkVariable` 多语言键
- 保留 `IsEditable="True"` 允许手动输入数值

#### 4. 多语言文件

- 复用已有的 `VisionCapture_LinkVariable` 键（zh-CN: "链接变量", en-US: "Link Variable"）
- 无需新增键

---

## 二、PICK步骤单独执行

### 当前状态

- 系统已有**全局单步调试模式**（OverView底部"单步: 开/关"+"下一步"按钮），但这是逐步推进模式
- **不存在**"选中某一步直接运行"的功能
- `ProcessStepExecutor.ExecuteSingleStepAsync` 是 private 方法
- `StationTaskBase.ExecuteStepSafeAsync` 是公开方法，可包装单步执行

### 修改方案

#### 1. ProcessStepExecutor 新增公开方法

```csharp
/// <summary> 单独执行指定步骤（用于步骤编辑器中的调试运行） </summary>
public async Task ExecuteSingleStepAsync(ProcessStep step, CancellationToken token)
```

- 内部调用 `ExecuteWithRunStepAsync`，享受暂停/急停/可恢复异常保护
- 执行前发布步骤高亮事件
- 执行后写入 `_stepOutputs`

#### 2. IProcessSequenceService 新增接口方法

```csharp
/// <summary> 单独执行指定步骤 </summary>
Task RunSingleStepAsync(ProcessStep step);
```

#### 3. ProcessSequenceService 实现

- 找到当前工站的 StationTaskBase
- 调用 `RunCustomSequenceAsync` 包装单步执行
- 或直接调用 ProcessStepExecutor 的公开方法

#### 4. ProcessSequenceEditorViewModel 新增命令

```csharp
public DelegateCommand RunSingleStepCommand { get; }
```

- 仅在 `SelectedStep != null` 且任务未运行时可用
- 调用 `IProcessSequenceService.RunSingleStepAsync(SelectedStep)`

#### 5. 步骤编辑器UI

在步骤列表的右键菜单或工具栏中添加"运行此步骤"按钮：
```xml
<Button Content="{lang:Lang PSE_RunSingleStep}"
        Command="{Binding RunSingleStepCommand}"
        ToolTip="{lang:Lang PSE_RunSingleStepTip}"
        Style="{StaticResource MaterialDesignOutlinedButton}">
    <StackPanel Orientation="Horizontal">
        <materialDesign:PackIcon Kind="Play" Width="14" Height="14" Margin="0,0,4,0"/>
        <TextBlock Text="{lang:Lang PSE_RunSingleStep}"/>
    </StackPanel>
</Button>
```

#### 6. 多语言文件

新增键：
- `PSE_RunSingleStep`：zh-CN "运行此步骤" / en-US "Run Step"
- `PSE_RunSingleStepTip`：zh-CN "单独运行选中的步骤" / en-US "Run the selected step independently"

---

## 涉及文件清单

| 文件 | 修改内容 |
|------|----------|
| `Module/Controls/StepDetails/PickDetailViewModel.cs` | `OffsetVariableOptions` → `AvailableGlobalVariables`，加载逻辑改为GlobalVariable对象列表 |
| `Module/Controls/StepEditor/SubMoveRowViewModel.cs` | 新增 `AvailableGlobalVariables` 属性，OffsetVariableName setter自动回填数值 |
| `Module/Controls/StepDetails/PickDetailView.xaml` | Offset列编辑模板改为VisionCapture风格ComboBox |
| `StationTasks/Actions/ProcessStepExecutor.cs` | 新增 `ExecuteSingleStepAsync` 公开方法 |
| `Module/Services/IProcessSequenceService.cs` | 新增 `RunSingleStepAsync` 接口方法 |
| `Module/Services/ProcessSequenceService.cs` | 实现 `RunSingleStepAsync` |
| `Module/Controls/StepEditor/ProcessSequenceEditorViewModel.cs` | 新增 `RunSingleStepCommand` |
| `Module/Controls/StepEditor/ProcessSequenceEditorView.xaml` | 添加"运行此步骤"按钮 |
| `MainApp/Languages/Strings.zh-CN.xaml` | 新增 PSE_RunSingleStep 等键 |
| `MainApp/Languages/Strings.en-US.xaml` | 新增 PSE_RunSingleStep 等键 |

## 实施顺序

1. PICK全局变量绑定优化（3个文件）
2. 步骤单独执行功能（7个文件）
3. 构建验证
