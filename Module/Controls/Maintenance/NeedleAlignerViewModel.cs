using Core.Abstraction;
using Core.Models;
using Core.Services;
using Core.Utilities;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using Recipe.Interfaces;
using Recipe.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Module.ViewModels
{
    public class NeedleAlignerViewModel : BindableBase
    {
        private readonly IPositionMotionController _motionController;
        private readonly IParameterStorage _parameterStorage;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localization;
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IRecipePoolService _recipePoolService;

        private readonly ConcurrentQueue<string> _logQueue = new();
        private readonly Timer _logTimer;
        private readonly object _logLock = new();
        private CancellationTokenSource _calibrationCts;
        private const double SafeHeightOffset = 50.0;

        private int _systemNumber = 1;
        public int SystemNumber
        {
            get => _systemNumber;
            set => SetProperty(ref _systemNumber, value);
        }

        private NeedleCalibrationParams _parameters = new();
        public NeedleCalibrationParams Parameters
        {
            get => _parameters;
            set => SetProperty(ref _parameters, value);
        }

        private NeedleCompensationManager _compensationManager;
        public NeedleCompensationManager CompensationManager
        {
            get => _compensationManager;
            set => SetProperty(ref _compensationManager, value);
        }

        private bool _isCalibrating;
        public bool IsCalibrating
        {
            get => _isCalibrating;
            set => SetProperty(ref _isCalibrating, value);
        }

        private string _currentFilePath;
        /// <summary>当前加载的参数文件路径</summary>
        public string CurrentFilePath
        {
            get => _currentFilePath;
            set => SetProperty(ref _currentFilePath, value);
        }

        private string _calibrationStatus = "Ready";
        public string CalibrationStatus
        {
            get => _calibrationStatus;
            set => SetProperty(ref _calibrationStatus, value);
        }

        private double _calibrationProgress;
        public double CalibrationProgress
        {
            get => _calibrationProgress;
            set => SetProperty(ref _calibrationProgress, value);
        }

        private ObservableCollection<string> _calibrationLogs = new();
        public ObservableCollection<string> CalibrationLogs
        {
            get => _calibrationLogs;
            set => SetProperty(ref _calibrationLogs, value);
        }

        private ObservableCollection<GlobalVariable> _availableGlobalVariables = new();
        public ObservableCollection<GlobalVariable> AvailableGlobalVariables
        {
            get => _availableGlobalVariables;
            set => SetProperty(ref _availableGlobalVariables, value);
        }

        private string _compensationXLinkedVar;
        public string CompensationXLinkedVar
        {
            get => _compensationXLinkedVar;
            set
            {
                if (SetProperty(ref _compensationXLinkedVar, value))
                {
                    RaisePropertyChanged(nameof(IsCompensationXLinked));
                    if (!string.IsNullOrEmpty(value))
                    {
                        var gv = AvailableGlobalVariables.FirstOrDefault(v => v.Name == value);
                        if (gv != null && double.TryParse(gv.Value, out var val))
                            CompensationManager.CompensationX = val;
                    }
                }
            }
        }

        private string _compensationYLinkedVar;
        public string CompensationYLinkedVar
        {
            get => _compensationYLinkedVar;
            set
            {
                if (SetProperty(ref _compensationYLinkedVar, value))
                {
                    RaisePropertyChanged(nameof(IsCompensationYLinked));
                    if (!string.IsNullOrEmpty(value))
                    {
                        var gv = AvailableGlobalVariables.FirstOrDefault(v => v.Name == value);
                        if (gv != null && double.TryParse(gv.Value, out var val))
                            CompensationManager.CompensationY = val;
                    }
                }
            }
        }

        private string _compensationZLinkedVar;
        public string CompensationZLinkedVar
        {
            get => _compensationZLinkedVar;
            set
            {
                if (SetProperty(ref _compensationZLinkedVar, value))
                {
                    RaisePropertyChanged(nameof(IsCompensationZLinked));
                    if (!string.IsNullOrEmpty(value))
                    {
                        var gv = AvailableGlobalVariables.FirstOrDefault(v => v.Name == value);
                        if (gv != null && double.TryParse(gv.Value, out var val))
                            CompensationManager.CompensationZ = val;
                    }
                }
            }
        }

        /// <summary>X轴补偿是否已链接全局变量</summary>
        public bool IsCompensationXLinked => !string.IsNullOrEmpty(CompensationXLinkedVar);

        /// <summary>Y轴补偿是否已链接全局变量</summary>
        public bool IsCompensationYLinked => !string.IsNullOrEmpty(CompensationYLinkedVar);

        /// <summary>Z轴补偿是否已链接全局变量</summary>
        public bool IsCompensationZLinked => !string.IsNullOrEmpty(CompensationZLinkedVar);

        public DelegateCommand StartCalibrationCommand { get; }
        public DelegateCommand StopCalibrationCommand { get; }
        public DelegateCommand ApplyCompensationCommand { get; }
        public DelegateCommand ResetCompensationCommand { get; }
        public DelegateCommand ShowCompensationHistoryCommand { get; }
        public DelegateCommand SaveParametersCommand { get; }
        public DelegateCommand LoadParametersCommand { get; }
        public DelegateCommand ClearLogCommand { get; }
        public DelegateCommand<string> TeachSearchPointCommand { get; }
        public DelegateCommand UnlinkCompensationXCommand { get; }
        public DelegateCommand UnlinkCompensationYCommand { get; }
        public DelegateCommand UnlinkCompensationZCommand { get; }

        public NeedleAlignerViewModel(
            IPositionMotionController motionController,
            IParameterStorage parameterStorage,
            ILoggerService logger,
            ILocalizationService localization,
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            NeedleCompensationManager compensationManager,
            IRecipePoolService recipePoolService)
        {
            _motionController = motionController;
            _parameterStorage = parameterStorage;
            _logger = logger;
            _localization = localization;
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _compensationManager = compensationManager;
            _recipePoolService = recipePoolService;

            _logTimer = new Timer(ProcessLogQueue, null, 100, 100);

            StartCalibrationCommand = new DelegateCommand(
                async () => await StartCalibrationAsync(),
                () => !IsCalibrating)
                .ObservesProperty(() => IsCalibrating);

            StopCalibrationCommand = new DelegateCommand(
                StopCalibration,
                () => IsCalibrating)
                .ObservesProperty(() => IsCalibrating);

            ApplyCompensationCommand = new DelegateCommand(
                async () => await ApplyCompensationAsync(),
                () => !IsCalibrating)
                .ObservesProperty(() => IsCalibrating);

            ResetCompensationCommand = new DelegateCommand(
                ResetCompensation,
                () => !IsCalibrating)
                .ObservesProperty(() => IsCalibrating);

            ShowCompensationHistoryCommand = new DelegateCommand(
                ShowCompensationHistory,
                () => !IsCalibrating)
                .ObservesProperty(() => IsCalibrating);

            SaveParametersCommand = new DelegateCommand(
                async () => await SaveParametersAsync());

            LoadParametersCommand = new DelegateCommand(
                async () => await LoadParametersAsync());

            ClearLogCommand = new DelegateCommand(ClearLog);

            TeachSearchPointCommand = new DelegateCommand<string>(
                async step => await TeachSearchPointAsync(int.Parse(step ?? "1")),
                _ => !IsCalibrating)
                .ObservesProperty(() => IsCalibrating);

            UnlinkCompensationXCommand = new DelegateCommand(() => CompensationXLinkedVar = null);
            UnlinkCompensationYCommand = new DelegateCommand(() => CompensationYLinkedVar = null);
            UnlinkCompensationZCommand = new DelegateCommand(() => CompensationZLinkedVar = null);

            _ = LoadParametersAsync();
            _ = LoadGlobalVariablesAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// 执行四点搜索校准流程
        /// </summary>
        private async Task StartCalibrationAsync()
        {
            try
            {
                IsCalibrating = true;
                CalibrationStatus = _localization.GetResourceOrDefault("NeedleAligner_Status_Starting", "开始校准...");
                CalibrationProgress = 0;
                _calibrationCts = new CancellationTokenSource();
                var token = _calibrationCts.Token;

                var stationId = $"NeedleCalibration_System{SystemNumber}";
                for (int step = 1; step <= 4; step++)
                {
                    token.ThrowIfCancellationRequested();
                    CalibrationStatus = string.Format(
                        _localization.GetResourceOrDefault("NeedleAligner_Status_SearchPoint", "搜索点{0}..."),
                        step);
                    CalibrationProgress = step * 20.0;

                    var (targetX, targetY) = GetSearchPointCoordinates(step);
                    await MoveToPositionSafelyAsync(stationId, targetX, targetY, Parameters.ReferenceXYZ.Z, Parameters.SearchSpeed);
                    await Task.Delay(200, token);

                    AddLog(string.Format(
                        _localization.GetResourceOrDefault("NeedleAligner_Log_SearchPointCompleted", "搜索点{0}完成"),
                        step));
                }

                token.ThrowIfCancellationRequested();
                CalibrationStatus = _localization.GetResourceOrDefault("NeedleAligner_Status_FineSearch", "精细搜索...");
                CalibrationProgress = 80;

                var teachResult = await _motionController.TeachAsync(stationId);
                if (teachResult != null && teachResult.Count > 0)
                {
                    if (teachResult.TryGetValue("X", out double x) ||
                        teachResult.TryGetValue("Rx", out x))
                    {
                        Parameters.CurrentXYZ = new PointF(
                            (float)x,
                            Parameters.CurrentXYZ?.Y ?? 0,
                            Parameters.CurrentXYZ?.Z ?? 0);
                    }

                    CalibrationProgress = 100;
                    OnCalibrationCompleted();
                }

                CalibrationStatus = _localization.GetResourceOrDefault("NeedleAligner_Status_Completed", "校准完成");
                AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_CalibrationSuccess", "针头校准成功完成"));
            }
            catch (OperationCanceledException)
            {
                CalibrationStatus = _localization.GetResourceOrDefault("NeedleAligner_Status_Cancelled", "校准已取消");
                AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_CalibrationCancelled", "针头校准已取消"));
            }
            catch (Exception ex)
            {
                CalibrationStatus = string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Status_Error", "校准异常: {0}"),
                    ex.Message);
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_CalibrationError", "校准异常: {0}"),
                    ex.Message));
                _logger.Error(ex, "针头校准异常");
            }
            finally
            {
                IsCalibrating = false;
                _calibrationCts?.Dispose();
                _calibrationCts = null;
            }
        }

        /// <summary>
        /// 安全移动到目标位置：先抬升Z轴到安全高度，再水平移动，最后下降Z轴
        /// 防止针头在水平移动过程中碰撞工件或夹具
        /// </summary>
        private async Task MoveToPositionSafelyAsync(string stationId, double targetX, double targetY, double targetZ, double velocity)
        {
            var safeZPositions = new Dictionary<string, double> { { "DispZ", targetZ + SafeHeightOffset } };
            await _motionController.GotoAsync(stationId, safeZPositions, velocity);

            var horizontalPositions = new Dictionary<string, double> { { "DispX", targetX }, { "GantryY", targetY } };
            await _motionController.GotoAsync(stationId, horizontalPositions, velocity);

            var targetZPositions = new Dictionary<string, double> { { "DispZ", targetZ } };
            await _motionController.GotoAsync(stationId, targetZPositions, velocity * 0.5);
        }

        /// <summary>
        /// 根据步骤编号获取搜索点坐标
        /// </summary>
        private (double X, double Y) GetSearchPointCoordinates(int step)
        {
            return step switch
            {
                1 => (Parameters.SearchPoint1.X, Parameters.SearchPoint1.Y),
                2 => (Parameters.SearchPoint2.X, Parameters.SearchPoint2.Y),
                3 => (Parameters.SearchPoint3.X, Parameters.SearchPoint3.Y),
                4 => (Parameters.SearchPoint4.X, Parameters.SearchPoint4.Y),
                _ => (0, 0)
            };
        }

        /// <summary>
        /// 停止校准运动
        /// </summary>
        private void StopCalibration()
        {
            try
            {
                _calibrationCts?.Cancel();
                var stationId = $"NeedleCalibration_System{SystemNumber}";
                _motionController.Stop(stationId);
                CalibrationStatus = _localization.GetResourceOrDefault("NeedleAligner_Status_Stopped", "校准已停止");
                AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_CalibrationStopped", "针头校准已手动停止"));
            }
            catch (Exception ex)
            {
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_StopError", "停止校准失败: {0}"),
                    ex.Message));
            }
        }

        /// <summary>
        /// 校准完成后的补偿计算与历史保存（清零法）
        /// </summary>
        private void OnCalibrationCompleted()
        {
            try
            {
                double deltaX = Parameters.ReferenceXYZ.X - Parameters.CurrentXYZ.X;
                double deltaY = Parameters.ReferenceXYZ.Y - Parameters.CurrentXYZ.Y;
                double deltaZ = Parameters.ReferenceXYZ.Z - Parameters.CurrentXYZ.Z;

                CompensationManager.UpdateCompensation(
                    Parameters.CurrentXYZ.X, Parameters.CurrentXYZ.Y, Parameters.CurrentXYZ.Z,
                    Parameters.ReferenceXYZ.X, Parameters.ReferenceXYZ.Y, Parameters.ReferenceXYZ.Z);

                Parameters.CompensationXYZ = new PointF(
                    (float)CompensationManager.CompensationX,
                    (float)CompensationManager.CompensationY,
                    (float)CompensationManager.CompensationZ);

                SaveCompensationHistory(CompensationManager, deltaX, deltaY, deltaZ);

                CheckCompensationChange(deltaX, deltaY, deltaZ);

                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_CalibrationResult",
                        "校准完成 - 当前: X={0:F3}, Y={1:F3}, Z={2:F3}"),
                    Parameters.CurrentXYZ.X, Parameters.CurrentXYZ.Y, Parameters.CurrentXYZ.Z));
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_Delta",
                        "本次增量: ΔX={0:F3}, ΔY={1:F3}, ΔZ={2:F3}"),
                    deltaX, deltaY, deltaZ));
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_Compensation",
                        "补偿值: X={0:F3}, Y={1:F3}, Z={2:F3}"),
                    CompensationManager.CompensationX,
                    CompensationManager.CompensationY,
                    CompensationManager.CompensationZ));
            }
            catch (Exception ex)
            {
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_ProcessResultError", "处理校准结果失败: {0}"),
                    ex.Message));
            }
        }

        /// <summary>
        /// 应用补偿值：将当前补偿值写入全局变量，然后保存参数
        /// 考虑设备安全，不执行运动
        /// </summary>
        private async Task ApplyCompensationAsync()
        {
            try
            {
                var compensation = new PointF(
                    (float)CompensationManager.CompensationX,
                    (float)CompensationManager.CompensationY,
                    (float)CompensationManager.CompensationZ);

                _dialogService.ShowDialog("NotificationDialog", new DialogParameters
                {
                    { "title", _localization.GetResourceOrDefault("NeedleAligner_Dialog_ApplyTitle", "确认应用补偿") },
                    { "message", string.Format(
                        _localization.GetResourceOrDefault("NeedleAligner_Dialog_ApplyToGlobalMessage",
                            "将以下补偿值写入全局变量：\nX={0:F3}, Y={1:F3}, Z={2:F3}\n并保存参数，确定继续吗？"),
                        compensation.X, compensation.Y, compensation.Z) },
                    { "icon", MaterialDesignThemes.Wpf.PackIconKind.HelpCircle }
                }, async result =>
                {
                    if (result.Result == ButtonResult.OK || result.Result == ButtonResult.Yes)
                    {
                        await WriteCompensationToGlobalVariablesAsync(compensation);
                        await SaveParametersAsync();

                        AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_CompensationAppliedToGlobal", "补偿值已写入全局变量并保存参数"));
                        AddLog(string.Format(
                            _localization.GetResourceOrDefault("NeedleAligner_Log_Compensation",
                                "补偿值: X={0:F3}, Y={1:F3}, Z={2:F3}"),
                            compensation.X, compensation.Y, compensation.Z));
                    }
                });
            }
            catch (Exception ex)
            {
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_ApplyCompensationError", "应用补偿值失败: {0}"),
                    ex.Message));
                _logger.Error(ex, "应用针头补偿值失败");
            }
        }

        /// <summary>
        /// 将补偿值写入全局变量（链接变量或默认变量名）
        /// </summary>
        private async Task WriteCompensationToGlobalVariablesAsync(PointF compensation)
        {
            var poolId = _recipePoolService?.CurrentPoolName ?? "Default";
            var variables = (await _recipePoolService.LoadGlobalVariablesAsync(poolId)).ToList();

            UpdateOrAddGlobalVariable(variables, "NeedleAligner_CompX", compensation.X.ToString("F6"), "针头校准X补偿");
            UpdateOrAddGlobalVariable(variables, "NeedleAligner_CompY", compensation.Y.ToString("F6"), "针头校准Y补偿");
            UpdateOrAddGlobalVariable(variables, "NeedleAligner_CompZ", compensation.Z.ToString("F6"), "针头校准Z补偿");

            UpdateOrAddGlobalVariable(variables, "NeedleAligner_CompX_LinkedVar", CompensationXLinkedVar ?? "", "针头X补偿链接的全局变量名");
            UpdateOrAddGlobalVariable(variables, "NeedleAligner_CompY_LinkedVar", CompensationYLinkedVar ?? "", "针头Y补偿链接的全局变量名");
            UpdateOrAddGlobalVariable(variables, "NeedleAligner_CompZ_LinkedVar", CompensationZLinkedVar ?? "", "针头Z补偿链接的全局变量名");

            for (int i = 0; i < variables.Count; i++)
                variables[i].Index = i + 1;

            await _recipePoolService.SaveGlobalVariablesAsync(poolId, variables);

            _eventAggregator?.GetEvent<GlobalVariablesChangedEvent>()?.Publish(poolId);
        }

        /// <summary>
        /// 更新或添加全局变量
        /// </summary>
        private static void UpdateOrAddGlobalVariable(List<GlobalVariable> variables, string name, string value, string comment)
        {
            var existing = variables.FirstOrDefault(v => v.Name == name);
            if (existing != null)
            {
                existing.Value = value;
            }
            else
            {
                variables.Add(new GlobalVariable
                {
                    Name = name,
                    Type = GlobalVariableType.Double,
                    Value = value,
                    Comment = comment
                });
            }
        }

        /// <summary>
        /// 重置补偿值为零，需确认对话框
        /// </summary>
        private void ResetCompensation()
        {
            try
            {
                _dialogService.ShowDialog("NotificationDialog", new DialogParameters
                {
                    { "title", _localization.GetResourceOrDefault("NeedleAligner_Dialog_ResetTitle", "警告 - 重置补偿") },
                    { "message", _localization.GetResourceOrDefault("NeedleAligner_Dialog_ResetMessage",
                        "此操作将重置所有补偿值到零。\n此操作不可逆，确定要继续吗？") },
                    { "icon", MaterialDesignThemes.Wpf.PackIconKind.AlertCircle }
                }, result =>
                {
                    if (result.Result == ButtonResult.OK || result.Result == ButtonResult.Yes)
                    {
                        CompensationManager.ResetCompensation();
                        Parameters.CompensationXYZ = new PointF(0, 0, 0);

                        AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_CompensationReset", "补偿值已重置为零"));
                        AddLog(string.Format(
                            _localization.GetResourceOrDefault("NeedleAligner_Log_Compensation",
                                "补偿值: X={0:F3}, Y={1:F3}, Z={2:F3}"),
                            CompensationManager.CompensationX,
                            CompensationManager.CompensationY,
                            CompensationManager.CompensationZ));
                    }
                });
            }
            catch (Exception ex)
            {
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_ResetError", "重置补偿失败: {0}"),
                    ex.Message));
            }
        }

        /// <summary>
        /// 显示补偿历史记录到日志
        /// </summary>
        private void ShowCompensationHistory()
        {
            try
            {
                var history = LoadCompensationHistory();
                if (history != null && history.Count > 0)
                {
                    AddLog(string.Format(
                        _localization.GetResourceOrDefault("NeedleAligner_Log_HistoryHeader",
                            "=== 补偿历史记录（系统{0}）==="),
                        SystemNumber));
                    foreach (var record in history)
                    {
                        AddLog(string.Format(
                            _localization.GetResourceOrDefault("NeedleAligner_Log_HistoryRecord",
                                "{0:yyyy-MM-dd HH:mm:ss} | 补偿X={1:F3}, Y={2:F3}, Z={3:F3} | 操作员: {4}"),
                            record.Timestamp, record.CompensationX, record.CompensationY, record.CompensationZ, record.Operator));
                    }
                    AddLog(string.Format(
                        _localization.GetResourceOrDefault("NeedleAligner_Log_Compensation",
                            "补偿值: X={0:F3}, Y={1:F3}, Z={2:F3}"),
                        CompensationManager.CompensationX,
                        CompensationManager.CompensationY,
                        CompensationManager.CompensationZ));
                }
                else
                {
                    AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_NoHistory", "无补偿历史记录"));
                }
            }
            catch (Exception ex)
            {
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_ShowHistoryError", "显示补偿历史失败: {0}"),
                    ex.Message));
            }
        }

        /// <summary>
        /// 示教搜索点：读取当前运动位置并写入对应搜索点
        /// </summary>
        private async Task TeachSearchPointAsync(int step)
        {
            try
            {
                var stationId = $"NeedleCalibration_System{SystemNumber}";
                var result = await _motionController.TeachAsync(stationId);

                if (result != null && result.Count > 0)
                {
                    double x = 0, y = 0;
                    if (result.TryGetValue("X", out double rx) || result.TryGetValue("Rx", out rx))
                        x = rx;
                    if (result.TryGetValue("Y", out double ry) || result.TryGetValue("GantryY", out ry))
                        y = ry;

                    switch (step)
                    {
                        case 1:
                            Parameters.SearchPoint1 = new PointF((float)x, (float)y);
                            break;
                        case 2:
                            Parameters.SearchPoint2 = new PointF((float)x, (float)y);
                            break;
                        case 3:
                            Parameters.SearchPoint3 = new PointF((float)x, (float)y);
                            break;
                        case 4:
                            Parameters.SearchPoint4 = new PointF((float)x, (float)y);
                            break;
                    }

                    AddLog(string.Format(
                        _localization.GetResourceOrDefault("NeedleAligner_Log_TeachSearchPoint", "搜索点{0}示教完成: X={1:F3}, Y={2:F3}"),
                        step, x, y));
                }
            }
            catch (Exception ex)
            {
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_TeachSearchPointError", "搜索点示教失败: {0}"),
                    ex.Message));
            }
        }

        /// <summary>
        /// 保存校准参数到Config/Calibration/目录
        /// </summary>
        private async Task SaveParametersAsync()
        {
            try
            {
                CompensationManager.SaveToParameters(Parameters);
                Parameters.SystemNumber = SystemNumber;
                Parameters.LastCalibrationTime = DateTime.Now;
                Parameters.CompensationXLinkedVar = CompensationXLinkedVar;
                Parameters.CompensationYLinkedVar = CompensationYLinkedVar;
                Parameters.CompensationZLinkedVar = CompensationZLinkedVar;

                var calibrationDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Config", "Calibration");
                Directory.CreateDirectory(calibrationDir);

                var identifier = $"NeedleCalibration_System{SystemNumber}";
                _parameterStorage.Save(identifier, Parameters, calibrationDir);

                CurrentFilePath = Path.Combine(calibrationDir, $"{identifier}.json");

                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_ParametersSaved",
                        "对针系统{0}参数保存成功"),
                    SystemNumber));
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_Compensation",
                        "补偿值: X={0:F3}, Y={1:F3}, Z={2:F3}"),
                    CompensationManager.CompensationX,
                    CompensationManager.CompensationY,
                    CompensationManager.CompensationZ));

                _eventAggregator?.GetEvent<NeedleParametersSavedEvent>()
                    .Publish(new NeedleParametersSavedEventArgs
                    {
                        SystemNumber = SystemNumber,
                        Parameters = Parameters
                    });
            }
            catch (Exception ex)
            {
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_SaveError", "保存参数失败: {0}"),
                    ex.Message));
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 从Config/Calibration/目录加载校准参数并初始化补偿管理器
        /// </summary>
        private async Task LoadParametersAsync()
        {
            try
            {
                var calibrationDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Config", "Calibration");

                var identifier = $"NeedleCalibration_System{SystemNumber}";
                var loaded = _parameterStorage.Load<NeedleCalibrationParams>(identifier, calibrationDir);

                if (loaded != null)
                {
                    Parameters = loaded;
                    SystemNumber = loaded.SystemNumber;

                    CompensationManager.LoadFromParameters(Parameters);

                    CompensationXLinkedVar = Parameters.CompensationXLinkedVar;
                    CompensationYLinkedVar = Parameters.CompensationYLinkedVar;
                    CompensationZLinkedVar = Parameters.CompensationZLinkedVar;

                    CurrentFilePath = Path.Combine(calibrationDir, $"{identifier}.json");

                    AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_ParametersLoaded", "针头校准参数加载成功"));
                    AddLog(string.Format(
                        _localization.GetResourceOrDefault("NeedleAligner_Log_Compensation",
                            "补偿值: X={0:F3}, Y={1:F3}, Z={2:F3}"),
                        CompensationManager.CompensationX,
                        CompensationManager.CompensationY,
                        CompensationManager.CompensationZ));

                    RaisePropertyChanged(nameof(CompensationManager));
                }
            }
            catch (Exception ex)
            {
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_LoadError", "加载参数失败: {0}"),
                    ex.Message));
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 从配方池加载全局变量列表，恢复链接关系
        /// </summary>
        private async Task LoadGlobalVariablesAsync()
        {
            try
            {
                if (_recipePoolService == null) return;

                var poolId = _recipePoolService.CurrentPoolName ?? "Default";
                var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);

                AvailableGlobalVariables.Clear();
                foreach (var v in variables)
                    AvailableGlobalVariables.Add(v);

                var cxLink = variables.FirstOrDefault(v => v.Name == "NeedleAligner_CompX_LinkedVar");
                var cyLink = variables.FirstOrDefault(v => v.Name == "NeedleAligner_CompY_LinkedVar");
                var czLink = variables.FirstOrDefault(v => v.Name == "NeedleAligner_CompZ_LinkedVar");

                CompensationXLinkedVar = cxLink?.Value;
                CompensationYLinkedVar = cyLink?.Value;
                CompensationZLinkedVar = czLink?.Value;

                RaisePropertyChanged(nameof(IsCompensationXLinked));
                RaisePropertyChanged(nameof(IsCompensationYLinked));
                RaisePropertyChanged(nameof(IsCompensationZLinked));
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleAligner] 加载全局变量失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 添加日志到队列（带时间戳）
        /// </summary>
        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logEntry = $"[{timestamp}] {message}";
            _logQueue.Enqueue(logEntry);
        }

        /// <summary>
        /// 批量处理日志队列，限制最大100条
        /// </summary>
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

                if (hasNewLogs && CalibrationLogs.Count > 100)
                {
                    for (int i = CalibrationLogs.Count - 1; i >= 100; i--)
                    {
                        CalibrationLogs.RemoveAt(i);
                    }
                }
            });
        }

        /// <summary>
        /// 检查补偿值突变（>1mm时发出警告）
        /// </summary>
        private void CheckCompensationChange(double deltaX, double deltaY, double deltaZ)
        {
            double maxAllowedChange = 1.0;

            if (Math.Abs(deltaX) > maxAllowedChange ||
                Math.Abs(deltaY) > maxAllowedChange ||
                Math.Abs(deltaZ) > maxAllowedChange)
            {
                AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_CompensationWarning",
                    "警告：补偿值变化过大！"));
                AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_CompensationWarningAdvice",
                    "建议检查：针头是否磨损、校针器位置是否变动"));

                _eventAggregator?.GetEvent<CompensationChangeAlertEvent>()?
                    .Publish(new CompensationChangeAlertEventArgs
                    {
                        SystemNumber = SystemNumber,
                        DeltaX = deltaX,
                        DeltaY = deltaY,
                        DeltaZ = deltaZ,
                        Timestamp = DateTime.Now
                    });
            }
        }

        /// <summary>
        /// 保存补偿历史记录（清零法简化版）
        /// </summary>
        private void SaveCompensationHistory(NeedleCompensationManager manager, double deltaX = 0, double deltaY = 0, double deltaZ = 0)
        {
            try
            {
                var record = new CompensationHistoryRecord
                {
                    SystemNumber = SystemNumber,
                    Timestamp = DateTime.Now,
                    CompensationX = manager.CompensationX,
                    CompensationY = manager.CompensationY,
                    CompensationZ = manager.CompensationZ,
                    CurrentX = Parameters.CurrentXYZ?.X ?? 0,
                    CurrentY = Parameters.CurrentXYZ?.Y ?? 0,
                    CurrentZ = Parameters.CurrentXYZ?.Z ?? 0,
                    ReferenceX = Parameters.ReferenceXYZ?.X ?? 0,
                    ReferenceY = Parameters.ReferenceXYZ?.Y ?? 0,
                    ReferenceZ = Parameters.ReferenceXYZ?.Z ?? 0,
                    Operator = Parameters.Operator
                };

                var historyDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Config", "Calibration", "History");
                Directory.CreateDirectory(historyDir);

                var historyFile = Path.Combine(historyDir, $"CompensationHistory_System{SystemNumber}.json");
                var history = LoadCompensationHistory();
                history.Add(record);

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(history, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(historyFile, json);
            }
            catch (Exception ex)
            {
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_SaveHistoryError", "保存补偿历史失败: {0}"),
                    ex.Message));
            }
        }

        private List<CompensationHistoryRecord> LoadCompensationHistory()
        {
            try
            {
                var historyFile = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Config", "Calibration", "History",
                    $"CompensationHistory_System{SystemNumber}.json");

                if (File.Exists(historyFile))
                {
                    var json = File.ReadAllText(historyFile);
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<List<CompensationHistoryRecord>>(json)
                           ?? new List<CompensationHistoryRecord>();
                }
            }
            catch
            {
            }
            return new List<CompensationHistoryRecord>();
        }

        private void ClearLog()
        {
            CalibrationLogs.Clear();
            AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_Cleared", "日志已清空"));
        }
    }
}
