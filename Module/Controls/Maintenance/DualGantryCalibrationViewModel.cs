using Core.Abstraction;
using Core.Models;
using Core.Services;
using Core.Utilities;
using Framework.Dialogs;
using Newtonsoft.Json;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using TCPIPModule.Interfaces;

namespace Module.ViewModels
{
    /// <summary>
    /// 双龙门标定ViewModel——管理双龙门独立仿射标定、公共基准点采集、跨龙门Y基准对齐
    /// 机构特点：龙门1(Dx+Dy独立) + 龙门2(X2+共用Y) + 双上相机(Cam1/Cam2)
    /// 以共用下层Y轴为公共基准，融合两套龙门坐标系，消除跨龙门XY误差
    /// </summary>
    public class DualGantryCalibrationViewModel : BindableBase
    {
        private readonly IDualGantryCalibrationService _calibService;
        private readonly IPositionMotionController _motionController;
        private readonly IAxisConfigurationService _axisConfigService;
        private readonly ITCPEventService _tcpEventService;
        private readonly ITCPClientManagerService _tcpClientManager;
        private readonly IParameterStorage _parameterStorage;
        private readonly IFileDialogService _fileDialogService;
        private readonly ILocalizationService _localization;
        private readonly ILoggerService _logger;

        /// <summary>默认配置文件路径（Config/Calibration）</summary>
        private static readonly string ConfigDirectory = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Config", "Calibration");

        /// <summary>参数存储Key——记录上次使用的双龙门标定配置</summary>
        private const string ParameterStorageKey = "DualGantryCalibration_Default";

        /// <summary>默认文件名前缀</summary>
        private const string DefaultFilePrefix = "DualGantryCalibration_";

        /// <summary>龙门1自动标定取消令牌源</summary>
        private CancellationTokenSource? _gantry1Cts;

        /// <summary>龙门2自动标定取消令牌源</summary>
        private CancellationTokenSource? _gantry2Cts;

        /// <summary>公共基准点采集取消令牌源</summary>
        private CancellationTokenSource? _referenceCts;

        #region 属性 - 机构配置

        private ObservableCollection<string> _stationIdentifiers = new() { "DispenserStation", "LoadingStation", "AssemblyStation" };
        /// <summary>可选工站标识列表</summary>
        public ObservableCollection<string> StationIdentifiers { get => _stationIdentifiers; set => SetProperty(ref _stationIdentifiers, value); }

        private string _selectedStationIdentifier = "DispenserStation";
        /// <summary>选中的工站标识（切换时刷新可用轴并重置默认轴名）</summary>
        public string SelectedStationIdentifier
        {
            get => _selectedStationIdentifier;
            set
            {
                if (SetProperty(ref _selectedStationIdentifier, value))
                    OnStationChanged();
            }
        }

        private ObservableCollection<string> _availableAxes = new();
        /// <summary>当前工站可用轴名列表</summary>
        public ObservableCollection<string> AvailableAxes { get => _availableAxes; set => SetProperty(ref _availableAxes, value); }

        private string _commonAxisY = "GantryY";
        /// <summary>公共基准Y轴名（下层共用Y轴，运动将同步影响两套龙门）</summary>
        public string CommonAxisY { get => _commonAxisY; set => SetProperty(ref _commonAxisY, value); }

        private string _gantry1AxisX = "Dx";
        /// <summary>龙门1独立X轴名</summary>
        public string Gantry1AxisX { get => _gantry1AxisX; set => SetProperty(ref _gantry1AxisX, value); }

        private string _gantry1AxisY = "Dy";
        /// <summary>龙门1独立Y轴名</summary>
        public string Gantry1AxisY { get => _gantry1AxisY; set => SetProperty(ref _gantry1AxisY, value); }

        private string _gantry2AxisX = "X2";
        /// <summary>龙门2独立X轴名（Y轴与龙门1共用CommonAxisY）</summary>
        public string Gantry2AxisX { get => _gantry2AxisX; set => SetProperty(ref _gantry2AxisX, value); }

        private string _gantry1TcpConnection = string.Empty;
        /// <summary>Cam1 TCP连接名（龙门1视觉相机）</summary>
        public string Gantry1TcpConnection { get => _gantry1TcpConnection; set => SetProperty(ref _gantry1TcpConnection, value); }

        private string _gantry2TcpConnection = string.Empty;
        /// <summary>Cam2 TCP连接名（龙门2视觉相机）</summary>
        public string Gantry2TcpConnection { get => _gantry2TcpConnection; set => SetProperty(ref _gantry2TcpConnection, value); }

        private string _gantry1TriggerCommand = string.Empty;
        /// <summary>Cam1 触发拍照命令</summary>
        public string Gantry1TriggerCommand { get => _gantry1TriggerCommand; set => SetProperty(ref _gantry1TriggerCommand, value); }

        private string _gantry2TriggerCommand = string.Empty;
        /// <summary>Cam2 触发拍照命令</summary>
        public string Gantry2TriggerCommand { get => _gantry2TriggerCommand; set => SetProperty(ref _gantry2TriggerCommand, value); }

        private bool _enableVisionData = true;
        /// <summary>是否启用视觉数据接收（false时手动输入）</summary>
        public bool EnableVisionData { get => _enableVisionData; set => SetProperty(ref _enableVisionData, value); }

        private int _pointCount = 9;
        /// <summary>每套龙门标定点数（变更时同步更新两套龙门点位集合）</summary>
        public int PointCount
        {
            get => _pointCount;
            set
            {
                if (SetProperty(ref _pointCount, value))
                {
                    UpdateGantry1PointsCollection();
                    UpdateGantry2PointsCollection();
                }
            }
        }

        private int _autoCalibDelayMs = 500;
        /// <summary>自动标定每点间延时（毫秒）</summary>
        public int AutoCalibDelayMs { get => _autoCalibDelayMs; set => SetProperty(ref _autoCalibDelayMs, value); }

        #endregion

        #region 属性 - TCP连接

        private ObservableCollection<string> _tcpConnections = new();
        /// <summary>TCP连接名列表（供Cam1/Cam2选择）</summary>
        public ObservableCollection<string> TcpConnections { get => _tcpConnections; set => SetProperty(ref _tcpConnections, value); }

        #endregion

        #region 属性 - 标定数据

        private ObservableCollection<DualGantryCalibrationPoint> _gantry1Points = new();
        /// <summary>龙门1标定点集合</summary>
        public ObservableCollection<DualGantryCalibrationPoint> Gantry1Points
        {
            get => _gantry1Points;
            set => SetProperty(ref _gantry1Points, value);
        }

        private ObservableCollection<DualGantryCalibrationPoint> _gantry2Points = new();
        /// <summary>龙门2标定点集合</summary>
        public ObservableCollection<DualGantryCalibrationPoint> Gantry2Points
        {
            get => _gantry2Points;
            set => SetProperty(ref _gantry2Points, value);
        }

        private ObservableCollection<CommonReferencePoint> _commonReferencePoints = new();
        /// <summary>公共基准点集合（共用Y轴上的对齐基准）</summary>
        public ObservableCollection<CommonReferencePoint> CommonReferencePoints
        {
            get => _commonReferencePoints;
            set => SetProperty(ref _commonReferencePoints, value);
        }

        private CommonReferencePoint? _selectedCommonReferencePoint;
        /// <summary>当前选中的公共基准点（用于分步采集Cam1/Cam2数据）</summary>
        public CommonReferencePoint? SelectedCommonReferencePoint
        {
            get => _selectedCommonReferencePoint;
            set
            {
                if (SetProperty(ref _selectedCommonReferencePoint, value))
                {
                    CaptureGantry1ReferenceCommand.RaiseCanExecuteChanged();
                    CaptureGantry2ReferenceCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private AffineCalibrationResult? _gantry1CalibrationResult;
        /// <summary>龙门1仿射标定结果（视觉→机械）</summary>
        public AffineCalibrationResult? Gantry1CalibrationResult
        {
            get => _gantry1CalibrationResult;
            set
            {
                if (SetProperty(ref _gantry1CalibrationResult, value))
                {
                    CaptureGantry1ReferenceCommand.RaiseCanExecuteChanged();
                    ComputeGantryTransformCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private AffineCalibrationResult? _gantry2CalibrationResult;
        /// <summary>龙门2仿射标定结果（视觉→机械）</summary>
        public AffineCalibrationResult? Gantry2CalibrationResult
        {
            get => _gantry2CalibrationResult;
            set
            {
                if (SetProperty(ref _gantry2CalibrationResult, value))
                {
                    CaptureGantry2ReferenceCommand.RaiseCanExecuteChanged();
                    ComputeGantryTransformCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private GantryTransform? _gantryTransform;
        /// <summary>跨龙门变换参数（龙门1→龙门2）</summary>
        public GantryTransform? GantryTransform
        {
            get => _gantryTransform;
            set
            {
                if (SetProperty(ref _gantryTransform, value))
                {
                    VerifyTransformCommand.RaiseCanExecuteChanged();
                }
            }
        }

        #endregion

        #region 属性 - 状态

        private bool _isGantry1Calibrating;
        /// <summary>龙门1是否正在自动标定</summary>
        public bool IsGantry1Calibrating
        {
            get => _isGantry1Calibrating;
            set
            {
                if (SetProperty(ref _isGantry1Calibrating, value))
                {
                    StartGantry1AutoCalibCommand.RaiseCanExecuteChanged();
                    StopGantry1AutoCalibCommand.RaiseCanExecuteChanged();
                    // 龙门2启动命令依赖龙门1状态（互斥）
                    StartGantry2AutoCalibCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isGantry2Calibrating;
        /// <summary>龙门2是否正在自动标定</summary>
        public bool IsGantry2Calibrating
        {
            get => _isGantry2Calibrating;
            set
            {
                if (SetProperty(ref _isGantry2Calibrating, value))
                {
                    StartGantry2AutoCalibCommand.RaiseCanExecuteChanged();
                    StopGantry2AutoCalibCommand.RaiseCanExecuteChanged();
                    // 龙门1启动命令依赖龙门2状态（互斥）
                    StartGantry1AutoCalibCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isCapturingReference;
        /// <summary>是否正在采集公共基准点</summary>
        public bool IsCapturingReference
        {
            get => _isCapturingReference;
            set
            {
                if (SetProperty(ref _isCapturingReference, value))
                {
                    CaptureGantry1ReferenceCommand.RaiseCanExecuteChanged();
                    CaptureGantry2ReferenceCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _currentFileName = string.Empty;
        /// <summary>当前加载的文件名（仅文件名，不含路径）</summary>
        public string CurrentFileName
        {
            get => _currentFileName;
            set => SetProperty(ref _currentFileName, value);
        }

        private string _statusText = string.Empty;
        /// <summary>状态栏文本</summary>
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

        private Brush _statusColor = Brushes.LightGray;
        /// <summary>状态栏颜色</summary>
        public Brush StatusColor { get => _statusColor; set => SetProperty(ref _statusColor, value); }

        #endregion

        #region 属性 - 坐标变换验证

        private double _verifyInputX;
        /// <summary>验证输入龙门1 X坐标</summary>
        public double VerifyInputX { get => _verifyInputX; set => SetProperty(ref _verifyInputX, value); }

        private double _verifyInputY;
        /// <summary>验证输入龙门1 Y坐标</summary>
        public double VerifyInputY { get => _verifyInputY; set => SetProperty(ref _verifyInputY, value); }

        private double _verifyOutputX;
        /// <summary>验证输出龙门2 X坐标（由GantryTransform计算，只读）</summary>
        public double VerifyOutputX { get => _verifyOutputX; private set => SetProperty(ref _verifyOutputX, value); }

        private double _verifyOutputY;
        /// <summary>验证输出龙门2 Y坐标（由GantryTransform计算，只读）</summary>
        public double VerifyOutputY { get => _verifyOutputY; private set => SetProperty(ref _verifyOutputY, value); }

        #endregion

        #region 命令 - 龙门1

        public DelegateCommand<DualGantryCalibrationPoint> TeachGantry1PointCommand { get; }
        public DelegateCommand<DualGantryCalibrationPoint> MoveGantry1PointCommand { get; }
        public DelegateCommand<DualGantryCalibrationPoint> DeleteGantry1PointCommand { get; }
        public DelegateCommand AddGantry1PointCommand { get; }
        public DelegateCommand StartGantry1AutoCalibCommand { get; }
        public DelegateCommand StopGantry1AutoCalibCommand { get; }
        public DelegateCommand ComputeGantry1CalibrationCommand { get; }

        #endregion

        #region 命令 - 龙门2

        public DelegateCommand<DualGantryCalibrationPoint> TeachGantry2PointCommand { get; }
        public DelegateCommand<DualGantryCalibrationPoint> MoveGantry2PointCommand { get; }
        public DelegateCommand<DualGantryCalibrationPoint> DeleteGantry2PointCommand { get; }
        public DelegateCommand AddGantry2PointCommand { get; }
        public DelegateCommand StartGantry2AutoCalibCommand { get; }
        public DelegateCommand StopGantry2AutoCalibCommand { get; }
        public DelegateCommand ComputeGantry2CalibrationCommand { get; }

        #endregion

        #region 命令 - 跨龙门对齐

        public DelegateCommand AddReferencePointCommand { get; }
        public DelegateCommand CaptureGantry1ReferenceCommand { get; }
        public DelegateCommand CaptureGantry2ReferenceCommand { get; }
        public DelegateCommand ComputeGantryTransformCommand { get; }
        public DelegateCommand VerifyTransformCommand { get; }

        #endregion

        #region 命令 - 文件操作

        public DelegateCommand SaveConfigCommand { get; }
        public DelegateCommand SaveAsConfigCommand { get; }
        public DelegateCommand ImportConfigCommand { get; }
        public DelegateCommand ExportConfigCommand { get; }

        #endregion

        /// <summary>
        /// 构造函数——注入所有依赖服务，初始化命令、订阅事件、初始化点位集合
        /// </summary>
        public DualGantryCalibrationViewModel(
            IDualGantryCalibrationService calibService,
            IPositionMotionController motionController,
            IAxisConfigurationService axisConfigService,
            ITCPEventService tcpEventService,
            ITCPClientManagerService tcpClientManager,
            IParameterStorage parameterStorage,
            IFileDialogService fileDialogService,
            ILocalizationService localization,
            ILoggerService logger)
        {
            _calibService = calibService;
            _motionController = motionController;
            _axisConfigService = axisConfigService;
            _tcpEventService = tcpEventService;
            _tcpClientManager = tcpClientManager;
            _parameterStorage = parameterStorage;
            _fileDialogService = fileDialogService;
            _localization = localization;
            _logger = logger;

            // 龙门1命令初始化
            TeachGantry1PointCommand = new DelegateCommand<DualGantryCalibrationPoint>(
                async p => await ExecuteTeachGantry1PointAsync(p));
            MoveGantry1PointCommand = new DelegateCommand<DualGantryCalibrationPoint>(
                async p => await ExecuteMoveGantry1PointAsync(p));
            DeleteGantry1PointCommand = new DelegateCommand<DualGantryCalibrationPoint>(
                ExecuteDeleteGantry1Point);
            AddGantry1PointCommand = new DelegateCommand(ExecuteAddGantry1Point);
            StartGantry1AutoCalibCommand = new DelegateCommand(
                ExecuteStartGantry1AutoCalib,
                () => !IsGantry1Calibrating && !IsGantry2Calibrating);
            StopGantry1AutoCalibCommand = new DelegateCommand(
                ExecuteStopGantry1AutoCalib,
                () => IsGantry1Calibrating);
            ComputeGantry1CalibrationCommand = new DelegateCommand(
                ExecuteComputeGantry1Calibration,
                () => Gantry1Points.Count(p => p.IsCalibrated) >= 3);

            // 龙门2命令初始化
            TeachGantry2PointCommand = new DelegateCommand<DualGantryCalibrationPoint>(
                async p => await ExecuteTeachGantry2PointAsync(p));
            MoveGantry2PointCommand = new DelegateCommand<DualGantryCalibrationPoint>(
                async p => await ExecuteMoveGantry2PointAsync(p));
            DeleteGantry2PointCommand = new DelegateCommand<DualGantryCalibrationPoint>(
                ExecuteDeleteGantry2Point);
            AddGantry2PointCommand = new DelegateCommand(ExecuteAddGantry2Point);
            StartGantry2AutoCalibCommand = new DelegateCommand(
                ExecuteStartGantry2AutoCalib,
                () => !IsGantry1Calibrating && !IsGantry2Calibrating);
            StopGantry2AutoCalibCommand = new DelegateCommand(
                ExecuteStopGantry2AutoCalib,
                () => IsGantry2Calibrating);
            ComputeGantry2CalibrationCommand = new DelegateCommand(
                ExecuteComputeGantry2Calibration,
                () => Gantry2Points.Count(p => p.IsCalibrated) >= 3);

            // 跨龙门对齐命令初始化
            AddReferencePointCommand = new DelegateCommand(ExecuteAddReferencePoint);
            CaptureGantry1ReferenceCommand = new DelegateCommand(
                async () => await ExecuteCaptureGantry1ReferenceAsync(),
                () => !IsCapturingReference && Gantry1CalibrationResult != null && SelectedCommonReferencePoint != null);
            CaptureGantry2ReferenceCommand = new DelegateCommand(
                async () => await ExecuteCaptureGantry2ReferenceAsync(),
                () => !IsCapturingReference && Gantry2CalibrationResult != null && SelectedCommonReferencePoint != null);
            ComputeGantryTransformCommand = new DelegateCommand(
                ExecuteComputeGantryTransform,
                () => CommonReferencePoints.Count(p => p.IsCaptured) >= 2
                      && Gantry1CalibrationResult != null
                      && Gantry2CalibrationResult != null);
            VerifyTransformCommand = new DelegateCommand(
                ExecuteVerifyTransform,
                () => GantryTransform != null && GantryTransform.IsAligned);

            // 文件操作命令初始化
            SaveConfigCommand = new DelegateCommand(async () => await ExecuteSaveConfigAsync());
            SaveAsConfigCommand = new DelegateCommand(async () => await ExecuteSaveAsConfigAsync());
            ImportConfigCommand = new DelegateCommand(async () => await ExecuteImportConfigAsync());
            ExportConfigCommand = new DelegateCommand(async () => await ExecuteExportConfigAsync());

            // 订阅服务事件
            _calibService.PointCalibrated += OnPointCalibrated;
            _calibService.VisionDataReceived += OnVisionDataReceived;
            _calibService.GantryCalibrationCompleted += OnGantryCalibrationCompleted;
            _calibService.CommonReferenceCaptured += OnCommonReferenceCaptured;
            _calibService.GantryTransformComputed += OnGantryTransformComputed;
            _calibService.CalibrationError += OnCalibrationError;

            // 初始化两套龙门点位集合（各9个空点）
            UpdateGantry1PointsCollection();
            UpdateGantry2PointsCollection();

            // 异步初始化
            _ = InitializeAsync();
        }

        #region 初始化

        /// <summary>初始化：刷新轴列表、加载TCP连接、自动加载上次配置</summary>
        private async Task InitializeAsync()
        {
            RefreshAvailableAxes();
            await LoadTcpConnectionsAsync();
            await TryAutoLoadConfigAsync();
            UpdateStatus(L("DualGantryCalib_Idle", "空闲"), Brushes.LightGray);
        }

        /// <summary>工站切换时刷新可用轴列表并重置默认轴名</summary>
        private void OnStationChanged()
        {
            RefreshAvailableAxes();
            // 重置轴名为默认值（保留CommonAxisY默认GantryY）
            CommonAxisY = AvailableAxes.FirstOrDefault(a => a.IndexOf("Y", StringComparison.OrdinalIgnoreCase) >= 0) ?? "GantryY";
            Gantry1AxisX = AvailableAxes.FirstOrDefault() ?? "Dx";
            Gantry1AxisY = AvailableAxes.Skip(1).FirstOrDefault() ?? "Dy";
            Gantry2AxisX = AvailableAxes.Skip(2).FirstOrDefault() ?? "X2";
        }

        /// <summary>从IAxisConfigurationService获取所有工站的可用轴名（双龙门标定需跨工站选轴）</summary>
        private void RefreshAvailableAxes()
        {
            try
            {
                // 读取所有轴，双龙门标定需要跨工站选择（公共Y轴、龙门1轴、龙门2轴可能分属不同工站）
                var axes = _axisConfigService.GetAllAxes();
                AvailableAxes = new ObservableCollection<string>(axes.Select(a => a.Name));

                if (AvailableAxes.Count == 0)
                {
                    _logger.Warn("双龙门标定: 未读取到任何轴配置");
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"双龙门标定: 获取轴列表失败 - {ex.Message}");
                AvailableAxes = new ObservableCollection<string>();
            }
        }

        /// <summary>构建当前双龙门标定配置对象（从所有属性读取）</summary>
        private DualGantryCalibrationConfig BuildConfig()
        {
            return new DualGantryCalibrationConfig
            {
                StationIdentifier = SelectedStationIdentifier,
                CommonAxisY = CommonAxisY,
                Gantry1AxisX = Gantry1AxisX,
                Gantry1AxisY = Gantry1AxisY,
                Gantry2AxisX = Gantry2AxisX,
                Gantry1TcpConnection = Gantry1TcpConnection,
                Gantry2TcpConnection = Gantry2TcpConnection,
                Gantry1TriggerCommand = Gantry1TriggerCommand,
                Gantry2TriggerCommand = Gantry2TriggerCommand,
                EnableVisionData = EnableVisionData,
                PointCount = PointCount,
                AutoCalibDelayMs = AutoCalibDelayMs,
                LastFileName = CurrentFileName
            };
        }

        #endregion

        #region 龙门1自动标定

        /// <summary>启动龙门1自动标定流程（互斥：龙门1与龙门2不可同时标定）</summary>
        private async void ExecuteStartGantry1AutoCalib()
        {
            if (IsGantry1Calibrating || IsGantry2Calibrating) return;

            IsGantry1Calibrating = true;
            _gantry1Cts = new CancellationTokenSource();

            try
            {
                UpdateStatus(L("DualGantryCalib_Calibrating", "标定中...") + " - " + L("DualGantryCalib_Gantry1", "龙门1"),
                    Brushes.Orange);

                // 订阅龙门1视觉数据
                if (EnableVisionData && !string.IsNullOrEmpty(Gantry1TcpConnection))
                {
                    _calibService.SubscribeVisionData(1, Gantry1TcpConnection);
                }

                await _calibService.StartAutoCalibrationAsync(
                    1,
                    Gantry1Points.ToList(),
                    BuildConfig(),
                    _gantry1Cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.Info("双龙门标定: 龙门1自动标定已取消");
                UpdateStatus(L("DualGantryCalib_Idle", "空闲"), Brushes.LightGray);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "双龙门标定: 龙门1自动标定启动失败");
                UpdateStatus($"{L("DualGantryCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
                IsGantry1Calibrating = false;
            }
        }

        /// <summary>停止龙门1自动标定</summary>
        private void ExecuteStopGantry1AutoCalib()
        {
            _calibService.StopAutoCalibration();
            _gantry1Cts?.Cancel();
            _calibService.UnsubscribeVisionData(1);
            IsGantry1Calibrating = false;
            UpdateStatus(L("DualGantryCalib_Idle", "空闲"), Brushes.LightGray);
        }

        #endregion

        #region 龙门2自动标定

        /// <summary>启动龙门2自动标定流程（互斥：龙门1与龙门2不可同时标定）</summary>
        private async void ExecuteStartGantry2AutoCalib()
        {
            if (IsGantry1Calibrating || IsGantry2Calibrating) return;

            IsGantry2Calibrating = true;
            _gantry2Cts = new CancellationTokenSource();

            try
            {
                UpdateStatus(L("DualGantryCalib_Calibrating", "标定中...") + " - " + L("DualGantryCalib_Gantry2", "龙门2"),
                    Brushes.Orange);

                // 订阅龙门2视觉数据
                if (EnableVisionData && !string.IsNullOrEmpty(Gantry2TcpConnection))
                {
                    _calibService.SubscribeVisionData(2, Gantry2TcpConnection);
                }

                await _calibService.StartAutoCalibrationAsync(
                    2,
                    Gantry2Points.ToList(),
                    BuildConfig(),
                    _gantry2Cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.Info("双龙门标定: 龙门2自动标定已取消");
                UpdateStatus(L("DualGantryCalib_Idle", "空闲"), Brushes.LightGray);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "双龙门标定: 龙门2自动标定启动失败");
                UpdateStatus($"{L("DualGantryCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
                IsGantry2Calibrating = false;
            }
        }

        /// <summary>停止龙门2自动标定</summary>
        private void ExecuteStopGantry2AutoCalib()
        {
            _calibService.StopAutoCalibration();
            _gantry2Cts?.Cancel();
            _calibService.UnsubscribeVisionData(2);
            IsGantry2Calibrating = false;
            UpdateStatus(L("DualGantryCalib_Idle", "空闲"), Brushes.LightGray);
        }

        #endregion

        #region 龙门1单点操作

        /// <summary>示教龙门1单点（读取当前Dx+Dy机械坐标）</summary>
        private async Task ExecuteTeachGantry1PointAsync(DualGantryCalibrationPoint? point)
        {
            if (point == null) return;
            try
            {
                var result = await _calibService.TeachPointAsync(1, BuildConfig());
                point.MachineX = result.MachineX;
                point.MachineY = result.MachineY;
                point.IsTaught = true;
                ComputeGantry1CalibrationCommand.RaiseCanExecuteChanged();
                UpdateStatus(string.Format(L("DualGantryCalib_PointTaught", "龙门1点位 {0} 示教完成"), point.Name),
                    Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "双龙门标定: 龙门1示教失败");
                UpdateStatus($"{L("DualGantryCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
            }
        }

        /// <summary>移动到龙门1指定点位（Dx+Dy）</summary>
        private async Task ExecuteMoveGantry1PointAsync(DualGantryCalibrationPoint? point)
        {
            if (point == null) return;
            try
            {
                await _calibService.MoveToPointAsync(1, point, BuildConfig());
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "双龙门标定: 龙门1移动失败");
                UpdateStatus($"{L("DualGantryCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
            }
        }

        /// <summary>删除龙门1指定点位并重新编号</summary>
        private void ExecuteDeleteGantry1Point(DualGantryCalibrationPoint? point)
        {
            if (point == null) return;
            Gantry1Points.Remove(point);
            // 重新编号
            for (int i = 0; i < Gantry1Points.Count; i++)
            {
                Gantry1Points[i].Index = i + 1;
                Gantry1Points[i].Name = $"G1-P{i + 1}";
            }
            ComputeGantry1CalibrationCommand.RaiseCanExecuteChanged();
        }

        /// <summary>添加龙门1标定点</summary>
        private void ExecuteAddGantry1Point()
        {
            var newPoint = new DualGantryCalibrationPoint
            {
                Index = Gantry1Points.Count + 1,
                Name = $"G1-P{Gantry1Points.Count + 1}"
            };
            Gantry1Points.Add(newPoint);
            PointCount = Gantry1Points.Count;
            ComputeGantry1CalibrationCommand.RaiseCanExecuteChanged();
        }

        #endregion

        #region 龙门2单点操作

        /// <summary>示教龙门2单点（读取当前X2+CommonY机械坐标）</summary>
        private async Task ExecuteTeachGantry2PointAsync(DualGantryCalibrationPoint? point)
        {
            if (point == null) return;
            try
            {
                var result = await _calibService.TeachPointAsync(2, BuildConfig());
                point.MachineX = result.MachineX;
                point.MachineY = result.MachineY;
                point.IsTaught = true;
                ComputeGantry2CalibrationCommand.RaiseCanExecuteChanged();
                UpdateStatus(string.Format(L("DualGantryCalib_PointTaught", "龙门2点位 {0} 示教完成"), point.Name),
                    Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "双龙门标定: 龙门2示教失败");
                UpdateStatus($"{L("DualGantryCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
            }
        }

        /// <summary>移动到龙门2指定点位（X2+CommonY）</summary>
        private async Task ExecuteMoveGantry2PointAsync(DualGantryCalibrationPoint? point)
        {
            if (point == null) return;
            try
            {
                await _calibService.MoveToPointAsync(2, point, BuildConfig());
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "双龙门标定: 龙门2移动失败");
                UpdateStatus($"{L("DualGantryCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
            }
        }

        /// <summary>删除龙门2指定点位并重新编号</summary>
        private void ExecuteDeleteGantry2Point(DualGantryCalibrationPoint? point)
        {
            if (point == null) return;
            Gantry2Points.Remove(point);
            // 重新编号
            for (int i = 0; i < Gantry2Points.Count; i++)
            {
                Gantry2Points[i].Index = i + 1;
                Gantry2Points[i].Name = $"G2-P{i + 1}";
            }
            ComputeGantry2CalibrationCommand.RaiseCanExecuteChanged();
        }

        /// <summary>添加龙门2标定点</summary>
        private void ExecuteAddGantry2Point()
        {
            var newPoint = new DualGantryCalibrationPoint
            {
                Index = Gantry2Points.Count + 1,
                Name = $"G2-P{Gantry2Points.Count + 1}"
            };
            Gantry2Points.Add(newPoint);
            PointCount = Gantry2Points.Count;
            ComputeGantry2CalibrationCommand.RaiseCanExecuteChanged();
        }

        #endregion

        #region 仿射计算

        /// <summary>计算龙门1仿射标定结果（>=3点最小二乘法）</summary>
        private void ExecuteComputeGantry1Calibration()
        {
            try
            {
                var calibratedPoints = Gantry1Points.Where(p => p.IsCalibrated).ToList();
                if (calibratedPoints.Count < 3)
                {
                    UpdateStatus(L("DualGantryCalib_MinPointsRequired", "标定至少需要3个点"), Brushes.Orange);
                    return;
                }

                var result = _calibService.ComputeCalibration(calibratedPoints);
                Gantry1CalibrationResult = result;
                UpdateStatus($"{L("DualGantryCalib_Gantry1Calibrated", "龙门1标定完成")} - RMS: {result.RmsError:F6}",
                    result.RmsError < 0.05 ? Brushes.LightGreen : Brushes.Orange);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "双龙门标定: 龙门1计算失败");
                UpdateStatus($"{L("DualGantryCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
            }
        }

        /// <summary>计算龙门2仿射标定结果（>=3点最小二乘法）</summary>
        private void ExecuteComputeGantry2Calibration()
        {
            try
            {
                var calibratedPoints = Gantry2Points.Where(p => p.IsCalibrated).ToList();
                if (calibratedPoints.Count < 3)
                {
                    UpdateStatus(L("DualGantryCalib_MinPointsRequired", "标定至少需要3个点"), Brushes.Orange);
                    return;
                }

                var result = _calibService.ComputeCalibration(calibratedPoints);
                Gantry2CalibrationResult = result;
                UpdateStatus($"{L("DualGantryCalib_Gantry2Calibrated", "龙门2标定完成")} - RMS: {result.RmsError:F6}",
                    result.RmsError < 0.05 ? Brushes.LightGreen : Brushes.Orange);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "双龙门标定: 龙门2计算失败");
                UpdateStatus($"{L("DualGantryCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
            }
        }

        #endregion

        #region 公共基准点采集

        /// <summary>添加新的公共基准点（空点，待分步采集Cam1/Cam2数据）</summary>
        private void ExecuteAddReferencePoint()
        {
            var newPoint = new CommonReferencePoint
            {
                Index = CommonReferencePoints.Count + 1
            };
            CommonReferencePoints.Add(newPoint);
            SelectedCommonReferencePoint = newPoint;
            ComputeGantryTransformCommand.RaiseCanExecuteChanged();
        }

        /// <summary>采集Cam1公共基准数据（Y轴在Cam1视野内时调用，填充选中基准点的Gantry1数据）</summary>
        private async Task ExecuteCaptureGantry1ReferenceAsync()
        {
            if (IsCapturingReference) return;
            if (Gantry1CalibrationResult == null) return;
            if (SelectedCommonReferencePoint == null)
            {
                UpdateStatus(L("DualGantryCalib_SelectReferenceFirst", "请先选择公共基准点"), Brushes.Orange);
                return;
            }

            IsCapturingReference = true;
            _referenceCts = new CancellationTokenSource();

            try
            {
                UpdateStatus(L("DualGantryCalib_CapturingReference", "采集公共基准点...") + " - Cam1",
                    Brushes.Orange);

                var (commonY1, vx, vy) = await _calibService.CaptureReferenceGantry1Async(
                    BuildConfig(), _referenceCts.Token);

                // 填充选中基准点的Gantry1数据
                SelectedCommonReferencePoint.CommonY1 = commonY1;
                SelectedCommonReferencePoint.Gantry1VisionX = vx;
                SelectedCommonReferencePoint.Gantry1VisionY = vy;
                SelectedCommonReferencePoint.IsGantry1Captured = true;

                ComputeGantryTransformCommand.RaiseCanExecuteChanged();

                UpdateStatus(string.Format(L("DualGantryCalib_ReferenceCaptured", "公共基准点 {0} 采集完成") + " - Cam1",
                    SelectedCommonReferencePoint.Index), Brushes.LightGreen);
            }
            catch (OperationCanceledException)
            {
                _logger.Info("双龙门标定: Cam1公共基准采集已取消");
                UpdateStatus(L("DualGantryCalib_Idle", "空闲"), Brushes.LightGray);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "双龙门标定: Cam1公共基准采集失败");
                UpdateStatus($"{L("DualGantryCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
            }
            finally
            {
                IsCapturingReference = false;
            }
        }

        /// <summary>采集Cam2公共基准数据（Y轴在Cam2视野内时调用，填充选中基准点的Gantry2数据）</summary>
        private async Task ExecuteCaptureGantry2ReferenceAsync()
        {
            if (IsCapturingReference) return;
            if (Gantry2CalibrationResult == null) return;
            if (SelectedCommonReferencePoint == null)
            {
                UpdateStatus(L("DualGantryCalib_SelectReferenceFirst", "请先选择公共基准点"), Brushes.Orange);
                return;
            }

            IsCapturingReference = true;
            _referenceCts = new CancellationTokenSource();

            try
            {
                UpdateStatus(L("DualGantryCalib_CapturingReference", "采集公共基准点...") + " - Cam2",
                    Brushes.Orange);

                var (commonY2, vx, vy) = await _calibService.CaptureReferenceGantry2Async(
                    BuildConfig(), _referenceCts.Token);

                // 填充选中基准点的Gantry2数据
                SelectedCommonReferencePoint.CommonY2 = commonY2;
                SelectedCommonReferencePoint.Gantry2VisionX = vx;
                SelectedCommonReferencePoint.Gantry2VisionY = vy;
                SelectedCommonReferencePoint.IsGantry2Captured = true;

                ComputeGantryTransformCommand.RaiseCanExecuteChanged();

                UpdateStatus(string.Format(L("DualGantryCalib_ReferenceCaptured", "公共基准点 {0} 采集完成") + " - Cam2",
                    SelectedCommonReferencePoint.Index), Brushes.LightGreen);
            }
            catch (OperationCanceledException)
            {
                _logger.Info("双龙门标定: Cam2公共基准采集已取消");
                UpdateStatus(L("DualGantryCalib_Idle", "空闲"), Brushes.LightGray);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "双龙门标定: Cam2公共基准采集失败");
                UpdateStatus($"{L("DualGantryCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
            }
            finally
            {
                IsCapturingReference = false;
            }
        }

        #endregion

        #region 跨龙门对齐

        /// <summary>计算跨龙门变换参数（基于公共基准点拟合OffsetX/OffsetY/RotationDeg）</summary>
        private void ExecuteComputeGantryTransform()
        {
            try
            {
                var capturedPoints = CommonReferencePoints.Where(p => p.IsCaptured).ToList();
                if (capturedPoints.Count < 2)
                {
                    UpdateStatus(L("DualGantryCalib_MinReferenceRequired", "跨龙门对齐至少需要2个公共基准点"),
                        Brushes.Orange);
                    return;
                }

                if (Gantry1CalibrationResult == null || Gantry2CalibrationResult == null)
                {
                    UpdateStatus(L("DualGantryCalib_CalibrationRequired", "请先完成双龙门标定"), Brushes.Orange);
                    return;
                }

                var transform = _calibService.ComputeGantryTransform(
                    capturedPoints, Gantry1CalibrationResult, Gantry2CalibrationResult);
                GantryTransform = transform;

                // 残差>0.05mm时警告
                if (transform.Residual > 0.05)
                {
                    UpdateStatus($"{L("DualGantryCalib_TransformWarning", "对齐残差较大，建议检查标定质量")} - Residual: {transform.Residual:F6}",
                        Brushes.Orange);
                }
                else
                {
                    UpdateStatus($"{L("DualGantryCalib_TransformComputed", "跨龙门对齐完成")} - Residual: {transform.Residual:F6}",
                        Brushes.LightGreen);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "双龙门标定: 跨龙门对齐失败");
                UpdateStatus($"{L("DualGantryCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
            }
        }

        /// <summary>
        /// 验证坐标变换（输入龙门1坐标，输出龙门2等效坐标）
        /// 输入: VerifyInputX = Dx（龙门1 X轴位置）, VerifyInputY = shared_Y + Dy（龙门1绝对Y）
        /// 输出: VerifyOutputX = X2（龙门2 X轴位置）, VerifyOutputY = shared_Y（龙门2共用Y轴坐标）
        /// </summary>
        private void ExecuteVerifyTransform()
        {
            if (GantryTransform == null || !GantryTransform.IsAligned) return;

            try
            {
                var (x2, y2) = GantryTransform.TransformGantry1ToGantry2(VerifyInputX, VerifyInputY);
                VerifyOutputX = x2;
                VerifyOutputY = y2;
                UpdateStatus(string.Format(L("DualGantryCalib_VerifyResult", "验证结果: ({0:F4}, {1:F4})"), x2, y2),
                    Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "双龙门标定: 坐标变换验证失败");
                UpdateStatus($"{L("DualGantryCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
            }
        }

        #endregion

        #region 文件操作

        /// <summary>保存配置到当前文件（或默认路径）</summary>
        private async Task ExecuteSaveConfigAsync()
        {
            try
            {
                var data = BuildCurrentData();
                Directory.CreateDirectory(ConfigDirectory);

                var fileName = CurrentFileName;
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = $"{DefaultFilePrefix}{DateTime.Now:yyyyMMdd_HHmmss}.json";
                }

                var filePath = Path.Combine(ConfigDirectory, fileName);
                await Task.Run(() =>
                {
                    var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                    File.WriteAllText(filePath, json);
                });

                data.Config.LastFileName = fileName;
                CurrentFileName = fileName;

                _parameterStorage.Save(ParameterStorageKey, data.Config, ConfigDirectory);

                UpdateStatus($"{L("DualGantryCalib_SaveSuccess", "保存成功")}: {fileName}", Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "双龙门标定: 保存失败");
                UpdateStatus($"{L("DualGantryCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
            }
        }

        /// <summary>另存为</summary>
        private async Task ExecuteSaveAsConfigAsync()
        {
            try
            {
                var defaultName = $"{DefaultFilePrefix}{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var filePath = _fileDialogService.ShowSaveFileDialog(
                    filter: "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    title: L("DualGantryCalib_SaveAs", "另存为"),
                    defaultFileName: defaultName);

                if (string.IsNullOrEmpty(filePath)) return;

                var data = BuildCurrentData();
                await Task.Run(() =>
                {
                    var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                    File.WriteAllText(filePath, json);
                });

                CurrentFileName = Path.GetFileName(filePath);
                data.Config.LastFileName = CurrentFileName;

                _parameterStorage.Save(ParameterStorageKey, data.Config, ConfigDirectory);

                UpdateStatus($"{L("DualGantryCalib_SaveSuccess", "保存成功")}: {CurrentFileName}", Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "双龙门标定: 另存为失败");
                UpdateStatus($"{L("DualGantryCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
            }
        }

        /// <summary>导入配置</summary>
        private async Task ExecuteImportConfigAsync()
        {
            try
            {
                var filePath = _fileDialogService.ShowOpenFileDialog(
                    filter: "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    title: L("DualGantryCalib_Import", "导入"),
                    initialDirectory: Directory.Exists(ConfigDirectory) ? ConfigDirectory : null);

                if (string.IsNullOrEmpty(filePath)) return;

                await LoadFromFileAsync(filePath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "双龙门标定: 导入失败");
                UpdateStatus($"{L("DualGantryCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
            }
        }

        /// <summary>导出配置</summary>
        private async Task ExecuteExportConfigAsync()
        {
            try
            {
                var defaultName = $"{DefaultFilePrefix}{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var filePath = _fileDialogService.ShowSaveFileDialog(
                    filter: "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    title: L("DualGantryCalib_Export", "导出"),
                    defaultFileName: defaultName);

                if (string.IsNullOrEmpty(filePath)) return;

                var data = BuildCurrentData();
                await Task.Run(() =>
                {
                    var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                    File.WriteAllText(filePath, json);
                });

                UpdateStatus($"{L("DualGantryCalib_SaveSuccess", "保存成功")}: {Path.GetFileName(filePath)}",
                    Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "双龙门标定: 导出失败");
                UpdateStatus($"{L("DualGantryCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
            }
        }

        #endregion

        #region 自动加载

        /// <summary>尝试自动加载上次使用的配置文件</summary>
        private async Task TryAutoLoadConfigAsync()
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);

                var defaultConfig = _parameterStorage.Load<DualGantryCalibrationConfig>(
                    ParameterStorageKey, ConfigDirectory);

                if (defaultConfig != null && !string.IsNullOrEmpty(defaultConfig.LastFileName))
                {
                    var filePath = Path.Combine(ConfigDirectory, defaultConfig.LastFileName);
                    if (File.Exists(filePath))
                    {
                        await LoadFromFileAsync(filePath);
                        return;
                    }
                }

                // 回退：加载目录下最新的双龙门标定文件
                if (Directory.Exists(ConfigDirectory))
                {
                    var latestFile = Directory.GetFiles(ConfigDirectory, $"{DefaultFilePrefix}*.json")
                        .OrderByDescending(File.GetLastWriteTime)
                        .FirstOrDefault();

                    if (latestFile != null)
                    {
                        await LoadFromFileAsync(latestFile);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"双龙门标定: 自动加载失败 - {ex.Message}");
            }
        }

        /// <summary>从文件加载双龙门标定数据并应用所有属性</summary>
        private async Task LoadFromFileAsync(string filePath)
        {
            var data = await Task.Run(() =>
            {
                var json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<DualGantryCalibrationData>(json);
            });

            if (data == null) return;

            // 应用配置
            SelectedStationIdentifier = data.Config.StationIdentifier;
            CommonAxisY = data.Config.CommonAxisY;
            Gantry1AxisX = data.Config.Gantry1AxisX;
            Gantry1AxisY = data.Config.Gantry1AxisY;
            Gantry2AxisX = data.Config.Gantry2AxisX;
            Gantry1TcpConnection = data.Config.Gantry1TcpConnection;
            Gantry2TcpConnection = data.Config.Gantry2TcpConnection;
            Gantry1TriggerCommand = data.Config.Gantry1TriggerCommand;
            Gantry2TriggerCommand = data.Config.Gantry2TriggerCommand;
            EnableVisionData = data.Config.EnableVisionData;
            PointCount = data.Config.PointCount;
            AutoCalibDelayMs = data.Config.AutoCalibDelayMs;

            // 应用龙门1点位数据
            Gantry1Points.Clear();
            foreach (var point in data.Gantry1Points)
            {
                Gantry1Points.Add(point);
            }

            // 应用龙门2点位数据
            Gantry2Points.Clear();
            foreach (var point in data.Gantry2Points)
            {
                Gantry2Points.Add(point);
            }

            // 应用公共基准点
            CommonReferencePoints.Clear();
            foreach (var point in data.CommonReferencePoints)
            {
                CommonReferencePoints.Add(point);
            }

            // 应用标定结果
            Gantry1CalibrationResult = data.Gantry1CalibrationResult;
            Gantry2CalibrationResult = data.Gantry2CalibrationResult;
            GantryTransform = data.GantryTransform;

            // 更新文件名（仅显示文件名）
            CurrentFileName = Path.GetFileName(filePath);

            UpdateStatus($"{L("DualGantryCalib_LoadSuccess", "加载成功")}: {CurrentFileName}", Brushes.LightGreen);
        }

        #endregion

        #region TCP连接

        /// <summary>加载TCP连接名列表（合并TCPClientManager与TCPEventService的连接）</summary>
        private async Task LoadTcpConnectionsAsync()
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    TcpConnections.Clear();

                    if (_tcpClientManager?.Clients != null)
                    {
                        foreach (var kvp in _tcpClientManager.Clients)
                        {
                            TcpConnections.Add(kvp.Key);
                        }
                    }

                    var serverNames = _tcpEventService?.GetServerNames();
                    if (serverNames != null)
                    {
                        foreach (var name in serverNames)
                        {
                            if (!TcpConnections.Contains(name))
                                TcpConnections.Add(name);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Warn($"双龙门标定: 加载TCP连接列表失败 - {ex.Message}");
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>根据PointCount更新龙门1点位集合（增减点位）</summary>
        private void UpdateGantry1PointsCollection()
        {
            while (Gantry1Points.Count < PointCount)
            {
                Gantry1Points.Add(new DualGantryCalibrationPoint
                {
                    Index = Gantry1Points.Count + 1,
                    Name = $"G1-P{Gantry1Points.Count + 1}"
                });
            }
            while (Gantry1Points.Count > PointCount && PointCount >= 1)
            {
                Gantry1Points.RemoveAt(Gantry1Points.Count - 1);
            }
            ComputeGantry1CalibrationCommand.RaiseCanExecuteChanged();
        }

        /// <summary>根据PointCount更新龙门2点位集合（增减点位）</summary>
        private void UpdateGantry2PointsCollection()
        {
            while (Gantry2Points.Count < PointCount)
            {
                Gantry2Points.Add(new DualGantryCalibrationPoint
                {
                    Index = Gantry2Points.Count + 1,
                    Name = $"G2-P{Gantry2Points.Count + 1}"
                });
            }
            while (Gantry2Points.Count > PointCount && PointCount >= 1)
            {
                Gantry2Points.RemoveAt(Gantry2Points.Count - 1);
            }
            ComputeGantry2CalibrationCommand.RaiseCanExecuteChanged();
        }

        /// <summary>构建当前双龙门标定完整数据对象（用于序列化）</summary>
        private DualGantryCalibrationData BuildCurrentData()
        {
            return new DualGantryCalibrationData
            {
                Config = BuildConfig(),
                Gantry1Points = Gantry1Points.ToList(),
                Gantry2Points = Gantry2Points.ToList(),
                CommonReferencePoints = CommonReferencePoints.ToList(),
                Gantry1CalibrationResult = Gantry1CalibrationResult,
                Gantry2CalibrationResult = Gantry2CalibrationResult,
                GantryTransform = GantryTransform
            };
        }

        /// <summary>更新状态栏（线程安全，切换到UI线程）</summary>
        private void UpdateStatus(string text, Brush color)
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                StatusText = text;
                StatusColor = color;
            });
        }

        /// <summary>获取本地化字符串</summary>
        private string L(string key, string defaultValue = "")
        {
            return _localization?.GetResourceOrDefault(key, defaultValue) ?? defaultValue;
        }

        #endregion

        #region 服务事件处理

        /// <summary>单点标定完成事件处理：根据龙门编号更新对应点位集合</summary>
        private void OnPointCalibrated(int gantryId, int index, DualGantryCalibrationPoint point)
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                var points = gantryId == 1 ? Gantry1Points : Gantry2Points;
                if (index >= 0 && index < points.Count)
                {
                    points[index].MachineX = point.MachineX;
                    points[index].MachineY = point.MachineY;
                    points[index].VisionX = point.VisionX;
                    points[index].VisionY = point.VisionY;
                    points[index].IsCalibrated = true;
                }

                if (gantryId == 1)
                    ComputeGantry1CalibrationCommand.RaiseCanExecuteChanged();
                else
                    ComputeGantry2CalibrationCommand.RaiseCanExecuteChanged();

                UpdateStatus(string.Format(L("DualGantryCalib_PointCalibrated", "点位 {0} 标定完成"), point.Name),
                    Brushes.LightGreen);
            });
        }

        /// <summary>视觉数据到达事件处理：更新状态</summary>
        private void OnVisionDataReceived(int gantryId, double x, double y)
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                var gantryName = gantryId == 1
                    ? L("DualGantryCalib_Gantry1", "龙门1")
                    : L("DualGantryCalib_Gantry2", "龙门2");
                UpdateStatus($"{L("DualGantryCalib_VisionDataReceived", "视觉数据已接收")} - {gantryName}: ({x:F4}, {y:F4})",
                    Brushes.LightGreen);
            });
        }

        /// <summary>单龙门标定完成事件处理：设置对应标定结果并重置标定状态</summary>
        private void OnGantryCalibrationCompleted(int gantryId, AffineCalibrationResult result)
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                if (gantryId == 1)
                {
                    Gantry1CalibrationResult = result;
                    IsGantry1Calibrating = false;
                    _calibService.UnsubscribeVisionData(1);
                    UpdateStatus($"{L("DualGantryCalib_Gantry1Calibrated", "龙门1标定完成")} - RMS: {result.RmsError:F6}",
                        result.RmsError < 0.05 ? Brushes.LightGreen : Brushes.Orange);
                }
                else
                {
                    Gantry2CalibrationResult = result;
                    IsGantry2Calibrating = false;
                    _calibService.UnsubscribeVisionData(2);
                    UpdateStatus($"{L("DualGantryCalib_Gantry2Calibrated", "龙门2标定完成")} - RMS: {result.RmsError:F6}",
                        result.RmsError < 0.05 ? Brushes.LightGreen : Brushes.Orange);
                }
            });
        }

        /// <summary>公共基准点采集完成事件处理：添加到集合</summary>
        private void OnCommonReferenceCaptured(CommonReferencePoint point)
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                point.Index = CommonReferencePoints.Count + 1;
                CommonReferencePoints.Add(point);
                ComputeGantryTransformCommand.RaiseCanExecuteChanged();
                UpdateStatus(string.Format(L("DualGantryCalib_ReferenceCaptured", "公共基准点 {0} 采集完成"), point.Index),
                    Brushes.LightGreen);
            });
        }

        /// <summary>跨龙门对齐完成事件处理：设置变换参数</summary>
        private void OnGantryTransformComputed(GantryTransform transform)
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                GantryTransform = transform;
                if (transform.Residual > 0.05)
                {
                    UpdateStatus($"{L("DualGantryCalib_TransformWarning", "对齐残差较大，建议检查标定质量")} - Residual: {transform.Residual:F6}",
                        Brushes.Orange);
                }
                else
                {
                    UpdateStatus($"{L("DualGantryCalib_TransformComputed", "跨龙门对齐完成")} - Residual: {transform.Residual:F6}",
                        Brushes.LightGreen);
                }
            });
        }

        /// <summary>标定错误事件处理：更新状态并重置标定状态</summary>
        private void OnCalibrationError(int gantryId, string error)
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                if (gantryId == 1)
                {
                    IsGantry1Calibrating = false;
                }
                else if (gantryId == 2)
                {
                    IsGantry2Calibrating = false;
                }
                UpdateStatus($"{L("DualGantryCalib_Error", "标定错误")}: {error}", Brushes.Red);
            });
        }

        #endregion

        /// <summary>跨龙门计算方法</summary>
        //  龙门1基准坐标(CameraRefX, CameraRefY)
        //  龙门2基准坐标(GripperRefX, GripperRefY)
        //  龙门1移动量 Δx1 = TransResultX - CameraRefX
        //   Δy1 = TransResultY - CameraRefY

        // 标定补偿（差分移动只需旋转+缩放，偏移量抵消）：
        //  Δx2 = (Δx1·cos(θ) - Δy1·sin(θ)) · Scale
        //  Δy2 = (Δx1·sin(θ) + Δy1·cos(θ)) · Scale

        // 龙门2最终位置（保持现有符号约定）：
        //  GripperFinalX = GripperRefX - Δx2
        //  GripperFinalY = GripperRefY + Δy2

    }
}
