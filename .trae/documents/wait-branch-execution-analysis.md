# WaitDetailViewModel 和 ConditionBranchViewModel 分析报告

## 问题1：执行步骤时的执行函数在哪里？

### WaitDetailViewModel

**WaitDetailViewModel 本身没有执行函数**，它只是 UI 编辑器 ViewModel，负责配置延时参数。

执行链路如下：

```
WaitDetailViewModel (UI编辑器，仅配置)
    ↓ 保存到
ProcessStep.WaitDetail (数据模型)
    ↓ 读取于
WaitStepAction.ExecuteAsync() (实际执行函数)
    ↓ 位于
StationTasks/Actions/WaitStepAction.cs
```

**WaitStepAction.ExecuteAsync** 的完整代码：

```csharp
public async Task ExecuteAsync(ProcessStep step, StationTaskBase task, CancellationToken token)
{
    double delayMs = step.WaitDetail?.ActualDelayMs ?? 1000;
    _logger.Info($"WAIT 步骤 [{step.Seq}] 开始延时: {delayMs} ms");
    await Task.Delay((int)delayMs, token);  // 支持取消打断
    _logger.Info($"WAIT 步骤 [{step.Seq}] 延时完成: {delayMs} ms");
}
```

调度入口在 `ProcessStepExecutor.ExecuteSingleStepAsync()` 中：

```csharp
case StepType.WAIT:
    await ExecuteWithRunStepAsync(stepLabel, step, token);
    // → _actionMap[StepType.WAIT].ExecuteAsync() → WaitStepAction.ExecuteAsync()
```

### ConditionBranchViewModel

**ConditionBranchViewModel 本身也没有执行函数**，它只是 UI 编辑器 ViewModel，负责配置条件分支规则。

条件分支的执行逻辑不在独立的 Action 中，而是作为**附加配置**嵌入在 `ProcessStepExecutor` 中：

```
ConditionBranchViewModel (UI编辑器，仅配置)
    ↓ 保存到
ProcessStep.BranchConfig (数据模型)
    ↓ 读取于
ProcessStepExecutor.ExecuteBranchLogicAsync() (实际执行函数)
    ↓ 位于
StationTasks/Actions/ProcessStepExecutor.cs:534-578
```

执行链路：

```
ExecuteSingleStepAsync()
    ├── 步骤主逻辑 (如 WaitStepAction/VisionStepAction)
    └── 步骤完成后检查 BranchConfig
        └── ExecuteBranchLogicAsync()
            ├── CollectContextVariablesAsync()  → 收集 @GV: 和 @Output: 变量
            ├── EvaluateCondition()            → 通过 FormulaEvaluator 求值
            └── HandleDefaultActionAsync()     → Continue/Stop/SkipTo
```

**注意**：`StepType.BRANCH` 枚举值存在，但在 `ExecuteSingleStepAsync` 的 switch 中没有对应的 case，会落入 default 被跳过。条件分支是作为其他步骤的附加配置使用的。

---

## 问题2：ConditionBranchViewModel 的条件表达式可以编辑公式吗？

### ✅ 可以，已支持公式编辑

**UI层面**：条件表达式使用 `IsEditable="True"` 的 ComboBox，用户可以自由输入任意表达式，同时下拉提供变量名辅助选择。

**支持的公式格式**：

| 格式 | 示例 | 说明 |
|------|------|------|
| 简单比较 | `@GV:H2 > 10` | 全局变量与常量比较 |
| 参数引用 | `@Output:步骤3_VISION结果 == true` | 引用前序步骤输出 |
| 复合表达式 | `@GV:H2 - @GV:Slot > 0.27` | 四则运算后比较 |
| 布尔判断 | `@GV:检测结果 == true` | 布尔值判断 |

**底层解析器**：`FormulaEvaluator`（递归下降解析器）

支持的运算符：
- 四则运算：`+ - * /`
- 括号：`( )`
- 比较运算：`> < >= <= == !=`

变量替换规则：
- `@GV:变量名` → 替换为全局变量值
- `@Output:参数名` → 替换为步骤输出参数值
- 未找到的变量 → 替换为 0

### 当前限制

1. **不支持逻辑运算符**（AND/OR/&&/||），只能写单个比较表达式
2. **不支持字符串比较**，所有值都转为 double 计算
3. **不支持函数调用**（如 abs(), max() 等）
4. **下拉辅助只提供变量名**，不提供运算符或常量模板
5. **无表达式语法校验**，错误表达式静默返回 0/false

---

## 结论

两个 ViewModel 都是纯 UI 编辑器，不包含执行逻辑。执行逻辑分别在：

| ViewModel | 对应执行函数 | 文件位置 |
|-----------|-------------|---------|
| WaitDetailViewModel | WaitStepAction.ExecuteAsync() | StationTasks/Actions/WaitStepAction.cs |
| ConditionBranchViewModel | ProcessStepExecutor.ExecuteBranchLogicAsync() | StationTasks/Actions/ProcessStepExecutor.cs:534 |

条件表达式已支持公式编辑，使用递归下降解析器 FormulaEvaluator，支持四则运算和比较运算。
