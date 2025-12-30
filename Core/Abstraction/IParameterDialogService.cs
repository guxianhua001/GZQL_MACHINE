using System;
namespace Core.Abstraction
{
    public interface IParameterDialogService
    {
        Task<bool> ShowEditorDialog(string title, TaskParametersBase parameters, Action<TaskParametersBase> onSaved = null);
    }
}
