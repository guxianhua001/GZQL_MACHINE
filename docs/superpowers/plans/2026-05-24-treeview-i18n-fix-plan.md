# TreeView 多语言硬编码修复实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking。

**Goal:** 修复 tree-structure.json 中装配单元下3个节点的英文硬编码问题，清理冗余 DisplayName 字段和死代码，确保 TreeView 完全通过 ILocalizationService 实现多语言切换

**Architecture:** JSON 配置文件仅保留 Name（中文默认值）+ LocalizationKey（资源键），运行时由 TreeViewModel 通过 ILocalizationService.GetResourceOrDefault 统一解析显示文本。移除从未使用的 DisplayName 字段和两处死代码方法。

**Tech Stack:** WPF + Prism + Newtonsoft.Json + ILocalizationService（XAML ResourceDictionary 驱动）

---

### Task 1: 修复 tree-structure.json — 移除 DisplayName + 修复3个硬编码节点

**Files:**
- Modify: `MainApp/bin/Debug/net9.0-windows7.0/Config/tree-structure.json`
- 注意：同时检查源目录 `Config/tree-structure.json` 是否存在，若存在需同步修改

- [ ] **Step 1: 修改 tree-structure.json**

对整个文件执行以下变更：

**全局变更：删除所有节点的 `"DisplayName"` 字段**（每个节点都有一行 DisplayName，全部删除）

**修复装配单元下3个叶子节点（第91-122行区域）：**

节点1 — Work Order → 工单配置：
```json
// Before:
{
  "Name": "Work Order",
  "LocalizationKey": "Tree_Equipment_TestStation_Test",
  "DisplayName": "Work Order",
  "Path": "Equipment/TestStation/WorkOrderConfigView",
  ...
}
// After:
{
  "Name": "工单配置",
  "LocalizationKey": "Tree_Equipment_TestStation_WorkOrder",
  "Path": "Equipment/TestStation/WorkOrderConfigView",
  ...
}
```

节点2 — Cad Alignment → CAD对位：
```json
// Before:
{
  "Name": "Cad Alignment",
  "LocalizationKey": "Tree_Equipment_TestStation_Test",
  "DisplayName": "Cad Alignment",
  "Path": "Equipment/TestStation/CadAlignmentView",
  ...
}
// After:
{
  "Name": "CAD对位",
  "LocalizationKey": "Tree_Equipment_TestStation_CadAlignment",
  "Path": "Equipment/TestStation/CadAlignmentView",
  ...
}
```

节点3 — Process Sequence → 工艺序列：
```json
// Before:
{
  "Name": "Process Sequence",
  "LocalizationKey": "Tree_Equipment_TestStation_Test",
  "DisplayName": "Process Sequence",
  "Path": "Equipment/TestStation/ProcessSequenceEditorView",
  ...
}
// After:
{
  "Name": "工艺序列",
  "LocalizationKey": "Tree_Equipment_TestStation_ProcessSequence",
  "Path": "Equipment/TestStation/ProcessSequenceEditorView",
  ...
}
```

其余所有节点仅删除 `"DisplayName"` 行，保持 Name 和 LocalizationKey 不变。

- [ ] **Step 2: 检查源 Config 目录是否有同名文件需同步**

确认 `c:\WorkFiles\GZQL_MACHINE\Config\tree-structure.json` 是否存在。如果存在，执行与 Step 1 相同的修改以保持同步。

---

### Task 2: 补充中文语言资源 — Strings.zh-CN.xaml

**Files:**
- Modify: `MainApp/Languages/Strings.zh-CN.xaml`

- [ ] **Step 1: 在 Tree_ 键区域末尾新增3个键**

在现有最后一个 Tree_ 键（约 L1505 `Tree_Equipment_Maintenance`）之后添加：

```xml
<sys:String x:Key="Tree_Equipment_TestStation_WorkOrder">工单配置</sys:String>
<sys:String x:Key="Tree_Equipment_TestStation_CadAlignment">CAD对位</sys:String>
<sys:String x:Key="Tree_Equipment_TestStation_ProcessSequence">工艺序列</sys:String>
```

---

### Task 3: 补充英文语言资源 — Strings.en-US.xaml

**Files:**
- Modify: `MainApp/Languages/Strings.en-US.xaml`

- [ ] **Step 1: 在 Tree_ 键区域末尾新增3个键**

在现有最后一个 Tree_ 键（约 L1478 `Tree_Equipment_Maintenance`）之后添加：

```xml
<sys:String x:Key="Tree_Equipment_TestStation_WorkOrder">Work Order</sys:String>
<sys:String x:Key="Tree_Equipment_TestStation_CadAlignment">Cad Alignment</sys:String>
<sys:String x:Key="Tree_Equipment_TestStation_ProcessSequence">Process Sequence</sys:String>
```

---

### Task 4: 清理 TreeNode.cs 死代码

**Files:**
- Modify: `Core/Models/TreeNode.cs`

- [ ] **Step 1: 删除 GetLocalizedDisplayName() 死代码方法**

删除以下方法（当前 L71-81）：

```csharp
// 删除此方法 —— 从未被调用，且逻辑错误（忽略LocalizationKey）
private string GetLocalizedDisplayName()
{
    if (!string.IsNullOrEmpty(LocalizationKey))
    {
        return Name;
    }
    return Name;
}
```

此方法在整个项目中无任何调用点，且其逻辑（有 Key 时仍返回原始 Name）与 ILocalizationService 驱动的架构矛盾。

---

### Task 5: 清理 TreeViewModel.cs 死代码

**Files:**
- Modify: `Framework/ViewModels/TreeViewModel.cs`

- [ ] **Step 1: 删除 GetLocalizedNodeName2() 死代码方法**

删除以下方法（当前 L113-135）：

```csharp
// 删除此方法 —— 从未被调用，是基于culture code的旧方案遗留
public string GetLocalizedNodeName2(TreeNode node)
{
    if (node == null) return string.Empty;
    string currentCulture = _localizationService.CurrentCultureCode;
    if (currentCulture.StartsWith("zh"))
        return node.Name;
    else if (currentCulture.StartsWith("en"))
        return node.DisplayName ?? node.Name;
    else
        return node.DisplayName ?? node.Name;
}
```

项目中实际使用的是 `GetLocalizedNodeName()` 方法（通过 ILocalizationService 查询），`GetLocalizedNodeName2()` 是旧版 culture-switching 方案的残留。

---

### Task 6: 构建验证

- [ ] **Step 1: 执行项目构建**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\MainApp\MainApp.csproj`
Expected: Build succeeded, 无编译错误

- [ ] **Step 2: 验证 JSON 格式正确性**

确认 tree-structure.json 为合法 JSON（无 trailing comma 等语法问题）

---

## 自检清单

- [x] **Spec 覆盖**: tree-structure.json DisplayName 移除 → Task 1; 3节点英文→中文 → Task 1; Key 去重 → Task 1; 中文资源补充 → Task 2; 英文资源补充 → Task 3; TreeNode 死代码 → Task 4; TreeViewModel 死代码 → Task 5
- [x] **占位符扫描**: 所有步骤包含具体代码内容，无 TBD/TODO
- [x] **类型一致性**: LocalizationKey 命名在各文件间一致（`Tree_Equipment_TestStation_WorkOrder` 等）
