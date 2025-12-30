// NeedleCalibrationViewModel.cs
using Core.Models;
using Core.Utilities;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using Stations.Service;
using Stations.Services;
using Stations.TaskParameters;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Stations.ViewModels
{
    public class NeedleAlignerViewModel : BindableBase, IDialogAware
    {
        private DispenserStation _dispenserStation;
        public DispenserStation DispenserStation
        {
            get => _dispenserStation;
            set => SetProperty(ref _dispenserStation, value);
        }
        private readonly ILoggerService _logger;

        private bool _isCalibrating = false;
        private string _calibrationStatus = "就绪";
        private double _calibrationProgress = 0;
        private ObservableCollection<string> _calibrationLogs = new ObservableCollection<string>();

        #region IDialogAware 实现

        public string Title => "针头校准";
        public event Action<IDialogResult> RequestClose;

        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        {
            // 清理资源
            if (_isCalibrating)
            {
                _dispenserStation.StopNeedleCalibration();
            }
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            // 订阅状态更新事件
            _dispenserStation.NeedleCalibrationStatusUpdated += OnNeedleCalibrationStatusUpdated;

            AddLog("针头校准对话框已打开");
            AddLog($"当前配方: {_dispenserStation.CurrentRecipeName}");
        }

        #endregion

        #region 属性

        public bool IsCalibrating
        {
            get => _isCalibrating;
            set => SetProperty(ref _isCalibrating, value);
        }

        public string CalibrationStatus
        {
            get => _calibrationStatus;
            set => SetProperty(ref _calibrationStatus, value);
        }

        public double CalibrationProgress
        {
            get => _calibrationProgress;
            set => SetProperty(ref _calibrationProgress, value);
        }

        public ObservableCollection<string> CalibrationLogs
        {
            get => _calibrationLogs;
            set => SetProperty(ref _calibrationLogs, value);
        }

        #endregion

        #region 命令

        public ICommand StartCalibrationCommand { get; }
        public ICommand StopCalibrationCommand { get; }
        public ICommand ApplyCompensationCommand { get; }
        public ICommand ResetCompensationCommand { get; }
        public ICommand SaveParametersCommand { get; }
        public ICommand LoadParametersCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ClearLogCommand { get; }

        #endregion

        private readonly NeedleCalibrationService _calibrationService;

        // 更新属性访问
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _logQueue = new();
        private readonly System.Threading.Timer _logTimer;
        private readonly object _logLock = new();
        private NeedleCalibrationParams _parameters;
        public NeedleCalibrationParams Parameters
        {
            get => _parameters;
            set => SetProperty(ref _parameters, value);
        }

        public NeedleAlignerViewModel(
            TaskInstanceManager taskManager,
            NeedleCalibrationService calibrationService,
            ILoggerService logger)
        {
            _logger = logger;
            _calibrationService = calibrationService;
            _dispenserStation = taskManager.GetTask<DispenserStation>();

            // 初始化参数（从服务加载或创建新实例）
            Parameters = _calibrationService.CurrentParameters?.Clone() ?? new NeedleCalibrationParams();

            // 订阅校准完成事件
            _dispenserStation.NeedleCalibrationCompleted += OnNeedleCalibrationCompleted;
            _dispenserStation.NeedleCalibrationStatusUpdated += OnCalibrationStatusUpdated;

            // 订阅参数加载事件
            _calibrationService.ParametersLoaded += OnParametersLoaded;

            // 初始化命令
            StartCalibrationCommand = new DelegateCommand(
                async () => await StartCalibrationAsync(),
                CanExecuteCalibration)
                .ObservesProperty(() => IsCalibrating);

            StopCalibrationCommand = new DelegateCommand(
                StopCalibration,
                () => IsCalibrating)
                .ObservesProperty(() => IsCalibrating);

            ApplyCompensationCommand = new DelegateCommand(
                ApplyCompensation,
                () => !IsCalibrating)
                .ObservesProperty(() => IsCalibrating);

            ResetCompensationCommand = new DelegateCommand(
                ResetCompensation,
                () => !IsCalibrating)
                .ObservesProperty(() => IsCalibrating);

            ClearLogCommand = new DelegateCommand(ClearLog);

            SaveParametersCommand = new DelegateCommand(async () => await SaveParametersAsync());
            LoadParametersCommand = new DelegateCommand(async () => await LoadParametersAsync());

            CancelCommand = new DelegateCommand(() =>
            {
                RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
            });

            // 初始化时加载参数
            _ = InitializeAsync();

            // 初始化日志定时器（每100ms批量更新一次日志）
            _logTimer = new Timer(ProcessLogQueue, null, 100, 100);
        }
        private async Task InitializeAsync()
        {
            if (_calibrationService != null)
            {
                await _calibrationService.LoadParametersAsync(_dispenserStation.CurrentRecipeName);
                RaisePropertyChanged(nameof(Parameters));
            }
        }
        private void OnNeedleCalibrationCompleted(NeedleCalibrationParams completedParams)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 更新UI显示校准结果
                Parameters.CurrentXYZ = completedParams.CurrentXYZ;
                Parameters.CompensationXYZ = completedParams.CompensationXYZ;

                AddLog($"校准完成 - 当前值: X={completedParams.CurrentXYZ.X:F3}, Y={completedParams.CurrentXYZ.Y:F3}, Z={completedParams.CurrentXYZ.Z:F3}");
                AddLog($"补偿值: X={completedParams.CompensationXYZ.X:F3}, Y={completedParams.CompensationXYZ.Y:F3}, Z={completedParams.CompensationXYZ.Z:F3}");
            });
        }

        private void OnCalibrationStatusUpdated(string status, double progress)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CalibrationStatus = status;
                CalibrationProgress = progress;
                AddLog(status);
            });
        }

        private void OnParametersLoaded(NeedleCalibrationParams loadedParams)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Parameters = loadedParams.Clone();
                AddLog($"参数加载完成: {loadedParams.CalibrationName}");
            });
        }
        #region 操作方法

        private async Task SaveParametersAsync()
        {
            try
            {
                // 将当前UI参数同步到服务
                SyncUIParametersToService();
                bool success = await _calibrationService.SaveParametersAsync(_dispenserStation.CurrentRecipeName);
                if (success)
                {
                    AddLog("针头校准参数保存成功");
                }
                else
                {
                    AddLog("针头校准参数保存失败");
                }
            }
            catch (Exception ex)
            {
                AddLog($"保存参数失败: {ex.Message}");
            }
        }

        private async Task LoadParametersAsync()
        {
            try
            {
                bool success = await _calibrationService.LoadParametersAsync(_dispenserStation.CurrentRecipeName);
                if (success)
                {
                    AddLog("针头校准参数加载成功");
                    RaisePropertyChanged(nameof(Parameters)); // 通知UI更新
                }
                else
                {
                    AddLog("针头校准参数加载失败");
                }
            }
            catch (Exception ex)
            {
                AddLog($"加载参数失败: {ex.Message}");
            }
        }

        // 更新校准方法
        private async Task StartCalibrationAsync()
        {
            try
            {
                IsCalibrating = true;
                CalibrationStatus = "开始校准...";
                CalibrationProgress = 0;

                // 保存当前参数
                //await _calibrationService.SaveParametersAsync();
                SyncUIParametersToService();

                bool success = await _dispenserStation.ExecuteNeedleCalibrationAsync(_calibrationService.CurrentParameters)
               .ConfigureAwait(false); // 使用 ConfigureAwait(false) 避免回到 UI 线程

                if (success)
                {
                    CalibrationStatus = "校准完成";
                    AddLog("针头校准成功完成");

                    // 保存校准后的参数
                    //await _calibrationService.SaveParametersAsync();

                    // 通知UI更新显示补偿值
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 触发属性变更通知
                        Parameters = _calibrationService.CurrentParameters.Clone();
                        RaisePropertyChanged(nameof(Parameters));
                    });
                }
                else
                {
                    CalibrationStatus = "校准失败";
                    AddLog("针头校准失败");
                }
            }
            catch (Exception ex)
            {
                CalibrationStatus = $"校准异常: {ex.Message}";
                AddLog($"校准异常: {ex.Message}");
                _logger.Error(ex, "针头校准异常");
            }
            finally
            {
                IsCalibrating = false;
            }
        }

        private void StopCalibration()
        {
            _dispenserStation.StopNeedleCalibration();
            CalibrationStatus = "校准已停止";
            AddLog("针头校准已手动停止");
        }

        private void ApplyCompensation()
        {
            try
            {
                _dispenserStation.ApplyNeedleCompensation(_calibrationService.CurrentParameters.CompensationXYZ);
                AddLog("针头补偿值已应用");
            }
            catch (Exception ex)
            {
                AddLog($"应用补偿值失败: {ex.Message}");
                _logger.Error(ex, "应用针头补偿值失败");
            }
        }

        private void ResetCompensation()
        {
            _dispenserStation.ResetNeedleCompensation();
            AddLog("针头补偿值已重置");
        }

        private void ClearLog()
        {
            CalibrationLogs.Clear();
            AddLog("日志已清空");
        }
        private void SyncUIParametersToService()
        {
            try
            {
                // 将UI中修改的参数同步到服务
                var serviceParams = _calibrationService.CurrentParameters;
                if (serviceParams != null)
                {
                    // 复制所有参数值
                    serviceParams.ReferenceXYZ = new PointF(
                        Parameters.ReferenceXYZ.X,
                        Parameters.ReferenceXYZ.Y,
                        Parameters.ReferenceXYZ.Z);

                    serviceParams.SearchPoint1 = new PointF(
                        Parameters.SearchPoint1.X,
                        Parameters.SearchPoint1.Y);

                    serviceParams.SearchPoint2 = new PointF(
                        Parameters.SearchPoint2.X,
                        Parameters.SearchPoint2.Y);

                    serviceParams.SearchPoint3 = new PointF(
                        Parameters.SearchPoint3.X,
                        Parameters.SearchPoint3.Y);

                    serviceParams.SearchPoint4 = new PointF(
                        Parameters.SearchPoint4.X,
                        Parameters.SearchPoint4.Y);

                    serviceParams.SearchRange = Parameters.SearchRange;
                    serviceParams.ZSearchCount = Parameters.ZSearchCount;
                    serviceParams.SearchSpeed = Parameters.SearchSpeed;
                    serviceParams.FineSearchSpeed = Parameters.FineSearchSpeed;
                    serviceParams.NeedleBaseHeight = Parameters.NeedleBaseHeight;

                    AddLog("参数已同步到校准服务");
                }
            }
            catch (Exception ex)
            {
                AddLog($"同步参数失败: {ex.Message}");
            }
        }

        #endregion

        #region 事件处理

        private void OnNeedleCalibrationStatusUpdated(string status, double progress)
        {
            CalibrationStatus = status;
            CalibrationProgress = progress;
        }

        #endregion

        #region 辅助方法

        private bool CanExecuteCalibration() => !IsCalibrating;

        private void ProcessLogQueue(object state)
        {
            if (_logQueue.IsEmpty) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                bool hasNewLogs = false;
                while (_logQueue.TryDequeue(out var logEntry))
                {
                    CalibrationLogs.Insert(0, logEntry);
                    hasNewLogs = true;
                }

                // 限制日志数量
                if (hasNewLogs && CalibrationLogs.Count > 100)
                {
                    for (int i = CalibrationLogs.Count - 1; i >= 100; i--)
                    {
                        CalibrationLogs.RemoveAt(i);
                    }
                }
            });
        }

        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logEntry = $"[{timestamp}] {message}";

            // 将日志放入队列，定时器会批量处理
            _logQueue.Enqueue(logEntry);
        }

        #endregion


    }
}