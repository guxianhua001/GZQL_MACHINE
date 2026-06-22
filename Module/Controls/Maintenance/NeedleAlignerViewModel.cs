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
        private readonly ISafetyZoneMonitor _safetyZoneMonitor;
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
        /// <summary>搜索点移动取消令牌，供停止按钮中断运动</summary>
        private CancellationTokenSource _searchPointMoveCts;
        /// <summary>配置文件保留天数</summary>
        private const int ConfigRetentionDays = 30;

        /// <summary>各系统参数快照缓存（含文件路径），切换系统时保留未保存的编辑</summary>
        private readonly Dictionary<int, NeedleSystemState> _systemStateCache = new();

        private int _systemNumber = 1;
        /// <summary>当前对针系统（1 或 2），切换时加载对应参数集</summary>
        public int SystemNumber
        {
            get => _systemNumber;
            set
            {
                if (_systemNumber == value) return;
                var previous = _systemNumber;
                if (SetProperty(ref _systemNumber, value))
                    _ = SwitchSystemAsync(previous, value);
            }
        }

        /// <summary>当前系统对针位置标题（随 SystemNumber 变化）</summary>
        public string CurrentAlignPositionTitle =>
            string.Format(
                _localization.GetResourceOrDefault("NeedleAligner_AlignPositionForSystem", "系统{0} 对针位置"),
                SystemNumber);

        /// <summary>当前系统对针位置 X（绑定 Parameters 中对应系统）</summary>
        public double CurrentAlignX
        {
            get => GetCurrentAlignPosition().X;
            set => SetCurrentAlignComponent(v => v.X = (float)value, nameof(CurrentAlignX));
        }

        public double CurrentAlignY
        {
            get => GetCurrentAlignPosition().Y;
            set => SetCurrentAlignComponent(v => v.Y = (float)value, nameof(CurrentAlignY));
        }

        public double CurrentAlignZ
        {
            get => GetCurrentAlignPosition().Z;
            set => SetCurrentAlignComponent(v => v.Z = (float)value, nameof(CurrentAlignZ));
        }

        /// <summary>当前系统寻针高度（委托 Parameters.SearchNeedleHeight，随 SystemNumber 切换）</summary>
        public double CurrentSearchNeedleHeight
        {
            get => Parameters?.SearchNeedleHeight ?? 0;
            set
            {
                if (Parameters == null) return;
                Parameters.SearchNeedleHeight = value;
                RaisePropertyChanged(nameof(CurrentSearchNeedleHeight));
            }
        }

        /// <summary>X 寻针传感器 DI（随当前系统参数文件）</summary>
        public int SensorDiX
        {
            get => Parameters?.SensorDiX ?? 38;
            set
            {
                if (Parameters == null || Parameters.SensorDiX == value) return;
                Parameters.SensorDiX = value;
                RaisePropertyChanged(nameof(SensorDiX));
            }
        }

        /// <summary>Y 寻针传感器 DI</summary>
        public int SensorDiY
        {
            get => Parameters?.SensorDiY ?? 37;
            set
            {
                if (Parameters == null || Parameters.SensorDiY == value) return;
                Parameters.SensorDiY = value;
                RaisePropertyChanged(nameof(SensorDiY));
            }
        }

        /// <summary>搜索点传感器下拉：SensorX / SensorY</summary>
        public IReadOnlyList<NeedleSearchSensorOption> SearchSensorOptions { get; }

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
                    RaisePropertyChanged(nameof(CurrentSearchNeedleHeight));
                    RaisePropertyChanged(nameof(SensorDiX));
                    RaisePropertyChanged(nameof(SensorDiY));
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
                    RaisePropertyChanged(nameof(TcpTotalOffsetX));
                    RaisePropertyChanged(nameof(TcpTotalOffsetY));
                    RaisePropertyChanged(nameof(TcpTotalOffsetZ));
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

        private bool _isSearchPointMoving;
        /// <summary>搜索点是否正在移动（移动中可点停止）</summary>
        public bool IsSearchPointMoving
        {
            get => _isSearchPointMoving;
            private set => SetProperty(ref _isSearchPointMoving, value);
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
                {
                    RaisePropertyChanged(nameof(CalculatedCompX));
                    RaisePropertyChanged(nameof(TcpGlobalWritePreviewX));
                }
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
                {
                    RaisePropertyChanged(nameof(CalculatedCompY));
                    RaisePropertyChanged(nameof(TcpGlobalWritePreviewY));
                }
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
                {
                    RaisePropertyChanged(nameof(CalculatedCompZ));
                    RaisePropertyChanged(nameof(TcpGlobalWritePreviewZ));
                }
            }
        }

        /// <summary>本次待应用增量 ΔX = 基准X - 当前X（校准后有效；Apply 后刷新为实时偏差，不清零）</summary>
        private double _pendingIncrementX;
        private double _pendingIncrementY;
        private double _pendingIncrementZ;

        /// <summary>相对固定基准的实时偏差（只读参考）</summary>
        public double CalibrationDeltaX =>
            (Parameters?.ReferenceXYZ.X ?? 0) - (Parameters?.CurrentXYZ.X ?? 0);

        /// <summary>相对固定基准的实时偏差（只读参考）</summary>
        public double CalibrationDeltaY =>
            (Parameters?.ReferenceXYZ.Y ?? 0) - (Parameters?.CurrentXYZ.Y ?? 0);

        /// <summary>相对固定基准的实时偏差（只读参考）</summary>
        public double CalibrationDeltaZ =>
            ((Parameters?.CurrentXYZ.Z ?? 0) - Parameters?.ReferenceXYZ.Z ?? 0);

        /// <summary>本次待应用增量 X</summary>
        public double PendingIncrementX
        {
            get => _pendingIncrementX;
            private set
            {
                if (Math.Abs(_pendingIncrementX - value) < 0.0001) return;
                _pendingIncrementX = value;
                RaisePropertyChanged(nameof(PendingIncrementX));
                RaisePropertyChanged(nameof(TcpGlobalWritePreviewX));
            }
        }

        public double PendingIncrementY
        {
            get => _pendingIncrementY;
            private set
            {
                if (Math.Abs(_pendingIncrementY - value) < 0.0001) return;
                _pendingIncrementY = value;
                RaisePropertyChanged(nameof(PendingIncrementY));
                RaisePropertyChanged(nameof(TcpGlobalWritePreviewY));
            }
        }

        public double PendingIncrementZ
        {
            get => _pendingIncrementZ;
            private set
            {
                if (Math.Abs(_pendingIncrementZ - value) < 0.0001) return;
                _pendingIncrementZ = value;
                RaisePropertyChanged(nameof(PendingIncrementZ));
                RaisePropertyChanged(nameof(TcpGlobalWritePreviewZ));
            }
        }

        /// <summary>累计 TCP 补偿 X</summary>
        public double TcpTotalOffsetX => CompensationManager?.TcpTotalOffsetX ?? 0;

        /// <summary>累计 TCP 补偿 Y</summary>
        public double TcpTotalOffsetY => CompensationManager?.TcpTotalOffsetY ?? 0;

        /// <summary>累计 TCP 补偿 Z</summary>
        public double TcpTotalOffsetZ => CompensationManager?.TcpTotalOffsetZ ?? 0;

        /// <summary>X轴累计 TCP 补偿（可手动微调）</summary>
        public double CompensationX
        {
            get => CompensationManager?.TcpTotalOffsetX ?? 0;
            set
            {
                if (CompensationManager != null && Math.Abs(CompensationManager.TcpTotalOffsetX - value) > 0.0001)
                {
                    CompensationManager.TcpTotalOffsetX = value;
                    RaisePropertyChanged(nameof(TcpTotalOffsetX));
                    RaisePropertyChanged(nameof(CalculatedCompX));
                    RaisePropertyChanged(nameof(TcpGlobalWritePreviewX));
                }
            }
        }

        /// <summary>Y轴累计 TCP 补偿</summary>
        public double CompensationY
        {
            get => CompensationManager?.TcpTotalOffsetY ?? 0;
            set
            {
                if (CompensationManager != null && Math.Abs(CompensationManager.TcpTotalOffsetY - value) > 0.0001)
                {
                    CompensationManager.TcpTotalOffsetY = value;
                    RaisePropertyChanged(nameof(TcpTotalOffsetY));
                    RaisePropertyChanged(nameof(CalculatedCompY));
                    RaisePropertyChanged(nameof(TcpGlobalWritePreviewY));
                }
            }
        }

        /// <summary>Z轴累计 TCP 补偿</summary>
        public double CompensationZ
        {
            get => CompensationManager?.TcpTotalOffsetZ ?? 0;
            set
            {
                if (CompensationManager != null && Math.Abs(CompensationManager.TcpTotalOffsetZ - value) > 0.0001)
                {
                    CompensationManager.TcpTotalOffsetZ = value;
                    RaisePropertyChanged(nameof(TcpTotalOffsetZ));
                    RaisePropertyChanged(nameof(CalculatedCompZ));
                    RaisePropertyChanged(nameof(TcpGlobalWritePreviewZ));
                }
            }
        }

        /// <summary>全局变量值 = TCP 偏移 + 表达式偏移量</summary>
        public double CalculatedCompX =>
            TcpTotalOffsetX + EvaluateExpression(CompensationXExpression);

        /// <summary>全局变量值 Y</summary>
        public double CalculatedCompY =>
            TcpTotalOffsetY + EvaluateExpression(CompensationYExpression);

        /// <summary>全局变量值 Z</summary>
        public double CalculatedCompZ =>
            TcpTotalOffsetZ + EvaluateExpression(CompensationZExpression);

        /// <summary>Apply 预览/写入值 = 本次增量 + 表达式（Apply 后 TcpTotalOffset 与 Pending 一致）</summary>
        public double TcpGlobalWritePreviewX =>
            PendingIncrementX + EvaluateExpression(CompensationXExpression);

        /// <summary>Apply 预览 Y</summary>
        public double TcpGlobalWritePreviewY =>
            PendingIncrementY + EvaluateExpression(CompensationYExpression);

        /// <summary>Apply 预览 Z</summary>
        public double TcpGlobalWritePreviewZ =>
            PendingIncrementZ + EvaluateExpression(CompensationZExpression);

        /// <summary>X轴补偿是否已链接全局变量</summary>
        public bool IsCompensationXLinked => !string.IsNullOrEmpty(CompensationXLinkedVar);

        /// <summary>Y轴补偿是否已链接全局变量</summary>
        public bool IsCompensationYLinked => !string.IsNullOrEmpty(CompensationYLinkedVar);

        /// <summary>Z轴补偿是否已链接全局变量</summary>
        public bool IsCompensationZLinked => !string.IsNullOrEmpty(CompensationZLinkedVar);

        #region 步骤导航（两步：1=配置示教，2=校准应用）

        private const int MaxStep = 2;

        private int _currentStep = 1;
        /// <summary>当前工作流步骤（1=配置示教, 2=校准应用）</summary>
        public int CurrentStep
        {
            get => _currentStep;
            set
            {
                var clamped = Math.Clamp(value, 1, MaxStep);
                if (SetProperty(ref _currentStep, clamped))
                {
                    RaisePropertyChanged(nameof(Step1State));
                    RaisePropertyChanged(nameof(Step2State));
                    RaisePropertyChanged(nameof(CurrentStepTitle));
                    RaisePropertyChanged(nameof(IsStep1Active));
                    RaisePropertyChanged(nameof(IsStep2Active));
                    GoPrevCommand?.RaiseCanExecuteChanged();
                    GoNextCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>步骤1：配置示教</summary>
        public StepState Step1State => GetStepState(1);
        /// <summary>步骤2：校准应用</summary>
        public StepState Step2State => GetStepState(2);

        public bool IsStep1Active => CurrentStep == 1;
        public bool IsStep2Active => CurrentStep == 2;

        /// <summary>当前步骤标题</summary>
        public string CurrentStepTitle => CurrentStep switch
        {
            1 => _localization.GetResourceOrDefault("NeedleAligner_Step1_Title", "配置示教"),
            2 => _localization.GetResourceOrDefault("NeedleAligner_Step2_Title", "校准应用"),
            _ => ""
        };

        /// <summary>根据步骤号计算步骤状态</summary>
        private StepState GetStepState(int step)
        {
            if (step < CurrentStep) return StepState.Done;
            if (step == CurrentStep) return StepState.Active;
            return StepState.Pending;
        }

        /// <summary>上一步</summary>
        private void GoPrev() { if (CurrentStep > 1) CurrentStep--; }

        /// <summary>下一步</summary>
        private void GoNext() { if (CurrentStep < MaxStep) CurrentStep++; }

        #endregion

        public DelegateCommand StartCalibrationCommand { get; }
        public DelegateCommand StopCalibrationCommand { get; }
        public DelegateCommand ApplyCompensationCommand { get; }
        public DelegateCommand ResetCompensationCommand { get; }
        public DelegateCommand ShowCompensationHistoryCommand { get; }
        public DelegateCommand SaveParametersCommand { get; }
        public DelegateCommand LoadParametersCommand { get; }
        public DelegateCommand ClearLogCommand { get; }
        public DelegateCommand<string> TeachSearchPointCommand { get; }
        /// <summary>移动到搜索点（移动前确认 Z 轴安全高度）</summary>
        public DelegateCommand<string> MoveSearchPointCommand { get; }
        /// <summary>停止搜索点移动</summary>
        public DelegateCommand StopSearchPointMoveCommand { get; }
        public DelegateCommand TeachAlignPositionCommand { get; }
        /// <summary>示教当前系统寻针高度（仅读取针尖 Z）</summary>
        public DelegateCommand TeachSearchNeedleHeightCommand { get; }
        public DelegateCommand System1Command { get; }
        public DelegateCommand System2Command { get; }
        public DelegateCommand UnlinkCompensationXCommand { get; }
        public DelegateCommand UnlinkCompensationYCommand { get; }
        public DelegateCommand UnlinkCompensationZCommand { get; }
        /// <summary>上一步命令</summary>
        public DelegateCommand GoPrevCommand { get; }
        /// <summary>下一步命令</summary>
        public DelegateCommand GoNextCommand { get; }
        /// <summary>设置永久基准：将当前测量值写入 ReferenceXYZ</summary>
        public DelegateCommand SetPermanentReferenceCommand { get; }

        public NeedleAlignerViewModel(
            INeedleAlignerMotionService needleMotion,
            ISafetyZoneMonitor safetyZoneMonitor,
            IParameterStorage parameterStorage,
            ILoggerService logger,
            ILocalizationService localization,
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            NeedleCompensationManager compensationManager,
            IRecipePoolService recipePoolService)
        {
            _needleMotion = needleMotion;
            _safetyZoneMonitor = safetyZoneMonitor;
            _parameterStorage = parameterStorage;
            _logger = logger;
            _localization = localization;
            SearchSensorOptions = BuildSearchSensorOptions();
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
                _ => !IsCalibrating && !IsSearchPointMoving)
                .ObservesProperty(() => IsCalibrating)
                .ObservesProperty(() => IsSearchPointMoving);

            MoveSearchPointCommand = new DelegateCommand<string>(
                step => MoveSearchPointAsync(int.Parse(step ?? "1")),
                _ => !IsCalibrating && !IsSearchPointMoving)
                .ObservesProperty(() => IsCalibrating)
                .ObservesProperty(() => IsSearchPointMoving);

            StopSearchPointMoveCommand = new DelegateCommand(
                StopSearchPointMove,
                () => IsSearchPointMoving)
                .ObservesProperty(() => IsSearchPointMoving);

            TeachAlignPositionCommand = new DelegateCommand(
                () => TeachAlignPosition(),
                () => !IsCalibrating)
                .ObservesProperty(() => IsCalibrating);

            TeachSearchNeedleHeightCommand = new DelegateCommand(
                () => TeachSearchNeedleHeight(),
                () => !IsCalibrating)
                .ObservesProperty(() => IsCalibrating);

            System1Command = new DelegateCommand(() => SystemNumber = 1);
            System2Command = new DelegateCommand(() => SystemNumber = 2);

            UnlinkCompensationXCommand = new DelegateCommand(() => CompensationXLinkedVar = null);
            UnlinkCompensationYCommand = new DelegateCommand(() => CompensationYLinkedVar = null);
            UnlinkCompensationZCommand = new DelegateCommand(() => CompensationZLinkedVar = null);

            GoPrevCommand = new DelegateCommand(GoPrev, () => CurrentStep > 1)
                .ObservesProperty(() => CurrentStep);
            GoNextCommand = new DelegateCommand(GoNext, () => CurrentStep < MaxStep)
                .ObservesProperty(() => CurrentStep);

            SetPermanentReferenceCommand = new DelegateCommand(
                SetPermanentReference,
                () => !IsCalibrating)
                .ObservesProperty(() => IsCalibrating);

            _eventAggregator.GetEvent<Recipe.Events.GlobalVariablesChangedEvent>().Subscribe(OnGlobalVariablesChanged, ThreadOption.UIThread);

            _ = InitializeAsync().ConfigureAwait(false);
        }

        /// <summary>初始化：创建默认全局变量，加载变量列表，再自动加载 JSON 配置</summary>
        private async Task InitializeAsync()
        {
            await EnsureDefaultCompGlobalVariablesAsync();
            await LoadGlobalVariablesAsync();
            await TryAutoLoadConfigAsync();
            await EnsureSystemCachedAsync(SystemNumber == 1 ? 2 : 1);
            RaiseSystemUiProperties();
        }

        /// <summary>
        /// 执行四点寻针校准（后台线程，避免阻塞 UI）
        /// </summary>
        private async Task  StartCalibrationAsync()
        {
            IsCalibrating = true;
            CalibrationStatus = _localization.GetResourceOrDefault("NeedleAligner_Status_Starting", "开始校准...");
            CalibrationProgress = 0;
            _calibrationCts = new CancellationTokenSource();
            var token = _calibrationCts.Token;

            // 捕获当前系统参数快照，防止校准过程中 UI 编辑与系统切换干扰
            var systemNumber = SystemNumber;
            var parametersSnapshot = Parameters?.Clone() ?? new NeedleCalibrationParams();

            var progress = new Progress<NeedleAlignerProgressReport>(p =>
            {
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    if (!string.IsNullOrEmpty(p.Status))
                        CalibrationStatus = p.Status;
                    CalibrationProgress = p.Progress;
                    if (!string.IsNullOrEmpty(p.DetailLog))
                        AddLog(p.DetailLog);
                });
            });

            try
            {
                var result = await Task.Run(async () =>
                    await _needleMotion.ExecuteNeedleCalibrationAsync(
                        parametersSnapshot, systemNumber, progress, token), token).ConfigureAwait(true);

                if (result.Success)
                {
                    Parameters.CurrentXYZ = new PointF(
                        result.MeasuredCenter.X,
                        result.MeasuredCenter.Y,
                        (float)result.MeasuredHeight);
                    Parameters.CompensationXYZ = result.Compensation;
                    StashCurrentSystemState(systemNumber);

                    CalibrationProgress = 100;
                    OnCalibrationCompleted();
                    await SaveParametersAsync(syncGlobalVariables: false);

                    CalibrationStatus = _localization.GetResourceOrDefault("NeedleAligner_Status_Completed", "校准完成");
                    AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_CalibrationSuccess", "针头校准成功完成"));
                }
                else
                {
                    var errorMsg = result.ErrorMessage ?? _localization.GetResourceOrDefault("NeedleAligner_Status_UnknownError", "未知错误");
                    CalibrationStatus = string.Format(
                        _localization.GetResourceOrDefault("NeedleAligner_Status_Error", "校准异常: {0}"),
                        errorMsg);
                    AddLog(CalibrationStatus);

                    // 传感器搜索失败弹窗报警，确认后抬 Z
                    ShowCalibrationAlarmDialog(errorMsg);
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
                // MotionService 已通过 _logger.Error 记录完整异常堆栈，此处仅更新 UI 日志避免重复

                // 异常弹窗报警，确认后抬 Z
                ShowCalibrationAlarmDialog(ex.Message);
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
        /// 校准完成：计算相对固定基准的本次增量；Apply 后 TcpTotalOffset 将设为 Pending
        /// </summary>
        private void OnCalibrationCompleted()
        {
            try
            {
                PendingIncrementX = CalibrationDeltaX;
                PendingIncrementY = CalibrationDeltaY;
                PendingIncrementZ = CalibrationDeltaZ;

                SaveCompensationHistory(PendingIncrementX, PendingIncrementY, PendingIncrementZ, recordPendingOnly: true);
                CheckCompensationChange(PendingIncrementX, PendingIncrementY, PendingIncrementZ);
                RaiseCalibrationDeltaAndCalculatedChanged();

                // 标定数据写入 logs info
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_ReferenceXYZ",
                        "固定基准: X={0:F3}, Y={1:F3}, Z={2:F3}"),
                    Parameters.ReferenceXYZ.X, Parameters.ReferenceXYZ.Y, Parameters.ReferenceXYZ.Z));
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_CalibrationResult",
                        "校准完成 - 当前: X={0:F3}, Y={1:F3}, Z={2:F3}"),
                    Parameters.CurrentXYZ.X, Parameters.CurrentXYZ.Y, Parameters.CurrentXYZ.Z));
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_Delta",
                        "本次增量: ΔX={0:F3}, ΔY={1:F3}, ΔZ={2:F3}"),
                    PendingIncrementX, PendingIncrementY, PendingIncrementZ));
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_TcpTotalOffset",
                        "累计TCP: X={0:F3}, Y={1:F3}, Z={2:F3}"),
                    TcpTotalOffsetX, TcpTotalOffsetY, TcpTotalOffsetZ));
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_CalculatedComp",
                        "全局变量值: X={0:F3}, Y={1:F3}, Z={2:F3}"),
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
        /// 应用补偿：TcpTotalOffset = PendingIncrement → 全局变量 = TcpTotalOffset + 表达式 → Pending 不清零
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
                            "将以下值写入全局变量：\nX={0:F3}, Y={1:F3}, Z={2:F3}\n（TCP 偏移 + 表达式偏移量）\n固定基准不变，确定继续吗？"),
                        TcpGlobalWritePreviewX, TcpGlobalWritePreviewY, TcpGlobalWritePreviewZ) },
                    { "icon", MaterialDesignThemes.Wpf.PackIconKind.HelpCircle }
                }, async result =>
                {
                    if (result.Result == ButtonResult.OK || result.Result == ButtonResult.Yes)
                    {
                        var (globalX, globalY, globalZ) = ApplyTcpOffsetFromPending();

                        // 2. 写入全局变量
                        await WriteCompensationToGlobalVariablesAsync(globalX, globalY, globalZ);
                        await SaveParametersAsync(syncGlobalVariables: false);

                        AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_CompensationAppliedToGlobal", "TCP 总计已写入全局变量并保存参数"));
                        AddLog(string.Format(
                            _localization.GetResourceOrDefault("NeedleAligner_Log_CalculatedComp",
                                "全局变量写入值: X={0:F3}, Y={1:F3}, Z={2:F3}"),
                            globalX, globalY, globalZ));
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
        /// 将 TcpTotalOffset + 表达式偏移量写入用户链接的 Double 全局变量
        /// </summary>
        private async Task WriteCompensationToGlobalVariablesAsync(double valueX, double valueY, double valueZ)
        {
            var poolId = _recipePoolService?.CurrentPoolName ?? "Default";
            var variables = (await _recipePoolService.LoadGlobalVariablesAsync(poolId)).ToList();

            RemoveLegacyCompGlobalVariableEntries(variables);

            if (!string.IsNullOrEmpty(CompensationXLinkedVar))
                UpdateOrAddGlobalVariable(variables, CompensationXLinkedVar, valueX.ToString("F6"), "针头校准X补偿", GlobalVariableType.Double);
            if (!string.IsNullOrEmpty(CompensationYLinkedVar))
                UpdateOrAddGlobalVariable(variables, CompensationYLinkedVar, valueY.ToString("F6"), "针头校准Y补偿", GlobalVariableType.Double);
            if (!string.IsNullOrEmpty(CompensationZLinkedVar))
                UpdateOrAddGlobalVariable(variables, CompensationZLinkedVar, valueZ.ToString("F6"), "针头校准Z补偿", GlobalVariableType.Double);

            for (int i = 0; i < variables.Count; i++)
                variables[i].Index = i + 1;

            await _recipePoolService.SaveGlobalVariablesAsync(poolId, variables);

            _eventAggregator?.GetEvent<GlobalVariablesChangedEvent>()?.Publish(poolId);
        }

        /// <summary>
        /// Apply：TcpTotalOffset 设为 PendingIncrement（替换，非累加）；全局变量 = TcpTotalOffset + 表达式
        /// </summary>
        private (double GlobalX, double GlobalY, double GlobalZ) ApplyTcpOffsetFromPending()
        {
            var exprX = EvaluateExpression(CompensationXExpression);
            var exprY = EvaluateExpression(CompensationYExpression);
            var exprZ = EvaluateExpression(CompensationZExpression);

            // TCP 偏移 = 本次增量（与 Pending 一致，非历史累加）
            CompensationManager.ApplyPendingOffset(
                PendingIncrementX, PendingIncrementY, PendingIncrementZ);

            Parameters.CompensationXYZ = new PointF(
                (float)CompensationManager.TcpTotalOffsetX,
                (float)CompensationManager.TcpTotalOffsetY,
                (float)CompensationManager.TcpTotalOffsetZ);

            // 全局变量 = TCP 偏移 + 表达式
            var globalX = TcpTotalOffsetX + exprX;
            var globalY = TcpTotalOffsetY + exprY;
            var globalZ = TcpTotalOffsetZ + exprZ;

            // Apply 后 Pending 刷新为实时偏差，不清零
            RefreshPendingIncrementFromDelta();
            CompensationXExpression = null;
            CompensationYExpression = null;
            CompensationZExpression = null;

            RaisePropertyChanged(nameof(CompensationX));
            RaisePropertyChanged(nameof(CompensationY));
            RaisePropertyChanged(nameof(CompensationZ));
            RaisePropertyChanged(nameof(TcpTotalOffsetX));
            RaisePropertyChanged(nameof(TcpTotalOffsetY));
            RaisePropertyChanged(nameof(TcpTotalOffsetZ));
            RaiseCalibrationDeltaAndCalculatedChanged();

            SaveCompensationHistory(globalX, globalY, globalZ, recordPendingOnly: false);

            AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_TcpTotalApplied",
                "TCP 偏移已设为本次增量，固定基准保持不变"));
            AddLog(string.Format(
                _localization.GetResourceOrDefault("NeedleAligner_Log_TcpTotalOffset",
                    "累计TCP: X={0:F3}, Y={1:F3}, Z={2:F3}"),
                TcpTotalOffsetX, TcpTotalOffsetY, TcpTotalOffsetZ));

            return (globalX, globalY, globalZ);
        }

        /// <summary>清空待应用增量（重置等场景使用）</summary>
        private void ClearPendingIncrement()
        {
            PendingIncrementX = 0;
            PendingIncrementY = 0;
            PendingIncrementZ = 0;
        }

        /// <summary>将 PendingIncrement 刷新为当前实时偏差 CalibrationDelta（Ref - Current）</summary>
        private void RefreshPendingIncrementFromDelta()
        {
            PendingIncrementX = CalibrationDeltaX;
            PendingIncrementY = CalibrationDeltaY;
            PendingIncrementZ = CalibrationDeltaZ;
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
                        "此操作将重置累计 TCP 偏移与待应用增量到零。\n固定基准 ReferenceXYZ 不会改变。\n此操作不可逆，确定要继续吗？") },
                    { "icon", MaterialDesignThemes.Wpf.PackIconKind.AlertCircle }
                }, result =>
                {
                    if (result.Result == ButtonResult.OK || result.Result == ButtonResult.Yes)
                    {
                        CompensationManager.ResetTcpTotalOffset();
                        Parameters.CompensationXYZ = new PointF(0, 0, 0);
                        // Reset 后 PendingIncrement 刷新为当前实时偏差，不清零
                        RefreshPendingIncrementFromDelta();
                        CompensationXExpression = null;
                        CompensationYExpression = null;
                        CompensationZExpression = null;

                        RaisePropertyChanged(nameof(CompensationX));
                        RaisePropertyChanged(nameof(CompensationY));
                        RaisePropertyChanged(nameof(CompensationZ));
                        RaisePropertyChanged(nameof(TcpTotalOffsetX));
                        RaisePropertyChanged(nameof(TcpTotalOffsetY));
                        RaisePropertyChanged(nameof(TcpTotalOffsetZ));
                        RaiseCalibrationDeltaAndCalculatedChanged();

                        AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_CompensationReset", "累计 TCP 偏移已重置为零"));
                        AddLog(string.Format(
                            _localization.GetResourceOrDefault("NeedleAligner_Log_TcpTotalOffset",
                                "累计TCP: X={0:F3}, Y={1:F3}, Z={2:F3}"),
                            TcpTotalOffsetX, TcpTotalOffsetY, TcpTotalOffsetZ));
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
                            "{0:yyyy-MM-dd HH:mm:ss} | 增量 X={1:F3}, Y={2:F3}, Z={3:F3} | 累计TCP X={4:F3}, Y={5:F3}, Z={6:F3} | 操作员: {7}"),
                        record.Timestamp,
                        record.CompensationX, record.CompensationY, record.CompensationZ,
                        record.TcpTotalOffsetX, record.TcpTotalOffsetY, record.TcpTotalOffsetZ,
                        record.Operator));
                    }
                    AddLog(string.Format(
                        _localization.GetResourceOrDefault("NeedleAligner_Log_TcpTotalOffset",
                            "累计TCP: X={0:F3}, Y={1:F3}, Z={2:F3}"),
                        TcpTotalOffsetX, TcpTotalOffsetY, TcpTotalOffsetZ));
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

        /// <summary>移动到搜索点：先提示 Z 轴安全高度，确认后以搜索速度执行 XY 移动</summary>
        private void MoveSearchPointAsync(int step)
        {
            if (!TryGetSearchPointCoordinates(step, out float x, out float y))
            {
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_MoveSearchPointError", "移动到搜索点失败: {0}"),
                    _localization.GetResourceOrDefault("NeedleAligner_Error_InvalidSearchPoint", "无效的搜索点编号")));
                return;
            }

            var message = BuildMoveSearchPointConfirmMessage(step, x, y);
            _dialogService.ShowDialog("NotificationDialog", new DialogParameters
            {
                {
                    "title",
                    _localization.GetResourceOrDefault("NeedleAligner_Dialog_MoveSearchPointTitle", "移动到搜索点")
                },
                { "message", message },
                { "icon", MaterialDesignThemes.Wpf.PackIconKind.ShieldAlertOutline }
            }, async result =>
            {
                if (result.Result != ButtonResult.OK && result.Result != ButtonResult.Yes)
                    return;

                await ExecuteMoveSearchPointAsync(step, x, y);
            });
        }

        /// <summary>读取指定搜索点坐标</summary>
        private bool TryGetSearchPointCoordinates(int step, out float x, out float y)
        {
            x = y = 0;
            if (Parameters == null)
                return false;

            switch (step)
            {
                case 1:
                    x = Parameters.SearchPoint1X;
                    y = Parameters.SearchPoint1Y;
                    return true;
                case 2:
                    x = Parameters.SearchPoint2X;
                    y = Parameters.SearchPoint2Y;
                    return true;
                case 3:
                    x = Parameters.SearchPoint3X;
                    y = Parameters.SearchPoint3Y;
                    return true;
                case 4:
                    x = Parameters.SearchPoint4X;
                    y = Parameters.SearchPoint4Y;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>构建移动前 Z 轴安全高度确认文案</summary>
        private string BuildMoveSearchPointConfirmMessage(int step, float x, float y)
        {
            var speed = Parameters?.SearchSpeed ?? 5.0;
            var safeHeight = Parameters?.SafeHeight ?? 0;
            var lowAxes = _safetyZoneMonitor?.GetSafetyStatus()?.LowHeightAxisNames;

            if (lowAxes != null && lowAxes.Count > 0)
            {
                return string.Format(
                    _localization.GetResourceOrDefault(
                        "NeedleAligner_Dialog_MoveSearchPointUnsafeZ",
                        "以下 Z 轴未达安全高度：{0}\n移动前将先抬升当前针头 Z 轴至参数安全高度 ({1:F3} mm)，再以搜索速度 {2:F1} mm/s 移动到 P{3} (X={4:F3}, Y={5:F3})。\n是否继续？"),
                    string.Join(", ", lowAxes),
                    safeHeight,
                    speed,
                    step,
                    x,
                    y);
            }

            return string.Format(
                _localization.GetResourceOrDefault(
                    "NeedleAligner_Dialog_MoveSearchPointSafeZ",
                    "所有 Z 轴已在安全高度。\n将以搜索速度 {0:F1} mm/s 移动到搜索点 P{1} (X={2:F3}, Y={3:F3})。\n是否继续？"),
                speed,
                step,
                x,
                y);
        }

        /// <summary>执行搜索点 XY 移动（内部先抬 Z 至安全高度）</summary>
        private async Task ExecuteMoveSearchPointAsync(int step, float x, float y)
        {
            _searchPointMoveCts?.Cancel();
            _searchPointMoveCts?.Dispose();
            _searchPointMoveCts = new CancellationTokenSource();
            var token = _searchPointMoveCts.Token;
            var systemNumber = SystemNumber;
            var parametersSnapshot = Parameters?.Clone() ?? new NeedleCalibrationParams();

            try
            {
                IsSearchPointMoving = true;
                AddLog(string.Format(
                    _localization.GetResourceOrDefault(
                        "NeedleAligner_Log_MoveSearchPointStart",
                        "开始移动到搜索点 P{0}: X={1:F3}, Y={2:F3}，速度={3:F1} mm/s"),
                    step, x, y, parametersSnapshot.SearchSpeed));

                await _needleMotion.MoveToSearchPointXYAsync(
                    parametersSnapshot, systemNumber, x, y, token);

                AddLog(string.Format(
                    _localization.GetResourceOrDefault(
                        "NeedleAligner_Log_MoveSearchPointDone",
                        "搜索点 P{0} 移动完成: X={1:F3}, Y={2:F3}"),
                    step, x, y));
            }
            catch (OperationCanceledException)
            {
                AddLog(_localization.GetResourceOrDefault(
                    "NeedleAligner_Log_MoveSearchPointStopped", "搜索点移动已停止"));
            }
            catch (Exception ex)
            {
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_MoveSearchPointError", "移动到搜索点失败: {0}"),
                    ex.Message));
                _logger.Warn($"[NeedleAligner] 移动到搜索点 P{step} 失败: {ex.Message}");
            }
            finally
            {
                IsSearchPointMoving = false;
                _searchPointMoveCts?.Dispose();
                _searchPointMoveCts = null;
            }
        }

        /// <summary>停止搜索点移动：取消任务并立即停止相关轴</summary>
        private void StopSearchPointMove()
        {
            try
            {
                _searchPointMoveCts?.Cancel();
                _needleMotion.StopMotion(SystemNumber);
            }
            catch (Exception ex)
            {
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_StopSearchPointMoveError", "停止搜索点移动失败: {0}"),
                    ex.Message));
            }
        }

        /// <summary>示教当前系统对针位置（内部仍读 Dx/Dy/针尖 Z 轴）</summary>
        private void TeachAlignPosition()
        {
            try
            {
                var positions = _needleMotion.ReadCurrentPositions(SystemNumber);
                if (!TryGetPosition(positions, out double x, "Dx") ||
                    !TryGetPosition(positions, out double y, "Dy") ||
                    !TryGetNeedleZ(positions, SystemNumber, out double z))
                {
                    AddLog(string.Format(
                        _localization.GetResourceOrDefault("NeedleAligner_Log_TeachAlignNoAxis", "对针位置示教失败: 系统{0}未读取到运动轴"),
                        SystemNumber));
                    return;
                }

                SetCurrentAlignPosition(new PointF((float)x, (float)y, (float)z));
                // 对针示教 Z 同步到当前系统寻针高度
                if (Parameters != null)
                    Parameters.SearchNeedleHeight = z;
                StashCurrentSystemState(SystemNumber);

                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_TeachAlign", "系统{0}对针位置示教: X={1:F3}, Y={2:F3}, Z={3:F3}"),
                    SystemNumber, x, y, z));
            }
            catch (Exception ex)
            {
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_TeachAlignError", "对针位置示教失败: {0}"),
                    ex.Message));
            }
        }

        /// <summary>设置永久基准：将当前测量值 CurrentXYZ 写入 ReferenceXYZ，并持久化保存</summary>
        private async void SetPermanentReference()
        {
            if (Parameters == null) return;
            var cur = Parameters.CurrentXYZ;
            Parameters.ReferenceXYZ = new PointF(cur.X, cur.Y, cur.Z);
            StashCurrentSystemState(SystemNumber);
            RaiseCalibrationDeltaAndCalculatedChanged();

            // 持久化保存永久基准参数
            await SaveParametersAsync(syncGlobalVariables: false);

            AddLog(string.Format(
                _localization.GetResourceOrDefault("NeedleAligner_Log_SetPermanentReference",
                    "永久基准已设置: X={0:F3}, Y={1:F3}, Z={2:F3}"),
                cur.X, cur.Y, cur.Z));
        }

        /// <summary>校准失败弹窗报警：显示错误信息，确认后抬 Z 到安全高度</summary>
        private void ShowCalibrationAlarmDialog(string errorMessage)
        {
            _dialogService.ShowDialog("NotificationDialog", new DialogParameters
            {
                { "title", _localization.GetResourceOrDefault("NeedleAligner_Dialog_AlarmTitle", "寻针报警") },
                { "message", string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Dialog_AlarmMessage",
                        "自动寻针失败：{0}\n\n确认后将抬起 Z 轴到安全高度。"),
                    errorMessage) },
                { "icon", MaterialDesignThemes.Wpf.PackIconKind.AlertCircle }
            }, result =>
            {
                if (result.Result == ButtonResult.OK || result.Result == ButtonResult.Yes)
                {
                    // 确认后抬 Z 到安全高度
                    _ = RaiseZAfterFailureAsync();
                }
            });
        }

        /// <summary>寻针失败确认后抬 Z 到安全高度</summary>
        private async Task RaiseZAfterFailureAsync()
        {
            try
            {
                if (Parameters == null) return;
                var cts = new CancellationTokenSource();
                await _needleMotion.MoveToSafeHeightAsync(Parameters, SystemNumber, cts.Token);
                AddLog(_localization.GetResourceOrDefault(
                    "NeedleAligner_Log_RaiseZAfterFailure", "寻针失败后已抬升至安全高度"));
            }
            catch (Exception ex)
            {
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_RaiseZError", "抬升安全高度失败: {0}"),
                    ex.Message));
            }
        }

        /// <summary>示教当前系统寻针高度（仅读取针尖 Z 轴位置）</summary>
        private void TeachSearchNeedleHeight()
        {
            try
            {
                var positions = _needleMotion.ReadCurrentPositions(SystemNumber);
                if (!TryGetNeedleZ(positions, SystemNumber, out double z))
                {
                    AddLog(string.Format(
                        _localization.GetResourceOrDefault("NeedleAligner_Log_TeachSearchHeightNoAxis", "寻针高度示教失败: 系统{0}未读取到针尖Z轴"),
                        SystemNumber));
                    return;
                }

                if (Parameters != null)
                    Parameters.SearchNeedleHeight = z;
                StashCurrentSystemState(SystemNumber);

                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_TeachSearchHeight", "系统{0}寻针高度示教: Z={1:F3}"),
                    SystemNumber, z));
            }
            catch (Exception ex)
            {
                AddLog(string.Format(
                    _localization.GetResourceOrDefault("NeedleAligner_Log_TeachSearchHeightError", "寻针高度示教失败: {0}"),
                    ex.Message));
            }
        }

        private PointF GetCurrentAlignPosition() =>
            SystemNumber == 1 ? Parameters.AlignPositionSystem1 : Parameters.AlignPositionSystem2;

        private void SetCurrentAlignPosition(PointF point)
        {
            if (SystemNumber == 1)
                Parameters.AlignPositionSystem1 = point;
            else
                Parameters.AlignPositionSystem2 = point;

            RaisePropertyChanged(nameof(CurrentAlignX));
            RaisePropertyChanged(nameof(CurrentAlignY));
            RaisePropertyChanged(nameof(CurrentAlignZ));
        }

        private void SetCurrentAlignComponent(Action<PointF> update, string propertyName)
        {
            var p = GetCurrentAlignPosition();
            update(p);
            SetCurrentAlignPosition(p);
            RaisePropertyChanged(propertyName);
        }

        /// <summary>兼容旧版 JSON：未配置寻针高度时沿用对针位置 Z</summary>
        private static void MigrateLegacySearchNeedleHeight(NeedleCalibrationParams parameters)
        {
            if (parameters == null) return;

            if (Math.Abs(parameters.SearchNeedleHeightSystem1) < 1e-6
                && Math.Abs(parameters.AlignPositionSystem1.Z) > 1e-6)
            {
                parameters.SearchNeedleHeightSystem1 = parameters.AlignPositionSystem1.Z;
            }

            if (Math.Abs(parameters.SearchNeedleHeightSystem2) < 1e-6
                && Math.Abs(parameters.AlignPositionSystem2.Z) > 1e-6)
            {
                parameters.SearchNeedleHeightSystem2 = parameters.AlignPositionSystem2.Z;
            }
        }

        /// <summary>切换系统：缓存旧系统完整状态，加载新系统缓存或磁盘配置</summary>
        private async Task SwitchSystemAsync(int previousSystem, int newSystem)
        {
            if (IsCalibrating)
            {
                _systemNumber = previousSystem;
                RaisePropertyChanged(nameof(SystemNumber));
                AddLog(_localization.GetResourceOrDefault(
                    "NeedleAligner_Log_SystemSwitchBlocked",
                    "校准进行中，无法切换针头系统"));
                return;
            }

            try
            {
                StashCurrentSystemState(previousSystem);

                if (_systemStateCache.TryGetValue(newSystem, out var cached))
                {
                    ApplySystemState(cached, newSystem);
                    AddLog(string.Format(
                        _localization.GetResourceOrDefault(
                            "NeedleAligner_Log_SystemSwitched",
                            "已切换到针头系统{0}"),
                        newSystem));
                    return;
                }

                var loaded = await LoadSystemStateFromDiskAsync(newSystem);
                _systemStateCache[newSystem] = loaded;
                ApplySystemState(loaded, newSystem);
                AddLog(string.Format(
                    _localization.GetResourceOrDefault(
                        "NeedleAligner_Log_SystemSwitched",
                        "已切换到针头系统{0}"),
                    newSystem));
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleAligner] 切换系统失败: {ex.Message}");
            }
        }

        /// <summary>将当前 UI 状态同步回 Parameters，便于按系统缓存</summary>
        private void SyncCurrentStateToParameters()
        {
            if (Parameters == null) return;

            CompensationManager?.SaveToParameters(Parameters);
            Parameters.CompensationXLinkedVar = CompensationXLinkedVar;
            Parameters.CompensationYLinkedVar = CompensationYLinkedVar;
            Parameters.CompensationZLinkedVar = CompensationZLinkedVar;
            Parameters.CompensationXExpression = CompensationXExpression;
            Parameters.CompensationYExpression = CompensationYExpression;
            Parameters.CompensationZExpression = CompensationZExpression;
        }

        /// <summary>缓存当前系统的参数与文件信息</summary>
        private void StashCurrentSystemState(int systemNumber)
        {
            if (Parameters == null) return;

            Parameters.SystemNumber = systemNumber;
            SyncCurrentStateToParameters();
            _systemStateCache[systemNumber] = new NeedleSystemState
            {
                Parameters = Parameters.Clone(),
                CurrentFilePath = CurrentFilePath,
                CurrentFileName = CurrentFileName
            };
        }

        /// <summary>应用指定系统的完整状态到 UI</summary>
        private void ApplySystemState(NeedleSystemState state, int systemNumber)
        {
            if (state?.Parameters == null)
            {
                state = new NeedleSystemState
                {
                    Parameters = CreateDefaultParametersForSystem(systemNumber)
                };
            }

            ApplyParametersFromSystem(state.Parameters, systemNumber);
            CurrentFilePath = state.CurrentFilePath;
            CurrentFileName = state.CurrentFileName;
            RaiseSystemUiProperties();
        }

        /// <summary>创建指定系统的默认参数集</summary>
        private static NeedleCalibrationParams CreateDefaultParametersForSystem(int systemNumber)
        {
            return new NeedleCalibrationParams
            {
                SystemNumber = systemNumber,
                CalibrationName = "Default",
                CompensationXLinkedVar = NeedleAlignerGlobalVariableNames.DefaultCompXLinkedVar,
                CompensationYLinkedVar = NeedleAlignerGlobalVariableNames.DefaultCompYLinkedVar,
                CompensationZLinkedVar = NeedleAlignerGlobalVariableNames.DefaultCompZLinkedVar
            };
        }

        /// <summary>预加载另一系统参数到缓存（不切换 UI）</summary>
        private async Task EnsureSystemCachedAsync(int systemNumber)
        {
            if (_systemStateCache.ContainsKey(systemNumber))
                return;

            var loaded = await LoadSystemStateFromDiskAsync(systemNumber);
            _systemStateCache[systemNumber] = loaded;
        }

        /// <summary>从配方池记录或目录加载指定系统的参数快照</summary>
        private async Task<NeedleSystemState> LoadSystemStateFromDiskAsync(int systemNumber)
        {
            try
            {
                if (_recipePoolService != null)
                {
                    var poolName = _recipePoolService.CurrentPoolName ?? "Default";
                    var extKey = $"NeedleAligner_CurrentFile_System{systemNumber}";
                    var extData = await _recipePoolService.GetExtensionDataAsync<NeedleAlignerFileRecord>(poolName, extKey);

                    if (extData?.FilePath != null && File.Exists(extData.FilePath))
                    {
                        var loaded = await DeserializeParametersFileAsync(extData.FilePath, systemNumber);
                        if (loaded != null)
                        {
                            return new NeedleSystemState
                            {
                                Parameters = loaded,
                                CurrentFilePath = extData.FilePath,
                                CurrentFileName = Path.GetFileName(extData.FilePath)
                            };
                        }
                    }
                }

                var calibrationDir = GetCalibrationDirectory(systemNumber);
                var latest = Directory
                    .EnumerateFiles(calibrationDir, $"NeedleCalibration_System{systemNumber}_*.json")
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .FirstOrDefault();

                if (latest != null)
                {
                    var loaded = await DeserializeParametersFileAsync(latest, systemNumber);
                    if (loaded != null)
                    {
                        return new NeedleSystemState
                        {
                            Parameters = loaded,
                            CurrentFilePath = latest,
                            CurrentFileName = Path.GetFileName(latest)
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleAligner] 加载系统{systemNumber}参数失败: {ex.Message}");
            }

            return new NeedleSystemState
            {
                Parameters = CreateDefaultParametersForSystem(systemNumber)
            };
        }

        /// <summary>反序列化参数文件并校正系统编号</summary>
        private static async Task<NeedleCalibrationParams> DeserializeParametersFileAsync(string filePath, int systemNumber)
        {
            if (!File.Exists(filePath))
                return null;

            var json = await File.ReadAllTextAsync(filePath);
            var loaded = JsonConvert.DeserializeObject<NeedleCalibrationParams>(json);
            if (loaded == null)
                return null;

            loaded.SystemNumber = systemNumber;
            MigrateLegacySearchNeedleHeight(loaded);
            return loaded;
        }

        private void ApplyParametersFromSystem(NeedleCalibrationParams loaded, int systemNumber)
        {
            loaded.SystemNumber = systemNumber;
            MigrateLegacySearchNeedleHeight(loaded);
            Parameters = loaded;
            CompensationManager.LoadFromParameters(Parameters);
            // 切换系统后 PendingIncrement 刷新为当前实时偏差
            RefreshPendingIncrementFromDelta();

            CompensationXLinkedVar = ResolveCompXLinkedVar(Parameters.CompensationXLinkedVar);
            CompensationYLinkedVar = ResolveCompYLinkedVar(Parameters.CompensationYLinkedVar);
            CompensationZLinkedVar = ResolveCompZLinkedVar(Parameters.CompensationZLinkedVar);
            Parameters.CompensationXLinkedVar = CompensationXLinkedVar;
            Parameters.CompensationYLinkedVar = CompensationYLinkedVar;
            Parameters.CompensationZLinkedVar = CompensationZLinkedVar;
            CompensationXExpression = Parameters.CompensationXExpression;
            CompensationYExpression = Parameters.CompensationYExpression;
            CompensationZExpression = Parameters.CompensationZExpression;
        }

        private void RaiseSystemUiProperties()
        {
            RaisePropertyChanged(nameof(Parameters));
            RaisePropertyChanged(nameof(CurrentFilePath));
            RaisePropertyChanged(nameof(CurrentFileName));
            RaisePropertyChanged(nameof(CurrentAlignPositionTitle));
            RaisePropertyChanged(nameof(CurrentAlignX));
            RaisePropertyChanged(nameof(CurrentAlignY));
            RaisePropertyChanged(nameof(CurrentAlignZ));
            RaisePropertyChanged(nameof(CurrentSearchNeedleHeight));
            RaisePropertyChanged(nameof(SensorDiX));
            RaisePropertyChanged(nameof(SensorDiY));
            RaisePropertyChanged(nameof(CompensationX));
            RaisePropertyChanged(nameof(CompensationY));
            RaisePropertyChanged(nameof(CompensationZ));
            RaisePropertyChanged(nameof(TcpTotalOffsetX));
            RaisePropertyChanged(nameof(TcpTotalOffsetY));
            RaisePropertyChanged(nameof(TcpTotalOffsetZ));
            RaisePropertyChanged(nameof(PendingIncrementX));
            RaisePropertyChanged(nameof(PendingIncrementY));
            RaisePropertyChanged(nameof(PendingIncrementZ));
            RaisePropertyChanged(nameof(CompensationXExpression));
            RaisePropertyChanged(nameof(CompensationYExpression));
            RaisePropertyChanged(nameof(CompensationZExpression));
            RaisePropertyChanged(nameof(CompensationXLinkedVar));
            RaisePropertyChanged(nameof(CompensationYLinkedVar));
            RaisePropertyChanged(nameof(CompensationZLinkedVar));
            RaisePropertyChanged(nameof(IsCompensationXLinked));
            RaisePropertyChanged(nameof(IsCompensationYLinked));
            RaisePropertyChanged(nameof(IsCompensationZLinked));
            Parameters?.NotifyUiBindingsRefresh();
            RaiseCalibrationDeltaAndCalculatedChanged();
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
                StashCurrentSystemState(SystemNumber);
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
                    await WriteCompensationToGlobalVariablesAsync(CalculatedCompX, CalculatedCompY, CalculatedCompZ);

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
                    var sn = loaded.SystemNumber > 0 ? loaded.SystemNumber : SystemNumber;
                    var state = new NeedleSystemState
                    {
                        Parameters = loaded,
                        CurrentFilePath = filePath,
                        CurrentFileName = Path.GetFileName(filePath)
                    };

                    if (_systemNumber != sn)
                    {
                        _systemNumber = sn;
                        RaisePropertyChanged(nameof(SystemNumber));
                    }

                    ApplySystemState(state, sn);
                    _systemStateCache[sn] = new NeedleSystemState
                    {
                        Parameters = Parameters.Clone(),
                        CurrentFilePath = CurrentFilePath,
                        CurrentFileName = CurrentFileName
                    };

                    await EnsureLinkedCompVariablesExistAsync(
                        CompensationXLinkedVar, CompensationYLinkedVar, CompensationZLinkedVar);
                    await LoadGlobalVariablesAsync();

                    RaisePropertyChanged(nameof(CompensationManager));

                    AddLog(_localization.GetResourceOrDefault("NeedleAligner_Log_ParametersLoaded", "针头校准参数加载成功"));
                    AddLog(string.Format(
                        _localization.GetResourceOrDefault("NeedleAligner_Log_TcpTotalOffset",
                            "累计TCP: X={0:F3}, Y={1:F3}, Z={2:F3}"),
                        TcpTotalOffsetX, TcpTotalOffsetY, TcpTotalOffsetZ));
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

            if ((e.PropertyName == nameof(NeedleCalibrationParams.AlignPositionSystem1) && SystemNumber == 1)
                || (e.PropertyName == nameof(NeedleCalibrationParams.AlignPositionSystem2) && SystemNumber == 2))
            {
                RaisePropertyChanged(nameof(CurrentAlignX));
                RaisePropertyChanged(nameof(CurrentAlignY));
                RaisePropertyChanged(nameof(CurrentAlignZ));
            }

            if (e.PropertyName is nameof(NeedleCalibrationParams.SearchNeedleHeight)
                || (e.PropertyName == nameof(NeedleCalibrationParams.SearchNeedleHeightSystem1) && SystemNumber == 1)
                || (e.PropertyName == nameof(NeedleCalibrationParams.SearchNeedleHeightSystem2) && SystemNumber == 2))
            {
                RaisePropertyChanged(nameof(CurrentSearchNeedleHeight));
            }

            if (e.PropertyName is nameof(NeedleCalibrationParams.SensorDiX))
                RaisePropertyChanged(nameof(SensorDiX));
            if (e.PropertyName is nameof(NeedleCalibrationParams.SensorDiY))
                RaisePropertyChanged(nameof(SensorDiY));
        }

        /// <summary>补偿管理器数值变更时刷新计算结果</summary>
        private void OnCompensationManagerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(NeedleCompensationManager.TcpTotalOffsetX):
                    RaisePropertyChanged(nameof(CompensationX));
                    RaisePropertyChanged(nameof(TcpTotalOffsetX));
                    RaisePropertyChanged(nameof(CalculatedCompX));
                    RaisePropertyChanged(nameof(TcpGlobalWritePreviewX));
                    break;
                case nameof(NeedleCompensationManager.TcpTotalOffsetY):
                    RaisePropertyChanged(nameof(CompensationY));
                    RaisePropertyChanged(nameof(TcpTotalOffsetY));
                    RaisePropertyChanged(nameof(CalculatedCompY));
                    RaisePropertyChanged(nameof(TcpGlobalWritePreviewY));
                    break;
                case nameof(NeedleCompensationManager.TcpTotalOffsetZ):
                    RaisePropertyChanged(nameof(CompensationZ));
                    RaisePropertyChanged(nameof(TcpTotalOffsetZ));
                    RaisePropertyChanged(nameof(CalculatedCompZ));
                    RaisePropertyChanged(nameof(TcpGlobalWritePreviewZ));
                    break;
                case nameof(NeedleCompensationManager.CompensationX):
                    RaisePropertyChanged(nameof(CompensationX));
                    RaisePropertyChanged(nameof(TcpTotalOffsetX));
                    RaisePropertyChanged(nameof(CalculatedCompX));
                    RaisePropertyChanged(nameof(TcpGlobalWritePreviewX));
                    break;
                case nameof(NeedleCompensationManager.CompensationY):
                    RaisePropertyChanged(nameof(CompensationY));
                    RaisePropertyChanged(nameof(TcpTotalOffsetY));
                    RaisePropertyChanged(nameof(CalculatedCompY));
                    RaisePropertyChanged(nameof(TcpGlobalWritePreviewY));
                    break;
                case nameof(NeedleCompensationManager.CompensationZ):
                    RaisePropertyChanged(nameof(CompensationZ));
                    RaisePropertyChanged(nameof(TcpTotalOffsetZ));
                    RaisePropertyChanged(nameof(CalculatedCompZ));
                    RaisePropertyChanged(nameof(TcpGlobalWritePreviewZ));
                    break;
            }
        }

        /// <summary>通知校准增量与计算结果属性变更</summary>
        private void RaiseCalibrationDeltaAndCalculatedChanged()
        {
            RaisePropertyChanged(nameof(CalibrationDeltaX));
            RaisePropertyChanged(nameof(CalibrationDeltaY));
            RaisePropertyChanged(nameof(CalibrationDeltaZ));
            RaisePropertyChanged(nameof(PendingIncrementX));
            RaisePropertyChanged(nameof(PendingIncrementY));
            RaisePropertyChanged(nameof(PendingIncrementZ));
            RaisePropertyChanged(nameof(TcpTotalOffsetX));
            RaisePropertyChanged(nameof(TcpTotalOffsetY));
            RaisePropertyChanged(nameof(TcpTotalOffsetZ));
            RaisePropertyChanged(nameof(CalculatedCompX));
            RaisePropertyChanged(nameof(CalculatedCompY));
            RaisePropertyChanged(nameof(CalculatedCompZ));
            RaisePropertyChanged(nameof(TcpGlobalWritePreviewX));
            RaisePropertyChanged(nameof(TcpGlobalWritePreviewY));
            RaisePropertyChanged(nameof(TcpGlobalWritePreviewZ));
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
        /// 添加日志到队列（带时间戳），同时写入文件日志（ILoggerService）
        /// </summary>
        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logEntry = $"[{timestamp}] {message}";
            _logQueue.Enqueue(logEntry);
            _logger.Info($"[NeedleAligner] {message}");
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
        /// 保存补偿历史记录（校准记录待应用增量；应用后记录增量与累计 TCP）
        /// </summary>
        private void SaveCompensationHistory(double deltaX, double deltaY, double deltaZ, bool recordPendingOnly)
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
                    TcpTotalOffsetX = recordPendingOnly ? TcpTotalOffsetX : CompensationManager.TcpTotalOffsetX,
                    TcpTotalOffsetY = recordPendingOnly ? TcpTotalOffsetY : CompensationManager.TcpTotalOffsetY,
                    TcpTotalOffsetZ = recordPendingOnly ? TcpTotalOffsetZ : CompensationManager.TcpTotalOffsetZ,
                    CurrentX = Parameters.CurrentXYZ?.X ?? 0,
                    CurrentY = Parameters.CurrentXYZ?.Y ?? 0,
                    CurrentZ = Parameters.CurrentXYZ?.Z ?? 0,
                    ReferenceX = Parameters.ReferenceXYZ?.X ?? 0,
                    ReferenceY = Parameters.ReferenceXYZ?.Y ?? 0,
                    ReferenceZ = Parameters.ReferenceXYZ?.Z ?? 0,
                    Operator = Parameters.Operator,
                    Comments = recordPendingOnly ? "CalibrationPending" : "Applied"
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
        private string GetCalibrationDirectory(int? systemNumber = null)
        {
            var dir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Config", "Calibration", $"System{systemNumber ?? SystemNumber}");
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

        /// <summary>启动时自动加载当前系统的校准参数</summary>
        private async Task TryAutoLoadConfigAsync()
        {
            try
            {
                var state = await LoadSystemStateFromDiskAsync(SystemNumber);
                ApplySystemState(state, SystemNumber);
                StashCurrentSystemState(SystemNumber);

                if (state.CurrentFilePath == null)
                {
                    await ApplyDefaultLinkedVariablesAsync();
                    StashCurrentSystemState(SystemNumber);
                    _logger.Info($"[NeedleAligner] 系统{SystemNumber}无可加载的校准配置文件，已应用默认参数");
                }
                else
                {
                    _logger.Info($"[NeedleAligner] 系统{SystemNumber}已加载: {state.CurrentFilePath}");
                }
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

        /// <summary>构建搜索点传感器下拉选项（多语言）</summary>
        private IReadOnlyList<NeedleSearchSensorOption> BuildSearchSensorOptions() => new[]
        {
            new NeedleSearchSensorOption(
                NeedleSearchSensorKind.SensorX,
                _localization.GetResourceOrDefault("NeedleAligner_SensorX", "X传感器")),
            new NeedleSearchSensorOption(
                NeedleSearchSensorKind.SensorY,
                _localization.GetResourceOrDefault("NeedleAligner_SensorY", "Y传感器"))
        };
    }

    /// <summary>搜索点传感器下拉项</summary>
    public sealed class NeedleSearchSensorOption
    {
        public NeedleSearchSensorOption(NeedleSearchSensorKind value, string label)
        {
            Value = value;
            Label = label;
        }

        public NeedleSearchSensorKind Value { get; }
        public string Label { get; }
    }

    /// <summary>单套针头系统的参数与文件快照</summary>
    internal sealed class NeedleSystemState
    {
        public NeedleCalibrationParams Parameters { get; init; }
        public string CurrentFilePath { get; init; }
        public string CurrentFileName { get; init; }
    }

    /// <summary>记录最后使用的对针参数文件路径</summary>
    public class NeedleAlignerFileRecord
    {
        public string FilePath { get; set; }
    }
}
