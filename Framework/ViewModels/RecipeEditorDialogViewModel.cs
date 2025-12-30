// Framework/ViewModels/RecipeEditorDialogViewModel.cs
using Prism.Mvvm;
using Prism.Commands;
using Prism.Services.Dialogs;
using System;

namespace Framework.ViewModels
{
    public class RecipeEditorDialogViewModel : BindableBase, IDialogAware
    {
        public RecipeEditorDialogViewModel()
        {
            SaveCommand = new DelegateCommand(ExecuteSave, CanSave)
                .ObservesProperty(() => RecipeName)
                .ObservesProperty(() => Description);

            CancelCommand = new DelegateCommand(ExecuteCancel);
        }

        #region 属性

        private string _title = "配方编辑器";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private string _recipeName;
        public string RecipeName
        {
            get => _recipeName;
            set => SetProperty(ref _recipeName, value);
        }

        private string _description;
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private string _mode = "Create";
        public string Mode
        {
            get => _mode;
            set => SetProperty(ref _mode, value);
        }

        #endregion

        #region 命令

        public DelegateCommand SaveCommand { get; }
        public DelegateCommand CancelCommand { get; }

        private bool CanSave() => !string.IsNullOrWhiteSpace(RecipeName);

        private void ExecuteSave()
        {
            var parameters = new DialogParameters
            {
                { "RecipeName", RecipeName.Trim() },
                { "Description", Description?.Trim() }
            };

            RequestClose?.Invoke(new DialogResult(ButtonResult.OK, parameters));
        }

        private void ExecuteCancel()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        #endregion

        #region IDialogAware 实现

        public event Action<IDialogResult> RequestClose;

        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        {
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.ContainsKey("Title"))
                Title = parameters.GetValue<string>("Title");

            if (parameters.ContainsKey("Mode"))
                Mode = parameters.GetValue<string>("Mode");

            if (parameters.ContainsKey("RecipeName"))
                RecipeName = parameters.GetValue<string>("RecipeName");

            if (parameters.ContainsKey("Description"))
                Description = parameters.GetValue<string>("Description");
        }

        #endregion
    }
}