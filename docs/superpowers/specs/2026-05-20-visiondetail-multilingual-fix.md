# VisionDetailView 多语言修复设计

> **日期：** 2026-05-20
> **状态：** 待审批
> **方案：** A — 最小侵入式修复

---

## 1. 问题摘要

### 1.1 发现的问题

| 问题类型 | 数量 | 严重程度 |
|---------|------|---------|
| ViewModel 硬编码中文字符串 | 2 处 | 🔴 高 |
| 英文资源文件未翻译 | 23 个键 | 🔴 高 |
| 缺失资源键 | 1 个 | 🟡 中 |

### 1.2 影响范围

- **文件：** VisionDetailView.xaml, VisionDetailViewModel.cs, Strings.zh-CN.xaml, Strings.en-US.xaml
- **功能：** 视觉配置详情弹窗的多语言显示
- **用户影响：** 英文模式下 UI 显示中文，日志消息为中文

---

## 2. 修复方案（方案 A）

### 2.1 核心原则

- ✅ 只修改有问题的部分，不改变现有架构
- ✅ 复用项目现有的 `LangExtension` + `L()` 多语言机制
- ✅ 改动量最小化，风险最低化

### 2.2 修复清单

#### 2.2.1 ViewModel 硬编码修复

**文件：** `Module/Controls/StepDetails/VisionDetailViewModel.cs`

**修复 #1 — 第 247 行**
```csharp
// ❌ 当前
_logger?.Error($"加载TCP连接列表失败: {ex.Message}");

// ✅ 修复后
_logger?.Error(string.Format(L("VisionDetail_Log_TcpLoadFailed"), ex.Message));
```

**修复 #2 — 第 517 行**
```csharp
// ❌ 当前
return "变量映射:\n" + string.Join("\n", results);

// ✅ 修复后
return L("VisionDetail_Map_Header") + "\n" + string.Join("\n", results);
```

#### 2.2.2 资源文件修改

**文件：** `MainApp/Languages/Strings.zh-CN.xaml`

新增键：
```xml
<sys:String x:Key="VisionDetail_Map_Header">变量映射:</sys:String>
```

**文件：** `MainApp/Languages/Strings.en-US.xaml`

新增键：
```xml
<sys:String x:Key="VisionDetail_Map_Header">Variable Mapping:</sys:String>
```

替换 23 个未翻译条目：

| 键名 | 替换前（中文） | 替换后（英文） |
|------|---------------|---------------|
| VisionDetail_VisionConfig | 视觉配置 | Vision Config |
| VisionDetail_DataParseScript | 数据解析脚本 | Data Parse Script |
| VisionDetail_WriteScriptHint | // 在此编写数据解析脚本 | // Write data parse script here |
| VisionDetail_DefaultTemplate | 默认模板 | Default Template |
| VisionDetail_InputScriptVision | 输入 C# 脚本解析视觉系统返回数据 | Enter C# script to parse vision system response |
| VisionDetail_CommConfig | 通讯配置 | Communication Config |
| VisionDetail_Method | 方式 | Method |
| VisionDetail_TcpConnection | TCP连接 | TCP Connection |
| VisionDetail_TriggerCommand | 触发命令 | Trigger Command |
| VisionDetail_VariableMapping | 变量映射 | Variable Mapping |
| VisionDetail_Column_KeyName | 键名 | Key Name |
| VisionDetail_Column_GlobalVar | 全局变量 | Global Variable |
| VisionDetail_ExecuteTest | 执行测试 | Execute Test |
| VisionDetail_Sample | 示例 | Sample |
| VisionDetail_TestData | 测试数据: | Test Data: |
| VisionDetail_TriggerExecute | 触发执行 | Trigger Execute |
| VisionDetail_SampleExecute | 示例执行 | Sample Execute |
| VisionDetail_VarMappingNote | 变量映射将解析结果写入全局变量 | Variable mapping writes parse results to global variables |
| VisionDetail_QuickTest | 快速测试当前配置 | Quick test current config |
| VisionDetail_Test | 测试 | Test |
| VisionDetail_SaveConfig | 保存配置 | Save Config |

---

## 3. 验证策略

### 3.1 编译验证

- ✅ 项目编译无错误
- ✅ 无警告（如有相关规则）

### 3.2 功能验证

1. 启动应用，打开 VisionDetailView
2. **中文模式验证：**
   - 所有 UI 文本显示正常
   - 执行测试时日志消息为中文
   - 变量映射结果前缀为"变量映射:"
3. **英文模式验证：**
   - 切换语言后所有文本正确显示英文
   - 执行测试时日志消息为英文
   - 变量映射结果前缀为"Variable Mapping:"
4. **功能回归验证：**
   - 保存配置功能正常
   - 关闭弹窗功能正常
   - 变量映射增删功能正常

### 3.3 风险评估

| 风险项 | 级别 | 概率 | 缓解措施 |
|--------|------|------|---------|
| 资源键拼写错误 | 低 | 低 | 复用已有键，新键简单明确 |
| 格式化参数丢失 | 低 | 极低 | 保持 `{0}` 占位符不变 |
| 影响现有功能 | 极低 | 极低 | 仅改字符串，不改逻辑 |

---

## 4. 实施计划

### 4.1 涉及文件

| 文件路径 | 修改类型 | 改动量 |
|---------|---------|--------|
| `Module/Controls/StepDetails/VisionDetailViewModel.cs` | 代码修改 | 2 行替换 |
| `MainApp/Languages/Strings.zh-CN.xaml` | 资源新增 | +1 键 |
| `MainApp/Languages/Strings.en-US.xaml` | 资源修改 | +1 新增 + 23 替换 |

### 4.2 预期效果

- 🎯 VisionDetailView 实现 **100% 多语言覆盖**
- 🎯 中英文切换完全正常
- 🎯 符合项目 WPF+PRISM+MaterialDesign 架构规范
- 🎯 无硬编码中文字符串残留

---

## 5. 审批记录

- [ ] 设计文档审批
- [ ] 实施完成
- [ ] 功能验证通过
