using Core.Abstraction;
using Core.Constants;
using Core.Models;
using Core.Services;
using Core.Utilities;
using Module.Services;
using MotionControl.Interfaces;
using Newtonsoft.Json;
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
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Module.ViewModels
{
    public class NeedleAlignerViewModel : BindableBase
    {
        private readonly INeedleAlignerMotionService _needleMotion;
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
        /// <summary>配置文件保留天数</summary>
        private const int ConfigRetentionDays = 30;

        private int _systemNumber = 1;
        public int SystemNumber
        {
            get => _systemNumber;
            set
            {
                if (SetProperty(ref _systemNumber, value))
                    _ = TryAutoLoadConfigAsync();
            }
        }

        private NeedleCalibrationParams _parameters = new();
        public NeedleCalibrationParams Parameters
        {
            get => _parameters;
            set
            {
                if (_parameters != null)
                    _parameters.PropertyChanged -= OnParametersPropertyChanged;

                if (SetProperty(ref _parameters, value))
                {
                    if (_parameters != null)
                        _parameters.PropertyChanged += OnParametersPropertyChanged;
                    RaiseCalibrationDeltaAndCalculatedChanged();
                }
            }
        }

        private NeedleCompensationManager _compensationManager;
        public NeedleCompensationManager CompensationManager
        {
            get => _compensationManager;
            set
            {
                if (_compensationManager != null)
                    _compensationManager.PropertyChanged -= OnCompensationManagerPropertyChanged;

                if (SetProperty(ref _compensationManager, value))
                {
                    if (_compensationManager != null)
                        _compensationManager.PropertyChanged += OnCompensationManagerPropertyChanged;
                    RaisePropertyChanged(nameof(CompensationX));
                    RaisePropertyChanged(nameof(CompensationY));
                    RaisePropertyChanged(nameof(CompensationZ));
                    RaiseCalibrationDeltaAndCalculatedChanged();
                }
            }
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

        private string _currentFileName;
        /// <summary>当前加载的参数文件名（显示用）</summary>
        public string CurrentFileName
        {
            get => _currentFileName;
            set => SetProperty(ref _currentFileName, value);
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

        private ObservableCollection<GlobalVariable> _linkableGlobalVariables = new();
        /// <summary>可链接的全局变量列表（仅Double类型，供GlobalVariableLinkControl使用）</summary>
        public ObservableCollection<GlobalVariable> LinkableGlobalVariables
        {
            get => _linkableGlobalVariables;
            set => SetProperty(ref _linkableGlobalVariables, value);
        }

        private string _compensationXLinkedVar;
        /// <summary>X轴补偿链接的全局变量名（单向：仅补偿写入全局变量，不回读）</summary>
        public string CompensationXLinkedVar
        {
            get => _compensationXLinkedVar;
            set
            {
                if (SetProperty(ref _compensationXLinkedVar, value))
                    RaisePropertyChanged(nameof(IsCompensationXLinked));
            }
        }

        private string _compensationYLinkedVar;
        /// <summary>Y轴补偿链接的全局变量名（单向：仅补偿写入全局变量，不回读）</summary>
        public string CompensationYLinkedVar
        {
            get => _compensationYLinkedVar;
            set
            {
                if (SetProperty(ref _compensationYLinkedVar, value))
                    RaisePropertyChanged(nameof(IsCompensationYLinked));
            }
        }

        private string _compensationZLinkedVar;
        /// <summary>Z轴补偿链接的全局变量名（单向：仅补偿写入全局变量，不回读）</summary>
        public string CompensationZLinkedVar
        {
            get => _compensationZLinkedVar;
            set
            {
                if (SetProperty(ref _compensationZLinkedVar, value))
                    RaisePropertyChanged(nameof(IsCompensationZLinked));
            }
        }

        private string _compensationXExpression;
        /// <summary>X轴补偿表达式</summary>
        public string CompensationXExpression
        {
            get => _compensationXExpression;
            set
            {
                if (SetProperty(ref _compensationXExpression, value))
                    RaisePropertyChanged(nameof(CalculatedCompX));
            }
        }

        private string _compensationYExpression;
        /// <summary>Y轴补偿表达式</summary>
        public string CompensationYExpression
        {
            get => _compensationYExpression;
            set
            {
                if (SetProperty(ref _compensationYExpression, value))
                    RaisePropertyChanged(nameof(CalculatedCompY));
            }
        }

        private string _compensationZExpression;
        /// <summary>Z轴补偿表达式</summary>
        public string CompensationZExpression
        {
            get => _compensationZExpression;
            set
            {
                if (SetProperty(ref _compensationZExpression, value))
                    RaisePropertyChanged(nameof(CalculatedCompZ));
            }
        }

        /// <summary>校准增量 ΔX = 基准X - 当前X</summary>
        public double CalibrationDeltaX =>
            (Parameters?.ReferenceXYZ.X ?? 0) - (Parameters?.CurrentXYZ.X ?? 0);

        /// <summary>校准增量 ΔY = 基准Y - 当前Y</summary>
        public double CalibrationDeltaY =>
            (Parameters?.ReferenceXYZ.Y ?? 0) - (Parameters?.CurrentXYZ.Y ?? 0);

        /// <summary>校准增量 ΔZ = 基准Z - 当前Z</summary>
        public double CalibrationDeltaZ =>
            (Parameters?.ReferenceXYZ.Z ?? 0) - (Parameters?.CurrentXYZ.Z ?? 0);

        /// <summary>X轴补偿值（绑定 CompensationManager）</summary>
        public double CompensationX
        {
            get => CompensationManager?.CompensationX ?? 0;
            set
            {
                if (CompensationManager != null && Math.Abs(CompensationManager.CompensationX - value) > 0.0001)
                {
                    CompensationManager.CompensationX = value;
                    RaisePropertyChanged(nameof(CalculatedCompX));
                }
            }
        }

        /// <summary>Y轴补偿值（绑定 CompensationManager）</summary>
        public double CompensationY
        {
            get => CompensationManager?.CompensationY ?? 0;
            set
            {
                if (CompensationManager != null && Math.Abs(CompensationManager.CompensationY - value) > 0.0001)
                {
                    CompensationManager.CompensationY = value;
                    RaisePropertyChanged(nameof(CalculatedCompY));
                }
            }
        }

        /// <summary>Z轴补偿值（绑定 CompensationManager）</summary>
        public double CompensationZ
        {
            get => CompensationManager?.CompensationZ ?? 0;
            set
            {
                if (CompensationManager != null && Math.Abs(CompensationManager.CompensationZ - value) > 0.0001)
                {
                    CompensationManager.CompensationZ = value;
                    RaisePropertyChanged(nameof(CalculatedCompZ));
                }
            }
        }

        /// <summary>计算后的X补偿 = 偏差ΔX + 表达式结果</summary>
        public double CalculatedCompX =>
            CalibrationDeltaX + EvaluateExpression(CompensationXExpression);

        /// <summary>计算后的Y补偿 = 偏差ΔY + 表达式结果</summary>
        public double CalculatedCompY =>
            CalibrationDeltaY + EvaluateExpression(CompensationYExpression);

        /// <summary>计算后的Z补偿 = 偏差ΔZ + 表达式结果</summary>
        public double CalculatedCompZ =>
            CalibrationDeltaZ + EvaluateExpression(CompensationZExpression);

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
        public DelegateCommand<string> TeachAlignPositionCommand { get; }
        public DelegateCommand System1Command { get; }
        public DelegateCommand System2Command { get; }
        public DelegateCommand UnlinkCompensationXCommand { get; }
        public DelegateCommand UnlinkCompensationYCommand { get; }
        public DelegateCommand UnlinkCompensationZCommand { get; }

        public NeedleAlignerViewModel(
            INeedleAlignerMotionService needleMotion,
            IParameterStorage parameterStorage,
            ILoggerService logger,
            ILocalizationService localization,
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            NeedleCompensationManager compensationManager,
            IRecipePoolService recipePoolService)
        {
            _needleMotion = needleMotion;
            _parameterStorage = parameterStorage;
            _logger = logger;
            _localization = localization;
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _compensationManager = compensationManager;
            _compensationManager.PropertyChanged += OnCompensationManagerPropertyChanged;
            _parameters.PropertyChanged += OnParametersPropertyChanged;
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

            TeachAlignPositionCommand = new DelegateCommand<string>(
                async sys => await TeachAlignPositionAsync(int.Parse(sys ?? "1")),
                _ => !IsCalibrating)
                .ObservesProperty(() => IsCalibrating);

            System1Command = new DelegateCommand(() => SystemNumber = 1);
            System2Command = new DelegateCommand(() => SystemNumber = 2);

            UnlinkCompensationXCommand = new DelegateCommand(() => CompensationXLinkedVar = null);
            UnlinkCompensationYCommand = new DelegateCommand(() => CompensationYLinkedVar = null);
            UnlinkCompensationZCommand = new DelegateCommand(() => CompensationZLinkedVar = null);

            _eventAggregator.GetEvent<Recipe.Events.GlobalVariablesChangedEvent>().Subscribe(OnGlobalVariablesChanged, ThreadOption.UIThread);

            _ = InitializeAsync().ConfigureAwait(false);
        }

        /// <summary>初始化：创建默认全局变量，加载变量列表，再自动加载 JSON 配置</summary>
        private async Task InitializeAsync()
        {
            await EnsureDefaultCompGlobalVariablesAsync();
            await LoadGlobalVariablesAsync();
            await TryAutoLoadConfigAsync();
        }

        /// <summary>
        /// 执行四点寻针校准：抬安全高度 → 对针位 → 寻边 → Z 寻高 → 计算补偿（参考 ExecuteNeedleCalibrationAsync）
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

                var progress = new Progress<(string Status, double Progress)>(p =>
                {
                    CalibrationStatus = p.Status;
                    CalibrationProgress = p.Progress;
                });

                var result = await _needleMotion.ExecuteNeedleCalibrationAsync(
                    Parameters, SystemNumber, progress, token);

                if (result.Success)
                {
                    Parameters.CurrentXYZ = new PointF(
                        result.MeasuredCenter.X,
                        result.MeasuredCenter.Y,
                        (float)result.MeasuredHeight);
                    Parameters.CompensationXYZ = result.Compensation;

                    CalibrationProgress = 100;
                    OnCalibrationCompleted();
                    await SaveParametersAsync(syncGlobalVariables: false);

                    CalibrationStatus = _localization.GetResourceOrDefault("NeedleAligner_Status_Completed", "校准完成");
                    AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_CalibrationSuccess", "针头校准成功完成"));
                }
                else
                {
                    CalibrationStatus = string.Format(
                        _localization.GetResourceOrDefault("NeedleAligner_Status_Error", "校准异常: {0}"),
                        result.ErrorMessage ?? "未知错误");
                    AddLog(CalibrationStatus);
                }
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
        /// 停止校准运动
        /// </summary>
        private void StopCalibration()
        {
            try
            {
                _calibrationCts?.Cancel();
                _needleMotion.StopMotion(SystemNumber);
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
        /// 校准完成：记录偏差Δ，CalculatedComp = Δ + 表达式（应用前不清零基准）
        /// </summary>
        private void OnCalibrationCompleted()
        {
            try
            {
                double deltaX = CalibrationDeltaX;
                double deltaY = CalibrationDeltaY;
                double deltaZ = CalibrationDeltaZ;

                SaveCompensationHistory(deltaX, deltaY, deltaZ);

                CheckCompensationChange(deltaX, deltaY, deltaZ);

                RaiseCalibrationDeltaAndCalculatedChanged();

                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_CalibrationResult",
                        "校准完成 - 当前: X={0:F3}, Y={1:F3}, Z={2:F3}"),
                    Parameters.CurrentXYZ.X, Parameters.CurrentXYZ.Y, Parameters.CurrentXYZ.Z));
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_Delta",
                        "本次增量: ΔX={0:F3}, ΔY={1:F3}, ΔZ={2:F3}"),
                    deltaX, deltaY, deltaZ));
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_CalculatedComp",
                        "计算结果: X={0:F3}, Y={1:F3}, Z={2:F3}"),
                    CalculatedCompX, CalculatedCompY, CalculatedCompZ));
            }
            catch (Exception ex)
            {
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_ProcessResultError", "处理校准结果失败: {0}"),
                    ex.Message));
            }
        }

        /// <summary>
        /// 应用补偿：CalculatedComp 写入全局变量 → 基准跟进当前 → 表达式清零 → 保存参数
        /// </summary>
        private async Task ApplyCompensationAsync()
        {
            try
            {
                _dialogService.ShowDialog("NotificationDialog", new DialogParameters
                {
                    { "title", _localization.GetResourceOrDefault("NeedleAligner_Dialog_ApplyTitle", "确认应用补偿") },
                    { "message", string.Format(
                        _localization.GetResourceOrDefault("NeedleAligner_Dialog_ApplyToGlobalMessage",
                            "将以下补偿值写入全局变量：\nX={0:F3}, Y={1:F3}, Z={2:F3}\n并保存参数，确定继续吗？"),
                        CalculatedCompX, CalculatedCompY, CalculatedCompZ) },
                    { "icon", MaterialDesignThemes.Wpf.PackIconKind.HelpCircle }
                }, async result =>
                {
                    if (result.Result == ButtonResult.OK || result.Result == ButtonResult.Yes)
                    {
                        var appliedX = CalculatedCompX;
                        var appliedY = CalculatedCompY;
                        var appliedZ = CalculatedCompZ;

                        await WriteCompensationToGlobalVariablesAsync();
                        CommitZeroClearAfterApply();
                        await SaveParametersAsync(syncGlobalVariables: false);

                        AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_CompensationAppliedToGlobal", "补偿值已写入全局变量并保存参数"));
                        AddLog(string.Format(
                            _localization.GetResourceOrDefault("NeedleAligner_Log_CalculatedComp",
                                "计算结果: X={0:F3}, Y={1:F3}, Z={2:F3}"),
                            appliedX, appliedY, appliedZ));
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
        /// 将 CalculatedComp 写入用户链接的 Double 全局变量（链接名仅保存在 JSON）
        /// </summary>
        private async Task WriteCompensationToGlobalVariablesAsync()
        {
            var poolId = _recipePoolService?.CurrentPoolName ?? "Default";
            var variables = (await _recipePoolService.LoadGlobalVariablesAsync(poolId)).ToList();

            RemoveLegacyCompGlobalVariableEntries(variables);

            if (!string.IsNullOrEmpty(CompensationXLinkedVar))
                UpdateOrAddGlobalVariable(variables, CompensationXLinkedVar, CalculatedCompX.ToString("F6"), "针头校准X补偿", GlobalVariableType.Double);
            if (!string.IsNullOrEmpty(CompensationYLinkedVar))
                UpdateOrAddGlobalVariable(variables, CompensationYLinkedVar, CalculatedCompY.ToString("F6"), "针头校准Y补偿", GlobalVariableType.Double);
            if (!string.IsNullOrEmpty(CompensationZLinkedVar))
                UpdateOrAddGlobalVariable(variables, CompensationZLinkedVar, CalculatedCompZ.ToString("F6"), "针头校准Z补偿", GlobalVariableType.Double);

            for (int i = 0; i < variables.Count; i++)
                variables[i].Index = i + 1;

            await _recipePoolService.SaveGlobalVariablesAsync(poolId, variables);

            _eventAggregator?.GetEvent<GlobalVariablesChangedEvent>()?.Publish(poolId);
        }

        /// <summary>
        /// 清零法：应用后基准跟进当前测量值，表达式与补偿清零
        /// </summary>
        private void CommitZeroClearAfterApply()
        {
            var current = Parameters.CurrentXYZ ?? new PointF();
            Parameters.ReferenceXYZ = new PointF(current.X, current.Y, current.Z);

            CompensationManager.ResetCompensation();
            Parameters.CompensationXYZ = new PointF(0, 0, 0);
            CompensationXExpression = null;
            CompensationYExpression = null;
            CompensationZExpression = null;

            RaisePropertyChanged(nameof(CompensationX));
            RaisePropertyChanged(nameof(CompensationY));
            RaisePropertyChanged(nameof(CompensationZ));
            RaiseCalibrationDeltaAndCalculatedChanged();

            AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_ReferenceUpdated",
                "基准已更新为当前测量值，偏差与表达式已清零"));
        }

        /// <summary>更新或添加全局变量（默认 Double 类型）</summary>
        private static void UpdateOrAddGlobalVariable(List<GlobalVariable> variables, string name, string value, string comment)
            => UpdateOrAddGlobalVariable(variables, name, value, comment, GlobalVariableType.Double);

        /// <summary>更新或添加全局变量，支持指定类型</summary>
        private static void UpdateOrAddGlobalVariable(List<GlobalVariable> variables, string name, string value, string comment, GlobalVariableType type)
        {
            var existing = variables.FirstOrDefault(v => v.Name == name);
            if (existing != null)
            {
                existing.Value = value;
                existing.Type = type;
            }
            else
            {
                variables.Add(new GlobalVariable
                {
                    Name = name,
                    Type = type,
                    Value = value,
                    Comment = comment
                });
            }
        }

        private static string ResolveCompXLinkedVar(string linkedVarFromJson) =>
            string.IsNullOrWhiteSpace(linkedVarFromJson)
                ? NeedleAlignerGlobalVariableNames.DefaultCompXLinkedVar
                : linkedVarFromJson;

        private static string ResolveCompYLinkedVar(string linkedVarFromJson) =>
            string.IsNullOrWhiteSpace(linkedVarFromJson)
                ? NeedleAlignerGlobalVariableNames.DefaultCompYLinkedVar
                : linkedVarFromJson;

        private static string ResolveCompZLinkedVar(string linkedVarFromJson) =>
            string.IsNullOrWhiteSpace(linkedVarFromJson)
                ? NeedleAlignerGlobalVariableNames.DefaultCompZLinkedVar
                : linkedVarFromJson;

        /// <summary>无 JSON 配置时应用默认补偿链接，并同步到 Parameters</summary>
        private async Task ApplyDefaultLinkedVariablesAsync()
        {
            await EnsureDefaultCompGlobalVariablesAsync();
            await LoadGlobalVariablesAsync();

            CompensationXLinkedVar = NeedleAlignerGlobalVariableNames.DefaultCompXLinkedVar;
            CompensationYLinkedVar = NeedleAlignerGlobalVariableNames.DefaultCompYLinkedVar;
            CompensationZLinkedVar = NeedleAlignerGlobalVariableNames.DefaultCompZLinkedVar;
            Parameters.CompensationXLinkedVar = CompensationXLinkedVar;
            Parameters.CompensationYLinkedVar = CompensationYLinkedVar;
            Parameters.CompensationZLinkedVar = CompensationZLinkedVar;
        }

        /// <summary>在配方池创建默认 Double 补偿变量（若不存在）</summary>
        private async Task EnsureDefaultCompGlobalVariablesAsync()
        {
            if (_recipePoolService == null) return;

            try
            {
                var poolId = _recipePoolService.CurrentPoolName ?? "Default";
                var variables = (await _recipePoolService.LoadGlobalVariablesAsync(poolId)).ToList();
                var changed = false;

                RemoveLegacyCompGlobalVariableEntries(variables);

                changed |= EnsureDoubleGlobalVariable(variables,
                    NeedleAlignerGlobalVariableNames.DefaultCompXLinkedVar, "针头对针X补偿（默认）");
                changed |= EnsureDoubleGlobalVariable(variables,
                    NeedleAlignerGlobalVariableNames.DefaultCompYLinkedVar, "针头对针Y补偿（默认）");
                changed |= EnsureDoubleGlobalVariable(variables,
                    NeedleAlignerGlobalVariableNames.DefaultCompZLinkedVar, "针头对针Z补偿（默认）");

                if (!changed) return;

                for (int i = 0; i < variables.Count; i++)
                    variables[i].Index = i + 1;

                await _recipePoolService.SaveGlobalVariablesAsync(poolId, variables);
                _eventAggregator?.GetEvent<GlobalVariablesChangedEvent>()?.Publish(poolId);
                _logger.Info("[NeedleAligner] 已创建默认补偿全局变量");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleAligner] 创建默认补偿全局变量失败: {ex.Message}");
            }
        }

        /// <summary>确保 JSON 指定的链接目标在全局变量池中存在</summary>
        private async Task EnsureLinkedCompVariablesExistAsync(params string[] linkedVarNames)
        {
            if (_recipePoolService == null) return;

            var names = linkedVarNames?.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToArray();
            if (names == null || names.Length == 0) return;

            try
            {
                var poolId = _recipePoolService.CurrentPoolName ?? "Default";
                var variables = (await _recipePoolService.LoadGlobalVariablesAsync(poolId)).ToList();
                var changed = false;

                foreach (var name in names)
                    changed |= EnsureDoubleGlobalVariable(variables, name, "针头对针补偿链接变量");

                if (!changed) return;

                for (int i = 0; i < variables.Count; i++)
                    variables[i].Index = i + 1;

                await _recipePoolService.SaveGlobalVariablesAsync(poolId, variables);
                _eventAggregator?.GetEvent<GlobalVariablesChangedEvent>()?.Publish(poolId);
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleAligner] 确保链接变量存在失败: {ex.Message}");
            }
        }

        /// <summary>全局变量池不存在时添加 Double 变量，初始值 0</summary>
        private static bool EnsureDoubleGlobalVariable(List<GlobalVariable> variables, string name, string comment)
        {
            if (variables.Any(v => v.Name == name))
                return false;

            variables.Add(new GlobalVariable
            {
                Name = name,
                Type = GlobalVariableType.Double,
                Value = "0",
                Comment = comment
            });
            return true;
        }

        /// <summary>
        /// 清理旧版重复项：String 类型链接元数据、无 LinkedVar 后缀的重复 Double 变量
        /// </summary>
        private static void RemoveLegacyCompGlobalVariableEntries(List<GlobalVariable> variables)
        {
            variables.RemoveAll(v =>
                (v.Type == GlobalVariableType.String &&
                 (v.Name == NeedleAlignerGlobalVariableNames.DefaultCompXLinkedVar ||
                  v.Name == NeedleAlignerGlobalVariableNames.DefaultCompYLinkedVar ||
                  v.Name == NeedleAlignerGlobalVariableNames.DefaultCompZLinkedVar)) ||
                (v.Name == NeedleAlignerGlobalVariableNames.LegacyCompXKey ||
                 v.Name == NeedleAlignerGlobalVariableNames.LegacyCompYKey ||
                 v.Name == NeedleAlignerGlobalVariableNames.LegacyCompZKey));
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
                        CompensationXExpression = null;
                        CompensationYExpression = null;
                        CompensationZExpression = null;

                        RaisePropertyChanged(nameof(CompensationX));
                        RaisePropertyChanged(nameof(CompensationY));
                        RaisePropertyChanged(nameof(CompensationZ));
                        RaiseCalibrationDeltaAndCalculatedChanged();

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

        /// <summary>示教搜索点：读取当前 Dx/Dy 并写入对应搜索点</summary>
        private Task TeachSearchPointAsync(int step)
        {
            try
            {
                var positions = _needleMotion.ReadCurrentPositions(SystemNumber);
                if (!TryGetPosition(positions, out double x, "Dx") ||
                    !TryGetPosition(positions, out double y, "Dy"))
                {
                    AddLog(string.Format(
                        _localization.GetResourceOrDefault("NeedleAligner_Log_TeachSearchPointError", "搜索点示教失败: {0}"),
                        "未读取到 Dx/Dy 轴位置"));
                    return Task.CompletedTask;
                }

                switch (step)
                {
                    case 1: Parameters.SearchPoint1 = new PointF((float)x, (float)y); break;
                    case 2: Parameters.SearchPoint2 = new PointF((float)x, (float)y); break;
                    case 3: Parameters.SearchPoint3 = new PointF((float)x, (float)y); break;
                    case 4: Parameters.SearchPoint4 = new PointF((float)x, (float)y); break;
                }

                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_TeachSearchPoint", "搜索点{0}示教完成: X={1:F3}, Y={2:F3}"),
                    step, x, y));
            }
            catch (Exception ex)
            {
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_TeachSearchPointError", "搜索点示教失败: {0}"),
                    ex.Message));
            }

            return Task.CompletedTask;
        }

        /// <summary>示教对针位置：系统1(Dx Dy Dz₂)，系统2(Dx Dy Dz₃)</summary>
        private Task TeachAlignPositionAsync(int systemNumber)
        {
            try
            {
                var positions = _needleMotion.ReadCurrentPositions(systemNumber);
                if (!TryGetPosition(positions, out double x, "Dx") ||
                    !TryGetPosition(positions, out double y, "Dy") ||
                    !TryGetNeedleZ(positions, systemNumber, out double z))
                {
                    AddLog(string.Format(
                        _localization.GetResourceOrDefault("NeedleAligner_Log_TeachAlignNoAxis", "对针位置示教失败: 系统{0}未读取到 Dx/Dy/针尖Z轴"),
                        systemNumber));
                    return Task.CompletedTask;
                }

                var point = new PointF((float)x, (float)y, (float)z);
                if (systemNumber == 1)
                    Parameters.AlignPositionSystem1 = point;
                else
                    Parameters.AlignPositionSystem2 = point;

                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_TeachAlign", "系统{0}对针位置示教: X={1:F3}, Y={2:F3}, Z={3:F3}"),
                    systemNumber, x, y, z));
            }
            catch (Exception ex)
            {
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_TeachAlignError", "对针位置示教失败: {0}"),
                    ex.Message));
            }

            return Task.CompletedTask;
        }

        private static bool TryGetPosition(IReadOnlyDictionary<string, double> positions, out double value, params string[] names)
        {
            foreach (var name in names)
            {
                if (positions.TryGetValue(name, out value))
                    return true;
            }

            value = 0;
            return false;
        }

        private static bool TryGetNeedleZ(IReadOnlyDictionary<string, double> positions, int systemNumber, out double z)
        {
            var names = systemNumber == 1
                ? new[] { "Dz₂", "Dz2" }
                : new[] { "Dz₃", "Dz3" };
            return TryGetPosition(positions, out z, names);
        }

        /// <summary>
        /// 保存校准参数；syncGlobalVariables=true 时将 CalculatedComp 同步到全局变量池
        /// </summary>
        private async Task SaveParametersAsync(bool syncGlobalVariables = true)
        {
            try
            {
                CompensationManager.SaveToParameters(Parameters);
                Parameters.SystemNumber = SystemNumber;
                Parameters.LastCalibrationTime = DateTime.Now;
                Parameters.CompensationXLinkedVar = CompensationXLinkedVar;
                Parameters.CompensationYLinkedVar = CompensationYLinkedVar;
                Parameters.CompensationZLinkedVar = CompensationZLinkedVar;
                Parameters.CompensationXExpression = CompensationXExpression;
                Parameters.CompensationYExpression = CompensationYExpression;
                Parameters.CompensationZExpression = CompensationZExpression;

                var calibrationDir = GetCalibrationDirectory();
                var fileName = $"NeedleCalibration_System{SystemNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var filePath = Path.Combine(calibrationDir, fileName);

                var json = JsonConvert.SerializeObject(Parameters, Formatting.Indented);
                await File.WriteAllTextAsync(filePath, json);

                CurrentFilePath = filePath;
                CurrentFileName = fileName;
                await SaveCurrentFileToRecipePoolAsync();

                if (syncGlobalVariables)
                    await WriteCompensationToGlobalVariablesAsync();

                QueueCleanupOldConfigFiles(calibrationDir, filePath, SystemNumber);

                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_ParametersSaved",
                        "对针系统{0}参数保存成功"),
                    SystemNumber));
                if (syncGlobalVariables)
                {
                    AddLog(string.Format(
                        _localization.GetResourceOrDefault("NeedleAligner_Log_CalculatedComp",
                            "计算结果: X={0:F3}, Y={1:F3}, Z={2:F3}"),
                        CalculatedCompX,
                        CalculatedCompY,
                        CalculatedCompZ));
                }

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
        }

        /// <summary>
        /// 弹出文件对话框，从JSON文件加载校准参数
        /// </summary>
        private async Task LoadParametersAsync()
        {
            try
            {
                var calibrationDir = GetCalibrationDirectory();
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    InitialDirectory = calibrationDir
                };

                if (dialog.ShowDialog() != true) return;

                await LoadFromPathAsync(dialog.FileName);
            }
            catch (Exception ex)
            {
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_LoadError", "加载参数失败: {0}"),
                    ex.Message));
            }
        }

        /// <summary>
        /// 从指定路径加载校准参数并应用
        /// </summary>
        private async Task LoadFromPathAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_FileNotFound", "参数文件不存在"));
                    return;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var loaded = JsonConvert.DeserializeObject<NeedleCalibrationParams>(json);

                if (loaded != null)
                {
                    Parameters = loaded;
                    if (loaded.SystemNumber > 0)
                        SystemNumber = loaded.SystemNumber;

                    CompensationManager.LoadFromParameters(Parameters);

                    CompensationXLinkedVar = ResolveCompXLinkedVar(Parameters.CompensationXLinkedVar);
                    CompensationYLinkedVar = ResolveCompYLinkedVar(Parameters.CompensationYLinkedVar);
                    CompensationZLinkedVar = ResolveCompZLinkedVar(Parameters.CompensationZLinkedVar);
                    Parameters.CompensationXLinkedVar = CompensationXLinkedVar;
                    Parameters.CompensationYLinkedVar = CompensationYLinkedVar;
                    Parameters.CompensationZLinkedVar = CompensationZLinkedVar;
                    CompensationXExpression = Parameters.CompensationXExpression;
                    CompensationYExpression = Parameters.CompensationYExpression;
                    CompensationZExpression = Parameters.CompensationZExpression;

                    await EnsureLinkedCompVariablesExistAsync(
                        CompensationXLinkedVar, CompensationYLinkedVar, CompensationZLinkedVar);
                    await LoadGlobalVariablesAsync();

                    CurrentFilePath = filePath;
                    CurrentFileName = Path.GetFileName(filePath);

                    RaisePropertyChanged(nameof(CompensationX));
                    RaisePropertyChanged(nameof(CompensationY));
                    RaisePropertyChanged(nameof(CompensationZ));
                    RaiseCalibrationDeltaAndCalculatedChanged();

                    AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_ParametersLoaded", "针头校准参数加载成功"));
                    AddLog(string.Format(
                        _localization.GetResourceOrDefault("NeedleAligner_Log_Compensation",
                            "补偿值: X={0:F3}, Y={1:F3}, Z={2:F3}"),
                        CalculatedCompX,
                        CalculatedCompY,
                        CalculatedCompZ));

                    RaisePropertyChanged(nameof(CompensationManager));
                }
            }
            catch (Exception ex)
            {
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_LoadError", "加载参数失败: {0}"),
                    ex.Message));
            }
        }

        /// <summary>从配方池加载全局变量列表并刷新可链接集合（链接关系仅从 JSON 恢复）</summary>
        private async Task LoadGlobalVariablesAsync()
        {
            try
            {
                if (_recipePoolService == null) return;

                var poolId = _recipePoolService.CurrentPoolName ?? "Default";
                var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);

                AvailableGlobalVariables = new ObservableCollection<GlobalVariable>(variables);
                RefreshLinkableGlobalVariables();

                RaisePropertyChanged(nameof(IsCompensationXLinked));
                RaisePropertyChanged(nameof(IsCompensationYLinked));
                RaisePropertyChanged(nameof(IsCompensationZLinked));
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleAligner] 加载全局变量失败: {ex.Message}");
            }
        }

        /// <summary>外部全局变量变更时重新加载，同步下拉列表和链接变量值</summary>
        private async void OnGlobalVariablesChanged(string poolId)
        {
            try
            {
                var currentPoolId = _recipePoolService?.CurrentPoolName ?? "Default";
                if (!string.Equals(poolId, currentPoolId, StringComparison.OrdinalIgnoreCase))
                    return;

                var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);

                AvailableGlobalVariables.Clear();
                foreach (var v in variables)
                    AvailableGlobalVariables.Add(v);

                RefreshLinkableGlobalVariables();

                // 单向绑定：仅刷新下拉列表，不回读链接变量的数值到补偿
                if (IsCompensationXLinked)
                    RaisePropertyChanged(nameof(CalculatedCompX));
                if (IsCompensationYLinked)
                    RaisePropertyChanged(nameof(CalculatedCompY));
                if (IsCompensationZLinked)
                    RaisePropertyChanged(nameof(CalculatedCompZ));
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleAligner] 全局变量变更同步失败: {ex.Message}");
            }
        }

        /// <summary>刷新可链接的全局变量列表（仅保留 Double 类型，供 GlobalVariableLinkControl 使用）</summary>
        private void RefreshLinkableGlobalVariables()
        {
            var linkable = AvailableGlobalVariables
                .Where(v => v.Type == GlobalVariableType.Double)
                .ToList();
            LinkableGlobalVariables = new ObservableCollection<GlobalVariable>(linkable);
            RaisePropertyChanged(nameof(IsCompensationXLinked));
            RaisePropertyChanged(nameof(IsCompensationYLinked));
            RaisePropertyChanged(nameof(IsCompensationZLinked));
        }

        /// <summary>参数坐标变更时刷新增量与计算结果</summary>
        private void OnParametersPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(NeedleCalibrationParams.ReferenceXYZ)
                or nameof(NeedleCalibrationParams.CurrentXYZ))
            {
                RaiseCalibrationDeltaAndCalculatedChanged();
            }
        }

        /// <summary>补偿管理器数值变更时刷新计算结果</summary>
        private void OnCompensationManagerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(NeedleCompensationManager.CompensationX):
                    RaisePropertyChanged(nameof(CompensationX));
                    RaisePropertyChanged(nameof(CalculatedCompX));
                    break;
                case nameof(NeedleCompensationManager.CompensationY):
                    RaisePropertyChanged(nameof(CompensationY));
                    RaisePropertyChanged(nameof(CalculatedCompY));
                    break;
                case nameof(NeedleCompensationManager.CompensationZ):
                    RaisePropertyChanged(nameof(CompensationZ));
                    RaisePropertyChanged(nameof(CalculatedCompZ));
                    break;
            }
        }

        /// <summary>通知校准增量与计算结果属性变更</summary>
        private void RaiseCalibrationDeltaAndCalculatedChanged()
        {
            RaisePropertyChanged(nameof(CalibrationDeltaX));
            RaisePropertyChanged(nameof(CalibrationDeltaY));
            RaisePropertyChanged(nameof(CalibrationDeltaZ));
            RaisePropertyChanged(nameof(CalculatedCompX));
            RaisePropertyChanged(nameof(CalculatedCompY));
            RaisePropertyChanged(nameof(CalculatedCompZ));
        }

        /// <summary>安全计算数学表达式，如 "0.1+0.2+0.3"，失败返回0</summary>
        private static double EvaluateExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return 0;
            try
            {
                var result = new DataTable().Compute(expression, null);
                return Convert.ToDouble(result);
            }
            catch
            {
                return 0;
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
        /// 保存补偿历史记录（记录偏差Δ）
        /// </summary>
        private void SaveCompensationHistory(double deltaX, double deltaY, double deltaZ)
        {
            try
            {
                var record = new CompensationHistoryRecord
                {
                    SystemNumber = SystemNumber,
                    Timestamp = DateTime.Now,
                    CompensationX = deltaX,
                    CompensationY = deltaY,
                    CompensationZ = deltaZ,
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

        /// <summary>获取校准参数存储目录：Config/Calibration/System{N}</summary>
        private string GetCalibrationDirectory()
        {
            var dir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Config", "Calibration", $"System{SystemNumber}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>将当前文件路径保存到配方池 ExtensionData</summary>
        private async Task SaveCurrentFileToRecipePoolAsync()
        {
            try
            {
                var poolName = _recipePoolService?.CurrentPoolName ?? "Default";
                await _recipePoolService.SetExtensionDataAsync(poolName,
                    $"NeedleAligner_CurrentFile_System{SystemNumber}",
                    new NeedleAlignerFileRecord { FilePath = CurrentFilePath });
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleAligner] 保存文件记录到配方池失败: {ex.Message}");
            }
        }

        /// <summary>尝试从配方池记录自动加载最近使用的校准参数文件</summary>
        private async Task TryAutoLoadConfigAsync()
        {
            try
            {
                var poolName = _recipePoolService?.CurrentPoolName ?? "Default";
                var extKey = $"NeedleAligner_CurrentFile_System{SystemNumber}";
                var extData = await _recipePoolService.GetExtensionDataAsync<NeedleAlignerFileRecord>(poolName, extKey);

                if (extData?.FilePath != null && File.Exists(extData.FilePath))
                {
                    _logger.Info($"[NeedleAligner] 从配方池记录加载: {extData.FilePath}");
                    await LoadFromPathAsync(extData.FilePath);
                    return;
                }

                // 回退：加载目录中最新的配置文件
                var calibrationDir = GetCalibrationDirectory();
                var latest = Directory
                    .EnumerateFiles(calibrationDir, $"NeedleCalibration_System{SystemNumber}_*.json")
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .FirstOrDefault();

                if (latest != null)
                {
                    _logger.Info($"[NeedleAligner] 配方池无记录，加载最新文件: {latest}");
                    await LoadFromPathAsync(latest);
                    return;
                }

                await ApplyDefaultLinkedVariablesAsync();
                _logger.Info($"[NeedleAligner] 系统{SystemNumber}无可加载的校准配置文件，已应用默认补偿链接");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleAligner] 自动加载校准配置失败: {ex.Message}");
            }
        }

        /// <summary>后台异步清理过期校准配置文件，避免阻塞UI线程</summary>
        private void QueueCleanupOldConfigFiles(string configDir, string currentFilePath, int systemNumber)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    var cutoff = DateTime.Now.AddDays(-ConfigRetentionDays);
                    var cleanedCount = 0;

                    foreach (var file in Directory.EnumerateFiles(configDir, $"NeedleCalibration_System{systemNumber}_*.json"))
                    {
                        if (string.Equals(file, currentFilePath, StringComparison.OrdinalIgnoreCase))
                            continue;

                        try
                        {
                            if (File.GetLastWriteTime(file) >= cutoff)
                                continue;

                            File.Delete(file);
                            cleanedCount++;
                            _logger.Info($"[NeedleAligner] 已清理过期校准配置文件: {file}");
                        }
                        catch (Exception ex)
                        {
                            _logger.Warn($"[NeedleAligner] 清理文件失败: {file}, {ex.Message}");
                        }
                    }

                    if (cleanedCount > 0)
                        _logger.Info($"[NeedleAligner] 本次清理了 {cleanedCount} 个过期文件 (保留{ConfigRetentionDays}天)");
                }
                catch (Exception ex)
                {
                    _logger.Warn($"[NeedleAligner] 清理旧校准配置文件异常: {ex.Message}");
                }
            });
        }
    }

    /// <summary>记录最后使用的对针参数文件路径</summary>
    public class NeedleAlignerFileRecord
    {
        public string FilePath { get; set; }
    }
}
