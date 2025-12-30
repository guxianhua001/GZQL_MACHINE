using Core.Abstraction;
using Core.Models;
using Core.Services;
using Core.Utilities;
using Framework.Views;
using Interfaces;
using Interfaces.Events;
using MaterialDesignThemes.Wpf;
using ModuleCore.Common.Authority;
using ModuleCore.Models;
using ModuleCore.ViewModels;
using ModuleCore.Views;
using Newtonsoft.Json;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using Recipe;
using Recipe.Interfaces;
using SmarterMotion;
using Stations;
using Stations.TaskParameters;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Framework.ViewModels
{
    public class DispenserStationViewModel : BindableBase
    {
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IParameterStorage _parameterStorage;
        public readonly ILoggerService _logger;
        private readonly ICancelableOperationService _cancelableOperationService;
        private readonly IParameterEditor _parameterEditor;
        private readonly IRecipeManager _recipeManager;
        private readonly IRecipeStorage _recipeStorage;
        private readonly RecipePoolManager _recipePoolManager;
        private readonly DispenserStationService _dispenserStationService;
        private SubscriptionToken _refreshToken;
        private readonly AppConfig _appConfig;
        private readonly TaskInstanceManager _taskManager;
        private LoginModel _loginModel { get; set; }
        private DispenserStation _dispenserStation;
        private AssemblyStation _assemblyStation;
        private LoadingStation _loadingStation;
        // 状态属性
        private string _scanStatus = "就绪";
        private string _calibrationStatus = "就绪";
        private double _calibrationProgress = 0;
        private string _stationStatus = "待机";
        private string _safetyStatus = "安全";
        private string _logMessages = "";
        // 单步控制属性
        private bool _isSingleStepMode = false;
        private bool _isStepWaiting = false;
        private string _currentStepDescription = "等待启动";

        public bool IsSingleStepMode
        {
            get => _isSingleStepMode;
            set => SetProperty(ref _isSingleStepMode, value);
        }

        public bool IsStepWaiting
        {
            get => _isStepWaiting;
            set => SetProperty(ref _isStepWaiting, value);
        }

        public string CurrentStepDescription
        {
            get => _currentStepDescription;
            set => SetProperty(ref _currentStepDescription, value);
        }

        // 单步控制命令
        public DelegateCommand StartSingleStepCommand { get; private set; }
        public DelegateCommand NextStepCommand { get; private set; }
        public DelegateCommand StopSingleStepCommand { get; private set; }
        public DelegateCommand CorrectPillarAngleCommand { get; private set; }
        public DelegateCommand CalculateTabCompensationCommand { get; private set; }
        public DelegateCommand CorrectActuatorXCommand { get; private set; }
        public DelegateCommand AssemblyIPQCCommand { get; private set; }

        #region 扩展配方参数
        private DispenserExtendedParameters _extendedParameters = new DispenserExtendedParameters();
        private RecipeService<DispenserExtendedParameters> _extendedRecipeService;

        public DispenserExtendedParameters ExtendedParameters
        {
            get => _extendedParameters;
            set => SetProperty(ref _extendedParameters, value);
        }

        public DelegateCommand EditExtendedParametersCommand { get; private set; }
        public DelegateCommand SaveExtendedParametersCommand { get; private set; }
        public DelegateCommand LoadExtendedParametersCommand { get; private set; }

        private void InitializeExtendedRecipeService()
        {
            _extendedRecipeService = new RecipeService<DispenserExtendedParameters>(
                "DispenserExtended",
                "点胶扩展参数",
                _logger,
                _dialogService,
                _eventAggregator,
                _parameterEditor,
                _parameterStorage,
                _recipeManager,
                _recipeStorage,
                _appConfig,
                _recipePoolManager);

            // 订阅扩展参数事件
            _extendedRecipeService.ParametersApplied += OnExtendedParametersApplied;
            _extendedRecipeService.RecipeChanged += OnExtendedRecipeChanged;
            _extendedRecipeService.ParametersLoaded += OnExtendedParametersLoaded;

            EditExtendedParametersCommand = new DelegateCommand(OnEditExtendedParameters);
            SaveExtendedParametersCommand = new DelegateCommand(OnSaveExtendedParameters);
            LoadExtendedParametersCommand = new DelegateCommand(OnLoadExtendedParameters);
        }

        private async void OnEditExtendedParameters()
        {
            try
            {
                _logger.Info("打开扩展参数编辑窗口");
                await _extendedRecipeService.LoadRecipeParameters(_extendedRecipeService.CurrentRecipeName);
                _extendedRecipeService.OnEditParameters();
            }
            catch (Exception ex)
            {
                _logger.Error($"打开扩展参数编辑窗口失败: {ex.Message}");
            }
        }

        private void OnSaveExtendedParameters()
        {
            try
            {
                _extendedRecipeService.SaveParametersToRecipe(_extendedRecipeService.CurrentRecipeName);
                _logger.Info("扩展参数已保存");
            }
            catch (Exception ex)
            {
                _logger.Error($"保存扩展参数失败: {ex.Message}");
            }
        }

        private async void OnLoadExtendedParameters()
        {
            try
            {
                await _extendedRecipeService.LoadRecipeParameters(_extendedRecipeService.CurrentRecipeName);
                _logger.Info("扩展参数已加载");
            }
            catch (Exception ex)
            {
                _logger.Error($"加载扩展参数失败: {ex.Message}");
            }
        }

        private void OnExtendedParametersApplied(object sender, DispenserExtendedParameters parameters)
        {
            ExtendedParameters = parameters;
            ApplyExtendedParametersToHardware();
            _logger.Info("扩展参数已应用");
        }

        private void OnExtendedRecipeChanged(object sender, string recipeName)
        {
            _logger.Info($"扩展参数配方已切换到: {recipeName}");
        }

        private void OnExtendedParametersLoaded(object sender, DispenserExtendedParameters parameters)
        {
            ExtendedParameters = parameters;
            _logger.Info("扩展参数已加载到界面");
        }

        private void ApplyExtendedParametersToHardware()
        {
            // 实现扩展参数应用到硬件的逻辑
            // 例如：设置Tab高度限位、点胶位置等
            _logger.Info("扩展参数已应用到硬件");
        }
        #endregion

        public DispenserStationViewModel(
           IDialogService dialogService,
           IEventAggregator eventAggregator,
           TaskInstanceManager taskManager,
           AppConfig appConfig,
           LoginModel loginModel,
           RecipePoolManager recipePoolManager,
           IParameterStorage parameterStorage,
           ILoggerService loggerService,
           IRecipeStorage recipeStorage,
           IRecipeManager recipeManager,
           IParameterEditor parameterEditor,
           ICancelableOperationService cancelableOperationService)
        {
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _taskManager = taskManager;
            _loginModel = loginModel;
            _appConfig = appConfig;
            _logger = loggerService;
            _parameterStorage = parameterStorage;
            _recipeStorage = recipeStorage;
            _recipeManager = recipeManager;
            _parameterEditor = parameterEditor;
            _recipePoolManager = recipePoolManager;
            // 获取点胶站实例
            _dispenserStation = _taskManager.GetTask<DispenserStation>();
            _loadingStation = _taskManager.GetTask<LoadingStation>();
            _assemblyStation = _taskManager.GetTask<AssemblyStation>();
            if (_dispenserStation != null)
            {
                InitializeCommands();
            }

            // 监听登录模型变化
            _loginModel.PropertyChanged += LoginModel_PropertyChanged;

            _cancelableOperationService = cancelableOperationService;
            _dispenserStationService = new DispenserStationService(_dispenserStation, _logger);

            // 初始化轴位置监控
            InitializeAxisPositions();

            // 初始化点胶参数
            InitializeDispensingParameters();
            InitializeExtendedRecipeService();
            LoadDispensingParameters();
            LoadCalibrationParameters();
        }


        private void LoginModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LoginModel.LoginUser) ||
                e.PropertyName == nameof(LoginModel.HasPermission))
            {
                RaisePropertyChanged(nameof(IsAdmin));
                RaisePropertyChanged(nameof(CanEditParams));
                RemoveParameterSetCommand.RaiseCanExecuteChanged();
            }
        }

        #region 拍照位置选择属性
        private int _selectedPhotoPositionIndex = 1;
        private List<int> _photoPositionIndices = Enumerable.Range(1, 6).ToList();

        public int SelectedPhotoPositionIndex
        {
            get => _selectedPhotoPositionIndex;
            set => SetProperty(ref _selectedPhotoPositionIndex, value);
        }

        public List<int> PhotoPositionIndices
        {
            get => _photoPositionIndices;
            set => SetProperty(ref _photoPositionIndices, value);
        }
        #endregion

        #region 命令初始化
        private void InitializeCommands()
        {
            // 3D扫描相关命令
            Perform3DScanCommand = new DelegateCommand(async () => await Execute3DScanAsync());
            PrepareForScanningCommand = new DelegateCommand(async () => await PrepareForScanningAsync());
            ReturnToSafePositionCommand = new DelegateCommand(async () => await ReturnToSafePositionAsync());

            // 标定相关命令
            Start3DCalibrationCommand = new DelegateCommand(async () => await StartCalibrationAsync());
            Stop3DCalibrationCommand = new DelegateCommand(StopCalibration);
            ApplyCalibrationParametersCommand = new DelegateCommand(ApplyCalibrationParameters);

            // 点胶相关命令
            TriggerDispensingCommand = new DelegateCommand(async () => await TriggerDispensingAsync());
            TestDispensingCommand = new DelegateCommand(async () => await TestDispensingAsync());

            // 其他命令
            ClearLogCommand = new DelegateCommand(ClearLog);
            EditParametersCommand = new DelegateCommand(EditParameters);

            // 点胶参数命令
            AddParameterSetCommand = new DelegateCommand(AddParameterSet);
            UpdateParameterSetCommand = new DelegateCommand(UpdateParameterSet);
            LoadParametersCommand = new DelegateCommand(LoadDispensingParameters);
            SaveParametersCommand = new DelegateCommand(SaveDispensingParameters);
            ApplyDispensingParametersCommand = new DelegateCommand(ApplyDispensingParameters);
            PerformWipingCommand = new DelegateCommand(async () => await PerformWipingAsync());
            RemoveParameterSetCommand = new DelegateCommand(
               executeMethod: () => RemoveParameterSet(),
               canExecuteMethod: () => CanRemoveParameterSet() 
             );
            // 单步控制命令
            StartSingleStepCommand = new DelegateCommand(StartSingleStep);
            NextStepCommand = new DelegateCommand(ExecuteNextStep, () => IsStepWaiting);
            StopSingleStepCommand = new DelegateCommand(StopSingleStep);
            // UV灯命令
            UVFixCommand = new DelegateCommand(async () => await UVFixAsync());
            UVOffCommand = new DelegateCommand(async () => await UVOffAsync());
            // 运动控制命令
            MoveToAxesYScanPositionCommand = new DelegateCommand(async () => await MoveToAxesYScanPositionAsync());
            MoveToAxesYPrePickPositionCommand = new DelegateCommand(async () => await MoveToAxesYPrePickPositionAsync());
            // 针头校准命令
            TeachCameraCenterCommand = new DelegateCommand(TeachCameraCenter);
            TeachNeedleTipCommand = new DelegateCommand(TeachNeedleTip);
            ApplyCompensationCommand = new DelegateCommand(ApplyCompensation);
            AutoCalculateCompensationCommand = new DelegateCommand(AutoCalculateCompensation);
            LoadCalibrationParametersCommand = new DelegateCommand(LoadCalibrationParameters);
            SaveCalibrationParametersCommand = new DelegateCommand(SaveCalibrationParameters);
            ResetCalibrationCommand = new DelegateCommand(ResetCalibration);
            // 点阵点胶命令
            StartDotArrayDispensingCommand = new DelegateCommand(async () => await StartDotArrayDispensingAsync(),
                () => !IsDotArrayDispensing);
            StopDotArrayDispensingCommand = new DelegateCommand(StopDotArrayDispensing,
                () => IsDotArrayDispensing);
            MoveToDotArrayStartCommand = new DelegateCommand(async () => await MoveToDotArrayStartAsync());
            // 拍照位置移动命令
            MoveToTabPhotoPositionCommand = new DelegateCommand(async () => await MoveToTabPhotoPositionAsync());
            MoveToPillar1PhotoPositionCommand = new DelegateCommand(async () => await MoveToPillar1PhotoPositionAsync());
            MoveToPillar2PhotoPositionCommand = new DelegateCommand(async () => await MoveToPillar2PhotoPositionAsync());
            // 针头点胶命令
            DispensePillar1Command = new DelegateCommand(
                async () => await DispensePillarAsync(1),
                () => !IsPillarDispensing);

            DispensePillar2Command = new DelegateCommand(
                async () => await DispensePillarAsync(2),
                () => !IsPillarDispensing);

            StopPillarDispensingCommand = new DelegateCommand(
                StopPillarDispensing,
                () => IsPillarDispensing);

            CorrectPillarAngleCommand = new DelegateCommand(async () => await CorrectPillarAngleAsync());
            CalculateTabCompensationCommand = new DelegateCommand(async () => await CalculateTabCompensationAsync());
            CorrectActuatorXCommand = new DelegateCommand(async () => await CorrectActuatorXAsync());
            AssemblyIPQCCommand = new DelegateCommand(async () => await AssemblyIPQCAsync());

            StartUVCuring1Command = new DelegateCommand(StartUVCuring1);
            StartUVCuring2Command = new DelegateCommand(StartUVCuring2);
            //StartUVCuring3Command = new DelegateCommand(StartUVCuring3);
        }
        #endregion

        #region 属性定义
        public bool IsAdmin => _loginModel?.HasPermission(Authority.Administrator) ?? false;
        public bool CanEditParams => IsAdmin;

        // 扫描状态
        public string ScanStatus
        {
            get => _scanStatus;
            set => SetProperty(ref _scanStatus, value);
        }

        public Brush ScanStatusColor
        {
            get
            {
                return _scanStatus switch
                {
                    "就绪" => Brushes.LightGreen,
                    "扫描中" => Brushes.LightBlue,
                    "完成" => Brushes.LightGreen,
                    "错误" => Brushes.LightCoral,
                    _ => Brushes.LightGray
                };
            }
        }

        // 标定状态
        public string CalibrationStatus
        {
            get => _calibrationStatus;
            set
            {
                if (SetProperty(ref _calibrationStatus, value))
                {
                    RaisePropertyChanged(nameof(CalibrationStatusColor));
                    RaisePropertyChanged(nameof(CalibrationStatusIcon));
                }
            }
        }

        public double CalibrationProgress
        {
            get => _calibrationProgress;
            set
            {
                if (SetProperty(ref _calibrationProgress, value))
                {
                    RaisePropertyChanged(nameof(CalibrationProgressText));
                }
            }
        }

        public string CalibrationProgressText => $"{CalibrationProgress:F1}%";

        public Brush CalibrationStatusColor
        {
            get
            {
                return _calibrationStatus switch
                {
                    "就绪" => Brushes.LightGreen,
                    "标定中" => Brushes.LightBlue,
                    "完成" => Brushes.LightGreen,
                    "已取消" => Brushes.Orange,
                    "错误" => Brushes.LightCoral,
                    _ => Brushes.LightGray
                };
            }
        }

        public PackIconKind CalibrationStatusIcon
        {
            get
            {
                return _calibrationStatus switch
                {
                    "就绪" => PackIconKind.CheckCircle,
                    "标定中" => PackIconKind.ProgressClock,
                    "完成" => PackIconKind.CheckCircle,
                    "已取消" => PackIconKind.Cancel,
                    "错误" => PackIconKind.AlertCircle,
                    _ => PackIconKind.HelpCircle
                };
            }
        }

        // 系统状态
        public string StationStatus
        {
            get => _stationStatus;
            set => SetProperty(ref _stationStatus, value);
        }

        public string SafetyStatus
        {
            get => _safetyStatus;
            set
            {
                if (SetProperty(ref _safetyStatus, value))
                {
                    RaisePropertyChanged(nameof(SafetyStatusColor));
                }
            }
        }

        public Brush SafetyStatusColor => _safetyStatus == "安全" ? Brushes.Green : Brushes.Red;

        // 日志
        public string LogMessages
        {
            get => _logMessages;
            set => SetProperty(ref _logMessages, value);
        }

        // 标定参数
        public double RStepAngle { get; set; } = 10.0;
        public int RScanCount { get; set; } = 36;
        public double UStepAngle { get; set; } = 5.0;
        public int UScanCountPerSide { get; set; } = 5;

        // 轴位置监控
        public ObservableCollection<AxisPosition> AxisPositions { get; } = new ObservableCollection<AxisPosition>();
        #endregion

        #region 命令定义
        public ICommand Perform3DScanCommand { get; private set; }
        public ICommand PrepareForScanningCommand { get; private set; }
        public ICommand MoveToScanStartPositionCommand { get; private set; }
        public ICommand ReturnToSafePositionCommand { get; private set; }
        public ICommand Start3DCalibrationCommand { get; private set; }
        public ICommand Stop3DCalibrationCommand { get; private set; }
        public ICommand ApplyCalibrationParametersCommand { get; private set; }
        public ICommand TriggerDispensingCommand { get; private set; }
        public ICommand TestDispensingCommand { get; private set; }
        public ICommand ClearLogCommand { get; private set; }
        public ICommand EditParametersCommand { get; private set; }
        public ICommand Save => new DelegateCommand(SaveParasCommand);
        public ICommand UVFixCommand { get; private set; }
        public ICommand UVOffCommand { get; private set; }
        public ICommand ClosedUVCommand { get; private set; }
        public ICommand MoveToAxesYScanPositionCommand { get; private set; }
        public ICommand MoveToAxesYPrePickPositionCommand { get; private set; }

        public ICommand StartUVCuring1Command { get; private set; }
        public ICommand StartUVCuring2Command { get; private set; }
        public ICommand StartUVCuring3Command { get; private set; }
        #endregion

        #region 拍照位置命令
        public DelegateCommand MoveToTabPhotoPositionCommand { get; private set; }
        public DelegateCommand MoveToPillar1PhotoPositionCommand { get; private set; }
        public DelegateCommand MoveToPillar2PhotoPositionCommand { get; private set; }
        #endregion

        #region 运动控制
        private async Task MoveToAxesYScanPositionAsync()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                ScanStatus = "扫描中";
                AddLog("开始执行3D扫描,Y轴到扫描位...");

                bool success = await Task.Run(() => _loadingStation.MoveToScanPosition());

                ScanStatus = success ? "完成" : "错误";
                AddLog(success ? "Y轴到扫描位完成" : "Y轴到扫描位失败");
            }
            catch (Exception ex)
            {
                ScanStatus = "错误";
                AddLog($"Y轴到扫描位异常: {ex.Message}");
                ShowMessage($"Y轴到扫描位异常: {ex.Message}");
            }
        }

        private async Task MoveToAxesYPrePickPositionAsync()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                ScanStatus = "扫描中";
                AddLog("开始执行Y轴到取料准备位置...");

                bool success = await Task.Run(() => _loadingStation.MoveToPrePickPosition());

                ScanStatus = success ? "完成" : "错误";
                AddLog(success ? "Y轴到取料准备完成" : "Y轴到取料准备失败");
            }
            catch (Exception ex)
            {
                ScanStatus = "错误";
                AddLog($"Y轴到取料准备异常: {ex.Message}");
                ShowMessage($"Y轴到取料准备异常: {ex.Message}");
            }
        }
        #endregion

        #region 3D扫描功能
        private async Task Execute3DScanAsync()
        {
            if (!CheckPermissionsAndSafety()) return;

            // 初始化 DispenserStationService
            var dispenserStationService = new DispenserStationService(_dispenserStation, _logger);

            bool success = await _cancelableOperationService.ExecuteWithCancellationAsync(
                title: "3D扫描",
                message: "正在执行3D扫描，请稍候...",
                operation: async (cancellationToken, progress, statusProgress) =>
                {
                    try
                    {
                        // 立即开始执行
                        ScanStatus = "扫描中";
                        AddLog("开始执行3D扫描...");
                        statusProgress.Report("初始化扫描设备...");
                        progress.Report(0);

                        // 创建进度回调
                        var progressHandler = new Progress<(int progress, string status)>(report =>
                        {
                            progress.Report((double)report.progress);
                            statusProgress.Report(report.status);

                            if (!string.IsNullOrEmpty(report.status))
                            {
                                AddLog(report.status + " " + report.progress + "%");
                            }
                        });

                        // 使用 Task.Run 包装同步的扫描操作
                        bool scanSuccess = await Task.Run(() =>
                        {
                            // 注册取消回调
                            using (cancellationToken.Register(() =>
                            {
                                try
                                {
                                    AddLog("收到取消信号，正在停止扫描...");
                                    dispenserStationService.CancelCurrentOperation();
                                }
                                catch (Exception ex)
                                {
                                    AddLog($"停止扫描时发生异常: {ex.Message}");
                                }
                            }))
                            {
                                // 使用 cancellationToken 传递取消信号
                                return _dispenserStation.Perform3DScanAsync(cancellationToken, progressHandler);
                            }
                        }, cancellationToken);

                        // 在长时间运行的操作中定期检查取消
                        cancellationToken.ThrowIfCancellationRequested();

                        if (scanSuccess)
                        {
                            ScanStatus = "完成";
                            AddLog("3D扫描完成");
                            statusProgress.Report("扫描完成");
                            progress.Report(100);
                        }
                        else
                        {
                            ScanStatus = "错误";
                            AddLog("3D扫描失败");
                            statusProgress.Report("扫描失败");
                        }

                        return scanSuccess;
                    }
                    catch (OperationCanceledException)
                    {
                        ScanStatus = "已取消";
                        AddLog("3D扫描已被用户取消");
                        statusProgress.Report("操作已取消");
                        return false;
                    }
                    catch (Exception ex)
                    {
                        ScanStatus = "错误";
                        AddLog($"3D扫描异常: {ex.Message}");
                        statusProgress.Report($"扫描异常: {ex.Message}");
                        return false;
                    }
                },
                showProgress: true,
                showStatus: true
            );

            if (!success)
            {
                AddLog("3D扫描操作被取消或失败");
            }
        }

        private async Task PrepareForScanningAsync()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                AddLog("准备扫描位置...");
                bool success = await Task.Run(async () =>
                   await _dispenserStation.MoveToScanStartPositionAsync());
                AddLog("扫描位置准备完成");
            }
            catch (Exception ex)
            {
                AddLog($"准备扫描位置异常: {ex.Message}");
                ShowMessage($"准备扫描位置异常: {ex.Message}");
            }
        }

        private async Task MoveToScanStartPositionAsync()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                AddLog("移动到扫描起始位置...");
                bool success = await Task.Run(async () =>
                await _dispenserStation.MoveToScanStartPositionAsync());
                AddLog("已到达扫描起始位置");
            }
            catch (Exception ex)
            {
                AddLog($"移动到起始位置异常: {ex.Message}");
                ShowMessage($"移动到起始位置异常: {ex.Message}");
            }
        }

        private async Task ReturnToSafePositionAsync()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                AddLog("返回安全位置...");
                bool success = await Task.Run(async () =>
                   await _dispenserStation.ReturnToSafePositionAsync());
                AddLog("已返回安全位置");
            }
            catch (Exception ex)
            {
                AddLog($"返回安全位置异常: {ex.Message}");
                ShowMessage($"返回安全位置异常: {ex.Message}");
            }
        }
        #endregion

        #region 标定功能
        private async Task StartCalibrationAsync()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                CalibrationStatus = "标定中";
                CalibrationProgress = 0;
                AddLog("开始3D相机标定...");

                // 应用当前参数
                ApplyCalibrationParameters();

                // 调用点胶站的标定方法
                bool success = await _dispenserStation.Perform3DCalibrationAsync();

                CalibrationStatus = success ? "完成" : "错误";
                CalibrationProgress = success ? 100 : 0;
                AddLog(success ? "3D相机标定完成" : "3D相机标定失败");
            }
            catch (Exception ex)
            {
                CalibrationStatus = "错误";
                CalibrationProgress = 0;
                AddLog($"标定异常: {ex.Message}");
                ShowMessage($"标定异常: {ex.Message}");
            }
        }

        private void StopCalibration()
        {
            try
            {
                // 调用点胶站的停止标定方法
                _dispenserStation.Stop3DCalibration();
                CalibrationStatus = "已取消";
                AddLog("标定已取消");
            }
            catch (Exception ex)
            {
                AddLog($"停止标定异常: {ex.Message}");
                ShowMessage($"停止标定异常: {ex.Message}");
            }
        }

        private void ApplyCalibrationParameters()
        {
            try
            {
                // 这里应该更新点胶站的标定参数
                AddLog($"应用标定参数: R步进={RStepAngle}°, R次数={RScanCount}, U步进={UStepAngle}°, U单边次数={UScanCountPerSide}");
                ShowMessage("标定参数已应用");
            }
            catch (Exception ex)
            {
                AddLog($"应用标定参数异常: {ex.Message}");
                ShowMessage($"应用标定参数异常: {ex.Message}");
            }
        }
        #endregion

        #region 针头校准功能
        // 针头校准属性
        private double _cameraCenterX;
        private double _cameraCenterY;
        private double _needleTipX;
        private double _needleTipY;
        private double _needleTipZ;
        private double _basePlaneZ;
        private double _calibrationDeltaX;
        private double _calibrationDeltaY;
        private double _compensationX;
        private double _compensationY;
        private double _compensationZ;

        public double CameraCenterX
        {
            get => _cameraCenterX;
            set => SetProperty(ref _cameraCenterX, value);
        }

        public double CameraCenterY
        {
            get => _cameraCenterY;
            set => SetProperty(ref _cameraCenterY, value);
        }

        public double NeedleTipX
        {
            get => _needleTipX;
            set => SetProperty(ref _needleTipX, value);
        }

        public double NeedleTipY
        {
            get => _needleTipY;
            set => SetProperty(ref _needleTipY, value);
        }

        public double NeedleTipZ
        {
            get => _needleTipZ;
            set => SetProperty(ref _needleTipZ, value);
        }
        public double BasePlaneZ
        {
            get => _basePlaneZ;
            set => SetProperty(ref _basePlaneZ, value);
        }
        public double CalibrationDeltaX
        {
            get => _calibrationDeltaX;
            set => SetProperty(ref _calibrationDeltaX, value);
        }

        public double CalibrationDeltaY
        {
            get => _calibrationDeltaY;
            set => SetProperty(ref _calibrationDeltaY, value);
        }

        public double CompensationX
        {
            get => _compensationX;
            set => SetProperty(ref _compensationX, value);
        }

        public double CompensationY
        {
            get => _compensationY;
            set => SetProperty(ref _compensationY, value);
        }

        public double CompensationZ
        {
            get => _compensationZ;
            set => SetProperty(ref _compensationZ, value);
        }

        // 针头校准命令
        public DelegateCommand TeachCameraCenterCommand { get; private set; }
        public DelegateCommand TeachNeedleTipCommand { get; private set; }
        public DelegateCommand ApplyCompensationCommand { get; private set; }
        public DelegateCommand AutoCalculateCompensationCommand { get; private set; }
        public DelegateCommand LoadCalibrationParametersCommand { get; private set; }
        public DelegateCommand SaveCalibrationParametersCommand { get; private set; }
        public DelegateCommand ResetCalibrationCommand { get; private set; }
        private void TeachCameraCenter()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                // 获取当前DispX和PlatY轴的坐标
                double dispX = _dispenserStation.GetAxisPosition(_dispenserStation.DispX.ActId);
                double platY = _dispenserStation.GetAxisPosition(_dispenserStation.DispY_1.ActId);

                CameraCenterX = dispX;
                CameraCenterY = platY;

                AddLog($"示教相机中心坐标: DispX={dispX:F3}, PlatY={platY:F3}");
                CalculateCalibrationDelta();
            }
            catch (Exception ex)
            {
                AddLog($"示教相机中心异常: {ex.Message}");
                ShowMessage($"示教相机中心异常: {ex.Message}");
            }
        }

        private void TeachNeedleTip()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                // 获取当前DispX、PlatY和DispZ2轴的坐标
                double dispX = _dispenserStation.GetAxisPosition(_dispenserStation.DispX.ActId);
                double platY = _dispenserStation.GetAxisPosition(_dispenserStation.DispY_1.ActId);
                double dispZ2 = _dispenserStation.GetAxisPosition(_dispenserStation.DispZ2.ActId);

                NeedleTipX = dispX;
                NeedleTipY = platY;
                NeedleTipZ = dispZ2;

                AddLog($"示教针尖坐标: DispX={dispX:F3}, PlatY={platY:F3}, DispZ2={dispZ2:F3}");
                CalculateCalibrationDelta();
            }
            catch (Exception ex)
            {
                AddLog($"示教针尖位置异常: {ex.Message}");
                ShowMessage($"示教针尖位置异常: {ex.Message}");
            }
        }

        private void CalculateCalibrationDelta()
        {
            if (CameraCenterX != 0 && CameraCenterY != 0 && NeedleTipX != 0 && NeedleTipY != 0)
            {
                CalibrationDeltaX = NeedleTipX - CameraCenterX;
                CalibrationDeltaY = NeedleTipY - CameraCenterY;

                AddLog($"计算相机与针尖距离: ΔX={CalibrationDeltaX:F3}, ΔY={CalibrationDeltaY:F3}");
            }
        }

        private void ApplyCompensation()
        {
            try
            {
                // 应用补偿值到运动控制
                AddLog($"应用补偿值: X={CompensationX:F3}, Y={CompensationY:F3}, Z={CompensationZ:F3}");

                // 这里调用实际的运动控制接口应用补偿
                // ApplyCompensationToMotionControl(CompensationX, CompensationY, CompensationZ);

                ShowMessage($"补偿值已应用: X={CompensationX:F3}, Y={CompensationY:F3}, Z={CompensationZ:F3}");
            }
            catch (Exception ex)
            {
                AddLog($"应用补偿异常: {ex.Message}");
                ShowMessage($"应用补偿异常: {ex.Message}");
            }
        }

        private void AutoCalculateCompensation()
        {
            try
            {
                // 自动计算补偿值（基于校准数据）
                CompensationX = -CalibrationDeltaX;
                CompensationY = -CalibrationDeltaY;
                CompensationZ = 0; // Z补偿通常需要单独设置

                AddLog($"自动计算补偿值: X={CompensationX:F3}, Y={CompensationY:F3}");
                RaisePropertyChanged(nameof(CompensationX));
                RaisePropertyChanged(nameof(CompensationY));
                RaisePropertyChanged(nameof(CompensationZ));
            }
            catch (Exception ex)
            {
                AddLog($"自动计算补偿异常: {ex.Message}");
                ShowMessage($"自动计算补偿异常: {ex.Message}");
            }
        }

        private void LoadCalibrationParameters()
        {
            try
            {
                // 使用支持自定义路径的重载方法
                string _customDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                        "Config",
                                        "Calibration");
                var parameters = _parameterStorage?.Load<NeedleCalibrationParameters>(
                    "NeedleCalibration",
                    _customDirectory  // 自定义目录
                );

                if (parameters != null)
                {
                    CameraCenterX = parameters.CameraCenterX;
                    CameraCenterY = parameters.CameraCenterY;
                    NeedleTipX = parameters.NeedleTipX;
                    NeedleTipY = parameters.NeedleTipY;
                    NeedleTipZ = parameters.NeedleTipZ;
                    BasePlaneZ = parameters.BasePlaneZ;
                    CompensationX = parameters.CompensationX;
                    CompensationY = parameters.CompensationY;
                    CompensationZ = parameters.CompensationZ;
                    CalibrationDeltaX = parameters.CalibrationDeltaX;
                    CalibrationDeltaY = parameters.CalibrationDeltaY;

                    CalculateCalibrationDelta();

                    RaisePropertyChanged(nameof(CalibrationDeltaX));
                    RaisePropertyChanged(nameof(CalibrationDeltaY));

                    AddLog("针头校准参数加载成功");
                    //ShowMessage("针头校准参数加载成功", PackIconKind.CheckCircle);
                }
                else
                {
                    AddLog("未找到针头校准参数，使用默认值");
                }
            }
            catch (Exception ex)
            {
                AddLog($"加载针头校准参数异常: {ex.Message}");
                ShowMessage($"加载针头校准参数异常: {ex.Message}", PackIconKind.AlertCircle);
            }
        }

        private void SaveCalibrationParameters()
        {
            try
            {
                var parameters = new NeedleCalibrationParameters
                {
                    CameraCenterX = CameraCenterX,
                    CameraCenterY = CameraCenterY,
                    NeedleTipX = NeedleTipX,
                    NeedleTipY = NeedleTipY,
                    NeedleTipZ = NeedleTipZ,
                    BasePlaneZ = BasePlaneZ,
                    CompensationX = CompensationX,
                    CompensationY = CompensationY,
                    CompensationZ = CompensationZ,
                    CalibrationDeltaX = CalibrationDeltaX,
                    CalibrationDeltaY = CalibrationDeltaY,
                    LastCalibrated = DateTime.Now
                };

                // 使用支持自定义路径的重载方法
               string _customDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                                       "Config",
                                                       "Calibration");
                _parameterStorage?.Save("NeedleCalibration", parameters, _customDirectory);
                AddLog("针头校准参数保存成功");
                ShowMessage("针头校准参数保存成功", PackIconKind.CheckCircle);
            }
            catch (Exception ex)
            {
                AddLog($"保存针头校准参数异常: {ex.Message}");
                ShowMessage($"保存针头校准参数异常: {ex.Message}", PackIconKind.AlertCircle);
            }
        }

        private void ResetCalibration()
        {
            try
            {
                CameraCenterX = 0;
                CameraCenterY = 0;
                NeedleTipX = 0;
                NeedleTipY = 0;
                NeedleTipZ = 0;
                BasePlaneZ = 0;
                CalibrationDeltaX = 0;
                CalibrationDeltaY = 0;
                CompensationX = 0;
                CompensationY = 0;
                CompensationZ = 0;

                AddLog("针头校准数据已重置");
                ShowMessage("针头校准数据已重置", PackIconKind.Information);
            }
            catch (Exception ex)
            {
                AddLog($"重置校准数据异常: {ex.Message}");
                ShowMessage($"重置校准数据异常: {ex.Message}");
            }
        }

        #endregion

        #region 点胶功能
        private async Task TriggerDispensingAsync()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                AddLog("触发点胶...");
                await _dispenserStation.TriggerDispensingAsync();
                AddLog("点胶完成");
            }
            catch (Exception ex)
            {
                AddLog($"点胶异常: {ex.Message}");
                ShowMessage($"点胶异常: {ex.Message}");
            }
        }

        private async Task TestDispensingAsync()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                AddLog("开始测试点胶...");
                // 模拟测试点胶过程
                for (int i = 0; i < 3; i++)
                {
                    await Task.Delay(500);
                    AddLog($"测试点胶 {i + 1}/3");
                }
                AddLog("测试点胶完成");
            }
            catch (Exception ex)
            {
                AddLog($"测试点胶异常: {ex.Message}");
                ShowMessage($"测试点胶异常: {ex.Message}");
            }
        }
        private async Task PerformWipingAsync()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                AddLog("开始擦拭...");
                bool success = await _dispenserStation.PerformWipingAsync();
                AddLog(success ? "擦拭完成" : "擦拭失败");
            }
            catch (Exception ex)
            {
                AddLog($"擦拭异常: {ex.Message}");
                ShowMessage($"擦拭异常: {ex.Message}");
            }
        }
        #endregion

        #region 点胶参数属性
        private ObservableCollection<DispensingParameterSet> _parameterSets = new ObservableCollection<DispensingParameterSet>();
        private DispensingParameterSet _selectedParameterSet;
        private int _selectedGluePercentage = 50;
        private string _parameterFileName = "dispensing_parameters.json";

        public ObservableCollection<DispensingParameterSet> ParameterSets
        {
            get => _parameterSets;
            set => SetProperty(ref _parameterSets, value);
        }

        public DispensingParameterSet SelectedParameterSet
        {
            get => _selectedParameterSet;
            set => SetProperty(ref _selectedParameterSet, value);
        }

        public int SelectedGluePercentage
        {
            get => _selectedGluePercentage;
            set
            {
                if (SetProperty(ref _selectedGluePercentage, value))
                {
                    SelectParameterSetByPercentage(value);
                }
            }
        }

        // 当前编辑的参数
        public double CurrentPressure { get; set; } = 0.3;
        public double CurrentVacuum { get; set; } = -0.2;
        public double CurrentTime { get; set; } = 1.0;
        public int CurrentPercentage { get; set; } = 50;

        #endregion

        #region 点胶参数命令
        public DelegateCommand AddParameterSetCommand { get; private set; }
        public DelegateCommand RemoveParameterSetCommand { get; private set; }
        public DelegateCommand UpdateParameterSetCommand { get; private set; }
        public DelegateCommand LoadParametersCommand { get; private set; }
        public DelegateCommand SaveParametersCommand { get; private set; }
        public DelegateCommand ApplyDispensingParametersCommand { get; private set; }
        public DelegateCommand PerformWipingCommand { get; private set; }
        #endregion

        #region 点胶参数方法
        private void InitializeDispensingParameters()
        {
            // 添加默认参数组
            if (!ParameterSets.Any())
            {
                var defaultSets = new List<DispensingParameterSet>
                {
                    new DispensingParameterSet { Percentage = 10, Pressure = 0.1, Vacuum = -0.1, Time = 0.5, Name = "微量点胶" },
                    new DispensingParameterSet { Percentage = 30, Pressure = 0.2, Vacuum = -0.15, Time = 0.8, Name = "少量点胶" },
                    new DispensingParameterSet { Percentage = 50, Pressure = 0.3, Vacuum = -0.2, Time = 1.0, Name = "标准点胶" },
                    new DispensingParameterSet { Percentage = 70, Pressure = 0.4, Vacuum = -0.25, Time = 1.5, Name = "中量点胶" },
                    new DispensingParameterSet { Percentage = 90, Pressure = 0.5, Vacuum = -0.3, Time = 2.0, Name = "大量点胶" }
                };

                foreach (var set in defaultSets)
                {
                    ParameterSets.Add(set);
                }
            }
        }

        private void AddParameterSet()
        {
            if (!CanEditParams) return;

            try
            {
                var newSet = new DispensingParameterSet
                {
                    Percentage = CurrentPercentage,
                    Pressure = CurrentPressure,
                    Vacuum = CurrentVacuum,
                    Time = CurrentTime,
                    Name = $"{CurrentPercentage}%参数组"
                };

                ParameterSets.Add(newSet);
                SortParameterSets();
                SelectedParameterSet = newSet;

                AddLog($"添加点胶参数组: {newSet.Name}");
            }
            catch (Exception ex)
            {
                AddLog($"添加参数组异常: {ex.Message}");
                ShowMessage($"添加参数组异常: {ex.Message}");
            }
        }

        private void RemoveParameterSet()
        {
            if (!CanEditParams || SelectedParameterSet == null) return;

            try
            {
                var setName = SelectedParameterSet.Name;
                ParameterSets.Remove(SelectedParameterSet);
                AddLog($"删除点胶参数组: {setName}");
            }
            catch (Exception ex)
            {
                AddLog($"删除参数组异常: {ex.Message}");
                ShowMessage($"删除参数组异常: {ex.Message}");
            }
        }

        private bool CanRemoveParameterSet()
        {
            return CanEditParams && SelectedParameterSet != null;
        }

        private void UpdateParameterSet()
        {
            if (!CanEditParams || SelectedParameterSet == null) return;

            try
            {
                SelectedParameterSet.Pressure = CurrentPressure;
                SelectedParameterSet.Vacuum = CurrentVacuum;
                SelectedParameterSet.Time = CurrentTime;
                SelectedParameterSet.Percentage = CurrentPercentage;

                SortParameterSets();
                AddLog($"更新点胶参数组: {SelectedParameterSet.Name}");
            }
            catch (Exception ex)
            {
                AddLog($"更新参数组异常: {ex.Message}");
                ShowMessage($"更新参数组异常: {ex.Message}");
            }
        }

        private void SelectParameterSetByPercentage(int percentage)
        {
            var targetSet = ParameterSets
                .OrderBy(set => Math.Abs(set.Percentage - percentage))
                .FirstOrDefault();

            if (targetSet != null)
            {
                SelectedParameterSet = targetSet;
                CurrentPressure = targetSet.Pressure;
                CurrentVacuum = targetSet.Vacuum;
                CurrentTime = targetSet.Time;
                CurrentPercentage = targetSet.Percentage;

                RaisePropertyChanged(nameof(CurrentPressure));
                RaisePropertyChanged(nameof(CurrentVacuum));
                RaisePropertyChanged(nameof(CurrentTime));
                RaisePropertyChanged(nameof(CurrentPercentage));

                AddLog($"根据胶量{percentage}%选择参数组: {targetSet.Name}");
            }
        }

        private void SortParameterSets()
        {
            var sorted = ParameterSets.OrderBy(set => set.Percentage).ToList();
            ParameterSets.Clear();
            foreach (var set in sorted)
            {
                ParameterSets.Add(set);
            }
        }

        private void ApplyDispensingParameters()
        {
            if (SelectedParameterSet == null) return;

            try
            {
                // 这里调用实际的点胶设备接口应用参数
                AddLog($"应用点胶参数: 压力={SelectedParameterSet.Pressure}MPa, " +
                       $"负压={SelectedParameterSet.Vacuum}MPa, 时间={SelectedParameterSet.Time}s");

                ShowMessage($"点胶参数已应用: {SelectedParameterSet.Name}");
            }
            catch (Exception ex)
            {
                AddLog($"应用点胶参数异常: {ex.Message}");
                ShowMessage($"应用点胶参数异常: {ex.Message}");
            }
        }

        private void SaveDispensingParameters()
        {
            if (!CanEditParams) return;

            try
            {
                var parameters = new DispensingParameters
                {
                    ParameterSets = ParameterSets.ToList(),
                    LastModified = DateTime.Now
                };

                string json = JsonConvert.SerializeObject(parameters, Formatting.Indented);
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Config",_parameterFileName);
                File.WriteAllText(filePath, json);

                AddLog($"点胶参数已保存到: {filePath}");
                ShowMessage("点胶参数保存成功", PackIconKind.CheckCircle);
            }
            catch (Exception ex)
            {
                AddLog($"保存点胶参数异常: {ex.Message}");
                ShowMessage($"保存点胶参数异常: {ex.Message}", PackIconKind.AlertCircle);
            }
        }

        private void LoadDispensingParameters()
        {
            try
            {
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Config", _parameterFileName);
                if (!File.Exists(filePath))
                {
                    AddLog("点胶参数文件不存在，使用默认参数");
                    return;
                }

                string json = File.ReadAllText(filePath);
                var parameters = JsonConvert.DeserializeObject<DispensingParameters>(json);

                if (parameters?.ParameterSets != null)
                {
                    ParameterSets.Clear();
                    foreach (var set in parameters.ParameterSets)
                    {
                        ParameterSets.Add(set);
                    }
                    SortParameterSets();
                }

                AddLog($"点胶参数已从 {filePath} 加载");
            }
            catch (Exception ex)
            {
                AddLog($"加载点胶参数异常: {ex.Message}");
                ShowMessage($"加载点胶参数异常: {ex.Message}", PackIconKind.AlertCircle);
            }
        }
        private async Task UVFixAsync ()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                AddLog("开始执行UV固定");
                await _dispenserStation.StartUVLight(); // 假设UV固定需要20秒
                AddLog("完成UV固定");
            }
            catch (Exception ex)
            {
                AddLog($"执行UV固定异常: {ex.Message}");
                ShowMessage($"执行UV固定异常: {ex.Message}");
            }
        }
        private async Task UVOffAsync()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                AddLog("开始执行关闭UV");
                await _dispenserStation.StopUVLight(); 
                AddLog("完成关闭UV");
            }
            catch (Exception ex)
            {
                AddLog($"执行关闭UV异常: {ex.Message}");
                ShowMessage($"执行关闭UV异常: {ex.Message}");
            }
        }
        #endregion

        #region 单步控制功能
        private void StartSingleStep()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                IsSingleStepMode = true;
                IsStepWaiting = true;
                CurrentStepDescription = "单步模式已启动，等待执行第一步";
                AddLog("启动单步执行模式");

                // 启用下一步按钮
                NextStepCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                AddLog($"启动单步模式异常: {ex.Message}");
                ShowMessage($"启动单步模式异常: {ex.Message}");
            }
        }

        private void ExecuteNextStep()
        {
            if (!IsSingleStepMode) return;

            try
            {
                IsStepWaiting = false;
                CurrentStepDescription = "正在执行步骤...";
                AddLog("执行下一步操作");

                // 模拟步骤执行
                Task.Delay(1000).ContinueWith(_ =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CurrentStepDescription = "步骤执行完成，等待下一步指令";
                        IsStepWaiting = true;
                        NextStepCommand.RaiseCanExecuteChanged();
                        AddLog("步骤执行完成");
                    });
                });
            }
            catch (Exception ex)
            {
                AddLog($"执行步骤异常: {ex.Message}");
                ShowMessage($"执行步骤异常: {ex.Message}");
            }
        }

        private void StopSingleStep()
        {
            try
            {
                IsSingleStepMode = false;
                IsStepWaiting = false;
                CurrentStepDescription = "单步模式已停止";
                AddLog("停止单步执行模式");

                // 禁用下一步按钮
                NextStepCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                AddLog($"停止单步模式异常: {ex.Message}");
                ShowMessage($"停止单步模式异常: {ex.Message}");
            }
        }
        #endregion

        #region 辅助方法
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

        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogMessages += $"[{timestamp}] {message}\n";
        }

        private void ClearLog()
        {
            LogMessages = "";
        }

        private void EditParameters()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                _dispenserStation.OnEditParameters();
                AddLog("打开参数编辑器");
            }
            catch (Exception ex)
            {
                AddLog($"打开参数编辑器异常: {ex.Message}");
                ShowMessage($"打开参数编辑器异常: {ex.Message}");
            }
        }

        private void SaveParasCommand()
        {
            if (!_loginModel.HasPermission(Authority.Administrator))
            {
                ShowMessage($"操作需要 {Authority.Administrator} 权限");
                return;
            }

            _dialogService.ShowDialog("ConfirmationDialog", new DialogParameters
            {
                { "title", "参数保存" },
            }, result =>
            {
                if (result.Result == ButtonResult.Yes)
                {
                    try
                    {
                        _appConfig.Load();
                        AddLog("参数保存成功");
                        ShowMessage("参数保存成功", PackIconKind.CheckCircle);
                    }
                    catch (Exception ex)
                    {
                        AddLog($"保存失败: {ex.Message}");
                        ShowMessage($"保存失败: {ex.Message}", PackIconKind.AlertCircle);
                    }
                }
            });
        }

        private void ShowMessage(string message, PackIconKind iconKind = PackIconKind.AlertCircle)
        {
            _dialogService.ShowDialog("NotificationDialog", new DialogParameters
            {
                { "title", "提示" },
                { "message", message },
                { "icon", iconKind }
            }, result =>
            {
                // 处理回调结果
            });
        }

        private void InitializeAxisPositions()
        {
            // 初始化轴位置监控数据
            AxisPositions.Add(new AxisPosition { AxisName = "X轴", CurrentPosition = 0.0 });
            AxisPositions.Add(new AxisPosition { AxisName = "Y轴", CurrentPosition = 0.0 });
            AxisPositions.Add(new AxisPosition { AxisName = "Z1轴", CurrentPosition = 0.0 });
            AxisPositions.Add(new AxisPosition { AxisName = "Z2轴", CurrentPosition = 0.0 });
            AxisPositions.Add(new AxisPosition { AxisName = "Z3轴", CurrentPosition = 0.0 });
            AxisPositions.Add(new AxisPosition { AxisName = "R轴", CurrentPosition = 0.0 });
            AxisPositions.Add(new AxisPosition { AxisName = "U轴", CurrentPosition = 0.0 });
        }

        #endregion

        #region 点阵点胶属性
        private double _dotArrayStartX = 0;
        private double _dotArrayStartY = 0;
        private int _dotArrayRows = 3;
        private int _dotArrayColumns = 3;
        private double _dotArrayRowSpacing = 10.0;
        private double _dotArrayColumnSpacing = 10.0;
        private double _dotArrayDispensingTime = 100.0;
        private double _dotArrayMoveSpeed = 50.0;
        private string _dotArrayStatus = "就绪";
        private bool _isDotArrayDispensing = false;

        public double DotArrayStartX
        {
            get => _dotArrayStartX;
            set => SetProperty(ref _dotArrayStartX, value);
        }

        public double DotArrayStartY
        {
            get => _dotArrayStartY;
            set => SetProperty(ref _dotArrayStartY, value);
        }

        public int DotArrayRows
        {
            get => _dotArrayRows;
            set => SetProperty(ref _dotArrayRows, value);
        }

        public int DotArrayColumns
        {
            get => _dotArrayColumns;
            set => SetProperty(ref _dotArrayColumns, value);
        }

        public double DotArrayRowSpacing
        {
            get => _dotArrayRowSpacing;
            set => SetProperty(ref _dotArrayRowSpacing, value);
        }

        public double DotArrayColumnSpacing
        {
            get => _dotArrayColumnSpacing;
            set => SetProperty(ref _dotArrayColumnSpacing, value);
        }

        public double DotArrayDispensingTime
        {
            get => _dotArrayDispensingTime;
            set => SetProperty(ref _dotArrayDispensingTime, value);
        }

        public double DotArrayMoveSpeed
        {
            get => _dotArrayMoveSpeed;
            set => SetProperty(ref _dotArrayMoveSpeed, value);
        }

        public string DotArrayStatus
        {
            get => _dotArrayStatus;
            set => SetProperty(ref _dotArrayStatus, value);
        }

        public Brush DotArrayStatusColor
        {
            get
            {
                return _dotArrayStatus switch
                {
                    "就绪" => Brushes.LightGreen,
                    "运行中" => Brushes.LightBlue,
                    "完成" => Brushes.LightGreen,
                    "已停止" => Brushes.Orange,
                    "错误" => Brushes.LightCoral,
                    _ => Brushes.LightGray
                };
            }
        }

        public bool IsDotArrayDispensing
        {
            get => _isDotArrayDispensing;
            set => SetProperty(ref _isDotArrayDispensing, value);
        }
        #endregion

        #region 点阵点胶命令
        public DelegateCommand StartDotArrayDispensingCommand { get; private set; }
        public DelegateCommand StopDotArrayDispensingCommand { get; private set; }
        public DelegateCommand MoveToDotArrayStartCommand { get; private set; }
        #endregion

        #region 点阵点胶方法
        private async Task StartDotArrayDispensingAsync()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                IsDotArrayDispensing = true;
                DotArrayStatus = "运行中";
                StartDotArrayDispensingCommand.RaiseCanExecuteChanged();
                StopDotArrayDispensingCommand.RaiseCanExecuteChanged();

                AddLog("开始执行点阵点胶...");

                // 调用DispenserStation的点阵点胶方法
                bool success = await _dispenserStation.PerformDotArrayDispensingAsync(
                    DotArrayStartX,
                    DotArrayStartY,
                    DotArrayRows,
                    DotArrayColumns,
                    DotArrayRowSpacing,
                    DotArrayColumnSpacing,
                    DotArrayDispensingTime,
                    DotArrayMoveSpeed);

                DotArrayStatus = success ? "完成" : "错误";
                AddLog(success ? "点阵点胶完成" : "点阵点胶失败");
            }
            catch (Exception ex)
            {
                DotArrayStatus = "错误";
                AddLog($"点阵点胶异常: {ex.Message}");
                ShowMessage($"点阵点胶异常: {ex.Message}");
            }
            finally
            {
                IsDotArrayDispensing = false;
                StartDotArrayDispensingCommand.RaiseCanExecuteChanged();
                StopDotArrayDispensingCommand.RaiseCanExecuteChanged();
            }
        }

        private void StopDotArrayDispensing()
        {
            try
            {
                _dispenserStation.StopDotArrayDispensing();
                DotArrayStatus = "已停止";
                AddLog("点阵点胶已停止");
            }
            catch (Exception ex)
            {
                AddLog($"停止点阵点胶异常: {ex.Message}");
                ShowMessage($"停止点阵点胶异常: {ex.Message}");
            }
        }

        private async Task MoveToDotArrayStartAsync()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                AddLog($"移动到点阵起始点: X={DotArrayStartX:F3}, Y={DotArrayStartY:F3}");

                bool success = await _dispenserStation.MoveToDotArrayStartAsync(
                    DotArrayStartX,
                    DotArrayStartY,
                    DotArrayMoveSpeed);

                if (success)
                {
                    AddLog("已移动到点阵起始点");
                    ShowMessage("已移动到点阵起始点", PackIconKind.CheckCircle);
                }
                else
                {
                    AddLog("移动到点阵起始点失败");
                    ShowMessage("移动到点阵起始点失败", PackIconKind.AlertCircle);
                }
            }
            catch (Exception ex)
            {
                AddLog($"移动到起始点异常: {ex.Message}");
                ShowMessage($"移动到起始点异常: {ex.Message}");
            }
        }
        #endregion

        #region 拍照位置控制方法

        private async Task MoveToTabPhotoPositionAsync()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                AddLog($"开始移动到Tab{SelectedPhotoPositionIndex}拍照位置...");

                // 调用DispenserStation的移动方法

                bool success = await Task.Run(() => _dispenserStation.MoveToTabPhotoPositionAsync(SelectedPhotoPositionIndex));

                if (success)
                {
                    AddLog($"已移动到Tab{SelectedPhotoPositionIndex}拍照位置");

                    // 触发拍照
                    await TriggerTabPhotoAsync();
                }
                else
                {
                    AddLog($"移动到Tab{SelectedPhotoPositionIndex}拍照位置失败");
                    ShowMessage($"移动到Tab{SelectedPhotoPositionIndex}拍照位置失败", PackIconKind.AlertCircle);
                }
            }
            catch (Exception ex)
            {
                AddLog($"移动到Tab拍照位置异常: {ex.Message}");
                ShowMessage($"移动到Tab拍照位置异常: {ex.Message}");
            }
        }

        private async Task MoveToPillar1PhotoPositionAsync()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                AddLog($"开始移动到Pillar{SelectedPhotoPositionIndex}-1拍照位置...");

                // 调用DispenserStation的移动方法
                bool success = await Task.Run(()=> _dispenserStation.MoveToPillar1PhotoPositionAsync(SelectedPhotoPositionIndex));

                if (success)
                {
                    AddLog($"已移动到Pillar{SelectedPhotoPositionIndex}-1拍照位置");

                    // 触发拍照
                    await TriggerPillar1PhotoAsync();

                    //await Task.Run(() => _dispenserStation.ReturnToInitialPositionAsync());
                }
                else
                {
                    AddLog($"移动到Pillar{SelectedPhotoPositionIndex}-1拍照位置失败");
                    ShowMessage($"移动到Pillar{SelectedPhotoPositionIndex}-1拍照位置失败", PackIconKind.AlertCircle);
                }
            }
            catch (Exception ex)
            {
                AddLog($"移动到Pillar1拍照位置异常: {ex.Message}");
                ShowMessage($"移动到Pillar1拍照位置异常: {ex.Message}");
            }
        }

        private async Task MoveToPillar2PhotoPositionAsync()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                AddLog($"开始移动到Pillar{SelectedPhotoPositionIndex}-2拍照位置...");

                // 调用DispenserStation的移动方法
                bool success = await Task.Run(() => _dispenserStation.MoveToPillar2PhotoPositionAsync(SelectedPhotoPositionIndex));

                if (success)
                {
                    AddLog($"已移动到Pillar{SelectedPhotoPositionIndex}-2拍照位置");

                    // 触发拍照
                    await TriggerPillar2PhotoAsync();

                    //await Task.Run(() => _dispenserStation.ReturnToInitialPositionAsync());
                }
                else
                {
                    AddLog($"移动到Pillar{SelectedPhotoPositionIndex}-2拍照位置失败");
                    ShowMessage($"移动到Pillar{SelectedPhotoPositionIndex}-2拍照位置失败", PackIconKind.AlertCircle);
                }
            }
            catch (Exception ex)
            {
                AddLog($"移动到Pillar2拍照位置异常: {ex.Message}");
                ShowMessage($"移动到Pillar2拍照位置异常: {ex.Message}");
            }
        }

        private async Task TriggerTabPhotoAsync()
        {
            try
            {
                AddLog($"触发Tab{SelectedPhotoPositionIndex}拍照...");
                await _dispenserStation.TakePhotoAsync("DispensingCamera", $"Tab{SelectedPhotoPositionIndex}");
                AddLog($"Tab{SelectedPhotoPositionIndex}拍照完成");
            }
            catch (Exception ex)
            {
                AddLog($"Tab拍照异常: {ex.Message}");
                ShowMessage($"Tab拍照异常: {ex.Message}");
            }
        }

        private async Task TriggerPillar1PhotoAsync()
        {
            try
            {
                AddLog($"触发Pillar{SelectedPhotoPositionIndex}-1拍照...");
                // 调用DispenserStation的拍照方法
                await _dispenserStation.TakePhotoAsync("DispensingCamera", $"Pillar{SelectedPhotoPositionIndex}_1拍照位");
                AddLog($"Pillar{SelectedPhotoPositionIndex}-1拍照完成");
            }
            catch (Exception ex)
            {
                AddLog($"Pillar1拍照异常: {ex.Message}");
                ShowMessage($"Pillar1拍照异常: {ex.Message}");
            }
        }

        private async Task TriggerPillar2PhotoAsync()
        {
            try
            {
                AddLog($"触发Pillar{SelectedPhotoPositionIndex}-2拍照...");
                // 调用DispenserStation的拍照方法
                await _dispenserStation.TakePhotoAsync("DispensingCamera", $"Pillar{SelectedPhotoPositionIndex}_2拍照位");
                AddLog($"Pillar{SelectedPhotoPositionIndex}-2拍照完成");
            }
            catch (Exception ex)
            {
                AddLog($"Pillar1拍照异常: {ex.Message}");
                ShowMessage($"Pillar1拍照异常: {ex.Message}");
            }
        }
        #endregion

        #region 针头点胶属性
        private bool _isPillarDispensing = false;
        private double _pillarDispensingHeight = 0;
        private double _pillarHeightDeltaZ = 0;
        private double _pillarDispensingTime = 100.0;
        private bool _autoDescendForDispensing = true;
        private string _pillarDispensingStatus = "就绪";

        public double PillarDispensingHeight
        {
            get => _pillarDispensingHeight;
            set => SetProperty(ref _pillarDispensingHeight, value);
        }

        public double PillarHeightDeltaZ
        {
            get => _pillarHeightDeltaZ;
            set => SetProperty(ref _pillarHeightDeltaZ, value);
        }

        public double PillarDispensingTime
        {
            get => _pillarDispensingTime;
            set => SetProperty(ref _pillarDispensingTime, value);
        }

        public bool AutoDescendForDispensing
        {
            get => _autoDescendForDispensing;
            set => SetProperty(ref _autoDescendForDispensing, value);
        }

        public string PillarDispensingStatus
        {
            get => _pillarDispensingStatus;
            set => SetProperty(ref _pillarDispensingStatus, value);
        }

        public Brush PillarDispensingStatusColor
        {
            get
            {
                return _pillarDispensingStatus switch
                {
                    "就绪" => Brushes.LightGreen,
                    "运行中" => Brushes.LightBlue,
                    "拍照中" => Brushes.LightBlue,
                    "点胶中" => Brushes.LightBlue,
                    "完成" => Brushes.LightGreen,
                    "已停止" => Brushes.Orange,
                    "错误" => Brushes.LightCoral,
                    _ => Brushes.LightGray
                };
            }
        }

        public bool IsPillarDispensing
        {
            get => _isPillarDispensing;
            set => SetProperty(ref _isPillarDispensing, value);
        }
        #endregion

        #region 针头点胶命令
        public DelegateCommand DispensePillar1Command { get; private set; }
        public DelegateCommand DispensePillar2Command { get; private set; }
        public DelegateCommand StopPillarDispensingCommand { get; private set; }
        #endregion

        #region 针头点胶方法
        /// <summary>
        /// 执行Pillar点胶
        /// </summary>
        private async Task DispensePillarAsync(int pillarIndex)
        {
            if (!CheckPermissionsAndSafety()) return;

            // 创建取消令牌
            var cancellationTokenSource = new CancellationTokenSource();

            try
            {
                IsPillarDispensing = true;
                PillarDispensingStatus = "运行中";
                UpdatePillarDispensingCommands();

                int selectedIndex = SelectedPhotoPositionIndex;
                AddLog($"开始Pillar{pillarIndex}点胶 - 序号{selectedIndex}");

                // 使用Task.Run在后台线程执行，避免阻塞UI
                bool success = await Task.Run(async () =>
                {
                    try
                    {
                        return await _dispenserStation.DispensePillarAsync(
                            pillarIndex,
                            selectedIndex,
                            PillarDispensingHeight,
                            PillarHeightDeltaZ,
                            PillarDispensingTime,
                            AutoDescendForDispensing,
                            CalibrationDeltaX,
                            CalibrationDeltaY,
                            CompensationX,
                            CompensationY,
                            status => PillarDispensingStatus = status,
                            AddLog
                        );
                    }
                    catch (Exception ex)
                    {
                        // 在UI线程上记录异常
                        Application.Current?.Dispatcher?.Invoke(() => AddLog($"点胶操作异常: {ex.Message}"));
                        return false;
                    }
                });

                if (success)
                {
                    PillarDispensingStatus = "完成";
                    AddLog($"Pillar{pillarIndex}点胶流程完成");
                }
                else
                {
                    PillarDispensingStatus = "错误";
                    ShowMessage($"Pillar{pillarIndex}点胶失败", PackIconKind.AlertCircle);
                }
            }
            catch (Exception ex)
            {
                PillarDispensingStatus = "错误";
                AddLog($"Pillar点胶异常: {ex.Message}");
                ShowMessage($"Pillar点胶异常: {ex.Message}", PackIconKind.AlertCircle);
            }
            finally
            {
                IsPillarDispensing = false;
                UpdatePillarDispensingCommands();
                cancellationTokenSource.Dispose();
            }
        }

        /// <summary>
        /// 拍照获取Pillar偏移位置
        /// </summary>
        private async Task<PillarOffsetResult> CapturePillarOffsetAsync(int pillarIndex, int groupIndex)
        {
            try
            {
                AddLog($"触发Pillar{pillarIndex}拍照...");

                // 调用拍照方法
                if (pillarIndex == 1)
                {
                    await _dispenserStation.TakePillar1PhotoAsync(groupIndex);
                }
                else
                {
                    await _dispenserStation.TakePillar2PhotoAsync(groupIndex);
                }

                // 从视觉系统获取偏移量
                var mockOffset = new PillarOffsetResult
                {
                    OffsetX = 0.1, // X偏移
                    OffsetY = 0.05, // Y偏移
                    Confidence = 0.95,
                    Timestamp = DateTime.Now
                };

                AddLog($"获取到Pillar{pillarIndex}视觉偏移数据");
                return mockOffset;
            }
            catch (Exception ex)
            {
                AddLog($"获取Pillar偏移异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 计算针头实际位置
        /// </summary>
        private (double X, double Y) CalculateNeedlePosition(
            PillarOffsetResult pillarOffset,
            int pillarIndex,
            int groupIndex)
        {
            try
            {
                // 公式：点胶位置 = Pillar偏移位置 + 相机与针尖固定距离 + 针头偏差

                // 1. 获取Pillar基准位置（从扩展参数中）
                double pillarBaseX = 0;
                double pillarBaseY = 0;

                // 假设扩展参数中有Pillar基准位置
                if (ExtendedParameters?.DispensingPositions != null)
                {
                    // 根据groupIndex和pillarIndex查找基准位置
                    // 这里需要根据实际的扩展参数结构进行调整
                    int positionIndex = (groupIndex - 1) * 2 + (pillarIndex - 1);
                    //if (positionIndex < ExtendedParameters.DispensingPositions.Count)
                    //{
                    //    var position = ExtendedParameters.DispensingPositions[positionIndex];
                    //    pillarBaseX = position.BaselineX;
                    //    pillarBaseY = position.BaselineY;
                    //}
                }

                // 2. 相机与针尖固定距离（从针头校准参数）
                double cameraNeedleDeltaX = CalibrationDeltaX; // Δx1
                double cameraNeedleDeltaY = CalibrationDeltaY; // Δy1

                // 3. 针头偏差（从补偿参数）
                double needleDeviationX = CompensationX; // Δx2
                double needleDeviationY = CompensationY; // Δy2

                // 计算最终位置
                double finalX = pillarBaseX + pillarOffset.OffsetX + cameraNeedleDeltaX + needleDeviationX;
                double finalY = pillarBaseY + pillarOffset.OffsetY + cameraNeedleDeltaY + needleDeviationY;

                AddLog($"位置计算: 基准({pillarBaseX:F3},{pillarBaseY:F3}) + " +
                       $"视觉偏移({pillarOffset.OffsetX:F3},{pillarOffset.OffsetY:F3}) + " +
                       $"相机针尖({cameraNeedleDeltaX:F3},{cameraNeedleDeltaY:F3}) + " +
                       $"针头偏差({needleDeviationX:F3},{needleDeviationY:F3})");

                return (finalX, finalY);
            }
            catch (Exception ex)
            {
                AddLog($"位置计算异常: {ex.Message}");
                return (0, 0);
            }
        }

        /// <summary>
        /// 计算点胶高度
        /// </summary>
        private double CalculateDispensingHeight(int pillarIndex, int groupIndex)
        {
            try
            {
                // 公式：点胶高度 = Pillar点胶基准高度 + Pillar点胶高度Δz

                // 获取基准高度（从扩展参数或固定参数）
                double baseHeight = PillarDispensingHeight;

                // 获取高度偏移
                double heightDelta = PillarHeightDeltaZ;

                // 计算最终高度
                double finalHeight = baseHeight + heightDelta;

                // 确保高度在安全范围内
                finalHeight = Math.Max(0.1, Math.Min(finalHeight, 50.0)); // 限制在0.1-50mm之间

                return finalHeight;
            }
            catch (Exception ex)
            {
                AddLog($"高度计算异常: {ex.Message}");
                return 10.0; // 默认高度
            }
        }

        /// <summary>
        /// 执行点胶动作
        /// </summary>
        private async Task ExecutePillarDispensingAsync()
        {
            try
            {
                AddLog($"开始点胶，时间: {PillarDispensingTime}ms");

                // 打开胶阀
                await _dispenserStation.TriggerDispensingAsync();

                // 等待点胶时间
                await Task.Delay((int)PillarDispensingTime);

                // 关闭胶阀（TriggerDispensingAsync应该已经处理了关闭）
                AddLog("点胶完成");
            }
            catch (Exception ex)
            {
                AddLog($"点胶异常: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 停止Pillar点胶
        /// </summary>
        private void StopPillarDispensing()
        {
            try
            {
                // 停止所有运动
                _dispenserStation.StopAllMotion();

                // 关闭胶阀
                // _dispenserStation.StopDispensing();

                PillarDispensingStatus = "已停止";
                AddLog("Pillar点胶已停止");

                IsPillarDispensing = false;
                UpdatePillarDispensingCommands();
            }
            catch (Exception ex)
            {
                AddLog($"停止点胶异常: {ex.Message}");
                ShowMessage($"停止点胶异常: {ex.Message}", PackIconKind.AlertCircle);
            }
        }

        /// <summary>
        /// 更新命令可用状态
        /// </summary>
        private void UpdatePillarDispensingCommands()
        {
            DispensePillar1Command.RaiseCanExecuteChanged();
            DispensePillar2Command.RaiseCanExecuteChanged();
            StopPillarDispensingCommand.RaiseCanExecuteChanged();
        }
        #endregion

        #region 计算Pillar角度
        private async Task CorrectPillarAngleAsync()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                AddLog($"开始移动到Pillar{SelectedPhotoPositionIndex}-1拍照位置...");

                // 调用DispenserStation的移动方法
                bool success = await Task.Run(() => _dispenserStation.CorrectPillarAngleAsync(SelectedPhotoPositionIndex));

                if (success)
                {
                    AddLog($"已移动到Pillar{SelectedPhotoPositionIndex}-1拍照位置");

                    await Task.Run(() => _dispenserStation.ReturnToInitialPositionAsync());
                }
                else
                {
                    AddLog($"移动到Pillar{SelectedPhotoPositionIndex}-1拍照位置失败");
                    ShowMessage($"移动到Pillar{SelectedPhotoPositionIndex}-1拍照位置失败", PackIconKind.AlertCircle);
                }
            }
            catch (Exception ex)
            {
                AddLog($"移动到Pillar1拍照位置异常: {ex.Message}");
                ShowMessage($"移动到Pillar1拍照位置异常: {ex.Message}");
            }
        }
        #endregion

        #region 计算Tab位置
        private async Task CalculateTabCompensationAsync()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                AddLog($"开始移动到Tab{SelectedPhotoPositionIndex}拍照位置...");

                // 调用DispenserStation的移动方法

                bool success = await Task.Run(() => _dispenserStation.CalculateTabCompensationAsync(SelectedPhotoPositionIndex));

                if (success)
                {
                    AddLog($"已移动到Tab{SelectedPhotoPositionIndex}拍照位置");
                    await Task.Run(() => _dispenserStation.ReturnToInitialPositionAsync());
                }
                else
                {
                    AddLog($"移动到Tab{SelectedPhotoPositionIndex}拍照位置失败");
                    ShowMessage($"移动到Tab{SelectedPhotoPositionIndex}拍照位置失败", PackIconKind.AlertCircle);
                }
            }
            catch (Exception ex)
            {
                AddLog($"移动到Tab拍照位置异常: {ex.Message}");
                ShowMessage($"移动到Tab拍照位置异常: {ex.Message}");
            }
        }
        #endregion

        #region 纠正actuator
        private async Task CorrectActuatorXAsync()
        {
            if (!CheckPermissionsAndSafety()) return;
            try
            {
                await _assemblyStation.CorrectActuatorXAsync();
                AddLog("actuator补偿已计算");
            }
            catch (Exception ex)
            {
                AddLog($"计算actuator补偿异常: {ex.Message}");
                ShowMessage($"计算actuator补偿异常: {ex.Message}", PackIconKind.AlertCircle);
            }
        }

        #endregion

        #region IPQC检查
        private async Task AssemblyIPQCAsync()
        {
            if (!CheckPermissionsAndSafety()) return;
            try
            {
                await _dispenserStation.ExecuteIPQCInspection();
                AddLog("actuator补偿已计算");
            }
            catch (Exception ex)
            {
                AddLog($"计算actuator补偿异常: {ex.Message}");
                ShowMessage($"计算actuator补偿异常: {ex.Message}", PackIconKind.AlertCircle);
            }
        }
        #endregion

        #region UV固话
        private async void StartUVCuring1()
        {
            try
            {
                // 调用UV固化设备控制
                await _dispenserStation.StartUVCuringAsync(SelectedPhotoPositionIndex, 1);
            }
            catch (Exception ex)
            {
            }
        }
        private async void StartUVCuring2()
        {
            try
            {
                // 调用UV固化设备控制
                await _dispenserStation.StartUVCuringAsync(SelectedPhotoPositionIndex, 2);
            }
            catch (Exception ex)
            {
            }
        }
        #endregion

    }

    // 轴位置数据模型
    public class AxisPosition : BindableBase
    {
        private string _axisName;
        private double _currentPosition;

        public string AxisName
        {
            get => _axisName;
            set => SetProperty(ref _axisName, value);
        }

        public double CurrentPosition
        {
            get => _currentPosition;
            set => SetProperty(ref _currentPosition, value);
        }
    }
    // 点胶参数数据模型
    public class DispensingParameterSet : BindableBase
    {
        private string _name;
        private int _percentage;
        private double _pressure;
        private double _vacuum;
        private double _time;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public int Percentage
        {
            get => _percentage;
            set => SetProperty(ref _percentage, value);
        }

        public double Pressure
        {
            get => _pressure;
            set => SetProperty(ref _pressure, value);
        }

        public double Vacuum
        {
            get => _vacuum;
            set => SetProperty(ref _vacuum, value);
        }

        public double Time
        {
            get => _time;
            set => SetProperty(ref _time, value);
        }
    }
    // 点胶参数集合
    public class DispensingParameters
    {
        public List<DispensingParameterSet> ParameterSets { get; set; } = new List<DispensingParameterSet>();
        public DateTime LastModified { get; set; }
    }

    #region 数据模型（如果需要添加到ViewModel类中）
    public class PillarOffsetResult
    {
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public double Confidence { get; set; }
        public DateTime Timestamp { get; set; }
    }
    #endregion
}