using Core.Utilities;
using MotionControl.Card;
using MotionControl.Dialogs;
using MotionControl.Interfaces;
using MotionControl.Services;
using MotionControl.ViewModels;
using MotionControl.Views;
using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;

namespace MotionControl
{
    public class MotionControlModule : IModule
    {
        private readonly IRegionManager _regionManager;

        public MotionControlModule(IRegionManager regionManager)
        {
            _regionManager = regionManager;
        }
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var logger = containerProvider.Resolve<ILoggerService>();
            try
            {
                // 1. 初始化运动服务
                var motionService = containerProvider.Resolve<IMotionService>();
                // ConfigureAwait(false)：OnInitialized 在 UI 线程同步等待，避免 InitializeAsync 内 await 续延切回 UI 造成死锁
                motionService.InitializeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                // 默认慢轮询(100ms)；轴操作面板打开时 MotionService 自动切到 10ms
                motionService.StartPolling();

                // 2. 初始化夹爪服务
                var gripperService = containerProvider.Resolve<IGripperService>();
                gripperService.InitializeAsync().ConfigureAwait(false).GetAwaiter().GetResult();

                // 3. 启动系统状态监控
                var stateService = containerProvider.Resolve<ISystemStateService>();

                // 4. 加载安全互锁配置（不依赖维护界面是否打开）
                var safetyMonitor = containerProvider.Resolve<ISafetyZoneMonitor>();
                var safetyConfigLoader = containerProvider.Resolve<ISafetyZoneConfigLoader>();
                safetyMonitor.UpdateConfig(safetyConfigLoader.Load());

                logger.Info("Motion system, gripper service, and state monitoring started successfully.");
            }
            catch (Exception ex)
            {
                logger.Error($"Motion system initialization failed: {ex.Message}");
                // 运动功能不可用，但软件继续运行
            }
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 注册工厂
            containerRegistry.RegisterSingleton<IMotionCardFactory, MotionCardFactory>();
            containerRegistry.RegisterSingleton<IAxisOperationPanelState, AxisOperationPanelState>();
            containerRegistry.RegisterSingleton<IMotionService, MotionService>();
            // 配置解析
            containerRegistry.RegisterSingleton<IHardwareConfigLoader, HardwareConfigParser>();
            containerRegistry.RegisterSingleton<ISystemStateService, SystemStateService>();
            containerRegistry.RegisterSingleton<ISpeedOverrideService, SpeedOverrideService>();
            containerRegistry.RegisterForNavigation<StationStateView, StationStateViewModel>();
            containerRegistry.RegisterForNavigation<TaskMonitorView, TaskMonitorViewModel>();
            containerRegistry.RegisterForNavigation<RecoverableFaultDialogView, RecoverableFaultDialogViewModel>();
            containerRegistry.RegisterForNavigation<SpeedControlView, SpeedControlViewModel>();
            containerRegistry.RegisterForNavigation<IODisplayView, IODisplayViewModel>();
            // 轴控制面板
            containerRegistry.RegisterForNavigation<AxisControlPanelView, AxisControlPanelViewModel>();
            // 注册任务管理器
            containerRegistry.RegisterSingleton<ITaskManager, StationTaskManager>();
            containerRegistry.RegisterSingleton<Core.Abstraction.IADValueConverter, Core.Services.UniversalADValueConverter>();
            
            // ★ 注册夹爪服务
            containerRegistry.RegisterSingleton<IGripperService, GripperService>();

            // ★ 轴参数设置（从 ModuleCore 迁移）
            containerRegistry.RegisterSingleton<IAxisParameterService, AxisParameterService>();
            containerRegistry.RegisterForNavigation<AxisSettingView, AxisSettingViewModel>();
            containerRegistry.RegisterDialog<ParameterProgressDialog>();

            // ★ 位置编辑器运动控制
            containerRegistry.RegisterSingleton<Core.Abstraction.IPositionMotionController, PositionMotionControllerImpl>();

            // ★ 安全区域监控服务（运动互锁，配置驱动）
            containerRegistry.RegisterSingleton<ISafetyZoneConfigLoader, SafetyZoneConfigLoader>();
            containerRegistry.RegisterSingleton<ISafetyZoneMonitor, SafetyZoneMonitor>();
        }

    }
}