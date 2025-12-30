// Framework/Services/ParameterDialogService.cs
using System;
using System.Threading.Tasks;
using Core.Abstraction;
using Prism.Services.Dialogs;
using Prism.Ioc;
using Framework.Views;

namespace Framework.Services
{
    public class ParameterDialogService : IParameterDialogService
    {
        private readonly IContainerProvider _container;

        public ParameterDialogService(IContainerProvider container)
        {
            _container = container;
        }

        public async Task<bool> ShowEditorDialog(string title, TaskParametersBase parameters,
            Action<TaskParametersBase> onSaved = null)
        {
            var dialogService = _container.Resolve<IDialogService>();
            var dialogParameters = new DialogParameters
            {
                { "title", title },
                { "parameters", parameters },
                { "onSaved", onSaved }  //传递回调函数
            };

            // 使用TaskCompletionSource将回调转换为Task
            var tcs = new TaskCompletionSource<bool>();

            // 正确的ShowDialog调用需要三个参数
            dialogService.ShowDialog(
               "ParameterEditor",
                dialogParameters,
                result =>
                {
                    // 当对话框关闭时，设置任务结果
                    tcs.SetResult(result.Result == ButtonResult.OK);
                });

            // 等待对话框关闭
            return await tcs.Task;
        }
    }
}
