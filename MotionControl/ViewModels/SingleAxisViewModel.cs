using Core.Abstraction;
using MotionControl.Events;
using MotionControl.Interfaces;
using MotionControl.Models;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace MotionControl.ViewModels
{
    /// <summary>
    /// 单个轴的 ViewModel
    /// 采用事件驱动模式监控轴状态；UI 合并刷新，无额外防抖延迟
    /// </summary>
    public class SingleAxisViewModel : BindableBase, IDisposable
    {
        private readonly IMotionService _motionService;
        private readonly ILocalizationService _localizationService;
        private readonly int _axisId;
        private readonly string _name;
        private readonly string _direction;

        private IDisposable _statusSubscription;
        private volatile AxisStateChangedEvent _latestStatusEvent;
        private long _statusEventSequence;
        private long _lastAppliedStatusSequence;
        private int _uiUpdateScheduled;

        /// <summary>允许执行回零：未回零、断使能再上使能、急停/报警复位后可回零；回零成功后置 false</summary>
        private bool _allowHome = true;

        // Jog 状态（由 SafeJogBehavior 控制）
        private bool _isJogging;
        public bool IsJogging
        {
            get => _isJogging;
            set => SetProperty(ref _isJogging, value);
        }

        // ========== 基础属性 ==========
        
        public int AxisId => _axisId;
        public string Name => _name;
        public string Direction => _direction;

        // 缓存的本地化文本，避免每次绑定刷新都查资源字典
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

        // ========== 运动参数 ==========

        private double _position;
        public double Position
        {
            get => _position;
            set => SetProperty(ref _position, value);
        }

        private double _speed = 10.0;  // 默认速度 mm/s
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

        // ========== 状态属性（由事件更新）==========

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

        /// <summary>回零完成（CheckHomeDone==1，由轮询事件或归零命令后刷新）</summary>
        private bool _isHomeOk;
        public bool IsHomeOk
        {
            get => _isHomeOk;
            set => SetProperty(ref _isHomeOk, value);
        }

        // IMotionService 公开引用（供 SafeJogBehavior 使用）
        public IMotionService MotionService => _motionService;

        /// <summary>安全区域监控（供 Jog 点动前互锁检查）</summary>
        public ISafetyZoneMonitor SafetyZoneMonitor { get; }

        // ========== 命令 ==========

        public DelegateCommand MovePositiveCommand { get; }
        public DelegateCommand MoveNegativeCommand { get; }
        public DelegateCommand HomeCommand { get; }
        public DelegateCommand StopCommand { get; }
        public DelegateCommand ClearPositionCommand { get; }
        public DelegateCommand ClearAlarmCommand { get; }
        public DelegateCommand ServoOnCommand { get; }
        public DelegateCommand ServoOffCommand { get; }

        // ========== 构造函数 ==========

        public SingleAxisViewModel(
            AxisConfig axisConfig,
            IMotionService motionService,
            ILocalizationService localizationService,
            ISafetyZoneMonitor safetyZoneMonitor = null)
        {
            _axisId = axisConfig.LogicalId;
            _name = axisConfig.Name;
            _direction = axisConfig.Direction ?? "X";
            _motionService = motionService ?? throw new ArgumentNullException(nameof(motionService));
            _localizationService = localizationService;
            SafetyZoneMonitor = safetyZoneMonitor;

            // 初始化步距选项
            var distances = new[] { 0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 20 };
            foreach (var d in distances) DistanceOptions.Add(d);
            StepSize = _stepSize;

            // 初始化缓存的本地化文本
            RefreshLocalizedText();

            // 初始化命令
            MovePositiveCommand = new DelegateCommand(() => ExecuteMoveRelative(_stepSize));
            MoveNegativeCommand = new DelegateCommand(() => ExecuteMoveRelative(-_stepSize));
            HomeCommand = new DelegateCommand(ExecuteHome, CanExecuteHome);
            StopCommand = new DelegateCommand(ExecuteStop);
            ClearPositionCommand = new DelegateCommand(ExecuteClearPosition);
            ClearAlarmCommand = new DelegateCommand(ExecuteClearAlarm);
            ServoOnCommand = new DelegateCommand(() => ExecuteServo(true));
            ServoOffCommand = new DelegateCommand(() => ExecuteServo(false));

            // 订阅事件驱动的状态更新
            SubscribeToStatusEvents();
        }

        // ========== 事件驱动状态监控 ==========

        /// <summary>订阅轴状态：轮询线程推送后合并刷新 UI（序列号避免重复 BeginInvoke 死循环）</summary>
        private void SubscribeToStatusEvents()
        {
            var observable = (_motionService as IObservable<AxisStateChangedEvent>)
                ?? throw new InvalidOperationException("IMotionService does not implement IObservable<AxisStateChangedEvent>");

            _statusSubscription = observable.Subscribe(new AxisStatusObserver(
                onNext: e =>
                {
                    if (e.AxisId != _axisId) return;
                    _latestStatusEvent = e;
                    Interlocked.Increment(ref _statusEventSequence);
                    ScheduleStatusUiUpdate();
                },
                onError: ex => System.Diagnostics.Debug.WriteLine($"Axis {_axisId} status error: {ex.Message}")
            ));
        }

        /// <summary>合并调度 UI 刷新，同一时刻仅排队一次 BeginInvoke</summary>
        private void ScheduleStatusUiUpdate()
        {
            if (Interlocked.CompareExchange(ref _uiUpdateScheduled, 1, 0) != 0)
                return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                Interlocked.Exchange(ref _uiUpdateScheduled, 0);
                return;
            }

            dispatcher.BeginInvoke(ApplyLatestStatusToUi, DispatcherPriority.Render);
        }

        /// <summary>在 UI 线程应用最新轴状态</summary>
        private void ApplyLatestStatusToUi()
        {
            try
            {
                var e = _latestStatusEvent;
                if (e == null || e.AxisId != _axisId) return;

                bool homeChanged = IsHomeOk != e.IsHomeOk;
                bool servoChanged = IsServoOn != e.IsServoOn;

                Position = e.Position;
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

                Interlocked.Exchange(ref _lastAppliedStatusSequence, _statusEventSequence);
            }
            finally
            {
                Interlocked.Exchange(ref _uiUpdateScheduled, 0);
                // 仅当回调期间又来了新采样时才补调度（不能用 != null，否则会无限 BeginInvoke 卡死 UI）
                if (Interlocked.Read(ref _statusEventSequence) != Interlocked.Read(ref _lastAppliedStatusSequence))
                    ScheduleStatusUiUpdate();
            }
        }

        /// <summary>回零按钮可用：已允许回零且伺服 ON（断使能→再上使能后 _allowHome 为 true）</summary>
        private bool CanExecuteHome() => _allowHome && IsServoOn;

        /// <summary>根据回零/使能/急停状态更新是否允许再次回零</summary>
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

        // 补充属性：IsMoving（用于内部逻辑）
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

        // ========== 命令实现 ==========

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

        /// <summary>
        /// 停止轴运动（公开方法，供 StationAxisViewModel 调用）
        /// </summary>
        public void ExecuteStop()
        {
            _ = RunAxisCommandAsync(() =>
            {
                _motionService.StopAxis(_axisId);
                if (_isJogging)
                    Application.Current?.Dispatcher.Invoke(() => IsJogging = false);
            }, "AxisError_StopFailed", "停止失败: ");
        }

        private void ExecuteClearPosition()
        {
            _ = RunAxisCommandAsync(() =>
            {
                _motionService.ClearPosition(_axisId);
                Application.Current?.Dispatcher.Invoke(() => Position = 0);
            }, "AxisError_ClearPosFailed", "清零失败: ");
        }

        private void ExecuteClearAlarm()
        {
            _ = RunAxisCommandAsync(() =>
            {
                _motionService.ClearAlarm(_axisId);
                Application.Current?.Dispatcher.Invoke(SetAllowHome, true);
            }, "AxisError_ClearAlarmFailed", "清除报警失败: ");
        }

        private void ExecuteServo(bool enable)
        {
            _ = RunAxisCommandAsync(() =>
            {
                if (enable)
                {
                    _motionService.EnableAxis(_axisId);
                    Application.Current?.Dispatcher.Invoke(() => HomeCommand.RaiseCanExecuteChanged());
                }
                else
                {
                    _motionService.DisableAxis(_axisId);
                    Application.Current?.Dispatcher.Invoke(() => SetAllowHome(true));
                }
            }, "AxisError_ServoOpFailed", "伺服操作失败: ");
        }

        /// <summary>在后台线程执行读卡/写卡，避免阻塞 UI 与轮询争抢卡锁</summary>
        private async Task RunAxisCommandAsync(Action action, string errorResourceKey, string errorFallbackPrefix)
        {
            try
            {
                await Task.Run(action).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ShowError($"{GetLocalizedText(errorResourceKey, errorFallbackPrefix)}{ex.Message}");
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

        /// <summary>
        /// 刷新缓存的本地化文本（构造时、状态变更时、语言切换时调用）
        /// 避免每次属性访问都查资源字典
        /// </summary>
        private void RefreshLocalizedText()
        {
            LocalizedAxisName = string.IsNullOrEmpty(_name)
                ? string.Empty
                : _localizationService?.GetResourceOrDefault($"Axis_{_name}", _name) ?? _name;

            string homeKey = IsHomeOk ? "HomeStatus_Initialized" : "HomeStatus_NotInitialized";
            string homeFallback = IsHomeOk ? "已初始化" : "未初始化";
            LocalizedHomeStatus = _localizationService?.GetResourceOrDefault(homeKey, homeFallback) ?? homeFallback;
        }

        // ========== IDisposable ==========

        public void Dispose()
        {
            _statusSubscription?.Dispose();
            _latestStatusEvent = null;
        }
    }

    /// <summary>
    /// 轴状态事件的 IObserver 实现（支持命名回调）
    /// 用于将 IObservable.Subscribe 转为委托模式
    /// </summary>
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
