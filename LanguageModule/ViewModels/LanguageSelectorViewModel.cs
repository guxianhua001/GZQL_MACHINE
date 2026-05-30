using System.Collections.ObjectModel;
using Core.Abstraction;
using Core.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;

namespace Language.ViewModels
{
    /// <summary>
    /// 语言选择器 ViewModel
    /// </summary>
    public class LanguageSelectorViewModel : BindableBase
    {
        private readonly ILocalizationService _localizationService;
        private readonly IEventAggregator _eventAggregator;
        private ObservableCollection<LanguageItem> _languages;
        private LanguageItem _selectedLanguage;

        public ObservableCollection<LanguageItem> Languages
        {
            get => _languages;
            private set => SetProperty(ref _languages, value);
        }

        public LanguageItem SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (SetProperty(ref _selectedLanguage, value) && value != null)
                {
                    _localizationService.SetLanguage(value.CultureCode);
                }
            }
        }

        /// <summary>语言选择器标题</summary>
        public string LanguageSelectorTitle => _localizationService.GetResource("LanguageSelector.Title");

        /// <summary>语言选择器提示</summary>
        public string LanguageSelectorTooltip => _localizationService.GetResource("LanguageSelector.Tooltip");

        public DelegateCommand RefreshCommand { get; }

        public LanguageSelectorViewModel(
            ILocalizationService localizationService,
            IEventAggregator eventAggregator)
        {
            _localizationService = localizationService;
            _eventAggregator = eventAggregator;
            RefreshCommand = new DelegateCommand(RefreshLanguages);

            // 订阅语言变更事件以刷新标题
            _eventAggregator.GetEvent<Core.Events.LanguageChangedEvent>()
                .Subscribe(OnLanguageChanged, ThreadOption.UIThread);
            _localizationService.LanguageChanged += OnLanguageChangedHandler;

            InitializeLanguages();
        }

        private void InitializeLanguages()
        {
            Languages = new ObservableCollection<LanguageItem>(_localizationService.SupportedLanguages);
            SelectedLanguage = _localizationService.CurrentLanguage;
        }

        private void RefreshLanguages()
        {
            Languages.Clear();
            foreach (var language in _localizationService.SupportedLanguages)
            {
                Languages.Add(language);
            }
        }

        private void OnLanguageChanged(string cultureCode)
        {
            RaisePropertyChanged(nameof(LanguageSelectorTitle));
            RaisePropertyChanged(nameof(LanguageSelectorTooltip));
            SelectedLanguage = _localizationService.CurrentLanguage;
        }

        private void OnLanguageChangedHandler(object sender, Core.Abstraction.LanguageChangedEventArgs e)
        {
            OnLanguageChanged(e.NewCultureCode);
        }
    }
}
