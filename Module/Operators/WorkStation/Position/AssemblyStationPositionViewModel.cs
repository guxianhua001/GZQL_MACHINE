using Core.Abstraction;
using Core.Abstractions.IConfiguration;
using Core.Utilities;
using Framework.Mvvm;
using ModuleCore.Models;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using Recipe.Interfaces;
using Stations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.ViewModels
{
    public class AssemblyStationPositionViewModel : RegionViewModelBase
    {
        private PositionViewModel _viewModel;

        public PositionViewModel ViewModel
        {
            get => _viewModel;
            set => SetProperty(ref _viewModel, value);
        }
        public AssemblyStationPositionViewModel(
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
            ViewModel.TaskId = 3;
            ViewModel.AxisIdGroup = new[] { 0, 1, 5, 12, 13 };
        }
    }
}
