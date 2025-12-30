using System;

namespace Core.Abstraction
{
    /// <summary>
    /// 参数服务接口
    /// </summary>
    public interface IParameterService
    {
        /// <summary>
        /// 加载所有可配置的参数分组
        /// </summary>
        Task<IEnumerable<ParameterGroup>> LoadParametersAsync();
        /// <summary>
        /// 保存参数配置
        /// </summary>
        Task SaveParametersAsync(IEnumerable<ParameterGroup> parameterGroups);
        /// <summary>
        /// 重置为默认配置
        /// </summary>
        Task<IEnumerable<ParameterGroup>> ResetToDefaultsAsync();
    }
}
