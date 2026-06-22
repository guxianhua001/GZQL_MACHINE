# MultiStationPositionEditorView 操作按钮布局重新设计

## 摘要

将 `MultiStationPositionEditorView` 中分散在三处的操作按钮（工站选择栏、操作工具栏、底部按钮区）整合为**统一工具栏**，按功能分组（速度控制 / 位置管理 / 运动控制 / 行排序），采用 **MaterialDesignRaisedButton（主要操作）+ MaterialDesignOutlinedButton（次要操作）** 主次分明的样式风格，为所有按钮补全 `PackIcon` 图标，移除冗余资源。

---

## 当前状态分析

### 文件位置
- View: [MultiStationPositionEditorView.xaml](file:///c:\WorkFiles\GZQL_MACHINE\RecipeManagement\Views\MultiStationPositionEditorView.xaml)
- ViewModel: [MultiStationPositionEditorViewModel.cs](file:///c:\WorkFiles\GZQL_MACHINE\RecipeManagement\ViewModels\MultiStationPositionEditorViewModel.cs)

### 当前布局问题
1. **按钮分散三处**：工站选择栏（Row 1，添加/删除）、操作工具栏（Row 2，示教/撤销/前往/停止）、底部按钮区（Row 4，上移/下移/关闭），同类操作分两处，职责混乱。
2. **样式不统一**：所有按钮使用默认 Button 样式，未显式使用 MaterialDesign 样式，与项目其他 View（ParameterEditorView、RecipeManagerView）约定不一致。
3. **图标缺失**：仅上移/下移按钮有 `PackIcon`，其余 6 个操作按钮（添加/删除/示教/撤销/前往/停止）均无图标，不符合项目"按钮使用 PackIcon"约定。
4. **宽度不齐**：停止按钮 Width=80，上移/下移 Width=100，其余无固定宽度，视觉不齐。
5. **冗余资源**：`BooleanToVisibilityConverter`（行 23）在 XAML 中未被使用。

### 当前按钮清单
| 位置 | 按钮 | 命令 | 图标 | 样式 |
|------|------|------|------|------|
| Row 1 | 添加位置 | AddPositionCommand | 无 | 默认 |
| Row 1 | 删除位置 | DeletePositionCommand | 无 | 默认 |
| Row 2 | 示教 | TeachCommand | 无 | 默认 |
| Row 2 | 撤销 | UndoCommand | 无 | 默认 |
| Row 2 | 前往 | ReplayCommand | 无 | 默认 |
| Row 2 | 停止 | StopCommand | 无 | 默认 (Width=80) |
| Row 4 | 上移 | MoveUpCommand | ArrowUp | 默认 (Width=100) |
| Row 4 | 下移 | MoveDownCommand | ArrowDown | 默认 (Width=100) |
| Row 4 | 关闭 | CloseCommand | 无 | 默认 (Width=100) |

---

## 提议变更

### 变更 1：XAML 布局重构

**文件**：[MultiStationPositionEditorView.xaml](file:///c:\WorkFiles\GZQL_MACHINE\RecipeManagement\Views\MultiStationPositionEditorView.xaml)

**目标**：将 8 个操作按钮 + 速度选择整合到 Row 2 统一工具栏，按功能分组；Row 1 仅保留工站选择 ComboBox；Row 4 仅保留关闭按钮。

**新布局结构**：
```
Grid (5 行)
├── Row 0 (Auto): 标题 "位置编辑器"
├── Row 1 (Auto): 工站选择栏（Label + ComboBox，无按钮）
├── Row 2 (Auto): 统一操作工具栏
│   └── StackPanel Horizontal
│       ├── 速度组: Label "速度:" + ComboBox + Label "mm/s"
│       ├── Rectangle VSeparator
│       ├── 位置管理组: 添加位置(Raised) + 删除位置(Outlined,红)
│       ├── Rectangle VSeparator
│       ├── 运动控制组: 示教(Raised) + 撤销(Outlined) + 前往(Raised) + 停止(Raised,红)
│       ├── Rectangle VSeparator
│       └── 行排序组: 上移(Outlined) + 下移(Outlined)
├── Row 3 (*): DataGrid（不变）
└── Row 4 (Auto): 关闭按钮（右对齐，Outlined）
```

**关键改动点**：
- **Row 1**（行 48-67）：移除添加/删除按钮，仅保留 `TextBlock` + `ComboBox`（工站选择器）
- **Row 2**（行 69-95）：重构为统一工具栏，包含速度选择 + 全部 8 个操作按钮，用 `VSeparator` 分隔 4 个功能组
- **Row 4**（行 111-156）：移除上移/下移按钮（已移至 Row 2），仅保留右对齐的关闭按钮；简化为 `StackPanel HorizontalAlignment="Right"`

### 变更 2：按钮样式与图标

**样式分类**（参考 [ParameterEditorView.xaml](file:///c:\WorkFiles\GZQL_MACHINE\Framework\Views\ParameterEditorView.xaml) 行 330-349 的 RaisedButton/OutlinedButton 模式）：

| 按钮 | 样式 | 图标 (PackIcon Kind) | 颜色说明 | 理由 |
|------|------|---------------------|----------|------|
| 添加位置 | MaterialDesignRaisedButton | Plus | 默认主题色 | 主要管理操作 |
| 删除位置 | MaterialDesignOutlinedButton | Delete | 红色前景 (#D32F2F) | 次要、破坏性操作 |
| 示教 | MaterialDesignRaisedButton | Target | 默认主题色 | 主要运动操作 |
| 撤销 | MaterialDesignOutlinedButton | Undo | 默认 | 次要辅助操作 |
| 前往 | MaterialDesignRaisedButton | MapMarkerRight | 默认主题色 | 主要运动操作 |
| 停止 | MaterialDesignRaisedButton | Stop | 红色背景 (#D32F2F) | 安全关键，需醒目 |
| 上移 | MaterialDesignOutlinedButton | ArrowUp | 默认 | 次要排序操作 |
| 下移 | MaterialDesignOutlinedButton | ArrowDown | 默认 | 次要排序操作 |
| 关闭 | MaterialDesignOutlinedButton | Close | 默认 | 次要导航操作 |

**按钮尺寸决策**：
- 工具栏按钮（Row 2）：`Height="40"` + `Padding="16,8"`，**不设固定 Width**（Width=Auto），让内容自适应
  - 理由：用户选择的 Width=120 适用于底部 2-3 个按钮的场景；工具栏需容纳 8 个按钮 + 速度选择 + 3 个分隔线，固定 120 会导致总宽 ~1110px 溢出。自适应宽度更紧凑实用。
- 关闭按钮（Row 4）：`Width="120" Height="40"`（遵循 ParameterEditorView 约定，底部按钮固定宽度）

**按钮内容结构**（统一模式）：
```xml
<Button Style="{StaticResource MaterialDesignRaisedButton}"
        Height="40"
        Padding="16,8"
        Command="{Binding XxxCommand}"
        Margin="4,0">
    <StackPanel Orientation="Horizontal">
        <materialDesign:PackIcon Kind="Xxx"
                                 Width="18"
                                 Height="18"
                                 VerticalAlignment="Center"
                                 Margin="0,0,6,0" />
        <TextBlock Text="{lang:Lang Xxx}"
                   VerticalAlignment="Center" />
    </StackPanel>
</Button>
```

**停止按钮特殊处理**（安全关键，红色背景）：
```xml
<Button Style="{StaticResource MaterialDesignRaisedButton}"
        Height="40"
        Padding="16,8"
        Command="{Binding StopCommand}"
        Background="#D32F2F"
        BorderBrush="#D32F2F"
        Margin="4,0">
    <StackPanel Orientation="Horizontal">
        <materialDesign:PackIcon Kind="Stop" ... />
        <TextBlock Text="{lang:Lang Stop}" ... />
    </StackPanel>
</Button>
```

**删除按钮特殊处理**（破坏性操作，红色前景）：
```xml
<Button Style="{StaticResource MaterialDesignOutlinedButton}"
        Height="40"
        Padding="16,8"
        Command="{Binding DeletePositionCommand}"
        BorderBrush="#D32F2F"
        Foreground="#D32F2F"
        Margin="4,0">
    ...
</Button>
```

### 变更 3：新增 VSeparator 资源

**文件**：[MultiStationPositionEditorView.xaml](file:///c:\WorkFiles\GZQL_MACHINE\RecipeManagement\Views\MultiStationPositionEditorView.xaml) 的 `UserControl.Resources`

在资源字典中新增 `VSeparator` 样式（复用 [RecipeManagerView.xaml](file:///c:\WorkFiles\GZQL_MACHINE\RecipeManagement\Views\RecipeManagerView.xaml) 行 30-42 的定义）：
```xml
<!-- 工具栏分组分隔线 -->
<Style x:Key="VSeparator"
       TargetType="Rectangle">
    <Setter Property="Width" Value="1" />
    <Setter Property="Height" Value="24" />
    <Setter Property="Fill" Value="{DynamicResource MaterialDesignDivider}" />
    <Setter Property="Margin" Value="8,0" />
    <Setter Property="VerticalAlignment" Value="Center" />
</Style>
```

### 变更 4：移除冗余资源

**文件**：[MultiStationPositionEditorView.xaml](file:///c:\WorkFiles\GZQL_MACHINE\RecipeManagement\Views\MultiStationPositionEditorView.xaml) 行 23

移除未被使用的 `<BooleanToVisibilityConverter x:Key="BoolToVis" />`。

### 变更 5：多语言键检查（无需新增）

已验证所有按钮文案的多语言键均存在于：
- [Strings.zh-CN.xaml](file:///c:\WorkFiles\GZQL_MACHINE\MainApp\Languages\Strings.zh-CN.xaml)
- [Strings.en-US.xaml](file:///c:\WorkFiles\GZQL_MACHINE\MainApp\Languages\Strings.en-US.xaml)

| 键 | zh-CN | en-US |
|----|-------|-------|
| PositionEditor_AddPosition | 添加位置 | Add Position |
| PositionEditor_DeletePosition | 删除位置 | Delete Position |
| AssemblyStep_Button_Teach | 示教 | Teach |
| PositionEditor_Undo | 撤销 | Undo |
| PositionEditor_Goto | 前往 | Go To |
| Stop | 停止 | Stop |
| PositionEditor_Up | 上移 | Up |
| PositionEditor_Down | 下移 | Down |
| Close | 关闭 | Close |

**无需新增多语言键**，本次改动为纯布局/样式重构。

---

## 假设与决策

1. **工站选择 ComboBox 保留在 Row 1**：工站选择器是数据选择控件，非操作按钮，不纳入统一工具栏。
2. **关闭按钮保留在 Row 4**：关闭是页面导航操作，非位置编辑操作，置于底部右对齐符合惯例。
3. **工具栏按钮不设固定 Width**：8 个按钮 + 速度选择 + 分隔线，固定 120px 会溢出；自适应宽度 + Height=40 保持视觉统一。
4. **停止按钮用 RaisedButton + 红色背景**：安全关键操作需醒目，红色实心按钮最符合工业设备安全规范。
5. **删除按钮用 OutlinedButton + 红色前景**：破坏性操作需提示风险，但非安全关键，红色描边即可。
6. **SaveCommand 不绑定 UI**：遵循现有架构，保存由配方管理器统一 Save Pool 完成（版本记录 v2026.06.18 已确认）。
7. **ViewModel 无需修改**：所有命令已存在，本次仅重构 XAML 布局与样式。

---

## 验证步骤

1. **编译验证**：构建 Recipe 项目，确认 XAML 无编译错误
   ```powershell
   dotnet build c:\WorkFiles\GZQL_MACHINE\RecipeManagement\Recipe.csproj
   ```
2. **视觉验证**：运行 MainApp，打开配方管理 → 位置参数标签页，检查：
   - 统一工具栏显示 4 个功能组，分隔线清晰
   - 所有按钮有图标 + 文字
   - 主要按钮（添加/示教/前往/停止）为实心 Raised 样式
   - 次要按钮（删除/撤销/上移/下移/关闭）为描边 Outlined 样式
   - 停止按钮红色背景醒目
   - 删除按钮红色描边/前景
   - 工站选择栏仅剩 Label + ComboBox
   - 底部仅剩右对齐关闭按钮
3. **功能验证**：
   - 添加/删除位置正常
   - 示教/前往/停止运动控制正常（含确认对话框）
   - 撤销正常
   - 上移/下移行排序正常
   - 关闭按钮正常
4. **多语言验证**：切换中英文，所有按钮文案正确显示
5. **更新版本修改记录**：在 [版本修改记录.txt](file:///c:\WorkFiles\GZQL_MACHINE\MainApp\bin\Debug\net9.0-windows7.0\版本修改记录.txt) 顶部新增条目
