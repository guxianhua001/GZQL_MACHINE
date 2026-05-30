# TreeView 多语言硬编码修复设计文档

**日期**: 2026-05-24
**方案**: 方案A — JSON瘦身 + LocalizationKey唯一数据源

## 问题摘要

TreeView 通过 `tree-structure.json` 配置节点，存在以下多语言硬编码问题：

1. **装配单元下3个节点 Name 为英文**（Work Order / Cad Alignment / Process Sequence），导致中文模式下显示英文
2. **3个节点的 LocalizationKey 完全重复**（均为 `Tree_Equipment_TestStation_Test`）
3. **语言资源文件缺少 TestStation 相关键**
4. **JSON 中 DisplayName 字段冗余**（运行时被 ILocalizationService 覆盖，从未使用）
5. **TreeNode.cs 和 TreeViewModel.cs 中存在死代码**

## 修改清单

### 1. tree-structure.json
- 移除所有节点的 `DisplayName` 字段
- 修复装配单元下3个节点：
  - `"Work Order"` → Name: `"工单配置"`, Key: `Tree_Equipment_TestStation_WorkOrder`
  - `"Cad Alignment"` → Name: `"CAD对位"`, Key: `Tree_Equipment_TestStation_CadAlignment`
  - `"Process Sequence"` → Name: `"工艺序列"`, Key: `Tree_Equipment_TestStation_ProcessSequence`

### 2. Strings.zh-CN.xaml
- 新增 3 个中文翻译键

### 3. Strings.en-US.xaml
- 新增 3 个英文翻译键

### 4. TreeNode.cs (Core/Models)
- 删除死代码方法 `GetLocalizedDisplayName()` (L71-81)

### 5. TreeViewModel.cs (Framework/ViewModels)
- 删除死代码方法 `GetLocalizedNodeName2()` (L113-135)

## 数据流（优化后）

```
tree-structure.json (Name + LocalizationKey, 无DisplayName)
  → JsonTreeConfigService.LoadTreeStructureAsync()
  → TreeViewModel.ProcessNodeLocalization()
    → ILocalizationService.GetResourceOrDefault(key, fallback=Name)
    → node.DisplayName = 翻译结果
  → LanguageChanged → UpdateAllNodesDisplayName() → UI刷新
  → TreeView XAML Text="{Binding DisplayName}"
```

## 影响范围

- 运行时行为不变，语言切换流程不受影响
- 向后兼容：Newtonsoft.Json 忽略多余字段，旧JSON带DisplayName也不会报错
