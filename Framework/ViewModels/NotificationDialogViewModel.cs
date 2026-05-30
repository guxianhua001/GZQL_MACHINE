using MaterialDesignThemes.Wpf;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.ViewModels
{
    public class NotificationDialogViewModel : BindableBase, IDialogAware
    {

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
        private PackIconKind _iconKind = PackIconKind.AlertCircle;
        public PackIconKind IconKind
        {
            get => _iconKind;
            set => SetProperty(ref _iconKind, value);
        }
        public DelegateCommand ConfirmCommand { get; }

        public NotificationDialogViewModel()
        {
            ConfirmCommand = new DelegateCommand(OnYes);
        }
        private void OnYes()
        {
            // 使用ButtonResult.OK
            RequestClose?.Invoke(new DialogResult(ButtonResult.OK));
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
            // 参数验证防错处理
            if (parameters == null) return;
            parameters.TryGetValue<string>("title", out var title);
            parameters.TryGetValue<string>("message", out var message);
            parameters.TryGetValue<PackIconKind>("icon", out var icon);
            Title = title ?? "系统提示";
            Message = message ?? "操作已完成";
            IconKind = icon;
        }
    }
}
