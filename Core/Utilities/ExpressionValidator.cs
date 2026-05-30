using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Core.Utilities
{
    /// <summary>
    /// 条件分支表达式校验器
    /// 支持变量引用(@Output:xxx, @GV:xxx)、比较运算符(>, <, ==, =, !=, >=, <=)、
    /// 逻辑运算符(&&, ||, !)、算术运算符(+, -, *, /)、括号、数字(含小数)和布尔字面量
    /// </summary>
    public static class ExpressionValidator
    {
        private static readonly HashSet<string> ValidOperators = new(StringComparer.OrdinalIgnoreCase)
        { "==", "=", "!=", ">", "<", ">=", "<=", "&&", "||", "+", "-", "*", "/", "!", "(", ")" };

        private static readonly Regex VariablePattern = new(@"@(Output|GV):[\w\u4e00-\u9fa5]+",
            RegexOptions.Compiled);

        /// <summary>
        /// Token 提取正则：按优先级匹配双字符运算符、单字符运算符、数字(含小数)、布尔字面量、标识符
        /// </summary>
        private static readonly Regex TokenPattern = new(
            @"(==|!=|>=|<=|&&|\|\|)|([><=+\-*/!()])|(\d+\.?\d*)|(true|false)|(\w+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// 校验条件表达式语法
        /// </summary>
        /// <param name="expression">条件表达式，如 "@Output:步骤3_结果 > 10"</param>
        /// <param name="availableVariables">可用变量名列表</param>
        /// <returns>空字符串表示校验通过，否则返回错误描述</returns>
        public static string Validate(string expression, IEnumerable<string> availableVariables)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return "表达式不能为空";

            var varSet = availableVariables?.ToHashSet(StringComparer.OrdinalIgnoreCase)
                         ?? new HashSet<string>();

            var referencedVars = VariablePattern.Matches(expression)
                .Select(m => m.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var v in referencedVars)
            {
                if (!varSet.Contains(v))
                    return $"未知变量: {v}";
            }

            int depth = 0;
            for (int i = 0; i < expression.Length; i++)
            {
                if (expression[i] == '(') depth++;
                else if (expression[i] == ')')
                {
                    depth--;
                    if (depth < 0) return $"位置 {i}: 多余的右括号";
                }
            }
            if (depth > 0) return "括号不匹配";

            string sanitized = VariablePattern.Replace(expression, "VAR");

            foreach (Match match in TokenPattern.Matches(sanitized))
            {
                if (match.Groups[1].Success || match.Groups[2].Success)
                {
                    string op = match.Value;
                    if (!ValidOperators.Contains(op))
                        return $"非法运算符: '{op}'";
                }
                else if (match.Groups[3].Success)
                {
                    // 数字字面量（含小数），始终合法
                }
                else if (match.Groups[4].Success)
                {
                    // 布尔字面量 true/false，始终合法
                }
                else if (match.Groups[5].Success)
                {
                    string ident = match.Value;
                    if (!string.Equals(ident, "VAR", StringComparison.OrdinalIgnoreCase))
                        return $"未识别的标识符: '{ident}'";
                }
            }

            string covered = TokenPattern.Replace(sanitized, "");
            var unexpected = covered.Where(ch => !char.IsWhiteSpace(ch)).ToList();
            if (unexpected.Count > 0)
                return $"非法字符: '{unexpected[0]}'";

            return string.Empty;
        }
    }
}
