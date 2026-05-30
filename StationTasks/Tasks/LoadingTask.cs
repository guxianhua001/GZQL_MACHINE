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
    public partial class LoadingTask : RecipeStationBase<LoadingStationParams>
    {
        private int AxisY => ResolveAxisId("Y");
        private int AxisRx => ResolveAxisId("Rx");
        private int AxisRz => ResolveAxisId("Rz");

        private const int ClawDoId = 100;
        private readonly Random _rand = new Random();

        public override string StationIdentifierValue => "LoadingStation";

        public LoadingTask(IMotionService motion, IPositionProvider positionProvider,
            IStationInteractionService interaction, IEventAggregator ea, ILoggerService logger,
            IAlarmService alarmService,
            ISystemStateService systemState, IRecipeServiceFactory recipeServiceFactory,
            IRecipePoolService recipePoolService, IStationRegistry stationRegistry,
            ISpeedOverrideService speedOverride,
            ILocalizationService localizationService)
            : base(motion, positionProvider, interaction, ea, logger, alarmService, systemState,
                  recipeServiceFactory, recipePoolService, stationRegistry, speedOverride,
                  1, localizationService?.GetResourceOrDefault("Station_LoadingStation", "上下料系统") ?? "上下料系统", "LoadingStation") { _logger = logger; }

        protected override async Task ExecuteCycleAsync(CancellationToken token)
        {
            await RunStep("通知装配站取料", async () =>
            {
                await Task.Delay(_rand.Next(1000, 3000), token);
            });

            await RunStep("夹紧工件", async () =>
            {
                await Task.Delay(_rand.Next(300, 800), token);
            });

            await RunStep("Y轴升高至安全位", async () =>
            {
                await Task.Delay(_rand.Next(800, 1500), token);
            });

            await RunStep("移动到放料位置", async () =>
            {
                await Task.Delay(_rand.Next(1000, 2000), token);
            });

            if (_rand.NextDouble() < 0.1)
            {
                await RunStep("视觉定位检测", async () =>
                {
                    await Task.Delay(500, token);
                });
            }

            await RunStep("通知装配站放料并等待", async () =>
            {
                await Task.Delay(_rand.Next(1000, 3000), token);
            });

            await RunStep("松开夹爪", async () =>
            {
                await Task.Delay(_rand.Next(300, 800), token);
            });

            await RunStep("返回待机位置", async () =>
            {
                await Task.Delay(_rand.Next(1000, 2000), token);
            });

            Logger.Info("=== 上下料循环完成，稍后重启 ===");
            await Task.Delay(500, token);
        }

        public override async Task HomeAsync()
        {
            State = TaskState.Homing;
            Logger.Info($"[{TaskName}] 开始初始化...");
            PublishTaskStatusChanged("初始化中", State);

            try
            {
                await RunStep("预加载位置数据", PreloadPositionsAsync);

                await RunStep("Y轴回原点", () => _motion.HomeAsync(AxisY, 1, 5, 20));
                await RunStep("Rx轴回原点", () => _motion.HomeAsync(AxisRx, 1, 5, 20));
                await RunStep("Rz轴回原点", () => _motion.HomeAsync(AxisRz, 1, 5, 20));

                State = TaskState.Idle;
                Logger.Info($"[{TaskName}] 初始化完成。");
                PublishTaskStatusChanged("待机", State);
            }
            catch
            {
                State = TaskState.Error;
                Logger.Error($"[{TaskName}] 初始化失败！");
                throw;
            }
        }

    }
}
