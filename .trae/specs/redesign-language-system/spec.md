# 语言系统重新设计 Spec

## Why

当前语言系统使用 `DynamicResource` 绑定方式，在 XAML 设计器中所有文字均不可见，严重影响 UI 维护和调试效率。需要一种既能运行时切换语言、又不影响设计时可见性的新方案。

## 现状分析

### 当前架构
```
App.xaml → 空的 ResourceDictionary 占位
    ↓
LocalizationService.LoadResourcesToXaml() → 运行时从 .resx 读取 → 注入到 Application.Resources.MergedDictionaries
    ↓
XAML 中使用 {DynamicResource Key} → 运行时解析到字符串
```

### 核心问题
1. **设计时文字不可见**：`DynamicResource` 在 VS 设计器中无法解析（资源字典在运行时才注入），导致所有绑定语言的文字在设计器中显示为空白
2. **双重资源机制冲突**：同时存在 `.resx`（ResourceManager 方式）和 `Strings.{culture}.xaml`（ResourceDictionary 方式），但 `Strings.{culture}.xaml` 文件实际不存在（`MainApp/Languages/` 目录为空），导致 `UpdateResourceDictionaries()` 失败
3. **接口过度设计**：`ILocalizationService` 有 30+ 个方法、5 个事件、3 个 EventArgs 类、2 个子接口（`ILocalizationServiceFactory`、`ILocalizationResourceProvider`），但实际只用了 `GetResource()` 和 `SetLanguage()`
4. **ViewModel 绑定方式不统一**：部分用 `DynamicResource`，部分用 `LocalizedViewModelBase.L()` 方法，部分直接硬编码中文

### 受影响的 XAML 文件（使用 DynamicResource 做语言绑定）
- `ModuleCore/Views/LoginView.xaml` — 7 处
- `ModuleCore/Views/WindowClosedQuestion.xaml` — 5 处
- `ModuleCore/Views/MainWindow.xaml` — 1 处
- `Module/Operators/MotorControl/SpeedRatioView.xaml` — 1 处

## What Changes

- **BREAKING**：移除 `DynamicResource` 语言绑定方式，改用 `x:Static` + MarkupExtension 方案
- **BREAKING**：精简 `ILocalizationService` 接口，移除未使用的方法和事件
- 新增 `Lang` MarkupExtension，支持 XAML 中 `{lang:Lang Key}` 语法
- 新增默认语言资源字典（XAML ResourceDictionary），确保设计时文字可见
- 重构 `LocalizedViewModelBase`，简化为 `LocalizationBehavior` 附加属性方式
- 保留 `.resx` 作为翻译源文件，构建时自动生成 XAML 资源字典
- 新增 T4 模板或 Source Generator，从 `.resx` 自动生成强类型访问类

## Impact

- Affected specs: 语言系统全部功能
- Affected code:
  - `Core/Abstraction/ILocalizationService.cs` — 精简接口
  - `Core/Services/LocalizationService.cs` — 重写核心逻辑
  - `LanguageModule/` — 重构整个模块
  - `ModuleCore/Views/LoginView.xaml` — DynamicResource → Lang 标记扩展
  - `ModuleCore/Views/WindowClosedQuestion.xaml` — 同上
  - `ModuleCore/Views/MainWindow.xaml` — 同上
  - `Module/Operators/MotorControl/SpeedRatioView.xaml` — 同上
  - `MainApp/App.xaml` — 移除空占位 ResourceDictionary，添加默认语言字典
  - `MainApp/App.xaml.cs` — 简化启动逻辑

## ADDED Requirements

### Requirement: Lang MarkupExtension
系统 SHALL 提供 `Lang` MarkupExtension，允许在 XAML 中以 `{lang:Lang Key}` 方式绑定本地化字符串。

#### Scenario: 设计时显示默认文字
- **WHEN** 开发者在 VS 设计器中打开包含 `{lang:Lang Login_Login}` 的 XAML
- **THEN** 设计器 SHALL 显示该 Key 对应的默认语言（zh-CN）文字

#### Scenario: 运行时切换语言
- **WHEN** 用户在运行时切换语言为 English
- **THEN** 所有使用 `{lang:Lang Key}` 的 UI 元素 SHALL 自动更新为英文文字

#### Scenario: Key 不存在
- **WHEN** 使用了不存在的 Key 如 `{lang:Lang NonExistent}`
- **THEN** SHALL 显示 `[NonExistent]` 作为回退文字，不抛出异常

### Requirement: 默认语言资源字典
系统 SHALL 在 `MainApp/Languages/Strings.zh-CN.xaml` 中提供默认语言的完整 XAML ResourceDictionary，作为设计时和运行时的基础资源。

#### Scenario: 设计时资源可用
- **WHEN** VS 设计器加载 XAML
- **THEN** SHALL 能从默认语言资源字典中解析所有文字，设计器中可见

### Requirement: 翻译源文件管理
系统 SHALL 使用 `.resx` 文件作为翻译源（便于翻译工具协作），并通过构建步骤自动生成对应的 XAML ResourceDictionary。

#### Scenario: 添加新语言
- **WHEN** 开发者添加 `Resources.ja.resx`（日语）
- **THEN** 构建后 SHALL 自动生成 `Strings.ja-JP.xaml`

#### Scenario: 添加新翻译条目
- **WHEN** 开发者在 `Resources.zh.resx` 和 `Resources.en.resx` 中添加新 Key
- **THEN** 构建后 SHALL 自动更新对应的 XAML 资源字典

### Requirement: LocalizationBehavior 附加属性
系统 SHALL 提供 `LocalizationBehavior` 附加属性，替代 `LocalizedViewModelBase` 基类继承方式，使任何 ViewModel 无需继承特定基类即可支持语言切换刷新。

#### Scenario: ViewModel 绑定语言属性
- **WHEN** ViewModel 属性使用 `[Localized]` 特性标记
- **THEN** 语言切换时 SHALL 自动触发 `RaisePropertyChanged`

#### Scenario: 不继承基类
- **WHEN** ViewModel 不继承 `LocalizedViewModelBase`
- **THEN** 仍可通过 `ILocalizationService.GetResource()` 获取翻译文字

### Requirement: 语言切换即时生效
系统 SHALL 在语言切换时，使所有已打开的 UI 元素立即更新文字，无需重启应用或手动刷新页面。

#### Scenario: 切换语言后 UI 更新
- **WHEN** 用户从中文切换到英文
- **THEN** 当前所有可见的 `{lang:Lang}` 绑定和 ViewModel `[Localized]` 属性 SHALL 在 200ms 内更新

## MODIFIED Requirements

### Requirement: ILocalizationService 接口精简
精简 `ILocalizationService` 接口，只保留核心方法：

保留的方法：
- `CurrentLanguage` / `CurrentCultureCode` / `SupportedLanguages`
- `SetLanguage(string cultureCode)` — 切换语言
- `GetResource(string key)` — 获取翻译
- `GetResource(string key, params object[] args)` — 格式化翻译
- `TryGetResource(string key, out string value)` — 安全获取
- `LanguageChanged` 事件

移除的方法/事件：
- `LoadResourcesToXaml()` — 不再需要手动加载
- `AddResourceManager` / `RemoveResourceManager` — 内部实现细节
- `ResourcesLoaded` 事件 — 过度设计
- `SupportedLanguagesChanged` 事件 — 过度设计
- `ILocalizationServiceFactory` — 过度设计
- `ILocalizationResourceProvider` — 过度设计
- `LocalizationServiceOptions` — 过度设计

### Requirement: LocalizedViewModelBase 简化
`LocalizedViewModelBase` 不再作为必须继承的基类，改为可选。核心翻译逻辑通过 `ILocalizationService` 直接调用。

## REMOVED Requirements

### Requirement: DynamicResource 语言绑定
**Reason**: 导致 VS 设计器中文字不可见，严重影响开发效率
**Migration**: 所有 `{DynamicResource Key}` 替换为 `{lang:Lang Key}`

### Requirement: 运行时注入 ResourceDictionary
**Reason**: `LoadResourcesToXaml()` 方式与设计时可见性冲突
**Migration**: 改为 App.xaml 中静态引用默认语言字典 + Lang MarkupExtension 运行时切换

### Requirement: ILocalizationServiceFactory / ILocalizationResourceProvider
**Reason**: 过度设计，从未实际使用
**Migration**: 直接使用 `ILocalizationService`

## 技术方案

### Lang MarkupExtension 实现原理
```csharp
// 使用方式：{lang:Lang Login_Login}
// 设计时：从默认语言 ResourceDictionary 查找 Key，返回文字
// 运行时：订阅 ILocalizationService.LanguageChanged，自动更新 TargetProperty
public class LangExtension : MarkupExtension, INotifyPropertyChanged
{
    public string Key { get; }
    
    // ProvideValue 返回绑定到 this 的 Binding
    // 当语言切换时，触发 PropertyChanged → Binding 自动刷新
}
```

### 设计关键点
1. **LangExtension.ProvideValue()** 返回 `new Binding(nameof(Value)) { Source = this }`，而非直接返回字符串
2. 语言切换时，LangExtension 更新 `Value` 属性并触发 `PropertyChanged`
3. WPF Binding 机制自动将新值推送到 UI 元素
4. 设计时，LangExtension 从默认语言字典查找 Key 返回文字，设计器可见

### 资源文件结构
```
MainApp/
  Localization/
    Resources.resx          ← 默认（中文）翻译源
    Resources.zh.resx       ← 中文翻译源
    Resources.en.resx       ← 英文翻译源
    Resources.Designer.cs   ← 自动生成的强类型类
  Languages/
    Strings.zh-CN.xaml      ← 自动生成：中文资源字典（设计时 + 运行时）
    Strings.en-US.xaml      ← 自动生成：英文资源字典
```

### App.xaml 资源加载
```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <!-- 默认语言字典（确保设计时可见）-->
            <ResourceDictionary Source="/MainApp;component/Languages/Strings.zh-CN.xaml" />
            <!-- MaterialDesign 主题 -->
            ...
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```
