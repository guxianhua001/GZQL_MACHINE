using Core.Abstraction;
using Core.Events;
using Prism.Events;
using Prism.Services.Dialogs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Framework.Services
{
    public class CancelableOperationService : ICancelableOperationService
    {
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;

        public CancelableOperationService(IDialogService dialogService, IEventAggregator eventAggregator)
        {
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
        }

        public async Task<bool> ExecuteWithCancellationAsync(
         string title,
         string message,
         Func<CancellationToken, IProgress<double>, IProgress<string>, Task<bool>> operation,
         bool showProgress = false,
         bool showStatus = false)
        {
            var operationId = Guid.NewGuid().ToString();
            var cancellationTokenSource = new CancellationTokenSource();

            // 使用 TaskCompletionSource 跟踪对话框关闭
            var dialogCloseTcs = new TaskCompletionSource<bool>();

            // 创建进度报告器 - 使用 WeakReference 避免内存泄漏
            var progress = new Progress<double>(value =>
            {
                _eventAggregator.GetEvent<OperationProgressEvent>()
                    .Publish(new OperationProgressData
                    {
                        Progress = value,
                        OperationId = operationId,
                        IsCompleted = false
                    });
            });

            var statusProgress = new Progress<string>(status =>
            {
                _eventAggregator.GetEvent<OperationProgressEvent>()
                    .Publish(new OperationProgressData
                    {
                        Status = status,
                        OperationId = operationId,
                        IsCompleted = false
                    });
            });

            try
            {
                // 1. 显示对话框
                var dialogParameters = new DialogParameters
                {
                    { "Title", title },
                    { "Message", message },
                    { "ShowProgress", showProgress },
                    { "ShowStatus", showStatus },
                    { "OperationId", operationId },
                    { "CancellationTokenSource", cancellationTokenSource }
                };

                // 2. 同时开始执行操作（并行执行）
                var operationTask = Task.Run(async () =>
                {
                    try
                    {
                        // 立即开始操作
                        var result = await operation(cancellationTokenSource.Token, progress, statusProgress);

                        // 操作完成后发布完成事件
                        _eventAggregator.GetEvent<OperationProgressEvent>()
                            .Publish(new OperationProgressData
                            {
                                Progress = result ? 100 : 0,
                                Status = result ? "操作完成" : "操作失败",
                                OperationId = operationId,
                                IsCompleted = true,
                                Success = result
                            });

                        return result;
                    }
                    catch (OperationCanceledException)
                    {
                        // 发布取消事件
                        _eventAggregator.GetEvent<OperationProgressEvent>()
                            .Publish(new OperationProgressData
                            {
                                Progress = 0,
                                Status = "操作已取消",
                                OperationId = operationId,
                                IsCompleted = true,
                                Success = false
                            });
                        return false;
                    }
                    catch (Exception ex)
                    {
                        // 发布异常事件
                        _eventAggregator.GetEvent<OperationProgressEvent>()
                            .Publish(new OperationProgressData
                            {
                                Progress = 0,
                                Status = $"操作异常: {ex.Message}",
                                OperationId = operationId,
                                IsCompleted = true,
                                Success = false
                            });
                        return false;
                    }
                });

                // 3. 显示对话框（非阻塞）
                var dialogResult = ButtonResult.None;
                _dialogService.ShowDialog("CancelableOperationDialog", dialogParameters, result =>
                {
                    dialogResult = result.Result;
                    dialogCloseTcs.SetResult(true);
                });

                // 4. 等待操作完成或取消
                var operationResult = await operationTask;

                // 5. 等待对话框完成关闭动画（如果操作完成）
                if (operationTask.IsCompleted)
                {
                    await Task.Delay(1500); // 给用户看到完成状态的时间
                }

                return operationResult;
            }
            finally
            {
                cancellationTokenSource.Dispose();
            }
        }
        private Task<ButtonResult> ShowDialogAsync(
            string title,
            string message,
            bool showProgress,
            bool showStatus,
            string operationId,
            CancellationTokenSource cancellationTokenSource,
            Func<Task<bool>> startOperation)
        {
            var tcs = new TaskCompletionSource<ButtonResult>();

            var parameters = new DialogParameters
            {
                { "Title", title },
                { "Message", message },
                { "ShowProgress", showProgress },
                { "ShowStatus", showStatus },
                { "OperationId", operationId },
                { "CancellationTokenSource", cancellationTokenSource },
                { "StartOperation", startOperation }  // 传递操作启动函数
            };

            _dialogService.ShowDialog("CancelableOperationDialog", parameters, result =>
            {
                tcs.SetResult(result.Result);
            });

            return tcs.Task;
        }
    }
}