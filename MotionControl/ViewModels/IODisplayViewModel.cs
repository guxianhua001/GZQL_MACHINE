using MotionControl.Events;
using MotionControl.Interfaces;
using MotionControl.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;

namespace MotionControl.ViewModels
{
    /// <summary>
    /// IO 显示面板 ViewModel
    /// 负责实时刷新 DI/DO 通道状态，支持按需刷新（页面可见时才启动定时器）
    /// 使用 Interlocked 原子操作防止高频定时器下的重入问题，保障工业控制的快速响应性与安全性
    /// 订阅 StationStateChangedEvent 实现与状态面板同步的报警指示灯
    /// </summary>
    public class IODisplayViewModel : BindableBase, IDisposable
    {
        private readonly IMotionService _motionService;
        private readonly IEventAggregator _ea;
        private SubscriptionToken _stateToken;

        // ========== 防重入令牌（Interlocked 原子整数） ==========
        // 0 = 空闲（可进入刷新），1 = 正在刷新中
        private int _isRefreshing = 0;

        // ========== 定时器 ==========
        private DispatcherTimer _refreshTimer;

        // ========== 属性 ==========

        private bool _isVisible;
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (SetProperty(ref _isVisible, value))
                {
                    OnVisibilityChanged(value);
                }
            }
        }

        /// <summary>报警红灯是否点亮（与SystemStateService闪烁周期同步，800ms）</summary>
        private bool _alarmLightOn;
        public bool AlarmLightOn
        {
            get => _alarmLightOn;
            set => SetProperty(ref _alarmLightOn, value);
        }

        /// <summary>数字输入通道列表</summary>
        public ObservableCollection<DiChannelItem> DIList { get; } = new();

        /// <summary>数字输出通道列表</summary>
        public ObservableCollection<DoChannelItem> DOList { get; } = new();

        /// <summary>DO 切换命令</summary>
        public DelegateCommand<DoChannelItem> ToggleDoCommand { get; }

        // ========== 构造函数 ==========

        public IODisplayViewModel(IMotionService motionService, IEventAggregator ea)
        {
            _motionService = motionService ?? throw new ArgumentNullException(nameof(motionService));
            _ea = ea ?? throw new ArgumentNullException(nameof(ea));

            ToggleDoCommand = new DelegateCommand<DoChannelItem>(OnToggleDo);

            _stateToken = _ea.GetEvent<StationStateChangedEvent>().Subscribe(OnStationStateChanged);

            SetupRefreshTimer();
            InitializeChannels();
        }

        // ========== 初始化通道配置 ==========

        /// <summary>
        /// 从运动服务加载 DI/DO 硬件配置，构建视图通道列表
        /// 配置来源：hwcfg.xml → IoConfig（通过 GetInputConfigurations / GetOutputConfigurations）
        /// 三色灯/蜂鸣器DO通道关联LightType，用于显示对应颜色
        /// </summary>
        private void InitializeChannels()
        {
            DIList.Clear();
            DOList.Clear();

            // 构建灯光类型查找表：LogicalId → LightType
            var lightTypeMap = new Dictionary<int, string>();
            try
            {
                var lightConfigs = _motionService.GetLightConfigurations();
                if (lightConfigs != null)
                {
                    foreach (var light in lightConfigs)
                    {
                        if (light.LogicalId.HasValue)
                            lightTypeMap[light.LogicalId.Value] = light.LightType;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IODisplay] ⚠️ 加载灯光配置失败: {ex.Message}");
            }

            try
            {
                var diConfigs = _motionService.GetInputConfigurations();
                if (diConfigs != null)
                {
                    foreach (var cfg in diConfigs)
                    {
                        DIList.Add(new DiChannelItem(cfg.LogicalId, cfg.Port, cfg.Name));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IODisplay] ⚠️ 加载 DI 配置失败: {ex.Message}");
            }

            try
            {
                var doConfigs = _motionService.GetOutputConfigurations();
                if (doConfigs != null)
                {
                    foreach (var cfg in doConfigs)
                    {
                        lightTypeMap.TryGetValue(cfg.LogicalId, out string lightType);
                        DOList.Add(new DoChannelItem(cfg.LogicalId, cfg.Port, cfg.Name, lightType));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IODisplay] ⚠️ 加载 DO 配置失败: {ex.Message}");
            }
        }

        // ========== 定时器生命周期管理 ==========

        /// <summary>
        /// 创建刷新定时器（100ms 间隔），绑定 Tick 回调但不启动
        /// 100ms 间隔兼顾 UI 流畅度与 CPU 占用，符合工业 HMI 实时监控需求
        /// </summary>
        private void SetupRefreshTimer()
        {
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _refreshTimer.Tick += OnTimerTick;
        }

        /// <summary>
        /// 启动定时刷新：重置防重入标志并启动 DispatcherTimer
        /// 在页面变为可见时调用
        /// </summary>
        private void StartRefreshing()
        {
            Interlocked.Exchange(ref _isRefreshing, 0);
            _refreshTimer?.Start();
        }

        /// <summary>
        /// 停止定时刷新并重置防重入标志
        /// 在页面变为不可见或 Dispose 时调用
        /// </summary>
        private void StopRefreshing()
        {
            _refreshTimer?.Stop();
            Interlocked.Exchange(ref _isRefreshing, 0);
        }

        // ========== 可见性驱动的刷新控制 ==========

        /// <summary>
        /// 页面可见性变更回调
        /// true  → 启动定时器 + 立即强制刷新一次（避免用户看到旧数据）
        /// false → 停止定时器（节省资源）
        /// </summary>
        private void OnVisibilityChanged(bool visible)
        {
            if (visible)
            {
                StartRefreshing();
                ForceRefreshOnce();
            }
            else
            {
                StopRefreshing();
            }
        }

        // ========== Interlocked 原子防重入 - 定时器 Tick ==========

        /// <summary>
        /// DispatcherTimer Tick 回调入口
        /// 使用 Interlocked.CompareExchange 实现"非阻塞互斥锁"语义：
        ///   - 尝试将 _isRefreshing 从 0→1（获取令牌）
        ///   - 若当前值为 1（上一次刷新未完成），直接跳过本次 Tick
        ///   - finally 中无条件归还令牌（0），保证不会死锁
        ///
        /// 为什么不用 lock / bool：
        ///   - lock 会阻塞 UI 线程（DispatcherTimer 运行在 UI 线程）
        ///   - bool 在多线程读写下不安全（JIT 可能缓存到寄存器）
        ///   - Interlocked 是 CPU 级原子指令，无锁无等待，最适合高频轮询场景
        /// </summary>
        private void OnTimerTick(object sender, EventArgs e)
        {
            // 🔒 原子操作：尝试获得"刷新令牌"
            int originalValue = Interlocked.CompareExchange(
                ref _isRefreshing,
                value: 1,
                comparand: 0
            );

            if (originalValue == 1)
                return; // ✅ 上一次刷新仍在进行中，安全跳过本次 Tick

            try
            {
                ExecuteRefreshLogic();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IODisplay] 💥 刷新异常: {ex.Message}");
            }
            finally
            {
                // 🔓 无条件归还令牌，确保后续 Tick 可以正常进入
                Interlocked.Exchange(ref _isRefreshing, 0);
            }
        }

        // ========== 强制单次刷新（受原子保护） ==========

        /// <summary>
        /// 立即执行一次刷新（不受定时器调度）
        /// 用于页面首次可见、手动触发等需要即时更新的场景
        /// 同样使用 Interlocked 保护，避免与定时器 Tick 并发冲突
        /// </summary>
        private void ForceRefreshOnce()
        {
            int original = Interlocked.CompareExchange(ref _isRefreshing, 1, 0);
            if (original != 0)
                return; // 已在刷新中，跳过

            try
            {
                ExecuteRefreshLogic();
            }
            finally
            {
                Interlocked.Exchange(ref _isRefreshing, 0);
            }
        }

        // ========== 核心刷新逻辑 ==========

        /// <summary>
        /// 执行实际的 IO 状态读取与 UI 更新
        /// 设计原则：
        ///   - 每个通道独立 try-catch，单点故障不影响其他点
        ///   - DI 从硬件实时读取，DO 仅做显示更新（避免误写）
        ///   - 全部操作在 UI 线程完成（DispatcherTimer 保证）
        /// </summary>
        private void ExecuteRefreshLogic()
        {
            RefreshDIChannels();
            RefreshDOChannels();
        }

        /// <summary>
        /// 遍历 DIList，逐通道调用 ReadDi 读取硬件状态并更新 IsActive
        /// 单点异常仅记录日志，不影响其余通道的刷新
        /// </summary>
        private void RefreshDIChannels()
        {
            for (int i = 0; i < DIList.Count; i++)
            {
                try
                {
                    var item = DIList[i];
                    bool state = _motionService.ReadDi(item.LogicalId);
                    item.IsActive = state;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[IODisplay] DI[{i}] 读取失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 遍历 DOList，从硬件缓存读取当前输出状态
        /// 注意：此处仅读取状态用于 UI 显示，不做写入操作
        /// DO 的实际切换由 ToggleDoCommand 触发
        /// </summary>
        private void RefreshDOChannels()
        {
            for (int i = 0; i < DOList.Count; i++)
            {
                try
                {
                    var item = DOList[i];
                    bool state = _motionService.ReadDo(item.LogicalId);
                    item.IsActive = state;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[IODisplay] DO[{i}] 读取失败: {ex.Message}");
                }
            }
        }

        // ========== DO 切换操作 ==========

        /// <summary>
        /// DO 输出切换命令处理
        /// 执行流程：
        ///   1. 调用 WriteDo 写入硬件
        ///   2. 乐观更新 UI（立即反映新状态，提升操作手感）
        ///   3. 异常时回滚 UI 状态并记录日志
        /// </summary>
        private void OnToggleDo(DoChannelItem item)
        {
            if (item == null) return;

            bool newValue = !item.IsActive;

            try
            {
                _motionService.WriteDo(item.LogicalId, newValue);
                item.IsActive = newValue;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IODisplay] DO 切换失败 (LogicalId={item.LogicalId}): {ex.Message}");
                item.IsActive = !newValue;
            }
        }

        // ========== 资源释放 ==========

        /// <summary>
        /// 状态变更回调：同步报警红灯与SystemStateService闪烁周期（800ms）
        /// RedLight已包含闪烁相位，直接驱动UI即可
        /// </summary>
        private void OnStationStateChanged(StationStatePayload payload)
        {
            AlarmLightOn = payload.RedLight;
        }

        /// <summary>
        /// 释放定时器和事件订阅资源，防止内存泄漏
        /// 必须在 View 卸载时调用（建议通过 Prism 的 Region 导航生命周期自动触发）
        /// </summary>
        public void Dispose()
        {
            StopRefreshing();
            _stateToken?.Dispose();
            if (_refreshTimer != null)
            {
                _refreshTimer.Tick -= OnTimerTick;
                _refreshTimer = null;
            }
        }
    }
}
