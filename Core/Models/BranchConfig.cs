using Prism.Mvvm;
using System;
using System.Collections.Generic;

namespace Core.Models
{
    /// <summary>
    /// 步骤输出参数定义，用于将步骤执行结果输出到全局变量或作为后续条件判断的数据源
    /// </summary>
    public class BranchOutputParameter : BindableBase
    {
        private string _name;
        private string _value;
        private GlobalVariableType _outputType = GlobalVariableType.Bool;
        private string _targetGlobalVariable;
        private List<string> _filteredGlobalVariableNames = new();

        /// <summary> 参数名称（如"检测结果"、"测量值"） </summary>
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        /// <summary> 参数值（运行时自动填充步骤执行结果，如 true/false 或数值） </summary>
        public string Value { get => _value; set => SetProperty(ref _value, value); }

        /// <summary> 输出值类型（用于与全局变量类型一致性校验） </summary>
        public GlobalVariableType OutputType
        {
            get => _outputType;
            set
            {
                if (SetProperty(ref _outputType, value))
                    FilteredGlobalVariableNamesChanged?.Invoke(this);
            }
        }

        /// <summary> 目标全局变量名（可选，设置后运行时自动写入该全局变量） </summary>
        public string TargetGlobalVariable
        {
            get => _targetGlobalVariable;
            set => SetProperty(ref _targetGlobalVariable, value);
        }

        /// <summary> 按当前 OutputType 过滤后的全局变量名列表（由 ViewModel 设置） </summary>
        public List<string> FilteredGlobalVariableNames
        {
            get => _filteredGlobalVariableNames;
            set => SetProperty(ref _filteredGlobalVariableNames, value);
        }

        /// <summary> OutputType 变化时通知 ViewModel 刷新过滤列表 </summary>
        public event Action<BranchOutputParameter> FilteredGlobalVariableNamesChanged;
    }

    /// <summary>
    /// 单个分支条件规则：当条件满足时跳转到指定目标
    /// 支持多个条件的优先级匹配，第一个满足的条件即生效
    /// </summary>
    public class BranchCondition : BindableBase
    {
        private string _conditionExpression;
        private int _targetStepSeq;
        private string _description;

        /// <summary>
        /// 条件表达式，支持格式：
        /// - 简单比较: "@GV:变量名 > 10"
        /// - 参数引用: "@Output:参数名 == true"
        /// - 复合表达式: "@GV:H2 - @GV:Slot > 0.27"
        /// - 布尔判断: "@GV:检测结果 == true"
        /// </summary>
        public string ConditionExpression { get => _conditionExpression; set => SetProperty(ref _conditionExpression, value); }

        /// <summary> 条件满足时跳转的目标步骤Seq号（0表示继续下一步） </summary>
        public int TargetStepSeq { get => _targetStepSeq; set => SetProperty(ref _targetStepSeq, value); }

        /// <summary> 条件描述（用于UI显示，如"检测通过→跳转组装"） </summary>
        public string Description { get => _description; set => SetProperty(ref _description, value); }
    }

    /// <summary>
    /// 步骤的条件分支配置，定义该步骤执行后的输出参数、条件判断和跳转逻辑
    /// 类似于CheckDetail但更通用，适用于所有步骤类型
    /// 支持基于全局变量或输出参数的表达式求值，实现灵活的流程控制
    /// </summary>
    public class BranchConfig
    {
        /// <summary> 是否启用条件分支（默认false，向后兼容） </summary>
        public bool IsEnabled { get; set; }

        /// <summary> 输出参数列表（该步骤执行后产生的结果数据） </summary>
        public List<BranchOutputParameter> OutputParameters { get; set; } = new List<BranchOutputParameter>();

        /// <summary> 条件规则列表（按优先级从高到低评估，第一个匹配的条件生效） </summary>
        public List<BranchCondition> Conditions { get; set; } = new List<BranchCondition>();

        /// <summary> 所有条件都不满足时的默认动作（Continue=继续下一步, Stop=终止序列, SkipTo=跳转到指定步骤） </summary>
        public DefaultBranchAction DefaultAction { get; set; } = DefaultBranchAction.SkipTo;

        /// <summary> 默认动作的目标步骤Seq（仅DefaultAction=SkipTo时有效） </summary>
        public int DefaultTargetStepSeq { get; set; } = 0;
    }

    /// <summary> 默认分支动作枚举 </summary>
    public enum DefaultBranchAction
    {
        Continue,
        Stop,
        SkipTo
    }

    /// <summary>
    /// 步骤输出信息（携带名称和类型），用于输出参数下拉选择
    /// 选择后自动推断 BranchOutputParameter.OutputType
    /// </summary>
    public class StepOutputInfo
    {
        public string Name { get; set; }
        public GlobalVariableType OutputType { get; set; }

        public override string ToString() => Name;
    }
}
