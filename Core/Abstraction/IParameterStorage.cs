// Core.Abstraction/IParameterStorage.cs
using System;

namespace Core.Abstraction
{
    /// <summary>
    /// ：负责参数持久化（存储/读取）
    /// </summary>
    public interface IParameterStorage
    {
        TParams Load<TParams>(string identifier) where TParams : TaskParametersBase, new();
        void Save<TParams>(string identifier, TParams parameters) where TParams : TaskParametersBase;

        // 支持自定义路径
        T Load<T>(string identifier, string customDirectory) where T : class, new();
        void Save<T>(string identifier, T data, string customDirectory) where T : class;
    }
}