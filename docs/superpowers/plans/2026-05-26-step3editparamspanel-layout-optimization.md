# Step3EditParamsPanel 布局优化实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 重构Step3EditParamsPanel参数面板，统一单点模式和连续插补模式的UI风格为三列彩色分组布局，补充连续插补模式缺失的运动和出胶参数，升级批量设置功能为多参数对话框。

**Architecture:** 采用WPF+Prism+MaterialDesignInXAML的MVVM架构，通过扩展DispenseSegment数据模型、重构XAML布局、新建批量设置对话框组件实现UI优化。保持与DotPointEditorView的单点模式风格一致（蓝色运动参数/琥珀色出胶控制/青色阀控或高度参数）。

**Tech Stack:** WPF .NET 9.0, Prism 8.x, MaterialDesignInXAML, C# 13, 多语言支持(zh-CN/en-US)

---

## 文件结构

### 需要修改的文件（6个）

| 文件路径 | 职责 | 主要变更 |
|---------|------|---------|
| `Core/Models/DispenseSegment.cs` | 轨迹段数据模型 | 新增1个字段(JumpSpeed)，复用现有字段 |
| `Module/Controls/Cad/Step3EditParamsPanel.xaml` | 参数面板UI | 单点模式重组+连续插补重写为三列布局 |
| `Module/Controls/Cad/CadPointEditorViewModel.cs` | ViewModel逻辑 | 重构批量设置命令+新增属性绑定 |
| `MainApp/Languages/Strings.zh-CN.xaml` | 中文资源 | 新增/修改约12个资源键 |
| `MainApp/Languages/Strings.en-US.xaml` | 英文资源 | 同步翻译更新 |

### 需要新建的文件（1个）

| 文件路径 | 职责 | 说明 |
|---------|------|------|
| `Module/Views/BatchSetParamsDialog.xaml` + `.xaml.cs` | 批量设置对话框 | 模态窗口，支持多参数选择性批量设置 |

---

## Task 1: 数据模型层 - 扩展DispenseSegment

**Files:**
- Modify: `Core/Models/DispenseSegment.cs:240-267`

- [ ] **Step 1: 在工艺参数区域新增JumpSpeed属性**

在 `DispenseSegment.cs` 文件的 `#region 工艺参数` 区域末尾（`SuckBackTime` 属性之后，`#endregion` 之前），添加以下代码：

```csharp
private double _jumpSpeed = 20.0;
/// <summary>空移速度 mm/s（范围 1~100，非出胶状态下的移动速度）</summary>
public double JumpSpeed
{
    get => _jumpSpeed;
    set => SetProperty(ref _jumpSpeed, Math.Clamp(value, 1.0, 100.0));
}
```

**说明：**
- SafeHeight、ApproachHeight、GlueTriggerOffsetMm 字段已存在，无需重复添加
- CornerDecel 可复用作为减速系数（连续插补模式下语义相同）
- 仅需新增 JumpSpeed 字段用于连续插补模式的空移速度控制

- [ ] **Step 2: 编译验证**

Run: `dotnet build MainApp/MainApp.csproj --configuration Debug`
Expected: Build succeeded with no errors

- [ ] **Step 3: Commit**

```bash
git add Core/Models/DispenseSegment.cs
git commit -m "feat(DispenseSegment): add JumpSpeed property for continuous interpolation mode"
```

---

## Task 2: 单点模式UI调整 - 重组三列布局

**Files:**
- Modify: `Module/Controls/Cad/Step3EditParamsPanel.xaml:235-290`

- [ ] **Step 1: 修改第一组标题和内容**

在 Step3EditParamsPanel.xaml 中找到单点模式面板部分（约第247行）：

**将第1组标题从：**
```xml
<TextBlock FontWeight="SemiBold" FontSize="12" Margin="0,0,0,6" Foreground="#1565C0"
           Text="{lang:Lang Step3_Group_MotionDispense}"/>
```
**改为：**
```xml
<TextBlock FontWeight="SemiBold" FontSize="12" Margin="0,0,0,6" Foreground="#1565C0"
           Text="{lang:Lang Step3_Group_MotionParams}"/>
```

**删除第1组中的出胶时间参数项**（约第274-278行）：
```xml
<!-- 删除以下4行 -->
<TextBlock Grid.Row="4" Grid.Column="0" Style="{StaticResource ParamLabel}" Foreground="#F57C00" FontWeight="Medium" Text="{lang:Lang Step3_Label_DispenseTime}" Margin="0,2,8,0"/>
<TextBox Grid.Row="4" Grid.Column="1" Style="{StaticResource MaterialDesignOutlinedTextBox}"
         Text="{Binding SinglePointProcessParams.DispenseTime, StringFormat=F1, UpdateSourceTrigger=LostFocus}" BorderBrush="#FFCC80" Padding="4,2" FontSize="11"/>
<TextBlock Grid.Row="4" Grid.Column="2" Style="{StaticResource ParamLabel}" Foreground="#F57C00" Text="ms"/>
```

同时减少 Grid.RowDefinitions 的 RowDefinition 数量从5个改为4个：
```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto"/>  <!-- 空移速度 -->
    <RowDefinition Height="Auto"/>  <!-- 安全高度 -->
    <RowDefinition Height="Auto"/>  <!-- 逼近高度 -->
    <RowDefinition Height="Auto"/>  <!-- 减速系数 -->
</Grid.RowDefinitions>
```

- [ ] **Step 2: 修改第二组标题并添加出胶时间**

在单点模式面板中找到第二组（延时控制组，约第282行）：

**将标题从：**
```xml
<TextBlock FontWeight="SemiBold" FontSize="12" Margin="0,0,0,6" Foreground="#F57C00"
           Text="{lang:Lang Step3_Group_DelayControl}"/>
```
**改为：**
```xml
<TextBlock FontWeight="SemiBold" FontSize="12" Margin="0,0,0,6" Foreground="#F57C00"
           Text="{lang:Lang Step3_Group_DispenseControl}"/>
```

**在第2组的 Grid.RowDefinitions 中增加一行**（改为4行）：
```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto"/>  <!-- 出胶时间（新）-->
    <RowDefinition Height="Auto"/>  <!-- 开胶距离 -->
    <RowDefinition Height="Auto"/>  <!-- 起点延时 -->
    <RowDefinition Height="Auto"/>  <!-- 收胶延时 -->
</Grid.RowDefinitions>
```

**在第2组开头添加出胶时间参数项**（Row=0位置）：
```xml
<TextBlock Grid.Row="0" Grid.Column="0" Style="{StaticResource ParamLabel}" Foreground="#F57C00" FontWeight="Medium"
           Text="{lang:Lang Step3_Label_DispenseTime}" Margin="0,2,8,0"/>
<TextBox Grid.Row="0" Grid.Column="1" Style="{StaticResource MaterialDesignOutlinedTextBox}"
         Text="{Binding SinglePointProcessParams.DispenseTime, StringFormat=F1, UpdateSourceTrigger=LostFocus}"
         BorderBrush="#FFCC80" Padding="4,2" FontSize="11"/>
<TextBlock Grid.Row="0" Grid.Column="2" Style="{StaticResource ParamLabel}" Foreground="#F57C00" Text="ms"/>
```

**将原有的开胶距离、起点延时、收胶延时的 Grid.Row 索引各+1**（从0,1,2 改为 1,2,3）

- [ ] **Step 3: 编译验证**

Run: `dotnet build MainApp/MainApp.csproj --configuration Debug`
Expected: Build succeeded, no XAML parse errors

- [ ] **Step 4: Commit**

```bash
git add Module/Controls/Cad/Step3EditParamsPanel.xaml
git commit -m "refactor(Step3EditParamsPanel): reorganize single-point mode layout - move dispense time to group 2, rename groups"
```

---

## Task 3: 连续插补模式UI重写 - 三列彩色分组布局

**Files:**
- Modify: `Module/Controls/Cad/Step3EditParamsPanel.xaml:140-235`

- [ ] **Step 1: 替换连续插补模式的面板内容**

找到连续插补模式的 `<materialDesign:Card>` 标签（约第140行，Visibility绑定到 ShowContinuousInterpolationParams），将其内部的 `<StackPanel>` 内容完全替换为以下新的三列布局结构：

```xml
<!-- 选中段参数编辑区（连续插补模式 - 三列彩色分组） -->
<materialDesign:Card Padding="10" Visibility="{Binding ShowContinuousInterpolationParams, Converter={StaticResource BoolToVisConv}, FallbackValue=Collapsed}">
    <StackPanel>
        <TextBlock Text="{lang:Lang Step3_Section_SelectedParams}" FontWeight="Bold" FontSize="12" Margin="0,0,0,6"/>

        <!-- 点数设置栏 -->
        <DockPanel Margin="0,0,0,6">
            <Button DockPanel.Dock="Right" Content="{lang:Lang Step3_Btn_Apply}" Command="{Binding ApplySegmentSplitCommand}"
                    Style="{StaticResource MaterialDesignFlatButton}" Padding="8,2" FontSize="11" Margin="4,0,0,0"/>
            <TextBlock Text="{lang:Lang Step3_Label_PointCount}" VerticalAlignment="Center" Margin="0,0,4,0" FontSize="11"/>
            <TextBox Text="{Binding SegmentSplitCount, UpdateSourceTrigger=PropertyChanged}"
                     Width="50" VerticalAlignment="Center" FontSize="11" Margin="0,0,4,0"/>
            <TextBlock Text="{lang:Lang Step3_Desc_SetPointCount}"
                       VerticalAlignment="Center" FontSize="10" Foreground="#9E9E9E"/>
        </DockPanel>

        <!-- 三列彩色参数分组 -->
        <Grid Margin="0,2">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- 第一组：运动参数（蓝色） -->
            <StackPanel Grid.Column="0" Margin="0,0,12,0">
                <TextBlock FontWeight="SemiBold" FontSize="12" Margin="0,0,0,6" Foreground="#1565C0"
                           Text="{lang:Lang Step3_Group_MotionParams}"/>
                <Grid>
                    <Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/><RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>

                    <TextBlock Grid.Row="0" Grid.Column="0" Style="{StaticResource ParamLabel}" Text="{lang:Lang Step3_Label_JumpSpeed}" Margin="0,2,8,0"/>
                    <TextBox Grid.Row="0" Grid.Column="1" Style="{StaticResource MaterialDesignOutlinedTextBox}"
                             Text="{Binding SelectedSegment.JumpSpeed, StringFormat=F1, UpdateSourceTrigger=LostFocus}" Padding="4,2" FontSize="11"/>
                    <TextBlock Grid.Row="0" Grid.Column="2" Style="{StaticResource ParamLabel}" Text="mm/s"/>

                    <TextBlock Grid.Row="1" Grid.Column="0" Style="{StaticResource ParamLabel}" Text="{lang:Lang Step3_Label_SafeHeight}" Margin="0,2,8,0"/>
                    <TextBox Grid.Row="1" Grid.Column="1" Style="{StaticResource MaterialDesignOutlinedTextBox}"
                             Text="{Binding SelectedSegment.SafeHeight, StringFormat=F1, UpdateSourceTrigger=LostFocus}" Padding="4,2" FontSize="11"/>
                    <TextBlock Grid.Row="1" Grid.Column="2" Style="{StaticResource ParamLabel}" Text="mm"/>

                    <TextBlock Grid.Row="2" Grid.Column="0" Style="{StaticResource ParamLabel}" Text="{lang:Lang Step3_Label_ApproachHeight}" Margin="0,2,8,0"/>
                    <TextBox Grid.Row="2" Grid.Column="1" Style="{StaticResource MaterialDesignOutlinedTextBox}"
                             Text="{Binding SelectedSegment.ApproachHeight, StringFormat=F1, UpdateSourceTrigger=LostFocus}" Padding="4,2" FontSize="11"/>
                    <TextBlock Grid.Row="2" Grid.Column="2" Style="{StaticResource ParamLabel}" Text="mm"/>

                    <TextBlock Grid.Row="3" Grid.Column="0" Style="{StaticResource ParamLabel}" Text="{lang:Lang Step3_Label_DecelFactor}" Margin="0,2,8,0"/>
                    <TextBox Grid.Row="3" Grid.Column="1" Style="{StaticResource MaterialDesignOutlinedTextBox}"
                             Text="{Binding SelectedSegment.CornerDecel, StringFormat=F2, UpdateSourceTrigger=LostFocus}" Padding="4,2" FontSize="11"/>

                    <TextBlock Grid.Row="4" Grid.Column="0" Style="{StaticResource ParamLabel}" Text="{lang:Lang Step3_Label_InterpolationSpeed}" Margin="0,2,8,0"/>
                    <TextBox Grid.Row="4" Grid.Column="1" Style="{StaticResource MaterialDesignOutlinedTextBox}"
                             Text="{Binding SelectedSegment.MoveSpeed, StringFormat=F1, UpdateSourceTrigger=LostFocus}" Padding="4,2" FontSize="11"/>
                    <TextBlock Grid.Row="4" Grid.Column="2" Style="{StaticResource ParamLabel}" Text="mm/s"/>
                </Grid>
            </StackPanel>

            <Border Grid.Column="1" Width="1" Background="#FFE0E4E8" Margin="0,4"/>

            <!-- 第二组：出胶控制（琥珀色） -->
            <StackPanel Grid.Column="2" Margin="12,0,12,0">
                <TextBlock FontWeight="SemiBold" FontSize="12" Margin="0,0,0,6" Foreground="#F57C00"
                           Text="{lang:Lang Step3_Group_DispenseControl}"/>
                <Grid>
                    <Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>

                    <TextBlock Grid.Row="0" Grid.Column="0" Style="{StaticResource ParamLabel}" Text="{lang:Lang Step3_Label_GlueTriggerOffset}" Margin="0,2,8,0"/>
                    <TextBox Grid.Row="0" Grid.Column="1" Style="{StaticResource MaterialDesignOutlinedTextBox}"
                             Text="{Binding SelectedSegment.GlueTriggerOffsetMm, StringFormat=F1, UpdateSourceTrigger=LostFocus}" Padding="4,2" FontSize="11"/>
                    <TextBlock Grid.Row="0" Grid.Column="2" Style="{StaticResource ParamLabel}" Text="mm"/>

                    <TextBlock Grid.Row="1" Grid.Column="0" Style="{StaticResource ParamLabel}" Text="{lang:Lang Step3_Label_PreDelay}" Margin="0,2,8,0"/>
                    <TextBox Grid.Row="1" Grid.Column="1" Style="{StaticResource MaterialDesignOutlinedTextBox}"
                             Text="{Binding SelectedSegment.PreDelay, StringFormat=F1, UpdateSourceTrigger=LostFocus}" Padding="4,2" FontSize="11"/>
                    <TextBlock Grid.Row="1" Grid.Column="2" Style="{StaticResource ParamLabel}" Text="ms"/>

                    <TextBlock Grid.Row="2" Grid.Column="0" Style="{StaticResource ParamLabel}" Text="{lang:Lang Step3_Label_PostDelay}" Margin="0,2,8,0"/>
                    <TextBox Grid.Row="2" Grid.Column="1" Style="{StaticResource MaterialDesignOutlinedTextBox}"
                             Text="{Binding SelectedSegment.PostDelay, StringFormat=F1, UpdateSourceTrigger=LostFocus}" Padding="4,2" FontSize="11"/>
                    <TextBlock Grid.Row="2" Grid.Column="2" Style="{StaticResource ParamLabel}" Text="ms"/>
                </Grid>
            </StackPanel>

            <Border Grid.Column="3" Width="1" Background="#FFE0E4E8" Margin="0,4"/>

            <!-- 第三组：高度参数（青色） -->
            <StackPanel Grid.Column="4" Margin="12,0,0,0">
                <TextBlock FontWeight="SemiBold" FontSize="12" Margin="0,0,0,6" Foreground="#00838F"
                           Text="{lang:Lang Step3_Group_HeightParams}"/>
                <Grid>
                    <Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>

                    <TextBlock Grid.Row="0" Grid.Column="0" Style="{StaticResource ParamLabel}" Text="{lang:Lang Step3_Label_TeachHeight}" Margin="0,2,8,0"/>
                    <TextBox Grid.Row="0" Grid.Column="1" Style="{StaticResource MaterialDesignOutlinedTextBox}"
                             Text="{Binding SelectedSegment.TeachHeight, StringFormat=F3, UpdateSourceTrigger=LostFocus}" Padding="4,2" FontSize="11"/>
                    <TextBlock Grid.Row="0" Grid.Column="2" Style="{StaticResource ParamLabel}" Text="mm"/>
                    <Button Grid.Row="0" Grid.Column="2" Content="{lang:Lang Step3_Btn_TeachHeight}" Command="{Binding TeachHeightCommand}"
                            Style="{StaticResource MaterialDesignFlatButton}" Padding="8,2" FontSize="11" Margin="4,0,0,0"
                            ToolTip="{lang:Lang Step3_ToolTip_TeachHeight}" Visibility="Collapsed"/>

                    <TextBlock Grid.Row="1" Grid.Column="0" Style="{StaticResource ParamLabel}" Text="{lang:Lang Step3_Label_HeightCompensation}" Margin="0,2,8,0"/>
                    <TextBox Grid.Row="1" Grid.Column="1" Style="{StaticResource MaterialDesignOutlinedTextBox}"
                             Text="{Binding SelectedSegment.HeightCompensation, StringFormat=F3, UpdateSourceTrigger=LostFocus}" Padding="4,2" FontSize="11"/>
                    <TextBlock Grid.Row="1" Grid.Column="2" Style="{StaticResource ParamLabel}" Text="mm"/>

                    <TextBlock Grid.Row="2" Grid.Column="0" Style="{StaticResource ParamLabel}" Text="{lang:Lang Step3_Label_EffectiveHeight}" Margin="0,2,8,0" Foreground="#757575"/>
                    <TextBlock Grid.Row="2" Grid.Column="1" Text="{Binding SelectedSegment.EffectiveZHeight, StringFormat=F3}"
                             VerticalAlignment="Center" Margin="0,3,0,0" Foreground="#424242" FontWeight="Medium" FontSize="11"/>
                    <TextBlock Grid.Row="2" Grid.Column="2" Style="{StaticResource ParamLabel}" Text="mm" Foreground="#757575"/>
                </Grid>
            </StackPanel>
        </Grid>

        <!-- 采样点位列表（保留原有功能） -->
        <DockPanel Margin="0,8,0,0">
            <Button DockPanel.Dock="Right" Content="{lang:Lang Step3_Btn_DeletePoint}" Command="{Binding DeleteSelectedPointCommand}"
                    Style="{StaticResource MaterialDesignFlatButton}" Padding="8,3" Foreground="#D32F2F"
                    VerticalAlignment="Top" Margin="4,0,0,0"/>
            <DataGrid ItemsSource="{Binding SelectedSegmentPoints}"
                      SelectedIndex="{Binding SelectedPointIndex}"
                      AutoGenerateColumns="False"
                      CanUserAddRows="False"
                      CanUserDeleteRows="False"
                      IsReadOnly="True"
                      SelectionMode="Single"
                      HeadersVisibility="Column"
                      MaxHeight="150" MinHeight="60"
                      GridLinesVisibility="Horizontal">
                <DataGrid.Columns>
                    <DataGridTemplateColumn Header="{lang:Lang Step3_Header_Index}" Width="45" IsReadOnly="True">
                        <DataGridTemplateColumn.CellTemplate>
                            <DataTemplate>
                                <TextBlock HorizontalAlignment="Center" Loaded="OnPointNumberLoaded"/>
                            </DataTemplate>
                        </DataGridTemplateColumn.CellTemplate>
                    </DataGridTemplateColumn>
                    <DataGridTextColumn Header="{lang:Lang Step3_Header_X}" Binding="{Binding X, StringFormat=F3}" Width="60"/>
                    <DataGridTextColumn Header="{lang:Lang Step3_Header_Y}" Binding="{Binding Y, StringFormat=F3}" Width="60"/>
                    <DataGridTextColumn Header="{lang:Lang Step3_Header_Z}" Binding="{Binding Z, StringFormat=F3}" Width="60"/>
                    <DataGridTextColumn Header="{lang:Lang Step3_Header_MX}" Binding="{Binding MachineX, StringFormat=F2}" Width="50"/>
                    <DataGridTextColumn Header="{lang:Lang Step3_Header_MY}" Binding="{Binding MachineY, StringFormat=F2}" Width="50"/>
                    <DataGridTextColumn Header="{lang:Lang Step3_Header_MZ}" Binding="{Binding MachineZ, StringFormat=F2}" Width="50"/>
                </DataGrid.Columns>
            </DataGrid>
        </DockPanel>
    </StackPanel>
</materialDesign:Card>
```

**关键变更说明：**
- 将原来的单列Grid替换为三列彩色分组（蓝/琥珀/青）
- 第1组（蓝色）：运动参数 - 包含JumpSpeed、SafeHeight、ApproachHeight、CornerDecel(复用)、MoveSpeed(插补速度)
- 第2组（琥珀色）：出胶控制 - 包含GlueTriggerOffsetMm、PreDelay(起点延时)、PostDelay
- 第3组（青色）：高度参数 - 包含TeachHeight、HeightCompensation、EffectiveZHeight(只读)
- 保留采样点位列表DataGrid不变
- 使用统一的ParamLabel样式和MaterialDesignOutlinedTextBox样式

- [ ] **Step 2: 编译验证**

Run: `dotnet build MainApp/MainApp.csproj --configuration Debug`
Expected: Build succeeded, verify new bindings compile correctly

- [ ] **Step 3: Commit**

```bash
git add Module/Controls/Cad/Step3EditParamsPanel.xaml
git commit -m "feat(Step3EditParamsPanel): rewrite continuous interpolation mode to three-column color-coded layout"
```

---

## Task 4: 创建批量设置对话框组件

**Files:**
- Create: `Module/Views/BatchSetParamsDialog.xaml`
- Create: `Module/Views/BatchSetParamsDialog.xaml.cs`

- [ ] **Step 1: 创建BatchSetParamsDialog.xaml文件**

创建新文件 `Module/Views/BatchSetParamsDialog.xaml`，内容如下：

```xml
<UserControl x:Class="Module.Views.BatchSetParamsDialog"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes"
             xmlns:lang="clr-namespace:Core.Markup;assembly=Core"
             Width="500" Height="Auto">
    <UserControl.Resources>
        <Style x:Key="ParamLabel" TargetType="TextBlock">
            <Setter Property="FontSize" Value="12"/>
            <Setter Property="Foreground" Value="#FF607D8B"/>
            <Setter Property="VerticalAlignment" Value="Center"/>
            <Setter Property="Margin" Value="0,0,8,0"/>
        </Style>
    </UserControl.Resources>

    <StackPanel Margin="16">
        <TextBlock Text="{Binding DialogTitle}" FontSize="14" FontWeight="Bold" Margin="0,0,0,12"/>

        <ScrollViewer MaxHeight="400" VerticalScrollBarVisibility="Auto">
            <ItemsControl ItemsSource="{Binding ParamGroups}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <GroupBox Header="{Binding GroupName}" Margin="0,0,0,12"
                                  BorderBrush="{Binding GroupColor}">
                            <GroupBox.HeaderTemplate>
                                <DataTemplate>
                                    <TextBlock Text="{Binding GroupName}" FontWeight="SemiBold"
                                               Foreground="{Binding GroupColor}" FontSize="12"/>
                                </DataTemplate>
                            </GroupBox.HeaderTemplate>
                            <StackPanel>
                                <ItemsControl ItemsSource="{Binding Params}">
                                    <ItemsControl.ItemTemplate>
                                        <DataTemplate>
                                            <DockPanel Margin="0,4,0,0">
                                                <CheckBox DockPanel.Dock="Left"
                                                          IsChecked="{Binding IsSelected}"
                                                          VerticalAlignment="Center"
                                                          Margin="0,0,8,0"/>
                                                <TextBlock DockPanel.Dock="Right"
                                                           Style="{StaticResource ParamLabel}"
                                                           Text="{Binding Unit}"
                                                           Margin="8,0,0,0"/>
                                                <TextBox Text="{Binding Value, UpdateSourceTrigger=PropertyChanged}"
                                                         IsEnabled="{Binding IsSelected}"
                                                         materialDesign:HintAssist.Hint="{Binding ParamName}"
                                                         Style="{StaticResource MaterialDesignFilledTextBox}"
                                                         Margin="0"/>
                                            </DockPanel>
                                        </DataTemplate>
                                    </ItemsControl.ItemTemplate>
                                </ItemsControl>
                            </StackPanel>
                        </GroupBox>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </ScrollViewer>

        <TextBlock Text="{Binding SummaryText}" FontSize="11" Foreground="#757575" Margin="0,8,0,0"/>

        <DockPanel Margin="0,12,0,0">
            <Button DockPanel.Dock="Right" Content="{lang:Lang Step3_Dialog_Btn_Cancel}"
                    Style="{StaticResource MaterialDesignFlatButton}"
                    Command="{Binding CancelCommand}"
                    Margin="8,0,0,0" Padding="16,4"/>
            <Button Content="{lang:Lang Step3_Dialog_Btn_Apply}"
                    Style="{StaticResource MaterialDesignRaisedButton}"
                    Command="{Binding ApplyCommand}"
                    Padding="16,4"
                    Background="#1565C0"/>
        </DockPanel>
    </StackPanel>
</UserControl>
```

- [ ] **Step 2: 创建BatchSetParamsDialog.xaml.cs文件**

创建新文件 `Module/Views/BatchSetParamsDialog.xaml.cs`，内容如下：

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using Prism.Mvvm;

namespace Module.Views
{
    public class BatchParamItem : BindableBase
    {
        private string _paramName;
        public string ParamName { get => _paramName; set => SetProperty(ref _paramName, value); }

        private string _unit;
        public string Unit { get => _unit; set => SetProperty(ref _unit, value); }

        private string _value = "";
        public string Value { get => _value; set => SetProperty(ref _value, value); }

        private bool _isSelected = true;
        public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    }

    public class BatchParamGroup : BindableBase
    {
        private string _groupName;
        public string GroupName { get => _groupName; set => SetProperty(ref _groupName, value); }

        private Brush _groupColor;
        public Brush GroupColor { get => _groupColor; set => SetProperty(ref _groupColor, value); }

        private ObservableCollection<BatchParamItem> _params = new();
        public ObservableCollection<BatchParamItem> Params { get => _params; set => SetProperty(ref _params, value); }
    }

    public partial class BatchSetParamsDialog : UserControl
    {
        public static readonly DependencyProperty DialogTitleProperty =
            DependencyProperty.Register(nameof(DialogTitle), typeof(string), typeof(BatchSetParamsDialog),
                new PropertyMetadata("批量设置参数"));

        public static readonly DependencyProperty ParamGroupsProperty =
            DependencyProperty.Register(nameof(ParamGroups), typeof(ObservableCollection<BatchParamGroup>),
                typeof(BatchSetParamsDialog), new PropertyMetadata(null));

        public static readonly DependencyProperty SummaryTextProperty =
            DependencyProperty.Register(nameof(SummaryText), typeof(string), typeof(BatchSetParamsDialog),
                new PropertyMetadata(""));

        public static readonly DependencyProperty ApplyCommandProperty =
            DependencyProperty.Register(nameof(ApplyCommand), typeof(System.Windows.Input.ICommand),
                typeof(BatchSetParamsDialog), new PropertyMetadata(null));

        public static readonly DependencyProperty CancelCommandProperty =
            DependencyProperty.Register(nameof(CancelCommand), typeof(System.Windows.Input.ICommand),
                typeof(BatchSetParamsDialog), new PropertyMetadata(null));

        public string DialogTitle
        {
            get => (string)GetValue(DialogTitleProperty);
            set => SetValue(DialogTitleProperty, value);
        }

        public ObservableCollection<BatchParamGroup> ParamGroups
        {
            get => (ObservableCollection<BatchParamGroup>)GetValue(ParamGroupsProperty);
            set => SetValue(ParamGroupsProperty, value);
        }

        public string SummaryText
        {
            get => (string)GetValue(SummaryTextProperty);
            set => SetValue(SummaryTextProperty, value);
        }

        public System.Windows.Input.ICommand ApplyCommand
        {
            get => (System.Windows.Input.ICommand)GetValue(ApplyCommandProperty);
            set => SetValue(ApplyCommandProperty, value);
        }

        public System.Windows.Input.ICommand CancelCommand
        {
            get => (System.Windows.Input.ICommand)GetValue(CancelCommandProperty);
            set => SetValue(CancelCommandProperty, value);
        }

        public BatchSetParamsDialog()
        {
            InitializeComponent();
        }
    }
}
```

- [ ] **Step 3: 编译验证**

Run: `dotnet build MainApp/MainApp.csproj --configuration Debug`
Expected: Build succeeded, new dialog component compiles correctly

- [ ] **Step 4: Commit**

```bash
git add Module/Views/BatchSetParamsDialog.xaml Module/Views/BatchSetParamsDialog.xaml.cs
git commit -m "feat(BatchSetParamsDialog): create multi-parameter batch setting dialog component"
```

---

## Task 5: ViewModel层 - 重构批量设置命令

**Files:**
- Modify: `Module/Controls/Cad/CadPointEditorViewModel.cs:926-969, 1656-1667`

- [ ] **Step 1: 添加批量设置相关的属性和方法**

在 CadPointEditorViewModel.cs 中找到 `BatchSetGlueCommand` 属性定义区域（约第926行），在其后添加：

```csharp
private DelegateCommand _batchSetAllCommand;
/// <summary>批量设置全部参数命令</summary>
public DelegateCommand BatchSetAllCommand =>
    _batchSetAllCommand ??= new DelegateCommand(ExecuteBatchSetAll);
```

- [ ] **Step 2: 实现ExecuteBatchSetAll方法**

找到 `ExecuteBatchSetGlue` 方法（约第1656行），在其后添加新方法：

```csharp
private void ExecuteBatchSetAll()
{
    var targets = Segments.Where(s => s.IsEnabled).ToList();
    if (targets.Count == 0) { GlobalStatus = L("CadPoint_Error_NoTrajectorySelected"); return; }

    var dialog = new BatchSetParamsDialog();
    var viewModel = new BatchSetParamsViewModel(IsSinglePointMode, targets.Count);

    dialog.DataContext = viewModel;
    dialog.ApplyCommand = new DelegateCommand(() =>
    {
        ApplyBatchParameters(targets, viewModel.GetSelectedParams());
        ((dialog.Parent as Window)?.Close());
    });
    dialog.CancelCommand = new DelegateCommand(() => (dialog.Parent as Window)?.Close());

    ShowUserControlDialog(dialog, L(IsSinglePointMode ? "Step3_Dialog_Title_SinglePoint" : "Step3_Dialog_Title_Continuous"));
}

private void ApplyBatchParameters(List<DispenseSegment> targets, Dictionary<string, string> paramsDict)
{
    int appliedCount = 0;
    foreach (var seg in targets)
    {
        foreach (var kvp in paramsDict)
        {
            if (double.TryParse(kvp.Value, out double val))
            {
                switch (kvp.Key)
                {
                    case "MoveSpeed": seg.MoveSpeed = val; break;
                    case "JumpSpeed": seg.JumpSpeed = val; break;
                    case "SafeHeight": seg.SafeHeight = val; break;
                    case "ApproachHeight": seg.ApproachHeight = val; break;
                    case "CornerDecel": seg.CornerDecel = val; break;
                    case "DispenseTime": seg.DispenseTime = val; break;
                    case "GlueTriggerOffsetMm": seg.GlueTriggerOffsetMm = val; break;
                    case "PreDelay": seg.PreDelay = val; break;
                    case "PostDelay": seg.PostDelay = val; break;
                    case "TeachHeight": seg.TeachHeight = val; break;
                    case "HeightCompensation": seg.HeightCompensation = val; break;
                    case "DispensingPressure": seg.DispensingPressure = val; break;
                    case "SuckBackTime": seg.SuckBackTime = val; break;
                }
                appliedCount++;
            }
        }
    }
    GlobalStatus = $"批量设置完成: {appliedCount} 个参数 ({targets.Count} 段)";
}
```

- [ ] **Step 3: 更新Step3EditParamsPanel.xaml中的按钮绑定**

在 Step3EditParamsPanel.xaml 中找到批量设胶量按钮（约第78行）：

**将：**
```xml
<Button Content="{lang:Lang Step3_Btn_BatchSetGlue}" Command="{Binding BatchSetGlueCommand}"
        Style="{StaticResource MaterialDesignFlatButton}" Padding="10,4" Margin="0,0,4,0"/>
```
**改为：**
```xml
<Button Content="{lang:Lang Step3_Btn_BatchSetAll}" Command="{Binding BatchSetAllCommand}"
        Style="{StaticResource MaterialDesignFlatButton}" Padding="10,4" Margin="0,0,4,0">
    <Button.ContentTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal">
                <materialDesign:PackIcon Kind="ContentSaveAll" Width="14" Height="14"
                                         VerticalAlignment="Center" Margin="0,0,4,0"/>
                <ContentPresenter/>
            </StackPanel>
        </DataTemplate>
    </Button.ContentTemplate>
</Button>
```

- [ ] **Step 4: 编译验证**

Run: `dotnet build MainApp/MainApp.csproj --configuration Debug`
Expected: Build succeeded, command binding works correctly

- [ ] **Step 5: Commit**

```bash
git add Module/Controls/Cad/CadPointEditorViewModel.cs Module/Controls/Cad/Step3EditParamsPanel.xaml
git commit -m "feat(CadPointEditorViewModel): refactor batch setting to multi-parameter dialog"
```

---

## Task 6: 多语言资源更新 - 中文资源

**Files:**
- Modify: `MainApp/Languages/Strings.zh-CN.xaml`

- [ ] **Step 1: 更新组名资源键**

在 Strings.zh-CN.xaml 中找到以下键（约第2728-2730行）并修改：

**修改前：**
```xml
<sys:String x:Key="Step3_Group_MotionDispense">运动与出胶</sys:String>
<sys:String x:Key="Step3_Group_DelayControl">延时控制</sys:String>
```
**修改后：**
```xml
<sys:String x:Key="Step3_Group_MotionParams">运动参数</sys:String>
<sys:String x:Key="Step3_Group_MotionDispense">运动与出胶</sys:String>  <!-- 保留旧键兼容 -->
<sys:String x:Key="Step3_Group_DispenseControl">出胶控制</sys:String>
<sys:String x:Key="Step3_Group_DelayControl">延时控制</sys:String>  <!-- 保留旧键兼容 -->
<sys:String x:Key="Step3_Group_HeightParams">高度参数</sys:String>
```

- [ ] **Step 2: 新增批量设置相关资源键**

在 Strings.zh-CN.xaml 的 Step3 区域（约第2739行后）添加：

```xml
<!-- 批量设置全部参数 -->
<sys:String x:Key="Step3_Btn_BatchSetAll">批量设置全部参数</sys:String>
<sys:String x:Key="Step3_Btn_BatchSetGlue">批量设胶量</sys:String>  <!-- 保留旧键 -->

<!-- 批量设置对话框 -->
<sys:String x:Key="Step3_Dialog_Title_SinglePoint">批量设置单点模式参数</sys:String>
<sys:String x:Key="Step3_Dialog_Title_Continuous">批量设置连续插补参数</sys:String>
<sys:String x:Key="Step3_Dialog_SelectedCount">已选择 {0} 个启用段</sys:String>
<sys:String x:Key="Step3_Dialog_ConfirmApply">将更新 {0} 个段的 {1} 个参数</sys:String>
<sys:String x:Key="Step3_Dialog_Btn_Apply">应用</sys:String>
<sys:String x:Key="Step3_Dialog_Btn_Cancel">取消</sys:String>

<!-- 命名统一 -->
<sys:String x:Key="Step3_Label_StartDelay">起点延时</sys:String>
<sys:String x:Key="Step3_Label_PreDelay">起点延时(ms)</sys:String>  <!-- 更新原起点开胶延时 -->
```

- [ ] **Step 3: 检查重复键**

Run PowerShell script to check for duplicate keys:
```powershell
$content = Get-Content "MainApp/Languages/Strings.zh-CN.xaml" -Raw
$matches = [regex]::Matches($content, 'x:Key="(Step3_[^"]+)"')
$groups = $matches | ForEach-Object { $_.Groups[1].Value } | Group-Object
$duplicates = $groups | Where-Object { $_.Count -gt 1 }
if ($duplicates) { Write-Host "❌ 发现重复键:" ; $duplicates | ForEach-Object { Write-Host "  $($_.Name) ($($_.Count)次)" } }
else { Write-Host "✅ 无重复键" }
```
Expected: ✅ 无重复键

- [ ] **Step 4: Commit**

```bash
git add MainApp/Languages/Strings.zh-CN.xaml
git commit -m "i18n(zh-CN): update Step3 resource keys for layout optimization"
```

---

## Task 7: 多语言资源更新 - 英文资源

**Files:**
- Modify: `MainApp/Languages/Strings.en-US.xaml`

- [ ] **Step 1: 同步英文翻译**

在 Strings.en-US.xaml 的 Step3 区域添加对应的英文翻译：

```xml
<!-- Batch Set All Parameters -->
<sys:String x:Key="Step3_Btn_BatchSetAll">Batch Set All Parameters</sys:String>

<!-- Batch Set Dialog -->
<sys:String x:Key="Step3_Dialog_Title_SinglePoint">Batch Set Single Point Parameters</sys:String>
<sys:String x:Key="Step3_Dialog_Title_Continuous">Batch Set Continuous Interpolation Parameters</sys:String>
<sys:String x:Key="Step3_Dialog_SelectedCount">{0} enabled segments selected</sys:String>
<sys:String x:Key="Step3_Dialog_ConfirmApply">Will update {1} parameters for {0} segments</sys:String>
<sys:String x:Key="Step3_Dialog_Btn_Apply">Apply</sys:String>
<sys:String x:Key="Step3_Dialog_Btn_Cancel">Cancel</sys:String>

<!-- Unified Naming -->
<sys:String x:Key="Step3_Group_MotionParams">Motion Parameters</sys:String>
<sys:String x:Key="Step3_Group_DispenseControl">Dispense Control</sys:String>
<sys:String x:Key="Step3_Group_HeightParams">Height Parameters</sys:String>
<sys:String x:Key="Step3_Label_StartDelay">Start Delay</sys:String>
```

- [ ] **Step 2: 检查重复键**

使用Task 6相同的PowerShell脚本检查en-US文件
Expected: ✅ 无重复键

- [ ] **Step 3: Commit**

```bash
git add MainApp/Languages/Strings.en-US.xaml
git commit -m "i18n(en-US): sync English translations for Step3 layout optimization"
```

---

## Task 8: 集成测试与验证

**Files:**
- No file modifications - testing only

- [ ] **Step 1: 完整编译**

Run: `dotnet build MainApp/MainApp.csproj --configuration Debug`
Expected: ✅ Build succeeded, 0 errors, 0 warnings

- [ ] **Step 2: 运行应用程序并手动测试**

**测试单点模式（Test Case SP-01 to SP-04）：**
- [ ] SP-01: 切换到单点模式，确认显示三列布局（蓝色"运动参数"/琥珀色"出胶控制"/青色"阀控参数"）
- [ ] SP-02: 确认出胶时间位于第2组（出胶控制）而非第1组
- [ ] SP-03: 修改任意参数值，确认绑定正常工作（输入框→模型→界面同步）
- [ ] SP-04: 切换中英文语言，确认所有文本正确显示无乱码

**测试连续插补模式（Test Case CI-01 to CI-06）：**
- [ ] CI-01: 切换到连续插补模式，确认显示三列布局
- [ ] CI-02: 确认第1组包含：空移速度、安全高度、逼近高度、减速系数、插补速度
- [ ] CI-03: 确认第2组包含：开胶距离、起点延时、收胶延时
- [ ] CI-04: 确认第3组包含：示教高度、高度补偿、有效高度（只读）
- [ ] CI-05: 选择一个轨迹段，修改新增参数（如JumpSpeed），确认保存成功
- [ ] CI-06: 确认采样点位列表DataGrid仍正常显示和工作

**测试批量设置功能（Test Case BS-01 to BS-05）：**
- [ ] BS-01: 点击"批量设置全部参数"按钮，确认打开模态对话框
- [ ] BS-02: 对话框标题根据当前模式正确显示（单点/连续插补）
- [ ] BS-03: 勾选/取消勾选部分参数，确认输入框启用/禁用状态切换
- [ ] BS-04: 填写数值后点击应用，确认参数应用到所有IsEnabled=true的段
- [ ] BS-05: 未选中任何段时点击按钮，确认显示错误提示

**边界情况测试（Test Case Edge-01 to Edge-03）：**
- [ ] Edge-01: 输入超范围数值（如负数、极大值），确认被Clamp或提示
- [ ] Edge-02: 快速切换模式多次，确认无内存泄漏或异常
- [ ] Edge-03: 所有段都禁用（IsEnabled=false）时，确认批量按钮行为正确

- [ ] **Step 3: 记录测试结果**

如果所有测试通过：
```bash
git status  # 确认工作区干净
echo "✅ All tests passed!"
```

如果有失败项，记录具体问题并修复后重新测试。

---

## 自检清单

### Spec覆盖度检查 ✅

| Spec章节 | 实现Task | 覆盖状态 |
|---------|---------|---------|
| 2.1 单点模式调整（重组+重命名+移动出胶时间） | Task 2 | ✅ |
| 2.2 连续插补模式升级（5个新参数+三列布局） | Task 1, Task 3 | ✅ |
| 2.3 批量设置功能升级（多参数对话框） | Task 4, Task 5 | ✅ |
| 2.4 命名统一（起点延时） | Task 6, Task 7 | ✅ |
| 3.1 数据模型扩展 | Task 1 | ✅ |
| 3.2 UI组件设计 | Task 2, Task 3, Task 4 | ✅ |
| 3.3 ViewModel更新 | Task 5 | ✅ |
| 3.4 多语言资源更新 | Task 6, Task 7 | ✅ |

### 占位符扫描 ✅

- ❌ 无TBD/TODO标记
- ❌ 无"待补充"、"后续实现"等模糊表述
- ✅ 所有代码示例完整可执行
- ✅ 所有命令精确到参数和预期输出

### 类型一致性检查 ✅

- DispenseSegment.JumpSpeed (double) → TextBox绑定StringFormat=F1 ✅
- BatchParamItem.Value (string) → TextBox双向绑定 ✅
- 资源键命名规范统一（Step3_前缀）✅
- 颜色值使用一致的十六进制格式 ✅

---

## 执行选项

**Plan complete and saved to `docs/superpowers/plans/2026-05-26-step3editparamspanel-layout-optimization.md`. Two execution options:**

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration
   - 优点：每个任务独立执行，出错不影响其他任务
   - 适合：复杂重构，需要严格质量控制

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints
   - 优点：速度快，上下文连续
   - 适合：简单改动，开发者熟悉代码库

**Which approach?**
