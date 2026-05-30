# DataDashboardView 多语言修复实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended)
> **Goal:** 为 DataDashboardView 新增多语言支持基础设施，消除 15 处硬编码中文字符串（3处UI + 12处日志），补全 18 个英文翻译，实现 100% 多语言覆盖
> **Architecture:** 参照 SeekDetailViewModel 成功模式，新增 ILocalizationService 依赖注入和 L() 便捷方法
> **Tech Stack:** WPF, PRISM, ResourceDictionary, ILocalizationService

---

## 文件结构

```
修改:
├── Module/Controls/StepDetails/DataDashboardViewModel.cs   # +基础设施 + 15处修复
├── MainApp/Languages/Strings.zh-CN.xaml                    # +7 键
└── MainApp/Languages/Strings.en-US.xaml                    # +7 键 + 18 替换
```

---

### Task 1: 新增多语言基础设施

**Files:** Modify: `Module/Controls/StepDetails/DataDashboardViewModel.cs`

- [ ] **Step 1: 新增 using 引用**（在 `using Core.Abstraction;` 之后或 using 区域）

- [ ] **Step 2: 新增字段** `private readonly ILocalizationService _localizationService;`

- [ ] **Step 3: 修改构造函数** — 新增第三个参数 `ILocalizationService localizationService` 并赋值

- [ ] **Step 4: 新增 L() 方法**
```csharp
private string L(string key) => _localizationService.GetResource(key);
```

- [ ] **Step 5: 编译验证**

---

### Task 2: 修复 UI 硬编码（3 处）

| # | 行号 | 当前 | 资源键 |
|---|------|------|--------|
| 1 | ~283 | `"选择示意图"` | DataDetail_SelectDiagramTitle |
| 2 | ~284 | `"图片文件\|*..."` | DataDetail_ImageFileFilter |
| 3 | ~327 | `$"变量{maxSeq + 1}"` | DataDetail_DefaultFieldName |

---

### Task 3: 修复日志硬编码（12 处）

所有 `_logger.Info/Warn/Error` 中的中文替换为 `L("DataDetail_Log_xxx")` 格式

涉及行号: 188, 203, 215, 219, 249, 254, 260, 316, 362, 437, 462, 484

---

### Task 4: zh-CN 新增资源键（~7 个）

在 Strings.zh-CN.xaml 的 DataDetail 区域末尾添加：

```xml
<sys:String x:Key="DataDetail_SelectDiagramTitle">选择示意图</sys:String>
<sys:String x:Key="DataDetail_ImageFileFilter">图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|所有文件|*.*</sys:String>
<sys:String x:Key="DataDetail_DefaultFieldName">变量{0}</sys:String>
<sys:String x:Key="DataDetail_Log_DataLoaded">看板数据已加载, 字段数={0}, 人工确认={1}</sys:String>
<sys:String x:Key="DataDetail_Log_PoolIdEmpty">当前配方池ID为空，无法加载全局变量</sys:String>
<sys:String x:Key="DataDetail_Log_DiagramSuccess">示意图加载成功: {0}</sys:String>
<sys:String x:Key="DataDetail_Log_DiagramNotExist">示意图文件不存在: {0}</sys:String>
<!-- ... 其他日志键 -->
```

---

### Task 5: en-US 补全翻译 + 新增键

1. **替换 18 个现有条目为英文**
2. **新增 ~7 个运行时文本键（英文值）**

---

### Task 6: 最终验证

- 编译通过
- 零硬编码残留
- 资源键完整性 (zh-CN = en-US)

---

## 执行统计

| 指标 | 数量 |
|------|------|
| 总 Task | 6 |
| 修复硬编码 | 15 |
| 新增资源键 | ~7 |
| 替换翻译 | 18 |
