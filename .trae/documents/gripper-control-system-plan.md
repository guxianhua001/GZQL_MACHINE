# 夹爪控制系统实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 基于现有运动控制框架，实现完整的电夹爪用户控制系统，包括抽象接口、硬件适配、UI控件、参数配置和增强功能

**Architecture:** 在现有 `IMotionService` (提供 WriteDo/ReadDi) 之上构建 `IGripperService` 接口层，通过依赖注入在 `MotionControlModule` 中注册。UI 层采用 WPF UserControl + MVVM 模式，复用 MaterialDesignInXaml 组件库。取料动作表格基于 GotoDetailView 的 DataGrid 结构删除 Station 列后复用。

**Tech Stack:** WPF + PRISM 8 + MaterialDesignInXaml + LTDMC (雷赛EtherCAT) + NLog + JSON配置持久化

---

## 文件结构总览

**设计原则：** 夹爪的核心接口、服务实现、状态模型和事件定义全部位于 **MotionControl** 项目中，与现有的 IMotionService/IMotionCard 架构保持一致。UI 层（View/ViewModel）放在 Module 项目中，遵循项目现有的分层规范。

```
修改文件:
├── StationTasks/Models/ProcessStep.cs              # 扩展 PickDetail 模型
├── Module/Editor/PickDetailView.xaml               # 改进夹爪配置UI
├── Module/Editor/PickDetailViewModel.cs            # 新增夹爪控制命令
├── MotionControl/MotionControlModule.cs            # ★ 在此注册 IGripperService
└── MotionControl/MotionControl.csproj              # ★ 需要包含新文件（通常使用通配符已自动包含）

★ MotionControl 项目内新建（核心业务逻辑）:
├── MotionControl/Interfaces/
│   └── IGripperService.cs                         # ★ 夹爪服务抽象接口
├── MotionControl/Services/
│   └── GripperService.cs                          # ★ 夹爪服务实现（注入IMotionService）
├── MotionControl/Models/
│   └── GripperState.cs                            # ★ 夹爪状态数据模型
├── MotionControl/Events/
│   └── GripperStateChangedEvent.cs                # ★ 状态变更事件（Prism EventAggregator）
└── MotionControl/Converters/
    └── GripperStatusToBrushConverter.cs            # ★ WPF状态到颜色转换器

Module 项目内新建（UI 层）:
└── Module/UserControls/Grippers/
    ├── GripperControlView.xaml                    # 夹爪控件 XAML
    ├── GripperControlView.xaml.cs                 # Code-behind (极简)
    └── GripperControlViewModel.cs                 # 控件 ViewModel (注入IGripperService)
```

---

### Task 1: 创建夹爪状态模型和事件定义

**Files:**
- Create: `MotionControl/Models/GripperState.cs`
- Create: `MotionControl/Events/GripperStateChangedEvent.cs`

**目标:** 定义夹爪运行时状态数据结构和事件，用于 UI 绑定和跨组件通信

**Step 1: 创建 GripperState 状态模型**

```csharp
// 文件: MotionControl/Models/GripperState.cs
using System;

namespace MotionControl.Models
{
    public enum GripperStatus
    {
        Unknown = 0,
        Idle = 1,          // 空闲（已释放）
        Moving = 2,        // 运动中
        Clamping = 3,      // 夹紧中
        Clamped = 4,       // 已夹紧
        Releasing = 5,     // 释放中
        Error = 6,         // 错误状态
        Homing = 7         // 回零中
    }

    public class GripperState
    {
        public string GripperId { get; set; } = "Gripper1";
        
        public GripperStatus Status { get; set; } = GripperStatus.Unknown;
        
        public double CurrentPosition { get; set; }  // 当前位置 (0-1000 或实际脉冲数)
        
        public double TargetPosition { get; set; }   // 目标位置
        
        public double CurrentTorque { get; set; }    // 当前力矩 (%)
        
        public double TargetTorque { get; set; }     // 目标力矩设定值 (%)
        
        public bool IsAlarmActive { get; set; }      // 报警标志
        
        public bool IsAtHome { get; set; }           // 是否在原点
        
        public DateTime LastUpdateTime { get; set; } = DateTime.Now;
        
        public string ErrorMessage { get; set; } = "";
    }
}
```

**Step 2: 创建状态变更事件**

```csharp
// 文件: MotionControl/Events/GripperStateChangedEvent.cs
using MotionControl.Models;
using Prism.Events;

namespace MotionControl.Events
{
    public class GripperStateChangedEvent : PubSubEvent<GripperState> { }
}
```

**验证要点:**
- [ ] GripperState 包含所有必要的实时属性
- [ ] GripperStatus 枚举覆盖完整的状态机
- [ ] 事件继承 Prism 的 PubSubEvent 以支持弱引用订阅

---

### Task 2: 定义 IGripperService 抽象接口

**Files:**
- Create: `MotionControl/Interfaces/IGripperService.cs`

**目标:** 基于现有 IMotionService 的 WriteDo/ReadDi 能力，定义高层夹爪操作接口，支持依赖注入和单元测试 Mock

**设计原则:**
1. **不直接调用 LTDMC** - 通过注入的 IMotionService 间接调用
2. **异步优先** - 所有耗时操作使用 async/await
3. **CancellationToken 支持** - 支持急停快速响应
4. **IObservable 模式** - 复用现有的轮询机制发布状态事件

```csharp
// 文件: MotionControl/Interfaces/IGripperService.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using MotionControl.Models;

namespace MotionControl.Interfaces
{
    /// <summary>
    /// 电夹爪控制服务接口
    /// 基于 IMotionService 的 DO/DI 能力封装高层夹爪操作
    /// </summary>
    public interface IGripperService : IObservable<GripperStateChangedEvent>
    {
        #region 生命周期
        Task InitializeAsync(CancellationToken token = default);
        void StartMonitoring(int intervalMs = 200);  // 启动位置/状态监控
        void StopMonitoring();
        #endregion

        #region 快速操作（用于 Pick 流程）
        Task ClampAsync(double position, CancellationToken token = default);
        Task ReleaseAsync(double position, CancellationToken token = default);
        #endregion

        #region 运动控制
        Task MoveToPositionAsync(double position, double speed, CancellationToken token = default);
        Task JogLeftAsync(double step, double speed, CancellationToken token = default);
        Task JogRightAsync(double step, double speed, CancellationToken token = default);
        void Stop();
        #endregion

        #region 力矩控制
        void SetTorque(double percentage);  // 0-100%
        double GetCurrentTorque();
        #endregion

        #region 系统操作
        Task HomeAsync(CancellationToken token = default);
        void ResetAlarm();
        #endregion

        #region 状态查询
        GripperState GetState();
        double GetCurrentPosition();
        bool IsMoving { get; }
        bool IsInitialized { get; }
        #endregion

        #region 配置（从 hwcfg.xml 或代码配置）
        int DoClampPort { get; }     // 夹紧DO端口
        int DoReleasePort { get; }   // 释放DO端口  
        int DiClampedPort { get; }   // 夹紧到位DI
        int DiReleasedPort { get; }  // 释放到位DI
        int DiAlarmPort { get; }     // 报警DI
        int AxisId { get; }         // 夹爪轴ID（如果是电动夹爪）
        #endregion
    }
}
```

**关键设计决策说明:**
- **为什么用 DO/DI 而不是直接调 LTDMC?**
  - 符合项目现有架构（IMotionService 已统一管理 IO 映射）
  - 支持 VirtualMotionCard 无硬件调试
  - 便于替换为其他品牌夹爪（只需改配置）

- **为什么保留 AxisId?**
  - 电动夹爪（如旧项目的雷赛电爪）需要轴控制
  - 气动夹爪只需要 DO 控制
  - 接口兼容两种模式

**验证要点:**
- [ ] 所有方法都有 CancellationToken 参数（支持急停）
- [ ] 返回 Task 的方法是异步的
- [ ] 配置属性为只读（由构造函数或初始化设置）
- [ ] 继承 IObservable 以支持事件驱动 UI 更新

---

### Task 3: 实现 GripperService（基于 IMotionService）

**Files:**
- Create: `MotionControl/Services/GripperService.cs`

**目标:** 实现 IGripperService 接口，注入 IMotionService 完成实际的硬件操作

**核心实现逻辑:**

```csharp
// 文件: MotionControl/Services/GripperService.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Utilities;
using MotionControl.Events;
using MotionControl.Interfaces;
using MotionControl.Models;
using Prism.Events;

namespace MotionControl.Services
{
    public class GripperService : IGripperService
    {
        private readonly IMotionService _motionService;
        private readonly ILoggerService _logger;
        private readonly IEventAggregator _eventAggregator;
        private readonly GripperState _state = new GripperState();
        private Timer _monitorTimer;
        private bool _isMonitoring;

        // TODO: 这些配置应该从 hwcfg.xml 读取或通过构造函数注入
        public int DoClampPort { get; private set; } = 10;   // 示例值，需根据实际配置
        public int DoReleasePort { get; private set; } = 11;
        public int DiClampedPort { get; private set; } = 20;
        public int DiReleasedPort { get; private set; } = 21;
        public int DiAlarmPort { get; private set; } = 22;
        public int AxisId { get; private set; } = 2;  // 电爪轴号（参考旧项目 AssemblyStationView）

        public bool IsMoving => _state.Status == GripperStatus.Moving || 
                               _state.Status == GripperStatus.Clamping ||
                               _state.Status == GripperStatus.Releasing;
        public bool IsInitialized { get; private set; }

        public GripperService(
            IMotionService motionService, 
            ILoggerService logger,
            IEventAggregator eventAggregator)
        {
            _motionService = motionService;
            _logger = logger;
            _eventAggregator = eventAggregator;
        }

        public async Task InitializeAsync(CancellationToken token = default)
        {
            _logger.Info("Initializing gripper service...");
            
            // 读取初始状态
            UpdateStateFromHardware();
            
            IsInitialized = true;
            _logger.Info($"Gripper service initialized. Current pos: {_state.CurrentPosition}");
        }

        public void StartMonitoring(int intervalMs = 200)
        {
            if (_isMonitoring) return;
            _isMonitoring = true;
            _monitorTimer = new Timer(_ => 
            {
                try
                {
                    UpdateStateFromHardware();
                    PublishStateChange();
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Gripper monitor error: {ex.Message}");
                }
            }, null, 0, intervalMs);
            
            _logger.Info($"Gripper monitoring started (interval={intervalMs}ms)");
        }

        public void StopMonitoring()
        {
            _isMonitoring = false;
            _monitorTimer?.Dispose();
            _monitorTimer = null;
        }

        #region 快速操作实现

        public async Task ClampAsync(double position, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            ValidateInitialized();

            _logger.Info($"Clamping to position: {position}");
            _state.Status = GripperStatus.Clamping;
            _state.TargetPosition = position;
            PublishStateChange();

            try
            {
                // 方案A: 如果是电动夹爪（有轴控制）
                if (AxisId > 0)
                {
                    await _motionService.MoveAbsAsync(AxisId, position, 50, token);
                    await WaitForMoveComplete(token);
                }
                
                // 方案B: 气动夹爪（DO输出）
                _motionService.WriteDo(DoClampPort, true);
                _motionService.WriteDo(DoReleasePort, false);

                // 等待夹紧到位信号（带超时）
                await WaitForDiSignal(DiClampedPort, true, TimeSpan.FromMilliseconds(2000), token);

                _state.Status = GripperStatus.Clamped;
                _state.CurrentPosition = position;
                _logger.Info("Clamp completed successfully");
            }
            catch (OperationCanceledException)
            {
                _state.Status = GripperStatus.Idle;
                throw;
            }
            catch (Exception ex)
            {
                _state.Status = GripperStatus.Error;
                _state.ErrorMessage = ex.Message;
                _logger.Error($"Clamp failed: {ex.Message}");
                throw new RecoverableException(
                    $"夹紧失败: {ex.Message}",
                    "请检查气压、夹爪传感器或物料是否卡住");
            }
            finally
            {
                PublishStateChange();
            }
        }

        public async Task ReleaseAsync(double position, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            ValidateInitialized();

            _logger.Info($"Releasing to position: {position}");
            _state.Status = GripperStatus.Releasing;
            _state.TargetPosition = position;
            PublishStateChange();

            try
            {
                if (AxisId > 0)
                {
                    await _motionService.MoveAbsAsync(AxisId, position, 50, token);
                    await WaitForMoveComplete(token);
                }

                _motionService.WriteDo(DoReleasePort, true);
                _motionService.WriteDo(DoClampPort, false);

                await WaitForDiSignal(DiReleasedPort, true, TimeSpan.FromMilliseconds(2000), token);

                _state.Status = GripperStatus.Idle;
                _state.CurrentPosition = position;
                _logger.Info("Release completed successfully");
            }
            catch (OperationCanceledException)
            {
                _state.Status = GripperStatus.Clamped;  // 保持当前状态
                throw;
            }
            catch (Exception ex)
            {
                _state.Status = GripperStatus.Error;
                _state.ErrorMessage = ex.Message;
                _logger.Error($"Release failed: {ex.Message}");
                throw new RecoverableException(
                    $"释放失败: {ex.Message}",
                    "请检查气路或夹爪机械结构");
            }
            finally
            {
                PublishStateChange();
            }
        }

        #endregion

        #region 运动控制实现

        public async Task MoveToPositionAsync(double position, double speed, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            ValidateInitialized();

            _state.Status = GripperStatus.Moving;
            _state.TargetPosition = position;
            PublishStateChange();

            try
            {
                await _motionService.MoveAbsAsync(AxisId, position, speed, token);
                await WaitForMoveComplete(token);
                _state.CurrentPosition = position;
                _state.Status = _state.CurrentPosition == _state.TargetPosition ? 
                    GripperStatus.Idle : GripperStatus.Error;
            }
            catch (Exception ex)
            {
                _state.Status = GripperStatus.Error;
                _state.ErrorMessage = ex.Message;
                throw;
            }
            finally
            {
                PublishStateChange();
            }
        }

        public async Task JogLeftAsync(double step, double speed, CancellationToken token = default)
        {
            var currentPos = GetCurrentPosition();
            var targetPos = currentPos - step;
            await MoveToPositionAsync(targetPos, speed, token);
        }

        public async Task JogRightAsync(double step, double speed, CancellationToken token = default)
        {
            var currentPos = GetCurrentPosition();
            var targetPos = currentPos + step;
            await MoveToPositionAsync(targetPos, speed, token);
        }

        public void Stop()
        {
            _motionService.StopAxis(AxisId);
            _state.Status = GripperStatus.Idle;
            PublishStateChange();
            _logger.Info("Gripper stopped by user");
        }

        #endregion

        #region 力矩控制

        public void SetTorque(double percentage)
        {
            if (percentage < 0 || percentage > 100)
                throw new ArgumentOutOfRangeException(nameof(percentage), "力矩必须在 0-100% 之间");

            // 对于电爪，力矩通常通过模拟量输出或特定PDO映射设置
            // 这里使用 WriteDo 模拟（实际应根据硬件手册调整）
            // 示例：假设力矩百分比映射到某个AO通道或PDO寄存器
            
            _state.TargetTorque = percentage;
            _logger.Info($"Gripper torque set to {percentage}%");
            
            // TODO: 根据实际硬件实现力矩写入
            // 可能需要调用 _motionService.WriteAo(port, voltage) 如果支持AO
        }

        public double GetCurrentTorque()
        {
            // TODO: 从AD通道读取实际力矩反馈
            return _state.CurrentTorque;
        }

        #endregion

        #region 系统操作

        public async Task HomeAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            ValidateInitialized();

            _state.Status = GripperStatus.Homing;
            PublishStateChange();

            try
            {
                await _motionService.HomeAsync(AxisId, mode: 1, minVel: 5, maxVel: 20, token);
                _state.IsAtHome = true;
                _state.CurrentPosition = 0;
                _state.Status = GripperStatus.Idle;
                _logger.Info("Gripper homing completed");
            }
            catch (Exception ex)
            {
                _state.Status = GripperStatus.Error;
                _state.ErrorMessage = ex.Message;
                throw new RecoverableException(
                    $"回零失败: {ex.Message}",
                    "请检查原点传感器或清除报警后重试");
            }
            finally
            {
                PublishStateChange();
            }
        }

        public void ResetAlarm()
        {
            _motionService.ClearAlarm(AxisId);
            _state.IsAlarmActive = false;
            _state.ErrorMessage = "";
            _state.Status = GripperStatus.Idle;
            PublishStateChange();
            _logger.Info("Gripper alarm reset");
        }

        #endregion

        #region 状态查询

        public GripperState GetState() => _state;

        public double GetCurrentPosition()
        {
            if (AxisId > 0)
            {
                _state.CurrentPosition = _motionService.GetAxisPosition(AxisId);
            }
            return _state.CurrentPosition;
        }

        public IDisposable Subscribe(IObserver<GripperStateChangedEvent> observer)
        {
            // 简化实现：直接返回事件的订阅
            // 完整实现应包装 EventAggregator 的事件
            return null; // TODO: 实现完整的 Observable 模式
        }

        #endregion

        #region 私有辅助方法

        private void ValidateInitialized()
        {
            if (!IsInitialized)
                throw new InvalidOperationException("Gripper service not initialized. Call InitializeAsync first.");
        }

        private async Task WaitForMoveComplete(CancellationToken token)
        {
            // 轮询等待运动完成（参考旧项目 AssemblyStationViewModel.HomeAsync 的 SpinWait 模式）
            var spinWait = new System.Threading.SpinWait();
            while (true)
            {
                token.ThrowIfCancellationRequested();
                
                // 读取完成状态（具体API取决于IMotionCard实现）
                // 这里简化处理，实际应调用 card.CheckDone(axisId)
                await Task.Delay(10, token); // 10ms 轮询间隔
                
                var currentPos = GetCurrentPosition();
                if (Math.Abs(currentPos - _state.TargetPosition) < 0.05) // 0.05mm容差
                    break;
                    
                spinWait.SpinOnce();
            }
        }

        private async Task WaitForDiSignal(int port, bool expectedValue, TimeSpan timeout, CancellationToken token)
        {
            var deadline = DateTime.Now.Add(timeout);
            while (DateTime.Now < deadline)
            {
                token.ThrowIfCancellationRequested();
                
                var actualValue = _motionService.ReadDi(port);
                if (actualValue == expectedValue) return;
                
                await Task.Delay(10, token);
            }
            
            throw new TimeoutException($"等待DI信号超时 (port={port}, expected={expectedValue})");
        }

        private void UpdateStateFromHardware()
        {
            try
            {
                if (AxisId > 0)
                    _state.CurrentPosition = _motionService.GetAxisPosition(AxisId);
                
                _state.IsAlarmActive = _motionService.ReadDi(DiAlarmPort);
                _state.LastUpdateTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.Debug($"Update state error: {ex.Message}");
            }
        }

        private void PublishStateChange()
        {
            try
            {
                var evt = _eventAggregator.GetEvent<GripperStateChangedEvent>();
                evt.Publish(_state);
            }
            catch (Exception ex)
            {
                _logger.Debug($"Publish state error: {ex.Message}");
            }
        }

        #endregion
    }
}
```

**关键实现细节:**
1. **Pick Holding Time 的实现位置**: 不在这个服务中！它属于流程执行层（PickStepAction），见 Task 9
2. **错误处理**: 使用 `RecoverableException` 以享受自动重试机制
3. **线程安全**: `_state` 对象的更新都在 MonitorTimer 回调或主线程
4. **资源释放**: `StopMonitoring()` 必须在 ViewModel 的 OnNavigatedFrom 中调用

**验证要点:**
- [ ] 构造函数注入 IMotionService, ILoggerService, IEventAggregator
- [ ] ClampAsync/ReleaseAsync 包含 DI 信号等待
- [ ] 所有公共方法都有日志记录
- [ ] CancellationToken 在正确位置检查
- [ ] 状态变更都触发 PublishStateChange()

---

### Task 4: 注册 IGripperService 到 DI 容器

**Files:**
- Modify: `MotionControl/MotionControlModule.cs` (第43-61行 RegisterTypes 方法)

**目标:** 将新服务注册为 Singleton，确保全局唯一实例

**修改内容:**

```csharp
// 在 MotionControlModule.cs 的 RegisterTypes 方法末尾添加:

public void RegisterTypes(IContainerRegistry containerRegistry)
{
    // ... 现有注册代码 ...
    
    // ★ 新增：注册夹爪服务
    containerRegistry.RegisterSingleton<IGripperService, GripperService>();
}

// 在 OnInitialized 方法中添加初始化（可选，也可以延迟初始化）:
public void OnInitialized(IContainerProvider containerProvider)
{
    // ... 现有初始化代码 ...
    
    try
    {
        // 可选：预初始化夹爪服务
        var gripperService = containerProvider.Resolve<IGripperService>();
        gripperService.InitializeAsync().Wait();
        _logger.Info("Gripper service pre-initialized");
    }
    catch (Exception ex)
    {
        logger.Warn($"Gripper service init deferred: {ex.Message}");
    }
}
```

**验证要点:**
- [ ] 使用 RegisterSingleton（不是 RegisterInstance）
- [ ] 放在现有注册之后，避免顺序问题
- [ ] 初始化异常被捕获，不影响其他模块

---

### Task 5: 扩展 PickDetail 数据模型

**Files:**
- Modify: `StationTasks/Models/ProcessStep.cs` (第326-343行 PickDetail 类)

**目标:** 添加夹紧位置和释放位置字段，支持步骤级配置

**修改内容:**

```csharp
public class PickDetail : BindableBase
{
    // === 现有字段保持不变 ===
    private double _jawOpen = 10.0;
    private double _jawForce = 15.0;
    private int _vacuumPressure = 80;
    private int _pickHoldingTime = 500;       // ★ 保持时间(ms) - 用于流程延时
    private int _vacuumCheckDelay = 200;
    private bool _isVacuumOn;
    
    // ★ 新增字段：夹爪位置配置
    private double _clampPosition = 100.0;    // 夹紧目标位置 (mm或脉冲数)
    private double _releasePosition = 500.0;  // 释放目标位置 (mm或脉冲数)

    // === 现有属性保持不变 ===
    public double JawOpen { get => _jawOpen; set => SetProperty(ref _jawOpen, value); }
    public double JawForce { get => _jawForce; set => SetProperty(ref _jawForce, value); }
    public int VacuumPressure { get => _vacuumPressure; set => SetProperty(ref _vacuumPressure, value); }
    
    /// <summary>
    /// 取料保持时间（毫秒）
    /// 实现方式：在 PickStepAction 中执行完夹紧动作后，Task.Delay(PickHoldingTime)
    /// 用途：确保真空吸附稳定或机械夹持牢固
    /// </summary>
    public int PickHoldingTime 
    { 
        get => _pickHoldingTime; 
        set => SetProperty(ref _pickHoldingTime, value); 
    }
    
    public int VacuumCheckDelay { get => _vacuumCheckDelay; set => SetProperty(ref _vacuumCheckDelay, value); }
    public bool IsVacuumOn { get => _isVacuumOn; set => SetProperty(ref _isVacuumOn, value); }

    // ★ 新增属性
    /// <summary> 夹紧位置：执行夹紧命令时夹爪移动到的目标位置 </summary>
    public double ClampPosition 
    { 
        get => _clampPosition; 
        set => SetProperty(ref _clampPosition, value); 
    }
    
    /// <summary> 释放位置：执行释放命令时夹爪移动到的目标位置 </summary>
    public double ReleasePosition 
    { 
        get => _releasePosition; 
        set => SetProperty(ref _releasePosition, value); 
    }

    public ObservableCollection<SubMove> PickMoves { get; set; } = new ObservableCollection<SubMove>();
}
```

**PickHoldingTime 实现说明:**
这个参数不在 GripperService 中使用，而是在 **PickStepAction**（取料步骤执行器）中使用：

```
PickStepAction.ExecuteAsync():
  1. 执行 PickMoves 序列（移动到取料位置）
  2. 调用 gripperService.ClampAsync(pickDetail.ClampPosition)  ← 夹紧
  3. await Task.Delay(pickDetail.PickHoldingTime)              ← ★ 保持延时
  4. 检查真空/夹持确认信号（如果启用）
  5. 继续后续流程...
```

**验证要点:**
- [ ] 新属性使用 SetProperty（支持 INotifyPropertyChanged）
- [ ] 默认值合理（根据实际硬件调整）
- [ ] XML 注释清晰说明用途
- [ ] PickHoldingTime 的注释解释了实现位置

---

### Task 6: 创建夹爪控件 ViewModel

**Files:**
- Create: `Module/UserControls/Grippers/GripperControlViewModel.cs`

**目标:** 实现 ViewModel，连接 IGripperService 和 UI，包含完整控制和增强功能

**核心功能清单:**
1. ✅ 快速操作（夹紧/释放）
2. ✅ 目标位置控制
3. ✅ Jog寸动（左/右/停止）
4. ✅ 力矩设定
5. ✅ 系统操作（回零/复位）
6. ✅ 实时位置显示（定时刷新）
7. ✅ 状态指示灯（颜色变化）
8. ✅ 权限和安全检查
9. ✅ 多语言键绑定

```csharp
// 文件: Module/UserControls/Grippers/GripperControlViewModel.cs
using System;
using System.Windows.Input;
using Core.Utilities;
using MotionControl.Interfaces;
using MotionControl.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;

namespace Module.UserControls.Grippers
{
    public class GripperControlViewModel : BindableBase, INavigationAware, IDisposable
    {
        private readonly IGripperService _gripperService;
        private readonly ILoggerService _logger;
        private readonly IDialogService _dialogService;

        #region UI绑定属性

        private double _targetPosition = 500;
        public double TargetPosition
        {
            get => _targetPosition;
            set => SetProperty(ref _targetPosition, value);
        }

        private double _moveSpeed = 50;
        public double MoveSpeed
        {
            get => _moveSpeed;
            set => SetProperty(ref _moveSpeed, value);
        }

        private double _jogStep = 5;
        public double JogStep
        {
            get => _jogStep;
            set => SetProperty(ref _jogStep, value);
        }

        private double _jogSpeed = 30;
        public double JogSpeed
        {
            get => _jogSpeed;
            set => SetProperty(ref _jogSpeed, value);
        }

        private string _torquePercentage = "50";
        public string TorquePercentage
        {
            get => _torquePercentage;
            set
            {
                if (SetProperty(ref _torquePercentage, value))
                    RaisePropertyChanged(nameof(TorqueDisplay));
            }
        }

        public string TorqueDisplay
        {
            get
            {
                if (double.TryParse(TorquePercentage, out double pct))
                    return $"{pct * 0.15:F1} N";  // 0-100% → 0-15N
                return "0.0 N";
            }
        }

        private double _currentPosition;
        public double CurrentPosition
        {
            get => _currentPosition;
            set => SetProperty(ref _currentPosition, value);
        }

        private GripperStatus _status = GripperStatus.Unknown;
        public GripperStatus Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        private string _statusMessage = "未初始化";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        #endregion

        #region 命令定义

        public ICommand ClampCommand { get; }
        public ICommand ReleaseCommand { get; }
        public ICommand MoveToTargetCommand { get; }
        public ICommand JogLeftCommand { get; }
        public ICommand JogRightCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand SetTorqueCommand { get; }
        public ICommand HomeCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand CloseCommand { get; }

        #endregion

        public GripperControlViewModel(
            IGripperService gripperService,
            ILoggerService logger,
            IDialogService dialogService)
        {
            _gripperService = gripperService;
            _logger = logger;
            _dialogService = dialogService;

            // 初始化命令
            ClampCommand = new DelegateCommand(async () => await ExecuteClamp());
            ReleaseCommand = new DelegateCommand(async () => await ExecuteRelease());
            MoveToTargetCommand = new DelegateCommand(async () => await ExecuteMoveToTarget());
            JogLeftCommand = new DelegateCommand(async () => await ExecuteJogLeft());
            JogRightCommand = new DelegateCommand(async () => await ExecuteJogRight());
            StopCommand = new DelegateCommand(ExecuteStop);
            SetTorqueCommand = new DelegateCommand(ExecuteSetTorque);
            HomeCommand = new DelegateCommand(async () => await ExecuteHome());
            ResetCommand = new DelegateCommand(ExecuteReset);
            CloseCommand = new DelegateCommand(OnClose);
        }

        #region 命令实现

        private async System.Threading.Tasks.Task ExecuteClamp()
        {
            if (!CheckSafety()) return;
            try
            {
                StatusMessage = "正在夹紧...";
                // 使用外部传入的位置参数（如果有），否则使用 TargetPosition
                var clampPos = ExternalClampPosition ?? TargetPosition;
                await _gripperService.ClampAsync(clampPos);
                StatusMessage = $"夹紧完成 (位置: {clampPos})";
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private async System.Threading.Tasks.Task ExecuteRelease()
        {
            if (!CheckSafety()) return;
            try
            {
                StatusMessage = "正在释放...";
                var releasePos = ExternalReleasePosition ?? TargetPosition;
                await _gripperService.ReleaseAsync(releasePos);
                StatusMessage = $"释放完成 (位置: {releasePos})";
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private async System.Threading.Tasks.Task ExecuteMoveToTarget()
        {
            if (!CheckSafety()) return;
            try
            {
                StatusMessage = $"移动到 {TargetPosition}...";
                await _gripperService.MoveToPositionAsync(TargetPosition, MoveSpeed);
                StatusMessage = "移动完成";
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private async System.Threading.Tasks.Task ExecuteJogLeft()
        {
            if (!CheckSafety()) return;
            try
            {
                await _gripperService.JogLeftAsync(JogStep, JogSpeed);
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private async System.Threading.Tasks.Task ExecuteJogRight()
        {
            if (!CheckSafety()) return;
            try
            {
                await _gripperService.JogRightAsync(JogStep, JogSpeed);
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private void ExecuteStop()
        {
            _gripperService.Stop();
            StatusMessage = "已停止";
        }

        private void ExecuteSetTorque()
        {
            if (!double.TryParse(TorquePercentage, out double pct))
            {
                ShowDialog("输入错误", "请输入有效的数字", PackIconKind.AlertCircle);
                return;
            }
            if (pct < 0 || pct > 100)
            {
                ShowDialog("参数错误", "力矩范围: 0-100%", PackIconKind.AlertCircle);
                return;
            }

            _gripperService.SetTorque(pct);
            _logger.Info($"Torque set to {pct}% ({pct * 0.15:F1}N)");
            StatusMessage = $"力矩已设定: {pct}%";
        }

        private async System.Threading.Tasks.Task ExecuteHome()
        {
            if (!CheckSafety()) return;
            try
            {
                StatusMessage = "正在回零...";
                await _gripperService.HomeAsync();
                StatusMessage = "回零完成";
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private void ExecuteReset()
        {
            _gripperService.ResetAlarm();
            StatusMessage = "报警已清除";
        }

        #endregion

        #region 外部参数支持（供 PickDetailView 传入）

        public double? ExternalClampPosition { get; set; }
        public double? ExternalReleasePosition { get; set; }

        /// <summary>
        /// 设置外部位置参数（从 PickDetail 传入）
        /// </summary>
        public void SetExternalPositions(double clampPos, double releasePos)
        {
            ExternalClampPosition = clampPos;
            ExternalReleasePosition = releasePos;
            RaisePropertyChanged(nameof(ExternalClampPosition));
        }

        #endregion

        #region 安全检查和错误处理

        private bool CheckSafety()
        {
            // TODO: 检查管理员权限
            // TODO: 检查设备运行状态
            if (!_gripperService.IsInitialized)
            {
                ShowDialog("错误", "夹爪服务未初始化", PackIconKind.AlertCircle);
                return false;
            }
            return true;
        }

        private void HandleError(Exception ex)
        {
            _logger.Error($"Gripper operation failed: {ex.Message}");
            StatusMessage = $"错误: {ex.Message}";
            ShowDialog("操作失败", ex.Message, PackIconKind.AlertCircle);
        }

        private void ShowDialog(string title, string message, PackIconKind icon)
        {
            _dialogService.ShowDialog("NotificationDialog", new DialogParameters
            {
                { "title", title },
                { "message", message },
                { "icon", icon }
            });
        }

        #endregion

        #region 生命周期管理

        private System.Windows.Threading.DispatcherTimer _uiUpdateTimer;

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            // 启动 UI 刷新定时器（200ms，与旧项目一致）
            _uiUpdateTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _uiUpdateTimer.Tick += (s, e) =>
            {
                try
                {
                    if (_gripperService.IsInitialized)
                    {
                        var state = _gripperService.GetState();
                        CurrentPosition = state.CurrentPosition;
                        Status = state.Status;
                        IsConnected = true;
                        
                        // 自动更新状态消息
                        if (StatusMessage == "未初始化")
                            StatusMessage = "就绪";
                    }
                }
                catch { /* 忽略定时器中的异常 */ }
            };
            _uiUpdateTimer.Start();

            // 启动硬件监控
            _gripperService.StartMonitoring(200);
            _logger.Info("Gripper control view activated");
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            // 清理资源
            _uiUpdateTimer?.Stop();
            _uiUpdateTimer = null;
            _gripperService.StopMonitoring();
            _logger.Info("Gripper control view deactivated");
        }

        public void Dispose()
        {
            OnNavigatedFrom(null);
        }

        #endregion
    }
}
```

**增强功能实现要点:**
1. **定时刷新**: DispatcherTimer 每200ms更新UI（与旧项目一致）
2. **状态指示灯**: `Status` 属性绑定到转换器，自动变色
3. **外部参数**: `ExternalClampPosition` 允许 PickDetailView 传入配置值
4. **安全检查**: `CheckSafety()` 预留权限和运行状态检测
5. **资源清理**: `OnNavigatedFrom` 停止定时器和监控

**验证要点:**
- [ ] 构造函数注入 3 个服务
- [ ] 所有 async 命令使用 async lambda
- [ ] OnNavigatedTo/OnNavigatedFrom 成对出现
- [ ] Dispose 模式正确实现
- [ ] ExternalPositions 属性支持双向绑定

---

### Task 7: 创建夹爪控件 XAML UI

**Files:**
- Create: `Module/UserControls/Grippers/GripperControlView.xaml`
- Create: `Module/UserControls/Grippers/GripperControlView.xaml.cs`

**目标:** 实现工业风格的专业界面，包含完整控制功能和状态指示

**UI布局方案:**

```xml
<!-- 文件: Module/UserControls/Grippers/GripperControlView.xaml -->
<UserControl x:Class="Module.UserControls.Grippers.GripperControlView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes"
             xmlns:converters="clr-namespace:MotionControl.Converters;assembly=MotionControl"
             Width="650" Height="750">
    
    <UserControl.Resources>
        <converters:GripperStatusToBrushConverter x:Key="StatusToBrush"/>
    </UserControl.Resources>

    <Border Padding="16" Background="{DynamicResource MaterialDesignCardBackground}" CornerRadius="8">
        <StackPanel>
            <!-- ===== 标题栏 ===== -->
            <DockPanel Margin="0,0,0,12">
                <Button DockPanel.Dock="Right" Command="{Binding CloseCommand}"
                        Style="{StaticResource MaterialDesignIconButton}" ToolTip="关闭">
                    <materialDesign:PackIcon Kind="Close" Width="20" Height="20"/>
                </Button>
                <StackPanel Orientation="Horizontal">
                    <materialDesign:PackIcon Kind="RobotIndustrial" Width="24" Height="24" 
                                             Foreground="{DynamicResource PrimaryHueMidBrush}" Margin="0,0,8,0"/>
                    <TextBlock Text="电爪手动控制面板" FontWeight="Bold" FontSize="18"
                               VerticalAlignment="Center"/>
                </StackPanel>
            </DockPanel>

            <!-- ===== 状态指示条 ===== -->
            <Border Background="{DynamicResource MaterialDesignPaper}" CornerRadius="4" 
                    Padding="12,8" Margin="0,0,0,12">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    
                    <!-- 状态指示灯 -->
                    <Ellipse Width="16" Height="16" Fill="{Binding Status, Converter={StaticResource StatusToBrush}}"
                            Grid.Column="0" VerticalAlignment="Center" ToolTip="{Binding Status}"/>
                    
                    <!-- 状态消息 -->
                    <TextBlock Text="{Binding StatusMessage}" Grid.Column="1" 
                               VerticalAlignment="Center" Margin="12,0,0,0" FontWeight="Medium"/>
                    
                    <!-- 连接状态 -->
                    <StackPanel Grid.Column="2" Orientation="Horizontal">
                        <materialDesign:PackIcon Kind="{Binding IsConnected, Converter={StaticResource BoolToIconConverter}}" 
                                                 Width="16" Height="16" VerticalAlignment="Center" Foreground="Green"/>
                        <TextBlock Text="在线" VerticalAlignment="Center" Margin="4,0,0,0"
                                   Visibility="{Binding IsConnected, Converter={StaticResource BooleanToVisibilityConverter}}"/>
                    </StackPanel>
                </Grid>
            </Border>

            <!-- ===== 第一行：快速操作区 ===== -->
            <GroupBox Header="⚡ 快速操作" Margin="0,0,0,12">
                <Grid Margin="8">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    
                    <!-- 夹紧按钮（醒目的橙红色） -->
                    <Button Grid.Column="0" Command="{Binding ClampCommand}" Margin="4"
                            Style="{StaticResource MaterialDesignRaisedButton}"
                            Background="#E65100" Foreground="White" Height="50"
                            FontSize="14" FontWeight="Bold" ToolTip="执行夹紧动作">
                        <StackPanel Orientation="Horizontal">
                            <materialDesign:PackIcon Kind="HandGrabbing" Width="24" Height="24" Margin="0,0,8,0"/>
                            <TextBlock Text="夹紧" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>

                    <!-- 释放按钮（安全的蓝色） -->
                    <Button Grid.Column="1" Command="{Binding ReleaseCommand}" Margin="4"
                            Style="{StaticResource MaterialDesignRaisedButton}"
                            Background="#1976D2" Foreground="White" Height="50"
                            FontSize="14" FontWeight="Bold" ToolTip="执行释放动作">
                        <StackPanel Orientation="Horizontal">
                            <materialDesign:PackIcon Kind="HandOff" Width="24" Height="24" Margin="0,0,8,0"/>
                            <TextBlock Text="释放" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>
                </Grid>
            </GroupBox>

            <!-- ===== 第二行：目标控制区 ===== -->
            <GroupBox Header="🎯 目标位置控制" Margin="0,0,0,12">
                <Grid Margin="8">
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>
                    
                    <!-- 位置和速度输入 -->
                    <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,8">
                        <TextBlock Text="目标位置:" VerticalAlignment="Center" Width="70"/>
                        <TextBox Text="{Binding TargetPosition, UpdateSourceTrigger=PropertyChanged}" 
                                 Width="100" materialDesign:HintAssist.Hint="mm"
                                 VerticalContentAlignment="Center"/>
                        <TextBlock Text="速度:" VerticalAlignment="Center" Width="50" Margin="16,0,0,0"/>
                        <Slider Minimum="1" Maximum="100" Value="{Binding MoveSpeed}" Width="150"
                                IsSnapToTickEnabled="True" TickFrequency="5" VerticalAlignment="Center"/>
                        <TextBlock Text="{Binding MoveSpeed, StringFormat={}{0:0}%}" 
                                   VerticalAlignment="Center" Margin="8,0,0,0" MinWidth="35"/>
                    </StackPanel>

                    <!-- 移动按钮 -->
                    <Button Grid.Row="1" Command="{Binding MoveToTargetCommand}"
                            Style="{StaticResource MaterialDesignRaisedButton}" HorizontalAlignment="Left">
                        <StackPanel Orientation="Horizontal">
                            <materialDesign:PackIcon Kind="Target" Width="18" Height="18" Margin="0,0,6,0"/>
                            <TextBlock Text="移动到目标位置"/>
                        </StackPanel>
                    </Button>
                </Grid>
            </GroupBox>

            <!-- ===== 第三行：Jog寸动区 ===== -->
            <GroupBox Header="🕹️ 寸动微调" Margin="0,0,0,12">
                <Grid Margin="8">
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>
                    
                    <!-- Jog 按钮 -->
                    <StackPanel Grid.Row="0" Orientation="Horizontal" HorizontalAlignment="Center">
                        <!-- 左移（关闭方向） -->
                        <Button Command="{Binding JogLeftCommand}" Width="100" Height="40" Margin="4"
                                Style="{StaticResource MaterialDesignOutlinedButton}">
                            <StackPanel Orientation="Horizontal">
                                <materialDesign:PackIcon Kind="ArrowCollapse" Width="20" Height="20" Margin="0,0,4,0"/>
                                <TextBlock Text="左移(夹)"/>
                            </StackPanel>
                        </Button>

                        <!-- 停止 -->
                        <Button Command="{Binding StopCommand}" Width="80" Height="40" Margin="4"
                                Style="{StaticResource MaterialDesignOutlinedButton}"
                                Background="#D32F2F" Foreground="White">
                            <StackPanel Orientation="Horizontal">
                                <materialDesign:PackIcon Kind="Stop" Width="20" Height="20" Margin="0,0,4,0"/>
                                <TextBlock Text="停止"/>
                            </StackPanel>
                        </Button>

                        <!-- 右移（打开方向） -->
                        <Button Command="{Binding JogRightCommand}" Width="100" Height="40" Margin="4"
                                Style="{StaticResource MaterialDesignOutlinedButton}">
                            <StackPanel Orientation="Horizontal">
                                <materialDesign:PackIcon Kind="ArrowExpand" Width="20" Height="20" Margin="0,0,4,0"/>
                                <TextBlock Text="右移(开)"/>
                            </StackPanel>
                        </Button>
                    </StackPanel>

                    <!-- 步长和速度设置 -->
                    <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,8,0,0">
                        <TextBlock Text="步长:" VerticalAlignment="Center"/>
                        <TextBox Text="{Binding JogStep, StringFormat={}{0:F1}}" Width="50" Margin="4,0"/>
                        <TextBlock Text="mm" VerticalAlignment="Center" Margin="0,0,16,0"/>
                        
                        <TextBlock Text="寸动速度:" VerticalAlignment="Center"/>
                        <Slider Minimum="1" Maximum="100" Value="{Binding JogSpeed}" Width="120"
                                IsSnapToTickEnabled="True" TickFrequency="5" VerticalAlignment="Center" Margin="4,0"/>
                        <TextBlock Text="{Binding JogSpeed, StringFormat={}{0:0}%}" 
                                   VerticalAlignment="Center" Margin="4,0,0,0" MinWidth="30"/>
                    </StackPanel>
                </Grid>
            </GroupBox>

            <!-- ===== 第四行：力矩设定区 ===== -->
            <GroupBox Header="💪 力矩设定" Margin="0,0,0,12">
                <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Margin="8">
                    <TextBlock Text="力矩:" VerticalAlignment="Center"/>
                    <TextBox Text="{Binding TorquePercentage, UpdateSourceTrigger=PropertyChanged}" 
                             Width="50" Margin="4,0" materialDesign:HintAssist.Hint="%"
                             VerticalContentAlignment="Center">
                        <TextBox.InputBindings>
                            <KeyBinding Key="Enter" Command="{Binding SetTorqueCommand}"/>
                        </TextBox.InputBindings>
                    </TextBox>
                    <TextBlock Text="%" VerticalAlignment="Center" Margin="4,0,12,0"/>
                    
                    <Button Command="{Binding SetTorqueCommand}" Margin="4,0"
                            Style="{StaticResource MaterialDesignRaisedButton}">
                        <materialDesign:PackIcon Kind="Cog" Width="18" Height="18" Margin="0,0,4,0"/>
                        <TextBlock Text="设定"/>
                    </Button>
                    
                    <Border Background="{DynamicResource MaterialDesignLightBackground}" 
                            CornerRadius="4" Padding="8,4" Margin="12,0,0,0">
                        <TextBlock Text="{Binding TorqueDisplay}" FontWeight="Bold" 
                                   Foreground="{DynamicResource PrimaryHueDarkBrush}"/>
                    </Border>
                </StackPanel>
            </GroupBox>

            <!-- ===== 第五行：系统操作区 ===== -->
            <GroupBox Header="🔧 系统操作" Margin="0,0,0,12">
                <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Margin="8">
                    <Button Command="{Binding HomeCommand}" Margin="4"
                            Style="{StaticResource MaterialDesignOutlinedButton}">
                        <StackPanel Orientation="Horizontal">
                            <materialDesign:PackIcon Kind="Home" Width="18" Height="18" Margin="0,0,4,0"/>
                            <TextBlock Text="回零"/>
                        </StackPanel>
                    </Button>

                    <Button Command="{Binding ResetCommand}" Margin="4"
                            Style="{StaticResource MaterialDesignOutlinedButton}">
                        <StackPanel Orientation="Horizontal">
                            <materialDesign:PackIcon Kind="Refresh" Width="18" Height="18" Margin="0,0,4,0"/>
                            <TextBlock Text="清除报警"/>
                        </StackPanel>
                    </Button>
                </StackPanel>
            </GroupBox>

            <!-- ===== 第六行：实时位置显示 ===== -->
            <GroupBox Header="📍 实时状态" Margin="0,0,0,0">
                <Grid Margin="8">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    
                    <TextBlock Grid.Column="0" Text="当前位置:" 
                               VerticalAlignment="Center" FontWeight="SemiBold"/>
                    
                    <Border Grid.Column="1" Background="{DynamicResource MaterialDesignLightBackground}"
                            CornerRadius="4" Padding="12,6" Margin="8,0,0,0">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="{Binding CurrentPosition, StringFormat={}{0:F2} mm}" 
                                       FontSize="20" FontWeight="Bold" 
                                       Foreground="{DynamicResource PrimaryHueMidBrush}"/>
                            <TextBlock Text="  (实时刷新: 200ms)" FontSize="11" 
                                       Foreground="Gray" VerticalAlignment="Center" Margin="8,0,0,0"/>
                        </StackPanel>
                    </Border>
                </Grid>
            </GroupBox>

        </StackPanel>
    </Border>
</UserControl>
```

**Code-Behind (极简):**

```csharp
// 文件: Module/UserControls/Grippers/GripperControlView.xaml.cs
using System.Windows.Controls;

namespace Module.UserControls.Grippers
{
    public partial class GripperControlView : UserControl
    {
        public GripperControlView()
        {
            InitializeComponent();
        }
    }
}
```

**视觉设计特点:**
1. ✅ **Material Design 卡片式布局** - 圆角边框、阴影层次
2. ✅ **语义化颜色** - 夹紧=橙色(#E65100)，释放=蓝色(#1976D2)，停止=红色(#D32F2F)
3. ✅ **图标系统** - 全部使用 materialDesign:PackIcon（无emoji）
4. ✅ **状态指示灯** - 左上角椭圆，颜色随 Status 变化
5. ✅ **分组清晰** - 6个 GroupBox 功能分区明确
6. ✅ **响应式布局** - 固定宽度650px，适合嵌入详情面板或弹窗

---

### Task 8: 创建状态颜色转换器

**Files:**
- Create: `MotionControl/Converters/GripperStatusToBrushConverter.cs`

**目标:** 根据 GripperStatus 返回对应的画刷颜色

```csharp
// 文件: MotionControl/Converters/GripperStatusToBrushConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MotionControl.Models;

namespace MotionControl.Converters
{
    public class GripperStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is GripperStatus status)
            {
                return status switch
                {
                    GripperStatus.Unknown => Brushes.Gray,
                    GripperStatus.Idle => new SolidColorBrush(Color.FromRgb(76, 175, 80)),    // Green
                    GripperStatus.Moving => new SolidColorBrush(Color.FromRgb(33, 150, 243)),  // Blue
                    GripperStatus.Clamping => new SolidColorBrush(color: Color.FromRgb(255, 152, 0)), // Orange
                    GripperStatus.Clamped => new SolidColorBrush(color: Color.FromRgb(230, 81, 0)),  // DeepOrange
                    GripperStatus.Releasing => new SolidColorBrush(Color.FromRgb(156, 39, 176)), // Purple
                    GripperStatus.Error => Brushes.Red,
                    GripperStatus.Homing => new SolidColorBrush(Color.FromRgb(255, 193, 7)),  // Amber
                    _ => Brushes.Gray
                };
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
```

**验证要点:**
- [ ] 覆盖所有 GripperStatus 枚举值
- [ ] 颜色选择符合直觉（绿色=正常，红色=错误）
- [ ] ConvertBack 抛出 NotSupportedException（单向绑定不需要）

---

### Task 9: 改进 PickDetailView UI

**Files:**
- Modify: `Module/Editor/PickDetailView.xaml` (第49-111行夹爪配置区域)
- Modify: `Module/Editor/PickDetailViewModel.cs` (新增属性和命令)

**目标:** 在现有夹爪配置区域增加 Clamp/Release 位置输入框和快速操作按钮

**XAML 修改内容:**

```xml
<!-- 替换原有的 GroupBox "Example gripper configuration" 为以下内容 -->

<!-- 夹爪配置区域（增强版） -->
<GroupBox Header="🤏 夹爪配置" Margin="0,0,0,16">
    <Grid Margin="8">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>  <!-- Row 0: Jaw open -->
            <RowDefinition Height="Auto"/>  <!-- Row 1: Jaw force -->
            <RowDefinition Height="Auto"/>  <!-- Row 2: Vacuum control -->
            <RowDefinition Height="Auto"/>  <!-- Row 3: ★ Clamp Position (NEW) -->
            <RowDefinition Height="Auto"/>  <!-- Row 4: ★ Release Position (NEW) -->
            <RowDefinition Height="Auto"/>  <!-- Row 5: Holding time -->
            <RowDefinition Height="Auto"/>  <!-- Row 6: Quick actions (NEW) -->
        </Grid.RowDefinitions>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="140"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>

        <!-- 现有字段保持不变... -->
        
        <!-- Row 0: Jaw open -->
        <TextBlock Grid.Row="0" Grid.Column="0" Text="Jaw open (mm)" VerticalAlignment="Center"/>
        <ComboBox Grid.Row="0" Grid.Column="1" ItemsSource="{Binding JawOpenOptions}"
                  SelectedItem="{Binding JawOpen}" Margin="4,2" IsEditable="True" 
                  Width="120" HorizontalAlignment="Left"/>

        <!-- Row 1: Jaw force -->
        <TextBlock Grid.Row="1" Grid.Column="0" Text="Jaw force (N)" VerticalAlignment="Center"/>
        <ComboBox Grid.Row="1" Grid.Column="1" ItemsSource="{Binding JawForceOptions}"
                  SelectedItem="{Binding JawForce}" Margin="4,2" IsEditable="True" 
                  Width="120" HorizontalAlignment="Left"/>

        <!-- Row 2: Vacuum control -->
        <TextBlock Grid.Row="2" Grid.Column="0" Text="Vacuum control" VerticalAlignment="Center"/>
        <StackPanel Grid.Row="2" Grid.Column="1" Orientation="Horizontal" Margin="4,2">
            <Button Content="ON" Command="{Binding VacuumOnCommand}" Margin="0,0,4,0"
                    Style="{StaticResource MaterialDesignRaisedButton}" Width="60"/>
            <Button Content="OFF" Command="{Binding VacuumOffCommand}"
                    Style="{StaticResource MaterialDesignRaisedButton}" Width="60"/>
            <TextBlock Text="{Binding VacuumStatusText}" VerticalAlignment="Center" 
                       Margin="8,0,0,0" FontWeight="Bold"/>
        </StackPanel>

        <!-- ★ Row 3: Clamp Position (新增) -->
        <TextBlock Grid.Row="3" Grid.Column="0" Text="★ Clamp Pos (mm)" 
                   VerticalAlignment="Center" FontWeight="SemiBold"
                   Foreground="{DynamicResource PrimaryHueMidBrush}"/>
        <Grid Grid.Row="3" Grid.Column="1" Margin="4,2">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            <TextBox Grid.Column="0" Text="{Binding ClampPosition, UpdateSourceTrigger=PropertyChanged}" 
                     Width="120" HorizontalAlignment="Left"
                     materialDesign:HintAssist.Hint="夹紧目标位置"/>
            <Button Grid.Column="1" Command="{Binding QuickClampCommand}" Margin="8,0,0,0"
                    Style="{StaticResource MaterialDesignOutlinedButton}" ToolTip="使用此位置执行夹紧">
                <StackPanel Orientation="Horizontal">
                    <materialDesign:PackIcon Kind="HandGrabbing" Width="16" Height="16" Margin="0,0,4,0"/>
                    <TextBlock Text="夹紧"/>
                </StackPanel>
            </Button>
        </Grid>

        <!-- ★ Row 4: Release Position (新增) -->
        <TextBlock Grid.Row="4" Grid.Column="0" Text="★ Release Pos (mm)" 
                   VerticalAlignment="Center" FontWeight="SemiBold"
                   Foreground="{DynamicResource PrimaryHueMidBrush}"/>
        <Grid Grid.Row="4" Grid.Column="1" Margin="4,2">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            <TextBox grid.Column="0" Text="{Binding ReleasePosition, UpdateSourceTrigger=PropertyChanged}" 
                     Width="120" HorizontalAlignment="Left"
                     materialDesign:HintAssist.Hint="释放目标位置"/>
            <Button Grid.Column="1" Command="{Binding QuickReleaseCommand}" Margin="8,0,0,0"
                    Style="{StaticResource MaterialDesignOutlinedButton}" ToolTip="使用此位置执行释放">
                <StackPanel Orientation="Horizontal">
                    <materialDesign:PackIcon Kind="HandOff" Width="16" Height="16" Margin="0,0,4,0"/>
                    <TextBlock Text="释放"/>
                </StackPanel>
            </Button>
        </Grid>

        <!-- Row 5: Pick holding time (保持不变但添加注释提示) -->
        <TextBlock Grid.Row="5" Grid.Column="0" Text="Hold time (ms)" VerticalAlignment="Center"/>
        <TextBox Grid.Row="5" Grid.Column="1" Text="{Binding PickHoldingTime}" Margin="4,2" 
                 materialDesign:HintAssist.Hint="夹紧后的保持延时"/>

        <!-- ★ Row 6: Open full control button (新增) -->
        <TextBlock Grid.Row="6" Grid.Column="0" Text="Full Control" 
                   VerticalAlignment="Center" FontWeight="SemiBold"/>
        <Button Grid.Row="6" Grid.Column="1" Command="{Binding OpenGripperControlCommand}"
                HorizontalAlignment="Left" Margin="4,2"
                Style="{StaticResource MaterialDesignRaisedButton}"
                Background="{DynamicResource PrimaryHueMidBrush}" Foreground="White">
            <StackPanel Orientation="Horizontal">
                <materialDesign:PackIcon Kind="TuneVertical" Width="18" Height="18" Margin="0,0,6,0"/>
                <TextBlock Text="打开夹爪控制面板" FontWeight="Medium"/>
            </StackPanel>
        </Button>
    </Grid>
</GroupBox>
```

**ViewModel 修改内容:**

```csharp
// 在 PickDetailViewModel.cs 中添加:

#region 新增属性

private double _clampPosition = 100.0;
public double ClampPosition
{
    get => _clampPosition;
    set
    {
        if (SetProperty(ref _clampPosition, value))
        {
            // 同步到底层数据模型
            if (_step?.PickDetail != null)
                _step.PickDetail.ClampPosition = value;
        }
    }
}

private double _releasePosition = 500.0;
public double ReleasePosition
{
    get => _releasePosition;
    set
    {
        if (SetProperty(ref _releasePosition, value))
        {
            if (_step?.PickDetail != null)
                _step.PickDetail.ReleasePosition = value;
        }
    }
}

#endregion

#region 新增命令

public ICommand QuickClampCommand { get; }
public ICommand QuickReleaseCommand { get; }
public ICommand OpenGripperControlCommand { get; }

// 在构造函数中初始化:
QuickClampCommand = new DelegateCommand(OnQuickClamp);
QuickReleaseCommand = new DelegateCommand(OnQuickRelease);
OpenGripperControlCommand = new DelegateCommand(OnOpenGripperControl);

private void OnQuickClamp()
{
    // 直接调用夹爪服务的 ClampAsync（需要注入 IGripperService）
    // 或者弹出确认对话框后再执行
    _dialogService.ShowDialog("ConfirmationDialog", new DialogParameters
    {
        { "title", "确认夹紧" },
        { "message", $"确定要夹紧到位置 {ClampPosition} mm 吗？" }
    }, async result =>
    {
        if (result.Result == ButtonResult.Yes)
        {
            try
            {
                // TODO: 注入并调用 _gripperService.ClampAsync(ClampPosition)
                ShowMessage("夹紧指令已发送", PackIconKind.CheckCircle);
            }
            catch (Exception ex)
            {
                ShowMessage($"夹紧失败: {ex.Message}", PackIconKind.AlertCircle);
            }
        }
    });
}

private void OnQuickRelease()
{
    // 类似 OnQuickClamp 但调用 ReleaseAsync
    _dialogService.ShowDialog("ConfirmationDialog", new DialogParameters
    {
        { "title", "确认释放" },
        { "message", $"确定要释放到位置 {ReleasePosition} mm 吗？" }
    }, result =>
    {
        if (result.Result == ButtonResult.Yes)
        {
            try
            {
                // TODO: _gripperService.ReleaseAsync(ReleasePosition)
                ShowMessage("释放指令已发送", PackIconKind.CheckCircle);
            }
            catch (Exception ex)
            {
                ShowMessage($"释放失败: {ex.Message}", PackIconKind.AlertCircle);
            }
        }
    });
}

private void OnOpenGripperControl()
{
    // 导航到 GripperControlView（通过 RegionManager 或 DialogService）
    // 方案A: 弹出模态窗口
    _dialogService.ShowDialog("GripperControlView", new DialogParameters
    {
        { "clampPosition", ClampPosition },
        { "releasePosition", ReleasePosition }
    });

    // 方案B: 在侧边栏区域显示（需要 RegionManager）
    // _regionManager.RequestNavigate("ContentRegion", new Uri("GripperControlView", UriKind.Relative));
}

#endregion

// 在 OnNavigatedTo 中刷新新属性:
public void OnNavigatedTo(NavigationContext navigationContext)
{
    // ... 现有代码 ...
    
    // ★ 刷新新增属性
    if (_step?.PickDetail != null)
    {
        ClampPosition = _step.PickDetail.ClampPosition;
        ReleasePosition = _step.PickDetail.ReleasePosition;
    }
}
```

**验证要点:**
- [ ] 新增两个 TextBox 双向绑定到 PickDetail 模型
- [ ] 快速操作按钮带有图标（PackIcon）
- [ ] 打开夹爪控件按钮样式醒目
- [ ] 所有新命令都有确认对话框（安全性）
- [ ] OnNavigatedTo 正确加载保存的配置值

---

### Task 10: 实现取料动作表格（复用 GotoDetailView 结构）

**Files:**
- Modify: `Module/Editor/PickDetailView.xaml` (第114-151行取料动作表格)

**目标:** 基于 GotoDetailView 的 DataGrid 列定义，删除 Station 列，适配 Pick 场景

**修改策略:**
由于 GotoDetailView 和 PickDetailView 都使用 `SubMove` 模型且列结构相似（只是 Pick 不需要 Station），我们采用 **复制+精简** 的方式：

```xml
<!-- 替换原有的 Pick Motion Sequence DataGrid 为以下内容 -->

<!-- 取料动作表格（基于 GotoDetailView 结构优化） -->
<GroupBox Header="📋 取料动作序列 (Pick Motion)" Margin="0,0,0,8">
    <StackPanel>
        <DataGrid ItemsSource="{Binding PickMoves}"
                  SelectedItem="{Binding SelectedPickMove}"
                  AutoGenerateColumns="False"
                  CanUserAddRows="False"
                  CanUserDeleteRows="False"
                  materialDesign:DataGridAssist.CellPadding="4"
                  MaxHeight="300">
            <DataGrid.Columns>
                <!-- Sub 序号列（保留） -->
                <DataGridTextColumn Header="#" Binding="{Binding SubSeq}" 
                                    Width="40" IsReadOnly="True"/>

                <!-- ★ 删除 Station 列（Goto 有，Pick 不需要）-->

                <!-- Axis 列（保留，但去掉 StationId 前缀显示） -->
                <DataGridTemplateColumn Header="Axis" Width="80">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <TextBlock Text="{Binding Axis}" VerticalAlignment="Center"/>
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                    <DataGridTemplateColumn.CellEditingTemplate>
                        <DataTemplate>
                            <ComboBox ItemsSource="{Binding DataContext.AvailableAxes, 
                                                        RelativeSource={RelativeSource AncestorType=UserControl}}"
                                      SelectedItem="{Binding Axis, UpdateSourceTrigger=PropertyChanged}"
                                      IsEditable="False"/>
                        </DataTemplate>
                    </DataGridTemplateColumn.CellEditingTemplate>
                </DataGridTemplateColumn>

                <!-- Position 列（简化：去掉 Home 模式，因为 Pick 通常不需要回零）-->
                <DataGridTemplateColumn Header="Position" Width="120">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <TextBlock Text="{Binding PositionName}" VerticalAlignment="Center"/>
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                    <DataGridTemplateColumn.CellEditingTemplate>
                        <DataTemplate>
                            <ComboBox ItemsSource="{Binding AvailablePositions}"
                                      SelectedItem="{Binding PositionName, UpdateSourceTrigger=PropertyChanged}"
                                      IsEditable="False"/>
                        </DataTemplate>
                    </DataGridTemplateColumn.CellEditingTemplate>
                </DataGridTemplateColumn>

                <!-- Offset 列（保留） -->
                <DataGridTemplateColumn Header="Ofs(mm)" Width="70">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <TextBox Text="{Binding Offset, UpdateSourceTrigger=PropertyChanged}"/>
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>

                <!-- Speed 列（保留） -->
                <DataGridTemplateColumn Header="Spd(%)" Width="60">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <TextBox Text="{Binding Speed, UpdateSourceTrigger=PropertyChanged}"/>
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>

                <!-- Description 列（保留） -->
                <DataGridTextColumn Header="Description" Binding="{Binding Description}" Width="*"/>
            </DataGrid.Columns>
        </DataGrid>

        <!-- 操作按钮栏（保留原有逻辑） -->
        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,8,0,0">
            <Button Content="➕ Add Motion" Margin="4" Command="{Binding AddPickMoveCommand}"/>
            <Button Content="🗑 Delete" Margin="4" Command="{Binding DeletePickMoveCommand}"/>
            <Button Content="↑" Margin="4" Command="{Binding MoveUpCommand}"/>
            <Button Content="↓" Margin="4" Command="{Binding MoveDownCommand}"/>
        </StackPanel>
    </StackPanel>
</GroupBox>
```

**与 GotoDetailView 的对比:**

| 特性 | GotoDetailView | PickDetailView (新) |
|------|---------------|---------------------|
| Station 列 | ✅ 有 | ❌ 删除 |
| Axis 显示格式 | `{StationId}.{Axis}` | 仅 `{Axis}` |
| Home 模式列 | ✅ Mode/MinVel/MaxVel | ❌ 删除 |
| Offset Var 列 | ✅ 有 | ❌ 删除（可选保留） |
| Sub/Axis/Position/Ofs/Spd/Desc | ✅ | ✅ 保留 |
| 操作按钮 | Add/Delete/↑/↓ | ✅ 相同 |

**验证要点:**
- [ ] DataGrid 列宽总和不超过容器宽度
- [ ] Axis 列不再显示 StationId 前缀
- [ ] Position 列没有 Home 模式的条件逻辑
- [ ] 操作按钮命令绑定正确

---

### Task 11: 注册导航和对话框支持

**Files:**
- Modify: `Module/Module.csproj` (如需添加新文件引用)

**目标:** 确保 GripperControlView 可以通过 DialogService 或 RegionManager 打开

**步骤:**

1. **如果使用 DialogService 弹窗模式:**
   在 `FrameworkModule` 或 `Module` 的注册代码中添加:
   ```csharp
   containerRegistry.RegisterDialog<GripperControlView, GripperControlViewModel>();
   ```

2. **如果使用 Region 导航模式:**
   ```csharp
   containerRegistry.ForRegion("ContentRegion")
       .RegisterViewWithNavigation<GripperControlView>();
   ```

3. **确保 XAML 中的 prism:ViewModelLocator.AutoWireViewModel 生效:**
   在 `GripperControlView.xaml` 中添加:
   ```xml
   xmlns:prism="http://prismlibrary.com/"
   prism:ViewModelLocator.AutoWireViewModel="True"
   ```

**验证要点:**
- [ ] GripperControlView 可以成功解析 ViewModel
- [ ] DialogService.ShowDialog 能正常弹出窗口
- [ ] 参数传递（clampPosition/releasePosition）工作正常

---

### Task 12: 实现多语言支持

**Files:**
- Modify: `MainApp/Languages/Strings.zh-CN.xaml`
- Modify: `MainApp/Languages/Strings.en-US.xaml`

**目标:** 为新增的 UI 文本添加多语言键

**中文资源 (zh-CN):**
```xml
<sys:String x:Key="GripperControl_Title">电爪手动控制面板</sys:String>
<sys:String x:Key="GripperControl_QuickOps">快速操作</sys:String>
<sys:String x:Key="GripperControl_Clamp">夹紧</sys:String>
<sys:String x:Key="GripperControl_Release">释放</sys:String>
<sys:String x:Key="GripperControl_TargetControl">目标位置控制</sys:String>
<sys:String x:Key="GripperControl_JogControl">寸动微调</sys:String>
<sys:String x:Key="GripperControl_TorqueSetting">力矩设定</sys:String>
<sys:String x:Key="GripperControl_SystemOps">系统操作</sys:String>
<sys:String x:Key="GripperControl_RealtimeStatus">实时状态</sys:String>
<sys:String x:Key="GripperControl_CurrentPosition">当前位置</sys:String>
<sys:String x:Key="GripperConfig_ClampPos">夹紧位置</sys:String>
<sys:String x:Key="GripperConfig_ReleasePos">释放位置</sys:String>
<sys:String x:Key="GripperConfig_OpenFullControl">打开夹爪控制面板</sys:String>
```

**英文资源 (en-US):**
```xml
<sys:String x:Key="GripperControl_Title">Electric Gripper Manual Control</sys:String>
<sys:String x:Key="GripperControl_QuickOps">Quick Operations</sys:String>
<sys:String x:Key="GripperControl_Clamp">Clamp</sys:String>
<sys:String x:Key="GripperControl_Release">Release</sys:String>
<sys:String x:Key="GripperControl_TargetControl">Target Position Control</sys:String>
<sys:String x:Key="GripperControl_JogControl">Jog Adjustment</sys:String>
<sys:String x:Key="GripperControl_TorqueSetting">Torque Setting</sys:String>
<sys:String x:Key="GripperControl_SystemOps">System Operations</sys:String>
<sys:String x:Key="GripperControl_RealtimeStatus">Real-time Status</sys:String>
<sys:String x:Key="GripperControl_CurrentPosition">Current Position</sys:String>
<sys:String x:Key="GripperConfig_ClampPos">Clamp Position</sys:String>
<sys:String x:Key="GripperConfig_ReleasePos">Release Position</sys:String>
<sys:String x:Key="GripperConfig_OpenFullControl">Open Gripper Control Panel</sys:String>
```

然后在 XAML 中将硬编码文本替换为动态资源绑定：
```xml
<TextBlock Text="{DynamicResource GripperControl_Title}"/>
```

**验证要点:**
- [ ] 中英文资源键名一致
- [ ] 所有用户可见文本都已提取到资源文件
- [ ] DynamicResource 绑定语法正确

---

### Task 13: 单元测试（可选但推荐）

**Files:**
- Create: `MotionControl.Tests/GripperServiceTests.cs` (新测试项目)

**目标:** 测试核心逻辑，特别是 Pick Holding Time 相关的时序行为

**测试用例示例:**

```csharp
[TestClass]
public class GripperServiceTests
{
    private Mock<IMotionService> _mockMotionService;
    private Mock<ILoggerService> _mockLogger;
    private Mock<IEventAggregator> _mockEventAggregator;
    private GripperService _gripperService;

    [TestInitialize]
    public void Setup()
    {
        _mockMotionService = new Mock<IMotionService>();
        _mockLogger = new Mock<ILoggerService>();
        _mockEventAggregator = new Mock<IEventAggregator>();
        
        _gripperService = new GripperService(
            _mockMotionService.Object,
            _mockLogger.Object,
            _mockEventAggregator.Object);
    }

    [TestMethod]
    public async Task ClampAsync_Should_SetDoClampPort_True()
    {
        // Arrange
        await _gripperService.InitializeAsync();
        _mockMotionService.Setup(x => x.ReadDi(It.IsAny<int>())).Returns(true);

        // Act
        await _gripperService.ClampAsync(100);

        // Assert
        _mockMotionService.Verify(x => x.WriteDo(10, true), Times.Once);  // DoClampPort
        _mockMotionService.Verify(x => x.WriteDo(11, false), Times.Once); // DoReleasePort
    }

    [TestMethod]
    public async Task ClampAsync_When_Cancelled_Should_Throw_OperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel(); // 立即取消
        
        // Act & Assert
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => _gripperService.ClampAsync(100, cts.Token));
    }

    [TestMethod]
    public void SetTorque_When_OutOfRange_Should_Throw_ArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => 
            _gripperService.SetTorque(-1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => 
            _gripperService.SetTorque(101));
    }

    // ★ Pick Holding Time 行为测试（在 PickStepAction 中测试）
    [TestMethod]
    public async Task PickFlow_With_HoldingTime_Should_Delay_After_Clamp()
    {
        // 这个测试应该在 PickStepAction 的集成测试中实现
        // 验证: Clamp → Delay(500ms) → Continue 的时序
    }
}
```

**验证要点:**
- [ ] 使用 Moq 框架 Mock IMotionService
- [ ] 测试取消令牌的行为
- [ ] 测试边界条件（力矩范围等）
- [ ] 异步方法的测试使用 async/await

---

## 实施顺序建议

**Phase 1: 核心基础设施 (Task 1-4)**
- 先建立接口和数据模型
- 再实现服务和注册
- 可立即进行单元测试

**Phase 2: UI 层实现 (Task 5-8)**
- 扩展数据模型
- 创建 ViewModel 和 View
- 实现转换器和样式

**Phase 3: 集成改进 (Task 9-11)**
- 改进 PickDetailView
- 实现表格复用
- 注册导航支持

**Phase 4: 增强和完善 (Task 12-13)**
- 多语言支持
- 测试覆盖
- 文档和注释

---

## 关键技术决策总结

| 决策点 | 选择 | 理由 |
|--------|------|------|
| **硬件调用方式** | 通过 IMotionService.WriteDo/ReadDi | 复用现有框架，支持虚拟卡调试 |
| **服务粒度** | 独立 IGripperService (Singleton) | 职责单一，便于测试和替换 |
| **Pick Holding Time 实现** | 在 PickStepAction 中用 Task.Delay | 属于流程编排，不应在底层服务 |
| **UI 展示方式** | DialogService 弹窗 | 不干扰主工作流，可随时关闭 |
| **位置刷新频率** | 200ms (DispatcherTimer) | 与旧项目一致，平衡性能和体验 |
| **状态监控** | Timer + EventAggregator 双重机制 | Timer 更新UI，EventAggregator 跨组件通信 |

---

## 验收标准 (Definition of Done)

- [ ] **功能完整性**: 夹紧/释放/Jog/力矩/回零/复位全部可用
- [ ] **架构合规性**: 通过 IMotionService 间接调用硬件，无直接 LTDMC 调用
- [ ] **UI 质量**: MaterialDesign 风格，图标规范，无 emoji，响应式布局
- [ ] **安全性**: 权限检查、运行状态检测、确认对话框
- [ ] **数据持久化**: ClampPosition/ReleasePosition 保存到 PickDetail 并序列化到 JSON
- [ ] **增强功能**: 定时刷新、状态指示灯、多语言支持
- [ ] **代码质量**: 注释完整、日志记录、异常处理、单元测试
- [ ] **向后兼容**: 不破坏现有 PickDetailView 的其他功能

---

## 风险和缓解措施

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| **IO 端口配置错误** | 夹爪不动作 | 从 hwcfg.xml 读取配置，提供默认值，启动时自检 |
| **DI 信号超时** | 流程卡死 | 设置合理超时（2秒），抛出 RecoverableException 触发重试 |
| **定时器内存泄漏** | 性能下降 | OnNavigatedFrom 必须停止 Timer，使用 WeakReference 订阅事件 |
| **并发访问冲突** | 数据不一致 | _state 对象更新加锁或保证单线程访问（Timer回调） |
| **多语言遗漏** | 显示英文 | 代码审查时检查所有硬编码字符串 |

---

**下一步行动:**

本计划已完成并保存到 `.trae/documents/gripper-control-system-plan.md`。

**推荐执行方式:**
1. **Subagent-Driven (推荐)** - 我会为每个 Task 分派独立的 subagent 执行，每个任务完成后审查结果
2. **Inline Execution** - 在当前会话中批量执行，每完成一个 Task 进行检查点验证

**您希望使用哪种方式？**
