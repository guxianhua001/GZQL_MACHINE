using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;

namespace Framework.ViewModels
{
    public class BusyIndicatorViewModel : BindableBase, INavigationAware
    {
        private readonly IEventAggregator _eventAggregator;

        #region Fields and Constructor

        public BusyIndicatorViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            CancelCommand = new DelegateCommand(ExecuteCancel, CanExecuteCancel)
                .ObservesProperty(() => CanCancel);

            // 初始化属性
            ProgressValue = 0;
            IsIndeterminate = true;
            StatusMessage = "正在准备保存参数...";
            CurrentOperation = "请稍候...";
            CanCancel = true;
        }

        #endregion

        #region Properties

        private int _progressValue;
        public int ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        private bool _isIndeterminate;
        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set => SetProperty(ref _isIndeterminate, value);
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string _currentOperation;
        public string CurrentOperation
        {
            get => _currentOperation;
            set => SetProperty(ref _currentOperation, value);
        }

        private bool _canCancel;
        public bool CanCancel
        {
            get => _canCancel;
            set => SetProperty(ref _canCancel, value);
        }

        #endregion

        #region Commands

        public DelegateCommand CancelCommand { get; private set; }

        private void ExecuteCancel()
        {
            // 发布取消事件
            // _eventAggregator.GetEvent<CancelOperationEvent>().Publish();
            // 或者执行其他取消逻辑
            CanCancel = false;
            StatusMessage = "正在取消操作...";
        }

        private bool CanExecuteCancel()
        {
            return CanCancel;
        }

        #endregion

        #region 进度管理方法

        public void UpdateProgress(int progress, string operation = null)
        {
            ProgressValue = progress;
            IsIndeterminate = false;

            if (!string.IsNullOrEmpty(operation))
            {
                CurrentOperation = operation;
            }

            StatusMessage = $"正在保存参数... ({progress}%)";
        }

        public void SetIndeterminate(string message = null)
        {
            IsIndeterminate = true;
            if (!string.IsNullOrEmpty(message))
            {
                StatusMessage = message;
            }
        }

        public void SetCompleted(string message = "参数保存完成")
        {
            ProgressValue = 100;
            IsIndeterminate = false;
            StatusMessage = message;
            CurrentOperation = "完成";
            CanCancel = false;
        }

        public void SetFailed(string errorMessage)
        {
            StatusMessage = $"保存失败: {errorMessage}";
            CurrentOperation = "错误";
            CanCancel = true;
        }

        #endregion

        #region INavigationAware Implementation

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            if (navigationContext.Parameters.ContainsKey("recipeName"))
            {
                var recipeName = navigationContext.Parameters["recipeName"] as string;
                if (!string.IsNullOrEmpty(recipeName))
                {
                    StatusMessage = $"正在保存配方 '{recipeName}' 的参数...";
                }
            }
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            // 清理资源
            ProgressValue = 0;
            IsIndeterminate = true;
            CanCancel = true;
        }

        #endregion
    }
}