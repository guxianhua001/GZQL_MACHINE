
using System;

namespace Core.Abstraction
{
    /// <summary>
    /// 标记参数编辑器应忽略的属性（不显示、不编辑、不保存）
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ParameterIgnoreAttribute : Attribute
    {
    }
}
