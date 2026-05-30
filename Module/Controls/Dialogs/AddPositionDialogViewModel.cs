
using Prism.Regions;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;

namespace Framework.ViewModels
{
    public class AddPositionDialogViewModel :BindableBase, IDialogAware
    {
        public event Action<IDialogResult> RequestClose;

        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        private string _comment;
        public string Comment
        {
            get => _comment;
            set => SetProperty(ref _comment, value);
        }
        public DelegateCommand ConfirmCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public AddPositionDialogViewModel()
        {
            ConfirmCommand = new DelegateCommand(OnConfirm);
            CancelCommand = new DelegateCommand(OnCancel);
        }
        private void OnConfirm()
        {
            var parameters = new DialogParameters
            {
                { "name", Name },
                { "comment", Comment }
            };
            RequestClose?.Invoke(new DialogResult(ButtonResult.OK, parameters));
        }
        private void OnCancel()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        // IDialogAware 接口实现
        public string Title => "添加新位置";
        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }
        public void OnDialogOpened(IDialogParameters parameters) { }
    }
}
