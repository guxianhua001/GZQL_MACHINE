# Pick 动作增强方案实施计划（续）

## 当前进度

✅ **Step 1 已完成**: SubMove模型扩展
- 文件: `StationTasks/Models/ProcessStep.cs`
- 已添加 `SubMoveAction` 枚举（None, Clamp, Release, Hold, VacuumOn, VacuumOff）
- 已在 SubMove 类中添加 `Action` 和 `ActionParameter` 属性

## 剩余步骤

### Step 2: 修改 PickStepAction.ExecuteAsync - 添加 Action 分发逻辑

**文件**: `StationTasks/Actions/PickStepAction.cs`

**目标**: 在 foreach 循环中，每个 SubMove 执行前先检查并执行 Action

**改造点**:
1. 在第69行的 foreach 循环内部，运动执行之前插入 Action 分发逻辑
2. 使用 switch 语句处理不同的 Action 类型
3. Action 参数为0时使用 PickDetail 的默认值
4. 保留原有的 Phase 2/3/4 作为向后兼容（当 PickMoves 中无任何非 None Action 时执行）

**代码结构**:
```csharp
foreach (var subMove in pickDetail.PickMoves)
{
    token.ThrowIfCancellationRequested();

    // ★ 新增：执行子步骤动作（夹爪/延时等）
    await ExecuteSubMoveActionAsync(subMove, pickDetail, step.Seq, token);

    // 原有的运动执行逻辑（仅当有轴配置时）
    if (!string.IsNullOrEmpty(subMove.Axis) || subMove.AxisId > 0)
    {
        // ... 保持现有逻辑不变
    }
}

// ★ 修改：仅在没有任何非None Action时执行原有硬编码流程
bool hasAnyAction = pickDetail.PickMoves.Any(m => m.Action != SubMoveAction.None);
if (!hasAnyAction)
{
    // 原有的 Phase 2/3/4 逻辑保持不变
}
```

---

### Step 3: 修改 SubMoveRowViewModel - 添加 Action 绑定属性

**文件**: `Module/Controls/StepEditor/SubMoveRowViewModel.cs`

**目标**: 为 UI 提供 Action 类型的绑定支持

**新增内容**:
1. 添加 `Action` 属性转发到 `_subMove.Action`
2. 添加 `ActionParameter` 属性转发到 `_subMove.ActionParameter`
3. 添加 `AvailableActions` 集合（ObservableCollection<SubMoveAction>）供 ComboBox 使用
4. 添加 `IsActionParamVisible` 计算属性（根据当前 Action 类型决定参数列是否可见）
5. 添加 `ActionParamHintText` 计算属性（根据 Action 类型返回不同的提示文字）

**代码示例**:
```csharp
// 转发 Action 属性
public SubMoveAction Action
{
    get => _subMove.Action;
    set
    {
        if (_subMove.Action != value)
        {
            _subMove.Action = value;
            RaisePropertyChanged(nameof(Action));
            RaisePropertyChanged(nameof(IsActionParamVisible));
            RaisePropertyChanged(nameof(ActionParamHintText));
        }
    }
}

public double ActionParameter
{
    get => _subMove.ActionParameter;
    set => _subMove.ActionParameter = value;
}

// 可用的动作类型列表
private static readonly ObservableCollection<SubMoveAction> _availableActions =
    new ObservableCollection<SubMoveAction>(Enum.GetValues(typeof(SubMoveAction)).Cast<SubMoveAction>());
public ObservableCollection<SubMoveAction> AvailableActions => _availableActions;

// 参数列是否可见/启用
public bool IsActionParamVisible => Action == SubMoveAction.Clamp || Action == SubMoveAction.Release || Action == SubMoveAction.Hold;

// 参数列提示文字
public string ActionParamHintText => Action switch
{
    SubMoveAction.Clamp => "位置 (mm)",
    SubMoveAction.Release => "位置 (mm)",
    SubMoveAction.Hold => "时间 (ms)",
    _ => string.Empty
};
```

---

### Step 4: 修改 PickDetailView XAML - UI 优化（3个优化点）

**文件**: `Module/Controls/StepDetails/PickDetailView.xaml`

#### 优化1: 列名优化（更易懂）

| 原列名 | 新列名 | 多语言键 |
|--------|--------|----------|
| Sub | 序号 | `PickDetail_Column_Seq` |
| (新增) | 动作类型 | `PickDetail_Column_ActionType` |
| (新增) | 动作参数 | `PickDetail_Column_ActionParam` |
| Station | 工站 | `PickDetail_Column_Station` |
| Axis | 轴 | `PickDetail_Column_Axis` |
| Position | 位置 | `PickDetail_Column_Position` |
| Ofs(mm) | 偏移 | `PickDetail_Column_Offset` |
| Spd | 速度 | `PickDetail_Column_Speed` |
| Description | 描述 | `PickDetail_Column_Description` |

#### 优化2: 智能显示/隐藏（提升体验）

在 DataGrid 中新增两列：
1. **动作类型列**: ComboBox 绑定到 `AvailableActions`
2. **动作参数列**: TextBox + MultiDataTrigger 智能控制

**动作参数列的显示规则**:
- Action = None → 禁用(灰色) + 半透明
- Action = Clamp → 启用, Header="位置 (mm)"
- Action = Release → 启用, Header="位置 (mm)"
- Action = Hold → 启用, Header="时间 (ms)"
- Action = VacuumOn/VacuumOff → 隐藏

**XAML 实现要点**:
```xml
<!-- 动作类型列 -->
<DataGridTemplateColumn Header="{lang:Lang PickDetail_Column_ActionType}" Width="Auto">
    <DataGridTemplateColumn.CellEditingTemplate>
        <DataTemplate>
            <ComboBox ItemsSource="{Binding AvailableActions}"
                      SelectedItem="{Binding Action, UpdateSourceTrigger=PropertyChanged}" />
        </DataTemplate>
    </DataGridTemplateColumn.CellEditingTemplate>
</DataGridTemplateColumn>

<!-- 动作参数列（智能显示） -->
<DataGridTemplateColumn Header="{Binding DataContext.SelectedRowActionParamHint, RelativeSource={RelativeSource AncestorType=UserControl}}" Width="Auto">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <TextBox Text="{Binding ActionParameter, UpdateSourceTrigger=PropertyChanged}">
                <TextBox.Style>
                    <Style TargetType="TextBox" BasedOn="{StaticResource {x:Type TextBox}}">
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding Action}" Value="{x:Static local:SubMoveAction.None}">
                                <Setter Property="IsEnabled" Value="False" />
                                <Setter Property="Opacity" Value="0.5" />
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </TextBox.Style>
            </TextBox>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

#### 优化3: 增加提示文字

在 DataGrid 上方添加提示信息：

```xml
<TextBlock Text="{lang:Lang PickDetail_ActionParamHint}"
           Foreground="{DynamicResource MaterialDesignBrush.LightBlue}"
           FontSize="12"
           Margin="0,4,0,8"
           TextWrapping="Wrap">
    <TextBlock.Style>
        <Style TargetType="TextBlock">
            <Style.Triggers>
                <DataTrigger Binding="{Binding HasAnyActionConfigured}" Value="True">
                    <Setter Property="Visibility" Value="Visible" />
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </TextBlock.Style>
</TextBlock>
```

**提示文字内容**:
- 中文: "💡 动作参数为空或0时，自动使用上方【夹爪配置】的默认值"
- 英文: "💡 When action parameter is empty or 0, the default value from [Gripper Config] above will be used automatically"

---

### Step 5: 多语言支持

**文件**:
- `MainApp/Languages/Strings.zh-CN.xaml`
- `MainApp/Languages/Strings.en-US.xaml`

**新增多语言键**:

| 键名 | 中文值 | 英文值 |
|------|--------|--------|
| `PickDetail_Column_Seq` | 序号 | Seq |
| `PickDetail_Column_ActionType` | 动作类型 | Action Type |
| `PickDetail_Column_ActionParam` | 动作参数 | Action Param |
| `PickDetail_Column_Station` | 工站 | Station |
| `PickDetail_Column_Axis` | 轴 | Axis |
| `PickDetail_Column_Position` | 位置 | Position |
| `PickDetail_Column_Offset` | 偏移 | Offset |
| `PickDetail_Column_Speed` | 速度 | Speed |
| `PickDetail_Column_Description` | 描述 | Description |
| `PickDetail_Action_None` | （无运动） | (Motion Only) |
| `PickDetail_Action_Clamp` | 夹紧 | Clamp |
| `PickDetail_Action_Release` | 释放 | Release |
| `PickDetail_Action_Hold` | 延时 | Hold |
| `PickDetail_Action_VacuumOn` | 开真空 | Vacuum On |
| `PickDetail_Action_VacuumOff` | 关真空 | Vacuum Off |
| `PickDetail_ActionParamHint` | 💡 动作参数为空或0时，自动使用上方【夹爪配置】的默认值 | 💡 When action parameter is empty or 0, the default value from [Gripper Config] above will be used automatically |
| `PickDetail_Param_Position_mm` | 位置 (mm) | Position (mm) |
| `PickDetail_Param_Time_ms` | 时间 (ms) | Time (ms) |

---

### Step 6: 构建验证

**命令**: `dotnet build GZQL_MACHINE.sln --no-restore`

**预期结果**: 0 错误，0 警告

**验证点**:
1. ✅ SubMoveAction 枚举正确序列化/反序列化
2. ✅ PickStepAction.ExecuteAsync 正确分发 Action
3. ✅ SubMoveRowViewModel 正确转发属性
4. ✅ PickDetailView 表格正确显示 Action 列
5. ✅ 动作参数列根据 Action 类型智能显示/隐藏
6. ✅ 多语言切换正常工作
7. ✅ 向后兼容：旧配方加载后 Action 默认为 None

---

## 影响范围总结

| 文件 | 修改类型 | 优先级 |
|------|----------|--------|
| `StationTasks/Actions/PickStepAction.cs` | 修改 ExecuteAsync | 🔴 高 |
| `Module/Controls/StepEditor/SubMoveRowViewModel.cs` | 添加属性 | 🔴 高 |
| `Module/Controls/StepDetails/PickDetailView.xaml` | UI 重构 | 🔴 高 |
| `MainApp/Languages/Strings.zh-CN.xaml` | 添加键值对 | 🟡 中 |
| `MainApp/Languages/Strings.en-US.xaml` | 添加键值对 | 🟡 中 |

## 向后兼容性保证

1. **JSON 反序列化**: Newtonsoft.Json 对缺失字段使用默认值 → `SubMoveAction.None`
2. **旧配方加载**: 所有现有 SubMove 的 Action = None，行为与修改前完全一致
3. **UI 显示**: Action 列默认显示"（无运动）"，不影响现有操作习惯
4. **执行逻辑**: 仅当检测到非 None Action 时才跳过原有硬编码流程

## 预计工作量

- Step 2 (PickStepAction): ~40 行代码
- Step 3 (SubMoveRowViewModel): ~30 行代码
- Step 4 (PickDetailView XAML): ~80 行 XAML
- Step 5 (多语言): ~20 行 XML
- Step 6 (构建验证): 2-3 分钟

**总计**: 约 170 行代码修改，预计 30 分钟完成
