# ReleaseDetailView 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 参考 PickDetailView 设计并实现 ReleaseDetailView（释放步骤详情配置视图），移除夹紧位置、保压时间、真空夹紧检测，新增下放延时时间，动作表格复用 Pick 的结构

**Architecture:** 完全对标 PickDetailView 的三层架构（XAML View → ViewModel → Model），新增 ReleaseDetail 模型类挂载到 ProcessStep，通过 Prism DialogHost 弹窗集成到步骤序列编辑器

**Tech Stack:** WPF + PRISM 9 + MaterialDesignInXAML + .NET 9.0-windows7.0

---

## 参考基准 — PickDetailView 结构分析

### PickDetailView 布局结构（800px 宽）
```
┌─────────────────────────────────────────────┐
│ 标题栏: PICK {StepDescription}    [✖关闭]   │
├─────────────────────────────────────────────┤
│ 🤏 夹爪配置 (GroupBox)                       │
│ ┌──────────────────┬──────────────────────┐ │
│ │ 张开度(mm)       │ [ComboBox]           │ │
│ │ 夹紧力(N)        │ [ComboBox]           │ │
│ │ 真空控制         │ [开启][关闭] 状态文本 │ │
│ │ 夹紧位置(mm)     │ [TextBox][夹紧按钮]  │ │ ← 移除
│ │ 跳过夹紧检测     │ [☐ CheckBox]         │ │ ← 移除
│ │ 释放位置(mm)     │ [TextBox][释放按钮]  │ │ ← 保留
│ │ 保持时间(ms)     │ [TextBox]            │ │ ← 替换为下放延时
│ │                  │ [打开夹爪控制面板]    │ │
│ └──────────────────┴──────────────────────┘ │
├─────────────────────────────────────────────┤
│ 📋 取料动作序列 (GroupBox)                   │
│ ┌──────────────────────────────────────────┐│
│ │ DataGrid: 子序|工站|轴|位置|偏移|速度|描述 ││
│ │ [➕添加] [🗑删除] [↑] [↓]                ││
│ └──────────────────────────────────────────┘│
├─────────────────────────────────────────────┤
│                    [取消] [保存]              │
└─────────────────────────────────────────────┘
```

### PickDetail 模型属性清单
| 属性 | 类型 | 默认值 | Release 中处理 |
|------|------|--------|----------------|
| JawOpen | double | 10.0 | ✅ **保留** |
| JawForce | double | 15.0 | ✅ **保留** |
| VacuumPressure | int | 80 | ✅ **保留** |
| IsVacuumOn | bool | false | ✅ **保留** |
| ClampPosition | double | 100.0 | ❌ **移除** |
| SkipClampCheck | bool | false | ❌ **移除** |
| PickHoldingTime | int | 500 | ❌ **移除** |
| ReleasePosition | double | 500.0 | ✅ **保留** |
| VacuumCheckDelay | int | 200 | ❌ **移除**（真空检测相关） |
| PickMoves | ObservableCollection\<SubMove\> | [] | ✅ **保留**（改名 ReleaseMoves） |

### 新增属性
| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| ReleaseDelayTime | int | 300 | 下放延时时间（毫秒），执行释放动作后等待产品完全脱离的时间 |

---

## 文件变更清单

| # | 文件路径 | 操作 | 说明 |
|---|----------|------|------|
| 1 | `StationTasks/Models/ProcessStep.cs` | **修改** | 新增 ReleaseDetail 类定义 + ProcessStep.ReleaseDetail 属性 |
| 2 | `Module/Controls/StepDetails/ReleaseDetailView.xaml` | **新建** | XAML 视图（参考 PickDetailView 布局） |
| 3 | `Module/Controls/StepDetails/ReleaseDetailView.xaml.cs` | **新建** | Code-Behind（空壳，仅 InitializeComponent） |
| 4 | `Module/Controls/StepDetails/ReleaseDetailViewModel.cs` | **新建** | ViewModel（对标 PickDetailViewModel） |
| 5 | `Module/PrimModel.cs` | **修改** | 注册 ReleaseDetailView 导航 |
| 6 | `Module/Controls/StepEditor/ProcessSequenceEditorViewModel.cs` | **修改** | 新增 ShowReleaseDetailDialog + RELEASE 分支调用 |
| 7 | `MainApp/Languages/Strings.zh-CN.xaml` | **修改** | 新增 ReleaseDetail 中文资源键 |
| 8 | `MainApp/Languages/Strings.en-US.xaml` | **修改** | 新增 ReleaseDetail 英文资源键 |

**复用文件（无需修改）：**
- `Module/Controls/StepEditor/SubMoveRowViewModel.cs` — 动作行 ViewModel 直接复用
- `StationTasks/Models/ProcessStep.cs` 中的 `SubMove` 类 — 动作行模型直接复用

---

## Task 1: 创建 ReleaseDetail 数据模型

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\StationTasks\Models\ProcessStep.cs`

- [ ] **Step 1: 在 PickDetail 类定义之后（约第376行后）添加 ReleaseDetail 类**

```csharp
public class ReleaseDetail : BindableBase
{
    private double _jawOpen = 10.0;
    private double _jawForce = 15.0;
    private int _vacuumPressure = 80;
    private bool _isVacuumOn;
    private double _releasePosition = 500.0;
    private int _releaseDelayTime = 300;

    /// <summary> 夹爪张开度（mm）</summary>
    public double JawOpen { get => _jawOpen; set => SetProperty(ref _jawOpen, value); }

    /// <summary> 夹爪夹紧力（N），释放时用于松开夹持 </summary>
    public double JawForce { get => _jawForce; set => SetProperty(ref _jawForce, value); }

    /// <summary> 真空压力设定值（kPa），释放时用于关闭真空 </summary>
    public int VacuumPressure { get => _vacuumPressure; set => SetProperty(ref _vacuumPressure, value); }

    /// <summary> 真空开关状态（true=开启/false=关闭）</summary>
    public bool IsVacuumOn { get => _isVacuumOn; set => SetProperty(ref _isVacuumOn, value); }

    /// <summary> 释放位置（mm）：执行释放命令时夹爪移动到的目标位置 </summary>
    public double ReleasePosition { get => _releasePosition; set => SetProperty(ref _releasePosition, value); }

    /// <summary> 下放延时时间（ms）：释放动作完成后等待产品完全脱离的延时 </summary>
    public int ReleaseDelayTime { get => _releaseDelayTime; set => SetProperty(ref _releaseDelayTime, value); }

    /// <summary> 释放动作序列（子移动列表）</summary>
    public ObservableCollection<SubMove> ReleaseMoves { get; set; } = new ObservableCollection<SubMove>();
}
```

- [ ] **Step 2: 在 ProcessStep 类中添加 ReleaseDetail 属性**

在 `PickDetail` 属性之后（约第66行后）、`IpqcDetail` 之前添加：

```csharp
private ReleaseDetail _releaseDetail;
/// <summary> RELEASE 步骤的释放配置（仅 StepType.RELEASE 时使用，其他步骤为 null） </summary>
[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
public ReleaseDetail ReleaseDetail
{
    get => _releaseDetail;
    set
    {
        if (_releaseDetail != value)
        {
            _releaseDetail = value;
            OnPropertyChanged();
        }
    }
}
```

注意：需要确认文件顶部已有 `[JsonProperty]` 相关的 using（Newtonsoft.Json），如果没有则使用 `[JsonIgnore]` 或直接不加 attribute。

- [ ] **Step 3: 验证编译通过**

Run: `dotnet build StationTasks/StationTasks.csproj --configuration Debug`
Expected: Build succeeded

---

## Task 2: 新增多语言资源键

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\MainApp\Languages\Strings.zh-CN.xaml`
- Modify: `c:\WorkFiles\GZQL_MACHINE\MainApp\Languages\Strings.en-US.xaml`

- [ ] **Step 1: 在 Strings.zh-CN.xaml 中 PickDetailView 资源键区域之后添加**

在 `PickDetail_Log_ReleaseFailed` 行（约第2050行）之后插入：

```xml
    <!-- ═══ ReleaseDetailView - 放料配置详情 ═══ -->
    <sys:String x:Key="ReleaseDetail_GripperConfig">🤏 夹爪配置</sys:String>
    <sys:String x:Key="ReleaseDetail_JawOpen">张开度 (mm)</sys:String>
    <sys:String x:Key="ReleaseDetail_JawForce">夹紧力 (N)</sys:String>
    <sys:String x:Key="ReleaseDetail_VacuumControl">真空控制</sys:String>
    <sys:String x:Key="ReleaseDetail_VacuumOn">开启</sys:String>
    <sys:String x:Key="ReleaseDetail_VacuumOff">关闭</sys:String>
    <sys:String x:Key="ReleaseDetail_VacuumStatus_Off">真空关闭</sys:String>
    <sys:String x:Key="ReleaseDetail_ReleasePosition">释放位置 (mm)</sys:String>
    <sys:String x:Key="ReleaseDetail_Release">释放</sys:String>
    <sys:String x:Key="ReleaseDetail_ReleaseDelayTime">下放延时 (ms)</sys:String>
    <sys:String x:Key="ReleaseDetail_OpenGripperPanel">打开夹爪控制面板</sys:String>
    <sys:String x:Key="ReleaseDetail_ReleaseMotionSeq">📋 放料动作序列</sys:String>

    <!-- DataGrid 列头 -->
    <sys:String x:Key="ReleaseDetail_Column_Sub">子序</sys:String>
    <sys:String x:Key="ReleaseDetail_Column_Station">工站</sys:String>
    <sys:String x:Key="ReleaseDetail_Column_Axis">轴</sys:String>
    <sys:String x:Key="ReleaseDetail_Column_Position">位置</sys:String>
    <sys:String x:Key="ReleaseDetail_Column_Offset">偏移(mm)</sys:String>
    <sys:String x:Key="ReleaseDetail_Column_Speed">速度</sys:String>
    <sys:String x:Key="ReleaseDetail_Column_Description">描述</sys:String>

    <!-- 操作按钮和对话框消息 -->
    <sys:String x:Key="ReleaseDetail_ConfirmRelease_Title">确认释放操作</sys:String>
    <sys:String x:Key="ReleaseDetail_ConfirmRelease_Msg">确定要执行释放动作吗？\n目标位置: {0} mm</sys:String>
    <sys:String x:Key="ReleaseDetail_Log_ReleaseDone">快速释放完成，位置: {0} mm</sys:String>
    <sys:String x:Key="ReleaseDetail_Log_ReleaseFailed">快速释放失败: {0}</sys:String>
    <sys:String x:Key="ReleaseDetail_Alert_ReleaseFailed">释放失败</sys:String>
```

- [ ] **Step 2: 在 Strings.en-US.xaml 中对应位置添加英文资源键**

在 `PickDetail_Log_ReleaseFailed` 行（约第1758行）之后插入：

```xml
    <!-- ReleaseDetailView - Release Configuration Details -->
    <sys:String x:Key="ReleaseDetail_GripperConfig">Gripper Config</sys:String>
    <sys:String x:Key="ReleaseDetail_JawOpen">Jaw Open (mm)</sys:String>
    <sys:String x:Key="ReleaseDetail_JawForce">Jaw Force (N)</sys:String>
    <sys:String x:Key="ReleaseDetail_VacuumControl">Vacuum Control</sys:String>
    <sys:String x:Key="ReleaseDetail_VacuumOn">ON</sys:String>
    <sys:String x:Key="ReleaseDetail_VacuumOff">OFF</sys:String>
    <sys:String x:Key="ReleaseDetail_VacuumStatus_Off">Vacuum OFF</sys:String>
    <sys:String x:Key="ReleaseDetail_ReleasePosition">Release Pos (mm)</sys:String>
    <sys:String x:Key="ReleaseDetail_Release">Release</sys:String>
    <sys:String x:Key="ReleaseDetail_ReleaseDelayTime">Drop Delay (ms)</sys:String>
    <sys:String x:Key="ReleaseDetail_OpenGripperPanel">Open Gripper Panel</sys:String>
    <sys:String x:Key="ReleaseDetail_ReleaseMotionSeq">Release Motion Seq</sys:String>

    <sys:String x:Key="ReleaseDetail_Column_Sub">Sub</sys:String>
    <sys:String x:Key="ReleaseDetail_Column_Station">Station</sys:String>
    <sys:String x:Key="ReleaseDetail_Column_Axis">Axis</sys:String>
    <sys:String x:Key="ReleaseDetail_Column_Position">Position</sys:String>
    <sys:String x:Key="ReleaseDetail_Column_Offset">Offset(mm)</sys:String>
    <sys:String x:Key="ReleaseDetail_Column_Speed">Speed</sys:String>
    <sys:String x:Key="ReleaseDetail_Column_Description">Desc</sys:String>

    <sys:String x:Key="ReleaseDetail_ConfirmRelease_Title">Confirm Release</sys:String>
    <sys:String x:Key="ReleaseDetail_ConfirmRelease_Msg">Execute release action?\nTarget position: {0} mm</sys:String>
    <sys:String x:Key="ReleaseDetail_Log_ReleaseDone">Quick release done at {0} mm</sys:String>
    <sys:String x:Key="ReleaseDetail_Log_ReleaseFailed">Quick release failed: {0}</sys:String>
    <sys:String x:Key="ReleaseDetail_Alert_ReleaseFailed">Release Failed</sys:String>
```

- [ ] **Step 3: 验证编译通过**

---

## Task 3: 创建 ReleaseDetailView XAML

**Files:**
- Create: `c:\WorkFiles\GZQL_MACHINE\Module\Controls\StepDetails\ReleaseDetailView.xaml`

- [ ] **Step 1: 创建完整的 ReleaseDetailView.xaml**

```xml
<UserControl x:Class="Module.Views.ReleaseDetailView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes"
             xmlns:converters="clr-namespace:Framework.Converters;assembly=Framework"
             xmlns:lang="clr-namespace:Core.Markup;assembly=Core"
             Width="800">
    <Border Padding="16"
            Background="{DynamicResource MaterialDesignCardBackground}"
            CornerRadius="4">
        <StackPanel>
            <!-- 标题栏 + 关闭按钮 -->
            <DockPanel Margin="0,0,0,12">
                <Button DockPanel.Dock="Right"
                        Command="{Binding CloseCommand}"
                        ToolTip="{lang:Lang Close}"
                        Width="30" Height="30"
                        Style="{StaticResource MaterialDesignIconButton}">
                    <materialDesign:PackIcon Kind="Close" />
                </Button>
                <TextBlock FontWeight="Bold"
                           FontSize="14"
                           VerticalAlignment="Center"
                           Text="{Binding StepDescription, StringFormat=RELEASE {0}}" />
            </DockPanel>

            <!-- 夹爪配置区域（移除夹紧位置、跳过夹紧检测、保持时间；新增下放延时时间） -->
            <GroupBox Header="{lang:Lang ReleaseDetail_GripperConfig}" Margin="0,0,0,16">
                <Grid Margin="8">
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="140"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>

                    <!-- Row 0: 张开度 -->
                    <TextBlock Grid.Row="0" Grid.Column="0" Text="{lang:Lang ReleaseDetail_JawOpen}" VerticalAlignment="Center"/>
                    <ComboBox Grid.Row="0" Grid.Column="1"
                              ItemsSource="{Binding JawOpenOptions}"
                              SelectedItem="{Binding JawOpen}"
                              Margin="4,2"
                              IsEditable="True"
                              Width="120"
                              HorizontalAlignment="Left"
                              materialDesign:HintAssist.Hint="mm" />

                    <!-- Row 1: 夹紧力 -->
                    <TextBlock Grid.Row="1" Grid.Column="0" Text="{lang:Lang ReleaseDetail_JawForce}" VerticalAlignment="Center"/>
                    <ComboBox Grid.Row="1" Grid.Column="1"
                              ItemsSource="{Binding JawForceOptions}"
                              SelectedItem="{Binding JawForce}"
                              Margin="4,2"
                              IsEditable="True"
                              Width="120"
                              HorizontalAlignment="Left"
                              materialDesign:HintAssist.Hint="N" />

                    <!-- Row 2: 真空控制 -->
                    <TextBlock Grid.Row="2" Grid.Column="0" Text="{lang:Lang ReleaseDetail_VacuumControl}" VerticalAlignment="Center"/>
                    <StackPanel Grid.Row="2" Grid.Column="1" Orientation="Horizontal" Margin="4,2">
                        <Button Content="{lang:Lang ReleaseDetail_VacuumOn}"
                                Command="{Binding VacuumOnCommand}"
                                Margin="0,0,4,0"
                                Style="{StaticResource MaterialDesignRaisedButton}"
                                Width="60"/>
                        <Button Content="{lang:Lang ReleaseDetail_VacuumOff}"
                                Command="{Binding VacuumOffCommand}"
                                Style="{StaticResource MaterialDesignRaisedButton}"
                                Width="60"/>
                        <TextBlock Text="{Binding VacuumStatusText}"
                                   VerticalAlignment="Center"
                                   Margin="8,0,0,0"
                                   FontWeight="Bold"/>
                    </StackPanel>

                    <!-- Row 3: 释放位置 -->
                    <TextBlock Grid.Row="3" Grid.Column="0" Text="{lang:Lang ReleaseDetail_ReleasePosition}" VerticalAlignment="Center"/>
                    <StackPanel Grid.Row="3" Grid.Column="1" Orientation="Horizontal" Margin="4,2">
                        <TextBox Text="{Binding ReleasePosition}" Width="80" materialDesign:HintAssist.Hint="mm"/>
                        <Button Command="{Binding QuickReleaseCommand}"
                                Margin="8,0,0,0"
                                Style="{StaticResource MaterialDesignRaisedButton}">
                            <StackPanel Orientation="Horizontal">
                                <materialDesign:PackIcon Kind="OpenInNew" Width="16" Height="16" VerticalAlignment="Center" Margin="0,0,4,0"/>
                                <TextBlock Text="{lang:Lang ReleaseDetail_Release}" VerticalAlignment="Center"/>
                            </StackPanel>
                        </Button>
                    </StackPanel>

                    <!-- Row 4: 下放延时时间（新增，替代 PickHoldingTime） -->
                    <TextBlock Grid.Row="4" Grid.Column="0" Text="{lang:Lang ReleaseDetail_ReleaseDelayTime}" VerticalAlignment="Center"/>
                    <TextBox Grid.Row="4" Grid.Column="1"
                             Text="{Binding ReleaseDelayTime}"
                             Margin="4,2"
                             Width="120"
                             HorizontalAlignment="Left"
                             materialDesign:HintAssist.Hint="milliseconds"/>

                    <!-- Row 5: 打开夹爪控制面板按钮 -->
                    <StackPanel Grid.Row="5" Grid.Column="0" Grid.ColumnSpan="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,12,0,0">
                        <Button Command="{Binding OpenGripperControlCommand}"
                                Style="{StaticResource MaterialDesignRaisedButton}"
                                Background="{DynamicResource PrimaryHueMidBrush}">
                            <StackPanel Orientation="Horizontal">
                                <materialDesign:PackIcon Kind="TuneVertical" Width="16" Height="16" VerticalAlignment="Center" Margin="0,0,6,0"/>
                                <TextBlock Text="{lang:Lang ReleaseDetail_OpenGripperPanel}" VerticalAlignment="Center" FontWeight="Bold"/>
                            </StackPanel>
                        </Button>
                    </StackPanel>
                </Grid>
            </GroupBox>

            <!-- 放料动作表格（与 Pick 完全一致的列结构） -->
            <GroupBox Header="{lang:Lang ReleaseDetail_ReleaseMotionSeq}" Margin="0,0,0,8">
                <StackPanel>
                    <DataGrid ItemsSource="{Binding SubMoveRows}"
                              SelectedItem="{Binding SelectedSubMoveRow}"
                              AutoGenerateColumns="False"
                              CanUserAddRows="False"
                              CanUserDeleteRows="False"
                              materialDesign:DataGridAssist.CellPadding="4"
                              MaxHeight="300">
                        <DataGrid.Columns>
                            <DataGridTextColumn Header="{lang:Lang ReleaseDetail_Column_Sub}" Binding="{Binding SubSeq}" Width="50" IsReadOnly="True"/>

                            <DataGridTemplateColumn Header="{lang:Lang ReleaseDetail_Column_Station}" Width="130">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <TextBlock Text="{Binding StationId}" VerticalAlignment="Center" />
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                                <DataGridTemplateColumn.CellEditingTemplate>
                                    <DataTemplate>
                                        <ComboBox ItemsSource="{Binding DataContext.StationItems, RelativeSource={RelativeSource AncestorType=UserControl}}"
                                                  SelectedValue="{Binding StationId, UpdateSourceTrigger=PropertyChanged}"
                                                  SelectedValuePath="StationId"
                                                  DisplayMemberPath="DisplayName"
                                                  IsEditable="False" />
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellEditingTemplate>
                            </DataGridTemplateColumn>

                            <DataGridTemplateColumn Header="Axis" Width="80">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <TextBlock VerticalAlignment="Center">
                                            <TextBlock.Text>
                                                <MultiBinding StringFormat="{}{0}.{1}">
                                                    <Binding Path="StationId" />
                                                    <Binding Path="Axis" />
                                                </MultiBinding>
                                            </TextBlock.Text>
                                        </TextBlock>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                                <DataGridTemplateColumn.CellEditingTemplate>
                                    <DataTemplate>
                                        <ComboBox ItemsSource="{Binding AvailableAxes}"
                                                  SelectedItem="{Binding Axis, UpdateSourceTrigger=PropertyChanged}"
                                                  IsEditable="False" />
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellEditingTemplate>
                            </DataGridTemplateColumn>

                            <DataGridTemplateColumn Header="{lang:Lang ReleaseDetail_Column_Position}" Width="120">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <TextBlock Text="{Binding PositionName}" VerticalAlignment="Center" />
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                                <DataGridTemplateColumn.CellEditingTemplate>
                                    <DataTemplate>
                                        <ComboBox ItemsSource="{Binding AvailablePositions}"
                                                  SelectedItem="{Binding PositionName, UpdateSourceTrigger=PropertyChanged}"
                                                  IsEditable="False" />
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellEditingTemplate>
                            </DataGridTemplateColumn>

                            <DataGridTemplateColumn Header="{lang:Lang ReleaseDetail_Column_Offset}" Width="70">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <TextBox Text="{Binding Offset, UpdateSourceTrigger=PropertyChanged}" />
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>

                            <DataGridTemplateColumn Header="{lang:Lang ReleaseDetail_Column_Speed}" Width="60">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <TextBox Text="{Binding Speed, UpdateSourceTrigger=PropertyChanged}" />
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>

                            <DataGridTextColumn Header="{lang:Lang ReleaseDetail_Column_Description}" Binding="{Binding Description}" Width="*"/>
                        </DataGrid.Columns>
                    </DataGrid>

                    <StackPanel Orientation="Horizontal"
                                HorizontalAlignment="Left"
                                Margin="0,12,0,0">
                        <Button Content="{lang:Lang PickDetail_Add}"
                                Margin="0,0,4,0"
                                Command="{Binding AddMoveCommand}" />
                        <Button Content="{lang:Lang PickDetail_Delete}"
                                Margin="0,0,4,0"
                                Command="{Binding DeleteMoveCommand}" />
                        <Button Content="↑"
                                Margin="0,0,4,0"
                                Command="{Binding MoveUpCommand}" />
                        <Button Content="↓"
                                Margin="0,0,4,0"
                                Command="{Binding MoveDownCommand}" />
                    </StackPanel>
                </StackPanel>
            </GroupBox>

            <!-- 底部关闭/保存按钮 -->
            <StackPanel Orientation="Horizontal"
                        HorizontalAlignment="Right"
                        Margin="0,16,0,0">
                <Button Content="{lang:Lang Close}"
                        Command="{Binding CloseCommand}"
                        Style="{StaticResource MaterialDesignOutlinedButton}"
                        Margin="0,0,8,0" />
                <Button Content="{lang:Lang Save}"
                        Command="{Binding SaveCommand}"
                        Style="{StaticResource MaterialDesignRaisedButton}" />
            </StackPanel>
        </StackPanel>
    </Border>
</UserControl>
```

**与 PickDetailView 的关键差异：**
- 标题格式: `RELEASE {0}` 而非 `PICK {0}`
- 移除了 3 行: ClampPosition(夹紧位置)、SkipClampCheck(跳过夹紧检测)、PickHoldingTime(保持时间)
- 新增 1 行: ReleaseDelayTime(下放延时时间)
- GroupBox Header 使用 ReleaseDetail_ 前缀资源键
- DataGrid 列头使用 ReleaseDetail_ 前缀资源键
- 动作表格命令名: AddMoveCommand/DeleteMoveCommand（非 AddPickMoveCommand）

- [ ] **Step 2: 验证 XAML 无语法错误**

---

## Task 4: 创建 ReleaseDetailView Code-Behind

**Files:**
- Create: `c:\WorkFiles\GZQL_MACHINE\Module\Controls\StepDetails\ReleaseDetailView.xaml.cs`

- [ ] **Step 1: 创建 Code-Behind 文件**

```csharp
using System.Windows.Controls;

namespace Module.Views
{
    public partial class ReleaseDetailView : UserControl
    {
        public ReleaseDetailView()
        {
            InitializeComponent();
        }
    }
}
```

与 PickDetailView.xaml.cs 保持一致（空壳模式）。

---

## Task 5: 创建 ReleaseDetailViewModel

**Files:**
- Create: `c:\WorkFiles\GZQL_MACHINE\Module\Controls\StepDetails\ReleaseDetailViewModel.cs`

- [ ] **Step 1: 创建完整的 ViewModel**

```csharp
using Core.Models;
using Core.Services;
using Core.Utilities;
using MotionControl.Interfaces;
using StationTasks.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Core.Abstraction;
using Prism.Ioc;

namespace Module.ViewModels
{
    public class ReleaseDetailViewModel : BindableBase, INavigationAware
    {
        private readonly IRegionManager _regionManager;
        private readonly IContainerProvider _containerProvider;
        private readonly IAxisConfigurationService _axisConfig;
        private readonly ILoggerService _logger;
        private readonly IGripperService _gripperService;
        private readonly IDialogService _dialogService;
        private readonly IPositionProvider _positionProvider;
        private readonly IStationRegistry _stationRegistry;
        private ProcessStep _step;

        public ProcessStep Step
        {
            get => _step;
            set
            {
                if (SetProperty(ref _step, value) && value != null)
                {
                    if (_step.ReleaseDetail == null)
                        _step.ReleaseDetail = new ReleaseDetail();
                    if (_step.ReleaseDetail.ReleaseMoves == null)
                        _step.ReleaseDetail.ReleaseMoves = new ObservableCollection<SubMove>();
                    InitializeSubMoveRows();
                    RaisePropertyChanged(nameof(JawOpen));
                    RaisePropertyChanged(nameof(JawForce));
                    RaisePropertyChanged(nameof(VacuumPressure));
                    RaisePropertyChanged(nameof(ReleasePosition));
                    RaisePropertyChanged(nameof(ReleaseDelayTime));
                    RaisePropertyChanged(nameof(IsVacuumOn));
                    RaisePropertyChanged(nameof(ReleaseMoves));
                    RaisePropertyChanged(nameof(StepDescription));
                }
            }
        }

        public string StepDescription => _step == null ? "—" : $"Seq{_step.Seq} - {_step.CompFeature ?? "—"} → {_step.SiteFeature ?? "—"}";

        public double JawOpen
        {
            get => _step?.ReleaseDetail?.JawOpen ?? 0;
            set { if (_step?.ReleaseDetail != null) _step.ReleaseDetail.JawOpen = value; }
        }
        public double JawForce
        {
            get => _step?.ReleaseDetail?.JawForce ?? 0;
            set { if (_step?.ReleaseDetail != null) _step.ReleaseDetail.JawForce = value; }
        }
        public int VacuumPressure
        {
            get => _step?.ReleaseDetail?.VacuumPressure ?? 0;
            set { if (_step?.ReleaseDetail != null) _step.ReleaseDetail.VacuumPressure = value; }
        }
        public double ReleasePosition
        {
            get => _step?.ReleaseDetail?.ReleasePosition ?? 0;
            set { if (_step?.ReleaseDetail != null) _step.ReleaseDetail.ReleasePosition = value; }
        }
        public int ReleaseDelayTime
        {
            get => _step?.ReleaseDetail?.ReleaseDelayTime ?? 300;
            set { if (_step?.ReleaseDetail != null) _step.ReleaseDetail.ReleaseDelayTime = value; }
        }
        public bool IsVacuumOn
        {
            get => _step?.ReleaseDetail?.IsVacuumOn ?? false;
            set { if (_step?.ReleaseDetail != null) _step.ReleaseDetail.IsVacuumOn = value; }
        }
        private string _vacuumStatusText;
        public string VacuumStatusText
        {
            get => _vacuumStatusText ?? (_vacuumStatusText = L("ReleaseDetail_VacuumStatus_Off"));
            set => SetProperty(ref _vacuumStatusText, value);
        }

        public ObservableCollection<SubMove> ReleaseMoves
        {
            get
            {
                if (_step?.ReleaseDetail == null) return new ObservableCollection<SubMove>();
                if (_step.ReleaseDetail.ReleaseMoves == null)
                    _step.ReleaseDetail.ReleaseMoves = new ObservableCollection<SubMove>();
                return _step.ReleaseDetail.ReleaseMoves;
            }
        }

        public ObservableCollection<double> JawOpenOptions { get; } = new ObservableCollection<double> { 5.0, 10.0, 15.0, 20.0, 25.0 };
        public ObservableCollection<double> JawForceOptions { get; } = new ObservableCollection<double> { 5.0, 10.0, 15.0, 20.0, 30.0 };

        private ObservableCollection<SubMoveRowViewModel> _subMoveRows;
        public ObservableCollection<SubMoveRowViewModel> SubMoveRows
        {
            get => _subMoveRows;
            set => SetProperty(ref _subMoveRows, value);
        }
        private SubMoveRowViewModel _selectedSubMoveRow;
        public SubMoveRowViewModel SelectedSubMoveRow
        {
            get => _selectedSubMoveRow;
            set => SetProperty(ref _selectedSubMoveRow, value);
        }
        private ObservableCollection<StationItem> _stationItems;
        public ObservableCollection<StationItem> StationItems
        {
            get => _stationItems;
            set => SetProperty(ref _stationItems, value);
        }

        public ICommand AddMoveCommand { get; }
        public ICommand DeleteMoveCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand VacuumOnCommand { get; }
        public ICommand VacuumOffCommand { get; }
        public ICommand QuickReleaseCommand { get; }
        public ICommand OpenGripperControlCommand { get; }

        private string L(string key) => _containerProvider.Resolve<ILocalizationService>().GetResource(key);

        public ReleaseDetailViewModel(
            IRegionManager regionManager,
            IContainerProvider containerProvider,
            IAxisConfigurationService axisConfig,
            ILoggerService logger,
            IGripperService gripperService,
            IDialogService dialogService,
            IPositionProvider positionProvider,
            IStationRegistry stationRegistry)
        {
            _regionManager = regionManager;
            _containerProvider = containerProvider;
            _axisConfig = axisConfig;
            _logger = logger;
            _gripperService = gripperService;
            _dialogService = dialogService;
            _positionProvider = positionProvider;
            _stationRegistry = stationRegistry;

            AddMoveCommand = new DelegateCommand(OnAddSubMove);
            DeleteMoveCommand = new DelegateCommand(OnDeleteSubMove, () => SelectedSubMoveRow != null).ObservesProperty(() => SelectedSubMoveRow);
            MoveUpCommand = new DelegateCommand(OnMoveUp, () => SelectedSubMoveRow != null && SubMoveRows.IndexOf(SelectedSubMoveRow) > 0).ObservesProperty(() => SelectedSubMoveRow);
            MoveDownCommand = new DelegateCommand(OnMoveDown, () => SelectedSubMoveRow != null && SubMoveRows.IndexOf(SelectedSubMoveRow) < SubMoveRows.Count - 1).ObservesProperty(() => SelectedSubMoveRow);
            CloseCommand = new DelegateCommand(OnClose);
            SaveCommand = new DelegateCommand(OnSave);
            VacuumOnCommand = new DelegateCommand(() => IsVacuumOn = true);
            VacuumOffCommand = new DelegateCommand(() => IsVacuumOn = false);
            QuickReleaseCommand = new DelegateCommand(async () => await OnQuickReleaseAsync());
            OpenGripperControlCommand = new DelegateCommand(OnOpenGripperControl);

            LoadStations();
        }

        private void LoadStations()
        {
            var stations = _stationRegistry.GetAllStations();
            StationItems = new ObservableCollection<StationItem>(
                stations.Select(s => new StationItem
                {
                    StationId = s.StationIdentifier,
                    DisplayName = s.StationIdentifier
                }));
        }

        private void InitializeSubMoveRows()
        {
            if (_step?.ReleaseDetail?.ReleaseMoves == null) return;
            var rows = new ObservableCollection<SubMoveRowViewModel>();
            foreach (var move in _step.ReleaseDetail.ReleaseMoves)
            {
                var row = new SubMoveRowViewModel(move, _positionProvider);
                rows.Add(row);
                if (!string.IsNullOrEmpty(move.StationId))
                    row.LoadAxesAndPositionsAsync(move.StationId).ConfigureAwait(false);
            }
            SubMoveRows = rows;
        }

        private void SyncRowsToStep()
        {
            if (_step?.ReleaseDetail == null) return;
            _step.ReleaseDetail.ReleaseMoves = new ObservableCollection<SubMove>(
                SubMoveRows.Select(r => r.SubMove));
        }

        private void OnAddSubMove()
        {
            var newMove = new SubMove
            {
                SubSeq = ((char)('a' + SubMoveRows.Count)).ToString(),
                Axis = "",
                PositionName = "",
                Offset = 0,
                Speed = 50,
                Description = ""
            };
            var row = new SubMoveRowViewModel(newMove, _positionProvider);
            SubMoveRows.Add(row);
        }

        private void OnDeleteSubMove()
        {
            if (SelectedSubMoveRow != null)
                SubMoveRows.Remove(SelectedSubMoveRow);
            UpdateSequences();
        }

        private void OnMoveUp()
        {
            int idx = SubMoveRows.IndexOf(SelectedSubMoveRow);
            SubMoveRows.Move(idx, idx - 1);
            UpdateSequences();
        }

        private void OnMoveDown()
        {
            int idx = SubMoveRows.IndexOf(SelectedSubMoveRow);
            SubMoveRows.Move(idx, idx + 1);
            UpdateSequences();
        }

        private void UpdateSequences()
        {
            for (int i = 0; i < SubMoveRows.Count; i++)
                SubMoveRows[i].SubSeq = ((char)('a' + i)).ToString();
        }

        private void OnClose()
        {
            try
            {
                var session = MaterialDesignThemes.Wpf.DialogHost.GetDialogSession("MainDialogHost");
                session?.Close(false);
            }
            catch (InvalidOperationException) { }
        }

        private void OnSave()
        {
            SyncRowsToStep();
            OnClose();
        }

        private async Task OnQuickReleaseAsync()
        {
            var result = await ShowConfirmationAsync(
                L("ReleaseDetail_ConfirmRelease_Title"),
                string.Format(L("ReleaseDetail_ConfirmRelease_Msg"), ReleasePosition));
            if (result != ButtonResult.Yes) return;

            try
            {
                await _gripperService.ReleaseAsync(ReleasePosition);
                _logger.Info(string.Format(L("ReleaseDetail_Log_ReleaseDone"), ReleasePosition));
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(L("ReleaseDetail_Log_ReleaseFailed"), ex.Message));
                ShowAlert(L("ReleaseDetail_Alert_ReleaseFailed"), ex.Message);
            }
        }

        private void OnOpenGripperControl()
        {
            var parameters = new DialogParameters
            {
                { "clampPosition", 0 },
                { "releasePosition", ReleasePosition }
            };
            _dialogService.ShowDialog("GripperControlView", parameters, result =>
            {
                if (result.Result == ButtonResult.OK && result.Parameters != null)
                {
                    if (result.Parameters.ContainsKey("releasePosition"))
                        ReleasePosition = result.Parameters.GetValue<double>("releasePosition");
                }
            });
        }

        private async Task<ButtonResult> ShowConfirmationAsync(string title, string message)
        {
            var tcs = new TaskCompletionSource<ButtonResult>();
            _dialogService.ShowDialog("ConfirmationDialog", new DialogParameters
            {
                { "title", title },
                { "message", message }
            }, result => tcs.SetResult(result.Result));
            return await tcs.Task;
        }

        private void ShowAlert(string title, string message)
        {
            _dialogService.ShowDialog("NotificationDialog", new DialogParameters
            {
                { "title", title },
                { "message", message }
            }, result => { });
        }

        #region INavigationAware
        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _step = navigationContext.Parameters.GetValue<ProcessStep>("step");
            if (_step == null) return;

            if (_step.ReleaseDetail == null)
                _step.ReleaseDetail = new ReleaseDetail();
            if (_step.ReleaseDetail.ReleaseMoves == null)
                _step.ReleaseDetail.ReleaseMoves = new ObservableCollection<SubMove>();

            InitializeSubMoveRows();

            RaisePropertyChanged(nameof(JawOpen));
            RaisePropertyChanged(nameof(JawForce));
            RaisePropertyChanged(nameof(VacuumPressure));
            RaisePropertyChanged(nameof(ReleasePosition));
            RaisePropertyChanged(nameof(ReleaseDelayTime));
            RaisePropertyChanged(nameof(IsVacuumOn));
            RaisePropertyChanged(nameof(ReleaseMoves));
            RaisePropertyChanged(nameof(StepDescription));
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
        #endregion
    }
}
```

**与 PickDetailViewModel 的关键差异：**
- 类名: `ReleaseDetailViewModel`
- 模型引用: `_step.ReleaseDetail` 替代 `_step.PickDetail`
- 属性: 移除 `ClampPosition`, `SkipClampCheck`, `PickHoldingTime`, `VacuumCheckDelay`
- 新增: `ReleaseDelayTime` 属性
- 集合: `ReleaseMoves` 替代 `PickMoves`
- 命令: 移除 `QuickClampCommand`, 保留 `QuickReleaseCommand`
- 动作命令: `AddMoveCommand`/`DeleteMoveCommand`（非 AddPickMoveCommand）
- 资源键前缀: `ReleaseDetail_` 替代 `PickDetail_`
- `OnOpenGripperControl`: clampPosition 传 0（Release 不需要夹紧位置）
- `OnNavigatedTo`: 初始化 ReleaseDetail 而非 PickDetail

- [ ] **Step 2: 验证编译通过**

---

## Task 6: 注册 ReleaseDetailView 并集成到步骤编辑器

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\Module\PrimModel.cs`
- Modify: `c:\WorkFiles\GZQL_MACHINE\Module\Controls\StepEditor\ProcessSequenceEditorViewModel.cs`

- [ ] **Step 1: 在 PrimModel.cs 中注册导航**

在 PickDetailView 注册行（约第83行）之后添加：

```csharp
containerRegistry.RegisterForNavigation<ReleaseDetailView, ReleaseDetailViewModel>();
```

- [ ] **Step 2: 在 ProcessSequenceEditorViewModel.cs 中添加 RELEASE 分支**

**6a. 添加 ShowReleaseDetailDialog 方法**

在 `ShowPickDetailDialog` 方法（约第432行）之后添加：

```csharp
private async void ShowReleaseDetailDialog(ProcessStep step)
{
    var vm = _containerProvider.Resolve<ReleaseDetailViewModel>();
    var view = new ReleaseDetailView();
    view.DataContext = vm;
    vm.Step = step;
    await ShowDialogSafely(view);
    await AutoSaveSequenceAsync();
}
```

**6b. 在步骤类型分支中添加 RELEASE 调用**

在 PICK 分支（约第307-310行）之后添加：

```csharp
else if (step.Step == StepType.RELEASE)
{
    ShowReleaseDetailDialog(step);
}
```

最终代码结构应为：
```csharp
else if (step.Step == StepType.PICK)
{
    ShowPickDetailDialog(step);
}
else if (step.Step == StepType.RELEASE)
{
    ShowReleaseDetailDialog(step);
}
```

- [ ] **Step 3: 验证全量编译通过**

Run: `dotnet build GZQL_MACHINE.sln --configuration Debug`
Expected: Build succeeded, 0 errors related to ReleaseDetail

---

## Task 7: 集成验证

**Files:** No new files (manual verification)

- [ ] **Step 1: 全量编译验证**

Run: `dotnet build GZQL_MACHINE.sln --configuration Debug`
Expected: Build succeeded

- [ ] **Step 2: 功能验证清单**

| # | 验证项 | 操作步骤 | 预期结果 |
|---|--------|----------|----------|
| 1 | RELEASE 步骤打开详情 | 步骤序列器中双击 RELEASE 类型步骤 | 弹出 ReleaseDetailView 对话框 |
| 2 | 标题显示 | 观察标题栏 | 显示 `RELEASE SeqX - 特征 → 工位` |
| 3 | 张开度下拉 | 点击张开度 ComboBox | 显示 5/10/15/20/25 选项 |
| 4 | 夹紧力下拉 | 点击夹紧力 ComboBox | 显示 5/10/15/20/30 选项 |
| 5 | 真空控制 | 点击 开启/关闭 按钮 | IsVacuumOn 状态切换，状态文本更新 |
| 6 | 释放位置 | 输入数值并点击释放按钮 | 弹出确认对话框，确认后调用 GripperService |
| 7 | 下放延时时间 | 输入数值如 `500` | ReleaseDelayTime 属性正确更新 |
| 8 | 夹爪控制面板 | 点击「打开夹爪控制面板」| 弹出 GripperControlView 对话框 |
| 9 | 动作表格-添加 | 点击「➕添加」| 新增一行 SubMove，子序自动递增(a,b,c...) |
| 10 | 动作表格-选择工站 | 编辑工站列 | 显示 StationItems 下拉列表 |
| 11 | 动作表格-选轴/位置 | 选择工站后编辑轴/位置列 | 显示该工站对应的轴和位置选项 |
| 12 | 动作表格-排序 | 选择行后点击 ↑/↓ | 行顺序调整，子序重新编号 |
| 13 | 动作表格-删除 | 选择行后点击删除 | 行被移除 |
| 14 | 保存 | 点击保存按钮 | ReleaseMoves 同步回 Step.ReleaseDetail，对话框关闭 |
| 15 | 取消 | 点击取消按钮 | 对话框关闭不保存 |
| 16 | 中英文切换 | 切换界面语言 | 所有 ReleaseDetail 资源键正确切换 |

---

## 实施依赖关系

```
Task 1 (ReleaseDetail 模型)
   ↓
Task 2 (多语言资源) ──┬──→ Task 3 (XAML View)
   │                     ↓
Task 4 (Code-Behind) ──→ Task 5 (ViewModel) ──→ Task 6 (注册+集成) ──→ Task 7 (验证)
```

Task 1, 2, 4 无依赖可并行。Task 3 依赖 Task 2（资源键）。Task 5 依赖 Task 1（模型类）。Task 6 依赖 Task 3+4+5。

---

## 风险与注意事项

1. **ProcessStep.Json 反序列化**: 新增 `ReleaseDetail` 属性不影响已有 JSON 文件（默认为 null，旧数据不受影响）
2. **SubMoveRowViewModel 复用**: 动作行 ViewModel 完全通用，不需要任何修改
3. **GripperControlView 参数**: Release 不需要 ClampPosition，传入 0 作为占位值
4. **IGripperService.ReleaseAsync**: 确认该方法已存在且签名兼容（从 PickDetailViewModel 的使用可知已存在）
5. **IPositionProvider**: 复用 Pick 的同一实例，轴/位置加载逻辑一致
