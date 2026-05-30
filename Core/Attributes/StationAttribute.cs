
using System;

namespace Core.Abstraction
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class StationAttribute : Attribute
    {
        public int TaskId { get; }
        public Type ParameterType { get; }
        public string Identifier { get; set; }      // 工站唯一标识
        public string DisplayName { get; set; }     // 工站显示名称

        public StationAttribute(int taskId, Type parameterType)
        {
            TaskId = taskId;
            ParameterType = parameterType;
        }
    }
}