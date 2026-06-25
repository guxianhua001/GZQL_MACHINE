using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using Framework.Mvvm;
using MaterialDesignThemes.Wpf;
using MotionControl.Interfaces;
using Module.Services;
using Module.Views;
using ModuleCore.Common.Authority;
using ModuleCore.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Ioc;
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
    /// <summary>
    /// 上下料 ViewModel：实现 IDestructible，离开页面时自动停止定时器刷新
    /// </summary>
    public class LoadUnloadViewModel : ViewModelBase
    {
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IAppSettingService _appConfig;
        private readonly ILoadUnloadController _controller;
        private readonly ILocalizationService _localization;
        private readonly ILoggerService _logger;
        private readonly IBaseDialogService _baseDialogService;
        private readonly IContainerProvider _containerProvider;
        private readonly IMotionInterlockService _motionInterlock;
        private LoginModel _loginModel { get; set; }

        /// <summary> 实时状态刷新定时器，页面销毁时停止 </summary>
        private System.Windows.Threading.DispatcherTimer _statusTimer;

        /// <summary> 防止 UpdateRealTimeStatus 异步重入的标志位 </summary>
        private volatile bool _isUpdatingStatus;

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
        private double _rxPosition;
        private double _rzPosition;
        private double _yPosition;
        private double _ryPosition;
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
        /// <summary> Rx 轴实时位置 </summary>
        public double RxPosition
        {
            get => _rxPosition;
            set => SetProperty(ref _rxPosition, value);
        }
        /// <summary> Rz 轴实时位置 </summary>
        public double RzPosition
        {
            get => _rzPosition;
            set => SetProperty(ref _rzPosition, value);
        }
        /// <summary> Y 轴实时位置 </summary>
        public double YPosition
        {
            get => _yPosition;
            set => SetProperty(ref _yPosition, value);
        }
        /// <summary> Ry 轴实时位置 </summary>
        public double RyPosition
        {
            get => _ryPosition;
            set => SetProperty(ref _ryPosition, value);
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
        public ICommand HomeAllCommand { get; private set; }
        public ICommand GoToPickCommand { get; private set; }
        public ICommand GoToScanCommand { get; private set; }
        public ICommand GoToUnloadCommand { get; private set; }
        public ICommand MoveToSelectedSiteCommand { get; private set; }
        public ICommand EditSitePositionCommand { get; private set; }
        public ICommand OpenGripperPanelCommand { get; private set; }
        public ICommand ClampCommand { get; private set; }
        public ICommand ReleaseCommand { get; private set; }
        public ICommand AutoPickUpCommand { get; private set; }
        public ICommand AutoScanCommand { get; private set; }
        public ICommand View3DScanDataCommand { get; private set; }
        public ICommand AutoUnloadCommand { get; private set; }
        public ICommand GripperVacuumOnCommand { get; private set; }
        public ICommand GripperVacuumOffCommand { get; private set; }
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
            ILoggerService logger,
            IBaseDialogService baseDialogService,
            IContainerProvider containerProvider,
            IMotionInterlockService motionInterlock)
        {
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _loginModel = loginModel;
            _appConfig = appConfig;
            _controller = controller;
            _localization = localization;
            _logger = logger;
            _baseDialogService = baseDialogService;
            _containerProvider = containerProvider;
            _motionInterlock = motionInterlock;

            _loginModel.PropertyChanged += LoginModel_PropertyChanged;

            InitializeCommands();
            InitializeStatus();
        }

        private void InitializeCommands()
        {
            VacuumOnCommand = ExecuteAsyncOperation(ChuckVacuumOnAction);
            VacuumOffCommand = ExecuteAsyncOperation(ChuckVacuumOffAction);
            HomeAllCommand = ExecuteAsyncOperation(PlatformHomeAction);
            GoToPickCommand = ExecuteAsyncOperation(MoveToPickPositionAction);
            GoToScanCommand = ExecuteAsyncOperation(MoveToScanPositionAction);
            GoToUnloadCommand = ExecuteAsyncOperation(MoveToUnloadPositionAction);
            MoveToSelectedSiteCommand = ExecuteAsyncOperation(MoveToSelectedSiteAction);
            EditSitePositionCommand = ExecuteAsyncOperation(EditSitePositionAction);
            OpenGripperPanelCommand = new DelegateCommand(OnOpenGripperPanel);
            ClampCommand = ExecuteAsyncOperation(ClampAction);
            ReleaseCommand = ExecuteAsyncOperation(ReleaseAction);
            AutoPickUpCommand = ExecuteAsyncOperation(AutoPickUpAction);
            AutoScanCommand = ExecuteAsyncOperation(AutoScanAction);
            View3DScanDataCommand = ExecuteAsyncOperation(View3DScanDataAction);
            AutoUnloadCommand = ExecuteAsyncOperation(AutoUnloadAction);
            GripperVacuumOnCommand = ExecuteAsyncOperation(GripperVacuumOnAction);
            GripperVacuumOffCommand = ExecuteAsyncOperation(GripperVacuumOffAction);
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

            // 实时刷新定时器，保存引用以便离开页面时停止
            _statusTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _statusTimer.Tick += (s, e) => UpdateRealTimeStatus();
            _statusTimer.Start();
        }

        /// <summary>
        /// 页面销毁时停止定时器并取消事件订阅，防止内存泄漏
        /// </summary>
        public override void Destroy()
        {
            _statusTimer?.Stop();
            _statusTimer = null;

            if (_loginModel != null)
                _loginModel.PropertyChanged -= LoginModel_PropertyChanged;

            base.Destroy();
        }

        /// <summary>
        /// 异步刷新轴回零状态和实时位置，带重入保护防止并发硬件查询
        /// DispatcherTimer 的 Tick 为 async void，必须防止上一轮 await 未完成时下一轮叠加
        /// </summary>
        private async void UpdateRealTimeStatus()
        {
            // 重入保护：上一轮尚未完成时跳过本次，避免硬件查询拥堵
            if (_isUpdatingStatus) return;
            _isUpdatingStatus = true;

            try
            {
                var axisStatus = await _controller.GetAxisReadyStatusAsync();
                YAxisReady = axisStatus.TryGetValue("Y", out var y) && y;
                RxAxisReady = axisStatus.TryGetValue("Rx", out var rx) && rx;
                RzAxisReady = axisStatus.TryGetValue("Rz", out var rz) && rz;
                RyAxisReady = axisStatus.TryGetValue("Ry", out var ry) && ry;

                // 逐轴更新实时位置
                var positions = await _controller.GetRealTimePositionsAsync();
                RxPosition = GetPosition(positions, "Rx");
                RzPosition = GetPosition(positions, "Rz");
                YPosition = GetPosition(positions, "Y");
                RyPosition = GetPosition(positions, "Ry");
                RealTimePositions = $"Rx:{RxPosition:F2} Rz:{RzPosition:F2} Y:{YPosition:F2} Ry:{RyPosition:F2}";

                // 通过定时器轮询真空反馈 DI 状态，自动更新指示灯
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

                // 实时夹爪位置（从 IGripperService 硬件读取）
                var gripperPos = _controller.GetGripperPosition();
                GripperStatus = $"{gripperPos:F1} mm";
            }
            catch
            {
            }
            finally
            {
                _isUpdatingStatus = false;
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

        /// <summary>
        /// 异步操作包装器：管理 IsMoving 状态和异常处理
        /// 注意：WPF 场景下不使用 ConfigureAwait(false)，确保 IsMoving 在 UI 线程设置，
        /// 避免 PropertyChanged 在非 UI 线程触发导致绑定失效
        /// </summary>
        private DelegateCommand ExecuteAsyncOperation(Func<Task> execute, Func<bool> canExecute = null)
        {
            return new DelegateCommand(
                async () =>
                {
                    if (IsMoving) return;

                    if (!_controller.CanExecuteMotion())
                    {
                        ShowMessage(
                            _motionInterlock.GetBlockedMessage(),
                            PackIconKind.AlertCircle);
                        return;
                    }

                    IsMoving = true;
                    try
                    {
                        await execute();
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

        /// <summary>
        /// 平台回零：完成后立即刷新轴状态指示器，不等待下一轮定时器
        /// </summary>
        private async Task PlatformHomeAction()
        {
            UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_HomeAll", "Homing platform"), true);
            await _controller.HomeAllAsync();
            UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_HomeAll", "Homing platform"), false);
            // 回零完成后立即刷新轴状态，确保指示器同步更新
            UpdateRealTimeStatus();
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

        /// <summary>
        /// 弹出电爪操作面板对话框
        /// </summary>
        private void OnOpenGripperPanel()
        {
            _dialogService.ShowDialog("GripperControlView", null, result =>
            {
                UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_GripperPanelClosed", "Gripper panel closed"));
            });
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

        private async void OnOpenStageAlign()
        {
            // 通过容器解析 ViewModel，创建 View 并绑定（使用 BaseDialogWindow 跟随主题切换）
            var viewModel = _containerProvider.Resolve<ProductCalibrationViewModel>();
            var view = new ProductCalibrationView { DataContext = viewModel };

            // 使用 BaseDialogService 弹出，风格统一跟随主题
            var title = _localization.GetResourceOrDefault("ProductCalib_Title", "Product Align");
            await _baseDialogService.ShowDialog(view, title, "CameraBurst");

            // 关闭后更新状态
            UpdateRealTimeStatus();
            UpdateStepStatus(_localization.GetResourceOrDefault("LoadUnload_Step_StageAlignDone", "Stage alignment completed."));
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
