using Interfaces.Views;
using MaterialDesignThemes.Wpf;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace Interfaces.ViewModels
{
    public class MessageDialogViewModel : INotifyPropertyChanged
    {
        private Action<object?> _closeCallback;
        private Timer _autoCloseTimer;

        /// <summary>
        /// 设置对话框关闭回调
        /// </summary>
        public Action<object> CloseCallback
        {
            get => _closeCallback;
            set => SetField(ref _closeCallback, value, nameof(CloseCallback));
        }

        private bool _isYesButtonVisible;
        public bool IsYesButtonVisible
        {
            get => _isYesButtonVisible;
            set => SetField(ref _isYesButtonVisible, value);
        }

        private bool _isNoButtonVisible;
        public bool IsNoButtonVisible
        {
            get => _isNoButtonVisible;
            set => SetField(ref _isNoButtonVisible, value);
        }

        private bool _isProgressVisible;
        public bool IsProgressVisible
        {
            get => _isProgressVisible;
            set => SetField(ref _isProgressVisible, value);
        }

        private bool _isExtraButtonVisible;
        public bool IsExtraButtonVisible
        {
            get => _isExtraButtonVisible;
            set => SetField(ref _isExtraButtonVisible, value);
        }
        private string _extraButtonText = "附加操作";
        public string ExtraButtonText
        {
            get => _extraButtonText;
            set => SetField(ref _extraButtonText, value);
        }
        public bool IsYesButtonDefault { get; set; }
        public bool IsNoButtonDefault { get; set; }
        public bool IsExtraButtonDefault { get; set; }

        private string _title;
        public string Title
        {
            get => _title;
            set => SetField(ref _title, value);
        }

        private string _message;
        public string Message
        {
            get => _message;
            set => SetField(ref _message, value);
        }

        private PackIconKind? _iconKind;
        public PackIconKind? IconKind
        {
            get => _iconKind;
            set => SetField(ref _iconKind, value);
        }

        private string _yesButtonText;
        public string YesButtonText
        {
            get => _yesButtonText;
            set => SetField(ref _yesButtonText, value);
        }

        private string _noButtonText;
        public string NoButtonText
        {
            get => _noButtonText;
            set => SetField(ref _noButtonText, value);
        }

        private int? _autoCloseTimeout;
        public int? AutoCloseTimeout
        {
            get => _autoCloseTimeout;
            set
            {
                SetField(ref _autoCloseTimeout, value);
                SetupAutoCloseTimer();
            }
        }
        // 按钮索引映射属性
        public string[] ButtonResults { get; set; }
        private void SetupButtonMappings()
        {
            // 设置按钮结果映射
            ButtonResults = new string[3];
            if (IsYesButtonVisible)
                ButtonResults[0] = YesButtonText;
            if (IsNoButtonVisible)
                ButtonResults[1] = NoButtonText;
            if (IsExtraButtonVisible)
                ButtonResults[2] = ExtraButtonText;
        }
        public void SetButtonResults(string[] results)
        {
            if (results != null && results.Length > 0)
            {
                ButtonResults = results;
            }
        }

        #region 命令
        public ICommand YesCommand { get; }
        public ICommand NoCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ExtraCommand { get; }
        #endregion
        public MessageDialogViewModel()
        {
            // 命令返回按钮索引
            YesCommand = new RelayCommand(() => Close(0));   // 返回按钮索引
            NoCommand = new RelayCommand(() => Close(1));   // 返回按钮索引
            CancelCommand = new RelayCommand(() => Close(-1)); // -1 表示取消
            ExtraCommand = new RelayCommand(() => Close(2)); // 返回按钮索引
            // 初始值
            ButtonResults = new[] { "确认", "取消", "更多" };
            // 默认值
            IsYesButtonVisible = true;
            IsNoButtonVisible = true;
            IsExtraButtonVisible = false;  // 默认不显示
        }

        private void Close(object result)
        {
            // 停止自动关闭定时器
            _autoCloseTimer?.Dispose();

            // 执行关闭回调
            CloseCallback?.Invoke(result);
        }
        // 自动关闭
        private void SetupAutoCloseTimer()
        {
            if (AutoCloseTimeout.HasValue && AutoCloseTimeout > 0)
            {
                IsProgressVisible = true;
                _autoCloseTimer = new Timer(_ => {
                    Application.Current.Dispatcher.Invoke(() => Close(null));
                }, null, AutoCloseTimeout.Value, Timeout.Infinite);
            }
        }

        #region INotifyPropertyChanged 实现
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
        #endregion
    }
}
