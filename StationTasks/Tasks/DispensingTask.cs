using AlarmModule.Interfaces;
using Core.Utilities;
using MotionControl.Interfaces;
using MotionControl.Services;
using Prism.Events;
using Recipe.Interfaces;
using StationTasks.Services;
using StationTasks.Params;
using Core.Abstraction;

namespace StationTasks.Tasks
{
    /// <summary>
    /// 点胶工站任务（partial class）
    /// Z-Scan 3D扫描操作见 DispensingTask.ZScan.cs
    /// </summary>
    public partial class DispensingTask : RecipeStationBase<DispenserStationParams>
    {
        /// <summary> 轴参数服务（用于插补系查找等） </summary>
        private readonly IAxisParameterService _axisParameterService;
        /// <summary> 全局速度比例服务（基类 _speedOverride 为 private，此处额外存储） </summary>
        private readonly ISpeedOverrideService _speedOverrideLocal;
        /// <summary> 本地化服务（用于初始化进度消息多语言支持） </summary>
        private readonly ILocalizationService _localizationService;
        /// <summary> Dx轴 — Dispenser gantry X (逻辑ID从hwcfg.xml动态获取) </summary>
        private int AxisDx => ResolveAxisId("Dx");
        /// <summary> Dy轴 — Dispenser gantry Y </summary>
        private int AxisDy => ResolveAxisId("Dy");
        /// <summary> Dz₁轴 — Dispenser head 1 Z </summary>
        private int AxisDz1 => ResolveAxisId("Dz₁");
        /// <summary> Dz₂轴 — Dispenser head 2 Z </summary>
        private int AxisDz2 => ResolveAxisId("Dz₂");
        /// <summary> Dz3轴 — Dispenser head 3 Z </summary>
        private int AxisDz3 => ResolveAxisId("Dz₃");
        private readonly Random _rand = new Random();

        public override string StationIdentifierValue => "DispenserStation";

        public DispensingTask(IMotionService motion, IPositionProvider recipePool,
            IStationInteractionService interaction, IEventAggregator ea, ILoggerService logger,
            IAlarmService alarmService,
            ISystemStateService systemState, IRecipeServiceFactory recipeServiceFactory,
            IRecipePoolService recipePoolService, IStationRegistry stationRegistry,
            ISpeedOverrideService speedOverride,
            ILocalizationService localizationService,
            IAxisParameterService axisParameterService)
            : base(motion, recipePool, interaction, ea, logger, alarmService, systemState,
                  recipeServiceFactory, recipePoolService, stationRegistry, speedOverride,
                  2, localizationService?.GetResourceOrDefault("Station_DispenserStation", "点胶系统") ?? "点胶系统", "DispenserStation")
        {
            _axisParameterService = axisParameterService;
            _speedOverrideLocal = speedOverride;
            _localizationService = localizationService;
        }

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

        private async Task MoveToPosition(string posName)
        {
            var x = await GetPositionAsync(posName, "Dx");
            var y = await GetPositionAsync(posName, "Dy");
            await _motion.MoveLineAbsAsync(0, new[] { AxisDx, AxisDy }, new[] { x, y }, 40);
            // 点胶工位默认使用针头1(Dz₂轴)进行位置移动；Dz₁为相机/3D扫描轴，不作为点胶轴
            // Dz₂ 为可选轴：位置存在则移动，不存在则跳过（用 TryGetPositionAsync 避免抛异常）
            var (hasZ2, z2) = await TryGetPositionAsync(posName, "Dz₂");
            if (hasZ2) await _motion.MoveAbsAsync(AxisDz2, z2, 20);
        }
    }
}
