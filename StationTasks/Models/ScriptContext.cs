using System;
using System.Collections.Generic;
using System.Globalization;

namespace StationTasks.Models
{
    /// <summary>
    /// 脚本执行上下文，封装全局变量和步骤输出参数的读写，自动处理类型转换
    /// 脚本通过 ctx.GetDouble("变量名") 读取、ctx.Set("变量名", value) 写入
    /// </summary>
    public class ScriptContext
    {
        private readonly Dictionary<string, string> _globalVariables;
        private readonly Dictionary<string, string> _stepOutputs;
        private readonly Dictionary<string, string> _snapshot;
        private readonly Dictionary<string, string> _changes;

        /// <summary>
        /// 创建脚本执行上下文
        /// </summary>
        /// <param name="globalVariables">全局变量字典（Key=变量名, Value=变量值字符串）</param>
        /// <param name="stepOutputs">前序步骤输出参数字典</param>
        public ScriptContext(
            Dictionary<string, string> globalVariables,
            Dictionary<string, string> stepOutputs)
        {
            _globalVariables = globalVariables ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _stepOutputs = stepOutputs ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _snapshot = new Dictionary<string, string>(_globalVariables, StringComparer.OrdinalIgnoreCase);
            _changes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        // ═══ 读取全局变量 ═══

        /// <summary> 读取全局变量，自动转换为 double，变量不存在或转换失败返回默认值 </summary>
        public double GetDouble(string name, double defaultValue = 0)
        {
            if (_globalVariables.TryGetValue(name, out var val) && double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                return result;
            return defaultValue;
        }

        /// <summary> 安全读取全局变量 double 值 </summary>
        public bool TryGetDouble(string name, out double value)
        {
            value = 0;
            if (!_globalVariables.TryGetValue(name, out var val)) return false;
            return double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        /// <summary> 读取全局变量，自动转换为 int，变量不存在或转换失败返回默认值 </summary>
        public int GetInt(string name, int defaultValue = 0)
        {
            if (_globalVariables.TryGetValue(name, out var val) && int.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                return result;
            return defaultValue;
        }

        /// <summary> 读取全局变量字符串值 </summary>
        public string GetString(string name, string defaultValue = "")
        {
            return _globalVariables.TryGetValue(name, out var val) ? val : defaultValue;
        }

        /// <summary> 读取全局变量，自动转换为 bool </summary>
        public bool GetBool(string name, bool defaultValue = false)
        {
            if (_globalVariables.TryGetValue(name, out var val))
            {
                if (bool.TryParse(val, out var result)) return result;
                if (val == "1") return true;
                if (val == "0") return false;
            }
            return defaultValue;
        }

        // ═══ 读取步骤输出参数 ═══

        /// <summary> 读取步骤输出参数，自动转换为 double </summary>
        public double GetOutputDouble(string name, double defaultValue = 0)
        {
            if (_stepOutputs.TryGetValue(name, out var val) && double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                return result;
            return defaultValue;
        }

        /// <summary> 读取步骤输出参数字符串值 </summary>
        public string GetOutputString(string name, string defaultValue = "")
        {
            return _stepOutputs.TryGetValue(name, out var val) ? val : defaultValue;
        }

        // ═══ 写入变量 ═══

        /// <summary> 写入全局变量（double 值自动转换为字符串，执行后自动回写） </summary>
        public void Set(string name, double value)
        {
            var strVal = value.ToString(CultureInfo.InvariantCulture);
            _globalVariables[name] = strVal;
            _changes[name] = strVal;
        }

        /// <summary> 写入全局变量（int 值自动转换为字符串） </summary>
        public void Set(string name, int value)
        {
            var strVal = value.ToString(CultureInfo.InvariantCulture);
            _globalVariables[name] = strVal;
            _changes[name] = strVal;
        }

        /// <summary> 写入全局变量（字符串值） </summary>
        public void Set(string name, string value)
        {
            _globalVariables[name] = value ?? "";
            _changes[name] = value ?? "";
        }

        /// <summary> 写入全局变量（bool 值自动转换为字符串） </summary>
        public void Set(string name, bool value)
        {
            var strVal = value.ToString();
            _globalVariables[name] = strVal;
            _changes[name] = strVal;
        }

        // ═══ 变更检测 ═══

        /// <summary>
        /// 获取所有变更的变量（包括 Set 调用和直接修改 globalVariables 的差异）
        /// </summary>
        public Dictionary<string, string> GetChanges()
        {
            // 检测通过 globalVariables 直接修改但未通过 Set 记录的变更
            foreach (var kv in _globalVariables)
            {
                if (!_snapshot.TryGetValue(kv.Key, out var oldVal) || oldVal != kv.Value)
                {
                    if (!_changes.ContainsKey(kv.Key))
                        _changes[kv.Key] = kv.Value;
                }
            }

            return new Dictionary<string, string>(_changes, StringComparer.OrdinalIgnoreCase);
        }
    }
}
