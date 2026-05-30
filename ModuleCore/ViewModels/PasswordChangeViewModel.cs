using Core.Abstraction;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;

namespace ModuleCore.ViewModels
{
    public class PasswordChangeViewModel : BindableBase, IDialogAware
    {
        private readonly ILocalizationService _localizationService;
        private string _Name;

        public event Action<IDialogResult> RequestClose;

        public string Name
        {
            get { return _Name; }
            set { SetProperty(ref _Name, value); }
        }

        public string Title
        {
            get
            {
                var template = _localizationService?.GetResourceOrDefault("PasswordChange_Title", "Change password for: {0}");
                return string.Format(template ?? "Change password for: {0}", Name);
            }
        }

        public PasswordChangeViewModel(ILocalizationService localizationService)
        {
            _localizationService = localizationService;
        }

        public bool CanCloseDialog()
        {
            return true;
        }

        public void Close()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Yes));
        }

        public void OnDialogClosed()
        {
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            Name = parameters.GetValue<string>("name");
            RaisePropertyChanged(nameof(Title));
        }
    }
}
