
using MaterialDesignThemes.Wpf;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace Framework.ViewModels
{
    public class MessageDialogViewModel : BindableBase, IDialogAware
    {
        private Action<object?> _closeCallback;
        private Timer _autoCloseTimer;

        /// <summary>
        /// 设置对话框关闭回调
        /// </summary>
        public Action<object> CloseCallback
        {
            get => _closeCallback;
            set => SetProperty(ref _closeCallback, value, nameof(CloseCallback));
        }

        private bool _isYesButtonVisible;
        public bool IsYesButtonVisible
        {
            get => _isYesButtonVisible;
            set => SetProperty(ref _isYesButtonVisible, value);
        }

        private bool _isNoButtonVisible;
        public bool IsNoButtonVisible
        {
            get => _isNoButtonVisible;
            set => SetProperty(ref _isNoButtonVisible, value);
        }

        private bool _isProgressVisible;
        public bool IsProgressVisible
        {
            get => _isProgressVisible;
            set => SetProperty(ref _isProgressVisible, value);
        }

        private bool _isExtraButtonVisible;
        public bool IsExtraButtonVisible
        {
            get => _isExtraButtonVisible;
            set => SetProperty(ref _isExtraButtonVisible, value);
        }
        private string _extraButtonText = "附加操作";
        public string ExtraButtonText
        {
            get => _extraButtonText;
            set => SetProperty(ref _extraButtonText, value);
        }
        public bool IsYesButtonDefault { get; set; }
        public bool IsNoButtonDefault { get; set; }
        public bool IsExtraButtonDefault { get; set; }

        private string _title;
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private string _message;
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        private PackIconKind? _iconKind;
        public PackIconKind? IconKind
        {
            get => _iconKind;
            set => SetProperty(ref _iconKind, value);
        }

        private string _yesButtonText;
        public string YesButtonText
        {
            get => _yesButtonText;
            set => SetProperty(ref _yesButtonText, value);
        }

        private string _noButtonText;
        public string NoButtonText
        {
            get => _noButtonText;
            set => SetProperty(ref _noButtonText, value);
        }

        private int? _autoCloseTimeout;

        public event Action<IDialogResult> RequestClose;

        public int? AutoCloseTimeout
        {
            get => _autoCloseTimeout;
            set
            {
                SetProperty(ref _autoCloseTimeout, value);
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
        public DelegateCommand YesCommand { get; }
        public DelegateCommand NoCommand { get; }
        public DelegateCommand CancelCommand { get; }
        public DelegateCommand ExtraCommand { get; }
        #endregion
        public MessageDialogViewModel()
        {
            // 命令返回按钮索引
            YesCommand = new DelegateCommand(() => Close(0));   // 返回按钮索引
            NoCommand = new DelegateCommand(() => Close(1));   // 返回按钮索引
            CancelCommand = new DelegateCommand(() => Close(-1)); // -1 表示取消
            ExtraCommand = new DelegateCommand(() => Close(2)); // 返回按钮索引
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

            // 静态 DialogService 路径（Window.CloseCallback）
            CloseCallback?.Invoke(result);

            // Prism IDialogService 路径
            if (RequestClose != null)
            {
                var parameters = new DialogParameters();
                if (result is int index)
                    parameters.Add("buttonIndex", index);

                var buttonResult = result is int idx && idx >= 0 ? ButtonResult.OK : ButtonResult.Cancel;
                RequestClose.Invoke(new DialogResult(buttonResult, parameters));
            }
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

        #region IDialogAware 实现
        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        {
            _autoCloseTimer?.Dispose();
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.ContainsKey("message"))
                Message = parameters.GetValue<string>("message");
            if (parameters.ContainsKey("title"))
                Title = parameters.GetValue<string>("title");
            if (parameters.ContainsKey("iconKind"))
                IconKind = parameters.GetValue<PackIconKind>("iconKind");
            if (parameters.ContainsKey("yesButtonText"))
                YesButtonText = parameters.GetValue<string>("yesButtonText");
            if (parameters.ContainsKey("noButtonText"))
                NoButtonText = parameters.GetValue<string>("noButtonText");
            if (parameters.ContainsKey("extraButtonText"))
                ExtraButtonText = parameters.GetValue<string>("extraButtonText");
            if (parameters.ContainsKey("showYesButton"))
                IsYesButtonVisible = parameters.GetValue<bool>("showYesButton");
            if (parameters.ContainsKey("showNoButton"))
                IsNoButtonVisible = parameters.GetValue<bool>("showNoButton");
            if (parameters.ContainsKey("showExtraButton"))
                IsExtraButtonVisible = parameters.GetValue<bool>("showExtraButton");
            if (parameters.ContainsKey("autoCloseTimeout"))
                AutoCloseTimeout = parameters.GetValue<int>("autoCloseTimeout");
        }
        #endregion
    }
}
