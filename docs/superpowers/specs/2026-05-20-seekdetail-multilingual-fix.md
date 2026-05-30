# SeekDetailView 多语言修复设计

> **日期：** 2026-05-20
> **状态：** 待审批
> **方案：** A — 完整修复（含基础设施）

---

## 1. 问题摘要

### 1.1 发现的问题

| 问题类型 | 数量 | 严重程度 |
|---------|------|---------|
| ViewModel 硬编码中文字符串 | 7 处 | 🔴🔴🔴 严重 |
| 英文资源文件未翻译 | 17 个键（100%） | 🔴🔴🔴 严重 |
| 缺少多语言基础设施（L() 方法） | 1 套 | 🔴🔴 高 |
| 缺失资源键 | ~6 个 | 🟡 中 |

### 1.2 与 VisionDetail 对比

| 对比项 | VisionDetail | SeekDetail |
|--------|-------------|------------|
| ViewModel 硬编码数 | 2 处 | **7 处** |
| 英文未翻译率 | 52% (23/44) | **100% (17/17)** |
| L() 方法 | ✅ 已有 | ❌ **缺失** |
| ILocalizationService 注入 | ✅ 已有 | ❌ **缺失** |
| 需新增资源键 | 1 个 | **~6 个** |

### 1.3 影响范围

- **文件：** SeekDetailView.xaml, SeekDetailViewModel.cs, Strings.zh-CN.xaml, Strings.en-US.xaml
- **功能：** SEEK 步骤详细配置弹窗的多语言显示（通道配置、导入导出、实时刷新）
- **用户影响：**
  - 英文模式下 UI 显示中文
  - 导入/导出对话框标题、过滤器为中文
  - 错误消息为中文
  - 默认通道描述为中文

---

## 2. 修复方案（方案 A — 完整修复）

### 2.1 核心原则

- ✅ 新增完整的多语言支持基础设施（L() 方法 + 依赖注入）
- ✅ 消除所有硬编码中文字符串
- ✅ 补全所有英文翻译
- ✅ 与 VisionDetailViewModel 保持架构一致性
- ✅ 符合项目 WPF+PRISM 架构规范

### 2.2 修复清单

#### 2.2.1 新增多语言基础设施

**文件：** `Module/Controls/StepDetails/SeekDetailViewModel.cs`

**修改 #1 — 新增 using 引用**

在文件顶部添加：
```csharp
using Core.Abstraction;  // ILocalizationService 接口
```

**修改 #2 — 新增字段依赖注入**

```csharp
private readonly ILocalizationService _localizationService;
```

**修改 #3 — 修改构造函数签名和实现**

当前：
```csharp
public SeekDetailViewModel(IMotionService motionService, IRecipePoolService recipePoolService)
{
    _motionService = motionService;
    _recipePoolService = recipePoolService;
    // ... 命令初始化
}
```

修改后：
```csharp
public SeekDetailViewModel(
    IMotionService motionService,
    IRecipePoolService recipePoolService,
    ILocalizationService localizationService)
{
    _motionService = motionService;
    _recipePoolService = recipePoolService;
    _localizationService = localizationService;
    // ... 命令初始化（不变）
}
```

**修改 #4 — 新增 L() 便捷方法**

在类中添加（建议放在属性定义区域之后）：
```csharp
/// <summary>
/// 获取多语言文本（便捷方法）
/// </summary>
private string L(string key) => _localizationService.GetResource(key);
```

#### 2.2.2 修复 7 处硬编码

| # | 行号 | 当前代码（❌） | 修复后代码（✅） | 资源键 |
|---|------|---------------|-----------------|--------|
| 1 | 114 | `Description = "径向力"` | `Description = L("SeekDetail_DefaultDesc")` | SeekDetail_DefaultDesc |
| 2 | 189 | `Filter = "JSON 文件\|*.json"` | `Filter = L("SeekDetail_JsonFileFilter")` | SeekDetail_JsonFileFilter |
| 3 | 190 | `Title = "导入 SEEK 通道配置"` | `Title = L("SeekDetail_ImportDialogTitle")` | SeekDetail_ImportDialogTitle |
| 4 | 206 | `$"导入失败: {ex.Message}"` | `string.Format(L("SeekDetail_Error_ImportFailed"), ex.Message)` | SeekDetail_Error_ImportFailed |
| 5 | 215 | `Filter = "JSON 文件\|*.json"` | `Filter = L("SeekDetail_JsonFileFilter")` | 复用 #2 |
| 6 | 216 | `Title = "导出 SEEK 通道配置"` | `Title = L("SeekDetail_ExportDialogTitle")` | SeekDetail_ExportDialogTitle |
| 7 | 228 | `$"导出失败: {ex.Message}"` | `string.Format(L("SeekDetail_Error_ExportFailed"), ex.Message)` | SeekDetail_Error_ExportFailed |

#### 2.2.3 资源文件修改

**文件 A：** `MainApp/Languages/Strings.zh-CN.xaml`

在 SeekDetailView 相关键值区域（约第 1645 行之后）添加：

```xml
<!-- SeekDetailView - 运行时文本 -->
<sys:String x:Key="SeekDetail_DefaultDesc">径向力</sys:String>
<sys:String x:Key="SeekDetail_JsonFileFilter">JSON 文件|*.json</sys:String>
<sys:String x:Key="SeekDetail_ImportDialogTitle">导入 SEEK 通道配置</sys:String>
<sys:String x:Key="SeekDetail_ExportDialogTitle">导出 SEEK 通道配置</sys:String>
<sys:String x:Key="SeekDetail_Error_ImportFailed">导入失败: {0}</sys:String>
<sys:String x:Key="SeekDetail_Error_ExportFailed">导出失败: {0}</sys:String>
```

**文件 B：** `MainApp/Languages/Strings.en-US.xaml`

**操作 1：替换 17 个现有条目为英文**

| 键名 | 替换前（中文） | 替换后（英文） |
|------|---------------|---------------|
| SeekDetail_Offline | 离线 | Offline |
| SeekDetail_RealTimeCollecting | ● 实时采集中 | ● Collecting |
| SeekDetail_Column_LinkedChannel | 链接通道 | Linked Ch |
| SeekDetail_Column_GlobalVar | 全局变量 | Global Var |
| SeekDetail_AddChannel | 添加 | Add |
| SeekDetail_AddChannelToolTip | 添加通道 | Add channel |
| SeekDetail_DeleteChannel | 删除 | Delete |
| SeekDetail_DeleteChannelToolTip | 删除选中通道 | Delete selected channel |
| SeekDetail_Refresh | 刷新 | Refresh |
| SeekDetail_RefreshToolTip | 开始实时刷新力值数据 | Start real-time force refresh |
| SeekDetail_Stop | 停止 | Stop |
| SeekDetail_StopToolTip | 停止实时刷新 | Stop real-time refresh |
| SeekDetail_Import | 导入 | Import |
| SeekDetail_ImportToolTip | 从 JSON 文件导入通道配置 | Import channel config from JSON |
| SeekDetail_Export | 导出 | Export |
| SeekDetail_ExportToolTip | 导出通道配置为 JSON 文件 | Export channel config to JSON |
| SeekDetail_ConfirmContinue | 确认继续 | Confirm & Continue |

**操作 2：新增 6 个运行时文本键**

```xml
<!-- SeekDetailView - Runtime Text -->
<sys:String x:Key="SeekDetail_DefaultDesc">Radial Force</sys:String>
<sys:String x:Key="SeekDetail_JsonFileFilter">JSON Files|*.json</sys:String>
<sys:String x:Key="SeekDetail_ImportDialogTitle">Import SEEK Channel Config</sys:String>
<sys:String x:Key="SeekDetail_ExportDialogTitle">Export SEEK Channel Config</sys:String>
<sys:String x:Key="SeekDetail_Error_ImportFailed">Import failed: {0}</sys:String>
<sys:String x:Key="SeekDetail_Error_ExportFailed">Export failed: {0}</sys:String>
```

---

## 3. 验证策略

### 3.1 编译验证

- ✅ 项目编译无错误
- ✅ 无警告（如有相关规则）
- ⚠️ 注意：构造函数签名变更可能影响 DI 注册，需确认 Prism 容器能正确解析

### 3.2 功能验证

1. 启动应用，打开 SeekDetailView
2. **中文模式验证：**
   - 所有 UI 文本显示正常（标题、按钮、列头等）
   - 点击"导入"按钮 → 对话框标题为"导入 SEEK 通道配置"
   - 点击"导出"按钮 → 对话框标题为"导出 SEEK 通道配置"
   - 默认通道 Description 为"径向力"
   - 导入无效文件 → Debug 输出包含"导入失败:"
3. **英文模式验证：**
   - 切换语言后所有 UI 文本正确显示英文
   - "离线" → "Offline"，"● 实时采集中" → "● Collecting"
   - 导入对话框标题 → "Import SEEK Channel Config"
   - 默认通道 Description → "Radial Force"
4. **功能回归验证：**
   - 添加/删除通道功能正常
   - 开始/停止刷新功能正常
   - 导入/导出 JSON 功能正常
   - 保存并关闭功能正常

### 3.3 风险评估

| 风险项 | 级别 | 概率 | 缓解措施 |
|--------|------|------|---------|
| 构造函数变更导致 DI 解析失败 | 🟡 中 | 低 | PRISM 支持构造函数注入，只需确认接口已注册 |
| 资源键拼写错误 | 低 | 低 | 复用已有命名规范，新键简单明确 |
| 格式化参数丢失 | 低 | 极低 | 保持 `{0}` 占位符不变 |
| 影响现有功能 | 极低 | 极低 | 仅改字符串和添加依赖，不改业务逻辑 |

---

## 4. 实施计划

### 4.1 涉及文件

| 文件路径 | 修改类型 | 改动量 |
|---------|---------|--------|
| `Module/Controls/StepDetails/SeekDetailViewModel.cs` | 代码修改 | ~20 行 |
| `MainApp/Languages/Strings.zh-CN.xaml` | 资源新增 | +6 键 |
| `MainApp/Languages/Strings.en-US.xaml` | 资源修改 | +6 新增 + 17 替换 |

### 4.2 预期效果

- 🎯 SeekDetailView 实现 **100% 多语言覆盖**
- 🎯 与 VisionDetailViewModel 架构模式完全一致
- 🎯 中英文切换完全正常（包括运行时文本）
- 🎯 无硬编码中文字符串残留
- 🎯 未来可复用 L() 方法扩展其他功能

---

## 5. 审批记录

- [ ] 设计文档审批
- [ ] 实施完成
- [ ] 功能验证通过
