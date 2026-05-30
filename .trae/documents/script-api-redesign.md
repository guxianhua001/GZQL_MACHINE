# SCRIPT 步骤 API 重设计计划

## 问题分析

### 问题 1：字符串拼接陷阱
`globalVariables["angle"] = globalVariables["offsetX"] + globalVariables["offsetY"]`
- `offsetX = "0.5"`, `offsetY = "-0.3"` → 结果是 `"0.5-0.3"`（字符串拼接），不是 `0.2`（数值相加）
- 根因：所有值都是 `string` 类型，`+` 运算符是字符串拼接

### 问题 2：API 过于复杂
当前 API：
```csharp
public static Dictionary<string, string> Execute(
    IDictionary<string, string> globalVariables,
    IDictionary<string, string> stepOutputs)
```
- 用户需要理解两个字典参数的区别
- 所有值都是 string，数值运算必须手动 `double.Parse` / `.ToString()`
- `result` 返回值概念模糊——"返回的结果给谁用？"
- 直接修改 `globalVariables` 和通过 `result` 返回两种写法并存，令人困惑

### 问题 3：全局变量 UI 不刷新
`ScriptStepAction` 执行后保存了全局变量，但没有发布 `GlobalVariablesChangedEvent`，导致 `GlobalVariablesViewModel` 不知道数据已变更。

### 对比分析

| 维度 | 当前 API（Dictionary） | 参考项目（ExeModule） | 新设计（ScriptContext） |
|------|----------------------|---------------------|----------------------|
| 方法签名 | `static Dictionary<string,string> Execute(dict, dict)` | `override bool ExeModule()` | `static bool Execute(ScriptContext)` |
| 读取变量 | `globalVariables["名"]` → string | 直接访问字段 | `context.GetDouble("名")` → double |
| 写入变量 | `result["名"] = val` 或 `globalVariables["名"] = val` | 直接赋值 | `context.Set("名", val)` |
| 类型转换 | 手动 `double.Parse` / `.ToString` | 无需（强类型） | 自动（ScriptContext 内部处理） |
| 返回值 | Dictionary（语义模糊） | bool（成功/失败） | bool（成功/失败） |
| 学习成本 | 高（两个字典 + string 陷阱） | 低 | 低 |

## 设计方案：引入 ScriptContext

### 核心思路
引入 `ScriptContext` 类作为脚本的唯一交互入口，封装全局变量和步骤输出参数的读写，自动处理类型转换，统一写入方式。

### 新脚本约定

```csharp
public class ScriptAction
{
    public static bool Execute(ScriptContext ctx)
    {
        // 读取变量（自动类型转换）
        double offsetX = ctx.GetDouble("offsetX");   // 不存在返回 0
        double offsetY = ctx.GetDouble("offsetY");

        // 数值运算
        double angle = offsetX + offsetY;

        // 写入变量（直接设置，自动回写全局变量）
        ctx.Set("angle", angle);

        return true;  // true=成功, false=失败(触发异常处理)
    }
}
```

### ScriptContext 类设计

```csharp
public class ScriptContext
{
    // 内部存储
    private readonly Dictionary<string, string> _globalVariables;
    private readonly Dictionary<string, string> _stepOutputs;
    private readonly Dictionary<string, string> _snapshot;  // 执行前快照
    private readonly Dictionary<string, string> _changes;   // 变更记录

    // 读取方法（自动类型转换，变量不存在返回默认值）
    double GetDouble(string name, double defaultValue = 0);
    int GetInt(string name, int defaultValue = 0);
    string GetString(string name, string defaultValue = "");
    bool GetBool(string name, bool defaultValue = false);
    bool TryGetDouble(string name, out double value);

    // 读取步骤输出参数
    double GetOutputDouble(string name, double defaultValue = 0);
    string GetOutputString(string name, string defaultValue = "");

    // 写入方法（自动记录变更，执行后自动回写全局变量）
    void Set(string name, double value);
    void Set(string name, int value);
    void Set(string name, string value);
    void Set(string name, bool value);

    // 变更检测
    Dictionary<string, string> GetChanges();  // 返回所有变更（直接修改 + Set 调用）
}
```

---

## 实施步骤

### 步骤 1：创建 ScriptContext 类
**文件**：`c:\WorkFiles\GZQL_MACHINE\StationTasks\Models\ScriptContext.cs`（新建）

- 内部持有 `_globalVariables`、`_stepOutputs`、`_snapshot`、`_changes` 四个字典
- `GetDouble/GetInt/GetString/GetBool` — 从 `_globalVariables` 读取并自动类型转换
- `GetOutputDouble/GetOutputString` — 从 `_stepOutputs` 读取
- `Set` — 写入 `_globalVariables` 和 `_changes`（覆盖快照检测逻辑）
- `GetChanges` — 合并快照差异 + Set 调用记录，返回最终变更集
- 所有值统一以 `string` 存储在内部字典中，`Set` 方法自动 `.ToString()`

### 步骤 2：修改 ScriptStepAction
**文件**：`c:\WorkFiles\GZQL_MACHINE\StationTasks\Actions\ScriptStepAction.cs`

1. 编译委托类型从 `Func<IDictionary<string,string>, IDictionary<string,string>, Dictionary<string,string>>` 改为 `Func<ScriptContext, bool>`
2. `ExecuteAsync` 中创建 `ScriptContext` 实例（传入 globalVariables + stepOutputs）
3. 调用 `_compiledDelegate(ctx)` 执行脚本
4. 返回 `false` 时抛出 `RecoverableException`
5. 从 `ctx.GetChanges()` 获取变更集，写入全局变量 + 步骤输出参数
6. **新增**：注入 `IEventAggregator`，执行完成后发布 `GlobalVariablesChangedEvent`
7. `EnsureCompiled` 中方法签名验证改为 `Execute(ScriptContext)` → 返回 `bool`

### 步骤 3：修改 ScriptDetailViewModel
**文件**：`c:\WorkFiles\GZQL_MACHINE\Module\Editor\ScriptDetailViewModel.cs`

1. 编译委托类型同步改为 `Func<ScriptContext, bool>`
2. `CompileScript` 方法签名验证同步更新
3. `OnExecute` 预览执行：创建 `ScriptContext`，调用委托，从 `ctx.GetChanges()` 显示结果
4. 默认模板更新为新 API 风格
5. 双击变量插入逻辑更新：插入 `ctx.GetDouble("变量名")` / `ctx.Set("变量名", value)`

### 步骤 4：修改 ScriptDetailView.xaml
**文件**：`c:\WorkFiles\GZQL_MACHINE\Module\Editor\ScriptDetailView.xaml`

1. 右侧面板提示信息更新为新 API 语法
2. 双击全局变量插入 `ctx.GetDouble("变量名")`
3. 双击步骤输出参数插入 `ctx.GetOutputDouble("参数名")`

### 步骤 5：修改 ScriptDetailView.xaml.cs
**文件**：`c:\WorkFiles\GZQL_MACHINE\Module\Editor\ScriptDetailView.xaml.cs`

1. 双击事件处理中更新插入文本格式

### 步骤 6：构建验证

---

## 文件变更清单

| 操作 | 文件路径 | 说明 |
|------|---------|------|
| 新建 | `StationTasks\Models\ScriptContext.cs` | 脚本上下文类，封装变量读写和类型转换 |
| 修改 | `StationTasks\Actions\ScriptStepAction.cs` | 改用 ScriptContext + 发布 GlobalVariablesChangedEvent |
| 修改 | `Module\Editor\ScriptDetailViewModel.cs` | 编译/执行逻辑改用 ScriptContext + 更新默认模板 |
| 修改 | `Module\Editor\ScriptDetailView.xaml` | 提示信息更新为新 API |
| 修改 | `Module\Editor\ScriptDetailView.xaml.cs` | 双击插入文本更新 |

---

## 关键设计考量

1. **向后兼容**：ScriptContext 是新类，不破坏现有模型
2. **类型安全**：`GetDouble`/`Set` 自动处理类型转换，消除 string 拼接陷阱
3. **简单直观**：`ctx.Set("angle", offsetX + offsetY)` 一行搞定读写，无需 `double.Parse`/`.ToString`
4. **返回值语义清晰**：`bool` = 成功/失败，不再有 `result` 字典的困惑
5. **UI 刷新**：发布 `GlobalVariablesChangedEvent`，与 SCAN 等步骤行为一致
6. **变更追踪**：ScriptContext 内部统一追踪变更，不再需要快照对比逻辑
