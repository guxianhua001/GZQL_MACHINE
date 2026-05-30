# CureDetailView（UV固化）实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 参考 PickDetailView 设计并实现 CureDetailView（UV固化步骤详情配置视图），包含UV头选择、固化时间、4阶段参数（持续时间+强度）、动作表格

**Architecture:** 完全对标 PickDetailView/ReleaseDetailView 的三层架构（XAML View → ViewModel → Model），新增 CureDetail 模型类挂载到 ProcessStep，通过 Prism DialogHost 弹窗集成到步骤序列编辑器

**Tech Stack:** WPF + PRISM 9 + MaterialDesignInXAML + .NET 9.0-windows7.0

---

## 功能需求分析

### 用户需求拆解

| # | 需求 | 说明 |
|---|------|------|
| 1 | **UV头选择** | 支持 Head1 和 Head2 两个选项，RadioButton 或 ComboBox 选择 |
| 2 | **固化时间** | 总固化时间（毫秒或秒） |
| 3 | **阶段1-4 参数** | 每个阶段包含：持续时间 + 强度（%或 mW/cm²） |
| 4 | **动作表格** | 与 PickDetailView 对齐的 SubMove 表格（子序\|工站\|轴\|位置\|偏移\|速度\|描述） |

### UI 布局设计（800px 宽）

```
┌─────────────────────────────────────────────────────┐
│ 标题栏: CURE {StepDescription}    [✖关闭]          │
├─────────────────────────────────────────────────────┤
│ 💡 UV固化配置 (GroupBox)                             │
│ ┌──────────────────────────┬──────────────────────┐ │
│ │ UV头选择                │ ○ Head1  ● Head2      │ │
│ │ 固化时间(ms)            │ [TextBox]             │ │
│ ├──────────────────────────┼──────────────────────┤ │
│ │ 阶段1 持续时间(ms)      │ [TextBox]            │ │
│ │ 阶段1 强度(%)           │ [TextBox]            │ │
│ ├──────────────────────────┼──────────────────────┤ │
│ │ 阶段2 持续时间(ms)      │ [TextBox]            │ │
│ │ 阶段2 强度(%)           │ [TextBox]            │ │
│ ├──────────────────────────┼──────────────────────┤ │
│ │ 阶段3 持续时间(ms)      │ [TextBox]            │ │
│ │ 阶段3 强度(%)           │ [TextBox]            │ │
│ ├──────────────────────────┼──────────────────────┤ │
│ │ 阶段4 持续时间(ms)      │ [TextBox]            │ │
│ │ 阶段4 强度(%)           │ [TextBox]            │ │
│ └──────────────────────────┴──────────────────────┘ │
├─────────────────────────────────────────────────────┤
│ 📋 固化动作序列 (GroupBox)                          │
│ ┌──────────────────────────────────────────────────┐│
│ │ DataGrid: 子序|工站|轴|位置|偏移|速度|描述       ││
│ │ [➕添加] [🗑删除] [↑] [↓]                       ││
│ └──────────────────────────────────────────────────┘│
├─────────────────────────────────────────────────────┤
│                    [取消] [保存]                      │
└─────────────────────────────────────────────────────┘
```

---

## 数据模型设计 — CureDetail

### 属性清单

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| UvHeadIndex | int | 1 | UV头选择（1=Head1, 2=Head2） |
| CureTimeMs | int | 5000 | 总固化时间（毫秒） |
| Stage1DurationMs | int | 1000 | 阶段1 持续时间（ms） |
| Stage1Intensity | double | 50.0 | 阶段1 强度（%） |
| Stage2DurationMs | int | 1000 | 阶段2 持续时间（ms） |
| Stage2Intensity | double | 80.0 | 阶段2 强度（%） |
| Stage3DurationMs | int | 1000 | 阶段3 持续时间（ms） |
| Stage3Intensity | double | 100.0 | 阶段3 强度（%） |
| Stage4DurationMs | int | 2000 | 阶段4 持续时间（ms） |
| Stage4Intensity | double | 80.0 | 阶段4 强度（%） |
| CureMoves | ObservableCollection\<SubMove\> | [] | 固化动作序列 |

---

## 文件变更清单

| # | 文件路径 | 操作 | 说明 | 状态 |
|---|----------|------|------|------|
| 1 | `StationTasks/Models/ProcessStep.cs` | **修改** | 新增 CureDetail 类定义 + ProcessStep.CureDetail 属性 | ✅ 已完成 |
| 2 | `Module/Controls/StepDetails/CureDetailView.xaml` | **新建** | XAML 视图（UV配置区 + 4阶段参数 + 动作表格） | ✅ 已完成 |
| 3 | `Module/Controls/StepDetails/CureDetailView.xaml.cs` | **新建** | Code-Behind（空壳，仅 InitializeComponent） | ✅ 已完成 |
| 4 | `Module/Controls/StepDetails/CureDetailViewModel.cs` | **新建** | ViewModel（对标 PickDetailViewModel） | ✅ 已完成 |
| 5 | `Module/PrimModel.cs` | **修改** | 注册 CureDetailView 导航 | ✅ 已完成 |
| 6 | `Module/Controls/StepEditor/ProcessSequenceEditorViewModel.cs` | **修改** | 新增 ShowCureDetailDialog + CURE 分支调用 | ✅ 已完成 |
| 7 | `MainApp/Languages/Strings.zh-CN.xaml` | **修改** | 新增 CureDetail 中文资源键 | ✅ 已完成 |
| 8 | `MainApp/Languages/Strings.en-US.xaml` | **修改** | 新增 CureDetail 英文资源键 | ✅ 已完成 |

**复用文件（无需修改）：**
- `Module/Controls/StepEditor/SubMoveRowViewModel.cs`
- `StationTasks/Models/ProcessStep.cs` 中的 `SubMove` 类

---

## Task 1: 创建 CureDetail 数据模型 ✅ 已完成

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\StationTasks\Models\ProcessStep.cs`

- [x] **Step 1: 在 ReleaseDetail 类定义之后添加 CureDetail类**

在 `ReleaseDetail` 类的闭合大括号之后（约第396行后）添加：

```csharp
    public class CureDetail : BindableBase
    {
        private int _uvHeadIndex = 1;
        private int _cureTimeMs = 5000;
        private int _stage1DurationMs = 1000;
        private double _stage1Intensity = 50.0;
        private int _stage2DurationMs = 1000;
        private double _stage2Intensity = 80.0;
        private int _stage3DurationMs = 1000;
        private double _stage3Intensity = 100.0;
        private int _stage4DurationMs = 2000;
        private double _stage4Intensity = 80.0;

        public int UvHeadIndex { get => _uvHeadIndex; set => SetProperty(ref _uvHeadIndex, value); }
        public int CureTimeMs { get => _cureTimeMs; set => SetProperty(ref _cureTimeMs, value); }
        public int Stage1DurationMs { get => _stage1DurationMs; set => SetProperty(ref _stage1DurationMs, value); }
        public double Stage1Intensity { get => _stage1Intensity; set => SetProperty(ref _stage1Intensity, value); }
        public int Stage2DurationMs { get => _stage2DurationMs; set => SetProperty(ref _stage2DurationMs, value); }
        public double Stage2Intensity { get => _stage2Intensity; set => SetProperty(ref _stage2Intensity, value); }
        public int Stage3DurationMs { get => _stage3DurationMs; set => SetProperty(ref _stage3DurationMs, value); }
        public double Stage3Intensity { get => _stage3Intensity; set => SetProperty(ref _stage3Intensity, value); }
        public int Stage4DurationMs { get => _stage4DurationMs; set => SetProperty(ref _stage4DurationMs, value); }
        public double Stage4Intensity { get => _stage4Intensity; set => SetProperty(ref _stage4Intensity, value); }

        public ObservableCollection<SubMove> CureMoves { get; set; } = new ObservableCollection<SubMove>();
    }
```

- [x] **Step 2: 在 ProcessStep 类中添加 CureDetail 属性**

在 `ReleaseDetail` 属性之后、`IpqcDetail` 属性之前添加：

```csharp
        private CureDetail _cureDetail;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public CureDetail CureDetail
        {
            get => _cureDetail;
            set
            {
                if (_cureDetail != value)
                {
                    _cureDetail = value;
                    OnPropertyChanged();
                }
            }
        }
```

- [x] **Step 3: 验证编译通过**

Run: `dotnet build StationTasks/StationTasks.csproj --configuration Debug`
Expected: Build succeeded

---

## Task 2: 新增多语言资源键 ✅ 已完成

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\MainApp\Languages\Strings.zh-CN.xaml`
- Modify: `c:\WorkFiles\GZQL_MACHINE\MainApp\Languages\Strings.en-US.xaml`

- [x] **Step 1: 在 Strings.zh-CN.xaml 中 ReleaseDetailView 资源键区域之后插入**

在 `ReleaseDetail_Alert_ReleaseFailed` 行之后、下一个注释块之前插入：

```xml
    <!-- ═══ CureDetailView - UV固化配置详情 ═══ -->
    <sys:String x:Key="CureDetail_UvConfig">💡 UV固化配置</sys:String>
    <sys:String x:Key="CureDetail_UvHeadSelect">UV头选择</sys:String>
    <sys:String x:Key="CureDetail_Head1">Head 1</sys:String>
    <sys:String x:Key="CureDetail_Head2">Head 2</sys:String>
    <sys:String x:Key="CureDetail_CureTime">固化时间 (ms)</sys:String>
    <sys:String x:Key="CureDetail_Stage1">阶段 1</sys:String>
    <sys:String x:Key="CureDetail_Stage2">阶段 2</sys:String>
    <sys:String x:Key="CureDetail_Stage3">阶段 3</sys:String>
    <sys:String x:Key="CureDetail_Stage4">阶段 4</sys:String>
    <sys:String x:Key="CureDetail_Duration">持续时间 (ms)</sys:String>
    <sys:String x:Key="CureDetail_Intensity">强度 (%)</sys:String>

    <!-- 动作序列 -->
    <sys:String x:Key="CureDetail_CureMotionSeq">📋 固化动作序列</sys:String>
    <sys:String x:Key="CureDetail_Column_Sub">子序</sys:String>
    <sys:String x:Key="CureDetail_Column_Station">工站</sys:String>
    <sys:String x:Key="CureDetail_Column_Axis">轴</sys:String>
    <sys:String x:Key="CureDetail_Column_Position">位置</sys:String>
    <sys:String x:Key="CureDetail_Column_Offset">偏移(mm)</sys:String>
    <sys:String x:Key="CureDetail_Column_Speed">速度</sys:String>
    <sys:String x:Key="CureDetail_Column_Description">描述</sys:String>
```

- [x] **Step 2: 在 Strings.en-US.xaml 中对应位置插入英文资源键**

在 `ReleaseDetail_Alert_ReleaseFailed` 行之后插入：

```xml
    <!-- CureDetailView - UV Curing Configuration Details -->
    <sys:String x:Key="CureDetail_UvConfig">UV Curing Config</sys:String>
    <sys:String x:Key="CureDetail_UvHeadSelect">UV Head Select</sys:String>
    <sys:String x:Key="CureDetail_Head1">Head 1</sys:String>
    <sys:String x:Key="CureDetail_Head2">Head 2</sys:String>
    <sys:String x:Key="CureDetail_CureTime">Cure Time (ms)</sys:String>
    <sys:String x:Key="CureDetail_Stage1">Stage 1</sys:String>
    <sys:String x:Key="CureDetail_Stage2">Stage 2</sys:String>
    <sys:String x:Key="CureDetail_Stage3">Stage 3</sys:String>
    <sys:String x:Key="CureDetail_Stage4">Stage 4</sys:String>
    <sys:String x:Key="CureDetail_Duration">Duration (ms)</sys:String>
    <sys:String x:Key="CureDetail_Intensity">Intensity (%)</sys:String>

    <sys:String x:Key="CureDetail_CureMotionSeq">Cure Motion Seq</sys:String>
    <sys:String x:Key="CureDetail_Column_Sub">Sub</sys:String>
    <sys:String x:Key="CureDetail_Column_Station">Station</sys:String>
    <sys:String x:Key="CureDetail_Column_Axis">Axis</sys:String>
    <sys:String x:Key="CureDetail_Column_Position">Position</sys:String>
    <sys:String x:Key="CureDetail_Column_Offset">Offset(mm)</sys:String>
    <sys:String x:Key="CureDetail_Column_Speed">Speed</sys:String>
    <sys:String x:Key="CureDetail_Column_Description">Desc</sys:String>
```

---

## Task 3: 创建 CureDetailView XAML ✅ 已完成

**Files:**
- Create: `c:\WorkFiles\GZQL_MACHINE\Module\Controls\StepDetails\CureDetailView.xaml`

- [x] **Step 1: 创建完整的 CureDetailView.xaml**

```xml
<UserControl x:Class="Module.Views.CureDetailView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes"
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
                           Text="{Binding StepDescription, StringFormat=CURE {0}}" />
            </DockPanel>

            <!-- UV固化配置区域 -->
            <GroupBox Header="{lang:Lang CureDetail_UvConfig}" Margin="0,0,0,16">
                <DockPanel>
                    <Grid Margin="8" DockPanel.Dock="Top">
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                        </Grid.RowDefinitions>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="150"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>

                        <!-- Row 0: UV头选择 -->
                        <TextBlock Grid.Row="0" Grid.Column="0" Text="{lang:Lang CureDetail_UvHeadSelect}" VerticalAlignment="Center"/>
                        <StackPanel Grid.Row="0" Grid.Column="1" Orientation="Horizontal" Margin="4,2">
                            <RadioButton Content="{lang:Lang CureDetail_Head1}"
                                         IsChecked="{Binding IsHead1Selected}"
                                         GroupName="UvHeadGroup"
                                         Margin="0,0,12,0" />
                            <RadioButton Content="{lang:Lang CureDetail_Head2}"
                                         IsChecked="{Binding IsHead2Selected}"
                                         GroupName="UvHeadGroup" />
                        </StackPanel>

                        <!-- Row 1: 固化时间 -->
                        <TextBlock Grid.Row="1" Grid.Column="0" Text="{lang:Lang CureDetail_CureTime}" VerticalAlignment="Center"/>
                        <TextBox Grid.Row="1" Grid.Column="1"
                                 Text="{Binding CureTimeMs}"
                                 Margin="4,2"
                                 Width="120"
                                 HorizontalAlignment="Left"
                                 materialDesign:HintAssist.Hint="milliseconds"/>

                        <!-- 分隔线效果：空行 -->
                        <Border Grid.Row="2" Grid.Column="0" Grid.ColumnSpan="2" Height="1" Background="{DynamicResource MaterialDesignDividerBrush}" Margin="0,8,0,8"/>

                        <!-- 阶段 1 -->
                        <TextBlock Grid.Row="3" Grid.Column="0" Text="{lang:Lang CureDetail_Stage1}" FontWeight="Bold" VerticalAlignment="Center"/>
                        <TextBlock Grid.Row="3" Grid.Column="1" Foreground="{DynamicResource MaterialDesign.Brush.Primary}" FontSize="11" VerticalAlignment="Center" Margin="4,2">━━━━━━━━━━━━━━━━━━━</TextBlock>

                        <TextBlock Grid.Row="4" Grid.Column="0" Text="{lang:Lang CureDetail_Duration}" VerticalAlignment="Center" Margin="20,0,0,0"/>
                        <TextBox Grid.Row="4" Grid.Column="1" Text="{Binding Stage1DurationMs}" Margin="4,2" Width="120" HorizontalAlignment="Left" materialDesign:HintAssist.Hint="ms"/>

                        <TextBlock Grid.Row="5" Grid.Column="0" Text="{lang:Lang CureDetail_Intensity}" VerticalAlignment="Center" Margin="20,0,0,0"/>
                        <TextBox Grid.Row="5" Grid.Column="1" Text="{Binding Stage1Intensity}" Margin="4,2" Width="120" HorizontalAlignment="Left" materialDesign:HintAssist.Hint="%"/>

                        <!-- 阶段 2 -->
                        <TextBlock Grid.Row="6" Grid.Column="0" Text="{lang:Lang CureDetail_Stage2}" FontWeight="Bold" VerticalAlignment="Center"/>
                        <TextBlock Grid.Row="6" Grid.Column="1" Foreground="{DynamicResource MaterialDesign.Brush.Primary}" FontSize="11" VerticalAlignment="Center" Margin="4,2">━━━━━━━━━━━━━━━━━━━</TextBlock>

                        <TextBlock Grid.Row="7" Grid.Column="0" Text="{lang:Lang CureDetail_Duration}" VerticalAlignment="Center" Margin="20,0,0,0"/>
                        <TextBox Grid.Row="7" Grid.Column="1" Text="{Binding Stage2DurationMs}" Margin="4,2" Width="120" HorizontalAlignment="Left" materialDesign:HintAssist.Hint="ms"/>

                        <TextBlock Grid.Row="8" Grid.Column="0" Text="{lang:Lang CureDetail_Intensity}" VerticalAlignment="Center" Margin="20,0,0,0"/>
                        <TextBox Grid.Row="8" Grid.Column="1" Text="{Binding Stage2Intensity}" Margin="4,2" Width="120" HorizontalAlignment="Left" materialDesign:HintAssist.Hint="%"/>

                        <!-- 阶段 3 -->
                        <TextBlock Grid.Row="9" Grid.Column="0" Text="{lang:Lang CureDetail_Stage3}" FontWeight="Bold" VerticalAlignment="Center"/>
                        <TextBlock Grid.Row="9" Grid.Column="1" Foreground="{DynamicResource MaterialDesign.Brush.Primary}" FontSize="11" VerticalAlignment="Center" Margin="4,2">━━━━━━━━━━━━━━━━━━━</TextBlock>

                        <TextBlock Grid.Row="10" Grid.Column="0" Text="{lang:Lang CureDetail_Duration}" VerticalAlignment="Center" Margin="20,0,0,0"/>
                        <TextBox Grid.Row="10" Grid.Column="1" Text="{Binding Stage3DurationMs}" Margin="4,2" Width="120" HorizontalAlignment="Left" materialDesign:HintAssist.Hint="ms"/>
                    </Grid>

                    <!-- 阶段3强度 + 阶段4 全部参数（用内嵌 StackPanel 延续） -->
                    <StackPanel Margin="160,0,0,0">
                        <Grid>
                            <Grid.RowDefinitions>
                                <RowDefinition Height="Auto"/><RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/><RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/><RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/><RowDefinition Height="Auto"/>
                            </Grid.RowDefinitions>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="130"/><ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>

                            <TextBlock Grid.Row="0" Grid.Column="0" Text="{lang:Lang CureDetail_Intensity}" VerticalAlignment="Center"/>
                            <TextBox Grid.Row="0" Grid.Column="1" Text="{Binding Stage3Intensity}" Margin="4,2" Width="120" HorizontalAlignment="Left" materialDesign:HintAssist.Hint="%"/>

                            <TextBlock Grid.Row="1" Grid.Column="0" Grid.ColumnSpan="2" Text="{lang:Lang CureDetail_Stage4}" FontWeight="Bold" Margin="0,8,0,0"/>
                            <TextBlock Grid.Row="2" Grid.Column="0" Grid.ColumnSpan="2" Foreground="{DynamicResource MaterialDesign.Brush.Primary}" FontSize="11">━━━━━━━━━━━━━━━━━━━</TextBlock>

                            <TextBlock Grid.Row="3" Grid.Column="0" Text="{lang:Lang CureDetail_Duration}" VerticalAlignment="Center"/>
                            <TextBox Grid.Row="3" Grid.Column="1" Text="{Binding Stage4DurationMs}" Margin="4,2" Width="120" HorizontalAlignment="Left" materialDesign:HintAssist.Hint="ms"/>

                            <TextBlock Grid.Row="4" Grid.Column="0" Text="{lang:Lang CureDetail_Intensity}" VerticalAlignment="Center"/>
                            <TextBox Grid.Row="4" Grid.Column="1" Text="{Binding Stage4Intensity}" Margin="4,2" Width="120" HorizontalAlignment="Left" materialDesign:HintAssist.Hint="%"/>
                        </Grid>
                    </StackPanel>
                </DockPanel>
            </GroupBox>

            <!-- 固化动作表格（与 Pick/Release 完全一致的列结构） -->
            <GroupBox Header="{lang:Lang CureDetail_CureMotionSeq}" Margin="0,0,0,8">
                <StackPanel>
                    <DataGrid ItemsSource="{Binding SubMoveRows}"
                              SelectedItem="{Binding SelectedSubMoveRow}"
                              AutoGenerateColumns="False"
                              CanUserAddRows="False"
                              CanUserDeleteRows="False"
                              materialDesign:DataGridAssist.CellPadding="4"
                              MaxHeight="300">
                        <DataGrid.Columns>
                            <DataGridTextColumn Header="{lang:Lang CureDetail_Column_Sub}" Binding="{Binding SubSeq}" Width="50" IsReadOnly="True"/>

                            <DataGridTemplateColumn Header="{lang:Lang CureDetail_Column_Station}" Width="130">
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

                            <DataGridTemplateColumn Header="{lang:Lang CureDetail_Column_Position}" Width="120">
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

                            <DataGridTemplateColumn Header="{lang:Lang CureDetail_Column_Offset}" Width="70">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <TextBox Text="{Binding Offset, UpdateSourceTrigger=PropertyChanged}" />
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>

                            <DataGridTemplateColumn Header="{lang:Lang CureDetail_Column_Speed}" Width="60">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <TextBox Text="{Binding Speed, UpdateSourceTrigger=PropertyChanged}" />
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>

                            <DataGridTextColumn Header="{lang:Lang CureDetail_Column_Description}" Binding="{Binding Description}" Width="*"/>
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

**XAML 关键设计点：**
- UV头选择使用 RadioButton（GroupName="UvHeadGroup"，互斥选择）
- 4个阶段用分隔线视觉分组，每个阶段显示标题+持续时间+强度
- 动作表格完全复用 Pick 的列结构（SubMoveRowViewModel）
- 使用 `{lang:Lang PickDetail_Add/Delete}` 复用已有的添加/删除按钮文本
- **重要修复**: GroupBox 内部使用 `<DockPanel>` 包裹 `<Grid>` 和 `<StackPanel>`，避免 WPF MC3089 错误（GroupBox 只能有一个直接子元素）

---

## Task 4: 创建 CureDetailView Code-Behind ✅ 已完成

**Files:**
- Create: `c:\WorkFiles\GZQL_MACHINE\Module\Controls\StepDetails\CureDetailView.xaml.cs`

- [x] **Step 1: 创建 Code-Behind 文件**

```csharp
using System.Windows.Controls;

namespace Module.Views
{
    public partial class CureDetailView : UserControl
    {
        public CureDetailView()
        {
            InitializeComponent();
        }
    }
}
```

---

## Task 5: 创建 CureDetailViewModel ✅ 已完成

**Files:**
- Create: `c:\WorkFiles\GZQL_MACHINE\Module\Controls\StepDetails\CureDetailViewModel.cs`

- [x] **Step 1: 创建完整的 ViewModel**

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
    public class CureDetailViewModel : BindableBase, INavigationAware
    {
        private readonly IRegionManager _regionManager;
        private readonly IContainerProvider _containerProvider;
        private readonly IAxisConfigurationService _axisConfig;
        private readonly ILoggerService _logger;
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
                    if (_step.CureDetail == null)
                        _step.CureDetail = new CureDetail();
                    if (_step.CureDetail.CureMoves == null)
                        _step.CureDetail.CureMoves = new ObservableCollection<SubMove>();
                    InitializeSubMoveRows();
                    RaisePropertyChanged(nameof(UvHeadIndex));
                    RaisePropertyChanged(nameof(CureTimeMs));
                    RaisePropertyChanged(nameof(Stage1DurationMs));
                    RaisePropertyChanged(nameof(Stage1Intensity));
                    RaisePropertyChanged(nameof(Stage2DurationMs));
                    RaisePropertyChanged(nameof(Stage2Intensity));
                    RaisePropertyChanged(nameof(Stage3DurationMs));
                    RaisePropertyChanged(nameof(Stage3Intensity));
                    RaisePropertyChanged(nameof(Stage4DurationMs));
                    RaisePropertyChanged(nameof(Stage4Intensity));
                    RaisePropertyChanged(nameof(IsHead1Selected));
                    RaisePropertyChanged(nameof(IsHead2Selected));
                    RaisePropertyChanged(nameof(CureMoves));
                    RaisePropertyChanged(nameof(StepDescription));
                }
            }
        }

        public string StepDescription => _step == null ? "—" : $"Seq{_step.Seq} - {_step.CompFeature ?? "—"} → {_step.SiteFeature ?? "—"}";

        public int UvHeadIndex
        {
            get => _step?.CureDetail?.UvHeadIndex ?? 1;
            set { if (_step?.CureDetail != null) _step.CureDetail.UvHeadIndex = value; }
        }
        public bool IsHead1Selected
        {
            get => UvHeadIndex == 1;
            set { if (value) UvHeadIndex = 1; }
        }
        public bool IsHead2Selected
        {
            get => UvHeadIndex == 2;
            set { if (value) UvHeadIndex = 2; }
        }
        public int CureTimeMs
        {
            get => _step?.CureDetail?.CureTimeMs ?? 5000;
            set { if (_step?.CureDetail != null) _step.CureDetail.CureTimeMs = value; }
        }
        public int Stage1DurationMs
        {
            get => _step?.CureDetail?.Stage1DurationMs ?? 1000;
            set { if (_step?.CureDetail != null) _step.CureDetail.Stage1DurationMs = value; }
        }
        public double Stage1Intensity
        {
            get => _step?.CureDetail?.Stage1Intensity ?? 50.0;
            set { if (_step?.CureDetail != null) _step.CureDetail.Stage1Intensity = value; }
        }
        public int Stage2DurationMs
        {
            get => _step?.CureDetail?.Stage2DurationMs ?? 1000;
            set { if (_step?.CureDetail != null) _step.CureDetail.Stage2DurationMs = value; }
        }
        public double Stage2Intensity
        {
            get => _step?.CureDetail?.Stage2Intensity ?? 80.0;
            set { if (_step?.CureDetail != null) _step.CureDetail.Stage2Intensity = value; }
        }
        public int Stage3DurationMs
        {
            get => _step?.CureDetail?.Stage3DurationMs ?? 1000;
            set { if (_step?.CureDetail != null) _step.CureDetail.Stage3DurationMs = value; }
        }
        public double Stage3Intensity
        {
            get => _step?.CureDetail?.Stage3Intensity ?? 100.0;
            set { if (_step?.CureDetail != null) _step.CureDetail.Stage3Intensity = value; }
        }
        public int Stage4DurationMs
        {
            get => _step?.CureDetail?.Stage4DurationMs ?? 2000;
            set { if (_step?.CureDetail != null) _step.CureDetail.Stage4DurationMs = value; }
        }
        public double Stage4Intensity
        {
            get => _step?.CureDetail?.Stage4Intensity ?? 80.0;
            set { if (_step?.CureDetail != null) _step.CureDetail.Stage4Intensity = value; }
        }

        public ObservableCollection<SubMove> CureMoves
        {
            get
            {
                if (_step?.CureDetail == null) return new ObservableCollection<SubMove>();
                if (_step.CureDetail.CureMoves == null)
                    _step.CureDetail.CureMoves = new ObservableCollection<SubMove>();
                return _step.CureDetail.CureMoves;
            }
        }

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

        public CureDetailViewModel(
            IRegionManager regionManager,
            IContainerProvider containerProvider,
            IAxisConfigurationService axisConfig,
            ILoggerService logger,
            IDialogService dialogService,
            IPositionProvider positionProvider,
            IStationRegistry stationRegistry)
        {
            _regionManager = regionManager;
            _containerProvider = containerProvider;
            _axisConfig = axisConfig;
            _logger = logger;
            _dialogService = dialogService;
            _positionProvider = positionProvider;
            _stationRegistry = stationRegistry;

            AddMoveCommand = new DelegateCommand(OnAddSubMove);
            DeleteMoveCommand = new DelegateCommand(OnDeleteSubMove, () => SelectedSubMoveRow != null).ObservesProperty(() => SelectedSubMoveRow);
            MoveUpCommand = new DelegateCommand(OnMoveUp, () => SelectedSubMoveRow != null && SubMoveRows.IndexOf(SelectedSubMoveRow) > 0).ObservesProperty(() => SelectedSubMoveRow);
            MoveDownCommand = new DelegateCommand(OnMoveDown, () => SelectedSubMoveRow != null && SubMoveRows.IndexOf(SelectedSubMoveRow) < SubMoveRows.Count - 1).ObservesProperty(() => SelectedSubMoveRow);
            CloseCommand = new DelegateCommand(OnClose);
            SaveCommand = new DelegateCommand(OnSave);

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
            if (_step?.CureDetail?.CureMoves == null) return;
            var rows = new ObservableCollection<SubMoveRowViewModel>();
            foreach (var move in _step.CureDetail.CureMoves)
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
            if (_step?.CureDetail == null) return;
            _step.CureDetail.CureMoves = new ObservableCollection<SubMove>(
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

        #region INavigationAware
        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _step = navigationContext.Parameters.GetValue<ProcessStep>("step");
            if (_step == null) return;

            if (_step.CureDetail == null)
                _step.CureDetail = new CureDetail();
            if (_step.CureDetail.CureMoves == null)
                _step.CureDetail.CureMoves = new ObservableCollection<SubMove>();

            InitializeSubMoveRows();

            RaisePropertyChanged(nameof(UvHeadIndex));
            RaisePropertyChanged(nameof(CureTimeMs));
            RaisePropertyChanged(nameof(Stage1DurationMs));
            RaisePropertyChanged(nameof(Stage1Intensity));
            RaisePropertyChanged(nameof(Stage2DurationMs));
            RaisePropertyChanged(nameof(Stage2Intensity));
            RaisePropertyChanged(nameof(Stage3DurationMs));
            RaisePropertyChanged(nameof(Stage3Intensity));
            RaisePropertyChanged(nameof(Stage4DurationMs));
            RaisePropertyChanged(nameof(Stage4Intensity));
            RaisePropertyChanged(nameof(IsHead1Selected));
            RaisePropertyChanged(nameof(IsHead2Selected));
            RaisePropertyChanged(nameof(CureMoves));
            RaisePropertyChanged(nameof(StepDescription));
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
        #endregion
    }
}
```

**与 PickDetailViewModel 的关键差异：**
- 类名: `CureDetailViewModel`
- 模型引用: `_step.CureDetail` 替代 `_step.PickDetail`
- 属性: 11 个属性（UvHeadIndex + CureTimeMs + 4阶段×2参数）
- UV头选择: `IsHead1Selected` / `IsHead2Selected` 双向绑定到 RadioButton
- 无夹爪相关命令（VacuumOn/Off、QuickClamp/Release、OpenGripperControl）
- 集合: `CureMoves` 替代 `PickMoves`
- 资源键前缀: `CureDetail_`

---

## Task 6: 注册导航并集成到步骤编辑器 ✅ 已完成

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\Module\PrimModel.cs`
- Modify: `c:\WorkFiles\GZQL_MACHINE\Module\Controls\StepEditor\ProcessSequenceEditorViewModel.cs`

- [x] **Step 1: PrimModel.cs 中注册导航**

在 ReleaseDetailView 注册行之后添加：
```csharp
containerRegistry.RegisterForNavigation<CureDetailView, CureDetailViewModel>();
```

- [x] **Step 2a: ProcessSequenceEditorViewModel.cs 添加 ShowCureDetailDialog 方法**

在 `ShowReleaseDetailDialog` 方法之后添加：
```csharp
private async void ShowCureDetailDialog(ProcessStep step)
{
    var vm = _containerProvider.Resolve<CureDetailViewModel>();
    var view = new CureDetailView();
    view.DataContext = vm;
    vm.Step = step;
    await ShowDialogSafely(view);
    await AutoSaveSequenceAsync();
}
```

- [x] **Step 2b: 添加 CURE 分支调用**

在 RELEASE 分支（约第313行）之后添加：
```csharp
else if (step.Step == StepType.CURE)
{
    ShowCureDetailDialog(step);
}
```

- [x] **Step 3: 验证全量编译通过**

✅ **编译结果**: 0 个错误，1619 个警告（均为已有警告，非本次修改引入）

---

## Task 7: 集成验证与编译检查 ⬜ 待执行

**Files:** No new files (manual verification)

- [ ] **Step 1: 全量编译最终验证**

Run: `dotnet build GZQL_MACHINE.sln --configuration Debug`
Expected: Build succeeded (0 errors)

- [ ] **Step 2: 功能验证清单**

| # | 验证项 | 操作步骤 | 预期结果 |
|---|--------|----------|----------|
| 1 | CURE 步骤打开详情 | 步骤序列器中双击 CURE 类型步骤 | 弹出 CureDetailView 对话框 |
| 2 | 标题显示 | 观察标题栏 | 显示 `CURE SeqX - 特征 → 工位` |
| 3 | UV头选择 | 点击 Head1/Head2 RadioButton | UvHeadIndex 属性正确更新为 1 或 2 |
| 4 | 切换UV头 | 从 Head1 切换到 Head2 | Head1 取消选中，Head2 选中 |
| 5 | 固化时间 | 输入数值如 `8000` | CureTimeMs 正确更新 |
| 6 | 阶段参数 | 输入各阶段的持续时间和强度 | 各属性正确更新 |
| 7 | 动作表格-添加 | 点击「➕添加」| 新增一行 SubMove |
| 8 | 动作表格-操作 | 编辑/删除/排序 | 正常工作 |
| 9 | 保存 | 点击保存按钮 | CureMoves 同步回 Step.CureDetail |
| 10 | 取消 | 点击取消按钮 | 对话框关闭不保存 |
| 11 | 中英文切换 | 切换界面语言 | 所有 CureDetail 资源键正确切换 |

---

## 实施依赖关系

```
Task 1 (CureDetail 模型) ✅
   ↓
Task 2 (多语言资源) ✅ ──┬──→ Task 3 (XAML View) ✅
   │                     ↓
Task 4 (Code-Behind) ✅ ──→ Task 5 (ViewModel) ✅ ──→ Task 6 (注册+集成) ✅ ──→ Task 7 ⬜ 验证
```

Task 1, 2, 4 已完成。Task 3 依赖 Task 2。Task 5 依赖 Task 1。Task 6 依赖 Task 3+4+5。

---

## 风险与注意事项

1. **ProcessStep.Json 反序列化**: 新增 `CureDetail` 属性不影响已有 JSON（默认 null）
2. **SubMoveRowViewModel 完全复用**: 无需任何修改
3. **RadioButton 绑定**: 使用两个 bool 属性 (`IsHead1Selected`, `IsHead2Selected`) 映射到单一 `UvHeadIndex` int 属性，确保双向绑定正确
4. **StepType.CURE 已存在**: 枚举中已有 CURE 值，无需修改枚举
5. **GroupBox 多子元素问题已修复**: 使用 `<DockPanel>` 包裹内部布局避免 MC3089 错误
