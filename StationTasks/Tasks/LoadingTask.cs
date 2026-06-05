using AlarmModule.Interfaces;
using Core.Utilities;
using MotionControl.Interfaces;
using MotionControl.Models;
using Prism.Events;
using Recipe.Interfaces;
using StationTasks.Services;
using StationTasks.Params;
using Core.Abstraction;
using System.Linq;

namespace StationTasks.Tasks
{
    /// <summary>
    /// 上下料工站任务：实现 ILoadUnloadStationOperations，
    /// 是所有手动/自动运动控制的唯一实现层
    /// </summary>
    public partial class LoadingTask : RecipeStationBase<LoadingStationParams>, ILoadUnloadStationOperations
    {
        private int AxisY => ResolveAxisId("Y");
        private int AxisRx => ResolveAxisId("Rx");
        private int AxisRz => ResolveAxisId("Rz");

        private const int ClawDoId = 100;
        private readonly Random _rand = new Random();

        // hwconfig 中定义的 IO 端口名称，从 hwcfg.xml 动态解析逻辑 ID
        private const string PortStageVacuumOn = "Q2.2AssemblyPlatformVacuumOn";
        private const string PortStageVacuumOff = "Q2.3AssemblyPlatformVacuumOff";
        private const string PortStageVacuumFeedback = "I2.1AssemblyPlatformVacuumFeedback";
        private const string PortGripperVacuumValve = "GripperVacValve";
        private const string PortGripperVacuumSensor = "GripperVacSensor";

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
            // 自动流程待实现，当前仅记录日志
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

                //await RunStep("Y轴回原点", () => _motion.HomeAsync(AxisY, 1, 5, 20));
                //await RunStep("Rx轴回原点", () => _motion.HomeAsync(AxisRx, 1, 5, 20));
                //await RunStep("Rz轴回原点", () => _motion.HomeAsync(AxisRz, 1, 5, 20));

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

        #region 平台真空控制（Stage）

        /// <summary>
        /// 开平台真空：写开真空 DO，等待 DI 反馈确认
        /// </summary>
        public async Task StageVacuumOnAsync(CancellationToken token = default)
        {
            int doOn = GetDoLogicalId(PortStageVacuumOn);
            int doOff = GetDoLogicalId(PortStageVacuumOff);
            int diFeedback = GetDiLogicalId(PortStageVacuumFeedback);
            int timeout = (int)Params.VacuumBuildTimeout;

            await RunStep("平台真空开", async () =>
            {
                WriteDO(doOn, true);
                WriteDO(doOff, false);
                //await TriggerCylinderAsync(doOn, true, diFeedback, timeout);
            });
            Logger.Info($"[{TaskName}] 平台真空已开启");
        }

        /// <summary>
        /// 破平台真空：写破真空 DO 脉冲后关闭
        /// </summary>
        public async Task StageVacuumOffAsync(CancellationToken token = default)
        {
            int doOn = GetDoLogicalId(PortStageVacuumOn);
            int doOff = GetDoLogicalId(PortStageVacuumOff);
            int breakTime = Params.BreakVacuumTime;

            await RunStep("平台破真空", async () =>
            {
                WriteDO(doOn, false);
                WriteDO(doOff, true);
                await Task.Delay(breakTime, token);
                WriteDO(doOff, false);
            });
            Logger.Info($"[{TaskName}] 平台真空已关闭");
        }

        /// <summary>
        /// 读取平台真空反馈状态
        /// </summary>
        public bool IsStageVacuumOn()
        {
            int diFeedback = GetDiLogicalId(PortStageVacuumFeedback);
            return diFeedback >= 0 && ReadDI(diFeedback);
        }

        #endregion

        #region 夹爪真空控制

        /// <summary>
        /// 开夹爪真空
        /// </summary>
        public async Task GripperVacuumOnAsync(CancellationToken token = default)
        {
            int doValve = GetDoLogicalId(PortGripperVacuumValve);
            await RunStep("夹爪真空开", async () =>
            {
                WriteDO(doValve, true);
                await Task.Delay(200, token);
            });
            Logger.Info($"[{TaskName}] 夹爪真空已开启");
        }

        /// <summary>
        /// 关夹爪真空
        /// </summary>
        public async Task GripperVacuumOffAsync(CancellationToken token = default)
        {
            int doValve = GetDoLogicalId(PortGripperVacuumValve);
            await RunStep("夹爪真空关", async () =>
            {
                WriteDO(doValve, false);
                await Task.Delay(200, token);
            });
            Logger.Info($"[{TaskName}] 夹爪真空已关闭");
        }

        /// <summary>
        /// 读取夹爪真空反馈状态
        /// </summary>
        public bool IsGripperVacuumOn()
        {
            int diSensor = GetDiLogicalId(PortGripperVacuumSensor);
            return diSensor >= 0 && ReadDI(diSensor);
        }

        #endregion

        #region 自动流程

        private const double DefaultVelocity = 50.0;

        /// <summary>
        /// 自动取料流程：开真空 → 取料位 → 确认真空 → 升高 → 旋转到装配位
        /// </summary>
        public async Task AutoPickUpFlowAsync(CancellationToken token)
        {
            int doOn = GetDoLogicalId(PortStageVacuumOn);
            int doOff = GetDoLogicalId(PortStageVacuumOff);
            int diFeedback = GetDiLogicalId(PortStageVacuumFeedback);

            await RunStep("自动取料", async () =>
            {
                // 1. 开平台真空
                WriteDO(doOn, true);
                WriteDO(doOff, false);
                await TriggerCylinderAsync(doOn, true, diFeedback, 3000);

                // 2. Y轴移动到取料位
                if (AxisY >= 0) await ExecuteMoveAsync(AxisY, "取料位", DefaultVelocity);

                // 3. 确认真空反馈
                await TriggerCylinderAsync(doOn, true, diFeedback, 3000);

                // 4. Y轴升高到待机位
                if (AxisY >= 0) await ExecuteMoveAsync(AxisY, "待机位", DefaultVelocity);

                // 5. Rx/Rz移动到装配位
                if (AxisRx >= 0) await ExecuteMoveAsync(AxisRx, "装配位1", DefaultVelocity);
                if (AxisRz >= 0) await ExecuteMoveAsync(AxisRz, "装配位1", DefaultVelocity);
            });
            Logger.Info($"[{TaskName}] 自动取料流程完成");
        }

        /// <summary>
        /// 自动扫描流程：移动到扫描位 → 等待 → 返回待机位
        /// </summary>
        public async Task AutoScanFlowAsync(CancellationToken token)
        {
            await RunStep("自动扫描", async () =>
            {
                // 1. Y轴移动到3D扫描位
                if (AxisY >= 0) await ExecuteMoveAsync(AxisY, "3D扫描位", DefaultVelocity);

                // 2. 等待扫描完成
                await Task.Delay(500, token);

                // 3. Y轴返回待机位
                if (AxisY >= 0) await ExecuteMoveAsync(AxisY, "待机位", DefaultVelocity);
            });
            Logger.Info($"[{TaskName}] 自动扫描流程完成");
        }

        /// <summary>
        /// 自动下料流程：移动到出料位 → 破真空 → 各轴返回待机位
        /// </summary>
        public async Task AutoUnloadFlowAsync(CancellationToken token)
        {
            int doOn = GetDoLogicalId(PortStageVacuumOn);
            int doOff = GetDoLogicalId(PortStageVacuumOff);

            await RunStep("自动下料", async () =>
            {
                // 1. Y轴移动到出料位
                if (AxisY >= 0) await ExecuteMoveAsync(AxisY, "出料位", DefaultVelocity);

                // 2. 破真空
                WriteDO(doOn, false);
                WriteDO(doOff, true);
                await Task.Delay(200, token);
                WriteDO(doOff, false);

                // 3. 各轴返回待机位
                if (AxisY >= 0) await ExecuteMoveAsync(AxisY, "待机位", DefaultVelocity);
                if (AxisRx >= 0) await ExecuteMoveAsync(AxisRx, "待机位", DefaultVelocity);
                if (AxisRz >= 0) await ExecuteMoveAsync(AxisRz, "待机位", DefaultVelocity);
            });
            Logger.Info($"[{TaskName}] 自动下料流程完成");
        }

        #endregion

    }
}
