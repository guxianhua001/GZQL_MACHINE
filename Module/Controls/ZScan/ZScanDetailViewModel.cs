using Core.Abstraction;
using Core.Models;
using Core.Services;
using Core.Utilities;
using Framework.Models;
using Microsoft.Win32;
using Module.Models;
using Module.Services;
using MotionControl.Interfaces;
using MotionControl.Services;
using Prism.Commands;
using Prism.Events;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using Recipe.Events;
using Recipe.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TCPIPModule.Interfaces;

namespace Module.ViewModels
{
    /// <summary>
    /// Z-SCAN 步骤详细配置 ViewModel，以模态弹窗形式展示
    /// 支持图片管理、运动控制、通讯配置、数据接收等完整功能
    /// </summary>
    public class ZScanDetailViewModel : BindableBase, IDialogAware
    {
        private readonly IDialogService _dialogService;
        private readonly IMotionService _motionService;
        private readonly ITCPClientManagerService _tcpClientManagerService;
        private readonly ITCPEventService _tcpEventService;
        private readonly IContainerProvider _containerProvider;
        private readonly ILoggerService _logger;
        private readonly IZScanConfigService _zscanConfigService;
        private readonly IZScanCalibrationService _zscanCalibrationService;
        private readonly IZScanArcCompensationService _zscanArcCompensationService;
        private readonly INeedleTeachService _needleTeachService;
        /// <summary> 点胶工站 ZScan 操作接口（运动序列委托给 DispensingTask 执行） </summary>
        private readonly IDispensingZScanOperations _dispensingOps;
        /// <summary> 配方池服务（用于全局变量链接读写） </summary>
        private readonly IRecipePoolService _recipePoolService;
        private readonly IEventAggregator _eventAggregator;

        #region 原有属性

        private int _totalPoints;
        private string _zNominalRange;
        private double _zMaxDelta;
        private string _statusText;
        private Brush _statusColor;
        private ObservableCollection<ZScanPointDetail> _pointDetails;
        private ZScanPointDetail _selectedPointDetail;

        /// <summary> 批量更新测量点时抑制逐行全局变量回写，结束后统一同步 </summary>
        private bool _suppressLinkedGlobalVariableSync;

        #endregion

        #region Task 7: 运动控制属性

        private bool _isScanning;
        /// <summary> 是否正在执行3D扫描（用于禁用按钮防止重复操作）</summary>
        public bool IsScanning { get => _isScanning; set => SetProperty(ref _isScanning, value); }

        // 运动参数（从位置服务或配方加载）
        private int _zAxisIdNeedle1 = 3;
        /// <summary> 针头1对应的Z轴编号（Dz₂, LogicalId=3） </summary>
        public int ZAxisIdNeedle1 { get => _zAxisIdNeedle1; set => SetProperty(ref _zAxisIdNeedle1, value); }

        private int _zAxisIdNeedle2 = 4;
        /// <summary> 针头2对应的Z轴编号（Dz₃, LogicalId=4），双针头支持 </summary>
        public int ZAxisIdNeedle2 { get => _zAxisIdNeedle2; set => SetProperty(ref _zAxisIdNeedle2, value); }

        /// <summary> 根据当前针头索引返回对应 Z 轴 ID（针头1→Dz₂/ZAxisIdNeedle1, 针头2→Dz₃/ZAxisIdNeedle2） </summary>
        private int ResolveCurrentZAxisId() =>
            _currentNeedleIndex == 0 ? ZAxisIdNeedle1 : ZAxisIdNeedle2;

        private int _xAxisId = 8;
        /// <summary> X轴编号 </summary>
        public int XAxisId { get => _xAxisId; set => SetProperty(ref _xAxisId, value); }
        private int _yAxisId = 6;
        public int YAxisId { get => _yAxisId; set => SetProperty(ref _yAxisId, value); }

        private double _zInitPosition = 0.0;
        /// <summary> Z轴初始/安全高度位置（mm）</summary>
        public double ZInitPosition { get => _zInitPosition; set => SetProperty(ref _zInitPosition, value); }

        private double _moveSpeed = 30.0;
        /// <summary> 运动速度（mm/s）— 用于针头示教、移动基准Z等辅助操作 </summary>
        public double MoveSpeed { get => _moveSpeed; set => SetProperty(ref _moveSpeed, value); }

        private double _scanSpeed = 30.0;
        /// <summary> 扫描速度（mm/s，范围10-60）— Dx轴从起始点到结束点的运动速度 </summary>
        public double ScanSpeed
        {
            get => _scanSpeed;
            set
            {
                var clamped = Math.Clamp(value, 10.0, 60.0);
                if (SetProperty(ref _scanSpeed, clamped))
                    RaisePropertyChanged(nameof(ScanSpeedDisplay));
            }
        }
        /// <summary> 扫描速度显示文本（带单位） </summary>
        public string ScanSpeedDisplay => $"{_scanSpeed:F1} mm/s";

        #endregion

        #region 位置编辑器引用属性

        private string _safePositionName = "SafePosition";
        /// <summary> 安全位置名（来自位置编辑器），Dz₁/Dz₂/Dz3 抬起到此高度 </summary>
        public string SafePositionName
        {
            get => _safePositionName;
            set => SetProperty(ref _safePositionName, value);
        }

        private string _scanStartPositionName = "ScanStartPosition";
        /// <summary> 3D扫描起始位置名，Dx+Dy 插补运动到此位 </summary>
        public string ScanStartPositionName
        {
            get => _scanStartPositionName;
            set => SetProperty(ref _scanStartPositionName, value);
        }

        private string _scanEndPositionName = "ScanEndPosition";
        /// <summary> 3D扫描结束位置名，Dx 单独运动到此位 </summary>
        public string ScanEndPositionName
        {
            get => _scanEndPositionName;
            set => SetProperty(ref _scanEndPositionName, value);
        }

        private string _standbyPositionName = "StandbyPosition";
        /// <summary> 待机位置名，Dx+Dy 插补运动到此位 </summary>
        public string StandbyPositionName
        {
            get => _standbyPositionName;
            set => SetProperty(ref _standbyPositionName, value);
        }

        private string _triggerIOName = "Q3.3DispensingStation3DCameraTrigger";
        /// <summary> 3D相机触发IO端口名（来自 hwcfg.xml） </summary>
        public string TriggerIOName
        {
            get => _triggerIOName;
            set => SetProperty(ref _triggerIOName, value);
        }

        private int _dataReceiveTimeoutMs = 10000;
        /// <summary> 相机数据接收超时时间（毫秒） </summary>
        public int DataReceiveTimeoutMs
        {
            get => _dataReceiveTimeoutMs;
            set => SetProperty(ref _dataReceiveTimeoutMs, value);
        }

        /// <summary> 扫描数据接收 TaskCompletionSource（供 SubscribeCameraData 回调触发） </summary>
        private System.Threading.Tasks.TaskCompletionSource<List<double>> _scanDataTcs;

        #endregion

        #region Task 8: 通讯配置属性

        /// <summary> 通讯方式选项列表 </summary>
        public ObservableCollection<string> CommunicationTypes { get; } = new ObservableCollection<string> { "TCPIP", "Serial" };

        private string _selectedCommunicationType = "TCPIP";
        /// <summary> 当前选择的通讯方式 </summary>
        public string SelectedCommunicationType
        {
            get => _selectedCommunicationType;
            set { if (SetProperty(ref _selectedCommunicationType, value)) RaisePropertyChanged(nameof(IsTcpSelected)); }
        }

        /// <summary> 当前是否选择了 TCPIP 通讯方式 </summary>
        public bool IsTcpSelected => SelectedCommunicationType == "TCPIP";

        /// <summary> TCP 连接名称列表 </summary>
        public ObservableCollection<string> TcpConnections { get; } = new ObservableCollection<string>();

        private string _selectedConnectionName;
        /// <summary> 当前选择的TCP连接名称（对应ITCPEventService.CameraMessageReceived事件的cameraName参数）</summary>
        public string SelectedConnectionName { get => _selectedConnectionName; set => SetProperty(ref _selectedConnectionName, value); }

        /// <summary> TCP数据接收事件处理器引用，用于取消订阅防止内存泄漏 </summary>
        private Action<string, string>? _cameraDataHandler;

        /// <summary> PointDetails 集合变更事件处理器引用 </summary>
        private NotifyCollectionChangedEventHandler? _pointDetailsCollectionChangedHandler;

        /// <summary> 单个测量点属性变更事件处理器引用 </summary>
        private PropertyChangedEventHandler? _pointPropertyChangedHandler;

        #endregion

        #region 双针头支持（仅 Z 标定参数分针头；数据表格全局共享）

        /// <summary> 每根针头的 Z 标定状态（不含测量表格） </summary>
        private class NeedleState
        {
            public ZScanCalibrationConfig Calibration { get; set; } = new ZScanCalibrationConfig();
            public double NeedleCompensationInput, NeedleCompensationValue;
            public int CalibrationStep;
            public string LastCalibrationTimeText = string.Empty;
        }

        private readonly NeedleState[] _needles = { new NeedleState(), new NeedleState() };

        private int _currentNeedleIndex;
        /// <summary> 当前活动针头索引（0=Dz1, 1=Dz2），仅切换 Z 标定参数，数据表格不变 </summary>
        public int CurrentNeedleIndex
        {
            get => _currentNeedleIndex;
            set
            {
                if (_currentNeedleIndex == value) return;
                SaveCurrentNeedleState();
                SetProperty(ref _currentNeedleIndex, value);
                // 先切换标定服务针头，再恢复 UI（避免写入错误针头）
                _zscanCalibrationService.SetCurrentNeedle(value);
                LoadCurrentNeedleState();
                RaisePropertyChanged(nameof(CurrentNeedleDisplayName));
            }
        }

        /// <summary> 当前针头显示名称（用于标定区域标题） </summary>
        public string CurrentNeedleDisplayName => _currentNeedleIndex == 0 ? "Dz1" : "Dz2";

        /// <summary> 保存当前针头的 Z 标定参数到 _needles[_currentNeedleIndex] </summary>
        private void SaveCurrentNeedleState()
        {
            CaptureCalibrationToNeedleState(_needles[_currentNeedleIndex]);
        }

        /// <summary> 将 UI 与标定服务中的标定参数写入针头状态 </summary>
        private void CaptureCalibrationToNeedleState(NeedleState s)
        {
            var cal = s.Calibration ??= new ZScanCalibrationConfig();
            cal.CameraZOffset = _zscanCalibrationService.CameraZOffset;
            cal.NeedleZOffset = _zscanCalibrationService.NeedleZOffset;
            cal.BaseZ = _baseZInput;
            cal.MeasuredMZ = _measuredMZ;
            cal.DeltaZ = _deltaZInput;
            cal.CurrentZHeight = _currentZHeightInput;
            cal.ZHeightDifference = _zHeightDifference;
            cal.BaseDispenseHeight = _baseDispenseHeight;
            cal.DispenseHeight = _calculatedDispenseHeight;
            s.NeedleCompensationInput = _needleCompensationInput;
            s.NeedleCompensationValue = _needleCompensationValue;
            s.CalibrationStep = _calibrationStep;
            s.LastCalibrationTimeText = _lastCalibrationTimeText;
        }

        /// <summary> 从针头状态恢复标定参数到 UI 与标定服务 </summary>
        private void ApplyCalibrationFromNeedleState(NeedleState s)
        {
            var cal = s.Calibration ?? new ZScanCalibrationConfig();
            _zscanCalibrationService.RestoreState(cal.CameraZOffset, cal.NeedleZOffset, cal.BaseZ, cal.MeasuredMZ);

            _suppressBaseZAutoApply = true;
            _baseZInput = cal.BaseZ;
            RaisePropertyChanged(nameof(BaseZInput));
            _suppressBaseZAutoApply = false;

            _needleZOffset = cal.NeedleZOffset;
            RaisePropertyChanged(nameof(NeedleZOffset));
            _measuredMZ = cal.MeasuredMZ;
            RaisePropertyChanged(nameof(MeasuredMZ));
            _deltaZInput = cal.DeltaZ;
            RaisePropertyChanged(nameof(DeltaZInput));
            _currentZHeightInput = cal.CurrentZHeight;
            RaisePropertyChanged(nameof(CurrentZHeightInput));
            _zHeightDifference = cal.ZHeightDifference;
            RaisePropertyChanged(nameof(ZHeightDifference));
            _baseDispenseHeight = cal.BaseDispenseHeight;
            RaisePropertyChanged(nameof(BaseDispenseHeight));
            _calculatedDispenseHeight = cal.DispenseHeight;
            RaisePropertyChanged(nameof(CalculatedDispenseHeight));
            _needleCompensationInput = s.NeedleCompensationInput;
            RaisePropertyChanged(nameof(NeedleCompensationInput));
            _needleCompensationValue = s.NeedleCompensationValue;
            RaisePropertyChanged(nameof(NeedleCompensationValue));
            _calibrationStep = s.CalibrationStep;
            RaisePropertyChanged(nameof(CalibrationStep));
            _lastCalibrationTimeText = s.LastCalibrationTimeText;
            RaisePropertyChanged(nameof(LastCalibrationTimeText));
        }

        /// <summary> 从 _needles[_currentNeedleIndex] 恢复 Z 标定参数（数据表格不切换） </summary>
        private void LoadCurrentNeedleState()
        {
            ApplyCalibrationFromNeedleState(_needles[_currentNeedleIndex]);
        }

        #endregion

        #region 标定功能属性

        private double _needleZOffset;
        public double NeedleZOffset { get => _needleZOffset; set => SetProperty(ref _needleZOffset, value); }

        private double _needleCompensationInput;
        public double NeedleCompensationInput { get => _needleCompensationInput; set => SetProperty(ref _needleCompensationInput, value); }

        private string _lastCalibrationTimeText = string.Empty;
        public string LastCalibrationTimeText { get => _lastCalibrationTimeText; set => SetProperty(ref _lastCalibrationTimeText, value); }

        private double _baseZInput;
        private bool _suppressBaseZAutoApply;

        /// <summary> 基准Z输入；失焦后自动同步到标定服务 Current.BaseZ </summary>
        public double BaseZInput
        {
            get => _baseZInput;
            set
            {
                if (!SetProperty(ref _baseZInput, value))
                    return;
                SyncBaseZToService(value, advanceStep: !_suppressBaseZAutoApply, logChange: !_suppressBaseZAutoApply);
            }
        }

        private double _measuredMZ;
        public double MeasuredMZ { get => _measuredMZ; set => SetProperty(ref _measuredMZ, value); }

        private double _deltaZInput;
        public double DeltaZInput { get => _deltaZInput; set => SetProperty(ref _deltaZInput, value); }

        private double _needleCompensationValue;
        public double NeedleCompensationValue { get => _needleCompensationValue; set => SetProperty(ref _needleCompensationValue, value); }

        private double _calculatedDispenseHeight;
        public double CalculatedDispenseHeight { get => _calculatedDispenseHeight; set => SetProperty(ref _calculatedDispenseHeight, value); }

        private int _calibrationStep;
        public int CalibrationStep { get => _calibrationStep; set => SetProperty(ref _calibrationStep, value); }

        private double _currentZHeightInput;
        public double CurrentZHeightInput { get => _currentZHeightInput; set => SetProperty(ref _currentZHeightInput, value); }

        private double _zHeightDifference;
        public double ZHeightDifference { get => _zHeightDifference; set => SetProperty(ref _zHeightDifference, value); }

        private double _baseDispenseHeight;
        public double BaseDispenseHeight { get => _baseDispenseHeight; set => SetProperty(ref _baseDispenseHeight, value); }

        private string _currentFilePath;
        public string CurrentFilePath { get => _currentFilePath; set => SetProperty(ref _currentFilePath, value); }

        #endregion

        #region 多表格管理属性

        private ObservableCollection<ZScanTableConfig> _tables = new ObservableCollection<ZScanTableConfig>();
        public ObservableCollection<ZScanTableConfig> Tables { get => _tables; set => SetProperty(ref _tables, value); }

        private ZScanTableConfig _selectedTable;
        public ZScanTableConfig SelectedTable { get => _selectedTable; set { if (SetProperty(ref _selectedTable, value)) OnSelectedTableChanged(); } }

        private ZScanDataFormat _currentDataFormat = ZScanDataFormat.DoubleArray;
        public ZScanDataFormat CurrentDataFormat { get => _currentDataFormat; set { if (SetProperty(ref _currentDataFormat, value)) OnDataFormatChanged(); } }

        public ObservableCollection<ZScanDataFormat> DataFormatOptions { get; } = new ObservableCollection<ZScanDataFormat> { ZScanDataFormat.Double, ZScanDataFormat.DoubleArray };

        #endregion

        #region 命令定义

        // 原有命令
        public ICommand AddRowCommand { get; }
        public ICommand DeleteSelectedRowCommand { get; }
        public ICommand ImportCSVCommand { get; }
        public ICommand ExportCSVCommand { get; }
        public ICommand RescanCommand { get; }

        // Task 7: 运动控制命令
        /// <summary> 开始3D扫描流程（移动轴→触发相机→等待数据→解析更新）</summary>
        public ICommand Start3DScanCommand { get; }
        /// <summary> 紧急停止当前运动（优先级最高）</summary>
        public ICommand StopCommand { get; }
        /// <summary> 移动到安全待机位置 </summary>
        public ICommand ReturnToStandbyCommand { get; }

        // 标定命令
        public ICommand CalibrateCameraZCommand { get; }
        public ICommand ApplyNeedleCompensationCommand { get; }
        public ICommand ResetCalibrationCommand { get; }
        public ICommand MoveNeedleToBaseZCommand { get; }
        public ICommand TeachNeedleMZCommand { get; }
        public ICommand CalculateDispenseHeightCommand { get; }

        // 多表格管理命令
        public ICommand AddTableCommand { get; }
        public ICommand DeleteTableCommand { get; }
        public ICommand SaveConfigCommand { get; }
        public ICommand LoadConfigCommand { get; }

        // 全局变量链接命令
        public ICommand UnlinkRowGlobalVariableCommand { get; }

        public ObservableCollection<GlobalVariable> AvailableGlobalVariables { get; }

        #endregion

        /// <summary>
        /// 构造函数：注入所有依赖服务
        /// </summary>
        public ZScanDetailViewModel(
            IDialogService dialogService,
            IMotionService motionService,
            ITCPClientManagerService tcpClientManagerService,
            ITCPEventService tcpEventService,
            IContainerProvider containerProvider,
            ILoggerService logger,
            IZScanConfigService zscanConfigService,
            IZScanCalibrationService zscanCalibrationService,
            IZScanArcCompensationService zscanArcCompensationService,
            INeedleTeachService needleTeachService,
            IDispensingZScanOperations dispensingOps,
            IEventAggregator eventAggregator)
        {
            _dialogService = dialogService;
            _motionService = motionService;
            _tcpClientManagerService = tcpClientManagerService;
            _tcpEventService = tcpEventService;
            _containerProvider = containerProvider;
            _logger = logger;
            _zscanConfigService = zscanConfigService;
            _zscanCalibrationService = zscanCalibrationService;
            _zscanArcCompensationService = zscanArcCompensationService;
            _needleTeachService = needleTeachService;
            _dispensingOps = dispensingOps;
            _eventAggregator = eventAggregator;
            _recipePoolService = _containerProvider.Resolve<IRecipePoolService>();

            _zscanCalibrationService.CalibrationChanged += OnCalibrationChanged;

            // 订阅全局变量变更：全局变量页保存/配方池保存后同步刷新可链接列表
            _eventAggregator.GetEvent<GlobalVariablesChangedEvent>()
                .Subscribe(OnGlobalVariablesChanged, ThreadOption.UIThread);
            _eventAggregator.GetEvent<RecipePoolChangedEvent>()
                .Subscribe(OnRecipePoolChanged, ThreadOption.UIThread);

            // 原有命令初始化
            AddRowCommand = new DelegateCommand(OnAddRow);
            DeleteSelectedRowCommand = new DelegateCommand(OnDeleteSelectedRow, () => SelectedPointDetail != null).ObservesProperty(() => SelectedPointDetail);
            ImportCSVCommand = new DelegateCommand(OnImportCSV);
            ExportCSVCommand = new DelegateCommand(OnExportCSV);
            RescanCommand = new DelegateCommand(OnRescan);

            // Task 7: 运动控制命令初始化（带启用条件）
            Start3DScanCommand = new DelegateCommand(async () => await OnStart3DScanAsync(), CanStartScan)
                .ObservesProperty(() => IsScanning);
            StopCommand = new DelegateCommand(OnStop);
            ReturnToStandbyCommand = new DelegateCommand(async () => await OnReturnToStandbyAsync(), CanReturnToStandby)
                .ObservesProperty(() => IsScanning);

            CalibrateCameraZCommand = new DelegateCommand(OnCalibrateCameraZ);
            ApplyNeedleCompensationCommand = new DelegateCommand(OnApplyNeedleCompensation, () => NeedleCompensationInput != 0).ObservesProperty(() => NeedleCompensationInput);
            ResetCalibrationCommand = new DelegateCommand(OnResetCalibration);

            MoveNeedleToBaseZCommand = new DelegateCommand(async () => await OnMoveNeedleToBaseZAsync(), () => BaseZInput > 0).ObservesProperty(() => BaseZInput);
            TeachNeedleMZCommand = new DelegateCommand(async () => await OnTeachNeedleMZAsync() );
            CalculateDispenseHeightCommand = new DelegateCommand(OnCalculateDispenseHeight);

            AddTableCommand = new DelegateCommand(OnAddTable);
            DeleteTableCommand = new DelegateCommand(OnDeleteTable, () => SelectedTable != null).ObservesProperty(() => SelectedTable);
            SaveConfigCommand = new DelegateCommand(OnSaveConfig);
            LoadConfigCommand = new DelegateCommand(() => OnLoadConfig());

            UnlinkRowGlobalVariableCommand = new DelegateCommand(() =>
            {
                if (SelectedPointDetail != null)
                    OnUnlinkRowGlobalVariable(SelectedPointDetail);
            });
            AvailableGlobalVariables = new ObservableCollection<GlobalVariable>();
            LoadAvailableGlobalVariables();
        }

        #region 原有属性实现

        /// <summary> Z-SCAN 详情页标题 </summary>
        public string Title => "Z-SCAN DETAIL";

        public int TotalPoints
        {
            get => _totalPoints;
            set => SetProperty(ref _totalPoints, value);
        }

        public string ZNominalRange
        {
            get => _zNominalRange;
            set => SetProperty(ref _zNominalRange, value);
        }

        public double ZMaxDelta
        {
            get => _zMaxDelta;
            set => SetProperty(ref _zMaxDelta, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public Brush StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }

        public ObservableCollection<ZScanPointDetail> PointDetails
        {
            get => _pointDetails;
            set => SetProperty(ref _pointDetails, value);
        }

        public ZScanPointDetail SelectedPointDetail
        {
            get => _selectedPointDetail;
            set => SetProperty(ref _selectedPointDetail, value);
        }

        #endregion

        /// <summary>
        /// 显示提示消息弹窗（使用 CustomDialog UserControl + Prism IDialogService）
        /// 带"确定"按钮，用户点击后关闭
        /// </summary>
        private void ShowHintMessage(string message, string title = "提示")
        {
            var buttons = new ObservableCollection<DialogButton>
            {
                new DialogButton { Text = "确定", BackgroundHex = "#2196F3", ButtonIndex = 0 }
            };
            _dialogService.ShowDialog("CustomDialog", new DialogParameters
            {
                { "title", title },
                { "message", message },
                { "buttons", buttons }
            }, _ => { });
        }

        #region 命令启用条件判断方法

        /// <summary> 判断是否可以开始扫描：非扫描状态且设备就绪 </summary>
        private bool CanStartScan() => !IsScanning;

        /// <summary> 判断是否可以返回待机：非扫描状态 </summary>
        private bool CanReturnToStandby() => !IsScanning;

        #endregion

        #region 对话框生命周期

        public void OnDialogOpened(IDialogParameters parameters)
        {
            InitializeCore();

            _logger?.Info("Z-SCAN Detail 弹窗打开");
        }

        /// <summary>
        /// 嵌入模式初始化方法（供 ZScanDetailView.xaml.cs 的 Loaded 事件调用）
        /// 当 ZScanDetailView 被直接嵌入到其他页面时使用此路径
        /// 与 OnDialogOpened() 共享相同的初始化逻辑，但无需对话框参数
        /// </summary>
        public void InitializeForEmbeddedMode()
        {
            InitializeCore();

            _logger?.Info("Z-SCAN Detail 嵌入模式初始化完成");
        }

        /// <summary>
        /// 核心初始化逻辑（对话框模式和嵌入模式共享）
        /// 包括：数据加载、运动参数、TCP连接、事件订阅
        /// </summary>
        private void InitializeCore()
        {
            LoadSampleData();
            LoadTcpConnections();
            SubscribeCameraData();

            // Task 9: 注册 PointDetails 事件监听（自动计算引擎）
            SubscribePointDetailsEvents();
            OnLoadConfig(showDialog: false);  // 初始化时自动加载最新文件，不弹对话框
            UpdateCalibrationDisplay();
        }

        public void OnDialogClosed()
        {
            UnsubscribeCameraData();

            // Task 9: 取消 PointDetails 事件监听，防止内存泄漏
            UnsubscribePointDetailsEvents();

            _eventAggregator.GetEvent<GlobalVariablesChangedEvent>().Unsubscribe(OnGlobalVariablesChanged);
            _eventAggregator.GetEvent<RecipePoolChangedEvent>().Unsubscribe(OnRecipePoolChanged);

            _logger?.Info("Z-SCAN Detail 弹窗关闭");
        }

        public bool CanCloseDialog() => true;

        public event Action<IDialogResult> RequestClose;

        #endregion

        #region 数据加载方法

        private void LoadSampleData()
        {
            TotalPoints = 48;
            ZNominalRange = "3.500 – 5.200 mm";
            ZMaxDelta = 0.031;
            StatusText = "Scanned";
            StatusColor = Brushes.Green;

            PointDetails = new ObservableCollection<ZScanPointDetail>
            {
                new ZScanPointDetail { Segment = 1, PointNumber = 1, X = 10.500, Y = 20.300, ZNominal = 5.000, ZMeasured = 5.012, DeltaZ = 0.012, FeatureName = "tab001", Nominal = 5.000, Range = 0.100, DataIndex = 0 },
                new ZScanPointDetail { Segment = 1, PointNumber = 2, X = 10.800, Y = 20.500, ZNominal = 5.010, ZMeasured = 5.025, DeltaZ = 0.015, FeatureName = "tab002", Nominal = 5.010, Range = 0.100, DataIndex = 1 },
                new ZScanPointDetail { Segment = 1, PointNumber = 3, X = 11.100, Y = 20.700, ZNominal = 5.020, ZMeasured = 5.051, DeltaZ = 0.031, FeatureName = "pillar001", Nominal = 5.020, Range = 0.100, DataIndex = 2 },
                new ZScanPointDetail { Segment = 1, PointNumber = 4, X = 11.400, Y = 20.900, ZNominal = 5.030, ZMeasured = 5.049, DeltaZ = 0.019, FeatureName = "pillar002", Nominal = 5.030, Range = 0.100, DataIndex = 3 },
                new ZScanPointDetail { Segment = 1, PointNumber = 32, X = 22.000, Y = 28.100, ZNominal = 5.200, ZMeasured = 5.218, DeltaZ = 0.018, FeatureName = "chassis012", Nominal = 5.200, Range = 0.100, DataIndex = 4 }
            };

            // Task 9: 初始化完成后重新计算所有行和统计信息
            foreach (var point in PointDetails)
            {
                RecalculateRow(point);
            }
            RecalculateStatistics();
        }

        #endregion

        #region Task 7: 运动控制逻辑实现

        /// <summary>
        /// 执行完整的3D扫描流程（运动委托给 DispensingTask，数据接收在 VM 层处理）：
        /// 1-7. 运动序列由 IDispensingZScanOperations.ExecuteZScan3DSequenceAsync 执行
        /// 8. 异步等待相机数据并解析到表格 ZActual
        /// 触发拍照后就异步等待接收数据，带超时报警
        /// </summary>
        private async Task OnStart3DScanAsync()
        {
            if (IsScanning) return;

            var cts = new CancellationTokenSource();
            try
            {
                IsScanning = true;
                StatusText = "Scanning...";
                StatusColor = Brushes.Orange;
                _logger?.Info("Z-SCAN 开始3D扫描");

                // 创建数据接收 TaskCompletionSource
                _scanDataTcs = new TaskCompletionSource<List<double>>();

                // 步骤1-7：运动序列委托给 DispensingTask（享受 RunStep 安全保护）
                // Dx 轴使用 ScanSpeed（用户设置10-60mm/s），其他轴从轴参数配置获取速度
                await _dispensingOps.ExecuteZScan3DSequenceAsync(
                    SafePositionName, ScanStartPositionName, ScanEndPositionName,
                    StandbyPositionName, TriggerIOName, ScanSpeed,
                    status => { StatusText = status; },
                    cts.Token);

                // 步骤8：异步等待相机数据（带超时报警）
                _logger?.Info($"Z-SCAN 等待3D相机数据返回（超时={DataReceiveTimeoutMs}ms）...");
                StatusText = "Waiting for data...";
                var timeoutTask = Task.Delay(DataReceiveTimeoutMs, cts.Token);
                var completedTask = await Task.WhenAny(_scanDataTcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    // 超时报警
                    StatusText = "Data Timeout";
                    StatusColor = Brushes.Red;
                    _logger?.Error($"Z-SCAN 相机数据接收超时 ({DataReceiveTimeoutMs}ms)");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ShowHintMessage($"3D相机数据接收超时（{DataReceiveTimeoutMs}ms），请检查相机连接和触发信号。");
                    });
                }
                else
                {
                    // 数据已接收，更新表格并自动保存
                    var parsedValues = await _scanDataTcs.Task;
                    _logger?.Info($"Z-SCAN 接收到 {parsedValues.Count} 个测量点数据");
                    if (parsedValues.Count > 0)
                    {
                        UpdatePointDetailsFromCameraData(parsedValues);
                        RecalculateStatistics();

                        // 自动保存到 ZScan 文件夹
                        await AutoSaveZScanConfigAsync();
                    }

                    StatusText = "Scan Completed";
                    StatusColor = Brushes.Green;
                    _logger?.Info("Z-SCAN 3D扫描完成");
                }
            }
            catch (OperationCanceledException)
            {
                StatusText = "Stopped";
                StatusColor = Brushes.Yellow;
                _logger?.Warn("Z-SCAN 扫描被用户停止");
            }
            catch (Exception ex)
            {
                StatusText = "Error";
                StatusColor = Brushes.Red;
                _logger?.Error($"Z-SCAN 扫描异常: {ex.Message}\n{ex.StackTrace}");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ShowHintMessage($"扫描失败: {ex.Message}");
                });
            }
            finally
            {
                IsScanning = false;
                cts.Dispose();
            }
        }

        /// <summary>
        /// 紧急停止当前运动（优先级最高）
        /// 立即调用IMotionService.StopAxis()停止所有轴运动
        /// </summary>
        private void OnStop()
        {
            try
            {
                _logger?.Warn("Z-SCAN 用户触发停止");

                // 同时停止两根针头的Z轴和X轴（安全优先）
                _motionService.StopAxis(ZAxisIdNeedle1);
                _motionService.StopAxis(ZAxisIdNeedle2);
                _motionService.StopAxis(XAxisId);
                _motionService.StopAxis(YAxisId);

                IsScanning = false;
                StatusText = "Stopped";
                StatusColor = Brushes.Red;

                ShowHintMessage("已停止！请检查设备状态后重试。");
                _logger?.Info("Z-SCAN 停止命令已执行");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Z-SCAN 停止失败: {ex.Message}");
                ShowHintMessage($"停止失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 返回待机位动作（运动委托给 DispensingTask）：
        /// 1. Dz₁/Dz₂/Dz3 抬起到安全高度（并行）
        /// 2. Dx+Dy 插补运动到待机位（StandbyPosition）
        /// </summary>
        private async Task OnReturnToStandbyAsync()
        {
            if (IsScanning) return;

            var cts = new CancellationTokenSource();
            try
            {
                StatusText = "Returning to Standby...";
                StatusColor = Brushes.Orange;
                _logger?.Info("Z-SCAN 开始返回待机位置");

                // 运动序列委托给 DispensingTask（享受 RunStep 安全保护）
                // 各轴速度从轴参数配置获取 + 全局速度比例
                await _dispensingOps.ReturnToStandbyAsync(
                    SafePositionName, StandbyPositionName,
                    status => { StatusText = status; },
                    cts.Token);

                StatusText = "Standby";
                StatusColor = Brushes.Blue;
                _logger?.Info("Z-SCAN 已返回待机位置");
            }
            catch (OperationCanceledException)
            {
                StatusText = "Stopped";
                StatusColor = Brushes.Yellow;
                _logger?.Warn("Z-SCAN 返回待机被中断");
            }
            catch (Exception ex)
            {
                StatusText = "Error";
                StatusColor = Brushes.Red;
                _logger?.Error($"Z-SCAN 返回待机失败: {ex.Message}");
                ShowHintMessage($"返回待机失败: {ex.Message}");
            }
            finally
            {
                cts.Dispose();
            }
        }

        #endregion

        #region Task 8: 通讯配置与数据接收实现

        /// <summary>
        /// 从IAppSettingService加载所有已配置的TCP连接名称
        /// 包含Client和Server两种模式的所有配置项
        /// 参考ScanDetailViewModel.LoadTcpConnections()方法（第410-434行）
        /// </summary>
        private void LoadTcpConnections()
        {
            TcpConnections.Clear();
            try
            {
                // 优先从AppSettingService获取所有配置项（含Server模式）
                var appConfig = _containerProvider.Resolve<IAppSettingService>();
                if (appConfig?.Clients != null)
                {
                    foreach (var client in appConfig.Clients)
                        TcpConnections.Add(client.ClientName);
                }

                // 如果AppSettingService无数据，回退到ClientManagerService（仅Client模式）
                if (TcpConnections.Count == 0 && _tcpClientManagerService?.Clients != null)
                {
                    foreach (var name in _tcpClientManagerService.Clients.Keys)
                        TcpConnections.Add(name);
                }

                // 默认选择第一个连接
                if (TcpConnections.Count > 0 && string.IsNullOrEmpty(SelectedConnectionName))
                    SelectedConnectionName = TcpConnections[0];

                _logger?.Info($"Z-SCAN TCP连接列表已加载: {TcpConnections.Count} 个");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Z-SCAN 加载TCP连接列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 订阅TCP数据接收事件：3D相机通过IO触发后被动回传数据
        /// 扫描进行中时不过滤连接名，确保任何来源的相机数据都能被接收并传递给 TCS
        /// 非扫描期间仍按 SelectedConnectionName 过滤
        /// </summary>
        private void SubscribeCameraData()
        {
            // 先取消旧订阅（防止重复订阅）
            UnsubscribeCameraData();

            _cameraDataHandler = (cameraName, message) =>
            {
                // 快速检查：是否为3D相机数据（避免处理无关消息）
                if (!message.Contains("VISION_RESULT:SUCCESS"))
                    return;

                // 扫描进行中：优先通过 TCS 传递数据，不过滤连接名
                // （解决数据比运动序列先到达时因连接名不匹配被静默丢弃的问题）
                var tcs = _scanDataTcs;
                var isScanActive = IsScanning && tcs != null && !tcs.Task.IsCompleted;

                if (isScanActive)
                {
                    // 扫描中：解析数据并直接设置 TCS（在 TCP 线程上快速完成，不依赖 UI 线程）
                    var parsedValues = ParseCameraData(message);
                    if (parsedValues.Count > 0)
                    {
                        tcs.TrySetResult(parsedValues);
                        _logger?.Info($"Z-SCAN 扫描数据已接收（来源={cameraName}，{parsedValues.Count}个点），TCS 已设置");
                    }
                    return;
                }

                // 非扫描期间：按连接名过滤，直接更新表格
                if (!string.IsNullOrEmpty(SelectedConnectionName) && cameraName == SelectedConnectionName)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            _logger?.Info($"Z-SCAN 收到相机数据 [{cameraName}]: {message}");

                            var parsedValues = ParseCameraData(message);
                            if (parsedValues.Count > 0)
                            {
                                UpdatePointDetailsFromCameraData(parsedValues);
                                RecalculateStatistics();
                                ShowHintMessage($"接收到{parsedValues.Count}个测量点数据，表格已更新");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.Error($"Z-SCAN 相机数据处理失败: {ex.Message}");
                        }
                    });
                }
                else if (!string.IsNullOrEmpty(cameraName))
                {
                    _logger?.Debug($"Z-SCAN 相机数据被连接名过滤忽略: 来源={cameraName}, 期望={SelectedConnectionName}");
                }
            };

            _tcpEventService.CameraMessageReceived += _cameraDataHandler;
            _logger?.Info($"Z-SCAN 已订阅TCP数据接收: 监听连接 '{SelectedConnectionName}'");
        }

        /// <summary>
        /// 取消订阅TCP数据接收事件，防止内存泄漏
        /// 必须在OnDialogClosed()中调用
        /// </summary>
        private void UnsubscribeCameraData()
        {
            if (_cameraDataHandler != null)
            {
                _tcpEventService.CameraMessageReceived -= _cameraDataHandler;
                _cameraDataHandler = null;
                _logger?.Info("Z-SCAN 已取消订阅TCP数据接收");
            }
        }

        /// <summary>
        /// 解析相机原始数据字符串
        /// 数据格式示例：Camera=3DCAMERA;VISION_RESULT:SUCCESS:value1,value2,value3,...
        /// 提取VISION_RESULT:SUCCESS:后的数值数组
        /// </summary>
        /// <param name="rawData">相机返回的原始数据字符串</param>
        /// <returns>解析后的数值列表（按顺序对应各测量点）</returns>
        private List<double> ParseCameraData(string rawData)
        {
            var result = new List<double>();

            if (string.IsNullOrEmpty(rawData))
                return result;

            try
            {
                // 查找VISION_RESULT:SUCCESS:标记
                const string marker = "VISION_RESULT:SUCCESS:";
                int startIndex = rawData.IndexOf(marker);
                if (startIndex < 0)
                {
                    _logger?.Warn($"Z-SCAN 相机数据格式错误: 未找到'{marker}'标记");
                    return result;
                }

                // 提取数值部分
                string valuesStr = rawData.Substring(startIndex + marker.Length).Trim();

                // 分割数值（逗号分隔）
                string[] valueStrings = valuesStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var valStr in valueStrings)
                {
                    if (double.TryParse(valStr.Trim(), out double value))
                    {
                        result.Add(value);
                    }
                    else
                    {
                        _logger?.Warn($"Z-SCAN 无法解析数值: '{valStr.Trim()}'");
                    }
                }

                _logger?.Info($"Z-SCAN 相机数据解析成功: {result.Count} 个数值");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Z-SCAN 相机数据解析异常: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 根据相机数据更新PointDetails表格中的ZMeasured字段
        /// 按DataIndex（即PointNumber-1）匹配到对应行
        /// 并自动重新计算DeltaZ和Status
        /// </summary>
        /// <param name="measuredValues">从相机数据解析出的测量值列表</param>
        private void UpdatePointDetailsFromCameraData(List<double> measuredValues)
        {
            if (PointDetails == null || measuredValues.Count == 0)
                return;

            double[] arcHeights = measuredValues.ToArray();
            double totalOffset = _zscanCalibrationService.TotalZOffset;

            var pointDataList = PointDetails.Select(p => new ZScanPointData
            {
                Segment = p.Segment,
                PointNumber = p.PointNumber,
                X = p.X,
                Y = p.Y,
                ZNominal = p.ZNominal,
                ZMeasured = p.ZMeasured,
                DeltaZ = p.DeltaZ,
                Nominal = p.Nominal,
                Range = p.Range,
                DataIndex = p.DataIndex,
                Description = p.Description,
                Status = p.Status,
                PointType = p.PointType,
                GlobalVariableLink = p.GlobalVariableLink
            }).ToList();

            _zscanArcCompensationService.Compensate(pointDataList, arcHeights, totalOffset, CurrentDataFormat);

            _suppressLinkedGlobalVariableSync = true;
            try
            {
                for (int i = 0; i < Math.Min(pointDataList.Count, PointDetails.Count); i++)
                {
                    var point = PointDetails[i];
                    var data = pointDataList[i];
                    point.ZMeasured = data.ZMeasured;
                    point.DeltaZ = data.DeltaZ;
                    RecalculateRow(point);
                    _logger?.Debug($"Z-SCAN 更新点[{point.PointNumber}]: ZMeasured={point.ZMeasured:F3}, DeltaZ={point.DeltaZ:F3}");
                }
            }
            finally
            {
                _suppressLinkedGlobalVariableSync = false;
            }

            _logger?.Info($"Z-SCAN 已更新 {Math.Min(pointDataList.Count, PointDetails.Count)} 个测量点（含标定偏移={totalOffset:F3}，数据格式={CurrentDataFormat}，数据点数={measuredValues.Count}）");

            // 同步已链接的全局变量（DeltaZ 值回写）
            _ = SyncLinkedGlobalVariablesAsync();
        }

        #endregion

        #region 原有功能方法（保持不变）

        #region Task 9: 自动计算引擎

        /// <summary>
        /// 单行重新计算方法：当测量点的 ZMeasured、Nominal 或 Range 值变化时触发
        /// 计算逻辑：
        /// 1. DeltaZ = ZMeasured - Nominal（使用新的 Nominal 字段）
        /// 2. 根据 Range 判定 Status：
        ///    - Range > 0 且 |DeltaZ| <= Range → "Pass"
        ///    - Range > 0 且 |DeltaZ| > Range → "Fail"
        ///    - Range == 0 或 ZMeasured == 0 → "Pending"
        /// </summary>
        /// <param name="point">需要重新计算的测量点</param>
        private void RecalculateRow(ZScanPointDetail point)
        {
            if (point == null) return;

            // 计算 DeltaZ = 实测值 - 标称值（使用新 Nominal 字段）
            point.DeltaZ = point.ZNominal - point.ZMeasured;

            // 判定状态：根据公差范围判断合格性
            if (point.Range > 0 && point.ZMeasured != 0)
            {
                // 有公差范围且有实测值时才判定 Pass/Fail
                if (Math.Abs(point.DeltaZ) <= point.Range)
                {
                    point.Status = "Pass";
                }
                else
                {
                    point.Status = "Fail";
                }
            }
            else
            {
                // 无公差范围或无实测值时标记为待检测
                point.Status = "Pending";
            }

            // 触发属性变更通知，确保 UI 更新
            point.NotifyPropertyChanged(nameof(ZScanPointDetail.DeltaZ));
            point.NotifyPropertyChanged(nameof(ZScanPointDetail.Status));

            _logger?.Debug($"Z-SCAN 行重算完成: 点[{point.PointNumber}] DeltaZ={point.DeltaZ:F3}, Status={point.Status}");
        }

        /// <summary>
        /// 全局统计重新计算方法：更新所有统计信息和状态显示
        /// 统计项包括：
        /// 1. TotalPoints：总测量点数
        /// 2. ZNominalRange：标称值范围（格式："min – max mm"）
        /// 3. ZMaxDelta：最大偏差绝对值
        /// 4. StatusText / StatusColor：整体状态判定
        ///    - ZMaxDelta <= 0.05 → "All Pass" (Green)
        ///    - 0.05 < ZMaxDelta <= 0.1 → "Warning" (Orange)
        ///    - ZMaxDelta > 0.1 或有 Fail 行 → "High ΔZ" (Red)
        /// 5. Pass/Fail/Pending 数量统计
        /// </summary>
        private void RecalculateStatistics()
        {
            if (PointDetails == null || PointDetails.Count == 0)
            {
                TotalPoints = 0;
                ZNominalRange = "N/A";
                ZMaxDelta = 0;
                StatusText = "No Data";
                StatusColor = Brushes.Gray;
                return;
            }

            // 更新总点数
            TotalPoints = PointDetails.Count;

            // 计算 Nominal 最小值和最大值（用于显示范围）
            double minNominal = PointDetails.Min(p => p.Nominal);
            double maxNominal = PointDetails.Max(p => p.Nominal);
            ZNominalRange = $"{minNominal:F3} – {maxNominal:F3} mm";

            // 计算最大 DeltaZ 绝对值
            ZMaxDelta = PointDetails.Max(p => Math.Abs(p.DeltaZ));

            // 统计各状态数量
            int passCount = PointDetails.Count(p => p.Status == "Pass");
            int failCount = PointDetails.Count(p => p.Status == "Fail");
            int pendingCount = PointDetails.Count(p => p.Status == "Pending");

            // 根据最大偏差和 Fail 数量更新整体状态
            bool hasFailRows = failCount > 0;

            if (hasFailRows || ZMaxDelta > 0.1)
            {
                // 存在不合格行或偏差过大
                StatusText = $"High ΔZ ({failCount} Fail)";
                StatusColor = Brushes.Red;
            }
            else if (ZMaxDelta > 0.05)
            {
                // 偏差在警告范围内
                StatusText = "Warning";
                StatusColor = Brushes.Orange;
            }
            else
            {
                // 所有检测点合格
                StatusText = "All Pass";
                StatusColor = Brushes.Green;
            }

            _logger?.Info($"Z-SCAN 统计更新: 总点={TotalPoints}, Z范围={ZNominalRange}, 最大ΔZ={ZMaxDelta:F3}, 状态={StatusText} (Pass={passCount}, Fail={failCount}, Pending={pendingCount})");
        }

        /// <summary>
        /// 注册 PointDetails 集合的事件监听：
        /// 1. CollectionChanged 事件：监听集合增删操作
        /// 2. 每个元素的 PropertyChanged 事件：监听 ZMeasured、Nominal、Range 字段变化
        /// 在构造函数或 OnDialogOpened() 中调用
        /// </summary>
        private void SubscribePointDetailsEvents()
        {
            // 先取消旧订阅（防止重复订阅）
            UnsubscribePointDetailsEvents();

            // 监听集合变更事件（新增/删除行）
            _pointDetailsCollectionChangedHandler = (sender, e) =>
            {
                // 当集合变更时，重新计算全局统计
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        RecalculateStatistics();

                        // 如果是新增元素，订阅该元素的属性变更事件
                        if (e.Action == NotifyCollectionChangedAction.Add)
                        {
                            foreach (ZScanPointDetail newItem in e.NewItems)
                            {
                                SubscribePointPropertyChanges(newItem);
                            }
                        }

                    // 如果是删除元素，取消订阅（可选，因为对象会被GC回收）
                        if (e.Action == NotifyCollectionChangedAction.Remove)
                        {
                            foreach (ZScanPointDetail oldItem in e.OldItems)
                            {
                                UnsubscribePointPropertyChanges(oldItem);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error($"Z-SCAN 集合变更事件处理异常: {ex.Message}");
                    }
                });
            };

            // 监听单个测量点的属性变更事件
            _pointPropertyChangedHandler = (sender, e) =>
            {
                try
                {
                    var point = sender as ZScanPointDetail;
                    if (point == null) return;

                    // 链接全局变量或 ΔZ 变化时，回写全局变量
                    if (e.PropertyName == nameof(ZScanPointDetail.LinkedGlobalVarName))
                    {
                        if (!string.IsNullOrEmpty(point.LinkedGlobalVarName))
                            _ = SyncLinkedGlobalVariablesAsync();
                        return;
                    }

                    if (e.PropertyName == nameof(ZScanPointDetail.DeltaZ))
                    {
                        if (!_suppressLinkedGlobalVariableSync && !string.IsNullOrEmpty(point.LinkedGlobalVarName))
                            _ = SyncLinkedGlobalVariablesAsync();
                        return;
                    }

                    // 只关注关键字段变化：ZMeasured、Nominal、Range
                    if (e.PropertyName == nameof(ZScanPointDetail.ZMeasured) ||
                        e.PropertyName == nameof(ZScanPointDetail.Nominal) ||
                        e.PropertyName == nameof(ZScanPointDetail.Range))
                    {
                        // 重新计算该行并更新统计（使用 Dispatcher 延迟执行避免循环）
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                RecalculateRow(point);
                                RecalculateStatistics();
                            }
                            catch (Exception innerEx)
                            {
                                _logger?.Error($"Z-SCAN 属性变更计算异常: {innerEx.Message}");
                            }
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Error($"Z-SCAN 属性变更事件处理异常: {ex.Message}");
                }
            };

            // 注册集合变更事件
            if (PointDetails != null)
            {
                PointDetails.CollectionChanged += _pointDetailsCollectionChangedHandler;

                // 为现有每个元素注册属性变更事件
                foreach (var point in PointDetails)
                {
                    SubscribePointPropertyChanges(point);
                }
            }

            _logger?.Debug("Z-SCAN 已注册 PointDetails 事件监听");
        }

        /// <summary>
        /// 订阅单个测量点的属性变更事件
        /// </summary>
        /// <param name="point">要监听的测量点</param>
        private void SubscribePointPropertyChanges(ZScanPointDetail point)
        {
            if (point != null && _pointPropertyChangedHandler != null)
            {
                point.PropertyChanged += _pointPropertyChangedHandler;
            }
        }

        /// <summary>
        /// 取消订阅单个测量点的属性变更事件
        /// </summary>
        /// <param name="point">要取消监听的测量点</param>
        private void UnsubscribePointPropertyChanges(ZScanPointDetail point)
        {
            if (point != null && _pointPropertyChangedHandler != null)
            {
                point.PropertyChanged -= _pointPropertyChangedHandler;
            }
        }

        /// <summary>
        /// 取消所有 PointDetails 相关的事件订阅，防止内存泄漏
        /// 必须在 OnDialogClosed() 中调用
        /// </summary>
        private void UnsubscribePointDetailsEvents()
        {
            // 取消集合变更事件订阅
            if (PointDetails != null && _pointDetailsCollectionChangedHandler != null)
            {
                PointDetails.CollectionChanged -= _pointDetailsCollectionChangedHandler;
            }

            // 取消所有现有元素的属性变更事件订阅
            if (PointDetails != null && _pointPropertyChangedHandler != null)
            {
                foreach (var point in PointDetails)
                {
                    UnsubscribePointPropertyChanges(point);
                }
            }

            _pointDetailsCollectionChangedHandler = null;
            _pointPropertyChangedHandler = null;

            _logger?.Debug("Z-SCAN 已取消 PointDetails 事件监听");
        }

        #endregion

        private void OnAddRow()
        {
            try
            {
                if (PointDetails == null)
                {
                    _logger?.Warn("Z-SCAN PointDetails 为空，无法添加行");
                    return;
                }

                int nextPt = PointDetails.Count + 1;
                var newPoint = new ZScanPointDetail
                {
                    Segment = 1,
                    PointNumber = nextPt,
                    X = 0,
                    Y = 0,
                    ZNominal = 0,
                    ZMeasured = 0,
                    DeltaZ = 0,
                    FeatureName = "other",
                    Nominal = 0,
                    Range = 0.100, // 默认公差范围
                    DataIndex = nextPt - 1,
                    Description = $"Point {nextPt}"
                };

                // 添加新行（会触发 CollectionChanged 事件）
                PointDetails.Add(newPoint);

                _logger?.Info($"Z-SCAN 已添加新行: 点号={nextPt}, 总点数={PointDetails.Count}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Z-SCAN 添加行失败: {ex.Message}\n{ex.StackTrace}");
                ShowHintMessage($"添加行失败: {ex.Message}");
            }
        }

        private void OnDeleteSelectedRow()
        {
            if (SelectedPointDetail != null)
            {
                PointDetails.Remove(SelectedPointDetail);
                TotalPoints = PointDetails.Count;
                RecalculateStatistics();
            }
        }

        /// <summary>
        /// 从CSV文件导入测量点数据：
        /// 支持格式：
        /// - 新版完整格式（12个字段）：Segment,PointNumber,X,Y,ZNominal,ZMeasured,DeltaZ,Description,Nominal,Range,DataIndex,Status
        /// - 旧版兼容格式（8个字段）：Segment,PointNumber,X,Y,ZNominal,ZMeasured,DeltaZ,FeatureName
        /// - 自动检测标题行（第一行为非数字时跳过）
        ///
        /// 导入完成后：
        /// 1. 替换当前 PointDetails 集合
        /// 2. 对每一行调用 RecalculateRow() 确保数据一致性
        /// 3. 调用 RecalculateStatistics() 更新统计信息
        /// </summary>
        private void OnImportCSV()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                Title = "导入 Z-SCAN 测量数据"
            };

            if (openFileDialog.ShowDialog() != true)
                return;

            try
            {
                // 读取所有行（使用 UTF-8 编码，支持中文）
                var lines = File.ReadAllLines(openFileDialog.FileName, System.Text.Encoding.UTF8);

                if (lines.Length == 0)
                {
                    ShowHintMessage("文件为空，无法导入数据。");
                    return;
                }

                var newPoints = new ObservableCollection<ZScanPointDetail>();
                int startLine = 0;
                int warningCount = 0;

                // 自动检测标题行：如果第一行第一个字段不是数字，则认为是标题行
                if (lines.Length > 0)
                {
                    var firstField = lines[0].Split(',')[0].Trim();
                    if (!int.TryParse(firstField, out _))
                    {
                        startLine = 1; // 跳过标题行
                        _logger?.Info($"Z-SCAN 检测到CSV标题行，将从第2行开始解析数据");
                    }
                }

                // 解析数据行
                for (int i = startLine; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue; // 跳过空行

                    var parts = line.Split(',');

                    try
                    {
                        // 支持两种格式：新版12字段 或 旧版8字段
                        if (parts.Length >= 8)
                        {
                            var point = new ZScanPointDetail();

                            // 解析必填字段（前7个字段 + FeatureName/Description）
                            point.Segment = int.TryParse(parts[0].Trim(), out int seg) ? seg : 1;
                            point.PointNumber = int.TryParse(parts[1].Trim(), out int ptNum) ? ptNum : (i - startLine + 1);
                            point.X = double.TryParse(parts[2].Trim(), out double x) ? x : 0;
                            point.Y = double.TryParse(parts[3].Trim(), out double y) ? y : 0;
                            point.ZNominal = double.TryParse(parts[4].Trim(), out double zNom) ? zNom : 0;
                            point.ZMeasured = double.TryParse(parts[5].Trim(), out double zMea) ? zMea : 0;
                            point.DeltaZ = double.TryParse(parts[6].Trim(), out double deltaZ) ? deltaZ : 0;

                            // 判断是新版还是旧版格式
                            if (parts.Length >= 12)
                            {
                                // 新版完整格式（12字段）
                                point.Description = parts[7].Trim();
                                point.Nominal = double.TryParse(parts[8].Trim(), out double nominal) ? nominal : point.ZNominal;
                                point.Range = double.TryParse(parts[9].Trim(), out double range) ? range : 0.100;
                                point.DataIndex = int.TryParse(parts[10].Trim(), out int dIdx) ? dIdx : point.PointNumber - 1;
                                point.Status = parts.Length > 11 ? parts[11].Trim() : "Pending";
                                point.FeatureName = point.Description; // 同步到旧字段保持兼容性
                            }
                            else
                            {
                                // 旧版兼容格式（8字段）
                                point.FeatureName = parts[7].Trim();
                                point.Description = parts[7].Trim(); // 同步到新字段
                                point.Nominal = point.ZNominal; // 从 ZNominal 复制
                                point.Range = 0.100; // 默认公差范围
                                point.DataIndex = point.PointNumber - 1; // 默认索引
                                point.Status = "Pending"; // 待重新计算
                            }

                            newPoints.Add(point);
                        }
                        else
                        {
                            // 字段不足，记录警告并跳过
                            _logger?.Warn($"Z-SCAN CSV 第{i + 1}行字段数不足（{parts.Length}个），已跳过");
                            warningCount++;
                        }
                    }
                    catch (Exception parseEx)
                    {
                        // 单行解析失败不影响其他行
                        _logger?.Warn($"Z-SCAN CSV 第{i + 1}行解析失败: {parseEx.Message}");
                        warningCount++;
                    }
                }

                // 检查是否有有效数据
                if (newPoints.Count == 0)
                {
                    ShowHintMessage($"文件中未找到有效的数据行。\n警告: {warningCount} 行解析失败。");
                    return;
                }

                // 使用 Clear() + Add() 保持同一集合引用
                // 避免替换集合导致事件订阅丢失（CollectionChanged 和 PropertyChanged 监听仍有效）
                PointDetails.Clear();
                foreach (var point in newPoints)
                {
                    PointDetails.Add(point);  // 触发 CollectionChanged.Add 事件，自动订阅新元素的 PropertyChanged
                }

                // Task 9: 对每一行重新计算确保数据一致性（Add 时已触发事件，此处确保最终一致性）
                _suppressLinkedGlobalVariableSync = true;
                try
                {
                    foreach (var point in PointDetails)
                        RecalculateRow(point);
                }
                finally
                {
                    _suppressLinkedGlobalVariableSync = false;
                }

                // 更新统计信息（Clear+Add 已触发 CollectionChanged，此处确保最终一致性）
                RecalculateStatistics();
                _ = SyncLinkedGlobalVariablesAsync();

                // 显示结果消息
                string message = $"CSV 导入成功！\n" +
                               $"成功导入: {newPoints.Count} 行数据\n";
                if (warningCount > 0)
                {
                    message += $"警告: {warningCount} 行因格式错误被跳过\n";
                }
                message += $"\n文件: {Path.GetFileName(openFileDialog.FileName)}";

                ShowHintMessage(message);
                _logger?.Info($"Z-SCAN CSV 导入完成: {newPoints.Count} 行成功, {warningCount} 行警告");
            }
            catch (FileNotFoundException ex)
            {
                ShowHintMessage($"文件未找到: {ex.Message}");
                _logger?.Error($"Z-SCAN CSV 文件未找到: {ex.Message}");
            }
            catch (IOException ex)
            {
                ShowHintMessage($"文件读取错误: {ex.Message}\n请检查文件是否被其他程序占用。");
                _logger?.Error($"Z-SCAN CSV 文件读取错误: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowHintMessage($"导入失败: {ex.Message}");
                _logger?.Error($"Z-SCAN CSV 导入异常: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 导出测量点数据到CSV文件：
        /// - 导出所有字段（包括新增的 Description, Nominal, Range, DataIndex, Status）
        /// - 第一行为标题行
        /// - 数值格式统一为 F3（3位小数）
        /// - 使用 UTF-8 BOM 编码确保中文正常显示（Excel 兼容）
        /// - 默认文件名：ZScan_Data.csv
        /// </summary>
        private void OnExportCSV()
        {
            // 检查是否有数据可导出
            if (PointDetails == null || PointDetails.Count == 0)
            {
                ShowHintMessage("当前没有数据可导出，请先添加或导入测量点数据。");
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                DefaultExt = ".csv",
                FileName = "ZScan_Data.csv",
                Title = "导出 Z-SCAN 测量数据"
            };

            if (saveFileDialog.ShowDialog() != true)
                return;

            try
            {
                // 使用 UTF-8 BOM 编码（Excel 可以正确识别中文）
                var utf8WithBom = new System.Text.UTF8Encoding(true);

                using (var writer = new StreamWriter(saveFileDialog.FileName, false, utf8WithBom))
                {
                    // 写入标题行（12个字段）
                    writer.WriteLine("Segment,PointNumber,X,Y,ZNominal,ZMeasured,DeltaZ,Description,Nominal,Range,DataIndex,Status");

                    // 写入数据行（数值格式统一为 F3）
                    foreach (var p in PointDetails)
                    {
                        writer.WriteLine(
                            $"{p.Segment}," +
                            $"{p.PointNumber}," +
                            $"{p.X:F3}," +
                            $"{p.Y:F3}," +
                            $"{p.ZNominal:F3}," +
                            $"{p.ZMeasured:F3}," +
                            $"{p.DeltaZ:F3}," +
                            $"{EscapeCsvField(p.Description)}," +
                            $"{p.Nominal:F3}," +
                            $"{p.Range:F3}," +
                            $"{p.DataIndex}," +
                            $"{p.Status}"
                        );
                    }
                }

                // 显示成功消息
                string message = $"CSV 导出成功！\n" +
                               $"文件路径: {saveFileDialog.FileName}\n" +
                               $"导出行数: {PointDetails.Count} 行\n" +
                               $"编码格式: UTF-8 BOM（兼容 Excel 中文显示）";

                ShowHintMessage(message);
                _logger?.Info($"Z-SCAN CSV 导出完成: {PointDetails.Count} 行 → {saveFileDialog.FileName}");
            }
            catch (UnauthorizedAccessException ex)
            {
                ShowHintMessage($"没有写入权限: {ex.Message}\n请检查文件夹权限或选择其他位置。");
                _logger?.Error($"Z-SCAN CSV 导出权限错误: {ex.Message}");
            }
            catch (IOException ex)
            {
                ShowHintMessage($"文件写入错误: {ex.Message}\n请检查文件是否被其他程序打开。");
                _logger?.Error($"Z-SCAN CSV 文件写入错误: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowHintMessage($"导出失败: {ex.Message}");
                _logger?.Error($"Z-SCAN CSV 导出异常: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// CSV 字段转义处理：如果字段包含逗号、引号或换行符，用双引号包裹并将内部引号加倍
        /// 确保包含中文描述的字段能正确导出
        /// </summary>
        /// <param name="field">原始字段值</param>
        /// <returns>转义后的安全字符串</returns>
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;

            // 如果包含特殊字符，需要用引号包裹
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }

            return field;
        }

        private void OnRescan()
        {
            ShowHintMessage("正在重新扫描...");
            LoadSampleData();
        }

        #endregion

        #region 标定功能实现

        private void OnCalibrateCameraZ()
        {
            try
            {
                double measuredZ = _zInitPosition;
                double referenceZ = ZInitPosition;
                _zscanCalibrationService.CalibrateCameraZ(measuredZ, referenceZ);
                UpdateCalibrationDisplay();
                _logger?.Info($"Z-SCAN [Dz{_currentNeedleIndex + 1}] 相机Z标定完成: CameraZOffset={_zscanCalibrationService.CameraZOffset:F3}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Z-SCAN [Dz{_currentNeedleIndex + 1}] 相机Z标定失败: {ex.Message}");
            }
        }

        private void OnApplyNeedleCompensation()
        {
            try
            {
                _zscanCalibrationService.ApplyNeedleCompensation(NeedleCompensationInput);
                UpdateCalibrationDisplay();
                _logger?.Info($"Z-SCAN [Dz{_currentNeedleIndex + 1}] 换针补偿已应用: NeedleZOffset={_zscanCalibrationService.NeedleZOffset:F3}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Z-SCAN [Dz{_currentNeedleIndex + 1}] 换针补偿应用失败: {ex.Message}");
            }
        }

        private void OnResetCalibration()
        {
            try
            {
                _zscanCalibrationService.ResetCalibration();
                CalibrationStep = 0;
                BaseZInput = 0;
                MeasuredMZ = 0;
                DeltaZInput = 0;
                NeedleCompensationValue = 0;
                CalculatedDispenseHeight = 0;
                CurrentZHeightInput = 0;
                ZHeightDifference = 0;
                BaseDispenseHeight = 0;
                NeedleZOffset = 0;
                _logger?.Info($"Z-SCAN [Dz{_currentNeedleIndex + 1}] 标定已重置");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Z-SCAN [Dz{_currentNeedleIndex + 1}] 标定重置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 将基准Z同步到标定服务 Current.BaseZ
        /// </summary>
        private void SyncBaseZToService(double value, bool advanceStep = true, bool logChange = true)
        {
            try
            {
                _zscanCalibrationService.SetBaseZ(value);
                if (advanceStep && value > 0 && CalibrationStep < 1)
                    CalibrationStep = 1;
                if (logChange)
                    _logger?.Info($"Z-SCAN [Dz{_currentNeedleIndex + 1}] 基准Z高度: {value:F3}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Z-SCAN [Dz{_currentNeedleIndex + 1}] 设置基准Z失败: {ex.Message}");
            }
        }

        private async Task OnMoveNeedleToBaseZAsync()
        {
            try
            {
                int zAxis = ResolveCurrentZAxisId();
                await _needleTeachService.MoveNeedleToBaseZAsync(zAxis, BaseZInput, MoveSpeed);
                CalibrationStep = 2;
                _logger?.Info($"Z-SCAN [Dz{_currentNeedleIndex + 1}] 针头已移动到基准Z高度: {BaseZInput:F3}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Z-SCAN [Dz{_currentNeedleIndex + 1}] 移动针头到基准Z失败: {ex.Message}");
                ShowHintMessage($"移动针头到基准Z失败: {ex.Message}");
            }
        }

        private async Task OnTeachNeedleMZAsync()
        {
            try
            {
                int zAxis = ResolveCurrentZAxisId();
                double mz = await _needleTeachService.TeachCurrentPositionAsync(zAxis);
                MeasuredMZ = mz;
                _zscanCalibrationService.TeachNeedleMZ(mz);
                CalibrationStep = 3;
                _logger?.Info($"Z-SCAN [Dz{_currentNeedleIndex + 1}] 针头示教MZ: {mz:F3}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Z-SCAN [Dz{_currentNeedleIndex + 1}] 针头示教失败: {ex.Message}");
                ShowHintMessage($"针头示教失败: {ex.Message}");
            }
        }

        private void OnCalculateDispenseHeight()
        {
            try
            {
                // 计算前确保 UI 输入已同步到标定服务（避免 Current.BaseZ 仍为 0）
                SyncBaseZToService(BaseZInput, advanceStep: false, logChange: false);
                ZHeightDifference = _zscanCalibrationService.CalculateZHeightDifference(BaseZInput, CurrentZHeightInput);
                CalculatedDispenseHeight = _zscanCalibrationService.CalculateDispenseHeight(
                    BaseZInput, MeasuredMZ, CurrentZHeightInput, NeedleCompensationValue);
                CalibrationStep = 4;
                _logger?.Info($"Z-SCAN [Dz{_currentNeedleIndex + 1}] Step4: 基准Z={BaseZInput:F3}, 当前Z={CurrentZHeightInput:F3}, Z高度差={ZHeightDifference:F3}, 基准点胶高度(MZ)={MeasuredMZ:F3}, 补偿={NeedleCompensationValue:F3}, 点胶高度={CalculatedDispenseHeight:F3}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Z-SCAN [Dz{_currentNeedleIndex + 1}] 计算点胶高度失败: {ex.Message}");
            }
        }

        private void OnCalibrationChanged()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateCalibrationDisplay();
            }));
        }

        private void UpdateCalibrationDisplay()
        {
            NeedleZOffset = _zscanCalibrationService.NeedleZOffset;
            LastCalibrationTimeText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        #endregion

        #region 多表格管理实现

        private void OnAddTable()
        {
            try
            {
                int tableNum = Tables.Count + 1;
                var newTable = new ZScanTableConfig
                {
                    TableName = $"Table{tableNum}",
                    DataFormat = ZScanDataFormat.Double,
                    Calibration = new ZScanCalibrationConfig()
                };
                Tables.Add(newTable);
                SelectedTable = newTable;
                _logger?.Info($"Z-SCAN 新建表格: {newTable.TableName}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Z-SCAN 新建表格失败: {ex.Message}");
            }
        }

        private void OnDeleteTable()
        {
            try
            {
                if (SelectedTable == null) return;
                string name = SelectedTable.TableName;
                Tables.Remove(SelectedTable);
                if (Tables.Count > 0)
                    SelectedTable = Tables[0];
                _logger?.Info($"Z-SCAN 删除表格: {name}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Z-SCAN 删除表格失败: {ex.Message}");
            }
        }

        private ZScanTableConfig _previousTable;

        private void OnSelectedTableChanged()
        {
            if (_previousTable != null && PointDetails != null)
            {
                SyncPointDetailsToTable(_previousTable);
            }

            if (SelectedTable == null) return;

            CurrentDataFormat = SelectedTable.DataFormat;

            if (SelectedTable.Points != null && SelectedTable.Points.Count > 0)
            {
                PointDetails = new ObservableCollection<ZScanPointDetail>(
                    SelectedTable.Points.Select(p => new ZScanPointDetail
                    {
                        Segment = p.Segment,
                        PointNumber = p.PointNumber,
                        X = p.X,
                        Y = p.Y,
                        ZNominal = p.ZNominal,
                        ZMeasured = p.ZMeasured,
                        DeltaZ = p.DeltaZ,
                        Nominal = p.Nominal,
                        Range = p.Range,
                        DataIndex = p.DataIndex,
                        Description = p.Description,
                        Status = p.Status,
                        PointType = p.PointType,
                        LinkedGlobalVarName = p.GlobalVariableLink?.IsLinked == true
                            ? p.GlobalVariableLink.VariableName : null
                    }));
            }
            else
            {
                PointDetails = new ObservableCollection<ZScanPointDetail>();
            }

            // Z 标定参数为针头级，切换表格不加载/覆盖标定

            _previousTable = SelectedTable;
            SubscribePointDetailsEvents();
            RecalculateStatistics();
            _logger?.Info($"Z-SCAN 切换表格: {SelectedTable.TableName}");
        }

        private void OnDataFormatChanged()
        {
            if (SelectedTable != null)
            {
                SelectedTable.DataFormat = CurrentDataFormat;
            }
        }

        /// <summary>
        /// 构建待保存的配置文件：共享测量表格 + 双针头独立 Z 标定
        /// </summary>
        private ZScanConfigFile BuildConfigFile()
        {
            SyncPointDetailsToTable(SelectedTable);
            SaveCurrentNeedleState();
            return new ZScanConfigFile
            {
                Needle1Calibration = CloneCalibration(_needles[0].Calibration),
                Needle2Calibration = CloneCalibration(_needles[1].Calibration),
                Tables = _tables?.ToList() ?? new List<ZScanTableConfig>(),
                DefaultTableName = SelectedTable?.TableName ?? string.Empty,
                CommunicationType = SelectedCommunicationType,
                ConnectionName = SelectedConnectionName ?? string.Empty
            };
        }

        /// <summary> 深拷贝标定配置（避免序列化时引用共享） </summary>
        private static ZScanCalibrationConfig CloneCalibration(ZScanCalibrationConfig source)
        {
            if (source == null) return new ZScanCalibrationConfig();
            return new ZScanCalibrationConfig
            {
                ConfigName = source.ConfigName,
                CameraZOffset = source.CameraZOffset,
                NeedleZOffset = source.NeedleZOffset,
                LastCalibrationTime = source.LastCalibrationTime,
                Operator = source.Operator,
                BaseZ = source.BaseZ,
                MeasuredMZ = source.MeasuredMZ,
                DeltaZ = source.DeltaZ,
                CurrentZHeight = source.CurrentZHeight,
                ZHeightDifference = source.ZHeightDifference,
                BaseDispenseHeight = source.BaseDispenseHeight,
                DispenseHeight = source.DispenseHeight,
                NeedleCompensationLink = source.NeedleCompensationLink
            };
        }

        /// <summary>
        /// 自动保存配置到 Config/ZScan 文件夹（ZScan_时间.json 格式）
        /// 保存针头级标定与各针头独立表格
        /// </summary>
        private void OnSaveConfig()
        {
            try
            {
                var configFile = BuildConfigFile();
                string savedPath = _zscanConfigService.SaveWithTimestamp(configFile);
                CurrentFilePath = Path.GetFileName(savedPath);

                // 保存配置后同步已链接全局变量
                _ = SyncLinkedGlobalVariablesAsync();

                _logger?.Info($"Z-SCAN 配置已自动保存: {savedPath}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Z-SCAN 配置保存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 自动保存配置（异步包装，供扫描完成后调用）
        /// </summary>
        private async Task AutoSaveZScanConfigAsync()
        {
            await Task.Run(() =>
            {
                var configFile = BuildConfigFile();
                string savedPath = _zscanConfigService.SaveWithTimestamp(configFile);
                CurrentFilePath = Path.GetFileName(savedPath);
                _logger?.Info($"Z-SCAN 扫描后自动保存: {savedPath}");
            });
        }

        /// <summary>
        /// 加载配置：打开文件选择对话框，默认指向 Config/ZScan 文件夹
        /// 初始化时自动加载最新文件（不弹对话框）
        /// </summary>
        /// <param name="showDialog">是否弹出文件选择对话框（初始化时为 false）</param>
        private void OnLoadConfig(bool showDialog = true)
        {
            try
            {
                ZScanConfigFile configFile = null;
                string configDir = _zscanConfigService.GetConfigPath();

                if (showDialog)
                {
                    // 弹出文件选择对话框，默认指向 ZScan 文件夹
                    var localizer = _containerProvider?.Resolve<ILocalizationService>();
                    var dialogTitle = localizer?.GetResourceOrDefault("ZScanDetail_LoadConfigDialogTitle", "Z-SCAN Load Config") ?? "Z-SCAN Load Config";
                    var dialog = new OpenFileDialog
                    {
                        Title = dialogTitle,
                        Filter = "ZScan Config (*.json)|ZScan_*.json|All files (*.*)|*.*",
                        InitialDirectory = Directory.Exists(configDir) ? configDir : AppDomain.CurrentDomain.BaseDirectory
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        configFile = _zscanConfigService.LoadFromFile(dialog.FileName);
                        _logger?.Info($"Z-SCAN 用户选择加载配置: {dialog.FileName}");
                    }
                }
                else
                {
                    // 初始化时自动查找最新文件
                    var latestFile = FindLatestZScanFile();
                    if (!string.IsNullOrEmpty(latestFile))
                    {
                        configFile = _zscanConfigService.LoadFromFile(latestFile);
                        _logger?.Info($"Z-SCAN 自动加载最新配置: {latestFile}");
                    }
                    else
                    {
                        configFile = _zscanConfigService.LoadLastFromRecipePool();
                    }
                }

                if (configFile != null)
                {
                    ApplyConfigFile(configFile);
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Z-SCAN 配置加载失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从 Config/ZScan/ 目录查找最新的 ZScan_*.json 文件（按文件名排序，取最新）
        /// </summary>
        /// <returns>最新文件路径，未找到返回 null</returns>
        private string FindLatestZScanFile()
        {
            try
            {
                string configDir = _zscanConfigService.GetConfigPath();
                if (!Directory.Exists(configDir)) return null;

                var zscanFiles = Directory.GetFiles(configDir, "ZScan_*.json")
                    .OrderByDescending(f => f)
                    .FirstOrDefault();

                return zscanFiles;
            }
            catch (Exception ex)
            {
                _logger?.Warn($"Z-SCAN 查找最新文件失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 将配置文件应用到 ViewModel：共享表格 + 双针头标定
        /// </summary>
        private void ApplyConfigFile(ZScanConfigFile configFile)
        {
            LoadSharedTables(configFile);

            _needles[0].Calibration = ResolveNeedleCalibration(configFile, 0);
            _needles[1].Calibration = ResolveNeedleCalibration(configFile, 1);

            if (!string.IsNullOrEmpty(configFile.CommunicationType))
                SelectedCommunicationType = configFile.CommunicationType;
            if (!string.IsNullOrEmpty(configFile.ConnectionName)
                && TcpConnections.Contains(configFile.ConnectionName))
                SelectedConnectionName = configFile.ConnectionName;

            _zscanCalibrationService.SetCurrentNeedle(_currentNeedleIndex);
            LoadCurrentNeedleState();

            CurrentFilePath = !string.IsNullOrEmpty(_zscanConfigService.LastSavedFilePath)
                ? Path.GetFileName(_zscanConfigService.LastSavedFilePath)
                : string.Empty;
            _logger?.Info($"Z-SCAN 配置已加载: 共享表格={_tables.Count}个, Z标定分针头独立");
        }

        /// <summary>
        /// 加载共享测量表格（不随针头切换）；向后兼容旧版 Needle1Tables/Needle2Tables
        /// </summary>
        private void LoadSharedTables(ZScanConfigFile configFile)
        {
            var tables = ResolveSharedTables(configFile);
            if (tables.Count == 0) return;

            _tables = new ObservableCollection<ZScanTableConfig>(tables);
            RaisePropertyChanged(nameof(Tables));

            var selectedName = !string.IsNullOrEmpty(configFile.DefaultTableName)
                ? configFile.DefaultTableName
                : configFile.Needle1SelectedTableName;
            _selectedTable = !string.IsNullOrEmpty(selectedName)
                ? tables.FirstOrDefault(t => t.TableName == selectedName) ?? tables[0]
                : tables[0];
            RaisePropertyChanged(nameof(SelectedTable));

            _previousTable = null;
            OnSelectedTableChanged();
        }

        /// <summary> 解析共享表格列表：优先 Tables，旧版回退 Needle1Tables </summary>
        private static List<ZScanTableConfig> ResolveSharedTables(ZScanConfigFile configFile)
        {
            if (configFile.Tables?.Count > 0) return configFile.Tables;
            if (configFile.Needle1Tables?.Count > 0) return configFile.Needle1Tables;
            if (configFile.Needle2Tables?.Count > 0) return configFile.Needle2Tables;
            return new List<ZScanTableConfig>();
        }

        /// <summary>
        /// 解析针头标定配置：优先针头级字段，旧版回退到该针头首表标定
        /// </summary>
        private static ZScanCalibrationConfig ResolveNeedleCalibration(ZScanConfigFile configFile, int needleIndex)
        {
            var direct = needleIndex == 0 ? configFile.Needle1Calibration : configFile.Needle2Calibration;
            if (direct != null && HasCalibrationData(direct))
                return CloneCalibration(direct);

            // 旧版分针头表格中的标定回退
            var legacyTables = needleIndex == 0 ? configFile.Needle1Tables : configFile.Needle2Tables;
            if (legacyTables?.Count > 0 && legacyTables[0].Calibration != null && HasCalibrationData(legacyTables[0].Calibration))
                return CloneCalibration(legacyTables[0].Calibration);

            if (needleIndex == 0)
            {
                var tables = ResolveSharedTables(configFile);
                if (tables.Count > 0 && tables[0].Calibration != null && HasCalibrationData(tables[0].Calibration))
                    return CloneCalibration(tables[0].Calibration);
            }

            return new ZScanCalibrationConfig();
        }

        /// <summary> 判断标定配置是否含有效数据（区分默认空对象与旧版未设置） </summary>
        private static bool HasCalibrationData(ZScanCalibrationConfig cal)
        {
            return cal.CameraZOffset != 0 || cal.NeedleZOffset != 0 || cal.BaseZ != 0
                || cal.MeasuredMZ != 0 || cal.DispenseHeight != 0 || cal.CurrentZHeight != 0;
        }

        private void SyncPointDetailsToTable()
        {
            SyncPointDetailsToTable(SelectedTable);
        }

        private void SyncPointDetailsToTable(ZScanTableConfig table)
        {
            if (table == null || PointDetails == null) return;
            table.Points = PointDetails.Select(p => new ZScanPointData
            {
                Segment = p.Segment,
                PointNumber = p.PointNumber,
                X = p.X,
                Y = p.Y,
                ZNominal = p.ZNominal,
                ZMeasured = p.ZMeasured,
                DeltaZ = p.DeltaZ,
                Nominal = p.Nominal,
                Range = p.Range,
                DataIndex = p.DataIndex,
                Description = p.Description,
                Status = p.Status,
                PointType = p.PointType,
                GlobalVariableLink = p.GlobalVariableLink
            }).ToList();
            table.DataFormat = CurrentDataFormat;
            // 测量点数据与针头级 Z 标定分离，不在表格中保存标定参数
        }

        #endregion

        #region 全局变量链接实现

        /// <summary>
        /// 同步已链接全局变量：将 DeltaZ 值回写到每行绑定的全局变量
        /// 遍历 PointDetails，对已设置 LinkedGlobalVarName 的行，
        /// 将当前 DeltaZ 值写入对应的全局变量并持久化
        /// </summary>
        private async Task SyncLinkedGlobalVariablesAsync()
        {
            if (_suppressLinkedGlobalVariableSync || PointDetails == null || PointDetails.Count == 0)
                return;

            try
            {
                var poolId = _recipePoolService.CurrentPoolName ?? "Default";
                var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);
                bool hasUpdate = false;

                foreach (var point in PointDetails)
                {
                    if (string.IsNullOrEmpty(point.LinkedGlobalVarName))
                        continue;

                    var gv = variables.FirstOrDefault(v => v.Name == point.LinkedGlobalVarName);
                    if (gv != null)
                    {
                        string newValue = point.DeltaZ.ToString("F6");
                        if (gv.Value != newValue)
                        {
                            gv.Value = newValue;
                            hasUpdate = true;
                            _logger?.Debug($"Z-SCAN 全局变量同步: {gv.Name} = {point.DeltaZ:F6} (行{point.PointNumber})");
                        }
                    }
                    else
                    {
                        _logger?.Warn($"Z-SCAN 全局变量链接未找到: {point.LinkedGlobalVarName} (行{point.PointNumber})");
                    }
                }

                if (hasUpdate)
                {
                    await _recipePoolService.SaveGlobalVariablesAsync(poolId, variables);

                    // 发布全局变量变更事件，通知 GV 页面重新加载最新数据
                    _eventAggregator.GetEvent<GlobalVariablesChangedEvent>().Publish(poolId);

                    // 同步更新 AvailableGlobalVariables 集合，保持 UI 显示一致
                    foreach (var point in PointDetails.Where(p => !string.IsNullOrEmpty(p.LinkedGlobalVarName)))
                    {
                        var localGv = AvailableGlobalVariables.FirstOrDefault(v => v.Name == point.LinkedGlobalVarName);
                        if (localGv != null)
                            localGv.Value = point.DeltaZ.ToString("F6");
                    }
                    _logger?.Info($"Z-SCAN 已同步 {PointDetails.Count(p => !string.IsNullOrEmpty(p.LinkedGlobalVarName))} 个链接全局变量");
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Z-SCAN 同步全局变量失败: {ex.Message}");
            }
        }

        private void OnUnlinkRowGlobalVariable(ZScanPointDetail point)
        {
            if (point == null) return;
            string varName = point.LinkedGlobalVarName;
            point.LinkedGlobalVarName = null;
            _logger?.Info($"Z-SCAN 行{point.PointNumber}已取消全局变量链接: {varName}");
        }

        /// <summary>
        /// 从配方池重新加载可链接的全局变量列表（供下拉框绑定）
        /// </summary>
        private void LoadAvailableGlobalVariables()
        {
            try
            {
                var variables = _recipePoolService.LoadGlobalVariablesAsync(_recipePoolService.CurrentPoolName ?? "Default").GetAwaiter().GetResult();
                AvailableGlobalVariables.Clear();
                if (variables != null)
                {
                    foreach (var v in variables)
                        AvailableGlobalVariables.Add(v);
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Z-SCAN 加载全局变量列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 全局变量被外部更新时（全局变量页保存、配方池保存、其他步骤回写）刷新可链接列表
        /// </summary>
        private void OnGlobalVariablesChanged(string poolId)
        {
            if (poolId != _recipePoolService.CurrentPoolName && poolId != _recipePoolService.CurrentPoolId)
                return;

            LoadAvailableGlobalVariables();
            _logger?.Debug($"Z-SCAN 已同步全局变量列表（池={poolId}，共 {AvailableGlobalVariables.Count} 项）");
        }

        /// <summary> 配方池切换时重新加载全局变量 </summary>
        private void OnRecipePoolChanged(string poolName)
        {
            LoadAvailableGlobalVariables();
            _logger?.Debug($"Z-SCAN 配方池切换，已重新加载全局变量（池={poolName}）");
        }

        #endregion
    }
}
