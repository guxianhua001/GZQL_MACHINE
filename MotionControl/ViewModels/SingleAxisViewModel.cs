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

namespace MotionControl.ViewModels
{
    /// <summary>
    /// 单个轴的 ViewModel
    /// 采用事件驱动模式监控轴状态（替代 DispatcherTimer 轮询）
    /// 使用 SemaphoreSlim 防止重入
    /// </summary>
    public class SingleAxisViewModel : BindableBase, IDisposable
    {
        private readonly IMotionService _motionService;
        private readonly ILocalizationService _localizationService;
        private readonly int _axisId;
        private readonly string _name;
        private readonly string _direction;

        // 事件订阅
        private IDisposable _statusSubscription;
        
        // 防抖计时器（替代 Rx.Throttle）
        private System.Windows.Threading.DispatcherTimer _debounceTimer;
        
        // 重入保护锁（非阻塞模式）
        private readonly SemaphoreSlim _updateLock = new(1, 1);
        
        // 待处理的最新事件
        private AxisStateChangedEvent _pendingEvent;

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

        private bool _isHomeOk;
        public bool IsHomeOk
        {
            get => _isHomeOk;
            set => SetProperty(ref _isHomeOk, value);
        }

        // IMotionService 公开引用（供 SafeJogBehavior 使用）
        public IMotionService MotionService => _motionService;

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

        public SingleAxisViewModel(AxisConfig axisConfig, IMotionService motionService, 
                                   ILocalizationService localizationService)
        {
            _axisId = axisConfig.LogicalId;
            _name = axisConfig.Name;
            _direction = axisConfig.Direction ?? "X";
            _motionService = motionService ?? throw new ArgumentNullException(nameof(motionService));
            _localizationService = localizationService;

            // 初始化步距选项
            var distances = new[] { 0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 20 };
            foreach (var d in distances) DistanceOptions.Add(d);
            StepSize = _stepSize;

            // 初始化缓存的本地化文本
            RefreshLocalizedText();

            // 初始化命令
            MovePositiveCommand = new DelegateCommand(() => ExecuteMoveRelative(_stepSize));
            MoveNegativeCommand = new DelegateCommand(() => ExecuteMoveRelative(-_stepSize));
            HomeCommand = new DelegateCommand(ExecuteHome);
            StopCommand = new DelegateCommand(ExecuteStop);
            ClearPositionCommand = new DelegateCommand(ExecuteClearPosition);
            ClearAlarmCommand = new DelegateCommand(ExecuteClearAlarm);
            ServoOnCommand = new DelegateCommand(() => ExecuteServo(true));
            ServoOffCommand = new DelegateCommand(() => ExecuteServo(false));

            // 订阅事件驱动的状态更新
            SubscribeToStatusEvents();
        }

        // ========== 事件驱动状态监控 ==========

        /// <summary>
        /// 订阅轴状态变更事件（核心：替代定时器轮询）
        /// 手动过滤当前轴事件 + DispatcherTimer 防抖
        /// </summary>
        private void SubscribeToStatusEvents()
        {
            // 将 IObservable 转换为可观察序列
            var observable = (_motionService as IObservable<AxisStateChangedEvent>) 
                ?? throw new InvalidOperationException("IMotionService does not implement IObservable<AxisStateChangedEvent>");

            // 初始化防抖计时器（50ms 间隔，避免频繁刷新 UI）
            _debounceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _debounceTimer.Tick += OnDebounceTick;

            // 订阅事件：手动过滤 + 防抖
            _statusSubscription = observable.Subscribe(new AxisStatusObserver(
                onNext: e =>
                {
                    if (e.AxisId == _axisId)
                    {
                        _pendingEvent = e;
                        _debounceTimer.Stop();
                        _debounceTimer.Start();
                    }
                },
                onError: ex => System.Diagnostics.Debug.WriteLine($"Axis {_axisId} status error: {ex.Message}")
            ));
        }

        /// <summary>
        /// 防抖计时器触发：处理缓存的最新事件
        /// </summary>
        private void OnDebounceTick(object sender, EventArgs e)
        {
            _debounceTimer.Stop();
            
            if (_pendingEvent != null)
            {
                var evt = _pendingEvent;
                _pendingEvent = null;
                
                // 异步更新状态
                _ = UpdateStatusFromEventAsync(evt);
            }
        }

        /// <summary>
        /// 从事件更新轴状态（异步、防重入）
        /// </summary>
        private async Task UpdateStatusFromEventAsync(AxisStateChangedEvent e)
        {
            // 非阻塞尝试获取锁：若上一更新未完成，跳过本次（不排队）
            if (!await _updateLock.WaitAsync(0))
                return;

            try
            {
                // 在 UI 线程更新属性
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Position = e.Position;
                    IsMoving = e.IsMoving;
                    IsAlarmed = e.IsAlarmed;
                    IsServoOn = e.IsServoOn;
                    IsMEL = e.IsMEL;
                    IsORG = e.IsORG;
                    IsPEL = e.IsPEL;
                    IsASTP = e.IsASTP;
                    IsHomeOk = e.IsHomeOk;

                    // 更新缓存的本地化文本
                    RefreshLocalizedText();
                });
            }
            finally
            {
                _updateLock.Release();
            }
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
                await _motionService.MoveRelAsync(_axisId, distance, Speed);
            }
            catch (Exception ex)
            {
                ShowError($"{GetLocalizedText("AxisError_MoveRelFailed", "相对移动失败: ")}{ex.Message}");
            }
        }

        private async void ExecuteHome()
        {
            try
            {
                await _motionService.HomeAsync(_axisId);
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
            try
            {
                _motionService.StopAxis(_axisId);
                
                if (_isJogging)
                {
                    IsJogging = false;
                }
            }
            catch (Exception ex)
            {
                ShowError($"{GetLocalizedText("AxisError_StopFailed", "停止失败: ")}{ex.Message}");
            }
        }

        private void ExecuteClearPosition()
        {
            try
            {
                _motionService.ClearPosition(_axisId);
                Position = 0;
            }
            catch (Exception ex)
            {
                ShowError($"{GetLocalizedText("AxisError_ClearPosFailed", "清零失败: ")}{ex.Message}");
            }
        }

        private void ExecuteClearAlarm()
        {
            try
            {
                _motionService.ClearAlarm(_axisId);
            }
            catch (Exception ex)
            {
                ShowError($"{GetLocalizedText("AxisError_ClearAlarmFailed", "清除报警失败: ")}{ex.Message}");
            }
        }

        private void ExecuteServo(bool enable)
        {
            try
            {
                if (enable)
                    _motionService.EnableAxis(_axisId);
                else
                    _motionService.DisableAxis(_axisId);
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
            _debounceTimer?.Stop();
            _debounceTimer = null;
            _statusSubscription?.Dispose();
            _updateLock?.Dispose();
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
