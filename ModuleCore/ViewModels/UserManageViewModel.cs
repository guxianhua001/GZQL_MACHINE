using ModuleCore.Models;
using Prism.Commands;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Linq;

namespace ModuleCore.ViewModels
{
    public class UserManageViewModel : BindableBase, IDialogAware
    {
        public LoginModel Model { get; set; }

        public UserManageViewModel(IContainerExtension container)
        {
            Model = container.Resolve<LoginModel>();
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
            ButtonResult result = ButtonResult.None;

            if (parameter?.ToLower() == "true")
            {
                result = ButtonResult.Yes;
            }
            else
            if (parameter?.ToLower() == "false")
            {
                Model.LoginUser = Model.UserList.FirstOrDefault();
                result = ButtonResult.No;
            }
            RaiseRequestClose(new DialogResult(result));
        }

        private DelegateCommand _UpdateConfigCommand;
        public DelegateCommand UpdateConfigCommand =>
             _UpdateConfigCommand ??= new DelegateCommand(ExecuteUpdateConfig);
        private void ExecuteUpdateConfig()
        {
            Model.SaveUsers();
        }

        //窗体关闭
        public virtual void RaiseRequestClose(IDialogResult dialogResult)
        {
            RequestClose?.Invoke(dialogResult);
        }

        private string _title = "用户管理";

        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        public event Action<IDialogResult> RequestClose;

        public bool CanCloseDialog()
        {
            return true;
        }

        public void OnDialogClosed()
        {
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
        }


    }
}