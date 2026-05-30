using Core.Abstraction;
using Prism.Mvvm;

namespace Module.ViewModels
{
    public class MaintenanceViewModel : BindableBase
    {
        private readonly ILocalizationService _localizationService;

        private int _selectedTabIndex;
        /// <summary>
        /// 当前选中的选项卡索引
        /// </summary>
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        public MaintenanceViewModel(ILocalizationService localizationService)
        {
            _localizationService = localizationService;
            _selectedTabIndex = 0;
        }
    }
}
