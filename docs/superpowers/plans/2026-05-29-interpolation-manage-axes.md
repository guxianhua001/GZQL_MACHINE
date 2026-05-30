# 插补系管理轴功能 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 完善插补系管理轴功能，支持添加/移除轴到坐标系，并与 hwcfg.xml 配置文件双向同步。

**Architecture:** 在 AxisParameterService 中新增 hwcfg.xml 插补系读写方法；ViewModel 中实现添加/移除轴逻辑并同步到 hwcfg.xml；View 中在插补系轴列表区域添加添加/移除按钮和可用轴选择器。

**Tech Stack:** WPF + Prism + MaterialDesignInXaml + LINQ to XML

---

## 文件结构

| 操作 | 文件路径 | 职责 |
|------|---------|------|
| 修改 | `MotionControl/Services/IAxisParameterService.cs` | 添加 hwcfg.xml 插补系同步接口方法 |
| 修改 | `MotionControl/Services/AxisParameterService.cs` | 实现 hwcfg.xml 插补系读写同步 |
| 修改 | `MotionControl/ViewModels/AxisSettingViewModel.cs` | 添加管理轴命令和逻辑 |
| 修改 | `MotionControl/Views/AxisSettingView.xaml` | 插补系轴列表区域添加添加/移除按钮 |
| 修改 | `MainApp/Languages/Strings.zh-CN.xaml` | 中文语言资源 |
| 修改 | `MainApp/Languages/Strings.en-US.xaml` | 英文语言资源 |

---

### Task 1: IAxisParameterService 添加 hwcfg.xml 同步接口

**Files:**
- Modify: `MotionControl/Services/IAxisParameterService.cs`

- [ ] **Step 1: 在接口中添加 hwcfg.xml 同步方法**

在 `LoadAllInterpolationSystems` 方法声明之后添加：

```csharp
        /// <summary>
        /// 将插补系轴配置同步到hwcfg.xml（更新axes属性）
        /// </summary>
        void SyncInterpolationAxesToHwConfig(IEnumerable<InterpolationSystem> systems);

        /// <summary>
        /// 从hwcfg.xml读取插补系轴配置（axes属性格式："卡号-轴号,卡号-轴号"）
        /// </summary>
        void LoadInterpolationAxesFromHwConfig(IEnumerable<InterpolationSystem> systems);
```

---

### Task 2: AxisParameterService 实现 hwcfg.xml 同步

**Files:**
- Modify: `MotionControl/Services/AxisParameterService.cs`

- [ ] **Step 1: 在 AxisParameterService 类末尾（`GetInterpolationSpeeds` 方法之后）添加两个新方法**

```csharp
        /// <summary>
        /// 将插补系轴配置同步到hwcfg.xml（更新axes属性）
        /// axes属性格式："卡号-轴号,卡号-轴号"
        /// </summary>
        public void SyncInterpolationAxesToHwConfig(IEnumerable<InterpolationSystem> systems)
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, HW_CONFIG_PATH);
                if (!File.Exists(configPath))
                {
                    _logger.Warn($"hwcfg.xml not found at {configPath}");
                    return;
                }

                XDocument doc = XDocument.Load(configPath);
                var systemElements = doc.Descendants("InterpolationSystems")
                    .Elements("System");

                foreach (var system in systems)
                {
                    var sysElem = systemElements.FirstOrDefault(e =>
                        (int?)e.Attribute("coordId") == system.CoordId &&
                        (int?)e.Attribute("actCardId") == system.ActCardId);

                    if (sysElem != null)
                    {
                        string axesValue = system.Axes != null && system.Axes.Any()
                            ? string.Join(",", system.Axes)
                            : "";

                        sysElem.Attribute("axes")?.Remove();
                        sysElem.Add(new XAttribute("axes", axesValue));

                        var oldAxisElems = sysElem.Elements("Axis").ToList();
                        foreach (var old in oldAxisElems) old.Remove();

                        foreach (var axisId in system.Axes ?? Enumerable.Empty<string>())
                        {
                            sysElem.Add(new XElement("Axis", new XAttribute("configId", axisId)));
                        }
                    }
                }

                doc.Save(configPath);
                _logger.Info("Synced interpolation axes to hwcfg.xml");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to sync interpolation axes to hwcfg.xml");
                throw;
            }
        }

        /// <summary>
        /// 从hwcfg.xml读取插补系轴配置（axes属性格式："卡号-轴号,卡号-轴号"）
        /// </summary>
        public void LoadInterpolationAxesFromHwConfig(IEnumerable<InterpolationSystem> systems)
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, HW_CONFIG_PATH);
                if (!File.Exists(configPath))
                {
                    _logger.Warn($"hwcfg.xml not found at {configPath}");
                    return;
                }

                XDocument doc = XDocument.Load(configPath);
                var systemElements = doc.Descendants("InterpolationSystems")
                    .Elements("System");

                foreach (var system in systems)
                {
                    var sysElem = systemElements.FirstOrDefault(e =>
                        (int?)e.Attribute("coordId") == system.CoordId &&
                        (int?)e.Attribute("actCardId") == system.ActCardId);

                    if (sysElem != null)
                    {
                        var axesList = new List<string>();

                        string axesAttr = sysElem.Attribute("axes")?.Value;
                        if (!string.IsNullOrWhiteSpace(axesAttr))
                        {
                            axesList = axesAttr.Split(',')
                                .Select(a => a.Trim())
                                .Where(a => !string.IsNullOrEmpty(a))
                                .ToList();
                        }

                        if (!axesList.Any())
                        {
                            foreach (var axElem in sysElem.Elements("Axis"))
                            {
                                string configId = axElem.Attribute("configId")?.Value;
                                if (!string.IsNullOrEmpty(configId))
                                    axesList.Add(configId);
                            }
                        }

                        system.Axes = axesList;
                    }
                }

                _logger.Info("Loaded interpolation axes from hwcfg.xml");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load interpolation axes from hwcfg.xml");
            }
        }
```

- [ ] **Step 2: 更新 LoadInterpolationSystems 方法，优先从 axes 属性读取轴列表**

将 `LoadInterpolationSystems` 方法中解析轴的部分替换。找到以下代码：

```csharp
                    var axisElems = sysElem.Elements("Axis");
                    foreach (var axElem in axisElems)
                    {
                        string configId = axElem.Attribute("configId")?.Value;
                        if (!string.IsNullOrEmpty(configId))
                            system.Axes.Add(configId);
                    }
```

替换为：

```csharp
                    string axesAttr = sysElem.Attribute("axes")?.Value;
                    if (!string.IsNullOrWhiteSpace(axesAttr))
                    {
                        system.Axes = axesAttr.Split(',')
                            .Select(a => a.Trim())
                            .Where(a => !string.IsNullOrEmpty(a))
                            .ToList();
                    }
                    else
                    {
                        var axisElems = sysElem.Elements("Axis");
                        foreach (var axElem in axisElems)
                        {
                            string configId = axElem.Attribute("configId")?.Value;
                            if (!string.IsNullOrEmpty(configId))
                                system.Axes.Add(configId);
                        }
                    }
```

---

### Task 3: ViewModel 添加管理轴命令和逻辑

**Files:**
- Modify: `MotionControl/ViewModels/AxisSettingViewModel.cs`

- [ ] **Step 1: 添加新命令声明**

在 `LoadSystemParamsCommand` 声明之后添加：

```csharp
        public DelegateCommand AddAxisToSystemCommand { get; }
        public DelegateCommand RemoveAxisFromSystemCommand { get; }
```

- [ ] **Step 2: 添加可用轴和选中可用轴属性**

在 `SelectedAxesInSystem` 属性之后添加：

```csharp
        private ObservableCollection<AxisInfo> _availableAxesForSystem = new ObservableCollection<AxisInfo>();
        public ObservableCollection<AxisInfo> AvailableAxesForSystem
        {
            get => _availableAxesForSystem;
            set => SetProperty(ref _availableAxesForSystem, value);
        }

        private AxisInfo _selectedAvailableAxis;
        public AxisInfo SelectedAvailableAxis
        {
            get => _selectedAvailableAxis;
            set => SetProperty(ref _selectedAvailableAxis, value);
        }

        private AxisInSystem _selectedAxisInSystem;
        public AxisInSystem SelectedAxisInSystem
        {
            get => _selectedAxisInSystem;
            set => SetProperty(ref _selectedAxisInSystem, value);
        }
```

- [ ] **Step 3: 在构造函数中初始化新命令**

在 `LoadSystemParamsCommand = new DelegateCommand(LoadSystemConfigurations);` 之后添加：

```csharp
            AddAxisToSystemCommand = new DelegateCommand(OnAddAxisToSystem, CanAddAxisToSystem);
            RemoveAxisFromSystemCommand = new DelegateCommand(OnRemoveAxisFromSystem, CanRemoveAxisFromSystem);
```

- [ ] **Step 4: 更新 UpdateAxesInSystem 方法，同时刷新可用轴列表**

将 `UpdateAxesInSystem` 方法替换为：

```csharp
        /// <summary>
        /// 更新插补系中的轴显示，同时刷新可用轴列表
        /// </summary>
        private void UpdateAxesInSystem()
        {
            if (SelectedSystem == null) return;

            SelectedAxesInSystem.Clear();

            foreach (var axisId in SelectedSystem.Axes)
            {
                var parts = axisId.Split('-');
                if (parts.Length != 2) continue;

                int setCardId = int.Parse(parts[0]);
                int setAxisId = int.Parse(parts[1]);

                var axisConfig = Axes.FirstOrDefault(a =>
                    a.CardId == setCardId &&
                    a.AxisId == setAxisId);

                if (axisConfig != null)
                {
                    SelectedAxesInSystem.Add(new AxisInSystem
                    {
                        Name = axisConfig.Name,
                        ConfigId = axisConfig.Name,
                        SetCardId = setCardId,
                        SetAxisId = setAxisId
                    });
                }
                else
                {
                    SelectedAxesInSystem.Add(new AxisInSystem
                    {
                        Name = axisId,
                        ConfigId = axisId,
                        SetCardId = setCardId,
                        SetAxisId = setAxisId
                    });
                }
            }

            RefreshAvailableAxes();
        }

        /// <summary>
        /// 刷新可用轴列表（排除已在插补系中的轴）
        /// </summary>
        private void RefreshAvailableAxes()
        {
            AvailableAxesForSystem.Clear();

            if (SelectedSystem == null) return;

            var usedAxes = new HashSet<string>(SelectedSystem.Axes);

            foreach (var axis in Axes)
            {
                if (!usedAxes.Contains(axis.ConfigId))
                {
                    AvailableAxesForSystem.Add(axis);
                }
            }
        }
```

- [ ] **Step 5: 添加添加/移除轴方法**

在 `LoadSystemConfigurations` 方法之后添加：

```csharp
        /// <summary>
        /// 添加选中轴到当前插补系
        /// </summary>
        private void OnAddAxisToSystem()
        {
            if (SelectedSystem == null || SelectedAvailableAxis == null) return;

            string configId = SelectedAvailableAxis.ConfigId;
            if (!SelectedSystem.Axes.Contains(configId))
            {
                SelectedSystem.Axes.Add(configId);
                UpdateAxesInSystem();
                SyncSystemAxesToHwConfig();
                AddAxisToSystemCommand.RaiseCanExecuteChanged();
                RemoveAxisFromSystemCommand.RaiseCanExecuteChanged();
            }
        }

        private bool CanAddAxisToSystem() => SelectedSystem != null && SelectedAvailableAxis != null;

        /// <summary>
        /// 从当前插补系移除选中轴
        /// </summary>
        private void OnRemoveAxisFromSystem()
        {
            if (SelectedSystem == null || SelectedAxisInSystem == null) return;

            string configId = $"{SelectedAxisInSystem.SetCardId}-{SelectedAxisInSystem.SetAxisId}";
            if (SelectedSystem.Axes.Contains(configId))
            {
                SelectedSystem.Axes.Remove(configId);
                UpdateAxesInSystem();
                SyncSystemAxesToHwConfig();
                AddAxisToSystemCommand.RaiseCanExecuteChanged();
                RemoveAxisFromSystemCommand.RaiseCanExecuteChanged();
            }
        }

        private bool CanRemoveAxisFromSystem() => SelectedSystem != null && SelectedAxisInSystem != null;

        /// <summary>
        /// 同步插补系轴配置到hwcfg.xml
        /// </summary>
        private void SyncSystemAxesToHwConfig()
        {
            try
            {
                _parameterService.SyncInterpolationAxesToHwConfig(InterpolationSystems);
                _parameterService.SaveAllInterpolationSystems(InterpolationSystems);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"同步到配置文件失败: {ex.Message}", "警告",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
```

---

### Task 4: View 插补系轴列表区域添加添加/移除按钮

**Files:**
- Modify: `MotionControl/Views/AxisSettingView.xaml`

- [ ] **Step 1: 替换插补系轴列表区域，添加可用轴选择器和添加/移除按钮**

找到以下代码段（约第 412-462 行）：

```xml
                                            <GroupBox Header="{lang:Lang AxisSetting_IncludedAxes}"
                                                      Margin="0,10,0,0">
                                                <Grid>
                                                    <Grid.RowDefinitions>
                                                        <RowDefinition Height="*" />
                                                        <RowDefinition Height="Auto" />
                                                    </Grid.RowDefinitions>

                                                    <ListBox Grid.Row="0"
                                                             ItemsSource="{Binding SelectedAxesInSystem}"
                                                             MinHeight="160"
                                                             ScrollViewer.VerticalScrollBarVisibility="Auto">
```

将整个 `<GroupBox Header="{lang:Lang AxisSetting_IncludedAxes}"` 到其对应的 `</GroupBox>` 替换为：

```xml
                                            <GroupBox Header="{lang:Lang AxisSetting_IncludedAxes}"
                                                      Margin="0,10,0,0">
                                                <Grid>
                                                    <Grid.RowDefinitions>
                                                        <RowDefinition Height="*" />
                                                        <RowDefinition Height="Auto" />
                                                    </Grid.RowDefinitions>

                                                    <ListBox Grid.Row="0"
                                                             ItemsSource="{Binding SelectedAxesInSystem}"
                                                             SelectedItem="{Binding SelectedAxisInSystem}"
                                                             MinHeight="120"
                                                             ScrollViewer.VerticalScrollBarVisibility="Auto">
                                                        <ListBox.ItemContainerStyle>
                                                            <Style TargetType="ListBoxItem"
                                                                   BasedOn="{StaticResource MaterialDesignListBoxItem}">
                                                                <Setter Property="Padding" Value="0" />
                                                                <Setter Property="Background" Value="Transparent" />
                                                            </Style>
                                                        </ListBox.ItemContainerStyle>
                                                        <ListBox.ItemTemplate>
                                                            <DataTemplate>
                                                                <StackPanel Orientation="Horizontal" Margin="4">
                                                                    <TextBlock Text="{Binding Name}" FontWeight="Bold" VerticalAlignment="Center" MinWidth="120" />
                                                                    <TextBlock Text="{lang:Lang AxisSetting_BracketOpen}" VerticalAlignment="Center" />
                                                                    <TextBlock Text="{lang:Lang AxisSetting_Card}" VerticalAlignment="Center" />
                                                                    <TextBlock Text="{Binding SetCardId}" VerticalAlignment="Center" Margin="2,0,0,0" />
                                                                    <TextBlock Text="{lang:Lang AxisSetting_BracketSeparator}" VerticalAlignment="Center" />
                                                                    <TextBlock Text="{lang:Lang AxisSetting_AxisId}" VerticalAlignment="Center" />
                                                                    <TextBlock Text="{Binding SetAxisId}" VerticalAlignment="Center" Margin="2,0,0,0" />
                                                                    <TextBlock Text="{lang:Lang AxisSetting_BracketClose}" VerticalAlignment="Center" />
                                                                </StackPanel>
                                                            </DataTemplate>
                                                        </ListBox.ItemTemplate>
                                                    </ListBox>

                                                    <StackPanel Grid.Row="1" Orientation="Horizontal" Margin="0,8,0,0" HorizontalAlignment="Center">
                                                        <Button Command="{Binding RemoveAxisFromSystemCommand}" Style="{DynamicResource MaterialDesignOutlinedButton}" ToolTip="{lang:Lang AxisSetting_RemoveAxisToolTip}" materialDesign:ButtonAssist.CornerRadius="4" BorderBrush="#C62828" Foreground="#C62828" Padding="8,4">
                                                            <StackPanel Orientation="Horizontal">
                                                                <materialDesign:PackIcon Kind="MinusCircleOutline" Width="16" Height="16" VerticalAlignment="Center"/>
                                                                <TextBlock Text="{lang:Lang AxisSetting_RemoveAxis}" Margin="4,0,0,0" VerticalAlignment="Center" FontSize="12"/>
                                                            </StackPanel>
                                                        </Button>
                                                    </StackPanel>
                                                </Grid>
                                            </GroupBox>

                                            <GroupBox Header="{lang:Lang AxisSetting_AvailableAxes}" Margin="0,8,0,0">
                                                <Grid>
                                                    <Grid.RowDefinitions>
                                                        <RowDefinition Height="*" />
                                                        <RowDefinition Height="Auto" />
                                                    </Grid.RowDefinitions>

                                                    <ListBox Grid.Row="0"
                                                             ItemsSource="{Binding AvailableAxesForSystem}"
                                                             SelectedItem="{Binding SelectedAvailableAxis}"
                                                             MinHeight="80"
                                                             MaxHeight="120"
                                                             ScrollViewer.VerticalScrollBarVisibility="Auto">
                                                        <ListBox.ItemContainerStyle>
                                                            <Style TargetType="ListBoxItem"
                                                                   BasedOn="{StaticResource MaterialDesignListBoxItem}">
                                                                <Setter Property="Padding" Value="0" />
                                                                <Setter Property="Background" Value="Transparent" />
                                                            </Style>
                                                        </ListBox.ItemContainerStyle>
                                                        <ListBox.ItemTemplate>
                                                            <DataTemplate>
                                                                <StackPanel Orientation="Horizontal" Margin="4">
                                                                    <materialDesign:PackIcon Kind="Axis" Width="16" Height="16" Foreground="#1565C0" VerticalAlignment="Center" Margin="0,0,4,0"/>
                                                                    <TextBlock Text="{Binding Name}" VerticalAlignment="Center" MinWidth="100"/>
                                                                    <TextBlock Text="{Binding ConfigId}" Foreground="#FF607D8B" VerticalAlignment="Center" FontSize="11"/>
                                                                </StackPanel>
                                                            </DataTemplate>
                                                        </ListBox.ItemTemplate>
                                                    </ListBox>

                                                    <StackPanel Grid.Row="1" Orientation="Horizontal" Margin="0,8,0,0" HorizontalAlignment="Center">
                                                        <Button Command="{Binding AddAxisToSystemCommand}" Style="{DynamicResource MaterialDesignRaisedButton}" Background="#FF1E88E5" Foreground="White" ToolTip="{lang:Lang AxisSetting_AddAxisToolTip}" materialDesign:ButtonAssist.CornerRadius="4" Padding="8,4">
                                                            <StackPanel Orientation="Horizontal">
                                                                <materialDesign:PackIcon Kind="PlusCircleOutline" Width="16" Height="16" VerticalAlignment="Center"/>
                                                                <TextBlock Text="{lang:Lang AxisSetting_AddAxis}" Margin="4,0,0,0" VerticalAlignment="Center" FontSize="12"/>
                                                            </StackPanel>
                                                        </Button>
                                                    </StackPanel>
                                                </Grid>
                                            </GroupBox>
```

---

### Task 5: 多语言资源更新

**Files:**
- Modify: `MainApp/Languages/Strings.zh-CN.xaml`
- Modify: `MainApp/Languages/Strings.en-US.xaml`

- [ ] **Step 1: 在中文语言文件中添加新键**

在 `AxisSetting_InterpolationOperation` 行之后添加：

```xml
    <sys:String x:Key="AxisSetting_AvailableAxes">可用轴</sys:String>
    <sys:String x:Key="AxisSetting_AddAxis">添加轴</sys:String>
    <sys:String x:Key="AxisSetting_AddAxisToolTip">将选中的轴添加到当前插补系</sys:String>
    <sys:String x:Key="AxisSetting_RemoveAxis">移除轴</sys:String>
    <sys:String x:Key="AxisSetting_RemoveAxisToolTip">从当前插补系移除选中的轴</sys:String>
```

- [ ] **Step 2: 在英文语言文件中添加新键**

在 `AxisSetting_InterpolationOperation` 行之后添加：

```xml
    <sys:String x:Key="AxisSetting_AvailableAxes">Available Axes</sys:String>
    <sys:String x:Key="AxisSetting_AddAxis">Add Axis</sys:String>
    <sys:String x:Key="AxisSetting_AddAxisToolTip">Add selected axis to current interpolation system</sys:String>
    <sys:String x:Key="AxisSetting_RemoveAxis">Remove Axis</sys:String>
    <sys:String x:Key="AxisSetting_RemoveAxisToolTip">Remove selected axis from current interpolation system</sys:String>
```

---

## 自检清单

### 1. 需求覆盖

| 需求 | 对应 Task |
|------|-----------|
| 可添加轴到坐标系 | Task 3 (AddAxisToSystemCommand) + Task 4 (UI按钮) |
| 可从坐标系移除轴 | Task 3 (RemoveAxisFromSystemCommand) + Task 4 (UI按钮) |
| 与hwcfg.xml同步（axes属性格式"卡号-轴号,卡号-轴号"） | Task 2 (SyncInterpolationAxesToHwConfig) |
| 从hwcfg.xml加载axes属性 | Task 2 (LoadInterpolationAxesFromHwConfig + LoadInterpolationSystems更新) |

### 2. 占位符扫描

无 TBD、TODO、implement later 等占位符。

### 3. 类型一致性

- `InterpolationSystem.Axes` 为 `List<string>`，格式为 `"卡号-轴号"` ✓
- `AxisInfo.ConfigId` 返回 `"{CardId}-{AxisId}"` 格式 ✓
- `AxisInSystem.SetCardId` / `SetAxisId` 为 `int` ✓
- `SyncInterpolationAxesToHwConfig` 接收 `IEnumerable<InterpolationSystem>` ✓
- `AddAxisToSystemCommand` / `RemoveAxisFromSystemCommand` 为 `DelegateCommand` 带 CanExecute ✓
