using Core.Abstraction;
using Core.Abstractions.IConfiguration;
using Core.Events;
using Core.Services;
using Core.Utilities;
using Prism.Commands;
using Prism.Events;
using Prism.Ioc;
using Prism.Services.Dialogs;
using Recipe;
using Recipe.Events;
using Recipe.Interfaces;
using Recipe.Models;
using SmarterMotion;
using Stations.Services;
using Stations.TaskParameters;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using static Stations.RegisterTask;

namespace Stations
{
    /// <summary>
    /// 装配工位
    /// </summary>
    [TaskId(3)]
    public partial class AssemblyStation : XTaskBase<AssemblyStationParams>, ITask, IDeviceManager, IParameterEditable
    {
        private readonly RecipeService<AssemblyStationParams> _recipeService;
        private AssemblyStationParams _internalParameters = new AssemblyStationParams();
        // 实现 IParameterEditable 接口
        public string EditTitle => $"{Name} - 参数编辑";
        public object Parameters => _recipeService.Parameters;
        public string Identifier => "AssemblyStation";

        private readonly ILoggerService _logger;
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IContainerExtension _container;
        private readonly IParameterEditor _parameterEditor;
        private readonly IParameterStore _parameterStore;
        private readonly IParameterStorage _parameterStorage;
        private readonly IAxisConfigService _axisConfigService;
        private readonly IRecipeManager _recipeManager;      // 配方管理器
        private readonly IRecipeStorage _recipeStorage;      // 配方存储
        private readonly ITCPEventService _tcpEventService;
        private readonly IVisionDataService _visionDataService;  // 添加视觉数据服务
        private readonly ICameraEventProcessor _cameraEventProcessor;  // 相机事件处理器
        private readonly ICompensationService _compensationService;    // 补偿服务
        private readonly ICameraController _cameraController;
        private readonly Dictionary<string, TaskCompletionSource<string>> _pendingCameraRequests = new();
        private readonly object _requestLock = new object();
        private IAppConfig _appConfig;
        private RecipePoolManager _recipePoolManager;
        // 配方相关字段                                  
        public ICommand EditParametersCommand => _recipeService.EditParametersCommand;
        public ICommand SwitchRecipeCommand => _recipeService.SwitchRecipeCommand;
        // 配方相关属性
        public string CurrentRecipeName => _recipeService?.CurrentRecipeName ?? "Default";
        public List<string> AvailableRecipes => _recipeService?.AvailableRecipes ?? new List<string>();
        public bool IsParametersVisible { get; set; } = true; // 控制参数在Overview中的可见性

        private IAxis AsmZ;     // 轴1 装配工位Z轴
        private IAxis AsmU;     // 轴2 装配工位U轴
        private IAxis AsmX;     // 轴6 装配工位X轴
        private IAxis AsmY;     // 轴12 拨片Y轴
        private IAxis AsmCamY;  // 轴13 侧相机Y轴
        private IAxis PlatY;    // 轴10 装配平台Y轴
        public IAxis DispX;     // 轴9 点胶工位X轴
        public IAxis DispY_1;   // 轴7 点胶工位Y轴主轴
        public IAxis DispZ3;    // 轴5 点胶工位Z轴3

        // 装配吸真空
        private XDo AsmVac;
        // 装配破真空
        private XDo AsmBreakVac;

        // 相机客户端名称常量
        private const string CAMERA_CLIENT = "CAMERA";

        /// <summary>
        /// 重写获取当前配方名称的方法
        /// </summary>
        protected override string GetCurrentRecipeName()
        {
            return _recipeService?.CurrentRecipeName ?? "DefaultRecipe";
        }

        private LoadingStation _loadingStation;
        private DispenserStation _dispenserStation;


        public AssemblyStation(
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
            ITCPEventService tcpEventService,
            IVisionDataService visionDataService, 
            ICameraEventProcessor cameraEventProcessor,
            ICompensationService compensationService,
            ICameraController cameraController,
            TaskInstanceManager taskManager
            )
            : base(taskId, "Assembly Station", eventAggregator)
        {
            _logger = logger;
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _container = container;
            _parameterEditor = parameterEditor;
            _parameterStorage = parameterStorage;
            _axisConfigService = axisConfigService;
            _recipeManager = recipeManager;
            _recipeStorage = recipeStorage;
            _appConfig = appConfig;
            _recipePoolManager = recipePoolManager;
            _tcpEventService = tcpEventService;
            _visionDataService = visionDataService; 
            _cameraEventProcessor = cameraEventProcessor;
            _compensationService = compensationService;
            _cameraController = cameraController;
            _loadingStation = taskManager.GetTask<LoadingStation>();
            _dispenserStation = taskManager.GetTask<DispenserStation>();
            // 初始化配方服务
            _recipeService = new RecipeService<AssemblyStationParams>(
                stationIdentifier: "AssemblyStation",
                stationName: "Assembly Station",
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
            // 注册视觉数据回调
            RegisterVisionCallbacks();
        }
        public override void SetTaskId(int taskId)
        {
            // 允许后续设置任务ID
            TaskId = taskId;
            Name = $"Assembly Station #{taskId}";
        }

        #region 视觉数据处理
        private void RegisterVisionCallbacks()
        {
            // 注册各个相机的回调
            _visionDataService.RegisterStation("AssemblyStation", "PickupCamera", OnPickupVisionDataReceived);
            _visionDataService.RegisterStation("AssemblyStation", "SideCamera", OnSideVisionDataReceived);
            _visionDataService.RegisterStation("AssemblyStation", "BottomCamera", OnBottomVisionDataReceived);

            _logger.Info("已注册视觉数据回调");
        }

        private void OnPickupVisionDataReceived(string data)
        {
            _logger.Info($"收到取料相机视觉数据: {data}");
        }

        // 事件声明
        public event EventHandler<PhotoCompletedEventArgs> OnPhotoCompleted;
        private void OnSideVisionDataReceived(string data)
        {
            _logger.Info($"组装站收到侧相机视觉数据: {data}");
            // 触发事件，把原始 data 抛出去
            OnPhotoCompleted?.Invoke(this, new PhotoCompletedEventArgs
            {
                CameraName = "SideCamera",
                Data = data,
                Success = data.Contains("VISION_RESULT:SUCCESS")
            });  // 触发事件，把原始 data 抛出去
        }

        private void OnBottomVisionDataReceived(string data)
        {
            _logger.Info($"收到底部相机视觉数据: {data}");
            // 处理底部相机数据...
        }

        private void ResetOffsetValues()
        {
            _pickSlotOffsetX = 0;
            _pickSlotOffsetY = 0;
        }

        #endregion

        #region 配方相关方法
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
        private void OnRecipeParametersApplied(object sender, AssemblyStationParams parameters)
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

        private void OnParametersLoaded(object sender, AssemblyStationParams parameters)
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

        // 组装状态枚举
        public enum AssemblyState
        {
            Initialize,
            WaitForLoadingStationSignal,
            MoveToPickupPhotoPosition,
            TakePickupPhoto,
            WaitForPickupPhotoComplete,
            PickupMaterial,
            AlignSlotAngle,
            PerformPickSlot,
            CheckStripperSlotPhoto,
            TakeSideCameraPhoto,
            WaitForSideCameraPhotoComplete,
            MoveToBottomCameraPhotoPosition,
            TakeBottomCameraPhoto,
            WaitForBottomCameraPhotoComplete,
            MoveZToStandbyAfterPhoto,
            WaitForGlueComplete,
            WaitForTab1PhotoPosition,
            MoveToAssemblyPosition,
            WaitForMaterialReady,
            PerformAssemblyOperation,
            NotifyMaterialMoveIn,
            WaitForMaterialInPlace,
            MoveHorizontalSmallStep,
            MoveDownSmallStep,
            ReleaseGripper,
            NotifyMaterialMoveBack,
            WaitForMaterialBack,
            PillarGlue,
            WaitUVFixed,
            AssemblyIPQC1,
            AssemblyIPQC2,
            PostAssembly,
            MoveXToPickPosition,
            Complete,
            Error
        }
        private bool _isRunningInProgress = false;
        private CancellationTokenSource _runningCTS;
        private AssemblyState _currentAssemblyState = AssemblyState.Initialize;
        private int _currentPickupCycle = 1; // 当前取料周期序号
        private int _currentAssemblyStep = 1; // 当前处理的组装步骤
        private int _requestedStationIndex = 0; 
        private bool _isSingleStepMode = false;
        private double _pickSlotOffsetX = 0; // 取料槽偏移量
        private double _pickSlotOffsetY = 0; // 取料槽偏移量
        // 实现 IDeviceManager 接口
        public void RegisterDevice()
        {
            // 注册设备信息，例如轴、DI/DO等
            AsmZ = XDevice.Instance.FindAxisById(0);
            AsmU = XDevice.Instance.FindAxisById(1);
            AsmX = XDevice.Instance.FindAxisById(5);
            AsmY = XDevice.Instance.FindAxisById(12);
            AsmCamY = XDevice.Instance.FindAxisById(13);
            PlatY = XDevice.Instance.FindAxisById(9);
            DispZ3 = XDevice.Instance.FindAxisById(4);
            DispY_1 = XDevice.Instance.FindAxisById(6);
            DispX = XDevice.Instance.FindAxisById(8);
        }
        public override void Initialize()
        {
            RegisterDevice();
        }
        protected override void Homing(CancellationToken cancellation)
        {
            base.Homing(cancellation);
            ExecuteHoming();
        }
        protected override void Running(object runMode)
        {
            try
            {
                // 根据运行模式执行不同的流程
                if (runMode is string mode)
                {
                    switch (mode)
                    {
                        case "Auto":
                            // 自动运行组装流程
                            //ExecuteAssemblyProcess().Wait();
                            break;
                        case "SingleStep":
                            // 单步模式运行
                            IsSingleStepMode = true;
                            //ExecuteAssemblyProcess().Wait();
                            break;
                        default:
                            _logger.Warn($"未知的运行模式: {mode}");
                            break;
                    }
                }
                else
                {
                    // 默认自动运行
                    ExecuteAssemblyProcess().Wait();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"运行模式执行失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行组装流程
        /// </summary>
        public async Task<bool> ExecuteAssemblyProcess()
        {
            // 检查重入锁
            if (_isRunningInProgress)
            {
                _logger.Warn("组装流程已在执行中，忽略重复调用");
                return false;
            }

            try
            {
                // 设置重入锁
                _isRunningInProgress = true;
                _runningCTS = new CancellationTokenSource();

                // 重置状态机
                _currentAssemblyState = AssemblyState.Initialize;
                _currentAssemblyStep = 1;

                // 流程状态机
                while (_currentAssemblyState != AssemblyState.Complete &&
                       _currentAssemblyState != AssemblyState.Error)
                {
                    // 检查取消请求
                    if (_runningCTS.Token.IsCancellationRequested)
                    {
                        _logger.Info("组装流程已被取消");
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

                    switch (_currentAssemblyState)
                    {
                        // 0. 初始化组装流程
                        case AssemblyState.Initialize:
                            stepResult = await InitializeAssembly();
                            break;

                        // 1. 等待上料工站触发取料
                        case AssemblyState.WaitForLoadingStationSignal:
                            stepResult = await WaitForLoadingStationSignal();
                            break;

                        // 2. 执行取料动作
                        case AssemblyState.PickupMaterial:
                            stepResult = await PickupMaterial();
                            break;

                        // 3. 执行Slot纠正动作(触发点胶站纠正去拍照)
                        case AssemblyState.AlignSlotAngle:
                            stepResult = await AlignSlotAngleAsync();
                            break;

                        // 4. 执行拨片动作 
                        case AssemblyState.PerformPickSlot:
                            stepResult = await PerformStripperSlotAsync();
                            break;

                        // 5. 拨片后拍照
                        case AssemblyState.CheckStripperSlotPhoto:
                            stepResult = await CheckStripperSlotPhotoAsync();
                            break;

                        // 6. 移动到底部相机拍照
                        case AssemblyState.MoveToBottomCameraPhotoPosition:
                            stepResult = await MoveToBottomCameraPhotoPosition();
                            break;

                        // 7. Z轴抬升到待机位
                        case AssemblyState.MoveZToStandbyAfterPhoto:
                            stepResult = await MoveZToStandbyAfterPhoto();
                            break;

                        // 8. 等待点胶站点胶完成信号(曲线)
                        case AssemblyState.WaitForGlueComplete:
                            stepResult = await WaitForGlueCompleteAaync();
                            break;

                        // 9. 轴X移到组装位置 Y轴移动到预组装位置
                        case AssemblyState.MoveToAssemblyPosition:
                            stepResult = await MoveToAssemblyPosition();
                            break;

                        // 10. 开始组装动作
                        case AssemblyState.PerformAssemblyOperation:
                            stepResult = await AssembleModule(_currentAssemblyStep);
                            break;

                        // 11. IPQC 1 (组装后 未点胶)
                        case AssemblyState.AssemblyIPQC1:
                            stepResult = await AssemblyIPQC();
                            break;

                        // 12. Pillar点胶、固化
                        case AssemblyState.PillarGlue:
                            stepResult = await NotifyDispenserSystemForPillarGlue();
                            break;

                        // 13. 等待UV固化完成
                        case AssemblyState.WaitUVFixed:
                            stepResult = await WaitUVFixComplete();
                            break;

                        // 14. IPQC 2 (组装后 已点胶)
                        case AssemblyState.AssemblyIPQC2:
                            stepResult = await AssemblyIPQC();
                            break;

                        // 15. 组装完成
                        case AssemblyState.PostAssembly:
                            stepResult = await PostAssembly();
                            break;

                        // 999. 组装错误
                        case AssemblyState.Error:
                            await HandleAssemblyError();
                            break;
                    }
                    if (!stepResult) 
                    {
                        _logger.Error($"组装流程在状态 {_currentAssemblyState} 执行失败");
                        ReportAlarm(XAlarmLevel.PAUSE, (int)MachineAlarmCode.组装流程异常, (int)XSysAlarmId.MACHINE,
                            AlarmCategory.SYSTEM.ToString(), $"组装流程失败: {_currentAssemblyState}");
                        return false;
                    }
                    // 单步模式下等待继续信号
                    if (IsSingleStepMode)
                    {
                        await WaitForSingleStepContinue();
                    }
                }
                _logger.Info("【组装工站】组装流程完成");
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Warn("组装流程被操作员取消");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "组装流程执行异常");
                _currentAssemblyState = AssemblyState.Error;
                ReportAlarm(XAlarmLevel.PAUSE, (int)MachineAlarmCode.组装流程失败, (int)XSysAlarmId.MACHINE,
                    AlarmCategory.SYSTEM.ToString(), $"组装流程失败: {ex.Message}");
                return false;
            }
            finally
            {
                // 释放重入锁
                _isRunningInProgress = false;
                _runningCTS?.Dispose();
                _logger.Info("组装流程结束");
            }
        }

        /// <summary>
        /// 停止组装流程
        /// </summary>
        public void StopAssemblyProcess()
        {
            if (_isRunningInProgress && _runningCTS != null)
            {
                _runningCTS.Cancel();
                _currentAssemblyProcessState = AssemblyProcessState.Complete;
                _logger.Info("组装流程停止请求已发送");
            }
            else
            {
                _logger.Warn("未检测到运行的组装流程，无法停止");
            }
        }

        /// <summary>
        /// 单步执行下一个状态
        /// </summary>
        public void SingleStepNext()
        {
            if (_isProcessRunning && IsSingleStepMode)
            {
                _singleStepEvent.Set();
                _logger.Info("单步执行下一步");
            }
        }

        /// <summary>
        /// 启动单步模式
        /// </summary>
        public void StartSingleStepMode()
        {
            if (_isRunningInProgress)
            {
                StopAssemblyProcess();
            }

            IsSingleStepMode = true;

            // 启动组装流程
            //Task.Run(() => ExecuteAssemblyProcess());
        }
        /// <summary>
        /// 停止单步模式
        /// </summary>
        public void StopSingleStepMode()
        {
            IsSingleStepMode = false;
            StopAssemblyProcess();
            _singleStepEvent.Set(); // 确保释放等待
            _logger.Info("单步模式已停止");
        }
        // 单步模式控制
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

        private async Task WaitForSingleStepContinue()
        {
            _logger.Info($"单步模式等待继续 - 当前状态: {_currentAssemblyProcessState}");
            _singleStepEvent.Reset();

            // 等待继续信号或取消
            await Task.Run(() =>
            {
                WaitHandle.WaitAny(new[] { _singleStepEvent, _processCTS.Token.WaitHandle });
            });

            if (_processCTS.Token.IsCancellationRequested)
            {
                throw new OperationCanceledException();
            }
        }

        #region 流程控制属性
        private PickProcessState _currentPickState = PickProcessState.Initialize;
        private PhotoProcessState _currentPhotoState = PhotoProcessState.Initialize;
        private AssemblyProcessState _currentAssemblyProcessState = AssemblyProcessState.Initialize;
        private int _currentAssemblyPosition = 1; // 当前装配位置(1-6)
        private CancellationTokenSource _processCTS;
        private bool _isProcessRunning = false;
        #endregion

        #region 流程相关枚举
        public enum PickProcessState
        {
            Initialize,
            MoveZToSafePosition,
            MoveXToPhotoPosition,
            MoveZToPhotoPosition,
            TakePhoto,
            MoveZToStandby,
            MoveXYToPickPosition,
            MoveZDownToPick,
            GripperClamp,
            CheckClampSuccess,
            MoveZUpAfterPick,
            Complete,
            Error
        }

        public enum PhotoProcessState
        {
            Initialize,                      // 0.初始化
            MoveZToStandby,                  // 1.Z轴抬起到待机位
            MoveXYToTabPhotoPosition,        // 2.XY一起运动到取料拍照位(tab{moduleNumber})
            MoveZToPhotoHeight,              // 3.Z轴到拍照高度
            TriggerTabPhoto,                 // 4.触发拍照
            WaitForTabPhotoComplete,         // 5.等待拍照完成
            MoveXYToPillar1PhotoPosition,    // 6.XY一起运动到拍照位(Pillar{moduleNumber}_1)
            TriggerPillar1Photo,             // 7.触发拍照
            WaitForPillar1PhotoComplete,     // 8.等待拍照完成
            MoveXYToPillar2PhotoPosition,    // 9.XY一起运动到拍照位(Pillar{moduleNumber}_2)
            TriggerPillar2Photo,             // 10.触发拍照
            WaitForPillar2PhotoComplete,     // 11.等待拍照完成
            MoveZToStandbyAfterPhoto,        // 12.Z轴抬起到待机高度
            Complete,                        // 完成
            Error                            // 错误
        }

        public enum AssemblyProcessState
        {
            Initialize,
            PlatYToPhotoPosition,
            TakeAssemblyPhoto,
            MoveZToStandby,
            MoveXToAssemblyPosition,
            MovePlatYToWaitPosition,
            MoveZDownToPreAssembly,        //Z轴下降到预组装高度
            MoveZDownToAssembly,           //Z轴下降到组装高度
            MovePlatYToPreAssemblyPosition,//Y轴移动到预组装位
            MovePlatYToAssemblyPosition,   //Y轴移动到组装位
            MoveCameraToAssemblyPosition1,  // 移动相机到组装位
            MoveCameraToAssemblyPosition2,
            MoveXSmallStep,
            MoveZDownSmallStep,
            TakeIPQCPhoto,            // ipqc复检拍照
            ReleaseGripper,
            CheckAssemblySuccess,
            MovePlatYBackToWait,
            MoveZUpToStandby,
            MoveXBackToStandby,
            Complete,
            Error
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 人工确认对话框
        /// </summary>
        private async Task<string> GetOperatorConfirmation(string title, string message, string[] options)
        {
            object result = await Framework.Services.DialogService.ShowDialogAsync(
                title: title,
                message: message,
                buttons: options,
                defaultButtonIndex: 0
            );

            if (result is int index && index >= 0)
            {
                // 根据索引获取对应按钮文本
                return options.ElementAtOrDefault(index) ?? $"选项 {index + 1}";
            }

            // 处理取消或异常情况
            return null;
        }

        private async Task ShowOperatorAlert(string title, string message)
        {
            await Framework.Services.DialogService.ShowDialogAsync(
                title: title,
                message: message,
                buttons: new[] { "确定" },
                defaultButtonIndex: 0
            );
        }
        // 单步控制相关
        private Action<string, bool> _stepStatusCallback;

        /// <summary>
        /// 设置步骤状态回调
        /// </summary>
        public void SetStepStatusCallback(Action<string, bool> callback)
        {
            _stepStatusCallback = callback;
            _logger.Info("步骤状态回调已设置");
        }
        /// <summary>
        /// 更新步骤状态
        /// </summary>
        private void UpdateStepStatus(string description, bool isWaiting = false)
        {

            _stepStatusCallback?.Invoke(description, isWaiting);

            string stateInfo = $"当前状态: {_currentAssemblyState} - {description}";

            if (isWaiting)
            {
                _logger.Info($"单步等待: {stateInfo}");
            }
            else
            {
                _logger.Info($"单步执行: {stateInfo}");
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
            ReportAlarm(XAlarmLevel.STOP, (int)MachineAlarmCode.组装流程异常, (int)XSysAlarmId.MACHINE,
                AlarmCategory.SYSTEM.ToString(), "组装流程进入错误状态");
        }
        #endregion

        /// <summary>
        /// 执行指定工位的组装流程
        /// </summary>
        public async Task<bool> ExecuteForStationAsync(int stationNumber, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Info($"开始执行工位{stationNumber}的组装流程");

                // 设置当前装配位置
                _currentAssemblyPosition = stationNumber;

                // 重置状态
                _currentAssemblyState = AssemblyState.Initialize;

                // 执行单个工位的组装流程
                return await ExecuteSingleStationAssemblyAsync(stationNumber, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Error($"执行工位{stationNumber}组装失败: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ExecuteSingleStationAssemblyAsync(int stationNumber, CancellationToken cancellationToken)
        {
            // 这里实现单个工位的具体组装逻辑

            _logger.Info($"正在执行工位{stationNumber}的组装...");

            // 模拟执行
            await Task.Delay(1000, cancellationToken);

            return true;
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
