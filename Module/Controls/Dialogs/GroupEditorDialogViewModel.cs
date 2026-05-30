using Prism.Mvvm;
using Prism.Commands;
using Prism.Services.Dialogs;
using Module.Models;
using System;
using Core.Models;

namespace Module.ViewModels
{
    public class GroupEditorDialogViewModel : BindableBase, IDialogAware
    {
        private string _title;
        private string _groupName;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string GroupName
        {
            get => _groupName;
            set => SetProperty(ref _groupName, value);
        }

        public DelegateCommand SaveCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public event Action<IDialogResult> RequestClose;

        public GroupEditorDialogViewModel()
        {
            SaveCommand = new DelegateCommand(OnSave);
            CancelCommand = new DelegateCommand(OnCancel);
        }

        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            var existingGroup = parameters.GetValue<Site>("group");
            if (existingGroup == null)
            {
                Title = "Add Group";
                GroupName = "";
            }
            else
            {
                Title = "Edit Group";
                GroupName = existingGroup.Name;
            }
        }

        private void OnSave()
        {
            var result = new DialogResult(ButtonResult.OK);
            var group = new Site { Name = GroupName };
            result.Parameters.Add("group", group);
            RequestClose?.Invoke(result);
        }

        private void OnCancel()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }
    }
}