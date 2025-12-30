using System;

namespace Core.Abstraction
{
    public interface IParameterEditable
    {
        string EditTitle { get; }
        object Parameters { get; }      // 只给 object，框架不关心真实类型

        /// <summary>
        /// 参数唯一标识符（用于存储）
        /// </summary>
        string Identifier { get; } // 添加此属性
    }
}
