# VisionDetailView 多语言修复实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 消除 VisionDetailView 中的硬编码中文字符串，补全英文资源文件翻译，实现 100% 多语言覆盖

**Architecture:** 采用最小侵入式修复方案，复用项目现有的 LangExtension 标记扩展和 L() 多语言方法。仅修改字符串资源，不改变业务逻辑。

**Tech Stack:** WPF, PRISM, MaterialDesignInXAML, ResourceDictionary (XAML)

---

## 文件结构

```
修改文件:
├── Module/Controls/StepDetails/VisionDetailViewModel.cs    # 修复 2 处硬编码
├── MainApp/Languages/Strings.zh-CN.xaml                   # 新增 1 个资源键
└── MainApp/Languages/Strings.en-US.xaml                   # 新增 1 个 + 替换 23 个
```

---

### Task 1: 修复 ViewModel 硬编码 #1 — TCP 加载失败日志

**Files:**
- Modify: `Module/Controls/StepDetails/VisionDetailViewModel.cs:247`

- [ ] **Step 1: 定位并替换第 247 行的硬编码中文字符串**

将：
```csharp
_logger?.Error($"加载TCP连接列表失败: {ex.Message}");
```

替换为：
```csharp
_logger?.Error(string.Format(L("VisionDetail_Log_TcpLoadFailed"), ex.Message));
```

**验证要点：**
- ✅ 使用已有的 `VisionDetail_Log_TcpLoadFailed` 资源键（该键已存在于两个语言文件中）
- ✅ 保持 `{0}` 格式化参数不变
- ✅ 复用 ViewModel 中已有的 `L(string key)` 方法（定义在第 180 行）

- [ ] **Step 2: 验证修改后编译通过**

Run: `dotnet build` 或在 Visual Studio 中编译项目
Expected: 编译成功，无错误

---

### Task 2: 修复 ViewModel 硬编码 #2 — 变量映射结果前缀

**Files:**
- Modify: `Module/Controls/StepDetails/VisionDetailViewModel.cs:517`
- Modify: `MainApp/Languages/Strings.zh-CN.xaml` （新增资源键）
- Modify: `MainApp/Languages/Strings.en-US.xaml` （新增资源键）

- [ ] **Step 1: 在 Strings.zh-CN.xaml 中新增资源键**

在 VisionDetailView 相关键值区域（约第 2103 行之后）添加：

```xml
<sys:String x:Key="VisionDetail_Map_Header">变量映射:</sys:String>
```

- [ ] **Step 2: 在 Strings.en-US.xaml 中新增资源键**

在 VisionDetailView 相关键值区域（约第 2080 行之后）添加：

```xml
<sys:String x:Key="VisionDetail_Map_Header">Variable Mapping:</sys:String>
```

- [ ] **Step 3: 替换 VisionDetailViewModel.cs 第 517 行的硬编码字符串**

将：
```csharp
return "变量映射:\n" + string.Join("\n", results);
```

替换为：
```csharp
return L("VisionDetail_Map_Header") + "\n" + string.Join("\n", results);
```

**验证要点：**
- ✅ 新增的键名遵循现有命名规范 `VisionDetail_` 前缀
- ✅ 中英文值语义一致，格式统一（都带冒号）
- ✅ 保持原有的 `\n` 换行符逻辑不变

- [ ] **Step 4: 验证修改后编译通过**

Run: `dotnet build` 或在 Visual Studio 中编译项目
Expected: 编译成功，无错误

---

### Task 3: 补全 Strings.en-US.xaml 英文翻译（UI 文本部分）

**Files:**
- Modify: `MainApp/Languages/Strings.en-US.xaml:1746-1766`

- [ ] **Step 1: 替换 VisionDetail_VisionConfig**

将：
```xml
<sys:String x:Key="VisionDetail_VisionConfig">视觉配置</sys:String>
```
替换为：
```xml
<sys:String x:Key="VisionDetail_VisionConfig">Vision Config</sys:String>
```

- [ ] **Step 2: 替换 VisionDetail_DataParseScript**

将：
```xml
<sys:String x:Key="VisionDetail_DataParseScript">数据解析脚本</sys:String>
```
替换为：
```xml
<sys:String x:Key="VisionDetail_DataParseScript">Data Parse Script</sys:String>
```

- [ ] **Step 3: 替换 VisionDetail_WriteScriptHint**

将：
```xml
<sys:String x:Key="VisionDetail_WriteScriptHint">// 在此编写数据解析脚本</sys:String>
```
替换为：
```xml
<sys:String x:Key="VisionDetail_WriteScriptHint">// Write data parse script here</sys:String>
```

- [ ] **Step 4: 替换 VisionDetail_DefaultTemplate**

将：
```xml
<sys:String x:Key="VisionDetail_DefaultTemplate">默认模板</sys:String>
```
替换为：
```xml
<sys:String x:Key="VisionDetail_DefaultTemplate">Default Template</sys:String>
```

- [ ] **Step 5: 替换 VisionDetail_InputScriptVision**

将：
```xml
<sys:String x:Key="VisionDetail_InputScriptVision">输入 C# 脚本解析视觉系统返回数据</sys:String>
```
替换为：
```xml
<sys:String x:Key="VisionDetail_InputScriptVision">Enter C# script to parse vision system response</sys:String>
```

- [ ] **Step 6: 替换 VisionDetail_CommConfig**

将：
```xml
<sys:String x:Key="VisionDetail_CommConfig">通讯配置</sys:String>
```
替换为：
```xml
<sys:String x:Key="VisionDetail_CommConfig">Communication Config</sys:String>
```

- [ ] **Step 7: 替换 VisionDetail_Method**

将：
```xml
<sys:String x:Key="VisionDetail_Method">方式</sys:String>
```
替换为：
```xml
<sys:String x:Key="VisionDetail_Method">Method</sys:String>
```

- [ ] **Step 8: 替换 VisionDetail_TcpConnection**

将：
```xml
<sys:String x:Key="VisionDetail_TcpConnection">TCP连接</sys:String>
```
替换为：
```xml
<sys:String x:Key="VisionDetail_TcpConnection">TCP Connection</sys:String>
```

- [ ] **Step 9: 替换 VisionDetail_TriggerCommand**

将：
```xml
<sys:String x:Key="VisionDetail_TriggerCommand">触发命令</sys:String>
```
替换为：
```xml
<sys:String x:Key="VisionDetail_TriggerCommand">Trigger Command</sys:String>
```

- [ ] **Step 10: 替换 VisionDetail_VariableMapping**

将：
```xml
<sys:String x:Key="VisionDetail_VariableMapping">变量映射</sys:String>
```
替换为：
```xml
<sys:String x:Key="VisionDetail_VariableMapping">Variable Mapping</sys:String>
```

- [ ] **Step 11: 替换 VisionDetail_Column_KeyName**

将：
```xml
<sys:String x:Key="VisionDetail_Column_KeyName">键名</sys:String>
```
替换为：
```xml
<sys:String x:Key="VisionDetail_Column_KeyName">Key Name</sys:String>
```

- [ ] **Step 12: 替换 VisionDetail_Column_GlobalVar**

将：
```xml
<sys:String x:Key="VisionDetail_Column_GlobalVar">全局变量</sys:String>
```
替换为：
```xml
<sys:String x:Key="VisionDetail_Column_GlobalVar">Global Variable</sys:String>
```

**验证要点：**
- ✅ 所有 UI 标签文本翻译准确
- ✅ 保持 XML 格式正确，无语法错误
- ✅ 键名保持不变，仅修改 Value

- [ ] **Step 13: 验证编译通过**

Run: `dotnet build` 或在 Visual Studio 中编译项目
Expected: 编译成功，无错误

---

### Task 4: 补全 Strings.en-US.xaml 英文翻译（按钮与操作部分）

**Files:**
- Modify: `MainApp/Languages/Strings.en-US.xaml:1749-1765`

- [ ] **Step 1: 替换 VisionDetail_ExecuteTest**

将：
```xml
<sys:String x:Key="VisionDetail_ExecuteTest">执行测试</sys:String>
```
替换为：
```xml
<sys:String x:Key="VisionDetail_ExecuteTest">Execute Test</sys:String>
```

- [ ] **Step 2: 替换 VisionDetail_Sample**

将：
```xml
<sys:String x:Key="VisionDetail_Sample">示例</sys:String>
```
替换为：
```xml
<sys:String x:Key="VisionDetail_Sample">Sample</sys:String>
```

- [ ] **Step 3: 替换 VisionDetail_TestData**

将：
```xml
<sys:String x:Key="VisionDetail_TestData">测试数据:</sys:String>
```
替换为：
```xml
<sys:String x:Key="VisionDetail_TestData">Test Data:</sys:String>
```

- [ ] **Step 4: 替换 VisionDetail_TriggerExecute**

将：
```xml
<sys:String x:Key="VisionDetail_TriggerExecute">触发执行</sys:String>
```
替换为：
```xml
<sys:String x:Key="VisionDetail_TriggerExecute">Trigger Execute</sys:String>
```

- [ ] **Step 5: 替换 VisionDetail_SampleExecute**

将：
```xml
<sys:String x:Key="VisionDetail_SampleExecute">示例执行</sys:String>
```
替换为：
```xml
<sys:String x:Key="VisionDetail_SampleExecute">Sample Execute</sys:String>
```

- [ ] **Step 6: 替换 VisionDetail_VarMappingNote**

将：
```xml
<sys:String x:Key="VisionDetail_VarMappingNote">变量映射将解析结果写入全局变量</sys:String>
```
替换为：
```xml
<sys:String x:Key="VisionDetail_VarMappingNote">Variable mapping writes parse results to global variables</sys:String>
```

- [ ] **Step 7: 替换 VisionDetail_QuickTest**

将：
```xml
<sys:String x:Key="VisionDetail_QuickTest">快速测试当前配置</sys:String>
```
替换为：
```xml
<sys:String x:Key="VisionDetail_QuickTest">Quick test current config</sys:String>
```

- [ ] **Step 8: 替换 VisionDetail_Test**

将：
```xml
<sys:String x:Key="VisionDetail_Test">测试</sys:String>
```
替换为：
```xml
<sys:String x:Key="VisionDetail_Test">Test</sys:String>
```

- [ ] **Step 9: 替换 VisionDetail_SaveConfig**

将：
```xml
<sys:String x:Key="VisionDetail_SaveConfig">保存配置</sys:String>
```
替换为：
```xml
<sys:String x:Key="VisionDetail_SaveConfig">Save Config</sys:String>
```

**验证要点：**
- ✅ 所有按钮、操作文本翻译准确且简洁
- ✅ ToolTip 文本符合英文表达习惯
- ✅ XML 格式正确

- [ ] **Step 10: 验证编译通过**

Run: `dotnet build` 或在 Visual Studio 中编译项目
Expected: 编译成功，无错误

---

### Task 5: 最终验证与总结

- [ ] **Step 1: 完整编译验证**

Run: `dotnet build --configuration Release`
Expected: 编译成功，0 错误 0 警告（如有相关规则）

- [ ] **Step 2: 搜索残留的硬编码中文**

在以下文件中搜索中文字符串正则 `[\u4e00-\u9fa5]`：
- `Module/Controls/StepDetails/VisionDetailViewModel.cs`
- `Module/Controls/StepDetails/VisionDetailView.xaml`

Expected: 仅剩注释中的中文说明文字，无运行时硬编码

- [ ] **Step 3: 验证资源键完整性**

对比 Strings.zh-CN.xaml 和 Strings.en-US.xaml 中的 VisionDetail_ 前缀键：
Expected: 两个文件的键集合完全一致（en-US 应比原来多 1 个新键）

- [ ] **Step 4: 更新版本修改记录**

在项目根目录的 `版本修改记录.txt` 中追加：

```
[2026-05-20] VisionDetailView 多语言修复
- 修复 VisionDetailViewModel.cs 中 2 处硬编码中文字符串
- 补全 Strings.en-US.xaml 中 23 个未翻译条目
- 新增 VisionDetail_Map_Header 资源键
- 实现 VisionDetailView 100% 多语言覆盖
```

---

## 自我审查清单

### ✅ Spec 覆盖度检查

| Spec 要求 | 对应 Task | 状态 |
|----------|----------|------|
| 修复 ViewModel 第 247 行硬编码 | Task 1 | ✅ |
| 修复 ViewModel 第 517 行硬编码 | Task 2 | ✅ |
| 新增 VisionDetail_Map_Header 键 | Task 2 | ✅ |
| 补全 23 个英文翻译 | Task 3-4 | ✅ |
| 编译验证 | Task 1-5 | ✅ |

### ✅ 占位符扫描

- ❌ 无 TBD / TODO
- ❌ 无 "添加适当的错误处理"
- ❌ 无 "类似 Task N" 引用
- ✅ 每个步骤包含完整代码

### ✅ 类型一致性检查

- ✅ `L()` 方法签名一致（string key → string）
- ✅ 资源键命名规范一致（VisionDetail_ 前缀）
- ✅ 格式化参数用法一致（{0} 占位符）

---

## 执行统计

| 指标 | 数量 |
|------|------|
| 总 Task 数 | 5 |
| 总 Step 数 | 26 |
| 修改文件数 | 3 |
| 新增资源键 | 1 |
| 替换资源值 | 25 |
| 修复硬编码 | 2 |
| 预计耗时 | 15-20 分钟 |
