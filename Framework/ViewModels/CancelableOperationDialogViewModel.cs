using Core.Events;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Threading;
using System.Threading.Tasks;  // 添加这个命名空间
using System.Windows;
using System.Windows.Input;

namespace Framework.ViewModels
{
    public class CancelableOperationDialogViewModel : BindableBase, IDialogAware
    {
        private string _title = "操作执行中";
        private string _message = "请稍候...";
        private double _progress;
        private string _progressText = "0%";  // 新增字段
        private string _statusMessage = string.Empty;
        private bool _showProgress = false;
        private bool _showStatus = false;
        private string _operationId;
        private bool _isOperationCompleted = false;

        private CancellationTokenSource _cancellationTokenSource;

        public event Action<IDialogResult> RequestClose;

        private readonly IEventAggregator _eventAggregator;

        public CancelableOperationDialogViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            CancelCommand = new DelegateCommand(ExecuteCancel, CanExecuteCancel);
            _cancellationTokenSource = new CancellationTokenSource();

            // 订阅操作进度事件
            _eventAggregator.GetEvent<OperationProgressEvent>()
                .Subscribe(OnOperationProgressUpdated, ThreadOption.UIThread);
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        public double Progress
        {
            get => _progress;
            set
            {
                if (SetProperty(ref _progress, value))
                {
                    // 当Progress变化时，自动更新ProgressText
                    ProgressText = $"{value:F1}%";
                }
            }
        }

        public string ProgressText
        {
            get => _progressText;
            private set => SetProperty(ref _progressText, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool ShowProgress
        {
            get => _showProgress;
            set => SetProperty(ref _showProgress, value);
        }

        public bool ShowStatus
        {
            get => _showStatus;
            set => SetProperty(ref _showStatus, value);
        }

        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public ICommand CancelCommand { get; }

        private void OnOperationProgressUpdated(OperationProgressData data)
        {
            if (data.OperationId != _operationId)
                return;

            if (data.IsCompleted)
            {
                _isOperationCompleted = true;

                // 更新最终状态
                if (!string.IsNullOrEmpty(data.Status))
                    StatusMessage = data.Status;

                if (ShowProgress && data.Progress >= 0)
                    Progress = data.Progress;

                // 延迟关闭对话框
                Task.Delay(1500).ContinueWith(_ =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CloseDialog(data.Success);
                    });
                });
            }
            else
            {
                // 只更新有值的属性，避免覆盖
                if (ShowProgress && data.Progress > 0)  // 只有大于0才更新进度
                {
                    Progress = data.Progress;
                }

                if (!string.IsNullOrEmpty(data.Status))
                    StatusMessage = data.Status;
            }
        }
        private bool CanExecuteCancel()
        {
            return !_isOperationCompleted;
        }

        public bool CanCloseDialog()
        {
            return true;
        }

        public void OnDialogClosed()
        {
            _cancellationTokenSource?.Dispose();
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.ContainsKey("Title"))
                Title = parameters.GetValue<string>("Title");

            if (parameters.ContainsKey("Message"))
                Message = parameters.GetValue<string>("Message");

            if (parameters.ContainsKey("ShowProgress"))
                ShowProgress = parameters.GetValue<bool>("ShowProgress");

            if (parameters.ContainsKey("ShowStatus"))
                ShowStatus = parameters.GetValue<bool>("ShowStatus");

            if (parameters.ContainsKey("OperationId"))
                _operationId = parameters.GetValue<string>("OperationId");

            if (parameters.ContainsKey("CancellationTokenSource"))
                _cancellationTokenSource = parameters.GetValue<CancellationTokenSource>("CancellationTokenSource");

            // 初始化进度显示
            if (ShowProgress)
            {
                Progress = 0;
                StatusMessage = "正在初始化...";
            }
        }

        public void UpdateProgress(double progress)
        {
            if (ShowProgress)
                Progress = progress;
        }

        public void UpdateStatus(string status)
        {
            StatusMessage = status;
        }

        private void ExecuteCancel()
        {
            if (_isOperationCompleted)
                return;

            _cancellationTokenSource.Cancel();
            var result = new DialogResult(ButtonResult.Cancel);
            RequestClose?.Invoke(result);
        }

        // 手动关闭对话框（操作完成时调用）
        public void CloseDialog(bool success = true)
        {
            var result = new DialogResult(success ? ButtonResult.OK : ButtonResult.Cancel);
            RequestClose?.Invoke(result);
        }
    }
}