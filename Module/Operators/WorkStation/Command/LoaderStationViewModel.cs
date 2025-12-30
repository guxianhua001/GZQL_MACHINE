using Core.Abstraction;
using Core.Services;
using Interfaces;
using Interfaces.Events;
using MaterialDesignThemes.Wpf;
using ModuleCore.Common.Authority;
using ModuleCore.Models;
using ModuleCore.ViewModels;
using ModuleCore.Views;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using SmarterMotion;
using Stations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Framework.ViewModels
{
    public class LoaderStationViewModel : BindableBase
    {
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private SubscriptionToken _refreshToken;
        private readonly AppConfig _appConfig;
        private readonly TaskInstanceManager _taskManager;
        private LoginModel _loginModel { get; set; }
        private LoadingStation _loadingStation;

        private bool _isLoadingRunning = false;
        private CancellationTokenSource _loadingCTS = new CancellationTokenSource();

        public LoaderStationViewModel(
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            TaskInstanceManager taskManager, 
            AppConfig appConfig, 
            LoginModel loginModel)
        {
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _taskManager = taskManager;
            _loginModel = loginModel;
            _loadingStation = taskManager.GetTask<LoadingStation>();
            _appConfig = appConfig;
            _loginModel.PropertyChanged += LoginModel_PropertyChanged;   // 监听登录模型变化
            InitializeCommands();
        }
        private void InitializeCommands()
        {
            // 基本操作命令
            LoadCommand = ExecuteAsyncOperation(MoveToLoadPositionAsync);
            UnloadCommand = ExecuteAsyncOperation(MoveToUnLoadPositionAsync);
            PlatformHomeCommand = ExecuteAsyncOperation(PlatformHomeAction);
            MoveToPickPositionCommand = ExecuteAsyncOperation(MoveToPickPositionAction);
            // 真空控制命令
            ChuckVacuumOnCommand = ExecuteAsyncOperation(ChuckVacuumOnAction);
            ChuckVacuumBreakCommand = ExecuteAsyncOperation(ChuckVacuumBreakAction);
            ChuckVacuumOffCommand = ExecuteAsyncOperation(ChuckVacuumOffAction);

            // 物料控制命令
            ClampMaterialCommand = ExecuteAsyncOperation(ClampMaterialAction);
            ReleaseMaterialCommand = ExecuteAsyncOperation(ReleaseMaterialAction);

            // 装配位置命令 - 使用 lambda 包装带参数的方法
            AssemblyPos1Command = ExecuteAsyncOperation(async () => await AssemblyPosAction(1));
            AssemblyPos2Command = ExecuteAsyncOperation(async () => await AssemblyPosAction(2));
            AssemblyPos3Command = ExecuteAsyncOperation(async () => await AssemblyPosAction(3));
            AssemblyPos4Command = ExecuteAsyncOperation(async () => await AssemblyPosAction(4));
            AssemblyPos5Command = ExecuteAsyncOperation(async () => await AssemblyPosAction(5));
            AssemblyPos6Command = ExecuteAsyncOperation(async () => await AssemblyPosAction(6));

            // 流程控制命令
            StartLoadingProcessCommand = ExecuteAsyncOperation(StartLoadingProcessAsync);
            StopLoadingProcessCommand = ExecuteAsyncOperation(StopLoadingProcessAction);
            PauseProcessCommand = ExecuteAsyncOperation(PauseProcessAction);
            SingleStepCommand = ExecuteAsyncOperation(SingleStepAction);
            StartSingleStepCommand = ExecuteAsyncOperation(StartSingleStepAction);
            // 标定命令
            CalibrateYAxisCommand = ExecuteAsyncOperation(CalibrateYAxisAction);
            CalibrateUAxisCommand = ExecuteAsyncOperation(CalibrateUAxisAction);
            CalibrateRAxisCommand = ExecuteAsyncOperation(CalibrateRAxisAction);
            CalibrateAllAxesCommand = ExecuteAsyncOperation(CalibrateAllAxesAction);

            // 参数命令 - 需要包装成异步方法
            EditParametersCommand = ExecuteAsyncOperation(OnEditParameters);
            // 设置回调
            if (_loadingStation != null)
            {
                _loadingStation.SetStepStatusCallback(UpdateStepStatus);
            }
        }

        private void InitializeStatus()
        {
            StepStatusList = new ObservableCollection<StepStatusItem>();
            UpdateAxisStatus();

            // 启动状态更新定时器
            StartStatusUpdateTimer();
        }

        private void StartStatusUpdateTimer()
        {
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += (s, e) => UpdateRealTimeStatus();
            timer.Start();
        }

        private void UpdateRealTimeStatus()
        {
            UpdateAxisStatus();
            UpdateVacuumStatus();
            UpdateProcessStatus();
        }

        private void UpdateAxisStatus()
        {
            YAxisReady = _loadingStation?.PlatY?.IsHomeOk ?? false;
            UAxisReady = _loadingStation?.PlatU?.IsHomeOk ?? false;
            RAxisReady = _loadingStation?.PlatR?.IsHomeOk ?? false;
        }

        private void UpdateVacuumStatus()
        {
            // 更新真空状态（需要根据实际硬件实现）
            // VacuumStatus = _loadingStation?.PlatVacSensor?.IsOn ?? false;
        }

        private void UpdateProcessStatus()
        {
            if (_loadingStation != null)
            {
                if (_loadingStation.IsRunning)
                    ProcessStatus = "运行中";
                else if (_loadingStation.IsPaused)
                    ProcessStatus = "已暂停";
                else if (_loadingStation.IsStopped)
                    ProcessStatus = "已停止";
                else
                    ProcessStatus = "待机";
            }
        }
        private void LoginModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LoginModel.LoginUser) ||
                e.PropertyName == nameof(LoginModel.HasPermission))
            {
                IsAdmin = _loginModel.HasPermission(Authority.Administrator);
            }
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
                if (result.Result == ButtonResult.OK)
                {
                    // 用户点击确认后的逻辑
                }
            });
        }

        #region 属性
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
        // 扫描状态
        private string _scanStatus = "就绪";
        public string ScanStatus
        {
            get => _scanStatus;
            set => SetProperty(ref _scanStatus, value);
        }
        public bool CanEditParams => IsAdmin;
        // 状态属性
        private bool _vacuumStatus;
        private bool _materialClampStatus;
        private bool _yAxisReady;
        private bool _uAxisReady;
        private bool _rAxisReady;
        private string _processStatus = "待机";
        private string _logMessages = "";
        private int _selectedTabIndex;

        public bool VacuumStatus
        {
            get => _vacuumStatus;
            set => SetProperty(ref _vacuumStatus, value);
        }

        public bool MaterialClampStatus
        {
            get => _materialClampStatus;
            set => SetProperty(ref _materialClampStatus, value);
        }

        public bool YAxisReady
        {
            get => _yAxisReady;
            set => SetProperty(ref _yAxisReady, value);
        }

        public bool UAxisReady
        {
            get => _uAxisReady;
            set => SetProperty(ref _uAxisReady, value);
        }

        public bool RAxisReady
        {
            get => _rAxisReady;
            set => SetProperty(ref _rAxisReady, value);
        }

        public string ProcessStatus
        {
            get => _processStatus;
            set => SetProperty(ref _processStatus, value);
        }

        public string LogMessages
        {
            get => _logMessages;
            set => SetProperty(ref _logMessages, value);
        }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        public ObservableCollection<StepStatusItem> StepStatusList { get; private set; } = new ObservableCollection<StepStatusItem>();
        #endregion

        #region 命令属性
        public DelegateCommand LoadCommand { get; private set; }
        public DelegateCommand UnloadCommand { get; private set; }
        public DelegateCommand Scan3DCommand { get; private set; }
        public DelegateCommand MoveToPickPositionCommand { get; private set; }
        public DelegateCommand PlatformHomeCommand { get; private set; }
        public DelegateCommand ChuckVacuumOnCommand { get; private set; }
        public DelegateCommand ChuckVacuumBreakCommand { get; private set; }
        public DelegateCommand ChuckVacuumOffCommand { get; private set; }
        public DelegateCommand ClampMaterialCommand { get; private set; }
        public DelegateCommand ReleaseMaterialCommand { get; private set; }
        public DelegateCommand AssemblyPos1Command { get; private set; }
        public DelegateCommand AssemblyPos2Command { get; private set; }
        public DelegateCommand AssemblyPos3Command { get; private set; }
        public DelegateCommand AssemblyPos4Command { get; private set; }
        public DelegateCommand AssemblyPos5Command { get; private set; }
        public DelegateCommand AssemblyPos6Command { get; private set; }
        public DelegateCommand StartLoadingProcessCommand { get; private set; }
        public DelegateCommand StopLoadingProcessCommand { get; private set; }
        public DelegateCommand StartSingleStepCommand { get; private set; }
        public DelegateCommand PauseProcessCommand { get; private set; }
        public DelegateCommand SingleStepCommand { get; private set; }
        public DelegateCommand CalibrateYAxisCommand { get; private set; }
        public DelegateCommand CalibrateUAxisCommand { get; private set; }
        public DelegateCommand CalibrateRAxisCommand { get; private set; }
        public DelegateCommand CalibrateAllAxesCommand { get; private set; }
        public DelegateCommand EditParametersCommand { get; private set; }
        public DelegateCommand SaveParametersCommand { get; private set; }
        #endregion

        #region 命令实现
        private DelegateCommand ExecuteAsyncOperation(Func<Task> execute, Func<bool> canExecute = null)
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

                    if (!CheckAllStationsStopped())
                    {
                        ShowMessage("设备运行中,禁止手动操作！", PackIconKind.AlertCircle);
                        return;
                    }

                    isExecuting = true;
                    try
                    {
                        await execute().ConfigureAwait(false);
                        AddLogMessage($"操作执行完成: {execute.Method.Name}");
                    }
                    catch (Exception ex)
                    {
                        AddLogMessage($"操作执行失败: {ex.Message}");
                        ShowMessage($"操作失败: {ex.Message}", PackIconKind.Error);
                    }
                    finally
                    {
                        isExecuting = false;
                    }
                },
                canExecute ?? (() => !isExecuting)
            );
        }

        private bool CheckAllStationsStopped()
        {
            foreach (XStation station in XStationManager.Instance.Stations.Values)
            {
                if (station.State == XStationState.RUNNING)
                {
                    return false;
                }
            }
            return true;
        }

        private void AddLogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            Application.Current.Dispatcher.Invoke(() =>
            {
                LogMessages = $"[{timestamp}] {message}\n" + LogMessages;
            });
        }

        private void UpdateStepStatus(string description, bool isWaiting)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 更新步骤状态列表
                var existingItem = StepStatusList.FirstOrDefault(x => x.Description == description);
                if (existingItem != null)
                {
                    existingItem.IsCompleted = !isWaiting;
                    existingItem.IsCurrent = isWaiting;
                }
                else
                {
                    StepStatusList.Add(new StepStatusItem
                    {
                        Description = description,
                        IsCompleted = !isWaiting,
                        IsCurrent = isWaiting
                    });
                }

                // 限制列表长度
                if (StepStatusList.Count > 50)
                {
                    StepStatusList.RemoveAt(0);
                }
            });

            if (isWaiting)
            {
                AddLogMessage($"等待: {description}");
            }
            else
            {
                AddLogMessage($"完成: {description}");
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
        #endregion

        #region 动作实现
        private async Task MoveToLoadPositionAsync()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                ScanStatus = "扫描中";
                AddLog("开始执行Y轴到扫描位...");

                // 确保返回 Task
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

        private async Task MoveToUnLoadPositionAsync()
        {
            if (!CheckPermissionsAndSafety()) return;

            try
            {
                ScanStatus = "扫描中";
                AddLog("开始执行Y轴到出料位...");

                // 确保返回 Task
                bool success = await Task.Run(() => _loadingStation.MoveToUnloadPosition());

                ScanStatus = success ? "完成" : "错误";
                AddLog(success ? "Y轴到出料位完成" : "Y轴到出料位失败");
            }
            catch (Exception ex)
            {
                ScanStatus = "错误";
                AddLog($"Y轴到出料位异常: {ex.Message}");
                ShowMessage($"Y轴到出料位异常: {ex.Message}");
            }
        }

        private async Task PlatformHomeAction()
        {
            try
            {
                AddLogMessage("执行平台归零");
                await Task.Run(() =>
                {
                    _loadingStation?.ResetPlatform();
                });
                AddLogMessage("平台归零完成");
            }
            catch (Exception ex)
            {
                AddLogMessage($"平台归零异常: {ex.Message}");
                throw;
            }
        }
        private async Task MoveToPickPositionAction()
        {
            try
            {
                AddLogMessage("执行X轴到取料位");
                _loadingStation?.MoveToPrePickPosition();
                await Task.CompletedTask; // 确保返回 Task
            }
            catch (Exception ex)
            {
                AddLogMessage($"X轴到取料位异常: {ex.Message}");
                throw;
            }
        }
        private async Task ChuckVacuumOnAction()
        {
            try
            {
                AddLogMessage("执行吸真空");
                _loadingStation?.TurnOnVacuum();
                await Task.CompletedTask; // 确保返回 Task
            }
            catch (Exception ex)
            {
                AddLogMessage($"吸真空异常: {ex.Message}");
                throw;
            }
        }

        private async Task ChuckVacuumBreakAction()
        {
            try
            {
                AddLogMessage("执行破真空");
                _loadingStation?.TurnOnBreakVacuum();
                await Task.CompletedTask; // 确保返回 Task
            }
            catch (Exception ex)
            {
                AddLogMessage($"破真空异常: {ex.Message}");
                throw;
            }
        }
        private async Task ChuckVacuumOffAction()
        {
            try
            {
                AddLogMessage("执行关闭真空");
                _loadingStation?.TurnOffVacuum();
                await Task.CompletedTask; // 确保返回 Task
            }
            catch (Exception ex)
            {
                AddLogMessage($"关闭真空异常: {ex.Message}");
                throw;
            }
        }

        private async Task ClampMaterialAction()
        {
            try
            {
                AddLogMessage("执行夹紧物料");
                // TODO: 调用夹紧物料逻辑
                await Task.CompletedTask; // 确保返回 Task
            }
            catch (Exception ex)
            {
                AddLogMessage($"夹紧物料异常: {ex.Message}");
                throw;
            }
        }

        private async Task ReleaseMaterialAction()
        {
            try
            {
                AddLogMessage("执行松开物料");
                // TODO: 调用松开物料逻辑
                await Task.CompletedTask; // 确保返回 Task
            }
            catch (Exception ex)
            {
                AddLogMessage($"松开物料异常: {ex.Message}");
                throw;
            }
        }

        private async Task AssemblyPosAction(int pos)
        {
            try
            {
                AddLogMessage($"移动到装配位置 {pos}");
                // TODO: 调用位置移动逻辑 
                await Task.Run(() =>
                {
                    _loadingStation?.MoveToAssemblyPosition(pos);
                });
                AddLogMessage($"成功移动到装配位置 {pos}");
            }
            catch (Exception ex)
            {
                AddLogMessage($"移动到装配位置 {pos} 异常: {ex.Message}");
                throw;
            }
        }

        private async Task StartLoadingProcessAsync()
        {
            if (_isLoadingRunning) return;

            try
            {
                _isLoadingRunning = true;
                _loadingCTS = new CancellationTokenSource();

                // 更新命令状态
                CommandManager.InvalidateRequerySuggested();

                AddLogMessage("开始上料流程...");

                // 在后台线程执行上料流程
                await Task.Run(async () =>
                {
                    try
                    {
                        await _loadingStation.ExecuteLoadingProcess();
                        AddLogMessage("上料流程完成");
                    }
                    catch (OperationCanceledException)
                    {
                        AddLogMessage("上料流程被取消");
                    }
                    catch (Exception ex)
                    {
                        AddLogMessage($"上料流程异常: {ex.Message}");
                    }
                });

            }
            catch (Exception ex)
            {
                AddLogMessage($"启动上料流程异常: {ex.Message}");
            }
            finally
            {
                _isLoadingRunning = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async Task StopLoadingProcessAction()
        {
            try
            {
                AddLogMessage("停止上料流程");
                _loadingStation?.StopLoadingProcess();
                await Task.CompletedTask; // 确保返回 Task
            }
            catch (Exception ex)
            {
                AddLogMessage($"停止上料流程异常: {ex.Message}");
                throw;
            }
        }
        private async Task StartSingleStepAction()
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
                            AddLogMessage("单步模式已启动");
                            // 调用工站的单步启动方法
                            _loadingStation.StartSingleStepMode();

                            ShowMessage("单步模式已启动", PackIconKind.PlayCircle);
                        }
                        catch (Exception ex)
                        {
                            ShowMessage($"启动单步模式失败: {ex.Message}", PackIconKind.AlertCircle);
                        }
                    }
                });
            });
        }
        private async Task PauseProcessAction()
        {
            try
            {
                AddLogMessage("暂停流程");
                _loadingStation?.Pause();
                await Task.CompletedTask; // 确保返回 Task
            }
            catch (Exception ex)
            {
                AddLogMessage($"暂停流程异常: {ex.Message}");
                throw;
            }
        }

        private async Task SingleStepAction()
        {
            try
            {
                AddLogMessage("单步执行");
                _loadingStation?.SingleStepNext();
                await Task.CompletedTask; // 确保返回 Task
            }
            catch (Exception ex)
            {
                AddLogMessage($"单步执行异常: {ex.Message}");
                throw;
            }
        }

        private async Task CalibrateYAxisAction()
        {
            try
            {
                AddLogMessage("开始Y轴标定");
                // TODO: 调用Y轴标定逻辑
                await Task.CompletedTask; // 确保返回 Task
            }
            catch (Exception ex)
            {
                AddLogMessage($"Y轴标定异常: {ex.Message}");
                throw;
            }
        }

        private async Task CalibrateUAxisAction()
        {
            try
            {
                AddLogMessage("开始U轴标定");
                // TODO: 调用U轴标定逻辑
                await Task.CompletedTask; // 确保返回 Task
            }
            catch (Exception ex)
            {
                AddLogMessage($"U轴标定异常: {ex.Message}");
                throw;
            }
        }

        private async Task CalibrateRAxisAction()
        {
            try
            {
                AddLogMessage("开始R轴标定");
                // TODO: 调用R轴标定逻辑
                await Task.CompletedTask; // 确保返回 Task
            }
            catch (Exception ex)
            {
                AddLogMessage($"R轴标定异常: {ex.Message}");
                throw;
            }
        }

        private async Task CalibrateAllAxesAction()
        {
            try
            {
                AddLogMessage("开始全部轴标定");
                // TODO: 调用全部标定逻辑
                await Task.CompletedTask; // 确保返回 Task
            }
            catch (Exception ex)
            {
                AddLogMessage($"全部轴标定异常: {ex.Message}");
                throw;
            }
        }

        private async Task OnEditParameters()
        {
            try
            {
                if (!CanEditParams)
                {
                    ShowMessage("没有参数编辑权限");
                    return;
                }
                _loadingStation?.OnEditParameters();
                await Task.CompletedTask; // 确保返回 Task
            }
            catch (Exception ex)
            {
                AddLogMessage($"编辑参数异常: {ex.Message}");
                throw;
            }
        }

        #endregion
    }

    // 步骤状态项
    public class StepStatusItem : BindableBase
    {
        private string _description;
        private bool _isCompleted;
        private bool _isCurrent;

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public bool IsCompleted
        {
            get => _isCompleted;
            set => SetProperty(ref _isCompleted, value);
        }

        public bool IsCurrent
        {
            get => _isCurrent;
            set => SetProperty(ref _isCurrent, value);
        }
    }
}
