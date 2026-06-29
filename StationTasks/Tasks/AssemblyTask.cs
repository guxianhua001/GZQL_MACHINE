using AlarmModule.Interfaces;
using Core.Utilities;
using MotionControl.Interfaces;
using Prism.Events;
using Recipe.Interfaces;
using StationTasks.Services;
using StationTasks.Params;
using Core.Abstraction;

namespace StationTasks.Tasks
{
    /// <summary>
    /// 组装工站任务（partial class，初始化动作见 AssemblyTask.Init.cs）
    /// </summary>
    public partial class AssemblyTask : RecipeStationBase<AssemblyStationParams>
    {
        private int AxisX => ResolveAxisId("X");
        private int AxisZ => ResolveAxisId("Z");
        private int AxisRy => ResolveAxisId("Ry");
        private int AxisEy => ResolveAxisId("Ey");
        private int AxisCy => ResolveAxisId("Cy");
        private readonly Random _rand = new Random();

        /// <summary> 本地化服务（用于初始化进度消息多语言支持） </summary>
        private readonly ILocalizationService _localizationService;

        public override string StationIdentifierValue => "AssemblyStation";

        public AssemblyTask(IMotionService motion, IPositionProvider recipePool,
            IStationInteractionService interaction, IEventAggregator ea, ILoggerService logger,
            IAlarmService alarmService,
            ISystemStateService systemState, IRecipeServiceFactory recipeServiceFactory,
            IRecipePoolService recipePoolService, IStationRegistry stationRegistry,
            ISpeedOverrideService speedOverride,
            ILocalizationService localizationService)
            : base(motion, recipePool, interaction, ea, logger, alarmService, systemState,
                  recipeServiceFactory, recipePoolService, stationRegistry, speedOverride,
                  3, localizationService?.GetResourceOrDefault("Station_AssemblyStation", "装配系统") ?? "装配系统", "AssemblyStation", localizationService) { _localizationService = localizationService; }

        protected override async Task ExecuteCycleAsync(CancellationToken token)
        {
            //await RunStep("通知装配站取料", async () =>
            //{
            //    await Task.Delay(_rand.Next(1000, 3000), token);
            //});

            //await RunStep("夹紧工件", async () =>
            //{
            //    await Task.Delay(_rand.Next(300, 800), token);
            //});

            //await RunStep("Y轴升高至安全位", async () =>
            //{
            //    await Task.Delay(_rand.Next(800, 1500), token);
            //});

            //await RunStep("移动到放料位置", async () =>
            //{
            //    await Task.Delay(_rand.Next(1000, 2000), token);
            //});

            //if (_rand.NextDouble() < 0.1)
            //{
            //    await RunStep("视觉定位检测", async () =>
            //    {
            //        await Task.Delay(500, token);
            //    });
            //}

            //await RunStep("通知装配站放料并等待", async () =>
            //{
            //    await Task.Delay(_rand.Next(1000, 3000), token);
            //});

            //await RunStep("松开夹爪", async () =>
            //{
            //    await Task.Delay(_rand.Next(300, 800), token);
            //});

            //await RunStep("返回待机位置", async () =>
            //{
            //    await Task.Delay(_rand.Next(1000, 2000), token);
            //});

            //Logger.Info("=== 装配循环完成，稍后重启 ===");
            //await Task.Delay(500, token);
        }

    }
}
