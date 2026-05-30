using Core.Abstraction;
using Prism.Mvvm;

namespace MainApp.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        private readonly ILocalizationService _localization;
        private string _title;
        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        public MainWindowViewModel(ILocalizationService localizationService)
        {
            _localization = localizationService;
            _title = _localization.GetResourceOrDefault("MainWindow_LoadingTitle", "Loading...");
        }
    }
}
