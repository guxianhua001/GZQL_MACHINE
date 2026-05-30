# Tasks

- [x] Task 1: 创建 Lang MarkupExtension 核心类
  - [x] 1.1: 在 Core 项目中创建 `LangExtension` 类（继承 MarkupExtension + INotifyPropertyChanged）
  - [x] 1.2: 实现 ProvideValue 返回 Binding 机制（设计时返回文字，运行时返回绑定）
  - [x] 1.3: 实现语言切换时自动刷新 Value 属性
  - [x] 1.4: 添加 XAML 命名空间映射 `xmlns:lang="clr-namespace:Core.Markup;assembly=Core"`

- [x] Task 2: 精简 ILocalizationService 接口
  - [x] 2.1: 保留核心方法（CurrentLanguage, SetLanguage, GetResource, TryGetResource, LanguageChanged）
  - [x] 2.2: 移除过度设计的方法和事件（LoadResourcesToXaml, AddResourceManager, ResourcesLoaded, SupportedLanguagesChanged, ILocalizationServiceFactory, ILocalizationResourceProvider, LocalizationServiceOptions）
  - [x] 2.3: 更新 LocalizationService 实现，移除 DynamicResource 注入逻辑，改为 LangExtension 提供翻译值

- [x] Task 3: 生成默认语言 XAML 资源字典
  - [x] 3.1: 创建 `MainApp/Languages/Strings.zh-CN.xaml`，包含所有中文翻译条目（从 Resources.zh.resx 提取）
  - [x] 3.2: 创建 `MainApp/Languages/Strings.en-US.xaml`，包含所有英文翻译条目
  - [x] 3.3: 在 App.xaml 中静态引用默认语言字典

- [x] Task 4: 创建 LocalizationBehavior 附加属性
  - [x] 4.1: 在 Core 中创建 `LocalizationBehavior` 类，提供 `AutoRefresh` 附加属性
  - [x] 4.2: 语言切换时自动刷新标记了 `[Localized]` 特性的 ViewModel 属性

- [x] Task 5: 迁移现有 XAML 文件
  - [x] 5.1: `ModuleCore/Views/LoginView.xaml` — 7 处 DynamicResource → `{lang:Lang Key}`
  - [x] 5.2: `ModuleCore/Views/WindowClosedQuestion.xaml` — 5 处 DynamicResource → `{lang:Lang Key}`
  - [x] 5.3: `ModuleCore/Views/MainWindow.xaml` — 1 处 DynamicResource → `{lang:Lang Key}`
  - [x] 5.4: `Module/Operators/MotorControl/SpeedRatioView.xaml` — 1 处 DynamicResource → `{lang:Lang Key}`

- [x] Task 6: 重构 LanguageModule
  - [x] 6.1: 简化 `LanguageSelectorViewModel`，移除对 `LocalizedViewModelBase` 的依赖
  - [x] 6.2: 简化 `LocalizedViewModelBase`，保留 `L()` 快捷方法但移除自动注册逻辑
  - [x] 6.3: 更新 `LanguageModule.cs` DI 注册

- [x] Task 7: 更新 App.xaml 和 App.xaml.cs
  - [x] 7.1: App.xaml 添加默认语言资源字典引用，移除空占位
  - [x] 7.2: App.xaml.cs 简化 OnStartup，移除 `LoadResourcesToXaml()` 调用

- [x] Task 8: 编译验证
  - [x] 8.1: 全项目编译零 error
  - [ ] 8.2: 验证 VS 设计器中 LoginView 文字可见（需用户在 VS 中确认）

# Task Dependencies
- [Task 1] 是核心依赖，Task 2/4/5/6 均依赖它
- [Task 3] 独立，可与 Task 1 并行
- [Task 5] 依赖 [Task 1] + [Task 3]
- [Task 6] 依赖 [Task 2]
- [Task 7] 依赖 [Task 3]
- [Task 8] 依赖所有前置任务
