using AxisConfiguration.Services;
using Core.Abstraction;
using Core.Events;
using Core.Models;
using Core.Services;
using Core.Utilities;
using Framework.Views;
using Interfaces;
using Interfaces.Events;
using Interfaces.SharedInterfaces;
using MaterialDesignThemes.Wpf;
using ModuleCore.Common.Authority;
using ModuleCore.Models;
using ModuleCore.ViewModels;
using ModuleCore.Views;
using Newtonsoft.Json;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using SmarterMotion;
using Stations;
using Stations.TaskParameters;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using static Stations.AssemblyStation;

namespace Framework.ViewModels
{
    public class AssemblyStationViewModel : BindableBase, INavigationAware
    {
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IParameterEditable _parameterSource;
        private readonly ILoggerService _logger;
        private SubscriptionToken _refreshToken;
        private readonly AppConfig _appConfig;
        private readonly TaskInstanceManager _taskManager;
        private LoginModel _loginModel { get; set; }

        private LoadingStation _loadingStation;
        private AssemblyStation _assemblyStation;
        // 定时器相关
        private DispatcherTimer _positionTimer;
        private bool _isActive;
        // 电爪控制属性  
        private double _gripperJogStep = 5;
        public double GripperJogStep
        {
            get => _gripperJogStep;
            set => SetProperty(ref _gripperJogStep, value);
        }
        private double _gripperTarget = 500;
        public double GripperTarget
        {
            get => _gripperTarget;
            set => SetProperty(ref _gripperTarget, value);
        }
        private double _gripperMoveSpeed = 50;
        public double GripperMoveSpeed
        {
            get => _gripperMoveSpeed;
            set => SetProperty(ref _gripperMoveSpeed, value);
        }
        private double _gripperJogSpeed = 30;
        public double GripperJogSpeed
        {
            get => _gripperJogSpeed;
            set => SetProperty(ref _gripperJogSpeed, value);
        }
        private System.Drawing.PointF _gripperCurrentPosition = System.Drawing.PointF.Empty;
        public System.Drawing.PointF GripperCurrentPosition
        {
            get => _gripperCurrentPosition;
            set => SetProperty(ref _gripperCurrentPosition, value, () =>
            {
                RaisePropertyChanged(nameof(GripperPositionDisplay));
            });
        }
        public string GripperPositionDisplay =>
                  $"{GripperCurrentPosition.X:F1}, {GripperCurrentPosition.Y:F1}";
        // 力矩相关属性
        private string _gripperTorquePercentage = "50"; // 默认50%
        public string GripperTorquePercentage
        {
            get => _gripperTorquePercentage;
            set
            {
                if (SetProperty(ref _gripperTorquePercentage, value))
                {
                    // 实时更新显示值
                    RaisePropertyChanged(nameof(GripperTorqueDisplay));
                }
            }
        }
        // 力矩显示值（计算对应的N值）
        public string GripperTorqueDisplay
        {
            get
            {
                if (double.TryParse(GripperTorquePercentage, out double percentage))
                {
                    // 0-100% 对应 0-15N
                    double torqueN = percentage * 15.0 / 100.0;
                    return $"{torqueN:F1} N";
                }
                return "0.0 N";
            }
        }
        // 设置力矩命令
        private DelegateCommand _setGripperTorqueCommand;
        public DelegateCommand SetGripperTorqueCommand =>
            _setGripperTorqueCommand ??= new DelegateCommand(ExecuteSetGripperTorque);
        // 电爪控制命令
        public DelegateCommand MoveGripperToTargetCommand { get; }
        public DelegateCommand<JogDirection?> GripperJogCommand { get; }
        public DelegateCommand GripperJogLeftCommand { get; }
        public DelegateCommand StopGripperJogCommand { get; }
        public DelegateCommand GripperJogRightCommand { get; }
        public DelegateCommand RecordGripperPositionCommand { get; }
        public DelegateCommand GripperResetCommand { get; }
        public DelegateCommand GripperHomeCommand { get; }

        // 单步控制属性
        private bool _isSingleStepMode = false;
        public bool IsSingleStepMode
        {
            get => _isSingleStepMode;
            set => SetProperty(ref _isSingleStepMode, value);
        }

        private string _currentStepDescription = "就绪";
        public string CurrentStepDescription
        {
            get => _currentStepDescription;
            set => SetProperty(ref _currentStepDescription, value);
        }

        private bool _isStepWaiting = false;
        public bool IsStepWaiting
        {
            get => _isStepWaiting;
            set => SetProperty(ref _isStepWaiting, value);
        }
        #region Offset显示属性
        private double _offsetX;
        private double _offsetY;
        private double _offsetU;
        private double _offsetH;

        public double OffsetX
        {
            get => _offsetX;
            set => SetProperty(ref _offsetX, value);
        }

        public double OffsetY
        {
            get => _offsetY;
            set => SetProperty(ref _offsetY, value);
        }
        public double OffsetU
        {
            get => _offsetU;
            set => SetProperty(ref _offsetU, value);
        }
        public double OffsetH
        {
            get => _offsetH;
            set => SetProperty(ref _offsetH, value);
        }
        #endregion
        // 单步控制命令
        public DelegateCommand StartSingleStepCommand { get; }
        public DelegateCommand NextStepCommand { get; }
        public DelegateCommand StopSingleStepCommand { get; }

        #region 相机标定命令

        public DelegateCommand StartCalibrationCommand { get; }
        public DelegateCommand NextCalibrationPointCommand { get; }
        public DelegateCommand StopCalibrationCommand { get; }
        public DelegateCommand SaveCalibrationPointsCommand { get; }
        public DelegateCommand LoadCalibrationPointsCommand { get; }
        public DelegateCommand<string> TakePhotoCommand { get; private set; }
        public DelegateCommand<string> GetCameraStatusCommand { get; private set; }
        // 相机状态属性
        private string _sideCameraStatus = "未知";
        public string SideCameraStatus
        {
            get => _sideCameraStatus;
            set => SetProperty(ref _sideCameraStatus, value);
        }

        private string _bottomCameraStatus = "未知";
        public string BottomCameraStatus
        {
            get => _bottomCameraStatus;
            set => SetProperty(ref _bottomCameraStatus, value);
        }
        private string _topCameraStatus = "未知";
        public string TopCameraStatus
        {
            get => _topCameraStatus;
            set => SetProperty(ref _topCameraStatus, value);
        }

        private bool _isSideCameraConnected = false;
        public bool IsSideCameraConnected
        {
            get => _isSideCameraConnected;
            set => SetProperty(ref _isSideCameraConnected, value);
        }

        private bool _isBottomCameraConnected = false;
        public bool IsBottomCameraConnected
        {
            get => _isBottomCameraConnected;
            set => SetProperty(ref _isBottomCameraConnected, value);
        }
        private bool _isTopCameraConnected = false;
        public bool IsTopCameraConnected
        {
            get => _isTopCameraConnected;
            set => SetProperty(ref _isTopCameraConnected, value);
        }
        private bool _isAutoCalibration = false;
        public bool IsAutoCalibration
        {
            get => _isAutoCalibration;
            set => SetProperty(ref _isAutoCalibration, value);
        }
        private string _autoCalibrationStatus = "就绪";
        public string AutoCalibrationStatus
        {
            get => _autoCalibrationStatus;
            set => SetProperty(ref _autoCalibrationStatus, value);
        }
        private bool _isAutoCalibrationRunning;
        public bool IsAutoCalibrationRunning
        {
            get => _isAutoCalibrationRunning;
            set => SetProperty(ref _isAutoCalibrationRunning, value);
        }
        private int _calibrationDelayMs = 1000;
        public int CalibrationDelayMs
        {
            get => _calibrationDelayMs;
            set => SetProperty(ref _calibrationDelayMs, value);
        }
        #endregion

        #region 自动标定命令

        public DelegateCommand StartAutoCalibrationCommand { get; }
        public DelegateCommand StopAutoCalibrationCommand { get; }
        public DelegateCommand PauseAutoCalibrationCommand { get; }
        public DelegateCommand ResumeAutoCalibrationCommand { get; }

        #endregion

        #region 流程命令属性
        public DelegateCommand StopProcessCommand { get; private set; }

        public DelegateCommand PickupPos1Command { get; private set; }
        public DelegateCommand PickupPos2Command { get; private set; }
        public DelegateCommand PickupPos3Command { get; private set; }
        public DelegateCommand PickupPos4Command { get; private set; }
        public DelegateCommand PickupPos5Command { get; private set; }
        public DelegateCommand PickupPos6Command { get; private set; }

        public DelegateCommand GoSideCameraCommand { get; private set; }
        public DelegateCommand GoBottomCameraCommand { get; private set; }
        public DelegateCommand GoModule1PhotoCommand { get; private set; }
        public DelegateCommand GoModule2PhotoCommand { get; private set; }
        public DelegateCommand GoModule3PhotoCommand { get; private set; }
        public DelegateCommand GoModule4PhotoCommand { get; private set; }
        public DelegateCommand GoModule5PhotoCommand { get; private set; }
        public DelegateCommand GoModule6PhotoCommand { get; private set; }
        public DelegateCommand TriggerBottomCameraPhotoCommand { get; private set; }
        public DelegateCommand TriggerSideCameraPhotoCommand { get; private set; }
        public DelegateCommand TriggerTopCameraPhotoCommand { get; private set; }
        public DelegateCommand AssemblyPos1Command { get; private set; }
        public DelegateCommand AssemblyPos2Command { get; private set; }
        public DelegateCommand AssemblyPos3Command { get; private set; }
        public DelegateCommand AssemblyPos4Command { get; private set; }
        public DelegateCommand AssemblyPos5Command { get; private set; }
        public DelegateCommand AssemblyPos6Command { get; private set; }
        public DelegateCommand GoToPickSlotPositionCommand { get; private set; }
        public DelegateCommand GoToAdjustSlotPositionCommand { get; private set; }
        public DelegateCommand AutoPickSlotCommand { get; private set; }
        public DelegateCommand MoveToAxesUStandbyPosCommand { get; private set; }
        public DelegateCommand AutoInspectionSlotCommand { get; private set; }
        #endregion

        #region 流程状态属性
        private string _currentProcessStatus = "就绪";
        public string CurrentProcessStatus
        {
            get => _currentProcessStatus;
            set => SetProperty(ref _currentProcessStatus, value);
        }

        private bool _isProcessRunning;
        public bool IsProcessRunning
        {
            get => _isProcessRunning;
            set => SetProperty(ref _isProcessRunning, value);
        }

        private int _currentProcessModule = 1;
        public int CurrentProcessModule
        {
            get => _currentProcessModule;
            set => SetProperty(ref _currentProcessModule, value);
        }
        #endregion

        #region 相机标定属性

        private bool _is9PointCalibration = true;
        public bool Is9PointCalibration
        {
            get => _is9PointCalibration;
            set => SetProperty(ref _is9PointCalibration, value);
        }

        private bool _is14PointCalibration = false;
        public bool Is14PointCalibration
        {
            get => _is14PointCalibration;
            set => SetProperty(ref _is14PointCalibration, value);
        }

        private bool _isSideCamera = true;
        public bool IsSideCamera
        {
            get => _isSideCamera;
            set
            {
                if (SetProperty(ref _isSideCamera, value) && value)
                {
                    IsBottomCamera = false;
                    UpdateCameraInfo();
                    UpdateCurrentConfiguration();
                }
            }
        }

        private bool _isBottomCamera = false;
        public bool IsBottomCamera
        {
            get => _isBottomCamera;
            set
            {
                if (SetProperty(ref _isBottomCamera, value) && value)
                {
                    IsSideCamera = false;
                    UpdateCameraInfo();
                    UpdateCurrentConfiguration();
                }
            }
        }

        private double _calibrationStartX = 100;
        public double CalibrationStartX
        {
            get => _calibrationStartX;
            set => SetProperty(ref _calibrationStartX, value);
        }

        private double _calibrationStartY = 100;
        public double CalibrationStartY
        {
            get => _calibrationStartY;
            set => SetProperty(ref _calibrationStartY, value);
        }

        private double _calibrationSpacing = 50;
        public double CalibrationSpacing
        {
            get => _calibrationSpacing;
            set => SetProperty(ref _calibrationSpacing, value);
        }

        private string _calibrationStatus = "就绪";
        public string CalibrationStatus
        {
            get => _calibrationStatus;
            set => SetProperty(ref _calibrationStatus, value);
        }

        private int _currentCalibrationPointIndex;
        public int CurrentCalibrationPointIndex
        {
            get => _currentCalibrationPointIndex;
            set => SetProperty(ref _currentCalibrationPointIndex, value);
        }

        private double _calibrationProgress;
        public double CalibrationProgress
        {
            get => _calibrationProgress;
            set => SetProperty(ref _calibrationProgress, value);
        }

        private bool _isCalibrationRunning;
        public bool IsCalibrationRunning
        {
            get => _isCalibrationRunning;
            set => SetProperty(ref _isCalibrationRunning, value);
        }
        private bool _isCalibrationWaiting;
        public bool IsCalibrationWaiting
        {
            get => _isCalibrationWaiting;
            set => SetProperty(ref _isCalibrationWaiting, value);
        }
        private double _calibrationRotationRadius = 75; // 默认旋转半径
        public double CalibrationRotationRadius
        {
            get => _calibrationRotationRadius;
            set => SetProperty(ref _calibrationRotationRadius, value);
        }
        private ObservableCollection<CalibrationPoint> _calibrationPoints = new ObservableCollection<CalibrationPoint>();
        // 当前显示的标定点（根据相机类型和标定类型动态切换）
        public ObservableCollection<CalibrationPoint> CalibrationPoints
        {
            get
            {
                string key = GetCurrentCalibrationKey();
                if (_calibrationPointsData.ContainsKey(key))
                {
                    return _calibrationPointsData[key];
                }
                return new ObservableCollection<CalibrationPoint>();
            }
        }
        // 获取当前标定配置键
        private string GetCurrentCalibrationKey()
        {
            string cameraType = IsSideCamera ? "Side" : "Bottom";
            string pointType = Is9PointCalibration ? "9Point" : "14Point";
            return $"{cameraType}_{pointType}";
        }
        // 当前相机类型显示
        private string _currentCameraType = "侧相机 (AsmX + AsmZ)";
        public string CurrentCameraType
        {
            get => _currentCameraType;
            set => SetProperty(ref _currentCameraType, value);
        }

        // 当前标定类型显示
        private string _currentCalibrationType = "9点标定";
        public string CurrentCalibrationType
        {
            get => _currentCalibrationType;
            set => SetProperty(ref _currentCalibrationType, value);
        }
        // 标定配置存储
        private Dictionary<string, EnhancedCalibrationConfig> _calibrationConfigs = new Dictionary<string, EnhancedCalibrationConfig>();
        private Dictionary<string, ObservableCollection<CalibrationPoint>> _calibrationPointsData = new Dictionary<string, ObservableCollection<CalibrationPoint>>();
        // 坐标轴方向设置
        private bool _isXAxisReversed;
        public bool IsXAxisReversed
        {
            get => _isXAxisReversed;
            set
            {
                if (SetProperty(ref _isXAxisReversed, value))
                {
                    UpdateCameraInfo();
                    UpdateCurrentCalibrationConfig();
                }
            }
        }
        private bool _isYAxisReversed;
        public bool IsYAxisReversed
        {
            get => _isYAxisReversed;
            set
            {
                if (SetProperty(ref _isYAxisReversed, value))
                {
                    UpdateCameraInfo();
                    UpdateCurrentCalibrationConfig();
                }
            }
        }
        // 坐标方向信息显示
        public string AxisDirectionInfo
        {
            get
            {
                string xDir = IsXAxisReversed ? "反向" : "正向";
                string yDir = IsYAxisReversed ? "反向" : "正向";
                return $"X轴{xDir}, Y轴{yDir}";
            }
        }
        private string _currentCameraInfo = "侧相机 (AsmX + AsmZ)";
        public string CurrentCameraInfo
        {
            get => _currentCameraInfo;
            set => SetProperty(ref _currentCameraInfo, value);
        }
        private void UpdateCameraInfo()
        {
            if (IsSideCamera)
            {
                CurrentCameraType = "侧相机 (AsmX + AsmZ)";
            }
            else if (IsBottomCamera)
            {
                CurrentCameraType = "下相机 (AsmX + PlatY)";
            }
            CurrentCalibrationType = Is9PointCalibration ? "9点标定" : "14点标定";
            // 通知相关属性更新
            RaisePropertyChanged(nameof(CurrentCameraType));
            RaisePropertyChanged(nameof(AxisDirectionInfo));
            RaisePropertyChanged(nameof(CurrentCalibrationType));
            _logger.Info($"切换相机: {CurrentCameraType}");
        }

        #endregion

        public AssemblyStationViewModel(
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            ILoggerService loggerService,
            TaskInstanceManager taskManager,
            AppConfig appConfig,
            LoginModel loginModel)
        {
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _taskManager = taskManager;
            _loginModel = loginModel;
            _logger = loggerService;
            //_parameterSource = parameterEditable;
            // 订阅刷新事件
            _refreshToken = _eventAggregator
                .GetEvent<TParamsNeedRefreshEvent>()
                .Subscribe(OnPositionsNeedRefresh);
            _appConfig = appConfig;
            _assemblyStation = _taskManager.GetTask<AssemblyStation>();
            // 监听登录模型变化
            _loginModel.PropertyChanged += LoginModel_PropertyChanged;
            // 电爪控制命令
            ManunalClampCommand = ExecuteAsyncOperation(() => ManunalClampCommandAction());
            ManunaReleaseCommand = ExecuteAsyncOperation(() => ManunaReleaseCommandAction());
            MoveGripperToTargetCommand = ExecuteAsyncOperation(() => MoveGripperToTargetAction());
            GripperJogCommand = new DelegateCommand<JogDirection?>(ExecuteGripperJog);
            GripperJogLeftCommand = new DelegateCommand(ExecuteJogLeftGripperJog);
            StopGripperJogCommand = new DelegateCommand(ExecuteStopGripperJog);
            GripperJogRightCommand = new DelegateCommand(ExecuteJogRightGripperJog);
            RecordGripperPositionCommand = ExecuteAsyncOperation(() => RecordGripperPositionAction());
            GripperResetCommand = ExecuteAsyncOperation(() => GripperResetAction());
            StartSingleStepCommand = ExecuteAsyncOperation(() => StartSingleStepAction());
            NextStepCommand = new DelegateCommand(ExecuteNextStep, () => IsStepWaiting);
            StopSingleStepCommand = new DelegateCommand(ExecuteStopSingleStep);
            GripperHomeCommand = ExecuteAsyncOperation(() => GripperHomeAction());
            // 相机标定命令
            StartCalibrationCommand = ExecuteAsyncOperation(() => StartCalibrationAction());
            NextCalibrationPointCommand = ExecuteAsyncOperation(() => NextCalibrationPointAction());
            StopCalibrationCommand = new DelegateCommand(ExecuteStopCalibration);
            SaveCalibrationPointsCommand = ExecuteAsyncOperation(() => SaveCalibrationPointsAction());
            LoadCalibrationPointsCommand = ExecuteAsyncOperation(() => LoadCalibrationPointsAction());
            InitializeTimer();
            // 设置回调
            if (_assemblyStation != null)
            {
                _assemblyStation.SetStepStatusCallback(UpdateStepStatus);
            }
            // 设置标定状态回调
            if (_assemblyStation != null)
            {
                _assemblyStation.SetCalibrationStatusCallback(UpdateCalibrationStatus);
                _assemblyStation.SetStepStatusCallback(UpdateStepStatus);
            }
            // 设置自动标定回调
            if (_assemblyStation != null)
            {
                _assemblyStation.SetAutoCalibrationStatusCallback(UpdateAutoCalibrationStatus);
            }
            // 初始化流程命令
            InitializeProcessCommands();
            // 初始化标定配置
            InitializeCalibrationConfigs();

            // 设置初始配置
            UpdateCurrentConfiguration();

            // 初始化相机控制命令
            TakePhotoCommand = new DelegateCommand<string>(async (cameraType) =>
            {
                await ExecuteTakePhotoCommand(cameraType);
            });

            GetCameraStatusCommand = new DelegateCommand<string>(async (cameraType) =>
            {
                await ExecuteGetCameraStatusCommand(cameraType);
            });
            // 自动标定命令
            StartAutoCalibrationCommand = ExecuteAsyncOperation(() => StartAutoCalibrationAction());
            StopAutoCalibrationCommand = new DelegateCommand(ExecuteStopAutoCalibration);
            PauseAutoCalibrationCommand = new DelegateCommand(ExecutePauseAutoCalibration);
            ResumeAutoCalibrationCommand = ExecuteAsyncOperation(() => ResumeAutoCalibrationAction());
            // 相机拍照命令
            TriggerBottomCameraPhotoCommand = ExecuteAsyncOperation(() => TriggerBottomCameraPhotoAction());
            TriggerSideCameraPhotoCommand = ExecuteAsyncOperation(() => TriggerSideCameraPhotoAction());
            TriggerTopCameraPhotoCommand = ExecuteAsyncOperation(() => TriggerTabCameraPhotoAction());
            // 订阅相机完成事件
            if (_assemblyStation != null)
            {
                _assemblyStation.OnPhotoCompleted += OnPhotoCompleted;
            }

        }
        private void InitializeProcessCommands()
        {
            StopProcessCommand = new DelegateCommand(ExecuteStopProcessCommand);
            MoveToAxesUStandbyPosCommand = ExecuteAsyncOperation(() => MoveToAxesUStandbyPosAction());
            // 取料命令
            PickupPos1Command = ExecuteAsyncOperation(() => ExecuteAssemblyPickupCommand(1));
            PickupPos2Command = ExecuteAsyncOperation(() => ExecuteAssemblyPickupCommand(2));
            PickupPos3Command = ExecuteAsyncOperation(() => ExecuteAssemblyPickupCommand(3));
            PickupPos4Command = ExecuteAsyncOperation(() => ExecuteAssemblyPickupCommand(4));
            PickupPos5Command = ExecuteAsyncOperation(() => ExecuteAssemblyPickupCommand(5));
            PickupPos6Command = ExecuteAsyncOperation(() => ExecuteAssemblyPickupCommand(6));
            // 拍照命令
            GoModule1PhotoCommand = ExecuteAsyncOperation(() => ExecuteGoModulePhotoCommand(1));
            GoModule2PhotoCommand = ExecuteAsyncOperation(() => ExecuteGoModulePhotoCommand(2));
            GoModule3PhotoCommand = ExecuteAsyncOperation(() => ExecuteGoModulePhotoCommand(3));
            GoModule4PhotoCommand = ExecuteAsyncOperation(() => ExecuteGoModulePhotoCommand(4));
            GoModule5PhotoCommand = ExecuteAsyncOperation(() => ExecuteGoModulePhotoCommand(5));
            GoModule6PhotoCommand = ExecuteAsyncOperation(() => ExecuteGoModulePhotoCommand(6));

            GoSideCameraCommand = ExecuteAsyncOperation(() => ExecuteGoSideCameraPhotoCommand());
            GoBottomCameraCommand = ExecuteAsyncOperation(() => ExecuteGoBottomCameraPhotoCommand());

            // 组装命令
            AssemblyPos1Command = ExecuteAsyncOperation(() => ExecuteAssembleModuleCommand(1));
            AssemblyPos2Command = ExecuteAsyncOperation(() => ExecuteAssembleModuleCommand(2));
            AssemblyPos3Command = ExecuteAsyncOperation(() => ExecuteAssembleModuleCommand(3));
            AssemblyPos4Command = ExecuteAsyncOperation(() => ExecuteAssembleModuleCommand(4));
            AssemblyPos5Command = ExecuteAsyncOperation(() => ExecuteAssembleModuleCommand(5));
            AssemblyPos6Command = ExecuteAsyncOperation(() => ExecuteAssembleModuleCommand(6));

            GoToAdjustSlotPositionCommand = ExecuteAsyncOperation(() => GoToAdjustSlotPositionAction());
            GoToPickSlotPositionCommand = ExecuteAsyncOperation(() => GoToPickSlotPositionAction());
            AutoPickSlotCommand = ExecuteAsyncOperation(() => AutoPickSlotAction());
            AutoInspectionSlotCommand = ExecuteAsyncOperation(() => AutoInspectionSlotAction());
        }
        private void LoginModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LoginModel.LoginUser) ||
                e.PropertyName == nameof(LoginModel.HasPermission))
            {
                IsAdmin = _loginModel.HasPermission(Authority.Administrator);
            }
        }
        private void OnPositionsNeedRefresh()
        {

        }
        // 初始化定时器
        private void InitializeTimer()
        {
            _positionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200) // 每200ms更新一次
            };
            _positionTimer.Tick += OnPositionTimerTick;
        }
        // 定时器回调方法
        private void OnPositionTimerTick(object sender, EventArgs e)
        {
            if (_isActive)
            {
                UpdateGripperPositionFromHardware();
            }
        }
        // 从硬件读取位置并更新
        private void UpdateGripperPositionFromHardware()
        {
            try
            {
                uint currentPos = 0;
                // 读取当前位置
                LTDMC.nmc_read_txpdo_extra_uint(0, 2, 2, 1, ref currentPos);

                // 更新UI
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    GripperCurrentPosition = new System.Drawing.PointF(currentPos, 0);
                });
            }
            catch (Exception ex)
            {
                // 记录错误但不中断定时器
                System.Diagnostics.Debug.WriteLine($"读取电爪位置失败: {ex.Message}");
            }
        }
        // 启动位置刷新
        private void StartPositionRefresh()
        {
            if (!_isActive)
            {
                _isActive = true;
                _positionTimer.Start();
                System.Diagnostics.Debug.WriteLine("电爪位置刷新已启动");
            }
        }
        // 停止位置刷新
        private void StopPositionRefresh()
        {
            if (_isActive)
            {
                _isActive = false;
                _positionTimer.Stop();
                System.Diagnostics.Debug.WriteLine("电爪位置刷新已停止");
            }
        }
        // 添加权限和安全检查方法
        private bool CheckPermissionsAndSafety()
        {
            if (!_loginModel.HasPermission(Authority.Administrator))
            {
                ShowMessage($"操作需要 {Authority.Administrator} 权限");
                return false;
            }

            // 检查设备状态
            foreach (XStation station in XStationManager.Instance.Stations.Values)
            {
                if (station.State == XStationState.RUNNING)
                {
                    ShowMessage("设备运行中,禁止手动操作！");
                    return false;
                }
            }

            return true;
        }
        //-----------------------------
        // 命令执行逻辑
        //-----------------------------
        private bool _isAdmin;
        public bool IsAdmin
        {
            get => _loginModel?.HasPermission(Authority.Administrator) ?? false;
            private set
            {
                if (SetProperty(ref _isAdmin, value))
                {
                    // 当管理员状态变化时，通知CanEditParams更新
                    RaisePropertyChanged(nameof(CanEditParams));
                }
            }
        }
        public bool CanEditParams => IsAdmin;
        private DelegateCommand ExecuteAsyncOperation(Action execute, Func<bool> canExecute = null)
        {
            bool isExecuting = false;

            return new DelegateCommand(
                async () =>
                {
                    if (isExecuting) return;
                    if (!_loginModel.HasPermission(Authority.Administrator))
                    {
                        ShowMessage($"操作需要 {Authority.Administrator} 权限");
                        return;
                    }
                    foreach (XStation station in XStationManager.Instance.Stations.Values)
                    {
                        if (station.State == XStationState.RUNNING)
                        {
                            var vm = new NotificationDialogViewModel
                            {
                                Title = "提示信息",
                                Message = "设备运行中,禁止手动操作！",
                                IconKind = PackIconKind.CheckCircle
                            };
                            new Interfaces.NotificationDialog(vm).ShowDialog();
                            return;
                        }
                    }
                    isExecuting = true;
                    CommandManager.InvalidateRequerySuggested();

                    try
                    {
                        await Task.Run(() =>
                        {
                            execute(); // 在后台线程执行同步方法
                        }).ConfigureAwait(false);
                    }
                    finally
                    {
                        isExecuting = false;
                        CommandManager.InvalidateRequerySuggested();
                    }
                },
                canExecute ?? (() => !isExecuting) // 动态可用状态
            );
        }
        private void ShowMessage(string message, PackIconKind iconKind = PackIconKind.AlertCircle)
        {
            if (Application.Current.Dispatcher.CheckAccess())
            {
                // 当前已在UI线程
                ShowDialogInternal(message, iconKind);
            }
            else
            {
                // 切换到UI线程
                Application.Current.Dispatcher.Invoke(() => ShowDialogInternal(message, iconKind));
            }
        }

        private void ShowDialogInternal(string message, PackIconKind iconKind)
        {
            _dialogService.ShowDialog("NotificationDialog", new DialogParameters
            {
                { "title", "提示" },
                { "message", message },
                { "icon", iconKind }
            }, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    // 用户点击确认后的逻辑
                }
            });
        }
        public DelegateCommand JogNegativeCommand { get; }
        public DelegateCommand JogPositiveCommand { get; }
        public DelegateCommand ManunalClampCommand { get; }
        public DelegateCommand ManunaReleaseCommand { get; }       

        #region 拨片动作
        private async Task GoToAdjustSlotPositionAction()
        {
            if (!CheckPermissionsAndSafety()) return;
            try
            {
                CurrentProcessStatus = $"开始移动到调整拨片位置,进行角度纠正...";
                IsProcessRunning = true;

                bool success = await _assemblyStation.AlignSlotAngleAsync();

                if (success)
                {
                    CurrentProcessStatus = $"移动到调整拨片位置,角度纠正完成";
                    ShowMessage($"Slot角度调整完成", PackIconKind.CheckCircle);
                }
                else
                {
                    CurrentProcessStatus = $"移动到调整拨片位置,角度纠正失败";
                    ShowMessage($"Slot角度调整失败", PackIconKind.AlertCircle);
                }
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message);
                _logger.Error($"移动到调整拨片位置,角度纠正失败: {ex.Message}");
            }
        }
        private async Task GoToPickSlotPositionAction()
        {
            if (!CheckPermissionsAndSafety()) return;
            try
            {
                CurrentProcessStatus = $"开始移动到拨片位置...";
                IsProcessRunning = true;

                bool success = await _assemblyStation.MoveAxesToSlotPosition(OffsetX + 2.0 - 0.3, OffsetY + 0.1);//补偿值，根据实际调整

                if (success)
                {
                    CurrentProcessStatus = $"移动到拨片位置完成";
                    ShowMessage($"移动到拨片位置完成", PackIconKind.CheckCircle);
                }
                else
                {
                    CurrentProcessStatus = $"移动到拨片位置失败";
                    ShowMessage($"移动到拨片位置失败", PackIconKind.AlertCircle);
                }
            }
            catch (Exception ex)
            {
                CurrentProcessStatus = $"执行拨片动作异常";
                ShowMessage($"执行拨片动作异常: {ex.Message}", PackIconKind.AlertCircle);
                _logger.Error($"执行拨片动作异常: {ex.Message}");
            }
            finally
            {
                IsProcessRunning = false;
            }
        }
        /// <summary>
        /// 自动拨片动作
        /// </summary>
        private async Task AutoPickSlotAction()
        {
            if (!CheckPermissionsAndSafety()) return;
            try
            {
                CurrentProcessStatus = $"开始执行拨片动作...";

                bool success = await _assemblyStation.ExecuteStripperSlotAction();

                if (success)
                {
                    CurrentProcessStatus = $"执行拨片动作完成";
                    ShowMessage($"执行拨片动作完成", PackIconKind.CheckCircle);
                }
                else
                {
                    CurrentProcessStatus = $"执行拨片动作失败";
                    ShowMessage($"执行拨片动作失败", PackIconKind.AlertCircle);
                }
            }
            catch (Exception ex)
            {
                CurrentProcessStatus = $"执行拨片动作异常";
                ShowMessage($"执行拨片动作异常: {ex.Message}", PackIconKind.AlertCircle);
                _logger.Error($"执行拨片动作异常: {ex.Message}");
            }
        }
        private async Task AutoInspectionSlotAction()
        {
            if (!CheckPermissionsAndSafety()) return;
            try
            {
                CurrentProcessStatus = $"开始执行拨片复查动作...";

                var result = await _assemblyStation.PerformSideCameraRecheckAsync();

                if (result.success)
                {
                    CurrentProcessStatus = $"执行拨片复查动作完成";
                    _logger.Info($"拨片复查动作完成, 偏移量: X={result.offsetX2}, Y={result.offsetY2}, U={result.offsetU2}, H={result.offsetH2}");
                    ShowMessage($"执行拨片复查完成", PackIconKind.CheckCircle);
                }
                else
                {
                    CurrentProcessStatus = $"执行拨片复查动作失败";
                    ShowMessage($"执行拨片复查失败", PackIconKind.AlertCircle);
                }
            }
            catch (Exception ex)
            {
                CurrentProcessStatus = $"执行拨片复查动作异常";
                ShowMessage($"执行拨片复查动作异常: {ex.Message}", PackIconKind.AlertCircle);
                _logger.Error($"执行拨片复查动作异常: {ex.Message}");
            }
        }
        #endregion

        #region 相机拍照
        private async void TriggerBottomCameraPhotoAction()
        {
            // 触发底部相机拍照
            var result = await _assemblyStation.TakePhotoAsync("CAMERA", "T5");
            if (result)
            {
                ShowMessage($"成功拍摄底部相机照片！");
            }
            else
            {
                ShowMessage($"失败拍摄底部相机照片！");
            }

        }
        private async void TriggerSideCameraPhotoAction()
        {
            // 触发侧边相机拍照
            var result = await _assemblyStation.TakePhotoAsync("CAMERA", "T4");
            if (result)
            {
                ShowMessage($"成功拍摄侧边相机照片！");
            }
            else
            {
                ShowMessage($"失败拍摄侧边相机照片！");
            }
        }
        private async void TriggerTabCameraPhotoAction()
        {
            // 触发顶部相机拍照
            var result = await _assemblyStation.TakePhotoAsync("CAMERA", "T1");
            if (result)
            {
                ShowMessage($"成功拍摄顶部相机照片！");
            }
            else
            {
                ShowMessage($"失败拍摄顶部相机照片！");
            }
        }
        private async void TriggerPillar1CameraPhotoAction()
        {
            // 触发顶部相机拍照
            var result = await _assemblyStation.TakePhotoAsync("CAMERA", "T2");
            if (result)
            {
                ShowMessage($"成功拍摄顶部相机照片！");
            }
            else
            {
                ShowMessage($"失败拍摄顶部相机照片！");
            }
        }
        private async void TriggerPillar2CameraPhotoAction()
        {
            // 触发顶部相机拍照
            var result = await _assemblyStation.TakePhotoAsync("CAMERA", "T3");
            if (result)
            {
                ShowMessage($"成功拍摄顶部相机照片！");
            }
            else
            {
                ShowMessage($"失败拍摄顶部相机照片！");
            }
        }
        #endregion

        #region 电爪控制方法
        // 电爪控制方法
        private void GripperResetAction()
        {

        }
        private void GripperHomeAction()
        {
            LTDMC.nmc_write_rxpdo_extra_uint(0, 2, 0, 1, (uint)165);// 回零
        }
        private void MoveGripperToTargetAction()
        {
            // 电爪移动到目标位置
            LTDMC.nmc_write_rxpdo_extra_uint(0, 2, 4, 1, (uint)GripperMoveSpeed);//设置速度
            LTDMC.nmc_write_rxpdo_extra_uint(0, 2, 3, 1, (uint)GripperTarget);//设置目标位置
            ShowMessage($"电爪移动到位置: {GripperTarget}, 速度={GripperMoveSpeed}");
        }
        /// <summary>
        /// 夹紧物料
        /// </summary>
        private void ManunalClampCommandAction()
        {
            // 夹紧位置
            var parameters = _assemblyStation.Parameters as AssemblyStationParams;
            LTDMC.nmc_write_rxpdo_extra_uint(0, 2, 3, 1, (uint)parameters.ClampPos);
            _logger.Info($"执行夹紧物料, 夹紧位置: {parameters.ClampPos}");
        }
        /// <summary>
        /// 松开物料
        /// </summary>
        private void ManunaReleaseCommandAction()
        {
            // 松开位置
            var parameters = _assemblyStation.Parameters as AssemblyStationParams;
            LTDMC.nmc_write_rxpdo_extra_uint(0, 2, 3, 1, (uint)parameters.ReleasePos);
            _logger.Info($"执行松开物料, 松开位置: {parameters.ReleasePos}");
        }
        private void ExecuteGripperJog(JogDirection? direction)
        {
            if (!direction.HasValue) return;

            // 读取当前位置
            var currentPos = GripperCurrentPosition;
            var step = GripperJogStep;

            // 根据方向计算目标位置
            System.Drawing.PointF targetPos = currentPos;
            switch (direction.Value)
            {
                case JogDirection.Left:
                    step = -(float)step;
                    break;
                case JogDirection.Right:
                    step = +(float)step;
                    break;
            }
            // 执行Jog移动
            LTDMC.nmc_write_rxpdo_extra_uint(0, 2, 4, 1, (uint)GripperJogSpeed); // 设置速度
            uint actPos = 0;
            LTDMC.nmc_read_txpdo_extra_uint(0, 2, 2, 1, ref actPos);// 获取实时位置
            uint tarPos = (uint)(actPos + step);
            LTDMC.nmc_write_rxpdo_extra_uint(0, 2, 3, 1, tarPos); // 设置目标位置
            ShowMessage($"电爪移动到位置: {tarPos}, 速度={GripperJogSpeed}");
        }
        private void ExecuteJogLeftGripperJog()
        {
            // 读取当前位置
            var currentPos = GripperCurrentPosition;
            var step = GripperJogStep;
            // 执行Jog移动
            LTDMC.nmc_write_rxpdo_extra_uint(0, 2, 4, 1, (uint)GripperJogSpeed); // 设置速度
            uint actPos = 0;
            LTDMC.nmc_read_txpdo_extra_uint(0, 2, 2, 1, ref actPos);// 获取实时位置
            uint tarPos = (uint)(actPos - step);
            LTDMC.nmc_write_rxpdo_extra_uint(0, 2, 3, 1, tarPos); // 设置目标位置
        }
        private void ExecuteStopGripperJog()
        {
            // 停止电爪Jog移动
            LTDMC.nmc_write_rxpdo_extra_uint(0, 2, 3, 1, 1);
        }
        private void ExecuteJogRightGripperJog()
        {
            // 读取当前位置
            var currentPos = GripperCurrentPosition;
            var step = GripperJogStep;
            // 执行Jog移动
            LTDMC.nmc_write_rxpdo_extra_uint(0, 2, 4, 1, (uint)GripperJogSpeed); // 设置速度
            uint actPos = 0;
            LTDMC.nmc_read_txpdo_extra_uint(0, 2, 2, 1, ref actPos);// 获取实时位置
            uint tarPos = (uint)(actPos + step);
            LTDMC.nmc_write_rxpdo_extra_uint(0, 2, 3, 1, tarPos); // 设置目标位置
        }
        private void RecordGripperPositionAction()
        {
            // 记录当前位置
            // 示例：GripperCurrentPosition = _task5.GetGripperCurrentPosition();
            ShowMessage($"记录电爪当前位置: {GripperPositionDisplay}");
        }

        // 更新电爪位置
        public void UpdateGripperPosition(System.Drawing.PointF newPosition)
        {
            GripperCurrentPosition = newPosition;
        }
        private void ExecuteSetGripperTorque()
        {
            try
            {
                if (double.TryParse(GripperTorquePercentage, out double percentage))
                {
                    // 验证输入范围
                    if (percentage < 0 || percentage > 100)
                    {
                        _logger.Warn($"力矩百分比超出范围: {percentage}%，应在0-100%之间");
                        MessageBox.Show("力矩百分比应在0-100%之间", "参数错误",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // 计算对应的力矩值 (0-15N)
                    double torqueN = percentage * 15.0 / 100.0;

                    // 调用设备API设置力矩
                    // _gripperController.SetTorque(torqueN);

                    _logger.Info($"设置夹爪力矩: {percentage}% ({torqueN:F1}N)");

                    ShowMessage($"夹爪力矩已设置为 {percentage}% ({torqueN:F1}N)");
                }
                else
                {
                    _logger.Warn($"无效的力矩百分比输入: {GripperTorquePercentage}");
                    MessageBox.Show("请输入有效的数字", "输入错误",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"设置夹爪力矩失败: {ex.Message}");
                MessageBox.Show($"设置力矩失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region 流程命令实现
        // 装配位取料命令实现
        private async Task ExecuteAssemblyPickupCommand(int position)
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                CurrentProcessStatus = $"开始{position}号装配位取料...";
                CurrentProcessModule = position;
                IsProcessRunning = true;

                bool success = await _assemblyStation.PickMaterialFromAssemblyPosition(position);

                if (success)
                {
                    CurrentProcessStatus = $"{position}号装配位取料完成";
                    ShowMessage($"{position}号装配位取料完成", PackIconKind.CheckCircle);
                }
                else
                {
                    CurrentProcessStatus = $"{position}号装配位取料失败";
                    ShowMessage($"{position}号装配位取料失败", PackIconKind.AlertCircle);
                }
            }
            catch (Exception ex)
            {
                CurrentProcessStatus = $"{position}号装配位取料异常";
                ShowMessage($"{position}号装配位取料异常: {ex.Message}", PackIconKind.AlertCircle);
                _logger.Error($"{position}号装配位取料异常: {ex.Message}");
            }
            finally
            {
                IsProcessRunning = false;
            }
        }
        private async Task ExecuteGoModulePhotoCommand(string cameraType = "Side")
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                CurrentProcessStatus = $"开始{cameraType}相机拍照...";
                IsProcessRunning = true;

                bool success = await _assemblyStation.TakePhotoAsync(cameraType);

                if (success)
                {
                    CurrentProcessStatus = $"{cameraType}相机拍照完成";
                    ShowMessage($"{cameraType}相机拍照完成", PackIconKind.CheckCircle);
                }
                else
                {
                    CurrentProcessStatus = $"{cameraType}相机拍照失败";
                    ShowMessage($"{cameraType}相机拍照失败", PackIconKind.AlertCircle);
                }
            }
            catch (Exception ex)
            {
                CurrentProcessStatus = $"{cameraType}相机拍照异常";
                ShowMessage($"{cameraType}相机拍照异常: {ex.Message}", PackIconKind.AlertCircle);
                _logger.Error($"{cameraType}相机拍照异常: {ex.Message}");
            }
            finally
            {
                IsProcessRunning = false;
            }
        }
        // 下相机拍照命令
        private async Task ExecuteGoBottomCameraPhotoCommand()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                CurrentProcessStatus = $"开始下相机拍照...";
                IsProcessRunning = true;

                bool success = await _assemblyStation.MoveAxesToBottomCameraPhoto();

                if (success)
                {
                    CurrentProcessStatus = $"下相机拍照完成";
                    ShowMessage($"下相机拍照完成", PackIconKind.CheckCircle);
                }
                else
                {
                    CurrentProcessStatus = $"下相机拍照失败";
                    ShowMessage($"下相机拍照失败", PackIconKind.AlertCircle);
                }
            }
            catch (Exception ex)
            {
                CurrentProcessStatus = $"下相机拍照异常";
                ShowMessage($"下相机拍照异常: {ex.Message}", PackIconKind.AlertCircle);
                _logger.Error($"下相机拍照异常: {ex.Message}");
            }
            finally
            {
                IsProcessRunning = false;
            }
        }
        // 侧相机拍照命令
        private async Task ExecuteGoSideCameraPhotoCommand()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                CurrentProcessStatus = $"开始侧相机拍照...";
                IsProcessRunning = true;

                bool success = await _assemblyStation.MoveAxesToSideCameraPhoto();

                if (success)
                {
                    CurrentProcessStatus = $"侧相机拍照完成";
                    ShowMessage($"侧相机拍照完成", PackIconKind.CheckCircle);
                }
                else
                {
                    CurrentProcessStatus = $"侧相机拍照失败";
                    ShowMessage($"侧相机拍照失败", PackIconKind.AlertCircle);
                }

            }
            catch (Exception ex)
            {
                CurrentProcessStatus = $"侧相机拍照异常";
                ShowMessage($"侧相机拍照异常: {ex.Message}", PackIconKind.AlertCircle);
                _logger.Error($"侧相机拍照异常: {ex.Message}");
            }
            finally
            {
                IsProcessRunning = false;
            }
        }
        // 组件拍照命令
        private async Task ExecuteGoModulePhotoCommand(int module)
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                CurrentProcessStatus = $"开始{module}号组件拍照...";
                CurrentProcessModule = module;
                IsProcessRunning = true;
                bool success = await _assemblyStation.TakePhotoForModule(module);

                if (success)
                {
                    CurrentProcessStatus = $"{module}号组件拍照完成";
                    ShowMessage($"{module}号组件拍照完成", PackIconKind.CheckCircle);
                }
                else
                {
                    CurrentProcessStatus = $"{module}号组件拍照失败";
                    ShowMessage($"{module}号组件拍照失败", PackIconKind.AlertCircle);
                }
            }
            catch (Exception ex)
            {
                CurrentProcessStatus = $"{module}号组件拍照异常";
                ShowMessage($"{module}号组件拍照异常: {ex.Message}", PackIconKind.AlertCircle);
                _logger.Error($"{module}号组件拍照异常: {ex.Message}");
            }
            finally
            {
                IsProcessRunning = false;
            }
        }
        // 装配组件命令
        private async Task ExecuteAssembleModuleCommand(int module)
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                CurrentProcessStatus = $"开始{module}号组件装配...";
                CurrentProcessModule = module;
                IsProcessRunning = true;

                bool success = await _assemblyStation.AssembleModule(module);

                if (success)
                {
                    CurrentProcessStatus = $"{module}号组件装配完成";
                    ShowMessage($"{module}号组件装配完成", PackIconKind.CheckCircle);

                    // 装配完成后可以通知其他系统
                    // await NotifyAssemblyComplete(module);
                }
                else
                {
                    CurrentProcessStatus = $"{module}号组件装配失败";
                    ShowMessage($"{module}号组件装配失败", PackIconKind.AlertCircle);
                }
            }
            catch (Exception ex)
            {
                CurrentProcessStatus = $"{module}号组件装配异常";
                ShowMessage($"{module}号组件装配异常: {ex.Message}", PackIconKind.AlertCircle);
                _logger.Error($"{module}号组件装配异常: {ex.Message}");
            }
            finally
            {
                IsProcessRunning = false;
            }
        }

        private void ExecuteStopProcessCommand()
        {
            try
            {
                _assemblyStation.StopAssemblyProcess();
                CurrentProcessStatus = "流程已停止";
                IsProcessRunning = false;
                ShowMessage("当前流程已停止", PackIconKind.StopCircle);
            }
            catch (Exception ex)
            {
                ShowMessage($"停止流程失败: {ex.Message}", PackIconKind.AlertCircle);
                _logger.Error($"停止流程失败: {ex.Message}");
            }
        }
        private async Task MoveToAxesUStandbyPosAction()
        {
            try
            {
                CurrentProcessStatus = "移动到U轴待机位置...";

                bool success = await Task.Run(() => _assemblyStation.MoveUAxisStandbyPos());

                if (success)
                {
                    CurrentProcessStatus = "已成功移动到U轴待机位置";
                }
                else
                {
                    CurrentProcessStatus = "移动到U轴待机位置失败";
                    ShowMessage("移动到U轴待机位置失败", PackIconKind.AlertCircle);
                }
            }
            catch (Exception ex)
            {
                CurrentProcessStatus = "移动到U轴待机位置异常";
                ShowMessage($"移动到U轴待机位置异常: {ex.Message}", PackIconKind.AlertCircle);
                _logger.Error($"移动到U轴待机位置异常: {ex.Message}");
            }
        }

        #endregion

        #region 单步控制方法
        // 单步控制方法
        private void StartSingleStepAction()
        {
            if (!_loginModel.HasPermission(Authority.Administrator))
            {
                ShowMessage($"操作需要 {Authority.Administrator} 权限");
                return;
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                _dialogService.ShowDialog("ConfirmationDialog", new DialogParameters
                {
                    { "title", "单步模式" },
                    { "message", "确认进入单步模式？" }
                }, async result =>
                {
                    if (result.Result == ButtonResult.Yes)
                    {
                        try
                        {
                            IsSingleStepMode = true;
                            CurrentStepDescription = "单步模式已启动";

                            // 调用工站的单步启动方法
                            _assemblyStation.StartSingleStepMode();

                            ShowMessage("单步模式已启动", PackIconKind.PlayCircle);
                        }
                        catch (Exception ex)
                        {
                            ShowMessage($"启动单步模式失败: {ex.Message}", PackIconKind.AlertCircle);
                            IsSingleStepMode = false;
                        }
                    }
                });
            });
        }

        private void ExecuteNextStep()
        {
            if (!IsStepWaiting) return;

            try
            {
                // 调用工站的下一步方法
                _assemblyStation.SingleStepNext();

                IsStepWaiting = false;
                NextStepCommand.RaiseCanExecuteChanged();

                ShowMessage("执行下一步", PackIconKind.SkipNext);
            }
            catch (Exception ex)
            {
                ShowMessage($"执行下一步失败: {ex.Message}", PackIconKind.AlertCircle);
            }
        }

        private void ExecuteStopSingleStep()
        {
            _dialogService.ShowDialog("ConfirmationDialog", new DialogParameters
            {
                { "title", "退出单步模式" },
                { "message", "确认退出单步模式？" }
            }, result =>
            {
                if (result.Result == ButtonResult.Yes)
                {
                    try
                    {
                        // 调用工站的停止单步方法
                        _assemblyStation.StopSingleStepMode();

                        IsSingleStepMode = false;
                        IsStepWaiting = false;
                        CurrentStepDescription = "单步模式已停止";
                        NextStepCommand.RaiseCanExecuteChanged();

                        ShowMessage("单步模式已停止", PackIconKind.StopCircle);
                    }
                    catch (Exception ex)
                    {
                        ShowMessage($"停止单步模式失败: {ex.Message}", PackIconKind.AlertCircle);
                    }
                }
            });
        }

        // 更新步骤状态的方法（由工站回调调用）
        public void UpdateStepStatus(string description, bool isWaiting)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentStepDescription = description;
                IsStepWaiting = isWaiting;
                NextStepCommand.RaiseCanExecuteChanged();
            });
        }
        /// <summary>
        /// 更新标定状态的方法（由工站回调调用）
        /// </summary>
        public void UpdateCalibrationStatus(string message, bool isWaiting)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CalibrationStatus = message;

                // 如果需要，可以更新其他相关的标定状态属性
                if (isWaiting)
                {
                    // 标定等待状态，可能需要启用某些按钮
                    IsCalibrationWaiting = true;
                }
                else
                {
                    // 标定进行中状态
                    IsCalibrationWaiting = false;
                }

                _logger.Info($"标定状态更新: {message}");
            });
        }
        #endregion

        #region 相机事件处理
        // 事件声明
        public event EventHandler<PhotoCompletedEventArgs> PhotoCompleted;
        public event EventHandler<CameraStatusChangedEventArgs> CameraStatusChanged;
        private void OnPhotoCompleted(object sender, PhotoCompletedEventArgs e)
        {
            // 在UI线程上更新属性
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (e.Success && !string.IsNullOrEmpty(e.Data))
                {
                    ParseOffsetValues(e.Data);
                    UpdateOffsetDisplay(e.CameraName);
                    //ShowMessage($"{e.CameraName}拍照成功: {e.Data}", PackIconKind.Camera);
                    _logger.Info($"{e.CameraName}拍照成功: {e.Data}");
                }
                else
                {
                    // 拍照失败时重置Offset值
                    ResetOffsetValues();
                    ShowMessage($"{e.CameraName}拍照失败: {e.ErrorMessage}", PackIconKind.AlertCircle);
                    _logger.Error($"{e.CameraName}拍照失败: {e.ErrorMessage}");
                }
            });
        }

        private void OnCameraStatusChanged(object sender, CameraStatusChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (e.CameraName.Contains("Side"))
                {
                    IsSideCameraConnected = e.IsConnected;
                    SideCameraStatus = e.Status;
                }
                else if (e.CameraName.Contains("Bottom"))
                {
                    IsBottomCameraConnected = e.IsConnected;
                    BottomCameraStatus = e.Status;
                }

                _logger.Info($"相机状态更新: {e.CameraName} - {e.Status}");
            });
        }
        // 解析Offset值
        private void ParseOffsetValues(string data)
        {
            try
            {
                // 1. 去掉所有不可见字符（零宽空格、BOM、回车、换行等）
                data = Regex.Replace(data, @"[\p{C}\p{Z}]", ""); // \p{C}=控制字符，\p{Z}=空白

                // 2. 正则提取 4 个数值
                var matchX = Regex.Match(data, @"offsetX=([+-]?\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
                var matchY = Regex.Match(data, @"offsetY=([+-]?\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
                var matchU = Regex.Match(data, @"offsetU=([+-]?\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
                var matchH = Regex.Match(data, @"offsetH=([+-]?\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);

                // 3. 解析并保留 3 位小数，失败则默认为 0
                OffsetX = Math.Round(matchX.Success && double.TryParse(matchX.Groups[1].Value, out double x) ? x : 0, 3);
                OffsetY = Math.Round(matchY.Success && double.TryParse(matchY.Groups[1].Value, out double y) ? y : 0, 3);
                OffsetU = Math.Round(matchU.Success && double.TryParse(matchU.Groups[1].Value, out double u) ? u : 0, 3);
                OffsetH = Math.Round(matchH.Success && double.TryParse(matchH.Groups[1].Value, out double h) ? h : 0, 3);

                _logger.Info($"解析Offset值: X={OffsetX:F3}, Y={OffsetY:F3}, U={OffsetU:F3}, H={OffsetH:F3}");
            }
            catch (Exception ex)
            {
                _logger.Error($"解析Offset值失败: {ex.Message}");
                OffsetX = 0;
                OffsetY = 0;
                OffsetU = 0;
                OffsetH = 0;
            }
        }
        // 更新Offset显示
        private void UpdateOffsetDisplay(string cameraName)
        {
            _logger.Info($"{cameraName} Offset值更新 - X: {OffsetX:F3}, Y: {OffsetY:F3}, U: {OffsetU:F3}, H: {OffsetH:F3}");
        }

        // 重置Offset值
        public void ResetOffsetValues()
        {
            OffsetX = 0;
            OffsetY = 0;
        }
        #endregion

        #region 相机控制方法

        private async void TakePhotoAction(string cameraType)
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                CurrentProcessStatus = $"开始{cameraType}相机拍照...";
                IsProcessRunning = true;

                bool success = await _assemblyStation.TakePhotoForModule(CurrentProcessModule);

                if (success)
                {
                    CurrentProcessStatus = $"{cameraType}相机拍照完成";
                    ShowMessage($"{cameraType}相机拍照完成", PackIconKind.Camera);
                }
                else
                {
                    CurrentProcessStatus = $"{cameraType}相机拍照失败";
                    ShowMessage($"{cameraType}相机拍照失败", PackIconKind.AlertCircle);
                }
            }
            catch (Exception ex)
            {
                CurrentProcessStatus = $"{cameraType}相机拍照异常";
                ShowMessage($"{cameraType}相机拍照异常: {ex.Message}", PackIconKind.AlertCircle);
                _logger.Error($"{cameraType}相机拍照异常: {ex.Message}");
            }
            finally
            {
                IsProcessRunning = false;
            }
        }

        private async void GetCameraStatusAction(string cameraType)
        {
            try
            {
                string status = await GetCameraStatusAsync(cameraType);

                if (cameraType == "Side")
                {
                    SideCameraStatus = status;
                }
                else
                {
                    BottomCameraStatus = status;
                }

                ShowMessage($"{cameraType}相机状态: {status}", PackIconKind.InfoCircle);
            }
            catch (Exception ex)
            {
                ShowMessage($"获取{cameraType}相机状态失败: {ex.Message}", PackIconKind.AlertCircle);
            }
        }
        public async Task<string> GetCameraStatusAsync(string cameraType)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 根据相机类型获取状态
                    switch (cameraType.ToLower())
                    {
                        case "side":
                            return IsSideCameraConnected ? "已连接" : "未连接";
                        case "bottom":
                            return IsBottomCameraConnected ? "已连接" : "未连接";
                        case "top":
                            return IsTopCameraConnected ? "已连接" : "未连接";
                        default:
                            return "未知相机类型";
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"获取相机状态失败: {ex.Message}");
                    return $"错误: {ex.Message}";
                }
            });
        }
        private async Task ExecuteTakePhotoCommand(string cameraType)
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                CurrentProcessStatus = $"开始{cameraType}相机拍照...";
                IsProcessRunning = true;

                bool success = await _assemblyStation.TakePhoto(cameraType, CurrentProcessModule);

                if (success)
                {
                    CurrentProcessStatus = $"{cameraType}相机拍照完成";
                    ShowMessage($"{cameraType}相机拍照完成", PackIconKind.Camera);
                }
                else
                {
                    CurrentProcessStatus = $"{cameraType}相机拍照失败";
                    ShowMessage($"{cameraType}相机拍照失败", PackIconKind.AlertCircle);
                }
            }
            catch (Exception ex)
            {
                CurrentProcessStatus = $"{cameraType}相机拍照异常";
                ShowMessage($"{cameraType}相机拍照异常: {ex.Message}", PackIconKind.AlertCircle);
                _logger.Error($"{cameraType}相机拍照异常: {ex.Message}");
            }
            finally
            {
                IsProcessRunning = false;
            }
        }

        private async Task ExecuteGetCameraStatusCommand(string cameraType)
        {
            try
            {
                string status = await GetCameraStatusAsync(cameraType);

                // 根据相机类型更新状态
                switch (cameraType.ToLower())
                {
                    case "side":
                        SideCameraStatus = status;
                        break;
                    case "bottom":
                        BottomCameraStatus = status;
                        break;
                    case "top":
                        TopCameraStatus = status;
                        break;
                }

                ShowMessage($"{cameraType}相机状态: {status}", PackIconKind.InfoCircle);
            }
            catch (Exception ex)
            {
                ShowMessage($"获取{cameraType}相机状态失败: {ex.Message}", PackIconKind.AlertCircle);
            }
        }

        #endregion

        #region 相机标定命令实现

        // 更新开始标定方法
        private async void StartCalibrationAction()
        {
            if (!_loginModel.HasPermission(Authority.Administrator))
            {
                ShowMessage($"操作需要 {Authority.Administrator} 权限");
                return;
            }

            try
            {
                // 获取当前配置（包含坐标轴方向）
                string key = GetCurrentCalibrationKey();
                if (!_calibrationConfigs.ContainsKey(key))
                {
                    ShowMessage("标定配置未初始化", PackIconKind.AlertCircle);
                    return;
                }

                var config = _calibrationConfigs[key];

                // 记录开始标定的配置信息
                _logger.Info($"开始标定 - 相机: {CurrentCameraInfo}, " +
                            $"标定类型: {CurrentCalibrationType}, " +
                            $"坐标轴方向: {AxisDirectionInfo}, " +
                            $"起始点: ({CalibrationStartX}, {CalibrationStartY})");

                // 使用增强的标定方法（考虑坐标轴方向）
                bool success = await _assemblyStation.StartCalibrationWithDirection(config);
                if (success)
                {
                    IsCalibrationRunning = true;
                    CurrentCalibrationPointIndex = 0;
                    CalibrationProgress = 0;
                    UpdateCalibrationPointsFromStation();

                    ShowMessage($"标定已启动 - {CurrentCameraInfo} - {AxisDirectionInfo}", PackIconKind.Camera);
                }
                else
                {
                    ShowMessage("启动标定失败", PackIconKind.AlertCircle);
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"启动标定失败: {ex.Message}", PackIconKind.AlertCircle);
                IsCalibrationRunning = false;
            }
        }
        private async void NextCalibrationPointAction()
        {
            if (!IsCalibrationRunning) return;

            try
            {
                // 移动到下一个标定点
                bool moveSuccess = await _assemblyStation.MoveToNextCalibrationPoint();
                if (moveSuccess)
                {
                    CurrentCalibrationPointIndex = _assemblyStation.CalibrationPoints
                        .FindIndex(p => p.Status == "待标定");

                    if (CurrentCalibrationPointIndex >= 0)
                    {
                        CalibrationProgress = (CurrentCalibrationPointIndex * 100.0) / _assemblyStation.CalibrationPoints.Count;
                        UpdateCalibrationPointsFromStation();
                    }
                    else
                    {
                        // 标定完成
                        IsCalibrationRunning = false;
                        CalibrationProgress = 100;
                        ShowMessage("标定完成", PackIconKind.CheckCircle);
                    }
                }
                else
                {
                    ShowMessage("移动到下一个标定点失败", PackIconKind.AlertCircle);
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"标定操作失败: {ex.Message}", PackIconKind.AlertCircle);
            }
        }

        private void ExecuteStopCalibration()
        {
            try
            {
                _assemblyStation.StopCalibration();
                IsCalibrationRunning = false;
                CalibrationStatus = "标定已停止";
                CalibrationProgress = 0;
                ShowMessage("标定已停止", PackIconKind.StopCircle);
            }
            catch (Exception ex)
            {
                ShowMessage($"停止标定失败: {ex.Message}", PackIconKind.AlertCircle);
            }
        }

        private void SaveCalibrationPointsAction()
        {
            if (!_loginModel.HasPermission(Authority.Administrator))
            {
                ShowMessage($"操作需要 {Authority.Administrator} 权限");
                return;
            }

            try
            {
                // 选择保存路径
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "JSON文件 (*.json)|*.json",
                    FileName = GetCalibrationFileName()
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    bool success = _assemblyStation.SaveCalibrationData(saveFileDialog.FileName);
                    if (success)
                    {
                        ShowMessage("标定数据保存成功", PackIconKind.CheckCircle);
                    }
                    else
                    {
                        ShowMessage("标定数据保存失败", PackIconKind.AlertCircle);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"保存标定数据失败: {ex.Message}", PackIconKind.AlertCircle);
            }
        }

        private void LoadCalibrationPointsAction()
        {
            if (!_loginModel.HasPermission(Authority.Administrator))
            {
                ShowMessage($"操作需要 {Authority.Administrator} 权限");
                return;
            }

            try
            {
                // 选择加载文件
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "JSON文件 (*.json)|*.json"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    bool success = _assemblyStation.LoadCalibrationData(openFileDialog.FileName);
                    if (success)
                    {
                        UpdateCalibrationPointsFromStation();
                        ShowMessage("标定数据加载成功", PackIconKind.CheckCircle);
                    }
                    else
                    {
                        ShowMessage("标定数据加载失败", PackIconKind.AlertCircle);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"加载标定数据失败: {ex.Message}", PackIconKind.AlertCircle);
            }
        }

        #endregion

        #region 自动标定方法

        private async void StartAutoCalibrationAction()
        {
            if (!_loginModel.HasPermission(Authority.Administrator))
            {
                ShowMessage($"操作需要 {Authority.Administrator} 权限");
                return;
            }

            try
            {
                // 获取当前配置
                string key = GetCurrentCalibrationKey();
                if (!_calibrationConfigs.ContainsKey(key))
                {
                    ShowMessage("标定配置未初始化", PackIconKind.AlertCircle);
                    return;
                }

                var config = _calibrationConfigs[key];

                // 记录开始自动标定
                _logger.Info($"开始自动标定 - 相机: {CurrentCameraInfo}, " +
                            $"标定类型: {CurrentCalibrationType}, " +
                            $"延时: {CalibrationDelayMs}ms");

                // 启动自动标定
                bool success = await _assemblyStation.StartAutoCalibration(config, CalibrationDelayMs);
                if (success)
                {
                    IsAutoCalibrationRunning = true;
                    IsCalibrationRunning = true;
                    AutoCalibrationStatus = "自动标定运行中";

                    ShowMessage($"自动标定已启动 - {CurrentCameraInfo}", PackIconKind.AutoFix);
                }
                else
                {
                    ShowMessage("启动自动标定失败", PackIconKind.AlertCircle);
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"启动自动标定失败: {ex.Message}", PackIconKind.AlertCircle);
                IsAutoCalibrationRunning = false;
                IsCalibrationRunning = false;
            }
        }

        private async void ResumeAutoCalibrationAction()
        {
            try
            {
                bool success = await _assemblyStation.ResumeAutoCalibration();
                if (success)
                {
                    IsAutoCalibrationRunning = true;
                    IsCalibrationRunning = true;
                    AutoCalibrationStatus = "自动标定运行中";
                    ShowMessage("自动标定已恢复", PackIconKind.PlayCircle);
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"恢复自动标定失败: {ex.Message}", PackIconKind.AlertCircle);
            }
        }

        private void ExecuteStopAutoCalibration()
        {
            try
            {
                _assemblyStation.StopAutoCalibration();
                IsAutoCalibrationRunning = false;
                IsCalibrationRunning = false;
                AutoCalibrationStatus = "自动标定已停止";
                ShowMessage("自动标定已停止", PackIconKind.StopCircle);
            }
            catch (Exception ex)
            {
                ShowMessage($"停止自动标定失败: {ex.Message}", PackIconKind.AlertCircle);
            }
        }

        private void ExecutePauseAutoCalibration()
        {
            try
            {
                _assemblyStation.PauseAutoCalibration();
                IsAutoCalibrationRunning = false;
                AutoCalibrationStatus = "自动标定已暂停";
                ShowMessage("自动标定已暂停", PackIconKind.PauseCircle);
            }
            catch (Exception ex)
            {
                ShowMessage($"暂停自动标定失败: {ex.Message}", PackIconKind.AlertCircle);
            }
        }

        // 更新自动标定状态回调
        public void UpdateAutoCalibrationStatus(string message, int currentPoint, int totalPoints, bool isRunning)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                AutoCalibrationStatus = message;
                CurrentCalibrationPointIndex = currentPoint;

                // 计算进度
                if (totalPoints > 0)
                {
                    CalibrationProgress = (currentPoint * 100.0) / totalPoints;
                }

                IsAutoCalibrationRunning = isRunning;
                IsCalibrationRunning = isRunning;

                _logger.Info($"自动标定状态: {message}");
            });
        }

        #endregion

        #region 标定辅助方法

        /// <summary>
        /// 从工站更新标定点列表
        /// </summary>
        private void UpdateCalibrationPointsFromStation()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CalibrationPoints.Clear();
                foreach (var point in _assemblyStation.CalibrationPoints)
                {
                    CalibrationPoints.Add(new CalibrationPoint
                    {
                        Index = point.Index,
                        MachineX = point.MachineX,
                        MachineY = point.MachineY,
                        PixelX = point.PixelX,
                        PixelY = point.PixelY,
                        Status = point.Status,
                        //StatusColor = GetStatusColor(point.Status)
                    });
                }
            });
        }
        /// <summary>
        /// 获取状态对应的颜色
        /// </summary>
        private string GetStatusColor(string status)
        {
            return status switch
            {
                "已标定" => "Green",
                "待标定" => "Gray",
                "标定中" => "Orange",
                _ => "Gray"
            };
        }

        /// <summary>
        /// 获取标定文件名
        /// </summary>
        private string GetCalibrationFileName()
        {
            string cameraType = IsSideCamera ? "Side" : "Bottom";
            string pointCount = Is9PointCalibration ? "9" : "14";
            return $"Calibration_{cameraType}_{pointCount}Points_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        }

        /// <summary>
        /// 从对话框获取像素坐标
        /// </summary>
        private async Task<System.Windows.Point?> GetPixelCoordinatesFromDialog()
        {
            try
            {
                var tcs = new TaskCompletionSource<System.Windows.Point?>();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var dialog = new PixelCoordinateDialog();
                    bool? result = dialog.ShowDialog();

                    if (result == true)
                    {
                        tcs.SetResult(new System.Windows.Point(Convert.ToDouble(dialog.PixelX), Convert.ToDouble(dialog.PixelY)));
                    }
                    else
                    {
                        tcs.SetResult(null);
                    }
                });

                return await tcs.Task;
            }
            catch (Exception ex)
            {
                _logger.Error($"显示像素坐标对话框时出错: {ex.Message}");
                return null;
            }
        }
        // 初始化标定配置
        private void InitializeCalibrationConfigs()
        {
            // 侧相机配置
            _calibrationConfigs["Side_9Point"] = new EnhancedCalibrationConfig
            {
                CameraType = "Side",
                Is9PointCalibration = true,
                IsXAxisReversed = false,
                IsYAxisReversed = false,
                StartX = 100,
                StartY = 100,
                Spacing = 50,
                RotationRadius = 75
            };

            _calibrationConfigs["Side_14Point"] = new EnhancedCalibrationConfig
            {
                CameraType = "Side",
                Is9PointCalibration = false,
                IsXAxisReversed = false,
                IsYAxisReversed = false,
                StartX = 100,
                StartY = 100,
                Spacing = 50,
                RotationRadius = 75
            };

            // 下相机配置
            _calibrationConfigs["Bottom_9Point"] = new EnhancedCalibrationConfig
            {
                CameraType = "Bottom",
                Is9PointCalibration = true,
                IsXAxisReversed = false,
                IsYAxisReversed = true, // 下相机通常Y轴方向相反
                StartX = 100,
                StartY = 100,
                Spacing = 50,
                RotationRadius = 75
            };

            _calibrationConfigs["Bottom_14Point"] = new EnhancedCalibrationConfig
            {
                CameraType = "Bottom",
                Is9PointCalibration = false,
                IsXAxisReversed = false,
                IsYAxisReversed = true, // 下相机通常Y轴方向相反
                StartX = 100,
                StartY = 100,
                Spacing = 50,
                RotationRadius = 75
            };

            // 初始化标定点数据
            foreach (var config in _calibrationConfigs)
            {
                _calibrationPointsData[config.Key] = new ObservableCollection<CalibrationPoint>();
            }
        }

        // 更新当前标定配置
        private void UpdateCurrentCalibrationConfig()
        {
            string key = GetCurrentCalibrationKey();
            if (_calibrationConfigs.ContainsKey(key))
            {
                var config = _calibrationConfigs[key];
                config.IsXAxisReversed = IsXAxisReversed;
                config.IsYAxisReversed = IsYAxisReversed;
                config.StartX = CalibrationStartX;
                config.StartY = CalibrationStartY;
                config.Spacing = CalibrationSpacing;
                config.RotationRadius = CalibrationRotationRadius;

                // 更新到工站
                _assemblyStation.UpdateCalibrationConfig(config);

                // 刷新标定点显示
                UpdateCalibrationPointsFromStation();
            }
        }

        // 当相机类型或标定类型改变时更新配置
        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.PropertyName == nameof(IsSideCamera) ||
                e.PropertyName == nameof(IsBottomCamera) ||
                e.PropertyName == nameof(Is9PointCalibration) ||
                e.PropertyName == nameof(Is14PointCalibration))
            {
                UpdateCurrentConfiguration();
                UpdateCameraInfo(); // 切换相机时更新相机信息
            }
            else if (e.PropertyName == nameof(CalibrationStartX) ||
                     e.PropertyName == nameof(CalibrationStartY) ||
                     e.PropertyName == nameof(CalibrationSpacing) ||
                     e.PropertyName == nameof(CalibrationRotationRadius))
            {
                UpdateCurrentCalibrationConfig();
                RaisePropertyChanged(nameof(AxisDirectionInfo)); // 更新方向信息显示
            }
        }

        // 更新当前配置
        private void UpdateCurrentConfiguration()
        {
            // 通知标定点集合变化
            RaisePropertyChanged(nameof(CalibrationPoints));

            // 更新方向设置到当前配置
            string key = GetCurrentCalibrationKey();
            if (_calibrationConfigs.ContainsKey(key))
            {
                var config = _calibrationConfigs[key];
                IsXAxisReversed = config.IsXAxisReversed;
                IsYAxisReversed = config.IsYAxisReversed;
                CalibrationStartX = config.StartX;
                CalibrationStartY = config.StartY;
                CalibrationSpacing = config.Spacing;
                CalibrationRotationRadius = config.RotationRadius;
            }

            // 切换到工站的对应配置
            string cameraType = IsSideCamera ? "Side" : "Bottom";
            _assemblyStation.SwitchCalibrationConfig(cameraType, Is9PointCalibration);
        }
        #endregion

        #region 实现接口

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            StartPositionRefresh();
        }
        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            StopPositionRefresh();
        }
        #endregion


        // 析构函数，确保资源释放
        ~AssemblyStationViewModel()
        {
            StopPositionRefresh();
            _positionTimer?.Stop();
            _positionTimer = null;
        }

    }
}
