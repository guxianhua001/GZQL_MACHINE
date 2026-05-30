# DataDashboardView 多语言修复设计

> **日期：** 2026-05-20
> **状态：** 待审批
> **方案：** 完整修复（SeekDetail 模式）

---

## 1. 问题摘要

### 1.1 发现的问题

| 问题类型 | 数量 | 严重程度 |
|---------|------|---------|
| ViewModel 硬编码中文字符串 | 15 处 | 🔴🔴🔴 |
| 英文资源文件未翻译 | 18 个（100%） | 🔴🔴🔴 |
| 缺少多语言基础设施（L() 方法） | 1 套 | 🔴🔴 |
| 缺失资源键 | ~7 个 | 🟡 |

### 1.2 硬编码分类

**UI 相关（3 处）— 高优先级：**
- 第 283 行：对话框标题 `"选择示意图"`
- 第 284 行：文件过滤器 `"图片文件|*.png;..."`
- 第 327 行：默认字段名 `$"变量{maxSeq + 1}"`

**日志相关（12 处）— 中优先级：**
- 第 188, 203, 215, 219, 249, 254, 260, 316, 362, 437, 462, 484 行

### 1.3 架构对齐目标

与已完成修复的模块保持一致：

| 模块 | 状态 | 资源键数 |
|------|------|---------|
| VisionDetailView | ✅ 已完成 | 46 |
| SeekDetailView | ✅ 已完成 | 23 |
| **DataDashboardView** | ⏳ 待修复 | **18+7=25** |

---

## 2. 修复方案

### 2.1 核心原则

- ✅ 复用 SeekDetail 成功模式
- ✅ 新增 ILocalizationService 依赖注入 + L() 方法
- ✅ 消除所有硬编码中文字符串
- ✅ 补全英文翻译

### 2.2 修复清单

#### 2.2.1 新增多语言基础设施

参照 SeekDetailViewModel：
1. 新增 `using Core.Abstraction;`
2. 新增 `private readonly ILocalizationService _localizationService;`
3. 构造函数新增参数 `ILocalizationService localizationService`
4. 新增 `private string L(string key) => _localizationService.GetResource(key);`

#### 2.2.2 修复 15 处硬编码

**UI 硬编码（3 处）：**
| # | 行号 | 当前 | 替换为 | 资源键 |
|---|------|------|--------|--------|
| 1 | 283 | `"选择示意图"` | `L("DataDetail_SelectDiagramTitle")` | DataDetail_SelectDiagramTitle |
| 2 | 284 | `"图片文件\|*..."` | `L("DataDetail_ImageFileFilter")` | DataDetail_ImageFileFilter |
| 3 | 327 | `$"变量{maxSeq + 1}"` | `string.Format(L("DataDetail_DefaultFieldName"), maxSeq + 1)` | DataDetail_DefaultFieldName |

**日志硬编码（12 处）：**
全部替换为 `L("DataDetail_Log_xxx")` 格式

#### 2.2.3 资源文件修改

**zh-CN 新增 ~7 个键**
**en-US 新增 ~7 个键 + 替换 18 个值为英文**

---

## 3. 验证策略

- 编译验证
- 硬编码残留检查
- 资源键完整性对比（zh-CN = en-US）
- 功能验证（打开对话框、加载图片等）

---

## 4. 实施计划

### 4.1 涉及文件

| 文件 | 改动量 |
|------|--------|
| DataDashboardViewModel.cs | ~30 行 |
| Strings.zh-CN.xaml | +7 键 |
| Strings.en-US.xaml | +7 键 + 18 替换 |

### 4.2 预期效果

- 🎯 DataDashboardView 实现 **100% 多语言覆盖**
- 🎯 三大 DetailView 全部完成国际化
- 🎯 形成可复用的标准化模式

---

## 5. 审批记录

- [ ] 设计文档审批
- [ ] 实施完成
- [ ] 验证通过
