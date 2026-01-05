using CommonServiceLocator;
using Interfaces;
using MaterialDesignThemes.Wpf;
using Framework.Models;
using ModuleCore;
using ModuleCore.ViewModels;
using ModuleCore.Views;
using Prism.Commands;
using Prism.Events;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using SmarterMotion;
using Stations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Threading;
using Interfaces.Utilities;
using Interfaces.Services;
using Interfaces.Events;
using System.Printing;
using Framework.Mvvm;
using Core.Abstraction;
using Core.Services;
using Recipe.Interfaces;
using Recipe.Events;
using Core.Utilities;
using Core.Abstractions.IConfiguration;
using Stations.Views;
using Stations.TaskParameters;
using Stations.Services;
using Core;
using Core.Events;
using Core.Models;

namespace Framework.ViewModels
{
    // 扩展方法实现
    public static class DialogServiceExtensions
    {
        public static Window ShowWithWindow(this IDialogService dialogService,
            string name,
            DialogParameters parameters,
            Action<IDialogResult> callback)
        {
            Window dialogWindow = null;

            var realCallback = new Action<IDialogResult>(result =>
            {
                // 获取实际生成的窗口
                dialogWindow = Application.Current.Windows.OfType<Window>()
                    .LastOrDefault(w => w.IsActive);
                callback?.Invoke(result);
            });
            dialogService.Show(name, parameters, realCallback);
            return dialogWindow;
        }
    }
    public class OverViewModel : RegionViewModelBase
    {
        public string ImagePath => "pack://application:,,,/ModuleCore;Component/Images/device1.png";
        private int _selectedStationId = 1; // 默认工站号
        public int SelectedStationId
        {
            get => _selectedStationId;
            set => SetProperty(ref _selectedStationId, value);
        }
        private string _recipeName = "未选择配方";
        public string RecipeName
        {
            get => _recipeName;
            set => SetProperty(ref _recipeName, value);
        }


        private int _stackCount;
        public int StackCount
        {
            get => _stackCount;
            set
            {
                _stackCount = value;
                SetProperty(ref _stackCount, value);
            }
        }
        private int _stack;
        public int Stack
        {
            get => _stack;
            set
            {
                _stack = value;
                SetProperty(ref _stack, value);
            }
        }
        // 命令定义
        public DelegateCommand EditParametersCommand1 { get; }
        public DelegateCommand EditParametersCommand2 { get; }
        public DelegateCommand EditParametersCommand3 { get; }
        public DelegateCommand NeedleCalibrationCommand { get; private set; }
        public DelegateCommand ReadBufferCommand { get; private set; }

        private System.Timers.Timer _dataTimer;
        private SemaphoreSlim _timerLock = new SemaphoreSlim(1, 1); // 添加信号量锁
        // 图表数据集合
        public ChartViewModel TorqueChart1 { get; } = new();
        public ChartViewModel TorqueChart2 { get; } = new();
        private readonly ChartViewModel[] _charts = new ChartViewModel[4];
        private readonly ILoggerService _logger;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeviceService _deviceService;
        private readonly IDialogService _dialogService;
        private readonly IDataAcquisitionService _dataService;
        private readonly IRegionManager _regionManager;
        private readonly IAlarmService _alarmService;
        private readonly IRecipeManager _recipeManager;
        private readonly IRecipeStorage _recipeStorage;
        private readonly EquipmentStatus _equipmentStatus;
        private SubscriptionToken _refreshToken;
        private SubscriptionToken _recipeChangedToken;
        private IAppConfig _appConfig;
        private readonly List<Task> _monitorTasks = new();

        private LoadingStation _loadingStation;
        public LoadingStation LoadingStation
        {
            get => _loadingStation;
            set => SetProperty(ref _loadingStation, value);
        }
        private DispenserStation _dispenserStation;
        public DispenserStation DispenserStation
        {
            get => _dispenserStation;
            set => SetProperty(ref _dispenserStation, value);
        }
        private AssemblyStation _assemblyStation;
        public AssemblyStation AssemblyStation
        {
            get => _assemblyStation;
            set => SetProperty(ref _assemblyStation, value);
        }
        public RecipePool _recipePool { get; }
        // 工位选择相关属性
        private bool _allStationsEnabled = false;
        public bool AllStationsEnabled
        {
            get => _allStationsEnabled;
            set
            {
                if (SetProperty(ref _allStationsEnabled, value))
                {
                    // 更新单个工位选择是否启用
                    IsSingleStationSelectionEnabled = !value;

                    // 如果启用全工位，自动切换到按顺序执行模式
                    if (value)
                    {
                        IsSequentialMode = true;
                    }

                    UpdateExecutionModeDescription();
                }
            }
        }

        // 下拉框选项
        private ObservableCollection<int> _stationOptions;
        public ObservableCollection<int> StationOptions
        {
            get => _stationOptions;
            set => SetProperty(ref _stationOptions, value);
        }

        // 当前选中的工位（下拉框）
        private int _selectedStation = 1;
        public int SelectedStation
        {
            get => _selectedStation;
            set => SetProperty(ref _selectedStation, value);
        }

        // 是否启用单个工位选择
        private bool _isSingleStationSelectionEnabled = true;
        public bool IsSingleStationSelectionEnabled
        {
            get => _isSingleStationSelectionEnabled;
            set => SetProperty(ref _isSingleStationSelectionEnabled, value);
        }

        // 执行模式选择
        private bool _isSingleStationMode = true;
        public bool IsSingleStationMode
        {
            get => _isSingleStationMode;
            set
            {
                if (SetProperty(ref _isSingleStationMode, value))
                {
                    if (value)
                    {
                        IsSequentialMode = false;
                        UpdateExecutionModeDescription();
                        ExecuteStartStationsExecution();
                    }
                }
            }
        }

        private bool _isSequentialMode = false;
        public bool IsSequentialMode
        {
            get => _isSequentialMode;
            set
            {
                if (SetProperty(ref _isSequentialMode, value))
                {
                    if (value)
                    {
                        IsSingleStationMode = false;
                        UpdateExecutionModeDescription();
                        ExecuteStartStationsExecution();
                    }
                }
            }
        }

        // 执行状态相关属性
        private string _currentExecutingStation = "未执行";
        public string CurrentExecutingStation
        {
            get => _currentExecutingStation;
            set => SetProperty(ref _currentExecutingStation, value);
        }

        private string _executionModeDescription = "单工位执行";
        public string ExecutionModeDescription
        {
            get => _executionModeDescription;
            set => SetProperty(ref _executionModeDescription, value);
        }

        private double _stationProgress = 0;
        public double StationProgress
        {
            get => _stationProgress;
            set => SetProperty(ref _stationProgress, value);
        }

        private string _stationProgressText = "0/0";
        public string StationProgressText
        {
            get => _stationProgressText;
            set => SetProperty(ref _stationProgressText, value);
        }

        // 命令
        public DelegateCommand ToggleAllStationsCommand { get; private set; }
        public DelegateCommand StartStationsExecutionCommand { get; private set; }
        public DelegateCommand StopStationsExecutionCommand { get; private set; }
        public DelegateCommand TogglePauseCommand { get; private set; }

        // 工站协调器
        private StationCoordinator _stationCoordinator;

        private void InitializeStationOptions()
        {
            // 创建1-6号工位选项
            StationOptions = new ObservableCollection<int> { 1, 2, 3, 4, 5, 6 };
        }

        protected readonly ILocalizationService _LocalizationService;
        protected readonly IEventAggregator _EventAggregator;

        private readonly HashSet<string> _localizedProperties = new();

        public OverViewModel(
            ILoggerService loggerService,
            IDialogService dialogService, 
            IRegionManager regionManager, 
            IContainerExtension container, 
            IEventAggregator eventAggregator , 
            IDeviceService deviceService,
            IDataAcquisitionService dataService,
            IRecipeManager recipeManager,
            IRecipeStorage recipeStorage,
            IAppConfig appConfig,
            ILocalizationService localizationService,
            TaskInstanceManager taskManager, 
            EquipmentStatus equipmentStatus, 
            RecipePool recipePool
            ) : base(regionManager)
        {
            _logger = loggerService;
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _regionManager = regionManager;
            _appConfig = appConfig;
            _recipePool = recipePool;
            _dataService = dataService;
            _deviceService = deviceService;
            _recipeManager = recipeManager;
            _recipeStorage = recipeStorage;
            //_stationCoordinator = stationCoordinator;
            //_alarmService = alarmService;
            _LocalizationService = localizationService;
            SelectedStationId = 1;
            InitializeSensor();
            // 订阅刷新事件
            _refreshToken = _eventAggregator
                .GetEvent<PositionsNeedRefreshEvent>()
                .Subscribe(OnProductNeedRefresh);
            //_dataService.DataUpdated += OnDataUpdated;
            LoadingStation = taskManager.GetTask<LoadingStation>();
            DispenserStation = taskManager.GetTask<DispenserStation>();
            AssemblyStation = taskManager.GetTask<AssemblyStation>();
            _equipmentStatus = equipmentStatus;
            InitCleanupTimer();
            // 初始化配方名称
            UpdateRecipeName();
            // 订阅配方改变事件
            _recipeChangedToken = _eventAggregator
                .GetEvent<RecipeChangedEvent>()
                .Subscribe(OnRecipeChanged);
            EditParametersCommand1 = new DelegateCommand(() =>
            {
                if (LoadingStation != null)
                {
                    LoadingStation.OnEditParameters();
                }
            });
            EditParametersCommand2 = new DelegateCommand(() =>
            {
                if (DispenserStation != null)
                {
                    DispenserStation.OnEditParameters();
                }
            });
            EditParametersCommand3 = new DelegateCommand(() =>
            {
                if (AssemblyStation != null)
                {
                    AssemblyStation.OnEditParameters();
                }
            });
            NeedleCalibrationCommand = new DelegateCommand(ExecuteNeedleCalibrationCommand);

            //InitializeStationOptions();
            //SubscribeToCoordinatorEvents();

            // 订阅语言变更事件
            //_eventAggregator.GetEvent<LanguageChangedEvent>()
            //    .Subscribe(OnLanguageChanged, ThreadOption.UIThread);
        }

        // 更新配方名称的方法
        private void UpdateRecipeName()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_appConfig != null && !string.IsNullOrEmpty(_appConfig.Name))
                {
                    RecipeName = "当前配方: " + _appConfig.Name;
                }
                else
                {
                    RecipeName = "未选择配方";
                }
            });
        }
        private void OnRecipeChanged(string newRecipeName)
        {
            UpdateRecipeName();
        }

        #region 文件清理
        // 清理定时器
        private System.Threading.Timer _cleanupTimer; // 替换为线程安全Timer
        private string _dataSavePath;
        private readonly SemaphoreSlim _cleanupLock = new(1, 1); // 清理锁防止并发执行
        public string DataSavePath
        {
            get => _dataSavePath;
            set => SetProperty(ref _dataSavePath, value);
        }
        private void InitCleanupTimer()
        {
            // 使用线程安全的Timer
            _cleanupTimer = new System.Threading.Timer(
                  _ => _ = CheckStorageAsync(),  // 异步执行
                  null,
                  dueTime: TimeSpan.Zero,        // 立即执行一次
                  period: TimeSpan.FromHours(12) // 每12小时执行
            );
        }
        private async Task CheckStorageAsync()
        {
            // 使用信号量确保不会并行执行多次清理
            if (!await _cleanupLock.WaitAsync(0))
                return;
            try
            {
                var savePath = DeviceConfigService.CurrentDataSavePath;
                if (string.IsNullOrEmpty(savePath))
                    return;
                var drive = Path.GetPathRoot(savePath);
                if (drive == null || !Directory.Exists(drive))
                    return;
                string driveName;
                double freePercent;
                try
                {
                    // 同步获取驱动器信息
                    var driveInfo = new DriveInfo(drive);
                    driveName = driveInfo.Name;
                    freePercent = driveInfo.AvailableFreeSpace * 100.0 / driveInfo.TotalSize;
                }
                catch (Exception ex)
                {
                    IMessage.Logger.Error($"获取磁盘信息失败: {ex.Message}");
                    return;
                }
                // 硬盘空间低于10%时触发清理
                if (freePercent >= 30)
                    return;
                IMessage.Logger.Warn($"磁盘空间不足 ({driveName})，可用空间: {freePercent:0.0}%。开始清理旧数据...");
                // 分阶段清理 - 避免一次性长时间操作
                for (var i = 0; i < 3; i++)
                {
                    await DeviceConfigService.CleanupExpiredDataAsync();

                    // 重新获取磁盘空间信息
                    try
                    {
                        var driveInfo = new DriveInfo(drive);
                        freePercent = driveInfo.AvailableFreeSpace * 100.0 / driveInfo.TotalSize;
                    }
                    catch (Exception ex)
                    {
                        IMessage.Logger.Error($"重新获取磁盘信息失败: {ex.Message}");
                        break;
                    }

                    if (freePercent > 15) break; // 达到15%就停止清理
                    await Task.Delay(1000); // 短暂让出时间片
                }
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"磁盘检查失败: {ex.Message}");
            }
            finally
            {
                _cleanupLock.Release();
            }
        }
        #endregion

        private void OnProductNeedRefresh()
        {
            // 带条件刷新的智能重载
            if (!string.IsNullOrEmpty(RecipeName))
            {
                RecipeName = "当前配方: " + _appConfig.Name;
            }
        }

        public SensorViewModel Sensor1 { get; } = new();
        public SensorViewModel Sensor2 { get; } = new();
        public CylinderViewModel Cylinder1 { get; } = new();
        public ObservableCollection<SensorViewModel> Sensors { get; } = new();//自动创建实例


        public void InitializeSensor()
        {
            //自动创建实例
            int diId = 5;
            var sensor1 = XDevice.Instance.FindDiById(diId);
            Sensors.Add(new SensorViewModel { Sensor = sensor1 });
            diId = 6;
            var sensor2 = XDevice.Instance.FindDiById(diId);
            Sensors.Add(new SensorViewModel { Sensor = sensor2 });

            InitializeCommands();

            InitializeDashboard();
        }
        private void InitializeDashboard()
        {
            // 注册视图
            _regionManager.RegisterViewWithRegion("DashboardRegion", typeof(StatusDashboardView));
        }


        #region 命令声明

        public DelegateCommand OpenLightCommand { get; private set; }
        public DelegateCommand CloseLightCommand { get; private set; }
        public DelegateCommand OpenSafetyDoorCommand { get; private set; }
        public DelegateCommand CloseSafetyDoorCommand { get; private set; }
        public DelegateCommand ShowLogCommand { get; private set; }
        public DelegateCommand ClearMaterialCommand { get; private set; }

        #endregion

        #region 命令初始化
        private void InitializeCommands()
        {
            double vout = 0;
            OpenLightCommand = new DelegateCommand(() =>
                XDevice.Instance.FindDoById(9).SetDo(1));
            CloseLightCommand = new DelegateCommand(() =>
                XDevice.Instance.FindDoById(9).SetDo(0));
            OpenSafetyDoorCommand = new DelegateCommand(async () =>
               XMachine.Instance.qSafeDoorList.ForEach(x => x.SetDo(1))
            );
            CloseSafetyDoorCommand = new DelegateCommand(async () =>
               XMachine.Instance.qSafeDoorList.ForEach(x => x.SetDo(0))
            );
            ShowLogCommand = new DelegateCommand(ExecuteShowLog, CanExecuteShowLog)
                   .ObservesProperty(() => IsLogViewAvailable);

            ClearMaterialCommand = new DelegateCommand(
                async () => await Task.Run(async () =>
                {
                    try
                    {
                        // 首先检查设备状态
                        bool running = false;
                        foreach (var task in XTaskManager.Instance.Tasks.Values)
                        {
                            if (task.Station.State == StationState.Running || task.Station.State == StationState.Pause)
                            {
                                running = true;
                                break;
                            }
                        }
                        if (running)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show("设备正在运行，无法执行清料操作!", "操作禁止",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                            });
                            return;
                        }
                        var dialogResult = await Application.Current.Dispatcher.Invoke(async () =>
                        {
                            return await Framework.Services.DialogService.ShowDialogAsync(
                                title: "确认清料操作",
                                message: "警告：将清除所有夹爪物料和平台物料！\n\n请确认设备处于安全状态。",
                                buttons: new[] { "取消", "确认" },
                                defaultButtonIndex: 0,
                                icon: PackIconKind.AlertCircle
                                //warningLevel: WarningLevel.High
                            );
                        });
                        if ((int)dialogResult == 0 )
                        {
                            // 取消操作...
                            return;
                        }
                        await Task.Delay(1000); // 模拟耗时操作
                    }
                    catch (Exception ex)
                    {
                        // 在UI线程显示错误
                        Application.Current.Dispatcher.Invoke(() => {
                            MessageBox.Show($"清料失败: {ex.Message}");
                        });
                    }
                }).ConfigureAwait(false)
            );
            ToggleAllStationsCommand = new DelegateCommand(ExecuteToggleAllStations);
            StartStationsExecutionCommand = new DelegateCommand(
                ExecuteStartStationsExecution,
                CanStartStationsExecution);
            StopStationsExecutionCommand = new DelegateCommand(
                ExecuteStopStationsExecution,
                CanStopStationsExecution);
            TogglePauseCommand = new DelegateCommand(
                ExecuteTogglePause,
                CanTogglePause);
        }
        private void ExecuteToggleAllStations()
        {
            // 已在属性设置器中处理逻辑
        }

        private bool CanStartStationsExecution()
        {
            //return !_stationCoordinator.IsRunning;
            return true;
        }

        private async void ExecuteStartStationsExecution()
        {
            try
            {
                // 获取要执行的工位列表
                List<int> stationsToExecute = GetStationsToExecute();

                if (stationsToExecute.Count == 0)
                {
                    //await _dialogService.ShowMessageAsync("提示", "请选择要执行的工位");
                    return;
                }

                _logger.Info($"开始执行工位: {string.Join(", ", stationsToExecute)}");

                // 通过事件发送工位选择信息到各个工站
                PublishStationSelectionToAllStations(stationsToExecute);

                // 启动协调器执行
                //await _stationCoordinator.StartExecutionAsync(stationsToExecute);

                // 更新按钮状态
                StartStationsExecutionCommand.RaiseCanExecuteChanged();
                StopStationsExecutionCommand.RaiseCanExecuteChanged();
                TogglePauseCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                _logger.Error($"启动工位执行失败: {ex.Message}");
                //await _dialogService.ShowMessageAsync("错误", $"启动失败: {ex.Message}");
            }
        }

        private bool CanStopStationsExecution()
        {
            //return _stationCoordinator.IsRunning;
            return true;
        }

        private async void ExecuteStopStationsExecution()
        {
            try
            {
                await _stationCoordinator.StopExecutionAsync();

                // 更新按钮状态
                StartStationsExecutionCommand.RaiseCanExecuteChanged();
                StopStationsExecutionCommand.RaiseCanExecuteChanged();
                TogglePauseCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                _logger.Error($"停止工位执行失败: {ex.Message}");
            }
        }

        private bool CanTogglePause()
        {
            //return _stationCoordinator.IsRunning;
            return true;
        }

        private void ExecuteTogglePause()
        {
            try
            {
                //_stationCoordinator.TogglePause();
            }
            catch (Exception ex)
            {
                _logger.Error($"暂停/继续操作失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取要执行的工位列表
        /// </summary>
        private List<int> GetStationsToExecute()
        {
            List<int> stations = new List<int>();

            if (AllStationsEnabled)
            {
                // 全工位：1-6
                stations = Enumerable.Range(1, 6).ToList();
            }
            else if (IsSingleStationMode)
            {
                // 单个工位模式：只执行选中的工位
                stations = new List<int> { SelectedStation };
            }
            else if (IsSequentialMode)
            {
                // 顺序执行模式：从选中工位开始到6
                if (SelectedStation >= 1 && SelectedStation <= 6)
                {
                    stations = Enumerable.Range(SelectedStation, 7 - SelectedStation).ToList();
                }
            }

            return stations;
        }

        /// <summary>
        /// 更新执行模式描述
        /// </summary>
        private void UpdateExecutionModeDescription()
        {
            if (AllStationsEnabled)
            {
                ExecutionModeDescription = "全工位执行 (1-6)";
            }
            else if (IsSingleStationMode)
            {
                ExecutionModeDescription = $"单工位执行 (工位{SelectedStation})";
            }
            else if (IsSequentialMode)
            {
                ExecutionModeDescription = $"顺序执行 (工位{SelectedStation}-6)";
            }
        }

        /// <summary>
        /// 发布工位选择信息到所有工站
        /// </summary>
        private void PublishStationSelectionToAllStations(List<int> stationNumbers)
        {
            try
            {
                // 创建工位选择消息
                var stationSelection = new StationSelectionMessage
                {
                    SelectedStations = stationNumbers,
                    StartStation = stationNumbers.FirstOrDefault(),
                    IsFullCycle = AllStationsEnabled,
                    IsSequentialMode = IsSequentialMode,
                    IsSingleStationMode = IsSingleStationMode,
                    Timestamp = DateTime.Now
                };

                // 通过事件聚合器发送给所有订阅者
                _eventAggregator.GetEvent<StationSelectedEvent>().Publish(stationSelection);

                // 直接设置各工站的属性
                SetStationPropertiesDirectly(stationNumbers);

                _logger.Info($"已发送工位选择信息到各工站: {string.Join(", ", stationNumbers)}");
            }
            catch (Exception ex)
            {
                _logger.Error($"发布工位选择信息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 直接设置各工站的属性
        /// </summary>
        private void SetStationPropertiesDirectly(List<int> stationNumbers)
        {
            try
            {
                if (_loadingStation != null)
                {
                    _loadingStation.SetAssemblyPositions(stationNumbers);
                    _logger.Info($"已设置LoadingStation装配位置: {string.Join(", ", stationNumbers)}");
                }

                if (_assemblyStation != null)
                {
                    _assemblyStation.SetAssemblyPositions(stationNumbers);
                    _logger.Info($"已设置AssemblyStation装配位置: {string.Join(", ", stationNumbers)}");
                }

                if (_dispenserStation != null)
                {
                    _dispenserStation.SetAssemblyPositions(stationNumbers);
                    _logger.Info($"已设置DispenserStation装配位置: {string.Join(", ", stationNumbers)}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"设置工站属性失败: {ex.Message}");
            }
        }

        private void SubscribeToCoordinatorEvents()
        {
            _stationCoordinator.StationChanged += (sender, e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CurrentExecutingStation = $"工位{e.StationNumber} - {e.Status}";
                });
            };

            _stationCoordinator.ProgressUpdated += (sender, e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StationProgress = e.CompletedCount;
                    StationProgressText = $"{e.CompletedCount}/{e.TotalCount}";
                });
            };

            _stationCoordinator.ExecutionCompleted += (sender, e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 执行完成后的处理
                    StartStationsExecutionCommand.RaiseCanExecuteChanged();
                    StopStationsExecutionCommand.RaiseCanExecuteChanged();
                    TogglePauseCommand.RaiseCanExecuteChanged();

                    // 重置当前执行状态
                    CurrentExecutingStation = "未执行";

                    // 显示完成消息
                    if (e.IsSuccess)
                    {
                        _logger.Info(e.Message);
                    }
                    else
                    {
                        _logger.Error(e.Message);
                    }
                });
            };

            //_stationCoordinator.PauseStateChanged += (sender, e) =>
            //{
            //    Application.Current.Dispatcher.Invoke(() =>
            //    {
            //        TogglePauseCommand.RaiseCanExecuteChanged();
            //    });
            //};
        }
        #endregion

        #region 日志窗口
        private bool CanExecuteShowLog()
        {
            return IsLogViewAvailable;
        }
        // 智能判断属性
        private const string LOG_VIEWER_IDENTIFIER = "LogViewerWindow";
        private WeakReference<Window> _logWindowRef; // 修改为泛型版本
        public bool IsLogViewAvailable =>
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                return Application.Current.Windows.OfType<Window>()
                    .All(w =>
                        w.Tag?.ToString() != LOG_VIEWER_IDENTIFIER &&
                        w.Name != "LogViewerWindow" &&
                        w.Content?.GetType().Name != "LogViewer");
            });
        private void ExecuteShowLog()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 查找现有窗口 - 使用公共类中的方法
                if (WindowManager.TryGetExistingWindow(WindowManager.LOG_VIEWER_IDENTIFIER, out var existingWindow))
                {
                    WindowManager.ActivateWindow(existingWindow);
                    return;
                }

                var parameters = new DialogParameters {
                    { "dialogWidth", 1200 },
                    { "dialogHeight", 800 },
                    { "sizeToContent", SizeToContent.WidthAndHeight }, // 关键修改
                    { "resizeMode", ResizeMode.NoResize },
                    { "windowStyle", WindowStyle.SingleBorderWindow },
                    { "windowStartupLocation", WindowStartupLocation.CenterOwner }
                };

                var dialogWindow = _dialogService.ShowWithWindow("LogViewer", parameters, r =>
                    _logWindowRef?.SetTarget(null));

                if (dialogWindow is Window window)
                {
                    // 使用公共类中的方法来标记窗口身份和处理关闭事件
                    WindowManager.MarkWindowIdentity(window);
                    window.Closed += (s, e) => WindowManager.OnLogWindowClosed(s, e, () =>
                        _logWindowRef?.SetTarget(null));

                    _logWindowRef = new WeakReference<Window>(window);
                }
            }, DispatcherPriority.Normal);
        }
        #endregion

        // 超时通知方法 (非阻塞)
        private void ShowMaterialTimeoutNotification()
        {
            Framework.Services.DialogService.ShowNonBlockingDialog(
                "上料提醒",
                "原料即将用完，请及时上料",
                PackIconKind.Factory
            );
        }
        private void ExecuteNeedleCalibrationCommand()
        {
            try
            {
                var parameters = new DialogParameters
                {
                    // 传递当前针头高度作为初始值
                    { "initialParams", new NeedleCalibrationParams {
                        //StartHeight = _dispenserStation.CurrentNeedleHeight,
                        //Tolerance = 0.01
                    }}
                };
                // 打开校准对话框
                _dialogService.ShowDialog("NeedleAlignerView", parameters, result =>
                {
                    // 当对话框关闭时回调
                    if (result.Result == ButtonResult.OK)
                    {
                        // 获取校准结果
                        var calibrationResult = result.Parameters.GetValue<NeedleCalibrationParams>("calibrationResult");

                        // 应用校准结果
                        //_dispenserStation.ApplyNeedleCalibration(calibrationResult);

                        //_logger.Info($"针头高度校准为: {calibrationResult.CalibratedHeight}mm");
                    }
                });
            }
            catch (Exception ex)
            {
                // 处理导航错误
            }
        }
        private void OnNeedleCalibrationComplete(IDialogResult result)
        {
            // 对话框关闭后处理结果
            if (result.Result == ButtonResult.OK)
            {
                // 获取返回的校准数据
                var finalHeight = result.Parameters.GetValue<double>("finalHeight");
                var calibrationResult = result.Parameters.GetValue<string>("status");

                // 更新系统设置
                // _settings.SetNeedleHeight(finalHeight);

                // 显示成功信息
                MessageBox.Show($"针头校准完成!\n最终高度: {finalHeight}mm\n状态: {calibrationResult}",
                                "校准成功",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
            }
            else if (result.Result == ButtonResult.Cancel)
            {
                // 用户取消了操作
            }
        }

    }
}


// 工位选择项模型
public class StationSelectionItem : BindableBase
    {
        private int _number;
        public int Number
        {
            get => _number;
            set => SetProperty(ref _number, value);
        }

        private string _displayText;
        public string DisplayText
        {
            get => _displayText;
            set => SetProperty(ref _displayText, value);
        }
    }

// 工位选择消息类
public class StationSelectionMessage
{
    public List<int> SelectedStations { get; set; }
    public int StartStation { get; set; }
    public bool IsFullCycle { get; set; }
    public bool IsSequentialMode { get; set; }
    public bool IsSingleStationMode { get; set; }
    public DateTime Timestamp { get; set; }
}

// 工位选择事件
public class StationSelectedEvent : PubSubEvent<StationSelectionMessage> { }

