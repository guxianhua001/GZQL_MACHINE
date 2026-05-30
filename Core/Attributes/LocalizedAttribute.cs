using System;

namespace Core.Attributes
{
    /// <summary>
    /// 标记需要本地化的属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class LocalizedAttribute : Attribute
    {
        /// <summary>
        /// 资源键（如果为null则使用属性名）
        /// </summary>
        public string ResourceKey { get; set; }

        /// <summary>
        /// 格式化参数属性名（如果有）
        /// </summary>
        public string FormatArgsProperty { get; set; }

        public LocalizedAttribute() { }

        public LocalizedAttribute(string resourceKey)
        {
            ResourceKey = resourceKey;
        }
    }
}