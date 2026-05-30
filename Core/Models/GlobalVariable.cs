﻿﻿﻿using Prism.Mvvm;
using System;

namespace Core.Models
{
    public enum GlobalVariableType
    {
        Int, IntArray,
        Double, DoubleArray,
        String, StringArray,
        Bool, BoolArray
    }

    public class GlobalVariable : BindableBase
    {
        private int _index;
        private string _name;
        private GlobalVariableType _type;
        private string _value;
        private string _comment;

        public int Index
        {
            get => _index;
            set => SetProperty(ref _index, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public GlobalVariableType Type
        {
            get => _type;
            set
            {
                var oldType = _type;
                if (SetProperty(ref _type, value))
                {
                    OnTypeChanged(oldType);
                }
            }
        }

        /// <summary>
        /// 变量值（统一string存储），setter中根据Type进行类型约束验证：
        /// Int/IntArray：只允许整数，自动截断小数部分
        /// Double/DoubleArray：允许小数
        /// Bool/BoolArray：只允许true/false
        /// String/StringArray：无约束
        /// </summary>
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, NormalizeValueByType(value, _type));
        }

        public string Comment
        {
            get => _comment;
            set => SetProperty(ref _comment, value);
        }

        /// <summary>
        /// 根据变量类型对输入值进行规范化约束
        /// Int类型：截断小数部分，只保留整数
        /// Bool类型：只允许true/false
        /// Double类型：验证是否为合法数值
        /// String类型：无约束
        /// </summary>
        private string NormalizeValueByType(string inputValue, GlobalVariableType type)
        {
            if (string.IsNullOrWhiteSpace(inputValue))
                return inputValue;

            switch (type)
            {
                case GlobalVariableType.Int:
                case GlobalVariableType.IntArray:
                    // Int类型：如果输入了小数，自动截断为整数
                    if (double.TryParse(inputValue, out double intVal))
                        return ((int)Math.Truncate(intVal)).ToString();
                    // 非数值输入保持原样（用户可能正在编辑中）
                    return inputValue;

                case GlobalVariableType.Double:
                case GlobalVariableType.DoubleArray:
                    // Double类型：验证是否为合法数值，不合法则保持原样
                    if (double.TryParse(inputValue, out _))
                        return inputValue;
                    return inputValue;

                case GlobalVariableType.Bool:
                case GlobalVariableType.BoolArray:
                    // Bool类型：只允许true/false（不区分大小写）
                    var lower = inputValue.Trim().ToLowerInvariant();
                    if (lower == "true" || lower == "false")
                        return lower;
                    // 输入1/0也自动转换为true/false
                    if (inputValue.Trim() == "1") return "true";
                    if (inputValue.Trim() == "0") return "false";
                    return inputValue;

                default:
                    return inputValue;
            }
        }

        private void OnTypeChanged(GlobalVariableType oldType)
        {
            // 如果当前值为空，或者等于旧类型的常见默认值，则更新为新类型的默认值
            bool shouldUpdate = string.IsNullOrWhiteSpace(Value) ||
                                Value == "0" ||
                                Value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                                Value.Equals("true", StringComparison.OrdinalIgnoreCase);
            if (shouldUpdate)
            {
                Value = GetDefaultValueForType(_type);
            }
            else
            {
                // 类型变更时，对当前值进行规范化（如从Double切到Int，截断小数）
                var normalized = NormalizeValueByType(Value, _type);
                if (normalized != Value)
                    SetProperty(ref _value, normalized, nameof(Value));
            }
        }

        private string GetDefaultValueForType(GlobalVariableType type)
        {
            switch (type)
            {
                case GlobalVariableType.Int: return "0";
                case GlobalVariableType.Double: return "0";
                case GlobalVariableType.Bool: return "false";
                case GlobalVariableType.String: return "";
                case GlobalVariableType.IntArray:
                case GlobalVariableType.DoubleArray:
                case GlobalVariableType.StringArray:
                case GlobalVariableType.BoolArray:
                    return "";
                default: return "";
            }
        }
    }
}