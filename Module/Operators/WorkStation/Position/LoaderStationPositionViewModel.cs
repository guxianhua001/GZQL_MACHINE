using Core.Abstraction;
using Core.Abstractions.IConfiguration;
using Core.Utilities;
using Framework.Mvvm;
using ModuleCore;
using ModuleCore.Models;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using Recipe.Interfaces;

namespace Framework.ViewModels
{
    public class LoaderStationPositionViewModel : RegionViewModelBase
    {
        private PositionViewModel _viewModel;

        public PositionViewModel ViewModel
        {
            get => _viewModel;
            set => SetProperty(ref _viewModel, value);
        }
        public LoaderStationPositionViewModel(
            ILoggerService loggerService,
            IDialogService dialogService, 
            IRegionManager regionManager, 
            IEventAggregator eventAggregator,
            IConfigurationService configService,
            LoginModel loginModel,
            IRecipeManager recipeManager,
            IRecipeStorage recipeStorage,
            IAppConfig appConfig) : base(regionManager)
        {
            ViewModel = new PositionViewModel(
                loggerService, 
                dialogService, 
                eventAggregator, 
                configService, 
                loginModel,
                recipeManager,
                recipeStorage,
                appConfig);
            ViewModel.TaskId = 1;
            ViewModel.AxisIdGroup = new[] { 9, 10, 11 };
        }
    }
}
