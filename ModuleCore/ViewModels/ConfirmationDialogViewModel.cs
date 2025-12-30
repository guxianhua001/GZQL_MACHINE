using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuleCore.ViewModels
{
    public class ConfirmationDialogViewModel : BindableBase, IDialogAware
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

        public DelegateCommand YesCommand { get; }
        public DelegateCommand NoCommand { get; }

        public ConfirmationDialogViewModel()
        {
            YesCommand = new DelegateCommand(OnYes);
            NoCommand = new DelegateCommand(OnNo);
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            Title = parameters.GetValue<string>("title");
            Message = parameters.GetValue<string>("message");
        }

        public event Action<IDialogResult> RequestClose;


        private void OnYes()
        {
            RequestClose(new DialogResult(ButtonResult.Yes));
        }

        private void OnNo()
        {
            RequestClose(new DialogResult(ButtonResult.No));
        }

        public bool CanCloseDialog()
        {
            return true;
        }

        public void OnDialogClosed()
        {
        }

    }
}
