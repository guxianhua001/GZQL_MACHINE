using Core.Abstraction;
using Core.Abstractions.IConfiguration;
using Core.Models;
using Core.Utilities;
using NLog;
using Prism.Commands;
using Prism.Events;
using Prism.Ioc;
using Prism.Services.Dialogs;
using Recipe;
using Recipe.Interfaces;
using SmarterMotion;
using Stations.Service;
using Stations.Services;
using Stations.TaskParameters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using static Stations.AssemblyStation;
using static Stations.RegisterTask;

namespace Stations
{
    /// <summary>
    /// 点胶模组
    /// </summary>
    [TaskId(2)]
    public partial class DispenserStation : XTaskBase<DispenserStationParams>, ITask, IDeviceManager, IParameterEditable
    {
        private readonly RecipeService<DispenserStationParams> _recipeService;
        private DispenserStationParams _internalParameters = new DispenserStationParams();
        // 实现 IParameterEditable 接口
        public string EditTitle => $"{Name} - 参数编辑";
        public object Parameters => _recipeService.Parameters;
        public string Identifier => "DispenserStation";

        private readonly ILoggerService _logger;
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IContainerExtension _container;
        private readonly IParameterEditor _parameterEditor;
        private readonly IParameterStore _parameterStore;
        private readonly IParameterStorage _parameterStorage;
        private readonly IAxisConfigService _axisConfigService;
        private readonly ICompensationService _compensationService;    // 补偿服务
        private RecipePoolManager _recipePoolManager;
        public ICommand EditParametersCommand => _recipeService.EditParametersCommand;
        public ICommand SwitchRecipeCommand => _recipeService.SwitchRecipeCommand;
        // 配方相关属性
        public string CurrentRecipeName => _recipeService?.CurrentRecipeName ?? "Default";
        public List<string> AvailableRecipes => _recipeService?.AvailableRecipes ?? new List<string>();
        public bool IsParametersVisible { get; set; } = true; // 控制参数在Overview中的可见性

        // 强类型参数属性
        public DispenserStationParams TypedParameters => ParametersBase as DispenserStationParams;

        public IAxis DispZ1;    // 轴3 点胶工位Z轴1
        public IAxis DispZ2;    // 轴4 点胶工位Z轴2
        public IAxis DispZ3;    // 轴5 点胶工位Z轴3
        public IAxis DispY_1;   // 轴7 点胶工位Y轴主轴
        public IAxis DispY_2;   // 轴8 点胶工位Y轴从轴
        public IAxis DispX;     // 轴9 点胶工位X轴
        public IAxis AsmX;      // 轴6 装配工位X轴
        public IAxis PlatY;     // 轴10 装配平台Y轴
        public IAxis PlatU;     // 轴11 装配平台U轴
        public IAxis PlatR;     // 轴12 装配平台R轴

        private XDo m_CameraExtTrigger;
        private XDo m_ShotGlueSolenoid;
        private XDo m_WipeMotorReset;
        private XDo m_WipeGlueValve;
        private XDo m_WipeMotor;
        // UV固化灯
        private XDo m_UVLight1;
        private XDo m_UVLight2;

        /// <summary>
        /// 重写获取当前配方名称的方法
        /// </summary>
        protected override string GetCurrentRecipeName()
        {
            return _recipeService?.CurrentRecipeName ?? "DefaultRecipe";
        }
        private LoadingStation _loadingStation;

        private readonly ICameraController _cameraController;
        private readonly IVisionDataService _visionDataService;
        private readonly ITCPEventService _tcpEventService;

        // 点胶补偿值
        private double _dispensingOffsetX = 0;
        private double _dispensingOffsetY = 0;
        private double _dispensingOffsetZ = 0;

        public DispenserStation(
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
            RecipePoolManager recipePoolManager,
            TaskInstanceManager taskManager,
            DmcMotionService motionService,
            ICameraController cameraController,
            ITCPEventService tCPEventService,
            IVisionDataService visionDataService,      // 注入视觉数据服务
            ICompensationService compensationService) 
        : base(taskId, "Dispenser Station", eventAggregator)
        {
            _logger = logger;
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _container = container;
            _parameterEditor = parameterEditor;
            _parameterStorage = parameterStorage;
            _axisConfigService = axisConfigService;
            _recipePoolManager = recipePoolManager;
            _cameraController = cameraController;
            _visionDataService = visionDataService;
            _tcpEventService  = tCPEventService;
            _compensationService = compensationService;
            // 初始化配方服务
            _recipeService = new RecipeService<DispenserStationParams>(
                stationIdentifier: "DispenserStation",
                stationName: "Dispenser Station",
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

            // 初始化针头校准服务
            _needleCalibrationService = new NeedleCalibrationService(
                parameterStorage, logger, eventAggregator);

            // 加载默认针头校准参数
            _ = _needleCalibrationService.LoadParametersAsync(CurrentRecipeName);

            // 注册视觉数据回调
            RegisterVisionCallbacks();  

            _loadingStation = taskManager.GetTask<LoadingStation>();

            //_ = InitializeAsync();
            _motionService = motionService;
        }
        private async Task InitializeAsync()
        {
            await LoadRecipeParametersAsync();
        }
        public override void SetTaskId(int taskId)
        {
            // 允许后续设置任务ID
            TaskId = taskId;
            Name = $"Dispenser Station #{taskId}";
        }

        private void RegisterVisionCallbacks()
        {
            // 注册点胶相机的视觉数据回调
            _visionDataService.RegisterStation("DispensingStation", "DispensingCamera", OnDispensingVisionDataReceived);
            _logger.Info("点胶站已注册视觉数据回调");
        }

        private void OnDispensingVisionDataReceived(string data)
        {
            _logger.Info($"点胶站收到视觉数据: {data}");
            // 解析视觉数据，Pillar的角度补偿 + Tab的XY补偿 "Camera=DispensingCamera;VISION_RESULT:SUCCESS:offsetX=0.00100000000000033,offsetY=-0.00399999999990541,offsetU=0"
        }

        /// <summary>
        /// 应用点胶补偿
        /// </summary>
        private void ApplyDispensingOffset()
        {
            // 获取当前配方参数
            var parameters = _recipeService.Parameters;

            if (parameters != null && parameters.DispensingPath != null)
            {
                var path = parameters.DispensingPath;

                // 应用XY补偿
                if (_dispensingOffsetX != 0 || _dispensingOffsetY != 0)
                {
                    //path.StartX += _dispensingOffsetX;
                    //path.StartY += _dispensingOffsetY;
                    //path.EndX += _dispensingOffsetX;
                    //path.EndY += _dispensingOffsetY;

                    _logger.Info($"应用XY补偿: ΔX={_dispensingOffsetX:F3}, ΔY={_dispensingOffsetY:F3}");
                }

                // 应用Z轴补偿
                if (_dispensingOffsetZ != 0)
                {
                    //path.DispensingHeight += _dispensingOffsetZ;
                    _logger.Info($"应用Z轴补偿: ΔZ={_dispensingOffsetZ:F3}");
                }

                // 触发参数更新事件
                //OnDispensingPathUpdated?.Invoke(this, path);
            }
        }

        /// <summary>
        /// 重置点胶补偿值
        /// </summary>
        public void ResetDispensingOffset()
        {
            _dispensingOffsetX = 0;
            _dispensingOffsetY = 0;
            _dispensingOffsetZ = 0;
            _logger.Info("点胶补偿值已重置");
        }

        // 属性
        public double DispensingOffsetX => _dispensingOffsetX;
        public double DispensingOffsetY => _dispensingOffsetY;
        public double DispensingOffsetZ => _dispensingOffsetZ;
        // 事件
        //public event EventHandler<DispensingPathParameters> OnDispensingPathUpdated;

        /// <summary>
        /// 异步加载配方参数
        /// </summary>
        private async Task LoadRecipeParametersAsync()
        {
            try
            {
                _logger.Info("开始加载配方参数...");

                // 使用 ConfigureAwait(false) 避免死锁
                await _recipeService.InitializationTask.ConfigureAwait(false);

                string currentRecipeName = _recipeService.CurrentRecipeName;
                _logger.Info($"当前配方名称: {currentRecipeName}");

                await _recipeService.LoadRecipeParameters(currentRecipeName)
                    .ConfigureAwait(false);

                if (_recipeService.Parameters != null)
                {
                    // 异步更新参数
                    await UpdateParametersAsync(_recipeService.Parameters);
                }

                _logger.Info("配方参数加载完成");
            }
            catch (Exception ex)
            {
                _logger.Error($"加载配方参数失败: {ex.Message}");
                _internalParameters = new DispenserStationParams();
                _logger.Warn("使用默认参数");
            }
        }

        private async Task UpdateParametersAsync(DispenserStationParams parameters)
        {
            // 如果在UI线程，直接更新；否则通过Dispatcher
            if (Application.Current?.Dispatcher?.CheckAccess() == true)
            {
                _internalParameters = parameters;
            }
            else
            {
                await Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    _internalParameters = parameters;
                });
            }
        }
        private int GetParameterCount(TaskParametersBase parameters)
        {
            if (parameters == null) return 0;
            return parameters.GetType().GetProperties().Length;
        }

        #region 配方服务事件订阅与处理
        public void OnEditParameters()
        {
            _recipeService.OnEditParameters();// 也可以使用回调
        }
        private void SubscribeToRecipeEvents()
        {
            // 订阅参数应用事件
            _recipeService.ParametersApplied += OnRecipeParametersApplied;

            // 订阅配方改变事件
            _recipeService.RecipeChanged += OnRecipeChanged;

            // 订阅参数加载事件
            _recipeService.ParametersLoaded += OnParametersLoaded;
        }
        private void OnRecipeParametersApplied(object sender, DispenserStationParams parameters)
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

        private void OnParametersLoaded(object sender, DispenserStationParams parameters)
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

        public void RegisterDevice()
        {
            DispZ1 = XDevice.Instance.FindAxisById(2);
            DispZ2 = XDevice.Instance.FindAxisById(3);
            DispZ3 = XDevice.Instance.FindAxisById(4);
            DispY_1 = XDevice.Instance.FindAxisById(6);
            DispY_2 = XDevice.Instance.FindAxisById(7);
            DispX = XDevice.Instance.FindAxisById(8);
            AsmX = XDevice.Instance.FindAxisById(5);

            PlatY = XDevice.Instance.FindAxisById(9);
            PlatU = XDevice.Instance.FindAxisById(10);
            PlatR = XDevice.Instance.FindAxisById(11);

            m_CameraExtTrigger = XDevice.Instance.FindDoById(28);
            m_ShotGlueSolenoid = XDevice.Instance.FindDoById(13);
            m_UVLight1 = XDevice.Instance.FindDoById(26);
            m_UVLight2 = XDevice.Instance.FindDoById(27);
            m_WipeMotorReset = XDevice.Instance.FindDoById(12);
            m_WipeGlueValve = XDevice.Instance.FindDoById(21);
            m_WipeMotor = XDevice.Instance.FindDoById(25);
        }

        public override void Initialize()
        {

        }
        protected override void Homing(CancellationToken cancellation)
        {
            base.Homing(cancellation);
            ExecuteHoming();
        }
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
                            //ExecuteDispensingProcessAsync().Wait();
                            break;
                        case "SingleStep":
                            // 单步模式运行
                            _isDispensingSingleStepMode = true;
                            //ExecuteDispensingProcessAsync().Wait();
                            break;
                        default:
                            _logger.Warn($"未知的运行模式: {mode}");
                            break;
                    }
                }
                else
                {
                    // 默认自动运行
                    ExecuteDispensingProcessAsync().Wait();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"运行模式执行失败: {ex.Message}");
            }
        }

        // 点胶流程状态枚举
        public enum DispensingState
        {
            Initialize,
            InitializeParameters,
            WaitForPillarCorrectionTrigger,
            CorrectPillar1,
            CaptureTabOffset,
            ReturnToScanPosition,
            WaitForTrigger3DImage,
            Perform3DScan,
            ExtractPathAndDispensing,           // 统一移动到组拍照位
            FirstCleanGlue,                     // 统一触发组拍照
            MoveToWaitPosition,          // 统一等待组拍照完成
            WaitForPillar1Dispensing,
            Pillar1Dispensing,
            NotifyAssemblyReady,
            Pillar2Dispensing,
            PillarDispensingComplete,
            DispensingCycle,
            Complete,
            SecondCleanGlue
        }

        private bool _isDispensingInProgress = false;
        private CancellationTokenSource _dispensingCTS;
        private DispensingState _currentDispensingState = DispensingState.Initialize;
        private int _currentDispensingStep = 1;
        private List<PointF> _dispensingPath = new List<PointF>();
        private int _currentDispensingPosition = 0;
        private int _currentPhotoGroup = 1;          // 当前拍照组序号 (1-6)
        private int _currentPhotoPosition = 1;       // 当前组内拍照位 (1:tab, 2:pillar1, 3:pillar2)
        private readonly int _totalPhotoGroups = 6;  // 总拍照组数
        private readonly int _positionsPerGroup = 3; // 每组拍照位数
        private int dispensingPathIndex = 0;         // 点胶路径索引 1-2
        private double basePlaneZ = 0;
        private double needleTipZ = 0;
        public double dispensingHeight = 0; // 最后的点胶高度
        // 单步模式控制
        private bool _isDispensingSingleStepMode = false;
        private ManualResetEvent _dispensingSingleStepEvent = new ManualResetEvent(false);

        public bool IsDispensingSingleStepMode
        {
            get => _isDispensingSingleStepMode;
            set
            {
                _isDispensingSingleStepMode = value;
                if (!value)
                {
                    _dispensingSingleStepEvent.Set(); // 退出单步模式时释放等待
                }
            }
        }

        /// <summary>
        /// 执行完整点胶流程（支持暂停、单步运行）
        /// </summary>
        public async Task<bool> ExecuteDispensingProcessAsync()
        {
            // 检查重入锁
            if (_isDispensingInProgress)
            {
                _logger.Warn("点胶流程已在执行中，忽略重复调用");
                return false;
            }

            try
            {
                // 设置重入锁
                _isDispensingInProgress = true;
                _dispensingCTS = new CancellationTokenSource();

                // 重置状态机
                _currentDispensingState = DispensingState.Initialize;
                _currentDispensingStep = 1;

                _logger.Info("【点胶工站】开始执行点胶流程");

                // 流程状态机
                while (_currentDispensingState != DispensingState.Complete)
                {
                    // 检查取消请求
                    if (_dispensingCTS.Token.IsCancellationRequested)
                    {
                        _logger.Info("点胶流程已被取消");
                        break;
                    }

                    // 检查程序暂停
                    if (IsPaused)
                    {
                        await WaitForContinue();
                        if (IsStopped) break;
                        continue;
                    }

                    bool stepResult = false;

                    switch (_currentDispensingState)
                    {
                        case DispensingState.Initialize:
                            stepResult = await InitializeDispensing();
                            break;

                        case DispensingState.InitializeParameters:
                            stepResult = await InitializeDispensingParametersAsync();
                            break;

                        // 1.等待上料站通知纠正Pillar
                        case DispensingState.WaitForPillarCorrectionTrigger:
                            stepResult = await WaitForPillarCorrectionTriggerAsync();
                            break;

                        // 2.纠正Pillar1
                        case DispensingState.CorrectPillar1:
                            stepResult = await CorrectPillar1Async();
                            break;

                        // 3.拍Tab获得偏移量
                        case DispensingState.CaptureTabOffset:
                            stepResult = await CaptureTabOffsetAsync();
                            break;

                        // 4.Y轴回到扫描位
                        case DispensingState.ReturnToScanPosition:
                            stepResult = await ReturnToScanPosition();
                            break;

                        // 5. 3D扫描
                        case DispensingState.Perform3DScan:
                            stepResult = await Perform3DScanForDispensingAsync();
                            break;

                        // 6. actuator轨迹提取和点胶
                        case DispensingState.ExtractPathAndDispensing:
                            stepResult = await ExtractPathAndDispensingAsync(_currentPhotoGroup, dispensingPathIndex);
                            break;

                        // 7. 第1次清胶
                        case DispensingState.FirstCleanGlue:
                            stepResult = await FirstCleanGlueAsync();
                            break;

                        // 8. 退到等待位
                        case DispensingState.MoveToWaitPosition:
                            stepResult = await MoveToWaitPositionAsync();
                            break;
                        
                        // 9. 等待Pillar1点胶
                        case DispensingState.WaitForPillar1Dispensing:
                            stepResult = await WaitForPillar1DispensingAsync();
                            break;

                        // 10. Pillar1点胶
                        case DispensingState.Pillar1Dispensing:
                            stepResult = await Pillar1DispensingAsync();
                            break;

                        // 11. Pillar2点胶
                        case DispensingState.Pillar2Dispensing:
                            await Pillar2DispensingAsync();
                            break;

                        // 12.Pillar点胶完成
                        case DispensingState.PillarDispensingComplete:
                            await PostPillarDispensingAsync();
                            break;

                        // 13.第2次清胶
                        case DispensingState.SecondCleanGlue:
                            stepResult = await SecondCleanGlueAsync();
                            break;

                        // 14.点胶完成 退到等待位
                        case DispensingState.DispensingCycle:
                            stepResult = await ExecuteDispensingCycleAsync(_dispensingCTS.Token);
                            break;

                        // 15.下一循环
                        case DispensingState.Complete:
                        
                            break;
                    }

                    if (!stepResult)
                    {
                        _logger.Error($"点胶流程在状态 {_currentDispensingState} 执行失败");
                        ReportAlarm(XAlarmLevel.PAUSE, (int)MachineAlarmCode.点胶流程失败, (int)XSysAlarmId.MACHINE,
                            AlarmCategory.SYSTEM.ToString(), $"点胶流程失败: {_currentDispensingState}");
                        return false;
                    }

                    // 单步模式下等待继续信号
                    if (IsDispensingSingleStepMode)
                    {
                        await WaitForDispensingSingleStepContinue();
                    }
                }

                _logger.Info("【点胶工站】点胶流程完成");
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Warn("点胶流程被操作员取消");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "点胶流程执行异常");
                ReportAlarm(XAlarmLevel.PAUSE, (int)MachineAlarmCode.点胶流程失败, (int)XSysAlarmId.MACHINE,
                    AlarmCategory.SYSTEM.ToString(), $"点胶流程失败: {ex.Message}");
                return false;
            }
            finally
            {
                // 释放重入锁
                _isDispensingInProgress = false;
                _dispensingCTS?.Dispose();
                _logger.Info("点胶流程结束");
            }
        }

        /// <summary>
        /// 初始化点胶流程
        /// </summary>
        private async Task<bool> InitializeDispensing()
        {
            try
            {
                _logger.Info("初始化点胶流程");
                // 执行初始化操作，如检查设备状态、复位等

                // 状态转移
                _currentDispensingState = DispensingState.InitializeParameters;
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "点胶流程初始化失败");
                return false;
            }
        }

        /// <summary>
        /// 停止点胶流程
        /// </summary>
        public void StopDispensingProcess()
        {
            if (_isDispensingInProgress && _dispensingCTS != null)
            {
                _dispensingCTS.Cancel();
                _logger.Info("点胶流程停止请求已发送");
            }
            else
            {
                _logger.Warn("未检测到运行的点胶流程，无法停止");
            }
        }

        /// <summary>
        /// 单步执行下一个状态
        /// </summary>
        public void DispensingSingleStepNext()
        {
            if (_isDispensingInProgress && IsDispensingSingleStepMode)
            {
                _dispensingSingleStepEvent.Set();
                _logger.Info("点胶单步执行下一步");
            }
        }

        /// <summary>
        /// 启动点胶单步模式
        /// </summary>
        public void StartDispensingSingleStepMode()
        {
            if (_isDispensingInProgress)
            {
                StopDispensingProcess();
            }

            IsDispensingSingleStepMode = true;
            // 启动点胶流程
            Task.Run(() => ExecuteDispensingProcessAsync());
        }

        /// <summary>
        /// 停止点胶单步模式
        /// </summary>
        public void StopDispensingSingleStepMode()
        {
            IsDispensingSingleStepMode = false;
            StopDispensingProcess();
            _dispensingSingleStepEvent.Set(); // 确保释放等待
            _logger.Info("点胶单步模式已停止");
        }

        /// <summary>
        /// 等待单步继续信号
        /// </summary>
        private async Task WaitForDispensingSingleStepContinue()
        {
            _logger.Info($"点胶单步模式等待继续 - 当前状态: {_currentDispensingState}");
            _dispensingSingleStepEvent.Reset();

            // 等待继续信号或取消
            await Task.Run(() =>
            {
                WaitHandle.WaitAny(new[] { _dispensingSingleStepEvent, _dispensingCTS.Token.WaitHandle });
            });

            if (_dispensingCTS.Token.IsCancellationRequested)
            {
                throw new OperationCanceledException();
            }
        }

        // 点胶步骤状态回调
        private Action<string, bool> _dispensingStepStatusCallback;

        /// <summary>
        /// 设置点胶步骤状态回调
        /// </summary>
        public void SetDispensingStepStatusCallback(Action<string, bool> callback)
        {
            _dispensingStepStatusCallback = callback;
            _logger.Info("点胶步骤状态回调已设置");
        }

        /// <summary>
        /// 更新点胶步骤状态
        /// </summary>
        private void UpdateStepStatus(string description, bool isWaiting = false)
        {
            _dispensingStepStatusCallback?.Invoke(description, isWaiting);

            if (isWaiting)
            {
                _logger.Info($"点胶单步等待: {description}");
            }
            else
            {
                _logger.Info($"点胶单步执行: {description}");
            }
        }
        private async Task WaitForContinue()
        {
            while (IsPaused && !IsStopped)
            {
                await Task.Delay(200);
            }
        }
        public void CancelCurrentOperation()
        {
            try
            {
                _logger.Info("用户请求取消当前操作");
                // 取消所有正在进行的操作
                _dispensingCTS?.Cancel();
                _calibrationCancellationTokenSource?.Cancel();

                _logger.Info("取消操作已执行");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "执行取消操作时发生异常");
            }
        }

        public void StopAllAxes()
        {
            MoveStop(); // 立即停止所有轴运动
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
                    _currentDispensingPosition = _selectedCurrentPosition;
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
