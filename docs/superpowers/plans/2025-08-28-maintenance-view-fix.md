# MaintenanceView 页面无法打开 修复计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复 MaintenanceView 页面无法正常打开的问题

**Architecture:** 根因是子视图中重复的 MergedDictionaries（BundledTheme + MaterialDesignTheme.Light.xaml）与 App.xaml 全局主题冲突，导致 XAML 资源解析异常。修复方案是移除所有子视图中的重复主题引用，仅保留自定义样式。

**Tech Stack:** WPF, Prism, MaterialDesignInXaml

---

## 根因分析

### 问题 1（严重）：MergedDictionaries 主题冲突

所有 4 个 Maintenance 视图都在 `UserControl.Resources` 中包含了：
```xml
<ResourceDictionary Source="pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml" />
<materialDesign:BundledTheme BaseTheme="Light" PrimaryColor="DeepPurple" SecondaryColor="Lime" />
```

而 `App.xaml` 已经全局注册了相同的主题。当子视图嵌套在父视图中时：
- MaintenanceView 包含 BundledTheme
- NeedleCameraAlignmentView 也包含 BundledTheme
- NeedleAlignerView 也包含 BundledTheme
- NeedleCalibrationVerifyView 也包含 BundledTheme

这导致 **4 层嵌套的重复主题注册**，可能引发：
- XAML 解析异常（资源键重复）
- 隐式样式覆盖冲突
- 内存泄漏

### 问题 2（中等）：NeedleAlignerView 缺少 CardHeaderStyle 定义

NeedleAlignerView.xaml 引用了 `CardHeaderStyle`（在 NeedleCameraAlignmentView 中定义），但自身 Resources 中没有定义。由于是独立 UserControl，资源不共享。

### 问题 3（轻微）：NeedleCalibrationVerifyView 缺少部分自定义样式

NeedleCalibrationVerifyView 没有定义 `ParamRowStyle`、`ParamLabelStyle` 等样式，但也没有引用它们，所以不是直接问题。

---

## 文件变更清单

| 文件 | 变更 |
|------|------|
| `Module\Controls\Maintenance\MaintenanceView.xaml` | 移除 MergedDictionaries 中的主题引用 |
| `Module\Controls\Maintenance\NeedleCameraAlignmentView.xaml` | 移除 MergedDictionaries 中的主题引用 |
| `Module\Controls\Maintenance\NeedleAlignerView.xaml` | 移除 MergedDictionaries 中的主题引用，补充 CardHeaderStyle |
| `Module\Controls\Maintenance\NeedleCalibrationVerifyView.xaml` | 移除 MergedDictionaries 中的主题引用 |

---

### Task 1: 移除 MaintenanceView.xaml 中的重复主题

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\Module\Controls\Maintenance\MaintenanceView.xaml:17-24`

- [ ] **Step 1: 移除 MergedDictionaries 中的主题引用**

将：
```xml
<UserControl.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml" />
            <materialDesign:BundledTheme BaseTheme="Light"
                                         PrimaryColor="DeepPurple"
                                         SecondaryColor="Lime" />
        </ResourceDictionary.MergedDictionaries>

        <Style x:Key="StatusPill" TargetType="Border">
```

改为：
```xml
<UserControl.Resources>
    <ResourceDictionary>
        <Style x:Key="StatusPill" TargetType="Border">
```

- [ ] **Step 2: 验证构建**

Run: `dotnet build "c:\WorkFiles\GZQL_MACHINE\Module\Module.csproj" --no-dependencies 2>&1 | Select-String -Pattern "error" | Where-Object { $_ -notmatch "DispenseDetailViewModel" }`
Expected: 0 errors

---

### Task 2: 移除 NeedleCameraAlignmentView.xaml 中的重复主题

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\Module\Controls\Maintenance\NeedleCameraAlignmentView.xaml:17-24`

- [ ] **Step 1: 移除 MergedDictionaries 中的主题引用**

将：
```xml
<UserControl.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml" />
            <materialDesign:BundledTheme BaseTheme="Light"
                                         PrimaryColor="DeepPurple"
                                         SecondaryColor="Lime" />
        </ResourceDictionary.MergedDictionaries>
        <converters:IntToBoolConverter x:Key="IntToBoolConverter" />
```

改为：
```xml
<UserControl.Resources>
    <ResourceDictionary>
        <converters:IntToBoolConverter x:Key="IntToBoolConverter" />
```

- [ ] **Step 2: 验证构建**

---

### Task 3: 移除 NeedleAlignerView.xaml 中的重复主题 + 补充缺失样式

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\Module\Controls\Maintenance\NeedleAlignerView.xaml:17-24`

- [ ] **Step 1: 移除 MergedDictionaries 中的主题引用 + 添加 CardHeaderStyle**

将：
```xml
<UserControl.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml" />
            <materialDesign:BundledTheme BaseTheme="Light"
                                         PrimaryColor="DeepPurple"
                                         SecondaryColor="Lime" />
        </ResourceDictionary.MergedDictionaries>
        <converters:InverseBooleanConverter x:Key="InverseBooleanConverter" />
```

改为：
```xml
<UserControl.Resources>
    <ResourceDictionary>
        <converters:InverseBooleanConverter x:Key="InverseBooleanConverter" />

        <Style x:Key="CardHeaderStyle" TargetType="StackPanel">
            <Setter Property="Orientation" Value="Horizontal" />
            <Setter Property="Margin" Value="0,0,0,12" />
        </Style>
```

- [ ] **Step 2: 验证构建**

---

### Task 4: 移除 NeedleCalibrationVerifyView.xaml 中的重复主题

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\Module\Controls\Maintenance\NeedleCalibrationVerifyView.xaml:17-24`

- [ ] **Step 1: 移除 MergedDictionaries 中的主题引用**

将：
```xml
<UserControl.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml" />
            <materialDesign:BundledTheme BaseTheme="Light"
                                         PrimaryColor="DeepPurple"
                                         SecondaryColor="Lime" />
        </ResourceDictionary.MergedDictionaries>
        <converters:IntToBoolConverter x:Key="IntToBoolConverter" />
```

改为：
```xml
<UserControl.Resources>
    <ResourceDictionary>
        <converters:IntToBoolConverter x:Key="IntToBoolConverter" />
```

- [ ] **Step 2: 验证构建**

---

### Task 5: 最终构建验证

- [ ] **Step 1: 完整构建 Module 项目**

Run: `dotnet build "c:\WorkFiles\GZQL_MACHINE\Module\Module.csproj" --no-dependencies 2>&1 | Select-String -Pattern "error" | Where-Object { $_ -notmatch "DispenseDetailViewModel" }`
Expected: 0 errors

- [ ] **Step 2: 完整构建 Core 项目**

Run: `dotnet build "c:\WorkFiles\GZQL_MACHINE\Core\Core.csproj" --no-dependencies 2>&1 | Select-String -Pattern "error"`
Expected: 0 errors
