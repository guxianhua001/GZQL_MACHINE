using Prism.Mvvm;
using Prism.Commands;
using Prism.Services.Dialogs;
using Module.Models;
using System;
using Core.Models;

namespace Module.ViewModels
{
    public class AxisEditorDialogViewModel : BindableBase, IDialogAware
    {
        private AxisConstant _axis;

        public string Title => "Edit Axis";

        public string Group { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        public DelegateCommand SaveCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public event Action<IDialogResult> RequestClose;

        public AxisEditorDialogViewModel()
        {
            SaveCommand = new DelegateCommand(OnSave);
            CancelCommand = new DelegateCommand(OnCancel);
        }

        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            _axis = parameters.GetValue<AxisConstant>("axis") ?? new AxisConstant();
            Group = _axis.Group;
            Name = _axis.Name;
            Description = _axis.Description;
        }

        private void OnSave()
        {
            _axis.Group = Group;
            _axis.Name = Name;
            _axis.Description = Description;
            var result = new DialogResult(ButtonResult.OK);
            result.Parameters.Add("axis", _axis);
            RequestClose?.Invoke(result);
        }

        private void OnCancel()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }
    }
}