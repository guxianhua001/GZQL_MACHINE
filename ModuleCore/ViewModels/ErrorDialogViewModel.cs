using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;

namespace ModuleCore.ViewModels
{
    public class ErrorDialogViewModel : BindableBase, IDialogAware
    {
        public event Action<IDialogResult> RequestClose;

        public string Title => "错误";

        private string _dialogTitle;
        public string DialogTitle
        {
            get => _dialogTitle;
            set => SetProperty(ref _dialogTitle, value);
        }

        private string _message = "发生未知错误";
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        private bool _isCritical;
        public bool IsCritical
        {
            get => _isCritical;
            set => SetProperty(ref _isCritical, value);
        }

        public DelegateCommand CloseDialogCommand { get; }

        public ErrorDialogViewModel()
        {
            CloseDialogCommand = new DelegateCommand(CloseDialog);
        }

        private void CloseDialog()
        {
            RequestClose?.Invoke(new DialogResult());
        }

        public bool CanCloseDialog()
        {
            return true;
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.ContainsKey("title"))
                DialogTitle = parameters.GetValue<string>("title");

            if (parameters.ContainsKey("message"))
                Message = parameters.GetValue<string>("message");

            if (parameters.ContainsKey("Critical"))
                IsCritical = parameters.GetValue<bool>("Critical");
        }

        public void OnDialogClosed()
        {
            // 清理操作
        }
    }
}
