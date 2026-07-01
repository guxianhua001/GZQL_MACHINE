using MaterialDesignThemes.Wpf;
using StationTasks.Events;
using MotionControl.Events;
using MotionControl.Interfaces;
using MotionControl.Models;
using MotionControl.Views;
using Core.Abstraction;
using StationTasks.Services;
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
        private readonly IMachineInitializationService _machineInitService;
        private readonly IMotionService _motionService;
        private readonly IHardwareConfigLoader _hwConfigLoader;
        private readonly DispatcherTimer _timer;

        // 解析后的 DO LogicalId（-1 表示未配置；从 hwcfg.xml OutputGroups 按 Group 解析，不硬编码点位名）
        // 注：安全门锁支持多扇，用列表存储；空列表表示未配置
        private int _workLightDoId = -1;
        private int _airBlowDoId = -1;
        private IList<int> _doorLockDoIds = new List<int>();
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
        // 安全门颜色（椭圆指示灯）
        private string _doorColor = "#4CAF50";
        public string DoorColor
        {
            get => _doorColor;
            set => SetProperty(ref _doorColor, value);
        }
        // 安全门状态文本（已锁定/已解锁）
        private string _doorStatusText;
        public string DoorStatusText
        {
            get => _doorStatusText;
            set => SetProperty(ref _doorStatusText, value);
        }
        // 安全门文本颜色
        private string _doorTextColor = "#2E7D32";
        public string DoorTextColor
        {
            get => _doorTextColor;
            set => SetProperty(ref _doorTextColor, value);
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
        // ===== DO 输出开关状态 =====
        private bool _isWorkLightOn;
        /// <summary>工作灯（柜内照明 Q2.7）通断状态</summary>
        public bool IsWorkLightOn { get => _isWorkLightOn; set => SetProperty(ref _isWorkLightOn, value); }

        private bool _isAirBlowOn;
        /// <summary>吹气（总气压控制 Q3.7）通断状态</summary>
        public bool IsAirBlowOn { get => _isAirBlowOn; set => SetProperty(ref _isAirBlowOn, value); }

        private bool _isSafetyDoorLocked;
        /// <summary>安全门锁锁定状态：true=已锁定（支持多扇，全部锁定才为 true；DO 由 hwcfg.xml Group="DoorLocks" 配置）</summary>
        public bool IsSafetyDoorLocked { get => _isSafetyDoorLocked; set => SetProperty(ref _isSafetyDoorLocked, value); }

        /// <summary>工作灯 DO 点位是否已配置（控制按钮 IsEnabled）</summary>
        public bool IsWorkLightAvailable => _workLightDoId >= 0;
        /// <summary>吹气 DO 点位是否已配置</summary>
        public bool IsAirBlowAvailable => _airBlowDoId >= 0;
        /// <summary>安全门锁 DO 点位是否已配置（支持多扇，至少一个即启用）</summary>
        public bool IsSafetyDoorLockAvailable => _doorLockDoIds.Count > 0;
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
        /// <summary> 工作灯开关命令 </summary>
        public DelegateCommand ToggleWorkLightCommand { get; }
        /// <summary> 吹气开关命令 </summary>
        public DelegateCommand ToggleAirBlowCommand { get; }
        /// <summary> 安全门锁定开关命令 </summary>
        public DelegateCommand ToggleSafetyDoorLockCommand { get; }
        #endregion
        public OverViewModel(IRegionManager regionManager,
            IEventAggregator ea,
            IContainerProvider container,
            ITaskManager taskManager,
            ISystemStateService systemState,
            ISpeedOverrideService speedOverride,
            IDialogService dialogService,
            ILocalizationService localization,
            IMachineInitializationService machineInitService,
            IMotionService motionService,
            IHardwareConfigLoader hwConfigLoader)
        {
            _regionManager = regionManager;
            _ea = ea;
            _container = container;
            _taskManager = taskManager;
            _systemState = systemState;
            _speedOverride = speedOverride;
            _dialogService = dialogService;
            _localization = localization;
            _machineInitService = machineInitService;
            _motionService = motionService;
            _hwConfigLoader = hwConfigLoader;
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

            // DO 输出开关命令（工作灯/吹气/安全门锁）
            ToggleWorkLightCommand = new DelegateCommand(OnToggleWorkLight);
            ToggleAirBlowCommand = new DelegateCommand(OnToggleAirBlow);
            ToggleSafetyDoorLockCommand = new DelegateCommand(OnToggleSafetyDoorLock);

            // 解析 DO 点位 LogicalId（配置在启动时已加载，安全；未配置返回 -1，按钮自动禁用）
            ResolveOutputDoIds();
            // 订阅 SystemStateService 安全门锁状态推送（由其 20ms 轮询线程回读 DO 并触发）
            _systemState.SafetyDoorLockChanged += OnSafetyDoorLockChanged;
        }

        /// <summary>
        /// 安全门锁状态变更回调（由 SystemStateService 轮询线程触发，需 Dispatcher 切换到 UI 线程）。
        /// 同步更新锁定状态、指示灯颜色、文本及文本颜色。
        /// </summary>
        private void OnSafetyDoorLockChanged(bool locked)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted) return;
            dispatcher.Invoke(() => ApplyDoorLockState(locked));
        }

        /// <summary>将安全门锁状态应用到 UI 绑定属性</summary>
        private void ApplyDoorLockState(bool locked)
        {
            IsSafetyDoorLocked = locked;
            DoorColor = locked ? "#4CAF50" : "#F44336";       // 绿=锁定, 红=解锁
            DoorTextColor = locked ? "#2E7D32" : "#C62828";   // 深绿=锁定, 深红=解锁
            DoorStatusText = _localization.GetResourceOrDefault(
                locked ? "OverView_Door_Locked" : "OverView_Door_Unlocked",
                locked ? "安全门: 已锁定" : "安全门: 已解锁");
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
        /// <summary>
        /// 整机初始化：使用 IMachineInitializationService 执行协调式初始化序列。
        /// 时序：Z轴归零→并行上下料/点胶/组装辅助轴→等待点胶完成→组装主轴→设置等待运行状态。
        /// </summary>
        private void OnInitialize()
        {
            _ = _machineInitService.InitializeMachineAsync();
        }
        private void OnStart()
        {
            if (!_systemState.CanStart)
            {
                ShowStartBlockedMessage();
                return;
            }
            _systemState.RequestStart();
            _taskManager.StartAllAsync();
        }

        /// <summary>启动条件不满足时提示用户（如需整机初始化或当前为急停等）</summary>
        private void ShowStartBlockedMessage()
        {
            string message = _systemState.CurrentState switch
            {
                StationState.WAITRESET or StationState.ESTOP or StationState.STOP or StationState.ALARM
                    => _localization.GetResourceOrDefault("StartBlocked_RequireInit",
                        "Device initialization is required before start. Please run machine initialization first."),
                _ => _localization.GetResourceOrDefault("StartBlocked_NotReady",
                    "Device is not ready to start in the current state.")
            };
            _dialogService.ShowDialog("NotificationDialog", new DialogParameters
            {
                { "title", _localization.GetResourceOrDefault("OverView_Dialog_Note", "Note") },
                { "message", message },
                { "icon", MaterialDesignThemes.Wpf.PackIconKind.AlertCircle }
            }, _ => { });
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
            // 与硬件急停同等：状态机 ESTOP + 广播 EmergencyStopAllEvent（TaskManager 订阅后停任务）
            _systemState.RequestEmergencyStop();
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
                case "EStop":
                    OnEStop();
                    break;
            }
        }
        #endregion

        #region DO 输出开关（工作灯/吹气/安全门锁）

        /// <summary>
        /// 从 hwcfg.xml OutputGroups 按 Group 解析三个 DO 输出点位的 LogicalId。
        /// 不硬编码物理点位名，完全依赖配置文件的分组定义。
        /// 未配置时返回 -1，对应按钮 IsEnabled=False。
        /// </summary>
        private void ResolveOutputDoIds()
        {
            var config = _hwConfigLoader.Load();
            _workLightDoId = ResolveDoIdByGroup(config, "WorkLights");
            _airBlowDoId = ResolveDoIdByGroup(config, "AirBlow");
            // 安全门锁支持多扇：解析分组内全部有效 DO LogicalId
            _doorLockDoIds = ResolveDoIdsByGroup(config, "DoorLocks");
            // 触发可用性属性通知（按钮 IsEnabled 绑定）
            RaisePropertyChanged(nameof(IsWorkLightAvailable));
            RaisePropertyChanged(nameof(IsAirBlowAvailable));
            RaisePropertyChanged(nameof(IsSafetyDoorLockAvailable));
        }

        /// <summary>
        /// 按 Group 从 MotionSystemConfig.OutputSignals 中解析首个有效 DO LogicalId。
        /// 未找到返回 -1 并记录警告（适用于单点位分组，如 WorkLights/AirBlow）。
        /// </summary>
        private int ResolveDoIdByGroup(MotionSystemConfig config, string group)
        {
            var cfg = config.OutputSignals.FirstOrDefault(o => o.Group == group && o.LogicalId.HasValue);
            if (cfg == null)
                System.Diagnostics.Debug.WriteLine($"[OverView] 输出分组 '{group}' 未配置或 io 为空，对应按钮将禁用");
            return cfg?.LogicalId ?? -1;
        }

        /// <summary>
        /// 按 Group 从 MotionSystemConfig.OutputSignals 中解析全部有效 DO LogicalId。
        /// 支持同分组多点位（如多扇安全门锁 DoorLocks），未找到返回空列表。
        /// </summary>
        private IList<int> ResolveDoIdsByGroup(MotionSystemConfig config, string group)
        {
            var ids = config.OutputSignals
                .Where(o => o.Group == group && o.LogicalId.HasValue)
                .Select(o => o.LogicalId.Value)
                .ToList();
            if (ids.Count == 0)
                System.Diagnostics.Debug.WriteLine($"[OverView] 输出分组 '{group}' 未配置或 io 为空，对应按钮将禁用");
            return ids;
        }

        /// <summary>
        /// 回读 DO 点位当前硬件状态，刷新 UI 显示。best-effort：运动卡未就绪时 catch 异常，保持默认 false。
        /// 安全门锁状态由 SystemStateService 轮询推送（SafetyDoorLockChanged 事件），此处不再直接 ReadDo。
        /// </summary>
        private void RefreshOutputStates()
        {
            if (_workLightDoId >= 0)
                IsWorkLightOn = TryReadDo(_workLightDoId);
            if (_airBlowDoId >= 0)
                IsAirBlowOn = TryReadDo(_airBlowDoId);
            // 安全门锁状态从 SystemStateService 获取（由其 20ms 轮询推送，首次进入时同步当前值）
            ApplyDoorLockState(_systemState.IsSafetyDoorLocked);
        }

        /// <summary>best-effort 读取 DO 状态，失败返回 false</summary>
        private bool TryReadDo(int logicalId)
        {
            try { return _motionService.ReadDo(logicalId); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OverView] ReadDo(logicalId={logicalId}) 失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>工作灯开关：toggle 柜内照明 Q2.7</summary>
        private void OnToggleWorkLight() => ToggleDo(_workLightDoId, v => IsWorkLightOn = v, nameof(IsWorkLightOn), "工作灯");

        /// <summary>吹气开关：toggle 总气压控制 Q3.7</summary>
        private void OnToggleAirBlow() => ToggleDo(_airBlowDoId, v => IsAirBlowOn = v, nameof(IsAirBlowOn), "吹气");

        /// <summary>
        /// 安全门锁开关：toggle DoorLocks 分组全部 DO 点位（true=锁定，支持多扇同时锁定/解锁）。
        /// 乐观更新全部 UI 属性（状态/颜色/文本），SystemStateService 20ms 内确认实际状态。
        /// 多门锁场景：同时写入所有点位，任一写入失败回滚 UI 并记录（实际状态由轮询纠正）。
        /// </summary>
        private void OnToggleSafetyDoorLock()
        {
            if (_doorLockDoIds.Count == 0) return;
            bool current = IsSafetyDoorLocked;
            bool newVal = !current;
            try
            {
                // 同时锁定/解锁所有安全门锁 DO 点位（多扇门同步控制）
                foreach (var doId in _doorLockDoIds)
                    _motionService.WriteDo(doId, newVal);
                ApplyDoorLockState(newVal);   // 乐观更新：状态+颜色+文本
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OverView] 安全门锁 DO 切换失败 (logicalIds=[{string.Join(",", _doorLockDoIds)}]): {ex.Message}");
                ApplyDoorLockState(current);  // 回滚到实际状态
            }
        }

        /// <summary>
        /// 通用 DO toggle：读取当前状态取反写入，乐观更新 UI，失败回滚。
        /// 约定 WriteDo(true)=开/锁定，与 SystemStateService.WriteLight 一致。
        /// </summary>
        private void ToggleDo(int doId, Action<bool> setState, string propName, string label)
        {
            if (doId < 0) return;
            // 取当前 UI 状态取反作为目标值（UI 状态由 ReadDo/上次写入维护）
            // 反射读属性值可能为 null（属性不存在），先转 bool? 再 ?? false 兜底
            bool current = (bool?)GetType().GetProperty(propName)?.GetValue(this) ?? false;
            bool newVal = !current;
            try
            {
                _motionService.WriteDo(doId, newVal);
                setState(newVal);   // 乐观更新
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OverView] {label} DO 切换失败 (logicalId={doId}): {ex.Message}");
                setState(current);  // 回滚
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
            // 回读 DO 输出硬件状态，刷新按钮通断/锁定显示（best-effort，失败保持默认）
            RefreshOutputStates();
        }
        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
    }
}