using StationTasks.Events;
using MotionControl.Events;
using MotionControl.Interfaces;
using MotionControl.Models;
using MotionControl.Views;
using Core.Abstraction;
using Prism.Commands;
using Prism.Events;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
namespace Module.ViewModels
{
    public class OverViewModel : BindableBase, INavigationAware
    {
        private readonly IRegionManager _regionManager;
        private readonly IEventAggregator _ea;
        private readonly IContainerProvider _container;
        private readonly ITaskManager _taskManager;
        private readonly ISystemStateService _systemState;
        private readonly ISpeedOverrideService _speedOverride;
        private readonly IDialogService _dialogService;
        private readonly ILocalizationService _localization;
        private readonly DispatcherTimer _timer;
        #region 绑定属性
        private DateTime _systemTime;
        public DateTime SystemTime
        {
            get => _systemTime;
            set => SetProperty(ref _systemTime, value);
        }
        // 三色灯颜色 (红: #F44336, 绿: #4CAF50, 橙黄: #FF9800, 灰: #9E9E9E)
        private string _trioLightColor = "#9E9E9E";
        public string TrioLightColor
        {
            get => _trioLightColor;
            set => SetProperty(ref _trioLightColor, value);
        }
        // 三色灯文本
        private string _trioLightText;
        public string TrioLightText
        {
            get => _trioLightText;
            set => SetProperty(ref _trioLightText, value);
        }
        // 安全门颜色
        private string _doorColor = "#4CAF50";
        public string DoorColor
        {
            get => _doorColor;
            set => SetProperty(ref _doorColor, value);
        }
        // 判断是否正在运行（用于控制暂停/恢复按钮的显隐和文本）
        private bool _isSystemRunning = false;
        public bool IsSystemRunning
        {
            get => _isSystemRunning;
            set => SetProperty(ref _isSystemRunning, value);
        }
        // 单步模式开关
        private bool _isSingleStepMode = false;
        public bool IsSingleStepMode
        {
            get => _isSingleStepMode;
            set
            {
                if (SetProperty(ref _isSingleStepMode, value))
                    SingleStepButtonText = value
                        ? _localization.GetResource("OverView_Btn_SingleStep_On")
                        : _localization.GetResource("OverView_Btn_SingleStep_Off");
            }
        }
        private string _singleStepButtonText;
        public string SingleStepButtonText
        {
            get => _singleStepButtonText;
            set => SetProperty(ref _singleStepButtonText, value);
        }
        // 速度百分比
        private double _speedPercent = 100;
        public double SpeedPercent
        {
            get => _speedPercent;
            set
            {
                if (SetProperty(ref _speedPercent, Math.Clamp(Math.Round(value), 1, 100)))
                {
                    _speedOverride.SpeedPercent = _speedPercent;
                    SpeedDisplayText = $"{_localization.GetResource("OverView_Speed")}: {_speedPercent:N0}%";
                }
            }
        }
        private string _speedDisplayText;
        public string SpeedDisplayText
        {
            get => _speedDisplayText;
            set => SetProperty(ref _speedDisplayText, value);
        }
        #endregion
        #region 命令
        public DelegateCommand InitializeCommand { get; }
        public DelegateCommand StartCommand { get; }
        public DelegateCommand PauseCommand { get; }
        public DelegateCommand ResumeCommand { get; }
        public DelegateCommand StopCommand { get; }
        public DelegateCommand EStopCommand { get; }
        public DelegateCommand ToggleSingleStepCommand { get; }
        public DelegateCommand StepNextCommand { get; }
        /// <summary> 打开LogViewer日志窗口命令 </summary>
        public DelegateCommand ShowLogViewerCommand { get; }
        #endregion
        public OverViewModel(IRegionManager regionManager,
            IEventAggregator ea,
            IContainerProvider container,
            ITaskManager taskManager,
            ISystemStateService systemState,
            ISpeedOverrideService speedOverride,
            IDialogService dialogService,
            ILocalizationService localization)
        {
            _regionManager = regionManager;
            _ea = ea;
            _container = container;
            _taskManager = taskManager;
            _systemState = systemState;
            _speedOverride = speedOverride;
            _dialogService = dialogService;
            _localization = localization;
            _speedPercent = _speedOverride.SpeedPercent;
            _speedOverride.SpeedChanged += OnSpeedChanged;
            _singleStepButtonText = _localization.GetResource("OverView_Btn_SingleStep_Off");
            _speedDisplayText = $"{_localization.GetResource("OverView_Speed")}: {_speedPercent:N0}%";
            // 初始化时钟
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => SystemTime = DateTime.Now;
            _timer.Start();
            SystemTime = DateTime.Now;
            // 绑定按钮命令
            InitializeCommand = new DelegateCommand(OnInitialize);
            StartCommand = new DelegateCommand(OnStart);
            PauseCommand = new DelegateCommand(OnPause);
            ResumeCommand = new DelegateCommand(OnResume);
            StopCommand = new DelegateCommand(OnStop);
            EStopCommand = new DelegateCommand(OnEStop);
            ToggleSingleStepCommand = new DelegateCommand(OnToggleSingleStep);
            StepNextCommand = new DelegateCommand(OnStepNext, () => IsSingleStepMode);
            ShowLogViewerCommand = new DelegateCommand(OnShowLogViewer);

            // 订阅全局唯一状态事件，驱动 UI 刷新
            _ea.GetEvent<StationStateChangedEvent>().Subscribe(OnStationStateChanged, ThreadOption.PublisherThread, false);

            // 订阅主窗口控制按钮点击事件（从MainWindow右侧栏触发）
            _ea.GetEvent<ControlButtonClickedEvent>().Subscribe(OnControlButtonClicked, ThreadOption.PublisherThread, false);
        }
        private void OnStationStateChanged(StationStatePayload payload)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted)
                return;

            try
            {
                dispatcher.Invoke(() =>
                {
                    if (payload.GreenLight) TrioLightColor = "#4CAF50";
                    else if (payload.RedLight) TrioLightColor = "#F44336";
                    else if (payload.OrangeLight) TrioLightColor = "#FF9800";
                    else TrioLightColor = "#9E9E9E";

                    TrioLightText = payload.State switch
                    {
                        StationState.RUNNING => _localization.GetResource("OverView_TrioLight_Running"),
                        StationState.PAUSE => _localization.GetResource("OverView_TrioLight_Pause"),
                        StationState.ESTOP => _localization.GetResource("OverView_TrioLight_EStop"),
                        StationState.ALARM => _localization.GetResource("OverView_TrioLight_Alarm"),
                        StationState.STOP => _localization.GetResource("OverView_TrioLight_Stop"),
                        StationState.WAITRESET => _localization.GetResource("OverView_TrioLight_WaitReset"),
                        StationState.WAITRUN => _localization.GetResource("OverView_TrioLight_WaitRun"),
                        StationState.RESETING => _localization.GetResource("OverView_TrioLight_Reseting"),
                        _ => _localization.GetResource("OverView_TrioLight_Idle")
                    };

                    IsSystemRunning = payload.State == StationState.RUNNING;
                });
            }
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }
        }

        #region 命令实现
        private void OnInitialize()
        {
            // 初始化时，重置所有任务状态
            _systemState.RequestReset();
            _taskManager.HomeAllAsync();
        }
        private void OnStart()
        {
            // 启动时：状态机必须先通过条件，然后 TaskManager 才真正调度 Task
            if (_systemState.CanStart)
            {
                _systemState.RequestStart();
                _taskManager.StartAllAsync();
            }
        }
        private void OnPause() 
        {
            _systemState.RequestPause();
            _taskManager.PauseAllAsync();
        }
        private void OnResume() 
        {
            _systemState.RequestResume();
            _taskManager.ResumeAllAsync();
        }
        private void OnStop() 
        {
            _systemState.RequestStop();
            _taskManager.StopAllAsync();
        }
        private void OnEStop() 
        {
            _systemState.RequestEmergencyStop();
            _taskManager.EmergencyStopAllAsync();
        }
        private void OnToggleSingleStep()
        {
            IsSingleStepMode = !IsSingleStepMode;
            if (IsSingleStepMode)
                _taskManager.EnableSingleStepAll();
            else
                _taskManager.DisableSingleStepAll();
            StepNextCommand.RaiseCanExecuteChanged();
        }
        private void OnStepNext()
        {
            _taskManager.StepNextAll();
        }
        private void OnSpeedChanged(double newPercent)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted)
                return;

            try
            {
                dispatcher.Invoke(() =>
                {
                    _speedPercent = newPercent;
                    RaisePropertyChanged(nameof(SpeedPercent));
                    SpeedDisplayText = $"{_localization.GetResource("OverView_Speed")}: {_speedPercent:N0}%";
                });
            }
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }
        }

        /// <summary> 打开LogViewer日志窗口（非模态，关闭后可再次打开） </summary>
        private void OnShowLogViewer()
        {
            var parameters = new DialogParameters {
                { "dialogWidth", 1200 },
                { "dialogHeight", 800 },
                { "sizeToContent", SizeToContent.WidthAndHeight },
                { "resizeMode", ResizeMode.NoResize },
                { "windowStyle", WindowStyle.SingleBorderWindow },
                { "windowStartupLocation", WindowStartupLocation.CenterOwner }
            };
            _dialogService.Show("LogView", parameters, r => { });
        }

        /// <summary>
        /// 处理主窗口控制按钮点击事件 - 根据按钮类型分发到对应命令
        /// </summary>
        private void OnControlButtonClicked(string buttonType)
        {
            switch (buttonType)
            {
                case "Initialize":
                    OnInitialize();
                    break;
                case "Start":
                    OnStart();
                    break;
                case "Pause":
                    OnPause();
                    break;
                case "Resume":
                    OnResume();
                    break;
                case "Stop":
                    OnStop();
                    break;
            }
        }
        #endregion
        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            if (_regionManager.Regions.ContainsRegionWithName("OverviewDeviceRegion"))
            {
                var deviceRegion = _regionManager.Regions["OverviewDeviceRegion"];
                if (!deviceRegion.Views.Any())
                {
                    // 设备主视觉区域 - 暂时显示占位符，后续可替换为3D设备状态视图
                    // region.RequestNavigate(nameof(DeviceVisualView));
                }
            }
            if (_regionManager.Regions.ContainsRegionWithName("OverviewTaskMonitorRegion"))
            {
                var region = _regionManager.Regions["OverviewTaskMonitorRegion"];
                if (!region.Views.Any())
                {
                    region.RequestNavigate(nameof(TaskMonitorView));
                }
            }
            if (_regionManager.Regions.ContainsRegionWithName("OverviewSpeedControlRegion"))
            {
                var speedRegion = _regionManager.Regions["OverviewSpeedControlRegion"];
                if (!speedRegion.Views.Any())
                {
                    speedRegion.RequestNavigate(nameof(SpeedControlView));
                }
            }
        }
        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
    }
}