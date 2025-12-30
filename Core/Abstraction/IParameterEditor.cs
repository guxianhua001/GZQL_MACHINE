using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Abstraction
{
    public interface IParameterEditor
    {
        Task<bool> EditParameters(IParameterEditable target, Action<TaskParametersBase> onSaved = null);
    }
}
