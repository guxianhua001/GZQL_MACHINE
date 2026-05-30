# 扩展多语言支持至所有模块 Spec

## Why

当前多语言系统（LangExtension + XAML 资源字典）仅在 3 个 XAML 文件中启用，而 59 个 XAML 文件仍使用 `DynamicResource` 绑定语言 key。语言资源文件中存在大量未被任何代码引用的 key，需要清理。需要将 `lang:Lang` 标记扩展推广到所有模块，确保全应用统一的多语言支持，同时不影响原有功能。

## 现状分析

### 当前架构
```
Core/Markup/LangExtension.cs       — Lang 标记扩展，支持 {lang:Lang Key}
Core/Services/LocalizationService.cs — 本地化服务，切换语言时替换资源字典 + 调用 LangExtension.InvalidateAll()
MainApp/Languages/Strings.zh-CN.xaml — 中文资源字典（~400+ key）
MainApp/Languages/Strings.en-US.xaml — 英文资源字典（~400+ key）
```

### 核心问题
1. **DynamicResource 仍大量使用**：59 个 XAML 文件使用 `DynamicResource` 绑定语言 key，未迁移到 `lang:Lang`
2. **未使用的 key 未清理**：语言资源文件中约 400+ key，但很多 key 可能未被任何 XAML 或 C# 代码引用
3. **模块未统一支持多语言**：Module、AlarmModule、RecipeManagement、MotionControl、Framework、Interfaces 等模块的 XAML 文件仍使用 DynamicResource
4. **C# 代码中硬编码中文**：部分 ViewModel 和 Service 中仍有硬编码的中文文字

### 已使用 lang:Lang 的文件（3个）
- `Module/Views/OverView.xaml`
- `ModuleCore/Views/WindowClosedQuestion.xaml`
- `ModuleCore/Views/LoginView.xaml`

### 仍使用 DynamicResource 的文件（59个）
- `Module/Controls/Assembly/` — 8 个文件
- `Module/Controls/Dispense/` — 8 个文件
- `Module/Controls/Loading/` — 2 个文件
- `Module/Controls/StepDetails/` — 9 个文件
- `Module/Controls/StepEditor/` — 1 个文件
- `Module/Controls/Configuration/` — 3 个文件
- `Module/Controls/Grippers/` — 1 个文件
- `Module/Controls/Cad/` — 1 个文件
- `MotionControl/Views/` — 2 个文件
- `AlarmModule/Views/` — 4 个文件
- `RecipeManagement/Views/` — 1 个文件
- `TCPIPModule/Views/` — 1 个文件
- `Framework/Views/` — 6 个文件
- `ModuleCore/Views/` — 8 个文件
- `Interfaces/Views/` — 2 个文件
- `MainApp/App.xaml` — 1 个文件

### C# 代码中使用 GetResource/L() 的文件
- `Module/PrimModel.cs`
- `MotionControl/ViewModels/SingleAxisViewModel.cs`
- `Framework/ViewModels/TreeViewModel.cs`
- `LanguageModule/ViewModels/LocalizedViewModelBase.cs`
- `LanguageModule/ViewModels/LanguageSelectorViewModel.cs`

## What Changes

- 将 59 个 XAML 文件中的 `DynamicResource` 语言绑定迁移为 `lang:Lang Key` 标记扩展
- 扫描所有 XAML 和 C# 代码，识别语言资源文件中未被引用的 key 并删除
- 确保迁移后所有模块的多语言切换功能正常工作
- 保留 `DynamicResource` 对非语言资源（如主题、样式）的使用，仅迁移语言 key

## Impact

- Affected specs: 多语言系统全部功能
- Affected code:
  - 59 个 XAML 文件 — `DynamicResource` → `lang:Lang Key`
  - `MainApp/Languages/Strings.zh-CN.xaml` — 删除未使用 key
  - `MainApp/Languages/Strings.en-US.xaml` — 删除未使用 key
  - 可能涉及少量 C# ViewModel — 硬编码中文迁移为 GetResource()

## ADDED Requirements

### Requirement: 全模块多语言支持
系统 SHALL 将所有使用 `DynamicResource` 绑定语言 key 的 XAML 文件迁移为 `{lang:Lang Key}` 标记扩展方式。

#### Scenario: XAML 文件迁移
- **WHEN** XAML 文件中存在 `DynamicResource` 绑定语言 key（如 `{DynamicResource Login_UserLogin}`）
- **THEN** SHALL 替换为 `{lang:Lang Login_UserLogin}` 标记扩展

#### Scenario: 非语言 DynamicResource 保留
- **WHEN** XAML 文件中的 `DynamicResource` 绑定的是非语言资源（如主题色、样式等）
- **THEN** SHALL 保留 `DynamicResource` 不做迁移

#### Scenario: 迁移后功能不受影响
- **WHEN** 完成所有 XAML 文件的迁移
- **THEN** 所有 UI 文字显示 SHALL 与迁移前完全一致
- **THEN** 语言切换功能 SHALL 正常工作

### Requirement: 未使用 key 清理
系统 SHALL 从语言资源文件中删除未被任何 XAML 或 C# 代码引用的 key。

#### Scenario: 识别未使用 key
- **WHEN** 语言资源文件中的 key 未在任何 XAML 文件（通过 `lang:Lang` 或 `DynamicResource`）或 C# 文件（通过 `GetResource()` 或 `L()`）中被引用
- **THEN** 该 key SHALL 被标记为未使用

#### Scenario: 删除未使用 key
- **WHEN** 确认 key 未被使用
- **THEN** SHALL 从 `Strings.zh-CN.xaml` 和 `Strings.en-US.xaml` 中同时删除该 key

#### Scenario: 保留安全 key
- **WHEN** key 在 C# 代码中通过字符串拼接或反射方式引用（难以静态分析）
- **THEN** SHALL 保留该 key，不删除

### Requirement: C# 硬编码中文迁移
系统 SHALL 将 ViewModel 和 Service 中硬编码的中文文字迁移为 `ILocalizationService.GetResource()` 调用。

#### Scenario: 硬编码中文识别
- **WHEN** C# 代码中存在直接赋值的中文字符串
- **THEN** SHALL 迁移为 `GetResource("Key")` 调用，并在语言资源文件中添加对应 key

## MODIFIED Requirements

### Requirement: DynamicResource 语言绑定迁移
所有 XAML 文件中的 `DynamicResource` 语言 key 绑定 SHALL 替换为 `{lang:Lang Key}` 标记扩展。此操作与 `redesign-language-system` spec 中 Task 5 的要求一致，但范围扩展到所有 59 个文件。

## REMOVED Requirements

### Requirement: DynamicResource 语言绑定方式
**Reason**: 导致 VS 设计器中文字不可见，且与 LangExtension 方式不统一
**Migration**: 所有 `{DynamicResource LanguageKey}` 替换为 `{lang:Lang LanguageKey}`

## 技术方案

### 迁移策略
1. **分模块迁移**：按模块分组（Assembly → Dispense → Loading → StepDetails → Configuration → Alarm → Recipe → Motion → Framework → Interfaces → ModuleCore），降低风险
2. **每模块迁移后验证**：每个模块迁移完成后，编译确认无错误
3. **语言 key 识别规则**：`DynamicResource` 绑定的 key 若在 `Strings.zh-CN.xaml` 中存在，则认为是语言 key，需要迁移

### 未使用 key 扫描方法
1. 从 `Strings.zh-CN.xaml` 提取所有 key 列表
2. 在所有 `.xaml` 文件中搜索 `lang:Lang Key` 和 `DynamicResource Key` 引用
3. 在所有 `.cs` 文件中搜索 `GetResource("Key")` 和 `L("Key")` 引用
4. 比对差集，识别未使用的 key
5. 对差集中的 key 进行人工确认（排除字符串拼接/反射引用）
6. 确认后从两个语言文件中删除

### 安全保障
- 迁移前备份语言资源文件
- 每个模块迁移后立即编译验证
- 最终全量编译验证
- 语言切换功能测试
