using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Module.ViewModels
{
    public class SetupCalibrationViewModel : BindableBase
    {
        private readonly IDialogService _dialogService;

        private string _selectedHead = "Head 1";
        private double _weightCheck = 0.0;
        private double _lineWidth = 1.2;
        private double _dispensePressure = 0.4;
        private double _dispenseSpeed = 20.0;
        private string _selectedRecipe = "Standard";
        private string _platformStatus = "Ready";

        public ObservableCollection<string> RecipeOptions { get; } = new ObservableCollection<string>
        {
            "Standard",
            "High Viscosity",
            "Fast",
            "Precision"
        };

        public string SelectedHead
        {
            get => _selectedHead;
            set => SetProperty(ref _selectedHead, value);
        }

        public double WeightCheck
        {
            get => _weightCheck;
            set => SetProperty(ref _weightCheck, value);
        }

        public double LineWidth
        {
            get => _lineWidth;
            set => SetProperty(ref _lineWidth, value);
        }

        public double DispensePressure
        {
            get => _dispensePressure;
            set => SetProperty(ref _dispensePressure, value);
        }

        public double DispenseSpeed
        {
            get => _dispenseSpeed;
            set => SetProperty(ref _dispenseSpeed, value);
        }

        public string SelectedRecipe
        {
            get => _selectedRecipe;
            set => SetProperty(ref _selectedRecipe, value);
        }

        public string PlatformStatus
        {
            get => _platformStatus;
            set => SetProperty(ref _platformStatus, value);
        }

        // 命令
        public ICommand CleanNozzleCommand { get; }
        public ICommand CalibrateNozzleCommand { get; }
        public ICommand PurgeCommand { get; }
        public ICommand WetCheckCommand { get; }
        public ICommand WeightCheckCommand { get; }
        public ICommand InspectCommand { get; }
        public ICommand SaveRecipeCommand { get; }

        public SetupCalibrationViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;

            CleanNozzleCommand = new DelegateCommand(OnCleanNozzle);
            CalibrateNozzleCommand = new DelegateCommand(OnCalibrateNozzle);
            PurgeCommand = new DelegateCommand(OnPurge);
            WetCheckCommand = new DelegateCommand(OnWetCheck);
            WeightCheckCommand = new DelegateCommand(OnWeightCheck);
            InspectCommand = new DelegateCommand(OnInspect);
            SaveRecipeCommand = new DelegateCommand(OnSaveRecipe);
        }

        private void OnCleanNozzle()
        {
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Nozzle cleaning started." } }, null);
            // 模拟操作
            PlatformStatus = "Cleaning...";
            System.Threading.Tasks.Task.Delay(1000).ContinueWith(_ =>
            {
                PlatformStatus = "Ready";
                _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Nozzle cleaned." } }, null);
            });
        }

        private void OnCalibrateNozzle()
        {
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Nozzle calibration started." } }, null);
        }

        private void OnPurge()
        {
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Purging..." } }, null);
        }

        private void OnWetCheck()
        {
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Wet check performed." } }, null);
        }

        private void OnWeightCheck()
        {
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", $"Weight check: {WeightCheck} mg" } }, null);
        }

        private void OnInspect()
        {
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", $"Line width inspected: {LineWidth} mm" } }, null);
        }

        private void OnSaveRecipe()
        {
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", $"Recipe '{SelectedRecipe}' saved." } }, null);
        }
    }
}