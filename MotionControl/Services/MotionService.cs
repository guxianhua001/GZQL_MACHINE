using AlarmModule.Interfaces;
using AlarmModule.Models;
using Core.Abstraction;
using Core.Utilities;
using MotionControl.Card;
using MotionControl.Events;
using MotionControl.Exceptions;
using MotionControl.Interfaces;
using MotionControl.Models;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace MotionControl.Services
{
    /// <summary>
    /// 运动控制服务实现
    /// 支持事件驱动的轴状态监控（IObservable），替代定时器轮询
    /// </summary>
    public class MotionService : IMotionService, IDisposable
    {
        private readonly IMotionCardFactory _cardFactory;
        private readonly IHardwareConfigLoader _configLoader;
        private readonly IEventAggregator _ea;
        private readonly ILoggerService _logger;
        /// <summary> 报警服务：用于在检测到轴报警时触发报警记录 </summary>
        private readonly IAlarmService _alarmService;
        /// <summary> AD值转换器：将原始AD值转换为物理量 </summary>
        private readonly IADValueConverter _adConverter;
        /// <summary> 安全区域监控器：运动前安全互锁检查，防止轴进入危险区域 </summary>
        private readonly ISafetyZoneMonitor _safetyZoneMonitor;
        private readonly IAxisOperationPanelState _axisPanelState;
        private MotionSystemConfig _config;

        private List<IMotionCard> _cards = new();
        private Dictionary<int, AxisState> _axisStates = new();
        /// <summary>逻辑轴号 → 卡物理轴号(actAxisId)</summary>
        private Dictionary<int, int> _logicalToPhysicalAxis = new();
        private Dictionary<int, IoState> _inputs = new();
        private Dictionary<int, IoState> _outputs = new();
        private Dictionary<int, IMotionCard> _axisCardMap = new();
        private Dictionary<int, IMotionCard> _ioCardMap = new();

        /// <summary> 是否运行在模拟环境（无真实雷赛/硬件卡） </summary>
        public bool IsSimulationMode => !_cards.Any(c => c is not VirtualMotionCard);

        // EtherCAT 总线状态轮询（约 1s 一次）
        private int _busPollCounter;
        private int _lastBusErrorCode = int.MinValue;
        private bool _lastPublishedIsSimulation = true;

        /// <inheritdoc />
        public int GetEtherCatBusErrorCode() => _lastBusErrorCode == int.MinValue ? ReadEtherCatBusErrorCode() : _lastBusErrorCode;

        // 高精度轮询线程；面板关闭时 100ms，打开时 10ms
        private Thread _pollThread;
        private volatile int _pollIntervalMs = 100;
        private const int FastPollIntervalMs = 10;
        private const int SlowPollIntervalMs = 100;
        private int _outputSlowCounter;
        private int _inputSlowCounter;
        /// <summary>回零状态慢轮询游标：每周期只查 1 根轴，避免 CheckHomeDone 拖慢整轮采样</summary>
        private int _homePollCursor;

        /// <summary>上一周期轴采样缓存，仅变化时发布事件，降低 UI 与订阅开销</summary>
        private readonly Dictionary<int, AxisPollSnapshot> _axisPollSnapshots = new();
        private CancellationTokenSource _pollCts;
        private readonly ManualResetEventSlim _stopEvent = new ManualResetEventSlim(false);
        private bool _isPolling;
        private readonly object _lock = new();

        // ========== IObservable<AxisStateChangedEvent> 实现 ==========
        private readonly List<IObserver<AxisStateChangedEvent>> _observers = new();
        private readonly object _observerLock = new();

        public IDisposable Subscribe(IObserver<AxisStateChangedEvent> observer)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            lock (_observerLock)
            {
                _observers.Add(observer);
            }

            // 新订阅者立即收到各轴缓存快照，避免 UI 等待首次变化
            PushCachedAxisStatesToObserver(observer);
            return new UnsubscribeAction(this, observer);
        }

        /// <summary>向单个订阅者推送当前轴缓存（Subscribe 时调用）</summary>
        private void PushCachedAxisStatesToObserver(IObserver<AxisStateChangedEvent> observer)
        {
            foreach (var kv in _axisStates)
            {
                int axisId = kv.Key;
                if (!_axisPollSnapshots.TryGetValue(axisId, out var snap) || !snap.IsInitialized)
                    continue;

                try
                {
                    observer.OnNext(new AxisStateChangedEvent
                    {
                        AxisId = axisId,
                        Name = kv.Value.Name,
                        Position = snap.Position,
                        IsMoving = snap.IsMoving,
                        IsAlarmed = snap.IsAlarmed,
                        IsServoOn = snap.IsServoOn,
                        IsMEL = snap.IsMEL,
                        IsORG = snap.IsORG,
                        IsPEL = snap.IsPEL,
                        IsASTP = snap.IsASTP,
                        IsHomeOk = snap.IsHomeOk,
                        StatusWord = snap.StatusWord
                    });
                }
                catch { /* 忽略 */ }
            }
        }

        // 内部类：取消订阅
        private class UnsubscribeAction : IDisposable
        {
            private readonly MotionService _service;
            private readonly IObserver<AxisStateChangedEvent> _observer;
            private bool _disposed;

            public UnsubscribeAction(MotionService service, IObserver<AxisStateChangedEvent> observer)
            {
                _service = service;
                _observer = observer;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    lock (_service._observerLock)
                    {
                        _service._observers.Remove(_observer);
                    }
                    _disposed = true;
                }
            }
        }

        /// <summary>
        /// 发布轴状态变更事件给所有订阅者
        /// 在轮询线程中调用，确保 UI 线程安全
        /// </summary>
        private void PublishAxisStateChanged(AxisStateChangedEvent e)
        {
            List<IObserver<AxisStateChangedEvent>> snapshot;
            lock (_observerLock)
            {
                snapshot = _observers.ToList();
            }
            foreach (var obs in snapshot)
            {
                try { obs.OnNext(e); }
                catch { /* 忽略订阅者异常 */ }
            }
        }

        public MotionService(IMotionCardFactory cardFactory, IHardwareConfigLoader configLoader,
                             IEventAggregator ea, ILoggerService logger, IAlarmService alarmService,
                             IADValueConverter adConverter, ISafetyZoneMonitor safetyZoneMonitor,
                             IAxisOperationPanelState axisPanelState)
        {
            _cardFactory = cardFactory;
            _configLoader = configLoader;
            _ea = ea;
            _logger = logger;
            _alarmService = alarmService;
            _adConverter = adConverter;
            _safetyZoneMonitor = safetyZoneMonitor;
            _axisPanelState = axisPanelState;

            if (_axisPanelState != null)
            {
                _pollIntervalMs = _axisPanelState.IsPanelOpen ? FastPollIntervalMs : SlowPollIntervalMs;
                _axisPanelState.PanelOpenChanged += OnAxisPanelOpenChanged;
            }
        }

        /// <summary>面板开关时切换轮询频率</summary>
        private void OnAxisPanelOpenChanged(bool isOpen)
        {
            _pollIntervalMs = isOpen ? FastPollIntervalMs : SlowPollIntervalMs;
            _logger.Info($"轴操作面板{(isOpen ? "打开" : "关闭")}，轮询间隔 {_pollIntervalMs}ms");
        }

        // ---------- 初始化 ----------
        public async Task InitializeAsync()
        {
            _config = _configLoader.Load();

            // 从 hwcfg.xml 加载 AD 模拟量通道配置到转换器（Singleton，全局生效）
            if (_config.AnalogInputs?.Count > 0)
            {
                foreach (var adCfg in _config.AnalogInputs)
                    _adConverter.UpdateChannelConfig(adCfg);
                _logger.Info($"已加载 {_config.AnalogInputs.Count} 个 AD 通道配置");
            }

            foreach (var cardCfg in _config.Cards)
            {
                var card = _cardFactory.GetCard(cardCfg.Index);
                if (card == null)
                {
                    _logger.Warn($"Card index {cardCfg.Index} not available (hardware may be missing)");
                    continue;
                }

                // 步骤1：检查总线状态
                int busStatus = card.CheckEtherCatStatus();
                if (busStatus != 0)
                {
                    _logger.Warn($"Card {cardCfg.Index} EtherCAT bus error (0x{busStatus:X}), attempting soft reset...");
                    card.SoftReset();
                    // 重置后再次检查
                    busStatus = card.CheckEtherCatStatus();
                    if (busStatus != 0)
                    {
                        _logger.Error($"Card {cardCfg.Index} bus reset failed, status: 0x{busStatus:X}. Skipping config load.");
                        continue;   // 跳过该卡，不加载配置
                    }
                }

                // 步骤2：总线正常后下载配置文件（path 为空时已在 HardwareConfigParser 中回退到轴卡配置文件节点）
                var configPath = cardCfg.ConfigPath;
                if (string.IsNullOrWhiteSpace(configPath))
                {
                    _logger.Warn($"Card {cardCfg.Index} ConfigPath is empty and no default config found in hwcfg.xml. Skipping config load.");
                    _cards.Add(card);
                    continue;
                }

                // 在后台线程调用雷赛加载配置文件
                int loadResult = await Task.Run(() => card.LoadConfig(configPath)).ConfigureAwait(false);
                if (loadResult != 0)
                    _logger.Error($"Card {cardCfg.Index} failed to load config '{configPath}', error code: {loadResult}");
                else
                    _logger.Info($"Card {cardCfg.Index} loaded config '{configPath}'");

                _cards.Add(card);
            }
            BuildMappings(_config);
            PublishEtherCatBusStatus(force: true);
        }
        private void BuildMappings(MotionSystemConfig config)
        {
            // 若无任何真实卡，则添加一张虚拟卡，以便轴/IO映射正常进行
            if (_cards.Count == 0)
            {
                _logger.Warn("No hardware cards found. Adding a virtual card for simulation.");
                _cards.Add(new VirtualMotionCard(-1));
            }

            // 轴映射
            foreach (var ax in config.Axes)
            {
                _axisStates[ax.LogicalId] = new AxisState { AxisId = ax.LogicalId, Name = ax.Name };
                _logicalToPhysicalAxis[ax.LogicalId] = ax.AxisId;
                var card = _cards.FirstOrDefault(c => c.CardId == ax.CardId);
                if (card != null)
                    _axisCardMap[ax.LogicalId] = card;
                else
                    _axisCardMap[ax.LogicalId] = _cards.First(); // 回退到第一张卡（虚拟卡）
            }

            // 输入映射
            foreach (var di in config.Inputs)
            {
                _inputs[di.LogicalId] = new IoState { Port = di.Port, Name = di.Name, IsInput = true };
                var card = _cards.FirstOrDefault(c => c.CardId == di.CardId);
                if (card != null)
                    _ioCardMap[di.LogicalId] = card;
                else
                    _ioCardMap[di.LogicalId] = _cards.First(); // 回退
            }

            // 输出映射
            foreach (var dout in config.Outputs)
            {
                _outputs[dout.LogicalId] = new IoState { Port = dout.Port, Name = dout.Name, IsInput = false };
                var card = _cards.FirstOrDefault(c => c.CardId == dout.CardId);
                if (card != null)
                    _ioCardMap[dout.LogicalId] = card;
                else
                    _ioCardMap[dout.LogicalId] = _cards.First(); // 回退
            }
        }
        public void Shutdown()
        {
            StopPolling();
            foreach (var card in _cards) card.Close();
        }

        // ---------- 轴操作（通过映射路由到对应卡） ----------
        private IMotionCard GetCardForAxis(int logicalAxisId)
        {
            if (_axisCardMap.TryGetValue(logicalAxisId, out var card))
                return card;
            throw new InvalidOperationException($"Axis {logicalAxisId} not mapped to any card");
        }

        /// <summary>逻辑轴号转卡物理轴号；未映射时回退为原值</summary>
        private int ToPhysicalAxisId(int logicalAxisId)
            => _logicalToPhysicalAxis.TryGetValue(logicalAxisId, out int physical) ? physical : logicalAxisId;

        private (IMotionCard card, int physicalId) ResolveAxis(int logicalAxisId)
            => (GetCardForAxis(logicalAxisId), ToPhysicalAxisId(logicalAxisId));

        public void EnableAxis(int axisId)
        {
            var (card, pid) = ResolveAxis(axisId);
            card.SetServo(pid, true);
        }

        public void DisableAxis(int axisId)
        {
            var (card, pid) = ResolveAxis(axisId);
            card.SetServo(pid, false);
        }

        public async Task MoveAbsAsync(int axisId, double position, double velocity, CancellationToken token = default)
        {
            var (card, pid) = ResolveAxis(axisId);
            await Task.Run(() =>
            {
                var (allowed, reason) = _safetyZoneMonitor.CheckMoveAllowed(axisId, position);
                if (!allowed)
                {
                    _logger.Error($"[安全互锁] 轴{axisId}绝对移动被拒绝 | 目标位置:{position:F3} | 原因:{reason}");
                    throw new SafetyViolationException($"轴{axisId}绝对移动被安全策略拒绝: {reason}", axisId, reason);
                }

                double startPos = card.GetPosition(pid);
                double distance = Math.Abs(position - startPos);
                int timeoutMs = CalculateMotionTimeout(distance, velocity);

                card.MoveAbs(pid, position, velocity);
                WaitForDone(card, pid, position, token, timeoutMs: timeoutMs);
            }, token);
        }

        /// <summary>
        /// 多轴同步绝对运动：所有轴同时下发运动指令，统一轮询等待完成。
        /// 到位判据 = CheckDone(卡完成信号) + GetPosition(编码器位置验证)，双重保险。
        /// </summary>
        public async Task MoveAbsMultiAxisAsync(
            IReadOnlyList<(int axisId, double position, double velocity)> moves,
            CancellationToken token = default)
        {
            if (moves == null || moves.Count == 0) return;

            // 预解析所有轴（在主线程，无锁竞争）
            var resolved = new List<(int logicalId, IMotionCard card, int pid, double targetPos, double velocity)>(moves.Count);
            foreach (var (axisId, position, velocity) in moves)
            {
                var (card, pid) = ResolveAxis(axisId);
                resolved.Add((axisId, card, pid, position, velocity));
            }

            await Task.Run(() =>
            {
                // 1. 安全检查 + 下发运动指令（所有轴先全部下发）
                foreach (var (logicalId, card, pid, targetPos, velocity) in resolved)
                {
                    var (allowed, reason) = _safetyZoneMonitor.CheckMoveAllowed(logicalId, targetPos);
                    if (!allowed)
                    {
                        _logger.Error($"[安全互锁] 轴{logicalId}绝对移动被拒绝 | 目标位置:{targetPos:F3} | 原因:{reason}");
                        throw new SafetyViolationException($"轴{logicalId}绝对移动被安全策略拒绝: {reason}", logicalId, reason);
                    }
                    card.MoveAbs(pid, targetPos, velocity);
                }

                // 2. 统一轮询：位置 + CheckDone 双重到位验证
                //    超时基于各轴最大运动时间动态计算
                var spinWait = new SpinWait();
                var pending = new HashSet<int>(resolved.Select(r => r.pid));
                var sw = System.Diagnostics.Stopwatch.StartNew();
                // 取所有轴中最大超时值，避免低速长距离轴误报超时
                int timeoutMs = resolved.Max(r => CalculateMotionTimeout(
                    Math.Abs(r.targetPos - r.card.GetPosition(r.pid)), r.velocity));
                const double tolerance = 0.05;

                while (pending.Count > 0)
                {
                    token.ThrowIfCancellationRequested();

                    if (sw.ElapsedMilliseconds > timeoutMs)
                    {
                        // 超时：列出未完成轴信息
                        var stuckAxes = resolved.Where(r => pending.Contains(r.pid))
                            .Select(r => $"轴{r.logicalId}(pid={r.pid}) 目标={r.targetPos:F3} 当前={r.card.GetPosition(r.pid):F3}");
                        throw new RecoverableException(
                            message: $"多轴运动超时({timeoutMs}ms): {string.Join("; ", stuckAxes)}",
                            suggestedAction: "请检查伺服使能、限位信号或机械卡死，复位后重试。"
                        );
                    }

                    // 双重到位判据：CheckDone=1 且 GetPosition 在 tolerance 内
                    var justDone = new List<int>();
                    foreach (var (_, card, pid, targetPos, _) in resolved)
                    {
                        if (!pending.Contains(pid)) continue;
                        // 先检查卡完成信号
                        if (card.CheckDone(pid) != 1) continue;
                        // 再校验编码器位置（防止虚假完成）
                        double pos = card.GetPosition(pid);
                        if (Math.Abs(pos - targetPos) <= tolerance)
                            justDone.Add(pid);
                        else
                            _logger?.Warn($"[多轴运动] 轴{pid} CheckDone=1 但位置偏差 {Math.Abs(pos - targetPos):F3}mm > {tolerance}mm，继续等待");
                    }
                    foreach (var pid in justDone)
                        pending.Remove(pid);

                    if (pending.Count > 0)
                        spinWait.SpinOnce();
                }
            }, token);
        }

        public async Task MoveRelAsync(int axisId, double distance, double velocity, CancellationToken token = default)
        {
            var (card, pid) = ResolveAxis(axisId);
            await Task.Run(() =>
            {
                double startPos = card.GetPosition(pid);
                double targetPos = startPos + distance;

                var (allowed, reason) = _safetyZoneMonitor.CheckMoveAllowed(axisId, targetPos);
                if (!allowed)
                {
                    _logger.Error($"[安全互锁] 轴{axisId}相对移动被拒绝 | 目标位置:{targetPos:F3} | 原因:{reason}");
                    throw new SafetyViolationException($"轴{axisId}相对移动被安全策略拒绝: {reason}", axisId, reason);
                }

                card.MoveRel(pid, distance, velocity);
                int timeoutMs = CalculateMotionTimeout(Math.Abs(distance), velocity);
                WaitForDone(card, pid, targetPos, token, timeoutMs: timeoutMs);
            }, token);
        }

        /// <inheritdoc />
        public Task MoveRelStartAsync(int axisId, double distance, double velocity)
        {
            return Task.Run(() =>
            {
                var (card, pid) = ResolveAxis(axisId);
                double startPos = card.GetPosition(pid);
                double targetPos = startPos + distance;
                var (allowed, reason) = _safetyZoneMonitor.CheckMoveAllowed(axisId, targetPos);
                if (!allowed)
                {
                    _logger.Error($"[安全互锁] 轴{axisId}相对移动被拒绝 | 目标位置:{targetPos:F3} | 原因:{reason}");
                    throw new SafetyViolationException($"轴{axisId}相对移动被安全策略拒绝: {reason}", axisId, reason);
                }

                card.MoveRel(pid, distance, velocity);
            });
        }

        public async Task MoveLineAbsAsync(int coordId, int[] axisIds, double[] positions, double velocity, CancellationToken token = default)
        {
            var card = GetCardForAxis(axisIds[0]);
            var physicalIds = axisIds.Select(ToPhysicalAxisId).ToArray();
            await Task.Run(() =>
            {
                var (allowed, reason) = _safetyZoneMonitor.CheckInterpolationMoveAllowed(axisIds, positions);
                if (!allowed)
                {
                    _logger.Error($"[安全互锁] 插补移动(坐标系{coordId})被拒绝 | 原因:{reason}");
                    throw new SafetyViolationException($"插补移动被安全策略拒绝: {reason}", axisIds[0], reason);
                }

                card.MoveLineAbs(coordId, physicalIds, positions, velocity);
                WaitForCoordDone(card, coordId, physicalIds, positions, token);
            });
        }

        /// <inheritdoc />
        public Task HomeAxisAsync(int axisId, CancellationToken token = default)
            => RunHomeAsync(axisId, applyHomeMode: false, mode: 0, minVel: 0, maxVel: 0, token);

        /// <inheritdoc />
        public Task HomeAsync(int axisId, int mode = 1, double minVel = 5, double maxVel = 20, CancellationToken token = default)
            => RunHomeAsync(axisId, applyHomeMode: true, mode, minVel, maxVel, token);

        /// <summary>执行回零并等待完成；applyHomeMode=false 时仅 GoHome，沿用卡内已配置参数</summary>
        private async Task RunHomeAsync(int axisId, bool applyHomeMode, int mode, double minVel, double maxVel, CancellationToken token)
        {
            var (card, pid) = ResolveAxis(axisId);
            await Task.Run(() =>
            {
                if (applyHomeMode)
                    card.SetHomeMode(pid, mode, minVel, maxVel);
                card.GoHome(pid);
                WaitHomeComplete(card, pid, axisId, token);
            }, token);
        }

        /// <summary>等待回零流程结束；pid 为物理轴号，logicalId 仅用于日志</summary>
        private static void WaitHomeComplete(IMotionCard card, int pid, int logicalId, CancellationToken token)
        {
            var spinWait = new SpinWait();
            if (card.CheckHomeDone(pid) == 1)
            {
                var waitStart = System.Diagnostics.Stopwatch.StartNew();
                while (card.CheckHomeDone(pid) == 1 && waitStart.ElapsedMilliseconds < 3000)
                {
                    token.ThrowIfCancellationRequested();
                    Thread.Sleep(1);
                }
            }

            var waitDone = System.Diagnostics.Stopwatch.StartNew();
            while (true)
            {
                token.ThrowIfCancellationRequested();
                int homeStatus = card.CheckHomeDone(pid);
                if (homeStatus == 1)
                    break;
                if (homeStatus < 0)
                {
                    throw new RecoverableException(
                        message: $"轴 {logicalId} 回原点失败，错误码: {homeStatus}",
                        suggestedAction: "请检查原点传感器是否正常、回零方向是否正确、未撞限位，复位后重试。"
                    );
                }
                if (waitDone.ElapsedMilliseconds > 120_000)
                {
                    throw new RecoverableException(
                        message: $"轴 {logicalId} 回原点超时",
                        suggestedAction: "请检查使能、限位与原点信号，断使能再上使能或急停复位后重试。"
                    );
                }
                spinWait.SpinOnce();
            }
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (card.CheckDone(pid) == 1)
                    break;
                spinWait.SpinOnce();
            }
        }

        public void JogStart(int axisId, bool positive, double speed)
        {
            var (card, pid) = ResolveAxis(axisId);
            int ret = card.MoveJog(pid, positive ? 0 : 1, speed);
            if (ret != 0)
                _logger.Warn($"JogStart 失败 | 逻辑轴:{axisId} 物理轴:{pid} 返回值:{ret}");
        }

        public void JogStop(int axisId)
        {
            var (card, pid) = ResolveAxis(axisId);
            card.Stop(pid);
        }

        public void StopAxis(int axisId)
        {
            var (card, pid) = ResolveAxis(axisId);
            card.Stop(pid);
        }

        public void EmergencyStop(int axisId)
        {
            var (card, pid) = ResolveAxis(axisId);
            card.EStop(pid);
        }

        public Interfaces.IAxis GetAxisState(int axisId) => _axisStates.TryGetValue(axisId, out var s) ? s : null;

        public void ClearAlarm(int axisId)
        {
            var (card, pid) = ResolveAxis(axisId);
            card.ClearAlarm(pid);
        }

        public async Task<int> CheckHomeDoneAsync(int axisId)
        {
            return await Task.Run(() =>
            {
                var (card, pid) = ResolveAxis(axisId);
                return card.CheckHomeDone(pid);
            });
        }

        public void ClearPosition(int axisId)
        {
            var (card, pid) = ResolveAxis(axisId);
            int ret = card.ClearPosition(pid);
            if (ret != 0)
                throw new InvalidOperationException($"轴 {axisId} 位置清零失败，错误码: {ret}");

            if (_axisStates.TryGetValue(axisId, out var state))
                state.ActualPosition = 0;
        }

        public double GetAxisPosition(int axisId)
        {
            var (card, pid) = ResolveAxis(axisId);
            return card.GetPosition(pid);
        }

        /// <summary>
        /// 等待轴运动完成，并校验最终位置是否在误差允许范围内。
        /// 到位判据 = CheckDone + GetPosition 双重验证，防止虚假完成信号。
        /// 超时基于距离/速度动态计算，避免低速运动误报。
        /// </summary>
        private void WaitForDone(IMotionCard card, int physicalAxisId, double targetPosition, CancellationToken token, double tolerance = 0.05, int timeoutMs = 30_000)
        {
            var spinWait = new SpinWait();
            var sw = System.Diagnostics.Stopwatch.StartNew();
        
            while (true)
            {
                token.ThrowIfCancellationRequested();
        
                if (sw.ElapsedMilliseconds > timeoutMs)
                {
                    double actualPos = card.GetPosition(physicalAxisId);
                    throw new RecoverableException(
                        message: $"轴 {physicalAxisId} 运动超时({timeoutMs}ms)。目标: {targetPosition:F3}, 当前: {actualPos:F3}",
                        suggestedAction: "请检查伺服使能、限位信号或机械卡死，复位后重试。"
                    );
                }
        
                // 双重到位判据：CheckDone=1 且 GetPosition 在 tolerance 内
                int done = card.CheckDone(physicalAxisId);
                if (done == 1)
                {
                    double actualPosition = card.GetPosition(physicalAxisId);
                    if (Math.Abs(actualPosition - targetPosition) <= tolerance)
                        break; // 真正到位
                    // CheckDone=1 但位置不在 tolerance → 继续等待（虚假完成）
                }
                spinWait.SpinOnce();
            }
        }
        
        /// <summary>
        /// 根据运动距离和速度动态计算超时时间（ms）
        /// 公式: max(5000ms基础, 移动时间*2 + 3000ms加减速缓冲)
        /// </summary>
        private static int CalculateMotionTimeout(double distance, double velocity)
        {
            if (velocity <= 0) return 60_000; // 防御性默认
            double moveTimeMs = distance / velocity * 1000;
            return Math.Max(5_000, (int)(moveTimeMs * 2) + 3_000);
        }
        /// <summary>
        /// 等待插补运动完成，并校验所有参与轴的最终位置
        /// </summary>
        private void WaitForCoordDone(IMotionCard card, int coordId, int[] axisIds, double[] targetPositions, CancellationToken token, double tolerance = 0.05)
        {
            var spinWait = new SpinWait();
            while (true)
            {
                // 1. 检查急停/停止取消信号
                token.ThrowIfCancellationRequested();
                // 2. 检查插补坐标系是否运动完成
                int done = card.CheckCoordDone(coordId);
                if (done == 1)
                    break;
                // 如果返回异常状态，主动抛出
                if (done < 0)
                    throw new Exception($"坐标系 {coordId} 状态检查异常！");
                // 3. 自旋等待，保证急停响应速度
                spinWait.SpinOnce();
            }
            // === 运动结束后，校验所有参与插补的轴位置 ===
            for (int i = 0; i < axisIds.Length; i++)
            {
                double actualPosition = card.GetPosition(axisIds[i]);
                if (Math.Abs(actualPosition - targetPositions[i]) > tolerance)
                {
                    throw new RecoverableException(
                        message: $"插补运动未到位。轴 {axisIds[i]} 目标: {targetPositions[i]:F3}, 实际: {actualPosition:F3}",
                        suggestedAction: "请检查插补轴是否撞击限位或存在机械卡死，复位后重试。"
                    );
                }
            }
        }
        // ---------- IO 操作 ----------
        public bool ReadDi(int logicalId)
        {
            if (!_ioCardMap.TryGetValue(logicalId, out var card) || !_inputs.TryGetValue(logicalId, out var io))
                throw new ArgumentException($"Invalid DI logical ID: {logicalId}");
            int val = 0;
            card.GetDi(io.Port, ref val);
            io.Value = (val != 0);
            return io.Value;
        }
        public bool ReadDo(int logicalId)
        {
            if (!_ioCardMap.TryGetValue(logicalId, out var card) ||
                !_outputs.TryGetValue(logicalId, out var io))
                throw new ArgumentException($"Invalid DO logical ID: {logicalId}");

            int raw = 0;
            card.GetDo(io.Port, ref raw);
            io.Value = (raw != 0);          // raw零=ON，1=OFF
            return io.Value;
        }
        public void WriteDo(int logicalId, bool value)
        {
            if (!_ioCardMap.TryGetValue(logicalId, out var card) || !_outputs.TryGetValue(logicalId, out var io))
                throw new ArgumentException($"Invalid DO logical ID: {logicalId}");
            card.SetDo(io.Port, value ? 1 : 0);
            io.Value = value;
        }

        // ---------- 轮询 ----------
        public void StartPolling(int intervalMs = SlowPollIntervalMs)
        {
            if (_isPolling) return;
            _pollIntervalMs = _axisPanelState?.IsPanelOpen == true ? FastPollIntervalMs : intervalMs;
            _pollCts = new CancellationTokenSource();
            _pollThread = new Thread(() => PollLoop(_pollCts.Token))
            {
                IsBackground = true,
                Priority = ThreadPriority.Normal
            };
            _isPolling = true;
            _pollThread.Start();
            PublishEtherCatBusStatus(force: true);
        }

        public void StopPolling()
        {
            if (!_isPolling) return;
            _pollCts.Cancel();
            if (!_pollThread.Join(2000))
                _pollThread.Interrupt();
            _isPolling = false;
        }

        private void PollLoop(CancellationToken token)
        {
            var stopwatch = new System.Diagnostics.Stopwatch();

            while (!token.IsCancellationRequested)
            {
                stopwatch.Restart();
                int intervalMs = _pollIntervalMs;
                bool panelOpen = _axisPanelState?.IsPanelOpen ?? false;

                if (panelOpen)
                {
                    PollVisibleAxesStatus();
                    CheckCriticalAlarms(skipVisibleAxes: true);
                    int slowFactor = Math.Max(1, intervalMs >= 50 ? 4 : 20);
                    if ((_outputSlowCounter++ % slowFactor) == 0)
                        PollOutputsSlow();
                    if ((_inputSlowCounter++ % 10) == 0)
                        PollInputsSlow();
                }
                else
                {
                    CheckCriticalAlarms(skipVisibleAxes: false);
                }

                if ((_busPollCounter++ % Math.Max(1, 1000 / intervalMs)) == 0)
                    PublishEtherCatBusStatus();

                long elapsed = stopwatch.ElapsedMilliseconds;
                long waitMs = intervalMs - elapsed;
                if (waitMs > 0)
                    Thread.Sleep((int)Math.Min(waitMs, intervalMs));
            }
        }

        /// <summary> 检查轴报警/急停；面板打开时可跳过当前 Tab 可见轴（已在 PollVisibleAxesStatus 中处理） </summary>
        private void CheckCriticalAlarms(bool skipVisibleAxes)
        {
            HashSet<int> visibleSet = null;
            if (skipVisibleAxes && _axisPanelState?.VisibleLogicalAxisIds is { Count: > 0 } visibleIds)
                visibleSet = new HashSet<int>(visibleIds);

            foreach (var kv in _axisStates)
            {
                int logicalId = kv.Key;
                if (visibleSet != null && visibleSet.Contains(logicalId)) continue;
                if (!_axisCardMap.TryGetValue(logicalId, out var card)) continue;
                int pid = ToPhysicalAxisId(logicalId);

                int io = 0;
                card.GetMotionIO(pid, ref io);
                bool alarm = (io & Leisai_Define.MIO_ALM) != 0 ||
                             (io & Leisai_Define.MIO_EMG) != 0;

                PublishAxisAlarmTransition(logicalId, kv.Value, alarm);
            }

            // 关键 I/O 在面板关闭时在此快检；面板打开时改由 PollInputsSlow 慢检
            if (!skipVisibleAxes)
                PollInputsSlow();
        }

        /// <summary>轴报警状态变化时发布事件并记录</summary>
        private void PublishAxisAlarmTransition(int logicalId, AxisState state, bool alarm)
        {
            if (alarm == state.IsAlarmed) return;

            state.IsAlarmed = alarm;
            _ea.GetEvent<AxisAlarmEvent>().Publish(new AxisAlarmPayload
            {
                AxisId = logicalId,
                IsAlarm = alarm
            });

            if (alarm)
            {
                _ = _alarmService.TriggerAlarmAsync(
                    "AXIS_ALARM",
                    AlarmLevel.Serious,
                    $"轴{logicalId}报警信号触发",
                    source: $"Axis{logicalId}",
                    type: AlarmType.HardwareFault);
            }
        }

        /// <summary>DI 慢采样（面板打开时约 100ms 一次）</summary>
        private void PollInputsSlow()
        {
            foreach (var kv in _inputs)
            {
                if (!_ioCardMap.TryGetValue(kv.Key, out var card)) continue;
                int val = 0;
                card.GetDi(kv.Value.Port, ref val);
                kv.Value.Value = (val != 0);
            }
        }

        /// <summary>
        /// 面板打开时：仅对当前 Tab 可见轴做快采样（位置/IO/伺服），合并报警检测，单次 GetMotionIO。
        /// 非可见轴仍走 CheckCriticalAlarms 的报警检测。
        /// </summary>
        private void PollVisibleAxesStatus()
        {
            var visibleIds = _axisPanelState?.VisibleLogicalAxisIds;
            bool restrictVisible = visibleIds != null && visibleIds.Count > 0;
            HashSet<int> visibleSet = restrictVisible ? new HashSet<int>(visibleIds) : null;

            int axisIndex = 0;
            int axisCount = _axisStates.Count;
            int homePollTarget = axisCount > 0 ? _homePollCursor % axisCount : -1;

            foreach (var kv in _axisStates)
            {
                int logicalId = kv.Key;
                if (!_axisCardMap.TryGetValue(logicalId, out var card))
                {
                    axisIndex++;
                    continue;
                }

                int pid = ToPhysicalAxisId(logicalId);
                bool isVisible = !restrictVisible || visibleSet.Contains(logicalId);

                if (!isVisible)
                {
                    axisIndex++;
                    continue;
                }

                var (cardResolved, pidResolved) = (card, pid);
                double newPos = cardResolved.GetPosition(pidResolved);
                bool isMoving = cardResolved.CheckDone(pidResolved) == 0;

                int io = 0;
                cardResolved.GetMotionIO(pidResolved, ref io);

                int motionSts = 0;
                cardResolved.GetMotionSts(pidResolved, ref motionSts);

                int etherCatSts = 0;
                cardResolved.GetEtherCatSts(pidResolved, ref etherCatSts);
                bool isServoOn = etherCatSts == Leisai_Define.AXIS_SM_OPERATION_ENABLED;
                bool isMEL = (io & Leisai_Define.MIO_MEL) != 0;
                bool isORG = (io & Leisai_Define.MIO_ORG) != 0;
                bool isPEL = (io & Leisai_Define.MIO_PEL) != 0;
                bool isALM = (io & Leisai_Define.MIO_ALM) != 0 || (io & Leisai_Define.MIO_EMG) != 0;
                bool isASTP = MotionConvert.BitEnable(motionSts, Leisai_Define.MTS_OTHER);

                PublishAxisAlarmTransition(logicalId, kv.Value, isALM);

                if (!_axisPollSnapshots.TryGetValue(logicalId, out var snap))
                    snap = _axisPollSnapshots[logicalId] = new AxisPollSnapshot();

                bool isHomeOk = snap.IsHomeOk;
                // 运动中或轮询到本轴时才读回零状态，避免每轮全轴 CheckHomeDone 拖至 ~1s
                if (isMoving || axisIndex == homePollTarget)
                    isHomeOk = cardResolved.CheckHomeDone(pidResolved) == 1;

                kv.Value.ActualPosition = newPos;
                kv.Value.IsMoving = isMoving;
                kv.Value.IsAlarmed = isALM;
                kv.Value.IsEnabled = isServoOn;

                if (!snap.IsInitialized || isMoving || snap.HasChanged(newPos, isMoving, isALM, isServoOn, isMEL, isORG, isPEL, isASTP, isHomeOk, io))
                {
                    snap.Update(newPos, isMoving, isALM, isServoOn, isMEL, isORG, isPEL, isASTP, isHomeOk, io);

                    PublishAxisStateChanged(new AxisStateChangedEvent
                    {
                        AxisId = logicalId,
                        Name = kv.Value.Name,
                        Position = newPos,
                        IsMoving = isMoving,
                        IsAlarmed = isALM,
                        IsServoOn = isServoOn,
                        IsMEL = isMEL,
                        IsORG = isORG,
                        IsPEL = isPEL,
                        IsASTP = isASTP,
                        IsHomeOk = isHomeOk,
                        StatusWord = io
                    });
                }

                axisIndex++;
            }

            if (axisCount > 0)
                _homePollCursor = (_homePollCursor + 1) % axisCount;
        }

        /// <summary>DO 慢采样（约 200ms @5ms 周期），避免拖慢轴状态刷新</summary>
        private void PollOutputsSlow()
        {
            foreach (var kv in _outputs)
            {
                if (!_ioCardMap.TryGetValue(kv.Key, out var card)) continue;
                int raw = 0;
                card.GetDo(kv.Value.Port, ref raw);
                kv.Value.Value = (raw != 0);
            }
        }

        private sealed class AxisPollSnapshot
        {
            private const double PositionEpsilon = 0.0005;
            public bool IsInitialized;
            public double Position;
            public bool IsMoving, IsAlarmed, IsServoOn, IsMEL, IsORG, IsPEL, IsASTP, IsHomeOk;
            public int StatusWord;

            public bool HasChanged(double pos, bool moving, bool alm, bool servo, bool mel, bool org, bool pel, bool astp, bool homeOk, int io) =>
                Math.Abs(pos - Position) > PositionEpsilon
                || moving != IsMoving || alm != IsAlarmed || servo != IsServoOn
                || mel != IsMEL || org != IsORG || pel != IsPEL || astp != IsASTP
                || homeOk != IsHomeOk || io != StatusWord;

            public void Update(double pos, bool moving, bool alm, bool servo, bool mel, bool org, bool pel, bool astp, bool homeOk, int io)
            {
                IsInitialized = true;
                Position = pos;
                IsMoving = moving;
                IsAlarmed = alm;
                IsServoOn = servo;
                IsMEL = mel;
                IsORG = org;
                IsPEL = pel;
                IsASTP = astp;
                IsHomeOk = homeOk;
                StatusWord = io;
            }
        }

        /// <summary>读取所有运动卡的 EtherCAT 总线错误码（取首个非零；模拟模式返回 0）</summary>
        private int ReadEtherCatBusErrorCode()
        {
            if (IsSimulationMode)
                return 0;

            foreach (var card in _cards)
            {
                if (card is VirtualMotionCard)
                    continue;

                int err = card.CheckEtherCatStatus();
                if (err != 0)
                    return err;
            }
            return 0;
        }

        /// <summary>轮询总线状态并发布变更事件（供 MainWindow 底部状态栏）</summary>
        private void PublishEtherCatBusStatus(bool force = false)
        {
            int errorCode = ReadEtherCatBusErrorCode();
            bool isSimulation = IsSimulationMode;
            if (!force && errorCode == _lastBusErrorCode && isSimulation == _lastPublishedIsSimulation)
                return;

            _lastBusErrorCode = errorCode;
            _lastPublishedIsSimulation = isSimulation;
            _ea.GetEvent<EtherCatBusStatusChangedEvent>().Publish(new EtherCatBusStatusPayload
            {
                ErrorCode = errorCode,
                IsSimulation = isSimulation
            });
        }

        public void Dispose() => Shutdown();

        /// <summary> 获取所有轴配置（来自 hwcfg.xml） </summary>
        public IReadOnlyList<AxisConfig> GetAxisConfigurations() => _config?.Axes ?? new List<AxisConfig>();

        /// <summary> 获取所有任务配置（来自 hwcfg.xml） </summary>
        public IReadOnlyList<TaskConfig> GetTaskConfigurations() => _config?.Tasks ?? new List<TaskConfig>();

        /// <summary> 获取所有数字输入（DI）配置列表 </summary>
        /// <returns>只读的 DI 配置集合</returns>
        public IReadOnlyList<IoConfig> GetInputConfigurations()
        {
            return _config?.Inputs?.AsReadOnly() ?? new List<IoConfig>().AsReadOnly();
        }

        /// <summary> 获取所有数字输出（DO）配置列表 </summary>
        /// <returns>只读的 DO 配置集合</returns>
        public IReadOnlyList<IoConfig> GetOutputConfigurations()
        {
            return _config?.Outputs?.AsReadOnly() ?? new List<IoConfig>().AsReadOnly();
        }

        /// <summary> 获取三色灯/蜂鸣器配置列表（来自hwcfg.xml TowerLights节） </summary>
        public IReadOnlyList<LightConfig> GetLightConfigurations()
        {
            return _config?.Lights?.AsReadOnly() ?? new List<LightConfig>().AsReadOnly();
        }

        #region 模拟量通道读取

        /// <summary>
        /// 读取单个模拟量通道并转换为物理量
        /// 通过雷赛运动控制卡 LTDMC.dmc_get_ad_input 直接读取AD值
        /// </summary>
        /// <param name="cardNo">卡号</param>
        /// <param name="channel">通道号</param>
        /// <returns>转换后的物理量值(N)</returns>
        public async Task<double> ReadAnalogChannelAsync(int cardNo, int channel)
        {
            return await Task.Run(() =>
            {
                double rawValue = 0;
                LTDMC.dmc_get_ad_input((ushort)cardNo, (ushort)channel, ref rawValue);
                double force = _adConverter.Convert(channel, rawValue);
                return force;
            });
        }

        /// <summary>
        /// 批量读取模拟量通道并转换为物理量
        /// channelMap 的 Value 为编码通道号：cardNo = Value/4+1, channel = Value%4
        /// </summary>
        /// <param name="channelMap">键为逻辑通道标识，值为编码通道号</param>
        /// <returns>逻辑通道标识 → 物理量值的字典</returns>
        public async Task<Dictionary<int, double>> ReadAnalogChannelsAsync(Dictionary<int, int> channelMap)
        {
            return await Task.Run(() =>
            {
                var rawValues = new Dictionary<int, double>();
                Parallel.ForEach(channelMap, kvp =>
                {
                    double rawValue = 0;
                    LTDMC.dmc_get_ad_input((ushort)(kvp.Value / 4 + 1), (ushort)(kvp.Value % 4), ref rawValue);
                    lock (rawValues) { rawValues[kvp.Key] = rawValue; }
                });
                var converted = _adConverter.ConvertBatch(rawValues);
                return converted;
            });
        }

        /// <summary>
        /// 检查指定模拟量通道是否已配置且可用
        /// dmc_get_ad_input: channel 取值范围 0~7，返回错误代码
        /// </summary>
        public bool IsAnalogChannelAvailable(int cardNo, int channel)
        {
            try
            {
                if (channel < 0 || channel > 7)
                    return false;
                double rawValue = 0;
                short result = LTDMC.dmc_get_ad_input((ushort)cardNo, (ushort)channel, ref rawValue);
                return result == 0;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region 连续插补

        /// <summary> 初始化连续插补：设置速度曲线、前瞻模式、打开插补列表 </summary>
        public void InitializeContinuousInterpolation(int coordId, int[] axisIds, double startVel = 5, double maxVel = 50, double acc = 0.1, double dec = 0.1, double endVel = 0,double sPara = 0.05)
        {
            var card = GetCardForCoord(coordId);
            card.SetVectorProfileUnit(coordId, startVel, maxVel, acc, dec, endVel);
            card.ContiSetLookaheadMode(coordId, 1, 200, 0, 0);
            card.SetVectorSProfile(coordId, 0, sPara);
            card.SetArcLimit(coordId, 0, 0, 0);
            card.ContiOpenList(coordId, axisIds.Length, axisIds);
        }

        /// <summary> 添加直线插补段到连续插补列表 </summary>
        public void AddLineSegment(int coordId, double[] targetPos, ushort posiMode = 1, int mark = 0)
        {
            var card = GetCardForCoord(coordId);
            card.ContiLineUnit(coordId, targetPos.Length, new int[targetPos.Length], targetPos, posiMode, mark);
        }

        /// <summary> 执行连续插补（启动并关闭列表） </summary>
        public void ExecuteContinuousInterpolation(int coordId)
        {
            var card = GetCardForCoord(coordId);
            card.ContiStartList(coordId);
            card.ContiCloseList(coordId);
        }

        /// <summary> 暂停连续插补 </summary>
        public void PauseContinuousInterpolation(int coordId)
        {
            var card = GetCardForCoord(coordId);
            card.ContiPauseList(coordId);
        }

        /// <summary>
        /// 点胶多段插补 / 连续轨迹运动 专用等待完成
        /// 工业级实时性，低CPU，无抖动，急停秒响应
        /// </summary>
        public async Task<bool> WaitForCoordMotionCompletionAsync(int coordId, TimeSpan timeout, CancellationToken token = default)
        {
            var card = GetCardForCoord(coordId);
            var sw = Stopwatch.StartNew();

            // 异步自旋等待（运动控制最高标准）
            return await Task.Run(() =>
            {
                var spinWait = new SpinWait();

                while (sw.Elapsed < timeout)
                {
                    // 急停：微秒级响应，点胶机必备
                    token.ThrowIfCancellationRequested();

                    // 运动完成检测
                    if (card.CheckCoordMotionDone(coordId) == 1)
                        return true;

                    // 智能低CPU自旋，不会占满核心
                    spinWait.SpinOnce();
                }

                // 超时
                return false;
            }, token);
        }
        /// <summary> 根据 coordId 获取对应的运动卡 </summary>
        private IMotionCard GetCardForCoord(int coordId)
        {
            // 坐标系0默认使用第一张卡
            if (_axisCardMap.Count > 0)
            {
                var firstAxis = _axisCardMap.Keys.First();
                return _axisCardMap[firstAxis];
            }
            throw new InvalidOperationException($"无法找到坐标系 {coordId} 对应的运动卡");
        }

        #endregion
    }

    /// <summary>
    /// Windows 多媒体定时器 API 封装——用于临时提升系统定时器精度
    /// 默认 Windows 定时器精度约 15.6ms，调用 timeBeginPeriod(1) 可提升到 1ms
    /// 这对运动控制等待循环的响应性至关重要
    /// </summary>
    internal static class WinmmTimeApi
    {
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", SetLastError = true)]
        internal static extern uint timeBeginPeriod(uint period);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", SetLastError = true)]
        internal static extern uint timeEndPeriod(uint period);
    }
}