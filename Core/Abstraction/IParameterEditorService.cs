// Core.Abstraction/IParameterEditorService.cs
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Abstraction
{
    public interface IParameterEditorService
    {
        /// <summary>
        /// 获取用于参数编辑器的所有分组参数
        /// </summary>
        Task<IEnumerable<ParameterGroup>> GetEditableParametersAsync();

        /// <summary>
        /// 保存编辑后的参数
        /// </summary>
        Task SaveEditableParametersAsync(IEnumerable<ParameterGroup> parameterGroups);
    }
}

