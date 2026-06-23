using Core.Events;
using Core.Utilities;
using MotionControl.Events;
using MotionControl.Interfaces;
using Prism.Events;
using System;
using System.Threading.Tasks;

namespace StationTasks.Services
{
    /// <summary>
    /// 整机初始化服务实现：触发各工站 HomeAsync() 并行回零。
    /// 架构层次：StationTasks → MotionControl → Core（拒绝倒置依赖）。
    /// 初始化动作通过各工站的 .Init.cs partial class 实现，工站间协调使用信号交互。
    /// 本服务仅负责：触发回零、防重入、发布系统状态事件。
    /// 安全机制：初始化前校验控制卡连接状态，避免未连接硬件时执行初始化。
    /// </summary>
    public class MachineInitializationService : IMachineInitializationService
    {
        private readonly ITaskManager _taskManager;
        private readonly IEventAggregator _ea;
        private readonly ILoggerService _logger;
        private readonly IMotionService _motion;

        /// <summary> 初始化锁，防止重复触发 </summary>
        private readonly object _initLock = new object();
        private bool _isInitializing;

        /// <summary> 是否正在执行初始化 </summary>
        public bool IsInitializing
        {
            get
            {
                lock (_initLock) return _isInitializing;
            }
        }

        public MachineInitializationService(
            ITaskManager taskManager,
            IEventAggregator ea,
            ILoggerService logger,
            IMotionService motion)
        {
            _taskManager = taskManager;
            _ea = ea;
            _logger = logger;
            _motion = motion;

            // 订阅复位按钮长按触发的初始化请求事件
            _ea.GetEvent<MachineInitializationRequestedEvent>().Subscribe(OnInitializationRequested, ThreadOption.BackgroundThread, false);
        }

        /// <summary>
        /// 复位按钮长按5秒后触发的事件回调
        /// </summary>
        private void OnInitializationRequested()
        {
            _ = InitializeMachineAsync();
        }

        /// <summary>
        /// 执行整机初始化序列。
        /// 各工站 HomeAsync() 内部通过 SignalToStation / WaitForSignalAsync 协调时序：
        /// 1. 所有Z轴先归零并回到待机位（Z, Dz₁, Dz₂, Dz₃）
        /// 2. 并行：上下料Y/Rz/Rx回零、点胶Dx/Dy回零+待机、组装Cy/Ey回零+待机
        /// 3. 等待点胶工站回零完成
        /// 4. 组装X/Ry回零+待机
        /// 5. 设置站状态为等待运行（WAITRUN）
        /// 安全校验：初始化前检查控制卡连接状态，未连接硬件时直接返回 false，避免无效初始化。
        /// </summary>
        /// <returns>true=初始化成功，false=初始化失败、被取消或硬件未连接</returns>
        public async Task<bool> InitializeMachineAsync()
        {
            // 防止重复触发初始化
            lock (_initLock)
            {
                if (_isInitializing)
                {
                    _logger.Warn("[MachineInit] 初始化已在进行中，忽略重复请求。");
                    return false;
                }
                _isInitializing = true;
            }

            // 初始化前安全校验：控制卡必须已连接且 EtherCAT 总线正常
            if (!IsControlCardConnected())
            {
                _logger.Warn("[MachineInit] 控制卡未连接或总线异常，放弃整机初始化。");
                lock (_initLock) { _isInitializing = false; }
                _ea.GetEvent<SystemResetResultEvent>().Publish(false);
                return false;
            }

            _logger.Info("[MachineInit] ===== 整机初始化开始 =====");

            try
            {
                // 调用 TaskManager.HomeAllAsync()：并行触发所有工站 HomeAsync()
                // 工站间时序由各 .Init.cs 内的信号交互保证
                await _taskManager.HomeAllAsync();

                // 初始化成功：发布复位结果事件，驱动状态机 RESETING → WAITRUN
                _ea.GetEvent<SystemResetResultEvent>().Publish(true);
                _logger.Info("[MachineInit] ===== 整机初始化完成 =====");
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Warn("[MachineInit] 整机初始化被取消（用户停止或急停）。");
                _ea.GetEvent<SystemResetResultEvent>().Publish(false);
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error($"[MachineInit] 整机初始化失败: {ex.Message}");
                _ea.GetEvent<SystemResetResultEvent>().Publish(false);
                return false;
            }
            finally
            {
                lock (_initLock)
                {
                    _isInitializing = false;
                }
            }
        }

        /// <summary>
        /// 检查控制卡是否已连接且通信正常。
        /// 非模拟模式且 EtherCAT 总线无错误时返回 true。
        /// 作为初始化前的安全兜底，防止未连接硬件时执行回零等动作导致异常。
        /// </summary>
        /// <returns>true=控制卡已连接且总线正常；false=模拟模式或总线异常</returns>
        private bool IsControlCardConnected()
        {
            // 模拟模式下无真实硬件卡，视为未连接
            if (_motion.IsSimulationMode)
            {
                _logger.Warn("[MachineInit] 控制卡未连接：当前为模拟模式，无真实硬件。");
                return false;
            }

            // EtherCAT 总线错误码非 0 表示通信异常
            int busError = _motion.GetEtherCatBusErrorCode();
            if (busError != 0)
            {
                _logger.Warn($"[MachineInit] 控制卡连接异常：EtherCAT 总线错误码 0x{busError:X}。");
                return false;
            }

            return true;
        }
    }
}
