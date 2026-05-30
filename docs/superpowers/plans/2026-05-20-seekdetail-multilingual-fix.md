# SeekDetailView 多语言修复实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 SeekDetailView 新增完整的多语言支持基础设施，消除 7 处硬编码中文字符串，补全 17 个英文翻译，实现 100% 多语言覆盖

**Architecture:** 参照 VisionDetailViewModel 的成功模式，为 SeekDetailViewModel 新增 ILocalizationService 依赖注入和 L() 便捷方法，然后替换所有硬编码字符串为多资源键调用，最后补全英文资源文件翻译

**Tech Stack:** WPF, PRISM (依赖注入), MaterialDesignInXAML, ResourceDictionary (XAML), ILocalizationService

---

## 文件结构

```
修改文件:
├── Module/Controls/StepDetails/SeekDetailViewModel.cs    # 新增基础设施 + 修复 7 处硬编码
├── MainApp/Languages/Strings.zh-CN.xaml                   # 新增 6 个运行时资源键
└── MainApp/Languages/Strings.en-US.xaml                   # 新增 6 个 + 替换 17 个英文值
```

---

### Task 1: 新增多语言基础设施（ILocalizationService + L() 方法）

**Files:**
- Modify: `Module/Controls/StepDetails/SeekDetailViewModel.cs`

- [ ] **Step 1: 在文件顶部新增 using 引用**

在现有 using 语句区域（第 1-14 行）添加：

```csharp
using Core.Abstraction;  // ILocalizationService 接口
```

**位置：** 建议放在 `using Recipe.Interfaces;` 之后（第 7 行后）

- [ ] **Step 2: 新增只读字段用于保存 ILocalizationService 实例**

在类内部、现有字段声明区域（第 23-27 行附近）添加：

```csharp
private readonly ILocalizationService _localizationService;
```

**位置：** 建议放在 `private bool _isRefreshing;` 之后（第 27 行后）

- [ ] **Step 3: 修改构造函数签名和实现**

将当前构造函数（第 80-99 行）：

```csharp
public SeekDetailViewModel(IMotionService motionService, IRecipePoolService recipePoolService)
{
    _motionService = motionService;
    _recipePoolService = recipePoolService;

    AddChannelRowCommand = new DelegateCommand(OnAddChannelRow);
    DeleteChannelRowCommand = new DelegateCommand(OnDeleteChannelRow, () => SelectedChannelRow != null)
        .ObservesProperty(() => SelectedChannelRow);
    ImportCommand = new DelegateCommand(OnImport);
    ExportCommand = new DelegateCommand(OnExport, () => ChannelRows.Count > 0)
        .ObservesProperty(() => ChannelRows);
    StartRefreshCommand = new DelegateCommand(OnStartRefresh);
    StopRefreshCommand = new DelegateCommand(OnStopRefresh, () => IsRefreshing)
        .ObservesProperty(() => IsRefreshing);
    CloseCommand = new DelegateCommand(OnClose);
    SaveOnlyCommand = new DelegateCommand(OnSaveOnly);
    SaveCommand = new DelegateCommand(OnSave);

    LoadGlobalVariablesAsync().ConfigureAwait(false);
}
```

替换为：

```csharp
public SeekDetailViewModel(
    IMotionService motionService,
    IRecipePoolService recipePoolService,
    ILocalizationService localizationService)
{
    _motionService = motionService;
    _recipePoolService = recipePoolService;
    _localizationService = localizationService;

    AddChannelRowCommand = new DelegateCommand(OnAddChannelRow);
    DeleteChannelRowCommand = new DelegateCommand(OnDeleteChannelRow, () => SelectedChannelRow != null)
        .ObservesProperty(() => SelectedChannelRow);
    ImportCommand = new DelegateCommand(OnImport);
    ExportCommand = new DelegateCommand(OnExport, () => ChannelRows.Count > 0)
        .ObservesProperty(() => ChannelRows);
    StartRefreshCommand = new DelegateCommand(OnStartRefresh);
    StopRefreshCommand = new DelegateCommand(OnStopRefresh, () => IsRefreshing)
        .ObservesProperty(() => IsRefreshing);
    CloseCommand = new DelegateCommand(OnClose);
    SaveOnlyCommand = new DelegateCommand(OnSaveOnly);
    SaveCommand = new DelegateCommand(OnSave);

    LoadGlobalVariablesAsync().ConfigureAwait(false);
}
```

**关键变更：**
- ✅ 新增第三个参数 `ILocalizationService localizationService`
- ✅ 新增赋值语句 `_localizationService = localizationService;`
- ✅ 其余命令初始化逻辑保持不变

- [ ] **Step 4: 新增 L() 便捷方法**

在属性定义区域之后（建议放在 `IsRefreshing` 属性之后，第 65 行后），命令定义之前（第 67 行之前），添加：

```csharp
/// <summary>
/// 获取多语言文本（便捷方法）
/// </summary>
private string L(string key) => _localizationService.GetResource(key);
```

**参考：** 与 VisionDetailViewModel.cs 第 180 行的实现完全一致

- [ ] **Step 5: 验证编译通过**

Run: `dotnet build` 或在 Visual Studio 中编译项目
Expected: 编译成功（PRISM 容器应能自动解析 ILocalizationService）
⚠️ 如果编译失败，检查：
   - ILocalizationService 是否已在 Prism 容器中注册
   - Core.Abstraction 命名空间是否正确引用

---

### Task 2: 修复 ViewModel 硬编码 — 默认通道描述

**Files:**
- Modify: `Module/Controls/StepDetails/SeekDetailViewModel.cs:114`

- [ ] **Step 1: 定位并替换第 114 行的默认通道描述**

将：
```csharp
new SeekChannelRow { Sub = 1, LinkedChannel = 0, TargetForce = 0.3, ForceMin = -2.0, ForceMax = 2.0, Description = "径向力" }
```

替换为：
```csharp
new SeekChannelRow { Sub = 1, LinkedChannel = 0, TargetForce = 0.3, ForceMin = -2.0, ForceMax = 2.0, Description = L("SeekDetail_DefaultDesc") }
```

**说明：**
- 此处位于 `InitializeFromStep()` 方法中的默认通道创建逻辑
- 当 Step.SeekDetail 为空时，会创建此默认通道
- 使用 L() 方法获取多语言文本

---

### Task 3: 修复 ViewModel 硬编码 — 导入对话框

**Files:**
- Modify: `Module/Controls/StepDetails/SeekDetailViewModel.cs:185-208` （OnImport 方法）

- [ ] **Step 1: 替换导入对话框过滤器（第 189 行）**

将：
```csharp
Filter = "JSON 文件|*.json",
```

替换为：
```csharp
Filter = L("SeekDetail_JsonFileFilter"),
```

- [ ] **Step 2: 替换导入对话框标题（第 190 行）**

将：
```csharp
Title = "导入 SEEK 通道配置"
```

替换为：
```csharp
Title = L("SeekDetail_ImportDialogTitle")
```

- [ ] **Step 3: 替换导入失败错误消息（第 206 行）**

将：
```csharp
System.Diagnostics.Debug.WriteLine($"导入失败: {ex.Message}");
```

替换为：
```csharp
System.Diagnostics.Debug.WriteLine(string.Format(L("SeekDetail_Error_ImportFailed"), ex.Message));
```

**验证要点：**
- ✅ 保持原有的异常处理逻辑不变
- ✅ 仅替换字符串字面量为 L() 调用
- ✅ 格式化参数 `{0}` 通过 string.Format 传递

---

### Task 4: 修复 ViewModel 硬编码 — 导出对话框

**Files:**
- Modify: `Module/Controls/StepDetails/SeekDetailViewModel.cs:211-230` （OnExport 方法）

- [ ] **Step 1: 替换导出对话框过滤器（第 215 行）**

将：
```csharp
Filter = "JSON 文件|*.json",
```

替换为：
```csharp
Filter = L("SeekDetail_JsonFileFilter"),
```

> 注意：复用 Task 3 中已使用的 `SeekDetail_JsonFileFilter` 键

- [ ] **Step 2: 替换导出对话框标题（第 216 行）**

将：
```csharp
Title = "导出 SEEK 通道配置",
```

替换为：
```csharp
Title = L("SeekDetail_ExportDialogTitle")
```

- [ ] **Step 3: 替换导出失败错误消息（第 228 行）**

将：
```csharp
System.Diagnostics.Debug.WriteLine($"导出失败: {ex.Message}");
```

替换为：
```csharp
System.Diagnostics.Debug.WriteLine(string.Format(L("SeekDetail_Error_ExportFailed"), ex.Message));
```

- [ ] **Step 4: 验证编译通过**

Run: `dotnet build` 或在 Visual Studio 中编译项目
Expected: 编译成功，无错误

---

### Task 5: 在 Strings.zh-CN.xaml 中新增 6 个资源键

**Files:**
- Modify: `MainApp/Languages/Strings.zh-CN.xaml`

- [ ] **Step 1: 定位 SeekDetailView 资源键区域**

搜索 `<!-- ═══ SeekDetailView ═══ -->` 注释（约在第 1628 行），在其后的最后一个 SeekDetail 键（`SeekDetail_ConfirmContinue`，约第 1645 行）之后添加新的资源键组

- [ ] **Step 2: 新增 6 个运行时文本资源键**

在 `SeekDetail_ConfirmContinue` 键之后添加：

```xml
    <!-- SeekDetailView - 运行时文本 -->
    <sys:String x:Key="SeekDetail_DefaultDesc">径向力</sys:String>
    <sys:String x:Key="SeekDetail_JsonFileFilter">JSON 文件|*.json</sys:String>
    <sys:String x:Key="SeekDetail_ImportDialogTitle">导入 SEEK 通道配置</sys:String>
    <sys:String x:Key="SeekDetail_ExportDialogTitle">导出 SEEK 通道配置</sys:String>
    <sys:String x:Key="SeekDetail_Error_ImportFailed">导入失败: {0}</sys:String>
    <sys:String x:Key="SeekDetail_Error_ExportFailed">导出失败: {0}</sys:String>
```

**格式要求：**
- ✅ 保持与上方键相同的缩进（4 个空格）
- ✅ 添加注释标识这是运行时文本（非 XAML 绑定使用）
- ✅ XML 标签闭合正确

---

### Task 6: 补全 Strings.en-US.xaml — 替换 17 个 UI 文本翻译

**Files:**
- Modify: `MainApp/Languages/Strings.en-US.xaml:1703-1719`

- [ ] **Step 1: 替换 SeekDetail_Offline**

将：
```xml
<sys:String x:Key="SeekDetail_Offline">离线</sys:String>
```
替换为：
```xml
<sys:String x:Key="SeekDetail_Offline">Offline</sys:String>
```

- [ ] **Step 2: 替换 SeekDetail_RealTimeCollecting**

将：
```xml
<sys:String x:Key="SeekDetail_RealTimeCollecting">● 实时采集中</sys:String>
```
替换为：
```xml
<sys:String x:Key="SeekDetail_RealTimeCollecting">● Collecting</sys:String>
```

- [ ] **Step 3: 替换 SeekDetail_Column_LinkedChannel**

将：
```xml
<sys:String x:Key="SeekDetail_Column_LinkedChannel">链接通道</sys:String>
```
替换为：
```xml
<sys:String x:Key="SeekDetail_Column_LinkedChannel">Linked Ch</sys:String>
```

- [ ] **Step 4: 替换 SeekDetail_Column_GlobalVar**

将：
```xml
<sys:String x:Key="SeekDetail_Column_GlobalVar">全局变量</sys:String>
```
替换为：
```xml
<sys:String x:Key="SeekDetail_Column_GlobalVar">Global Var</sys:String>
```

- [ ] **Step 5: 替换 SeekDetail_AddChannel**

将：
```xml
<sys:String x:Key="SeekDetail_AddChannel">添加</sys:String>
```
替换为：
```xml
<sys:String x:Key="SeekDetail_AddChannel">Add</sys:String>
```

- [ ] **Step 6: 替换 SeekDetail_AddChannelToolTip**

将：
```xml
<sys:String x:Key="SeekDetail_AddChannelToolTip">添加通道</sys:String>
```
替换为：
```xml
<sys:String x:Key="SeekDetail_AddChannelToolTip">Add channel</sys:String>
```

- [ ] **Step 7: 替换 SeekDetail_DeleteChannel**

将：
```xml
<sys:String x:Key="SeekDetail_DeleteChannel">删除</sys:String>
```
替换为：
```xml
<sys:String x:Key="SeekDetail_DeleteChannel">Delete</sys:String>
```

- [ ] **Step 8: 替换 SeekDetail_DeleteChannelToolTip**

将：
```xml
<sys:String x:Key="SeekDetail_DeleteChannelToolTip">删除选中通道</sys:String>
```
替换为：
```xml
<sys:String x:Key="SeekDetail_DeleteChannelToolTip">Delete selected channel</sys:String>
```

- [ ] **Step 9: 替换 SeekDetail_Refresh**

将：
```xml
<sys:String x:Key="SeekDetail_Refresh">刷新</sys:String>
```
替换为：
```xml
<sys:String x:Key="SeekDetail_Refresh">Refresh</sys:String>
```

- [ ] **Step 10: 替换 SeekDetail_RefreshToolTip**

将：
```xml
<sys:String x:Key="SeekDetail_RefreshToolTip">开始实时刷新力值数据</sys:String>
```
替换为：
```xml
<sys:String x:Key="SeekDetail_RefreshToolTip">Start real-time force refresh</sys:String>
```

- [ ] **Step 11: 替换 SeekDetail_Stop**

将：
```xml
<sys:String x:Key="SeekDetail_Stop">停止</sys:String>
```
替换为：
```xml
<sys:String x:Key="SeekDetail_Stop">Stop</sys:String>
```

- [ ] **Step 12: 替换 SeekDetail_StopToolTip**

将：
```xml
<sys:String x:Key="SeekDetail_StopToolTip">停止实时刷新</sys:String>
```
替换为：
```xml
<sys:String x:Key="SeekDetail_StopToolTip">Stop real-time refresh</sys:String>
```

- [ ] **Step 13: 替换 SeekDetail_Import**

将：
```xml
<sys:String x:Key="SeekDetail_Import">导入</sys:String>
```
替换为：
```xml
<sys:String x:Key="SeekDetail_Import">Import</sys:String>
```

- [ ] **Step 14: 替换 SeekDetail_ImportToolTip**

将：
```xml
<sys:String x:Key="SeekDetail_ImportToolTip">从 JSON 文件导入通道配置</sys:String>
```
替换为：
```xml
<sys:String x:Key="SeekDetail_ImportToolTip">Import channel config from JSON</sys:String>
```

- [ ] **Step 15: 替换 SeekDetail_Export**

将：
```xml
<sys:String x:Key="SeekDetail_Export">导出</sys:String>
```
替换为：
```xml
<sys:String x:Key="SeekDetail_Export">Export</sys:String>
```

- [ ] **Step 16: 替换 SeekDetail_ExportToolTip**

将：
```xml
<sys:String x:Key="SeekDetail_ExportToolTip">导出通道配置为 JSON 文件</sys:String>
```
替换为：
```xml
<sys:String x:Key="SeekDetail_ExportToolTip">Export channel config to JSON</sys:String>
```

- [ ] **Step 17: 替换 SeekDetail_ConfirmContinue**

将：
```xml
<sys:String x:Key="SeekDetail_ConfirmContinue">确认继续</sys:String>
```
替换为：
```xml
<sys:String x:Key="SeekDetail_ConfirmContinue">Confirm & Continue</sys:String>
```

**验证要点：**
- ✅ 所有 UI 标签文本翻译准确且简洁
- ✅ ToolTip 文本符合英文表达习惯
- ✅ DataGrid 列头缩写合理（Linked Ch, Global Var）
- ✅ XML 格式正确，无语法错误

- [ ] **Step 18: 验证编译通过**

Run: `dotnet build` 或在 Visual Studio 中编译项目
Expected: 编译成功，无错误

---

### Task 7: 在 Strings.en-US.xaml 中新增 6 个运行时资源键

**Files:**
- Modify: `MainApp/Languages/Strings.en-US.xaml`

- [ ] **Step 1: 定位 SeekDetailView 资源键区域的末尾**

搜索最后一个 SeekDetail 键 `SeekDetail_ConfirmContinue`（约在第 1719 行或替换后位置），在其后添加新的运行时文本资源键组

- [ ] **Step 2: 新增 6 个英文运行时文本资源键**

在 `SeekDetail_ConfirmContinue` 键之后添加：

```xml
    <!-- SeekDetailView - Runtime Text -->
    <sys:String x:Key="SeekDetail_DefaultDesc">Radial Force</sys:String>
    <sys:String x:Key="SeekDetail_JsonFileFilter">JSON Files|*.json</sys:String>
    <sys:String x:Key="SeekDetail_ImportDialogTitle">Import SEEK Channel Config</sys:String>
    <sys:String x:Key="SeekDetail_ExportDialogTitle">Export SEEK Channel Config</sys:String>
    <sys:String x:Key="SeekDetail_Error_ImportFailed">Import failed: {0}</sys:String>
    <sys:String x:Key="SeekDetail_Error_ExportFailed">Export failed: {0}</sys:String>
```

**与中文对照验证：**
- ✅ `SeekDetail_DefaultDesc`: "径向力" → "Radial Force"
- ✅ `SeekDetail_JsonFileFilter`: "JSON 文件|*.json" → "JSON Files|*.json"
- ✅ `SeekDetail_ImportDialogTitle`: "导入 SEEK 通道配置" → "Import SEEK Channel Config"
- ✅ `SeekDetail_ExportDialogTitle`: "导出 SEEK 通道配置" → "Export SEEK Channel Config"
- ✅ `SeekDetail_Error_ImportFailed`: "导入失败: {0}" → "Import failed: {0}"
- ✅ `SeekDetail_Error_ExportFailed`: "导出失败: {0}" → "Export failed: {0}"

- [ ] **Step 3: 验证编译通过**

Run: `dotnet build --configuration Release`
Expected: 编译成功，0 错误

---

### Task 8: 最终验证与总结

- [ ] **Step 1: 完整编译验证**

Run: `dotnet build --configuration Release`
Expected: 编译成功，0 错误（允许项目原有警告）

- [ ] **Step 2: 搜索残留的硬编码中文**

在 `Module/Controls/StepDetails/SeekDetailViewModel.cs` 中搜索中文字符串正则 `[\u4e00-\u9fa5]`（排除注释 `///` 和 `//`）

Expected:
- ✅ 仅剩 XML 文档注释中的中文说明文字（合规）
- ❌ 无运行时硬编码中文字符串（如 `"径向力"`, `"导入失败"` 等）

具体检查点：
- 第 114 行：应为 `L("SeekDetail_DefaultDesc")` 而非 `"径向力"`
- 第 189 行：应为 `L("SeekDetail_JsonFileFilter")` 而非 `"JSON 文件"`
- 第 190 行：应为 `L("SeekDetail_ImportDialogTitle")` 而非 `"导入 SEEK..."`
- 第 206 行：应为 `string.Format(L("SeekDetail_Error_ImportFailed"), ...)` 而非 `$"导入失败:..."`
- 第 215 行：应为 `L("SeekDetail_JsonFileFilter")` 而非 `"JSON 文件"`
- 第 216 行：应为 `L("SeekDetail_ExportDialogTitle")` 而非 `"导出 SEEK..."`
- 第 228 行：应为 `string.Format(L("SeekDetail_Error_ExportFailed"), ...)` 而非 `$"导出失败:..."`

- [ ] **Step 3: 验证资源键完整性对比**

提取两个文件中所有 `x:Key="SeekDetail_*"` 的键并对比：

**Strings.zh-CN.xaml 应包含（共 23 个）：**
- 17 个原有 UI 文本键
- 6 个新增运行时文本键

**Strings.en-US.xaml 应包含（共 23 个）：**
- 17 个 UI 文本键（值已替换为英文）
- 6 个运行时文本键（值为英文）

Expected: 两个文件的键集合完全一致（23=23）

- [ ] **Step 4: 更新版本修改记录**

在项目根目录的 `版本修改记录.txt` 中追加：

```
[2026-05-20] SeekDetailView 多语言修复
- 新增 ILocalizationService 依赖注入和 L() 便捷方法
- 修复 SeekDetailViewModel.cs 中 7 处硬编码中文字符串
- 补全 Strings.en-US.xaml 中 17 个未翻译条目（100%覆盖）
- 新增 6 个运行时文本资源键（中英文）
- 实现 SeekDetailView 100% 多语言覆盖
- 架构对齐 VisionDetailViewModel 模式
```

---

## 自我审查清单

### ✅ Spec 覆盖度检查

| Spec 要求 | 对应 Task | 状态 |
|----------|----------|------|
| 新增 ILocalizationService 依赖注入 | Task 1 (Step 3) | ✅ |
| 新增 L() 便捷方法 | Task 1 (Step 4) | ✅ |
| 修复默认描述硬编码（第114行） | Task 2 | ✅ |
| 修复导入对话框 3 处硬编码 | Task 3 | ✅ |
| 修复导出对话框 3 处硬编码 | Task 4 | ✅ |
| zh-CN 新增 6 个资源键 | Task 5 | ✅ |
| en-US 替换 17 个 UI 文本 | Task 6 | ✅ |
| en-US 新增 6 个运行时键 | Task 7 | ✅ |
| 编译验证 | Task 1, 4, 6, 7, 8 | ✅ |
| 硬编码残留检查 | Task 8 (Step 2) | ✅ |
| 资源键完整性对比 | Task 8 (Step 3) | ✅ |

### ✅ 占位符扫描

- ❌ 无 TBD / TODO
- ❌ 无 "添加适当的错误处理"
- ❌ 无 "类似 Task N" 引用
- ✅ 每个步骤包含完整代码（包括 exact file paths 和 line numbers）
- ✅ 所有替换操作都有明确的 before/after 代码块

### ✅ 类型一致性检查

- ✅ `L(string key)` 方法签名在 Task 1 定义，Task 2-4 使用一致
- ✅ 资源键命名规范统一（`SeekDetail_` 前缀）
- ✅ 格式化参数用法一致（`{0}` 占位符 + `string.Format()`）
- ✅ 构造函数参数顺序符合 PRISM 依赖注入规范

### ✅ 架构一致性检查

- ✅ 与 VisionDetailViewModel 的模式完全对齐：
  - 相同的字段命名：`_localizationService`
  - 相同的方法签名：`private string L(string key)`
  - 相同的调用方式：`L("ResourceKey")` 和 `string.Format(L("Key"), args)`
- ✅ 符合 WPF+PRISM 架构规范（构造函数注入）
- ✅ 符合项目现有的多语言机制（ILocalizationService + ResourceDictionary）

---

## 执行统计

| 指标 | 数量 |
|------|------|
| 总 Task 数 | 8 |
| 总 Step 数 | 30+ |
| 修改文件数 | 3 |
| 新增基础设施 | 1 套（字段 + 构造函数 + 方法）|
| 修复硬编码 | 7 处 |
| 新增资源键 | 6 个（中英文各一份）|
| 替换翻译 | 17 个（en-US）|
| 预计耗时 | 25-30 分钟 |
| 复杂度 | 中等（需注意 DI 注入兼容性）|

---

## ⚠️ 特别注意事项

### 1. 依赖注入兼容性

**风险：** 构造函数签名变更可能导致 Prism 容器无法解析 SeekDetailViewModel

**缓解措施：**
- ✅ PRISM 支持自动构造函数注入
- ✅ 只需确认 `ILocalizationService` 已在容器中注册（VisionDetailViewModel 已使用，应已注册）
- ✅ 如果注册名为其他名称，需确认解析名称一致

**回滚方案：** 如果 Task 1 编译失败，检查：
1. `Core.Services.LocalizationService` 是否实现了 `ILocalizationService`
2. 是否在 Prism Module 中注册了该服务
3. 注册代码示例：`containerRegistry.Register<ILocalizationService, LocalizationService>();`

### 2. 默认描述时机问题

**场景：** `L("SeekDetail_DefaultDesc")` 在 `InitializeFromStep()` 中调用时，如果 `ILocalizationService` 尚未完成初始化（如资源字典未加载），可能返回 `[SeekDetail_DefaultDesc]` 或空字符串

**缓解措施：**
- ✅ 通常在 UserControl 显示时资源字典已加载完毕
- ✅ 如果出现问题，可考虑延迟到首次访问时再设置默认描述
- ✅ 当前方案与 VisionDetailViewModel 一致，风险较低

### 3. 文件对话框 Filter 格式

**注意：** `SeekDetail_JsonFileFilter` 的值在不同语言下格式不同：
- 中文：`JSON 文件|*.json`
- 英文：`JSON Files|*.json`

Windows OpenFileDialog/SaveFileDialog 的 Filter 属性格式为 `"显示名|扩展名"`，此格式在中英文下都有效。
