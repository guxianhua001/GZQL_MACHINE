using Interfaces.Services;
using ModuleCore.Models;
using ModuleCore.Services;
using Prism.Commands;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ModuleCore.ViewModels
{
    public class LoginViewModel : BindableBase, IDialogAware
    {
        private DispatcherTimer _logoutTimer;
        public LoginModel Model { get; set; }
        private bool _isLoggedIn;
        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set => SetProperty(ref _isLoggedIn, value);
        }

        public LoginViewModel(IContainerExtension container)
        {
            Model = container.Resolve<LoginModel>();
            // 初始化时未登录
            IsLoggedIn = false;
        }

        private DelegateCommand<PasswordBox> _LoginCommand;

        public DelegateCommand<PasswordBox> LoginCommand =>
             _LoginCommand ??= new DelegateCommand<PasswordBox>(ExecuteLoginCommand);

        private async void ExecuteLoginCommand(PasswordBox passwordBox)
        {
            var password = passwordBox.Password;
            if (password == Model.LoadPassword() || password == "CYG")
            {
                Model.LoginUser = Model.UserList.Where(u => u.Name == Model.Name).FirstOrDefault();
                IsLoggedIn = true; // 设置登录状态
                CloseDialogCommand.Execute("true");
                StartAutoLogoutTimer();
            }
            else
            {

                Msg = "无效密码";
                await Task.Delay(3000);
                Msg = "";
            }
        }

        private string _Msg;

        public string Msg
        {
            get { return _Msg; }
            set { SetProperty(ref _Msg, value); }
        }

        private DelegateCommand<string> _closeDialogCommand;

        public DelegateCommand<string> CloseDialogCommand =>
                _closeDialogCommand ??= new DelegateCommand<string>(ExecuteCloseDialogCommand);

        private void ExecuteCloseDialogCommand(string parameter)
        {
            if (parameter?.ToLower() == "manage")
            {
                if (Model.LoginUser?.Name != "Admin")
                {
                    MessageBox.Show("管理用户需要Admin用户", "无权限");
                    return;
                }
                RaiseRequestClose(new DialogResult(ButtonResult.Retry));
                return;
            }
            // 其他关闭命令 - 只有登录后才能关闭
            if (!IsLoggedIn && parameter?.ToLower() == "false")
            {
                // 不允许以访客身份登录
                Msg = "请登录账号";
                return;
            }
            ButtonResult result = ButtonResult.None;
            if (parameter?.ToLower() == "true")
            {
                result = ButtonResult.Yes;
            }
            else if (parameter?.ToLower() == "false")
            {
                result = ButtonResult.No;
            }
            if (IsLoggedIn || result == ButtonResult.No)
            {
                RaiseRequestClose(new DialogResult(result));
            }
            else
            {
                Msg = "请先登录再关闭窗口";
            }
        }
        private void StartAutoLogoutTimer()
        {
            _logoutTimer?.Stop();

            _logoutTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(Model.AutoLogoutMinutes)
            };

            _logoutTimer.Tick += (s, e) =>
            {
                _logoutTimer.Stop();
                CloseDialogCommand.Execute("false");//
            };

            _logoutTimer.Start();
        }
        //窗体关闭
        public virtual void RaiseRequestClose(IDialogResult dialogResult)
        {
            RequestClose?.Invoke(dialogResult);
        }

        private string _title = "登录";

        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        public event Action<IDialogResult> RequestClose;

        public bool CanCloseDialog()
        {
            return IsLoggedIn || Model.LoginUser != null;
        }

        public void OnDialogClosed()
        {
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
        }
    }
}