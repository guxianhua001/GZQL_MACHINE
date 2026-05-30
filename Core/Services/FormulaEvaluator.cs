using Core.Abstraction;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;

namespace Core.Services
{
    /// <summary>
    /// 轻量表达式求值器：支持 @GV:/@Output: 变量引用 + 四则运算 + 条件判断 + 布尔字面量 true/false
    /// 布尔值 true=1, false=0，可在表达式中直接使用
    /// </summary>
    public class FormulaEvaluator : IFormulaEvaluator
    {
        private enum TokenType { Number, True, False, Plus, Minus, Multiply, Divide, LParen, RParen, GT, LT, GTE, LTE, EQ, NEQ, And, Or, Eof }

        private struct Token { public TokenType Type; public double Value; }

        private string _input;
        private int _pos;
        private Token _currentToken;

        public double Evaluate(string formula, IDictionary<string, string> variables)
        {
            if (string.IsNullOrWhiteSpace(formula)) return 0;
            try
            {
                var processed = PreprocessVariables(formula, variables);
                InitTokenizer(processed);
                NextToken();
                var result = ParseOrExpression();
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FormulaEvaluator] 公式求值失败: '{formula}' → {ex.Message}");
                return 0;
            }
        }

        public bool EvaluateCondition(string condition, IDictionary<string, string> variables)
        {
            if (string.IsNullOrWhiteSpace(condition)) return true;
            try
            {
                var processed = PreprocessVariables(condition, variables);
                InitTokenizer(processed);
                NextToken();
                var result = ParseComparison();
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FormulaEvaluator] 条件求值失败: '{condition}' → {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 预处理：将变量名替换为实际数值，处理 @GV: 和 @Output: 前缀
        /// 布尔字符串 true/false 自动转为 1/0
        /// </summary>
        private string PreprocessVariables(string formula, IDictionary<string, string> variables)
        {
            var result = formula;

            // 构建替换映射：键为变量全名（含前缀），值为数值化后的字符串
            var replacements = new List<KeyValuePair<string, string>>();
            foreach (var kv in variables)
            {
                if (string.IsNullOrEmpty(kv.Key)) continue;
                string numValue = NormalizeToNumeric(kv.Value);
                replacements.Add(new KeyValuePair<string, string>(kv.Key, numValue));
            }

            // 按键长度降序排列，避免短键名误替换长键名
            replacements.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));

            foreach (var kv in replacements)
                result = result.Replace(kv.Key, kv.Value);

            // 处理剩余未替换的 @GV: 引用为 0
            while (result.Contains("@GV:"))
            {
                var start = result.IndexOf("@GV:");
                var end = start + 4;
                while (end < result.Length && (char.IsLetterOrDigit(result[end]) || result[end] == '_' || result[end] == '-'))
                    end++;
                var varName = result.Substring(start, end - start);
                result = result.Replace(varName, "0");
                Debug.WriteLine($"[FormulaEvaluator] 未找到变量: {varName} → 替换为 0");
            }
            // 处理剩余未替换的 @Output: 引用为 0
            while (result.Contains("@Output:"))
            {
                var start = result.IndexOf("@Output:");
                var end = start + 8;
                while (end < result.Length && (char.IsLetterOrDigit(result[end]) || result[end] == '_' || result[end] == '-'))
                    end++;
                var varName = result.Substring(start, end - start);
                result = result.Replace(varName, "0");
                Debug.WriteLine($"[FormulaEvaluator] 未找到变量: {varName} → 替换为 0");
            }
            return result;
        }

        /// <summary> 将变量值规范化为数值字符串：true→1, false→0, 纯数字保持原样 </summary>
        private static string NormalizeToNumeric(string value)
        {
            if (string.IsNullOrEmpty(value)) return "0";
            var trimmed = value.Trim();
            if (string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase)) return "1";
            if (string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase)) return "0";
            if (double.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out _)) return trimmed;
            return "0";
        }

        private void InitTokenizer(string input) { _input = input; _pos = 0; }

        private void NextToken()
        {
            SkipWhitespace();
            if (_pos >= _input.Length)
            { _currentToken = new Token { Type = TokenType.Eof }; return; }

            char c = _input[_pos];

            // 布尔字面量 true / false
            if (char.IsLetter(c))
            {
                int start = _pos;
                while (_pos < _input.Length && char.IsLetter(_input[_pos]))
                    _pos++;
                var word = _input.Substring(start, _pos - start);
                if (string.Equals(word, "true", StringComparison.OrdinalIgnoreCase))
                {
                    _currentToken = new Token { Type = TokenType.True, Value = 1.0 };
                    return;
                }
                if (string.Equals(word, "false", StringComparison.OrdinalIgnoreCase))
                {
                    _currentToken = new Token { Type = TokenType.False, Value = 0.0 };
                    return;
                }
                throw new FormulaParseException($"无法识别的标识符: '{word}' at position {start}");
            }

            // 数字（含负数和小数）
            if (char.IsDigit(c) || (c == '-' && (_pos == 0 || !char.IsDigit(_input[_pos - 1]) && _input[_pos - 1] != ')')))
            {
                int start = _pos;
                if (c == '-') _pos++;
                while (_pos < _input.Length && (char.IsDigit(_input[_pos]) || _input[_pos] == '.')) _pos++;
                var numStr = _input.Substring(start, _pos - start);
                _currentToken = new Token { Type = TokenType.Number, Value = double.Parse(numStr, CultureInfo.InvariantCulture) };
                return;
            }

            // 运算符和括号
            switch (c)
            {
                case '+': _currentToken = new Token { Type = TokenType.Plus }; _pos++; return;
                case '-': _currentToken = new Token { Type = TokenType.Minus }; _pos++; return;
                case '*': _currentToken = new Token { Type = TokenType.Multiply }; _pos++; return;
                case '/': _currentToken = new Token { Type = TokenType.Divide }; _pos++; return;
                case '(': _currentToken = new Token { Type = TokenType.LParen }; _pos++; return;
                case ')': _currentToken = new Token { Type = TokenType.RParen }; _pos++; return;
                case '>':
                    if (_pos + 1 < _input.Length && _input[_pos + 1] == '=')
                    { _currentToken = new Token { Type = TokenType.GTE }; _pos += 2; return; }
                    _currentToken = new Token { Type = TokenType.GT }; _pos++; return;
                case '<':
                    if (_pos + 1 < _input.Length && _input[_pos + 1] == '=')
                    { _currentToken = new Token { Type = TokenType.LTE }; _pos += 2; return; }
                    _currentToken = new Token { Type = TokenType.LT }; _pos++; return;
                case '=':
                    if (_pos + 1 < _input.Length && _input[_pos + 1] == '=')
                    { _currentToken = new Token { Type = TokenType.EQ }; _pos += 2; return; }
                    // 单个 '=' 也视为相等比较（兼容用户习惯，如 @Output:结果=true）
                    _currentToken = new Token { Type = TokenType.EQ }; _pos++; return;
                case '!':
                    if (_pos + 1 < _input.Length && _input[_pos + 1] == '=')
                    { _currentToken = new Token { Type = TokenType.NEQ }; _pos += 2; return; }
                    break;
                case '&':
                    if (_pos + 1 < _input.Length && _input[_pos + 1] == '&')
                    { _currentToken = new Token { Type = TokenType.And }; _pos += 2; return; }
                    break;
                case '|':
                    if (_pos + 1 < _input.Length && _input[_pos + 1] == '|')
                    { _currentToken = new Token { Type = TokenType.Or }; _pos += 2; return; }
                    break;
            }

            throw new FormulaParseException($"无法识别的字符: '{c}' at position {_pos}");
        }

        private void SkipWhitespace()
        {
            while (_pos < _input.Length && char.IsWhiteSpace(_input[_pos])) _pos++;
        }

        // ====== 表达式语法分析（递归下降） ======

        // orExpr ::= andExpr (('||') andExpr)*
        private double ParseOrExpression()
        {
            double left = ParseAndExpression();
            while (_currentToken.Type == TokenType.Or)
            {
                NextToken();
                double right = ParseAndExpression();
                left = (Math.Abs(left) > 0.0001 || Math.Abs(right) > 0.0001) ? 1.0 : 0.0;
            }
            return left;
        }

        // andExpr ::= comparison (('&&') comparison)*
        private double ParseAndExpression()
        {
            double left = ParseComparisonAsNumber();
            while (_currentToken.Type == TokenType.And)
            {
                NextToken();
                double right = ParseComparisonAsNumber();
                left = (Math.Abs(left) > 0.0001 && Math.Abs(right) > 0.0001) ? 1.0 : 0.0;
            }
            return left;
        }

        // comparisonAsNumber: 执行比较并返回 1.0/0.0
        private double ParseComparisonAsNumber()
        {
            double left = ParseExpression();

            if (_currentToken.Type == TokenType.Eof ||
                _currentToken.Type == TokenType.RParen ||
                _currentToken.Type == TokenType.And ||
                _currentToken.Type == TokenType.Or)
                return left;

            var op = _currentToken.Type;
            NextToken();
            double right = ParseExpression();

            bool result = op switch
            {
                TokenType.GT => left > right,
                TokenType.LT => left < right,
                TokenType.GTE => left >= right,
                TokenType.LTE => left <= right,
                TokenType.EQ => Math.Abs(left - right) < 0.0001,
                TokenType.NEQ => Math.Abs(left - right) >= 0.0001,
                _ => throw new FormulaParseException($"未知的比较运算符: {op}")
            };
            return result ? 1.0 : 0.0;
        }

        // expression ::= term (('+' | '-') term)*
        private double ParseExpression()
        {
            double left = ParseTerm();
            while (_currentToken.Type == TokenType.Plus || _currentToken.Type == TokenType.Minus)
            {
                var op = _currentToken.Type;
                NextToken();
                double right = ParseTerm();
                left = op == TokenType.Plus ? left + right : left - right;
            }
            return left;
        }

        // term ::= factor (('*' | '/') factor)*
        private double ParseTerm()
        {
            double left = ParseFactor();
            while (_currentToken.Type == TokenType.Multiply || _currentToken.Type == TokenType.Divide)
            {
                var op = _currentToken.Type;
                NextToken();
                double right = ParseFactor();
                left = op == TokenType.Multiply ? left * right : right != 0 ? left / right : throw new DivideByZeroException("公式除零");
            }
            return left;
        }

        // factor ::= Number | True | False | '(' expression ')' | ('+' | '-') factor
        private double ParseFactor()
        {
            if (_currentToken.Type == TokenType.Number || _currentToken.Type == TokenType.True || _currentToken.Type == TokenType.False)
            {
                double val = _currentToken.Value;
                NextToken();
                return val;
            }
            if (_currentToken.Type == TokenType.LParen)
            {
                NextToken();
                double val = ParseOrExpression();
                Expect(TokenType.RParen);
                return val;
            }
            if (_currentToken.Type == TokenType.Minus)
            {
                NextToken();
                return -ParseFactor();
            }
            if (_currentToken.Type == TokenType.Plus)
            {
                NextToken();
                return ParseFactor();
            }
            throw new FormulaParseException($"意外的 Token: {_currentToken.Type} at position {_pos}");
        }

        // comparison ::= orExpression（顶层条件求值，返回 bool）
        private bool ParseComparison()
        {
            double result = ParseOrExpression();
            return Math.Abs(result) > 0.0001;
        }

        private void Expect(TokenType type)
        {
            if (_currentToken.Type != type)
                throw new FormulaParseException($"期望 {type} 但得到 {_currentToken.Type} at position {_pos}");
            NextToken();
        }
    }

    internal class FormulaParseException : Exception
    {
        public FormulaParseException(string message) : base(message) { }
    }
}
