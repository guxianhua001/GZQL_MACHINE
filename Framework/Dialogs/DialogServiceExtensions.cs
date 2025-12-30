using Prism.Services.Dialogs;
using System.Windows;

namespace Framework.Dialogs
{
    public static class DialogServiceExtensions
    {
        public static Task<IDialogResult> ShowDialogAsync(this IDialogService dialogService, string name, IDialogParameters parameters = null)
        {
            var tcs = new TaskCompletionSource<IDialogResult>();

            try
            {
                // 确保在UI线程调用
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    // 当前就在UI线程
                    dialogService.ShowDialog(name, parameters, result =>
                    {
                        tcs.TrySetResult(result);
                    });
                }
                else
                {
                    // 从非UI线程调用，需要调度到UI线程
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        dialogService.ShowDialog(name, parameters, result =>
                        {
                            tcs.TrySetResult(result);
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }

            return tcs.Task;
        }
    }
}