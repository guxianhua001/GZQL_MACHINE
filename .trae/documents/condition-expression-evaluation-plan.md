# 条件表达式评估逻辑优化计划

## 问题分析

### 当前表达式评估链路

用户输入表达式 `@Output:步骤6_DASHBOARD结果=true`，评估过程如下：

```
1. CollectContextVariablesAsync() 收集变量池
   ├── @GV:全局变量名 → "值"（从 RecipePoolService 加载）
   └── @Output:输出参数名 → "值"（从 _stepOutputs 累积字典加载）

2. FormulaEvaluator.PreprocessVariables() 变量替换
   ├── 将 @Output:步骤6_DASHBOARD结果 替换为实际值（如 "true"）
   └── 替换后表达式变成: "true==true" 或 "true=true"

3. FormulaEvaluator.Evaluate() 数值求值
   ├── InitTokenizer() 去空格 → "true==true"
   ├── NextToken() 解析Token → 遇到 't' 字符 → 抛出 FormulaParseException
   └── 异常被捕获 → 返回 0 → 条件为 false
```

### 核心问题

**FormulaEvaluator 是纯数值表达式求值器，不支持布尔字符串 `true`/`false`！**

- `@Output:步骤6_DASHBOARD结果` 的值是字符串 `"true"` 或 `"false"`
- 替换后表达式变成 `true==true`，但 Tokenizer 只识别数字和运算符
- 遇到字母 `t` 直接抛异常 `无法识别的字符: 't'`
- 异常被捕获返回 0，条件永远为 false

### 同样的问题也存在于

- `@GV:检测结果 == true`（全局变量值为 "true"/"false"）
- 任何变量值为布尔字符串的表达式

## 修复方案

### 修改 FormulaEvaluator.PreprocessVariables()

在变量替换阶段，将布尔字符串 `true`/`false` 预处理为数值 `1`/`0`：

```
替换前: @Output:步骤6_DASHBOARD结果 == true
变量值: @Output:步骤6_DASHBOARD结果 = "true"
替换后: true == true  ← 当前（报错）
替换后: 1 == 1       ← 修复后（正确）
```

### 具体修改

1. **FormulaEvaluator.PreprocessVariables()** — 变量值预处理
   - 将变量值中的 `"true"` → `"1"`, `"false"` → `"0"`（不区分大小写）
   - 将表达式中独立的 `true`/`false` 关键字也替换为 `1`/`0`

2. **FormulaEvaluator 新增布尔字面量支持** — 在 Tokenizer 中识别 `true`/`false`
   - 新增 `TokenType.True` 和 `TokenType.False`
   - `NextToken()` 遇到字母 `t`/`f` 开头时，读取整个单词
   - `ParseFactor()` 中处理 True/False Token

3. **ConditionBranchViewModel 提示文本** — 更新表达式编辑器的 Hint
   - 明确提示支持 `@Output:步骤6_DASHBOARD结果 == true` 格式
   - 提示 `true=1, false=0`，两种写法等价

## 修改文件清单

| 文件 | 修改内容 |
|------|----------|
| `Core/Services/FormulaEvaluator.cs` | 1. PreprocessVariables 中布尔值→数值预处理 2. Tokenizer 支持 true/false 字面量 |
| `Module/ViewModels/ConditionBranchViewModel.cs` | 无需修改（变量池逻辑已正确） |
| `Module/Views/ConditionBranchView.xaml` | 更新条件表达式 Hint 提示文本 |

## 支持的表达式示例（修复后）

| 表达式 | 替换过程 | 求值结果 |
|--------|----------|----------|
| `@Output:步骤6_DASHBOARD结果 == true` | `1 == 1` | true |
| `@Output:步骤6_DASHBOARD结果` | `1` | true（非0即true） |
| `@Output:步骤6_DASHBOARD结果 == false` | `1 == 0` | false |
| `@GV:H2 > 10.5` | `12.3 > 10.5` | true |
| `@Output:步骤6_DASHBOARD结果 && @GV:Count > 0` | 暂不支持 &&（需后续扩展） | — |
| `@GV:H2 - @GV:Slot > 0.27` | `12.3 - 12.0 > 0.27` | true |
