using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Module.ViewModels
{
    public class SimpleInputDialogViewModel : BindableBase, IDialogAware
    {
        private string _inputValue;
        public string InputValue { get => _inputValue; set => SetProperty(ref _inputValue, value); }
        public DelegateCommand OkCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public string Title => "Simple Input Dialog";

        public event Action<IDialogResult> RequestClose;

        public SimpleInputDialogViewModel()
        {
            OkCommand = new DelegateCommand(() =>
            {
                var p = new DialogParameters { { "value", InputValue } };
                RequestClose?.Invoke(new DialogResult(ButtonResult.OK, p));
            });
            CancelCommand = new DelegateCommand(() => RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel)));
        }
        public void OnDialogOpened(IDialogParameters parameters)
        {
            InputValue = parameters.GetValue<string>("value") ?? "";
        }
        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }
    }
}
