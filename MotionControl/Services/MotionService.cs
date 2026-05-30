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
        private MotionSystemConfig _config;

        private List<IMotionCard> _cards = new();
        private Dictionary<int, AxisState> _axisStates = new();
        private Dictionary<int, IoState> _inputs = new();
        private Dictionary<int, IoState> _outputs = new();
        private Dictionary<int, IMotionCard> _axisCardMap = new();
        private Dictionary<int, IMotionCard> _ioCardMap = new();

        /// <summary> 是否运行在模拟环境（所有卡均为 VirtualMotionCard） </summary>
        public bool IsSimulationMode => _cards.Count > 0 && _cards.All(c => c is VirtualMotionCard);
        // 高精度轮询线程 (轮询间隔10ms)
        private Thread _pollThread;
        private CancellationTokenSource _pollCts;
        private readonly ManualResetEventSlim _stopEvent = new ManualResetEventSlim(false);
        private bool _isPolling;
        private double _pollingInProgress;
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
            return new UnsubscribeAction(this, observer);
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
                             IADValueConverter adConverter)
        {
            _cardFactory = cardFactory;
            _configLoader = configLoader;
            _ea = ea;
            _logger = logger;
            _alarmService = alarmService;
            _adConverter = adConverter;
        }

        // ---------- 初始化 ----------
        public async Task InitializeAsync()
        {
            _config = _configLoader.Load();
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

                // 步骤2：总线正常后下载配置文件
                await Task.Run(() =>
                {
                    card.LoadConfig(cardCfg.ConfigPath);
                });
                _cards.Add(card);
            }
            BuildMappings(_config);
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
        private IMotionCard GetCardForAxis(int axisId)
        {
            if (_axisCardMap.TryGetValue(axisId, out var card))
                return card;
            throw new InvalidOperationException($"Axis {axisId} not mapped to any card");
        }

        public void EnableAxis(int axisId) => GetCardForAxis(axisId).SetServo(axisId, true);
        public void DisableAxis(int axisId) => GetCardForAxis(axisId).SetServo(axisId, false);

        public async Task MoveAbsAsync(int axisId, double position, double velocity, CancellationToken token = default)
        {
            var card = GetCardForAxis(axisId);
            await Task.Run(() =>
            {
                card.MoveAbs(axisId, position, velocity);
                WaitForDone(card, axisId, position, token); // 传入目标位置和令牌
            }, token);
        }

        public async Task MoveRelAsync(int axisId, double distance, double velocity, CancellationToken token = default)
        {
            var card = GetCardForAxis(axisId);
            await Task.Run(() =>
            {
                // 相对运动必须先读取当前位置，计算出绝对目标位置，才能做最终校验
                double startPos = card.GetPosition(axisId);
                double targetPos = startPos + distance;

                card.MoveRel(axisId, distance, velocity);
                WaitForDone(card, axisId, targetPos, token); // 传入计算出的绝对目标位置
            }, token);
        }

        public async Task MoveLineAbsAsync(int coordId, int[] axisIds, double[] positions, double velocity, CancellationToken token = default)
        {
            var card = GetCardForAxis(axisIds[0]);
            await Task.Run(() =>
            {
                card.MoveLineAbs(coordId, axisIds, positions, velocity);
                foreach (var id in axisIds) WaitForCoordDone(card, id, axisIds, positions, token);
            });
        }

        public async Task HomeAsync(int axisId, int mode = 1, double minVel = 5, double maxVel = 20, CancellationToken token = default)
        {
            var card = GetCardForAxis(axisId);
            await Task.Run(() =>
            {
                card.SetHomeMode(axisId, mode, minVel, maxVel);
                card.GoHome(axisId);
                var spinWait = new SpinWait();
                // 1：等待回零流程结束（搜索原点、找Z相）
                while (true)
                {
                    token.ThrowIfCancellationRequested(); // 支持急停/停止打断
                    int homeStatus = card.CheckHomeDone(axisId);
                    // 1 表示回零成功完成，-1 表示回零失败/超时，0 表示正在进行中
                    if (homeStatus == 1)
                        break;
                    if (homeStatus < 0) 
                    {
                        throw new RecoverableException(
                            message: $"轴 {axisId} 回原点失败，错误码: {homeStatus}",
                            suggestedAction: "请检查原点传感器是否正常、回零方向是否正确、未撞限位，复位后重试。"
                        );
                    }
                    spinWait.SpinOnce(); // 自旋等待，避免 CPU 空转
                }
                // 2：等待运动彻底停止（回零流程结束后，轴可能还在微调运动）
                while (true)
                {
                    token.ThrowIfCancellationRequested(); // 支持急停/停止打断
                    if (card.CheckDone(axisId) == 1)
                        break;
                    spinWait.SpinOnce();
                }
                // 3：回零完成后的位置校验
            }, token);
        }

        public void JogStart(int axisId, bool positive) => GetCardForAxis(axisId).MoveJog(axisId, positive ? 0 : 1);
        public void JogStop(int axisId) => GetCardForAxis(axisId).Stop(axisId);
        public void StopAxis(int axisId) => GetCardForAxis(axisId).Stop(axisId);
        public void EmergencyStop(int axisId) => GetCardForAxis(axisId).EStop(axisId);
        public Interfaces.IAxis GetAxisState(int axisId) => _axisStates.TryGetValue(axisId, out var s) ? s : null;
        public void ClearAlarm(int axisId) => GetCardForAxis(axisId).ClearAlarm(axisId);

        public async Task<int> CheckHomeDoneAsync(int axisId)
        {
            return await Task.Run(() =>
            {
                var card = GetCardForAxis(axisId);
                return card.CheckHomeDone(axisId);
            });
        }
        
        /// <summary> 清除轴位置（归零） </summary>
        public void ClearPosition(int axisId)
        {
            if (_axisStates.TryGetValue(axisId, out var state))
                state.ActualPosition = 0;
        }

        /// <summary>
        /// 获取轴当前位置（直接读卡，实时性高，用于位置触发等场景）
        /// </summary>
        public double GetAxisPosition(int axisId)
        {
            var card = GetCardForAxis(axisId);
            return card.GetPosition(axisId);
        }

        /// <summary>
        /// 等待轴运动完成，并校验最终位置是否在误差允许范围内
        /// </summary>
        /// <param name="card">运动卡实例</param>
        /// <param name="axisId">逻辑轴号</param>
        /// <param name="targetPosition">目标位置</param>
        /// <param name="token">取消令牌（用于急停快速响应）</param>
        /// <param name="tolerance">位置误差容忍度（根据你的机械精度设定，如0.01mm）</param>
        private void WaitForDone(IMotionCard card, int axisId, double targetPosition, CancellationToken token, double tolerance = 0.05)
        {
            var spinWait = new SpinWait();
            while (true)
            {
                // 检查急停/停止取消信号，实现微秒级响应
                token.ThrowIfCancellationRequested();
                // 检查运动是否完成
                int done = card.CheckDone(axisId);
                if (done == 1)
                    break;
                spinWait.SpinOnce();  // 超高速等待，不占满CPU
            }
            double actualPosition = card.GetPosition(axisId);
            if (Math.Abs(actualPosition - targetPosition) > tolerance)
            {
                // 如果位置不对，说明发生了碰撞、限位或伺服异常，主动抛出可恢复异常
                throw new RecoverableException(
                    message: $"轴 {axisId} 运动未到位。目标: {targetPosition:F3}, 实际: {actualPosition:F3}",
                    suggestedAction: "请检查轴是否撞击限位、伺服是否报警或存在机械卡死，复位后重试。"
                );
            }
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
            io.Value = (raw != 0);          // raw非零=ON，零=OFF
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
        public void StartPolling(int intervalMs = 10) // 默认 10 ms 快周期
        {
            if (_isPolling) return;
            _pollCts = new CancellationTokenSource();
            _pollThread = new Thread(() => PollLoop(intervalMs, _pollCts.Token))
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest // 提升优先级
            };
            _isPolling = true;
            _pollThread.Start();
        }

        public void StopPolling()
        {
            if (!_isPolling) return;
            _pollCts.Cancel();
            if (!_pollThread.Join(2000))
                _pollThread.Interrupt();
            _isPolling = false;
        }

        private void PollLoop(int intervalMs, CancellationToken token)
        {
            var stopwatch = new System.Diagnostics.Stopwatch();

            while (!token.IsCancellationRequested)
            {
                stopwatch.Restart();

                // 仅执行快速报警检查（不浪费时间在位置读取）
                CheckCriticalAlarms();

                // 每 N 次快速循环执行一次完整轮询（如每 3 次 = 30 ms）
                if ((_fastCycleCount++ % 3) == 0)
                {
                    PollFullStatus();
                }

                // 精确等待到下一个周期起点
                long elapsed = stopwatch.ElapsedMilliseconds;
                long waitMs = intervalMs - elapsed;
                if (waitMs > 0)
                {
                    // 自旋等待 + 短时间 sleep 提高精度
                    if (waitMs >= 2)
                        Thread.Sleep((int)(waitMs - 1));
                    while (stopwatch.ElapsedMilliseconds < intervalMs) ; // 自旋
                }
            }
        }
        private int _fastCycleCount = 0;

        /// <summary> 仅检查所有轴的报警和急停信号，不读取位置；检测到报警时触发AlarmModule报警记录 </summary>
        private void CheckCriticalAlarms()
        {
            foreach (var kv in _axisStates)
            {
                int axisId = kv.Key;
                if (!_axisCardMap.TryGetValue(axisId, out var card)) continue;

                int io = 0;
                card.GetMotionIO(axisId, ref io);
                bool alarm = (io & Leisai_Define.MIO_ALM) != 0 ||
                             (io & Leisai_Define.MIO_EMG) != 0;

                if (alarm != kv.Value.IsAlarmed)
                {
                    kv.Value.IsAlarmed = alarm;
                    _ea.GetEvent<AxisAlarmEvent>().Publish(new AxisAlarmPayload
                    {
                        AxisId = axisId,
                        IsAlarm = alarm
                    });

                    // 检测到新报警时，触发报警记录到AlarmModule
                    if (alarm)
                    {
                        _ = _alarmService.TriggerAlarmAsync(
                            "AXIS_ALARM",
                            AlarmLevel.Serious,
                            $"轴{axisId}报警信号触发",
                            source: $"Axis{axisId}",
                            type: AlarmType.HardwareFault);
                    }
                }
            }

            // 关键 I/O 也可在此快速检查（如安全门、光栅）
            foreach (var kv in _inputs)
            {
                if (!_ioCardMap.TryGetValue(kv.Key, out var card)) continue;
                int val = 0;
                card.GetDi(kv.Value.Port, ref val);
                kv.Value.Value = (val != 0);
            }
        }
        private void PollFullStatus()
        {
            if (Interlocked.CompareExchange(ref _pollingInProgress, 1, 0) != 0)
                return;

            try
            {
                foreach (var kv in _axisStates)
                {
                    int axisId = kv.Key;
                    var card = GetCardForAxis(axisId);
                    
                    // 读取位置和运动状态
                    double newPos = card.GetPosition(axisId);
                    bool isMoving = card.CheckDone(axisId) == 0;
                    
                    // 读取 IO 状态字（包含伺服、极限、报警等信息）
                    int io = 0;
                    card.GetMotionIO(axisId, ref io);

                    // 解析状态位（基于雷赛卡定义）
                    bool isServoOn = (io & Leisai_Define.MIO_SVON) != 0;
                    bool isMEL = (io & Leisai_Define.MIO_MEL) != 0;      // 负极限
                    bool isORG = (io & Leisai_Define.MIO_ORG) != 0;      // 原点
                    bool isPEL = (io & Leisai_Define.MIO_PEL) != 0;      // 正极限
                    bool isALM = (io & Leisai_Define.MIO_ALM) != 0 || (io & Leisai_Define.MIO_EMG) != 0;  // 报警/急停
                    bool isASTP = (io & Leisai_Define.MIO_ASTP) != 0;    // 急停状态

                    // 更新内部状态
                    kv.Value.ActualPosition = newPos;
                    kv.Value.IsMoving = isMoving;
                    kv.Value.IsAlarmed = isALM;
                    kv.Value.IsEnabled = isServoOn;

                    // 发布轴状态变更事件（事件驱动，替代定时器轮询）
                    PublishAxisStateChanged(new AxisStateChangedEvent
                    {
                        AxisId = axisId,
                        Name = kv.Value.Name,
                        Position = newPos,
                        IsMoving = isMoving,
                        IsAlarmed = isALM,
                        IsServoOn = isServoOn,
                        IsMEL = isMEL,
                        IsORG = isORG,
                        IsPEL = isPEL,
                        IsASTP = isASTP,
                        IsHomeOk = isORG,  // 简化：以 ORG 作为回零完成标志
                        StatusWord = io
                    });
                }
                // 输出回读也可以在这里慢速进行
                foreach (var kv in _outputs)
                {
                    if (!_ioCardMap.TryGetValue(kv.Key, out var card)) continue;
                    int raw = 0;
                    card.GetDo(kv.Value.Port, ref raw);
                    kv.Value.Value = (raw != 0);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _pollingInProgress, 0);
            }
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