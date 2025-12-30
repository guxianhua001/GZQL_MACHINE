using Core.Abstraction;
using Core.Abstractions.IConfiguration;
using Core.Utilities;
using NLog;
using Prism.Commands;
using Prism.Events;
using Prism.Ioc;
using Prism.Services.Dialogs;
using Recipe;
using Recipe.Interfaces;
using SmarterMotion;
using Stations.TaskParameters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Unity;
using static Stations.RegisterTask;

namespace Stations
{
    // 上料工站任务
    [TaskId(1)]
    public partial class LoadingStation : XTaskBase<LoadingStationParams> , ITask, IDeviceManager, IParameterEditable
    {
        private readonly RecipeService<LoadingStationParams> _recipeService;
        private LoadingStationParams _internalParameters = new LoadingStationParams();
        // 实现 IParameterEditable 接口
        public string EditTitle => $"{Name} - 参数编辑";
        public object Parameters => _recipeService.Parameters;
        public string Identifier => "LoadingStation";

        private readonly ILoggerService _logger;
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IContainerExtension _container;
        private readonly IParameterEditor _parameterEditor;
        private readonly IParameterStore _parameterStore;
        private readonly IParameterStorage _parameterStorage;
        private readonly IAxisConfigService _axisConfigService;
        private RecipePoolManager _recipePoolManager;
        public ICommand EditParametersCommand => _recipeService.EditParametersCommand;
        public ICommand SwitchRecipeCommand => _recipeService.SwitchRecipeCommand;
        // 配方相关属性
        public string CurrentRecipeName => _recipeService?.CurrentRecipeName ?? "Default";
        public List<string> AvailableRecipes => _recipeService?.AvailableRecipes ?? new List<string>();
        public bool IsParametersVisible { get; set; } = true; // 控制参数在Overview中的可见性

        public IAxis PlatY;     // 轴10 装配平台Y轴
        public IAxis PlatU;     // 轴11 装配平台U轴
        public IAxis PlatR;     // 轴12 装配平台R轴

        private XDo PlatVacValve;       // 装配平台吸真空电磁阀
        private XDo PlatBreakVacValve;  // 装配平台破真空电磁阀
        private XDo UvLamp1;             // UV灯控制
        private XDo UvLamp2;             // UV灯控制
        private XDi PlatVacSensor;      // 装配平台真空传感器 
        private XDi AssemblyReadySignal; // 装配就绪信号
        private XDi AssemblyCompleteSignal; // 装配完成信号

        // 强类型参数属性
        public LoadingStationParams TypedParameters => ParametersBase as LoadingStationParams;

        /// <summary>
        /// 重写获取当前配方名称的方法
        /// </summary>
        protected override string GetCurrentRecipeName()
        {
            return _recipeService?.CurrentRecipeName ?? "DefaultRecipe";
        }

        public LoadingStation(
            int taskId,
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            IContainerExtension container,
            IParameterEditor parameterEditor,
            IParameterStorage parameterStorage,
            IAxisConfigService axisConfigService,
            IRecipeManager recipeManager,
            IRecipeStorage recipeStorage,
            ILoggerService logger,
            IAppConfig appConfig,
            RecipePoolManager recipePoolManager)
            : base(taskId, "Loading Station", eventAggregator)
        {
            _logger = logger;
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _container = container;
            _parameterEditor = parameterEditor;
            _parameterStorage = parameterStorage;
            _axisConfigService = axisConfigService;
            _recipePoolManager = recipePoolManager;
            // 初始化配方服务
            _recipeService = new RecipeService<LoadingStationParams>(
                stationIdentifier: "LoadingStation",
                stationName: "Loading Station",
                loggerService: logger,
                dialogService: dialogService,
                eventAggregator: eventAggregator,
                parameterEditor: parameterEditor,
                parameterStorage: parameterStorage,
                recipeManager: recipeManager,
                recipeStorage: recipeStorage,
                appConfig: appConfig,
                recipePoolManager: recipePoolManager);

            // 订阅配方服务事件
            SubscribeToRecipeEvents();
        }

        #region 配方相关方法
        private void SubscribeToRecipeEvents()
        {
            // 订阅参数应用事件
            _recipeService.ParametersApplied += OnRecipeParametersApplied;

            // 订阅配方改变事件
            _recipeService.RecipeChanged += OnRecipeChanged;

            // 订阅参数加载事件
            _recipeService.ParametersLoaded += OnParametersLoaded;
        }
        private void OnRecipeParametersApplied(object sender, LoadingStationParams parameters)
        {
            try
            {
                _logger.Info($"[{Name}] 配方参数已应用: {CurrentRecipeName}");

                // 应用参数到硬件
                //ApplyParametersToHardware();
            }
            catch (Exception ex)
            {
                _logger.Error($"[{Name}] 应用配方参数失败: {ex.Message}");
            }
        }

        private void OnRecipeChanged(object sender, string newRecipeName)
        {
            try
            {
                _logger.Info($"[{Name}] 配方已切换: {newRecipeName}");

                // 执行配方切换后的逻辑
                OnRecipeSwitched(newRecipeName);
            }
            catch (Exception ex)
            {
                _logger.Error($"[{Name}] 处理配方切换事件失败: {ex.Message}");
            }
        }

        private void OnParametersLoaded(object sender, LoadingStationParams parameters)
        {
            try
            {
                _logger.Info($"[{Name}] 配方参数已加载: {CurrentRecipeName}");

                // 参数已自动加载到 _recipeService.Parameters
                // 这里可以执行额外的初始化逻辑
            }
            catch (Exception ex)
            {
                _logger.Error($"[{Name}] 处理参数加载事件失败: {ex.Message}");
            }
        }

        private void OnRecipeSwitched(string newRecipeName)
        {
            // 配方切换后的自定义逻辑
            // 例如：重置状态、更新硬件配置等

            _logger.Info($"[{Name}] 执行配方切换后逻辑: {newRecipeName}");
        }
        #endregion  
        public override void SetTaskId(int taskId)
        {
            // 允许后续设置任务ID
            TaskId = taskId;
            Name = $"Loading Station #{taskId}";
        }
        public void OnEditParameters()
        {
            _recipeService.OnEditParameters();// 也可以使用回调
        }
        /// <summary>
        /// 参数保存回调方法
        /// </summary>
        private void OnParametersSaved(TaskParametersBase savedParameters)
        {
            try
            {
                if (savedParameters is LoadingStationParams loadingParams)
                {
                    // 保存loadingParams参数到文件

                    Console.WriteLine($"参数已保存并应用: {Identifier}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"参数保存回调处理失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public override void Initialize()
        {
            //EventAggregator?.GetEvent<SomeEvent>().Publish(...);

        }
        // 实现 IDeviceManager 接口
        public void RegisterDevice()
        {
            // 注册设备信息，例如轴、DI/DO等
            PlatY = XDevice.Instance.FindAxisById(9);
            PlatU = XDevice.Instance.FindAxisById(10);
            PlatR = XDevice.Instance.FindAxisById(11);
            // 注册IO设备
            PlatVacValve = XDevice.Instance.FindDoById(19);
            PlatBreakVacValve = XDevice.Instance.FindDoById(20);
            UvLamp1 = XDevice.Instance.FindDoById(26);
            UvLamp2 = XDevice.Instance.FindDoById(27);
        }
        /// <summary>
        /// 任务复位
        /// </summary>
        protected override void Homing(CancellationToken cancellation)
        {
            base.Homing(cancellation);
            ExecuteHoming();
        }
        // 上料状态枚举
        // 上料状态枚举
        public enum LoadingState
        {
            Initialize,                 // 0. 初始化上料流程
            CheckMaterial,              // 1. 开载具吸真空，判断物料是否到位
            MoveTo3DScan,               // 2. 若到位，移到3D扫描位
            Notify3DScanStart,
            Perform3DScan,              // 3. 执行3D扫描，等待扫描完成
            MoveToPhotoPosition,        // 4. 到取料拍照位，通知装配站移到1号取料位拍照
            WaitForTopPhotoComplete,    // 5. 等待取料拍照完成信号
            MoveToPickPosition,         // 6. 移到取料位
            RotateToAssemblyPosition,   // 7. 若装配站取料完成，旋转到装配位
            WaitForPickupComplete,
            WaitAssemblySignal,         // 7. 等待装配站取料完成信号（获取XY的offset）
            MoveToTabPhoto,             // 8. 移到tab1_1拍照位（共6组）
            WaitTabPhotoComplete,       // 9. 等待拍照完成信号
            MoveToPillar1Photo,         // 10. 移到pillar1_1拍照位（共6组）
            WaitPillar1PhotoComplete,   // 11. 等待拍照完成信号
            MoveToPillar2Photo,         // 12. 移到pillar1_2拍照位（共6组）
            WaitAssemblyPhotoComplete,   // 13. 等待拍照完成信号
            PostAssembly,         // 14. 移到装配位等待位置
            NotifyMaterialrReady,       // 15. 通知装配站物料到位，准备取料
            WaitAssemblyComplete,       // 15. 等待装配站装配准备好信号
            MoveToAssembly,             // 16. 移到装配1号位置
            MoveBackFromAssembly,       // 17. 退回到装配位等待位置
            CheckNextAssembly,          // 18. 检查下一个装配位置
            RotateToNextAssemblyPosition,    
            StartDispensing,            // 19. 开始点胶流程
            MoveToDispensingStart,      // 20. 移到点胶起始位
            PerformDispensing,          // 21. 执行点胶
            CheckNextDispensing,        // 22. 检查下一个点胶位置
            MoveToUvStation,            // 23. 移动到UV工位
            StartUvCuring,              // 24. 开始UV固化
            StopUvCuring,               // 25. 停止UV固化
            MoveToStandby,              // 26. 移动到待机位
            Complete,                   // 27. 流程完成
            Error
        }
        // 流程控制属性
        private bool _isLoadingInProgress = false;
        private CancellationTokenSource _loadingCTS;
        private LoadingState _currentLoadingState = LoadingState.Initialize;
        private int _currentAssemblyPosition = 1;
        private int _currentDispensingPosition = 1;
        public bool IsRunning => _isLoadingInProgress;
        private int _currentPhotoGroup = 0;
        private int _totalPhotoGroups = 0;
        private const int totalAssemblyPositions = 6;

        protected override void Running(object runMode)
        {
            // 具体任务逻辑...
            try
            {
                // 根据运行模式执行不同的流程
                if (runMode is string mode)
                {
                    switch (mode)
                    {
                        case "Auto":
                            // 自动运行组装流程
                            ExecuteLoadingProcess().Wait();
                            break;
                        case "SingleStep":
                            // 单步模式运行
                            IsSingleStepMode = true;
                            ExecuteLoadingProcess().Wait();
                            break;
                        default:
                            _logger.Warn($"未知的运行模式: {mode}");
                            break;
                    }
                }
                else
                {
                    // 默认自动运行
                    ExecuteLoadingProcess().Wait();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"运行模式执行失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行上料流程
        /// </summary>
        public async Task ExecuteLoadingProcess()
        {
            if (_isLoadingInProgress)
            {
                _logger.Warn("上料流程已在执行中，忽略重复调用");
                return;
            }

            try
            {
                _isLoadingInProgress = true;
                IsStopped = false;
                IsPaused = false;
                _loadingCTS = new CancellationTokenSource();
                _currentLoadingState = LoadingState.Initialize;
                _currentAssemblyPosition = 1; // 从第一个装配位置开始
                _currentDispensingPosition = 1;

                // 重置拍照相关状态
                _currentPhotoGroup = 1;
                _totalPhotoGroups = 6;

                while (_currentLoadingState != LoadingState.Complete)
                {
                    if (_loadingCTS.Token.IsCancellationRequested) break;
                    if (IsPaused) await WaitForContinue();
                    if (IsStopped) break;

                    bool stepResult = await ExecuteCurrentState();
                    if (!stepResult)
                    {
                        _logger.Error($"状态执行失败: {_currentLoadingState}");
                        break;
                    }

                    // 单步模式等待
                    if (IsSingleStepMode)
                    {
                        await WaitForSingleStepContinue();
                    }
                }

                if (_currentLoadingState == LoadingState.Complete)
                {
                    _logger.Info("上料流程完成");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Warn("上料流程被取消");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "上料流程执行异常");
            }
            finally
            {
                _isLoadingInProgress = false;
                _loadingCTS?.Dispose();
            }
        }
        private async Task<bool> ExecuteCurrentState()
        {
            try
            {
                switch (_currentLoadingState)
                {
                    // 0. 初始化上料流程
                    case LoadingState.Initialize:
                        await InitializeLoading();
                        break;

                    // 1. 开载具吸真空，判断物料是否到位
                    case LoadingState.CheckMaterial:
                        await CheckMaterialAction();
                        break;

                    // 2. 移到取料位
                    case LoadingState.MoveToPickPosition:
                        await MoveToPickPosition();
                        break;

                    // 3. 旋转到第1个组装角度
                     case LoadingState.RotateToAssemblyPosition:
                         await RotateToAssemblyPosition();
                         break;

                    // 4.等待取料完成
                    case LoadingState.WaitForPickupComplete:
                        await WaitForPickupCompleteSignal();
                        break;

                    // 5. 通知点胶站开始纠正Pillar
                    case LoadingState.Notify3DScanStart:
                        await Notify3DVisionSystemForScan();
                        break;

                    // 6. 等待组装工位组装点胶完成
                    case LoadingState.WaitAssemblyPhotoComplete:
                        await WaitPillar2PhotoComplete();
                        break;

                    // 7. 旋转到下个角度
                    case LoadingState.RotateToNextAssemblyPosition:
                        await RotateToNextAssemblyPosition();
                        break;

                    // 8. 等待6个组装完成
                    case LoadingState.WaitAssemblyComplete:
                        await WaitAssemblyComplete();
                        break;

                    // 9. 退到出料位
                    case LoadingState.PostAssembly:
                        await MoveToAssemblyWaitPosition();
                        break;

                    // 10. 流程完成
                    case LoadingState.Complete:
                        _logger.Info("【上料流程】所有步骤完成");
                        break;

                    case LoadingState.Error:
                        await HandleAssemblyError();
                        break;
                    default:
                        _logger.Warn($"未知的状态: {_currentLoadingState}");
                        return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"状态执行异常 {_currentLoadingState}: {ex.Message}");
                HandleErrorState();
                return false;
            }
        }

        /// <summary>
        /// 单步执行下一个状态
        /// </summary>
        public void SingleStepNext()
        {
            if (_isLoadingInProgress && IsSingleStepMode)
            {
                _singleStepEvent.Set();
                _logger.Info("单步执行下一步");
            }
        }

        // 单步模式控制
        private bool _isSingleStepMode = false;
        private ManualResetEvent _singleStepEvent = new ManualResetEvent(false);

        public bool IsSingleStepMode
        {
            get => _isSingleStepMode;
            set
            {
                _isSingleStepMode = value;
                if (!value)
                {
                    _singleStepEvent.Set(); // 退出单步模式时释放等待
                }
            }
        }
        /// <summary>
        /// 启动单步模式
        /// </summary>
        public void StartSingleStepMode()
        {
            if (_isLoadingInProgress)
            {
                StopLoadingProcess();
            }
            IsSingleStepMode = true;
        }
        private async Task WaitForSingleStepContinue()
        {
            _logger.Info($"单步模式等待继续 - 当前状态: {_currentLoadingState}");
            _singleStepEvent.Reset();

            // 等待继续信号或取消
            await Task.Run(() =>
            {
                WaitHandle.WaitAny(new[] { _singleStepEvent, _loadingCTS.Token.WaitHandle });
            });

            if (_loadingCTS.Token.IsCancellationRequested)
            {
                throw new OperationCanceledException();
            }
        }
        private async Task WaitForContinue()
        {
            while (IsPaused && !IsStopped)
            {
                await Task.Delay(200);
            }
        }
        /// <summary>
        /// 停止上料流程
        /// </summary>
        public void StopLoadingProcess()
        {
            MoveStop();
            IsSingleStepMode = false;
            if (_isLoadingInProgress && _loadingCTS != null)
            {
                _loadingCTS.Cancel();
                _logger.Info("上料流程停止请求已发送");
            }
            else
            {
                _logger.Warn("未检测到运行的上料流程，无法停止");
            }
        }
        /// <summary>
        /// 处理组装错误
        /// </summary>
        private async Task HandleAssemblyError()
        {
            _logger.Error("【组装流程】进入错误处理状态");

            // 停止所有轴运动
            MoveStop();

            // 报告错误
            ReportAlarm(XAlarmLevel.STOP, (int)MachineAlarmCode.上料流程异常, (int)XSysAlarmId.MACHINE,
                AlarmCategory.SYSTEM.ToString(), "上料流程进入错误状态");
        }
        // 选中的工位列表
        private List<int> _selectedAssemblyPositions = new List<int> { 1, 2, 3, 4, 5, 6 };
        private int _currentPositionIndex = 0;

        // 是否按顺序执行所有选中工位
        private bool _isSequentialExecution = false;

        // 当前执行的工位号（基于选择）
        private int _selectedCurrentPosition = 1;

        /// <summary>
        /// 设置装配位置列表（从Overview传入）
        /// </summary>
        public void SetAssemblyPositions(List<int> positions)
        {
            try
            {
                if (positions == null || positions.Count == 0)
                {
                    _logger.Warn("设置的装配位置列表为空，使用默认值");
                    _selectedAssemblyPositions = new List<int> { 1, 2, 3, 4, 5, 6 };
                }
                else
                {
                    _selectedAssemblyPositions = positions.OrderBy(p => p).ToList();
                    _logger.Info($"LoadingStation已设置装配位置: {string.Join(", ", positions)}");
                }

                _currentPositionIndex = 0;

                // 如果有选中的工位，设置当前工位为第一个
                if (_selectedAssemblyPositions.Count > 0)
                {
                    _selectedCurrentPosition = _selectedAssemblyPositions[0];
                    _currentAssemblyPosition = _selectedCurrentPosition;
                }
                if (positions.Count <= 1)
                {
                    SetExecutionMode(false);
                }
                else
                {
                    SetExecutionMode(true);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"设置装配位置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置执行模式
        /// </summary>
        public void SetExecutionMode(bool isSequential)
        {
            _isSequentialExecution = isSequential;
            _logger.Info($"设置执行模式: {(isSequential ? "顺序执行" : "单工位执行")}");
        }
    }
}
