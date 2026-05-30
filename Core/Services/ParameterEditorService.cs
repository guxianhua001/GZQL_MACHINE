// Core/Services/ParameterEditorService.cs
using Core.Abstraction;
using Prism.Ioc;
using Prism.Services.Dialogs;

namespace Core.Services
{
    public class ParameterEditorService : IParameterEditor
    {
        private readonly IParameterStorage _storage;
        private readonly IContainerProvider _container;
        private readonly IParameterDialogService _dialogService;

        public ParameterEditorService(
            IParameterStorage storage,
            IContainerProvider container,
            IParameterDialogService dialogService)
        {
            _storage = storage;
            _container = container;
            _dialogService = dialogService;
        }

        public async Task<bool> EditParameters(IParameterEditable target, Action<TaskParametersBase> onSaved = null)
        {
            if (target?.Parameters is not TaskParametersBase parameters)
                return false;
            // 创建参数快照并编辑
            var snapshot = parameters.CreateSnapshot() as TaskParametersBase;
            string stationId = target.Identifier; // 从适配器获取工站标识
            bool edited = await _dialogService.ShowEditorDialog(target.EditTitle, snapshot, onSaved, stationId);

            if (!edited) return false;
            // 保存和应用变更
            _storage.Save(target.Identifier, snapshot);
            CopyParameters(snapshot, parameters);
            return true;
        }

        private void CopyParameters(TaskParametersBase source, TaskParametersBase target)
        {
            if (source.GetType() != target.GetType()) return;

            var properties = source.GetType().GetProperties();
            foreach (var prop in properties)
            {
                if (prop.CanRead && prop.CanWrite && prop.GetSetMethod() != null)
                {
                    var value = prop.GetValue(source);
                    prop.SetValue(target, value);
                }
            }
        }
    }
}
