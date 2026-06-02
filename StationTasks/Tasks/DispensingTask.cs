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
    public class DispensingTask : RecipeStationBase<DispenserStationParams>
    {
        /// <summary> Dx轴 — Dispenser gantry X (逻辑ID从hwcfg.xml动态获取) </summary>
        private int AxisDx => ResolveAxisId("Dx");
        /// <summary> Dy轴 — Dispenser gantry Y </summary>
        private int AxisDy => ResolveAxisId("Dy");
        /// <summary> Dz₁轴 — Dispenser head 1 Z </summary>
        private int AxisDz1 => ResolveAxisId("Dz₁");
        /// <summary> Dz₂轴 — Dispenser head 2 Z </summary>
        private int AxisDz2 => ResolveAxisId("Dz₂");
        /// <summary> Dz3轴 — Dispenser head 3 Z </summary>
        private int AxisDz3 => ResolveAxisId("Dz3");
        private readonly Random _rand = new Random();

        public override string StationIdentifierValue => "DispenserStation";

        public DispensingTask(IMotionService motion, IPositionProvider recipePool,
            IStationInteractionService interaction, IEventAggregator ea, ILoggerService logger,
            IAlarmService alarmService,
            ISystemStateService systemState, IRecipeServiceFactory recipeServiceFactory,
            IRecipePoolService recipePoolService, IStationRegistry stationRegistry,
            ISpeedOverrideService speedOverride,
            ILocalizationService localizationService)
            : base(motion, recipePool, interaction, ea, logger, alarmService, systemState,
                  recipeServiceFactory, recipePoolService, stationRegistry, speedOverride,
                  2, localizationService?.GetResourceOrDefault("Station_DispenserStation", "点胶系统") ?? "点胶系统", "DispenserStation") { }

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

            //Logger.Info("=== 点胶循环完成，稍后重启 ===");
            //await Task.Delay(500, token);
        }

        public override async Task HomeAsync()
        {
            State = TaskState.Homing;
            Logger.Info($"[{TaskName}] 开始点胶系统初始化...");
            PublishTaskStatusChanged("初始化中", State);

            try
            {
                await RunStep("预加载位置数据", PreloadPositionsAsync);

                await Task.Delay(1800);
                await Task.Delay(1600);
                await Task.Delay(1400);

                State = TaskState.Idle;
                Logger.Info($"[{TaskName}] 初始化完成，进入待机。");
                PublishTaskStatusChanged("待机", State);
            }
            catch
            {
                State = TaskState.Error;
                Logger.Error($"[{TaskName}] 初始化失败！");
                throw;
            }
        }

        private async Task MoveToPosition(string posName)
        {
            var x = await GetPositionAsync(posName, "Dx");
            var y = await GetPositionAsync(posName, "Dy");
            await _motion.MoveLineAbsAsync(0, new[] { AxisDx, AxisDy }, new[] { x, y }, 40);
            var z1 = await GetPositionAsync(posName, "Dz₁");
            if (!double.IsNaN(z1)) await _motion.MoveAbsAsync(AxisDz1, z1, 20);
        }
    }
}
