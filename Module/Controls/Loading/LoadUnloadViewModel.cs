using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using MaterialDesignThemes.Wpf;
using Module.Services;
using ModuleCore.Common.Authority;
using ModuleCore.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Module.ViewModels
{
    public class LoadUnloadViewModel : BindableBase
    {
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IAppSettingService _appConfig;
        private readonly ILoadUnloadController _controller;
        private readonly ILocalizationService _localization;
        private readonly ILoggerService _logger;
        private LoginModel _loginModel { get; set; }

        private bool _isMoving;
        public bool IsMoving
        {
            get => _isMoving;
            set => SetProperty(ref _isMoving, value);
        }

        private bool _yAxisReady;
        private bool _rxAxisReady;
        private bool _rzAxisReady;
        private bool _ryAxisReady;
        private string _processStatus;
        private string _vacuumStatusText;
        private System.Windows.Media.Brush _vacuumStatusColor = System.Windows.Media.Brushes.Red;
        private string _gripperVacuumStatusText;
        private System.Windows.Media.Brush _gripperVacuumStatusColor = System.Windows.Media.Brushes.Red;
        private string _realTimePositions;
        private string _gripperStatus;
        private ObservableCollection<string> _assySites = new ObservableCollection<string>
        {
            "ASSY_001", "ASSY_002", "ASSY_003", "ASSY_004", "ASSY_005", "ASSY_006"
        };
        private string _selectedSite = "ASSY_001";
        private bool _isAutoLoadingRunning;

        public ObservableCollection<StepStatusItem> StepStatusList { get; private set; } = new ObservableCollection<StepStatusItem>();

        #region 属性
        private bool _isAdmin;
        public bool IsAdmin
        {
            get => _loginModel?.HasPermission(Authority.Administrator) ?? false;
            private set
            {
                if (SetProperty(ref _isAdmin, value))
                    RaisePropertyChanged(nameof(CanEditParams));
            }
        }
        public bool CanEditParams => IsAdmin;
        public string ProcessStatus
        {
            get => _processStatus;
            set => SetProperty(ref _processStatus, value);
        }
        public bool YAxisReady
        {
            get => _yAxisReady;
            set => SetProperty(ref _yAxisReady, value);
        }
        public bool RxAxisReady
        {
            get => _rxAxisReady;
            set => SetProperty(ref _rxAxisReady, value);
        }
        public bool RzAxisReady
        {
            get => _rzAxisReady;
            set => SetProperty(ref _rzAxisReady, value);
        }
        public bool RyAxisReady
        {
            get => _ryAxisReady;
            set => SetProperty(ref _ryAxisReady, value);
        }
        public string VacuumStatusText
        {
            get => _vacuumStatusText;
            set => SetProperty(ref _vacuumStatusText, value);
        }
        public System.Windows.Media.Brush VacuumStatusColor
        {
            get => _vacuumStatusColor;
            set => SetProperty(ref _vacuumStatusColor, value);
        }
        public string RealTimePositions
        {
            get => _realTimePositions;
            set => SetProperty(ref _realTimePositions, value);
        }
        public string GripperStatus
        {
            get => _gripperStatus;
            set => SetProperty(ref _gripperStatus, value);
        }
        public ObservableCollection<string> AssySites
        {
            get => _assySites;
            set => SetProperty(ref _assySites, value);
        }
        public string SelectedSite
        {
            get => _selectedSite;
            set => SetProperty(ref _selectedSite, value);
        }
        public bool IsAutoLoadingRunning
        {
            get => _isAutoLoadingRunning;
            set => SetProperty(ref _isAutoLoadingRunning, value);
        }

        public string GripperVacuumStatusText
        {
            get => _gripperVacuumStatusText;
            set => SetProperty(ref _gripperVacuumStatusText, value);
        }

        public System.Windows.Media.Brush GripperVacuumStatusColor
        {
            get => _gripperVacuumStatusColor;
            set => SetProperty(ref _gripperVacuumStatusColor, value);
        }
        #endregion

        #region 命令属性
        public ICommand VacuumOnCommand { get; private set; }
        public ICommand VacuumOffCommand { get; private set; }
        public ICommand VacuumCheckCommand { get; private set; }
        public ICommand HomeAllCommand { get; private set; }
        public ICommand GoToPickCommand { get; private set; }
        public ICommand GoToScanCommand { get; private set; }
        public ICommand GoToUnloadCommand { get; private set; }
        public ICommand MoveToSelectedSiteCommand { get; private set; }
        public ICommand EditSitePositionCommand { get; private set; }
        public ICommand GripperOperationCommand { get; private set; }
        public ICommand GoToGripAngleCommand { get; private set; }
        public ICommand ClampCommand { get; private set; }
        public ICommand ReleaseCommand { get; private set; }
        public ICommand EditGripperParameterCommand { get; private set; }
        public ICommand AutoPickUpCommand { get; private set; }
        public ICommand AutoScanCommand { get; private set; }
        public ICommand View3DScanDataCommand { get; private set; }
        public ICommand AutoUnloadCommand { get; private set; }
        public ICommand GripperVacuumOnCommand { get; private set; }
        public ICommand GripperVacuumOffCommand { get; private set; }
        public ICommand GripperVacuumCheckCommand { get; private set; }
        public ICommand OpenStageAlignCommand { get; private set; }
        public ICommand EmergencyStopCommand { get; private set; }
        #endregion

        public LoadUnloadViewModel(
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            IAppSettingService appConfig,
            LoginModel loginModel,
            ILoadUnloadController controller,
            ILocalizationService localization,
            ILoggerService logger)
        {
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _loginModel = loginModel;
            _appConfig = appConfig;
            _controller = controller;
            _localization = localization;
            _logger = logger;

            _loginModel.PropertyChanged += LoginModel_PropertyChanged;

            InitializeCommands();
            InitializeStatus();
        }

        private void InitializeCommands()
        {
            VacuumOnCommand = ExecuteAsyncOperation(ChuckVacuumOnAction);
            VacuumOffCommand = ExecuteAsyncOperation(ChuckVacuumOffAction);
            VacuumCheckCommand = ExecuteAsyncOperation(ChuckVacuumCheckAction);
            HomeAllCommand = ExecuteAsyncOperation(PlatformHomeAction);
            GoToPickCommand = ExecuteAsyncOperation(MoveToPickPositionAction);
            GoToScanCommand = ExecuteAsyncOperation(MoveToScanPositionAction);
            GoToUnloadCommand = ExecuteAsyncOperation(MoveToUnloadPositionAction);
            MoveToSelectedSiteCommand = ExecuteAsyncOperation(MoveToSelectedSiteAction);
            EditSitePositionCommand = ExecuteAsyncOperation(EditSitePositionAction);
            GripperOperationCommand = ExecuteAsyncOperation(GripperOperationAction);
            GoToGripAngleCommand = ExecuteAsyncOperation(GoToGripAngleAction);
            ClampCommand = ExecuteAsyncOperation(ClampAction);
            ReleaseCommand = ExecuteAsyncOperation(ReleaseAction);
            EditGripperParameterCommand = ExecuteAsyncOperation(EditGripperParameterAction);
            AutoPickUpCommand = ExecuteAsyncOperation(AutoPickUpAction);
            AutoScanCommand = ExecuteAsyncOperation(AutoScanAction);
            View3DScanDataCommand = ExecuteAsyncOperation(View3DScanDataAction);
            AutoUnloadCommand = ExecuteAsyncOperation(AutoUnloadAction);
            GripperVacuumOnCommand = ExecuteAsyncOperation(GripperVacuumOnAction);
            GripperVacuumOffCommand = ExecuteAsyncOperation(GripperVacuumOffAction);
            GripperVacuumCheckCommand = ExecuteAsyncOperation(GripperVacuumCheckAction);
            OpenStageAlignCommand = new DelegateCommand(OnOpenStageAlign);
            EmergencyStopCommand = new DelegateCommand(OnEmergencyStop);
        }

        private void InitializeStatus()
        {
            ProcessStatus = _localization.GetResourceOrDefault("LoadUnload_Status_Standby", "Standby");
            VacuumStatusText = _localization.GetResourceOrDefault("LoadUnload_Vacuum_Off", "Off");
            GripperVacuumStatusText = _localization.GetResourceOrDefault("LoadUnload_Vacuum_Off", "Off");
            RealTimePositions = _localization.GetResourceOrDefault("LoadUnload_DefaultPosition", "Rx:0.00 Rz:0.00 Y:0.00 Ry:0.00");
            GripperStatus = _localization.GetResourceOrDefault("LoadUnload_DefaultGripperStatus", "0% (0N)");
            UpdateRealTimeStatus();
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += (s, e) => UpdateRealTimeStatus();
            timer.Start();
        }

        private async void UpdateRealTimeStatus()
        {
            try
            {
                var axisStatus = await _controller.GetAxisReadyStatusAsync();
                YAxisReady = axisStatus.TryGetValue("Y", out var y) && y;
                RxAxisReady = axisStatus.TryGetValue("Rx", out var rx) && rx;
                RzAxisReady = axisStatus.TryGetValue("Rz", out var rz) && rz;
                RyAxisReady = axisStatus.TryGetValue("Ry", out var ry) && ry;

                var positions = await _controller.GetRealTimePositionsAsync();
                RealTimePositions = $"Rx:{GetPosition(positions, "Rx"):F2} Rz:{GetPosition(positions, "Rz"):F2} Y:{GetPosition(positions, "Y"):F2} Ry:{GetPosition(positions, "Ry"):F2}";

                var vacStatus = _controller.GetVacuumStatus();
                VacuumStatusText = vacStatus == VacuumStatus.On
                    ? _localization.GetResourceOrDefault("LoadUnload_Status_Active", "Active")
                    : _localization.GetResourceOrDefault("LoadUnload_Vacuum_Off", "Off");
                VacuumStatusColor = vacStatus == VacuumStatus.On ? Brushes.Green : Brushes.Red;

                var gripVacStatus = _controller.GetGripperVacuumStatus();
                GripperVacuumStatusText = gripVacStatus == VacuumStatus.On
                    ? _localization.GetResourceOrDefault("LoadUnload_Status_Active", "Active")
                    : _localization.GetResourceOrDefault("LoadUnload_Vacuum_Off", "Off");
                GripperVacuumStatusColor = gripVacStatus == VacuumStatus.On ? Brushes.Green : Brushes.Red;
            }
            catch
            {
            }
        }

        private void LoginModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LoginModel.LoginUser) || e.PropertyName == nameof(LoginModel.HasPermission))
            {
                IsAdmin = _loginModel.HasPermission(Authority.Administrator);
            }
        }

        #region 辅助方法
        private double GetPosition(System.Collections.Generic.Dictionary<string, double> dict, string key)
        {
            return dict.TryGetValue(key, out var val) ? val : 0;
        }

        private DelegateCommand ExecuteAsyncOperation(Func<Task> execute, Func<bool> canExecute = null)
        {
            return new DelegateCommand(
                async () =>
                {
                    if (IsMoving) return;

                    if (!_controller.CanExecuteMotion())
                    {
                        ShowMessage(
                            _localization.GetResourceOrDefault("LoadUnload_Msg_MotionProhibited", "Manual operation is prohibited while the equipment is in operation!"),
                            PackIconKind.AlertCircle);
                        return;
                    }

                    IsMoving = true;
                    try
                    {
                        await execute().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        UpdateStepStatus($"{_localization.GetResourceOrDefault("LoadUnload_Step_Failed", "Operation failed")}: {ex.Message}");
                        ShowMessage($"{_localization.GetResourceOrDefault("LoadUnload_Step_Failed", "Operation failed")}: {ex.Message}", PackIconKind.Error);
                    }
                    finally
                    {
                        IsMoving = false;
                    }
                },
                canExecute ?? (() => !IsMoving)
            );
        }

        private void ShowMessage(string message, PackIconKind iconKind = PackIconKind.AlertCircle)
        {
            _dialogService.ShowDialog("NotificationDialog", new DialogParameters
            {
                { "title", _localization.GetResourceOrDefault("LoadUnload_Dialog_Note", "Note") },
                { "message", message },
                { "icon", iconKind }
            }, result => { });
        }

        private void UpdateStepStatus(string description, bool isWaiting = false)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            Application.Current.Dispatcher.Invoke(() =>
            {
                var existingItem = StepStatusList.FirstOrDefault(x => x.Description == description);
                if (existingItem != null)
                {
                    existingItem.IsCompleted = !isWaiting;
                    existingItem.IsCurrent = isWaiting;
                }
                else
                {
                    StepStatusList.Add(new StepStatusItem
                    {
                        Description = $"[{timestamp}] {description}\n",
                        IsCompleted = !isWaiting,
                        IsCurrent = isWaiting
                    });
                }
                if (StepStatusList.Count > 50) StepStatusList.RemoveAt(0);
            });
        }
        #endregion

        #region 动作实现
        private async Task ChuckVacuumOnAction()
        {
            UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_VacuumOn", "Turning vacuum ON"), true);
            await _controller.ChuckVacuumOnAsync();
            VacuumStatusColor = Brushes.Green;
            UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_VacuumOn", "Turning vacuum ON"), false);
        }

        private async Task ChuckVacuumOffAction()
        {
            UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_VacuumOff", "Turning vacuum OFF"), true);
            await _controller.ChuckVacuumOffAsync();
            VacuumStatusColor = Brushes.Red;
            UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_VacuumOff", "Turning vacuum OFF"), false);
        }

        private async Task ChuckVacuumCheckAction()
        {
            UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_VacuumCheck", "Checking vacuum"), true);
            var result = await _controller.ChuckVacuumCheckAsync();
            VacuumStatusText = result
                ? _localization.GetResourceOrDefault("LoadUnload_Status_Active", "Active")
                : _localization.GetResourceOrDefault("LoadUnload_Vacuum_Off", "Off");
            VacuumStatusColor = result ? Brushes.Green : Brushes.Red;
            UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_VacuumCheck", "Checking vacuum"), false);
        }

        private async Task PlatformHomeAction()
        {
            UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_HomeAll", "Homing platform"), true);
            await _controller.HomeAllAsync();
            UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_HomeAll", "Homing platform"), false);
        }

        private async Task MoveToPickPositionAction()
        {
            UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_MoveToPick", "Moving to pick position"), true);
            await _controller.MoveToPickPositionAsync();
            UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_MoveToPick", "Moving to pick position"), false);
        }

        private async Task MoveToScanPositionAction()
        {
            UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_MoveToScan", "Moving to scan position"), true);
            await _controller.MoveToScanPositionAsync();
            UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_MoveToScan", "Moving to scan position"), false);
        }

        private async Task MoveToUnloadPositionAction()
        {
            UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_MoveToUnload", "Moving to unload position"), true);
            await _controller.MoveToUnloadPositionAsync();
            UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_MoveToUnload", "Moving to unload position"), false);
        }

        private async Task MoveToSelectedSiteAction()
        {
            int pos = int.Parse(SelectedSite.Split('_')[1]);
            var stepDesc = _localization.GetResourceOrDefault("LoadUnload_Step_MoveToSite", "Moving to site") + $" {SelectedSite}";
            UpdateStepStatus(stepDesc, true);
            await _controller.MoveToAssemblyPositionAsync(pos);
            UpdateStepStatus(stepDesc, false);
        }

        private async Task EditSitePositionAction()
        {
            var stepDesc = _localization.GetResourceOrDefault("LoadUnload_Step_EditPosition", "Editing position") + $" {SelectedSite}";
            UpdateStepStatus(stepDesc, true);
            _dialogService.ShowDialog("NotificationDialog", new DialogParameters
            {
                { "title", _localization.GetResourceOrDefault("LoadUnload_Title", "Load / Unload") },
                { "message", _localization.GetResourceOrDefault("LoadUnload_Msg_EditPosition", "Edit position") + $" {SelectedSite}" },
                { "icon", PackIconKind.Pencil }
            }, result =>
            {
                UpdateStepStatus(stepDesc, false);
            });
            await Task.CompletedTask;
        }

        private async Task GripperOperationAction()
        {
            var stepDesc = _localization.GetResourceOrDefault("LoadUnload_Step_GripperOp", "Gripper operation");
            UpdateStepStatus(stepDesc, true);
            await _controller.ClampAsync();
            await Task.Delay(200);
            await _controller.ReleaseAsync();
            UpdateStepStatus(stepDesc, false);
        }

        private async Task GoToGripAngleAction()
        {
            UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_GripAngle", "Moving gripper to angle"), true);
            await _controller.MoveGripperToAngleAsync(90.0);
            UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_GripAngle", "Moving gripper to angle"), false);
        }

        private async Task ClampAction()
        {
            UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_Clamp", "Clamping gripper"), true);
            await _controller.ClampAsync();
            UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_Clamp", "Clamping gripper"), false);
        }

        private async Task ReleaseAction()
        {
            UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_Release", "Releasing gripper"), true);
            await _controller.ReleaseAsync();
            UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_Release", "Releasing gripper"), false);
        }

        private async Task EditGripperParameterAction()
        {
            var stepDesc = _localization.GetResourceOrDefault("LoadUnload_Step_EditGripper", "Editing gripper parameters");
            UpdateStepStatus(stepDesc, true);
            _dialogService.ShowDialog("NotificationDialog", new DialogParameters
            {
                { "title", _localization.GetResourceOrDefault("Gripper_Control_Title", "Gripper Control") },
                { "message", _localization.GetResourceOrDefault("LoadUnload_Msg_EditGripper", "Edit gripper parameters") },
                { "icon", PackIconKind.Pencil }
            }, result =>
            {
                UpdateStepStatus(stepDesc, false);
            });
            await Task.CompletedTask;
        }

        private async Task AutoPickUpAction()
        {
            var stepDesc = _localization.GetResourceOrDefault("LoadUnload_Step_AutoPickUp", "Auto Pick-Up");
            UpdateStepStatus(stepDesc, true);
            ProcessStatus = _localization.GetResourceOrDefault("LoadUnload_Status_PickUpInProgress", "Pick-Up in progress");
            await _controller.AutoPickUpAsync();
            ProcessStatus = _localization.GetResourceOrDefault("LoadUnload_Status_Standby", "Standby");
            UpdateStepStatus(stepDesc, false);
        }

        private async Task AutoScanAction()
        {
            var stepDesc = _localization.GetResourceOrDefault("LoadUnload_Step_AutoScan", "Auto Scanning");
            UpdateStepStatus(stepDesc, true);
            ProcessStatus = _localization.GetResourceOrDefault("LoadUnload_Status_ScanInProgress", "Scanning in progress");
            await _controller.AutoScanAsync();
            ProcessStatus = _localization.GetResourceOrDefault("LoadUnload_Status_Standby", "Standby");
            UpdateStepStatus(stepDesc, false);
        }

        private async Task View3DScanDataAction()
        {
            var stepDesc = _localization.GetResourceOrDefault("LoadUnload_Step_View3D", "Viewing 3D scan data");
            UpdateStepStatus(stepDesc, true);
            _eventAggregator.GetEvent<Prism.Events.PubSubEvent<string>>().Publish("NavigateToZScanView");
            UpdateStepStatus(stepDesc, false);
            await Task.CompletedTask;
        }

        private async Task AutoUnloadAction()
        {
            var stepDesc = _localization.GetResourceOrDefault("LoadUnload_Step_AutoUnload", "Auto Unload");
            UpdateStepStatus(stepDesc, true);
            ProcessStatus = _localization.GetResourceOrDefault("LoadUnload_Status_UnloadInProgress", "Unload in progress");
            await _controller.AutoUnloadAsync();
            ProcessStatus = _localization.GetResourceOrDefault("LoadUnload_Status_Standby", "Standby");
            UpdateStepStatus(stepDesc, false);
        }

        private async Task GripperVacuumOnAction()
        {
            var stepDesc = _localization.GetResourceOrDefault("LoadUnload_Step_GripperVacOn", "Turning gripper vacuum ON");
            UpdateStepStatus(stepDesc, true);
            await _controller.GripperVacuumOnAsync();
            GripperVacuumStatusText = _localization.GetResourceOrDefault("LoadUnload_Status_Active", "Active");
            GripperVacuumStatusColor = Brushes.Green;
            UpdateStepStatus(stepDesc, false);
        }

        private async Task GripperVacuumOffAction()
        {
            var stepDesc = _localization.GetResourceOrDefault("LoadUnload_Step_GripperVacOff", "Turning gripper vacuum OFF");
            UpdateStepStatus(stepDesc, true);
            await _controller.GripperVacuumOffAsync();
            GripperVacuumStatusText = _localization.GetResourceOrDefault("LoadUnload_Vacuum_Off", "Off");
            GripperVacuumStatusColor = Brushes.Red;
            UpdateStepStatus(stepDesc, false);
        }

        private async Task GripperVacuumCheckAction()
        {
            var stepDesc = _localization.GetResourceOrDefault("LoadUnload_Step_GripperVacCheck", "Checking gripper vacuum");
            UpdateStepStatus(stepDesc, true);
            var result = await _controller.GripperVacuumCheckAsync();
            GripperVacuumStatusText = result
                ? _localization.GetResourceOrDefault("LoadUnload_Status_Active", "Active")
                : _localization.GetResourceOrDefault("LoadUnload_Vacuum_Off", "Off");
            GripperVacuumStatusColor = result ? Brushes.Green : Brushes.Red;
            UpdateStepStatus(stepDesc, false);
        }

        private void OnOpenStageAlign()
        {
            _dialogService.ShowDialog("ProductCalibrationView", null, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    UpdateRealTimeStatus();
                    UpdateStepStatus("Stage alignment completed.");
                }
            });
        }

        private void OnEmergencyStop()
        {
            try
            {
                _controller.StopMotion();
                IsMoving = false;
                ProcessStatus = _localization.GetResourceOrDefault("LoadUnload_Status_EmergencyStop", "EMERGENCY STOP");
                UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_EmergencyStop", "Emergency stop activated"));
                _logger?.Info("LoadUnload: Emergency stop activated");
            }
            catch (Exception ex)
            {
                _logger?.Error($"LoadUnload: Emergency stop failed - {ex.Message}");
            }
        }
        #endregion
    }
}
