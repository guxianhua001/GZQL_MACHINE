using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Core.Abstraction;
using Core.Configuration;
using Core.Events;
using Core.Utilities;
using MotionControl.Events;
using MotionControl.Interfaces;
using MotionControl.Models;
using Prism.Events;
namespace MotionControl.Services
{
    public class SystemStateService : ISystemStateService, IDisposable
    {
        private readonly IMotionService _motion;
        private readonly IEventAggregator _ea;
        private readonly ILoggerService _logger;
        private readonly MotionSystemConfig _config;
        private readonly IAppSettingService _appSettings;
        private readonly ILocalizationService _localization;
        // 安全功能开关（从配置读取，支持运行时热更新）
        private bool _safetyGateEnabled = true;
        private bool _gratingEnabled = true;
        private bool _buzzerEnabled = false;
        private bool _safetyEventLogEnabled = true;
        // 安全信号分组
        private List<SignalConfig> _safetyGateSignals = new();
        private List<SignalConfig> _gratingSignals = new();
        // 轮询定时器
        private Timer _signalTimer;
        private bool _disposed;
        // 当前状态
        private StationState _currentState = StationState.WAITRESET;
        public StationState CurrentState => _currentState;
        private readonly object _stateLock = new();
        private CancellationTokenSource _resetCts;
        // 快速查找字典
        private Dictionary<int, SignalConfig> _signalLookup = new();
        private Dictionary<int, OutputSignalConfig> _outputLookup = new();
        private Dictionary<int, LightConfig> _lightLookup = new();
        // 上一次 DI 状态
        private Dictionary<int, bool> _previousDiStates = new();
        // 分组引用
        private List<SignalConfig> _safetySignals = new();
        private List<SignalConfig> _estopSignals = new();
        private List<SignalConfig> _controlButtons = new();
        // 灯光配置按类型快速访问
        private Dictionary<string, LightConfig> _lightByType = new();
        // 灯光控制
        private Timer _blinkTimer;
        private volatile bool _blinkPhase;
        private bool _greenOn, _greenBlink;
        private bool _redOn, _redBlink;
        private bool _orangeOn, _orangeBlink;
        private bool _lastGreen, _lastRed, _lastOrange;
        // 蜂鸣器锁存控制
        private Timer _buzzerPulseTimer;
        private bool _buzzerLatched;
        private bool _buzzerActualOutput;
        // 长按复位按钮检测
        private DateTime? _resetButtonPressedTime;
        private bool _resetLongPressHandled;
        public SystemStateService(IMotionService motion, IHardwareConfigLoader configLoader,
                                  IEventAggregator ea, ILoggerService logger,
                                  IAppSettingService appSettings,
                                  ILocalizationService localization)
        {
            _motion = motion;
            _ea = ea;
            _logger = logger;
            _appSettings = appSettings;
            _localization = localization;
            _config = configLoader.Load();
            // 从应用配置初始化安全功能开关
            LoadSafetySettings(_appSettings.Settings);
            InitializeMappings();
            _ea.GetEvent<EmergencyStopAllEvent>().Subscribe(OnTaskInternalEmergencyStop);
            _ea.GetEvent<SystemResetResultEvent>().Subscribe(OnSystemResetResult, ThreadOption.PublisherThread, false);
            // 订阅配置变更事件，运行时热更新安全开关
            _ea.GetEvent<Core.Events.DeviceConfigChangedEvent>().Subscribe(OnDeviceConfigChanged);
            _localization.LanguageChanged += OnLanguageChanged;
            _signalTimer = new Timer(OnTimerTick, null, 0, 20);
            _blinkTimer = new Timer(OnBlinkTick, null, Timeout.Infinite, Timeout.Infinite);
            _buzzerPulseTimer = new Timer(OnBuzzerPulseTick, null, Timeout.Infinite, Timeout.Infinite);
            UpdateLightsAndBuzzer();
            ApplyStartupInitPolicy();
        }

        /// <summary>
        /// 应用启动初始化策略：默认 WAITRESET 需整机初始化；调试关闭 RequireInitOnRestart 时直接进入 WAITRUN。
        /// </summary>
        private void ApplyStartupInitPolicy()
        {
            if (!_appSettings.Settings.RequireInitOnRestart && _currentState == StationState.WAITRESET)
            {
                _logger.Info("调试模式：已关闭「重开需初始化」，设备状态直接进入 WAITRUN。");
                TransitionTo(StationState.WAITRUN);
            }
        }
        /// <summary>
        /// 从 AppSettings 读取安全功能开关
        /// </summary>
        private void LoadSafetySettings(AppSettings settings)
        {
            _safetyGateEnabled = settings.EnableSafetyGate;
            _gratingEnabled = settings.EnableGrating;
            _buzzerEnabled = settings.EnableBuzzer;
            _safetyEventLogEnabled = settings.EnableSafetyEventLog;
        }
        /// <summary>
        /// 配置变更回调：热更新安全功能开关
        /// 蜂鸣器禁用时立即停止硬件输出
        /// </summary>
        private void OnDeviceConfigChanged(AppSettings settings)
        {
            LoadSafetySettings(settings);
            if (!_buzzerEnabled)
                StopBuzzerImmediately();
            _logger.Info($"Safety settings updated: Gate={_safetyGateEnabled}, Grating={_gratingEnabled}, Buzzer={_buzzerEnabled}, EventLog={_safetyEventLogEnabled}");
        }
        private void InitializeMappings()
        {
            _controlButtons.Clear();
            _safetySignals.Clear();
            _estopSignals.Clear();
            _safetyGateSignals.Clear();
            _gratingSignals.Clear();
            _signalLookup.Clear();
            foreach (var sig in _config.Signals)
            {
                if (!sig.LogicalId.HasValue) continue;
                _signalLookup[sig.LogicalId.Value] = sig;
                switch (sig.Group)
                {
                    case "ControlButtons": _controlButtons.Add(sig); break;
                    case "EStop": _estopSignals.Add(sig); break;
                    case "SafetyGates": _safetyGateSignals.Add(sig); break;
                    case "Grating": _gratingSignals.Add(sig); break;
                }
            }
            // 合并安全门和光幕信号为统一安全信号列表
            _safetySignals = _safetyGateSignals.Concat(_gratingSignals).ToList();
            foreach (var os in _config.OutputSignals)
            {
                if (os.LogicalId.HasValue) _outputLookup[os.LogicalId.Value] = os;
            }
            foreach (var light in _config.Lights)
            {
                if (light.LogicalId.HasValue)
                {
                    _lightLookup[light.LogicalId.Value] = light;
                    if (!string.IsNullOrEmpty(light.LightType)) _lightByType[light.LightType] = light;
                }
            }
            _logger.Info($"TowerLights 配置加载完成: [{string.Join(", ", _lightByType.Keys)}], 共{_lightByType.Count}项");
            foreach (var kv in _signalLookup) _previousDiStates[kv.Key] = false;
        }
        private void OnTimerTick(object state) => UpdateSignalStates();
        private void OnTaskInternalEmergencyStop()
        {
            if (_currentState != StationState.ESTOP) 
                TransitionTo(StationState.ESTOP);
        }
        public void UpdateSignalStates()
        {
            lock (_stateLock)
            {
                foreach (var kv in _signalLookup)
                {
                    int logicalId = kv.Key;
                    var signal = kv.Value;
                    bool currentActive = IsSignalActive(signal);
                    bool previousActive = _previousDiStates[logicalId];
                    if (currentActive != previousActive)
                    {
                        _previousDiStates[logicalId] = currentActive;
                        if (_safetyEventLogEnabled && IsSafetySignal(signal))
                        {
                            string signalCategory = GetSafetySignalCategory(signal);
                            _logger.Info($"Safety event: [{signalCategory}] signal '{signal.Name}' (LogicalId={logicalId}) changed from {previousActive} to {currentActive}");
                        }
                        if (currentActive && signal.Type == "Momentary") OnSignalActivated(signal);
                    }
                }
                CheckSafetyAndEStop();
                CheckResetButtonLongPress();
            }
        }
        /// <summary>
        /// 判断信号是否属于安全类（安全门或光幕）
        /// </summary>
        private bool IsSafetySignal(SignalConfig signal)
        {
            return signal.Group == "SafetyGates" || signal.Group == "Grating" || signal.Group == "EStop";
        }
        /// <summary>
        /// 获取安全信号的分类名称
        /// </summary>
        private string GetSafetySignalCategory(SignalConfig signal)
        {
            return signal.Group switch
            {
                "SafetyGates" => "SafetyGate",
                "Grating" => "Grating",
                "EStop" => "EStop",
                _ => signal.Group
            };
        }
        private void OnSignalActivated(SignalConfig signal)
        {
            if (signal.Group == "ControlButtons")
            {
                switch (signal.Name)
                {
                    case "StartButton": RequestStart(); break;
                    case "StopButton": RequestStop(); break;
                    case "ResetButton":
                        _buzzerLatched = false;
                        UpdateBuzzerOutput();
                        break;
                }
            }
        }
        private void CheckSafetyAndEStop()
        {
            // 模拟模式下无真实硬件，DI默认为0会导致LowActive信号误判为激活
            // 跳过安全检查，用户可通过 SimulateSafetyTrigger 手动测试安全逻辑
            if (_motion.IsSimulationMode) return;

            bool estopActive = _estopSignals.Any(s => IsSignalActive(s));
            if (estopActive && _currentState != StationState.ESTOP)
            {
                RequestEmergencyStop();
                return;
            }
            if (_safetyGateEnabled)
            {
                bool gateActive = _safetyGateSignals.Any(s => IsSignalActive(s));
                if (gateActive && _currentState == StationState.RUNNING)
                {
                    _logger.Warn("Safety gate signal active, pausing.");
                    RequestPause();
                    return;
                }
            }
            if (_gratingEnabled)
            {
                bool gratingActive = _gratingSignals.Any(s => IsSignalActive(s));
                if (gratingActive && _currentState == StationState.RUNNING)
                {
                    _logger.Warn("Grating signal active, pausing.");
                    RequestPause();
                    return;
                }
            }
        }

        /// <summary>
        /// 判断信号是否处于激活状态，考虑极性：
        /// LowActive（默认）: DI=0 为激活（常闭触点，断线安全）
        /// HighActive: DI=1 为激活（常开触点）
        /// </summary>
        private bool IsSignalActive(SignalConfig signal)
        {
            if (!signal.LogicalId.HasValue) return false;
            bool raw = _motion.ReadDi(signal.LogicalId.Value);
            return signal.Polarity == "HighActive" ? raw : !raw;
        }
        /// <summary>
        /// 立即停止蜂鸣器硬件输出（用于禁用蜂鸣器时调用）
        /// </summary>
        private void StopBuzzerImmediately()
        {
            _buzzerPulseTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _buzzerActualOutput = false;
            WriteBuzzer(false);
        }
        /// <summary>
        /// 检测复位按钮长按5秒以上，触发整机初始化。
        /// 注意：若复位按钮信号长亮（始终为激活状态），需在 hwcfg.xml 中取反 Polarity 配置：
        ///   - 当前 polarity="LowActive" 时改为 polarity="HighActive"
        ///   - 当前 polarity="HighActive" 时改为 polarity="LowActive"
        /// 信号取反后，未按下时 DI 读取为非激活，按下时读取为激活。
        /// 安全机制：硬件未连接时 DI 读取为 0，LowActive 信号会被误判为激活（按下），
        /// 因此在检测前先校验控制卡连接状态，避免未连接硬件时误触发初始化。
        /// </summary>
        private void CheckResetButtonLongPress()
        {
            // 硬件未连接时跳过长按检测：未连接硬件时 DI 读取为 0，
            // LowActive 信号会被误判为激活（按下），导致误触发初始化
            if (!IsControlCardConnected())
            {
                if (_resetButtonPressedTime != null || _resetLongPressHandled)
                {
                    _logger.Warn("控制卡未连接，复位按钮长按检测已禁用，避免误触发整机初始化。");
                    _resetButtonPressedTime = null;
                    _resetLongPressHandled = false;
                }
                return;
            }

            var resetSignals = _controlButtons.Where(s => s.Name == "ResetButton" && s.LogicalId.HasValue).ToList();
            if (resetSignals.Count == 0) return;

            bool isPressed = resetSignals.Any(s => IsSignalActive(s));

            // 信号反转检测：如果复位按钮在系统启动后始终为激活状态（长亮），
            // 提示用户检查 hwcfg.xml 中的 Polarity 配置
            if (!isPressed)
            {
                _resetButtonPressedTime = null;
                _resetLongPressHandled = false;
                return;
            }

            if (_resetButtonPressedTime == null)
            {
                _resetButtonPressedTime = DateTime.Now;
                _resetLongPressHandled = false;
                _logger.Info("复位按钮已按下，长按5秒将触发整机初始化...");
            }
            else if (!_resetLongPressHandled && (DateTime.Now - _resetButtonPressedTime.Value).TotalSeconds >= 5)
            {
                _logger.Warn("复位按钮长按5秒 -> 触发整机初始化。");
                _resetLongPressHandled = true;
                // 先驱动状态机 WAITRESET → RESETING（复位条件不满足时仅记录警告，不阻塞初始化）
                RequestReset();
                // 发布整机初始化请求事件，MachineInitializationService 订阅后执行初始化序列
                _ea.GetEvent<Core.Events.MachineInitializationRequestedEvent>().Publish();
            }
        }

        /// <summary>
        /// 检查控制卡是否已连接且通信正常。
        /// 初始化前安全校验：非模拟模式且 EtherCAT 总线无错误时返回 true。
        /// 用于避免未连接硬件时执行初始化导致异常或误动作。
        /// </summary>
        /// <returns>true=控制卡已连接且总线正常；false=模拟模式或总线异常</returns>
        private bool IsControlCardConnected()
        {
            // 模拟模式下无真实硬件卡，视为未连接
            if (_motion.IsSimulationMode)
                return false;

            // EtherCAT 总线错误码非 0 表示通信异常
            int busError = _motion.GetEtherCatBusErrorCode();
            if (busError != 0)
            {
                _logger.Warn($"控制卡连接异常：EtherCAT 总线错误码 0x{busError:X}。");
                return false;
            }

            return true;
        }
        // ---------- 状态机控制 ----------
        public void RequestStart()
        {
            if (!CanStart) return;
            if (_currentState == StationState.WAITRUN || _currentState == StationState.PAUSE)
                TransitionTo(StationState.RUNNING);
        }
        public void RequestStop()
        {
            //if (_currentState == StationState.RUNNING || _currentState == StationState.PAUSE)
                TransitionTo(StationState.STOP);
         }
        public void RequestPause()
        {
            if (_currentState == StationState.RUNNING) TransitionTo(StationState.PAUSE);
        }
        public void RequestResume()
        {
            if (_currentState == StationState.PAUSE)
            {
                if (_safetyGateEnabled && _safetyGateSignals.Any(s => IsSignalActive(s)))
                {
                    _logger.Warn("Cannot resume: safety gate signal still active.");
                    return;
                }
                if (_gratingEnabled && _gratingSignals.Any(s => IsSignalActive(s)))
                {
                    _logger.Warn("Cannot resume: grating signal still active.");
                    return;
                }
                TransitionTo(StationState.RUNNING);
            }
        }
        public void RequestReset()
        {
            if (_currentState != StationState.STOP &&
                _currentState != StationState.ESTOP &&
                _currentState != StationState.ALARM &&
                _currentState != StationState.WAITRESET)
                return;
            if (!CanReset)
            {
                _logger.Warn("Reset conditions not met.");
                return;
            }
            TransitionTo(StationState.RESETING);
        }
        public void RequestEmergencyStop()
        {
            if (_currentState == StationState.ESTOP) return;
            TransitionTo(StationState.ESTOP);
            // 广播急停：停止所有工站任务（与 UI 急停按钮行为一致）
            _ea.GetEvent<EmergencyStopAllEvent>().Publish();
        }
        private void TransitionTo(StationState newState)
        {
            if (_currentState == newState) return;
            _logger.Info($"State transition: {_currentState} -> {newState}");
            _currentState = newState;
            _resetButtonPressedTime = null;
            UpdateLightsAndBuzzer();
            NotifyStateChanged();
        }
        // ---------- 条件判断 ----------
        public bool CanStart => (_currentState == StationState.WAITRUN || _currentState == StationState.PAUSE) &&
                                !(_safetyGateEnabled && _safetyGateSignals.Any(s => IsSignalActive(s))) &&
                                !(_gratingEnabled && _gratingSignals.Any(s => IsSignalActive(s))) &&
                                !_estopSignals.Any(s => IsSignalActive(s)) &&
                                IsEtherCatBusHealthy();

        /// <summary>启动前检查 EtherCAT 总线（nmc_get_errcode==0）</summary>
        private bool IsEtherCatBusHealthy()
        {
            if (_motion.IsSimulationMode)
                return true;
            return _motion.GetEtherCatBusErrorCode() == 0;
        }

        public bool CanPause => _currentState == StationState.RUNNING;
        public bool CanResume => _currentState == StationState.PAUSE;
        public bool CanReset => CheckResetConditions();
        private bool CheckResetConditions()
        {
            var mustOffs = _config.Signals.Where(s => s.Name == "ResetMustOff" && s.LogicalId.HasValue);
            var mustOns = _config.Signals.Where(s => s.Name == "ResetMustOn" && s.LogicalId.HasValue);
            bool offOk = mustOffs.All(s => !IsSignalActive(s));
            bool onOk = mustOns.All(s => IsSignalActive(s));
            return offOk && onOk;
        }
        // ---------- 指示灯与蜂鸣器控制 ----------
        /// <summary>
        /// 根据当前状态更新三色灯和蜂鸣器
        /// 蜂鸣器采用锁存机制：报警/急停时锁存启动，仅复位按钮上升沿或禁用蜂鸣器可停止
        /// 脉冲节奏：3秒响 / 2秒停
        /// </summary>
        private void UpdateLightsAndBuzzer()
        {
            _greenOn = _greenBlink = false;
            _redOn = _redBlink = false;
            _orangeOn = _orangeBlink = false;
            switch (_currentState)
            {
                case StationState.RUNNING: _greenOn = true; break;
                case StationState.PAUSE: _greenBlink = true; break;
                case StationState.ALARM:
                case StationState.TIP:
                    _redBlink = true;
                    _buzzerLatched = true;
                    break;
                case StationState.STOP:
                case StationState.WAITRESET:
                    _redBlink = true; break;
                case StationState.WAITRUN: _orangeBlink = true; break;
                case StationState.ESTOP:
                    _redBlink = true;
                    _buzzerLatched = true;
                    break;
                case StationState.RESETING: _orangeOn = true; break;
                default: _orangeOn = true; break;
            }
            UpdateBuzzerOutput();
            bool needBlink = _greenBlink || _redBlink || _orangeBlink;
            _blinkTimer.Change(needBlink ? 0 : Timeout.Infinite, needBlink ? 800 : Timeout.Infinite);
            ApplyLightStates();
        }

        /// <summary>
        /// 根据锁存标志和蜂鸣器启用状态更新蜂鸣器硬件输出
        /// 锁存=true 且 启用=true → 启动脉冲（3s开/2s停）
        /// 锁存=false 或 启用=false → 立即停止
        /// _buzzerActualOutput 仅在蜂鸣器启用且锁存时为 true，确保 UI 蜂鸣器图标与硬件一致
        /// </summary>
        private void UpdateBuzzerOutput()
        {
            bool shouldBeActive = _buzzerLatched && _buzzerEnabled;
            if (shouldBeActive)
            {
                _buzzerActualOutput = true;
                WriteBuzzer(true);
                _buzzerPulseTimer.Change(3000, Timeout.Infinite);
            }
            else
            {
                _buzzerPulseTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _buzzerActualOutput = false;
                WriteBuzzer(false);
            }
        }

        /// <summary>
        /// 蜂鸣器脉冲回调：3秒响 / 2秒停 循环
        /// 若锁存已清除或蜂鸣器已禁用，立即停止不再继续
        /// </summary>
        private void OnBuzzerPulseTick(object state)
        {
            if (!_buzzerLatched || !_buzzerEnabled)
            {
                _buzzerActualOutput = false;
                WriteBuzzer(false);
                return;
            }
            _buzzerActualOutput = !_buzzerActualOutput;
            WriteBuzzer(_buzzerActualOutput);
            _buzzerPulseTimer.Change(_buzzerActualOutput ? 3000 : 2000, Timeout.Infinite);
        }
        private void OnBlinkTick(object state)
        {
            _blinkPhase = !_blinkPhase;
            ApplyLightStates();
            NotifyStateChanged();
        }
        private void ApplyLightStates()
        {
            _lastGreen = _greenOn || (_greenBlink && _blinkPhase);
            _lastRed = _redOn || (_redBlink && _blinkPhase);
            _lastOrange = _orangeOn || (_orangeBlink && _blinkPhase);
            WriteLight("Green", _lastGreen);
            WriteLight("Red", _lastRed);
            WriteLight("Orange", _lastOrange);
        }
        // 通用通知方法
        private void NotifyStateChanged()
        {
            _ea.GetEvent<StationStateChangedEvent>().Publish(new StationStatePayload
            {
                State = _currentState,
                Description = GetStateDescription(),
                GreenLight = _lastGreen,
                RedLight = _lastRed,
                OrangeLight = _lastOrange,
                Buzzer = _buzzerActualOutput
            });
        }
        private void OnSystemResetResult(bool isSuccess)
        {
            // 支持从 RESETING 或 WAITRESET 转换（长按复位按钮时 CanReset 不满足则停在 WAITRESET）
            if (_currentState != StationState.RESETING && _currentState != StationState.WAITRESET) return;
            if (isSuccess) TransitionTo(StationState.WAITRUN);
            else TransitionTo(StationState.ALARM);
        }
        private void WriteLight(string lightType, bool turnOn)
        {
            if (_lightByType.TryGetValue(lightType, out var light) && light.LogicalId.HasValue)
            {
                _motion.WriteDo(light.LogicalId.Value, turnOn);
            }
            else
            {
                _logger.Warn($"WriteLight: 未找到灯光配置 LightType='{lightType}', 可用类型=[{string.Join(",", _lightByType.Keys)}]");
            }
        }
        private void WriteBuzzer(bool on)
        {
            if (!_buzzerEnabled) return;
            if (_lightByType.TryGetValue("Buzzer", out var buzzer) && buzzer.LogicalId.HasValue)
            {
                _motion.WriteDo(buzzer.LogicalId.Value, on);
            }
            else
            {
                _logger.Warn($"WriteBuzzer: 未找到蜂鸣器配置, 可用类型=[{string.Join(",", _lightByType.Keys)}]");
            }
        }
        private string GetStateDescription() => _currentState switch
        {
            StationState.ESTOP => _localization.GetResource("StateDesc_EStop"),
            StationState.ALARM => _localization.GetResource("StateDesc_Alarm"),
            StationState.PAUSE => _localization.GetResource("StateDesc_Paused"),
            StationState.RESETING => _localization.GetResource("StateDesc_Resetting"),
            StationState.RUNNING => _localization.GetResource("StateDesc_Running"),
            StationState.STOP => _localization.GetResource("StateDesc_Stopped"),
            StationState.WAITRESET => _localization.GetResource("StateDesc_WaitReset"),
            StationState.CLEAR => _localization.GetResource("StateDesc_Clearing"),
            StationState.TIP => _localization.GetResource("StateDesc_TipAlarm"),
            StationState.WAITRUN => _localization.GetResource("StateDesc_WaitRun"),
            _ => _localization.GetResource("StateDesc_Unknown")
        };
        private void OnLanguageChanged(object sender, Core.Abstraction.LanguageChangedEventArgs e)
        {
            NotifyStateChanged();
        }
        public void Dispose()
        {
            if (!_disposed)
            {
                _localization.LanguageChanged -= OnLanguageChanged;
                _signalTimer?.Dispose();
                _blinkTimer?.Dispose();
                _buzzerPulseTimer?.Dispose();
                _disposed = true;
            }
        }
        public void SimulateButtonPress(string buttonName)
        {
            switch (buttonName)
            {
                case "StartButton": RequestStart(); break;
                case "StopButton": RequestStop(); break;
                case "ResetButton":
                    _buzzerLatched = false;
                    UpdateBuzzerOutput();
                    RequestReset();
                    break;
                case "EmergencyStop": RequestEmergencyStop(); break;
            }
        }
        public void SimulateSafetyTrigger(string signalName)
        {
            if (signalName.Contains("EmergencyStop")) RequestEmergencyStop();
            else if (signalName.Contains("Door") || signalName.Contains("Gate"))
            {
                if (_safetyGateEnabled && _currentState == StationState.RUNNING) RequestPause();
            }
            else if (signalName.Contains("Grating"))
            {
                if (_gratingEnabled && _currentState == StationState.RUNNING) RequestPause();
            }
        }
    }
}