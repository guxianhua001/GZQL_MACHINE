// Core.Abstraction/IParameterStore.cs
using System;

namespace Core.Abstraction
{
    public interface IParameterStore
    {
        /// <summary>
        /// 唯一标识符(推荐使用任务ID+任务名)
        /// </summary>
        string Identifier { get; }

        /// <summary>
        /// 参数版本
        /// </summary>
        string ConfigVersion { get; set; }

        /// <summary>
        /// 最后修改时间
        /// </summary>
        DateTime LastModified { get; }

        /// <summary>
        /// 创建参数副本（深拷贝）
        /// </summary>
        IParameterStore CreateSnapshot();
    }
}


