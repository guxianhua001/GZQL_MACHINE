using Core.Utilities;
using DryIoc;
using MotionControl.Interfaces;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using StationTasks.Actions;
using StationTasks.Services;
using StationTasks.Tasks;
using Core.Abstraction;
using Recipe.Interfaces;
using System.Linq;

namespace StationTasks
{
    public class StationTasksModule : IModule
    {
        private ILoggerService _logger;

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            var container = containerRegistry.GetContainer();

            container.RegisterMany(
                new[] { typeof(LoadingTask), typeof(DispensingTask), typeof(AssemblyTask) },
                Reuse.Singleton,
                serviceTypeCondition: serviceType =>
                    serviceType == typeof(ITask) ||
                    serviceType == typeof(IStationParameterProvider) ||
                    serviceType == typeof(IBatchSwitchable) ||
                    serviceType == typeof(Core.Abstraction.IDispensingZScanOperations) ||
                    serviceType == typeof(LoadingTask) ||
                    serviceType == typeof(DispensingTask) ||
                    serviceType == typeof(AssemblyTask)
            );

            // 注册整机初始化服务（协调三工站初始化时序）
            containerRegistry.RegisterSingleton<IMachineInitializationService, MachineInitializationService>();

            // 注册步骤动作实现
            container.RegisterMany(
                new[] { typeof(GotoStepAction), typeof(VisionStepAction), typeof(Scan3DStepAction), typeof(SeekStepAction), typeof(WaitStepAction), typeof(ScriptStepAction), typeof(DashboardStepAction), typeof(BranchStepAction), typeof(PickStepAction), typeof(CureStepAction), typeof(DispenseStepAction), typeof(ReleaseStepAction) },
                Reuse.Singleton,
                serviceTypeCondition: serviceType => serviceType == typeof(IProcessStepAction)
            );

            // 注册视觉数据解析器
            containerRegistry.RegisterSingleton<IVisionDataParser, DefaultVisionDataParser>();
            containerRegistry.RegisterSingleton<ScriptVisionDataParser>();
            containerRegistry.RegisterSingleton<Camera3DDataParser>();

            containerRegistry.RegisterSingleton<IStationInteractionService, StationInteractionService>();
            containerRegistry.RegisterSingleton<IPositionProvider, RecipePositionProvider>();
            containerRegistry.RegisterSingleton<IRecipeServiceFactory, RecipeServiceFactory>();
            containerRegistry.RegisterSingleton<VisionCaptureService>();
            containerRegistry.RegisterSingleton<BezierArcDispenseService>();
        }

        /// <summary>
        /// 模块初始化时强制解析所有ITask单例，触发构造函数执行，
        /// 使工站任务自注册到IStationRegistry中
        /// </summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
            _logger = containerProvider.Resolve<ILoggerService>();

            var tasks = containerProvider.Resolve<IEnumerable<ITask>>().ToList();
            _logger.Info($"[StationTasksModule] 已解析 {tasks.Count} 个工站任务并完成自注册");
        }
    }
}
