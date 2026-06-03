using Core.Abstraction;
using MotionControl.Events;
using MotionControl.Interfaces;
using MotionControl.Models;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace MotionControl.ViewModels
{
    /// <summary>
    /// 单个轴的 ViewModel
    /// 轮询线程推送状态 → 合并 BeginInvoke 刷新 UI（无 Timer，避免运动中不断重置导致延迟）
    /// </summary>
    public class SingleAxisViewModel : BindableBase, IDisposable
    {
        private readonly IMotionService _motionService;
        private readonly ILocalizationService _localizationService;
        private readonly IAxisOperationPanelState _axisPanelState;
        private readonly int _axisId;
        private readonly string _name;
        private readonly string _direction;

        private IDisposable _statusSubscription;
        private AxisStateChangedEvent _pendingStatusEvent;
        /// <summary>面板关闭时不刷新 UI</summary>
        private bool _uiRefreshEnabled = true;
        /// <summary>每轴仅保留一个待执行的 UI 刷新，始终应用最新 _pendingStatusEvent</summary>
        private int _uiDispatchFlag;

        /// <summary>允许执行回零：未回零、断使能再上使能、急停/报警复位后可回零；回零成功后置 false</summary>
        private bool _allowHome = true;

        // Jog 状态（由 SafeJogBehavior 控制）
        private bool _isJogging;
        public bool IsJogging
        {
            get => _isJogging;
            set => SetProperty(ref _isJogging, value);
        }

        public int AxisId => _axisId;
        public string Name => _name;
        public string Direction => _direction;

        private string _localizedAxisName;
        public string LocalizedAxisName
        {
            get => _localizedAxisName;
            private set => SetProperty(ref _localizedAxisName, value);
        }

        private string _localizedHomeStatus;
        public string LocalizedHomeStatus
        {
            get => _localizedHomeStatus;
            private set => SetProperty(ref _localizedHomeStatus, value);
        }

        private double _position;
        public double Position
        {
            get => _position;
            set => SetProperty(ref _position, value);
        }

        private double _speed = 10.0;
        public double Speed
        {
            get => _speed;
            set => SetProperty(ref _speed, value);
        }

        public double MinimumSpeed => 1;
        public double MaximumSpeed => 30;

        private double _stepSize = 0.1;
        public double StepSize
        {
            get => _stepSize;
            set => SetProperty(ref _stepSize, value);
        }

        public ObservableCollection<double> DistanceOptions { get; } = new();

        private bool _isServoOn;
        public bool IsServoOn
        {
            get => _isServoOn;
            set => SetProperty(ref _isServoOn, value);
        }

        private bool _isMEL;
        public bool IsMEL
        {
            get => _isMEL;
            set => SetProperty(ref _isMEL, value);
        }

        private bool _isORG;
        public bool IsORG
        {
            get => _isORG;
            set => SetProperty(ref _isORG, value);
        }

        private bool _isPEL;
        public bool IsPEL
        {
            get => _isPEL;
            set => SetProperty(ref _isPEL, value);
        }

        private bool _isALM;
        public bool IsALM
        {
            get => _isALM;
            set => SetProperty(ref _isALM, value);
        }

        private bool _isASTP;
        public bool IsASTP
        {
            get => _isASTP;
            set => SetProperty(ref _isASTP, value);
        }

        private bool _isHomeOk;
        public bool IsHomeOk
        {
            get => _isHomeOk;
            set => SetProperty(ref _isHomeOk, value);
        }

        public IMotionService MotionService => _motionService;
        public ISafetyZoneMonitor SafetyZoneMonitor { get; }

        public DelegateCommand MovePositiveCommand { get; }
        public DelegateCommand MoveNegativeCommand { get; }
        public DelegateCommand HomeCommand { get; }
        public DelegateCommand StopCommand { get; }
        public DelegateCommand ClearPositionCommand { get; }
        public DelegateCommand ClearAlarmCommand { get; }
        public DelegateCommand ServoOnCommand { get; }
        public DelegateCommand ServoOffCommand { get; }

        public SingleAxisViewModel(
            AxisConfig axisConfig,
            IMotionService motionService,
            ILocalizationService localizationService,
            ISafetyZoneMonitor safetyZoneMonitor = null,
            IAxisOperationPanelState axisPanelState = null)
        {
            _axisId = axisConfig.LogicalId;
            _name = axisConfig.Name;
            _direction = axisConfig.Direction ?? "X";
            _motionService = motionService ?? throw new ArgumentNullException(nameof(motionService));
            _localizationService = localizationService;
            SafetyZoneMonitor = safetyZoneMonitor;
            _axisPanelState = axisPanelState;

            if (_axisPanelState != null)
            {
                _uiRefreshEnabled = _axisPanelState.IsPanelOpen;
                _axisPanelState.PanelOpenChanged += OnAxisPanelOpenChanged;
            }

            var distances = new[] { 0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 20 };
            foreach (var d in distances) DistanceOptions.Add(d);

            RefreshLocalizedText();

            MovePositiveCommand = new DelegateCommand(() => ExecuteMoveRelative(_stepSize));
            MoveNegativeCommand = new DelegateCommand(() => ExecuteMoveRelative(-_stepSize));
            HomeCommand = new DelegateCommand(ExecuteHome, CanExecuteHome);
            StopCommand = new DelegateCommand(ExecuteStop);
            ClearPositionCommand = new DelegateCommand(ExecuteClearPosition);
            ClearAlarmCommand = new DelegateCommand(ExecuteClearAlarm);
            ServoOnCommand = new DelegateCommand(() => ExecuteServo(true));
            ServoOffCommand = new DelegateCommand(() => ExecuteServo(false));

            SubscribeToStatusEvents();
            SyncInitialStatusFromService();
        }

        /// <summary>订阅轮询事件：合并为单次 BeginInvoke(DataBind)，IO/位置同步刷新</summary>
        private void SubscribeToStatusEvents()
        {
            var observable = (_motionService as IObservable<AxisStateChangedEvent>)
                ?? throw new InvalidOperationException("IMotionService does not implement IObservable<AxisStateChangedEvent>");

            _statusSubscription = observable.Subscribe(new AxisStatusObserver(
                onNext: e =>
                {
                    if (e.AxisId != _axisId || !_uiRefreshEnabled) return;
                    _pendingStatusEvent = e;

                    if (Interlocked.CompareExchange(ref _uiDispatchFlag, 1, 0) != 0)
                        return;

                    var dispatcher = Application.Current?.Dispatcher;
                    if (dispatcher == null)
                    {
                        Interlocked.Exchange(ref _uiDispatchFlag, 0);
                        return;
                    }

                    dispatcher.BeginInvoke(() =>
                    {
                        Interlocked.Exchange(ref _uiDispatchFlag, 0);
                        if (!_uiRefreshEnabled) return;
                        var evt = _pendingStatusEvent;
                        if (evt == null || evt.AxisId != _axisId) return;
                        ApplyStatusFromEvent(evt);
                    }, DispatcherPriority.DataBind);
                },
                onError: ex => System.Diagnostics.Debug.WriteLine($"Axis {_axisId} status error: {ex.Message}")
            ));
        }

        /// <summary>面板打开时恢复刷新并同步最新缓存</summary>
        private void OnAxisPanelOpenChanged(bool isOpen)
        {
            _uiRefreshEnabled = isOpen;
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (isOpen)
                {
                    SyncInitialStatusFromService();
                    var evt = _pendingStatusEvent;
                    if (evt != null && evt.AxisId == _axisId)
                        ApplyStatusFromEvent(evt);
                }
                else
                {
                    _pendingStatusEvent = null;
                }
            }, DispatcherPriority.DataBind);
        }

        /// <summary>构造后立即从 MotionService 缓存拉一次，避免等首次变化才显示</summary>
        private void SyncInitialStatusFromService()
        {
            var axis = _motionService.GetAxisState(_axisId);
            if (axis == null) return;

            Position = axis.ActualPosition;
            IsMoving = axis.IsMoving;
            IsAlarmed = axis.IsAlarmed;
            IsALM = axis.IsAlarmed;
            IsServoOn = axis.IsEnabled;
        }

        /// <summary>状态灯/IO 字段</summary>
        private void ApplyIndicatorsFromEvent(AxisStateChangedEvent e)
        {
            bool homeChanged = IsHomeOk != e.IsHomeOk;
            bool servoChanged = IsServoOn != e.IsServoOn;

            IsMoving = e.IsMoving;
            IsAlarmed = e.IsAlarmed;
            IsALM = e.IsAlarmed;
            IsServoOn = e.IsServoOn;
            IsMEL = e.IsMEL;
            IsORG = e.IsORG;
            IsPEL = e.IsPEL;
            IsASTP = e.IsASTP;
            IsHomeOk = e.IsHomeOk;

            ApplyHomeAllowanceRules(e);
            if (homeChanged) RefreshLocalizedText();
            if (servoChanged) HomeCommand.RaiseCanExecuteChanged();
        }

        private void ApplyStatusFromEvent(AxisStateChangedEvent e)
        {
            ApplyIndicatorsFromEvent(e);
            Position = e.Position;
        }

        private bool CanExecuteHome() => _allowHome && IsServoOn;

        private void ApplyHomeAllowanceRules(AxisStateChangedEvent e)
        {
            if (!e.IsHomeOk || e.IsAlarmed || e.IsASTP)
                SetAllowHome(true);
        }

        private void SetAllowHome(bool allow)
        {
            if (_allowHome == allow) return;
            _allowHome = allow;
            HomeCommand.RaiseCanExecuteChanged();
        }

        private bool _isMoving;
        public bool IsMoving
        {
            get => _isMoving;
            set => SetProperty(ref _isMoving, value);
        }

        private bool _isAlarmed;
        public bool IsAlarmed
        {
            get => _isAlarmed;
            set => SetProperty(ref _isAlarmed, value);
        }

        private async void ExecuteMoveRelative(double distance)
        {
            try
            {
                await _motionService.MoveRelStartAsync(_axisId, distance, Speed).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ShowError($"{GetLocalizedText("AxisError_MoveRelFailed", "相对移动失败: ")}{ex.Message}");
            }
        }

        private async void ExecuteHome()
        {
            if (!CanExecuteHome())
            {
                ShowError(GetLocalizedText("AxisError_HomeNotAllowed",
                    "当前不可回零：请确认未初始化、或断使能再上使能、或急停/报警复位后再试。"));
                return;
            }

            try
            {
                await _motionService.HomeAxisAsync(_axisId);
                IsHomeOk = await _motionService.CheckHomeDoneAsync(_axisId) == 1;
                SetAllowHome(false);
                RefreshLocalizedText();
            }
            catch (Exception ex)
            {
                ShowError($"{GetLocalizedText("AxisError_HomeFailed", "归零失败: ")}{ex.Message}");
            }
        }

        public void ExecuteStop()
        {
            try
            {
                _motionService.StopAxis(_axisId);
                if (_isJogging)
                    IsJogging = false;
            }
            catch (Exception ex)
            {
                ShowError($"{GetLocalizedText("AxisError_StopFailed", "停止失败: ")}{ex.Message}");
            }
        }

        private async void ExecuteClearPosition()
        {
            try
            {
                await Task.Run(() => _motionService.ClearPosition(_axisId)).ConfigureAwait(true);
                Position = 0;
            }
            catch (Exception ex)
            {
                ShowError($"{GetLocalizedText("AxisError_ClearPosFailed", "清零失败: ")}{ex.Message}");
            }
        }

        private async void ExecuteClearAlarm()
        {
            try
            {
                await Task.Run(() => _motionService.ClearAlarm(_axisId)).ConfigureAwait(true);
                SetAllowHome(true);
            }
            catch (Exception ex)
            {
                ShowError($"{GetLocalizedText("AxisError_ClearAlarmFailed", "清除报警失败: ")}{ex.Message}");
            }
        }

        private async void ExecuteServo(bool enable)
        {
            try
            {
                await Task.Run(() =>
                {
                    if (enable) _motionService.EnableAxis(_axisId);
                    else _motionService.DisableAxis(_axisId);
                }).ConfigureAwait(true);

                if (enable)
                    HomeCommand.RaiseCanExecuteChanged();
                else
                    SetAllowHome(true);
            }
            catch (Exception ex)
            {
                ShowError($"{GetLocalizedText("AxisError_ServoOpFailed", "伺服操作失败: ")}{ex.Message}");
            }
        }

        private void ShowError(string message)
        {
            string title = GetLocalizedText("ErrorTitle", "错误");
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private string GetLocalizedText(string key, string fallback)
        {
            return _localizationService?.GetResourceOrDefault(key, fallback) ?? fallback;
        }

        private void RefreshLocalizedText()
        {
            LocalizedAxisName = string.IsNullOrEmpty(_name)
                ? string.Empty
                : _localizationService?.GetResourceOrDefault($"Axis_{_name}", _name) ?? _name;

            string homeKey = IsHomeOk ? "HomeStatus_Initialized" : "HomeStatus_NotInitialized";
            string homeFallback = IsHomeOk ? "已初始化" : "未初始化";
            LocalizedHomeStatus = _localizationService?.GetResourceOrDefault(homeKey, homeFallback) ?? homeFallback;
        }

        public void Dispose()
        {
            if (_axisPanelState != null)
                _axisPanelState.PanelOpenChanged -= OnAxisPanelOpenChanged;
            _statusSubscription?.Dispose();
            _pendingStatusEvent = null;
        }
    }

    internal class AxisStatusObserver : IObserver<AxisStateChangedEvent>
    {
        private readonly Action<AxisStateChangedEvent> _onNext;
        private readonly Action<Exception> _onError;
        private readonly Action _onCompleted;

        public AxisStatusObserver(Action<AxisStateChangedEvent> onNext, Action<Exception> onError = null, Action onCompleted = null)
        {
            _onNext = onNext ?? throw new ArgumentNullException(nameof(onNext));
            _onError = onError;
            _onCompleted = onCompleted;
        }

        public void OnNext(AxisStateChangedEvent value) => _onNext(value);
        public void OnError(Exception error) => _onError?.Invoke(error);
        public void OnCompleted() => _onCompleted?.Invoke();
    }
}
