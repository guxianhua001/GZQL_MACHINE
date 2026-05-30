# 点涂A 出胶参数组 增加起点开胶延时 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking。

**Goal:** 在 DotProcessParams 出胶参数组中新增"起点开胶延时"(PreDispenseDelay)参数，位于 DispenseTime 之后

**Architecture:** 在 DotProcessParams 模型类中新增 double 属性（遵循现有 SetProperty + Math.Clamp 模式），ParameterEditorViewModel 通过反射自动发现并注册到 UI 参数面板，多语言通过 Strings.{culture}.xaml 资源键 `Dispensing_Dot_PreDispenseDelay` 驱动

**Tech Stack:** WPF + Prism BindableBase + Newtonsoft.Json + XAML ResourceDictionary

---

### Task 1: DotProcessParams.cs 新增 PreDispenseDelay 属性

**Files:**
- Modify: `Core/Models/DotProcessParams.cs` (在 L56 DispenseTime 属性结束的 `}` 之后、L58 PostDelay 字段之前插入)

- [ ] **Step 1: 在出胶参数 region 中插入新属性**

在现有代码第 56 行 `}` (DispenseTime setter 结束) 之后、第 58 行 `private double _postDelay = 50.0;` 之前，插入：

```csharp
        private double _preDispenseDelay = 50.0;
        /// <summary>起点开胶延时 ms（范围 0~5000，到达起点后延迟开胶）</summary>
        public double PreDispenseDelay
        {
            get => _preDispenseDelay;
            set => SetProperty(ref _preDispenseDelay, Math.Clamp(value, 0.0, 5000.0));
        }
```

注意保持缩进一致（当前文件使用4空格缩进），新属性位于 `#region 出胶参数` 内部。

---

### Task 2: Strings.zh-CN.xaml 新增中文翻译键

**Files:**
- Modify: `MainApp/Languages/Strings.zh-CN.xaml`

- [ ] **Step 1: 在 Dispensing_Dot_DispenseTime 相关键之后插入**

在 `Dispensing_Dot_Tip_DispenseTime` 行（约 L646）之后、`Dispensing_Dot_PostDelay` 行（约 L647）之前，插入：

```xml
    <sys:String x:Key="Dispensing_Dot_PreDispenseDelay">起点开胶延时</sys:String>
    <sys:String x:Key="Dispensing_Dot_Tip_PreDispenseDelay">到达起点后延迟开胶时间，默认 50 ms</sys:String>
```

---

### Task 3: Strings.en-US.xaml 新增英文翻译键

**Files:**
- Modify: `MainApp/Languages/Strings.en-US.xaml`

- [ ] **Step 1: 在 Dispensing_Dot_DispenseTime 相关英文键之后插入**

找到 `Dispensing_Dot_Tip_DispenseTime` 和 `Dispensing_Dot_PostDelay` 之间，插入：

```xml
    <sys:String x:Key="Dispensing_Dot_PreDispenseDelay">Start Delay</sys:String>
    <sys:String x:Key="Dispensing_Dot_Tip_PreDispenseDelay">Delay before dispensing at start point, default 50 ms</sys:String>
```

---

### Task 4: 验证

- [ ] **Step 1: VS Code 诊断检查**

确认修改后的 3 个文件无诊断错误：
- Core/Models/DotProcessParams.cs
- MainApp/Languages/Strings.zh-CN.xaml
- MainApp/Languages/Strings.en-US.xaml

- [ ] **Step 2: 确认属性顺序**

验证 DotProcessParams.cs 中出胶参数组顺序为：DispenseTime → **PreDispenseDelay** → PostDelay → DotGlueTriggerOffsetMm

---

## 自检

- [x] **Spec 覆盖**: 模型属性 → Task 1; 中文资源 → Task 2; 英文资源 → Task 3; 验证 → Task 4
- [x] **占位符扫描**: 所有步骤包含完整代码内容
- [x] **类型一致性**: 属性名 PreDispenseDelay 在所有文件中一致；Key 命名遵循 Dispensing_Dot_{PropertyName} 模式
