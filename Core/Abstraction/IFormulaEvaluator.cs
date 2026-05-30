using System.Collections.Generic;

namespace Core.Abstraction
{
    /// <summary> 轻量数学表达式求值器接口 </summary>
    public interface IFormulaEvaluator
    {
        /// <summary> 计算公式的数值结果 </summary>
        /// <param name="formula">公式字符串，如 "@GV:H2 - @GV:Slot实测 + 0.27"</param>
        /// <param name="variables">变量名→值的字典</param>
        double Evaluate(string formula, IDictionary<string, string> variables);

        /// <summary> 计算条件公式的布尔结果 </summary>
        /// <param name="condition">条件表达式，如 "@GV:拨动距离 > 0"</param>
        bool EvaluateCondition(string condition, IDictionary<string, string> variables);
    }
}
