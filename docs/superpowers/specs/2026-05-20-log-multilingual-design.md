# 日志多语言支持系统 - 设计文档

## 📋 项目概述

为 GZQL_MACHINE 工业自动化软件实现**日志消息的多语言支持系统**，基于 **Roslyn Source Generator** 自动生成强类型日志 API，解决长期维护中大量硬编码中文字符串的问题。

### 核心目标
- ✅ 编译期类型安全：Key 拼写错误在编译时发现（非运行时 `[Missing_Key]`）
- ✅ IDE 完整支持：智能补全、重命名重构、Find All References
- ✅ 性能零开销：编译后就是普通方法调用，无反射/字典查找
- ✅ 长期可维护：代码即文档，适合日志量 >500 条的工业软件
- ✅ 复用现有架构：集成 LocalizationService + LoggerService

### 设计决策记录

| 决策点 | 选择 | 理由 |
|--------|------|------|
| **实现范围** | 仅正式日志 (Info/Warn/Error/Fatal) | Debug.WriteLine 保持硬编码用于开发调试 |
| **实现方式** | Roslyn Source Generator（强类型） | 编译期安全 + IDE 支持 + 长期维护性 |
| **技术选型** | Attribute 声明式（方案 B） | 定义处即文档，Source Generator 复杂度低 |
| **依赖注入** | ServiceLocator 模式 | 静态类获取 ILocalizationService 的最佳实践 |

---

## 🏗️ 架构设计

### 整体流程图

```
┌─────────────────────────────────────────────────────────────┐
│                     开发时 (Design Time)                      │
│                                                             │
│  ┌──────────────────────┐    ┌───────────────────────────┐   │
│  │ LogMessages.cs        │    │ Roslyn Source Generator    │   │
│  │ (开发者手动编写)       │    │ (自动扫描 + 代码生成)      │   │
│  │                      │    │                           │   │
│  │ [LogMessage(          │    │ 扫描:                    │   │
│  │   "zh-CN", "轴{0}..")]│    │ 1. partial class         │   │
│  │ public static partial │    │ 2. [LogMessage] Attribute │   │
│  │   string AxisMoved()  │───▶│ 3. 提取 Culture+Message  │   │
│  │                      │    │ 4. 生成 .g.cs 实现文件     │   │
│  └──────────────────────┘    └───────────┬───────────────┘   │
│                                      │                   │
└──────────────────────────────────────┼───────────────────┘
                                       │ 编译 (Build)
                                       ▼
┌─────────────────────────────────────────────────────────────┐
│                     运行时 (Runtime)                         │
│                                                             │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ 业务代码 (MotionService / VisionService / ...)        │  │
│  │                                                       │  │
│  │ _logger.Info(LogMessages.AxisMoved("X", 10.5, 20.3));│  │
│  │ _logger.Error(LogMessages.DiReadFailed(5, err));     │  │
│  │                                                       │  │
│  │ ✅ IDE 智能提示: 输入 LogMessages. 后列出方法           │  │
│  │ ✅ 编译期检查: 参数类型/数量必须匹配                    │  │
│  │ ✅ F12 跳转: 直接查看中英文定义                        │  │
│  └───────────────────────┬───────────────────────────────┘  │
│                          │                                  │
│                          ▼                                  │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ LogMessages.g.cs (自动生成)                          │  │
│  │                                                       │  │
│  │ static string AxisMoved(axis, x, y) {                 │  │
│  │   var culture = CurrentCulture;                       │  │
│  │   return culture switch {                             │  │
│  │     "zh-CN" => $"轴 {axis} 已移动到目标位置...",       │  │
│  │     "en-US"   => $"Axis {axis} moved to target..."     │  │
│  │   };                                                  │  │
│  }                                                      │  │
│  └───────────────────────┬───────────────────────────────┘  │
│                          │                                  │
│                          ▼                                  │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ LoggerService → NLog → 文件 / 控制台 / LogViewer     │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

---

## 📦 组件清单

### 新建文件（4个）

| 文件路径 | 说明 | 类型 |
|---------|------|------|
| `Core/Attributes/LogMessageAttribute.cs` | 自定义 Attribute | 手动编写 |
| `Core/Logging/LogMessages.cs` | 日志消息定义（partial class） | 手动编写 |
| `LogGenerator/LogMessagesGenerator.cs` | Roslyn Source Generator | 自动执行 |
| `LogGenerator/LogGenerator.csproj` | Generator 项目文件 | 配置文件 |

### 修改文件（2个）

| 文件路径 | 修改内容 |
|---------|---------|
| `GZQL_MACHINE.csproj` | 引用 Generator 项目，配置 Source Generator |
| `MainApp/Languages/Strings.zh-CN.xaml` | （可选）添加日志相关的翻译资源 |

---

## 🔧 详细组件设计

### 1️⃣ LogMessageAttribute（自定义属性）

**文件位置**: `Core/Attributes/LogMessageAttribute.cs`

```csharp
using System;

namespace Core.Attributes
{
    /// <summary>
    /// 日志消息多语言标记属性
    /// 用于标记 LogMessages 类中的 partial 方法，
    /// 供 Roslyn Source Generator 扫描并生成实现代码
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Method, 
        AllowMultiple = true,  // 同一方法可标记多种语言
        Inherited = false       // 不继承到子类
    )]
    public class LogMessageAttribute : Attribute
    {
        /// <summary>语言文化代码（如 zh-CN, en-US）</summary>
        public string CultureCode { get; }

        /// <summary>该语言下的消息模板（支持 {0}, {1} 占位符）</summary>
        public string Message { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="cultureCode">语言代码</param>
        /// <param name="message">消息模板</param>
        public LogMessageAttribute(string cultureCode, string message)
        {
            CultureCode = cultureCode ?? throw new ArgumentNullException(nameof(cultureCode));
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }
    }
}
```

**使用示例**:
```csharp
[LogMessage("zh-CN", "轴 {0} 已移动到目标位置")]
[LogMessage("en-US", "Axis {0} moved to target position")]
public static partial string AxisMoved(object arg0);
```

---

### 2️⃣ LogMessages（日志消息定义类）

**文件位置**: `Core/Logging/LogMessages.cs`

#### 设计原则
1. **按模块分组**：运动控制 / IO 控制 / 流程管理 / 视觉检测 / 报警系统
2. **命名规范**：`PascalCase`，语义清晰（动词+名词）
3. **参数顺序**：重要参数在前，可选参数在后
4. **必须覆盖的语言**：每个方法至少有 `zh-CN` 和 `en-US` 两个 Attribute

#### 代码骨架（初始版本，后续可扩展）

```csharp
using Core.Attributes;
using System;

namespace Core.Logging
{
    /// <summary>
    /// 日志消息常量定义（多语言支持）
    /// 
    /// <para>使用方式：</para>
    /// <code>
    /// // 在业务代码中调用（编译期类型安全）
    /// _logger.Info(LogMessages.AxisMoved("X"));
    /// _logger.Error(LogMessages.DiReadFailed(5, ex.Message));
    /// </code>
    /// 
    /// <para>扩展新日志：</para>
    /// <list type="bullet">
    ///   <item>在本类中添加新的 partial 方法声明</item>
    ///   <item>添加 [LogMessage] Attribute 标记各语言翻译</item>
    ///   <item>编译后 Roslyn Generator 自动生成实现</item>
    /// </list>
    /// </summary>
    public static partial class LogMessages
    {
        #region 初始化与依赖注入

        private static ILocalizationService _localization;
        
        /// <summary>
        /// 初始化 LocalizationService（需在 App 启动时调用一次）
        /// </summary>
        public static void Initialize(ILocalizationService localization)
        {
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        }

        /// <summary>
        /// 确保 LocalizationService 已初始化（内部使用）
        /// </summary>
        private static void EnsureInitialized()
        {
            if (_localization == null)
            {
                throw new InvalidOperationException(
                    "LogMessages 未初始化！请在 App.OnStartup 中调用 LogMessages.Initialize(service)");
            }
        }

        #endregion

        #region 运动控制相关 (Motion Control)

        /// <summary>
        /// 轴移动到目标位置
        /// </summary>
        [LogMessage("zh-CN", "轴 {0} 已移动到目标位置 ({1:F3}, {2:F3})")]
        [LogMessage("en-US", "Axis {0} moved to target position ({1:F3}, {2:F3})")]
        public static partial string AxisMovedToTarget(string axisName, double targetX, double targetY);

        /// <summary>
        /// 轴回零完成
        /// </summary>
        [LogMessage("zh-CN", "轴 {0} 回零完成")]
        [LogMessage("en-US", "Axis {0} homing completed")]
        public static partial string AxisHomingDone(string axisName);

        /// <summary>
        /// 急停触发
        /// </summary>
        [LogMessage("zh-CN", "⚠️ 收到急停信号，正在停止轴 {0}")]
        [LogMessage("en-US", "⚠️ Emergency stop received, stopping axis {0}")]
        public static partial string EmergencyStopTriggered(string axisName);

        #endregion

        #region IO 控制相关 (I/O Control)

        /// <summary>
        /// DI 状态变更
        /// </summary>
        [LogMessage("zh-CN", "DI [{0}] 状态: {1}")]
        [LogMessage("en-US", "DI [{0}] status: {1}")]
        public static partial string DiStatusChanged(int logicalId, bool isActive);

        /// <summary>
        /// DO 切换成功
        /// </summary>
        [LogMessage("zh-CN", "DO [{0}] 已切换为 {1}")]
        [LogMessage("en-US", "DO [{0}] toggled to {1}")]
        public static partial string DoToggled(int logicalId, bool newState);

        /// <summary>
        /// DO 切换失败
        /// </summary>
        [LogMessage("zh-CN", "❌ DO [{0}] 切换失败: {1}")]
        [LogMessage("en-US", "❌ DO [{0}] toggle failed: {1}")]
        public static partial string DoToggleFailed(int logicalId, string error);

        #endregion

        #region 流程控制相关 (Process Control)

        /// <summary>
        /// 流程启动
        /// </summary>
        [LogMessage("zh-CN", "▶ 流程开始: {0}")]
        [LogMessage("en-US", "▶ Process started: {0}")]
        public static partial string ProcessStarted(string processName);

        /// <summary>
        /// 步骤完成
        /// </summary>
        [LogMessage("zh-CN", "  ✓ 步骤 [{0}] {1} 完成")]
        [LogMessage("en-US", "  ✓ Step [{0}] {1} completed")]
        public static partial string StepCompleted(int stepIndex, string stepName);

        /// <summary>
        /// 流程完成
        /// </summary>
        [LogMessage("zh-CN", "✅ 流程 {0} 执行完毕，耗时 {1}")]
        [LogMessage("en-US", "✅ Process {0} completed, duration {1}")]
        public static partial string ProcessCompleted(string processName, TimeSpan duration);

        #endregion

        #region 视觉检测相关 (Vision)

        /// <summary>
        /// 相机拍照触发
        /// </summary>
        [LogMessage("zh-CN", "📷 触发相机 {0} 拍照")]
        [LogMessage("en-US", "📷 Trigger camera {0} capture")]
        public static partial string CameraTriggered(string cameraId);

        /// <summary>
        /// 视觉数据接收
        /// </summary>
        [LogMessage("zh-CN", "📥 收到相机 {0} 数据: {1} 个测量值")]
        [LogMessage("en-US", "📥 Received camera {0} data: {1} measurements")]
        public static partial string VisionDataReceived(string cameraId, int measurementCount);

        #endregion

        #region 报警系统相关 (Alarm)

        /// <summary>
        /// 报警触发
        /// </summary>
        [LogMessage("zh-CN", "🚨 报警触发: [{0}] {1}")]
        [LogMessage("en-US", "🚨 Alarm triggered: [{0}] {1}")]
        public static partial string AlarmTriggered(string alarmCode, string message);

        /// <summary>
        /// 报警确认
        /// </summary>
        [LogMessage("zh-CN", "✅ 报警已确认: {0}")]
        [LogMessage("en-US", "✅ Alarm acknowledged: {0}")]
        public static partial string AlarmAcknowledged(string alarmId);

        #endregion
    }
}
```

---

### 3️⃣ Roslyn Source Generator（核心）

**项目位置**: `LogGenerator/`

#### 3.1 项目文件 (.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>preview</LangVersion>
    <IsRoslynComponent>true</IsRosleynComponent>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="4.11.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.11.0" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

#### 3.2 Generator 实现（伪代码）

```csharp
[Generator]
public class LogMessagesGenerator : ISourceGenerator
{
    public void Execute(GeneratorExecutionContext context)
    {
        // 1. 查找所有包含 [LogMessage] Attribute 的 partial class
        var logMessagesClass = FindLogMessagesClass(context.Compilation);
        
        if (logMessagesClass == null) return; // 未找到则跳过
        
        // 2. 遍历所有带有 [LogMessage] 的方法
        var methods = GetAnnotatedMethods(logMessagesClass);
        
        foreach (var method in methods)
        {
            // 3. 提取方法的参数列表
            var parameters = method.Parameters;
            
            // 4. 提取所有 [LogMessage] Attribute 的 Culture+Message
            var translations = GetTranslations(method);
            
            // 5. 生成方法实现代码
            var implementation = GenerateMethodImplementation(
                methodName: method.Name,
                parameters: parameters,
                translations: translations
            );
            
            // 6. 添加到输出
            context.AddSource("LogMessages.g.cs", implementation);
        }
    }
    
    private string GenerateMethodImplementation(
        string methodName, 
        ImmutableArray<IParameterSymbol> parameters,
        Dictionary<string, string> translations)
    {
        // 生成 switch/case 结构的代码
        // 包含所有语言的翻译字符串
    }
}
```

#### 3.3 生成的代码示例（LogMessages.g.cs）

```csharp
// ⚠️ 此文件由 Roslyn Source Generator 自动生成，请勿手动修改！
// 修改 LogMessages.cs 后重新编译即可更新此文件

namespace Core.Logging
{
    public static partial class LogMessages
    {
        /// <inheritdoc/>
        public static string AxisMovedToTarget(string axisName, double targetX, double targetY)
        {
            EnsureInitialized();
            
            var culture = CultureInfo.CurrentUICulture.Name;
            
            // 优先使用 LocalizationService 动态获取（支持运行时切换）
            // 如果失败则回退到硬编码翻译
            try
            {
                var localized = _localization.GetResource($"Log_{methodName}");
                if (!localized.StartsWith("["))
                {
                    return string.Format(localized, axisName, targetX, targetY);
                }
            }
            catch { }
            
            // 回退到硬编码翻译（保证即使 LocalizationService 不可用也能工作）
            return culture switch
            {
                "zh-CN" => $"轴 {axisName} 已移动到目标位置 ({targetX:F3}, {targetY:F3})",
                "en-US"   => $"Axis {axisName} moved to target position ({targetX:F3}, {targetY:F3})",
                _         => $"Axis {axisName} moved to target position ({targetX:F3}, {targetY:F3})"
            };
        }
        
        // ... 其他方法的实现 ...
    }
}
```

---

### 4️⃣ 主项目集成配置

**修改 `GZQL_MACHINE.csproj`:

```xml
<ItemGroup>
  <!-- 引用 Source Generator 项目 -->
  <ProjectReference Include="..\LogGenerator\LogGenerator.csproj" 
                    OutputItemType="Analyzer" 
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

---

## 🚀 使用指南

### Step 1: 初始化（App 启动时）

```csharp
// App.xaml.cs 或 Program.cs
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    
    // 初始化日志多语言服务
    var localizationService = Container.Resolve<ILocalizationService>();
    LogMessages.Initialize(localizationService);
}
```

### Step 2: 在业务代码中使用

```csharp
// MotionService.cs
public async Task MoveAxisAsync(string axisName, double x, double y)
{
    try
    {
        await MoveToAsync(axisName, x, y);
        
        // ✅ IDE 智能提示：输入 LogMessages. 后列出所有可用方法
        // ✅ 编译期检查：参数类型、数量必须匹配
        _logger.Info(LogMessages.AxisMovedToTarget(axisName, x, y));
    }
    catch (Exception ex)
    {
        // ✅ F12 可直接跳转到定义查看中英文
        _logger.Error(ex, LogMessages.DoToggleFailed(GetAxisId(axisName), ex.Message));
    }
}

// VisionCaptureService.cs
public async Task CaptureAsync(string cameraId)
{
    _logger.Info(LogMessages.CameraTriggered(cameraId));
    
    var data = await TriggerCameraAsync(cameraId);
    
    _logger.Info(LogMessages.VisionDataReceived(cameraId, data.Measurements.Count));
}
```

### Step 3: 添加新日志消息

1. 打开 `Core/Logging/LogMessages.cs`
2. 在合适的 `#region` 下添加新方法：

```csharp
/// <summary>
/// 自定义操作完成
/// </summary>
[LogMessage("zh-CN", "自定义操作 {0} 完成")]
[LogMessage("en-US", "Custom operation {0} completed")]
public static partial string CustomOperationDone(string operationName);
```

3. **重新编译** → Generator 自动生成实现代码
4. 即可在代码中使用 `LogMessages.CustomOperationDone("Calibration")`

---

## 📊 与现有系统的集成关系

### 与 LocalizationService 的协作

```
┌──────────────────────────────────────────┐
│          LocalizationService              │
│  - 管理 UI 多语言资源字典               │
│  - 提供 GetResource(key) 方法          │
│  - 语言切换事件                        │
└──────────────────┬───────────────────────┘
                   │ Initialize()
                   ▼
┌──────────────────────────────────────────┐
│          LogMessages (静态类)             │
│  - 缓存 LocalizationService 引用        │
│  - 优先从动态资源获取翻译              │
│  - 回退到硬编码翻译（容错）            │
└──────────────────┬───────────────────────┘
                   │ 调用
                   ▼
┌──────────────────────────────────────────┐
│          LoggerService                  │
│  - Info(LogMessages.XXX())             │
│  - Error(ex, LogMessages.YYY())        │
│  - 写入 NLog / GlobalLogCache          │
└──────────────────────────────────────────┘
```

### 与 LoggerService 的兼容性

**无需修改 LoggerService 接口或实现！**

```csharp
// 原有调用方式（仍然有效）
_logger.Info("硬编码中文消息");  // ✅ 继续支持

// 新推荐方式（强类型）
_logger.Info(LogMessages.AxisMoved("X"));  // ✅ 推荐
```

**迁移策略**：
- **渐进式迁移**：新旧方式可共存
- **无破坏性变更**：不强制要求立即替换所有硬编码日志
- **Lint 规则（可选）**：可配置规则警告硬编码中文字符串

---

## ✅ 验收标准

### 功能完整性
- [ ] Roslyn Generator 成功解析 [LogMessage] Attribute
- [ ] 自动生成的 .g.cs 文件包含所有语言的 switch/case
- [ ] 编译期检查：参数数量/类型不匹配时报错
- [ ] IDE 支持：智能补全、F12 跳转、Rename 重命名

### 多语言正确性
- [ ] 中文环境显示中文日志
- [ ] 英文环境显示英文日志
- [ ] 参数化消息正确替换 {0}, {1} 占位符
- [ ] 切换语言后新生效的日志使用新语言

### 性能与稳定性
- [ ] 运行时性能损耗 < 1%（相比直接字符串拼接）
- [ ] 不影响 LoggerService 的原有功能
- [ ] Generator 编译时间 < 500ms（增量编译）

### 开发体验
- [ ] Visual Studio 2022 16.11+ 正确识别 Generator
- [ ] Rider 2021.3+ 正确识别 Generator
- [ ] 修改 LogMessages.cs 后重新编译即可更新 .g.cs
- [ ] 错误提示清晰（Attribute 格式错误时）

---

## 📈 扩展方向（未来迭代）

### Phase 2: 高级特性（可选）
1. **日志级别 Attribute**: `[LogLevel(Level = LogLevel.Warn)]` 控制默认级别
2. **结构化日志**: 自动序列化为 JSON（集成 Serilog）
3. **日志采样**: 高频日志自动降级（如每 100 次只记录 1 次）
4. **上下文增强**: 自动追加当前用户、工站号、批次信息

### Phase 3: 工具链完善
1. **VS Code 插件**: 快速创建日志消息的代码片段
2. **导出工具**: 从 LogMessages.cs 导出 Excel 翻译表给翻译人员
3. **统计报表**: 分析日志覆盖率（哪些代码路径缺少日志）

---

## 🎯 实施计划摘要

| 阶段 | 任务 | 复杂度 | 预计时间 |
|------|------|--------|---------|
| **Phase 1** | 创建 LogMessageAttribute | 低 | 15 分钟 |
| **Phase 1** | 创建 LogMessages.cs（含 20+ 示例方法） | 低 | 30 分钟 |
| **Phase 1** | 实现 Roslyn Source Generator | **高** | 2-3 小时 |
| **Phase 1** | 配置主项目引用 Generator | 低 | 10 分钟 |
| **Phase 1** | 集成测试（验证端到端流程） | 中 | 30 分钟 |
| **Phase 1** | 编写使用文档和迁移指南 | 低 | 20 分钟 |
| **总计** | | | **~4 小时** |

---

## 💡 风险与缓解措施

| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| **Roslyn Generator 调试困难** | 开发效率低 | 先用 T4 做原型验证逻辑，再迁移到 Roslyn |
| **IDE 兼容性问题** | 部分团队成员无法使用 | 提供 VS 2022 最低版本要求和安装指南 |
| **静态类依赖注入不纯粹** | 违反 DI 原则 | 这是工业界公认的最佳实践（如 ASP.NET Core Options） |
| **大量历史日志未迁移** | 代码库不一致 | 配置 Code Analysis 规则逐步迁移，不强求一次性替换 |

---

**设计版本**: v1.0  
**创建日期**: 2026-05-20  
**预计实施时间**: ~4 小时  
**技术栈**: C# 10 + Roslyn Source Generators + WPF PRISM
